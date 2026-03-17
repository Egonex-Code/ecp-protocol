// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using ECP.Core;
using ECP.Core.Models;
using ECP.Core.Token;
using Xunit;

namespace ECP.Core.Tests;

public class UniversalEmergencyTokenTests
{
    [Fact]
    public void CreateAndDecodeRoundtrip()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Security,
            EcpPriority.High,
            ActionFlags.SoundAlarm | ActionFlags.FlashLights,
            zoneHash: 0xBEEF,
            timestampMinutes: 0x1234,
            confirmHash: 0x2AAAA);

        var bytes = token.ToBytes();
        var decoded = UniversalEmergencyToken.FromBytes(bytes);

        Assert.Equal(token.EmergencyType, decoded.EmergencyType);
        Assert.Equal(token.Priority, decoded.Priority);
        Assert.Equal(token.ActionFlags, decoded.ActionFlags);
        Assert.Equal(token.ZoneHash, decoded.ZoneHash);
        Assert.Equal(token.TimestampMinutes, decoded.TimestampMinutes);
        Assert.Equal(token.ConfirmHash, decoded.ConfirmHash);
    }

    [Fact]
    public void TokenIsExactly8Bytes()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Critical,
            ActionFlags.None,
            zoneHash: 0xABCD,
            timestampMinutes: 1,
            confirmHash: 0);

        Assert.Equal(UniversalEmergencyToken.Size, token.ToBytes().Length);
    }

    [Fact]
    public void BitLayoutMatchesSpec()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Security,
            EcpPriority.High,
            (ActionFlags)0xAC,
            zoneHash: 0x1234,
            timestampMinutes: 0x5678,
            confirmHash: 0x2AAAA);

        var expected = BuildExpectedBytes(
            emergencyType: (byte)EmergencyType.Security,
            priority: (byte)EcpPriority.High,
            actionFlags: 0xAC,
            zoneHash: 0x1234,
            timestampMinutes: 0x5678,
            confirmHash: 0x2AAAA);

        Assert.Equal(expected, token.ToBytes());
    }

    [Fact]
    public void ToBase64IsMax12Chars()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Critical,
            ActionFlags.None,
            zoneHash: 0x0001,
            timestampMinutes: 1,
            confirmHash: 0);

        var base64 = token.ToBase64();

        Assert.True(base64.Length <= 12, $"Expected <= 12 chars, got {base64.Length}.");
    }

    [Fact]
    public void AlertReturns8Bytes()
    {
        var bytes = Ecp.Alert(EmergencyType.Fire, zoneHash: 0xABCD, priority: EcpPriority.Critical);

        Assert.Equal(UniversalEmergencyToken.Size, bytes.Length);
    }

    [Fact]
    public void TryDecodeValidUetReturnsTrue()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Medical,
            EcpPriority.Medium,
            ActionFlags.ShowMessage,
            zoneHash: 0x0102,
            timestampMinutes: 0x0304,
            confirmHash: 0x1FFFF);

        var bytes = token.ToBytes();

        var ok = Ecp.TryDecode(bytes, out var message);

        Assert.True(ok);
        Assert.True(message.IsUet);
        Assert.Equal(token, message.Token);
    }

    [Fact]
    public void TryDecodeInvalidLengthReturnsFalse()
    {
        var data = new byte[7];

        var ok = Ecp.TryDecode(data, out _);

        Assert.False(ok);
    }

    [Fact]
    public void DecodeNonUetNonEnvelopeThrowsEcpDecodeException()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };

        Assert.Throws<EcpDecodeException>(() => Ecp.Decode(data));
    }

    [Fact]
    public void AllZeroFieldsProduceZeroBytes()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Fire,
            EcpPriority.Low,
            ActionFlags.None,
            zoneHash: 0,
            timestampMinutes: 0,
            confirmHash: 0);

        var bytes = token.ToBytes();

        Assert.All(bytes, b => Assert.Equal(0x00, b));
    }

    [Fact]
    public void AllMaxFieldsProduceAllOnesBytes()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Reserved,
            EcpPriority.Critical,
            (ActionFlags)0xFF,
            zoneHash: 0xFFFF,
            timestampMinutes: 0xFFFF,
            confirmHash: 0x3FFFF);

        var bytes = token.ToBytes();

        Assert.All(bytes, b => Assert.Equal(0xFF, b));
    }

    [Fact]
    public void BigEndianPlacesEmergencyTypeInMostSignificantBits()
    {
        var token = UniversalEmergencyToken.Create(
            EmergencyType.Reserved,
            EcpPriority.Low,
            ActionFlags.None,
            zoneHash: 0,
            timestampMinutes: 0,
            confirmHash: 0);

        var bytes = token.ToBytes();

        Assert.Equal(0xF0, bytes[0]);
        for (var i = 1; i < bytes.Length; i++)
        {
            Assert.Equal(0x00, bytes[i]);
        }
    }

    [Fact]
    public void CreateThrowsWhenConfirmHashOutOfRange()
    {
        var tooLarge = (uint)(1 << 18);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UniversalEmergencyToken.Create(
                EmergencyType.Fire,
                EcpPriority.Low,
                ActionFlags.None,
                zoneHash: 0,
                timestampMinutes: 0,
                confirmHash: tooLarge));
    }

    private static byte[] BuildExpectedBytes(
        byte emergencyType,
        byte priority,
        byte actionFlags,
        ushort zoneHash,
        ushort timestampMinutes,
        uint confirmHash)
    {
        var bits = new bool[64];

        SetBits(bits, startBit: 0, bitCount: 4, value: emergencyType);
        SetBits(bits, startBit: 4, bitCount: 2, value: priority);
        SetBits(bits, startBit: 6, bitCount: 8, value: actionFlags);
        SetBits(bits, startBit: 14, bitCount: 16, value: zoneHash);
        SetBits(bits, startBit: 30, bitCount: 16, value: timestampMinutes);
        SetBits(bits, startBit: 46, bitCount: 18, value: confirmHash);

        var bytes = new byte[8];
        for (var i = 0; i < bytes.Length; i++)
        {
            byte value = 0;
            for (var bit = 0; bit < 8; bit++)
            {
                if (bits[(i * 8) + bit])
                {
                    value |= (byte)(1 << (7 - bit));
                }
            }

            bytes[i] = value;
        }

        return bytes;
    }

    private static void SetBits(bool[] bits, int startBit, int bitCount, ulong value)
    {
        for (var i = 0; i < bitCount; i++)
        {
            var bitIndex = startBit + i;
            var shift = bitCount - 1 - i;
            var bitSet = ((value >> shift) & 1UL) == 1UL;
            bits[bitIndex] = bitSet;
        }
    }
}
