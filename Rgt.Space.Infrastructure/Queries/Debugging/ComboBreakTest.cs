using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Debugging;

namespace Rgt.Space.Infrastructure.Queries.Debugging;

/// <summary>
/// Test query for the Combo-Break Debugger.
/// Simulates a multi-step workflow that can fail at any step.
/// </summary>
public static class ComboBreakTest
{
    public sealed record Query(int FailAt = 0) : IRequest<Result<Response>>;
    
    public sealed record Response(
        bool Success,
        string Message,
        List<string> CheckpointHistory,
        string? CheckpointLast);
    
    public sealed class Handler : IRequestHandler<Query, Result<Response>>
    {
        private readonly ICheckpointTracker _tracker;

        public Handler(ICheckpointTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            // Step 1: Validate Input
            var step1Result = await _tracker.InStep("step:ValidateInput", async () =>
            {
                await Task.Delay(50, ct);
                
                if (request.FailAt == 1)
                {
                    throw new InvalidOperationException("Simulated failure at step 1: ValidateInput");
                }
                
                return "Input validated";
            });

            // Step 2: Fetch Data
            var step2Result = await _tracker.InStep("step:FetchData", async () =>
            {
                await Task.Delay(50, ct);
                
                if (request.FailAt == 2)
                {
                    throw new InvalidOperationException("Simulated failure at step 2: FetchData");
                }
                
                return new { Id = Guid.NewGuid(), Name = "Test Data" };
            });

            // Step 3: Process Data
            var step3Result = await _tracker.InStep("step:ProcessData", async () =>
            {
                await Task.Delay(50, ct);
                
                if (request.FailAt == 3)
                {
                    throw new InvalidOperationException("Simulated failure at step 3: ProcessData");
                }
                
                return $"Processed: {step2Result.Name}";
            });

            // Step 4: Save Result
            await _tracker.InStepAsync("step:SaveResult", async () =>
            {
                await Task.Delay(50, ct);
                
                if (request.FailAt == 4)
                {
                    throw new InvalidOperationException("Simulated failure at step 4: SaveResult");
                }
            });

            return Result.Ok(new Response(
                Success: true,
                Message: "All steps completed successfully!",
                CheckpointHistory: _tracker.History.ToList(),
                CheckpointLast: _tracker.Last));
        }
    }
}
