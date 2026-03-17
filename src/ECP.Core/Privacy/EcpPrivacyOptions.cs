// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Privacy;

/// <summary>
/// Privacy-related configuration options.
/// </summary>
public sealed class EcpPrivacyOptions
{
    /// <summary>
    /// Epoch duration used to rotate zone hash salts.
    /// </summary>
    public TimeSpan EpochDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Retention window for confirmation/geo-quorum data.
    /// </summary>
    public TimeSpan ConfirmationRetention { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// When true, zone hashes are anonymized with epoch salts.
    /// </summary>
    public bool AnonymizeZoneHash { get; set; } = true;

    /// <summary>
    /// Optional base salt for zone hash anonymization.
    /// </summary>
    public ReadOnlyMemory<byte> ZoneHashSalt { get; set; } = ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Opt-in flag for heartbeat features (future).
    /// </summary>
    public bool HeartbeatOptIn { get; set; }
}

/// <summary>
/// Provides privacy options per tenant.
/// </summary>
public interface ITenantPrivacyOptionsProvider
{
    /// <summary>
    /// Returns privacy options for a tenant.
    /// </summary>
    EcpPrivacyOptions GetOptions(string tenantId);
}

/// <summary>
/// Default privacy options provider (same options for all tenants).
/// </summary>
public sealed class DefaultPrivacyOptionsProvider : ITenantPrivacyOptionsProvider
{
    private readonly EcpPrivacyOptions _options;

    /// <summary>
    /// Creates a provider that returns the provided options for every tenant.
    /// </summary>
    public DefaultPrivacyOptionsProvider(EcpPrivacyOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public EcpPrivacyOptions GetOptions(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        return _options;
    }
}
