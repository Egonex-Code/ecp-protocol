# ECP Wire Format Specification

**Version:** 1.0  
**Protocol version:** 0x01  
**Status:** Normative reference for ECP SDK 2.0.x  
**Patent notice:** Patent pending (UIBM). See [LICENSE.txt](../../LICENSE.txt) for terms.  
**Keywords:** The key words "MUST", "MUST NOT", "SHOULD", "SHOULD NOT", and "MAY" in this document are to be interpreted as described in [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).

---

## Table of contents

1. [Overview](#overview)
2. [Byte order](#byte-order)
3. [Universal Emergency Token (UET)](#universal-emergency-token-uet)
4. [Emergency Envelope](#emergency-envelope)
5. [Payload types](#payload-types)
6. [Integrity protection](#integrity-protection)
7. [Interoperability notes](#interoperability-notes)
8. [Size summary](#size-summary)
9. [Test vectors](#test-vectors)
10. [Changelog](#changelog)

---

## Overview

The ECP wire format defines two binary structures used to encode and transport emergency messages:

- **Universal Emergency Token (UET)** — A fixed 8-byte compact representation of a single emergency event. Designed for constrained channels (BLE, LoRa, SMS, narrowband IoT).
- **Emergency Envelope** — A variable-length message container with a fixed 22-byte header, a variable payload, and an optional integrity tag. Designed for reliable transport over WebSocket, SignalR, or any byte-stream channel.

Both structures use a fixed binary layout. There is no schema negotiation, no text encoding, and no field delimiters. Every field has a known offset and size.

---

## Byte order

All multi-byte integer fields are encoded in **big-endian** (network byte order).

This applies to:
- The 64-bit packed value inside the UET.
- All `uint16`, `uint32`, and `uint64` fields in the Envelope header.

---

## Universal Emergency Token (UET)

### Structure

The UET is a single 64-bit unsigned integer (8 bytes), with fields packed from the most significant bit downward.

```
Bit 63                                                              Bit 0
┌──────────┬──────────┬────────────┬───────────┬───────────┬──────────────┐
│ EmgType  │ Priority │ ActionFlags│ ZoneHash  │ Timestamp │ ConfirmHash  │
│  4 bits  │  2 bits  │   8 bits   │  16 bits  │  16 bits  │   18 bits    │
└──────────┴──────────┴────────────┴───────────┴───────────┴──────────────┘
Total: 64 bits = 8 bytes
```

### Field definitions

| Field | Bits | Range | Description |
|-------|------|-------|-------------|
| EmergencyType | 4 | 0–15 | Type of emergency event. See [Emergency types](#emergency-types). |
| Priority | 2 | 0–3 | Message priority level. See [Priority levels](#priority-levels). |
| ActionFlags | 8 | 0x00–0xFF | Bitmask of device actions to trigger. See [Action flags](#action-flags). |
| ZoneHash | 16 | 0–65535 | Geographic zone identifier (truncated geohash). |
| TimestampMinutes | 16 | 0–65535 | Event timestamp in minutes (see [Timestamp semantics](#timestamp-semantics)). |
| ConfirmHash | 18 | 0–262143 | Truncated hash for fast message correlation. |

### Encoding

To encode a UET, pack all fields into a single `uint64` using bitwise OR and shift operations, then write the result as 8 bytes in big-endian order.

```
value  = (EmergencyType  & 0x0F) << 60
value |= (Priority       & 0x03) << 58
value |= (ActionFlags    & 0xFF) << 50
value |= (ZoneHash       & 0xFFFF) << 34
value |= (Timestamp      & 0xFFFF) << 18
value |= (ConfirmHash    & 0x3FFFF) << 0
```

### Decoding

To decode a UET, read 8 bytes as a big-endian `uint64`, then extract each field using right-shift and mask:

```
EmergencyType = (value >> 60) & 0x0F
Priority      = (value >> 58) & 0x03
ActionFlags   = (value >> 50) & 0xFF
ZoneHash      = (value >> 34) & 0xFFFF
Timestamp     = (value >> 18) & 0xFFFF
ConfirmHash   = (value >>  0) & 0x3FFFF
```

### Timestamp semantics

The `TimestampMinutes` field stores a 16-bit unsigned value representing minutes since the Unix epoch (1970-01-01T00:00:00 UTC), truncated to 16 bits:

```
TimestampMinutes = (uint16)(UnixTimestampSeconds / 60)
```

With 16 bits, the value naturally wraps around approximately every **45.5 days** (65,536 minutes). Receivers should use modular arithmetic when comparing timestamps, accounting for wrap-around.

This compact representation is intentional: the UET is designed for real-time alerting where messages are relevant within a short time window, not for long-term archival.

### Emergency types

| Value | Name | Description |
|-------|------|-------------|
| 0 | Fire | Fire emergency |
| 1 | Evacuation | Evacuation order |
| 2 | Earthquake | Seismic event |
| 3 | Flood | Flood warning |
| 4 | Medical | Medical emergency |
| 5 | Security | Security incident |
| 6 | Chemical | Chemical/hazardous materials incident |
| 7 | Lockdown | Lockdown order |
| 8 | AllClear | End of emergency / all clear |
| 9 | Test | Test message |
| 10–14 | Custom1–Custom5 | Application-defined emergency types |
| 15 | Reserved | Reserved for future use |

### Priority levels

| Value | Name |
|-------|------|
| 0 | Low |
| 1 | Medium |
| 2 | High |
| 3 | Critical |

### Action flags

Each bit enables a specific device action. Multiple flags can be combined.

| Bit | Flag | Description |
|-----|------|-------------|
| 0 | SoundAlarm | Trigger alarm sound |
| 1 | FlashLights | Flash lights |
| 2 | Vibrate | Vibrate device |
| 3 | PlayVoice | Play voice instructions |
| 4 | ShowMessage | Show message on screen |
| 5 | LockDoors | Lock doors |
| 6 | UnlockDoors | Unlock doors |
| 7 | NotifyExternal | Notify external systems |

---

## Emergency Envelope

### Structure

The Envelope consists of three contiguous sections:

```
┌─────────────────────┬─────────────────────┬──────────────────────┐
│    Fixed Header      │    Payload           │    HMAC Tag          │
│    22 bytes          │    0–65535 bytes      │    0 or 8–16 bytes   │
└─────────────────────┴─────────────────────┴──────────────────────┘
Total length: 22 + PayloadLength + HmacLength
```

### Header layout

All offsets are in bytes from the start of the envelope.

| Offset | Size | Field | Type | Description |
|--------|------|-------|------|-------------|
| 0 | 2 | Magic | uint16 | Protocol identifier. Must be `0xEC50`. |
| 2 | 1 | Version | uint8 | Protocol version. Current: `0x01`. |
| 3 | 1 | Flags | uint8 | Bitfield. See [Envelope flags](#envelope-flags). |
| 4 | 1 | Priority | uint8 | Priority level (0–3). Same values as UET. |
| 5 | 1 | TTL | uint8 | Time-to-live in hops or seconds (context-dependent). |
| 6 | 1 | KeyVersion | uint8 | HMAC key version for key rotation support. |
| 7 | 8 | MessageId | uint64 | Cryptographically random message identifier. |
| 15 | 4 | Timestamp | uint32 | Unix timestamp in seconds (UTC). |
| 19 | 1 | PayloadType | uint8 | Payload content type. See [Payload types](#payload-types). |
| 20 | 2 | PayloadLength | uint16 | Length of the payload section in bytes. |

### Envelope flags

| Bit | Flag | Description |
|-----|------|-------------|
| 0 | NeedsConfirmation | Delivery confirmation is required from recipients. |
| 1 | Broadcast | Message is intended for multiple recipients. |
| 2 | Encrypted | Payload is encrypted (AES-GCM). |
| 3 | Compressed | Payload is compressed. |
| 4 | Cascade | Enable cascade propagation across nodes. |
| 5 | RetryAllowed | Sender allows retransmission on delivery failure. |
| 6–7 | — | Reserved. Must be zero. |

### Validation rules

A decoder **MUST** verify:

1. Bytes 0–1 equal `0xEC50` (magic number).
2. Byte 2 equals `0x01` (supported version).
3. Total length equals `22 + PayloadLength + HmacLength`.
4. If an HMAC key is available and HmacLength > 0, the HMAC tag must be verified before processing the payload.

A decoder **SHOULD** reject envelopes where:

- `PayloadLength` exceeds the remaining buffer after header and HMAC.
- `PayloadType` is not a recognized value.
- `Timestamp` is outside a reasonable time window (anti-replay).

---

## Payload types

| Value | Name | Description |
|-------|------|-------------|
| 0 | Alert | Emergency alert payload. May contain a UET or structured alert data. |
| 1 | Confirmation | Delivery confirmation from a recipient. |
| 2 | Ping | Health check / heartbeat. |
| 3 | Cascade | Cascade control message for multi-hop propagation. |
| 4 | CapabilityNegotiation | Endpoint capability and version negotiation. |
| 5 | KeyRotation | Key rotation notification. |

Payload content structure is type-specific and defined by the application layer. The Envelope header does not impose any structure on the payload bytes.

---

## Integrity protection

### Mechanism

The Envelope supports optional integrity protection using **HMAC-SHA256** with truncation.

- **Algorithm:** HMAC-SHA256 computed over the 22-byte header plus the payload bytes.
- **Truncation:** The full 32-byte HMAC output is truncated to a configurable length of **8–16 bytes**. The default is 12 bytes. Any integer value in the range 8–16 is valid.
- **Unsigned mode:** When HMAC length is 0, no integrity tag is appended. The Envelope total length is `22 + PayloadLength`.
- **Verification:** Receivers must use a timing-safe comparison to verify the truncated HMAC.

### HMAC coverage

```
HMAC input = Header (22 bytes) || Payload (PayloadLength bytes)
HMAC output = first HmacLength bytes of HMAC-SHA256(key, input)
```

### Key rotation

The `KeyVersion` field (offset 6) identifies which HMAC key was used. This enables zero-downtime key rotation: both sender and receiver can hold multiple active keys and select the correct one based on this field.

### HMAC length constraints

| HmacLength | Meaning |
|------------|---------|
| 0 | Unsigned envelope. No HMAC tag appended. |
| 8–11 | Signed. Minimum 8 bytes for meaningful collision resistance. |
| 12 | **Default.** Recommended for most use cases. |
| 13–16 | Signed. Maximum 16 bytes for higher collision resistance. |

Valid values: **0** (unsigned) or any integer **8–16** inclusive. Values 1–7 and values > 16 are invalid and **MUST** be rejected.

### Deriving HmacLength

The Envelope header does not contain an explicit HmacLength field. The HMAC length is agreed upon between sender and receiver through configuration or capability negotiation.

A decoder that knows the expected HmacLength can validate the total envelope length:

```
ExpectedTotal = 22 + PayloadLength + HmacLength
```

If the total buffer length does not match `ExpectedTotal`, the envelope **MUST** be rejected.

A decoder that does **not** know the HmacLength in advance can derive it from the received buffer:

```
HmacLength = TotalBufferLength - 22 - PayloadLength
```

The derived value **MUST** be validated: it must be 0 or in the range 8–16. Any other derived value indicates a corrupt or malformed envelope.

---

## Interoperability notes

### Implementing a UET decoder

A conformant UET decoder MUST:

1. Read exactly 8 bytes from the input buffer.
2. Interpret those bytes as a big-endian `uint64`.
3. Extract each field using the bit shifts and masks defined in [Encoding](#encoding) / [Decoding](#decoding).
4. Validate that `EmergencyType` is in range 0–15 and `Priority` is in range 0–3.

A conformant UET encoder MUST produce output that a conformant decoder can round-trip without data loss.

### Implementing an Envelope decoder

A conformant Envelope decoder MUST:

1. Verify that the buffer is at least 22 bytes.
2. Check bytes 0–1 for magic `0xEC50`.
3. Check byte 2 for version `0x01`.
4. Read `PayloadLength` from bytes 20–21.
5. Derive `HmacLength` as `TotalBufferLength - 22 - PayloadLength`.
6. Validate that `HmacLength` is 0 or in range 8–16.
7. If `HmacLength > 0` and an HMAC key is available, verify the HMAC tag using a timing-safe comparison.
8. Only process the payload after all validation passes.

### Agreement on HmacLength

Sender and receiver MUST agree on the expected HMAC length. This agreement can happen through:

- **Static configuration:** both sides use the same fixed HmacLength (e.g., the default 12).
- **Capability negotiation:** endpoints exchange supported parameters via `CapabilityNegotiation` payloads (PayloadType 4).
- **Derivation:** the receiver derives HmacLength from the buffer as described in [Deriving HmacLength](#deriving-hmaclength).

If the derived HmacLength does not match the expected value, the envelope SHOULD be rejected.

---

## Size summary

| Structure | Minimum size | Typical size | Maximum size |
|-----------|-------------|-------------|-------------|
| UET | 8 bytes | 8 bytes | 8 bytes |
| Envelope (unsigned, empty payload) | 22 bytes | — | — |
| Envelope (signed, empty payload) | 30 bytes | 34 bytes | 38 bytes |
| Envelope (signed, with UET payload) | 38 bytes | 42 bytes | 46 bytes |
| Envelope (signed, typical alert) | — | 45–100 bytes | 65,573 bytes |

Maximum size assumes PayloadLength = 65,535 and HmacLength = 16. In practice, emergency payloads are typically well under 1 KB.

For comparison:

| Format | Typical alert size |
|--------|-------------------|
| CAP/XML | ~669 bytes |
| JSON | ~270 bytes |
| ECP Envelope | 45–100 bytes |
| ECP UET only | 8 bytes |

---

## Test vectors

Deterministic, language-neutral test vectors are available in [`test-vectors/`](../../test-vectors/):

| File | Contents |
|------|----------|
| [`uet-vectors.json`](../../test-vectors/uet-vectors.json) | UET encode/decode vectors (all emergency types and priorities) |
| [`envelope-vectors.json`](../../test-vectors/envelope-vectors.json) | Envelope encode/decode vectors (signed and unsigned) |
| [`hmac-vectors.json`](../../test-vectors/hmac-vectors.json) | Truncated HMAC-SHA256 compute/verify vectors |
| [`negative-vectors.json`](../../test-vectors/negative-vectors.json) | Malformed or unauthenticated inputs that must be rejected |

Each vector includes: `id`, `kind`, `input`, `expectedHex`/`expectedBase64`, and `notes`.
All vectors are fully deterministic (no random values, no time dependencies). Same input must produce the same output across languages and platforms. See [`test-vectors/README.md`](../../test-vectors/README.md) for format details and validation flow.

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-03 | Initial public specification, aligned with ECP SDK 2.0.1. |
