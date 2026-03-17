// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using System.Text;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;
using ECP.Standard;
using Microsoft.Extensions.DependencyInjection;

var hmacKey = new byte[32];
RandomNumberGenerator.Fill(hmacKey);

var uet = Ecp.Alert(EmergencyType.Fire, zoneHash: 1234, priority: EcpPriority.Critical);
Console.WriteLine($"UET size: {uet.Length} bytes");

var envelope = Ecp.Envelope()
    .WithFlags(EcpFlags.Broadcast)
    .WithPriority(EcpPriority.High)
    .WithTtl(30)
    .WithKeyVersion(1)
    .WithPayload("Evacuate now")
    .WithHmacKey(hmacKey)
    .Build();

var envelopeBytes = envelope.ToBytes();
Console.WriteLine($"Envelope size: {envelopeBytes.Length} bytes");

var decoded = Ecp.DecodeEnvelope(envelopeBytes, hmacKey);
var decodedPayload = Encoding.UTF8.GetString(decoded.Payload.Span);
Console.WriteLine($"Decoded payload: {decodedPayload}");

var services = new ServiceCollection();
services.AddEcpStandard(options =>
{
    options.KeyProvider = new KeyRing();
    options.KeyVersion = 1;
});

using var provider = services.BuildServiceProvider();
Console.WriteLine($"DI ready: {provider.GetRequiredService<EcpOptions>() is not null}");
