# Implementation plan for issue 582: serve the client-side knowledge rules through the server

The emergency list, the continuous medication list and the growth references are the only
knowledge rules that never pass through the server. The browser fetches them from Google Sheets
by an id compiled into the bundle. This plan routes them through the resource provider and the
command dispatcher like every other rule, so that the scope switch of #580 covers them and a
deployment can point them elsewhere. The calculations stay in the browser; only the data path
moves.

## Problem description

`src/Informedica.GenPRES.Client/Utils.fs` (`GoogleDocs`) fetches seven sheets straight from
Google, from two ids hard-coded in the client:

| Sheet | Workbook | Parser | Used by |
|---|---|---|---|
| `emergencylist` | emergency-list workbook | `EmergencyTreatment.parse` | LifeSupport page |
| `continuousmeds` | emergency-list workbook | `ContinuousMedication.parse` | ContinuousMeds page |
| `products` | emergency-list workbook | `Products.parse` | nothing: `State.Products` is set and never read |
| `weight`, `height`, `weight neo`, `height neo` | emergency-list workbook | `NormalValues.parse` | patient weight and height estimation, every page |
| `Localization` | a second workbook | none (raw `string[][]`) | UI text, every page |

Consequences, as the issue states them:

- The rules bypass `GenFORM.Lib` resource loading, the `CachedResourceProvider`, `ReloadResources`
  and `GetResourceInfo`. A sheet edit reaches every browser on its next load, with no cache and no
  admin control.
- `GENPRES_URL_ID` does not cover them; a deployment cannot point them elsewhere. ADR-0001 records
  the split.
- The scope gate of #580 sits in `processCmd`. Rules the server never serves cannot be withheld
  there, so the LifeSupport and ContinuousMeds pages are hidden client-side only. The #580 plan
  lists this as a known limit.
- The server's Content Security Policy must allow `connect-src https://docs.google.com` for the
  browser fetch, a hole in the DMZ that ADR-0001 describes.

Two facts established while writing this plan, by fetching the public demo workbooks:

- The Google `gviz` endpoint answers a request for a tab that does not exist with the **first**
  tab of the workbook and HTTP 200. The main demo workbook has no `emergencylist`,
  `continuousmeds` or growth tabs; its `products` tab is GenFORM's product sheet, a different
  sheet under the same name. So the emergency-list sheets cannot simply be read from
  `GENPRES_URL_ID` today, and a loader must fail on a wrong header row rather than accept
  whatever comes back. `Shared.Csv.getColumn` already raises `KeyNotFoundException` on a missing
  column, which gives that check for free.
- The main demo workbook **does** carry a `Localization` tab, so UI text can come from
  `GENPRES_URL_ID` without a second id.

## Approaches considered

1. **Keep the client fetch, gate it client-side.** Rejected in the issue: the fetch and the
   calculation are in a public bundle, so nothing is withheld.
2. **A plain server proxy for the sheet CSV.** Rejected in the issue: no caching, no reload, no
   resource info, and a second loading path next to the resource provider.
3. **Add the sheets to `Resources.defaultRegistry` in `GenFORM.Lib`, with GenFORM-owned record
   types, and map to the `Shared` types in the server.** Rejected: `GenFORM.Lib` is a Core
   project and may not reference the Contract ring (`scripts/DependencyRule.fsx`), so the
   `Shared.Types` records the client already consumes would have to be duplicated and mapped.
   It would also add to the Google-Sheets loaders that #378 phase 2 is evicting from GenFORM.
4. **Extend `defaultRegistry` from the server and read the new keys through the one
   `CachedResourceProvider`.** Smallest change, and the first draft of this plan. Rejected on
   review: that provider caches an all-or-nothing `LoadedResources`, so a fatal formulary load
   failure empties the emergency list as well, and every `KnowledgeCmd` has to sit behind
   `requireLoaded`. Today the browser fetch is independent of the formulary, and an emergency
   page must not lose that.
5. **A second `CachedResourceProvider` over a registry of its own.** Rejected: `LoadedResources`
   and `ResourceState` are shaped after the formulary, so a second instance would carry empty
   formulary fields and an `IResourceProvider` whose members mean nothing for these sheets.
6. **A small server-side provider over `LoadEngine`, with its own registry, cache and reload.**
   `ResourceRegistry` is a plain `Map` and `LoadEngine` resolves whatever is registered, so the
   server, which is in the DMZ and already references `Shared`, `Utils.Lib` and `GenFORM.Lib`,
   builds a registry of the four keys and wraps the engine in the same lock-cache-reload shape
   as `CachedResourceProvider`, about forty lines. The parsers stay in `Shared/Models.fs`,
   unchanged, and run on the server. The formulary provider, GenFORM and `requireLoaded` are
   untouched; `ReloadResources` reloads both. Chosen.

## Chosen approach

### Contract

A new command family in `src/Informedica.GenPRES.Shared/Api.fs`:

```fsharp
    | KnowledgeCmd of KnowledgeCommand

and KnowledgeCommand =
    | GetEmergencyList
    | GetContinuousMeds
    | GetNormalValues
    | GetLocalization

// Response
    | KnowledgeResp of KnowledgeResponse

and KnowledgeResponse =
    | EmergencyListLoaded of BolusMedication list
    | ContinuousMedsLoaded of ContinuousMedication list
    | NormalValuesLoaded of NormalValues
    | LocalizationLoaded of string[][]
```

`Command.toString` gains one line per case. The payload types are the `Shared.Types` records the
client already holds in its state, so nothing changes downstream of the fetch.

`GetProducts` from the issue is deliberately absent: the `products` sheet is parsed into
`State.Products` and read nowhere in the client. The plan removes that dead load rather than
porting it. If a use turns up, it is one more case in the family.

`Localization` is not a knowledge rule, but it is the last client-side fetch. Serving it is what
lets the `docs.google.com` entry leave the CSP, so it is in scope, as the last and separately
droppable step.

### Scope classification (#580)

`Feature.ofCommand` is total over `Command`, so the family must be classified:

| Command | Feature |
|---|---|
| `GetEmergencyList` | `EmergencyList` |
| `GetContinuousMeds` | `ContinuousMeds` |
| `GetNormalValues` | `Patient` (new case) |
| `GetLocalization` | `Patient` |

`Feature.Patient` is new: patient data entry with weight and height estimation, which every page
uses. It goes into the placeholder accredited list. Localization maps there too because UI text
is not a function the MDR file will list, and a feature for it would exist only to be always on.
If the reviewer prefers a dedicated always-on case, it is one line.

Whichever of #580 step 1 and step 1 below lands second adds the classification and its
`ofCommand` totality tests. Once both are in, the "client-only" limit in the #580 plan goes away
with no change to the gate.

### Server configuration

`Config.Settings` gains `EmergencyUrlId: string`, read from `GENPRES_EMERGENCY_URL_ID` through
`nonBlank`, and shown redacted in the banner. When unset it defaults to the id of the workbook
every deployment reads today, the one compiled into the client as `dataEMLUrlId`, held as one
literal in `Config`. That is the "hard-coded default in source code" tier of the priority order
in `DEVELOPMENT.md`, and it is what makes the cutover safe: a deployment that upgrades without
touching its configuration keeps the emergency list, the continuous medication list and the
growth references it had, because the server fetches the same workbook the browser did. A
deployment whose sheet owners move the tabs into the main workbook sets the variable to the same
value as `GENPRES_URL_ID`; one that keeps a workbook of its own sets it to that. `validateStartup`
does not require it: a wrong workbook degrades to an empty list with a warning, see below, and
never stops the server.

The default must not be `GENPRES_URL_ID`: the main workbook has no emergency tabs, so that
default would turn an unconfigured upgrade into an empty emergency page. The first draft of
this plan had it that way and review caught it.

`.env.example`, the `Dockerfile` and `compose.yaml` carry the same id explicitly, the way they
carry the demo `GENPRES_URL_ID`, so the value is visible where operators look and a bare
`docker run` or `docker compose up` shows the emergency list. `Build.fs` `DockerRun` forwards the
variable when set. The rule that a proprietary id is injected at runtime and never baked applies
to this id as well.

### The knowledge provider

A new server file `ServerApi.Knowledge.fs`, compiled before `ServerApi.Adapters.fs`:

```fsharp
module Keys =
    let emergencyList = ResourceKey.create<BolusMedication list> "emergencyList"
    let continuousMeds = ResourceKey.create<ContinuousMedication list> "continuousMeds"
    let normalValues = ResourceKey.create<NormalValues> "normalValues"
    let localization = ResourceKey.create<string[][]> "localization"

/// The registry of the four sheets: `emergencyUrlId` for the first three, `urlId` for
/// the localization tab.
let registry urlId emergencyUrlId : ResourceRegistry = ...

/// Lazy, locked, reloadable snapshot of a registry, the shape of
/// `CachedResourceProvider` without its formulary-specific state.
type KnowledgeProvider(registry: ResourceRegistry) =
    member _.Get(key: ResourceKey<'T>) : 'T      // loads on first use
    member _.Reload() : unit                     // drops the snapshot, loads again
    member _.Warnings : Message list             // from the last load
```

`KnowledgeProvider` runs `LoadEngine registry`, calls `ForceAll`, and keeps `Resolved` and
`Warnings` under a lock, exactly as `CachedResourceProvider.getFromCache` and `ReloadCache` do.
It never touches `LoadedResources` or `ResourceState`, which is why it is a server type and not
a second GenFORM provider. Nothing loads at type initialisation: the first `Get` loads, as with
the formulary provider (AGENTS.md, "Never Perform IO in a Top-Level `let` Value").

Each loader is `ofResultOrDefault`, the pattern `Keys.totalsData` uses: a failed fetch or a
header row from the wrong tab yields the empty value plus a `Warning` naming the sheet and the
id, so the failure is visible in the server log, a `ReloadResources` retries it, and one bad
sheet never empties the others. The fetch is `Web.GoogleSheets.getDataFromSheet` with the
composed parser (`Shared.Csv.parseCSV >> EmergencyTreatment.parse` and so on), run synchronously
inside the loader thunk. Using the `Shared` CSV parser keeps the server's parse identical to what
the browser did.

The four growth sheets become one resource: the loader fetches all four and builds
`NormalValues`, as `GoogleDocs.loadNormalValues` does today, and a failure in any one of them
fails the resource as a whole, as today.

`Host` in `Server.fs` builds `KnowledgeProvider (Knowledge.registry urlId emergencyUrlId)` next
to `resourceProvider urlId` and passes both into `createServerApi`, `CompositionRoot.compose` and
`Adapters.makeAppEnv`. Its warnings are logged where `Api.getCachedProviderWithDataUrlId` logs the
GenFORM messages, at start-up and after each reload. GenFORM itself is not changed. The MCP host
keeps its `getCachedProviderWithDataUrlId`: it does not serve these sheets, and its own scope
question is out of this plan.

### Port and dispatcher

`ServerApi.Ports.fs`:

```fsharp
type KnowledgePort =
    {
        getEmergencyList: unit -> Async<Result<BolusMedication list, string[]>>
        getContinuousMeds: unit -> Async<Result<ContinuousMedication list, string[]>>
        getNormalValues: unit -> Async<Result<NormalValues, string[]>>
        getLocalization: unit -> Async<Result<string[][], string[]>>
    }
```

`AppEnv` gains `knowledge: KnowledgePort`; `Adapters.makeAppEnv` implements it as
`knowledge.Get Knowledge.Keys.x` inside a `try`, so an exception from a loader is an `Error`,
never an unhandled one. `processCmd` gets four arms in the group that bypasses `requireLoaded`,
next to `InteractionCmd GetDrugNames`, which reads a source of its own in the same way. A fatal
formulary load therefore refuses the order-context, formulary and nutrition families as it does
today and leaves the emergency list, the continuous medication list, the growth references and
the UI text served, which is what the browser fetch gives today. When #580 lands, the scope
gate still runs ahead of this bypass, as its plan requires.

`ReloadResources` reloads both. The order-context adapter in `Adapters.makeOrderContextPort`
matches `ReloadResources _` and, once the GenFORM reload has returned `Ok`, calls
`knowledge.Reload()` and logs its warnings. Reload stays one admin action with one password
check, and the settings page needs no change.

### Client

- `App.fs`: `LoadBolusMedication Started`, `LoadContinuousMedication Started` and
  `LoadNormalValues Started` dispatch `serverApi.processCommand (KnowledgeCmd ...)` instead of
  `GoogleDocs.load*`, and match the `KnowledgeResp` case; the `Msg` cases, the `Finished`
  handlers and the state fields are unchanged. The server's `string[]` error is joined into the
  `string` the handlers log today.
- `LoadProducts`, `State.Products` and `Products.parse`'s only caller go. `Products.parse` and
  the `Product` type stay in `Shared` until the maintainer decides on them; they are pure.
- `LoadLocalization Started` does the same with `GetLocalization`, in the last step.
- `GoogleDocs` is then empty and is deleted, together with the `Fable.SimpleHttp` reference in
  the client's `paket.references`, whose only user it was.
- Where the loads are dispatched depends on order of landing. Today they are in `init`. The #580
  plan moves the emergency and continuous loads into the settings-resolved handler, gated by
  feature; if #580 step 3 lands first, this plan's client step edits that handler instead of
  `init`. Either way the fetch is now a command, so an accredited server refuses it with
  `NOT_IN_SCOPE` whatever the client does.

### Documentation

- ADR-0001: the "Note that `GENPRES_URL_ID` selects the *server's* sheet only" remark under the
  Google Spreadsheets decision, and the consequence that "the emergency-list and
  continuous-medication pages ... will route through the server ... the last step of the
  migration" are amended to record that the data path now goes through the server and the
  calculations still run in the browser.
- `docs/implementation-plans/378-dependency-rule.md` phase 6 item 1 is this plan; mark it and
  link here. Item 3 there (calculations server-side, own issue, MDR validation) is the optional
  second phase of #582 and stays out of this plan.
- `docs/implementation-plans/580-scope-switch.md`: the first "known limit" paragraph is
  replaced by one sentence pointing here.
- `DEVELOPMENT.md`: `GENPRES_EMERGENCY_URL_ID` in the environment block, the default rule, and a
  line in the Docker section.
- The comment on `dataEMLUrlId` in `Client/Utils.fs` goes with the module.

### Consequences, stated rather than hidden

- **A sheet edit now takes effect on `ReloadResources` or restart, not on the next browser
  load.** This is the point of the change, and it is a change in operating procedure for whoever
  edits the emergency-list workbook.
- **The emergency list and the formulary stay independent, in both directions.** A fatal
  formulary load leaves the four knowledge commands served, because they bypass `requireLoaded`
  and read their own provider; a bad emergency-list workbook is a warning on that provider and
  never reaches the formulary. This is the independence the browser fetch has today, kept. The
  price is a second, small provider and a two-line fan-out in the reload path.
- **An unconfigured upgrade keeps its data.** The default emergency-list id is the workbook the
  client reads today, so a deployment that sets nothing new sees no change in content, only in
  the path the data takes.
- **A wrong or stale emergency-list id is a warning, not a crash.** Because Google returns the
  first tab for an unknown tab name, the parser's `KeyNotFoundException` on the header row is
  what turns "wrong workbook" into an `Error`; without it the server would serve the formulary's
  product sheet as the emergency list. The Shared tests below pin that behaviour.
- **The payload is served per client start**, not per sheet edit; the lists are a few hundred
  rows and go through the same gzip and Fable.Remoting path as the formulary.
- **The `products` sheet stops being fetched.** Nothing reads it.

## Confidence

High on the mechanism: every piece follows a pattern already in the code base (`ofResultOrDefault`
for optional sheets, `ResourceKey` and `LoadEngine` for typed, memoised access, the
`CachedResourceProvider` lock-and-reload shape, a port record and a `processCmd` arm per command
family, the `GetDrugNames` bypass, `Config.fromEnv` for the setting, `StubAdapterTests` for the
dispatcher). Medium on one decision that is not a software decision and is called out for the
reviewer: where `GetNormalValues` and `GetLocalization` sit in the accredited list.

## Steps

Each step is one PR under 200 changed lines. Shared and Server changes are drafted in `.fsx`
scripts and migrated by the maintainer; client files are edited directly.

1. **Shared contract.** `KnowledgeCmd`, `KnowledgeCommand`, `KnowledgeResp`, `KnowledgeResponse`,
   `Command.toString`; if #580 step 1 has landed, `Feature.Patient` and the four `ofCommand`
   lines with their totality tests. Tests in `tests/Informedica.GenPRES.Shared.Tests`, the first
   for these parsers: each of `EmergencyTreatment.parse`, `ContinuousMedication.parse` and
   `NormalValues.parse` parses a two-row fixture with exactly its declared columns, and raises
   `KeyNotFoundException` on a header row from another sheet (the `gviz` first-tab fallback).
   The fixture columns are the ones the parsers read today; this is the column-contract idea
   from `GenFORM.Tests` applied to `Shared`.
2. **Server configuration and provider.** `Settings.EmergencyUrlId` with the literal default,
   `fromEnv`, banner line; `ServerApi.Knowledge.fs` with the keys, `registry` and
   `KnowledgeProvider`; `Host` constructing it. Tests: `ConfigTests` (unset and blank give the
   default id, a set value wins); `KnowledgeProviderTests` over an in-memory registry, in the
   style of `ResourceErrorTests`: a failing loader yields the empty value and a `Warning` naming
   the sheet while the other keys load; a wrong-header response yields the same; `Get` loads
   once and `Reload` loads again; a loader that raises does not poison the provider.
3. **Port and dispatcher.** `KnowledgePort`, `AppEnv.knowledge`, the adapter including the
   reload fan-out, the four `processCmd` arms. Tests in `StubAdapterTests`: `makeEnv` gains the
   port; each command routes to its response; a not-loaded env still serves all four, proving
   the bypass, as the existing `GetDrugNames` test does; once #580 is in, an accredited env
   refuses `GetEmergencyList` and `GetContinuousMeds` with `NOT_IN_SCOPE` ahead of the bypass
   and passes `GetNormalValues`.
4. **Client: emergency list, continuous medication, normal values.** The three loads go through
   `processCommand`; `LoadProducts` and `State.Products` are removed; `GoogleDocs` keeps only
   `loadLocalization`.
5. **Client: localization, and the CSP.** `LoadLocalization` through `processCommand`;
   `GoogleDocs` and `Fable.SimpleHttp` deleted; `https://docs.google.com` dropped from
   `connect-src` in `Server.fs`, with the comment that anticipates it. `HttpTests` gets the
   assertion that the header no longer names it.
6. **Configuration and documentation.** `.env.example`, `Dockerfile`, `compose.yaml`, `Build.fs`
   `DockerRun`, `DEVELOPMENT.md`, the ADR-0001 amendment, the #378 and #580 plan updates.

Steps 1 to 3 are server-only and change no behaviour until step 4; the client keeps fetching from
Google until then, so each step ships green on its own.

## Verification

- `dotnet run` with `.env` from `.env.example`: the LifeSupport and ContinuousMeds pages show data;
  a patient without a weight gets an estimated weight; the Network tab shows no request to
  `docs.google.com` (after step 5), and one `KnowledgeCmd` request per sheet family at start.
- The server log shows the four sheets loaded with no warnings; `ReloadResources` from the
  settings page reloads them, and an edit to the public emergency-list workbook shows up after
  the reload and not before.
- `GENPRES_EMERGENCY_URL_ID` unset: the LifeSupport page shows the same data as before the
  upgrade. Set to the main demo workbook id: the server starts, the LifeSupport page is empty,
  the log carries a warning naming `emergencylist` and the id, and the formulary is unaffected.
  Set back to the emergency-list id: data again after `ReloadResources`.
- `GENPRES_URL_ID` set to a wrong id: the formulary commands return the `requireLoaded`
  messages, and the LifeSupport page still shows data.
- `curl` of `KnowledgeCmd GetEmergencyList` against `/api/...` returns the list; against a server
  whose formulary load failed, it still returns the list; against an accredited server (with
  #580), `NOT_IN_SCOPE`.
- `curl -sI http://localhost:8085/ | grep -i content-security-policy` shows no `docs.google.com`
  after step 5.
- `docker compose up` with no `GENPRES_EMERGENCY_URL_ID` in `.env`: the container serves the
  demo emergency list from the baked-in id.
- `dotnet run ServerTests` passes with the new Shared, Config, provider and stub tests;
  `dotnet fsi scripts/CheckDependencyRule.fsx` still passes, since every change is in the DMZ
  or the Contract ring and no Core project is touched; `dotnet run MarkdownLint` is clean for
  this document.
