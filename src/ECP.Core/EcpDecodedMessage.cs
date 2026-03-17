// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Envelope;
using ECP.Core.Token;

namespace ECP.Core;

/// <summary>
/// Kind of decoded ECP message.
/// </summary>
public enum EcpMessageKind : byte
{
    /// <summary>Unknown format.</summary>
    Unknown = 0,
    /// <summary>UET token.</summary>
    Uet = 1,
    /// <summary>Envelope message.</summary>
    Envelope = 2
}

/// <summary>
/// Result of decoding raw ECP bytes.
/// </summary>
public readonly record struct EcpDecodedMessage(
    EcpMessageKind Kind,
    UniversalEmergencyToken Token,
    EmergencyEnvelope Envelope)
{
    /// <summary>True when the decoded message is a UET token.</summary>
    public bool IsUet => Kind == EcpMessageKind.Uet;
    /// <summary>True when the decoded message is an envelope.</summary>
    public bool IsEnvelope => Kind == EcpMessageKind.Envelope;

    /// <summary>Creates a decoded message for a UET token.</summary>
    public static EcpDecodedMessage FromToken(UniversalEmergencyToken token)
    {
        return new EcpDecodedMessage(EcpMessageKind.Uet, token, default);
    }

    /// <summary>Creates a decoded message for an envelope.</summary>
    public static EcpDecodedMessage FromEnvelope(EmergencyEnvelope envelope)
    {
        return new EcpDecodedMessage(EcpMessageKind.Envelope, default, envelope);
    }

    /// <summary>Readable representation for debugging.</summary>
    public override string ToString()
    {
        return Kind switch
        {
            EcpMessageKind.Uet => $"UET({Token.EmergencyType}, {Token.Priority})",
            EcpMessageKind.Envelope => $"Envelope({Envelope.PayloadType}, {Envelope.PayloadLength} bytes)",
            _ => "Unknown"
        };
    }
}
