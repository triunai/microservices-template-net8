using System.Text.Json;

namespace MicroservicesBase.Core.Domain.Contracts.Audit;

/// <summary>
/// Request to decode audit payload from hex string
/// </summary>
public record DecodeAuditPayloadRequest(string HexString, string? Mode = null);

/// <summary>
/// Response from audit payload decoding
/// </summary>
public record DecodeAuditPayloadResponse(
    bool Success,
    string Message,
    JsonElement? CleanData = null,
    string? DataType = null,
    string? FormattedJson = null,
    bool IsValidJson = false,
    bool IsTruncated = false,
    int ByteLength = 0,
    string? Error = null
);
