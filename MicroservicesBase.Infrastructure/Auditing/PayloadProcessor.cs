using MicroservicesBase.Core.Configuration;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MicroservicesBase.Infrastructure.Auditing;

/// <summary>
/// Handles payload compression, masking, and size limiting for audit logs
/// </summary>
internal static class PayloadProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Process payload: mask PII → compress → enforce size limit
    /// </summary>
    public static byte[]? ProcessPayload(object? payload, PayloadSettings settings)
    {
        if (payload == null)
            return null;

        try
        {
            // 1. Serialize to JSON
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            
            // 2. Mask PII fields
            json = MaskPII(json, settings.MaskFields);
            
            // 3. Compress if enabled
            var data = settings.Compress 
                ? CompressString(json) 
                : Encoding.UTF8.GetBytes(json);
            
            // 4. Enforce size limit
            var maxSizeBytes = settings.MaxSizeKB * 1024;
            if (data.Length > maxSizeBytes)
            {
                // Truncate and add marker
                var truncated = new byte[maxSizeBytes];
                Array.Copy(data, truncated, maxSizeBytes);
                return truncated;
            }
            
            return data;
        }
        catch
        {
            // If processing fails, return null (don't break audit logging)
            return null;
        }
    }

    /// <summary>
    /// Compress string using Gzip
    /// </summary>
    public static byte[] CompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompress Gzip bytes to string (for reading audit logs)
    /// </summary>
    public static string DecompressString(byte[] compressedData)
    {
        using var inputStream = new MemoryStream(compressedData);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        
        gzipStream.CopyTo(outputStream);
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    /// <summary>
    /// Mask PII fields in JSON string
    /// </summary>
    private static string MaskPII(string json, List<string> fieldsToMask)
    {
        if (fieldsToMask == null || fieldsToMask.Count == 0)
            return json;

        foreach (var field in fieldsToMask)
        {
            json = MaskField(json, field);
        }

        return json;
    }

    /// <summary>
    /// Mask a specific field in JSON
    /// </summary>
    private static string MaskField(string json, string fieldName)
    {
        // Pattern: "fieldName":"value" or "fieldName":value
        var pattern = $"\"{fieldName}\"\\s*:\\s*\"([^\"]+)\"";
        
        return Regex.Replace(json, pattern, match =>
        {
            var value = match.Groups[1].Value;
            var masked = MaskValue(value, fieldName);
            return $"\"{fieldName}\":\"{masked}\"";
        }, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Mask a value based on field type
    /// </summary>
    private static string MaskValue(string value, string fieldName)
    {
        var lower = fieldName.ToLowerInvariant();

        // Email masking: j***@example.com
        if (lower.Contains("email"))
        {
            var atIndex = value.IndexOf('@');
            if (atIndex > 0)
            {
                var localPart = value.Substring(0, atIndex);
                var domain = value.Substring(atIndex);
                return localPart.Length > 1 
                    ? $"{localPart[0]}***{domain}" 
                    : $"***{domain}";
            }
        }

        // Phone masking: 555-***-1234
        if (lower.Contains("phone"))
        {
            if (value.Length >= 10)
            {
                var lastFour = value.Substring(value.Length - 4);
                return $"***-***-{lastFour}";
            }
        }

        // Card number masking: ****-****-****-1234
        if (lower.Contains("card") || lower.Contains("pan"))
        {
            if (value.Length >= 4)
            {
                var lastFour = value.Substring(value.Length - 4);
                return $"****-****-****-{lastFour}";
            }
        }

        // Default: mask middle characters
        if (value.Length <= 4)
            return "****";

        var prefix = value.Substring(0, 2);
        var suffix = value.Substring(value.Length - 2);
        return $"{prefix}***{suffix}";
    }
}

