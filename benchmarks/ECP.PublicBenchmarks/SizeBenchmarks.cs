// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using BenchmarkDotNet.Attributes;
using ECP.Core.Envelope;
using ECP.Core.Security;
using ECP.Core.Token;

namespace ECP.PublicBenchmarks;

[MemoryDiagnoser]
public class SizeBenchmarks
{
    private byte[] _payloadBytes = Array.Empty<byte>();
    private byte[] _jsonBytes = Array.Empty<byte>();
    private byte[] _capBytes = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _payloadBytes = Encoding.UTF8.GetBytes("Fire at Gate B2 now");
        _jsonBytes = Encoding.UTF8.GetBytes(BuildJsonPayload(270));
        _capBytes = Encoding.UTF8.GetBytes(BuildCapPayload(669));
    }

    [Benchmark]
    public int UetSize() => UniversalEmergencyToken.Size;

    [Benchmark]
    public int EnvelopeSignedSize() => EmergencyEnvelope.HeaderSize + _payloadBytes.Length + EcpSecurity.DefaultHmacLength;

    [Benchmark]
    public int EnvelopeUnsignedSize() => EmergencyEnvelope.HeaderSize + _payloadBytes.Length;

    [Benchmark]
    public int JsonTypicalSize() => _jsonBytes.Length;

    [Benchmark]
    public int CapTypicalSize() => _capBytes.Length;

    private static string BuildJsonPayload(int targetBytes)
    {
        const string prefix = "{\"type\":\"alert\",\"message\":\"";
        const string suffix = "\"}";
        return BuildFixedSizeString(prefix, suffix, targetBytes);
    }

    private static string BuildCapPayload(int targetBytes)
    {
        const string prefix = "<alert><info><headline>";
        const string suffix = "</headline></info></alert>";
        return BuildFixedSizeString(prefix, suffix, targetBytes);
    }

    private static string BuildFixedSizeString(string prefix, string suffix, int targetBytes)
    {
        if (targetBytes < prefix.Length + suffix.Length)
        {
            return string.Concat(prefix, suffix);
        }

        int fillerLength = targetBytes - prefix.Length - suffix.Length;
        string filler = new string('x', fillerLength);
        return string.Concat(prefix, filler, suffix);
    }
}
