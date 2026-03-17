// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using BenchmarkDotNet.Attributes;
using ECP.Core;
using ECP.Core.Models;
using ECP.Core.Token;

namespace ECP.PublicBenchmarks;

[MemoryDiagnoser]
public class UetBenchmarks
{
    private UniversalEmergencyToken _token;
    private byte[] _bytes = Array.Empty<byte>();
    private ushort _zoneHash;
    private ushort _timestampMinutes;
    private uint _confirmHash;

    [GlobalSetup]
    public void Setup()
    {
        _zoneHash = 0xABCD;
        _timestampMinutes = 12345;
        _confirmHash = 0x2AAAA;

        _token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Critical,
            ActionFlags.None,
            zoneHash: _zoneHash,
            timestampMinutes: _timestampMinutes,
            confirmHash: _confirmHash);

        _bytes = _token.ToBytes();
    }

    [Benchmark]
    public byte[] EncodeUet() => _token.ToBytes();

    [Benchmark]
    public byte EncodeUetNoAlloc()
    {
        Span<byte> buffer = stackalloc byte[UniversalEmergencyToken.Size];
        _token.WriteTo(buffer);
        return buffer[0];
    }

    [Benchmark]
    public UniversalEmergencyToken DecodeUet() => UniversalEmergencyToken.FromBytes(_bytes);

    [Benchmark]
    public byte[] EncodeUetOneLiner() => Ecp.Alert(
        EmergencyType.Fire,
        zoneHash: _zoneHash,
        priority: EcpPriority.Critical,
        actionFlags: ActionFlags.None,
        timestampMinutes: _timestampMinutes,
        confirmHash: _confirmHash);

    [Benchmark]
    public UniversalEmergencyToken DecodeUetFacade() => Ecp.DecodeToken(_bytes);

    [Benchmark]
    public bool TryDecodeAny() => Ecp.TryDecode(_bytes, out _);
}
