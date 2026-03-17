// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Models;
using ECP.Core.Negotiation;

namespace ECP.Core.Tests;

public class CapabilityNegotiationTests
{
    [Fact]
    public void PayloadRoundtripUsesBigEndian()
    {
        var payload = new CapabilityNegotiationPayload(
            minVersion: 1,
            maxVersion: 3,
            capabilities: EcpCapabilities.SupportsDictionary | EcpCapabilities.SupportsEncryption);

        var bytes = payload.ToBytes();

        Assert.Equal(4, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(3, bytes[1]);
        Assert.Equal(0, bytes[2]);
        Assert.Equal(9, bytes[3]);

        var decoded = CapabilityNegotiationPayload.FromBytes(bytes);
        Assert.Equal(payload, decoded);
    }

    [Fact]
    public void TryFromBytesRejectsInvalidLength()
    {
        Span<byte> bytes = stackalloc byte[3];
        Assert.False(CapabilityNegotiationPayload.TryFromBytes(bytes, out _));
    }

    [Fact]
    public void WriteToWritesExpectedBytes()
    {
        var payload = new CapabilityNegotiationPayload(
            minVersion: 1,
            maxVersion: 2,
            capabilities: EcpCapabilities.SupportsEncryption);

        Span<byte> buffer = stackalloc byte[CapabilityNegotiationPayload.Size];
        payload.WriteTo(buffer);

        Assert.Equal(1, buffer[0]);
        Assert.Equal(2, buffer[1]);
        Assert.Equal(0, buffer[2]);
        Assert.Equal(8, buffer[3]);
    }

    [Fact]
    public void TryWriteToReturnsFalseWhenTooSmall()
    {
        var payload = new CapabilityNegotiationPayload(1, 1, EcpCapabilities.None);
        Span<byte> buffer = stackalloc byte[2];

        Assert.False(payload.TryWriteTo(buffer));
    }

    [Fact]
    public void NegotiationSelectsHighestCommonVersionAndIntersection()
    {
        var handshake = new CapabilityHandshake(
            minVersion: 1,
            maxVersion: 2,
            capabilities: EcpCapabilities.SupportsDictionary | EcpCapabilities.SupportsTemplates | EcpCapabilities.SupportsCascade);

        var offer = new CapabilityNegotiationPayload(
            minVersion: 1,
            maxVersion: 3,
            capabilities: EcpCapabilities.SupportsTemplates | EcpCapabilities.SupportsCascade | EcpCapabilities.SupportsEncryption);

        var ok = handshake.TryProcessOffer("peer-1", offer, out var result);

        Assert.True(ok);
        Assert.Equal(2, result.NegotiatedVersion);
        Assert.Equal(EcpCapabilities.SupportsTemplates | EcpCapabilities.SupportsCascade, result.NegotiatedCapabilities);
    }

    [Fact]
    public void NegotiationFailsWhenNoOverlap()
    {
        var handshake = new CapabilityHandshake(
            minVersion: 1,
            maxVersion: 1,
            capabilities: EcpCapabilities.SupportsDictionary);

        var offer = new CapabilityNegotiationPayload(
            minVersion: 2,
            maxVersion: 3,
            capabilities: EcpCapabilities.SupportsDictionary);

        var ok = handshake.TryProcessOffer("peer-1", offer, out _);

        Assert.False(ok);
    }

    [Fact]
    public void HandshakeCachesPerPeer()
    {
        var handshake = new CapabilityHandshake(
            minVersion: 1,
            maxVersion: 2,
            capabilities: EcpCapabilities.SupportsDictionary | EcpCapabilities.SupportsTemplates);

        var offer1 = new CapabilityNegotiationPayload(
            minVersion: 1,
            maxVersion: 2,
            capabilities: EcpCapabilities.SupportsDictionary);

        var offer2 = new CapabilityNegotiationPayload(
            minVersion: 1,
            maxVersion: 2,
            capabilities: EcpCapabilities.SupportsTemplates);

        Assert.True(handshake.TryProcessOffer("peer-1", offer1, out var first));
        Assert.True(handshake.TryProcessOffer("peer-1", offer2, out var second));
        Assert.Equal(first, second);
    }

    [Fact]
    public void HandshakeCacheEvictsOldestWhenLimitExceeded()
    {
        var handshake = new CapabilityHandshake(
            minVersion: 1,
            maxVersion: 1,
            capabilities: EcpCapabilities.SupportsDictionary,
            cacheMaxEntries: 2);

        var offer = new CapabilityNegotiationPayload(
            minVersion: 1,
            maxVersion: 1,
            capabilities: EcpCapabilities.SupportsDictionary);

        Assert.True(handshake.TryProcessOffer("peer-1", offer, out _));
        Assert.True(handshake.TryProcessOffer("peer-2", offer, out _));
        Assert.True(handshake.TryProcessOffer("peer-3", offer, out _));

        Assert.False(handshake.TryGetCached("peer-1", out _));
        Assert.True(handshake.TryGetCached("peer-2", out _));
        Assert.True(handshake.TryGetCached("peer-3", out _));
    }
}
