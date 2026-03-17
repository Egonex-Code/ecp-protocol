// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core;
using ECP.Core.Privacy;
using ECP.Core.Security;
using ECP.Core.Strategy;
using ECP.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace ECP.DependencyInjection.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEcpCoreRegistersOptionsAndStrategy()
    {
        var services = new ServiceCollection();

        services.AddEcpCore();

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<EcpOptions>());
        Assert.NotNull(provider.GetRequiredService<IStrategySelector>());
    }

    [Fact]
    public void AddEcpCoreAppliesConfiguration()
    {
        var services = new ServiceCollection();

        services.AddEcpCore(options =>
        {
            options.HmacLength = 16;
            options.KeyVersion = 2;
            options.NeverWorse.DirectThreshold = 6;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<EcpOptions>();
        var selector = provider.GetRequiredService<IStrategySelector>();
        var strategy = selector.SelectStrategy(5, 120);

        Assert.Equal(16, options.HmacLength);
        Assert.Equal((byte)2, options.KeyVersion);
        Assert.Equal(6, options.NeverWorse.DirectThreshold);
        Assert.Equal(DeliveryMode.Direct, strategy.Mode);
    }

    [Fact]
    public void AddEcpCoreRegistersKeyProviderWhenProvided()
    {
        var services = new ServiceCollection();
        var keyRing = new KeyRing();

        services.AddEcpCore(options => options.KeyProvider = keyRing);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IKeyProvider>();

        Assert.Same(keyRing, resolved);
    }

    [Fact]
    public void AddEcpCoreRegistersPrivacyProvider()
    {
        var services = new ServiceCollection();

        services.AddEcpCore(options => options.Privacy.EpochDuration = TimeSpan.FromMinutes(1));

        using var provider = services.BuildServiceProvider();
        var privacyProvider = provider.GetRequiredService<ITenantPrivacyOptionsProvider>();
        var options = privacyProvider.GetOptions("tenant-a");

        Assert.Equal(TimeSpan.FromMinutes(1), options.EpochDuration);
        Assert.NotNull(provider.GetRequiredService<ZoneHashProvider>());
    }

}
