# Implementation plan for issue 898

## Problem description

Plan [706](706-client-view-tier.md) puts every piece of client state in one of five tiers:
domain, communication and view, which the two order lanes hold as one record each with one
field per tier; app-level UI, which is `App.State`; and component-local, the hooks. Plan
[895](895-session-signing-view-tier.md) put the Session and the signing on the same lane
record and took the three fields `App.update` stitched beside them into the lanes, so that
`App.State` holds the four lanes and nothing else of them.

`App.State` (`src/Informedica.GenPRES.Client/App.fs`) still lists its thirty-six fields flat
([#898](https://github.com/informedica/GenPRES/issues/898)): the four lanes; the patient
they are for and the draft the panel edits; fifteen `Deferred` fields, the reading of a plain
fetch each, three of them the admin's, with a retry counter beside one of them; the admin
login, its token and its attempt counter; the page, the disclaimer, the language and the hospital, the demo flag, the
three fields of the snackbar, the server error, the two list filters. A reader scans the list
to learn which field is a lane, which a fetch and which the UI, and a write in `App.update`
says nothing of the tier of the field it sets.

What the fields are, read by read and write by write:

| Group | Fields | Reads | Writes |
|---|---|---|---|
| the lanes | `OrderContext`, `OrderPlan`, `Session`, `Signing`, and `Patient`, the one the workbench and the plan are for | 38 | 8 |
| the plain fetches | `NormalValues`, `BolusMedication`, `ContinuousMedication`, `Products`, `Localization`, `Hospitals`, `Settings`, `Formulary`, `Parenteralia`, `Interactions`, `InteractionDrugNames` with `DrugNameRetries`, `ServerStatus` | 30 | 27 |
| the admin login | `IsAuthenticated`, `AuthToken`, `LoginAttempt`, and its three fetches `LogFiles`, `LogAnalysisReport`, `Reloading` | 14 | 18 |
| the app-level UI | `Page`, `ShowDisclaimer`, `Context` (the language and the hospital), `LanguageChosen`, `IsDemo`, `SnackbarMsg`, `SnackbarOpen`, `SnackbarSeverity`, `ServerError`, `EmergencyListFilter`, `ContinuousMedsFilter`, `PatientDraft` | 24 | 59 |

Two things the counts show. The snackbar is three fields written together at twelve sites,
three lines each, with two local `tell` helpers in the session and signing arms that do the
same; it is one value, a message with a severity, shown or not. And `ServerStatus` is written
three times and read nowhere; plan 896 noted it as "a bool, read nowhere" and left it.

## Approaches considered

1. **Order the flat fields by tier, with a comment per tier.** Rejected: the cheapest, but a
   reader still scans thirty-six fields, and a write in `App.update` still says nothing of
   the tier it touches; the issue asks for what a write says as much as for what the type
   says.
2. **One nested record per tier** (chosen): `App.State` becomes four fields, `Lanes`,
   `Fetches`, `Admin` and `Ui`, each a record of the fields above; a read is
   `state.Fetches.Formulary`, a write `{ state with Fetches.Formulary = … }`, the nested
   copy-and-update the file already uses for `State.Context.Localization`. The field names
   stay, so that every site changes by a prefix and nothing else, and the diff is mechanical.
3. **The messages grouped per tier as well**, `Msg = Lane of … | Fetch of … | Admin of … |
   Ui of …`, and `update` one function per group over the group's record. Rejected for now:
   every dispatch site in `ConcreteAppEnv` and every `Cmd.ofMsg` changes with it, and most
   arms touch two tiers (a fetch that reads the Session's token, an answer that tells the
   snackbar, a patient change that resets four fetches and two filters), so a function over
   one group's record would take the whole state anyway. The grouping of the state is what
   makes the tiers visible at every site; the split of `update` is a question for review
   below, with its cost.

## Chosen approach

Approach 2.

```fsharp
/// The four lanes, each a machine's state the pages read a projection of, and the patient
/// they are for: the draft, once it meets the minimum.
type Lanes =
    {
        Patient: Patient option
        OrderContext: OrderContextState
        OrderPlan: OrderPlanState
        Session: SessionState
        Signing: SigningState
    }

/// What the server is asked for, once or again: the reading of each plain fetch.
type Fetches =
    {
        NormalValues: Deferred<NormalValues>
        BolusMedication: Deferred<BolusMedication list>
        ContinuousMedication: Deferred<ContinuousMedication list>
        Products: Deferred<Product list>
        Localization: Deferred<string[][]>
        Hospitals: Deferred<string[]>
        Settings: Deferred<Api.ServerSettings>
        Formulary: Deferred<Formulary>
        Parenteralia: Deferred<Parenteralia>
        Interactions: Deferred<DrugInteraction[]>
        InteractionDrugNames: Deferred<string[]>
        // the drug names are asked again on a failure, three times
        DrugNameRetries: int
        ServerStatus: Deferred<bool>
    }

/// The admin login, under the token it bought, and what it fetches.
type Admin =
    {
        IsAuthenticated: bool
        AuthToken: string
        LoginAttempt: int
        LogFiles: Deferred<LogFileInfo[]>
        LogAnalysisReport: Deferred<string>
        Reloading: Deferred<unit>
    }

/// The snackbar: a message with a severity, shown or not.
type Snackbar =
    {
        Message: string
        Open: bool
        Severity: string
    }

/// The app-level UI: the page, the disclaimer, the language and the hospital, the snackbar,
/// the server error, the list filters, and the patient data as the panel edits it.
type Ui =
    {
        Page: Global.Pages
        ShowDisclaimer: bool
        Context: Context
        LanguageChosen: bool
        IsDemo: bool
        Snackbar: Snackbar
        ServerError: string option
        EmergencyListFilter: string[]
        ContinuousMedsFilter: string[]
        PatientDraft: Patient option
    }

type State =
    {
        Lanes: Lanes
        Fetches: Fetches
        Admin: Admin
        Ui: Ui
    }
```

The field names and their comments move as they are; a site reads `state.Ui.Page` where it
read `state.Page`, and writes `{ state with Ui.Page = page }` where it wrote `{ state with Page
= page }`. `initialState` builds the four records. The snackbar's three fields become one
record with two functions, `Snackbar.shown message severity` and `Snackbar.closed`, and the
twelve sites and the two local `tell`s write `Ui.Snackbar = Snackbar.shown … "error"`;
`withdrawInteractionsNotice` reads `state.Ui.Snackbar.Message` and writes `Snackbar.closed`.
`Snackbar.closed` is the value the `CloseSnackbar` arm sets today, field for field: the
empty message, not open, the severity `"error"`.

Where each group is decided by what the field is, not by who reads it: `Patient` is the one
the workbench and the plan are evaluated for, derived from the draft once it meets the
minimum, so it sits with the lanes; `PatientDraft` is what the panel edits and the lists read,
app-level UI; `IsDemo` is set when the settings answer and read by the view, UI;
`DrugNameRetries` counts the retries of one fetch and sits with it; the three admin fetches sit
with the login whose token they need. `ServerStatus` moves with the fetches unread, as it is;
its removal is a change of its own, in Left open.

`ConcreteAppEnv` and `View` read through the groups; the interfaces in `AppEnv.fs` are
unchanged, so no page changes.

What changes for the user: nothing. Every arm sets the same fields to the same values.

## Confidence

High: every change is a prefix on a field name, the compiler finds every site, and the
snackbar record is a value-for-value replacement of three fields. The Shared tests do not link
`App.fs`, so the proof is the compile and a walk through the demo where every group is
touched once: a snackbar shown and closed, a login and a logout, a language switch, a patient
entered, an order prescribed.

## Steps

One PR per group, so that each stays under 200 changed source lines with its share of
`initialState`, `ConcreteAppEnv` and `View`. The groups are independent: a group's fields move
into its record while the rest stay flat, so any order works; the snackbar goes first, as the
largest single reduction.

1. **The snackbar as one value** (`refactor(client)`). `Snackbar` with `shown` and `closed`;
   `App.State.Snackbar: Snackbar` in place of the three fields; the twelve write sites, the two
   `tell`s, `withdrawInteractionsNotice`, `CloseSnackbar` and the view's three reads. About 90
   changed source lines.
2. **`Ui`** (`refactor(client)`). The record, the ten fields into it, `initialState`, the
   sites. About 130 changed source lines.
3. **`Fetches`** (`refactor(client)`). About 150 changed source lines.
4. **`Admin`** (`refactor(client)`). About 90 changed source lines.
5. **`Lanes`** (`refactor(client)`). About 120 changed source lines.
6. **`update` per group, go/no-go** (`refactor(client)`, see the question below). The arms of
   one message family moved into a function of their own, `updateAdmin`, `updateFetches`,
   `updateUi`, each `Msg -> State -> State * Cmd<Msg>` over the whole state, with `update`
   delegating by message. Moving an arm counts its lines twice, so this is about 500 changed
   source lines over three PRs or one; not built unless the reviewer says so.
7. **Docs** (`docs`): this plan's As built; plan 706's left-open bullet on #898 closed.

Every code PR: the Fable compile with `App.jsx` inspected, `dotnet run ServerTests` (no linked
file changes; the run proves nothing broke in the linked ones), Fantomas, the dependency-rule
check. The docs PR: `dotnet run MarkdownLint`.

## Acceptance

- `App.State` has four fields, `Lanes`, `Fetches`, `Admin`, `Ui`, and no `Deferred`, `bool`,
  `string` or option field of its own.
- Every `{ state with` in `App.fs` writes a `Group.Field`; every `state.` read goes through
  a group.
- `grep -n "SnackbarMsg\|SnackbarOpen\|SnackbarSeverity" src/Informedica.GenPRES.Client/App.fs`
  finds nothing: the three retired fields are gone from the state, the writes and the view.
- By hand in the demo (`GENPRES_PROD=0`, `dotnet run`): the interactions notice shows on a
  plan with two interacting drugs and withdraws when one is removed; the snackbar closes on
  its cross; a wrong password says so and a right one opens the settings page, a logout closes
  it; a language switch shows the disclaimer and changes the texts; a patient entered
  evaluates the workbench and the formulary; an order prescribed lands in the plan; the
  server stopped shows the banner and its restart takes it away.

## Questions for review

1. Step 6, `update` per group: go or no-go? Proposed no-go. The grouping of the state makes
   the tier of every read and write visible, which is what the issue asks a reader to see;
   the split moves five hundred lines for functions that would each take the whole state,
   since the arms cross tiers. If go, three PRs over the size rule, said so in each.
2. `Patient` with the lanes, or its own group beside the draft? Proposed with the lanes: it
   is what they are evaluated for, and nothing else reads it but the checks that guard a
   command to them.
3. `Admin` as a group, or its login fields in `Ui` and its fetches in `Fetches`? Proposed a
   group: the three fetches are under the token the login bought, and a token error logs out,
   so they belong together.

## Left open

- `ServerStatus` is written and never read; the server banner reads `ServerError`. Its own
  small change, with the `CheckServer` arms that set it.
- `IsDemo` is a copy of `Settings`'s answer; it could be read from the fetch (`Deferred.map`)
  instead of stored beside it, on plan 706's rule that view state never stores what it can
  derive.
- The messages grouped per tier, approach 3.
- The snackbar as `Snackbar option`, none while closed, instead of an `Open` flag; a boolean
  in state, which the coding instructions ask to avoid, kept here because a value-for-value
  move of the three fields was the point of the step; the option is the next step, on its own.

## As built

Built in the order proposed, one PR at a time, each merged before the next started, the
client edited directly. Every step left the Fable compile with `App.jsx` inspected, Fantomas
and the dependency-rule check green; the Shared tests do not link `App.fs`, so the type
checker finding every site was the proof, the nested updates in the JSX the second reading.

| Step | PR | Landed |
|---|---|---|
| plan | #949 | this document; review: the acceptance grep made a zero-match check, what `Snackbar.closed` sets said precisely |
| 1, the snackbar | #950 | `Snackbar` with `shown` and `closed`; the twelve sites and the two `tell`s one line each; the withdrawal keeps the severity as it did; 126 source lines |
| 2, `Ui` | #951, #952 | `UiState`, eight fields then `Snackbar` and `PatientDraft`; 150 and 63 source lines |
| 3, `Fetches` | #953, #954 | `FetchesState`, the eight fetches asked once then the five refetched with the retry counter; 101 and 108 source lines |
| 4, `Admin` | #955 | `AdminState`; 109 source lines |
| 5, `Lanes` | #956 | `LanesState`; `App.State` is `Lanes`, `Fetches`, `Admin`, `Ui`; 137 source lines |
| 6, `update` per group | not built | no-go, question 1 answered: the grouping makes the tier of every read and write visible, and each function would take the whole state |
| 7, docs | this PR | this section; plan 706's left-open bullet on #898 closed |

### Deviations from the text above

- **The record types are `LanesState`, `FetchesState`, `AdminState`, `UiState`**, the fields
  `Lanes`, `Fetches`, `Admin`, `Ui`. The plan named type and field alike. With a type `Ui` in
  scope, `{ state with Ui.Page = … }` resolves `Ui.Page` as the type-qualified field of `Ui`
  and fails to compile ("fields from inconsistent types"): the nested path needs its first
  name to be a field, not a type, or the full `State.Ui.Page` at every site. The same clash
  is why the file wrote `State.Context.Localization` before; that site is
  `Ui.Context.Localization` now.
- **Steps 2 and 3 took two PRs each.** All ten UI fields came to 214 changed source lines
  and all thirteen fetch fields to 205, over the rule; each group landed in two halves, the
  record growing with the second, which compiles at every point.
- **The snackbar's withdrawal keeps its severity.** `withdrawInteractionsNotice` clears the
  message and closes through a nested update and leaves the severity, as it did; only
  `CloseSnackbar` and `initialState` set `closed`. Value for value, as the step was.
