# 🐛 Fix AuditLogger Shutdown Hang

## Context
The `AuditLogger` component has a critical bug in its shutdown/flush mechanism. The `BackgroundWriter` loop condition (`!ct.IsCancellationRequested`) depends on `_shutdownCts`, but this token source is never cancelled during `StopAsync` or `FlushAsync`. This causes the background writer to loop indefinitely (or spin tightly) after the channel is completed, preventing the application from shutting down gracefully.

## 🛑 Problem Analysis
1.  **Shutdown Signal**: `StopAsync` completes the channel writer (`_channel.Writer.Complete()`), preventing new writes.
2.  **Loop Condition**: `BackgroundWriter` loop checks `!ct.IsCancellationRequested` (derived from `_shutdownCts`).
3.  **Broken Logic**: 
    *   When the channel is empty and closed, `WaitToReadAsync` returns `false` immediately.
    *   The loop continues because `ct` is not cancelled.
    *   This creates a tight loop or ineffective polling until the process is forced to exit.

## 🛠️ Implementation Plan

### 1. Modify `StopAsync`
Explicitly cancel the `_shutdownCts` to break the `BackgroundWriter` loop.

```csharp
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_writerTask == null)
            return;

        _logger.LogInformation("Stopping audit logger and flushing pending entries...");
        
        // 1. Signal shutdown to background writer immediately
        await _shutdownCts.CancelAsync(); // or .Cancel() for synchronous
        
        // 2. Close channel to stop accepting new entries
        try
        {
            _channel.Writer.Complete();
        }
        catch (ChannelClosedException)
        {
            // Ignore if already closed
        }
        
        // 3. Wait for writer to finish processing all entries (handled in finally block)
        await _writerTask;
        
        _logger.LogInformation("Audit logger stopped. All entries flushed.");
    }
```

### 2. Modify `FlushAsync`
Apply the same fix to `FlushAsync` since it acts as a terminal flushing operation in this implementation.

```csharp
    public async Task FlushAsync(CancellationToken ct = default)
    {
        // 1. Signal shutdown
        await _shutdownCts.CancelAsync();

        try
        {
            _channel.Writer.Complete();
        }
        catch (ChannelClosedException)
        {
            // Ignore if already closed
        }
        
        // 2. Wait for writer to process all pending entries
        if (_writerTask != null)
        {
            await _writerTask;
        }
    }
```

### 3. Verification Logic
*   **Trigger**: `_shutdownCts.Cancel()` is called.
*   **Effect**: 
    *   If `BackgroundWriter` is waiting at `WaitToReadAsync`, it throws `OperationCanceledException`.
    *   Exception is caught in the `BackgroundWriter`.
    *   `finally` block executes.
    *   `finally` block drains the remaining items in the channel (which are still readable even if the writer is completed) and writes them to the DB.
    *   Task completes successfully.
*   **Outcome**: Clean, immediate shutdown with zero data loss.
