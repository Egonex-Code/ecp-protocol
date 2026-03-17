// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
// Sample 03 - generic decode router.
// Demonstrates: Ecp.TryDecode() automatic routing between UET and Envelope.
// Run: dotnet run
// Prerequisites: .NET 8 SDK (dependencies restored via ProjectReference).

using System.Security.Cryptography;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Token;

Console.WriteLine("=== ECP SDK - Sample 03: Generic Decode Router ===");
Console.WriteLine("Scenario: you receive raw bytes and need to determine the message type.");
Console.WriteLine();

byte[] uetBytes = Ecp.Alert(EmergencyType.Fire, zoneHash: 1001, priority: EcpPriority.Critical);

byte[] hmacKey = RandomNumberGenerator.GetBytes(32);
byte[] envelopeBytes = Ecp.Envelope()
    .WithType(EmergencyType.Fire)
    .WithFlags(EcpFlags.NeedsConfirmation)
    .WithPriority(EcpPriority.Critical)
    .WithTtl(120)
    .WithKeyVersion(1)
    .WithPayload(uetBytes)
    .WithHmacKey(hmacKey)
    .WithHmacLength(12)
    .Build()
    .ToBytes();

byte[] invalidBytes = [0xEC, 0x50, 0x01, 0x00, 0xFF];

Route("1) Receiving 8 bytes (UET)...", uetBytes);
Console.WriteLine();
Route($"2) Receiving {envelopeBytes.Length} bytes (signed Envelope)...", envelopeBytes);
Console.WriteLine();
Route($"3) Receiving {invalidBytes.Length} bytes (invalid)...", invalidBytes);
Console.WriteLine();

Console.WriteLine("Routing pattern:");
Console.WriteLine("  if (message.IsUet)     -> handle token");
Console.WriteLine("  if (message.IsEnvelope)-> handle envelope");

void Route(string label, byte[] rawBytes)
{
    Console.WriteLine(label);
    bool decoded = Ecp.TryDecode(rawBytes, out var message);
    Console.WriteLine($"   TryDecode: {decoded.ToString().ToLowerInvariant()}");

    if (!decoded)
    {
        Console.WriteLine("   -> Unknown format, discard.");
        return;
    }

    Console.WriteLine($"   Kind: {message.Kind}");

    if (message.IsUet)
    {
        var token = message.Token;
        Console.WriteLine($"   -> {token.EmergencyType} / {token.Priority} / Zone {token.ZoneHash}");
        return;
    }

    if (message.IsEnvelope)
    {
        // ⚠ PRODUCTION NOTE: Ecp.TryDecode() parses format but does not authenticate trust.
        // Always verify HMAC on signed envelopes before trusting payload contents (see Sample 02).
        EmergencyEnvelope envelope = message.Envelope;
        Console.WriteLine($"   -> {envelope.PayloadType} / {envelope.PayloadLength} bytes payload");
    }
}
