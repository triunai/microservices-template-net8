using Dapper;
using MicroservicesBase.Core.Abstractions.Auditing;
using MicroservicesBase.Core.Configuration;
using MicroservicesBase.Core.Constants;
using MicroservicesBase.Core.Domain.Auditing;
using MicroservicesBase.Infrastructure.Resilience;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using System.Threading.Channels;

namespace MicroservicesBase.Infrastructure.Auditing;

/// <summary>
/// Audit logger implementation using bounded queue + background writer pattern.
/// Thread-safe, non-blocking, with graceful shutdown flush and resilience policies.
/// </summary>
public sealed class AuditLogger : IAuditLogger, IHostedService
{
    private readonly Channel<AuditEntry> _channel;
    private readonly AuditSettings _settings;
    private readonly IConfiguration _configuration;
    private readonly ResiliencePipelineRegistry<string> _pipelineRegistry;
    private readonly IOptions<ResilienceSettings> _resilienceSettings;
    private readonly ILogger<AuditLogger> _logger;
    private Task? _writerTask;
    private readonly CancellationTokenSource _shutdownCts = new();

    public AuditLogger(
        IOptions<AuditSettings> settings,
        IConfiguration configuration,
        ResiliencePipelineRegistry<string> pipelineRegistry,
        IOptions<ResilienceSettings> resilienceSettings,
        ILogger<AuditLogger> logger)
    {
        _settings = settings.Value;
        _configuration = configuration;
        _pipelineRegistry = pipelineRegistry;
        _resilienceSettings = resilienceSettings;
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
        if (_writerTask == null)
            return;

        _logger.LogInformation("Stopping audit logger and flushing pending entries...");
        
        // Signal shutdown
        _channel.Writer.Complete();
        
        // Wait for writer to finish processing all entries
        await _writerTask;
        
        _logger.LogInformation("Audit logger stopped. All entries flushed.");
    }

    /// <summary>
    /// Enqueue an audit entry (non-blocking)
    /// </summary>
    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
            return;

        try
        {
            await _channel.Writer.WriteAsync(entry, ct);
        }
        catch (ChannelClosedException)
        {
            // Channel is closed (shutting down), log to fallback
            _logger.LogWarning("Audit channel closed, falling back to Serilog");
            LogToSerilog(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue audit entry");
        }
    }

    /// <summary>
    /// Flush all pending entries (blocks until complete)
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        _channel.Writer.Complete();
        
        // Wait for writer to process all pending entries
        if (_writerTask != null)
        {
            await _writerTask;
        }
    }

    /// <summary>
    /// Background task that reads from channel and writes batches to database
    /// </summary>
    private async Task BackgroundWriter(CancellationToken ct)
    {
        var batch = new List<AuditEntry>(_settings.Queue.BatchSize);
        var flushInterval = TimeSpan.FromSeconds(_settings.Queue.FlushIntervalSeconds);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for data with timeout (flush interval)
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(flushInterval);

                    // Wait for data to be available (blocks until data arrives or timeout)
                    await _channel.Reader.WaitToReadAsync(cts.Token);

                    // Drain all available entries
                    while (_channel.Reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                        
                        // Batch full, write immediately
                        if (batch.Count >= _settings.Queue.BatchSize)
                        {
                            await WriteBatchAsync(batch, ct);
                            batch.Clear();
                        }
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Timeout occurred (flush interval elapsed), flush pending entries
                    if (batch.Count > 0)
                    {
                        await WriteBatchAsync(batch, ct);
                        batch.Clear();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Audit logger shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit logger background writer crashed");
        }
        finally
        {
            // Flush remaining entries on shutdown
            while (_channel.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
                
                if (batch.Count >= _settings.Queue.BatchSize)
                {
                    await WriteBatchAsync(batch, CancellationToken.None);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                _logger.LogInformation("Flushing {Count} remaining audit entries on shutdown", batch.Count);
                await WriteBatchAsync(batch, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Write a batch of audit entries to database
    /// </summary>
    private async Task WriteBatchAsync(List<AuditEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0)
            return;

        try
        {
            // Group by tenant (each tenant has its own database)
            var groupedByTenant = entries.GroupBy(e => e.TenantId);

            foreach (var tenantGroup in groupedByTenant)
            {
                var tenantId = tenantGroup.Key;
                var tenantEntries = tenantGroup.ToList();

                await WriteTenantBatchAsync(tenantId, tenantEntries, ct);
            }

            _logger.LogDebug("Successfully wrote {Count} audit entries to database", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit batch to database. Falling back to Serilog.");
            
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
    /// Write audit entries for a specific tenant with resilience protection
    /// </summary>
        private async Task WriteTenantBatchAsync(string tenantId, List<AuditEntry> entries, CancellationToken ct)
        {
            // Get or create pipeline for this tenant on-demand (same pattern as SalesReadDac)
            var pipelineKey = tenantId; // Just use tenant ID as key
            if (!_pipelineRegistry.TryGetPipeline(pipelineKey, out var pipeline))
            {
                // Create pipeline on first access
                _pipelineRegistry.TryAddBuilder(pipelineKey, (builder, context) =>
                {
                    var settings = _resilienceSettings.Value.AuditDb;
                    builder.AddPipelineFromSettings(
                        settings,
                        ResiliencePolicies.IsSqlTransientError,
                        $"AuditDb:{tenantId}",
                        _logger);
                });
                pipeline = _pipelineRegistry.GetPipeline(pipelineKey);
            }

        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                // Get tenant connection string (same way we do for data access)
                var masterConnectionString = _configuration.GetConnectionString("TenantMaster")!;
                
                string? tenantConnectionString;
                await using (var masterConn = new SqlConnection(masterConnectionString))
                {
                    await masterConn.OpenAsync(token);
                    tenantConnectionString = await masterConn.QueryFirstOrDefaultAsync<string>(
                        SqlConstants.Queries.GetTenantConnectionString, 
                        new { TenantId = tenantId });
                }

                if (string.IsNullOrEmpty(tenantConnectionString))
                {
                    _logger.LogWarning("Tenant {TenantId} not found or inactive. Cannot write audit logs.", tenantId);
                    return;
                }

                // Bulk insert audit entries with CommandTimeout alignment
                await using var tenantConn = new SqlConnection(tenantConnectionString);
                await tenantConn.OpenAsync(token);

                //todo: make reusable stored proc ###
                const string insertSql = @"
                    INSERT INTO dbo.AuditLog (
                        TenantId, UserId, ClientId, IpAddress, UserAgent,
                        Action, EntityType, EntityId,
                        Timestamp, CorrelationId, RequestPath,
                        IsSuccess, StatusCode, ErrorCode, ErrorMessage, DurationMs,
                        RequestData, ResponseData, Delta,
                        IdempotencyKey, Source, RequestHash
                    ) VALUES (
                        @TenantId, @UserId, @ClientId, @IpAddress, @UserAgent,
                        @Action, @EntityType, @EntityId,
                        @Timestamp, @CorrelationId, @RequestPath,
                        @IsSuccess, @StatusCode, @ErrorCode, @ErrorMessage, @DurationMs,
                        @RequestData, @ResponseData, @Delta,
                        @IdempotencyKey, @Source, @RequestHash
                    )";

                // Use CommandTimeout of 3.5s (less than Polly's 4s timeout)
                    await tenantConn.ExecuteAsync(insertSql, entries, commandTimeout: SqlConstants.CommandTimeouts.AuditDb);
                
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

