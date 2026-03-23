# ECP — Emergency Communication Protocol

A binary protocol for emergency communications.

[![CI](https://github.com/Egonex-Code/ecp-protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/Egonex-Code/ecp-protocol/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue)](https://github.com/Egonex-Code/ecp-protocol/blob/main/LICENSE.txt)
[![Patent](https://img.shields.io/badge/Patent-Pending-orange)](#patent-and-license)

---

## Fast track (first 60 seconds)

- Install now: `dotnet add package ECP.Core`
- Compare protocols and generate snippets: [ECP Studio](https://egonex-code.github.io/ecp-protocol/studio/)
- Verify the size claims locally: [ProofCard sample](https://github.com/Egonex-Code/ecp-protocol/tree/main/samples/ProofCard)
- Need wire details: [Wire format specification](https://github.com/Egonex-Code/ecp-protocol/blob/main/docs/specification/wire-format.md)
- Need deterministic verification inputs: [Test vectors](https://github.com/Egonex-Code/ecp-protocol/tree/main/test-vectors)

---

## Table of contents

- [Fast track (first 60 seconds)](#fast-track-first-60-seconds)
- [Safety-Critical Use Notice](#safety-critical-use-notice)
- [What is ECP?](#what-is-ecp)
- [Quick start](#quick-start)
- [Choose your level of control](#choose-your-level-of-control)
- [Packages](#packages)
- [Package examples](#package-examples)
- [Dependency injection](#dependency-injection)
- [Benchmarks](#benchmarks)
- [Measured results](#measured-results)
- [Capabilities](#capabilities)
- [Privacy](#privacy)
- [Versioning](#versioning)
- [Patent and license](#patent-and-license)
- [Security](#security)
- [Export notice](#export-notice)
- [Resources](#resources)

---

## Safety-Critical Use Notice

ECP is provided under Apache-2.0 on an "AS IS" basis and is not warranted to be error-free.
ECP is not developed, tested, or certified as a standalone safety mechanism for life-safety systems.
Users and integrators are solely responsible for system-level hazard analysis, validation, certification,
and compliance with applicable laws and standards before operational deployment.
See also [NOTICE](https://github.com/Egonex-Code/ecp-protocol/blob/main/NOTICE) for additional legal and safety notices.

---

## What is ECP?

ECP encodes emergency alerts into compact binary messages — from 8 to 100 bytes — with built-in cryptographic integrity. It was designed for scenarios where bandwidth is limited, latency matters, and message authenticity cannot be optional.

### Size comparison

| Format | Alert size | Notes |
|--------|-----------|-------|
| CAP XML (OASIS) | 669 bytes | Industry standard for alerting |
| JSON over HTTP | 270 bytes | Common in web applications |
| ECP Envelope | 45–100 bytes | Binary, signed, with metadata |
| ECP Token (UET) | 8 bytes | Minimal alert, no payload |

These numbers are reproducible. Public benchmarks are available in [`benchmarks/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/benchmarks) so you can measure them yourself.

During testing, ECP has processed 263,000+ events in our emergency management system with an average data reduction of 96%. We publish these numbers for transparency — reproducible verification artifacts are available in [`test-vectors/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/test-vectors) and [`tests/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/tests). See the [Measured results](#measured-results) section.

---

## Quick start

### Install

```bash
dotnet add package ECP.Core
```

### Send your first alert

```csharp
using ECP.Core;
using ECP.Core.Models;

byte[] alert = Ecp.Alert(EmergencyType.Fire, zoneHash: 1, priority: EcpPriority.Critical);
```

That's it. `alert` contains the emergency type, priority, zone, timestamp, and action flags in 8 bytes.
Want to see the size difference? Run `dotnet run` in `samples/ProofCard` for a visual comparison.

> [!NOTE]
> `Ecp.Alert(...)` uses the current timestamp when `timestampMinutes` is omitted, so the resulting hex changes over time.
> For deterministic hex (for example, to match test vectors), pass fixed values for `timestampMinutes` and `confirmHash`, or call `UniversalEmergencyToken.Create(...)` with fixed fields.
>
> ```csharp
> byte[] deterministic = Ecp.Alert(
>     EmergencyType.Fire,
>     zoneHash: 1,
>     priority: EcpPriority.Critical,
>     actionFlags: ActionFlags.None,
>     timestampMinutes: 12345,
>     confirmHash: 0);
> ```

---

## Choose your level of control

ECP has a progressive API. Start simple, add control when you need it.

### Level 1 — One-liner

```csharp
using ECP.Core;
using ECP.Core.Models;

byte[] alert = Ecp.Alert(EmergencyType.Earthquake, zoneHash: 42, priority: EcpPriority.Critical);
// 8 bytes, done.
```

### Level 2 — Token (structured access)

```csharp
using ECP.Core;
using ECP.Core.Models;

var token = Ecp.Token(
    EmergencyType.Fire,
    EcpPriority.Critical,
    ActionFlags.SoundAlarm | ActionFlags.FlashLights);

byte[] bytes = token.ToBytes();   // 8 bytes
string base64 = token.ToBase64(); // 12 chars — fits in an SMS
```

### Level 3 — Envelope (full protocol)

```csharp
using ECP.Core;
using ECP.Core.Models;

byte[] hmacKey = new byte[32]; // your HMAC-SHA256 key (32 bytes recommended)

var envelope = Ecp.Envelope()
    .WithType(EmergencyType.Earthquake)
    .WithPriority(EcpPriority.Critical)
    .WithTtl(120)
    .WithPayload("Evacuate Building A via Stairway B")
    .WithHmacKey(hmacKey)
    .Build();

byte[] wire = envelope.ToBytes();  // 45–100 bytes, signed, verified
```

### Level 4 — Zero allocation (high throughput)

```csharp
using ECP.Core;
using ECP.Core.Models;
using ECP.Core.Token;

var token = Ecp.Token(EmergencyType.Fire, EcpPriority.Critical);
Span<byte> buffer = stackalloc byte[UniversalEmergencyToken.Size]; // 8 bytes
token.WriteTo(buffer);
// 0.28 ns, zero heap allocation
```

### Decode from any source

```csharp
using ECP.Core;
using ECP.Core.Models;

byte[] incomingBytes = GetIncomingBytes();

if (Ecp.TryDecode(incomingBytes, out var message))
{
    if (message.IsUet)
    {
        System.Console.WriteLine($"Type: {message.Token.EmergencyType}, Priority: {message.Token.Priority}");
    }
    else if (message.IsEnvelope)
    {
        System.Console.WriteLine($"Envelope payload type: {message.Envelope.PayloadType}");
    }
}

static byte[] GetIncomingBytes() => System.Array.Empty<byte>();
```

---

## Packages

| Package | Tier | What it does | When to use it |
|---------|------|--------------|----------------|
| **`ECP.Core`** | Free (Apache-2.0) | Protocol encoder/decoder, UET, Envelope, security | **Start here.** Works everywhere. |
| `ECP.Standard` | Free (Apache-2.0) | Core + Registry + Cascade + DI helpers | Full-featured applications |
| `ECP.Registry` | Free (Apache-2.0) | Semantic compression, multilingual templates | When you need smaller payloads |
| `ECP.Cascade` | Free (Apache-2.0) | P2P broadcast, adaptive fan-out, confirmations | Multi-node delivery |
| `ECP.DependencyInjection` | Free (Apache-2.0) | `AddEcpCore()` for .NET DI | ASP.NET Core / hosted services |
| `ECP.Transport.Abstractions` | Free (Apache-2.0) | Transport layer interfaces | Building custom transports |
| `ECP.Transport.WebSocket` | Free (Apache-2.0) | WebSocket transport | Real-time web delivery |
| `ECP.Transport.SignalR` | Free (Apache-2.0) | SignalR transport | ASP.NET Core SignalR |
| `ECP.Compatibility` | Free (Apache-2.0) | JSON-to-ECP bridge | Migrating from JSON APIs |
| `ECP.Offline` | Premium (commercial) | Offline activation, deterministic authorization, forensic chain integration | Enterprise offline and compliance scenarios |
| `ECP.Diagnostics.Enterprise` (planned) | Premium (commercial) | Advanced diagnostics and governance/compliance tooling | Large-scale operations and audit programs |

**Naming note:** `Cascade` is a public package name for multi-node propagation and confirmation behavior.

**Which package do I need?**

- **Embedded device / IoT / BLE?** → `ECP.Core` (zero dependencies)
- **ASP.NET Core application?** → `ECP.Standard` + `ECP.DependencyInjection`
- **Migrating from JSON?** → Add `ECP.Compatibility`
- **Offline authorization / forensic evidence?** → `ECP.Offline` (commercial license)

---

## Package examples

`ECP.Core` and `ECP.Standard` already have examples above. This section covers the other free packages with copy/paste snippets.

### ECP.Registry — explicit default dictionary parameters

```csharp
using System.Text;
using ECP.Registry.Dictionary;

var dictionary = EmergencyDictionary.CreateDefault(dictionaryId: 1, dictionaryVersion: 1);
byte[] input = Encoding.UTF8.GetBytes("immediate evacuation at Gate B2");

Span<byte> compressed = stackalloc byte[128];
dictionary.TryCompress(input, compressed, out int compressedLength);

Span<byte> restored = stackalloc byte[128];
dictionary.TryDecompress(compressed[..compressedLength], restored, out int restoredLength);
string text = Encoding.UTF8.GetString(restored[..restoredLength]);
```

### ECP.Cascade — trust score and fan-out limit

```csharp
using ECP.Cascade;

var trust = new TrustScoreService();
int defaultScore = trust.GetScore("node-alpha"); // 55 by default
trust.SetScore("node-alpha", 82);

int score = trust.GetScore("node-alpha");
int fanOut = trust.GetFanOutLimit("node-alpha"); // high tier => wider propagation
```

### ECP.Compatibility — full JsonBridge.ToEcp signature

```csharp
using ECP.Compatibility;
using ECP.Core.Models;

byte[] hmacKey = new byte[32];
string legacyJson = """{"payloadText":"Evacuate terminal 3 via stairway B","ttl":90}""";
byte[] ecpBytes = JsonBridge.ToEcp(
    legacyJson,
    hmacKey.AsSpan(),
    hmacLength: 16, // valid range: 8-16 (or 0 to disable HMAC)
    keyVersion: 1,
    priority: EcpPriority.Critical,
    ttlSeconds: 120,
    flags: EcpFlags.None,
    payloadType: EcpPayloadType.Alert);
```

### ECP.DependencyInjection — AddEcpCore registration output

```csharp
using System.Linq;
using ECP.Core.Security;
using ECP.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

byte[] hmacKey = new byte[32];
var services = new ServiceCollection();
var keyRing = new KeyRing();
keyRing.AddKey(1, hmacKey);

services.AddEcpCore(o => { o.HmacLength = 16; o.KeyVersion = 1; o.KeyProvider = keyRing; });
string[] registered = services.Select(d => d.ServiceType.FullName!).OrderBy(n => n).ToArray();
Console.WriteLine($"AddEcpCore registered {registered.Length} services.");
```

With a `KeyRing` provider, `AddEcpCore()` registers these 7 services:

- `ECP.Core.EcpOptions`
- `ECP.Core.Security.IKeyProvider`
- `ECP.Core.Security.ITenantKeyProvider`
- `ECP.Core.Tenancy.ITenantContext`
- `ECP.Core.Privacy.ITenantPrivacyOptionsProvider`
- `ECP.Core.Privacy.ZoneHashProvider`
- `ECP.Core.Strategy.IStrategySelector`

### ECP.Transport.Abstractions — transport-agnostic send flow

```csharp
using ECP.Transport.Abstractions;

static async Task SendAlertAsync(IEcpTransport transport, byte[] packet, CancellationToken ct)
{
    if (!transport.IsConnected)
    {
        await transport.ConnectAsync("wss://alerts.example/ws", ct);
    }

    await transport.SendAsync(packet, ct);
}
```

### ECP.Transport.WebSocket — constructor and ConnectAsync endpoint

```csharp
using System.Threading;
using ECP.Core.Security;
using ECP.Core;
using ECP.Transport.WebSocket;

byte[] hmacKey = new byte[32];
var keyRing = new KeyRing();
keyRing.AddKey(1, hmacKey);

await using var transport = new EcpWebSocketTransport(
    new EcpWebSocketOptions(),
    new EcpOptions { HmacLength = 16, KeyVersion = 1, KeyProvider = keyRing },
    keyRing);

string endpoint = "wss://alerts.example/ws";
var connect = (CancellationToken ct) => transport.ConnectAsync(endpoint, ct); // endpoint belongs here
```

### ECP.Transport.SignalR — constructor and ConnectAsync endpoint

```csharp
using System.Threading;
using ECP.Core.Security;
using ECP.Core;
using ECP.Transport.SignalR;

byte[] hmacKey = new byte[32];
var keyRing = new KeyRing();
keyRing.AddKey(1, hmacKey);

await using var transport = new EcpSignalRTransport(
    new EcpSignalROptions(),
    new EcpOptions { HmacLength = 16, KeyVersion = 1, KeyProvider = keyRing },
    keyRing);

string endpoint = "https://alerts.example/hubs/ecp";
var connect = (CancellationToken ct) => transport.ConnectAsync(endpoint, ct); // endpoint belongs here
```

---

## Dependency injection

### Minimal setup (Core only)

```csharp
using ECP.Core;
using ECP.Core.Security;
using ECP.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

byte[] hmacKey = new byte[32]; // load your key from secure storage

var keyRing = new KeyRing();
keyRing.AddKey(keyVersion: 1, key: hmacKey);

builder.Services.AddEcpCore(options =>
{
    options.HmacLength = 16;
    options.KeyVersion = 1;
    options.KeyProvider = keyRing;
});
```

### Full setup (Core + Registry + Cascade)

```csharp
using ECP.Core;
using ECP.Core.Security;
using ECP.Standard;

var builder = WebApplication.CreateBuilder(args);

byte[] hmacKey = new byte[32]; // load your key from secure storage

var keyRing = new KeyRing();
keyRing.AddKey(keyVersion: 1, key: hmacKey);

builder.Services.AddEcpStandard(options =>
{
    options.HmacLength = 16;
    options.KeyVersion = 1;
    options.KeyProvider = keyRing;
});
```

### Preset profiles

```csharp
using ECP.Core.Profiles;
using ECP.Standard;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEcpProfile(EcpProfile.Minimal);      // Core only
builder.Services.AddEcpProfile(EcpProfile.Enterprise);    // Core + Registry + Cascade
```

---

## Benchmarks

Run with [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 8.0.

**Environment:** 13th Gen Intel(R) Core(TM) i9-13900KF, .NET SDK 8.0.418 (Microsoft.NETCore.App 8.0.24), Release configuration, RyuJIT x64. Full details and runnable code are in [`benchmarks/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/benchmarks).

| Operation | Time | Allocation |
|-----------|------|-----------|
| UET Encode | 3.3 ns | 32 B |
| UET Encode (zero-alloc) | 0.28 ns | 0 B |
| UET Decode | 1.2 ns | 0 B |
| Envelope Build + Encode | 334 ns | 416 B |
| Envelope Encode (zero-alloc) | 6.5 ns | 0 B |
| Envelope Decode + HMAC verify | 262 ns | 0 B |

At 262 ns per decode, a single core can process roughly 3.8 million messages per second. Most of that time (~250 ns) is spent on HMAC-SHA256 verification — a deliberate choice to keep integrity verification on by default.

Runnable benchmark code is available in [`benchmarks/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/benchmarks) so you can reproduce these numbers on your own hardware. If your measurements differ significantly, please [open an issue](https://github.com/Egonex-Code/ecp-protocol/issues). We want these numbers to be honest.

---

## Measured results

These numbers come from testing with real event data. We share them for transparency.

Test vectors and source tests are available in [`test-vectors/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/test-vectors) and [`tests/`](https://github.com/Egonex-Code/ecp-protocol/tree/main/tests) so you can verify protocol behavior independently.

| Metric | Value |
|--------|-------|
| Events processed | 263,000+ |
| Average data reduction | 96% |
| Forensic records verified | 645 |
| Delivery confirmations tracked | 182 |
| Automated tests (SDK, private CI) | 235 |
| Public test projects | 10 |

We're a small team and this is a young protocol. If you find issues, inconsistencies, or have questions about these numbers, we genuinely want to hear from you.

Found it useful? A [star on the repo](https://github.com/Egonex-Code/ecp-protocol) helps other engineers discover ECP.

---

## Capabilities

- **8-byte alerts** — UET format encodes a complete emergency in 8 bytes
- **Zero dependencies** — Core uses only .NET BCL, no external packages
- **Transport agnostic** — Tested over WebSocket; designed for BLE, LoRa, SMS, satellite, NFC
- **Built-in security** — HMAC-SHA256 authentication, AES-GCM encryption (optional)
- **Zero-allocation paths** — `WriteTo(Span<byte>)` for constrained environments
- **Cascade broadcast** — P2P delivery, O(log N) scaling, confirmation aggregation
- **Forensic integrity (premium)** — Tamper-evident records for enterprise deployments (patent pending)
- **Offline activation (premium)** — Deterministic activation flows for disconnected scenarios (patent pending)
- **Semantic compression** — Two-level dictionary (global + tenant)
- **Progressive API** — One-liner → Token → Builder → Zero-alloc, choose your level

---

## Privacy

ECP-SDK does not collect, transmit, or store any data. No telemetry, no analytics, no network calls. The SDK runs entirely inside your application.

This design helps support compliance with regulations such as GDPR, HIPAA, and similar frameworks, depending on your system integration.

---

## Versioning

ECP follows [Semantic Versioning](https://semver.org/):

- **PATCH** — Bug fixes, no API changes
- **MINOR** — New features, backward compatible
- **MAJOR** — Breaking changes (with migration guide)

Public APIs marked `[Obsolete]` are maintained for at least one minor version before removal.

---

## Patent and license

ECP uses an Open Core model.

**Free packages (Apache-2.0):** The packages listed as "Free" in this README are licensed under Apache 2.0 and can be used in development and production, including commercial use.
**Premium packages (commercial license):** Premium modules (such as `ECP.Offline` and `ECP.Diagnostics.Enterprise`) require a separate commercial agreement. Contact [licensing@egonex-group.com](mailto:licensing@egonex-group.com).

ECP technology includes patent-pending elements filed with UIBM. For Apache-licensed packages, patent rights (if any) are granted only as stated in Section 3 of Apache License 2.0.

See [LICENSE.txt](https://github.com/Egonex-Code/ecp-protocol/blob/main/LICENSE.txt) and [NOTICE](https://github.com/Egonex-Code/ecp-protocol/blob/main/NOTICE) for legal terms and attribution notices.

---

## Security

Found a vulnerability? **Do not open a public issue.** See [SECURITY.md](https://github.com/Egonex-Code/ecp-protocol/blob/main/SECURITY.md) for our responsible disclosure policy.

---

## Export notice

This software uses standard cryptographic algorithms (AES-GCM, HMAC-SHA256) provided by the .NET runtime. It does not implement custom cryptographic primitives. Distribution of this software may be subject to export control regulations in certain jurisdictions.

---

## Resources

**In this repository:**

- [Wire format specification](https://github.com/Egonex-Code/ecp-protocol/blob/main/docs/specification/wire-format.md)
- [ECP Studio (live: compare, decode, generate)](https://egonex-code.github.io/ecp-protocol/studio/)
- [ECP Studio source](https://github.com/Egonex-Code/ecp-protocol/tree/main/docs/studio) — static tool source in this repository
- [Studio estimation model](https://github.com/Egonex-Code/ecp-protocol/blob/main/docs/studio/estimation-model.md) — formulas behind Compare and bandwidth estimates
- [Source code](https://github.com/Egonex-Code/ecp-protocol/tree/main/src) — free package implementations published in this repository
- [Samples](https://github.com/Egonex-Code/ecp-protocol/tree/main/samples) — 8 runnable console applications
- [Benchmarks](https://github.com/Egonex-Code/ecp-protocol/tree/main/benchmarks) — BenchmarkDotNet project, reproducible
- [Tests](https://github.com/Egonex-Code/ecp-protocol/tree/main/tests) — 10 public test projects
- [Test vectors](https://github.com/Egonex-Code/ecp-protocol/tree/main/test-vectors) — deterministic JSON vectors for cross-platform verification
- [Changelog](https://github.com/Egonex-Code/ecp-protocol/blob/main/CHANGELOG.md)
- [Contributing](https://github.com/Egonex-Code/ecp-protocol/blob/main/CONTRIBUTING.md)
- [Security policy](https://github.com/Egonex-Code/ecp-protocol/blob/main/SECURITY.md)

**External:**

- [NuGet packages](https://www.nuget.org/profiles/Egonex)
- [Egonex website](https://egonex-group.com)

---

*Made in Italy by [Egonex S.R.L.](https://egonex-group.com) — built for emergencies, open source for builders.*

*Copyright © 2026 Egonex S.R.L.*
