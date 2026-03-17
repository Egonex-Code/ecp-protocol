// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
namespace ECP.Core.Tenancy;

/// <summary>
/// Provides the current tenant identifier.
/// </summary>
public interface ITenantContext
{
    /// <summary>Tenant identifier.</summary>
    string TenantId { get; }
}

/// <summary>
/// Default tenant identifiers and helpers.
/// </summary>
public static class TenantDefaults
{
    /// <summary>Default tenant id used when none is provided.</summary>
    public const string DefaultTenantId = "default";
}

/// <summary>
/// Default tenant context implementation.
/// </summary>
public sealed class DefaultTenantContext : ITenantContext
{
    /// <inheritdoc />
    public string TenantId { get; }

    /// <summary>
    /// Creates a default tenant context.
    /// </summary>
    public DefaultTenantContext(string? tenantId = null)
    {
        TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? TenantDefaults.DefaultTenantId
            : tenantId;
    }
}
