// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Buffers.Binary;
using ECP.Core.Envelope;

namespace ECP.Transport.Abstractions;

/// <summary>
/// Incremental decoder for fragmented envelope streams.
/// </summary>
public sealed class EcpStreamDecoder
{
    private readonly int _hmacLength;
    private readonly byte[] _hmacKey;
    private byte[] _buffer;
    private int _count;
    private int _expectedLength = -1;

    /// <summary>
    /// Creates a decoder without HMAC verification.
    /// </summary>
    public EcpStreamDecoder(int hmacLength = EmergencyEnvelope.DefaultHmacLength)
        : this(ReadOnlySpan<byte>.Empty, hmacLength)
    {
    }

    /// <summary>
    /// Creates a decoder with optional HMAC verification.
    /// </summary>
    public EcpStreamDecoder(ReadOnlySpan<byte> hmacKey, int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        if (hmacLength != 0 && (hmacLength < 8 || hmacLength > 16))
        {
            throw new ArgumentOutOfRangeException(nameof(hmacLength), "HMAC length must be 0 or between 8 and 16 bytes.");
        }

        _hmacLength = hmacLength;
        _hmacKey = hmacKey.IsEmpty ? Array.Empty<byte>() : hmacKey.ToArray();
        _buffer = new byte[EmergencyEnvelope.HeaderSize];
    }

    /// <summary>
    /// Clears any buffered data and resets the decoder state.
    /// </summary>
    public void Reset()
    {
        _count = 0;
        _expectedLength = -1;
    }

    /// <summary>
    /// Feeds data into the decoder and returns true when a full envelope is decoded.
    /// </summary>
    public bool TryFeed(ReadOnlySpan<byte> data, out EmergencyEnvelope envelope)
    {
        envelope = default;

        if (!data.IsEmpty)
        {
            EnsureCapacity(_count + data.Length);
            data.CopyTo(_buffer.AsSpan(_count));
            _count += data.Length;
        }

        if (_expectedLength < 0 && _count >= EmergencyEnvelope.HeaderSize)
        {
            if (!TryParseHeader(out var payloadLength))
            {
                Reset();
                return false;
            }

            _expectedLength = EmergencyEnvelope.HeaderSize + payloadLength + _hmacLength;
        }

        if (_expectedLength > 0 && _count >= _expectedLength)
        {
            var message = _buffer.AsSpan(0, _expectedLength);
            if (!EmergencyEnvelope.TryDecode(message, _hmacKey, out envelope, _hmacLength))
            {
                Reset();
                return false;
            }

            var remaining = _count - _expectedLength;
            if (remaining > 0)
            {
                _buffer.AsSpan(_expectedLength, remaining).CopyTo(_buffer);
            }

            _count = remaining;
            _expectedLength = -1;
            return true;
        }

        return false;
    }

    private bool TryParseHeader(out ushort payloadLength)
    {
        payloadLength = 0;

        var magic = BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(0, 2));
        if (magic != EmergencyEnvelope.Magic)
        {
            return false;
        }

        if (_buffer[2] != EmergencyEnvelope.Version)
        {
            return false;
        }

        payloadLength = BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(20, 2));
        return true;
    }

    private void EnsureCapacity(int required)
    {
        if (_buffer.Length >= required)
        {
            return;
        }

        var newSize = _buffer.Length;
        while (newSize < required)
        {
            newSize *= 2;
        }

        Array.Resize(ref _buffer, newSize);
    }
}
