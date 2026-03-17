// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using ECP.Core.Models;

namespace ECP.Core.Envelope;

/// <summary>
/// Builder for creating emergency envelopes.
/// </summary>
public sealed class EnvelopeBuilder
{
    private const byte DefaultTtlSeconds = 120;

    private EcpFlags _flags;
    private EcpPriority _priority = EcpPriority.Critical;
    private byte _ttl = DefaultTtlSeconds;
    private byte _maxTtlSeconds = byte.MaxValue;
    private byte _keyVersion;
    private ulong? _messageId;
    private uint? _timestamp;
    private EcpPayloadType _payloadType = EcpPayloadType.Alert;
    private EmergencyType? _emergencyType;
    private byte[]? _payload;
    private byte[]? _hmacKey;
    private int _hmacLength = EmergencyEnvelope.DefaultHmacLength;
    private int _maxPayloadBytes = ushort.MaxValue;

    /// <summary>
    /// Sets the emergency type and forces payload type to Alert.
    /// If no payload is provided, a single-byte payload containing the type is generated.
    /// </summary>
    public EnvelopeBuilder WithType(EmergencyType emergencyType)
    {
        _emergencyType = emergencyType;
        _payloadType = EcpPayloadType.Alert;
        return this;
    }

    /// <summary>
    /// Sets the payload type.
    /// </summary>
    public EnvelopeBuilder WithPayloadType(EcpPayloadType payloadType)
    {
        _payloadType = payloadType;
        return this;
    }

    /// <summary>
    /// Sets the envelope priority.
    /// </summary>
    public EnvelopeBuilder WithPriority(EcpPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Priority must be a valid EcpPriority value.");
        }

        _priority = priority;
        return this;
    }

    /// <summary>
    /// Sets envelope flags.
    /// </summary>
    public EnvelopeBuilder WithFlags(EcpFlags flags)
    {
        _flags = flags;
        return this;
    }

    /// <summary>
    /// Sets the time-to-live in seconds.
    /// </summary>
    public EnvelopeBuilder WithTtl(byte ttlSeconds)
    {
        if (ttlSeconds > _maxTtlSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(ttlSeconds), $"TTL must be between 0 and {_maxTtlSeconds} seconds.");
        }

        _ttl = ttlSeconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum allowed TTL in seconds.
    /// </summary>
    public EnvelopeBuilder WithMaxTtl(byte maxTtlSeconds)
    {
        _maxTtlSeconds = maxTtlSeconds;
        if (_ttl > _maxTtlSeconds)
        {
            _ttl = _maxTtlSeconds;
        }

        return this;
    }

    /// <summary>
    /// Sets the key version used for HMAC rotation.
    /// </summary>
    public EnvelopeBuilder WithKeyVersion(byte keyVersion)
    {
        _keyVersion = keyVersion;
        return this;
    }

    /// <summary>
    /// Sets a fixed message identifier.
    /// </summary>
    public EnvelopeBuilder WithMessageId(ulong messageId)
    {
        _messageId = messageId;
        return this;
    }

    /// <summary>
    /// Sets a fixed Unix timestamp in seconds.
    /// </summary>
    public EnvelopeBuilder WithTimestamp(uint timestampSeconds)
    {
        _timestamp = timestampSeconds;
        return this;
    }

    /// <summary>
    /// Sets the payload bytes.
    /// </summary>
    public EnvelopeBuilder WithPayload(byte[] payload)
    {
        _payload = payload;
        return this;
    }

    /// <summary>
    /// Sets the payload bytes from a span.
    /// </summary>
    public EnvelopeBuilder WithPayload(ReadOnlySpan<byte> payload)
    {
        _payload = payload.ToArray();
        return this;
    }

    /// <summary>
    /// Sets the payload from a UTF-8 string.
    /// </summary>
    public EnvelopeBuilder WithPayload(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payload = Encoding.UTF8.GetBytes(payload);
        return this;
    }

    /// <summary>
    /// Sets the HMAC key for signing.
    /// </summary>
    public EnvelopeBuilder WithHmacKey(ReadOnlySpan<byte> hmacKey)
    {
        _hmacKey = hmacKey.ToArray();
        return this;
    }

    /// <summary>
    /// Sets the HMAC length in bytes (0 or 8-16).
    /// </summary>
    public EnvelopeBuilder WithHmacLength(int hmacLength)
    {
        if (hmacLength != 0 && (hmacLength < 8 || hmacLength > 16))
        {
            throw new ArgumentOutOfRangeException(nameof(hmacLength), "HMAC length must be 0 or between 8 and 16 bytes.");
        }

        _hmacLength = hmacLength;
        return this;
    }

    /// <summary>
    /// Sets the maximum payload length in bytes.
    /// </summary>
    public EnvelopeBuilder WithMaxPayloadBytes(int maxPayloadBytes)
    {
        if (maxPayloadBytes < 0 || maxPayloadBytes > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPayloadBytes), "Max payload bytes must be between 0 and 65,535.");
        }

        _maxPayloadBytes = maxPayloadBytes;
        return this;
    }

    /// <summary>
    /// Builds the envelope and computes the HMAC when enabled.
    /// </summary>
    public EmergencyEnvelope Build()
    {
        var payload = ResolvePayload();
        if (payload.Length > _maxPayloadBytes)
        {
            throw new InvalidOperationException($"Payload exceeds allowed limit of {_maxPayloadBytes} bytes.");
        }

        var messageId = _messageId ?? EmergencyEnvelope.GenerateMessageId();
        var timestamp = _timestamp ?? (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (_hmacLength == 0)
        {
            return new EmergencyEnvelope(
                _flags,
                _priority,
                _ttl,
                _keyVersion,
                messageId,
                timestamp,
                _payloadType,
                payload,
                ReadOnlyMemory<byte>.Empty,
                isValid: true);
        }

        if (_hmacKey is null || _hmacKey.Length == 0)
        {
            throw new InvalidOperationException("HMAC key is required. Call WithHmacKey(hmacKey) before Build().");
        }

        var headerAndPayload = new byte[EmergencyEnvelope.HeaderSize + payload.Length];
        var envelope = new EmergencyEnvelope(
            _flags,
            _priority,
            _ttl,
            _keyVersion,
            messageId,
            timestamp,
            _payloadType,
            payload,
            ReadOnlyMemory<byte>.Empty,
            isValid: true);

        envelope.WriteHeader(headerAndPayload);
        payload.CopyTo(headerAndPayload.AsSpan(EmergencyEnvelope.HeaderSize));

        var hmac = EmergencyEnvelope.ComputeHmac(_hmacKey, headerAndPayload, _hmacLength);

        return new EmergencyEnvelope(
            _flags,
            _priority,
            _ttl,
            _keyVersion,
            messageId,
            timestamp,
            _payloadType,
            payload,
            hmac,
            isValid: true);
    }

    private byte[] ResolvePayload()
    {
        if (_payload is not null)
        {
            return _payload;
        }

        if (_emergencyType.HasValue)
        {
            return new[] { (byte)_emergencyType.Value };
        }

        return Array.Empty<byte>();
    }
}
