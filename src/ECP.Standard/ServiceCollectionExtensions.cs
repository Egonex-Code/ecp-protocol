// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Linq;
using ECP.Cascade;
using ECP.Core;
using ECP.Core.Profiles;
using ECP.Core.Registry;
using ECP.Core.Security;
using ECP.DependencyInjection;
using ECP.Cascade.Confirmation;
using ECP.Cascade.GeoQuorum;
using ECP.Registry.Dictionary;
using ECP.Registry.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECP.Standard;

/// <summary>
/// Dependency injection registrations for ECP Standard.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers services based on a predefined profile.
    /// </summary>
    public static IServiceCollection AddEcpProfile(this IServiceCollection services, EcpProfile profile, Action<EcpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        switch (profile)
        {
            case EcpProfile.Minimal:
            case EcpProfile.Industrial:
                services.AddEcpCore(configure);
                return services;
            case EcpProfile.Standard:
                services.AddEcpCore(configure);
                return RegisterStandardProfile(services);
            case EcpProfile.Enterprise:
            case EcpProfile.Airport:
            case EcpProfile.Hospital:
                return services.AddEcpStandard(configure);
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown ECP profile.");
        }
    }

    /// <summary>
    /// Registers Core + Registry + Cascade services for ECP Standard.
    /// </summary>
    public static IServiceCollection AddEcpStandard(this IServiceCollection services, Action<EcpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddEcpCore(configure);
        var options = GetOptions(services);
        EnsureKeyProvider(services, options);

        services.TryAddSingleton<EmergencyDictionary>(_ => EmergencyDictionary.CreateDefault());
        services.TryAddSingleton<IDictionaryProvider>(sp => sp.GetRequiredService<EmergencyDictionary>());

        services.TryAddSingleton<TemplateRegistry>(_ => new TemplateRegistry(templateSetId: 1, templateVersion: 1));
        services.TryAddSingleton<ITemplateProvider>(sp => sp.GetRequiredService<TemplateRegistry>());

        services.TryAddSingleton<AntiReplayWindow>();
        services.TryAddSingleton<DedupCache>();
        services.TryAddSingleton<RateLimiter>(_ => new RateLimiter(options.CascadeRateLimitPerSecond));
        services.TryAddSingleton<TrustScoreService>(_ => new TrustScoreService(options.TrustScoring));
        services.TryAddSingleton<CascadeProtection>();
        services.TryAddSingleton<CascadeRouter>();

        return services;
    }

    private static IServiceCollection RegisterStandardProfile(IServiceCollection services)
    {
        services.TryAddSingleton<ConfirmationRetentionStore>();
        services.TryAddSingleton<GeoQuorumRetentionStore>();
        return services;
    }

    private static EcpOptions GetOptions(IServiceCollection services)
    {
        var options = services
            .FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(EcpOptions) &&
                descriptor.ImplementationInstance is EcpOptions)
            ?.ImplementationInstance as EcpOptions;

        if (options is null)
        {
            options = new EcpOptions();
            services.AddSingleton(options);
        }

        return options;
    }

    private static void EnsureKeyProvider(IServiceCollection services, EcpOptions options)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IKeyProvider)))
        {
            return;
        }

        if (options.KeyProvider is null)
        {
            options.KeyProvider = new KeyRing();
        }

        services.TryAddSingleton<IKeyProvider>(options.KeyProvider);
        if (options.KeyProvider is ITenantKeyProvider tenantKeyProvider)
        {
            services.TryAddSingleton<ITenantKeyProvider>(tenantKeyProvider);
        }
    }
}
