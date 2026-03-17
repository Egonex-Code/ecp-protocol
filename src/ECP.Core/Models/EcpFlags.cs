// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Models;

/// <summary>
/// Envelope flags used in the Emergency Envelope header.
/// </summary>
[Flags]
public enum EcpFlags : byte
{
    /// <summary>No flags.</summary>
    None = 0,
    /// <summary>Delivery confirmation required.</summary>
    NeedsConfirmation = 1 << 0,
    /// <summary>Broadcast mode.</summary>
    Broadcast = 1 << 1,
    /// <summary>Payload is encrypted.</summary>
    Encrypted = 1 << 2,
    /// <summary>Payload is compressed.</summary>
    Compressed = 1 << 3,
    /// <summary>Enable cascade propagation.</summary>
    Cascade = 1 << 4,
    /// <summary>Retries are allowed.</summary>
    RetryAllowed = 1 << 5,
    // Bits 6-7: Reserved
}
