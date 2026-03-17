// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Cascade;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;

namespace ECP.Cascade.Tests;

public class CascadeRouterTests
{
    [Fact]
    public void RejectsWhenCascadeFlagMissing()
    {
        var envelope = BuildEnvelope(ttl: 5, flags: EcpFlags.None);
        var router = CreateRouter();

        var decision = router.Evaluate(envelope, "node-1", DateTimeOffset.UtcNow);

        Assert.False(decision.ShouldForward);
        Assert.Contains("Cascade flag", decision.Reason);
    }

    [Fact]
    public void RejectsWhenTtlZero()
    {
        var envelope = BuildEnvelope(ttl: 0, flags: EcpFlags.Cascade);
        var router = CreateRouter();

        var decision = router.Evaluate(envelope, "node-1", DateTimeOffset.UtcNow);

        Assert.False(decision.ShouldForward);
        Assert.Contains("TTL", decision.Reason);
    }

    [Fact]
    public void RejectsWhenEnvelopeNotVerified()
    {
        var validEnvelope = BuildEnvelope(ttl: 5, flags: EcpFlags.Cascade);
        var invalidEnvelope = EmergencyEnvelope.Decode(validEnvelope.ToBytes());
        var router = CreateRouter();

        var decision = router.Evaluate(invalidEnvelope, "node-1", DateTimeOffset.UtcNow);

        Assert.False(decision.ShouldForward);
        Assert.Contains("signature", decision.Reason);
    }

    [Fact]
    public void ForwardsAndDecrementsTtl()
    {
        var envelope = BuildEnvelope(ttl: 5, flags: EcpFlags.Cascade);
        var router = CreateRouter();

        var decision = router.Evaluate(envelope, "node-1", DateTimeOffset.UtcNow);

        Assert.True(decision.ShouldForward);
        Assert.Equal(4, decision.ForwardedEnvelope.Ttl);

        var decoded = Ecp.DecodeEnvelope(decision.ForwardedEnvelope.ToBytes(), TestEnvelopeFactory.HmacKey);
        Assert.True(decoded.IsValid);
    }

    [Fact]
    public void RejectsWhenKeyMissing()
    {
        var envelope = BuildEnvelope(ttl: 5, flags: EcpFlags.Cascade);
        var router = CreateRouterWithoutKey();

        var decision = router.Evaluate(envelope, "node-1", DateTimeOffset.UtcNow);

        Assert.False(decision.ShouldForward);
        Assert.Contains("HMAC key", decision.Reason);
    }

    [Fact]
    public void RejectsWhenTenantKeyMissing()
    {
        var envelope = BuildEnvelope(ttl: 5, flags: EcpFlags.Cascade);
        var router = CreateRouterWithTenantKey("tenant-a");

        var decision = router.Evaluate(envelope, "node-1", DateTimeOffset.UtcNow, "tenant-b");

        Assert.False(decision.ShouldForward);
        Assert.Contains("HMAC key", decision.Reason);
    }

    private static CascadeRouter CreateRouter()
    {
        var keyRing = new KeyRing();
        keyRing.AddKey(1, TestEnvelopeFactory.HmacKey);

        var protection = new CascadeProtection(
            new AntiReplayWindow(),
            new DedupCache(),
            new RateLimiter(maxPerSecond: 100));

        return new CascadeRouter(protection, new TrustScoreService(), keyRing);
    }

    private static CascadeRouter CreateRouterWithTenantKey(string tenantId)
    {
        var keyRing = new KeyRing();
        keyRing.AddKey(tenantId, 1, TestEnvelopeFactory.HmacKey);

        var protection = new CascadeProtection(
            new AntiReplayWindow(),
            new DedupCache(),
            new RateLimiter(maxPerSecond: 100));

        return new CascadeRouter(protection, new TrustScoreService(), keyRing);
    }

    private static CascadeRouter CreateRouterWithoutKey()
    {
        var keyRing = new KeyRing();
        var protection = new CascadeProtection(
            new AntiReplayWindow(),
            new DedupCache(),
            new RateLimiter(maxPerSecond: 100));

        return new CascadeRouter(protection, new TrustScoreService(), keyRing);
    }

    private static EmergencyEnvelope BuildEnvelope(byte ttl, EcpFlags flags)
    {
        var now = DateTimeOffset.UtcNow;
        return new EnvelopeBuilder()
            .WithFlags(flags)
            .WithPriority(EcpPriority.High)
            .WithTtl(ttl)
            .WithKeyVersion(1)
            .WithMessageId(1234UL)
            .WithTimestamp((uint)now.ToUnixTimeSeconds())
            .WithPayloadType(EcpPayloadType.Alert)
            .WithPayload(new byte[] { 0x01, 0x02, 0x03 })
            .WithHmacKey(TestEnvelopeFactory.HmacKey)
            .Build();
    }
}
