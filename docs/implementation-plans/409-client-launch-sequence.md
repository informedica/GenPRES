# Implementation plan for issue 409: client side of the launch sequence

Scope: the GenPRES Client half of UC-1 (launch), with every server-side action stubbed behind the
final API contract. The plan is picked up in PR #574, which already scaffolds this flow, to
correct that PR to the shape below; later steps follow as PRs on top of it.

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
  request. This plan only stores it; sending it with commands is a later step.

Roadmap item 2.1.2 in `docs/roadmap/mvpap2019-gap-overview.md` (erase the launch, stop logging
the raw URL) is the one unblocked 2.1.x item. Decision D1 is still open; this plan is D1-neutral on
the client because the Launch is opaque to it.

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

For the stub there were two options: an in-memory single session without a cookie, or a real
cookie via `Remoting.fromContext`. Chosen: the cookie now, so the client is final and the server
stub is replaced later behind the same contract.

## Chosen approach

### Shared contract

Added to `src/Informedica.GenPRES.Shared/Types.fs` and `Api.fs` (drafted in a script, migrated by
the maintainer):

```fsharp
type Launch = Launch of string                      // opaque, sealed by MainEHR LaunchScript
type PresentationKey = PresentationKey of string    // random per page load, lives only in client memory
type OpenedToken = OpenedToken of string
type UserRole = Prescriber | Reader
type UserContext = { UserId: string; DisplayName: string; Role: UserRole }
type PatientContext = { PatientId: string; Patient: Patient }

type SessionOpened =                                 // no SessionId: it lives in the cookie
    {
        User: UserContext option                     // None = anonymous session (Rule 14)
        PatientContext: PatientContext option        // None = launch without active patient (ext 1a)
        OpenedToken: OpenedToken option
    }

type LaunchRefusal =
    | LaunchExpired | LaunchSpent | LaunchInvalid    // ext 4a: ask for a relaunch
    | NoBrowserIdentity                              // ext 3c: retry, then relaunch
    | NoRole                                         // ext 5a: offer anonymous open
    | WrongActivePatient                             // ext 5b: relaunch after fixing MainEHR
    | EnrolmentRequired                              // UC-2, shown as text only for now

type LaunchOutcome =
    | Opened of SessionOpened
    | RedirectTo of url: string                      // IdP hop (UC-1 step 3), see below; stub never returns it
    | Refused of LaunchRefusal

// IServerApi additions
presentLaunch: Launch * PresentationKey -> Async<LaunchOutcome>   // idempotent per key, Rule 2
getSession: unit -> Async<SessionOpened option>     // cookie-authenticated
closeSession: unit -> Async<unit>                   // Rule 10: explicit close
```

### Client session state machine

New file `src/Informedica.GenPRES.Client/Session.fs`, pure F# without React:

```fsharp
[<RequireQualifiedAccess>]
type Session =
    | Anonymous
    | Launching of Launch * PresentationKey * attempt: int
    | Resuming                                       // getSession in flight (reload, IdP return)
    | Open of SessionOpened
    | Refused of LaunchRefusal * retry: Launch option
    | Unreachable of Launch * PresentationKey * attempts: int   // ext 3a: server down after page served

type SessionMsg =
    | Present of Launch * PresentationKey            // key minted by App.fs, once per page load
    | Outcome of Launch * PresentationKey * Result<LaunchOutcome, string>   // Error = transport failure
    | Resume
    | Resumed of Result<SessionOpened option, string>
    | RefusedAtCallback of LaunchRefusal             // from #/session?refused={reason}, see IdP return
    | OpenAnonymous
    | Close
    | Closed

type SessionEffect =
    | CallPresentLaunch of Launch * PresentationKey
    | CallGetSession
    | CallCloseSession
    | GoTo of url: string                            // window.location.assign, for RedirectTo
    | SetPatient of Patient option                   // interpreted as UpdatePatient

val transition: SessionMsg -> Session -> Session * SessionEffect list
```

Rules encoded in `transition`:

- `Outcome (l, k, _)` is ignored unless the state is `Launching (l, k, _)` for the same Launch
  and the same key. `Resumed` is ignored unless the state is `Resuming`. This is the
  stale-request guard: a response can only land on the presentation that sent it.
- `Present (l, _)` while the state is `Launching (l, _, _)` or `Unreachable (l, _, _)` is a
  no-op: an in-flight presentation is never replaced by a second one for the same Launch, and
  the first key stays the one the server will recognise. A `Present` for a different Launch
  supersedes the current one, and the old one's outcome is then dropped by the guard above.
- A transport error while `Launching (l, k, n)` with `n < 3` yields `Launching (l, k, n + 1)`
  plus `CallPresentLaunch (l, k)`; at three attempts it becomes `Unreachable (l, k, 3)` and the
  UI offers Retry, which re-presents with the same key. A retry always carries the same Launch
  and the same `PresentationKey`. The key is what makes the retry safe: the server answers a
  repeated key as it answered the first time (Rule 2, see the server stub), so a response lost
  on the wire cannot turn a session that did open into `LaunchSpent`, while a presentation with
  another key, which is what any other browser has, is refused. The key exists only in this
  page load's memory (Rule 39); a reload has no Launch left to present anyway.
- `Opened s` yields `Open s` plus `SetPatient (s.PatientContext |> Option.map _.Patient)`.
  Patient changes go through `UpdatePatient` so order context, order plan, formulary and
  parenteralia reload. `Patient` is never assigned directly.
- `Closed` and `OpenAnonymous` yield `Anonymous` plus `SetPatient None`. A launched patient and
  everything derived from it (order context, order plan, formulary, parenteralia) leave the
  screen with the session; an anonymous open carries nothing over (Rule 7).
- `Refused NoRole` keeps `retry = None` and the UI offers an anonymous open. Other refusals keep
  the Launch only when a retry is meaningful (`NoBrowserIdentity`).
- `RefusedAtCallback r` yields `Refused (r, None)`: the Launch was consumed server-side.

### IdentityProvider return

`RedirectTo` unloads the client, so the client cannot keep the Launch across the hop. It does not
need to: from UC-1 step 3 and Rule 39, the Launch travels in the request state of the identity
round trip, and the server finishes the launch before the client runs again.

1. `presentLaunch` answers `RedirectTo url`. The server has put the Launch in the request state
   of that url (the OIDC `state` parameter, encrypted, or a server-side entry keyed by the
   `state` nonce; roadmap 2.1.3 decides which).
2. The client calls `window.location.assign url`. The IdentityProvider signs the browser on and
   redirects to a server callback, a plain GET outside Fable.Remoting, with the authorization
   code and `state`.
3. The callback redeems the code back-channel (edge C6), verifies the Launch, opens the Session
   and sets the cookie on its own redirect response, then redirects the browser to `/#/session`.
   On refusal it opens nothing and redirects to `/#/session?refused={reason}` with a fixed
   reason vocabulary (`expired`, `spent`, `invalid`, `no-identity`, `no-role`,
   `wrong-patient`, `enrolment`). A reason is not a secret; the token never appears in that url.
4. The client loads without a Launch, erases a `refused` parameter the same way it erases a
   launch, and dispatches `Resume` (cookie present, `getSession` answers `Some`) or
   `RefusedAtCallback`.

Only step 4 is client work in this plan. The stub never returns `RedirectTo`, but the client
handles it and the `refused` return, so no client change is needed when 2.1.3 lands.

### URL handling in `App.fs`

- Split `parseUrl` into `parsePatient` and `parseLaunch`, as PR #574 does. The launch URL form is
  `#/session?launch={token}`. In hash mode the token never reaches the server request line.
  `parseLaunch` also recognises `#/session?refused={reason}` from the IdentityProvider return
  and yields `RefusedAtCallback`; both forms are erased the same way.
- In `init`, when a Launch is present, call `history.replaceState(null, "", "#/session")` through
  `Browser.Dom` directly, not through `Router.navigate`: `Router.nav` in `FelizRouter.fs` always
  fires the navigation event and would re-enter `UrlChanged` and `UpdatePatient`. Do the same in
  `UrlChanged` for an in-app launch URL.
- Remove the raw-URL argument from both `Logging.warning "could not parse url…"` calls
  (roadmap 2.1.2).
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
  `SetPatient p` is `Cmd.ofMsg (UpdatePatient p)`.
- `init` and `UrlChanged` mint the `PresentationKey` with `window.crypto.randomUUID()` when they
  dispatch `Present`; `transition` never creates one, so it stays pure.
- An `ISession` capability on `ConcreteAppEnv` exposes the `Session` value and the `close`,
  `retry` and `openAnonymously` actions, instead of threading more props through `GenPres.fs` and
  `TitleBar.fs`.

### UI

- `TitleBar.fs`: when `Open` with a user, show display name and role next to the hospital, and a
  "Close session" menu item. Show nothing for anonymous.
- New `Views/SessionGate.fs`: a modal in the pattern of the disclaimer modal in `Pages/GenPres.fs`,
  shown for `Launching`, `Resuming`, `Refused` and `Unreachable`. Text per refusal. Actions: Retry
  (for `Unreachable` and `NoBrowserIdentity`), "Continue without launch" (for `NoRole` only),
  otherwise the instruction to relaunch from MainEHR. Edge C4 is one-way, so the client cannot
  trigger a relaunch itself.

### Server stub

Drafted in `src/Informedica.GenPRES.Server/Scripts/Session.fsx` and migrated by the maintainer:

- `Server.fs`: `Remoting.fromValue` becomes `Remoting.fromContext`, so handlers can read and set
  the cookie. This is the one non-stub server change; it is already noted there as deferred.
- `ServerApi.Ports.fs`: `SessionPort` with `open: Launch -> Async<LaunchOutcome>`,
  `find: string -> Async<SessionOpened option>` and `close: string -> Async<unit>`.
- `ServerApi.Adapters.fs`: an in-memory stub keyed by a random session id. Token conventions
  `expired`, `spent` (marked as used by another browser), `no-role`, `wrong-patient` and
  `no-identity` map to the matching refusal; anything else opens a session with a stub
  Prescriber and the existing `PatientPort` stub patient. With `GENPRES_PROD=1` every launch is
  refused with `LaunchInvalid`, so the stub fails closed in production.
- Presentation is idempotent per Rule 2, correlated by the `PresentationKey`, never by the
  presence or absence of a cookie. The stub keeps a spent-mark per Launch holding the key, the
  outcome, the session id and a lifetime (Rule 29; two minutes in the stub). A second
  presentation within the lifetime with the same key is answered as the first was and re-issues
  the same cookie; nothing opens twice. A presentation with a different key, or with no key, is
  `LaunchSpent`: a request without a key cannot be told apart from another browser, so it is
  treated as one. After the lifetime the Launch is `LaunchExpired`. The real implementation
  keys the same-browser check on the BrowserIdentity (Rule 2); the key is the stand-in that the
  stub, and the real server before the identity hop, can verify.
- `ServerApi.CompositionRoot.fs`: `presentLaunch` sets the cookie `genpres_session` with HttpOnly,
  SameSite=Strict, Path=/ and Secure when the request is HTTPS. `getSession` reads it and
  `closeSession` deletes it.
- Tests in `tests/Informedica.GenPRES.Server.Tests/StubAdapterTests.fs`: refusal mapping, a
  second presentation within the lifetime with the same key returns the first outcome and the
  same session, one with a different key is `LaunchSpent` and opens nothing, a presentation
  after the lifetime is `LaunchExpired`, production fail-closed, close removes the session.

### Out of scope

Each has a named extension point:

- Session endings and the Rule 11 notice (roadmap 2.1.6): add `Session.Ended of SessionEnding`
  when the server can report it.
- Sending the OpenedToken with every command.
- UC-2 PIN enrolment.
- WorkPlan carry-over (#518).
- The LaunchScript contract for a path query versus the hash form (decision D1).

## Confidence

Medium-high on the client shape: it follows UC-1 step by step and is D1-neutral. Medium on the
cookie in development: Vite proxies `/api` with `changeOrigin: true`, which should pass a
host-only cookie unchanged. To be confirmed in step 2.

## Steps

Each step is one PR under 200 changed lines. Client `.fs` files are edited directly (the UI
exception in `AGENTS.md`); Shared and Server changes are drafted in scripts and migrated by the
maintainer.

1. **Correct PR #574 to the router fix and URL hygiene** (client only). Keep the `FelizRouter`
   latest-callback fix and the `parsePatient`/`parseLaunch` split. Remove the redeem-token
   navigation, the `SessionContext` state, the session API stubs and the simulated delays. Erase
   the launch from URL and history in `init` and `UrlChanged`. Drop the raw URL from the warning
   logs. No session state yet: a launch URL simply becomes `#/session`. Closes roadmap 2.1.2.
2. **Shared contract and server stub.** Types and `IServerApi` additions in Shared, `SessionPort`,
   the stub adapter, cookie handling via `fromContext`, stub tests. Verify the cookie round trip
   through the Vite proxy.
3. **Client session state machine.** `Session.fs` with `transition`, `State.Session`,
   `SessionMsg` interpretation in `update`, `Resume` on load, `UpdatePatient` on open, disclaimer
   suppression, the `ISession` capability on `ConcreteAppEnv`.
4. **UI.** Title bar session indicator and close, the `SessionGate` modal with retry, anonymous
   open and relaunch texts, localisation terms for the new strings.
5. **Docs.** Tick roadmap 2.1.2. Nothing is written to `CHANGELOG.md` by hand: EasyBuild.ShipIt
   generates it on every push to `master` from the conventional-commit history and bumps the
   version in `Directory.Build.props` (see DEVELOPMENT.md, "Changelog & Release Automation").

### Commit conventions for the release notes

Only `feat` and `fix` commits render in the changelog; `docs`, `chore` and `build` never do. So
each step above lands as a `feat(client)`, `feat(api)` or `fix(client)` commit, and the docs step
as `docs(...)`. Where a step deserves more than its subject line in the release notes, put a
`=== changelog ===` block in the commit message body, opened and closed with the marker, for
example:

```text
fix(client): erase the launch token from URL and history

=== changelog ===
A launch token in the address bar ended up in browser history, referrer
headers and logs. The client now removes it on first presentation (Rule 39)
and no longer logs unparseable URLs verbatim.
=== changelog ===
```

The block must be in the commit message, not the PR body: with all three merge methods enabled,
only squash merges copy a PR body into the commit.

### Release gating

ShipIt offers no per-commit way to hold back a release: its CLI has no skip list or footer for
that, and a `[skip ci]` on one push only delays the run, because the next push walks history
back to `last_commit_released` and picks the commits up anyway. Two gates exist instead:

1. **Commit type.** A push whose commits are all non-rendering (`chore`, `docs`, `build`, merge
   commits) makes ShipIt report "nothing to ship" and open no release PR. Verified with
   `dotnet shipit --dry-run --allow-branch master --skip-merge-commit` on the current `master`.
   This is not a tool for hiding a feature: the launch steps are `feat`/`fix` and must render.
2. **The release PR.** ShipIt runs in pull-request mode and never releases by itself. The tag,
   the GitHub Release and the Docker push in `tag-release.yml` fire only when a maintainer merges
   the `release/master` PR. Leaving that PR open holds every release, for every change on
   `master`, until it is merged.

Because gate 2 holds all of `master`, this plan does not rely on it. The stubbed launch is safe to
ship in an alpha because the server stub fails closed under `GENPRES_PROD=1` (every launch is
refused) and an anonymous open is unchanged. That is the feature-flag approach CONTRIBUTING asks
for with incomplete work, and it keeps the release line free for unrelated fixes. If a step is
ever unsafe to ship, keep it on the feature branch until it is, rather than merging and holding
the release PR.

## Verification

Manual, with `dotnet run` and the demo sheet:

- Open `http://localhost:5173/#/session?launch=demo`. The address bar shows `#/session` before the
  first server call, the back button leaves the site (no history entry), the title bar shows the
  stub user, the patient is loaded through `UpdatePatient`, and no disclaimer appears.
- DevTools: the cookie `genpres_session` is HttpOnly and SameSite=Strict; no request URL contains
  the token; a reload keeps the session.
- `launch=expired` and `launch=wrong-patient`: the modal asks for a relaunch. `launch=no-role`:
  the modal offers an anonymous open, which yields a plain anonymous state without a patient
  and no order context. `launch=spent`: the modal asks for a relaunch.
- Open the same `launch=demo` url in a second tab, or a second browser, within two minutes: the
  second gets `LaunchSpent` and no second session is opened; the first tab keeps its session.
  Open it again after two minutes: `LaunchExpired`.
- Block the first `presentLaunch` response in DevTools (or stop the server between request and
  reply): the automatic retry, carrying the same key, is answered with the session the first
  call opened, not with a refusal. The key is visible in the request body and identical across
  the attempts.
- `#/session?refused=no-role`: the parameter is erased from the address bar and the modal offers
  an anonymous open.
- Stop the server and open a launch URL: three attempts, then the Unreachable modal with Retry.
  Start the server; Retry opens the session.
- Close the session from the title bar: anonymous state, cookie gone, patient and order context
  cleared.
- `GENPRES_PROD=1`: every launch is refused.
- `dotnet run ServerTests` passes with the new stub tests; `dotnet run MarkdownLint` is clean for
  this document.
