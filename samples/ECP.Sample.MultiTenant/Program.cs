// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using System.Text;
using ECP.Cascade.GeoQuorum;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;

const string TenantA = "Sector-Alpha";
const string TenantB = "Sector-Beta";
const byte KeyVersion = 1;

Console.WriteLine("ECP Sample - Multi-tenant (zone isolation)");
Console.WriteLine($"Tenants: {TenantA}, {TenantB}");

// Tenant-scoped keys (simulate a multi-zone facility)
var keyRing = new KeyRing();
var keyA = CreateKey();
var keyB = CreateKey();
keyRing.AddKey(TenantA, KeyVersion, keyA);
keyRing.AddKey(TenantB, KeyVersion, keyB);

// Build an alert envelope for Sector Alpha (cascade + confirmation required)
var envelopeA = BuildAlertEnvelope(
    emergencyType: EmergencyType.Security,
    payloadText: "Evacuate Sector Alpha via Exit 3",
    keyVersion: KeyVersion,
    hmacKey: keyA);

var bytesA = envelopeA.ToBytes();
var decodedA = Ecp.DecodeEnvelope(bytesA, keyA);
Console.WriteLine($"Tenant A HMAC verified: {decodedA.IsValid}");
Console.WriteLine($"Tenant A payload: \"{Encoding.UTF8.GetString(decodedA.Payload.Span)}\"");

// Show that a wrong tenant key fails verification
var wrongKeyCheck = Ecp.DecodeEnvelope(bytesA, keyB);
Console.WriteLine($"Tenant A verified with Tenant B key: {wrongKeyCheck.IsValid}");

// Build an alert envelope for Sector Beta
var envelopeB = BuildAlertEnvelope(
    emergencyType: EmergencyType.Fire,
    payloadText: "Hazard detected in Sector Beta - evacuate",
    keyVersion: KeyVersion,
    hmacKey: keyB);

var bytesB = envelopeB.ToBytes();
var decodedB = Ecp.DecodeEnvelope(bytesB, keyB);
Console.WriteLine($"Tenant B HMAC verified: {decodedB.IsValid}");
Console.WriteLine($"Tenant B payload: \"{Encoding.UTF8.GetString(decodedB.Payload.Span)}\"");

// GeoQuorum example: coverage per zone
var zones = new[]
{
    new ZoneConfirmationStats(zoneHash: 0x1A2B, confirmedCount: 42, expectedCount: 60),
    new ZoneConfirmationStats(zoneHash: 0x1A2C, confirmedCount: 18, expectedCount: 25)
};

var results = GeoQuorumCalculator.Calculate(zones);
Console.WriteLine("GeoQuorum results:");
foreach (var result in results)
{
    Console.WriteLine(
        $"Zone 0x{result.ZoneHash:X4}: {result.CoveragePercent:F1}% " +
        $"({result.ConfirmedCount}/{result.ExpectedCount})");
}

static EmergencyEnvelope BuildAlertEnvelope(
    EmergencyType emergencyType,
    string payloadText,
    byte keyVersion,
    ReadOnlySpan<byte> hmacKey)
{
    return new EnvelopeBuilder()
        // Cascade + confirmation required for critical alerts
        .WithFlags(EcpFlags.Cascade | EcpFlags.NeedsConfirmation | EcpFlags.Broadcast)
        .WithPriority(EcpPriority.Critical)
        .WithTtl(120)
        .WithKeyVersion(keyVersion)
        .WithType(emergencyType)
        .WithPayload(payloadText)
        .WithHmacKey(hmacKey)
        .Build();
}

static byte[] CreateKey()
{
    var key = new byte[32];
    RandomNumberGenerator.Fill(key);
    return key;
}
