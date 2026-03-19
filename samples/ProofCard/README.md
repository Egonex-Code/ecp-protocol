# ProofCard

ProofCard is a tiny CLI sample that shows a visual size comparison between CAP XML, JSON, and ECP for the default FIRE scenario.
It is designed to produce a terminal output that is easy to screenshot and share.

## Run (3 commands)

```bash
git clone https://github.com/Egonex-Code/ecp-protocol.git
cd ecp-protocol/samples/ProofCard
dotnet run
```

For full raw evidence (generated CAP XML, JSON, and ECP payload), run:

```bash
dotnet run -- --show-payload
```

## What it does

- Generates a canonical FIRE scenario in 3 formats (CAP XML, JSON, ECP UET).
- Measures all byte counts live with UTF-8 (`Encoding.UTF8.GetByteCount`), no hardcoded sizes.
- Uses a canonical vector aligned with public benchmark sizes (`CAP 669`, `JSON 270`, `ECP 8`).
- Renders proportional ASCII bars for immediate visual comparison.
- Includes verification metadata: OS, .NET version, UTC date, and a short run ID.
- Prints a proof hash so other developers can compare reproducible results.
- Generates a copy-ready social text block directly in the output.
- Keeps default output compact for clean screenshots; `--show-payload` enables full raw evidence mode.

## Notes

- `ProofCard` uses `ECP.Core` through NuGet (`PackageReference`), so developers can run it independently.
- `UseAppHost=false` is enabled to reduce antivirus false positives from generated `.exe` app-host files.
- First run may take longer due to package restore; subsequent runs are typically much faster.
