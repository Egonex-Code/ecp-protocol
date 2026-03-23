# ECP Studio Estimation Model

This document explains how the Compare tab in ECP Studio computes estimated byte sizes and daily transfer summaries.

The implementation lives in `docs/studio/advisor.js` and is intentionally deterministic for the same input set.

## Scope and intent

- Values shown in Compare are **estimates** for protocol fit and transfer impact.
- Wire-level correctness and protocol conformance are verified in `tests/` and `test-vectors/`.
- Inputs are normalized before calculation (message kind, payload size, transport, recipient band, readability, messages/day).

## Recipient band model

Studio maps recipient bands to representative counts:

| Band | Representative count |
| --- | ---: |
| `1` | 1 |
| `2-10` | 6 |
| `10-100` | 55 |
| `100-1000` | 550 |
| `1000+` | 1500 |

## Per-message size estimates

Let:

- `payloadBytes` = input payload size (or default when unknown)
- `complexity` = message-kind multiplier:
  - `alert`: 0.9
  - `telemetry`: 1.0
  - `command`: 1.2
  - `beacon`: 0.65
  - `generic`: 1.35

### ECP

- `ecp-uet = 8`
- `ecpInnerPayload = 8` for `alert` or `beacon`
- Otherwise:
  - `ecpInnerPayload = round(max(12, payloadBytes * 0.24 + complexity * 10))`
- `ecp-envelope = 22 + ecpInnerPayload + 12`

### JSON / Protobuf / MessagePack / CBOR / CAP XML

- `json = round(max(48, payloadBytes * 1.08 + 28 + complexity * 22))`
- `protobuf = round(max(16, payloadBytes * 0.58 + 14 + complexity * 10))`
- `messagepack = round(max(14, payloadBytes * 0.72 + 12 + complexity * 12))`
- `cbor = round(max(12, payloadBytes * 0.68 + 10 + complexity * 10))`
- `cap-xml = round(max(240, payloadBytes * 2.8 + 220 + complexity * 30))`

## Daily bandwidth estimate

Bandwidth uses transport profile overhead/retry values and recipient delivery units:

- `jsonDeliveryBytes = jsonBytes + transportOverhead.json`
- `ecpDeliveryBytes = ecpBytes + transportOverhead.ecpVariant`
- `jsonDeliveryUnits = recipientsRepresentativeCount`
- `ecpDeliveryUnits = strategy-derived units` (from `selectStrategy`)

Daily transfer:

- `jsonDailyBytes = round(jsonDeliveryBytes * messagesPerDay * jsonDeliveryUnits * jsonRetryFactor)`
- `ecpDailyBytes = round(ecpDeliveryBytes * messagesPerDay * ecpDeliveryUnits * ecpRetryFactor)`

Derived metrics:

- `perMessageSavingsPercent = max(0, (jsonBytes - ecpBytes) / jsonBytes * 100)`
- `dailySavingsPercent = max(0, (jsonDailyBytes - ecpDailyBytes) / jsonDailyBytes * 100)`

When JSON is the top recommendation for the selected profile, Studio reports absolute per-message and daily values without forcing an ECP-savings claim.

## Transport profile constants

Transport models currently used by Studio:

| Transport | JSON overhead | ECP UET overhead | ECP Envelope overhead | JSON retry | ECP retry |
| --- | ---: | ---: | ---: | ---: | ---: |
| WiFi/Ethernet | 54 | 28 | 32 | 1.01 | 1.01 |
| BLE | 20 | 12 | 15 | 1.08 | 1.05 |
| LoRa | 18 | 10 | 13 | 1.14 | 1.09 |
| Satellite | 126 | 86 | 90 | 1.08 | 1.05 |
| SMS | 22 | 14 | 17 | 1.12 | 1.08 |
| Mixed | 42 | 24 | 28 | 1.05 | 1.03 |

## Source references

See these functions in `docs/studio/advisor.js`:

- `normalizeAdvisorAnswers(...)`
- `describeRecipientsBandModel(...)`
- `estimateProtocolSizes(...)`
- `calculateBandwidth(...)`
- `selectStrategy(...)`
