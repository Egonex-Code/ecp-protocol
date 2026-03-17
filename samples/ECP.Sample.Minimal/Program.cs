// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text;
using ECP.Core;
using ECP.Core.Models;
using ECP.Core.Token;

Console.WriteLine("ECP Sample - Minimal (IoT/embedded)");

// Build a UET token without allocations
var token = Ecp.Token(
    emergencyType: EmergencyType.Fire,
    priority: EcpPriority.Critical,
    actionFlags: ActionFlags.SoundAlarm | ActionFlags.ShowMessage,
    zoneHash: 0x1A2B);

Span<byte> buffer = stackalloc byte[UniversalEmergencyToken.Size];
token.WriteTo(buffer);

Console.WriteLine($"UET bytes: {ToHex(buffer)}");

// Decode back (zero allocations)
if (Ecp.TryDecodeToken(buffer, out var decoded))
{
    Console.WriteLine($"EmergencyType: {decoded.EmergencyType}");
    Console.WriteLine($"Priority: {decoded.Priority}");
    Console.WriteLine($"ActionFlags: {decoded.ActionFlags}");
    Console.WriteLine($"ZoneHash: 0x{decoded.ZoneHash:X4}");
}

// Compare size vs JSON
var json = "{\"type\":\"fire\",\"priority\":\"critical\",\"zone\":\"A1\"}";
var jsonSize = Encoding.UTF8.GetByteCount(json);
Console.WriteLine($"JSON size: {jsonSize} bytes");
Console.WriteLine($"UET size: {UniversalEmergencyToken.Size} bytes");
Console.WriteLine($"Size reduction: {ComputeSavings(UniversalEmergencyToken.Size, jsonSize):P0}");

static string ToHex(ReadOnlySpan<byte> data)
{
    var sb = new StringBuilder(data.Length * 3);
    for (var i = 0; i < data.Length; i++)
    {
        if (i > 0)
        {
            sb.Append(' ');
        }

        sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
    }

    return sb.ToString();
}

static double ComputeSavings(int ecpSize, int legacySize)
{
    if (legacySize <= 0)
    {
        return 0d;
    }

    return 1d - (ecpSize / (double)legacySize);
}
