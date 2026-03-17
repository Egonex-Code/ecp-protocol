// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Transport.Abstractions;

namespace ECP.Transport.WebSocket;

/// <summary>
/// Configuration options for the WebSocket transport.
/// </summary>
public sealed class EcpWebSocketOptions : EcpTransportOptions
{
    /// <summary>Keep-alive interval.</summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(20);
    /// <summary>Receive buffer size.</summary>
    public int ReceiveBufferSize { get; set; } = 8 * 1024;
    /// <summary>Send buffer size.</summary>
    public int SendBufferSize { get; set; } = 8 * 1024;
    /// <summary>Max reconnect attempts.</summary>
    public int MaxReconnectAttempts { get; set; } = 5;
}
