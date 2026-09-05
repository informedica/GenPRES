# Security Policy

## Overview

GenPRES is a medical decision support system for medication safety in pediatric and adult care. Security vulnerabilities in this software could directly impact patient safety. We take security extremely seriously and appreciate responsible disclosure of any security issues.

The current document is a first draft and will be expanded over time to include more details on our security practices, vulnerability management, and best practices for contributors.

## Supported Versions

| Version | Supported          | Status        |
| ------- | ------------------ | ------------- |
| 0.1.x-alpha (current line, see `Directory.Build.props`) | :white_check_mark: | Pre-release, active development |
| Older pre-releases | :x: | Not supported |

Versions are derived by EasyBuild.ShipIt from the commit history; see [DEVELOPMENT.md](DEVELOPMENT.md#changelog--release-automation-easybuildshipit).

## Reporting a Vulnerability

**DO NOT** open a public issue for security vulnerabilities.

### Responsible Disclosure Process

1. **Report privately** via one of these methods:
   - GitHub Security Advisories (preferred): Use the "Report a vulnerability" button in the Security tab
   - Email: [INSERT SECURITY CONTACT EMAIL]
   - Subject line: `[SECURITY] Brief description of issue`

2. **Include in your report:**
   - Description of the vulnerability
   - Steps to reproduce the issue
   - Potential impact (especially related to patient safety)
   - Affected versions
   - Suggested fix (if available)
   - Whether you plan to publicly disclose (and timeline)

3. **Expected response timeline:**
   - **Initial response**: Within 48 hours
   - **Assessment**: Within 1 week
   - **Fix plan**: Within 2 weeks
   - **Security patch release**: Based on severity

4. **Coordinated disclosure:**
   - We will work with you to understand and validate the issue
   - We will develop and test a fix
   - We will coordinate public disclosure timing
   - We will credit you in the security advisory (unless you prefer to remain anonymous)

## Security Vulnerability Severity

### Critical (CVE Score 9.0-10.0)

- Remote code execution
- Privilege escalation
- Patient data exposure
- **Response time:** Immediate (24-48 hours)

### High (CVE Score 7.0-8.9)

- Authentication bypass
- Authorization bypass
- Sensitive data leakage
- **Response time:** 3-5 days

### Medium (CVE Score 4.0-6.9)

- Information disclosure
- Denial of service
- **Response time:** 1-2 weeks

### Low (CVE Score 0.1-3.9)

- Minor information disclosure
- Configuration issues
- **Response time:** As part of regular release cycle

## Security Best Practices for Contributors

### Code Security

- Never commit sensitive data (credentials, API keys, patient data)
- Use parameterized queries (SQL injection prevention)
- Validate and sanitize all user inputs
- Follow principle of least privilege
- Review [CONTRIBUTING.md](CONTRIBUTING.md) for secure coding guidelines

### Dependency Management

- Keep dependencies up to date
- Review dependency security advisories
- Use Dependabot or similar tools
- Audit third-party packages regularly

### Medical Device Compliance

- Follow MDR (EU Medical Device Regulation 2017/745) security requirements
- Maintain audit logs for all security-relevant events
- Implement access controls appropriate for medical software
- Ensure GDPR compliance for patient data

### Authentication & Authorization

- Implement secure session management
- Use strong password policies
- Support multi-factor authentication where applicable
- Implement proper role-based access control (RBAC)

### Data Protection

- Encrypt sensitive data at rest and in transit
- Implement secure key management
- Follow data minimization principles
- Ensure secure backup and recovery procedures

## Security Measures in GenPRES

> A full posture assessment of GenPRES with file:line evidence, severity per
> deployment context (dev / on-prem / SaaS), MDR / 21 CFR Part 11 mapping,
> and a remediation roadmap is maintained in
> [`docs/security/2026-04-10-security-review.md`](docs/security/2026-04-10-security-review.md).
> The list below is the high-level summary; refer to the security review
> for the authoritative state.

### Current Security Controls

1. **Input Validation**
   - Comprehensive validation of all medication orders
   - Unit of measure validation
   - Dosage range validation
   - Type-safe F# domain modeling
   - `LogAnalyzer` enforces a strict regex allow-list (`^genpres_[A-Za-z0-9_]+\.log$`) on log filenames, explicitly rejects path-traversal segments (`..`, `/`, `\`), and applies a 50 MB size cap

2. **Authentication & token handling**
   - Admin password compared with `CryptographicOperations.FixedTimeEquals` (constant-time, no per-character timing leak) — applies to both `LogAnalyzerCmd` and `ReloadResources`
   - Admin operations gated by short-lived HMAC-SHA256 tokens (1-hour TTL) issued after a `ValidatePassword` exchange; signature verification uses `FixedTimeEquals`
   - Auth token kept in browser memory only — never persisted to `localStorage` / `sessionStorage`; cleared on logout
   - Server fails closed: when `GENPRES_PASSWORD` is unset (or empty / whitespace) all admin operations are rejected
   - **Production password policy enforced at startup**: when `GENPRES_PROD=1`, the server refuses to bind any HTTP listener if `GENPRES_PASSWORD` is missing, empty, whitespace-only, or shorter than 16 characters. Operators must inject a CSPRNG-generated value (e.g. `openssl rand -base64 32`)

3. **Logging**
   - Calculation operations and requests logged (operational logging)
   - Error conditions recorded
   - *There is no user-attributable audit trail yet: GenPRES has no per-user identity. A tamper-evident audit trail is a §7.2 remediation item, see [security review F1](docs/security/2026-04-10-security-review.md#f1--no-tamper-evident-audit-trail)*

4. **Data Privacy**
   - Stateless session design (no persistent patient data)
   - Minimal data retention
   - GDPR-compliant data handling
   - Secure communication channels
   - **Secrets redacted in startup banner**: `GENPRES_URL_ID` is masked to its last-5 characters prefixed with `***`; `GENPRES_PASSWORD` is shown only as `***` or `NOT SET`. The full proprietary Sheet ID never reaches logs, screenshots, or bug reports

5. **Code Security**
   - Type-safe F# implementation
   - Immutable data structures
   - Comprehensive test coverage
   - **Newtonsoft.Json `TypeNameHandling`** is pinned to `None` in `Informedica.Utils.Lib/Json.fs` and guarded by three Expecto regression tests in the `JsonSecurity` sub-module — eliminates the canonical gadget-chain RCE vector by default. A SECURITY block-comment documents the policy; opting in to polymorphic deserialization requires a local `SerializationBinder` allow-list, never a global setting change

6. **Dependency Management**
   - Dependencies pinned via `paket.lock` and `package-lock.json`; updates are reviewed manually
   - No automated advisory monitoring, SBOM generation or secret scanning yet (see Planned Security Enhancements and [security review E8](docs/security/2026-04-10-security-review.md#e8--no-automated-secret-scanning))

7. **Build & Deployment**
   - **Proprietary `GENPRES_URL_ID` is never baked into the published Docker image** — the `Dockerfile` declares `ENV GENPRES_URL_ID=` / `ENV GENPRES_PASSWORD=` with empty defaults so the variables remain discoverable in container management UIs (Plesk, Portainer, Kubernetes manifests). Operators inject the real values at runtime via `docker run -e`, Docker secret, or Kubernetes secret
   - `.env` is gitignored (opt-in `.gitignore` strategy) and never tracked
   - Pre-commit hooks (Husky + Fantomas + markdown-lint) prevent unformatted code from being committed
   - Documented production password policy in `.env.example` and `DEVELOPMENT.md` (Password policy section)

### Planned Security Enhancements

- [ ] Automated security scanning in CI/CD (dependency advisories, secret scanning, SBOM)
- [ ] Penetration testing
- [ ] Security-focused code reviews
- [ ] Threat modeling workshops
- [ ] Security certification for medical device software

## Security Incident Response

In the event of a confirmed security incident:

1. **Containment**: Immediate actions to limit impact
2. **Assessment**: Full scope and impact analysis
3. **Remediation**: Fix development and testing
4. **Notification**: Inform affected parties and authorities as required
5. **Documentation**: Complete incident report for MDR compliance
6. **Post-mortem**: Lessons learned and process improvements

## Regulatory Compliance

GenPRES follows security requirements from:

- **EU MDR 2017/745** (Medical Device Regulation)
- **ISO 14971** (Risk Management)
- **IEC 62304** (Medical Device Software Life Cycle)
- **GDPR** (General Data Protection Regulation)
- **ISO 27001** (Information Security Management)

All security vulnerabilities are assessed for regulatory reporting requirements.

## Recognition

We believe in recognizing security researchers who help keep GenPRES secure:

- Public acknowledgment in security advisories (with permission)
- Credit in CHANGELOG.md for security fixes
- Recognition in the release notes

We currently do not offer a bug bounty program but greatly appreciate responsible disclosure.

## Questions

For non-security questions about GenPRES, please use:

- GitHub Issues for bugs and features
- GitHub Discussions for questions and ideas
- See [SUPPORT.md](SUPPORT.md) for more information

## Updates to This Policy

This security policy may be updated periodically. Check the commit history for changes.

**Last updated**: 2026-04-10
