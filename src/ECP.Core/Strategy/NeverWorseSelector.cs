// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Strategy;

/// <summary>
/// Never Worse strategy selector based on estimated byte cost.
/// </summary>
public sealed class NeverWorseSelector : IStrategySelector
{
    private const int UetSizeBytes = 8;
    private readonly int _miniCascadeThreshold;
    private readonly int _directThreshold;
    private readonly double _miniCascadeFanOutFactor;
    private readonly double _dictionarySavingsFactor;
    private readonly double _templateSavingsFactor;

    /// <summary>
    /// Creates a selector with generic default thresholds.
    /// </summary>
    public NeverWorseSelector()
        : this(new NeverWorseOptions())
    {
    }

    /// <summary>
    /// Creates a selector with custom thresholds.
    /// </summary>
    public NeverWorseSelector(NeverWorseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        _directThreshold = options.DirectThreshold;
        _miniCascadeThreshold = options.MiniCascadeThreshold;
        _miniCascadeFanOutFactor = options.MiniCascadeFanOutFactor;
        _dictionarySavingsFactor = options.DictionarySavingsFactor;
        _templateSavingsFactor = options.TemplateSavingsFactor;
    }

    /// <summary>
    /// Selects the delivery strategy that minimizes byte cost.
    /// </summary>
    public DeliveryStrategy SelectStrategy(int recipientCount, int messageSize, bool hasTemplate = false, bool hasDictionary = false)
    {
        if (recipientCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recipientCount), "Recipient count must be positive.");
        }

        if (messageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(messageSize), "Message size must be positive.");
        }

        if (messageSize <= UetSizeBytes)
        {
            return new DeliveryStrategy(DeliveryMode.UetOnly, UetSizeBytes, 1, "Message size <= 8 bytes, use UET only.");
        }

        var adjustedSize = ApplyCompression(messageSize, hasTemplate, hasDictionary);
        var directCost = adjustedSize * recipientCount;

        if (recipientCount <= _directThreshold)
        {
            return new DeliveryStrategy(DeliveryMode.Direct, directCost, 1, $"Recipients <= {_directThreshold}, direct send.");
        }

        if (recipientCount <= _miniCascadeThreshold)
        {
            var miniCost = EstimateMiniCascadeCost(recipientCount, adjustedSize, out var hops);
            if (miniCost < directCost)
            {
                return new DeliveryStrategy(DeliveryMode.MiniCascade, miniCost, hops,
                    $"Mini-cascade cheaper than direct (direct={directCost}, mini={miniCost}).");
            }

            return new DeliveryStrategy(DeliveryMode.Direct, directCost, 1,
                $"Direct cheaper than mini-cascade (direct={directCost}, mini={miniCost}).");
        }

        var fullCost = EstimateFullCascadeCost(recipientCount, adjustedSize, out var fullHops);
        if (fullCost <= directCost)
        {
            return new DeliveryStrategy(DeliveryMode.FullCascade, fullCost, fullHops,
                $"Full cascade cheaper than direct (direct={directCost}, cascade={fullCost}).");
        }

        return new DeliveryStrategy(DeliveryMode.Direct, directCost, 1,
            $"Direct cheaper than full cascade (direct={directCost}, cascade={fullCost}).");
    }

    private int ApplyCompression(int messageSize, bool hasTemplate, bool hasDictionary)
    {
        var factor = 1.0;
        if (hasTemplate)
        {
            factor *= _templateSavingsFactor;
        }

        if (hasDictionary)
        {
            factor *= _dictionarySavingsFactor;
        }

        var adjusted = (int)Math.Ceiling(messageSize * factor);
        return adjusted < 1 ? 1 : adjusted;
    }

    private int EstimateMiniCascadeCost(int recipientCount, int messageSize, out int hopCount)
    {
        hopCount = recipientCount <= 6 ? 2 : 3;
        var transmissions = 1 + (int)Math.Ceiling(recipientCount / _miniCascadeFanOutFactor);
        return transmissions * messageSize;
    }

    private static int EstimateFullCascadeCost(int recipientCount, int messageSize, out int hopCount)
    {
        hopCount = (int)Math.Ceiling(Math.Log2(recipientCount));
        hopCount = hopCount < 2 ? 2 : hopCount;
        var transmissions = hopCount + 1;
        return transmissions * messageSize;
    }

    private static void Validate(NeverWorseOptions options)
    {
        if (options.DirectThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Direct threshold must be greater than zero.");
        }

        if (options.MiniCascadeThreshold < options.DirectThreshold)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Mini-cascade threshold must be >= direct threshold.");
        }

        if (options.MiniCascadeFanOutFactor <= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Mini-cascade fan-out factor must be > 1.");
        }

        if (options.DictionarySavingsFactor <= 0.0 || options.DictionarySavingsFactor > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Dictionary savings factor must be in (0, 1].");
        }

        if (options.TemplateSavingsFactor <= 0.0 || options.TemplateSavingsFactor > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Template savings factor must be in (0, 1].");
        }
    }
}
