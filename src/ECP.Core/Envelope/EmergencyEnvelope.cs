// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using System.Security.Cryptography;
using ECP.Core.Models;
using ECP.Core.Security;

namespace ECP.Core.Envelope;

/// <summary>
/// Emergency Envelope with fixed header, payload, and truncated HMAC.
/// </summary>
public readonly struct EmergencyEnvelope
{
    /// <summary>Magic number for ECP envelopes.</summary>
    public const ushort Magic = 0xEC50;
    /// <summary>Envelope version.</summary>
    public const byte Version = 0x01;
    /// <summary>Header size in bytes.</summary>
    public const int HeaderSize = 22;
    /// <summary>Default HMAC length in bytes.</summary>
    public const int DefaultHmacLength = EcpSecurity.DefaultHmacLength;

    private const int MagicOffset = 0;
    private const int VersionOffset = 2;
    private const int FlagsOffset = 3;
    private const int PriorityOffset = 4;
    private const int TtlOffset = 5;
    private const int KeyVersionOffset = 6;
    private const int MessageIdOffset = 7;
    private const int TimestampOffset = 15;
    private const int PayloadTypeOffset = 19;
    private const int PayloadLengthOffset = 20;

    internal EmergencyEnvelope(
        EcpFlags flags,
        EcpPriority priority,
        byte ttl,
        byte keyVersion,
        ulong messageId,
        uint timestamp,
        EcpPayloadType payloadType,
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> hmac,
        bool isValid)
    {
        Flags = flags;
        Priority = priority;
        Ttl = ttl;
        KeyVersion = keyVersion;
        MessageId = messageId;
        Timestamp = timestamp;
        PayloadType = payloadType;
        Payload = payload;
        Hmac = hmac;
        IsValid = isValid;
    }

    /// <summary>Envelope flags.</summary>
    public EcpFlags Flags { get; }
    /// <summary>Priority of the envelope.</summary>
    public EcpPriority Priority { get; }
    /// <summary>Time-to-live in seconds.</summary>
    public byte Ttl { get; }
    /// <summary>Key version used for HMAC rotation.</summary>
    public byte KeyVersion { get; }
    /// <summary>64-bit message identifier.</summary>
    public ulong MessageId { get; }
    /// <summary>Unix timestamp in seconds.</summary>
    public uint Timestamp { get; }
    /// <summary>Payload type.</summary>
    public EcpPayloadType PayloadType { get; }
    /// <summary>Payload bytes.</summary>
    public ReadOnlyMemory<byte> Payload { get; }
    /// <summary>Truncated HMAC bytes.</summary>
    public ReadOnlyMemory<byte> Hmac { get; }
    /// <summary>True when HMAC is verified with a provided key.</summary>
    public bool IsValid { get; }
    /// <summary>Payload length in bytes.</summary>
    public ushort PayloadLength => (ushort)Payload.Length;
    /// <summary>Total envelope length in bytes.</summary>
    public int TotalLength => HeaderSize + Payload.Length + Hmac.Length;

    /// <summary>
    /// Serializes the envelope to a byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[TotalLength];
        WriteTo(bytes);
        return bytes;
    }

    /// <summary>
    /// Writes the envelope to the destination buffer.
    /// </summary>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < TotalLength)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        WriteHeader(destination);
        Payload.Span.CopyTo(destination.Slice(HeaderSize, Payload.Length));
        Hmac.Span.CopyTo(destination.Slice(HeaderSize + Payload.Length, Hmac.Length));
    }

    /// <summary>
    /// Attempts to write the envelope to the destination buffer.
    /// </summary>
    public bool TryWriteTo(Span<byte> destination)
    {
        if (destination.Length < TotalLength)
        {
            return false;
        }

        WriteTo(destination);
        return true;
    }

    /// <summary>
    /// Decodes an envelope from raw bytes without verifying the HMAC.
    /// </summary>
    public static EmergencyEnvelope Decode(ReadOnlySpan<byte> bytes, int hmacLength = DefaultHmacLength)
    {
        return Decode(bytes, ReadOnlySpan<byte>.Empty, hmacLength);
    }

    /// <summary>
    /// Decodes an envelope from raw bytes and verifies the HMAC when a key is provided.
    /// </summary>
    public static EmergencyEnvelope Decode(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> hmacKey, int hmacLength = DefaultHmacLength)
    {
        if (bytes.Length < HeaderSize + hmacLength)
        {
            throw new EcpDecodeException($"Envelope length is too small. Expected at least {HeaderSize + hmacLength} bytes, got {bytes.Length}.");
        }

        var magic = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(MagicOffset, 2));
        if (magic != Magic)
        {
            throw new EcpDecodeException($"Expected magic number 0x{Magic:X4} at offset 0, found 0x{magic:X4}.");
        }

        var version = bytes[VersionOffset];
        if (version != Version)
        {
            throw new EcpDecodeException($"Unsupported envelope version {version}.");
        }

        var flags = (EcpFlags)bytes[FlagsOffset];
        var priority = (EcpPriority)bytes[PriorityOffset];
        var ttl = bytes[TtlOffset];
        var keyVersion = bytes[KeyVersionOffset];
        var messageId = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(MessageIdOffset, 8));
        var timestamp = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(TimestampOffset, 4));
        var payloadType = (EcpPayloadType)bytes[PayloadTypeOffset];
        var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(PayloadLengthOffset, 2));

        var expectedLength = HeaderSize + payloadLength + hmacLength;
        if (bytes.Length != expectedLength)
        {
            throw new EcpDecodeException($"Invalid envelope length. Expected {expectedLength} bytes, got {bytes.Length}.");
        }

        var payloadSlice = bytes.Slice(HeaderSize, payloadLength);
        var hmacSlice = bytes.Slice(HeaderSize + payloadLength, hmacLength);

        var payload = payloadSlice.ToArray();
        var hmac = hmacSlice.ToArray();

        var isValid = hmacLength == 0;
        if (!hmacKey.IsEmpty && hmacLength != 0)
        {
            var data = bytes.Slice(0, HeaderSize + payloadLength);
            isValid = EcpSecurity.VerifyHmac(hmacKey, data, hmac);
        }

        return new EmergencyEnvelope(flags, priority, ttl, keyVersion, messageId, timestamp, payloadType, payload, hmac, isValid);
    }

    /// <summary>
    /// Attempts to decode an envelope without throwing.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> bytes, out EmergencyEnvelope envelope, int hmacLength = DefaultHmacLength)
    {
        return TryDecode(bytes, ReadOnlySpan<byte>.Empty, out envelope, hmacLength);
    }

    /// <summary>
    /// Attempts to decode an envelope and verify the HMAC when a key is provided.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> hmacKey, out EmergencyEnvelope envelope, int hmacLength = DefaultHmacLength)
    {
        try
        {
            envelope = Decode(bytes, hmacKey, hmacLength);
            return true;
        }
        catch (EcpDecodeException)
        {
            envelope = default;
            return false;
        }
    }

    /// <summary>
    /// Decodes an envelope view from raw bytes without copying the payload/HMAC.
    /// </summary>
    public static EmergencyEnvelopeView DecodeView(ReadOnlyMemory<byte> bytes, int hmacLength = DefaultHmacLength)
    {
        return DecodeView(bytes, ReadOnlySpan<byte>.Empty, hmacLength);
    }

    /// <summary>
    /// Decodes an envelope view and verifies the HMAC when a key is provided.
    /// </summary>
    public static EmergencyEnvelopeView DecodeView(ReadOnlyMemory<byte> bytes, ReadOnlySpan<byte> hmacKey, int hmacLength = DefaultHmacLength)
    {
        var span = bytes.Span;
        if (span.Length < HeaderSize + hmacLength)
        {
            throw new EcpDecodeException($"Envelope length is too small. Expected at least {HeaderSize + hmacLength} bytes, got {span.Length}.");
        }

        var magic = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(MagicOffset, 2));
        if (magic != Magic)
        {
            throw new EcpDecodeException($"Expected magic number 0x{Magic:X4} at offset 0, found 0x{magic:X4}.");
        }

        var version = span[VersionOffset];
        if (version != Version)
        {
            throw new EcpDecodeException($"Unsupported envelope version {version}.");
        }

        var flags = (EcpFlags)span[FlagsOffset];
        var priority = (EcpPriority)span[PriorityOffset];
        var ttl = span[TtlOffset];
        var keyVersion = span[KeyVersionOffset];
        var messageId = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(MessageIdOffset, 8));
        var timestamp = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(TimestampOffset, 4));
        var payloadType = (EcpPayloadType)span[PayloadTypeOffset];
        var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(PayloadLengthOffset, 2));

        var expectedLength = HeaderSize + payloadLength + hmacLength;
        if (span.Length != expectedLength)
        {
            throw new EcpDecodeException($"Invalid envelope length. Expected {expectedLength} bytes, got {span.Length}.");
        }

        var payload = bytes.Slice(HeaderSize, payloadLength);
        var hmac = bytes.Slice(HeaderSize + payloadLength, hmacLength);

        var isValid = hmacLength == 0;
        if (!hmacKey.IsEmpty && hmacLength != 0)
        {
            var data = span.Slice(0, HeaderSize + payloadLength);
            isValid = EcpSecurity.VerifyHmac(hmacKey, data, hmac.Span);
        }

        return new EmergencyEnvelopeView(flags, priority, ttl, keyVersion, messageId, timestamp, payloadType, payload, hmac, isValid);
    }

    /// <summary>
    /// Attempts to decode an envelope view without throwing.
    /// </summary>
    public static bool TryDecodeView(ReadOnlyMemory<byte> bytes, out EmergencyEnvelopeView envelope, int hmacLength = DefaultHmacLength)
    {
        return TryDecodeView(bytes, ReadOnlySpan<byte>.Empty, out envelope, hmacLength);
    }

    /// <summary>
    /// Attempts to decode an envelope view and verify the HMAC when a key is provided.
    /// </summary>
    public static bool TryDecodeView(ReadOnlyMemory<byte> bytes, ReadOnlySpan<byte> hmacKey, out EmergencyEnvelopeView envelope, int hmacLength = DefaultHmacLength)
    {
        try
        {
            envelope = DecodeView(bytes, hmacKey, hmacLength);
            return true;
        }
        catch (EcpDecodeException)
        {
            envelope = default;
            return false;
        }
    }

    internal static byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, int hmacLength)
    {
        return EcpSecurity.ComputeHmac(key, data, hmacLength);
    }

    internal static ulong GenerateMessageId()
    {
        Span<byte> buffer = stackalloc byte[8];
        RandomNumberGenerator.Fill(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    internal void WriteHeader(Span<byte> destination)
    {
        if (destination.Length < HeaderSize)
        {
            throw new ArgumentException("Destination buffer is too small for the envelope header.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(MagicOffset, 2), Magic);
        destination[VersionOffset] = Version;
        destination[FlagsOffset] = (byte)Flags;
        destination[PriorityOffset] = (byte)Priority;
        destination[TtlOffset] = Ttl;
        destination[KeyVersionOffset] = KeyVersion;
        BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(MessageIdOffset, 8), MessageId);
        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(TimestampOffset, 4), Timestamp);
        destination[PayloadTypeOffset] = (byte)PayloadType;
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(PayloadLengthOffset, 2), PayloadLength);
    }

    internal static bool HasMagic(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 2 && bytes[0] == 0xEC && bytes[1] == 0x50;
    }
}
