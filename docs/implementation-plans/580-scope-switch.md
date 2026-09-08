# Implementation plan for issue 580: scope switch

One trunk, two surfaces. The production server exposes only the MDR-accredited parts of GenPRES;
development, test and acceptance servers expose the full suite. The difference is a runtime
setting enforced by the server, so the code base keeps evolving on `master` and no release
branch has to be maintained and back-ported separately.

## Problem description

Production may only show the accredited parts of GenPRES, while the other environments need the
full suite to develop, test and accept what comes next. Today nothing in GenPRES can express
that distinction:

- `GENPRES_PROD` selects the data set (production cache files versus demo files,
  `ZIndex.Lib/FilePath.fs`), enforces the admin password policy at startup (`Server.fs`), and
  stamps `DemoVersion` on order-context responses (`ServerApi.Services.fs`). It gates no command
  family and no page.
- The client's `IsDemo` flag is initialised to `false` and never updated, so the "DEMO VERSION!"
  title suffix in `Pages/GenPres.fs` is dead code, and the `DemoVersion` field the server sends
  is read nowhere.
- The command dispatcher in `ServerApi.Command.fs` has one gate, `requireLoaded`, which only
  checks that resources are loaded.

Without a switch, the only way to keep unaccredited work out of production is a separate release
branch, which is exactly the split this plan avoids.

## Approaches considered

1. **Reuse `GENPRES_PROD` as the feature gate.** Rejected: it conflates data with scope, and would
   force acceptance onto demo data. Acceptance must run the real formulary with the full suite.
2. **Hide the unaccredited parts client-side only.** Rejected: the API stays reachable from any
   browser, so nothing is actually withheld.
3. **Separate builds or branches for production and the rest.** Rejected: that is the split of
   the release line this plan exists to prevent.
4. **A runtime scope switch, enforced in the server's command dispatcher and mirrored in the
   client from a server-provided setting.** Chosen.

## Chosen approach

### The setting

`GENPRES_SCOPE` with two values, `accredited` and `full`.

- Parsed in `Server.Config.fromEnv` into a new `Settings.Scope` field, next to `IsProd`.
- Default when unset or blank (`nonBlank`, as for the other secrets): `accredited` if `IsProd`,
  `full` otherwise. The safe direction: a production server that forgets the setting shows less,
  never more. No launch path may supply its own fallback, or the derivation is lost.
- Any other value is rejected in `Config.validateStartup`, so the server refuses to start, in the
  same way it refuses a short production password.
- `Settings` is threaded into `createServerApi` and `CompositionRoot.compose`, which today drop
  it. That also gives the command layer a home for the other settings it currently reads from the
  environment directly.

### One list, shared by client and server

In `src/Informedica.GenPRES.Shared`, so both sides compile against the same definition:

```fsharp
[<RequireQualifiedAccess>]
type Scope =
    | Accredited
    | Full

/// A part of GenPRES that the scope switch can withhold. One case per command family or
/// client-only page; the accredited set is a single list below.
[<RequireQualifiedAccess>]
type Feature =
    | EmergencyList        // LifeSupport page; client-side sheets until #582
    | ContinuousMeds       // page; client-side sheets until #582
    | Prescribe            // OrderContextCmd
    | OrderPlan            // OrderPlanCmd
    | Nutrition            // NutritionPlanCmd
    | Formulary            // FormularyCmd
    | Parenteralia         // ParenteraliaCmd
    | Interactions         // InteractionCmd
    | Admin                // LogAnalyzerCmd, ReloadResources, Settings page

module Feature =
    /// THE accredited list. Input from the MDR file, kept outside this repository; the
    /// placeholder below is to be replaced before the first production deployment.
    let accredited: Set<Feature> = set [ Feature.Prescribe; Feature.Formulary; Feature.Admin ]

    let isPermitted scope feature =
        match scope with
        | Scope.Full -> true
        | Scope.Accredited -> accredited |> Set.contains feature

    /// Total over Command: every case maps to exactly one Feature.
    let ofCommand: Api.Command -> Feature = ...
```

`Admin` stays permitted in both scopes because the settings page, log analysis and resource
reload are operations tooling, not clinical function. If the MDR file says otherwise, it is one
line in the list.

The client learns the scope through one new remoting method, which also carries the demo flag so
the dead `IsDemo` plumbing can be repaired instead of removed:

```fsharp
type ServerSettings = { Scope: Scope; IsDemo: bool }

// IServerApi addition
getSettings: unit -> Async<ServerSettings>
```

### Server enforcement

- `AppEnv` (`ServerApi.Ports.fs`) gains `scope: Scope`. `Adapters.makeAppEnv` takes it from
  `Settings`.
- `Command.processCmd` checks `Feature.ofCommand cmd |> Feature.isPermitted env.scope` as its
  first operation, before the `match` that today lets four commands bypass `requireLoaded`
  (`InteractionCmd GetDrugNames`, `ValidatePassword`, `ListLogFiles`, `AnalyzeLogFile`) and
  before `requireLoaded` itself. A refused command returns `Error [| "NOT_IN_SCOPE" |]`, a stable
  code the client can recognise, and touches no port. The existing `requireLoaded`
  short-circuit is the pattern.
- `getSettings` returns `{ Scope = settings.Scope; IsDemo = not settings.IsProd }`.
- `setDemoVersion` in `ServerApi.Services.fs` keeps reading the environment for now; folding it
  into `Settings` is a follow-up once the command layer has them.

### Client

- `State.Settings: Deferred<ServerSettings>`, fetched in `init` next to `checkServer`. Until it
  resolves the client behaves as `Accredited`: a short flash of the smaller menu in full mode is
  preferable to a flash of the larger one in production.
- The start page is normalised when settings resolve. `init` today lands on `LifeSupport`
  whether or not a `pg` parameter is present; when that page is withheld, the settings handler
  moves `Page` to the first permitted page, so an accredited client opened at the bare url never
  renders a withheld view.
- The client-side sheet loads (`LoadBolusMedication`, `LoadContinuousMedication`) run only when
  their feature is permitted. In accredited scope those sheets are neither shown nor fetched.
- `Global.Feature.ofPage: Pages -> Feature` maps every page to its feature.
- `Pages/GenPres.fs`: the `pages` list is filtered by `Feature.isPermitted scope`, so withheld
  pages are removed from the side menu, not greyed out like the unauthenticated Settings entry.
  The index bookkeeping for `interactionsIndex`, `formularyIndex` and `settingsIndex` then works
  on the filtered list.
- `App.fs` `UpdatePage` refuses a page outside the scope, in the same guard that refuses
  `Settings` when not authenticated. The `pg` URL parameter goes through the same guard, so a
  deep link to a withheld page lands on the first permitted page.
- `IsDemo` is set from `Settings.IsDemo`, which makes the existing title suffix live.
- A `NOT_IN_SCOPE` error from any command shows a localized "not available in this deployment"
  snackbar rather than the raw code. New `Terms` case plus a row in the Localization sheet, with
  the English text as the built-in default.

### Configuration and documentation

- `.env.example`: `GENPRES_SCOPE=full` with a comment on the default rule.
- `Dockerfile`: no `ENV GENPRES_SCOPE`; the image inherits the default rule, so a production
  container with `GENPRES_PROD=1` is accredited unless told otherwise.
- `compose.yaml`: `GENPRES_SCOPE: ${GENPRES_SCOPE:-}` next to `GENPRES_PROD`, deliberately
  without a `full` fallback: an empty value is "unset" to `fromEnv`, so the derivation from
  `GENPRES_PROD` still applies when an operator omits the setting.
- `Build.fs` `DockerRun`: forward `GENPRES_SCOPE` when set, as it forwards the url id and password.
- `DEVELOPMENT.md`: the environment block, the wrapper-script table (a `GENPRES_SCOPE` column) and
  a short "Environments" table:

| Environment | `GENPRES_PROD` | `GENPRES_SCOPE` |
|---|---|---|
| Development | 0 | full |
| Test and acceptance | 1 | full |
| Production | 1 | accredited (default) |

### Known limits, stated rather than hidden

- `EmergencyList` and `ContinuousMeds` read their sheets from Google directly in the browser and
  compute in the browser; they need no server command. The sheets are public, so their data
  cannot be withheld by GenPRES at all; what the accredited deployment withholds is the function.
  In this plan the client neither shows nor fetches them in accredited scope, which is the
  strongest enforcement available while the client loads them itself. Serving these rules
  through the server, so the scope gate covers them like every other feature, is #582. It
  blocks the first accredited production deployment if the MDR file lists either page as
  withheld, and once it lands this limit disappears without changes to the gate.
- The MCP host (`Informedica.MCP.Server`) calls the domain libraries directly, not
  `processCmd`, so the switch does not cover it. It is a separate deployable with its own scope
  question.
- The accredited list is a placeholder until the MDR file supplies it. The mechanism does not
  depend on its content.

## Confidence

High on the mechanism: one setting, one shared list, one gate in the dispatcher, one fetch in the
client, all following patterns that already exist (`requireLoaded`, the Settings guard,
`fromEnv` tests). Medium on the placeholder list, which is not a software decision.

## Steps

Each step is one PR under 200 changed lines. Shared and Server changes are drafted in scripts and
migrated by the maintainer; client files are edited directly.

1. **Shared contract.** `Scope`, `Feature`, `Feature.accredited`, `Feature.isPermitted`,
   `Feature.ofCommand`, `ServerSettings`, `getSettings` on `IServerApi`. Tests in
   `tests/Informedica.GenPRES.Shared.Tests`: `ofCommand` is total over every `Command` case
   (one test per case, so a new command family fails the build until it is classified).
2. **Server.** `Settings.Scope`, `fromEnv` default rule, `validateStartup` rejection,
   `Settings` threaded into `compose`, `AppEnv.scope`, the `processCmd` gate, `getSettings`.
   Tests: `ConfigTests` (default derivation for both `IsProd` values, invalid value rejected),
   `StubAdapterTests` (`makeEnv` gains `scope`; an accredited env refuses a withheld command
   with `NOT_IN_SCOPE` without touching any port; a full env passes it through; each of the four
   commands that bypass `requireLoaded` is refused too when its feature is withheld, so the gate
   is proven to sit ahead of that bypass).
3. **Client.** `getSettings` fetch, `State.Settings`, `Feature.ofPage`, filtered `pages`,
   the `UpdatePage` and `pg` guards, start-page normalisation, sheet loads gated by feature,
   `IsDemo` wired, the `NOT_IN_SCOPE` snackbar and its term.
4. **Configuration and docs.** `.env.example`, `compose.yaml`, `Build.fs` `DockerRun`,
   `DEVELOPMENT.md`, and a line in `docs/roadmap/mvpap2019-gap-overview.md` pointing here and
   to #582 (client-side knowledge rules served by the server).
5. **Accredited list.** Replace the placeholder with the list from the MDR file. One-line PR,
   reviewed by whoever owns that file.

## Verification

- `GENPRES_SCOPE=accredited dotnet run`: the side menu shows only the accredited pages; the bare
  url `http://localhost:5173/` opens on the first permitted page, not on the emergency list; a
  deep link `#/patient?pg=pe` lands on the first permitted page; the Network tab shows no
  request to the emergency-list or continuous-medication sheets; `curl` of a withheld command
  family against `/api/...` returns the `NOT_IN_SCOPE` error and nothing is logged from the
  ports, including `GetDrugNames` and the log-analyzer commands when their feature is withheld.
- `docker compose up` with `GENPRES_PROD=1` and no `GENPRES_SCOPE` in the environment: the
  container runs accredited.
- `GENPRES_SCOPE=full`: all pages, all commands, unchanged behaviour.
- `GENPRES_SCOPE=bogus`: the server refuses to start with a message naming the setting.
- `GENPRES_PROD=1` with `GENPRES_SCOPE` unset: accredited. `GENPRES_PROD=0` unset: full.
- `GENPRES_PROD=0`: the title bar shows the demo suffix; with `1` it does not.
- `dotnet run ServerTests` passes with the new Shared, Config and stub tests;
  `dotnet run MarkdownLint` is clean for this document.
