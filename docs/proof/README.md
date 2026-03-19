# Proof Web Page

Static web proof page for the canonical FIRE scenario.

- URL target (GitHub Pages): `https://egonex-code.github.io/ecp-protocol/proof`
- Source file: `docs/proof/index.html`
- Canonical values: `CAP 669`, `JSON 270`, `ECP UET 8`
- Canonical proof hash (must match CLI): `8a5340fce836`

## Validation checklist

1. Run CLI:
   - `cd samples/ProofCard`
   - `dotnet run -- --show-payload`
2. Confirm:
   - `CAP XML payload (669 bytes)`
   - `JSON payload (270 bytes)`
   - `ECP UET payload (8 bytes)`
   - `Proof hash: 8a5340fce836`
3. Open `docs/proof/index.html` and verify same values/hashes shown.

## Deployment note

For the expected URL path (`/proof`) with GitHub Pages, the repository Pages source should be set to:

- Branch: `main`
- Folder: `/docs`

## Lightweight visit tracking

The proof page supports lightweight and anonymous visit counting via:

- `https://countapi.mileshilliard.com`

- Total visits are tracked automatically (`total` key).
- Optional source code can be passed with `?ref=<code>` to track outreach channels.
  - Example: `https://egonex-code.github.io/ecp-protocol/proof/?ref=d01`
- `ref` is sanitized client-side (`[a-z0-9_-]`, max 32 chars).
- Browser `Do Not Track` is respected (`doNotTrack = 1/yes` disables tracking calls).
- Tracking failures never break the page.

This keeps the public page simple while allowing private channel attribution outside the repository.
