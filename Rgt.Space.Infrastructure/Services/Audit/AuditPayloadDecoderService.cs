using Rgt.Space.Core.Domain.Contracts.Audit;
using Rgt.Space.Infrastructure.Auditing;
using Rgt.Space.Infrastructure.Mapping.Audit;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Rgt.Space.Infrastructure.Services.Audit;

/// <summary>
/// Service for decoding audit payloads
/// </summary>
public interface IAuditPayloadDecoderService
{
    Task<DecodeAuditPayloadResponse> DecodePayloadAsync(string hexString, string? mode = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of audit payload decoder service
/// </summary>
public class AuditPayloadDecoderService : IAuditPayloadDecoderService
{
    private readonly AuditPayloadMapper _mapper;

    public AuditPayloadDecoderService(AuditPayloadMapper mapper)
    {
        _mapper = mapper;
    }

    public Task<DecodeAuditPayloadResponse> DecodePayloadAsync(string hexString, string? mode = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert hex string to byte array
            var byteArray = ConvertHexToByteArray(hexString);

            // Safety checks
            ValidateGzipData(byteArray);
            ValidateSize(byteArray);

            // Decompress using Gzip
            var decompressed = DecompressString(byteArray);

            // Safety check: max decompressed size (1MB)
            if (decompressed.Length > 1024 * 1024)
            {
                throw new InvalidOperationException("Decompressed data exceeds 1MB limit");
            }

            // Process based on mode
            if (!string.IsNullOrEmpty(mode) && mode.ToLowerInvariant() == "plain")
            {
                return Task.FromResult(ProcessPlainMode(decompressed, byteArray.Length));
            }

            // Default: JSON response mode
            return Task.FromResult(ProcessJsonMode(decompressed, byteArray.Length));
        }
        catch (Exception ex)
        {
            return Task.FromResult(_mapper.MapErrorToResponse($"Failed to decode hex string: {ex.Message}", ex.ToString()));
        }
    }

    /// <summary>
    /// Convert hex string to byte array
    /// </summary>
    private static byte[] ConvertHexToByteArray(string hexString)
    {
        var hexBytes = hexString;
        if (hexBytes.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hexBytes = hexBytes.Substring(2);
        }

        // Safety check: ensure even length
        if (hexBytes.Length % 2 != 0)
        {
            throw new ArgumentException("Hex string must have even length");
        }

        var byteArray = new byte[hexBytes.Length / 2];
        for (int i = 0; i < byteArray.Length; i++)
        {
            byteArray[i] = Convert.ToByte(hexBytes.Substring(i * 2, 2), 16);
        }

        return byteArray;
    }

    /// <summary>
    /// Validate Gzip magic bytes
    /// </summary>
    private static void ValidateGzipData(byte[] data)
    {
        if (data.Length < 2 || data[0] != 0x1F || data[1] != 0x8B)
        {
            throw new ArgumentException("Data does not appear to be Gzip compressed (missing magic bytes 1F 8B)");
        }
    }

    /// <summary>
    /// Validate data size
    /// </summary>
    private static void ValidateSize(byte[] data)
    {
        if (data.Length > 10 * 1024 * 1024) // 10MB limit for compressed data
        {
            throw new ArgumentException("Compressed data exceeds 10MB limit");
        }
    }

    /// <summary>
    /// Decompress Gzip data
    /// </summary>
    private static string DecompressString(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    /// <summary>
    /// Process plain text mode
    /// </summary>
    private DecodeAuditPayloadResponse ProcessPlainMode(string decompressed, int byteLength)
    {
        var prettyJson = FormatAsPrettyJson(decompressed);
        return new DecodeAuditPayloadResponse(
            Success: true,
            Message: "Successfully decoded audit payload (plain mode)",
            FormattedJson: prettyJson,
            ByteLength: byteLength,
            IsTruncated: decompressed.Contains("... [TRUNCATED]")
        );
    }

    /// <summary>
    /// Process JSON mode
    /// </summary>
    private DecodeAuditPayloadResponse ProcessJsonMode(string decompressed, int byteLength)
    {
        JsonDocument? jsonDoc = null;
        bool isValidJson = false;
        JsonElement? cleanData = null;
        string? dataType = null;
        string? formattedJson = null;

        try
        {
            jsonDoc = JsonDocument.Parse(decompressed);
            isValidJson = true;

            // Extract clean data from FluentResults wrapper
            if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
            {
                cleanData = valueElement;
                dataType = "FluentResults Success";
            }
            else if (jsonDoc.RootElement.TryGetProperty("valueOrDefault", out var valueOrDefaultElement))
            {
                cleanData = valueOrDefaultElement;
                dataType = "FluentResults Success (valueOrDefault)";
            }
            else
            {
                // Not a FluentResults wrapper, use raw data
                cleanData = jsonDoc.RootElement;
                dataType = "Raw JSON";
            }

            formattedJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // Not valid JSON, but that's okay
            dataType = "Raw Text";
        }

        return _mapper.MapToResponse(
            decompressed,
            byteLength,
            isValidJson,
            cleanData,
            dataType,
            formattedJson,
            decompressed.Contains("... [TRUNCATED]")
        );
    }

    /// <summary>
    /// Format decompressed data as pretty JSON
    /// </summary>
    private static string FormatAsPrettyJson(string decompressed)
    {
        try
        {
            // Try to parse as JSON and extract clean data
            var jsonDoc = JsonDocument.Parse(decompressed);

            // Extract clean data from FluentResults wrapper
            if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
            {
                return JsonSerializer.Serialize(valueElement, new JsonSerializerOptions { WriteIndented = true });
            }
            else if (jsonDoc.RootElement.TryGetProperty("valueOrDefault", out var valueOrDefaultElement))
            {
                return JsonSerializer.Serialize(valueOrDefaultElement, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                // Not a FluentResults wrapper, use raw data
                return JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch
        {
            // Not valid JSON, return as-is
            return decompressed;
        }
    }
}
