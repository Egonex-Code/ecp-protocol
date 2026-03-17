// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;

namespace ECP.Cascade.GeoQuorum;

/// <summary>
/// Confirmation statistics per geographic zone.
/// </summary>
public readonly record struct ZoneConfirmationStats
{
    private const byte FormatVersion = 1;
    /// <summary>16-bit zone hash.</summary>
    public ushort ZoneHash { get; }
    /// <summary>Number of confirmed recipients.</summary>
    public int ConfirmedCount { get; }
    /// <summary>Expected recipients in the zone.</summary>
    public int ExpectedCount { get; }

    /// <summary>
    /// Creates zone confirmation statistics.
    /// </summary>
    public ZoneConfirmationStats(ushort zoneHash, int confirmedCount, int expectedCount)
    {
        if (confirmedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmedCount), "Confirmed count cannot be negative.");
        }

        if (expectedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), "Expected count cannot be negative.");
        }

        ZoneHash = zoneHash;
        ConfirmedCount = confirmedCount;
        ExpectedCount = expectedCount;
    }

    /// <summary>
    /// Serializes the zone stats to bytes (versioned).
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[1 + 2 + 4 + 4];
        var span = bytes.AsSpan();
        span[0] = FormatVersion;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(1, 2), ZoneHash);
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(3, 4), ConfirmedCount);
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(7, 4), ExpectedCount);
        return bytes;
    }

    /// <summary>
    /// Deserializes zone stats from bytes (versioned).
    /// </summary>
    public static ZoneConfirmationStats FromBytes(ReadOnlySpan<byte> bytes)
    {
        const int totalLength = 1 + 2 + 4 + 4;
        if (bytes.Length != totalLength)
        {
            throw new ArgumentException("Zone confirmation payload has invalid length.", nameof(bytes));
        }

        var version = bytes[0];
        if (version != FormatVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), $"Unsupported zone stats format version {version}.");
        }

        var zoneHash = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2));
        var confirmed = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(3, 4));
        var expected = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(7, 4));
        return new ZoneConfirmationStats(zoneHash, confirmed, expected);
    }
}

/// <summary>
/// Geo-quorum result per zone.
/// </summary>
public readonly record struct GeoQuorumResult
{
    private const byte FormatVersion = 1;

    /// <summary>16-bit zone hash.</summary>
    public ushort ZoneHash { get; }
    /// <summary>Coverage percent (0-100).</summary>
    public double CoveragePercent { get; }
    /// <summary>Number of confirmed recipients.</summary>
    public int ConfirmedCount { get; }
    /// <summary>Expected recipients in the zone.</summary>
    public int ExpectedCount { get; }

    /// <summary>
    /// Creates a geo-quorum result.
    /// </summary>
    public GeoQuorumResult(ushort zoneHash, double coveragePercent, int confirmedCount, int expectedCount)
    {
        ZoneHash = zoneHash;
        CoveragePercent = coveragePercent;
        ConfirmedCount = confirmedCount;
        ExpectedCount = expectedCount;
    }

    /// <summary>
    /// Serializes the geo-quorum result to bytes (versioned).
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[1 + 2 + 8 + 4 + 4];
        var span = bytes.AsSpan();
        span[0] = FormatVersion;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(1, 2), ZoneHash);
        BinaryPrimitives.WriteInt64BigEndian(span.Slice(3, 8), BitConverter.DoubleToInt64Bits(CoveragePercent));
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(11, 4), ConfirmedCount);
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(15, 4), ExpectedCount);
        return bytes;
    }

    /// <summary>
    /// Deserializes a geo-quorum result from bytes (versioned).
    /// </summary>
    public static GeoQuorumResult FromBytes(ReadOnlySpan<byte> bytes)
    {
        const int totalLength = 1 + 2 + 8 + 4 + 4;
        if (bytes.Length != totalLength)
        {
            throw new ArgumentException("Geo-quorum payload has invalid length.", nameof(bytes));
        }

        var version = bytes[0];
        if (version != FormatVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), $"Unsupported geo-quorum format version {version}.");
        }

        var zoneHash = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(1, 2));
        var coverageBits = BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(3, 8));
        var confirmed = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(11, 4));
        var expected = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(15, 4));
        var coverage = BitConverter.Int64BitsToDouble(coverageBits);

        return new GeoQuorumResult(zoneHash, coverage, confirmed, expected);
    }
}
