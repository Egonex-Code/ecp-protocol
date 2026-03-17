// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Models;

namespace ECP.Transport.Abstractions;

/// <summary>
/// Shared configuration options for transports.
/// </summary>
public class EcpTransportOptions
{
    /// <summary>Handshake timeout.</summary>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Reconnect delays sequence.</summary>
    public TimeSpan[] ReconnectDelays { get; set; } =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];

    /// <summary>Minimum protocol version.</summary>
    public byte MinVersion { get; set; } = 1;

    /// <summary>Maximum protocol version.</summary>
    public byte MaxVersion { get; set; } = 1;

    /// <summary>Capability bitmap.</summary>
    public EcpCapabilities Capabilities { get; set; } = EcpCapabilities.None;
}
