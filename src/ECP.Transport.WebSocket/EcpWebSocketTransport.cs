// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Net.WebSockets;
using System.Threading.Channels;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Negotiation;
using ECP.Core.Security;
using ECP.Core.Tenancy;
using ECP.Transport.Abstractions;
using Microsoft.Extensions.Logging;

namespace ECP.Transport.WebSocket;

/// <summary>
/// WebSocket transport implementation for ECP.
/// </summary>
public sealed class EcpWebSocketTransport : IEcpTransport
{
    private static readonly Action<ILogger, Exception?> LogWebSocketReceiveFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1, nameof(LogWebSocketReceiveFailed)),
            "WebSocket receive failed.");

    private static readonly Action<ILogger, int, Exception?> LogReconnectAttemptFailed =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(2, nameof(LogReconnectAttemptFailed)),
            "Reconnect attempt {Attempt} failed.");

    private readonly EcpWebSocketOptions _options;
    private readonly EcpOptions _ecpOptions;
    private readonly IKeyProvider _keyProvider;
    private readonly string _tenantId;
    private readonly ILogger? _logger;
    private readonly Channel<byte[]> _channel;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private ClientWebSocket? _socket;
    private EcpStreamDecoder? _decoder;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private TaskCompletionSource<CapabilityNegotiationResult>? _handshakeTcs;
    private CapabilityHandshake? _handshake;
    private string? _endpoint;
    private bool _disposed;

    /// <summary>
    /// Latest negotiated capabilities (when available).
    /// </summary>
    public CapabilityNegotiationResult? NegotiatedCapabilities { get; private set; }

    /// <inheritdoc />
    public event Func<ReadOnlyMemory<byte>, Task>? OnMessageReceived;

    /// <summary>
    /// Creates a WebSocket transport with required dependencies.
    /// </summary>
    public EcpWebSocketTransport(
        EcpWebSocketOptions options,
        EcpOptions ecpOptions,
        IKeyProvider keyProvider,
        ITenantContext? tenantContext = null,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ecpOptions = ecpOptions ?? throw new ArgumentNullException(nameof(ecpOptions));
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _tenantId = tenantContext?.TenantId ?? ecpOptions.DefaultTenantId;
        _logger = loggerFactory?.CreateLogger<EcpWebSocketTransport>();
        _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleWriter = true });
    }

    /// <inheritdoc />
    public bool IsConnected => _socket?.State == WebSocketState.Open;

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

            _endpoint = endpoint;
            await CreateAndConnectSocketAsync(endpoint, ct).ConfigureAwait(false);
            EnsureReceiveLoop();
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
        if (!IsConnected || _socket is null)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        await _socket.SendAsync(data, WebSocketMessageType.Binary, true, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> ReceiveAsync(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_socket is null)
        {
            return;
        }

        _receiveCts?.Cancel();
        if (_receiveTask is not null)
        {
            await _receiveTask.ConfigureAwait(false);
        }

        _receiveTask = null;
        _receiveCts?.Dispose();
        _receiveCts = null;

        if (_socket.State == WebSocketState.Open)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", ct).ConfigureAwait(false);
        }

        _socket.Dispose();
        _socket = null;
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
        _receiveCts?.Dispose();
        _connectLock.Dispose();
    }

    private async Task CreateAndConnectSocketAsync(string endpoint, CancellationToken ct)
    {
        _socket?.Dispose();
        _socket = new ClientWebSocket
        {
            Options = { KeepAliveInterval = _options.KeepAliveInterval }
        };
        _socket.Options.SetBuffer(_options.ReceiveBufferSize, _options.SendBufferSize);

        if (!EcpTransportHelper.TryGetKey(_keyProvider, _tenantId, _ecpOptions.KeyVersion, out var key))
        {
            throw new InvalidOperationException("HMAC key not available for transport handshake.");
        }

        _decoder = new EcpStreamDecoder(key.Span, _ecpOptions.HmacLength);
        _handshake = new CapabilityHandshake(_options.MinVersion, _options.MaxVersion, _options.Capabilities);
        _handshakeTcs = EcpTransportHelper.CreateHandshakeTcs();

        await _socket.ConnectAsync(new Uri(endpoint), ct).ConfigureAwait(false);
    }

    private async Task PerformHandshakeAsync(CancellationToken ct)
    {
        await SendHandshakeOfferAsync(ct).ConfigureAwait(false);

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

    private async Task SendHandshakeOfferAsync(CancellationToken ct)
    {
        if (_handshake is null)
        {
            throw new InvalidOperationException("Handshake not initialized.");
        }

        if (!EcpTransportHelper.TryGetKey(_keyProvider, _tenantId, _ecpOptions.KeyVersion, out var key))
        {
            throw new InvalidOperationException("HMAC key not available for transport handshake.");
        }

        var offer = _handshake.CreateOffer();
        var envelope = EcpTransportHelper.BuildHandshakeEnvelope(offer, _ecpOptions, key.Span);
        await SendAsync(envelope.ToBytes(), ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        if (_socket is null || _decoder is null)
        {
            return;
        }

        var buffer = new byte[_options.ReceiveBufferSize];
        while (!ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                if (_logger is not null)
                {
                    LogWebSocketReceiveFailed(_logger, ex);
                }

                if (!await TryReconnectAsync(ct).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (!await TryReconnectAsync(ct).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            await FeedDecoderAsync(buffer, result.Count).ConfigureAwait(false);
        }
    }

    private async Task HandleEnvelopeAsync(EmergencyEnvelope envelope)
    {
        var peerId = _endpoint ?? _tenantId;
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

    private async Task<bool> TryReconnectAsync(CancellationToken ct)
    {
        if (_endpoint is null)
        {
            return false;
        }

        var attempts = 0;
        while (attempts < _options.MaxReconnectAttempts && !ct.IsCancellationRequested)
        {
            var delay = GetReconnectDelay(attempts);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            try
            {
                await CreateAndConnectSocketAsync(_endpoint, ct).ConfigureAwait(false);
                await PerformHandshakeAsync(ct).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                if (_logger is not null)
                {
                    LogReconnectAttemptFailed(_logger, attempts + 1, ex);
                }

                attempts++;
            }
        }

        return false;
    }

    private TimeSpan GetReconnectDelay(int attempt)
    {
        if (_options.ReconnectDelays.Length == 0)
        {
            return TimeSpan.Zero;
        }

        var index = Math.Min(attempt, _options.ReconnectDelays.Length - 1);
        return _options.ReconnectDelays[index];
    }

    private async Task FeedDecoderAsync(byte[] buffer, int count)
    {
        if (_decoder is null)
        {
            return;
        }

        if (_decoder.TryFeed(buffer.AsSpan(0, count), out var envelope))
        {
            await HandleEnvelopeAsync(envelope).ConfigureAwait(false);
            await DrainDecoderAsync().ConfigureAwait(false);
        }
    }

    private async Task DrainDecoderAsync()
    {
        if (_decoder is null)
        {
            return;
        }

        while (_decoder.TryFeed(ReadOnlySpan<byte>.Empty, out var envelope))
        {
            await HandleEnvelopeAsync(envelope).ConfigureAwait(false);
        }
    }

    private void EnsureReceiveLoop()
    {
        if (_receiveTask is { IsCompleted: false })
        {
            return;
        }

        _receiveCts?.Dispose();
        _receiveCts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
    }
}
