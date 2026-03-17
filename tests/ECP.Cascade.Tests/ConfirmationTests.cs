// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Cascade.Confirmation;
using ECP.Core.Privacy;

namespace ECP.Cascade.Tests;

public class ConfirmationTests
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
    public void AggregateUsesProvidedTimestamp()
    {
        var aggregator = new AckAggregator("node-1");
        var confirmations = new[]
        {
            new SingleConfirmation("r1", DateTimeOffset.UtcNow, ConfirmationType.Delivered, new byte[4])
        };
        var aggregatedAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var result = aggregator.Aggregate(100UL, confirmations, aggregatedAt);

        Assert.Equal(100UL, result.OriginalMessageId);
        Assert.Equal("node-1", result.AggregatorNodeId);
        Assert.Equal(aggregatedAt, result.AggregatedAt);
        Assert.Single(result.Confirmations);
    }

    [Fact]
    public void SingleConfirmationRejectsInvalidHmacLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SingleConfirmation("r1", DateTimeOffset.UtcNow, ConfirmationType.Read, new byte[3]));
    }

    [Fact]
    public void AggregatedConfirmationRejectsMissingAggregatorId()
    {
        var confirmations = new[]
        {
            new SingleConfirmation("r1", DateTimeOffset.UtcNow, ConfirmationType.Delivered, new byte[4])
        };

        Assert.Throws<ArgumentException>(() =>
            new AggregatedConfirmation(1UL, DateTimeOffset.UtcNow, string.Empty, confirmations));
    }

    [Fact]
    public void AckAggregatorRejectsNullConfirmations()
    {
        var aggregator = new AckAggregator("node-1");

        Assert.Throws<ArgumentNullException>(() =>
            aggregator.Aggregate(1UL, null!));
    }

    [Fact]
    public void SingleConfirmationRoundtrip()
    {
        var confirmedAt = new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero);
        var confirmation = new SingleConfirmation("recipient-1", confirmedAt, ConfirmationType.Read, new byte[] { 1, 2, 3, 4 });

        var bytes = confirmation.ToBytes();
        var decoded = SingleConfirmation.FromBytes(bytes);

        Assert.Equal("recipient-1", decoded.RecipientId);
        Assert.Equal(confirmedAt, decoded.ConfirmedAt);
        Assert.Equal(ConfirmationType.Read, decoded.Type);
        Assert.Equal(confirmation.TruncatedHmac.ToArray(), decoded.TruncatedHmac.ToArray());
    }

    [Fact]
    public void AggregatedConfirmationRoundtrip()
    {
        var confirmations = new[]
        {
            new SingleConfirmation("r1", new DateTimeOffset(2026, 2, 7, 10, 0, 0, TimeSpan.Zero), ConfirmationType.Delivered, new byte[] { 1, 1, 1, 1 }),
            new SingleConfirmation("r2", new DateTimeOffset(2026, 2, 7, 10, 1, 0, TimeSpan.Zero), ConfirmationType.Actioned, new byte[] { 2, 2, 2, 2 })
        };

        var aggregated = new AggregatedConfirmation(
            originalMessageId: 123UL,
            aggregatedAt: new DateTimeOffset(2026, 2, 7, 10, 5, 0, TimeSpan.Zero),
            aggregatorNodeId: "node-1",
            confirmations: confirmations);

        var bytes = aggregated.ToBytes();
        var decoded = AggregatedConfirmation.FromBytes(bytes);

        Assert.Equal(aggregated.OriginalMessageId, decoded.OriginalMessageId);
        Assert.Equal(aggregated.AggregatedAt, decoded.AggregatedAt);
        Assert.Equal(aggregated.AggregatorNodeId, decoded.AggregatorNodeId);
        Assert.Equal(aggregated.Confirmations.Count, decoded.Confirmations.Count);
        Assert.Equal(aggregated.Confirmations[0].RecipientId, decoded.Confirmations[0].RecipientId);
        Assert.Equal(aggregated.Confirmations[1].RecipientId, decoded.Confirmations[1].RecipientId);
    }

    [Fact]
    public void ConfirmationRetentionRespectsTenantRetention()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = new AggregatedConfirmation(
            originalMessageId: 1,
            aggregatedAt: now.AddMinutes(-5),
            aggregatorNodeId: "node-1",
            confirmations: new[]
            {
                new SingleConfirmation("r1", now, ConfirmationType.Delivered, new byte[] { 1, 2, 3, 4 })
            });

        var provider = new TestPrivacyProvider(
            new Dictionary<string, EcpPrivacyOptions>
            {
                ["tenant-a"] = new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.FromMinutes(1) },
                ["tenant-b"] = new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.FromMinutes(10) }
            },
            new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.FromMinutes(10) });

        var store = new ConfirmationRetentionStore(provider);
        store.Add("tenant-a", expired);
        store.Add("tenant-b", expired);

        Assert.Empty(store.GetAll("tenant-a", now));
        Assert.Single(store.GetAll("tenant-b", now));
    }

    [Fact]
    public void ConfirmationRetentionClearsWhenZero()
    {
        var now = DateTimeOffset.UtcNow;
        var confirmation = new AggregatedConfirmation(
            originalMessageId: 2,
            aggregatedAt: now,
            aggregatorNodeId: "node-1",
            confirmations: new[]
            {
                new SingleConfirmation("r1", now, ConfirmationType.Delivered, new byte[] { 1, 2, 3, 4 })
            });

        var provider = new TestPrivacyProvider(
            new Dictionary<string, EcpPrivacyOptions>
            {
                ["tenant-a"] = new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.Zero }
            },
            new EcpPrivacyOptions { ConfirmationRetention = TimeSpan.Zero });

        var store = new ConfirmationRetentionStore(provider);
        store.Add("tenant-a", confirmation);

        Assert.Empty(store.GetAll("tenant-a", now));
    }
}
