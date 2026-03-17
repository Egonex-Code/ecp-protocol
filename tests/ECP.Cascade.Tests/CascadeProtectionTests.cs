// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Cascade;
using ECP.Core.Strategy;
using ECP.Core.Security;

namespace ECP.Cascade.Tests;

public class CascadeProtectionTests
{
    [Fact]
    public void RateLimiterBlocksSecondRequestInSameSecond()
    {
        var limiter = new RateLimiter(maxPerSecond: 1);
        var now = DateTimeOffset.UtcNow;

        Assert.True(limiter.TryAcquire(now));
        Assert.False(limiter.TryAcquire(now));
    }

    [Fact]
    public void RateLimiterAllowsNextSecond()
    {
        var limiter = new RateLimiter(maxPerSecond: 1);
        var now = DateTimeOffset.UtcNow;

        Assert.True(limiter.TryAcquire(now));
        Assert.True(limiter.TryAcquire(now.AddSeconds(1)));
    }

    [Fact]
    public void RateLimiterIsolatedPerTenant()
    {
        var limiter = new RateLimiter(maxPerSecond: 1);
        var now = DateTimeOffset.UtcNow;

        Assert.True(limiter.TryAcquire("tenant-a", now));
        Assert.True(limiter.TryAcquire("tenant-b", now));
    }

    [Fact]
    public void RateLimiterClearResetsTenantWindow()
    {
        var limiter = new RateLimiter(maxPerSecond: 1);
        var now = DateTimeOffset.UtcNow;

        Assert.True(limiter.TryAcquire("tenant-a", now));
        Assert.False(limiter.TryAcquire("tenant-a", now));
        limiter.Clear("tenant-a");
        Assert.True(limiter.TryAcquire("tenant-a", now));
    }

    [Fact]
    public void DedupCacheRejectsDuplicateMessageId()
    {
        var cache = new DedupCache();
        var now = DateTimeOffset.UtcNow;
        var retention = TimeSpan.FromMinutes(5);

        Assert.True(cache.TryAdd(42UL, now, retention));
        Assert.False(cache.TryAdd(42UL, now, retention));
    }

    [Fact]
    public void DedupCachePurgesExpiredEntries()
    {
        var cache = new DedupCache();
        var now = DateTimeOffset.UtcNow;
        var retention = TimeSpan.FromSeconds(1);

        Assert.True(cache.TryAdd(42UL, now, retention));
        Assert.False(cache.TryAdd(42UL, now, retention));
        Assert.True(cache.TryAdd(42UL, now.AddSeconds(2), retention));
    }

    [Fact]
    public void DedupCacheIsolatedPerTenant()
    {
        var cache = new DedupCache();
        var now = DateTimeOffset.UtcNow;
        var retention = TimeSpan.FromMinutes(5);

        Assert.True(cache.TryAdd("tenant-a", 42UL, now, retention));
        Assert.True(cache.TryAdd("tenant-b", 42UL, now, retention));
    }

    [Fact]
    public void DedupCacheClearRemovesTenantEntries()
    {
        var cache = new DedupCache();
        var now = DateTimeOffset.UtcNow;
        var retention = TimeSpan.FromMinutes(5);

        Assert.True(cache.TryAdd("tenant-a", 42UL, now, retention));
        cache.Clear("tenant-a");
        Assert.True(cache.TryAdd("tenant-a", 42UL, now, retention));
    }

    [Fact]
    public void TrustScoreServiceAdjustsFanOut()
    {
        var defaults = new TrustScoringOptions();
        var trust = new TrustScoreService();
        trust.SetScore("node-high", 90);
        trust.SetScore("node-mid", 60);
        trust.SetScore("node-low", 10);

        Assert.Equal(defaults.HighFanOut, trust.GetFanOutLimit("node-high"));
        Assert.Equal(defaults.MidFanOut, trust.GetFanOutLimit("node-mid"));
        Assert.Equal(defaults.LowFanOut, trust.GetFanOutLimit("node-low"));
    }

    [Fact]
    public void TrustScoreServiceDefaultsToMidForUnknown()
    {
        var defaults = new TrustScoringOptions();
        var trust = new TrustScoreService();

        Assert.Equal(defaults.MidFanOut, trust.GetFanOutLimit("node-unknown"));
    }

    [Fact]
    public void TrustScoreServiceClampsScores()
    {
        var defaults = new TrustScoringOptions();
        var trust = new TrustScoreService();
        trust.SetScore("node-high", 200);
        trust.SetScore("node-low", -10);

        Assert.Equal(defaults.HighFanOut, trust.GetFanOutLimit("node-high"));
        Assert.Equal(defaults.LowFanOut, trust.GetFanOutLimit("node-low"));
    }

    [Fact]
    public void CascadeProtectionRejectsDuplicateMessageId()
    {
        var protection = new CascadeProtection(
            new AntiReplayWindow(),
            new DedupCache(),
            new RateLimiter(maxPerSecond: 100));

        var now = DateTimeOffset.UtcNow;
        var envelope = TestEnvelopeFactory.Create(messageId: 99UL, timestamp: now);

        Assert.True(protection.TryAccept(envelope, now, out _));
        Assert.False(protection.TryAccept(envelope, now, out _));
    }

    [Fact]
    public void CascadeProtectionIsolatedPerTenant()
    {
        var protection = new CascadeProtection(
            new AntiReplayWindow(),
            new DedupCache(),
            new RateLimiter(maxPerSecond: 1));

        var now = DateTimeOffset.UtcNow;
        var envelope = TestEnvelopeFactory.Create(messageId: 99UL, timestamp: now);

        Assert.True(protection.TryAccept(envelope, "tenant-a", now, out _));
        Assert.True(protection.TryAccept(envelope, "tenant-b", now, out _));
    }
}
