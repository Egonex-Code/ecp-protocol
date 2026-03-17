// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Models;

namespace ECP.Core.Envelope;

/// <summary>
/// Zero-copy view over an encoded emergency envelope.
/// </summary>
public readonly struct EmergencyEnvelopeView
{
    internal EmergencyEnvelopeView(
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
    /// <summary>Payload bytes (slice of the original buffer).</summary>
    public ReadOnlyMemory<byte> Payload { get; }
    /// <summary>Truncated HMAC bytes (slice of the original buffer).</summary>
    public ReadOnlyMemory<byte> Hmac { get; }
    /// <summary>True when HMAC is verified with a provided key.</summary>
    public bool IsValid { get; }
    /// <summary>Payload length in bytes.</summary>
    public ushort PayloadLength => (ushort)Payload.Length;
    /// <summary>Total envelope length in bytes.</summary>
    public int TotalLength => EmergencyEnvelope.HeaderSize + Payload.Length + Hmac.Length;
}
