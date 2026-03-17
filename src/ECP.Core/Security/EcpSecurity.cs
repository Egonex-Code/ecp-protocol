// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using System.Security.Cryptography;
using ECP.Core.Models;

namespace ECP.Core.Security;

/// <summary>
/// Cryptographic helpers for ECP.
/// </summary>
public static class EcpSecurity
{
    /// <summary>Default HMAC length in bytes.</summary>
    public const int DefaultHmacLength = 12;
    private const int FullHmacLength = 32;

    /// <summary>
    /// Computes a truncated HMAC-SHA256.
    /// </summary>
    public static byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, int hmacLength = DefaultHmacLength)
    {
        ValidateHmacLength(hmacLength);

        if (hmacLength == 0)
        {
            return Array.Empty<byte>();
        }

        Span<byte> full = stackalloc byte[FullHmacLength];
        if (!HMACSHA256.TryHashData(key, data, full, out var bytesWritten) || bytesWritten != FullHmacLength)
        {
            throw new CryptographicException("Failed to compute HMAC.");
        }

        var truncated = new byte[hmacLength];
        full.Slice(0, hmacLength).CopyTo(truncated);
        return truncated;
    }

    /// <summary>
    /// Verifies a truncated HMAC-SHA256 using a timing-safe comparison.
    /// </summary>
    public static bool VerifyHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> hmac)
    {
        ValidateHmacLength(hmac.Length);

        if (hmac.Length == 0)
        {
            return true;
        }

        Span<byte> full = stackalloc byte[FullHmacLength];
        if (!HMACSHA256.TryHashData(key, data, full, out var bytesWritten) || bytesWritten != FullHmacLength)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(full.Slice(0, hmac.Length), hmac);
    }

    /// <summary>
    /// Computes the 18-bit ConfirmHash used inside the UET.
    /// </summary>
    public static uint ComputeConfirmHash(
        ReadOnlySpan<byte> keyConfirm,
        ulong messageId,
        ushort timestampMinutes,
        ushort zoneHash,
        EmergencyType emergencyType,
        EcpPriority priority,
        ActionFlags actionFlags)
    {
        Span<byte> buffer = stackalloc byte[15];
        BinaryPrimitives.WriteUInt64BigEndian(buffer.Slice(0, 8), messageId);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(8, 2), timestampMinutes);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(10, 2), zoneHash);
        buffer[12] = (byte)emergencyType;
        buffer[13] = (byte)priority;
        buffer[14] = (byte)actionFlags;

        var hash = HMACSHA256.HashData(keyConfirm, buffer);
        var last24 = (uint)((hash[^3] << 16) | (hash[^2] << 8) | hash[^1]);
        return last24 & 0x3FFFF;
    }

    private static void ValidateHmacLength(int hmacLength)
    {
        if (hmacLength != 0 && (hmacLength < 8 || hmacLength > 16))
        {
            throw new ArgumentOutOfRangeException(nameof(hmacLength), "HMAC length must be 0 or between 8 and 16 bytes.");
        }
    }
}
