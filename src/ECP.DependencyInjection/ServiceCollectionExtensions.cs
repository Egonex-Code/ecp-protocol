// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Linq;
using ECP.Core;
using ECP.Core.Privacy;
using ECP.Core.Security;
using ECP.Core.Strategy;
using ECP.Core.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ECP.DependencyInjection;

/// <summary>
/// Dependency injection registrations for ECP Core.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core services (codec, security, strategy).
    /// </summary>
    public static IServiceCollection AddEcpCore(this IServiceCollection services, Action<EcpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = GetOrAddOptions(services, configure);
        if (options.KeyProvider is not null)
        {
            services.TryAddSingleton<IKeyProvider>(options.KeyProvider);
            if (options.KeyProvider is ITenantKeyProvider tenantKeyProvider)
            {
                services.TryAddSingleton<ITenantKeyProvider>(tenantKeyProvider);
            }
        }

        services.TryAddSingleton<ITenantContext>(_ => new DefaultTenantContext(options.DefaultTenantId));
        services.TryAddSingleton<ITenantPrivacyOptionsProvider>(_ => new DefaultPrivacyOptionsProvider(options.Privacy));
        services.TryAddSingleton<ZoneHashProvider>();
        services.TryAddSingleton<IStrategySelector>(_ => new NeverWorseSelector(options.NeverWorse));
        return services;
    }

    private static EcpOptions GetOrAddOptions(IServiceCollection services, Action<EcpOptions>? configure)
    {
        var existing = services
            .FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(EcpOptions) &&
                descriptor.ImplementationInstance is EcpOptions)
            ?.ImplementationInstance as EcpOptions;

        if (existing is null)
        {
            var options = new EcpOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
            return options;
        }

        configure?.Invoke(existing);
        return existing;
    }
}
