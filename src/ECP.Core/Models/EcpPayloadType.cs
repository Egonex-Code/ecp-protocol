// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Models;

/// <summary>
/// Payload type encoded in the Emergency Envelope header.
/// </summary>
public enum EcpPayloadType : byte
{
    /// <summary>Emergency alert payload.</summary>
    Alert = 0,
    /// <summary>Confirmation payload.</summary>
    Confirmation = 1,
    /// <summary>Ping payload.</summary>
    Ping = 2,
    /// <summary>Cascade control payload.</summary>
    Cascade = 3,
    /// <summary>Capability negotiation payload.</summary>
    CapabilityNegotiation = 4,
    /// <summary>Key rotation payload.</summary>
    KeyRotation = 5
}
