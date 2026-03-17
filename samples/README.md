# ECP SDK — Samples

Runnable console applications demonstrating core ECP SDK features. Each sample is a standalone .NET 8 project that you can build and run independently.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Repository dependencies restored locally (samples use `ProjectReference`, no ECP NuGet packages required)

## Quick start

```bash
cd samples/01-UetBasics
dotnet run
```

## Windows antivirus note

Some antivirus products may scan locally-built `.exe` files. If that happens on Windows, run samples through the .NET host:

```powershell
.\run-samples-no-exe.ps1
```

This script builds and runs each sample as a `.dll` via `dotnet`, avoiding direct `.exe` execution.

## Samples

| # | Name | What it demonstrates | Packages used |
|---|------|---------------------|---------------|
| 01 | [UET Basics](01-UetBasics/) | Create and decode an 8-byte Universal Emergency Token | ECP.Core |
| 02 | [Envelope Signed Round-trip](02-EnvelopeSignedRoundtrip/) | Build a signed envelope, verify HMAC, detect tampering | ECP.Core |
| 03 | [Generic Decode Router](03-GenericDecodeRouter/) | Receive raw bytes and auto-detect UET vs Envelope | ECP.Core |
| 04 | [DI & Key Rotation](04-DiAndKeyRing/) | Register ECP services, manage HMAC keys, rotate keys | ECP.Core, ECP.DependencyInjection, ECP.Standard |
| 05 | [ECP.Sample.Console](ECP.Sample.Console/) | End-to-end console flow using the standard package set | ECP.Core, ECP.Standard |
| 06 | [ECP.Sample.Minimal](ECP.Sample.Minimal/) | Minimal/embedded usage with compact token workflow | ECP.Core |
| 07 | [ECP.Sample.MultiTenant](ECP.Sample.MultiTenant/) | Tenant isolation, key separation, and geo-quorum basics | ECP.Core, ECP.Standard |
| 08 | [ECP.Sample.Playground](ECP.Sample.Playground/) | Strategy selection playground (recipient count vs payload size) | ECP.Core |

## Notes

- All HMAC keys in these samples are **randomly generated for demonstration only**. In production, use properly managed cryptographic keys.
- For the full wire format specification, see [docs/specification/wire-format.md](../docs/specification/wire-format.md).
- For security policy and responsible disclosure, see [SECURITY.md](../SECURITY.md).

## License

These samples are provided under the same license as the ECP SDK. See [LICENSE.txt](../LICENSE.txt) for terms.
