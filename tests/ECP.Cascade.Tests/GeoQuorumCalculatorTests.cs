// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Cascade.GeoQuorum;
using ECP.Core.Privacy;

namespace ECP.Cascade.Tests;

public class GeoQuorumCalculatorTests
{
    private sealed class TestPrivacyProvider : ITenantPrivacyOptionsProvider
    {
        private readonly Dictionary<string, EcpPrivacyOptions> _options;
        private readonly EcpPrivacyOptions _defaultOptions;

        public TestPrivacyProvider(Dictionary<string, EcpPrivacyOptions> options, EcpPrivacyOptions defaultOptions)
        {
            _options = options;
            _defaultOptions = defaultOptions;
        }

        public EcpPrivacyOptions GetOptions(string tenantId)
        {
            return _options.TryGetValue(tenantId, out var options) ? options : _defaultOptions;
        }
    }

    [Fact]
    public void CalculateComputesCoverageAndSorts()
    {
        var zones = new[]
        {
            new ZoneConfirmationStats(200, confirmedCount: 80, expectedCount: 100),
            new ZoneConfirmationStats(100, confirmedCount: 95, expectedCount: 100)
        };

        var results = GeoQuorumCalculator.Calculate(zones);

        Assert.Equal(2, results.Count);
        Assert.Equal((ushort)100, results[0].ZoneHash);
        Assert.Equal(95d, results[0].CoveragePercent);
        Assert.Equal((ushort)200, results[1].ZoneHash);
        Assert.Equal(80d, results[1].CoveragePercent);
    }

    [Fact]
    public void CalculateClampsAboveExpected()
    {
        var zones = new[]
        {
            new ZoneConfirmationStats(300, confirmedCount: 12, expectedCount: 10)
        };

        var results = GeoQuorumCalculator.Calculate(zones);

        Assert.Equal(100d, results[0].CoveragePercent);
    }

    [Fact]
    public void CalculateHandlesZeroExpected()
    {
        var zones = new[]
        {
            new ZoneConfirmationStats(400, confirmedCount: 0, expectedCount: 0)
        };

        var results = GeoQuorumCalculator.Calculate(zones);

        Assert.Equal(0d, results[0].CoveragePercent);
    }

    [Fact]
    public void ZoneConfirmationStatsRejectsNegativeCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZoneConfirmationStats(10, confirmedCount: -1, expectedCount: 5));
    }

    [Fact]
    public void ZoneConfirmationStatsRoundtrip()
    {
        var stats = new ZoneConfirmationStats(123, confirmedCount: 7, expectedCount: 10);

        var bytes = stats.ToBytes();
        var decoded = ZoneConfirmationStats.FromBytes(bytes);

        Assert.Equal(stats.ZoneHash, decoded.ZoneHash);
        Assert.Equal(stats.ConfirmedCount, decoded.ConfirmedCount);
        Assert.Equal(stats.ExpectedCount, decoded.ExpectedCount);
    }

    [Fact]
    public void GeoQuorumResultRoundtrip()
    {
        var result = new GeoQuorumResult(321, coveragePercent: 85.5, confirmedCount: 17, expectedCount: 20);

        var bytes = result.ToBytes();
        var decoded = GeoQuorumResult.FromBytes(bytes);

        Assert.Equal(result.ZoneHash, decoded.ZoneHash);
        Assert.Equal(result.CoveragePercent, decoded.CoveragePercent);
        Assert.Equal(result.ConfirmedCount, decoded.ConfirmedCount);
        Assert.Equal(result.ExpectedCount, decoded.ExpectedCount);
    }

    [Fact]
    public void GeoQuorumRetentionRespectsTenantRetention()
    {
        var now = DateTimeOffset.UtcNow;
        var results = new[]
        {
            new GeoQuorumResult(100, coveragePercent: 80, confirmedCount: 8, expectedCount: 10)
        };

        var provider = new TestPrivacyProvider(
            new Dictionary<string, EcpPrivacyOptions>
            {
                ["tenant-a"] = new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.FromMinutes(1) },
                ["tenant-b"] = new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.FromMinutes(10) }
            },
            new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.FromMinutes(10) });

        var store = new GeoQuorumRetentionStore(provider);
        store.Add("tenant-a", results, now.AddMinutes(-5));
        store.Add("tenant-b", results, now.AddMinutes(-5));

        Assert.Empty(store.GetAll("tenant-a", now));
        Assert.Single(store.GetAll("tenant-b", now));
    }

    [Fact]
    public void GeoQuorumRetentionClearsWhenZero()
    {
        var now = DateTimeOffset.UtcNow;
        var results = new[]
        {
            new GeoQuorumResult(200, coveragePercent: 75, confirmedCount: 6, expectedCount: 8)
        };

        var provider = new TestPrivacyProvider(
            new Dictionary<string, EcpPrivacyOptions>
            {
                ["tenant-a"] = new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.Zero }
            },
            new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.Zero });

        var store = new GeoQuorumRetentionStore(provider);
        store.Add("tenant-a", results, now);

        Assert.Empty(store.GetAll("tenant-a", now));
    }
}
