# Security Baseline for the Demo Deployment

> Formerly ADR-0015 (accepted 2026-04-11). Moved out of `docs/adr/` under issue #411 because it records the security posture in force and its remediation status, not a hard-to-reverse decision; see [ADR-0000](../adr/0000-documentation-rules.md).

**Date**: 2026-04-11

**Status**: Accepted

**References**:

- [Security Review 2026-04-10](2026-04-10-security-review.md)
- [System Architecture (ADR-0001)](../adr/0001-system-architecture.md)

## Context

GenPRES is a medical-device-class clinical decision support system (CDSS).
A formal static security review was performed on 2026-04-10
([`docs/security/2026-04-10-security-review.md`](2026-04-10-security-review.md)),
followed by a live probe of the public demo at <https://genpres.nl/>. The
review grades every finding against three deployment contexts:

- **C1** — local development / public demo (current state)
- **C2** — on-prem hospital network (the realistic next step)
- **C3** — public-internet SaaS (aspirational)

The review identified open findings in three remediation buckets (§7.2,
§7.3, §7.4). Two implementation passes resolved the cheap, high-leverage
items at the application layer; the rest are deferred to architectural
work tracked in the existing roadmap. This document exists so future
maintainers, reviewers, and auditors can locate the authoritative
security artifacts and understand which decisions were deliberately
taken at the demo level versus deferred to a non-demo deployment.

## Decision

1. The 2026-04-10 security review document is the **authoritative source
   of truth** for GenPRES security findings, severity grading, and
   remediation status. Future security work must update that document
   in-place via dated `Update — YYYY-MM-DD` sections rather than
   spawning parallel review files. The live remediation state is
   summarized in the
   [Current Status — 2026-04-11](2026-04-10-security-review.md#current-status--2026-04-11)
   section of that document; per-finding evidence lives in §4 of the
   same file.

2. The security baseline currently in force on the public demo
   <https://genpres.nl/> is the **post-2026-04-11 state** documented in
   the `Update — 2026-04-11` section of the review:
   - **L1** — `Fable.Remoting.Giraffe` upgraded to 6.1.0 (requires
     `Giraffe >= 8.2.0`); the earlier `Giraffe = 6.4.0` pin is removed.
     The `safeWebApi` wrapper catching `MissingMethodException` /
     `TypeLoadException` is retained as defense-in-depth.
   - **L2 / B5** legacy catch-all string replaced with a generic 404.
   - **B2** security response headers (HSTS, CSP, X-Content-Type-Options,
     X-Frame-Options, Referrer-Policy, Permissions-Policy) emitted via
     `securityHeadersMiddleware` registered through `app_config`.
   - **A2** per-IP fixed-window rate limiter
     (`Microsoft.AspNetCore.RateLimiting`, 60 requests / 10 s, partition
     keyed by `getClientIP` so `X-Forwarded-For` is honored).
   - **B3** trusted-proxy allow-list wired via ASP.NET
     `ForwardedHeadersMiddleware` and the `GENPRES_TRUSTED_PROXIES`
     env var (defaults to `127.0.0.1, ::1`, matching the Plesk →
     Kestrel loopback hop on the public demo). Spoofed `X-Forwarded-
     For` from clients outside the allow-list is ignored, which also
     bounds the rate-limiter's partition cardinality.
   - **D2** SRI `sha384` integrity attribute on the external font-awesome
     stylesheet in `index.html`.

3. **A5** (clinical RPCs unauthenticated) is **intentionally not
   remediated** on the public demo. The decision is recorded inline in
   §4 of the security review under the A5 finding. A5 must be re-enabled
   before any deployment beyond C1 (demo) by reusing the existing
   `validateToken` helper at `ServerApi.Command.fs:72-121` behind a new
   `GENPRES_REQUIRE_AUTH=1` feature flag.

4. The deployment-time regression check for the security baseline is a
   live test suite maintained out-of-repo by the maintainer. It is
   deliberately not part of the repository because it encodes
   deployment assumptions (target URL, demo credentials, expected HTTP
   behavior) rather than source-code invariants. The current suite
   verifies the items recorded in the `Update — 2026-04-11` section of
   the security review and is run before any production deploy.

5. The remaining items in §7.2, §7.3, and §7.4 of the security review
   remain open and **must** be addressed before any non-demo deployment,
   in particular:
   - **A1**, **A5**, **F1** (per-user identity, RBAC, tamper-evident
     audit trail) for any C2 / C3 rollout.
   - **B1** (TLS termination at the F# layer) if the deployment cannot
     guarantee an HTTPS-terminating reverse proxy.

6. **Design principle — untrusted input must not be used as a key into
   server-side storage without a trust-boundary check.** The rule
   applies to every keyed storage the server allocates on behalf of an
   incoming request: rate-limiter partitions, login-attempt counters,
   per-session caches, idempotency-key tables, password-reset token
   rows, deduplication maps. The B3 + A2 interaction is the canonical
   example. Before B3, the A2 rate limiter partitioned by
   `getClientIP`, which read `X-Forwarded-For` from any client. An
   attacker flooding the server with distinct spoofed
   `X-Forwarded-For` values would (a) bypass the rate limit entirely
   (each fake IP starts with a clean counter), and (b) exhaust server
   memory (the limiter allocates a new partition object per unique
   key). B3 fixed both with one change at the trust boundary:
   `getClientIP` now returns the real TCP-layer `RemoteIpAddress` for
   any request not arriving from an IP in `GENPRES_TRUSTED_PROXIES`,
   so attacker-controlled headers can no longer be partition keys.

   The fix is always at the trust boundary, in order of preference:

   1. **Refuse to use the untrusted input as a key at all.** Use a
      server-controlled value (real TCP source, authenticated user
      ID, server-generated session ID).
   2. **Validate the input against an existing finite set** before
      allocating storage (e.g. only create a failed-login counter
      slot after confirming the username exists in the users table).
   3. **Rate-limit the request that allocates**, keyed by something
      the attacker cannot forge.

   LRU eviction, ring buffers, and TTLs are *defense in depth*
   underneath these controls, never the primary fix: they bound the
   *symptom* (storage size) without bounding the *cause*
   (attacker-controlled key cardinality), and they let an attacker
   reset their own counter by flooding evictions.

   New code that introduces any keyed server-side storage on a
   request path must document — in a code comment or PR description —
   which of the three boundary controls applies. Code review for any
   new storage allocation in `Server.fs`, `ServerApi.*.fs`, or any
   future request handler must check this principle.

## Consequences

- The security review document, the regression test suite, and this
  baseline together form a single chain of evidence: posture →
  verification → record.
- Auditors and new contributors can find the authoritative security
  state in one place.
- Any change to the demo's runtime security profile (headers, rate
  limit, CSP, auth) must be reflected in both the security review's
  `Update — YYYY-MM-DD` section and, if it changes the *baseline*, in a
  dated update to this document.
- The intentional A5 non-remediation is now traceable through both the
  security review and this baseline, so it cannot accidentally be
  inherited by a non-demo deployment.
- The formal risk-analysis artifacts are maintained with the MDR
  documentation, outside this repository; the maintainer must map the
  resolved items into them through normal change control before any C2
  deployment.
- The "untrusted input as storage key" design principle (Decision 6)
  becomes a standing review item for any future request handler that
  allocates server-side state. Absence of an explicit trust-boundary
  justification is a blocking review comment for the introducing PR.
