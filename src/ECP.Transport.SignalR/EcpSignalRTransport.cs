// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Threading.Channels;
using System.Runtime.InteropServices;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Negotiation;
using ECP.Core.Security;
using ECP.Core.Tenancy;
using ECP.Transport.Abstractions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace ECP.Transport.SignalR;

/// <summary>
/// SignalR transport implementation for ECP.
/// </summary>
public sealed class EcpSignalRTransport : IEcpTransport
{
    private static readonly Action<ILogger, Exception?> LogHandshakeFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1, nameof(LogHandshakeFailed)),
            "Handshake failed after reconnection.");

    private readonly EcpSignalROptions _options;
    private readonly EcpOptions _ecpOptions;
    private readonly IKeyProvider _keyProvider;
    private readonly string _tenantId;
    private readonly ILogger? _logger;
    private readonly Channel<byte[]> _channel;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private HubConnection? _connection;
    private EcpStreamDecoder? _decoder;
    private TaskCompletionSource<CapabilityNegotiationResult>? _handshakeTcs;
    private CapabilityHandshake? _handshake;
    private bool _disposed;

    /// <summary>
    /// Latest negotiated capabilities (when available).
    /// </summary>
    public CapabilityNegotiationResult? NegotiatedCapabilities { get; private set; }

    /// <inheritdoc />
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;

    /// <summary>
    /// Creates a SignalR transport with required dependencies.
    /// </summary>
    public EcpSignalRTransport(
        EcpSignalROptions options,
        EcpOptions ecpOptions,
        IKeyProvider keyProvider,
        ITenantContext? tenantContext = null,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ecpOptions = ecpOptions ?? throw new ArgumentNullException(nameof(ecpOptions));
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _tenantId = tenantContext?.TenantId ?? ecpOptions.DefaultTenantId;
        _logger = loggerFactory?.CreateLogger<EcpSignalRTransport>();
        _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = true });
    }

    /// <inheritdoc />
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public async Task ConnectAsync(string endpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint must be provided.", nameof(endpoint));
        }

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                return;
            }

            if (!EcpTransportHelper.TryGetKey(_keyProvider, _tenantId, _ecpOptions.KeyVersion, out var key))
            {
                throw new InvalidOperationException("HMAC key not available for transport handshake.");
            }

            _decoder = new EcpStreamDecoder(key.Span, _ecpOptions.HmacLength);
            _handshake = new CapabilityHandshake(_options.MinVersion, _options.MaxVersion, _options.Capabilities);
            _handshakeTcs = EcpTransportHelper.CreateHandshakeTcs();

            _connection = new HubConnectionBuilder()
                .WithUrl(endpoint)
                .WithAutomaticReconnect(_options.ReconnectDelays)
                .Build();

            _connection.On<byte[]>(_options.ReceiveMethodName, HandleIncomingBytesAsync);
            _connection.Reconnected += async _ =>
            {
                try
                {
                    _handshakeTcs = EcpTransportHelper.CreateHandshakeTcs();
                    await PerformHandshakeAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (_logger is not null)
                    {
                        LogHandshakeFailed(_logger, ex);
                    }
                }
            };

            await _connection.StartAsync(ct).ConfigureAwait(false);
            await PerformHandshakeAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_connection is null || !IsConnected)
        {
            throw new InvalidOperationException("SignalR connection is not active.");
        }

        if (!MemoryMarshal.TryGetArray(data, out var segment) || segment.Array is null)
        {
            await _connection.SendAsync(_options.SendMethodName, data.ToArray(), ct).ConfigureAwait(false);
            return;
        }

        if (segment.Offset != 0 || segment.Count != segment.Array.Length)
        {
            await _connection.SendAsync(_options.SendMethodName, data.ToArray(), ct).ConfigureAwait(false);
            return;
        }

        await _connection.SendAsync(_options.SendMethodName, segment.Array, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> ReceiveAsync(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.StopAsync(ct).ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        _connection = null;
        _decoder = null;
        NegotiatedCapabilities = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync().ConfigureAwait(false);
        _connectLock.Dispose();
    }

    private async Task PerformHandshakeAsync(CancellationToken ct)
    {
        if (_handshake is null)
        {
            throw new InvalidOperationException("Handshake not initialized.");
        }

        _handshakeTcs = EcpTransportHelper.CreateHandshakeTcs();
        if (!EcpTransportHelper.TryGetKey(_keyProvider, _tenantId, _ecpOptions.KeyVersion, out var key))
        {
            throw new InvalidOperationException("HMAC key not available for transport handshake.");
        }

        var offer = _handshake.CreateOffer();
        var envelope = EcpTransportHelper.BuildHandshakeEnvelope(offer, _ecpOptions, key.Span);
        await SendAsync(envelope.ToBytes(), ct).ConfigureAwait(false);

        using var timeoutCts = new CancellationTokenSource(_options.HandshakeTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var handshakeTcs = _handshakeTcs ?? throw new InvalidOperationException("Handshake not initialized.");
        var completed = await Task.WhenAny(handshakeTcs.Task, Task.Delay(Timeout.Infinite, linked.Token)).ConfigureAwait(false);
        if (completed != handshakeTcs.Task)
        {
            throw new TimeoutException("Capability negotiation timed out.");
        }

        NegotiatedCapabilities = await handshakeTcs.Task.ConfigureAwait(false);
    }

    private async Task HandleIncomingBytesAsync(byte[] data)
    {
        if (_decoder is null)
        {
            return;
        }

        if (_decoder.TryFeed(data, out var envelope))
        {
            await HandleEnvelopeAsync(envelope).ConfigureAwait(false);
            while (_decoder.TryFeed(ReadOnlySpan<byte>.Empty, out envelope))
            {
                await HandleEnvelopeAsync(envelope).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleEnvelopeAsync(EmergencyEnvelope envelope)
    {
        var peerId = _connection?.ConnectionId ?? _tenantId;
        if (EcpTransportHelper.TryHandleCapabilityNegotiation(
            envelope,
            _handshake,
            peerId,
            _handshakeTcs,
            out var negotiated))
        {
            if (negotiated.HasValue)
            {
                NegotiatedCapabilities = negotiated.Value;
            }

            return;
        }

        var bytes = envelope.ToBytes();
        _channel.Writer.TryWrite(bytes);
        await EcpTransportHelper.InvokeHandlersAsync(OnMessageReceived, bytes).ConfigureAwait(false);
    }
}
