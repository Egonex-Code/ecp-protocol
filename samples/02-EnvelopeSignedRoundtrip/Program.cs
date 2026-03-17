// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
// Sample 02 - signed envelope round-trip.
// Demonstrates: EnvelopeBuilder, HMAC signing, verification with correct/wrong key, unsigned mode.
// Run: dotnet run
// Prerequisites: .NET 8 SDK (dependencies restored via ProjectReference).

using System.Security.Cryptography;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;

Console.WriteLine("=== ECP SDK - Sample 02: Envelope Signed Round-trip ===");
Console.WriteLine();

byte[] hmacKey = RandomNumberGenerator.GetBytes(32);
byte[] wrongKey = RandomNumberGenerator.GetBytes(32);

var payloadToken = Ecp.Token(
    EmergencyType.Fire,
    EcpPriority.Critical,
    ActionFlags.SoundAlarm | ActionFlags.FlashLights | ActionFlags.ShowMessage,
    zoneHash: 1001);
byte[] payloadBytes = payloadToken.ToBytes();

Console.WriteLine("1) Building signed envelope...");
var signedEnvelope = Ecp.Envelope()
    .WithType(EmergencyType.Fire)
    .WithFlags(EcpFlags.NeedsConfirmation)
    .WithPriority(EcpPriority.Critical)
    .WithTtl(120)
    .WithKeyVersion(1)
    .WithPayload(payloadBytes)
    .WithHmacKey(hmacKey)
    .WithHmacLength(12)
    .Build();

byte[] signedBytes = signedEnvelope.ToBytes();
Console.WriteLine($"   Payload:    UET token ({payloadBytes.Length} bytes)");
Console.WriteLine("   HMAC key:   random 32 bytes (demo only)");
Console.WriteLine("   HMAC len:   12 bytes (default)");
Console.WriteLine($"   Key version: {signedEnvelope.KeyVersion}");
Console.WriteLine($"   Envelope:   {signedEnvelope.TotalLength} bytes total (header {EmergencyEnvelope.HeaderSize} + payload {signedEnvelope.PayloadLength} + HMAC {signedEnvelope.Hmac.Length})");
Console.WriteLine();

Console.WriteLine("2) Decoding with CORRECT key...");
try
{
    var verifiedEnvelope = EmergencyEnvelope.Decode(signedBytes, hmacKey, hmacLength: 12);
    bool parsed = Ecp.TryDecodeEnvelope(signedBytes, hmacKey, out _, hmacLength: 12);
    Console.WriteLine($"   Parsed:      {parsed.ToString().ToLowerInvariant()}");
    Console.WriteLine($"   IsValid:     {verifiedEnvelope.IsValid} {(verifiedEnvelope.IsValid ? "✓" : "⚠")}");
    Console.WriteLine($"   Priority:    {verifiedEnvelope.Priority}");
    Console.WriteLine($"   PayloadType: {verifiedEnvelope.PayloadType}");
    Console.WriteLine($"   TTL:         {verifiedEnvelope.Ttl}s");
}
catch (Exception ex)
{
    Console.WriteLine($"   Decode failed unexpectedly: {ex.GetType().Name} ⚠");
}

Console.WriteLine();
Console.WriteLine("3) Decoding with WRONG key...");
try
{
    var wrongEnvelope = EmergencyEnvelope.Decode(signedBytes, wrongKey, hmacLength: 12);
    bool parsedWrong = Ecp.TryDecodeEnvelope(signedBytes, wrongKey, out _, hmacLength: 12);
    Console.WriteLine($"   Parsed:    {parsedWrong.ToString().ToLowerInvariant()}");
    Console.WriteLine($"   IsValid:   {wrongEnvelope.IsValid} {(wrongEnvelope.IsValid ? "⚠" : "✓ (tampering detected!)")}");
}
catch (Exception ex)
{
    Console.WriteLine($"   Decode failed unexpectedly: {ex.GetType().Name} ⚠");
}

Console.WriteLine();
// ⚠ WARNING: Unsigned mode (hmacLength: 0) provides NO authentication.
// ⚠ Do NOT use in production. Messages can be forged by anyone.
// This unsigned example is for troubleshooting/interoperability demos only.
Console.WriteLine("4) Decoding unsigned (hmacLength: 0)...");
try
{
    var unsignedEnvelope = Ecp.Envelope()
        .WithType(EmergencyType.Fire)
        .WithFlags(EcpFlags.None)
        .WithPriority(EcpPriority.Critical)
        .WithTtl(120)
        .WithPayload(payloadBytes)
        .WithHmacLength(0)
        .Build();

    byte[] unsignedBytes = unsignedEnvelope.ToBytes();
    var decodedUnsigned = EmergencyEnvelope.Decode(unsignedBytes, hmacLength: 0);
    Console.WriteLine($"   Unsigned envelope length: {unsignedBytes.Length} bytes");
    Console.WriteLine($"   PayloadType: {decodedUnsigned.PayloadType}");
    Console.WriteLine("   ⚠ No cryptographic verification performed.");
    Console.WriteLine("   ⚠ IMPORTANT: Always verify HMAC before trusting payload content.");
}
catch (Exception ex)
{
    Console.WriteLine($"   Unsigned decode failed: {ex.GetType().Name} ⚠");
}
