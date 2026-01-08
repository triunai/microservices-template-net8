using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Rgt.Space.Core.Abstractions.Debugging;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that tracks checkpoint progression for the Combo-Break Debugger.
/// Automatically marks handler entry, completion, and failure for every MediatR request.
/// </summary>
/// <remarks>
/// <para>
/// This behavior should be registered BEFORE <see cref="AuditLoggingBehavior{TRequest, TResponse}"/>
/// in the MediatR pipeline to ensure checkpoints are tracked before audit logging occurs.
/// </para>
/// <para>
/// <b>Thread Safety:</b> Uses <see cref="IHttpContextAccessor"/> which relies on AsyncLocal.
/// Do not capture HttpContext outside the request flow.
/// </para>
/// </remarks>
public sealed class CheckpointPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICheckpointTracker _tracker;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CheckpointPipelineBehavior(
        ICheckpointTracker tracker,
        IHttpContextAccessor httpContextAccessor)
    {
        _tracker = tracker;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var handlerName = typeof(TRequest).Name;
        
        // Store raw handler name for Recorder (handler identity ≠ checkpoint state)
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Items[HttpConstants.ContextKeys.CurrentHandlerName] = handlerName;
        }
        
        // Mark entry (this becomes "current" - the frame we're in)
        _tracker.Enter($"handler:{handlerName}");
        
        try
        {
            var response = await next();
            
            // Check FluentResults failure using pattern matching
            var isFailed = response switch
            {
                Result r => r.IsFailed,
                ResultBase rb => rb.IsFailed,
                _ => false
            };
            
            if (isFailed)
            {
                _tracker.Fail("result-failed");
                return response;
            }
            
            // Success: pop from stack, add to history
            _tracker.Complete();
            return response;
        }
        catch
        {
            _tracker.Fail("exception");
            throw;
        }
    }
}
