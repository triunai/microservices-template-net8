using System;
using System.Security.Cryptography;

namespace Rgt.Space.Core.Utilities;

public static class Uuid7
{
    /// <summary>
    /// Generates a new UUIDv7 (time-ordered).
    /// </summary>
    public static Guid NewUuid7()
    {
        // UUIDv7 format:
        // 0                   1                   2                   3
        // 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        // |                           unix_ts_ms                          |
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        // |          unix_ts_ms           |  ver  |       rand_a          |
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        // |var|                        rand_b                             |
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        // |                            rand_b                             |
        // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Big Endian timestamp
        bytes[0] = (byte)((timestamp >> 40) & 0xFF);
        bytes[1] = (byte)((timestamp >> 32) & 0xFF);
        bytes[2] = (byte)((timestamp >> 24) & 0xFF);
        bytes[3] = (byte)((timestamp >> 16) & 0xFF);
        bytes[4] = (byte)((timestamp >> 8) & 0xFF);
        bytes[5] = (byte)(timestamp & 0xFF);

        // Version 7
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);

        // Variant 1 (0b10xxxxxx)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // Swap bytes for .NET Guid endianness (Little Endian for first 3 fields)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes, 0, 4); // Swap _a (int)
            Array.Reverse(bytes, 4, 2); // Swap _b (short)
            Array.Reverse(bytes, 6, 2); // Swap _c (short)
        }

        return new Guid(bytes);
    }
}
