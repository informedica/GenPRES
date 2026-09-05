# Implementation plan for issue #419

## Problem description

[#419](https://github.com/informedica/GenPRES/issues/419): `failwith` throws a bare
`System.Exception`, which the .NET Framework Design Guidelines say never to throw. The
thread agreed this is mechanical work suited to an LLM, with one condition from the
discussion: rate every site and only claim a benefit where the exception type actually
carries information, so the change does not become a "colouring book exercise".

At the time of writing there are 114 `failwith`/`failwithf` calls in `src/` and `tests/`
(61 and 53), plus one hand-built `exn "..." |> raise`. `invalidOp` has two existing uses
(`Logging.Lib/Logging.fs`, `MCP.Server/Program.fs`), and `invalidArg`, `raise` and four
custom exception types (`SolverException`, `OrderException`, `BigRationalException`,
`ResourceLoadError`) are already in use, so every target idiom is already in the repo.

A further ten calls live outside `src/` and `tests/`: six in `Build.fs`, two in
`Helpers.fs` and two in `benchmark/Program.fs`. They are build and benchmark tooling, not
shipped code, but the rule is repository-wide, so they are converted too (category E,
`invalidOp`) in the second implementation PR.

## Approaches considered

1. **Replace only the sites where the type carries information** (argument validation,
   missing keys, timeouts, lost inner exceptions). Leaves ~40 `invalidOp`-class sites as
   `failwith`, so the rule "no `failwith`" cannot be enforced afterwards.
2. **Replace every site, categorized, with a benefit rating per category.** Same
   information gain as option 1 on the sites that matter, plus a codebase with no
   `System.Exception` throws left, so the rule can be stated in the coding instructions and
   checked with a grep. The low-benefit sites are labelled as such so reviewers can push
   back per category rather than per line.
3. **Introduce a project-wide custom exception hierarchy.** Rejected: the four existing
   custom exceptions each belong to one library's error model, and none of the 114 sites
   needs a new type. BCL types (`ArgumentException`, `InvalidOperationException`,
   `KeyNotFoundException`, `FormatException`, `NotSupportedException`, `TimeoutException`)
   cover them.

## Chosen approach

Option 2. Each site is assigned one of the categories below. The replacement never widens
or narrows behaviour: every new type still derives from `System.Exception`, so every
existing `| e ->` handler keeps working. Messages are preserved verbatim, with two
deliberate exceptions:

- **G.** `reraise ()` propagates the original exception, so the `"didn't catch {e}"` /
  `"something unexpected happened, didn't catch {e}"` wrapper text disappears. The wrapper
  is still written to the console by `writeErrorMessage` before the rethrow; only the
  exception that leaves `Solver.solve` changes, from a `System.Exception` with the wrapper
  message to the original exception with its own message, stack trace and inner exception.
- **I.** `"Operator is not supported"` becomes a `BigRationalException CannotMatchOperator`.
  The one test on that path (`GenUNITS.Tests`, "opFromString throws on an unknown token")
  uses an untyped `Expect.throws` and does not read the message.

No other site changes its message text.

| Category | Replacement | Benefit | Why |
|---|---|---|---|
| A. Precondition on an argument | `invalidArg (nameof x) msg` | High | Names the offending parameter; callers and tests can match `ArgumentException`. |
| B. String cannot be parsed | `raise (FormatException msg)` | Medium | BCL convention for malformed input; distinguishes bad data from a bug. |
| C. Lookup by name or key fails | `raise (KeyNotFoundException msg)` | High | Sheet parsers and the column-contract tests rely on `Csv.getColumn` raising; the type now says why. |
| D. Missing configuration or not initialized | `invalidOp msg` | Medium | Process state is wrong, not an argument. Startup guards stay fail-fast. |
| E. Invariant violated or unreachable branch | `invalidOp msg` | Low | No information gained; one-token change. This is the "colouring book" bucket and is labelled as such. |
| F. Unsupported operation | `raise (NotSupportedException msg)` | Medium | BCL convention. |
| G. Rethrow of a caught exception | `reraise ()` after logging | High | Today the catch-all in `Solver.fs` does `failwith $"didn't catch {e}"`, which discards the original stack trace and inner exception. This is a diagnostic bug, not style. |
| H. Timeout | `raise (TimeoutException msg)` | High | First-class BCL type; the sibling agent path already throws it and a test already asserts it. |
| I. Library already has a custom exception for this | existing `raiseExc` | Medium | `BigRational.fs` defines `CannotMatchOperator` and never raises it. No new custom exceptions. |
| T. Test bodies | Expecto `failtest` / `failtestf` | High | The run reports *failed* instead of *errored*; 33 tests already use it. |
| T2. Test stubs that must throw | `invalidOp` / `NotImplementedException` | n/a | Tests that inject a generic crash through a boundary keep doing so. |

### Safety check (done before writing this plan)

- No `| Failure msg ->` exception handler exists in `src/` or `tests/`. The one `Failure`
  match (`GenUNITS.Lib/UnitsParse.fs`) is FParsec's `ParserResult.Failure`.
- Every type-specific `try/with` (`SolverException`, `ResourceLoadError`,
  `KeyNotFoundException` in `GenFORM.Lib/Api.fs`, `MissingMethodException` in `Server.fs`)
  either has a catch-all fallback or does not catch `failwith` today and will not catch the
  replacement either.
- Every `Expect.throws` in the tests is untyped. The only `Expect.throwsT` targets
  `TimeoutException`.
- `ex.Message` is only ever logged, never pattern-matched, except in two tests noted below.

Three sites need care rather than a blanket rewrite:

1. `GenSOLVER.Lib/Solver.fs` catch-alls (`solveE` and the outer `try` in `solve`): must
   **not** become `SolverException`, or the outer handler would swallow them and turn a crash
   into `Error(...)`. Use `reraise ()` after the existing `writeErrorMessage`.
2. `Agents.Lib/Agent.fs` reply timeout: a test asserts the message contains `"200 ms"`. The
   `TimeoutException` keeps the text `"... after {fallbackMs} ms"`.
3. `GenPRES.Server.Tests/ResourceErrorTests.fs` injects a generic crash into a loader and
   asserts the engine reports `"Failed to load resources"`. That injection must stay a
   generic throw (`invalidOp`), not `ResourceLoadError`, or it takes the other branch.

### Site inventory

Source (63 + 1):

| Library | Sites | Categories |
|---|---|---|
| GenSOLVER | `Solver.fs` ×4, `Variable.fs` ×6, `Equation.fs` ×1 | G ×2, E ×2, A ×6, C ×1 |
| GenUNITS | `UnitsParse.fs`, `Api.fs` ×2, `Core.fs`, `Units.fs` ×3, `ValueUnit.fs` ×3, `Combine.fs` | B ×4, A ×5, F ×2 |
| Utils | `Result.fs`, `Csv.fs` ×2, `List.fs`, `BCL/String.fs`, `BCL/BigRational.fs` | E, B, C ×2, A, I |
| GenORDER | `OrderVariable.fs`, `Utils.fs`, `Medication.fs` ×3, `Order.fs` ×4 (incl. the `exn` raise) | A, D, B, F ×2, C ×2, E ×2 |
| GenFORM | `Api.fs`, `SolutionRule.fs`, `Product.fs` ×3 | A, E, B ×3 |
| ZForm / ZIndex / NKF | `Utils.fs`, `GStand.fs` ×4, `BST000T.fs`, `NKF/Utils.fs` | D ×2, E ×3, B, A |
| Shared (Fable) | `Localization.fs`, `Utils.fs` ×2, `Models.fs` ×2 | B ×3, C, A |
| Server / MCP / Agents / Client | `Server.fs` ×3, `McpServer.fs` ×2, `Agent.fs`, `Views/*.fs` ×2 `rowCreate` | D ×5, H, A ×2 |

Tests (51): 44 become `failtest`/`failtestf`; `FixtureJson.fs` (fixture parser) becomes
`FormatException`; `ZIndexFixture.fs` (fixture builder) becomes `invalidArg`; the five T2
stubs (`StubAdapterTests.notStubbed`, `ResourceErrorTests`, `Logging.Tests` bad formatter,
`Agents.Tests` ×2) become `NotImplementedException` or `invalidOp`. Tests wrapped in
`try ... with _ -> false` swallow `AssertException` just as they swallow `Exception`, so the
`failtest` swap gains nothing there; they are converted for consistency only.

## Confidence

High. The replacement is a type swap with preserved messages, the catch-site sweep found no
handler that depends on the exact `System.Exception` type, and the full Expecto suite plus a
Fable compile of `Shared`/`Client` verify it.

## Steps

1. This plan, plus a `failwith` rule in the Error Handling section of
   `.github/instructions/fsharp-coding.instructions.md`, and the test example in
   `AGENTS.md` / `.github/copilot-instructions.md` corrected to use `failtest`.
2. PR: high-benefit source sites (categories A, C, G, H, I), including the `Solver.fs`
   `reraise` and the `Agent.fs` `TimeoutException`. The existing timeout test is tightened
   to match on `TimeoutException`.
3. PR: remaining source sites (B, D, E, F), plus the ten build and benchmark tooling sites
   (`invalidOp`; FAKE fails a target on any exception, so build behaviour is unchanged).
4. PR: tests (T, T2). The `Logging.Tests` case that uses `failwith` *inside* an
   `Expect.throws` lambda is rewritten rather than converted, since its logic is inverted.
5. Each PR runs `dotnet run Format`, `dotnet run servertests`, and a Fable compile when
   `Shared` or `Client` is touched. Commits use `refactor(<scope>)`, which ShipIt does not
   render into the changelog; that is correct, nothing user-visible changes.
6. Done when `grep -rnE '\bfailwith(f)?\b|\bexn "' --include='*.fs' . --exclude-dir=node_modules`
   is empty, matching the repository-wide rule in the coding instructions.

## Process note

The repository's script-only policy keeps LLMs out of `.fs` files. The issue thread
anticipated that this refactor needs an exception to that policy, and the maintainer granted
it for #419 explicitly. The implementation PRs disclose this in the AI/Vibe Coding section.
