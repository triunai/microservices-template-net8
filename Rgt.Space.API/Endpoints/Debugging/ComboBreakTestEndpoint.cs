using FastEndpoints;
using MediatR;
using Microsoft.Extensions.Hosting;
using Rgt.Space.Infrastructure.Queries.Debugging;

namespace Rgt.Space.API.Endpoints.Debugging;

/// <summary>
/// Test endpoint for the Combo-Break Debugger.
/// Uses MediatR to go through the CheckpointPipelineBehavior.
/// </summary>
/// <remarks>
/// <b>DEV ONLY:</b> This endpoint should be disabled in production.
/// 
/// Usage:
/// - GET /api/v1/debugging/combo-break-test?failAt=0  → Success (no failure)
/// - GET /api/v1/debugging/combo-break-test?failAt=1  → Fails at step 1 (ValidateInput)
/// - GET /api/v1/debugging/combo-break-test?failAt=2  → Fails at step 2 (FetchData)
/// - GET /api/v1/debugging/combo-break-test?failAt=3  → Fails at step 3 (ProcessData)
/// - GET /api/v1/debugging/combo-break-test?failAt=4  → Fails at step 4 (SaveResult)
/// </remarks>
public class ComboBreakTestEndpoint : Endpoint<ComboBreakTestRequest, ComboBreakTestResponse>
{
    private readonly IMediator _mediator;

    public ComboBreakTestEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/debugging/combo-break-test");
        AllowAnonymous(); // For testing only
        Tags("Debugging");
        Description(d => d
            .WithSummary("Test Combo-Break Debugger")
            .WithDescription("Simulates a multi-step workflow to test checkpoint tracking. Pass failAt=1-4 to trigger failure at specific step."));
    }

    public override async Task HandleAsync(ComboBreakTestRequest req, CancellationToken ct)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment())
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        // Use MediatR which goes through CheckpointPipelineBehavior
        var result = await _mediator.Send(new ComboBreakTest.Query(req.FailAt), ct);
        
        if (result.IsFailed)
        {
            // This shouldn't happen for our test - exceptions are thrown, not Result.Fail
            ThrowError(result.Errors.First().Message);
            return;
        }
        
        Response = new ComboBreakTestResponse
        {
            Success = result.Value.Success,
            Message = result.Value.Message,
            CheckpointHistory = result.Value.CheckpointHistory,
            CheckpointLast = result.Value.CheckpointLast
        };
    }
}

public class ComboBreakTestRequest
{
    /// <summary>
    /// Step number to fail at (1-4). Use 0 for success.
    /// </summary>
    public int FailAt { get; set; } = 0;
}

public class ComboBreakTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> CheckpointHistory { get; set; } = new();
    public string? CheckpointLast { get; set; }
}
