// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using ECP.Core.Tenancy;

namespace ECP.Core.Security;

/// <summary>
/// In-memory key ring for key rotation scenarios.
/// </summary>
public sealed class KeyRing : ITenantKeyProvider
{
    private readonly Dictionary<string, Dictionary<byte, byte[]>> _tenants = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// Adds or replaces a key for the specified version.
    /// </summary>
    public void AddKey(byte keyVersion, ReadOnlySpan<byte> key)
    {
        AddKey(TenantDefaults.DefaultTenantId, keyVersion, key);
    }

    /// <summary>
    /// Adds or replaces a key for the specified tenant and version.
    /// </summary>
    public void AddKey(string tenantId, byte keyVersion, ReadOnlySpan<byte> key)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            var keys = GetTenantKeys(tenantId);
            keys[keyVersion] = key.ToArray();
        }
    }

    /// <summary>
    /// Removes a key for the specified version.
    /// </summary>
    public bool RemoveKey(byte keyVersion)
    {
        return RemoveKey(TenantDefaults.DefaultTenantId, keyVersion);
    }

    /// <summary>
    /// Removes a key for the specified tenant and version.
    /// </summary>
    public bool RemoveKey(string tenantId, byte keyVersion)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            return _tenants.TryGetValue(tenantId, out var keys) && keys.Remove(keyVersion);
        }
    }

    /// <summary>
    /// Returns true when a key for the version exists.
    /// </summary>
    public bool TryGetKey(byte keyVersion, out ReadOnlyMemory<byte> key)
    {
        return TryGetKey(TenantDefaults.DefaultTenantId, keyVersion, out key);
    }

    /// <summary>
    /// Returns true when a key for the tenant and version exists.
    /// </summary>
    public bool TryGetKey(string tenantId, byte keyVersion, out ReadOnlyMemory<byte> key)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            if (_tenants.TryGetValue(tenantId, out var keys) &&
                keys.TryGetValue(keyVersion, out var value))
            {
                key = value;
                return true;
            }

            key = default;
            return false;
        }
    }

    /// <summary>
    /// Exposes the versions currently stored for the default tenant.
    /// </summary>
    public IReadOnlyCollection<byte> Versions => GetVersions(TenantDefaults.DefaultTenantId);

    /// <summary>
    /// Exposes the versions currently stored for a tenant.
    /// </summary>
    public IReadOnlyCollection<byte> GetVersions(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            return _tenants.TryGetValue(tenantId, out var keys)
                ? keys.Keys.ToArray()
                : Array.Empty<byte>();
        }
    }

    private Dictionary<byte, byte[]> GetTenantKeys(string tenantId)
    {
        if (!_tenants.TryGetValue(tenantId, out var keys))
        {
            keys = new Dictionary<byte, byte[]>();
            _tenants[tenantId] = keys;
        }

        return keys;
    }
}
