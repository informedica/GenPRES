# Implementation plan for issue 896

## Problem description

A plain fetch blanks on refetch ([#896](https://github.com/informedica/GenPRES/issues/896)).
`LoadFormulary Started` sets `Formulary = InProgress` in `src/Informedica.GenPRES.Client/App.fs`
and the formulary page loses its selects, its text and its dose check until the answer, and
the page's background tint with them; the parenteralia page the same; the interactions page
loses its rows and the side menu its badge while the plan's drugs are checked again; the
settings page replaces the log table by a spinner on every refresh. The refetches are frequent:
every filter change on the formulary and the parenteralia pages, every order added to or
removed from the plan, every patient change.

`Deferred<'t>` (`Deferred.fs`) is the reading of a plain fetch, three cases: not asked yet,
asked with nothing to show meanwhile, answered. Plan [706](706-client-view-tier.md) took
`Provisional` out of it, since its meaning was the order lanes' and not the type's, and the
lanes got their own view DUs with a `Changing` case that keeps the value shown while a request
runs. The plain fetches have no such case: a refetch has nothing to keep the previous value in.

What the fifteen `Deferred` fields of `App.State` do, checked one by one:

| Field | Fetched again? | The page meanwhile |
|---|---|---|
| `Formulary` | on every filter change, patient change, page open, admin reload | blank selects, no text, no dose check, no tint |
| `Parenteralia` | the same | blank selects, no text |
| `Interactions` | on every change of the plan's drugs | no rows, no badge |
| `InteractionDrugNames` | only while not yet answered (a retry) | nothing to keep |
| `LogFiles` | the refresh button, the page opened | a spinner instead of the table |
| `LogAnalysisReport` | one per file clicked | a spinner: the previous report is another file's |
| `Reloading` | the reload button | a unit: the spinner is the meaning |
| `ServerStatus` | every five seconds until reached | a bool, read nowhere |
| `Localization` | once | the fallback strings; never blank |
| `NormalValues`, `BolusMedication`, `ContinuousMedication`, `Products`, `Settings`, `Hospitals` | once | not refetched |

Four fields lose a value the page had: the formulary, the parenteralia, the interactions and
the log files. Found on the way, not this issue's: the emergency list and the continuous
medication page show an empty table without a spinner when there is no patient, since the
interventions are computed to `InProgress` then (`calculateInterventions`); the parenteralia
page passes `HasNotStartedYet` for its terms and always shows the English fallbacks; the
patient summary renders empty while the terms load.

## Approaches considered

1. **A fourth case, `Refreshing of 't`** (chosen): the previous value kept while a refetch
   runs. The `Started` arm of a fetch with a value to keep sets it; the pages render from
   `Resolved x` and `Refreshing x` alike and grey or mark on `Refreshing`, as they render from
   `Settled` and `Changing` in the order lanes and build a command from `Settled` only. The
   type stays the reading of a plain fetch; the case is the type's, since every plain fetch
   with a value can be asked again.
2. **Keep the value beside the field**: a second field per fetch, or a `(Deferred * 't option)`
   pair, holding the last answer. Rejected: two places for one fact, and every reader has to
   know to look in the second.
3. **A view DU per fetch**, as the order lanes have. Rejected: the lanes have a request under
   way with a payload of its own and a selection beside it; a plain fetch has one value and
   one flag, which the fourth case says.

## Chosen approach

Approach 1.

```fsharp
/// A value the server is asked for, in the four states a page can find it in: not asked yet;
/// asked with nothing to show meanwhile; answered; asked again, the previous answer kept and
/// shown meanwhile.
type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Resolved of 't
    | Refreshing of 't
```

`map` and `bind` carry `Refreshing` as they carry `Resolved` (`bind` keeps the case: a
`Resolved` answer of the function over a `Refreshing` value is `Refreshing`); `defaultValue`
and `toOption` read the value of both. One helper, `Deferred.refresh`: the fetch asked again,
the value kept when there is one (`Resolved v` and `Refreshing v` become `Refreshing v`, the
other two `InProgress`).

The `Started` arms of the four fetches set the field through `refresh`; their guard against a
second request while one runs (`InProgress -> state, Cmd.none`) covers `Refreshing` too, so a
filter change during a refetch is dropped as it is today; the formulary's own `Resolved form`
read before the request takes `Refreshing form` as well. The interaction check
(`CheckInteractions`) refreshes; the drug names refresh (harmless: nothing to keep while
unanswered). The log analysis, the reload and the server status keep `InProgress`: another
file's report, a unit, an unread bool. The fetches asked once keep their arms: `refresh` would
be a no-op, and the diff stays where the fix is.

The pages, on the rule of plan 706: render from both cases, act from `Resolved` only.

- `Views/Formulary.fs`: `init` seeds from `Refreshing form` too, so the selects keep their
  state through a refetch instead of re-initialising to empty; the five selects take
  `Refreshing form` as disabled with their value and options kept and shown, where today they
  are disabled and empty, so a change while the answer is on its way cannot be made from the
  control, the same as today; the `update` arms that send a filter stay on `Resolved`, the
  second guard for the same rule; the text and the dose check render from both.
  `ViewHelpers.progressOrEmpty` shows the progress on `Refreshing` as on `InProgress`, over the
  content shown. `Pages/GenPres.fs` tints the background from both. What a refetch changes is
  what the page shows meanwhile, never what it accepts: a change is possible on `Resolved` only,
  and the answer never lands over a change of the user's, since none can be made.
- `Views/Parenteralia.fs`: the same shape, three selects.
- `Views/Interactions.fs`: the rows and the "checked" flag from both; the loading flag on
  `Refreshing` too, so the progress shows over the rows; the effect's guard against a second
  check covers `Refreshing`. `Pages/GenPres.fs` keeps the badge from both.
- `Views/Settings.fs`: the log table from both, with the progress above it on `Refreshing`.

Nothing else reads a case that can be `Refreshing`: the terms, the hospitals, the interventions
go through `defaultValue` or match `Resolved` on a field that is never refreshed.

## Confidence

High. The type change is four helper arms and one function, under test; the `Started` arms are
one line each; the page arms are the `Resolved x | Refreshing x` shape the order pages already
use. The risk is a read site missed: a `| Resolved x -> … | _ -> empty` arm compiles unchanged
and keeps blanking, so the list above is the list to check, not the compiler.

## Steps

1. **`Deferred.Refreshing`, the fetches and the pages** (`fix(client)`, one PR). `Deferred.fs`
   with the case, the helpers and `refresh`; the four `Started` arms and the check in
   `App.fs`; `ViewHelpers.progressOrEmpty`; the arms of the four pages and the two in
   `Pages/GenPres.fs`. Tests: a new `DeferredTests.fs` in the Shared tests (the file is already
   linked), `map`, `bind`, `defaultValue`, `toOption` and `refresh` over the four cases. About
   90 source lines; its own `fix` commit, so that the changelog carries it.
2. **Docs** (`docs`): this plan's As built; plan 706's left-open bullet on #896 closed.

## Verification

- `dotnet run ServerTests` (the Deferred tests), the Fable compile with the touched `.jsx`
  inspected, Fantomas, the dependency-rule check.
- By hand in the demo: on the formulary page, change the indication: while the answer is on
  its way the selects stay filled, greyed, with the progress showing, the text stays until the
  new one arrives, the tint stays; then change the route: the same; the same on the
  parenteralia page; on the plan page, add an order:
  the interactions page keeps its rows and the side menu its badge while the check runs; on the
  settings page, press the refresh icon: the log table stays with the progress above it.

## Left open

- The interventions computed to `InProgress` without a patient, and the empty table it gives
  the emergency list and the continuous medication page.
- The parenteralia page's terms passed as `HasNotStartedYet`.
- The patient summary rendered empty while the terms load.

## As built

To be filled in as the steps land.
