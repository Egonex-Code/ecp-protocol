// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using System.Text.Json;
using ECP.Compatibility;
using ECP.Core.Envelope;
using ECP.Core.Models;

namespace ECP.Compatibility.Tests;

public class JsonBridgeTests
{
    private static readonly byte[] TestKey = new byte[]
    {
        0x10, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
        0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xF0, 0x01,
        0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89,
        0x9A, 0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xF1, 0x02
    };

    [Fact]
    public void ToEcpWrapsLegacyJsonPayload()
    {
        var json = "{\"type\":\"alert\",\"level\":2}";

        var bytes = JsonBridge.ToEcp(json, TestKey);
        var envelope = EmergencyEnvelope.Decode(bytes, TestKey);
        var payload = Encoding.UTF8.GetString(envelope.Payload.Span);

        Assert.Equal(json, payload);
    }

    [Fact]
    public void ToEcpUsesEnvelopeJsonFields()
    {
        var json = "{\"payloadText\":\"hello\",\"priority\":\"High\",\"ttl\":7," +
                   "\"flags\":\"Broadcast\",\"payloadType\":\"Alert\"," +
                   "\"keyVersion\":3,\"messageId\":123,\"timestamp\":1700000000}";

        var bytes = JsonBridge.ToEcp(
            json,
            TestKey,
            keyVersion: 1,
            priority: EcpPriority.Low,
            ttlSeconds: 5,
            flags: EcpFlags.None,
            payloadType: EcpPayloadType.Ping);

        var envelope = EmergencyEnvelope.Decode(bytes, TestKey);

        Assert.Equal(EcpPriority.High, envelope.Priority);
        Assert.Equal((byte)7, envelope.Ttl);
        Assert.Equal(EcpFlags.Broadcast, envelope.Flags);
        Assert.Equal(EcpPayloadType.Alert, envelope.PayloadType);
        Assert.Equal((byte)3, envelope.KeyVersion);
        Assert.Equal(123UL, envelope.MessageId);
        Assert.Equal(1700000000U, envelope.Timestamp);
    }

    [Fact]
    public void ToJsonReturnsPayloadWhenJson()
    {
        var payload = "{\"p\":1}";
        var envelope = CreateEnvelope(1UL, payload);

        var json = JsonBridge.ToJson(envelope.ToBytes(), TestKey);

        Assert.Equal(payload, json);
    }

    [Fact]
    public void ToJsonWithMetadataReturnsEnvelopeJson()
    {
        var payload = "{\"p\":3}";
        var envelope = CreateEnvelope(10UL, payload);

        var json = JsonBridge.ToJson(
            envelope.ToBytes(),
            TestKey,
            EmergencyEnvelope.DefaultHmacLength,
            includeMetadata: true);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(payload, doc.RootElement.GetProperty("payloadText").GetString());
        Assert.Equal(10UL, doc.RootElement.GetProperty("messageId").GetUInt64());
    }

    [Fact]
    public void ToJsonReturnsEnvelopeJsonWhenPayloadNotJson()
    {
        var envelope = CreateEnvelope(2UL, "\u0001\u0002");
        var json = JsonBridge.ToJson(envelope.ToBytes(), TestKey);

        using var doc = JsonDocument.Parse(json);
        var payloadBase64 = doc.RootElement.GetProperty("payloadBase64").GetString();

        Assert.Equal("AQI=", payloadBase64);
    }

    [Fact]
    public void TryDecodeCompatDetectsEnvelope()
    {
        var envelope = CreateEnvelope(3UL, "{\"p\":2}");
        var bytes = envelope.ToBytes();

        Assert.True(JsonBridge.TryDecodeCompat(bytes, TestKey, out var message));
        Assert.True(message.IsEcp);
        Assert.Equal(3UL, message.Envelope.MessageId);
        Assert.Equal("{\"p\":2}", message.Json);
    }

    [Fact]
    public void TryDecodeCompatDetectsJson()
    {
        var json = "{\"legacy\":true}";
        var bytes = Encoding.UTF8.GetBytes(json);

        Assert.True(JsonBridge.TryDecodeCompat(bytes, TestKey, out var message));
        Assert.False(message.IsEcp);
        Assert.Equal(json, message.Json);
    }

    [Fact]
    public void ToEcpRejectsUnknownFlagBits()
    {
        var json = "{\"payloadText\":\"hello\",\"flags\":128}";

        Assert.Throws<ArgumentOutOfRangeException>(() => JsonBridge.ToEcp(json, TestKey));
    }

    [Fact]
    public void ToEcpRejectsBothPayloadFields()
    {
        var json = "{\"payloadText\":\"hello\",\"payloadBase64\":\"AQI=\"}";

        Assert.Throws<ArgumentException>(() => JsonBridge.ToEcp(json, TestKey));
    }

    [Fact]
    public void ToEcpRejectsInvalidBase64()
    {
        var json = "{\"payloadBase64\":\"@@@\"}";

        Assert.Throws<ArgumentException>(() => JsonBridge.ToEcp(json, TestKey));
    }

    private static EmergencyEnvelope CreateEnvelope(ulong messageId, string payloadText)
    {
        var now = DateTimeOffset.UtcNow;
        return new EnvelopeBuilder()
            .WithFlags(EcpFlags.None)
            .WithPriority(EcpPriority.Medium)
            .WithTtl(5)
            .WithKeyVersion(1)
            .WithMessageId(messageId)
            .WithTimestamp((uint)now.ToUnixTimeSeconds())
            .WithPayloadType(EcpPayloadType.Alert)
            .WithPayload(payloadText)
            .WithHmacKey(TestKey)
            .Build();
    }
}
