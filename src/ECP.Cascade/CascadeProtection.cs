// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Envelope;
using ECP.Core.Security;
using ECP.Core.Tenancy;

namespace ECP.Cascade;

/// <summary>
/// Anti-abuse protections for cascade propagation.
/// </summary>
public sealed class CascadeProtection
{
    private static readonly TimeSpan MinimumDedupRetention = TimeSpan.FromMinutes(10);

    private readonly AntiReplayWindow _antiReplay;
    private readonly DedupCache _dedup;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Creates a protection layer with provided components.
    /// </summary>
    public CascadeProtection(AntiReplayWindow antiReplay, DedupCache dedup, RateLimiter rateLimiter)
    {
        _antiReplay = antiReplay ?? throw new ArgumentNullException(nameof(antiReplay));
        _dedup = dedup ?? throw new ArgumentNullException(nameof(dedup));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    /// <summary>
    /// Validates rate limit, deduplication, and anti-replay constraints.
    /// </summary>
    public bool TryAccept(EmergencyEnvelope envelope, DateTimeOffset now, out string reason)
    {
        return TryAccept(envelope, TenantDefaults.DefaultTenantId, now, out reason);
    }

    /// <summary>
    /// Validates rate limit, deduplication, and anti-replay constraints for a tenant.
    /// </summary>
    public bool TryAccept(EmergencyEnvelope envelope, ITenantContext tenantContext, DateTimeOffset now, out string reason)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return TryAccept(envelope, tenantContext.TenantId, now, out reason);
    }

    /// <summary>
    /// Validates rate limit, deduplication, and anti-replay constraints for a tenant.
    /// </summary>
    public bool TryAccept(EmergencyEnvelope envelope, string tenantId, DateTimeOffset now, out string reason)
    {
        if (!_rateLimiter.TryAcquire(tenantId, now))
        {
            reason = "Rate limit exceeded.";
            return false;
        }

        var retention = GetRetention(envelope.Ttl);
        if (!_dedup.TryAdd(tenantId, envelope.MessageId, now, retention))
        {
            reason = "Duplicate MessageId detected.";
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(envelope.Timestamp);
        if (!_antiReplay.TryAccept(tenantId, envelope.MessageId, timestamp, envelope.Ttl, now, out var antiReplayReason))
        {
            reason = antiReplayReason ?? "Anti-replay rejected.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static TimeSpan GetRetention(byte ttl)
    {
        var ttlSeconds = Math.Max(1, ttl * 2);
        var retention = TimeSpan.FromSeconds(ttlSeconds);
        return retention < MinimumDedupRetention ? MinimumDedupRetention : retention;
    }
}
