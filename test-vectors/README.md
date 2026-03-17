# ECP Test Vectors

This directory provides deterministic, language-neutral vectors for validating ECP implementations without access to source code internals.

## Files

- `uet-vectors.json`: deterministic UET encode vectors.
- `envelope-vectors.json`: deterministic Envelope encode vectors (signed and unsigned).
- `hmac-vectors.json`: truncated HMAC-SHA256 compute/verify vectors.
- `negative-vectors.json`: malformed or unauthenticated inputs that must be rejected or marked invalid.

## Format Rules

- Hex values are uppercase.
- Binary fields are represented as `*Hex`.
- Each vector includes:
  - `id`: stable unique identifier.
  - `kind`: operation type (`uet-encode`, `envelope-encode`, `hmac-compute`, `hmac-verify`, etc.).
  - `input`: deterministic input values.
  - `expected*`: expected deterministic output/result.
  - `notes` or `reason`: interoperability context.

## Determinism Requirements

- No random values.
- No current-time dependencies.
- Fixed timestamps, message IDs, payloads, and keys.
- Same input MUST produce the same output across languages/platforms.

## Recommended Validation Flow

1. Parse vector JSON.
2. Execute the operation indicated by `kind`.
3. Compare output to `expectedHex` / `expectedBase64` / `expectedVerify`.
4. For negative vectors, confirm safe rejection behavior (`tryDecode=false`) or explicit auth failure (`isValid=false`).
