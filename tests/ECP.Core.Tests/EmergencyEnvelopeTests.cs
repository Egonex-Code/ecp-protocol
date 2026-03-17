// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using Xunit;

namespace ECP.Core.Tests;

public class EmergencyEnvelopeTests
{
    private static readonly byte[] HmacKey = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

    [Fact]
    public void HeaderIsExactly22Bytes()
    {
        Assert.Equal(22, EmergencyEnvelope.HeaderSize);
    }

    [Fact]
    public void MagicNumberIsInFirstTwoBytes()
    {
        var envelope = Ecp.Envelope()
            .WithPayload(Array.Empty<byte>())
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();

        Assert.Equal(0xEC, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
    }

    [Fact]
    public void EmptyPayloadResultsIn34Bytes()
    {
        var envelope = Ecp.Envelope()
            .WithPayload(Array.Empty<byte>())
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();

        Assert.Equal(EmergencyEnvelope.HeaderSize + EmergencyEnvelope.DefaultHmacLength, bytes.Length);
    }

    [Fact]
    public void EnvelopeWithoutHmacUsesHeaderAndPayloadOnly()
    {
        var payload = new byte[] { 0xAB };
        var envelope = Ecp.Envelope()
            .WithPayload(payload)
            .WithHmacLength(0)
            .Build();

        var bytes = envelope.ToBytes();

        Assert.Equal(EmergencyEnvelope.HeaderSize + payload.Length, bytes.Length);
        Assert.Equal(0, envelope.Hmac.Length);

        var decoded = EmergencyEnvelope.Decode(bytes, hmacLength: 0);
        Assert.Equal(payload, decoded.Payload.ToArray());
        Assert.True(decoded.IsValid);
    }

    [Fact]
    public void PayloadLengthMatchesHeaderValue()
    {
        var payload = new byte[100];
        var envelope = Ecp.Envelope()
            .WithPayload(payload)
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();
        var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(20, 2));

        Assert.Equal(payload.Length, payloadLength);
    }

    [Fact]
    public void EncodeDecodeRoundtrip()
    {
        var payload = new byte[] { 0x10, 0x20, 0x30 };
        var envelope = Ecp.Envelope()
            .WithPayloadType(EcpPayloadType.Alert)
            .WithPriority(EcpPriority.High)
            .WithFlags(EcpFlags.NeedsConfirmation)
            .WithTtl(120)
            .WithKeyVersion(2)
            .WithMessageId(0x0102030405060708)
            .WithTimestamp(0x01020304)
            .WithPayload(payload)
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();
        var decoded = Ecp.DecodeEnvelope(bytes, HmacKey);

        Assert.True(decoded.IsValid);
        Assert.Equal(envelope.Flags, decoded.Flags);
        Assert.Equal(envelope.Priority, decoded.Priority);
        Assert.Equal(envelope.Ttl, decoded.Ttl);
        Assert.Equal(envelope.KeyVersion, decoded.KeyVersion);
        Assert.Equal(envelope.MessageId, decoded.MessageId);
        Assert.Equal(envelope.Timestamp, decoded.Timestamp);
        Assert.Equal(envelope.PayloadType, decoded.PayloadType);
        Assert.Equal(payload, decoded.Payload.ToArray());
    }

    [Fact]
    public void InvalidHmacIsDetected()
    {
        var payload = new byte[] { 0xAA, 0xBB };
        var envelope = Ecp.Envelope()
            .WithPayload(payload)
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();
        var wrongKey = new byte[] { 0xFF, 0xEE, 0xDD, 0xCC };
        var decoded = Ecp.DecodeEnvelope(bytes, wrongKey);

        Assert.False(decoded.IsValid);
    }

    [Fact]
    public void MessageIdIsBigEndian()
    {
        var messageId = 0x0102030405060708UL;
        var envelope = Ecp.Envelope()
            .WithMessageId(messageId)
            .WithPayload(Array.Empty<byte>())
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();

        Assert.Equal(0x01, bytes[7]);
        Assert.Equal(0x02, bytes[8]);
        Assert.Equal(0x03, bytes[9]);
        Assert.Equal(0x04, bytes[10]);
        Assert.Equal(0x05, bytes[11]);
        Assert.Equal(0x06, bytes[12]);
        Assert.Equal(0x07, bytes[13]);
        Assert.Equal(0x08, bytes[14]);
    }

    [Fact]
    public void DecodeViewExposesPayloadSlice()
    {
        var payload = new byte[] { 0x10, 0x20, 0x30 };
        var envelope = Ecp.Envelope()
            .WithPayload(payload)
            .WithHmacKey(HmacKey)
            .Build();

        var bytes = envelope.ToBytes();
        var view = EmergencyEnvelope.DecodeView(bytes, HmacKey);

        Assert.True(view.IsValid);
        Assert.Equal(payload.Length, view.Payload.Length);

        bytes[EmergencyEnvelope.HeaderSize] ^= 0xFF;
        Assert.Equal(bytes[EmergencyEnvelope.HeaderSize], view.Payload.Span[0]);
    }

    [Fact]
    public void TryDecodeViewReturnsFalseOnInvalidMagic()
    {
        var bytes = new byte[EmergencyEnvelope.HeaderSize + EmergencyEnvelope.DefaultHmacLength];

        var ok = EmergencyEnvelope.TryDecodeView(bytes, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryDecodeDoesNotThrowOnRandomData()
    {
        var random = new Random(1234);

        for (var i = 0; i < 100; i++)
        {
            var length = random.Next(0, 128);
            var data = new byte[length];
            random.NextBytes(data);

            EcpDecodedMessage message;
            var exception = Record.Exception(() => Ecp.TryDecode(data, out message));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void EcpTryDecodeDetectsUnsignedEnvelope()
    {
        var payload = new byte[] { 0x10, 0x20 };
        var envelope = Ecp.Envelope()
            .WithPayload(payload)
            .WithHmacLength(0)
            .Build();

        var ok = Ecp.TryDecode(envelope.ToBytes(), out var message);

        Assert.True(ok);
        Assert.True(message.IsEnvelope);
        Assert.Equal(payload.Length, message.Envelope.PayloadLength);
    }

    [Fact]
    public void WithPriorityThrowsOnInvalidValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ecp.Envelope().WithPriority((EcpPriority)99));
    }

    [Fact]
    public void WithTtlThrowsWhenAboveMax()
    {
        var builder = Ecp.Envelope().WithMaxTtl(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithTtl(11));
    }

    [Fact]
    public void BuildThrowsWhenPayloadExceedsConfiguredMax()
    {
        var payload = new byte[5];
        var builder = Ecp.Envelope()
            .WithMaxPayloadBytes(4)
            .WithPayload(payload)
            .WithHmacKey(HmacKey);

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
