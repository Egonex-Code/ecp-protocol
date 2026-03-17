// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
// Sample 04 - dependency injection and key rotation.
// Demonstrates: AddEcpCore(), AddEcpStandard(), AddEcpProfile(), KeyRing key rotation.
// Run: dotnet run
// Prerequisites: .NET 8 SDK (dependencies restored via ProjectReference).

using System.Security.Cryptography;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Profiles;
using ECP.Core.Security;
using ECP.DependencyInjection;
using ECP.Standard;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== ECP SDK - Sample 04: Dependency Injection & Key Rotation ===");
Console.WriteLine();

Console.WriteLine("1) Setting up KeyRing...");
byte[] keyV1 = RandomNumberGenerator.GetBytes(32);
byte[] keyV2 = RandomNumberGenerator.GetBytes(32);

var keyRing = new KeyRing();
keyRing.AddKey(1, keyV1);
keyRing.AddKey(2, keyV2);

string versions = string.Join(", ", keyRing.Versions.OrderBy(v => v));
Console.WriteLine($"   Key v1: added ({keyV1.Length} bytes)");
Console.WriteLine($"   Key v2: added ({keyV2.Length} bytes)");
Console.WriteLine($"   Active versions: {versions}");
Console.WriteLine();

Console.WriteLine("2) Registering ECP services...");
var services = new ServiceCollection();

services.AddEcpCore(options =>
{
    options.HmacLength = 12;
    options.KeyVersion = 2;
    options.KeyProvider = keyRing;
});

services.AddEcpStandard();
services.AddEcpProfile(EcpProfile.Enterprise);

using ServiceProvider provider = services.BuildServiceProvider();
EcpOptions ecpOptions = provider.GetRequiredService<EcpOptions>();

Console.WriteLine("   AddEcpCore() with KeyRing ✓");
Console.WriteLine("   AddEcpStandard() ✓");
Console.WriteLine("   AddEcpProfile(EcpProfile.Enterprise) ✓");
Console.WriteLine();

Console.WriteLine("3) Encoding envelope with key v2...");
byte[] payload = Ecp.Alert(EmergencyType.Fire, zoneHash: 1001, priority: EcpPriority.Critical);
var envelope = Ecp.Envelope()
    .WithType(EmergencyType.Fire)
    .WithFlags(EcpFlags.NeedsConfirmation)
    .WithPriority(EcpPriority.Critical)
    .WithTtl(120)
    .WithKeyVersion(ecpOptions.KeyVersion)
    .WithPayload(payload)
    .WithHmacLength(ecpOptions.HmacLength)
    .WithHmacKey(keyV2)
    .Build();

byte[] envelopeBytes = envelope.ToBytes();
Console.WriteLine($"   KeyVersion in header: {envelope.KeyVersion}");
Console.WriteLine($"   Envelope: {envelopeBytes.Length} bytes");
Console.WriteLine();

Console.WriteLine("4) Decoding with KeyRing (auto key selection)...");
if (!Ecp.TryDecode(envelopeBytes, out var decodedMessage) || !decodedMessage.IsEnvelope)
{
    Console.WriteLine("   Could not parse envelope ⚠");
}
else
{
    int hmacLength = decodedMessage.Envelope.Hmac.Length;
    byte keyVersion = decodedMessage.Envelope.KeyVersion;

    bool keyFound = keyRing.TryGetKey(keyVersion, out ReadOnlyMemory<byte> selectedKey);
    Console.WriteLine($"   Lookup key for version {keyVersion}: {(keyFound ? "found ✓" : "not found ⚠")}");

    if (keyFound)
    {
        bool parsed = Ecp.TryDecodeEnvelope(envelopeBytes, selectedKey.Span, out EmergencyEnvelope verifiedEnvelope, hmacLength);
        bool valid = parsed && verifiedEnvelope.IsValid;
        Console.WriteLine($"   IsValid: {valid.ToString().ToLowerInvariant()} {(valid ? "✓" : "⚠")}");
    }
}

Console.WriteLine();
Console.WriteLine("5) Simulating key rotation...");
keyRing.RemoveKey(1);
bool hasV1 = keyRing.TryGetKey(1, out _);
bool hasV2 = keyRing.TryGetKey(2, out _);
Console.WriteLine($"   Lookup key v1: {(hasV1 ? "found ⚠" : "not found (rotated out) ✓")}");
Console.WriteLine($"   Lookup key v2: {(hasV2 ? "found ✓" : "not found ⚠")}");
Console.WriteLine();

Console.WriteLine("6) Available profiles:");
Console.WriteLine("   EcpProfile.Minimal     -> Core only");
Console.WriteLine("   EcpProfile.Standard    -> Core + confirmation + zone-based aggregation");
Console.WriteLine("   EcpProfile.Enterprise  -> Core + Registry + Cascade");
