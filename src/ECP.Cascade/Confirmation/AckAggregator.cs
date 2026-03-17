// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Cascade.Confirmation;

/// <summary>
/// Aggregates confirmations into a single package.
/// </summary>
public sealed class AckAggregator
{
    private readonly string _aggregatorNodeId;

    /// <summary>
    /// Creates a new aggregator for the given node.
    /// </summary>
    public AckAggregator(string aggregatorNodeId)
    {
        if (string.IsNullOrWhiteSpace(aggregatorNodeId))
        {
            throw new ArgumentException("Aggregator node id must be provided.", nameof(aggregatorNodeId));
        }

        _aggregatorNodeId = aggregatorNodeId;
    }

    /// <summary>
    /// Aggregates confirmations from multiple recipients.
    /// </summary>
    public AggregatedConfirmation Aggregate(
        ulong originalMessageId,
        IReadOnlyList<SingleConfirmation> confirmations,
        DateTimeOffset? aggregatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(confirmations);

        return new AggregatedConfirmation(
            originalMessageId,
            aggregatedAt ?? DateTimeOffset.UtcNow,
            _aggregatorNodeId,
            confirmations);
    }
}
