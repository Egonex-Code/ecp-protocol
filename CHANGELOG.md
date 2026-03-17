# Changelog

All notable changes to ECP-SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

---

## [2.0.5] — 2026-03-12

### Improved
- Updated README with complete package examples for all free packages
  (Registry, Cascade, Compatibility, DI, Transport) and corrected test count.
- Aligned test project dependencies (xunit 2.9.2, Test.Sdk 17.11.1) and
  CA1707 analyzer settings across all 10 test projects.
- Removed hardcoded version references from CONTRIBUTING.md, SECURITY.md,
  and issue templates to prevent version drift on future releases.

---

## [2.0.4] — 2026-03-12

### Improved
- NuGet package quality: XML documentation for IntelliSense, Source Link
  for source-level debugging, deterministic builds, and per-package
  descriptions.

---

## [2.0.1] — 2026-03-01

### Added
- Support for unsigned envelopes (HMAC length 0) in envelope builder and
  decoders, enabling use in trusted internal networks where cryptographic
  signing is not required.

### Changed
- HMAC length validation now accepts 0 (unsigned) or 8–16 bytes (signed).

---

## [2.0.0] — 2026-02-09

Initial public release.

### Core Protocol
- **Universal Emergency Token (UET)** — 8-byte binary token encoding
  emergency type, priority, action flags, zone, timestamp, and confirmation
  hash.
- **Emergency Envelope** — Variable-length binary envelope (45–100 bytes
  typical) with HMAC-SHA256 authentication, priority, TTL, and metadata.
- **Progressive API** — Four levels of control: one-liner (`Ecp.Alert`),
  token (`Ecp.Token`), builder (`Ecp.Envelope().Build()`), and
  zero-allocation (`WriteTo(Span<byte>)`).
- **Auto-detection decode** — `Ecp.TryDecode` automatically distinguishes
  UET from Envelope format based on message structure.

### Security
- HMAC-SHA256 message authentication with configurable length (8–16 bytes).
- AES-GCM authenticated encryption (optional).
- Key rotation via versioned `KeyRing`.
- Anti-replay protection via timestamps.

### Compression
- Two-level semantic dictionary (global + tenant) for payload compression.
- Multilingual template engine with positional parameters.

### Delivery
- Cascade broadcast with adaptive fan-out and confirmation aggregation.
- Zone-based confirmation aggregation.

### Transport
- Transport-agnostic design with pluggable implementations.
- WebSocket and SignalR transports included.
- JSON-to-ECP compatibility bridge for migration.

### Dependency Injection
- `AddEcpCore()` — Registers core protocol services.
- `AddEcpStandard()` — Registers full stack (Core + Registry + Cascade).
- `AddEcpProfile()` — Preset profiles (Minimal, Standard, Enterprise).
- Configurable options via `EcpOptions`.

### Platform
- .NET 8.0
- Zero external dependencies in `ECP.Core`.
- 181 automated tests.

---

## License

Free packages in this repository are licensed under Apache 2.0.
Premium modules (for example `ECP.Offline` and enterprise diagnostics) are
distributed under separate commercial terms.
See [LICENSE.txt](LICENSE.txt) and [NOTICE](NOTICE) for legal terms and notices.
Patent pending (UIBM).
