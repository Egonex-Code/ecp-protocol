// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Negotiation;
using ECP.Core.Security;

namespace ECP.Transport.Abstractions;

/// <summary>
/// Shared helpers for transport implementations.
/// </summary>
public static class EcpTransportHelper
{
    /// <summary>
    /// Attempts to resolve a key for a tenant and key version.
    /// </summary>
    public static bool TryGetKey(IKeyProvider keyProvider, string tenantId, byte keyVersion, out ReadOnlyMemory<byte> key)
    {
        if (keyProvider is ITenantKeyProvider tenantKeyProvider)
        {
            return tenantKeyProvider.TryGetKey(tenantId, keyVersion, out key);
        }

        return keyProvider.TryGetKey(keyVersion, out key);
    }

    /// <summary>
    /// Builds a capability handshake envelope.
    /// </summary>
    public static EmergencyEnvelope BuildHandshakeEnvelope(CapabilityNegotiationPayload offer, EcpOptions ecpOptions, ReadOnlySpan<byte> key)
    {
        return new EnvelopeBuilder()
            .WithFlags(EcpFlags.None)
            .WithPriority(EcpPriority.Low)
            .WithTtl(1)
            .WithKeyVersion(ecpOptions.KeyVersion)
            .WithPayloadType(EcpPayloadType.CapabilityNegotiation)
            .WithPayload(offer.ToBytes())
            .WithHmacLength(ecpOptions.HmacLength)
            .WithHmacKey(key)
            .Build();
    }

    /// <summary>
    /// Attempts to process a capability negotiation envelope.
    /// </summary>
    public static bool TryHandleCapabilityNegotiation(
        EmergencyEnvelope envelope,
        CapabilityHandshake? handshake,
        string peerId,
        TaskCompletionSource<CapabilityNegotiationResult>? handshakeTcs,
        out CapabilityNegotiationResult? negotiated)
    {
        negotiated = null;
        if (handshake is null || envelope.PayloadType != EcpPayloadType.CapabilityNegotiation)
        {
            return false;
        }

        if (!CapabilityNegotiationPayload.TryFromBytes(envelope.Payload.Span, out var offer))
        {
            return false;
        }

        if (handshake.TryProcessOffer(peerId, offer, out var result))
        {
            handshakeTcs?.TrySetResult(result);
            negotiated = result;
        }

        return true;
    }

    /// <summary>
    /// Creates a handshake completion source.
    /// </summary>
    public static TaskCompletionSource<CapabilityNegotiationResult> CreateHandshakeTcs()
    {
        return new TaskCompletionSource<CapabilityNegotiationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Invokes all registered message handlers asynchronously.
    /// </summary>
    public static async Task InvokeHandlersAsync(Func<ReadOnlyMemory<byte>, Task>? handlers, ReadOnlyMemory<byte> data)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            await ((Func<ReadOnlyMemory<byte>, Task>)handler)(data).ConfigureAwait(false);
        }
    }
}
