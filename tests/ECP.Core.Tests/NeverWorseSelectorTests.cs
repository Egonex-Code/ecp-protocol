// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Strategy;
using Xunit;

namespace ECP.Core.Tests;

public class NeverWorseSelectorTests
{
    private readonly NeverWorseSelector _selector = new();

    [Fact]
    public void Recipient1SelectsDirect()
    {
        var result = _selector.SelectStrategy(1, messageSize: 100);
        Assert.Equal(DeliveryMode.Direct, result.Mode);
    }

    [Fact]
    public void Recipient3SelectsDirect()
    {
        var result = _selector.SelectStrategy(3, messageSize: 120);
        Assert.Equal(DeliveryMode.Direct, result.Mode);
    }

    [Fact]
    public void Recipient5SelectsMiniCascade()
    {
        var result = _selector.SelectStrategy(5, messageSize: 120);
        Assert.Equal(DeliveryMode.MiniCascade, result.Mode);
    }

    [Fact]
    public void Recipient50SelectsFullCascade()
    {
        var result = _selector.SelectStrategy(50, messageSize: 120);
        Assert.Equal(DeliveryMode.FullCascade, result.Mode);
    }

    [Fact]
    public void Recipient1000SelectsFullCascade()
    {
        var result = _selector.SelectStrategy(1000, messageSize: 120);
        Assert.Equal(DeliveryMode.FullCascade, result.Mode);
    }

    [Fact]
    public void NeverWorseGuaranteeHoldsForDirectBaseline()
    {
        const int recipients = 10;
        const int messageSize = 140;
        var result = _selector.SelectStrategy(recipients, messageSize);

        Assert.True(result.EstimatedTotalBytes <= recipients * messageSize);
    }

    [Fact]
    public void UetOnlyWhenMessageSizeIsEight()
    {
        var result = _selector.SelectStrategy(5, messageSize: 8);
        Assert.Equal(DeliveryMode.UetOnly, result.Mode);
        Assert.Equal(8, result.EstimatedTotalBytes);
    }

    [Fact]
    public void TemplateReducesEstimatedCost()
    {
        var withoutTemplate = _selector.SelectStrategy(10, messageSize: 200, hasTemplate: false, hasDictionary: false);
        var withTemplate = _selector.SelectStrategy(10, messageSize: 200, hasTemplate: true, hasDictionary: false);

        Assert.True(withTemplate.EstimatedTotalBytes < withoutTemplate.EstimatedTotalBytes);
    }

    [Fact]
    public void DictionaryReducesEstimatedCost()
    {
        var withoutDictionary = _selector.SelectStrategy(10, messageSize: 200, hasTemplate: false, hasDictionary: false);
        var withDictionary = _selector.SelectStrategy(10, messageSize: 200, hasTemplate: false, hasDictionary: true);

        Assert.True(withDictionary.EstimatedTotalBytes < withoutDictionary.EstimatedTotalBytes);
    }

    [Fact]
    public void CustomOptionsCanShiftDirectThreshold()
    {
        var options = new NeverWorseOptions
        {
            DirectThreshold = 6,
            MiniCascadeThreshold = 12,
            MiniCascadeFanOutFactor = 2.5,
            DictionarySavingsFactor = 0.78,
            TemplateSavingsFactor = 0.55
        };

        var selector = new NeverWorseSelector(options);
        var result = selector.SelectStrategy(5, messageSize: 120);

        Assert.Equal(DeliveryMode.Direct, result.Mode);
    }
}
