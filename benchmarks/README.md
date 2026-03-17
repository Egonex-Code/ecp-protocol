# ECP SDK — Public Benchmarks

Public BenchmarkDotNet project used to verify the performance claims published in the main ECP README.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- `BenchmarkDotNet` (NuGet, restored automatically)

## Run benchmarks

Always run benchmarks in **Release** mode:

```bash
cd benchmarks/ECP.PublicBenchmarks
dotnet run -c Release
```

## Run filtered benchmarks

```bash
dotnet run -c Release -- --filter *Uet*
dotnet run -c Release -- --filter *Envelope*
dotnet run -c Release -- --filter *Size*
```

## README claim -> benchmark method mapping

| README claim | Benchmark method |
|---|---|
| UET Encode (3.3 ns, 32 B) | `UetBenchmarks.EncodeUet()` |
| UET Encode (zero-alloc, 0.28 ns, 0 B) | `UetBenchmarks.EncodeUetNoAlloc()` |
| UET Decode (1.2 ns, 0 B) | `UetBenchmarks.DecodeUet()` |
| Envelope Build + Encode (334 ns, 416 B) | `EnvelopeBenchmarks.EncodeEnvelope()` |
| Envelope Encode (zero-alloc, 6.5 ns, 0 B) | `EnvelopeBenchmarks.EncodeEnvelopeNoAlloc()` |
| Envelope Decode + HMAC verify (262 ns, 0 B) | `EnvelopeBenchmarks.DecodeEnvelopeView()` |

Derived throughput claim:

- `1,000,000,000 ns / 262 ns ≈ 3.8 million messages/sec`

## Notes

- BenchmarkDotNet performs multiple warmup and measurement iterations for statistical accuracy. A full run typically takes **2–5 minutes** depending on your hardware. This is expected behavior.
- Running `dotnet run -c Release` without `--filter` may show an interactive benchmark selection prompt, depending on your local environment.
- Results vary by CPU, memory, runtime, and OS.
- README numbers were measured on **13th Gen Intel(R) Core(TM) i9-13900KF, .NET SDK 8.0.418 (Microsoft.NETCore.App 8.0.24), Release, RyuJIT x64**.
- If your results differ significantly, please open an issue in the public repository with your environment details.

## Links

- Main README: [../README.md](../README.md)
- Wire format specification: [../docs/specification/wire-format.md](../docs/specification/wire-format.md)
- Security policy: [../SECURITY.md](../SECURITY.md)
- License: [../LICENSE.txt](../LICENSE.txt)
