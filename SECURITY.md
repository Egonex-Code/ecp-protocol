# Security Policy

## Reporting a vulnerability

If you discover a security vulnerability in ECP-SDK, **please report it
responsibly**. We take security seriously and appreciate your help.

**Do NOT open a public GitHub issue for security vulnerabilities.**

Send your report to: **[security@egonex-group.com](mailto:security@egonex-group.com)**

### What to include

- Description of the vulnerability
- Steps to reproduce (if applicable)
- ECP-SDK version affected (e.g., `ECP.Core x.y.z`)
- Potential impact assessment
- Any suggested fix (optional, but appreciated)

### Report template

You can copy and paste this template into your email:

```
VULNERABILITY REPORT — ECP-SDK

Package and version: [e.g. ECP.Core x.y.z]
Severity (your estimate): [Critical / High / Medium / Low]

Description:
[What is the vulnerability?]

Steps to reproduce:
1. [Step 1]
2. [Step 2]
3. [...]

Expected behavior:
[What should happen]

Actual behavior:
[What happens instead]

Impact:
[What could an attacker do with this?]

Environment:
- OS: [e.g. Windows 11, Linux Ubuntu 22.04]
- .NET version: [e.g. .NET 8.0.4]

Suggested fix (optional):
[Your suggestion, if any]
```

If the vulnerability is sensitive, you may encrypt your report. Contact us
at security@egonex-group.com to request our PGP public key.

---

## Response process

| Step | Timeline | What happens |
|------|----------|-------------|
| 1. Acknowledgment | Within 48 hours | We confirm we received your report |
| 2. Initial assessment | Within 5 business days | We evaluate severity and scope |
| 3. Investigation | Within 15 business days | We investigate and develop a fix |
| 4. Fix and release | Depends on severity | We release a patched version |
| 5. Public disclosure | See disclosure policy below | We publish a security advisory |

For critical vulnerabilities, we aim to release a fix as quickly as possible.
We will keep you informed throughout the process.

---

## Coordinated disclosure policy

We follow a **90-day coordinated disclosure** model:

- The reporter and Egonex agree on a disclosure date, which defaults to
  **90 calendar days** after the initial report.
- Extensions may be granted by mutual written agreement if a fix requires
  more time.
- If we are unable to release a fix within 90 days, we will publish a
  security advisory with available mitigations and a revised timeline.
- We will not request indefinite delays. Transparency is a priority.

We assign severity levels to reported vulnerabilities:

| Severity | Description | Target fix timeline |
|----------|-------------|---------------------|
| Critical | Remote exploitation, data breach, authentication bypass | 7 days |
| High | Significant impact requiring specific conditions | 30 days |
| Medium | Limited impact or difficult to exploit | 60 days |
| Low | Minimal impact, informational | 90 days |

---

## Scope

### In scope

- ECP-SDK NuGet packages: `ECP.Core`, `ECP.Standard`, `ECP.Registry`,
  `ECP.Cascade`, `ECP.DependencyInjection`, `ECP.Transport.Abstractions`,
  `ECP.Transport.WebSocket`, `ECP.Transport.SignalR`, `ECP.Compatibility`
- Code, samples, benchmarks, and tests published in the
  [ecp-protocol](https://github.com/Egonex-Code/ecp-protocol) repository
- The protocol specification (wire format, encoding, security mechanisms)

### Out of scope

- The Egonex website (egonex-group.com)
- Internal systems, infrastructure, or services operated by Egonex
- Any non-public package, private repository, or internal component
  not distributed through the public NuGet or GitHub channels
- Customer-specific deployments or integrations
- Third-party dependencies (report those to their respective maintainers)
- Issues that require physical access to the target system
- Social engineering attacks

---

## Safe harbor

Egonex S.R.L. supports responsible security research conducted in good faith.
If you comply with this policy, we will:

- Not pursue legal action against you for your research
- Work with you to understand and resolve the issue
- Credit you publicly (with your permission) in the security advisory

### Conditions

"Good faith" research means ALL of the following:

- You test only on systems you own or have explicit written authorization
  to test
- You report the vulnerability promptly after discovery
- You do not exploit the vulnerability beyond the minimum necessary to
  demonstrate it
- You do not access, modify, or delete data belonging to others
- You do not perform denial-of-service attacks, data exfiltration, or
  persistent access
- You do not use findings to develop a competing product or service

### Limitations

**This safe harbor policy does NOT waive, modify, or supersede the terms
of [LICENSE.txt](LICENSE.txt).** In particular:

- This policy does **not** create any warranty, support commitment, or
  service-level obligation beyond the applicable license terms.
- This policy does **not** grant trademark rights; use of Egonex and ECP
  marks remains subject to applicable trademark law and license terms.
- Patent rights for Apache-licensed packages, if any, are governed by
  Apache License 2.0, Section 3.
- This policy does **not** grant any rights to non-public packages or
  premium modules distributed under separate commercial terms.

In case of conflict between this policy and LICENSE.txt, the terms of
LICENSE.txt and applicable law shall prevail.

---

## Supported versions

| Version | Supported |
|---------|-----------|
| 2.x     | Yes       |
| < 2.0   | No        |

Only the latest release within a supported major version receives security
updates. We recommend always using the latest version.

---

## Security advisories and CVE

When a confirmed vulnerability is fixed, we will publish a security advisory
containing:

- Advisory identifier
- Affected versions
- Severity rating
- Description of the vulnerability
- Remediation steps (upgrade version, workaround if applicable)

We currently assign advisory identifiers internally. If the vulnerability
warrants a CVE, we will request one through an appropriate CVE Numbering
Authority (CNA).

Security advisories will be published in the
[ecp-protocol](https://github.com/Egonex-Code/ecp-protocol) repository
and, when applicable, through GitHub Security Advisories.

---

## Bug bounty

We do not currently operate a paid bug bounty program. We do offer public
acknowledgment (with your permission) for responsibly disclosed
vulnerabilities.

---

## Security features

ECP-SDK includes built-in security mechanisms:

- **HMAC-SHA256** — Message authentication and integrity verification
- **AES-GCM** — Optional authenticated encryption
- **Anti-replay** — Timestamp-based replay protection
- **Key rotation** — Versioned keys via KeyRing

These mechanisms are part of the public API and are documented in the
[README](README.md).

---

## Data collection

ECP-SDK does **not** collect, transmit, or store any user data, telemetry,
analytics, or usage information. The SDK operates entirely locally within
your application. No network calls are made by the SDK itself.

This design helps support compliance with data protection regulations such
as GDPR, HIPAA, and similar frameworks, depending on your system integration.

---

## Patent notice

ECP technology is the subject of patent applications filed with UIBM (Italian
Patent and Trademark Office). Patent pending.

For Apache-licensed packages, patent rights (if any) are granted only as
stated in Section 3 of Apache License 2.0.

See [LICENSE.txt](LICENSE.txt) and [NOTICE](NOTICE) for legal terms and notices.

---

## Contact

| Purpose | Email |
|---------|-------|
| Security vulnerabilities | security@egonex-group.com |
| Commercial licensing | licensing@egonex-group.com |
| General inquiries | info@egonex-group.com |
