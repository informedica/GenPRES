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
4. **Extend the registry from the server.** `ResourceRegistry` is a plain `Map`, `LoadEngine`
   resolves whatever is registered, and `CachedResourceProvider` takes the load function as a
   parameter. The server, which is in the DMZ and already references `Shared`, `Utils.Lib` and
   `GenFORM.Lib`, adds its own keys and loaders to `defaultRegistry` and reaches them through
   `provider.Get key`. The parsers stay in `Shared/Models.fs`, unchanged, and run on the server.
   Chosen.
5. **A second `CachedResourceProvider` for the emergency-list workbook**, so a formulary load
   failure does not take the emergency list down. Considered and not chosen for now:
   `ReloadResources` and `GetResourceInfo` would have to fan out over two providers, and the
   coupling it avoids is stated below as a consequence instead. It remains the fallback if that
   consequence is judged unacceptable.

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

`Config.Settings` gains `EmergencyUrlId: string option`, read from `GENPRES_EMERGENCY_URL_ID`
through `nonBlank`, and shown redacted in the banner. When unset it defaults to `UrlId`, so a
deployment whose sheet owners move the emergency tabs into the main workbook needs no second
variable, which is the issue's first option, while a deployment that keeps them separate sets the
variable, the issue's second option. `validateStartup` does not require it: a missing or wrong
workbook degrades to an empty list with a warning, see below, and never stops the server.

`.env.example`, the `Dockerfile` and `compose.yaml` carry the current public emergency-list id
the same way they carry the demo `GENPRES_URL_ID`, so a fresh clone, a bare `docker run` and
`docker compose up` keep showing the emergency list. `Build.fs` `DockerRun` forwards the variable
when set. The rule that the proprietary id is injected at runtime and never baked applies to
this id as well.

### Registry extension

A new server file `ServerApi.Knowledge.fs`, compiled before `ServerApi.Adapters.fs`:

```fsharp
module Keys =
    let emergencyList = ResourceKey.create<BolusMedication list> "emergencyList"
    let continuousMeds = ResourceKey.create<ContinuousMedication list> "continuousMeds"
    let normalValues = ResourceKey.create<NormalValues> "normalValues"
    let localization = ResourceKey.create<string[][]> "localization"

/// Adds the four loaders to a registry. `emergencyUrlId` for the first three, `urlId`
/// for the localization tab.
let addTo urlId emergencyUrlId (registry: ResourceRegistry) : ResourceRegistry = ...
```

Each loader is `ofResultOrDefault`, the pattern `Keys.totalsData` uses: a failed fetch or a
header row from the wrong tab yields the empty value plus a `Warning` naming the sheet and the
id, so the failure is visible in `GetResourceInfo` and on the settings page, a
`ReloadResources` retries it, and the formulary never goes down because the emergency-list
workbook did. The fetch is `Web.GoogleSheets.getDataFromSheet` with the composed parser
(`Shared.Csv.parseCSV >> EmergencyTreatment.parse` and so on), run synchronously inside the
loader thunk, never in a top-level value (AGENTS.md, "Never Perform IO in a Top-Level `let`
Value"). Using the `Shared` CSV parser keeps the server's parse identical to what the browser
did.

The four growth sheets become one resource: the loader fetches all four and builds
`NormalValues`, as `GoogleDocs.loadNormalValues` does today, and a failure in any one of them
fails the resource as a whole, as today.

`GenFORM.Lib/Api.fs` gets one function, `getCachedProviderWithRegistry logger registry`, and
`getCachedProviderWithDataUrlId` becomes a call to it with `defaultRegistry dataUrlId`. That is
the only change in a Core project; it exists because the message logging in that module is
private. `Host.resourceProvider` in `Server.fs` then builds
`Resources.defaultRegistry urlId |> Knowledge.addTo urlId emergencyUrlId` and passes it in. The
MCP host keeps calling `getCachedProviderWithDataUrlId`: it does not serve these sheets, and its
own scope question is out of this plan.

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
`provider.Get Knowledge.Keys.x`. `processCmd` gets four arms in the `requireLoaded` branch, next
to `FormularyCmd`. They sit behind `requireLoaded` on purpose: when the main load fails,
`CachedResourceProvider` caches an empty `LoadedResources` whose `Resolved` map is empty, and a
`Get` on it would throw. The consequence is stated below.

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
- **The emergency list is unavailable when the formulary fails to load.** Today the two are
  independent because the browser fetches the sheet itself; after this plan a fatal formulary
  load failure refuses every command, `KnowledgeCmd` included. The reverse is guarded: the
  emergency-list loaders are optional and cannot fail the formulary. If that coupling is not
  acceptable for a page used in emergencies, approach 5 (a second provider) is the remedy, at
  the cost of fanning `ReloadResources` and `GetResourceInfo` over two providers.
- **A wrong or stale emergency-list id is a warning, not a crash.** Because Google returns the
  first tab for an unknown tab name, the parser's `KeyNotFoundException` on the header row is
  what turns "wrong workbook" into an `Error`; without it the server would serve the formulary's
  product sheet as the emergency list. The Shared tests below pin that behaviour.
- **The payload is served per client start**, not per sheet edit; the lists are a few hundred
  rows and go through the same gzip and Fable.Remoting path as the formulary.
- **The `products` sheet stops being fetched.** Nothing reads it.

## Confidence

High on the mechanism: every piece follows a pattern already in the code base (`ofResultOrDefault`
for optional sheets, `ResourceKey` and `provider.Get` for typed access, a port record and a
`processCmd` arm per command family, `Config.fromEnv` for the setting, `StubAdapterTests` for the
dispatcher). Medium on two decisions that are not software decisions and are called out for the
reviewer: whether the formulary-to-emergency-list coupling is acceptable, and where
`GetNormalValues` and `GetLocalization` sit in the accredited list.

## Steps

Each step is one PR under 200 changed lines. Shared, Server and GenFORM changes are drafted in
`.fsx` scripts and migrated by the maintainer; client files are edited directly.

1. **Shared contract.** `KnowledgeCmd`, `KnowledgeCommand`, `KnowledgeResp`, `KnowledgeResponse`,
   `Command.toString`; if #580 step 1 has landed, `Feature.Patient` and the four `ofCommand`
   lines with their totality tests. Tests in `tests/Informedica.GenPRES.Shared.Tests`, the first
   for these parsers: each of `EmergencyTreatment.parse`, `ContinuousMedication.parse` and
   `NormalValues.parse` parses a two-row fixture with exactly its declared columns, and raises
   `KeyNotFoundException` on a header row from another sheet (the `gviz` first-tab fallback).
   The fixture columns are the ones the parsers read today; this is the column-contract idea
   from `GenFORM.Tests` applied to `Shared`.
2. **Server configuration and registry.** `Settings.EmergencyUrlId`, `fromEnv` with the
   default-to-`UrlId` rule, banner line; `ServerApi.Knowledge.fs` with the keys and `addTo`;
   `Api.getCachedProviderWithRegistry` in GenFORM with `getCachedProviderWithDataUrlId` delegating
   to it; `Host.resourceProvider settings`. Tests: `ConfigTests` (unset and blank default to
   `UrlId`, set value wins); a `ResourceErrorTests`-style test that a registry extended with a
   failing emergency-list loader still loads with `IsLoaded = true` and a `Warning` naming the
   sheet, and that a wrong-header response yields the same.
3. **Port and dispatcher.** `KnowledgePort`, `AppEnv.knowledge`, the adapter, the four
   `processCmd` arms. Tests in `StubAdapterTests`: `makeEnv` gains the port; each command routes
   to its response; a not-loaded env refuses all four with the `requireLoaded` messages; once
   #580 is in, an accredited env refuses `GetEmergencyList` and `GetContinuousMeds` with
   `NOT_IN_SCOPE` and passes `GetNormalValues`.
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
- The settings page's resource info lists the four resources with no warnings; `ReloadResources`
  reloads them, and an edit to the public emergency-list workbook shows up after the reload and
  not before.
- `GENPRES_EMERGENCY_URL_ID` set to the main demo workbook id: the server starts, the LifeSupport
  page is empty, resource info carries a warning naming `emergencylist` and the id, and the
  formulary is unaffected. Unset the variable: the same, since it defaults to `GENPRES_URL_ID`.
  Set it to the emergency-list id: data again after `ReloadResources`.
- `curl` of `KnowledgeCmd GetEmergencyList` against `/api/...` returns the list; against a server
  whose main load failed, it returns the `requireLoaded` messages; against an accredited server
  (with #580), `NOT_IN_SCOPE`.
- `curl -sI http://localhost:8085/ | grep -i content-security-policy` shows no `docs.google.com`
  after step 5.
- `docker compose up` with no `GENPRES_EMERGENCY_URL_ID` in `.env`: the container serves the
  demo emergency list from the baked-in id.
- `dotnet run ServerTests` passes with the new Shared, Config, resource-error and stub tests;
  `dotnet fsi scripts/CheckDependencyRule.fsx` still passes, since the server is in the DMZ and
  the only Core change is one function in `GenFORM.Lib/Api.fs` that does no IO itself;
  `dotnet run MarkdownLint` is clean for this document.
