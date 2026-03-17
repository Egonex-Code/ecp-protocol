// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using ECP.Core.Models;

namespace ECP.Core.Token;

/// <summary>
/// Universal Emergency Token (UET) encoded as a compact 64-bit value.
/// </summary>
public readonly struct UniversalEmergencyToken : IEquatable<UniversalEmergencyToken>
{
    /// <summary>
    /// Size of the UET in bytes.
    /// </summary>
    public const int Size = 8;

    private const int EmergencyTypeBits = 4;
    private const int PriorityBits = 2;
    private const int ActionFlagsBits = 8;
    private const int ZoneHashBits = 16;
    private const int TimestampBits = 16;
    private const int ConfirmHashBits = 18;

    private const int ConfirmHashShift = 0;
    private const int TimestampShift = ConfirmHashShift + ConfirmHashBits;
    private const int ZoneHashShift = TimestampShift + TimestampBits;
    private const int ActionFlagsShift = ZoneHashShift + ZoneHashBits;
    private const int PriorityShift = ActionFlagsShift + ActionFlagsBits;
    private const int EmergencyTypeShift = PriorityShift + PriorityBits;

    private const ulong EmergencyTypeMask = (1UL << EmergencyTypeBits) - 1;
    private const ulong PriorityMask = (1UL << PriorityBits) - 1;
    private const ulong ActionFlagsMask = (1UL << ActionFlagsBits) - 1;
    private const ulong ZoneHashMask = (1UL << ZoneHashBits) - 1;
    private const ulong TimestampMask = (1UL << TimestampBits) - 1;
    private const ulong ConfirmHashMask = (1UL << ConfirmHashBits) - 1;

    private readonly ulong _value;

    private UniversalEmergencyToken(ulong value)
    {
        _value = value;
    }

    /// <summary>Emergency type encoded in the token.</summary>
    public EmergencyType EmergencyType => (EmergencyType)((_value >> EmergencyTypeShift) & EmergencyTypeMask);
    /// <summary>Priority encoded in the token.</summary>
    public EcpPriority Priority => (EcpPriority)((_value >> PriorityShift) & PriorityMask);
    /// <summary>Action flags encoded in the token.</summary>
    public ActionFlags ActionFlags => (ActionFlags)((_value >> ActionFlagsShift) & ActionFlagsMask);
    /// <summary>16-bit geohash zone identifier.</summary>
    public ushort ZoneHash => (ushort)((_value >> ZoneHashShift) & ZoneHashMask);
    /// <summary>Timestamp in minutes (16-bit).</summary>
    public ushort TimestampMinutes => (ushort)((_value >> TimestampShift) & TimestampMask);
    /// <summary>18-bit confirmation hash for fast correlation.</summary>
    public uint ConfirmHash => (uint)((_value >> ConfirmHashShift) & ConfirmHashMask);
    /// <summary>Raw packed 64-bit value.</summary>
    public ulong RawValue => _value;

    /// <summary>
    /// Creates a UET from the specified fields.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any field exceeds its bit-width.
    /// </exception>
    public static UniversalEmergencyToken Create(
        EmergencyType emergencyType,
        EcpPriority priority,
        ActionFlags actionFlags,
        ushort zoneHash,
        ushort timestampMinutes,
        uint confirmHash)
    {
        Validate(emergencyType, priority, actionFlags, confirmHash);

        ulong value = ((ulong)emergencyType & EmergencyTypeMask) << EmergencyTypeShift;
        value |= ((ulong)priority & PriorityMask) << PriorityShift;
        value |= ((ulong)actionFlags & ActionFlagsMask) << ActionFlagsShift;
        value |= ((ulong)zoneHash & ZoneHashMask) << ZoneHashShift;
        value |= ((ulong)timestampMinutes & TimestampMask) << TimestampShift;
        value |= ((ulong)confirmHash & ConfirmHashMask) << ConfirmHashShift;

        return new UniversalEmergencyToken(value);
    }

    /// <summary>
    /// Reads a UET from an 8-byte big-endian buffer.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the span is not 8 bytes.</exception>
    public static UniversalEmergencyToken FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException("UET must be exactly 8 bytes.", nameof(bytes));
        }

        return new UniversalEmergencyToken(BinaryPrimitives.ReadUInt64BigEndian(bytes));
    }

    /// <summary>
    /// Tries to read a UET from an 8-byte big-endian buffer.
    /// </summary>
    public static bool TryFromBytes(ReadOnlySpan<byte> bytes, out UniversalEmergencyToken token)
    {
        if (bytes.Length != Size)
        {
            token = default;
            return false;
        }

        token = new UniversalEmergencyToken(BinaryPrimitives.ReadUInt64BigEndian(bytes));
        return true;
    }

    /// <summary>
    /// Serializes the token into an 8-byte big-endian array.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        WriteTo(bytes);
        return bytes;
    }

    /// <summary>
    /// Serializes the token into a Base64 string (typically 12 chars).
    /// </summary>
    public string ToBase64()
    {
        Span<byte> buffer = stackalloc byte[Size];
        WriteTo(buffer);
        return Convert.ToBase64String(buffer);
    }

    /// <summary>
    /// Writes the token to the provided buffer in big-endian order.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the span is smaller than 8 bytes.</exception>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException("Destination must be at least 8 bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, _value);
    }

    /// <summary>
    /// Tries to write the token to the provided buffer in big-endian order.
    /// </summary>
    public bool TryWriteTo(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, _value);
        return true;
    }

    /// <summary>
    /// Checks equality with another UET.
    /// </summary>
    public bool Equals(UniversalEmergencyToken other) => _value == other._value;
    /// <summary>
    /// Checks equality with another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is UniversalEmergencyToken other && Equals(other);
    /// <summary>
    /// Returns the hash code of the packed value.
    /// </summary>
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc cref="IEquatable{T}"/>
    public static bool operator ==(UniversalEmergencyToken left, UniversalEmergencyToken right) => left.Equals(right);

    /// <inheritdoc cref="IEquatable{T}"/>
    public static bool operator !=(UniversalEmergencyToken left, UniversalEmergencyToken right) => !left.Equals(right);

    private static void Validate(
        EmergencyType emergencyType,
        EcpPriority priority,
        ActionFlags actionFlags,
        uint confirmHash)
    {
        if ((uint)emergencyType > EmergencyTypeMask)
        {
            throw new ArgumentOutOfRangeException(nameof(emergencyType), "EmergencyType must be within 0-15.");
        }

        if ((uint)priority > PriorityMask)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be within 0-3.");
        }

        if (((uint)actionFlags & ~ActionFlagsMask) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actionFlags), "ActionFlags must fit in 8 bits.");
        }

        if (confirmHash > ConfirmHashMask)
        {
            throw new ArgumentOutOfRangeException(nameof(confirmHash), "ConfirmHash must be within 18 bits.");
        }
    }
}
