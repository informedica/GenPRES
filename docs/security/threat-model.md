# Threat Model (STRIDE)

> Moved out of issue [#413](https://github.com/informedica/GenPRES/issues/413) (opened 2026-07-14) on 2026-09-22, so that the threats live in a document with tables and a history, next to the [security review](2026-04-10-security-review.md) and the [baseline in force](security-baseline.md). This is a living document: edit it in a pull request whenever a threat or a mitigation comes to mind, and date the entry.

**Status**: Living document, last revised 2026-09-22

**References**:

- [Security Review 2026-04-10](2026-04-10-security-review.md), § 2 for the assets, actors and trust boundaries of the system as deployed
- [Security Baseline](security-baseline.md), the posture in force and its remediation status
- [EHR integration model](../scenarios/integration/README.md), the launched-and-signed workflow: the design in `GenPRES-MainEHR-Integration-V8.md`, the executable model `Integration.fsx`, and the use cases uc-01 to uc-11
- [Simulating the launch sequence](../../DEVELOPMENT.md#simulating-the-launch-sequence), what of that workflow runs today against server-side stubs

## Why a threat model

A code review can unearth some security issues, and penetration testing other kinds. Both are incomplete, because they fail to surface the security trade-offs a system has to make on purpose.

A code review may "reveal" that an application can be viewed by anyone on the internet, only for the product owner to confirm that this is by design, being the system's entire reason to exist. Other systems mix publicly visible data with data that must be protected from unauthorised access. Being explicit about such distinctions helps developers make good security decisions.

The [STRIDE](https://en.wikipedia.org/wiki/STRIDE_model) model is an easy yet effective way of capturing and discussing security threats. STRIDE stands for:

- Spoofing
- Tampering
- Repudiation
- Information disclosure
- Denial of service
- Elevation of privilege

For each category, ask: is the system threatened in this category? Why, or why not? How likely is a breach? What is its impact? What mitigations are possible? What do we decide to do about it?

### Trade-offs

As the saying goes, the only secure system is one that does not exist. The next most secure system is one that is turned off.

No practicable working system is secure. The purpose of threat modelling is not to make a system "secure", since that is impossible. The purpose is to identify threats and make informed decisions about them.

GenPRES states its own trade-off in the integration model: clinical decision support for anyone, order management only through a launch from the hospital's EHR (uc-07). The public demo at `https://genpres.nl/` exists for visibility, and gating it would defeat its purpose; that decision is recorded at finding A5 of the security review and in the baseline.

## How to read the tables

A threat can fit more than one category, and one threat can lead to another: an information disclosure that leaks credentials becomes a spoofing threat.

Every table has the same columns:

| Column | Meaning |
|---|---|
| Threat | The threat, stated as what an actor could do |
| Today | Where the system as deployed stands, dated |
| Mitigation | One of three states. **In force in every mode**: built and active whatever `GENPRES_PROD` says. **Built, demo only**: built against the server-side stand-ins, mounted only when `GENPRES_PROD=0`; protects nothing in production. **Designed, not built**: the integration design decides it, no code does it yet |
| Likelihood, Impact | Not rated. Nothing in the repository rates a threat for likelihood or impact; every cell says so until someone rates it, and that is the next step for this document |
| Tracking | The issue, document or decision that carries the item |

The integration design describes the launched-and-signed workflow. Since September 2026 it is built against server-side stand-ins for every party outside GenPRES (the LaunchScript, the IdentityProvider, the UserRegistry, the PatientDataPlatform, the MailService), mounted only when `GENPRES_PROD=0`. A production server answers 404 on their routes and refuses every launch, Session, signature and enrolment until the scope switch (#580) decides what production exposes, so none of it protects anything in production today. Parts of the design are not built in any mode: the signed request of step 7 (the browser's key pair proving every request), the idle and absolute lifetimes of a Session, the erasure of the launch token from the browser history, and reading the audit back; uc-01 lists them under "Designed, not built". The direct open of the client, the anonymous mode with demographics in the URL and no identity, exists beside the launched workflow in every mode. The rule, concept, guarantee and open-question numbers cited below are the integration document's own.

## Spoofing

Threats where an actor pretends to be someone they are not.

| Threat | Today (2026-09-22) | Mitigation | Likelihood | Impact | Tracking |
|---|---|---|---|---|---|
| A stolen Launch is presented by someone else | The Session's User comes only from the IdentityProvider, never from the Launch (Rule 4); a stolen Launch yields at most the thief's own Session under their own name (Guarantee 5, uc-01 ext 3b, adversarial test 2 of the model) | Built, demo only | not rated | not rated | uc-01 |
| A Client claims a Role it does not have | The Role comes from the UserRegistry at launch and is taken again at every signature (Rules 5, 38); the PIN is verified only by the Server (Rule 23) | Built, demo only | not rated | not rated | uc-03 |
| Someone takes over an open workstation | They can look and explore but attest nothing without the PIN (uc-05) | Built, demo only | not rated | not rated | uc-05 |
| The person at the signature is not the person at the launch | Guarantee 4 says plainly that a signature does not prove this | Designed, not built; depends on step-up signing through the hospital sign-on (Open Question 3) | not rated | not rated | Integration design, Open Question 3 |

## Tampering

Threats where data is changed in detrimental ways: ransomware, vandalism, or someone giving themselves an advantage.

| Threat | Today (2026-09-22) | Mitigation | Likelihood | Impact | Tracking |
|---|---|---|---|---|---|
| Patient data changed in the browser's address bar | The direct `#patient?by=...` route (`App.fs`, the url-parameter parser) is the anonymous mode: it carries an age, a weight and the other values a dose is computed from, and no identity. Nothing is stored, signed or attributed on that route, so someone editing the address bar changes only the input of their own calculation, as they could in the patient panel. Assessed 2026-09-22: not a tampering threat | Built, demo only: on the launched path the PatientId is sealed in the Launch, confirmed against the UserRegistry (Concept 3, Rule 6), and the Patient of every request is taken from the SessionRecord, never from the payload (Rule 33, Guarantee 1) | not rated | not rated | #408 (closed) documents the parameter set; #580 decides what production exposes |
| Edited, replayed or wrong-purpose tokens | Tokens are MAC'd, key-separated per purpose, canonical and spent once (Concept 17, Rules 34, 43, 45); the commit re-verifies everything in one transaction (Rule 42); the model's "stateless design under attack" section exercises edited tokens, wrong-purpose tokens, wrong-Patient payloads and forged stamps | Built, demo only. The signed request of step 7, which would bind every request to the browser's key pair, is designed, not built | not rated | not rated | uc-03 |
| Attribution stamps forged by the Client | Never accepted from the Client (Rule 35); both stores are append-only (Actor 5) | Built, demo only, on the SQLite session store | not rated | not rated | uc-03, session store |
| The knowledge base (the Google Sheet) edited | Editing the sheet changes a running system after a resource reload; Concept 18 says a KnowledgeRuleSet is "published" without saying by whom | None in any mode; not covered by the security review or the integration design | not rated | not rated | Unfiled |
| The store's administrator changes rows | Nothing outside GenPRES would notice (Open Question 5) | None | not rated | not rated | Integration design, Open Question 5 |

## Repudiation

Threats where a user may deny having performed an action and the system cannot prove otherwise.

| Threat | Today (2026-09-22) | Mitigation | Likelihood | Impact | Tracking |
|---|---|---|---|---|---|
| A Prescriber denies having signed an order plan version | Guarantee 4 declines to claim non-repudiation and states what would earn it: a fresh hospital sign-on over the SigningChallenge digest, kept with the plan (Open Question 3). The audit (Rule 46) records every launch, Session opening and ending, Submission committed or refused, failed PIN entry and PIN change, in the same transaction as the act | Audit built, demo only, on the SQLite store: the in-memory store audits nothing and nothing reads the table back yet (#516). Non-repudiation designed, not built | not rated | not rated | Integration design, Open Question 3; ADR-0007 |
| A User denies having received a confirmation code | Every mail is audited with the address it went to (Rule 27) | Built, demo only, with the stub MailService | not rated | not rated | uc-02, uc-06 |
| The audit itself is disputed | The audit has GenPRES's own schema, is not HL7 AuditEvent, and is not tamper-resistant against the store's administrator (Open Question 5); reading the audit is out of scope | Open | not rated | not rated | Integration design, Open Question 5 |
| Prescriptions sent out of the system | GenPRES does not send prescriptions anywhere yet. When it does, repudiation becomes real | Not applicable yet | not rated | not rated | Unfiled |

## Information disclosure

Threats where information is visible to actors who should not have it: cleartext transport, a readable database, a talkative error.

| Threat | Today (2026-09-22) | Mitigation | Likelihood | Impact | Tracking |
|---|---|---|---|---|---|
| Error messages carry internal details | The server registers no Fable.Remoting error handler, so an exception's message reaches the client, and the client shows it under "Server fout" (`App.fs`). The text seen in #418, `Error: getDataResult Exception: cannot find column Form in ...`, names the platform, a function and the sheet's columns; enough for an attacker to focus on | None. Rule 11 says a refused Client is told "the reason" without bounding how much reason | not rated | not rated | Unfiled; #418 (closed) is the bug, not the disclosure |
| The Launch token in the browser history | The client erases the token from the URL and the session history, but the browser records the visit before any script runs, and the omnibox suggests it | Designed, not built: keep the token out of a top-level GET, the LaunchScript posts the Launch (decision D1 of plan 409) | not rated | not rated | #599 |
| The SessionId leaks | Lives only in an HttpOnly, Secure, SameSite=Strict cookie and never in a URL (Rule 12); the Client is served so nothing is cached or carried in a referrer and no third-party script runs (Rule 39, Consequence 4) | The cookie: built, demo only. The security headers and the absence of third-party script: in force in every mode (security review B2) | not rated | not rated | uc-01 |
| The private store is copied out | Sessions, credentials, tokens and the audit are never copied to the PatientDataPlatform (Actor 5, Guarantee 4); the PIN never leaves GenPRES (Rule 23) | Built, demo only | not rated | not rated | Session store |
| A refusal tells the attacker why | Forged, expired and spent Launches get one indistinguishable refusal; a Session-ended notice is delivered only at a fresh launch of the same User, so whoever sits down next learns nothing; two Users on one Patient never see each other's work (Guarantee 3) | Built, demo only | not rated | not rated | uc-01, uc-04, uc-08 |
| Patient data in the log files | The order log holds age, weight and the order; the request log holds the client IP in full. Both are a deliberate choice, documented with the retention argument | Accepted; handle `data/logs` as patient data, see DEVELOPMENT.md, the log files | not rated | not rated | Security review F-findings |
| Cleartext transport | The server binds plain HTTP; the demo terminates TLS at the reverse proxy | Deployment concern, out of the integration design's scope; the demo terminates TLS at the reverse proxy | not rated | not rated | Security review B1 |

## Denial of service

Threats that overwhelm or crash the system, or lock a legitimate user out of it.

| Threat | Today (2026-09-22) | Mitigation | Likelihood | Impact | Tracking |
|---|---|---|---|---|---|
| Flooding the server with anonymous Sessions | Anonymous Sessions are rate-limited, capped and given an absolute lifetime; opens past the cap are refused without writing a record (Rule 14); Sessions idle out and have an absolute lifetime (Rules 10, 30, 41) | A global limit per client IP of 60 requests per 10 seconds is in force in every mode (`Server.fs`, the rate limiter). The cap on anonymous Sessions and the idle and absolute lifetimes (Rules 10, 14) are designed, not built | not rated | not rated | Security review B4 |
| Locking a Prescriber out of signing by guessing PINs at their open workstation | Three wrong PINs end the Session and lock the credential; every wrong entry past the third doubles the delay, up to a day. uc-05 ext 3a calls the cost landing on the Prescriber deliberate | Built, demo only; accepted trade-off, the lock decays and a reset lifts it (Rule 28) | not rated | not rated | uc-05 |
| Blocking a User's PIN reset with a hostile reset request | One outstanding confirmation code per credential, so a hostile request blocks the owner's own reset until the code voids after fifteen minutes (Rule 37, uc-06 ext 1a) | Built, demo only; accepted trade-off | not rated | not rated | uc-06 |
| Mail flooding through repeated reset requests | Not discussed in the design | None | not rated | not rated | Unfiled |
| Resource exhaustion through the request payload | The whole WorkPlan travels with every request (Open Question 2); no request size limit | None | not rated | not rated | Security review B4; Integration design, Open Question 2 |
| Distributed denial of service | Every web-facing system is vulnerable to a degree | Deployment concern | not rated | not rated | Security review B4 |

## Elevation of privilege

Threats where a user attains powers they should not have.

| Threat | Today (2026-09-22) | Mitigation | Likelihood | Impact | Tracking |
|---|---|---|---|---|---|
| A Client grants itself a Role | The Role is never taken from the Launch or the Client (Rules 5, 38); a Reader cannot sign (Rule 26); an anonymous Session can commit nothing (Rules 13, 14) | Built, demo only | not rated | not rated | uc-03, uc-09 |
| A User reaches a Patient that is not theirs | Patient scope is bound to what the User has active in MainEHR (Rule 6); MainEHR decides who may press the button (Rule 1) | Built, demo only | not rated | not rated | uc-01 |
| Roles finer than Prescriber and Reader | Nothing finer exists (Open Question 4) | Open | not rated | not rated | Integration design, Open Question 4 |
| The admin path | `GENPRES_PASSWORD` gates the settings page, log analysis and resource reload with a shared password and a short-lived HMAC token; outside the integration design entirely | In force in every mode: the password policy at start-up, the constant-time comparison, the short-lived token; see the baseline | not rated | not rated | Security review A1, A2, A3 |

## What this model does not cover

- Likelihood and impact ratings. The columns exist so that they can be filled; no threat is rated today.
- The regulatory risk analysis. This document is a security threat model, not the MDR risk file, which is maintained separately.
- The client as a trusted party. It runs on a machine the device does not control and is outside the demilitarized zone (ADR-0001, the dependency rule); every threat above assumes the client can be hostile.

## History

| Date | Change |
|---|---|
| 2026-07-14 | Opened as issue #413 with the STRIDE structure and two seeded threats: patient data in URL query parameters, and error messages that carry internal details |
| 2026-09-06 | The integration design mapped onto the six categories in a comment on #413, with what it enforces and what it leaves open per category |
| 2026-09-22 | Moved here; the launched workflow's controls recorded as built, demo only, with the production refusal and the not-built parts named; the two seeded threats re-checked against the client: the URL parameters are the anonymous mode and carry no identity, so that one is assessed as not a threat; the error-message disclosure is still open; #599 added |
