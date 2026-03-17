// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using ECP.Core.Models;

namespace ECP.Core.Negotiation;

/// <summary>
/// Capability negotiation payload (4 bytes).
/// Layout: MinVersion(1) + MaxVersion(1) + CapabilitiesBitmap(2).
/// </summary>
public readonly record struct CapabilityNegotiationPayload
{
    /// <summary>Payload size in bytes.</summary>
    public const int Size = 4;

    /// <summary>Minimum supported protocol version.</summary>
    public byte MinVersion { get; }
    /// <summary>Maximum supported protocol version.</summary>
    public byte MaxVersion { get; }
    /// <summary>Capability bitmap.</summary>
    public EcpCapabilities Capabilities { get; }

    /// <summary>
    /// Creates a capability negotiation payload.
    /// </summary>
    public CapabilityNegotiationPayload(byte minVersion, byte maxVersion, EcpCapabilities capabilities)
    {
        if (minVersion > maxVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(minVersion), "MinVersion must be <= MaxVersion.");
        }

        MinVersion = minVersion;
        MaxVersion = maxVersion;
        Capabilities = capabilities;
    }

    /// <summary>
    /// Serializes the payload to bytes.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        WriteTo(bytes);
        return bytes;
    }

    /// <summary>
    /// Writes the payload to the destination buffer.
    /// </summary>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException("Destination must be at least 4 bytes.", nameof(destination));
        }

        destination[0] = MinVersion;
        destination[1] = MaxVersion;
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(2, 2), (ushort)Capabilities);
    }

    /// <summary>
    /// Attempts to write the payload to the destination buffer.
    /// </summary>
    public bool TryWriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        WriteTo(destination);
        return true;
    }

    /// <summary>
    /// Deserializes the payload from bytes.
    /// </summary>
    public static CapabilityNegotiationPayload FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException("Capability payload must be exactly 4 bytes.", nameof(bytes));
        }

        var minVersion = bytes[0];
        var maxVersion = bytes[1];
        var capabilities = (EcpCapabilities)BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(2, 2));
        return new CapabilityNegotiationPayload(minVersion, maxVersion, capabilities);
    }

    /// <summary>
    /// Tries to deserialize the payload from bytes.
    /// </summary>
    public static bool TryFromBytes(ReadOnlySpan<byte> bytes, out CapabilityNegotiationPayload payload)
    {
        if (bytes.Length != Size)
        {
            payload = default;
            return false;
        }

        try
        {
            payload = FromBytes(bytes);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            payload = default;
            return false;
        }
    }
}
