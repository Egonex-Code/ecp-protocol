// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Privacy;
using ECP.Core.Tenancy;

namespace ECP.Cascade.Confirmation;

/// <summary>
/// In-memory retention store for aggregated confirmations.
/// </summary>
public sealed class ConfirmationRetentionStore
{
    private readonly ITenantPrivacyOptionsProvider _optionsProvider;
    private readonly Dictionary<string, List<Entry>> _entries = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// Creates a retention store using the provided privacy options provider.
    /// </summary>
    public ConfirmationRetentionStore(ITenantPrivacyOptionsProvider optionsProvider)
    {
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
    }

    /// <summary>
    /// Adds an aggregated confirmation for the default tenant.
    /// </summary>
    public void Add(AggregatedConfirmation confirmation)
    {
        Add(TenantDefaults.DefaultTenantId, confirmation);
    }

    /// <summary>
    /// Adds an aggregated confirmation for a tenant.
    /// </summary>
    public void Add(string tenantId, AggregatedConfirmation confirmation)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        ArgumentNullException.ThrowIfNull(confirmation);

        lock (_sync)
        {
            var list = GetTenantList(tenantId);
            list.Add(new Entry(confirmation, confirmation.AggregatedAt));
            PurgeInternal(tenantId, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// Returns the current confirmations for a tenant after purging expired entries.
    /// </summary>
    public IReadOnlyList<AggregatedConfirmation> GetAll(string tenantId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            if (!_entries.TryGetValue(tenantId, out var list))
            {
                return Array.Empty<AggregatedConfirmation>();
            }

            PurgeInternal(tenantId, now);
            return list.Select(entry => entry.Confirmation).ToArray();
        }
    }

    /// <summary>
    /// Clears retained confirmations for a tenant.
    /// </summary>
    public void Clear(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            _entries.Remove(tenantId);
        }
    }

    private List<Entry> GetTenantList(string tenantId)
    {
        if (!_entries.TryGetValue(tenantId, out var list))
        {
            list = new List<Entry>();
            _entries[tenantId] = list;
        }

        return list;
    }

    private void PurgeInternal(string tenantId, DateTimeOffset now)
    {
        if (!_entries.TryGetValue(tenantId, out var list) || list.Count == 0)
        {
            return;
        }

        var retention = _optionsProvider.GetOptions(tenantId).ConfirmationRetention;
        if (retention <= TimeSpan.Zero)
        {
            list.Clear();
            return;
        }

        list.RemoveAll(entry => now - entry.Timestamp > retention);
    }

    private readonly record struct Entry(AggregatedConfirmation Confirmation, DateTimeOffset Timestamp);
}
