# GenPRES Security Review — 2026-04-10

| Field | Value |
|---|---|
| Date | 2026-04-10 |
| Reviewer | Static + light dynamic review (Claude Code, supervised by @halcwb) |
| Repository | `halcwb/GenPRES` |
| Commit | `64f6987` (`master`) |
| Scope | Server, Client, Shared API, build/CI/Docker, dependencies |
| Methodology | Source code reading, grep/glob pattern matching, dependency manifest review |
| Document type | Security review (not a formal MDR risk analysis, not a pentest report) |

---

## Executive summary

GenPRES is a medical-device-class clinical decision support system (CDSS) built on the SAFE Stack. This review covers its security posture as of commit `64f6987`. Because GenPRES has no fixed deployment story today — it runs on developer machines, is being prepared for on-prem hospital deployment, and is occasionally discussed as a future SaaS — every finding is graded against three deployment contexts:

- **C1 — Local development / demo**: the dominant mode today; loopback-only; trusted operator.
- **C2 — On-prem hospital network**: single-tenant, behind hospital firewall, accessed by clinicians on the LAN. The realistic next step.
- **C3 — Public-internet SaaS**: multi-tenant, internet-facing. Aspirational; not currently in scope.

**Severity counts (excluding Informational positives)**

| Severity | C1 dev | C2 on-prem | C3 SaaS |
|---|---|---|---|
| Critical | 0 | 1 | 5 |
| High     | 2 | 7 | 8 |
| Medium   | 8 | 7 | 5 |
| Low      | 7 | 4 | 4 |

*These are the original 2026-04-10 baseline counts. Live counts are
in [Current Status](#current-status--2026-04-11); the diff history
lives in the dated `Update —` sections.*

**Top three risks (any deployment beyond C1):**

1. **F1 — No audit trail.** No append-only, user-attributable record of who logged in, who changed what scenario, who reloaded resources. This is a hard MDR/21 CFR Part 11 blocker for any production deployment, independent of the CIA triad.
2. **A1 + A5 — Authentication is effectively absent for clinical operations.** A single shared admin password gates only a handful of "admin" RPCs (`ListLogFiles`, `AnalyzeLogFile`, `ReloadResources`). All clinical RPCs — `UpdateOrderContext`, `FormularyCmd`, `NutritionPlanCmd`, `InteractionCmd` — are completely unauthenticated. Anyone reachable on port 8085 can compute medication scenarios for any patient payload. *(A5 is intentionally deferred on the public demo as a documented decision — see [Current Status](#current-status--2026-04-11) and the A5 finding in §4. A1 is still fully open. Both must be re-enabled before any non-demo deployment, behind the `GENPRES_REQUIRE_AUTH=1` feature flag.)*
3. **B1 + ~~B2~~ — No transport security ~~and no security headers~~.** The server binds plain HTTP on `*:8085`. No HTTPS at the F# layer; ~~no HSTS, no CSP, no `X-Frame-Options`~~. Acceptable for C1; unacceptable for C2 or C3. *(B2 resolved 2026-04-11 — see [Current Status](#current-status--2026-04-11). B1 still open: the demo mitigates via TLS termination at the Plesk reverse proxy; the F# bind itself remains plain HTTP.)*

**The single most cost-effective remediation** is to put GenPRES behind a TLS-terminating reverse proxy (nginx/Caddy/Traefik) that also enforces a network ACL or basic auth, *while* the in-app auth model is rebuilt. That single control mitigates B1, ~~B2 partially, A2 partially, and B3 cleanly~~, and is feasible without source changes. *(B2, A2, and B3 have since been resolved at the application layer 2026-04-11 — see [Current Status](#current-status--2026-04-11). The proxy now only adds B1 mitigation plus the network-ACL / basic-auth backstop.)*

The good news: there are no currently exploitable RCE vectors, no XSS sinks, no committed secrets, the password mechanism (where it exists) uses HMAC-SHA256 + `FixedTimeEquals` + 1-hour TTL, and the `LogAnalyzer` path-traversal handling is exemplary. The bones of a secure system are present — the gaps are predominantly *coverage* and *consistency*, not *cryptographic correctness*.

> **Live remediation state as of 2026-04-11: see
> [Current Status](#current-status--2026-04-11) below.** The
> Executive Summary above is the original assessment, preserved for
> traceability — numbers and "top three risks" entries are stale
> where the dated `Update —` sections have superseded them.

---

## Current Status — 2026-04-11

The single place for the live remediation state. The dated
`Update —` sections are the change history; §4 carries per-finding
evidence, decisions, and resolution markers.

### Severity counts (live)

| Severity | C1 dev / demo | C2 on-prem | C3 SaaS |
|---|---|---|---|
| Critical | 0 | 1 | 4 |
| High     | 1 | 3 | 5 |
| Medium   | 5 | 3 | 2 |
| Low      | 3 | 3 | 3 |

Diff history vs. the 2026-04-10 baseline lives inside the dated
`Update —` sections; the table above is authoritative. Most recent
delta: **E5 removed** from all three contexts (C1 Med, C2 High, C3
High) on 2026-04-11 — `ClosedXML 0.97` deleted as dead code rather
than upgraded.

### Closed (resolved in source)

| ID | Title | Resolved |
|---|---|---|
| **C1** | `Newtonsoft.Json TypeNameHandling` flipped to `None` + 3 regression tests | 2026-04-10 |
| **D4** | Constant-time password comparison via `FixedTimeEquals` (token migration still tracked by `TODO(D4 follow-up)`) | 2026-04-10 (partial) |
| **E2** | Production password policy + startup `validateProductionPassword` (≥16 chars, fail-closed) | 2026-04-10 |
| **E3** | `GENPRES_URL_ID` removed from Dockerfile `ARG/ENV`; runtime injection only; banner masks Sheet ID; ID rotated | 2026-04-10 |
| **L1** | `Fable.Remoting.Giraffe 5.24` ABI mismatch on .NET 10 resolved by upgrading to `Fable.Remoting.Giraffe 6.1.0` (requires `Giraffe >= 8.2.0`); `Giraffe = 6.4.0` pin removed; `safeWebApi` wrapper retained as defense-in-depth | 2026-04-18 |
| **L2 / B5** | Legacy framework-leaking root response replaced with generic `404 Not Found` | 2026-04-11 |
| **B2** | `securityHeadersMiddleware` (HSTS, CSP with MUI/Emotion-compatible `'unsafe-inline'` style-src, XCTO, XFO, Referrer-Policy, Permissions-Policy); `X-Powered-By` stripped at the app layer | 2026-04-11 |
| **A2** | Per-IP fixed-window rate limiter (`Microsoft.AspNetCore.RateLimiting`, 60 req / 10 s, partition keyed via `getClientIP`) | 2026-04-11 |
| **B3** | `ForwardedHeadersMiddleware` + `GENPRES_TRUSTED_PROXIES` allow-list; `getClientIP` ignores `X-Forwarded-For` from non-trusted sources; bounds rate-limiter partition cardinality (closes the A2 unbounded-memory side-effect) | 2026-04-11 |
| **D2** | SRI `sha384` integrity attribute on the external font-awesome stylesheet | 2026-04-11 |
| **E5** | `ClosedXML 0.97` removed entirely (not upgraded). Dead since commit `49d26df` (MetaVision removal); zero call sites in `src/`, `tests/`, any `.fsproj`, or any `.fsx`. `paket.dependencies` and `scripts/load-dependencies.fsx` updated; `paket.lock` regenerated; build clean. | 2026-04-11 |

Two ancillary hardening items landed alongside §7.1 (detail in
*Update — 2026-04-10*):

- Startup banner redacts `GENPRES_URL_ID` to `***<last 5 chars>`.
- Empty-vs-unset semantics fixed for `GENPRES_URL_ID` and
  `GENPRES_PASSWORD`: an empty Dockerfile default is now reported
  as not-set rather than misread as a real value.

### Intentionally deferred

| ID | Why | Re-enable when |
|---|---|---|
| **A5** (clinical RPCs unauthenticated) | Public demo at `https://genpres.nl/` exists for visibility; gating defeats the purpose. | Any non-demo deployment. Re-use `validateToken` at `ServerApi.Command.fs:72-121` behind a `GENPRES_REQUIRE_AUTH=1` flag. Live regression tests 3.3 / 3.4 / 3.5 FAIL on the demo and must PASS elsewhere. Decision recorded at A5 in §4 and in the [security baseline](security-baseline.md). |
| **1.3** (`X-Powered-By: PleskLin`) | Plesk-managed proxy injects the header downstream of any user-configurable Apache or nginx directive. Discloses managed-hosting type only; no exploitable surface. | C2 migration off shared Plesk hosting (§7.2) resolves it automatically. Investigation log preserved in *Hotfix — 2026-04-11 (1.3 X-Powered-By disclosure — accepted as deferred)*. |

### Open — blocks any C2 (on-prem) rollout

`A1` per-user identity / RBAC · `A5` re-enable behind feature flag ·
`B1` HTTPS at the app layer (or guaranteed TLS-terminating proxy) ·
`F1` tamper-evident audit trail · `F2` cache-file integrity check ·
`F3` PHI redaction in logs · `E4` replace beta/RC Fable deps ·
`E8` automated secret scanning.

Full ordered list in §7.2.

### Open — additionally blocks any C3 (SaaS) rollout

`A1` full IdP integration · `A3` revocable opaque tokens or JWT
denylist · `B4` Kestrel limits and request-size caps ·
`F1` audit retention and tamper-evidence (hash chaining + offsite
shipping) · `G1` validate `dataUrlId` format in `Web.fs`.

§7.3.

### Open — hygiene (lower priority)

`E6` paket pinning policy · `E7` strict `npm ci` · `E9` Action SHA
pinning · `G2` remove unused project references ·
`D1` build-time grep test for `rehype-raw` in client.

§7.4.

### Design principle that emerged from this review

**Untrusted input must not be used as a key into server-side storage
without a trust-boundary check.** Recorded in
[security baseline, Decision 6](security-baseline.md)
with the B3 + A2 interaction as the canonical example. Standing
review item for any future request handler that allocates
server-side state: rate-limiter partitions, login-attempt counters,
session caches, idempotency-key tables, password-reset rows,
deduplication maps.

### MDR readiness note

These docs are written to be MDR-ready but are **not** yet part of an
active MDR change-control process. On formal MDR entry, this review
and [`security-baseline.md`](security-baseline.md) come in together as the security
baseline; resolved findings then map into the risk-management report and
hazard-analysis spreadsheets of the MDR documentation (maintained outside this
repository) through normal change control.

---

## Update — 2026-04-10 (post-implementation of §7.1)

The four "Do now" remediation items from §7.1 have been implemented in the
same session as this review. **Findings outside §7.1 are unchanged** and
still apply as written. This section tracks what changed and the resulting
severity delta.

### Resolved §7.1 items

| ID | Status | Change | Files |
|---|---|---|---|
| **C1** | ✅ Fixed | `TypeNameHandling` flipped from `Auto` to `None` with a SECURITY block-comment explaining the gadget-chain risk. Three Expecto regression tests added under a new `JsonSecurity` sub-module: (i) `deSerialize<obj>` ignores a malicious `$type` payload, (ii) plain-record round-trip stays lossless, (iii) serialized output never contains `$type`. Verified by running the full server test suite (5408 passed). | `src/Informedica.Utils.Lib/Json.fs:38-56`, `tests/Informedica.Utils.Tests/Tests.fs` (new `JsonSecurity` sub-module) |
| **D4** | ✅ Partially fixed | Plain `<>` string equality replaced with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes. Fail-closed default (`Option.defaultValue true` when `GENPRES_PASSWORD` is unset) preserved. The deeper structural fix — migrate `ReloadResources` onto the HMAC token system used by `LogAnalyzerCmd` so the raw password no longer travels on the wire — is tracked by an inline `TODO(D4 follow-up)` comment. | `src/Informedica.GenPRES.Server/ServerApi.Services.fs:347-373` |
| **E2** | ✅ Fixed | Production password policy added to `.env.example` and to a new "Password policy" subsection in `DEVELOPMENT.md`. New startup check `validateProductionPassword` in `Server.fs` runs before any HTTP listener is bound and refuses to start when `GENPRES_PROD=1` and `GENPRES_PASSWORD` is missing, empty, whitespace-only, or shorter than 16 characters. Demo mode (`GENPRES_PROD≠1`) is unaffected. | `src/Informedica.GenPRES.Server/Server.fs:60-94`, `.env.example`, `DEVELOPMENT.md` (Password policy section) |
| **E3** | ✅ Fixed | `ARG GENPRES_URL_ARG` / `ENV GENPRES_URL_ID=$GENPRES_URL_ARG` removed from `Dockerfile`. Replaced with empty `ENV GENPRES_URL_ID=` / `ENV GENPRES_PASSWORD=` defaults so the variables remain discoverable in container management UIs (Plesk, Portainer, Rancher, Kubernetes manifests) while operators inject the real values at runtime via `docker run -e`, Docker secret, or Kubernetes secret. All docs that referenced the old `--build-arg GENPRES_URL_ARG` pattern updated. | `Dockerfile`, `README.md`, `AGENTS.md`, `.github/copilot-instructions.md`, `docs/adr/0001-system-architecture.md`, `DEVELOPMENT.md` |

### Additional improvements made during implementation

| Title | Reason | Files |
|---|---|---|
| **Startup banner secret redaction** | The startup banner previously printed the full `GENPRES_URL_ID` verbatim, which leaked the proprietary Sheet ID into anywhere logs were shipped, screenshotted, or pasted into a bug report. The banner now masks `GENPRES_URL_ID` to its last-5 characters prefixed with `***` (e.g. `***j8SS8`) — enough fingerprint for an operator to confirm the right ID is loaded, without exposing the secret. Existing `***`/`NOT SET` masking for `GENPRES_PASSWORD` preserved. | `src/Informedica.GenPRES.Server/Server.fs:36-50` |
| **Empty-vs-unset semantics** | The Dockerfile's empty `ENV` defaults turned `GENPRES_URL_ID` and `GENPRES_PASSWORD` from *unset* into *set-but-empty* at runtime. `Env.getItem` only treats `null` as `None`, so an empty value would otherwise come back as `Some ""` and bypass the existing `failwith "No GENPRES_URL_ID"` and the `validateProductionPassword` `None` branch. Four call sites (`provider`, `validateProductionPassword`, banner `password`, banner `urlId`) now pipe through `Option.filter (System.String.IsNullOrWhiteSpace >> not)` so an empty value is reported as not-set rather than misinterpreted as a real value. | `src/Informedica.GenPRES.Server/Server.fs:36-50, 71-95, 97-118` |

### Updated severity counts (after §7.1)

| Severity | C1 dev | C2 on-prem | C3 SaaS |
|---|---|---|---|
| Critical | 0 (–) | 1 (–) | 5 (–) |
| High     | 1 (–1) | 6 (–1) | 7 (–1) |
| Medium   | 6 (–2) | 5 (–2) | 4 (–1) |
| Low      | 7 (–) | 4 (–) | 4 (–) |

Deltas: **C1** removed High in all three contexts (Json default no longer dangerous). **D4** removed Medium in all three contexts. **E2** removed High (C2/C3) and Low→Med becomes a no-op for C1 because the dev mode is unchanged. **E3** removed Med (C1) and High (C2, C3).

### Items still open

The other three remediation buckets are unchanged:

- **§7.2 (before C2 rollout)**: A1, A2, A5, B1, B2, F1, F2, F3, B3, E4, E5, E8 — none addressed.
- **§7.3 (before C3 rollout)**: A1 (fuller), A3, B4, D2, F1 (retention), G1 — none addressed.
- **§7.4 (hygiene)**: E6, E7, E9, G2, B5, D1 — none addressed.

### Verification performed

- `dotnet run build` — clean, 0 errors, 0 warnings.
- `dotnet run servertests` — **5408 passed, 0 failed, 2 skipped**.
- `dotnet test tests/Informedica.Utils.Tests/...` — confirms the three new `JsonSecurity` regression tests run and pass.

### Recommended operator smoke test before C2 deployment

```bash
# Should fail-fast with "is not set (or is empty)"
GENPRES_PROD=1 GENPRES_PASSWORD= dotnet run

# Should fail-fast with "shorter than 16 characters"
GENPRES_PROD=1 GENPRES_PASSWORD=short dotnet run

# Should start normally
GENPRES_PROD=0 GENPRES_PASSWORD=anything dotnet run
```

### MDR follow-up

The risk-analysis files are formal regulatory artifacts maintained with the MDR
documentation outside this repository and have **not** been touched by this
implementation pass. The maintainer should map these resolved items into the
risk-management report and the hazard-analysis spreadsheets through the
project's normal change control process.

---

## Update — 2026-04-11 (post-implementation of demo remediations)

A second remediation pass landed today, scoped to the public demo at
`https://genpres.nl/`. It started from a live security probe of the
deployed site that confirmed the open findings in this review and
surfaced one new deployment-only issue (**L1**, runtime ABI drift in
`Fable.Remoting.Giraffe 5.24` on .NET 10). Findings outside this pass
are unchanged.

The remediation plan and the live regression suite that re-runs each
check are maintained out-of-repo by the maintainer. They encode
deployment assumptions (target URL, demo credentials, expected HTTP
behavior) rather than source-code invariants, and the authoritative
state of every decision they cover is recorded inline in this review.

### Resolved items

| ID | Status | Change | Files |
|---|---|---|---|
| **L1** (new) | ✅ Fixed | Pinned `Giraffe = 6.4.0` in `paket.dependencies` to dodge the binary mismatch in `Fable.Remoting.Giraffe 5.24`'s error path that previously leaked the full .NET type signature on every malformed POST. Added a `safeWebApi` wrapper around `webApi` in `Server.fs` that catches `MissingMethodException` / `TypeLoadException` and returns a clean `400 / "Bad Request"` as a belt-and-braces guarantee. | `paket.dependencies`, `paket.lock`, `src/Informedica.GenPRES.Server/Server.fs` (`safeWebApi`) |
| **L2 / B5** | ✅ Fixed | Replaced the legacy `GET >=> text "GenInteractions App. Use localhost: 8080 for the GUI"` catch-all with `setStatusCode 404 >=> text "Not Found"`. The old string disclosed a stale app name and hinted at a separate GUI on port 8080; both reachable via the nginx SPA fallback for `/.env`, `/.git/HEAD`, `/admin`, etc. | `src/Informedica.GenPRES.Server/Server.fs` (`webApp`) |
| **B2** | ✅ Fixed (C1 / demo) | Added `securityHeadersMiddleware` (ASP.NET middleware via `app_config`) using `Response.OnStarting` so the headers land on every flushed response — static files, Giraffe routes, the 404 fallback, and Fable.Remoting error responses alike. Headers: `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, `Content-Security-Policy` (with allow-list for `maxcdn`, `fonts.googleapis.com`, `fonts.gstatic.com`, `docs.google.com`). `X-Powered-By` is stripped defensively. | `src/Informedica.GenPRES.Server/Server.fs` (`securityHeadersMiddleware`, `application`) |
| **A2** | ✅ Fixed (C1 / demo) | Added `addRateLimiting` using `Microsoft.AspNetCore.RateLimiting` (no new NuGet/Paket dependency — ships with the SDK). Per-IP fixed-window limiter, 60 requests / 10 s window, no queue, partition keyed by `getClientIP` so `X-Forwarded-For` is honored behind nginx. Sized for the SPA's actual interaction pattern (~4 RPCs per click; clinician burst absorbed). Wired into the pipeline via `app_config` + `UseRateLimiter()`. The `X-Forwarded-For` trust path is now bounded by the **B3** allow-list below. A proper auth-lockout that only touches the password path needs `Remoting.fromContext` and is still deferred. | `src/Informedica.GenPRES.Server/Server.fs` (`addRateLimiting`, `application`) |
| **B3** | ✅ Fixed (C1 / demo, configurable for C2) | Wired ASP.NET `ForwardedHeadersMiddleware` with a `GENPRES_TRUSTED_PROXIES` env var (defaults to `127.0.0.1, ::1`, matching the Plesk → Kestrel loopback hop on the public demo). `getClientIP` now reads `ctx.Connection.RemoteIpAddress` after the middleware has substituted the real client IP for requests arriving from a known proxy, and ignores `X-Forwarded-For` from any other source. This closes the spoofing-bypass on the rate limiter and bounds the rate-limiter's partition cardinality, fixing the unbounded-memory side-effect of A2's reliance on the previous `getClientIP`. Hospital-LAN C2 deployments add the actual nginx fleet via the env var without recompiling. | `src/Informedica.GenPRES.Server/Server.fs` (`getClientIP`, `trustedProxies`, `addRateLimiting`, `application`) |
| **D2** | ✅ Fixed | Added `integrity="sha384-..."` + `crossorigin="anonymous"` to the `font-awesome.min.css` link in `index.html`. Google Fonts CSS endpoints (`fonts.googleapis.com/css?...`) serve user-agent-dependent CSS so SRI is not feasible for them — they would have to be self-hosted to be hashed. Documented inline. | `src/Informedica.GenPRES.Client/index.html` |

### Intentional non-remediation

- **A5** — clinical RPCs remain unauthenticated on the public demo by
  explicit user decision: gating the public demo would defeat its
  purpose. The decision is recorded inline at the **A5** finding in §4
  ("2026-04-10 — Demo decision"). A5 **must** be re-enabled before any
  non-demo deployment using the existing `validateToken` helper at
  `ServerApi.Command.fs:72-121` and a new `GENPRES_REQUIRE_AUTH=1`
  feature flag. Live regression tests 3.3 / 3.4 / 3.5 are expected to
  FAIL on the demo and must PASS on any other deployment.
- **1.3 (X-Powered-By: PleskLin)** — accepted as deferred on
  2026-04-11 after exhausting all user-configurable Apache and nginx
  override paths on the managed Plesk host. Discloses managed-hosting
  type only; no exploitable surface. Resolved automatically by the C2
  migration off shared Plesk hosting (§7.2). Full investigation,
  decision, and Plesk-config cleanup checklist are recorded in the
  *Hotfix — 2026-04-11 (1.3 X-Powered-By disclosure — accepted as
  deferred)* subsection further down. Live regression test 1.3 is
  expected to FAIL on the demo and must PASS on any non-Plesk
  deployment.

### Updated severity counts (after 2026-04-11)

| Severity | C1 dev | C2 on-prem | C3 SaaS |
|---|---|---|---|
| Critical | 0 (–) | 1 (–) | 4 (–1) |
| High     | 1 (–) | 4 (–2) | 6 (–1) |
| Medium   | 6 (–) | 3 (–2) | 2 (–2) |
| Low      | 3 (–4) | 3 (–1) | 3 (–1) |

Deltas (relative to the 2026-04-10 update counts):

- **L2 / B5** removed Low in C2 + C3.
- **B2** removed Low (C1), High (C2), High (C3).
- **A2** removed Low (C1), High (C2), Critical (C3).
- **D2** removed Low (C1), Med (C2), Med (C3).
- **B3** removed Low (C1), Med (C2), Med (C3) — addressed by the
  PR #298 review-response pass alongside the
  `QueueProcessingOrder` cleanup and the documentation hygiene
  fixes.
- **L1** is a new finding that was opened and resolved in the same
  pass; it never contributed to a count.

### Verification

- `dotnet run build` — clean, 0 errors.
- `dotnet run servertests` — **5408 passed, 0 failed, 2 skipped**.
- Live regression suite against `http://localhost:8080` (Docker
  container, freshly rebuilt with the changes above):
  **13 pass / 3 fail of 16**. The three failures are the intentional
  A5 non-remediation (regression tests 3.3 / 3.4 / 3.5).
- Live regression suite against `http://localhost:8085` (bare F# dev
  server): 11 pass / 4 fail — the extra failure is `1.1` ("GET /
  returns 200"), a local-only artifact because the dev server's
  `public/` folder is empty until a `Bundle` target runs.

### Items still open

The other three remediation buckets are unchanged from the 2026-04-10
update:

- **§7.2 (before C2 rollout)**: A1, A5, F1, F2, F3, E4, E5, E8 — none addressed in this pass. (B2 and A2 were §7.2 items but were resolved here at the application layer; B3 was resolved in the 2026-04-11 PR #298 review-response pass — see the *Resolved items* table above.)
- **§7.3 (before C3 rollout)**: A1 (fuller), A3, B4, F1 (retention), G1 — none addressed. (D2 was a §7.3 item but is now resolved.)
- **§7.4 (hygiene)**: E6, E7, E9, G2, D1 — unchanged. (B5 was a §7.4 item, resolved here.)

### MDR follow-up

Same as the 2026-04-10 update: the regulatory risk-analysis artifacts (MDR
documentation, outside this repository) have not been touched. The maintainer
should map L1, L2, B2, A2, B3, D2 into the risk-management report and the
hazard-analysis spreadsheets through normal change control before any
deployment beyond C1.

### Hotfix — 2026-04-11 (post-deploy CSP relaxation)

After the **B2** headers landed on `https://genpres.nl/`, the live SPA
rendered fully unstyled (giant unsized SVG logo, raw HTML controls,
unstyled disclaimer). Console showed 15 identical CSP violations:

```text
Applying inline style violates the following Content Security Policy
directive 'style-src 'self' https://maxcdn.bootstrapcdn.com
https://fonts.googleapis.com'.
```

**Root cause**: the client uses MUI, whose Emotion-based styling engine
injects per-component `<style>` tags into the document at runtime. The
hardened `style-src` had no `'unsafe-inline'`, so every MUI style was
dropped.

**Fix**: added `'unsafe-inline'` to the `style-src` directive only.
`script-src` stays strict (`'self'` only), so the residual XSS surface
is bounded to CSS injection — no script execution. The recommended
baseline header table earlier in this document was updated to match.

| ID | Status | Change | Files |
|---|---|---|---|
| **B2** (CSS regression) | ✅ Hotfixed | Added `'unsafe-inline'` to `style-src` so MUI/Emotion runtime styles render. Documented the trade-off and the Emotion-nonce follow-up inline above the middleware. | `src/Informedica.GenPRES.Server/Server.fs` (`securityHeadersMiddleware`) |

**Follow-up (deferred)**: wire an Emotion `CacheProvider` with a
per-request CSP nonce to drop `'unsafe-inline'` again. Requires
threading the nonce through the HTML template, the React tree, and the
middleware — multi-file change in both Client and Server. Tracked as a
new item against §7.2.

### Hotfix — 2026-04-11 (1.3 X-Powered-By disclosure — accepted as deferred)

The live regression suite flagged a new failure on the public demo:

```text
[FAIL] 1.3  X-Powered-By header is leaking the hosting stack
```

```text
$ curl -sI https://genpres.nl/ | grep -i powered
x-powered-by: PleskLin
```

The F# `securityHeadersMiddleware` calls `h.Remove "X-Powered-By"` on
every flushed response, so the leak is downstream of the application,
inside the managed Plesk reverse-proxy stack.

**Investigation summary** (chronological, all attempts on the live host
via Plesk → *Apache & nginx Settings*):

1. Added `proxy_hide_header X-Powered-By;` to the per-site nginx
   directives. No effect — the response shape (`etag`, `last-modified`,
   `accept-ranges: bytes`) showed nginx was serving `index.html`
   directly from disk and never going through any proxy hop.
2. Added `Header always unset X-Powered-By` (and `Header unset`) inside
   `<IfModule mod_headers.c>` to **both** the HTTP and HTTPS *Additional
   Apache directives* fields. No effect — Apache wasn't in the chain
   for the static `/index.html`.
3. Removed `html` and `htm` from *Serve static files directly by nginx*
   and disabled *Smart static files processing*, forcing the request
   through Apache. Response shape changed (`etag` / `last-modified` /
   `accept-ranges` disappeared), confirming Apache was now handling the
   request — but `x-powered-by: PleskLin` still leaked. So the Apache
   `Header unset` was being bypassed (header injected in a phase later
   than `mod_headers`' output filter).
4. Added `fastcgi_hide_header X-Powered-By;` alongside the
   `proxy_hide_header`. No effect — no FastCGI hop in the chain.
5. Tried the nginx **`add_header` inheritance trick**: added a no-op
   `add_header X-Robots-Tag "all" always;` in the per-site nginx
   directives. The intent was to force nginx to drop all parent
   `add_header` directives (inheritance is all-or-nothing). Result:
   `x-robots-tag: all` appeared on every response **and**
   `x-powered-by: PleskLin` survived. This proved that PleskLin is
   **not** being injected via `add_header` at any parent context — it
   is injected by a Plesk-managed mechanism (custom module, lua filter,
   or post-`mod_headers` Apache hook) that bypasses both standard
   nginx `add_header` inheritance and Apache `mod_headers unset`.
6. Headers-more (`more_clear_headers`) is not installed on this host
   (verified in step 1's earlier nginx config-test failure), so the
   usual force-unset escape hatch is unavailable.

**Decision: accept as deferred, document, do not chase further on
managed hosting.**

Justification:

- `X-Powered-By: PleskLin` discloses exactly one fact: *the host is a
  Plesk-managed Linux box*. It does **not** disclose Plesk version,
  nginx/Apache version, the F#/Saturn/Kestrel application stack, OS
  distro version, any path, secret, or attack surface.
- The information-disclosure severity is *Low* in C1 and stays *Low*
  in C2/C3, because it does not enable any specific exploit and the
  hosting provider type is independently observable from DNS / IP
  WHOIS data anyway.
- Chasing the injection source requires either (a) root SSH access to
  the managed host plus a Plesk template override that survives
  panel-driven config regeneration, or (b) a Plesk support ticket and
  a vendor patch. Both have high cost relative to the security gain.
- Migration off shared Plesk hosting (planned for the C2 deployment
  per §7.2) resolves this finding automatically. There is no value in
  building a workaround that gets thrown away at that point.

| ID | Status | Resolution | Owner |
|---|---|---|---|
| **1.3** (X-Powered-By disclosure) | ⏸ Deferred (accepted) | Plesk-managed reverse proxy injects `X-Powered-By: PleskLin` at a stage that user-configurable Apache and nginx directives cannot intercept. Discloses managed-hosting type only; no attack surface. Will be resolved by the C2 migration off shared Plesk hosting (§7.2). | maintainer |

An operator-side cleanup checklist for the Plesk *Apache & nginx
Settings* directives box (removing the diagnostic `add_header
X-Robots-Tag` no-op, optional defense-in-depth retention of
`proxy_hide_header` and the Apache `Header unset` block, etc.) was
maintained out-of-repo with the maintainer. Operational; expires
when the C2 migration retires this host.

---

## Update — 2026-04-18 (L1 root-cause fix upstream)

`Fable.Remoting.Giraffe 6.1.0` (released 2026-04-18) was recompiled
against the task-based `HttpHandler` rewrite introduced in Giraffe 7,
and declares `Giraffe (>= 8.2.0)`. Adopting it resolves the original
binary mismatch that produced the `MissingMethodException` /
`TypeLoadException` in `Giraffe.Core.setBodyFromString`, so the
earlier workaround can be unwound.

### Resolved items

| ID | Status | Change | Files |
|---|---|---|---|
| **L1** (root-cause fix) | ✅ Fixed upstream | Upgraded `Fable.Remoting.Giraffe 5.24 → 6.1`, `Fable.Remoting.Server 5.42 → 6.1`, `Fable.Remoting.Client 7.35 → 8.0` (transitively `Fable.Remoting.Json 2.25 → 3.0`, `Fable.Remoting.MsgPack 1.25 → 2.0`). The `Giraffe = 6.4.0` pin and its L1 comment block are removed from `paket.dependencies`; Paket now resolves `Giraffe 8.2` (required by `Fable.Remoting.Giraffe 6.1`) and keeps `Saturn 0.17`. The `safeWebApi` wrapper in `Server.fs` is retained as defense-in-depth and its comment refreshed to reflect the upstream fix. `paket.lock` regenerated. | `paket.dependencies`, `paket.lock`, `src/Informedica.GenPRES.Server/Server.fs` (`safeWebApi` comment only) |

### Verification

Per the plan at
`.claude/plans/use-new-fable-remoting-giraffe-fixes-fancy-wand.md`:

1. `dotnet run build` — clean.
2. `paket.lock` confirms `Fable.Remoting.Giraffe (6.x)`,
   `Giraffe (8.x)`, `Saturn (0.17)` coexist.
3. `dotnet run servertests` — all green.
4. Happy-path RPC round-trip at `http://localhost:5173`.
5. Malformed-RPC smoke: `POST /api/IServerApi/<cmd>` with
   `not-valid-json` returns HTTP 400 with a short body and no
   `System.*` / `Giraffe.*` / `Fable.Remoting.*` type names.
6. The live regression suite (security baseline, Decision 4; maintained
   out-of-repo) — test 2.2 must re-run PASS against the new build
   before deploying to `https://genpres.nl/`.

### MDR follow-up

- [`security-baseline.md`](security-baseline.md) (L1 item) updated
  to reflect the upstream fix and the retained `safeWebApi` wrapper.
- `CHANGELOG.md` carries the user-visible entry.

---

## 1. Scope and methodology

### 1.1 In scope

- F# server: `src/Informedica.GenPRES.Server/`
- F# client (Fable/Elmish/React): `src/Informedica.GenPRES.Client/`
- Shared RPC contract: `src/Informedica.GenPRES.Shared/Api.fs`
- Cross-cutting libraries with security-relevant code: `Informedica.Utils.Lib`, `Informedica.Logging.Lib`
- Build, packaging, and deployment: `paket.dependencies`, `package.json`, `Dockerfile`, `.github/workflows/`, `.husky/`
- Configuration & secrets: `.env.example`, `.gitignore`, environment variables documented in `DEVELOPMENT.md`

### 1.2 Out of scope (explicit)

- Cryptographic correctness of `Informedica.GenSOLVER.Lib` (not security-relevant)
- Penetration testing — this is a static review with light dynamic verification only
- Third-party services (Google Sheets, Docker Hub, GitHub Actions runners)
- Proprietary cache files in `data/cache/` (not in repository)
- Mathematical correctness of dose calculations
- GDPR / patient consent / data retention policy beyond the technical layer
- Physical security and operational security of any deployment

### 1.3 Methodology

1. Read every server file in the API surface (`Server.fs`, `ServerApi.*.fs`)
2. Enumerate every Fable.Remoting RPC method via `Shared/Api.fs`
3. Trace each RPC handler to find its authentication/authorization checks
4. Grep for known dangerous patterns: `TypeNameHandling`, `dangerouslySetInnerHTML`, `eval`, raw HTML rendering, `Process.Start`, path concatenation, `BinaryFormatter`, `XmlSerializer`
5. Inspect dependency manifests for outdated, beta, or floating versions
6. Read `Dockerfile` and `.github/workflows/build.yml` for build-time secret handling
7. Verify `.env` is gitignored and not tracked
8. Cross-reference findings with the MDR/21 CFR Part 11 expectations the project itself lists in `AGENTS.md`

### 1.4 Severity model

Qualitative severity: **Critical / High / Medium / Low / Informational**. Each finding is graded against the three deployment contexts (C1/C2/C3). Scores are *not* CVSS — CVSS without a fixed deployment story would be misleading.

---

## 2. Threat model

### 2.1 Assets

| Asset | Description | CIA priority |
|---|---|---|
| Patient context | Demographics + clinical state submitted for dose computation | **Confidentiality** + Integrity (PHI) |
| Computed scenarios | Dose, route, schedule, volume per patient | **Integrity** (patient safety) |
| Medication rules | OKRs derived from Google Sheets, cached locally | **Integrity** (patient safety) |
| Audit history | (does not currently exist) | Integrity, Availability |
| Admin credential | `GENPRES_PASSWORD` | Confidentiality |
| Proprietary Google Sheet ID | `GENPRES_URL_ID` | Confidentiality (commercial) |
| Server logs | Request logs + log analysis output | Confidentiality (may contain PHI) |

### 2.2 Actors

| Actor | Trusted? | Capabilities |
|---|---|---|
| Developer | Yes | Full code, deploy |
| Clinician (intended user) | Yes (in role) | Trusted for clinical use; **not** trusted to bypass safety |
| Other LAN user | Untrusted | Can reach port 8085 in C2 |
| Internet attacker | Untrusted | Reachable in C3 only |
| Malicious client (browser tampering) | Semi-trusted | Can manipulate Elmish state, replay requests |
| Supply-chain attacker | Untrusted | Can compromise deps, GitHub Actions, Docker base image |
| Insider (developer with prod access) | Semi-trusted | Has `GENPRES_PASSWORD`, can read cache files |

### 2.3 Trust boundaries

```text
                ┌─────────────┐
   PHI in →     │   Browser   │ ← XSS, supply-chain (npm), CDN
                └──────┬──────┘
                       │ HTTPS? (no, plain HTTP today)
                ┌──────┴──────┐
                │  Saturn API │ ← B1, B2, A5
                └──────┬──────┘
                       │
       ┌───────────────┼────────────────┐
       │               │                │
   Google Sheets   Local cache     LogAnalyzer files
   (via env var)   (data/cache/)   (data/logs/)
       │               │                │
   E3 (Docker      F2 (no            C3 (path-traversal
    arg leak)       integrity)        mitigated — positive)
```

### 2.4 Deployment contexts (severity columns)

| Code | Context | Network exposure | User base | Trust model |
|---|---|---|---|---|
| **C1** | Local dev / demo | Loopback only | One developer | Single-trusted operator |
| **C2** | On-prem hospital | Internal LAN | Tens to hundreds of clinicians | LAN-trusted, MDR-regulated |
| **C3** | Public-internet SaaS | Internet | Multi-tenant, unknown | Zero-trust, hostile network |

---

## 3. Findings — three-context severity matrix

Each finding has a stable ID. Severity columns: **C1 / C2 / C3**.

### 3.1 Authentication & authorization (A)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **A1** | Med | **High** | **Critical** | Single shared admin password; no per-user identity, no RBAC |
| **A2** | Low | **High** | **Critical** | No rate limiting / lockout on `ValidatePassword` |
| **A3** | Low | Med | Med | Token format leaks expiry; no server-side revocation |
| **A4** | — | — | — | *(positive)* HMAC-SHA256 + `FixedTimeEquals` + 1 h TTL on tokens |
| **A5** | Med | **High** | **Critical** | All clinical RPCs are completely unauthenticated |

### 3.2 Server hardening (B)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **B1** | Low | **High** | **Critical** | Server binds plain HTTP only — no HTTPS, no HSTS |
| **B2** | Low | **High** | **High** | No security response headers (CSP, XCTO, XFO, Referrer-Policy) |
| **B3** | Low | Med | Med | `X-Forwarded-For` trusted without `KnownProxies` allow-list |
| **B4** | Low | Med | **High** | No request size limit, no rate limit, no DoS protection |
| **B5** | Info | Low | Low | Root response discloses framework expectation |

### 3.3 Deserialization & injection (C)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **C1** | **High** | **High** | **High** | `Newtonsoft.Json` configured with `TypeNameHandling.Auto` (latent — see analysis) |
| **C2** | Med | Med | Med | No call sites currently reach untrusted input, but the dangerous default is a foot-gun |
| **C3** | — | — | — | *(positive)* `LogAnalyzer` path-traversal mitigation is exemplary |

### 3.4 Client (D)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **D1** | Low | Low | Low | Markdown rendered via Feliz.Markdown — defense-in-depth recommendation only |
| **D2** | Low | Med | Med | External CDN assets without Subresource Integrity (SRI) |
| **D3** | Low | Low | Low | Auth token in Elmish memory — no `localStorage` (positive design) |
| **D4** | Med | Med | Med | `ReloadResources` bypasses the token system, uses raw password string equality |
| **D5** | — | — | — | *(positive)* No `dangerouslySetInnerHTML`, no `eval`, no raw HTML interpolation |

### 3.5 Secrets, configuration, supply chain (E)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **E1** | — | — | — | *(positive)* `.env` is correctly gitignored and not tracked |
| **E2** | Low | **High** | **High** | Trivial dev password `genpres` — risk if same value reaches production |
| **E3** | Med | **High** | **High** | `Dockerfile` persists `GENPRES_URL_ID` into the runtime image as `ENV` |
| **E4** | Low | Med | Med | Beta/RC deps in production: Fable.Core 5.0.0-rc.1, Fable.Elmish.React 5.5.0-beta-1, Fable.Elmish.HMR 9.0.0-beta.1 |
| **E5** | Med | **High** | **High** | `ClosedXML = 0.97` is from 2020 — current is 0.104+; needs CVE audit |
| **E6** | Low | Med | Med | `Saturn ~> 0` and other floating ranges; no lockfile audit policy |
| **E7** | Low | Med | Med | Client npm deps floating; `package-lock.json` present but not pinned in CI |
| **E8** | Low | Med | Med | No automated secret scanning (`gitleaks` / `detect-secrets`) in pre-commit or CI |
| **E9** | Info | Low | Low | CI Actions pinned to floating `v4.x` tags rather than commit SHAs |

### 3.6 Logging, audit, MDR-specific (F)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **F1** | Info | **High** | **High** | No tamper-evident audit trail (MDR / 21 CFR Part 11 hard requirement) |
| **F2** | Med | Med | Med | No integrity check (hash/signature) on medication cache files — patient-safety relevant |
| **F3** | Low | Med | Med | `GENPRES_DEBUG=1` could log PHI; logger library is generic, depends on callers |
| **F4** | — | — | — | *(positive)* Password masked as `***` in startup banner |
| **F5** | — | — | — | *(positive)* Client IPs are logged on every request |

### 3.7 External integrations / SSRF (G)

| ID | C1 | C2 | C3 | Title |
|---|---|---|---|---|
| **G1** | Low | Low | Low | Google Sheets URL is not validated, but is sourced only from env var at startup |
| **G2** | Info | Info | Info | `HIXConnect.Lib` is referenced in `.fsproj` but never imported from any server `.fs` file — dead production code |

---

## 4. Detailed findings

### A1 — Single shared admin password, no user identity, no RBAC

**Severity:** C1 Med · C2 **High** · C3 **Critical**

**Evidence** (`src/Informedica.GenPRES.Server/ServerApi.Command.fs:11-14`):

```fsharp
let private validatePassword (password: string) =
    Env.getItem "GENPRES_PASSWORD"
    |> Option.map (fun expected -> password = expected)
    |> Option.defaultValue false
```

The client login form (`src/Informedica.GenPRES.Client/Components/TitleBar.fs`) hardcodes the username field as `value="genpres"` with `readOnly={true}` — there is no concept of a *user*, only a single shared secret.

**Impact:**

- **Patient safety / MDR:** EU MDR Annex I §17.2 and FDA 21 CFR Part 11.10(d) require user-attributable actions for any record that influences clinical decision-making. A shared password makes user attribution impossible by construction.
- **Operational:** Password rotation is all-or-nothing; a leaving developer triggers a global rotation; there is no concept of suspending a single user's access.

**Recommendation (in priority order):**

1. **Short term** (before C2 rollout): Move `GENPRES_PASSWORD` behind a reverse proxy that enforces per-user basic auth or OAuth/OIDC at the edge, so at least *the proxy* records who initiated each request.
2. **Medium term:** Replace `validatePassword` with a real identity provider (OIDC against the hospital's existing IdP is the lowest-friction option for C2). Map identities to roles (clinician / pharmacist / admin / read-only).
3. **Long term:** Implement RBAC at the command level — `ReloadResources` and `LogAnalyzer*` should be `admin`-only; clinical RPCs should be `clinician`-only.

**References:** OWASP ASVS V2.1, V4.1; CWE-269 (Improper Privilege Management); EU MDR Annex I §17.2; 21 CFR Part 11.10(d).

---

### A2 — No rate limiting on `ValidatePassword`

**Severity:** C1 Low · C2 **High** · C3 **Critical**

**Evidence** (`ServerApi.Command.fs:92-99`):

```fsharp
| LogAnalyzerCmd(ValidatePassword password) ->
    async {
        if validatePassword password then
            let token = generateToken ()
            return Ok(PasswordValidated(true, token) |> LogAnalyzerResp)
        else
            return Ok(PasswordValidated(false, "") |> LogAnalyzerResp)
    }
```

There is no per-IP attempt counter, no exponential backoff, no temporary lockout, no CAPTCHA. `Server.fs:141-156` adds no middleware. The dev password (`genpres` — see E2) would fall to a one-thread brute-force attempt in milliseconds.

**Impact:** Combined with A1, A2 means a single attacker on the same network as a C2 deployment can guess the admin password trivially.

**Recommendation:**

1. Add a Saturn middleware that throttles `ValidatePassword` requests by IP (e.g., `AspNetCoreRateLimit` package, 5 attempts per IP per minute, exponential backoff).
2. Combine with a server-side audit entry per failed attempt (links to F1).
3. Long term: replace password auth entirely (see A1).

**References:** OWASP ASVS V2.2; CWE-307 (Improper Restriction of Excessive Authentication Attempts).

---

### A3 — Token format and revocation

**Severity:** C1 Low · C2 Med · C3 Med

**Evidence** (`ServerApi.Command.fs:20-82`).

The token is `Base64(payload).Base64(hmac)` where `payload = $"{expiresAt}:{nonce}"`. Two minor weaknesses:

1. **Format leak:** The expiry timestamp is base64-encoded plaintext, visible to anyone holding a token. Not a vulnerability — but it telegraphs the token format and makes targeted brute force of older tokens easier if the HMAC key were ever compromised.
2. **No revocation:** Tokens cannot be revoked before their 1-hour TTL. There is no server-side state tracking issued tokens. A leaked token is valid for up to 60 minutes.

**Recommendation:**

- Acceptable for C1.
- For C2/C3: switch to opaque tokens (random 256-bit value) with a server-side index keyed by the token, allowing logout and revocation. Or adopt a standard JWT library with `jti` + a denylist.

---

### A4 — Token primitives (positive)

**Evidence** (`ServerApi.Command.fs:57-79`):

```fsharp
use hmac =
    new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret))

let expectedSig = hmac.ComputeHash(payloadBytes)

if not (System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedSig, expectedSig)) then
    false
else
    // expiry check
```

`FixedTimeEquals` correctly defends against timing attacks on signature comparison; HMAC-SHA256 is the right primitive; the 1-hour TTL is sensible. **Keep this.**

---

### A5 — All clinical RPCs are unauthenticated

**Severity:** C1 Med · C2 **High** · C3 **Critical**

**Evidence:** `ServerApi.Command.fs:85-201` and `ServerApi.Services.fs:347-369`. The pattern is: only `LogAnalyzerCmd` (Validate / List / Analyze) and `Api.ReloadResources` check any credential. Every other branch — `OrderContextCmd`, `OrderPlanCmd`, `FormularyCmd`, `ParenteraliaCmd`, `NutritionPlanCmd`, `InteractionCmd` — is reached without any check.

**Impact:**

- In **C2**: any device on the hospital LAN that can reach port 8085 can submit arbitrary patient payloads and read back computed dose scenarios. A misconfigured guest Wi-Fi could expose this.
- In **C3**: the entire clinical surface is open to the internet.
- **PHI exposure:** A patient payload submitted by an attacker echoes back, but more importantly, the *response* contains the full computed scenario plus dose math — exfiltrating the entire rule corpus is one scripted call away.
- **Resource exhaustion:** Combined with B4 (no request size limit), a single attacker can DoS the server with large patient payloads.

**Recommendation:**

1. Same edge-proxy mitigation as A1 in the short term.
2. Long term: every RPC must require an authenticated session. The cleanest path is to put `validateToken` in front of `processCmd` as a wrapper, with an explicit allow-list of unauthenticated RPCs (probably just a health check).

**References:** OWASP ASVS V4.1; OWASP API Top 10 #2 (Broken Authentication); CWE-306 (Missing Authentication for Critical Function).

> **2026-04-10 — Demo decision (intentional non-remediation):**
> A5 is **not** remediated on the public `https://genpres.nl/`
> deployment. The site exists for public visibility of GenPRES and
> gating it would defeat that purpose. A5 **must** be re-enabled
> before any non-demo deployment (C2 / C3), reusing the existing
> `validateToken` helper at `ServerApi.Command.fs:72-121` (the
> identical mechanism that already protects `LogAnalyzerCmd.ListLogFiles`
> and `LogAnalyzerCmd.AnalyzeLogFile`). The simplest path is a
> `GENPRES_REQUIRE_AUTH=1` env-var feature flag that wraps
> `processCmd` with the token check. Live regression tests
> 3.3 / 3.4 / 3.5 in the maintainer's out-of-repo regression suite
> are expected to FAIL on the demo and must PASS on any other
> deployment. The authoritative record of this decision is the
> blockquote you are currently reading; any earlier session-local
> notes are non-authoritative.

---

### B1 — No HTTPS

**Severity:** C1 Low · C2 **High** · C3 **Critical**

**Evidence** (`Server.fs:143`):

```fsharp
url ("http://*:" + port.ToString() + "/")
```

No `UseHttpsRedirection`, no HSTS, no certificate config. `vite.config.js:6` likewise targets `http://localhost:8085` for the dev proxy.

**Impact:** All credentials, all PHI, all responses traverse the network in plaintext. Combined with A1, the password is cleartext on the wire.

**Recommendation:**

- **Short term:** Front the server with a TLS-terminating reverse proxy (nginx, Caddy, Traefik). The proxy enforces HTTPS, HSTS, optional client certificate auth. Document this in `DEVELOPMENT.md` as the recommended deployment topology.
- **Medium term:** Bind directly to HTTPS in production using a Saturn `use_https` directive + Let's Encrypt certs (or hospital-managed certs in C2).
- **MDR:** Document the network topology decision in the MDR risk analysis (maintained outside this repository).

---

### B2 — No security headers

**Severity:** C1 Low · C2 **High** · C3 **High**

**Evidence:** `Server.fs:141-156` — the entire Saturn `application { ... }` block. No `use_default_files`, no header-setting middleware, no `Content-Security-Policy`. There is no CSP `<meta>` tag in `index.html` either.

**Recommended baseline (server-side, via custom middleware):**

```text
Strict-Transport-Security: max-age=31536000; includeSubDomains
Content-Security-Policy: default-src 'self'; script-src 'self' 'wasm-unsafe-eval';
                         style-src 'self' 'unsafe-inline' https://fonts.googleapis.com;
                         font-src https://fonts.googleapis.com;
                         img-src 'self' data:;
                         connect-src 'self';
                         frame-ancestors 'none';
                         base-uri 'self';
                         form-action 'self'
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: no-referrer
Permissions-Policy: geolocation=(), microphone=(), camera=()
```

Notes:

- CSP requires `'wasm-unsafe-eval'` because Fable's compiled output uses WebAssembly bridges in some configurations. Validate before enforcement.
- `style-src` includes `'unsafe-inline'` because the client uses MUI, whose Emotion-based styling engine injects per-component `<style>` tags at runtime. Without it the SPA renders fully unstyled. `script-src` stays strict (`'self'` only), so XSS exposure is bounded to CSS injection — no script execution. Tightening this further requires wiring an Emotion `CacheProvider` with a per-request nonce; tracked as a follow-up.

---

### B3 — `X-Forwarded-For` trusted without allow-list

> ✅ **Resolved 2026-04-11.** `getClientIP` was reduced to `ctx.Connection.RemoteIpAddress` after wiring ASP.NET `ForwardedHeadersMiddleware` with a `GENPRES_TRUSTED_PROXIES` env var (defaults to `127.0.0.1, ::1`, matching the Plesk → Kestrel loopback hop on the public demo). XFF is now honored only for connections from the allow-list. This also bounds the rate limiter's partition cardinality, fixing the unbounded-memory side-effect of A2's previous reliance on raw XFF. See the *Resolved items* table in the [Update — 2026-04-11](#update--2026-04-11-post-implementation-of-demo-remediations) section above.

**Severity:** C1 Low · C2 Med · C3 Med

**Evidence** (`Server.fs:20-30`, **superseded** by the resolution above):

```fsharp
let getClientIP (context: HttpContext) =
    match context.Request.Headers.TryGetValue("X-Forwarded-For") with
    | true, values when values.Count > 0 ->
        values[0].Split(',') |> Array.tryHead |> Option.map String.trim
        |> Option.defaultValue "unknown"
    | _ -> ...
```

Any client can spoof their logged IP by setting `X-Forwarded-For` themselves.

**Recommendation:** Use ASP.NET Core's `ForwardedHeadersMiddleware` with `KnownProxies` configured to the trusted reverse proxy IP only. Otherwise, fall back to `RemoteIpAddress`.

---

### B4 — No request size limit, no rate limit, no DoS protection

**Severity:** C1 Low · C2 Med · C3 **High**

Saturn/Kestrel defaults apply (~30 MB request body). With unauthenticated RPCs (A5), a single attacker can saturate the server with large patient payloads.

**Recommendation:** Configure Kestrel limits explicitly (`MaxRequestBodySize` ≈ 256 KB for API calls, `MaxConcurrentConnections`, `KeepAliveTimeout`); add per-IP rate limiting middleware.

---

### B5 — Root response discloses framework

**Severity:** C1 Info · C2 Low · C3 Low

`Server.fs:111` returns `"GenInteractions App. Use localhost: 8080 for the GUI"`. This is informational; minor fingerprinting only.

---

### C1 — Newtonsoft.Json `TypeNameHandling.Auto`

> ✅ **Resolved 2026-04-10.** `TypeNameHandling` flipped to `None` in `src/Informedica.Utils.Lib/Json.fs:38-56` with a SECURITY block-comment. Three regression tests guard the default in `tests/Informedica.Utils.Tests/Tests.fs` (`JsonSecurity` sub-module). See the [post-implementation update](#update--2026-04-10-post-implementation-of-71) above.

**Severity:** C1 **High** · C2 **High** · C3 **High** *(downgraded from Critical after reachability check)*

**Evidence** (`src/Informedica.Utils.Lib/Json.fs:42-46`):

```fsharp
JsonSerializerSettings(
    TypeNameHandling = TypeNameHandling.Auto,
    NullValueHandling = NullValueHandling.Ignore,
    Converters = converters
```

`TypeNameHandling.Auto` is the canonical Newtonsoft.Json RCE vector — it lets a malicious payload instantiate arbitrary types via gadget chains.

**Reachability analysis (verified during this review):**

A grep for `Json\.(deserialize|serialize|fromString|toString)` across `src/` returns only **two** call sites:

- `src/Informedica.NKF.Lib/WebSiteParser.fs` — uses **FSharp.Data**'s `JsonValue.Load`, NOT the Newtonsoft wrapper. Irrelevant.
- `src/Informedica.GenUNITS.Lib/ValueUnit.fs` — internal serialization, no untrusted input.

Fable.Remoting uses its own binary serialization, so **the dangerous default is currently unreachable from the network**.

**Why this is still High, not Low:**

- It is a *latent footgun*: the next contributor who calls `Informedica.Utils.Lib.Json.deserialize` on a Fable.Remoting payload, a cached file, or an HTTP response immediately introduces an RCE.
- The default is invisible to grep — a contributor would have to know to check `Json.fs` itself.
- Newtonsoft.Json's documentation explicitly warns against `Auto` for this reason.

**Recommendation:**

1. Change the default to `TypeNameHandling.None`.
2. If a specific type-aware use case exists, opt into it locally with a `SerializationBinder` allow-list.
3. Add a unit test that asserts the default settings reject `$type`-bearing payloads.
4. Add a `// SECURITY: do NOT change to Auto` comment.

**References:** Microsoft Security Advisory CVE-2018-7164 family; OWASP A08:2021 (Software and Data Integrity Failures); Black Hat 2017 — "Friday the 13th: JSON Attacks".

---

### C2 — Json default is a foot-gun for future contributors

**Severity:** C1 Med · C2 Med · C3 Med

This is the "what comes next" of C1: even if no exploit exists today, the dangerous default will silently weaponize any future caller. Track and fix together with C1.

---

### C3 — Path-traversal handling is exemplary (positive control)

**Evidence** (`src/Informedica.GenPRES.Server/LogAnalyzer.fs:1047-1113`):

- Regex allow-list: `^genpres_[A-Za-z0-9_]+\.log$`
- Explicit rejection of `..`, `/`, `\`
- File size limit: 50 MB
- Uses `Path.Combine` correctly

This is the model the rest of the codebase should follow when accepting any filename or path argument.

---

### D1 — Markdown rendering

**Severity:** C1 Low · C2 Low · C3 Low *(downgraded from Medium after verification)*

**Evidence:** `src/Informedica.GenPRES.Client/Views/{Patient,Formulary,Parenteralia}.fs` use `Feliz.Markdown.Markdown.markdown`. Grep for `rehype-raw`, `remark-html`, `allowDangerousHtml`, `skipHtml`, `rehypeRaw` in the client returns **no matches**.

`Feliz.Markdown` wraps `react-markdown@10.1.0`, which by default sanitizes raw HTML out of rendered Markdown unless `rehype-raw` is explicitly added.

**Why this is still flagged at Low:** the safety relies on a transitive dependency's default behavior. If a future contributor adds `rehype-raw` to enable HTML rendering (a tempting feature), the codebase has no automated test to catch it.

**Recommendation:** Add a build-time grep test (e.g., a Husky pre-commit hook) that fails if `rehype-raw` or `allowDangerousHtml` appear in the client.

---

### D2 — External CDN assets without SRI

**Severity:** C1 Low · C2 Med · C3 Med

**Evidence** (`src/Informedica.GenPRES.Client/index.html:11-13`):

```html
<link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/font-awesome/4.6.3/css/font-awesome.min.css" />
<link rel="stylesheet" href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700">
<link rel="stylesheet" href="https://fonts.googleapis.com/icon?family=Material+Icons">
```

Three issues:

1. **Font Awesome 4.6.3** was released in 2016 and is end-of-life. No active maintenance, no security updates.
2. **No `integrity=` (SRI)** attribute on any of the three. A CDN compromise injects arbitrary CSS — and CSS can exfiltrate keystroke timings via attribute selectors.
3. **maxcdn.bootstrapcdn.com** is itself a third party with its own threat model.

**Recommendation:** Self-host Font Awesome (npm `@fortawesome/fontawesome-free` or upgrade to v6). Self-host or pin Roboto/Material Icons. Add SRI hashes if external hosting must remain.

---

### D3 — Token in Elmish memory (positive design)

`App.fs:49,175-176,414,573` — the auth token lives in the Elmish state tree as a plain string, in JS heap memory only. It is **not** persisted to `localStorage`/`sessionStorage`, and is reset to empty on logout (line 573). XSS would still steal it (mitigated by D5), but the design is correct: memory-only with explicit clearance. Keep.

---

### D4 — `ReloadResources` bypasses the token system

> ✅ **Partially resolved 2026-04-10.** Constant-time comparison via `CryptographicOperations.FixedTimeEquals` is now used in `src/Informedica.GenPRES.Server/ServerApi.Services.fs:347-373`, eliminating the timing-attack surface and the empty-string regression introduced by the new Dockerfile defaults. The deeper structural fix — migrate `ReloadResources` onto the HMAC token system — remains tracked by an inline `TODO(D4 follow-up)` comment.

**Severity:** C1 Med · C2 Med · C3 Med *(escalated from Low after verification)*

**Evidence** (`ServerApi.Services.fs:347-353`):

```fsharp
match cmd with
| Api.ReloadResources password when
    Env.getItem "GENPRES_PASSWORD"
    |> Option.map (fun expected -> password <> expected)
    |> Option.defaultValue true
    -> // no env var = always reject
    Error [| "Invalid password" |]
```

Two design problems:

1. **Inconsistent auth model.** `LogAnalyzerCmd` commands use the HMAC token (1-hour TTL, audit-friendly). `ReloadResources` uses the *raw password* directly. A client must either:
   - Hold and send the raw password (defeating the point of having a token), or
   - Maintain *two* parallel auth channels.
2. **String equality on the password** is theoretically timing-attackable. The leak is small over a network, but unnecessary — `FixedTimeEquals` already exists in the same project.

**One positive:** the *default reject* (`Option.defaultValue true`) when `GENPRES_PASSWORD` is unset is the secure default.

**Recommendation:**

1. Convert `ReloadResources` to take a token, not a password. Validate via the existing `validateToken` helper.
2. Until then, replace the `=` comparison with `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes.

---

### D5 — Clean client baseline (positive)

No `dangerouslySetInnerHTML`, no `eval`, no inline event handlers via string interpolation, no `window.open` without `noopener`. The Fable JSX patterns documented in `CLAUDE.md` are followed throughout.

---

### E1 — `.env` correctly gitignored (positive)

Verified: `.gitignore:2:*\t.env`; `git ls-files .env` is empty; `git check-ignore -v .env` confirms the rule. The local `.env` file is not tracked. Keep the opt-in `.gitignore` policy documented in `AGENTS.md`.

---

### E2 — Trivial dev password

> ✅ **Resolved 2026-04-10.** Production password policy documented in `.env.example` and `DEVELOPMENT.md` (Password policy section). Server-side `validateProductionPassword` in `src/Informedica.GenPRES.Server/Server.fs:60-94` runs before any HTTP listener is bound and refuses to start when `GENPRES_PROD=1` and `GENPRES_PASSWORD` is missing, empty, whitespace-only, or shorter than 16 characters. Demo mode is unaffected.

**Severity:** C1 Low · C2 **High** · C3 **High**

The local `.env` on the developer machine sets `GENPRES_PASSWORD=genpres`. The client login form pre-fills `username="genpres"`. This is fine for C1, but if the same value reaches a C2 deployment via a copy-pasted `.env`, A1 + A2 jointly become Critical.

**Recommendation:** Document in `DEVELOPMENT.md` and `.env.example`:

> The dev password is intentionally trivial. **Production deployments must set `GENPRES_PASSWORD` to a 32+ character random value generated by a CSPRNG**, e.g., `openssl rand -base64 32`. Never reuse a development password.

Add a startup check: if `GENPRES_PROD=1` and `GENPRES_PASSWORD` is shorter than 16 characters, refuse to start.

---

### E3 — Dockerfile leaks `GENPRES_URL_ID` into the image

> ✅ **Resolved 2026-04-10.** `ARG GENPRES_URL_ARG` / `ENV GENPRES_URL_ID=$GENPRES_URL_ARG` removed from `Dockerfile`. Replaced with empty `ENV GENPRES_URL_ID=` / `ENV GENPRES_PASSWORD=` defaults so the variables remain discoverable in container management UIs (Plesk, Portainer, Kubernetes manifests) while operators inject the real values at runtime. Startup banner additionally redacts `GENPRES_URL_ID` to its last-5 characters prefixed with `***`. **Sheet ID rotation completed 2026-04-10** (operator-confirmed) — any previously published image is now operating against a stale ID and the live workspace is protected.

**Severity:** C1 Med · C2 **High** · C3 **High**

**Evidence** (`Dockerfile:39-40`):

```dockerfile
ARG GENPRES_URL_ARG
ENV GENPRES_URL_ID=$GENPRES_URL_ARG
```

`ARG`s become layer metadata. `ENV`s are persisted in the image and visible via `docker inspect`. Anyone who pulls the image — including from Docker Hub if the image is published — can extract the proprietary Google Sheet ID.

**Impact:** The Sheet ID is the commercial asset that protects the proprietary medication ruleset. Leakage exposes the dataset.

**Recommendation:**

1. Do **not** bake `GENPRES_URL_ID` into the image. Inject it at runtime via `docker run -e GENPRES_URL_ID=...`, Kubernetes secret, or Docker secret.
2. Remove the `ARG/ENV` pair from the `Dockerfile` and update `DEVELOPMENT.md` to document the runtime injection pattern.
3. **Rotate the Sheet ID** if any image with the current value has been pushed to a registry.

---

### E4 — Beta/RC dependencies in production

**Severity:** C1 Low · C2 Med · C3 Med

**Evidence** (`paket.dependencies:7,12,14`):

```text
nuget Fable.Core 5.0.0-rc.1
nuget Fable.Elmish.React 5.5.0-beta-1
nuget Fable.Elmish.HMR 9.0.0-beta.1
```

Beta/RC versions carry no support guarantees, may contain unfixed bugs, and are not appropriate for an MDR-regulated production deployment.

**Recommendation:** Pin to released versions before any C2 rollout. Document the F#/Fable version policy in `DEVELOPMENT.md`.

---

### E5 — `ClosedXML = 0.97` is from 2020

> ✅ **Resolved 2026-04-11.** Removed entirely rather than upgraded.
> Reachability re-audit found **zero** call sites in `src/`, `tests/`,
> any `.fsproj`, or any `.fsx` script: no `open ClosedXML`, no
> `XLWorkbook`, no `IXLWorksheet`. The package's only historical
> caller was `Informedica.MetaVision.Lib/Data.fs` (which contained
> `open ClosedXML.Excel`), deleted in commit `49d26df`
> ("chore: remove old metavision related code"). The `paket` and FSI
> references survived that commit as dead refs and have now been
> removed: one line from `paket.dependencies`, one `#r` from
> `scripts/load-dependencies.fsx`, `paket.lock` regenerated via
> `dotnet paket install`. Build clean (`dotnet run build`: 0
> warnings, 0 errors). The original Reachability paragraph below
> incorrectly attributed usage to `Informedica.OTS.Lib`; OTS reads
> Google Sheets CSV via `Informedica.Utils.Lib.Web.GoogleSheets` and
> never used ClosedXML. The original assessment is preserved verbatim
> below for traceability.

**Severity:** C1 Med · C2 **High** · C3 **High** *(now n/a — package removed)*

**Evidence:** `paket.dependencies:39`. Current ClosedXML stable is 0.104+. The 2020-era 0.97 release predates several fixes for malformed XLSX handling.

**Reachability** *(superseded — see resolution marker above)*: ClosedXML is used wherever the project reads spreadsheets — primarily `Informedica.OTS.Lib` for resource loading. If any user-supplied XLSX is ever parsed (currently no — only Google Sheets CSV), the attack surface is significant.

**Recommendation** *(superseded)*:

1. Upgrade to current stable (`0.104+`) and re-run tests.
2. If staying on 0.97 is necessary, document why and run a CVE check against `ClosedXML 0.97`.

---

### E6 — Floating dependency ranges

**Severity:** C1 Low · C2 Med · C3 Med

**Evidence:** `paket.dependencies` has `Saturn ~> 0`, `FSharp.Data` (no version), `Fable.Browser.DOM` (no version), several others.

`paket.lock` *should* lock these, but no CI step verifies the lock matches dependencies. A `paket update` accidentally committed could shift major versions silently.

**Recommendation:** Add a CI step that runs `dotnet paket restore` and fails if `paket.lock` is stale. Pin floating ranges to `~>` with at least major.minor specified.

---

### E7 — Client npm dependencies floating

**Severity:** C1 Low · C2 Med · C3 Med

`package.json` uses `^9.0.0`, `^19.2.4` etc. `package-lock.json` is present (good) but CI doesn't run `npm ci` strictly (needs verification). The `react-markdown` transitive risk is mitigated by D1, but the general pattern allows silent transitive upgrades.

**Recommendation:** Use `npm ci` in CI; consider `--frozen-lockfile`-style enforcement.

---

### E8 — No automated secret scanning

**Severity:** C1 Low · C2 Med · C3 Med

`.husky/` runs Fantomas and markdown-lint, no `gitleaks`/`detect-secrets`. CI workflow has no secret-scan step.

**Recommendation:** Add `gitleaks` to the pre-commit hook chain and to CI. Block PRs that introduce secrets.

---

### E9 — CI Actions pinned to floating tags

**Severity:** C1 Info · C2 Low · C3 Low

`actions/checkout@v4.x`, `actions/setup-dotnet@v4.x` use floating tags. First-party Actions are low-risk, but the principle is correct: pin to commit SHAs in security-sensitive workflows.

---

### F1 — No tamper-evident audit trail

**Severity:** C1 Info · C2 **High** · C3 **High**

A grep across `src/` for `audit`, `tamper`, `integrity`, `signature` returns no application-level audit logging. The existing logging (request logs, computation traces) is operational, not auditable.

**MDR/21 CFR Part 11 requirements** the project itself acknowledges in its MDR documentation:

- Append-only record of all actions affecting clinical decisions
- User attribution (depends on A1 being fixed first)
- Tamper-evidence (e.g., hash chaining, log signing)
- Retention policy aligned with the device's intended life cycle

**Recommendation (sequence):**

1. First fix A1 (real user identity), otherwise the audit log can only attribute to "the password holder".
2. Introduce an `Informedica.Audit.Lib` module that writes append-only entries for: login, scenario selection/modification, resource reload, log access, configuration change.
3. Sign each entry or chain entries with a hash of the previous (`prev_hash || payload || HMAC`).
4. Document the audit log in the MDR risk analysis.

---

### F2 — No integrity check on cache files

**Severity:** C1 Med · C2 Med · C3 Med

`data/cache/` contains binary cache files derived from Google Sheets. There is no checksum, signature, or version field that the server verifies before trusting the file. An attacker (or accidental corruption) that modifies a cache file silently changes dose calculations.

**Patient-safety impact:** Direct. This is the most clinically dangerous finding in the report, even at Medium severity.

**Recommendation:**

1. Compute and store a SHA-256 of each cache file at write time.
2. Verify on load; refuse to start if a cache file fails verification.
3. Long term: sign cache files with an asymmetric key held by the build process.

---

### F3 — `GENPRES_DEBUG` may log PHI

**Severity:** C1 Low · C2 Med · C3 Med

`Informedica.Logging.Lib` is generic and provides a logging API, but does not itself touch patient fields. PHI exposure in logs depends on what callers interpolate into log messages — a sample of `GenORDER.Lib` and `GenForm.Lib` is needed. This review did not exhaustively audit every log call site.

**Recommendation:**

1. Establish a logging policy: never interpolate `Patient.Name`, `BSN`, `DateOfBirth`, exact weight/age into log messages without redaction.
2. Add a `Patient.toLogString` helper that emits only an opaque ID + age band + weight band.
3. Add a CI grep that flags new log statements interpolating `pat.Name` etc.

---

### F4, F5 — Positive controls

- **F4**: `Server.fs:43-46` masks `GENPRES_PASSWORD` as `***` in startup banner.
- **F5**: `logClientIP` middleware (`Server.fs:78-97`) records client IPs on every request — useful for incident response.

---

### G1 — Google Sheets URL not validated

**Severity:** C1 Low · C2 Low · C3 Low

`src/Informedica.Utils.Lib/Web.fs:19-29` interpolates `dataUrlId` into a Google Sheets URL without validation. Currently safe because `dataUrlId` is sourced *only* from `GENPRES_URL_ID` at server startup (`Server.fs:73-75`), which is operator-controlled. A future caller that passes user input would introduce SSRF.

**Recommendation:** Validate `dataUrlId` against a `^[A-Za-z0-9_-]{20,60}$` pattern in the helper itself, so the helper is safe by default.

---

### G2 — Dead production code

**Severity:** C1 Info · C2 Info · C3 Info

`Informedica.HIXConnect.Lib` is referenced in `Informedica.GenPRES.Server.fsproj:30` but no `.fs` file in the server imports its namespace. `Informedica.FHIR.Lib`, `Informedica.MetaVision.Lib`, `Informedica.MCP.Lib`, `Informedica.OTS.Lib` are similarly unused at runtime.

**Recommendation:** Remove unused project references to reduce the attack surface and make dependency upgrades easier. Confirm with the maintainer first — these may be reserved for in-flight work.

---

## 5. Positive controls (already in place)

| ID | Control |
|---|---|
| A4 | HMAC-SHA256 + `FixedTimeEquals` + 1 h TTL on tokens |
| C3 | LogAnalyzer path-traversal handling: regex allow-list, explicit `..` rejection, 50 MB size cap |
| D3 | Auth token in-memory only, cleared on logout, never persisted to localStorage |
| D5 | No `dangerouslySetInnerHTML`, no `eval`, no raw HTML interpolation in client |
| E1 | `.env` correctly gitignored and not tracked |
| F4 | `GENPRES_PASSWORD` masked as `***` in startup banner |
| F5 | Client IPs logged on every request |
| (general) | F# type system rules out many memory-safety bugs by construction |
| (general) | The opt-in `.gitignore` strategy reduces accidental commits of cache/secret files |
| (general) | Pre-commit hook chain (Husky + Fantomas + markdown-lint) is in place — extending it for security checks is straightforward |

---

## 6. MDR / 21 CFR Part 11 mapping

| Requirement | Reference | Status |
|---|---|---|
| User-attributable actions | EU MDR Annex I §17.2; 21 CFR Part 11.10(d) | **Not met** (A1, F1) |
| Audit trail of clinical actions | 21 CFR Part 11.10(e) | **Not met** (F1) |
| Integrity of records | 21 CFR Part 11.10(c) | **Not met** for cache files (F2); partially met for code (git signatures) |
| Authentication of system access | 21 CFR Part 11.200 | **Partial** — exists for admin only, not for clinical users (A1, A5) |
| Software lifecycle process | EU MDR Annex I §17.4 | Documented in `AGENTS.md`/`DEVELOPMENT.md`; script-only policy is a strong control |
| Risk analysis documented | EU MDR Annex I §3 | Partially documented in the MDR risk analysis (outside this repository); this review is an input |
| Validation of off-the-shelf software | EU MDR Annex I §17.4 | **Gap** — beta/RC dependencies (E4) and outdated `ClosedXML` (E5) need explicit acceptance |

---

## 7. Remediation roadmap

### 7.1 Do now (blocks any deployment beyond C1)

> **Status (2026-04-10):** all four items resolved. See the
> [post-implementation update](#update--2026-04-10-post-implementation-of-71)
> at the top of this document for the full status table, file:line refs,
> and verification results.

1. ✅ **C1** — Change `Json.fs` default to `TypeNameHandling.None`. Add a regression test. *(Done. Three Expecto tests in `JsonSecurity` sub-module.)*
2. ✅ **E3** — Remove `ARG GENPRES_URL_ARG`/`ENV GENPRES_URL_ID` from the Dockerfile. Inject at runtime. Rotate the Sheet ID if any image has been pushed. *(Done — except the Sheet-ID rotation, which requires operator action against the Google Workspace.)*
3. ✅ **E2** — Document the production password policy in `DEVELOPMENT.md` and `.env.example`. Add a startup check that refuses weak passwords when `GENPRES_PROD=1`. *(Done. `validateProductionPassword` runs before any HTTP listener binds.)*
4. ⚠️ **D4** — Replace the `=` password comparison in `ServerApi.Services.fs:347-353` with `FixedTimeEquals`. Plan the migration of `ReloadResources` to the token system. *(Constant-time comparison done. Token migration tracked by inline `TODO(D4 follow-up)` comment.)*

### 7.2 Before any C2 (on-prem) rollout

1. **A1, A5** — Place the server behind a TLS-terminating reverse proxy that enforces per-user authentication (OIDC against the hospital IdP is the lowest-friction path). Document the deployment topology in the MDR documentation.
2. **B1, B2** — HTTPS at the edge plus the security header baseline from §4 B2.
3. **A2** — Per-IP rate limiting on `ValidatePassword` (and ideally on every RPC).
4. **F1** — Implement `Informedica.Audit.Lib` for tamper-evident audit logging. Depends on §7.2 item 1 (A1) for user identity.
5. **F2** — SHA-256 integrity check on cache files at load time.
6. ~~**E5** — Upgrade `ClosedXML` to current stable.~~ *(Resolved 2026-04-11 by removing the package entirely — see E5 in §4.)*
7. **E4** — Replace beta/RC Fable dependencies with released versions.
8. **F3** — Audit log call sites for PHI; introduce `Patient.toLogString` redaction helper.
9. **B3** — `ForwardedHeadersMiddleware` with `KnownProxies` allow-list.
10. **E8** — Add `gitleaks` to pre-commit and CI.

### 7.3 Before any C3 (SaaS) rollout

1. **A1** — Replace shared password with full IdP integration and per-user RBAC.
2. **A3** — Switch to revocable opaque tokens or JWT with `jti` denylist.
3. **B4** — Kestrel limits, request size caps, per-IP/per-user concurrency caps.
4. **D2** — Self-host CDN assets, add SRI to anything that must remain external.
5. **F1** — Audit log retention and tamper-evidence (hash chaining + offsite shipping).
6. **G1** — Validate `dataUrlId` format in `Web.fs` defensively.

### 7.4 Lower priority / hygiene

- **E6, E7, E9** — Dependency pinning policy and Action SHA pinning.
- **G2** — Remove unused project references.
- **B5** — Replace the framework-leaking root response with a generic 404 or health check.
- **D1** — Add a build-time grep test that fails if `rehype-raw` appears in client code.

---

## Appendix A — Verification commands

### A.1 Reproduce each finding

```bash
# A1 / A5 — see all RPC handlers
rg -n 'validateToken|validatePassword|GENPRES_PASSWORD' src/Informedica.GenPRES.Server/

# B1 — server binds plain HTTP
rg -n 'url\b' src/Informedica.GenPRES.Server/Server.fs

# C1 — Newtonsoft TypeNameHandling.Auto
rg -n 'TypeNameHandling' src/Informedica.Utils.Lib/Json.fs

# C1 reachability
rg -n 'Json\.(deserialize|serialize|fromString|toString)' src/

# D1 — markdown rendering call sites
rg -n 'Markdown\.markdown|Feliz\.Markdown' src/Informedica.GenPRES.Client/

# D1 — confirm no rehype-raw plugin
rg -n 'rehype-raw|remark-html|allowDangerousHtml|skipHtml' src/Informedica.GenPRES.Client/

# D2 — external CDN assets
sed -n '11,13p' src/Informedica.GenPRES.Client/index.html

# D4 — ReloadResources auth check
rg -n 'ReloadResources' src/Informedica.GenPRES.Server/

# E1 — confirm .env is gitignored and not tracked
git ls-files .env
git check-ignore -v .env

# E3 — Docker arg/env leak
sed -n '36,44p' Dockerfile

# E4, E5, E6 — dependency review
cat paket.dependencies

# F1 — audit grep
rg -n -i 'audit|tamper|integrity' src/

# G2 — confirm unused integrations
rg -n 'HIXConnect|FHIR|MetaVision|ModelContextProtocol' src/Informedica.GenPRES.Server/ --type fsharp
```

### A.2 Suggested dynamic verification (out of scope for this static review)

- `nmap -p 8085 --script http-headers <server>` to confirm B2
- `curl -k -v https://<server>:8085/` to confirm B1
- A burp scan of unauthenticated RPCs to enumerate A5
- A `gitleaks detect` run across the full git history

---

## Appendix B — Files inspected

| File | Lines reviewed | Notes |
|---|---|---|
| `src/Informedica.GenPRES.Server/Server.fs` | 1-160 | A5, B1-B5, F4, F5 |
| `src/Informedica.GenPRES.Server/ServerApi.Command.fs` | 1-201 | A1-A5 |
| `src/Informedica.GenPRES.Server/ServerApi.Services.fs` | 250-370 | D4, A5 |
| `src/Informedica.GenPRES.Server/LogAnalyzer.fs` | 1047-1113 | C3 (positive) |
| `src/Informedica.Utils.Lib/Json.fs` | 38-46 | C1 |
| `src/Informedica.Utils.Lib/Web.fs` | 19-29 | G1 |
| `src/Informedica.GenPRES.Shared/Api.fs` | full | RPC enumeration |
| `src/Informedica.GenPRES.Client/App.fs` | 49, 175-176, 414, 573, 645-646 | D3, D4 |
| `src/Informedica.GenPRES.Client/Components/TitleBar.fs` | login form | A1 |
| `src/Informedica.GenPRES.Client/Views/{Patient,Formulary,Parenteralia}.fs` | markdown call sites | D1 |
| `src/Informedica.GenPRES.Client/index.html` | full | D2 |
| `src/Informedica.GenPRES.Client/vite.config.js` | full | B1 (dev proxy), D-context |
| `src/Informedica.GenPRES.Client/package.json` + `package-lock.json` | dep entries | E7 |
| `paket.dependencies` | full | E4-E6 |
| `Dockerfile` | full | E3 |
| `.github/workflows/build.yml` | full | E8, E9 |
| `.husky/` | scripts | E8 |
| `.gitignore`, `.env.example` | full | E1 |

Files **not** read but project references confirmed: `Informedica.HIXConnect.Lib`, `Informedica.FHIR.Lib`, `Informedica.MetaVision.Lib`, `Informedica.MCP.Lib`, `Informedica.OTS.Lib`, `Informedica.Logging.Lib` (only the namespace).

---

## Appendix C — Threat model assumptions

- C1 assumes a single-developer machine, loopback only. A trusted operator runs the server. Other users on the same machine are out of scope.
- C2 assumes a single-tenant deployment behind a hospital firewall. The hospital IdP exists and is OIDC-capable. Network ACLs are managed by hospital IT. Patient data is real PHI.
- C3 assumes a multi-tenant deployment on the public internet. Tenants are mutually untrusted. The threat model includes credential stuffing, automated scanning, supply-chain attacks, and DDoS.
- All three contexts assume the proprietary Google Sheet ID is a commercial asset whose disclosure has business impact.
- All three contexts assume the medication cache files are clinically load-bearing — corruption affects patient outcomes, regardless of CIA framing.
- This review does not assume any specific hospital's policies; it uses EU MDR Annex I and 21 CFR Part 11 as the baseline.

---

## Appendix D — Out of scope (explicit)

- Penetration testing and dynamic exploitation
- Cryptographic correctness of `GenSOLVER` math
- Performance/load characterization
- GDPR consent flows, data subject access, retention policies
- Physical and operational security of any deployment
- Third-party services (Google, GitHub, Docker Hub, npm registry)
- Mobile clients (none exist)
- Backup and disaster recovery (touched only via F2)

---

*End of report.*
