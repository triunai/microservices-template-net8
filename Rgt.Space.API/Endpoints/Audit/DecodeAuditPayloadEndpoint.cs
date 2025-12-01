using FastEndpoints;
using Rgt.Space.Core.Domain.Contracts.Audit;
using Rgt.Space.Infrastructure.Persistence.Services.Audit;

namespace Rgt.Space.API.Endpoints.Audit;

/// <summary>
/// Decode audit payload from hex string (for debugging/analysis)
/// </summary>
public sealed class DecodeAuditPayloadEndpoint(IAuditPayloadDecoderService decoderService) : Endpoint<DecodeAuditPayloadRequest, DecodeAuditPayloadResponse>
{
    public override void Configure()
    {
        Get("/api/audit/decode");
        AllowAnonymous(); // For debugging purposes
        Summary(s =>
        {
            s.Summary = "Decode audit payload from hex string";
            s.Description = "Converts hex string from SQL Server VARBINARY(MAX) back to readable JSON. Default returns JSON. Use ?mode=plain for text/plain response.";
            s.Response<DecodeAuditPayloadResponse>(200, "Successfully decoded payload (JSON mode - default)");
            s.Response(200, "Successfully decoded payload (text/plain mode)");
            s.Response(400, "Invalid hex string or decompression failed");
        });
    }

    public override async Task HandleAsync(DecodeAuditPayloadRequest req, CancellationToken ct)
    {
        var result = await decoderService.DecodePayloadAsync(req.HexString, req.Mode, ct);

        if (!result.Success)
        {
            await Send.ResponseAsync(result, 400, ct);
            return;
        }

        // Handle plain text mode
        if (!string.IsNullOrEmpty(req.Mode) && req.Mode.ToLowerInvariant() == "plain")
        {
            HttpContext.Response.ContentType = "text/plain";
            await HttpContext.Response.WriteAsync(result.FormattedJson ?? result.Message, ct);
            return;
        }

        // Default: JSON response mode
        await Send.OkAsync(result, ct);
    }

}
