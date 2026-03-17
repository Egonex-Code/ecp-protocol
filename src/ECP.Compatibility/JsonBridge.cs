// Copyright (c) 2026 Egonex S.R.L.
// SPDX-License-Identifier: Apache-2.0
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for full license information.
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECP.Core.Envelope;
using ECP.Core.Models;

namespace ECP.Compatibility;

/// <summary>
/// JSON ↔ ECP compatibility bridge for legacy migrations.
/// </summary>
public static class JsonBridge
{
    private const byte KnownFlagsMask = 0b0011_1111;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Encodes a JSON message into an ECP envelope.
    /// </summary>
    public static byte[] ToEcp(
        string json,
        ReadOnlySpan<byte> hmacKey,
        int hmacLength = EmergencyEnvelope.DefaultHmacLength,
        byte keyVersion = 0,
        EcpPriority priority = EcpPriority.Critical,
        byte ttlSeconds = 120,
        EcpFlags flags = EcpFlags.None,
        EcpPayloadType payloadType = EcpPayloadType.Alert)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON input is required.", nameof(json));
        }

        if (hmacLength != 0 && hmacKey.IsEmpty)
        {
            throw new ArgumentException("HMAC key is required.", nameof(hmacKey));
        }

        var payload = ResolvePayload(json, out var envelopeJson);
        if (payload.Length > ushort.MaxValue)
        {
            throw new ArgumentException("Payload exceeds 65,535 bytes.", nameof(json));
        }

        var builder = new EnvelopeBuilder()
            .WithFlags(envelopeJson?.Flags ?? flags)
            .WithPriority(envelopeJson?.Priority ?? priority)
            .WithTtl(envelopeJson?.Ttl ?? ttlSeconds)
            .WithKeyVersion(envelopeJson?.KeyVersion ?? keyVersion)
            .WithPayloadType(envelopeJson?.PayloadType ?? payloadType)
            .WithPayload(payload)
            .WithHmacLength(hmacLength);

        if (hmacLength != 0)
        {
            builder.WithHmacKey(hmacKey);
        }

        if (envelopeJson?.MessageId is not null)
        {
            builder.WithMessageId(envelopeJson.MessageId.Value);
        }

        if (envelopeJson?.Timestamp is not null)
        {
            builder.WithTimestamp(envelopeJson.Timestamp.Value);
        }

        return builder.Build().ToBytes();
    }

    /// <summary>
    /// Decodes ECP bytes into JSON (legacy payload when available).
    /// </summary>
    public static string ToJson(ReadOnlySpan<byte> ecpBytes, ReadOnlySpan<byte> hmacKey, int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        return ToJson(ecpBytes, hmacKey, hmacLength, includeMetadata: false);
    }

    /// <summary>
    /// Decodes ECP bytes into JSON with optional envelope metadata.
    /// </summary>
    public static string ToJson(ReadOnlySpan<byte> ecpBytes, ReadOnlySpan<byte> hmacKey, int hmacLength, bool includeMetadata)
    {
        var envelope = EmergencyEnvelope.Decode(ecpBytes, hmacKey, hmacLength);
        return SerializeEnvelope(envelope, includeMetadata);
    }

    /// <summary>
    /// Attempts to decode either ECP bytes or JSON bytes from the same stream.
    /// </summary>
    public static bool TryDecodeCompat(
        ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> hmacKey,
        out CompatibilityDecodedMessage message,
        int hmacLength = EmergencyEnvelope.DefaultHmacLength)
    {
        if (LooksLikeEnvelope(data))
        {
            if (!EmergencyEnvelope.TryDecode(data, hmacKey, out var envelope, hmacLength))
            {
                message = default;
                return false;
            }

            var json = SerializeEnvelope(envelope, includeMetadata: false);
            message = CompatibilityDecodedMessage.FromEnvelope(envelope, json);
            return true;
        }

        if (TryGetJsonString(data, out var jsonText))
        {
            message = CompatibilityDecodedMessage.FromJson(jsonText);
            return true;
        }

        message = default;
        return false;
    }

    private static byte[] ResolvePayload(string json, out EcpJsonEnvelope? envelopeJson)
    {
        envelopeJson = TryParseEnvelopeJson(json);
        if (envelopeJson is null)
        {
            return Encoding.UTF8.GetBytes(json);
        }

        ValidateEnvelopeJson(envelopeJson);

        if (!string.IsNullOrWhiteSpace(envelopeJson.PayloadBase64))
        {
            try
            {
                return Convert.FromBase64String(envelopeJson.PayloadBase64);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("PayloadBase64 must be valid base64.", nameof(json), ex);
            }
        }

        if (envelopeJson.PayloadText is not null)
        {
            return Encoding.UTF8.GetBytes(envelopeJson.PayloadText);
        }

        return Array.Empty<byte>();
    }

    private static void ValidateEnvelopeJson(EcpJsonEnvelope envelopeJson)
    {
        if (!string.IsNullOrWhiteSpace(envelopeJson.PayloadBase64) &&
            envelopeJson.PayloadText is not null)
        {
            throw new ArgumentException("Provide either payloadBase64 or payloadText, not both.");
        }

        if (envelopeJson.Priority is not null &&
            !Enum.IsDefined(envelopeJson.Priority.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(envelopeJson), "Priority must be a valid EcpPriority value.");
        }

        if (envelopeJson.PayloadType is not null &&
            !Enum.IsDefined(envelopeJson.PayloadType.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(envelopeJson), "PayloadType must be a valid EcpPayloadType value.");
        }

        if (envelopeJson.Flags is not null)
        {
            var flags = (byte)envelopeJson.Flags.Value;
            if ((flags & ~KnownFlagsMask) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(envelopeJson), "Flags contains unknown bits.");
            }
        }
    }

    private static EcpJsonEnvelope? TryParseEnvelopeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var root = doc.RootElement;
            if (!HasProperty(root, "payloadBase64") && !HasProperty(root, "payloadText"))
            {
                return null;
            }

            return JsonSerializer.Deserialize<EcpJsonEnvelope>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetJsonString(ReadOnlySpan<byte> data, out string json)
    {
        json = string.Empty;
        if (data.IsEmpty)
        {
            return false;
        }

        try
        {
            json = Encoding.UTF8.GetString(data);
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            json = string.Empty;
            return false;
        }
    }

    private static string SerializeEnvelope(EmergencyEnvelope envelope, bool includeMetadata)
    {
        var payloadIsJson = TryGetJsonString(envelope.Payload.Span, out var payloadJson);

        if (!includeMetadata && payloadIsJson)
        {
            return payloadJson;
        }

        var dto = new EcpJsonEnvelope
        {
            Flags = envelope.Flags,
            Priority = envelope.Priority,
            Ttl = envelope.Ttl,
            KeyVersion = envelope.KeyVersion,
            MessageId = envelope.MessageId,
            Timestamp = envelope.Timestamp,
            PayloadType = envelope.PayloadType
        };

        if (payloadIsJson)
        {
            dto.PayloadText = payloadJson;
        }
        else
        {
            dto.PayloadBase64 = Convert.ToBase64String(envelope.Payload.Span);
        }

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private static bool LooksLikeEnvelope(ReadOnlySpan<byte> data)
    {
        return data.Length >= 2 && data[0] == 0xEC && data[1] == 0x50;
    }

    private sealed class EcpJsonEnvelope
    {
        public EcpFlags? Flags { get; set; }
        public EcpPriority? Priority { get; set; }
        public byte? Ttl { get; set; }
        public byte? KeyVersion { get; set; }
        public ulong? MessageId { get; set; }
        public uint? Timestamp { get; set; }
        public EcpPayloadType? PayloadType { get; set; }
        public string? PayloadBase64 { get; set; }
        public string? PayloadText { get; set; }
    }
}

/// <summary>
/// Result for compatibility decoding (ECP or JSON).
/// </summary>
public readonly record struct CompatibilityDecodedMessage(
    CompatibilityMessageKind Kind,
    EmergencyEnvelope Envelope,
    string Json)
{
    /// <summary>True when the decoded message is an ECP envelope.</summary>
    public bool IsEcp => Kind == CompatibilityMessageKind.EcpEnvelope;

    /// <summary>
    /// Creates a decoded message from an envelope.
    /// </summary>
    public static CompatibilityDecodedMessage FromEnvelope(EmergencyEnvelope envelope, string json) =>
        new(CompatibilityMessageKind.EcpEnvelope, envelope, json);

    /// <summary>
    /// Creates a decoded message from JSON.
    /// </summary>
    public static CompatibilityDecodedMessage FromJson(string json) =>
        new(CompatibilityMessageKind.Json, default, json);
}

/// <summary>
/// Compatibility decode result types.
/// </summary>
public enum CompatibilityMessageKind : byte
{
    /// <summary>ECP envelope decoded.</summary>
    EcpEnvelope = 0,
    /// <summary>JSON message decoded.</summary>
    Json = 1
}
