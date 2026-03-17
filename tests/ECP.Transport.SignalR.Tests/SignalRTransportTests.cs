// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Net;
using System.Net.Sockets;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Negotiation;
using ECP.Core.Security;
using ECP.Transport.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ECP.Transport.SignalR.Tests;

public class SignalRTransportTests
{
    [Fact]
    public async Task ConnectAndDisconnect()
    {
        await using var server = await SignalRTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport();
        await transport.ConnectAsync(server.Endpoint.ToString());

        Assert.True(transport.IsConnected);

        await transport.DisconnectAsync();
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public async Task HandshakeNegotiatesCapabilities()
    {
        await using var server = await SignalRTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsTemplates);

        var transport = CreateTransport(EcpCapabilities.SupportsTemplates | EcpCapabilities.SupportsCascade);
        await transport.ConnectAsync(server.Endpoint.ToString());

        Assert.NotNull(transport.NegotiatedCapabilities);
        Assert.Equal(EcpCapabilities.SupportsTemplates, transport.NegotiatedCapabilities?.NegotiatedCapabilities);
    }

    [Fact]
    public async Task SendReceiveRoundtrip()
    {
        await using var server = await SignalRTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport();
        await transport.ConnectAsync(server.Endpoint.ToString());

        var bytes = BuildEnvelope().ToBytes();
        await transport.SendAsync(bytes);

        var received = await transport.ReceiveAsync();
        Assert.Equal(bytes, received);
    }

    [Fact]
    public async Task OnMessageReceivedFires()
    {
        await using var server = await SignalRTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport();
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
        await using var server = await SignalRTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var transport = CreateTransport();
        await transport.ConnectAsync(server.Endpoint.ToString());
        await transport.SendAsync(BuildEnvelope().ToBytes());

        var received = await transport.ReceiveAsync();
        Assert.NotNull(received);
    }

    [Fact]
    public async Task ConnectFailsWhenKeyMissing()
    {
        await using var server = await SignalRTestServer.StartAsync(
            serverCapabilities: EcpCapabilities.SupportsDictionary);

        var options = new EcpSignalROptions { Capabilities = EcpCapabilities.SupportsDictionary };
        var ecpOptions = new EcpOptions { HmacLength = 12, KeyVersion = 1 };
        var keyProvider = new KeyRing();
        var transport = new EcpSignalRTransport(options, ecpOptions, keyProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.ConnectAsync(server.Endpoint.ToString()));
    }

    private static EcpSignalRTransport CreateTransport(EcpCapabilities? capabilities = null)
    {
        var options = new EcpSignalROptions
        {
            Capabilities = capabilities ?? EcpCapabilities.SupportsDictionary
        };
        var ecpOptions = new EcpOptions { HmacLength = 12, KeyVersion = 1 };
        var keyRing = new KeyRing();
        keyRing.AddKey(1, SignalRTestServer.SharedKey);
        return new EcpSignalRTransport(options, ecpOptions, keyRing);
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
            .WithHmacKey(SignalRTestServer.SharedKey)
            .Build();
    }

    private sealed class SignalRTestServer : IAsyncDisposable
    {
        public static readonly byte[] SharedKey =
        {
            0x10, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
            0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xF0, 0x01
        };

        private readonly WebApplication _app;
        public Uri Endpoint { get; }

        private SignalRTestServer(WebApplication app, Uri endpoint)
        {
            _app = app;
            Endpoint = endpoint;
        }

        public static async Task<SignalRTestServer> StartAsync(EcpCapabilities serverCapabilities)
        {
            var port = GetFreePort();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            builder.Services.AddSignalR();
            builder.Services.AddSingleton(new TestHubState(serverCapabilities));

            var app = builder.Build();
            app.MapHub<EcpTestHub>("/ecp");

            await app.StartAsync();
            return new SignalRTestServer(app, new Uri($"http://127.0.0.1:{port}/ecp"));
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private sealed class TestHubState
        {
            public CapabilityHandshake Handshake { get; }

            public TestHubState(EcpCapabilities capabilities)
            {
                Handshake = new CapabilityHandshake(1, 1, capabilities);
            }
        }

        private sealed class EcpTestHub : Hub
        {
            private readonly TestHubState _state;

            public EcpTestHub(TestHubState state)
            {
                _state = state;
            }

            public async Task SendEcp(byte[] data)
            {
                var envelope = EmergencyEnvelope.Decode(data, SharedKey);
                if (envelope.PayloadType == EcpPayloadType.CapabilityNegotiation &&
                    CapabilityNegotiationPayload.TryFromBytes(envelope.Payload.Span, out var offer))
                {
                    _state.Handshake.TryProcessOffer(Context.ConnectionId ?? "peer", offer, out _);
                    var response = _state.Handshake.CreateOffer();
                    var responseEnvelope = new EnvelopeBuilder()
                        .WithFlags(EcpFlags.None)
                        .WithPriority(EcpPriority.Low)
                        .WithTtl(1)
                        .WithKeyVersion(1)
                        .WithPayloadType(EcpPayloadType.CapabilityNegotiation)
                        .WithPayload(response.ToBytes())
                        .WithHmacKey(SharedKey)
                        .Build();

                    await Clients.Caller.SendAsync("ReceiveEcp", responseEnvelope.ToBytes());
                    return;
                }

                await Clients.Caller.SendAsync("ReceiveEcp", envelope.ToBytes());
            }
        }
    }
}
