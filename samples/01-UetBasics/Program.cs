// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
// Sample 01 - UET basics.
// Demonstrates: Ecp.Alert(), Ecp.Token(), ToBytes(), ToBase64(), TryDecodeToken().
// Run: dotnet run
// Prerequisites: .NET 8 SDK (dependencies restored via ProjectReference).

using ECP.Core;
using ECP.Core.Models;
using ECP.Core.Token;

Console.WriteLine("=== ECP SDK - Sample 01: UET Basics ===");
Console.WriteLine();

Console.WriteLine("1) Creating alert (one-liner)...");
byte[] oneLinerAlert = Ecp.Alert(EmergencyType.Fire, zoneHash: 1001, priority: EcpPriority.Critical);
Console.WriteLine($"   Ecp.Alert(Fire, zone: 1001, Critical) -> {oneLinerAlert.Length} bytes");
Console.WriteLine($"   Hex: {ToHex(oneLinerAlert)}");
Console.WriteLine();

Console.WriteLine("2) Creating token with full control...");
ActionFlags actions = ActionFlags.SoundAlarm | ActionFlags.FlashLights | ActionFlags.ShowMessage;
var token = Ecp.Token(
    EmergencyType.Fire,
    EcpPriority.Critical,
    actions,
    zoneHash: 1001,
    timestampMinutes: null,
    confirmHash: 0);

byte[] tokenBytes = token.ToBytes();
string tokenBase64 = token.ToBase64();

Console.WriteLine($"   Type:       {token.EmergencyType}");
Console.WriteLine($"   Priority:   {token.Priority}");
Console.WriteLine($"   Actions:    {FormatFlags(token.ActionFlags)}");
Console.WriteLine($"   Zone:       {token.ZoneHash}");
Console.WriteLine($"   Timestamp:  {token.TimestampMinutes} minutes (auto)");
Console.WriteLine($"   Confirm:    {token.ConfirmHash}");
Console.WriteLine($"   RawValue:   0x{token.RawValue:X16}");
Console.WriteLine($"   Encoded:    {tokenBytes.Length} bytes");
Console.WriteLine($"   Base64:     {tokenBase64}");
Console.WriteLine();

Console.WriteLine("3) Decoding back...");
bool decoded = Ecp.TryDecodeToken(tokenBytes, out UniversalEmergencyToken decodedToken);
Console.WriteLine($"   TryDecodeToken: {decoded.ToString().ToLowerInvariant()}");

if (decoded)
{
    bool typeMatch = decodedToken.EmergencyType == token.EmergencyType;
    bool priorityMatch = decodedToken.Priority == token.Priority;
    bool actionsMatch = decodedToken.ActionFlags == token.ActionFlags;
    bool zoneMatch = decodedToken.ZoneHash == token.ZoneHash;
    bool roundTripSuccess = decodedToken.RawValue == token.RawValue;

    Console.WriteLine($"   Type:       {decodedToken.EmergencyType} {(typeMatch ? "✓" : "⚠")}");
    Console.WriteLine($"   Priority:   {decodedToken.Priority} {(priorityMatch ? "✓" : "⚠")}");
    Console.WriteLine($"   Actions:    {FormatFlags(decodedToken.ActionFlags)} {(actionsMatch ? "✓" : "⚠")}");
    Console.WriteLine($"   Zone:       {decodedToken.ZoneHash} {(zoneMatch ? "✓" : "⚠")}");
    Console.WriteLine($"   Round-trip: {(roundTripSuccess ? "SUCCESS ✓" : "FAILED ⚠")}");
}
else
{
    Console.WriteLine("   Round-trip: FAILED ⚠");
}

Console.WriteLine();
Console.WriteLine("4) Size comparison:");
Console.WriteLine($"   UET:  {tokenBytes.Length} bytes");
Console.WriteLine("   JSON: ~270 bytes (typical CAP alert)");
Console.WriteLine("   CAP:  ~669 bytes");

static string ToHex(ReadOnlySpan<byte> bytes)
{
    return string.Join(" ", bytes.ToArray().Select(b => b.ToString("X2")));
}

static string FormatFlags(ActionFlags flags)
{
    string value = flags.ToString();
    return value.Replace(", ", " | ", StringComparison.Ordinal);
}
