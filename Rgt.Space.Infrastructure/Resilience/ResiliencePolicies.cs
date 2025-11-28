using Npgsql;
using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Configuration;
using Rgt.Space.Core.Constants;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using StackExchange.Redis;
using System.Net.Sockets;

namespace Rgt.Space.Infrastructure.Resilience;

/// <summary>
/// Factory for creating Polly v8 resilience pipelines with POS-optimized settings.
/// Provides error classifiers, pipeline builders, and logging callbacks.
/// </summary>
public static class ResiliencePolicies
{
    // Policy keys (use centralized constants)
    public const string MasterDbKey = ResilienceConstants.PipelineKeys.MasterDb;
    public const string RedisKey = ResilienceConstants.PipelineKeys.Redis;
    public const string TenantDbKeyPrefix = ResilienceConstants.PipelineKeys.TenantDbPrefix;
    public const string AuditDbKeyPrefix = ResilienceConstants.PipelineKeys.AuditDbPrefix;

    /// <summary>
    /// Builds a complete resilience pipeline with timeout, retry, circuit breaker, and optional bulkhead.
    /// Strategy order: Timeout (outer) → Retry → Circuit Breaker → Bulkhead (inner) → [Your Operation]
    /// </summary>
    public static ResiliencePipelineBuilder AddPipelineFromSettings(
        this ResiliencePipelineBuilder builder,
        PipelineSettings settings,
        Func<Exception, bool> shouldHandle,
        string pipelineName,
        ILogger logger)
    {
        // 4. Timeout (outermost - enforces total latency budget)
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMilliseconds(settings.TimeoutMs),
            OnTimeout = args =>
            {
                logger.LogWarning(
                    "Pipeline {PipelineName} timed out after {TimeoutMs}ms",
                    pipelineName, settings.TimeoutMs);
                return default;
            }
        });

        // 3. Retry (with jittered backoff to prevent thundering herds)
        // Only add retry if RetryCount > 0 (Polly v8 requires MaxRetryAttempts >= 1)
        if (settings.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = settings.RetryCount,
                DelayGenerator = args =>
                {
                    // Use pre-calculated delays with jitter
                    // NOTE: Polly v8 AttemptNumber is 0-based (0, 1, 2, ...)
                    if (args.AttemptNumber < settings.RetryDelaysMs.Length)
                    {
                        var baseDelay = settings.RetryDelaysMs[args.AttemptNumber];
                        return new ValueTask<TimeSpan?>(TimeSpan.FromMilliseconds(baseDelay));
                    }
                    return new ValueTask<TimeSpan?>(TimeSpan.FromMilliseconds(ResilienceConstants.Retry.FallbackDelayMs));
                },
                UseJitter = true, // ±25% jitter to prevent coordinated retries
                ShouldHandle = new PredicateBuilder().Handle(shouldHandle),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Pipeline {PipelineName} retry attempt {AttemptNumber}/{MaxAttempts} after {Delay}ms. Exception: {Exception}",
                        pipelineName, args.AttemptNumber + 1, settings.RetryCount, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
                    return default;
                }
            });
        }
        else
        {
            logger.LogInformation("Pipeline {PipelineName} configured with NO retries (fail-fast mode for POS)", pipelineName);
        }

        // 2. Circuit Breaker (failure-ratio based, prevents cascade failures)
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = settings.FailureRatio,
            SamplingDuration = TimeSpan.FromSeconds(settings.SamplingDurationSeconds),
            MinimumThroughput = settings.MinimumThroughput,
            BreakDuration = TimeSpan.FromSeconds(settings.BreakDurationSeconds),
            ShouldHandle = new PredicateBuilder().Handle(shouldHandle),
            OnOpened = args =>
            {
                logger.LogError(
                    "🔴 Circuit breaker OPENED for {PipelineName}. Failure ratio: {FailureRatio:P2}, Break duration: {BreakDuration}s",
                    pipelineName, settings.FailureRatio, settings.BreakDurationSeconds);
                return default;
            },
            OnClosed = args =>
            {
                logger.LogInformation(
                    "🟢 Circuit breaker CLOSED for {PipelineName}. System recovered.",
                    pipelineName);
                return default;
            },
            OnHalfOpened = args =>
            {
                logger.LogWarning(
                    "🟡 Circuit breaker HALF-OPEN for {PipelineName}. Testing recovery...",
                    pipelineName);
                return default;
            }
        });

        // 1. Bulkhead isolation (Note: Concurrency limiting requires additional packages in Polly v8)
        // For now, we'll rely on the underlying connection pool limits and may add this later
        // if (settings.BulkheadMaxConcurrency.HasValue)
        // {
        //     // TODO: Implement concurrency limiting when System.Threading.RateLimiting is added
        //     logger.LogInformation("Bulkhead max concurrency setting ({MaxConcurrency}) noted for future implementation", 
        //         settings.BulkheadMaxConcurrency.Value);
        // }

        return builder;
    }

    /// <summary>
    /// Determines if a SQL exception is transient and should be retried.
    /// Based on PostgreSQL error codes for timeouts, deadlocks, and connection issues.
    /// </summary>
    public static bool IsSqlTransientError(Exception exception)
    {
        return exception switch
        {
            NpgsqlException pgEx => pgEx.SqlState switch
            {
                SqlConstants.ErrorCodes.SerializationFailure => true,   // 40001
                SqlConstants.ErrorCodes.DeadlockDetected => true,       // 40P01
                SqlConstants.ErrorCodes.ConnectionException => true,    // 08000
                SqlConstants.ErrorCodes.ConnectionDoesNotExist => true, // 08003
                SqlConstants.ErrorCodes.ConnectionFailure => true,      // 08006
                SqlConstants.ErrorCodes.CannotConnectNow => true,       // 57P03
                SqlConstants.ErrorCodes.AdminShutdown => true,          // 57P01
                SqlConstants.ErrorCodes.CrashShutdown => true,          // 57P02
                _ => pgEx.IsTransient // Check Npgsql's built-in transient property
            },
            TimeoutException => true,
            SocketException => true,
            InvalidOperationException ex when ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a Redis exception is transient and should be retried.
    /// Covers connection issues but not serialization or configuration errors.
    /// </summary>
    public static bool IsRedisTransientError(Exception exception)
    {
        return exception switch
        {
            RedisException redisEx => redisEx.Message switch
            {
                // Connection/network issues
                var msg when msg.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
                var msg when msg.Contains("connection", StringComparison.OrdinalIgnoreCase) => true,
                var msg when msg.Contains("socket", StringComparison.OrdinalIgnoreCase) => true,
                var msg when msg.Contains("server", StringComparison.OrdinalIgnoreCase) => true,
                // Don't retry serialization or configuration errors
                _ => false
            },
            SocketException => true,
            TimeoutException => true,
            EndOfStreamException => true,
            // Don't retry these
            ArgumentException => false,
            InvalidOperationException => false,
            _ => false
        };
    }

    /// <summary>
    /// Creates a keyed pipeline name for per-tenant isolation.
    /// Example: "TenantDb:7ELEVEN", "AuditDb:BURGERKING"
    /// </summary>
    public static string CreateTenantKey(string prefix, string tenantId)
    {
        return $"{prefix}:{tenantId}";
    }
}
