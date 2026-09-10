# Implementation plan for issue 605

## Problem description

Plan [409](409-client-launch-sequence.md) built the client side of the launch sequence
([uc-01](../scenarios/integration/uc-01-launch.md)) and an in-memory server stub that answers
`Opened` or `Refused` from the launch word in one call. The stub never redirects, so the identity
hop of step 4 (LaunchRecord, `state` cookie, IdentityProvider, callback), the sealed Launch of
steps 4.2, 5.1 and 5.2, and step 5's UserRegistry and patient-data reads have no server code.
`LaunchOutcome.RedirectTo` is handled by the client but never produced, and Rules 2, 39 and 45 of
the [integration design](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md) cannot be
exercised end to end. Issue [#605](https://github.com/informedica/GenPRES/issues/605) asks for
the whole launch, steps 1 to 6, running against stubs hosted by the GenPRES server.

Decisions taken with the issue: the hash form `#/session?launch=` stays (decision D1 of plan 409
is not reopened; [#599](https://github.com/informedica/GenPRES/issues/599) stays open); the
signed request of step 7 and PIN enrolment (UC-2) are out; the Rule 8 and 11 endings are in.

## Approaches considered

1. **Keep the one-call stub** until a real IdentityProvider is integrated. Nothing to build now;
   the hop, the callback and the `refused` return stay untested, and Rules 2, 45 stay words.
2. **Stub the IdentityProvider inside the client** (answer the redirect with a client-side page).
   Rejected: the point of the hop is that the BrowserIdentity never passes through the client's
   hands (uc-01 step 4, edge C6); a client-side stub would test the wrong shape.
3. **Real hop with stubs hosted by the server, sealed Launch minted by a stub LaunchScript page,
   endings included.** Chosen. Every message of uc-01 steps 1 to 6 is exchanged over HTTP as it
   will be with MainEHR and Entra ID; only the actors behind the ports are in-memory stand-ins,
   mounted in full scope only.

## Chosen approach

Approach 3. The stubs are adapters in the DMZ ([ADR-0001](../adr/0001-system-architecture.md),
dependency rule); the launch logic is pure over a state record, as `SessionStub.present` is
today, so it runs under Expecto and in FSI. The scope switch ([#580](https://github.com/informedica/GenPRES/issues/580))
decides later whether any of it is exposed in production; until then the stubs are mounted only
when `GENPRES_PROD=0`, at the same place as the `sessionDisabled` swap.

### Ports (ServerApi.Ports.fs), stubs (ServerApi.Adapters.fs)

```fsharp
type BrowserIdentity = { Login: string; DisplayName: string }                 // Concept 4
type UserStanding = { User: UserContext; ActivePatientId: string option; PinSet: bool }  // Rules 5, 6, 24

type IdentityProviderPort = { authorizeUrl: state: string -> string; redeem: code: string -> BrowserIdentity option }
type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }
type PatientDataPort = { read: patientId: string -> Patient option }

type Callback = { State: string; StateCookie: string option; Code: string option; Error: string option }

type CallbackResult =
    | Opened of sessionId: string * redirect: string    // "/#/session"
    | Refused of LaunchRefusal * redirect: string       // "/#/session?refused=<word>"
```

`SessionPort` gains `callback: Callback -> Async<CallbackResult>` next to `present`, `find` and
`close`.

- `present (launch, key)`: verify seal and lifetime (`LaunchInvalid`, `LaunchExpired`); a record
  under the nonce with the same public key within the lifetime is answered as the first time
  (Rule 2), another key is `LaunchSpent`; a new nonce appends `LaunchRecord { Nonce; State;
  PatientId; PublicKey; Expiry; Outcome = None }` and answers `RedirectTo (authorizeUrl state)`.
  `state` is a fresh random id; the edge sets the `genpres_launch_state` cookie (HttpOnly, Lax,
  `Path=/callback`, `Max-Age` = the Launch lifetime).
- `callback`: the cookie must equal `State`, else `LaunchInvalid`; the record is found by state;
  expired is `LaunchExpired`; an outcome already recorded is answered again (Rule 45, the cookie
  proves the same browser); `Error = no-identity` or a code that does not redeem is
  `NoBrowserIdentity`; no standing is `NoRole`; another active patient is `WrongActivePatient`;
  a Prescriber without a PIN is `EnrolmentRequired` (Rule 25 binds only Prescribers; a Reader
  opens without a PIN, ext 5c); otherwise the patient data is read and the session opens in one
  act (Rule 40): the nonce is spent, the user's other sessions are closed and marked
  `SupersededByLaunch` (Rule 8), the session is written with `KeyThumbprint` and `OpenedToken`,
  and the outcome is appended to the record. When `PatientDataPort.read` answers `None`
  (ext 6a: the platform is unreachable or has nothing), the session still opens: the
  `PatientContext` carries the Launch's PatientId with an empty `Patient`, and the client shows
  the patient without imported data. Refusing would let a data outage block prescribing, which
  the design keeps open on purpose.

### The sealed Launch and the stub LaunchScript page

`LaunchSeal`: a key of 32 CSPRNG bytes made once per host start, so a token from an earlier run
is "not sealed under the key"; token = `base64url(json) + "." + base64url(HMAC-SHA256(key,
json))` with `{ pid; nonce; exp }`, `exp` = now + 2 minutes (Rule 29). `verify` yields
`LaunchClaims { PatientId; Nonce }` or the refusal. This is the shape the real LaunchScript
follows later, with a configured key.

`GET /stub/launch`: a plain HTML form (no inline script, so the CSP holds): PatientId and an
identity choice (`prescriber`, `reader`, `prescriber-other-patient`, `no-pin`, `unknown`,
`none`). `POST /stub/launch` mints the token, sets `genpres_stub_identity` (Lax, `Path=/`, two
minutes) and redirects to `/#/session?launch=<token>`.

Stub IdentityProvider, `GET /authorize?state=<state>`: reads `genpres_stub_identity`; `none`
redirects to `/callback?state=<state>&error=no-identity`, the same `state` as it received, so
the callback's cookie check passes and the error is what decides; anything else mints a one-time
code and redirects to `/callback?code=<code>&state=<state>`. Stub UserRegistry: `prescriber` is a
Prescriber with the launch's patient active; `reader` a Reader with the launch's patient active,
no PIN needed; `prescriber-other-patient` has another patient active; `no-pin` is a Prescriber
without a PIN; `unknown` has no standing. Stub patient data answers `Patient.empty` for every
PatientId except `no-data`, for which it answers `None`, so ext 6a can be exercised from the
stub page.

`GET /callback` is mounted always; with `sessionDisabled` it refuses `invalid`. It runs
`session.callback`, sets the session cookie on `Opened` and redirects.

### Endings (Rules 8, 11)

`SessionResponse` gains `SessionEnded of SessionEnding` with `SessionEnding = SupersededByLaunch`.
`GetSession` answers it when the cookie's session is in the endings, deletes the cookie and drops
the mark, so the User is told once. The client gains `Session.Ended of SessionEnding`, a gate
text with the reason and the "continue without launch" action, and three `Terms`.

### Client

`parseRefusal` learns the word `invalid`; `vite.config.js` proxies `/authorize`, `/callback` and
`/stub` to the server in development (cookies on `localhost` are port-agnostic, so the cookies
the server sets reach the Vite origin); the `Resumed` path already keeps the key stored before
the redirect. Everything else on the client stands.

### What stays as is

`Adapters.sessionDisabled` and the `IsProd` swap; `Keys.fs` without `sign`; `OpenedToken`
minted, not yet consumed; `EnrolmentRequired` as a refusal; `Integration.fsx` (its follow-up
note stands).

## Confidence

High on the server shape: it follows uc-01 step by step over ports that already exist in
outline, and the pure transition pattern of `SessionStub.present` is in place with tests. Medium
on the development proxy: two redirects cross the Vite origin, which the browser checks in PR 3.

## Steps

Each step is one PR against `master`, script-first for server and Shared code, direct edits for
the client, migration by the maintainer after review.

1. **Sealed Launch and the stub LaunchScript page.** `Server/Scripts/Launch.fsx`: `LaunchSeal`
   (mint, verify, base64url, HMAC) with tests for round trip, tampering, expiry, another key and
   garbage. Migrate to `ServerApi.Adapters.fs`. `SessionStub.present` verifies the seal instead
   of matching words (until step 2, any valid token opens with the stub prescriber; expired,
   invalid and spent become real). Routes `/stub/launch` in `Server.fs` behind `not IsProd`; the
   Vite proxy. Tests in `StubAdapterTests`. Browser: form, launch, session open; reload resumes;
   a replayed token is spent; a token from an earlier run is invalid.
2. **Identity hop, core.** `Server/Scripts/Hop.fsx`: the ports, the LaunchRecord by nonce with
   state, expiry, key and outcome, pure `present` and `callback` with the refusal ladder, the
   Rule 45 replay, the Rule 40 single act and the Rule 8 closes, the three stubs, tests over every
   refusal, the replay, the Reader opening without a PIN, and `read = None` opening without
   imported data. Migrate to `Ports.fs`, `Adapters.fs`, `CompositionRoot.fs`.
3. **Identity hop, edge.** `Server.fs`: `/authorize`, `/callback`, the `genpres_launch_state` and
   `genpres_stub_identity` cookies, `sessionDisabled.callback`; `HttpTests` for the state cookie;
   the client's `invalid` word; the identity select on the stub page. Browser, isolated contexts:
   every identity choice reaches the matching gate or an open session; a callback reload gets the
   same answer; a missing state cookie is invalid; two tabs (ext 8b).
4. **Endings.** Endings in the store; `GetSession` answers `SessionEnded`; `Api.fs` gains
   `SessionResponse.SessionEnded` and `SessionEnding`; the client gains `Session.Ended`, a
   `Resume` result, gate texts, machine and gate tests; three `Terms` script-first plus sheet
   rows. Browser: a launch in tab A, the same user in tab B; B is open, A's next `GetSession`
   shows the ending.
5. **Docs.** uc-01 gets an as-built note naming what each stub stands in for; plan 409's "Still
   open" is updated; `DEVELOPMENT.md` describes the stub launch page, the development proxy and
   the two stub cookies; this plan gets its as-built table.

## Acceptance

- Against `GENPRES_PROD=0 dotnet run`: `/stub/launch` with `prescriber` ends on `#/session` with
  the stub prescriber in the title bar and the session cookie set; the network shows
  `PresentLaunch` answering `RedirectTo`, two redirects, then `GetSession`. `reader` opens a
  session too, as a Reader. `prescriber-other-patient`, `no-pin`, `unknown` and `none` land on
  the gate with their texts; `none` offers no retry, the Launch is gone after the redirect.
  PatientId `no-data` opens a session whose patient carries no imported data.
- The same token replayed in the same browser context gets the same answer; in another context
  it is spent. A reloaded `/callback?code&state` gets the same answer. History holds `#/session`
  only (the omnibox residue is #599).
- With `GENPRES_PROD=1` and a valid password: `/stub/launch` and `/authorize` are 404 and
  `/callback` refuses `invalid`.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.
