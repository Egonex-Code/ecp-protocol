// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Security;
using ECP.Core.Tenancy;
using ECP.Core.Privacy;
using ECP.Core.Strategy;

namespace ECP.Core;

/// <summary>
/// Global configuration options for ECP encoding/decoding.
/// </summary>
public sealed class EcpOptions
{
    /// <summary>
    /// Truncated HMAC length in bytes (0 or 8-16).
    /// </summary>
    public int HmacLength { get; set; } = EcpSecurity.DefaultHmacLength;

    /// <summary>
    /// Key version used for outgoing envelopes.
    /// </summary>
    public byte KeyVersion { get; set; }

    /// <summary>
    /// Key provider for resolving HMAC keys by version.
    /// </summary>
    public IKeyProvider? KeyProvider { get; set; }

    /// <summary>
    /// Default cascade rate limit per second.
    /// </summary>
    public int CascadeRateLimitPerSecond { get; set; } = 100;

    /// <summary>
    /// Default tenant identifier used when no tenant context is provided.
    /// </summary>
    public string DefaultTenantId { get; set; } = TenantDefaults.DefaultTenantId;

    /// <summary>
    /// Privacy-related options (GDPR).
    /// </summary>
    public EcpPrivacyOptions Privacy { get; set; } = new();

    /// <summary>
    /// Strategy selection thresholds for Never Worse.
    /// </summary>
    public NeverWorseOptions NeverWorse { get; set; } = new();

    /// <summary>
    /// Trust scoring thresholds and fan-out tiers.
    /// </summary>
    public TrustScoringOptions TrustScoring { get; set; } = new();
}
