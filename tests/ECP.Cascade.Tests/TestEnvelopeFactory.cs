// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core.Envelope;
using ECP.Core.Models;

namespace ECP.Cascade.Tests;

internal static class TestEnvelopeFactory
{
    private static readonly byte[] TestKey = new byte[]
    {
        0x10, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88,
        0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xF0, 0x01,
        0x12, 0x23, 0x34, 0x45, 0x56, 0x67, 0x78, 0x89,
        0x9A, 0xAB, 0xBC, 0xCD, 0xDE, 0xEF, 0xF1, 0x02
    };

    public static ReadOnlySpan<byte> HmacKey => TestKey;

    public static EmergencyEnvelope Create(ulong messageId, DateTimeOffset timestamp)
    {
        return new EnvelopeBuilder()
            .WithFlags(EcpFlags.Cascade)
            .WithPriority(EcpPriority.High)
            .WithTtl(5)
            .WithKeyVersion(1)
            .WithMessageId(messageId)
            .WithTimestamp((uint)timestamp.ToUnixTimeSeconds())
            .WithPayloadType(EcpPayloadType.Alert)
            .WithPayload(new byte[] { 0x01, 0x02 })
            .WithHmacKey(TestKey)
            .Build();
    }
}
