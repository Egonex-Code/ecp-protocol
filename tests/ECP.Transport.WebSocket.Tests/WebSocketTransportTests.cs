// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Negotiation;
using ECP.Core.Security;
using ECP.Transport.Abstractions;
using ECP.Transport.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ECP.Transport.WebSocket.Tests;

public class WebSocketTransportTests
{
    [Fact]
    public async Task ConnectAndDisconnect()
    {
        await using var server = await WebSocketTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport(server.Endpoint);
        await transport.ConnectAsync(server.Endpoint.ToString());

        Assert.True(transport.IsConnected);

        await transport.DisconnectAsync();
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public async Task HandshakeNegotiatesCapabilities()
    {
        await using var server = await WebSocketTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsTemplates);

        var transport = CreateTransport(server.Endpoint, EcpCapabilities.SupportsTemplates | EcpCapabilities.SupportsCascade);
        await transport.ConnectAsync(server.Endpoint.ToString());

        Assert.NotNull(transport.NegotiatedCapabilities);
        Assert.Equal(EcpCapabilities.SupportsTemplates, transport.NegotiatedCapabilities?.NegotiatedCapabilities);
    }

    [Fact]
    public async Task SendReceiveRoundtrip()
    {
        await using var server = await WebSocketTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport(server.Endpoint);
        await transport.ConnectAsync(server.Endpoint.ToString());

        var bytes = BuildEnvelope().ToBytes();
        await transport.SendAsync(bytes);

        var received = await transport.ReceiveAsync();
        Assert.Equal(bytes, received);
    }

    [Fact]
    public async Task OnMessageReceivedFires()
    {
        await using var server = await WebSocketTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport(server.Endpoint);
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.OnMessageReceived += data =>
        {
            tcs.TrySetResult(data.ToArray());
            return Task.CompletedTask;
        };

        await transport.ConnectAsync(server.Endpoint.ToString());
        await transport.SendAsync(BuildEnvelope().ToBytes());

        var received = await tcs.Task;
        Assert.NotEmpty(received);
    }

    [Fact]
    public async Task ReceiveAsyncReturnsMessage()
    {
        await using var server = await WebSocketTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport(server.Endpoint);
        await transport.ConnectAsync(server.Endpoint.ToString());
        await transport.SendAsync(BuildEnvelope().ToBytes());

        var received = await transport.ReceiveAsync();
        Assert.NotNull(received);
    }

    [Fact]
    public async Task ConnectFailsWhenKeyMissing()
    {
        await using var server = await WebSocketTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var options = new EcpWebSocketOptions { Capabilities = EcpCapabilities.SupportsDictionary };
        var ecpOptions = new EcpOptions { HmacLength = 12, KeyVersion = 1 };
        var keyProvider = new KeyRing();
        var transport = new EcpWebSocketTransport(options, ecpOptions, keyProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.ConnectAsync(server.Endpoint.ToString()));
    }

    private static EcpWebSocketTransport CreateTransport(Uri endpoint, EcpCapabilities? capabilities = null)
    {
        var options = new EcpWebSocketOptions
        {
            Capabilities = capabilities ?? EcpCapabilities.SupportsDictionary
        };
        var ecpOptions = new EcpOptions { HmacLength = 12, KeyVersion = 1 };
        var keyRing = new KeyRing();
        keyRing.AddKey(1, WebSocketTestServer.SharedKey);
        return new EcpWebSocketTransport(options, ecpOptions, keyRing);
    }

    private static EmergencyEnvelope BuildEnvelope()
    {
        return new EnvelopeBuilder()
            .WithFlags(EcpFlags.None)
            .WithPriority(EcpPriority.Medium)
            .WithTtl(30)
            .WithKeyVersion(1)
            .WithPayloadType(EcpPayloadType.Alert)
            .WithPayload(new byte[] { 0x01, 0x02, 0x03 })
            .WithHmacKey(WebSocketTestServer.SharedKey)
            .Build();
    }

    private sealed class WebSocketTestServer : IAsyncDisposable
    {
        public static readonly byte[] SharedKey =
        {
            0x10, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xF0, 0x01
        };

        private readonly WebApplication _app;
        public Uri Endpoint { get; }

        private WebSocketTestServer(WebApplication app, Uri endpoint)
        {
            _app = app;
            Endpoint = endpoint;
        }

        public static async Task<WebSocketTestServer> StartAsync(EcpCapabilities serverCapabilities)
        {
            var port = GetFreePort();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            var app = builder.Build();
            app.UseWebSockets();
            app.Map("/ws", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await HandleConnectionAsync(socket, serverCapabilities);
            });

            await app.StartAsync();
            return new WebSocketTestServer(app, new Uri($"ws://127.0.0.1:{port}/ws"));
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static async Task HandleConnectionAsync(System.Net.WebSockets.WebSocket socket, EcpCapabilities serverCapabilities)
        {
            var decoder = new EcpStreamDecoder(SharedKey, hmacLength: 12);
            var handshake = new CapabilityHandshake(1, 1, serverCapabilities);
            var buffer = new byte[4096];

            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    return;
                }

                if (decoder.TryFeed(buffer.AsSpan(0, result.Count), out var envelope))
                {
                    await HandleEnvelopeAsync(socket, envelope, handshake);
                    while (decoder.TryFeed(ReadOnlySpan<byte>.Empty, out envelope))
                    {
                        await HandleEnvelopeAsync(socket, envelope, handshake);
                    }
                }
            }
        }

        private static async Task HandleEnvelopeAsync(System.Net.WebSockets.WebSocket socket, EmergencyEnvelope envelope, CapabilityHandshake handshake)
        {
            if (envelope.PayloadType == EcpPayloadType.CapabilityNegotiation &&
                CapabilityNegotiationPayload.TryFromBytes(envelope.Payload.Span, out var offer))
            {
                handshake.TryProcessOffer("client", offer, out _);
                var response = handshake.CreateOffer();
                var responseEnvelope = new EnvelopeBuilder()
                    .WithFlags(EcpFlags.None)
                    .WithPriority(EcpPriority.Low)
                    .WithTtl(1)
                    .WithKeyVersion(1)
                    .WithPayloadType(EcpPayloadType.CapabilityNegotiation)
                    .WithPayload(response.ToBytes())
                    .WithHmacKey(SharedKey)
                    .Build();

                var bytes = responseEnvelope.ToBytes();
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None);
                return;
            }

            var echo = envelope.ToBytes();
            await socket.SendAsync(new ArraySegment<byte>(echo), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
