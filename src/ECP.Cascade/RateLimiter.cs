// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Tenancy;

namespace ECP.Cascade;

/// <summary>
/// Simple per-node rate limiter based on per-second windows.
/// </summary>
public sealed class RateLimiter
{
    private readonly int _maxPerSecond;
    private readonly Dictionary<string, TenantState> _tenants = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// Creates a rate limiter with a maximum number of events per second.
    /// </summary>
    public RateLimiter(int maxPerSecond)
    {
        if (maxPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPerSecond), "Rate limit must be positive.");
        }

        _maxPerSecond = maxPerSecond;
    }

    /// <summary>
    /// Attempts to acquire a slot for the given time.
    /// </summary>
    public bool TryAcquire(DateTimeOffset now)
    {
        return TryAcquire(TenantDefaults.DefaultTenantId, now);
    }

    /// <summary>
    /// Attempts to acquire a slot for the given time and tenant.
    /// </summary>
    public bool TryAcquire(string tenantId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId must be provided.", nameof(tenantId));
        }

        var second = now.ToUnixTimeSeconds();
        lock (_sync)
        {
            var tenant = GetTenantState(tenantId);
            if (second != tenant.CurrentSecond)
            {
                tenant.CurrentSecond = second;
                tenant.Count = 0;
            }

            if (tenant.Count >= _maxPerSecond)
            {
                return false;
            }

            tenant.Count++;
            return true;
        }
    }

    /// <summary>
    /// Clears rate limit state for a tenant.
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

    private sealed class TenantState
    {
        public long CurrentSecond { get; set; }
        public int Count { get; set; }
    }
}
