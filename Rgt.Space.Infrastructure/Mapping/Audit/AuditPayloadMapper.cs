using Riok.Mapperly.Abstractions;
using Rgt.Space.Core.Domain.Contracts.Audit;
using System.Text.Json;

namespace Rgt.Space.Infrastructure.Mapping.Audit;

/// <summary>
/// Mapper for audit payload decoding operations using Mapperly
/// </summary>
[Mapper]
public partial class AuditPayloadMapper
{
    /// <summary>
    /// Map decompressed data to response
    /// </summary>
    public DecodeAuditPayloadResponse MapToResponse(
        string decompressed,
        int byteLength,
        bool isValidJson,
        JsonElement? cleanData,
        string? dataType,
        string? formattedJson,
        bool isTruncated
    )
    {
        return new DecodeAuditPayloadResponse(
            Success: true,
            Message: "Successfully decoded audit payload",
            CleanData: cleanData,
            DataType: dataType,
            FormattedJson: formattedJson,
            IsValidJson: isValidJson,
            IsTruncated: isTruncated,
            ByteLength: byteLength
        );
    }

    /// <summary>
    /// Map error to response
    /// </summary>
    public DecodeAuditPayloadResponse MapErrorToResponse(string errorMessage, string? errorDetails = null)
    {
        return new DecodeAuditPayloadResponse(
            Success: false,
            Message: errorMessage,
            Error: errorDetails
        );
    }
}
