// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;
using ECP.Core.Tenancy;

namespace ECP.Cascade;

/// <summary>
/// Propagation logic for cascade broadcast.
/// </summary>
public sealed class CascadeRouter
{
    private readonly CascadeProtection _protection;
    private readonly TrustScoreService _trust;
    private readonly IKeyProvider _keyProvider;

    /// <summary>
    /// Creates a cascade router with required services.
    /// </summary>
    public CascadeRouter(CascadeProtection protection, TrustScoreService trust, IKeyProvider keyProvider)
    {
        _protection = protection ?? throw new ArgumentNullException(nameof(protection));
        _trust = trust ?? throw new ArgumentNullException(nameof(trust));
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    }

    /// <summary>
    /// Evaluates whether to forward and returns a forwarding decision.
    /// </summary>
    public CascadeDecision Evaluate(EmergencyEnvelope envelope, string nodeId, DateTimeOffset now)
    {
        return Evaluate(envelope, nodeId, now, TenantDefaults.DefaultTenantId);
    }

    /// <summary>
    /// Evaluates whether to forward and returns a forwarding decision for a tenant.
    /// </summary>
    public CascadeDecision Evaluate(EmergencyEnvelope envelope, string nodeId, DateTimeOffset now, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return Evaluate(envelope, nodeId, now, tenantContext.TenantId);
    }

    /// <summary>
    /// Evaluates whether to forward and returns a forwarding decision for a tenant.
    /// </summary>
    public CascadeDecision Evaluate(EmergencyEnvelope envelope, string nodeId, DateTimeOffset now, string tenantId)
    {
        if (!envelope.Flags.HasFlag(EcpFlags.Cascade))
        {
            return CascadeDecision.Reject("Cascade flag not set.");
        }

        if (!envelope.IsValid)
        {
            return CascadeDecision.Reject("Envelope signature not verified.");
        }

        if (envelope.Ttl == 0)
        {
            return CascadeDecision.Reject("TTL expired.");
        }

        if (!_protection.TryAccept(envelope, tenantId, now, out var reason))
        {
            return CascadeDecision.Reject(reason);
        }

        if (!TryGetKey(tenantId, envelope.KeyVersion, out var key))
        {
            return CascadeDecision.Reject("HMAC key not available.");
        }

        var forwarded = BuildForwardedEnvelope(envelope, key.Span);
        var fanOut = _trust.GetFanOutLimit(nodeId);
        return CascadeDecision.Forward(forwarded, fanOut, "Accepted for cascade.");
    }

    private bool TryGetKey(string tenantId, byte keyVersion, out ReadOnlyMemory<byte> key)
    {
        if (_keyProvider is ITenantKeyProvider tenantKeyProvider)
        {
            return tenantKeyProvider.TryGetKey(tenantId, keyVersion, out key);
        }

        return _keyProvider.TryGetKey(keyVersion, out key);
    }

    private static EmergencyEnvelope BuildForwardedEnvelope(EmergencyEnvelope envelope, ReadOnlySpan<byte> hmacKey)
    {
        var hmacLength = envelope.Hmac.Length == 0 ? EmergencyEnvelope.DefaultHmacLength : envelope.Hmac.Length;
        var builder = new EnvelopeBuilder()
            .WithFlags(envelope.Flags)
            .WithPriority(envelope.Priority)
            .WithTtl((byte)(envelope.Ttl - 1))
            .WithKeyVersion(envelope.KeyVersion)
            .WithMessageId(envelope.MessageId)
            .WithTimestamp(envelope.Timestamp)
            .WithPayloadType(envelope.PayloadType)
            .WithPayload(envelope.Payload.Span)
            .WithHmacLength(hmacLength)
            .WithHmacKey(hmacKey);

        return builder.Build();
    }
}
