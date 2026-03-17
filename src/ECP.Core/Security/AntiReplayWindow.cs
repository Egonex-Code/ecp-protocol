// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using ECP.Core.Tenancy;

namespace ECP.Core.Security;

/// <summary>
/// Tracks recently seen MessageIds and validates timestamp windows.
/// </summary>
public sealed class AntiReplayWindow
{
    private readonly TimeSpan _allowedSkew;
    private readonly TimeSpan _minimumRetention;
    private readonly int _maxEntries;
    private readonly object _sync = new();
    private readonly Dictionary<string, TenantState> _tenants = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new anti-replay window with configurable limits.
    /// </summary>
    public AntiReplayWindow(TimeSpan? allowedSkew = null, TimeSpan? minimumRetention = null, int maxEntries = 100_000)
    {
        _allowedSkew = allowedSkew ?? TimeSpan.FromMinutes(5);
        _minimumRetention = minimumRetention ?? TimeSpan.FromMinutes(10);
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Number of cached message identifiers.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_sync)
            {
                var total = 0;
                foreach (var tenant in _tenants.Values)
                {
                    total += tenant.Entries.Count;
                }

                return total;
            }
        }
    }

    /// <summary>
    /// Number of cached message identifiers for a tenant.
    /// </summary>
    public int GetCount(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            return _tenants.TryGetValue(tenantId, out var tenant)
                ? tenant.Entries.Count
                : 0;
        }
    }

    /// <summary>
    /// Attempts to accept a message based on timestamp and MessageId.
    /// </summary>
    public bool TryAccept(ulong messageId, uint timestampSeconds, byte ttlSeconds, out string? reason)
    {
        return TryAccept(TenantDefaults.DefaultTenantId, messageId, DateTimeOffset.FromUnixTimeSeconds(timestampSeconds), ttlSeconds, DateTimeOffset.UtcNow, out reason);
    }

    /// <summary>
    /// Attempts to accept a message based on timestamp and MessageId.
    /// </summary>
    public bool TryAccept(ulong messageId, DateTimeOffset timestamp, byte ttlSeconds, DateTimeOffset now, out string? reason)
    {
        return TryAccept(TenantDefaults.DefaultTenantId, messageId, timestamp, ttlSeconds, now, out reason);
    }

    /// <summary>
    /// Attempts to accept a message for a tenant based on timestamp and MessageId.
    /// </summary>
    public bool TryAccept(string tenantId, ulong messageId, DateTimeOffset timestamp, byte ttlSeconds, DateTimeOffset now, out string? reason)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        lock (_sync)
        {
            if (IsOutsideWindow(timestamp, now))
            {
                reason = "Timestamp outside allowed window.";
                return false;
            }

            var retention = GetRetention(ttlSeconds);
            var tenant = GetTenantState(tenantId);
            Purge(tenant, now, retention);

            if (tenant.Entries.ContainsKey(messageId))
            {
                reason = "Duplicate messageId.";
                return false;
            }

            tenant.Entries[messageId] = now;
            tenant.Order.Enqueue(new Entry(messageId, now));
            Trim(tenant);

            reason = null;
            return true;
        }
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _tenants.Clear();
        }
    }

    /// <summary>
    /// Clears the cache for a tenant.
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

    private bool IsOutsideWindow(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var diff = (now - timestamp).Duration();
        return diff > _allowedSkew;
    }

    private TimeSpan GetRetention(byte ttlSeconds)
    {
        var ttlRetention = TimeSpan.FromSeconds(ttlSeconds * 2);
        return ttlRetention > _minimumRetention ? ttlRetention : _minimumRetention;
    }

    private static void Purge(TenantState tenant, DateTimeOffset now, TimeSpan retention)
    {
        while (tenant.Order.Count > 0)
        {
            var entry = tenant.Order.Peek();
            if (now - entry.SeenAt <= retention)
            {
                break;
            }

            tenant.Order.Dequeue();
            tenant.Entries.Remove(entry.MessageId);
        }
    }

    private void Trim(TenantState tenant)
    {
        while (tenant.Entries.Count > _maxEntries && tenant.Order.Count > 0)
        {
            var entry = tenant.Order.Dequeue();
            tenant.Entries.Remove(entry.MessageId);
        }
    }

    private readonly record struct Entry(ulong MessageId, DateTimeOffset SeenAt);

    private TenantState GetTenantState(string tenantId)
    {
        if (!_tenants.TryGetValue(tenantId, out var tenant))
        {
            tenant = new TenantState();
            _tenants[tenantId] = tenant;
        }

        return tenant;
    }

    private sealed class TenantState
    {
        public Queue<Entry> Order { get; } = new();
        public Dictionary<ulong, DateTimeOffset> Entries { get; } = new();
    }
}
