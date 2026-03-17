// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Token;

namespace ECP.Core;

/// <summary>
/// Static facade for the ECP SDK (one-liner, token, and decode APIs).
/// </summary>
public static class Ecp
{
    /// <summary>
    /// Encodes a one-line emergency alert into an 8-byte UET.
    /// </summary>
    public static byte[] Alert(
        EmergencyType emergencyType,
        ushort zoneHash,
        EcpPriority priority = EcpPriority.Critical,
        ActionFlags actionFlags = ActionFlags.None,
        ushort? timestampMinutes = null,
        uint confirmHash = 0)
    {
        var token = Token(emergencyType, priority, actionFlags, zoneHash, timestampMinutes, confirmHash);
        return token.ToBytes();
    }

    /// <summary>
    /// Creates a UET token from the provided fields.
    /// </summary>
    public static UniversalEmergencyToken Token(
        EmergencyType emergencyType,
        EcpPriority priority,
        ActionFlags actionFlags = ActionFlags.None,
        ushort zoneHash = 0,
        ushort? timestampMinutes = null,
        uint confirmHash = 0)
    {
        var timestamp = timestampMinutes ?? GetCurrentTimestampMinutes();
        return UniversalEmergencyToken.Create(emergencyType, priority, actionFlags, zoneHash, timestamp, confirmHash);
    }

    /// <summary>
    /// Decodes a UET token from an 8-byte buffer.
    /// </summary>
    public static UniversalEmergencyToken DecodeToken(ReadOnlySpan<byte> bytes)
    {
        return UniversalEmergencyToken.FromBytes(bytes);
    }

    /// <summary>
    /// Tries to decode a UET token from an 8-byte buffer.
    /// </summary>
    public static bool TryDecodeToken(ReadOnlySpan<byte> bytes, out UniversalEmergencyToken token)
    {
        return UniversalEmergencyToken.TryFromBytes(bytes, out token);
    }

    /// <summary>
    /// Tries to decode raw bytes as ECP (UET or Envelope).
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> bytes, out EcpDecodedMessage message)
    {
        if (UniversalEmergencyToken.TryFromBytes(bytes, out var token))
        {
            message = EcpDecodedMessage.FromToken(token);
            return true;
        }

        if (EmergencyEnvelope.HasMagic(bytes) &&
            (EmergencyEnvelope.TryDecode(bytes, out var envelope) ||
             TryDecodeEnvelopeWithDetectedHmac(bytes, out envelope)))
        {
            message = EcpDecodedMessage.FromEnvelope(envelope);
            return true;
        }

        message = default;
        return false;
    }

    /// <summary>
    /// Decodes raw bytes as ECP and throws on invalid data.
    /// </summary>
    public static EcpDecodedMessage Decode(ReadOnlySpan<byte> bytes)
    {
        if (UniversalEmergencyToken.TryFromBytes(bytes, out var token))
        {
            return EcpDecodedMessage.FromToken(token);
        }

        if (EmergencyEnvelope.HasMagic(bytes))
        {
            if (EmergencyEnvelope.TryDecode(bytes, out var envelope) ||
                TryDecodeEnvelopeWithDetectedHmac(bytes, out envelope))
            {
                return EcpDecodedMessage.FromEnvelope(envelope);
            }

            throw new EcpDecodeException($"Invalid envelope length. Actual length: {bytes.Length}.");
        }

        throw new EcpDecodeException($"Expected 8-byte UET or envelope magic 0xEC50 at offset 0. Actual length: {bytes.Length}.");
    }

    /// <summary>
    /// Creates a new envelope builder.
    /// </summary>
    public static EnvelopeBuilder Envelope()
    {
        return new EnvelopeBuilder();
    }

    /// <summary>
    /// Decodes an envelope and verifies the HMAC using the provided key.
    /// </summary>
    public static EmergencyEnvelope DecodeEnvelope(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> hmacKey, int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        return EmergencyEnvelope.Decode(bytes, hmacKey, hmacLength);
    }

    /// <summary>
    /// Tries to decode an envelope and verify the HMAC using the provided key.
    /// </summary>
    public static bool TryDecodeEnvelope(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> hmacKey, out EmergencyEnvelope envelope, int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        return EmergencyEnvelope.TryDecode(bytes, hmacKey, out envelope, hmacLength);
    }

    /// <summary>
    /// Decodes an envelope view without copying payload/HMAC and verifies using the provided key.
    /// </summary>
    public static EmergencyEnvelopeView DecodeEnvelopeView(ReadOnlyMemory<byte> bytes, ReadOnlySpan<byte> hmacKey, int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        return EmergencyEnvelope.DecodeView(bytes, hmacKey, hmacLength);
    }

    /// <summary>
    /// Tries to decode an envelope view without copying payload/HMAC and verifies using the provided key.
    /// </summary>
    public static bool TryDecodeEnvelopeView(ReadOnlyMemory<byte> bytes, ReadOnlySpan<byte> hmacKey, out EmergencyEnvelopeView envelope, int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        return EmergencyEnvelope.TryDecodeView(bytes, hmacKey, out envelope, hmacLength);
    }

    private static ushort GetCurrentTimestampMinutes()
    {
        var minutes = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        return (ushort)(minutes & 0xFFFF);
    }

    private static bool TryDecodeEnvelopeWithDetectedHmac(ReadOnlySpan<byte> bytes, out EmergencyEnvelope envelope)
    {
        envelope = default;
        if (bytes.Length < EmergencyEnvelope.HeaderSize)
        {
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(20, 2));
        var hmacLength = bytes.Length - EmergencyEnvelope.HeaderSize - payloadLength;
        if (hmacLength < 0)
        {
            return false;
        }

        if (hmacLength != 0 && (hmacLength < 8 || hmacLength > 16))
        {
            return false;
        }

        return EmergencyEnvelope.TryDecode(bytes, out envelope, hmacLength);
    }
}
