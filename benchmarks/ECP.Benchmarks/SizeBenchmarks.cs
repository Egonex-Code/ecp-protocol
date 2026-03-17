// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using BenchmarkDotNet.Attributes;
using ECP.Core.Envelope;
using ECP.Core.Models;
using ECP.Core.Security;
using ECP.Registry.Dictionary;

namespace ECP.Benchmarks;

[MemoryDiagnoser]
public class SizeBenchmarks
{
    private readonly EmergencyDictionary _dictionary = EmergencyDictionary.CreateDefault();
    private readonly byte[] _payloadBytes;
    private readonly byte[] _dictionaryGlobalPayloadBytes;
    private readonly byte[] _dictionaryTenantPayloadBytes;
    private readonly byte[] _jsonBytes;
    private readonly byte[] _capBytes;
    private readonly byte[] _dictionaryGlobalOutput;
    private readonly byte[] _dictionaryTenantOutput;
    private readonly int _uetSize = 8;
    private readonly int _defaultHmacLength = EcpSecurity.DefaultHmacLength;
    private readonly int _envelopeSizeSigned;
    private readonly int _envelopeSizeUnsigned;
    private readonly int _templateNumericPayloadLength = 6;

    public SizeBenchmarks()
    {
        _payloadBytes = Encoding.UTF8.GetBytes("Fire at Gate B2 now");
        _dictionaryGlobalPayloadBytes = Encoding.UTF8.GetBytes("immediate evacuation fire alarm now");
        _dictionaryTenantPayloadBytes = Encoding.UTF8.GetBytes("Gate B2 Terminal 3");
        _jsonBytes = Encoding.UTF8.GetBytes(BuildJsonPayload(270));
        _capBytes = Encoding.UTF8.GetBytes(BuildCapPayload(669));

        _dictionaryGlobalOutput = new byte[_dictionaryGlobalPayloadBytes.Length];
        _dictionaryTenantOutput = new byte[_dictionaryTenantPayloadBytes.Length];

        _envelopeSizeSigned = EmergencyEnvelope.HeaderSize + _payloadBytes.Length + _defaultHmacLength;
        _envelopeSizeUnsigned = EmergencyEnvelope.HeaderSize + _payloadBytes.Length;
    }

    [Benchmark]
    public int UetSize() => _uetSize;

    [Benchmark]
    public int EnvelopeSize()
    {
        return _envelopeSizeSigned;
    }

    [Benchmark]
    public int EnvelopeSizeUnsigned()
    {
        return _envelopeSizeUnsigned;
    }

    [Benchmark]
    public int JsonSize() => _jsonBytes.Length;

    [Benchmark]
    public int CapSize() => _capBytes.Length;

    [Benchmark]
    public int DictionaryCompressedSizeGlobal()
    {
        return _dictionary.TryCompress(_dictionaryGlobalPayloadBytes, _dictionaryGlobalOutput, out var written)
            ? written
            : _dictionaryGlobalPayloadBytes.Length;
    }

    [Benchmark]
    public int DictionaryCompressedSizeTenant()
    {
        return _dictionary.TryCompress(_dictionaryTenantPayloadBytes, _dictionaryTenantOutput, out var written)
            ? written
            : _dictionaryTenantPayloadBytes.Length;
    }

    [Benchmark]
    public int TemplateNumericPayloadSize()
    {
        return EmergencyEnvelope.HeaderSize + _templateNumericPayloadLength + _defaultHmacLength;
    }

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
            return prefix + suffix;
        }

        var fillerLength = targetBytes - prefix.Length - suffix.Length;
        var filler = new string('x', fillerLength);
        return string.Concat(prefix, filler, suffix);
    }
}
