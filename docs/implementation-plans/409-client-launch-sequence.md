# Implementation plan for issue 409: client side of the launch sequence

Scope: the GenPRES Client half of UC-1 (launch), with every server-side action stubbed behind the
final API contract. The plan was picked up in PR #574, which already scaffolded this flow, to
correct that PR to the shape below; later steps followed as PRs on top of it. Everything below the
problem description describes the plan as written; the section [As built](#as-built) at the end
records what landed, PR by PR, and where it deviates and why.

The sequence itself is [the launch sequence](../scenarios/integration/uc-01-launch.md). Step
numbers below refer to that page.

## Problem description

MainEHR opens GenPRES with a sealed Launch in the URL (UC-1). The client must present it, get a
Session back, and from then on carry only the session cookie and the OpenedToken. Today the client
has no notion of a session: any URL is parsed for patient parameters only, unparseable URLs are
logged raw (which would leak a Launch to the log), and the vendored router never fires
`UrlChanged`.

The V8 design (`docs/scenarios/integration/GenPRES-MainEHR-Integration-V8.md`) fixes the client
obligations:

- Rule 39: erase the Launch from URL and history at first presentation; keep it only in memory for
  retries within its lifetime (Rule 29: a page load, the IdentityProvider round trip, a retry or
  two).
- Rule 12: the SessionId is a bearer credential, never in a URL, never readable by script: an
  HttpOnly, Secure, SameSite=Strict cookie.
- Rule 7 and UC-1 extensions 4a, 5a, 5b: on refusal nothing opens; the client asks for a relaunch,
  or on "no Role" offers a fresh anonymous open that carries nothing over.
- Concept 17 and Rule 34: the OpenedToken comes back with the session and is returned with every
  request. This plan only stores it; it travels inside the signed proof of step 7, a later step.
- Launch step 3: the client generates a key pair at the launch. The public key correlates retries
  now and signs every request later (step 7).

Erasing the launch from the URL and the history is a requirement of this plan (Rule 39 above).
Separately, the raw-URL logging on parse failure is called out in
`docs/roadmap/feature-ehr-url-parameters.md` ("Additional context"); dropping the raw URL from
the warnings is the one part of that logging gap this plan closes on its own, and the two changes
travel together in step 1 only because both concern what a launch URL must never reveal.
Decision D1 is still open; this plan is D1-neutral on the client because the Launch is opaque to
it.

PR #574 diverges from V8 on three points: a redeem token in the URL, the SessionId in the response
body, and a launch that is never erased. Its `FelizRouter` fix and the split of the URL parser are
kept; the redeem hop is replaced by the contract below.

## Approaches considered

1. Keep PR #574's redeem-token flow and validate the redeem token later. Rejected: the redeem
   hop has no counterpart in V8 and puts a second secret in the URL.
2. Client holds the session credential in memory and sends it in every command payload, like the
   admin `AuthToken`. Rejected by Rule 12: a script-readable bearer.
3. Cookie-based session set by the server; the client keeps only a `SessionOpened` snapshot and
   the OpenedToken. Chosen.
4. To correlate a retry with its first presentation: a random per-page-load key held in memory,
   or the public key of a browser key pair. Chosen: the key pair. One mechanism serves both the
   retry now and the signed request of step 7 later; a random key would be thrown away when the
   signed request lands.

For the stub there were two options: an in-memory single session without a cookie, or a real
cookie via `Remoting.fromContext`. Chosen: the cookie now, so the client is final and the server
stub is replaced later behind the same contract.

## Chosen approach

This plan implements the client side of steps 2 to 6 of the launch sequence and a server stub
for steps 4 and 5. Step 7, the signed request, is out of scope; the stub only stores the public
key.

| Step | Client work in this plan | Server stub |
|------|--------------------------|-------------|
| 2 Erase | `parseLaunch`, erase from URL and history, drop the raw URL from the logs | |
| 3 Key pair | `Keys.fs`: generate, store the private key in IndexedDB, export the public key | |
| 4 Identity | `presentLaunch (Launch, PublicKey)`, retries, the `refused` return | never redirects; answers from the Launch text |
| 5 Verify and open | | refusal mapping, spent-mark per Launch and public key, cookie |
| 6 Session open | `Session.fs` state machine, `UpdatePatient`, title bar, session gate | `getSession`, `closeSession` |

Any refusal ends the same: no Session, the client shows the reason (Rule 7).

### Shared contract

Added to `src/Informedica.GenPRES.Shared/Types.fs` and `Api.fs` (drafted in a script, migrated by
the maintainer):

```fsharp
type Launch = Launch of string                      // opaque, sealed by MainEHR LaunchScript
type PublicKey = PublicKey of string                 // public JWK of the browser key pair, step 3
type OpenedToken = OpenedToken of string
type UserRole = Prescriber | Reader
type UserContext = { UserId: string; DisplayName: string; Role: UserRole }
type PatientContext = { PatientId: string; Patient: Patient }

type SessionOpened =                                 // no SessionId: it lives in the cookie
    {
        User: UserContext option                     // None = anonymous session (Rule 14)
        PatientContext: PatientContext option        // None = launch without active patient (ext 1a)
        OpenedToken: OpenedToken option
        KeyThumbprint: string option                 // which private key signs for this Session, step 7
    }

type LaunchRefusal =
    | LaunchExpired | LaunchSpent | LaunchInvalid    // ext 4a: ask for a relaunch
    | NoBrowserIdentity                              // ext 3c: retry, then relaunch
    | NoRole                                         // ext 5a: offer anonymous open
    | WrongActivePatient                             // ext 5b: relaunch after fixing MainEHR
    | EnrolmentRequired                              // UC-2, shown as text only for now

type LaunchOutcome =
    | Opened of SessionOpened
    | RedirectTo of url: string                      // step 4.2 as a payload, see below; the stub never returns it
    | Refused of LaunchRefusal

// IServerApi additions
presentLaunch: Launch * PublicKey -> Async<LaunchOutcome>   // idempotent per public key, Rule 2
getSession: unit -> Async<SessionOpened option>     // cookie-authenticated
closeSession: unit -> Async<unit>                   // Rule 10: explicit close
```

### Client session state machine

New file `src/Informedica.GenPRES.Client/Session.fs`, pure F# without React:

```fsharp
[<RequireQualifiedAccess>]
type Session =
    | Anonymous
    | Launching of Launch * PublicKey * attempt: int
    | Resuming                                       // getSession in flight (reload, IdP return)
    | Open of SessionOpened
    | Refused of LaunchRefusal * retry: (Launch * PublicKey) option
    | Unreachable of Launch * PublicKey * attempts: int   // ext 3a: server down after page served

type SessionMsg =
    | Present of Launch * PublicKey                  // key pair made by Keys.fs, once per page load
    | Outcome of Launch * PublicKey * Result<LaunchOutcome, string>   // Error = transport failure
    | Retry                                          // from Unreachable, or Refused with a retry
    | Resume
    | Resumed of Result<SessionOpened option, string>
    | RefusedAtCallback of LaunchRefusal             // from #/session?refused={reason}, step 4.5
    | OpenAnonymous
    | Close
    | Closed

type SessionEffect =
    | CallPresentLaunch of Launch * PublicKey
    | CallGetSession
    | CallCloseSession
    | GoTo of url: string                            // window.location.assign, for RedirectTo
    | SetPatient of Patient option                   // interpreted as UpdatePatient
    | KeepKey of thumbprint: string                  // Keys.keep: prune the other private keys

val transition: SessionMsg -> Session -> Session * SessionEffect list
```

Rules encoded in `transition`:

- `Outcome (l, k, _)` is ignored unless the state is `Launching (l, k, _)` for the same Launch
  and the same public key. `Resumed` is ignored unless the state is `Resuming`. This is the
  stale-request guard: a response can only land on the presentation that sent it.
- `Present (l, _)` while the state is `Launching (l, _, _)` is a no-op: an in-flight
  presentation is never replaced by a second one for the same Launch. In every other state,
  `Unreachable` and `Refused` included, `Present` starts a fresh presentation. A `Present` for a
  different Launch supersedes the current one, and the old one's outcome is then dropped by the
  guard above.
- A transport error while `Launching (l, k, n)` with `n < 3` yields `Launching (l, k, n + 1)`
  plus `CallPresentLaunch (l, k)`; at three attempts it becomes `Unreachable (l, k, 3)` and the
  UI offers Retry.
- `Retry` from `Unreachable (l, k, _)` or `Refused (_, Some (l, k))` yields `Launching (l, k, 1)`
  plus `CallPresentLaunch (l, k)`. `Retry` in any other state is a no-op. A retry always carries
  the same Launch and the same public key: the server answers a repeated key as it answered the
  first time (Rule 2, see the server stub), so a response lost on the wire cannot turn a session
  that did open into `LaunchSpent`. Another browser has another key and is refused. The Launch
  exists only in this page load's memory (Rule 39); a reload has nothing left to present.
- `Opened s` yields `Open s` plus `SetPatient (s.PatientContext |> Option.map _.Patient)`, and
  `KeepKey t` when `s.KeyThumbprint = Some t`. A session without a key (`None`: an anonymous
  session, or a stub that has none) prunes nothing; stored keys of other launches are left
  alone.
  Patient changes go through `UpdatePatient` so order context, order plan, formulary and
  parenteralia reload. `Patient` is never assigned directly.
- `Closed` and `OpenAnonymous` yield `Anonymous` plus `SetPatient None`. A launched patient and
  everything derived from it (order context, order plan, formulary, parenteralia) leave the
  screen with the session; an anonymous open carries nothing over (Rule 7).
- `Refused NoRole` keeps `retry = None` and the UI offers an anonymous open. Other refusals keep
  the Launch and key only when a retry is meaningful (`NoBrowserIdentity`).
- `RefusedAtCallback r` yields `Refused (r, None)`: the Launch was consumed server-side.

### Key pair

New file `src/Informedica.GenPRES.Client/Keys.fs`, the WebCrypto and IndexedDB interop:

- `generate: unit -> JS.Promise<PublicKey>`: creates a signing key pair with the private key not
  extractable, stores the private key in IndexedDB under the public key's thumbprint (RFC 7638),
  and returns the public key as a JWK string.
- `keep: string -> JS.Promise<unit>`: deletes every stored key except the one with the given
  thumbprint. Called on `Opened s` when `s.KeyThumbprint` is `Some`. Keys are per thumbprint, not a single
  entry, so a second launch in another tab that is refused cannot take away the key of the
  first tab's Session, which is still open and must still sign once step 7 lands. Pruning on
  open is safe: an open in the same browser closes the earlier Session anyway (Rule 8).
- `sign` is left for the step 7 plan; the key is generated now so the SessionRecord already holds
  the public key when it lands.
- `init` and `UrlChanged` await `generate` before dispatching `Present`; `transition` never touches
  the browser, so it stays pure.
- A reload has no Launch, so it resumes on the cookie; `getSession` returns the thumbprint and
  the client picks that private key. A new key pair is made only with a new Launch.

### IdentityProvider return

The hop is step 4 of the launch sequence: `presentLaunch` answers `RedirectTo url`, the client
calls `window.location.assign url`, and the server finishes the launch before the client runs
again. `RedirectTo` is a payload, not an HTTP 302: Fable.Remoting's XHR would follow a 302 and
try to parse the IdentityProvider's HTML as the response.

Two server-side points the client shape depends on, for the plan that implements the hop:

- The state of 4.2 is a LaunchRecord appended to the GenPRES Database. It holds the Launch's
  verified contents (nonce, PatientId, expiry), the `state` and the public key, never the
  sealed Launch, and is dropped whole after the expiry.
- The redirect of 4.2 also sets a `state` cookie (`HttpOnly`, `Secure`, `SameSite=Lax`,
  `Path=/callback`, `Max-Age` the Launch lifetime) and the callback refuses when it does not
  match the `state` in the URL, before the code is redeemed. This is the OpenID Connect
  correlation check (ASP.NET's OIDC handler does the same) and it is what stops a captured
  callback URL from opening the Session in another browser before step 7 exists. `Lax`
  because the callback is a cross-site top-level GET, on which `Strict` is not sent. The
  session cookie stays `Strict`: it is set on the callback response and only read by
  same-site requests afterwards. It is not sent on the landing GET of `#/session` either,
  which is fine, since `index.html` does not need it.
- The LaunchRecord and the spent-mark are one record per Launch, keyed by the nonce, holding
  `state`, the public key, the outcome and the session id. So the server answers a repeated callback
  (a reload of `/callback?code=...`, whose code is already redeemed) and a repeated presentation
  with the same public key alike: the first outcome, the same cookie (Rule 45). A refusal
  answered directly by `presentLaunch`, before the hop, records nothing, so a retry re-verifies
  the Launch; a refusal at the callback is appended to the LaunchRecord, so a reload of the
  callback gets the same answer.

The client work is the return:

- The client loads without a Launch, erases a `refused` parameter the same way it erases a
  launch, and dispatches `Resume` (cookie present, `getSession` answers `Some`) or
  `RefusedAtCallback`. A refusal at the callback cannot be retried by the client, which has no
  Launch left after the redirect: the modal asks for a relaunch, `no-identity` included.
- The reason vocabulary in `#/session?refused={reason}` is fixed: `expired`, `spent`, `invalid`,
  `no-identity`, `no-role`, `wrong-patient`, `enrolment`.

The stub never returns `RedirectTo`, but the client handles it and the `refused` return, so no
client change is needed when roadmap 2.1.3 lands.

### URL handling in `App.fs`

- Split `parseUrl` into `parsePatient` and `parseLaunch`, as PR #574 does. The launch URL form is
  `#/session?launch={token}`. In hash mode the token never reaches the server request line.
  `parseLaunch` also recognises `#/session?refused={reason}` from the IdentityProvider return
  and yields `RefusedAtCallback`; both forms are erased the same way.
- In `init`, when a Launch is present, call `history.replaceState(null, "", "#/session")` through
  `Browser.Dom` directly, not through `Router.navigate`: `Router.nav` in `FelizRouter.fs` always
  fires the navigation event and would re-enter `UrlChanged` and `UpdatePatient`. Do the same in
  `UrlChanged` for an in-app launch URL.
- Remove the raw-URL argument from both `Logging.warning "could not parse url…"` calls.
- Patient parameters in the URL are ignored while a launch is present; the session supplies the
  patient.
- `init` without a Launch dispatches `Resume`, so a reload keeps a launched session via the cookie.
- `ShowDisclaimer` is false whenever the session is not `Anonymous`.

### Wiring in `App.fs`

- `State.Session: Session` and `Msg.SessionMsg of SessionMsg`.
- `update` calls `Session.transition` and interprets each `SessionEffect` into a `Cmd`:
  `CallPresentLaunch (l, k)` runs `serverApi.presentLaunch (l, k)` and yields
  `Outcome (l, k, result)`, with a `try/with` that maps exceptions to `Error msg`;
  `GoTo url` calls `window.location.assign url`;
  `SetPatient p` is `Cmd.ofMsg (UpdatePatient p)`;
  `KeepKey t` runs `Keys.keep` for `t` and yields nothing.
- `init` and `UrlChanged` get the public key from `Keys.generate` when they dispatch `Present`.
- An `ISession` capability on `ConcreteAppEnv` exposes the `Session` value and the `close`,
  `retry` and `openAnonymously` actions, instead of threading more props through `GenPres.fs` and
  `TitleBar.fs`.

### UI

- `TitleBar.fs`: when `Open` with a user, show display name and role next to the hospital, and a
  "Close session" menu item. Show nothing for anonymous.
- New `Views/SessionGate.fs`: a modal in the pattern of the disclaimer modal in `Pages/GenPres.fs`,
  shown for `Launching`, `Resuming`, `Refused` and `Unreachable`. Text per refusal. Actions: Retry
  (for `Unreachable`, and for `NoBrowserIdentity` answered by `presentLaunch` itself, which the
  stub does), "Continue without launch" (for `NoRole` only),
  otherwise the instruction to relaunch from MainEHR. Edge C4 is one-way, so the client cannot
  trigger a relaunch itself.

### Server stub

Drafted in `src/Informedica.GenPRES.Server/Scripts/Session.fsx` and migrated by the maintainer:

- `Server.fs`: `Remoting.fromValue` becomes `Remoting.fromContext`, so handlers can read and set
  the cookie. This is the one non-stub server change; it is already noted there as deferred.
- `ServerApi.Ports.fs`: `SessionPort` with `open: Launch * PublicKey -> Async<LaunchOutcome>`,
  `find: string -> Async<SessionOpened option>` and `close: string -> Async<unit>`.
- `ServerApi.Adapters.fs`: an in-memory stub keyed by a random session id. Token conventions
  `expired`, `spent` (marked as used by another browser), `no-role`, `wrong-patient` and
  `no-identity` map to the matching refusal; anything else opens a session with a stub
  Prescriber and the existing `PatientPort` stub patient.
- Presentation is idempotent per Rule 2, correlated by the public key, never by the presence or
  absence of a cookie. The spent-mark is written only in the act that opens a Session (Rules 2,
  40); a refusal records nothing, so a retry after a transient refusal such as
  `NoBrowserIdentity` re-verifies the Launch. The stub keeps the spent-mark per Launch, keyed by
  the Launch text as its stand-in for the nonce, holding the public key, the outcome, the session
  id and an expiry (Rule 29; two minutes in the stub), and drops it after the expiry.
  A second presentation within the lifetime with the same public key is answered as the first
  was and re-issues the same cookie; nothing opens twice. A presentation with a different key,
  or with none, is `LaunchSpent`: it cannot be told from another browser, so it is treated as
  one. After the lifetime the Launch is `LaunchExpired`. The stub record is the same LaunchRecord
  the real server appends at 4.2 (see the IdentityProvider return), so the real server re-issues
  at the callback and at a repeated presentation from the same place. Rule 2 names the
  BrowserIdentity; the public key is what the stub, and the real server before the identity hop,
  can check, and the BrowserIdentity is a second check after it.
- The stub session record holds the public key's thumbprint; `getSession` returns it.
- `ServerApi.CompositionRoot.fs`: `presentLaunch` sets the cookie `genpres_session` with HttpOnly,
  SameSite=Strict, Path=/ and Secure when the request is HTTPS. `getSession` reads it and
  `closeSession` deletes it.
- Tests in `tests/Informedica.GenPRES.Server.Tests/StubAdapterTests.fs`: refusal mapping, a
  second presentation within the lifetime with the same public key returns the first outcome and
  the same session, one with a different key is `LaunchSpent` and opens nothing, a presentation
  after the lifetime is `LaunchExpired`, close removes the session.

### Out of scope

Each has a named extension point:

- The signed request on every call (launch step 7, DPoP): `Keys.sign` and a request
  interceptor, once the server verifies proofs. The OpenedToken travels inside that proof, so
  sending it is part of the same step.
- Session endings and the Rule 11 notice (roadmap 2.1.6): add `Session.Ended of SessionEnding`
  when the server can report it.
- UC-2 PIN enrolment.
- WorkPlan carry-over (#518).
- The LaunchScript contract for a path query versus the hash form (decision D1).
- Whether the production server exposes the stubbed session endpoints at all. That is a scope
  question, tracked in #580 (scope switch), not a launch question. The stub of step 2 opens a
  session for any Launch text outside its refusal vocabulary, so #580 is a prerequisite for
  merging step 2: the stub does not ship to a server without the scope switch.

## Confidence

Medium-high on the client shape: it follows the launch sequence step by step and is D1-neutral.
Medium on the cookie in development: Vite proxies `/api` with `changeOrigin: true`, which should
pass a host-only cookie unchanged. To be confirmed in step 2. Medium on WebCrypto and IndexedDB
from Fable: the client has no such interop yet.

## Steps

Each step is one PR under 200 changed lines. Client `.fs` files are edited directly (the UI
exception in `AGENTS.md`); Shared and Server changes are drafted in scripts and migrated by the
maintainer.

1. **Correct PR #574 to the router fix and URL hygiene** (client only). Keep the `FelizRouter`
   latest-callback fix and the `parsePatient`/`parseLaunch` split. Remove the redeem-token
   navigation, the `SessionContext` state, the session API stubs and the simulated delays. Erase
   the launch from URL and history in `init` and `UrlChanged`. Drop the raw URL from the warning
   logs. No session state yet: a launch URL simply becomes `#/session`.
2. **Shared contract and server stub.** Types and `IServerApi` additions in Shared, `SessionPort`,
   the stub adapter storing the public key, cookie handling via `fromContext`, stub tests. Verify
   the cookie round trip through the Vite proxy.
3. **Key pair and client session state machine.** `Keys.fs` (generate, keep, IndexedDB per
   thumbprint), `Session.fs`
   with `transition`, `State.Session`, `SessionMsg` interpretation in `update`, `Resume` on load,
   `UpdatePatient` on open, disclaimer suppression, the `ISession` capability on
   `ConcreteAppEnv`.
4. **UI.** Title bar session indicator and close, the `SessionGate` modal with retry, anonymous
   open and relaunch texts, localisation terms for the new strings.
5. **Docs.** Record what was built against this plan (the [As built](#as-built) section).

## Verification

Manual, with `dotnet run` and the demo sheet:

- Open `http://localhost:5173/#/session?launch=demo`. The address bar shows `#/session` before the
  first server call, the back button leaves the site (no history entry), the title bar shows the
  stub user, the patient is loaded through `UpdatePatient`, and no disclaimer appears.
- DevTools: the cookie `genpres_session` is HttpOnly and SameSite=Strict; no request URL contains
  the token; a reload keeps the session. Application > IndexedDB shows the private key entry,
  and `exportKey` on it fails from the console.
- `launch=expired` and `launch=wrong-patient`: the modal asks for a relaunch. `launch=no-role`:
  the modal offers an anonymous open, which yields a plain anonymous state without a patient
  and no order context. `launch=spent`: the modal asks for a relaunch.
- Open the same `launch=demo` url in a second tab, or a second browser, within two minutes: the
  second gets `LaunchSpent` and no second session is opened; the first tab keeps its session.
  Open it again after two minutes: `LaunchExpired`.
- Block the first `presentLaunch` response in DevTools (or stop the server between request and
  reply): the automatic retry, carrying the same public key, is answered with the session the
  first call opened, not with a refusal. The public key is visible in the request body and
  identical across the attempts.
- `#/session?refused=no-role`: the parameter is erased from the address bar and the modal offers
  an anonymous open.
- Stop the server and open a launch URL: three attempts, then the Unreachable modal with Retry.
  Start the server; Retry opens the session.
- Close the session from the title bar: anonymous state, cookie gone, patient and order context
  cleared.
- Two tabs, once step 7 exists: launch in tab 1, then `launch=no-role` in tab 2. Tab 2 is
  refused, tab 1 still signs its requests: its private key was not removed. Then a successful
  launch in tab 2: tab 1's Session is closed and its key is gone.
- Reload of `/callback?code=...&state=...`, once the real identity hop exists (the stub never
  redirects): the second load lands in the same Session, no refusal.
- `dotnet run ServerTests` passes with the new stub tests; `dotnet run MarkdownLint` is clean for
  this document.

## As built

Every step landed as one or more PRs from a fork branch against `master`, each drafted script-first
where it touched non-UI source and reviewed before migration. The plan's shape held; the deviations
are listed per step with the reason.

| Step | PR | Landed |
|------|----|--------|
| 1 | [#574](https://github.com/informedica/GenPRES/pull/574), [#587](https://github.com/informedica/GenPRES/pull/587) | #574 merged as scaffolded; #587 reverted its redeem-token contract and `SessionContext`, kept the `FelizRouter` fix, added `parseLaunch` and `eraseLaunch` (`history.replaceState`, not `Router.navigate`), dropped the raw URL from the warnings |
| 2a | [#588](https://github.com/informedica/GenPRES/pull/588) | Shared contract types; `SessionPort`; RFC 7638 `PublicKey.thumbprint`; in-memory `SessionStub` with a pure `present` transition; 17 tests |
| 2b | [#589](https://github.com/informedica/GenPRES/pull/589) | `processLaunch` and `processSession`; `SessionCookie` request port; `Http.sessionCookie`; `Remoting.fromContext` with the env built once in `Host.build`; 12 tests |
| 3a | [#591](https://github.com/informedica/GenPRES/pull/591) | `SessionMachine.fs` with `Session.transition`; 53 tests, linked into `Informedica.GenPRES.Shared.Tests` |
| 3b | [#592](https://github.com/informedica/GenPRES/pull/592) | `Keys.fs`: WebCrypto ECDSA P-256, private key in IndexedDB under the RFC 7638 thumbprint |
| 3c | [#593](https://github.com/informedica/GenPRES/pull/593) | `App.fs` wiring: `State.Session`, effect interpreter, `Resume` on load, `ISession` on `ConcreteAppEnv` |
| 4a | [#595](https://github.com/informedica/GenPRES/pull/595) | title bar: user and role behind a person button, "Close session" |
| 4b | [#596](https://github.com/informedica/GenPRES/pull/596) | `SessionGatePolicy.fs` (pure, tested) and `Views/SessionGate.fs` |
| follow-up | [#600](https://github.com/informedica/GenPRES/pull/600) | localisation: 22 `Terms` cases for the gate and the session menu, `gateFor` takes a translator, English and Dutch sheet rows |

### Deviations from the text above

- **API shape.** `IServerApi` did not gain three flat functions. It gained two command families
  next to `processCommand`, cut at the authentication boundary: `processLaunch: LaunchCommand ->
  Async<LaunchOutcome>` with `PresentLaunch of Launch * PublicKey`, and `processSession:
  SessionCommand -> Async<SessionResponse>` with `GetSession | CloseSession`. A refusal is a value,
  never an `Error`; each family has room to grow (the identity callback; Rule 11 endings, resume,
  PIN) without new API fields.
- **Port shape.** The port field is `present`, not `open` (an F# keyword), and it answers a
  server-side `LaunchResult` that carries the session id next to the `SessionOpened`: the plan's
  `Async<LaunchOutcome>` had nowhere to put the id the cookie needs, and the id never enters the
  client-facing `LaunchOutcome` (Rule 12).
- **Cookie handling.** The composition root never sees `HttpContext`. It takes a request-scoped
  `SessionCookie { read; write; delete }` built in `Server.fs`, so it is testable with an in-memory
  cookie; the attributes are tested once over `DefaultHttpContext`. `X-Forwarded-Proto` from the
  trusted proxies is honoured next to `X-Forwarded-For`, so the cookie is `Secure` behind the
  TLS-terminating proxy. `CloseSession` deletes the cookie in a `finally`.
- **Production stop-gap.** Until the scope switch (#580) lands, `Host.build` swaps the session port
  for `Adapters.sessionDisabled` when `GENPRES_PROD=1`: every launch is refused as invalid and no
  session is found. One `if` to delete when #580 decides what production exposes.
- **File name.** The machine lives in `SessionMachine.fs`, not `Session.fs`. The call sites
  `Session.Anonymous` and `Session.transition` need the coding standard's type-first pair `type
  Session` / `module Session`, and that pair cannot live in a file module also named `Session`
  (`open Session` then resolves `Session.X` to the outer module, FS0035).
- **Two more states and one more message.** `Close` from `Open` enters `Closing of SessionOpened`;
  `Closed` lands only there, so a close that completes after a newer launch has opened another
  session cannot end it. A close whose request never reached the server sends `CloseFailed`, which
  returns `Closing` to `Open`, and the app shows an error; any answer from the server means
  `Closed`, since the server deleted the cookie either way.
- **Keys.** `generate` returns `Async<PublicKey>`, not `JS.Promise`, to fit the client's
  `Cmd.fromAsync`; `thumbprint` and `list` were added. `keep` prunes by age, not by identity: each
  stored key carries its creation time and `keep` deletes only keys older than the Launch lifetime
  besides the kept one. Two launches racing in two tabs (ext 8b) both keep their fresh key; the
  loser's is pruned by a later launch. Pruning by identity let each tab delete the other's key and
  left the winning Session unable to sign.
- **URL patient.** URL patient parameters count only while no Session is `Open` or `Closing`, not
  while the session is merely non-anonymous: the router fires `UrlChanged` on mount while `Resume`
  is still in flight, and blocking `Resuming` broke anonymous patient URLs.
- **Disclaimer.** Gated at the view, `ShowDisclaimer && Session is Anonymous`, not in state, for
  the same reason (`Resuming` is transient) and so that an anonymous open after a refusal still
  sees it.
- **Gate texts.** The title bar and gate strings landed as English literals, in one place each,
  because turning them into `Terms` cases means editing `Shared/Localization.fs` (non-UI source)
  plus sheet rows. The script-first follow-up (#600) did that: the policy takes a translator
  (`Terms -> string`), every sentence is one term, numbers are filled through `{0}`/`{1}`.

### Still open

- Step 7, the signed request: the DPoP proof (`Keys.sign`) over the request envelope that
  plan 635 built, which already carries the OpenedToken.
- WorkPlan carry-over (#518); decision D1 (#599).
- The scope switch (#580), which retires the `IsProd` stop-gap.
- On a launch URL the gate hides the language switcher; the language itself follows the
  server default since #601.

Closed since this list was written: the identity hop (`RedirectTo`, the callback, the `state`
cookie), the Rule 8 and 11 endings, and the sealed Launch, all against server-hosted stubs
([plan 605](605-launch-with-server-stubs.md)); UC-2 enrolment against a stub MailService and an
in-memory credential store ([plan 615](615-enrolment-with-server-stubs.md)); UC-3 signing
against an in-memory record, the OpenedToken checked and re-minted at a signature and the
Session opened with the head of the record
([plan 622](622-signing-with-server-stubs.md)); Compute bound to the Session, the head's orders
into the cart at open, Rule 21's notice on every reply and UC-4 end to end
([plan 635](635-session-bound-compute.md)).
