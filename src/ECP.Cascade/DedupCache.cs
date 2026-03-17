// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Tenancy;

namespace ECP.Cascade;

/// <summary>
/// Deduplication cache for message identifiers with time-based retention.
/// </summary>
public sealed class DedupCache
{
    private readonly Dictionary<string, TenantState> _tenants = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// Attempts to add a message identifier, returning false if it already exists.
    /// </summary>
    public bool TryAdd(ulong messageId, DateTimeOffset now, TimeSpan retention)
    {
        return TryAdd(TenantDefaults.DefaultTenantId, messageId, now, retention);
    }

    /// <summary>
    /// Attempts to add a message identifier for a tenant, returning false if it already exists.
    /// </summary>
    public bool TryAdd(string tenantId, ulong messageId, DateTimeOffset now, TimeSpan retention)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be positive.");
        }

        lock (_sync)
        {
            var tenant = GetTenantState(tenantId);
            PurgeExpired(tenant, now);

            if (tenant.Entries.ContainsKey(messageId))
            {
                return false;
            }

            var expiresAt = now.UtcTicks + retention.Ticks;
            tenant.Entries[messageId] = expiresAt;
            tenant.Expirations.Enqueue(messageId, expiresAt);
            return true;
        }
    }

    /// <summary>
    /// Clears cached entries for a tenant.
    /// </summary>
    public void Clear(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            _tenants.Remove(tenantId);
        }
    }

    private TenantState GetTenantState(string tenantId)
    {
        if (!_tenants.TryGetValue(tenantId, out var state))
        {
            state = new TenantState();
            _tenants[tenantId] = state;
        }

        return state;
    }

    private static void PurgeExpired(TenantState tenant, DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;
        while (tenant.Expirations.Count > 0)
        {
            if (!tenant.Expirations.TryPeek(out var messageId, out var expiresAt))
            {
                break;
            }

            if (expiresAt > nowTicks)
            {
                break;
            }

            tenant.Expirations.Dequeue();
            tenant.Entries.Remove(messageId);
        }
    }

    private sealed class TenantState
    {
        public Dictionary<ulong, long> Entries { get; } = new();
        public PriorityQueue<ulong, long> Expirations { get; } = new();
    }
}
