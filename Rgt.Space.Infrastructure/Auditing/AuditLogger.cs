using Dapper;
using Rgt.Space.Core.Abstractions.Auditing;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Auditing;
using Rgt.Space.Infrastructure.Resilience;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Threading.Channels;
using Polly.Registry;
using Polly.CircuitBreaker;

namespace Rgt.Space.Infrastructure.Auditing;

/// <summary>
/// Audit logger implementation using bounded queue + background writer pattern.
/// Thread-safe, non-blocking, with graceful shutdown flush and resilience policies.
/// </summary>
public sealed class AuditLogger : IAuditLogger, IHostedService
{
    private readonly Channel<AuditEntry> _channel;
    private readonly AuditSettings _settings;
    private readonly IConfiguration _configuration;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<AuditLogger> _logger;
    private Task? _writerTask;
    private readonly CancellationTokenSource _shutdownCts = new();

    public AuditLogger(
        IOptions<AuditSettings> settings,
        IConfiguration configuration,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<AuditLogger> logger)
    {
        _settings = settings.Value;
        _configuration = configuration;
        // Standard Pattern A: Inject and use the pre-registered "AuditDb" pipeline
        _pipeline = pipelineProvider.GetPipeline("AuditDb");
        _logger = logger;

        // Create bounded channel (queue) with capacity limit
        var channelOptions = new BoundedChannelOptions(_settings.Queue.Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest // Drop oldest entries if queue is full
        };
        
        _channel = Channel.CreateBounded<AuditEntry>(channelOptions);
    }

    /// <summary>
    /// Start the background writer task
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Audit logging is disabled");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting audit logger background writer");
        _writerTask = BackgroundWriter(_shutdownCts.Token);
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the background writer and flush remaining entries
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // ... (existing code, no changes needed here)
        if (_writerTask == null)
            return;

        _logger.LogInformation("Stopping audit logger and flushing pending entries...");
        
        _shutdownCts.Cancel();

        try
        {
            _channel.Writer.Complete();
        }
        catch (ChannelClosedException) { }
        
        await _writerTask;
        
        _logger.LogInformation("Audit logger stopped. All entries flushed.");
    }

    // ... (LogAsync, FlushAsync, BackgroundWriter, WriteBatchAsync - No changes)

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        if (!_settings.Enabled) return;
        try { await _channel.Writer.WriteAsync(entry, ct); }
        catch (ChannelClosedException) { _logger.LogWarning("Audit channel closed, falling back to Serilog"); LogToSerilog(entry); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to enqueue audit entry"); }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        _shutdownCts.Cancel();
        try { _channel.Writer.Complete(); } catch (ChannelClosedException) { }
        if (_writerTask != null) await _writerTask;
    }

    private async Task BackgroundWriter(CancellationToken ct)
    {
        var batch = new List<AuditEntry>(_settings.Queue.BatchSize);
        var flushInterval = TimeSpan.FromSeconds(_settings.Queue.FlushIntervalSeconds);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(flushInterval);
                    await _channel.Reader.WaitToReadAsync(cts.Token);

                    while (_channel.Reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                        if (batch.Count >= _settings.Queue.BatchSize)
                        {
                            await WriteBatchAsync(batch, ct);
                            batch.Clear();
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (batch.Count > 0)
                    {
                        await WriteBatchAsync(batch, ct);
                        batch.Clear();
                    }
                }
            }
        }
        catch (OperationCanceledException) { _logger.LogInformation("Audit logger shutdown requested"); }
        catch (Exception ex) { _logger.LogError(ex, "Audit logger background writer crashed"); }
        finally
        {
            while (_channel.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
                if (batch.Count >= _settings.Queue.BatchSize) { await WriteBatchAsync(batch, CancellationToken.None); batch.Clear(); }
            }
            if (batch.Count > 0) { _logger.LogInformation("Flushing {Count} remaining audit entries on shutdown", batch.Count); await WriteBatchAsync(batch, CancellationToken.None); }
        }
    }

    private async Task WriteBatchAsync(List<AuditEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return;
        try
        {
            var groupedByTenant = entries.GroupBy(e => e.TenantId);
            foreach (var tenantGroup in groupedByTenant)
            {
                await WriteTenantBatchAsync(tenantGroup.Key, tenantGroup.ToList(), ct);
            }
            _logger.LogDebug("Successfully wrote {Count} audit entries to database", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit batch to database. Falling back to Serilog.");
            if (_settings.Backpressure.FallbackToFile) { foreach (var entry in entries) LogToSerilog(entry); }
        }
    }

    /// <summary>
    /// Write audit entries for a specific tenant with resilience protection
    /// </summary>
    private async Task WriteTenantBatchAsync(string tenantId, List<AuditEntry> entries, CancellationToken ct)
    {
        try
        {
            await _pipeline.ExecuteAsync(async token =>
            {
                // SINGLE DATABASE ARCHITECTURE: All audit logs write to PortalDb
                // This is a logical monolith - we use row-level isolation (client_id),
                // NOT database-per-tenant. TenantMaster is legacy and should not be used.
                var connectionString = _configuration.GetConnectionString("PortalDb");
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogWarning("PortalDb connection string not configured. Cannot write audit logs.");
                    return;
                }

                // Bulk insert audit entries with CommandTimeout alignment
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(token);

                //todo: make reusable stored proc ###
                const string insertSql = @"
                    INSERT INTO audit_log (
                        user_id, client_id, ip_address, user_agent,
                        action, entity_type, entity_id,
                        timestamp, correlation_id, request_path,
                        is_success, status_code, error_code, error_message, duration_ms,
                        request_data, response_data, delta,
                        idempotency_key, source, request_hash
                    ) VALUES (
                        @UserId, @ClientId, @IpAddress, @UserAgent,
                        @Action, @EntityType, @EntityId,
                        @Timestamp, @CorrelationId, @RequestPath,
                        @IsSuccess, @StatusCode, @ErrorCode, @ErrorMessage, @DurationMs,
                        @RequestData, @ResponseData, @Delta,
                        @IdempotencyKey, @Source, @RequestHash
                    )";

                // Use CommandTimeout of 3.5s (less than Polly's 4s timeout)
                // Use CommandDefinition to propagate CancellationToken properly
                var cmd = new CommandDefinition(insertSql, entries, cancellationToken: token, commandTimeout: SqlConstants.CommandTimeouts.AuditDb);
                await conn.ExecuteAsync(cmd);
                
                _logger.LogDebug("Successfully wrote {Count} audit entries for tenant {TenantId}", entries.Count, tenantId);
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Audit DB circuit breaker OPEN for tenant {TenantId}, falling back to Serilog", tenantId);
            
            // Circuit breaker is open - fallback to Serilog immediately
            if (_settings.Backpressure.FallbackToFile)
            {
                foreach (var entry in entries)
                {
                    LogToSerilog(entry);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit batch for tenant {TenantId}, falling back to Serilog", tenantId);
            
            // Database write failed - fallback to Serilog
            if (_settings.Backpressure.FallbackToFile)
            {
                foreach (var entry in entries)
                {
                    LogToSerilog(entry);
                }
            }
        }
    }

    /// <summary>
    /// Fallback: log to Serilog if database write fails
    /// </summary>
    private void LogToSerilog(AuditEntry entry)
    {
        _logger.LogInformation(
            "AUDIT: {Action} by {UserId} on {EntityType}/{EntityId} - Success: {IsSuccess}, StatusCode: {StatusCode}, Duration: {DurationMs}ms",
            entry.Action,
            entry.UserId ?? "Anonymous",
            entry.EntityType ?? "Unknown",
            entry.EntityId ?? "Unknown",
            entry.IsSuccess,
            entry.StatusCode,
            entry.DurationMs
        );
    }
}

