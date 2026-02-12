using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Services.Features;

public sealed class FeatureGate : IFeatureGate
{
    private readonly IFeatureReadDac _readDac;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FeatureGate> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(FeatureFlagConstants.CacheTtlSeconds);

    // Static ConcurrentDictionary for per-key stampede protection.
    //
    // WHY NOT just use MemoryCache.GetOrCreateAsync?
    // In .NET 8, GetOrCreateAsync does NOT guarantee single-invocation of the factory.
    // Under concurrent load, N requests for the same cache key all miss simultaneously
    // and all invoke the factory — causing N redundant DB hits. This defeats caching
    // and can choke the DB connection pool under traffic spikes.
    // (.NET 9's HybridCache fixes this, but we're on .NET 8.)
    //
    // WHY static?
    // FeatureGate is registered as scoped (one instance per HTTP request) but IMemoryCache
    // is singleton (shared across all requests). Without static, each request's FeatureGate
    // would have its own lock dictionary — zero cross-request stampede protection.
    // The dictionary is bounded by unique cache keys (feature codes × clients × users).
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public FeatureGate(
        IFeatureReadDac readDac,
        IMemoryCache cache,
        ILogger<FeatureGate> logger)
    {
        _readDac = readDac;
        _cache = cache;
        _logger = logger;
    }

    public async Task<FeatureDecision> IsEnabledAsync(
        string featureCode, Guid clientId, Guid? userId = null, CancellationToken ct = default)
    {
        var code = featureCode.Trim().ToUpperInvariant();

        // Fetch data progressively (short-circuit to avoid unnecessary DB calls)
        var feature = await GetCachedFeatureAsync(code, ct);

        ClientFeatureReadModel? clientFeature = null;
        UserOverrideReadModel? userOverride = null;

        if (feature is not null && feature.IsActive)
        {
            if (feature.RequiresClient && clientId != Guid.Empty)
                clientFeature = await GetCachedClientFeatureAsync(clientId, feature.Id, ct);

            // Only fetch user override if client gate passed (or no client gate)
            var clientGatePassed = !feature.RequiresClient || (clientFeature?.IsEnabled == true);
            if (clientGatePassed && userId.HasValue && userId.Value != Guid.Empty)
                userOverride = await GetCachedUserOverrideAsync(userId.Value, feature.Id, ct);
        }

        // Delegate decision to pure evaluator — single source of truth
        var decision = FeatureEvaluator.Evaluate(feature, code, clientId, clientFeature, userOverride);
        LogDecision(decision);
        return decision;
    }

    public async Task<FeatureDecision> IsEnabledGloballyAsync(
        string featureCode, CancellationToken ct = default)
    {
        var code = featureCode.Trim().ToUpperInvariant();

        var feature = await GetCachedFeatureAsync(code, ct);
        if (feature is null)
            return LogAndReturn(new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.FeatureNotFound, code));

        // Fail-fast: global method cannot evaluate client-scoped features
        if (feature.RequiresClient)
            return LogAndReturn(new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.RequiresClient, code));

        if (!feature.IsActive)
            return LogAndReturn(new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.GlobalOff, code));

        return LogAndReturn(new FeatureDecision(true, FeatureFlagConstants.ReasonCodes.GrantedSystem, code));
    }

    public void InvalidateFeature(string featureCode)
    {
        var code = featureCode.Trim().ToUpperInvariant();
        _cache.Remove($"Feature:{code}");
    }

    public void InvalidateClientFeature(Guid clientId, Guid featureId)
    {
        _cache.Remove($"ClientFeature:{clientId}:{featureId}");
    }

    public void InvalidateUserOverride(Guid userId, Guid featureId)
    {
        _cache.Remove($"UserOverride:{userId}:{featureId}");
    }

    // --- Cache with stampede protection ---

    // Generic helper: per-key SemaphoreSlim ensures only one thread invokes the factory
    // for a given cache key. All other concurrent callers wait for the winner's result.
    //
    // Pattern: TryGetValue (lock-free) → acquire semaphore → double-check → factory → set cache.
    // This is the standard "double-checked locking" adapted for async + IMemoryCache.
    private async Task<T?> GetCachedAsync<T>(
        string key, Func<CancellationToken, Task<T?>> factory, CancellationToken ct) where T : class
    {
        // Fast path: cache hit — no locking, no allocation
        if (_cache.TryGetValue(key, out T? cached))
            return cached;

        // Slow path: acquire per-key semaphore so only one thread invokes the factory
        var semaphore = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            // Double-check: another thread may have populated the cache while we waited
            if (_cache.TryGetValue(key, out cached))
                return cached;

            var result = await factory(ct);

            using var entry = _cache.CreateEntry(key);
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            entry.Size = 1;
            entry.Value = result;

            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private Task<FeatureReadModel?> GetCachedFeatureAsync(string code, CancellationToken ct) =>
        GetCachedAsync($"Feature:{code}", token => _readDac.GetByCodeAsync(code, token), ct);

    private Task<ClientFeatureReadModel?> GetCachedClientFeatureAsync(
        Guid clientId, Guid featureId, CancellationToken ct) =>
        GetCachedAsync($"ClientFeature:{clientId}:{featureId}",
            token => _readDac.GetClientFeatureAsync(clientId, featureId, token), ct);

    private Task<UserOverrideReadModel?> GetCachedUserOverrideAsync(
        Guid userId, Guid featureId, CancellationToken ct) =>
        GetCachedAsync($"UserOverride:{userId}:{featureId}",
            token => _readDac.GetUserOverrideAsync(userId, featureId, token), ct);

    private void LogDecision(FeatureDecision decision) =>
        _logger.LogDebug("flag:{FeatureCode}={Result} ({ReasonCode})",
            decision.FeatureCode, decision.IsEnabled ? "ON" : "OFF", decision.ReasonCode);

    private FeatureDecision LogAndReturn(FeatureDecision decision)
    {
        LogDecision(decision);
        return decision;
    }
}
