# ECP SDK - Public Test Projects

This folder contains public test projects for the Apache-licensed ECP packages.

## Scope

- Tests verify public API behavior (input/output), transport integration, and protocol conformance.
- Coverage includes UET, Envelope, HMAC/security, strategy/cascade, dictionary/templates, DI profiles, compatibility bridge, and transports.
- No private/offline modules are used.

## Projects

- `ECP.BlackBoxTests/`
- `ECP.Core.Tests/`
- `ECP.Cascade.Tests/`
- `ECP.Compatibility.Tests/`
- `ECP.DependencyInjection.Tests/`
- `ECP.Registry.Tests/`
- `ECP.Standard.Tests/`
- `ECP.Transport.Abstractions.Tests/`
- `ECP.Transport.SignalR.Tests/`
- `ECP.Transport.WebSocket.Tests/`

## Project references

- All test projects reference source projects via `ProjectReference`.
- No NuGet package access is required to run the test suite from this repository.

## Run

From repository root:

```bash
dotnet test ECP.Public.sln -c Release
```

## Relationship with Test Vectors

- JSON vectors are in `../test-vectors/`.
- `VectorConformanceTests` loads those JSON files and validates SDK output against expected values.
- This keeps C# tests and published vectors synchronized over time.
