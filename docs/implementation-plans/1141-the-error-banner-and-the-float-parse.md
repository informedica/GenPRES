# Implementation plan for issues #1141 and #1176

Two ways the client says something that is no longer, or not proven, true: a server error banner
that outlives the error, and a build warning that the browser may parse the normal values
differently from the server.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)
- [As built](#as-built)

## Problem description

- **#1141** `processError` in `App.fs` shows a snackbar and sets `Ui.ServerError`, the
  "Server fout" banner. Only a dismiss or a successful server check clears it. It is called for
  the order plan (`OrderPlanEffect.TellError`), the formulary, the parenteralia, the interactions,
  the login and, through `tokenError`, the admin calls. Seen in #1126: an order plan request
  failed in a race, the next one succeeded, and the banner still said "Gewicht en lengte onbekend"
  beside a panel showing both.
- **#1176** `Csv.tryParseFloat` in `src/Informedica.GenPRES.Shared/Utils.fs` calls
  `Double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture)`. Shared is compiled by
  Fable too, which drops both arguments and warns twice in every build. The browser parses the
  normal-values sheet through this function, and the normal values decide the estimated weight
  and height, so what the browser accepts has to be known.

### What Fable's parse does

Checked in `fable-library-js` 5.0.0, `Double.js`: `tryParse` refuses a blank string, removes the
first `_`, and takes JavaScript's unary `+`. Compared with `NumberStyles.Float` under the
invariant culture:

| Input | .NET | Fable |
|---|---|---|
| `0.0192`, `-1.5`, `+2`, `.5`, `1e3`, ` 1.5 ` | accepted | accepted, same value |
| `1,5`, `1,000` | refused | refused |
| `0x10`, `0b101`, `0o7` | refused | accepted as 16, 5, 7 |
| `1_000` | refused | accepted as 1000 |
| `Infinity` | accepted | accepted |
| `NaN` | accepted | refused |

The two agree on every decimal the sheet holds, and both refuse a decimal comma. They differ on
the JavaScript number prefixes and the underscore, which the sheet should never hold, but which
the browser would take as a number where the server would not.

## Decisions

Taken 2026-09-29 by the maintainer.

| Question | Decision |
|---|---|
| When the banner goes (#1141) | A request error is an answer to one request: the next successful answer of the same kind of request clears the banner it raised. The unreachable-server banner stays until the server check succeeds, as today. |
| The Fable parse (#1176) | Make it refuse what .NET refuses, rather than accept the difference with a comment: the parse feeds the patient estimate. |

## Approaches considered

For the banner (#1141):

- **Snackbar only**, as order context errors do. Loses the lasting notice for a fault the user has
  to act on, such as a token the server no longer takes.
- **Clear on any successful answer.** A formulary answer would clear an order plan error it says
  nothing about.
- **The banner knows which kind of request raised it, and that kind's next success clears it.**
  Chosen.

For the parse (#1176):

- **Only `#if FABLE_COMPILER` around a plain `Double.TryParse x`.** Silences the warning and keeps
  the prefixes and the underscore accepted.
- **The Fable branch first checks the text against the decimal shape `NumberStyles.Float`
  allows**, then parses. Same answers on both targets, no warning. Chosen.

## Chosen approach

### The banner belongs to its request (#1141)

- A pure `ServerErrorPolicy` module in `Client.Core`, written directly there with Expecto tests,
  as `PlanCellPolicy` was:

  ```fsharp
  [<RequireQualifiedAccess>]
  type ErrorSource =
      | OrderPlan
      | Formulary
      | Parenteralia
      | Interactions
      | Login
      | Admin
      | Server

  type ServerError = { Source: ErrorSource; Message: string }

  /// The banner after a successful answer from source: gone when it was raised by that source.
  let clearedBy source (error: ServerError option) = ...
  ```

- `App.fs`: `Ui.ServerError` becomes `ServerErrorPolicy.ServerError option`. `processError` takes
  the source, and each call site passes its own; `CheckServer` sets `Server`. The banner renders
  `Message` as today.
- The successful answers call `clearedBy`: `OrderPlanAnswered`, `LoadFormulary`,
  `LoadParenteralia`, `LoadInteractionsResult`, the login result, and the admin results. The
  server check clears every source, as today.
- The snackbar text stays.

`State` lives in the client project, so the edit of `App.fs` is under the UI exception; the rule
it applies is in `Client.Core`, tested.

### The same parse on both targets (#1176)

```fsharp
let tryParseFloat (x: string) =
#if FABLE_COMPILER
    // JavaScript's number parse also takes 0x, 0b and 0o prefixes and an underscore, which
    // NumberStyles.Float refuses; the shape check keeps the browser to what the server accepts
    if Regex.IsMatch(x, @"^\s*[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?\s*$") then
        Double.TryParse x
    else
        false, 0.
#else
    Double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture)
#endif
```

`Infinity` and `NaN` fall outside the shape and are refused in the browser; the server takes them.
No sheet value is either; the difference is stated in the comment of the pull request.

Shared is source outside the client, so the function is written and tested in a script first,
`src/Informedica.GenPRES.Shared/Scripts/TryParseFloat.fsx`, with the table above as its cases run
against the .NET branch, and the Fable branch checked with the regex on the same cases. The
maintainer migrates it.

## Confidence

Medium for #1141: which call sites count as one source (the admin calls share `tokenError`) may
move in review. High for #1176.

## Steps

1. **`ServerErrorPolicy` in `Client.Core`**, with tests for every source: cleared by its own
   success, kept by another's, the server source cleared only by the server check.
2. **The banner by source in `App.fs`.** `processError` with a source, the successes clearing.
3. **The parse script** in `Shared/Scripts`, the cases of the table, for the maintainer to migrate.

One pull request per step. Run `scripts/CheckDependencyRule.fsx` after step 1.

## Verification

- `dotnet run servertests` passes, the new `Client.Core` tests among them.
- Fable compiles without the two `FABLE` warnings for `Utils.fs` once step 3 is migrated;
  `npx vite build` passes.
- In the browser: an order plan error followed by a successful order plan answer leaves no
  banner; a formulary answer does not clear an order plan error; with the server stopped, the
  unreachable banner stays until it answers again.
- The normal values load in the browser, and the estimated weight and height of a patient
  without measurements are the same as before.

## As built

| Step | Pull request | Note |
|---|---|---|
