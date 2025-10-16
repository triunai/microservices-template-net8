using FastEndpoints;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MicroservicesBase.API.Endpoints.Audit;

/// <summary>
/// Decode audit payload from hex string (for debugging/analysis)
/// </summary>
public class DecodeAuditPayloadEndpoint : Endpoint<DecodeAuditPayloadRequest, DecodeAuditPayloadResponse>
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
        try
        {
            // Convert hex string to byte array (fix: only remove 0x prefix if present)
            var hexBytes = req.HexString;
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

            // Safety check: verify Gzip magic bytes
            if (byteArray.Length < 2 || byteArray[0] != 0x1F || byteArray[1] != 0x8B)
            {
                throw new ArgumentException("Data does not appear to be Gzip compressed (missing magic bytes 1F 8B)");
            }

            // Decompress using Gzip
            var decompressed = DecompressString(byteArray);
            
            // Safety check: max decompressed size (1MB)
            if (decompressed.Length > 1024 * 1024)
            {
                throw new InvalidOperationException("Decompressed data exceeds 1MB limit");
            }

            // Check if user wants plain text mode (optional parameter)
            if (!string.IsNullOrEmpty(req.Mode) && req.Mode.ToLowerInvariant() == "plain")
            {
                // Return as text/plain with pretty JSON
                var prettyJson = FormatAsPrettyJson(decompressed);
                HttpContext.Response.ContentType = "text/plain";
                await HttpContext.Response.WriteAsync(prettyJson, ct);
                return;
            }

            // Default: JSON response mode
            var result = ProcessJsonResponse(decompressed, byteArray.Length);
            await Send.OkAsync(result, ct);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(req.Mode) && req.Mode.ToLowerInvariant() == "plain")
            {
                HttpContext.Response.ContentType = "text/plain";
                await HttpContext.Response.WriteAsync($"Error: {ex.Message}", ct);
            }
            else
            {
                await Send.ResponseAsync(new DecodeAuditPayloadResponse
                {
                    Success = false,
                    Message = $"Failed to decode hex string: {ex.Message}",
                    Error = ex.ToString()
                }, 400, ct);
            }
        }
    }

    /// <summary>
    /// Decompress Gzip data (same implementation as PayloadProcessor.DecompressString)
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
    /// Format decompressed data as pretty JSON for text/plain mode
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

    /// <summary>
    /// Process decompressed data for JSON response mode
    /// </summary>
    private static DecodeAuditPayloadResponse ProcessJsonResponse(string decompressed, int byteLength)
    {
        JsonDocument? jsonDoc = null;
        bool isValidJson = false;
        JsonElement? cleanData = null;
        string? dataType = null;
        
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
        }
        catch
        {
            // Not valid JSON, but that's okay
            dataType = "Raw Text";
        }

        return new DecodeAuditPayloadResponse
        {
            Success = true,
            CleanData = cleanData,
            DataType = dataType,
            IsValidJson = isValidJson,
            FormattedJson = isValidJson && jsonDoc != null 
                ? JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true })
                : null,
            ByteLength = byteLength,
            IsTruncated = decompressed.Contains("... [TRUNCATED]"),
            Message = "Successfully decoded audit payload"
        };
    }
}

public class DecodeAuditPayloadRequest
{
    public string HexString { get; set; } = string.Empty;
    public string? Mode { get; set; } = null; // Optional parameter
}

public class DecodeAuditPayloadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public JsonElement? CleanData { get; set; }
    public string? DataType { get; set; }
    public string? FormattedJson { get; set; }
    public bool IsValidJson { get; set; }
    public bool IsTruncated { get; set; }
    public int ByteLength { get; set; }
    public string? Error { get; set; }
}
