// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Cascade;
using ECP.Cascade.Confirmation;
using ECP.Cascade.GeoQuorum;
using ECP.Core;
using ECP.Core.Profiles;
using ECP.Core.Registry;
using ECP.Core.Security;
using ECP.DependencyInjection;
using ECP.Registry.Dictionary;
using ECP.Registry.Templates;
using ECP.Standard;
using Microsoft.Extensions.DependencyInjection;

namespace ECP.Standard.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEcpStandardRegistersRegistryAndCascade()
    {
        var services = new ServiceCollection();

        services.AddEcpStandard();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<EmergencyDictionary>(provider.GetRequiredService<IDictionaryProvider>());
        Assert.IsType<TemplateRegistry>(provider.GetRequiredService<ITemplateProvider>());

        Assert.NotNull(provider.GetRequiredService<CascadeRouter>());
        Assert.NotNull(provider.GetRequiredService<CascadeProtection>());
        Assert.NotNull(provider.GetRequiredService<TrustScoreService>());
        Assert.NotNull(provider.GetRequiredService<RateLimiter>());
        Assert.NotNull(provider.GetRequiredService<DedupCache>());
        Assert.NotNull(provider.GetRequiredService<AntiReplayWindow>());
    }

    [Fact]
    public void AddEcpStandardEnsuresKeyProvider()
    {
        var services = new ServiceCollection();

        services.AddEcpStandard();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<EcpOptions>();
        var keyProvider = provider.GetRequiredService<IKeyProvider>();

        Assert.NotNull(options.KeyProvider);
        Assert.Same(options.KeyProvider, keyProvider);
    }

    [Fact]
    public void AddEcpStandardAppliesConfiguration()
    {
        var services = new ServiceCollection();

        services.AddEcpStandard(options =>
        {
            options.HmacLength = 16;
            options.TrustScoring.HighScoreThreshold = 65;
            options.TrustScoring.MidScoreThreshold = 35;
            options.TrustScoring.HighFanOut = 12;
            options.TrustScoring.MidFanOut = 7;
            options.TrustScoring.LowFanOut = 4;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<EcpOptions>();
        var trust = provider.GetRequiredService<TrustScoreService>();
        trust.SetScore("node-high", 80);

        Assert.Equal(16, options.HmacLength);
        Assert.Equal(65, options.TrustScoring.HighScoreThreshold);
        Assert.Equal(12, trust.GetFanOutLimit("node-high"));
    }

    [Fact]
    public void AddEcpStandardUsesConfiguredRateLimit()
    {
        var services = new ServiceCollection();

        services.AddEcpStandard(options => options.CascadeRateLimitPerSecond = 1);

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<RateLimiter>();
        var now = DateTimeOffset.UtcNow;

        Assert.True(limiter.TryAcquire(now));
        Assert.False(limiter.TryAcquire(now));
    }

    [Fact]
    public void AddEcpProfileMinimalRegistersCoreOnly()
    {
        var services = new ServiceCollection();

        services.AddEcpProfile(EcpProfile.Minimal);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<EcpOptions>());
        Assert.Null(provider.GetService<IDictionaryProvider>());
        Assert.Null(provider.GetService<CascadeRouter>());
    }

    [Fact]
    public void AddEcpProfileStandardRegistersConfirmationAndGeoQuorumOnly()
    {
        var services = new ServiceCollection();

        services.AddEcpProfile(EcpProfile.Standard);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ConfirmationRetentionStore>());
        Assert.NotNull(provider.GetRequiredService<GeoQuorumRetentionStore>());
        Assert.Null(provider.GetService<IDictionaryProvider>());
        Assert.Null(provider.GetService<CascadeRouter>());
    }

    [Fact]
    public void AddEcpProfileEnterpriseRegistersRegistryAndCascade()
    {
        var services = new ServiceCollection();

        services.AddEcpProfile(EcpProfile.Enterprise);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IDictionaryProvider>());
        Assert.NotNull(provider.GetRequiredService<CascadeRouter>());
    }

}
