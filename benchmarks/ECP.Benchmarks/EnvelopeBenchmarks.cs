// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using ECP.Core;
using ECP.Core.Envelope;
using ECP.Core.Models;

namespace ECP.Benchmarks;

[MemoryDiagnoser]
public class EnvelopeBenchmarks
{
    private byte[] _hmacKey = Array.Empty<byte>();
    private byte[] _payload = Array.Empty<byte>();

    private byte[] _bytes = Array.Empty<byte>();
    private byte[] _buffer = Array.Empty<byte>();
    private EmergencyEnvelope _envelope;

    private byte[] _unsignedBytes = Array.Empty<byte>();
    private byte[] _unsignedBuffer = Array.Empty<byte>();
    private EmergencyEnvelope _unsignedEnvelope;

    [GlobalSetup]
    public void Setup()
    {
        _hmacKey = new byte[32];
        RandomNumberGenerator.Fill(_hmacKey);

        _payload = new byte[64];
        RandomNumberGenerator.Fill(_payload);

        _envelope = CreateEnvelopeSigned();
        _bytes = _envelope.ToBytes();
        _buffer = new byte[_envelope.TotalLength];

        _unsignedEnvelope = CreateEnvelopeUnsigned();
        _unsignedBytes = _unsignedEnvelope.ToBytes();
        _unsignedBuffer = new byte[_unsignedEnvelope.TotalLength];
    }

    [Benchmark]
    public byte[] EncodeEnvelope() => CreateEnvelopeSigned().ToBytes();

    [Benchmark]
    public byte[] EncodeEnvelopeUnsigned() => CreateEnvelopeUnsigned().ToBytes();

    [Benchmark]
    public byte EncodeEnvelopeNoAlloc()
    {
        _envelope.WriteTo(_buffer);
        return _buffer[0];
    }

    [Benchmark]
    public EmergencyEnvelope DecodeEnvelope() => EmergencyEnvelope.Decode(_bytes, _hmacKey);

    [Benchmark]
    public EmergencyEnvelopeView DecodeEnvelopeView() => EmergencyEnvelope.DecodeView(_bytes, _hmacKey);

    [Benchmark]
    public EmergencyEnvelope DecodeEnvelopeNoVerify() => EmergencyEnvelope.Decode(_bytes);

    [Benchmark]
    public EmergencyEnvelope DecodeEnvelopeUnsigned() => EmergencyEnvelope.Decode(_unsignedBytes, hmacLength: 0);

    [Benchmark]
    public EmergencyEnvelopeView DecodeEnvelopeViewUnsigned() => EmergencyEnvelope.DecodeView(_unsignedBytes, hmacLength: 0);

    [Benchmark]
    public bool TryDecodeAnySigned() => Ecp.TryDecode(_bytes, out _);

    [Benchmark]
    public bool TryDecodeAnyUnsigned() => Ecp.TryDecode(_unsignedBytes, out _);

    private EmergencyEnvelope CreateEnvelopeSigned()
    {
        return CreateEnvelope(hmacLength: EmergencyEnvelope.DefaultHmacLength);
    }

    private EmergencyEnvelope CreateEnvelopeUnsigned()
    {
        return CreateEnvelope(hmacLength: 0);
    }

    private EmergencyEnvelope CreateEnvelope(int hmacLength)
    {
        var builder = Ecp.Envelope()
            .WithFlags(EcpFlags.Broadcast)
            .WithPriority(EcpPriority.High)
            .WithTtl(30) // implicit cast to byte
            .WithKeyVersion(1) // implicit cast to byte
            .WithPayloadType(EcpPayloadType.Alert)
            .WithPayload(_payload)
            .WithHmacLength(hmacLength);

        if (hmacLength != 0)
        {
            builder.WithHmacKey(_hmacKey);
        }

        return builder.Build();
    }
}
