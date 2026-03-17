// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Transport.Abstractions;

namespace ECP.Transport.Abstractions.Tests;

public class EcpStreamDecoderTests
{
    private static readonly byte[] TestKey = new byte[]
    {
        0x10, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
        0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xF0, 0x01,
        0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89,
        0x9A, 0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xF1, 0x02
    };

    [Fact]
    public void TryFeedReturnsFalseUntilComplete()
    {
        var envelope = CreateEnvelope(1UL);
        var bytes = envelope.ToBytes();
        var decoder = new EcpStreamDecoder(TestKey);

        var firstChunk = bytes.AsSpan(0, 10);
        Assert.False(decoder.TryFeed(firstChunk, out _));

        var remaining = bytes.AsSpan(10);
        Assert.True(decoder.TryFeed(remaining, out var decoded));
        Assert.True(decoded.IsValid);
        Assert.Equal(1UL, decoded.MessageId);
    }

    [Fact]
    public void TryFeedDecodesRemainderWithEmptyFeed()
    {
        var first = CreateEnvelope(1UL).ToBytes();
        var second = CreateEnvelope(2UL).ToBytes();
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);

        var decoder = new EcpStreamDecoder(TestKey);

        Assert.True(decoder.TryFeed(combined, out var decodedFirst));
        Assert.Equal(1UL, decodedFirst.MessageId);

        Assert.True(decoder.TryFeed(ReadOnlySpan<byte>.Empty, out var decodedSecond));
        Assert.Equal(2UL, decodedSecond.MessageId);
    }

    [Fact]
    public void TryFeedRejectsInvalidMagicThenAcceptsValid()
    {
        var bytes = CreateEnvelope(10UL).ToBytes();
        bytes[0] = 0x00;
        bytes[1] = 0x00;
        var decoder = new EcpStreamDecoder(TestKey);

        Assert.False(decoder.TryFeed(bytes, out _));

        var valid = CreateEnvelope(11UL).ToBytes();
        Assert.True(decoder.TryFeed(valid, out var decoded));
        Assert.Equal(11UL, decoded.MessageId);
    }

    [Fact]
    public void TryFeedRejectsInvalidVersionThenAcceptsValid()
    {
        var bytes = CreateEnvelope(12UL).ToBytes();
        bytes[2] = 0xFF;
        var decoder = new EcpStreamDecoder(TestKey);

        Assert.False(decoder.TryFeed(bytes, out _));

        var valid = CreateEnvelope(13UL).ToBytes();
        Assert.True(decoder.TryFeed(valid, out var decoded));
        Assert.Equal(13UL, decoded.MessageId);
    }

    [Fact]
    public void ResetClearsBufferedData()
    {
        var bytes = CreateEnvelope(20UL).ToBytes();
        var decoder = new EcpStreamDecoder(TestKey);

        Assert.False(decoder.TryFeed(bytes.AsSpan(0, 10), out _));
        decoder.Reset();

        Assert.True(decoder.TryFeed(bytes, out var decoded));
        Assert.Equal(20UL, decoded.MessageId);
    }

    [Fact]
    public void DecoderWithoutKeySkipsHmacValidation()
    {
        var bytes = CreateEnvelope(30UL).ToBytes();
        var decoder = new EcpStreamDecoder();

        Assert.True(decoder.TryFeed(bytes, out var decoded));
        Assert.False(decoded.IsValid);
        Assert.Equal(30UL, decoded.MessageId);
    }

    [Fact]
    public void DecoderAcceptsUnsignedEnvelope()
    {
        var bytes = CreateUnsignedEnvelope(40UL).ToBytes();
        var decoder = new EcpStreamDecoder(hmacLength: 0);

        Assert.True(decoder.TryFeed(bytes, out var decoded));
        Assert.Equal(40UL, decoded.MessageId);
        Assert.Equal(0, decoded.Hmac.Length);
    }

    private static EmergencyEnvelope CreateEnvelope(ulong messageId)
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
            .WithPayload(new byte[] { 0x01, 0x02, 0x03 })
            .WithHmacKey(TestKey)
            .Build();
    }

    private static EmergencyEnvelope CreateUnsignedEnvelope(ulong messageId)
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
            .WithPayload(new byte[] { 0x01, 0x02, 0x03 })
            .WithHmacLength(0)
            .Build();
    }
}
