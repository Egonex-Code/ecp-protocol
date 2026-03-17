// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Transport.Abstractions;

/// <summary>
/// Transport interface for sending and receiving raw ECP bytes.
/// </summary>
public interface IEcpTransport : IAsyncDisposable
{
    /// <summary>
    /// Sends raw bytes over the transport.
    /// </summary>
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Receives raw bytes from the transport.
    /// </summary>
    Task<byte[]> ReceiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Connects the transport to a remote endpoint.
    /// </summary>
    Task ConnectAsync(string endpoint, CancellationToken ct = default);

    /// <summary>
    /// Disconnects the transport.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Indicates whether the transport is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Raised when raw bytes are received.
    /// </summary>
    event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;
}
