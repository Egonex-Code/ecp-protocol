// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Privacy;
using ECP.Core.Tenancy;

namespace ECP.Cascade.GeoQuorum;

/// <summary>
/// In-memory retention store for geo-quorum results.
/// </summary>
public sealed class GeoQuorumRetentionStore
{
    private readonly ITenantPrivacyOptionsProvider _optionsProvider;
    private readonly Dictionary<string, List<Entry>> _entries = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// Creates a retention store using the provided privacy options provider.
    /// </summary>
    public GeoQuorumRetentionStore(ITenantPrivacyOptionsProvider optionsProvider)
    {
        _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
    }

    /// <summary>
    /// Adds a geo-quorum result set for the default tenant.
    /// </summary>
    public void Add(IReadOnlyList<GeoQuorumResult> results, DateTimeOffset calculatedAt)
    {
        Add(TenantDefaults.DefaultTenantId, results, calculatedAt);
    }

    /// <summary>
    /// Adds a geo-quorum result set for a tenant.
    /// </summary>
    public void Add(string tenantId, IReadOnlyList<GeoQuorumResult> results, DateTimeOffset calculatedAt)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        ArgumentNullException.ThrowIfNull(results);

        lock (_sync)
        {
            var list = GetTenantList(tenantId);
            list.Add(new Entry(results.ToArray(), calculatedAt));
            PurgeInternal(tenantId, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// Returns the current geo-quorum results for a tenant after purging expired entries.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<GeoQuorumResult>> GetAll(string tenantId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            if (!_entries.TryGetValue(tenantId, out var list))
            {
                return Array.Empty<IReadOnlyList<GeoQuorumResult>>();
            }

            PurgeInternal(tenantId, now);
            return list.Select(entry => (IReadOnlyList<GeoQuorumResult>)entry.Results.ToArray()).ToArray();
        }
    }

    /// <summary>
    /// Clears retained geo-quorum results for a tenant.
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

    private readonly record struct Entry(GeoQuorumResult[] Results, DateTimeOffset Timestamp);
}
