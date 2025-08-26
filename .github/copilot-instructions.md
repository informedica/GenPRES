# GitHub Copilot instructions for GenPRES

This file is a concise set of instructions for automated coding agents working on the GenPRES repository. Use it to guide safe, consistent, and testable changes. Keep edits small, test-driven, and follow existing repository patterns.

## Goal
- Make minimal, well-scoped changes that build and keep tests green.
- Preserve medical-device safety posture: do not ship breaking changes without tests and documentation (MDR requirements apply).

## Quick start — run & test
- Build / run the app locally: use the repo root and `dotnet run` (requires .NET 9).
- Run tests: `dotnet test` from repo root or run individual test projects under `tests/`.
- Env variables: set `GENPRES_URL_ID` (Google sheet id), `GENPRES_PROD=0` for demo mode.

## Key code locations
- F# libraries under `src/` (several libs: Informedica.GenForm.Lib, GenUnits.Lib, GenSolver.Lib, GenOrder.Lib, etc.).
- Server API & resource loading: `src/Informedica.GenForm.Lib/Api.fs` (ResourceConfig, loadAllResourcesWithConfig).
- Spreadsheet mappers/parsers: `src/Informedica.GenForm.Lib/Mapping.fs`, `Product.fs`, `DoseRule.fs`, `SolutionRule.fs`, `RenalRule.fs`, `Models.fs`.
- Units & BigRational logic: `src/Informedica.GenUnits.Lib/ValueUnit.fs`.
- Tests: `tests/` (Expecto + FsCheck). Look for BigRational and ValueUnit tests.
- Docs with sheet specs: `docs/mdr/design-history/genpres_resource_requirements.md`.

## Resource loading pattern
- Resources are loaded from Google Sheets via `Web.getDataFromSheet dataUrlId "SheetName"`.
- Mapping helper functions use `Csv.getStringColumn` / `Csv.getFloatOptionColumn` and call getString/getFloat-style delegates.
- The central `ResourceConfig` (in `Api.fs`) expects functions returning `GenFormResult<'T>` (alias for `Result<'T, Message list>`). Use the `*Result` variants where present (e.g., `Mapping.getRouteMapping` or `Mapping.getRouteMappingResult`) and wrap with `delay` when the signature expects a `unit -> GenFormResult<_>`.
- To add/modify sheet mappings: adjust the mapper in the corresponding module (e.g., `Product.Reconstitution.get`, `DoseRule.get`) and update `genpres_resource_requirements.md` to reflect column names.

## Result and error handling
- IO and parsing functions should return `GenFormResult<'T>` (i.e., Result). Use `FsToolkit.ErrorHandling.ResultCE` computation expression for readability (`result { let! x = ... }`).
- When editing `ResourceConfig` or callers, make sure to handle `Result` values consistently; use `Result.bind`, CE, or `delay` for unit-returning getters.

## BigRational & ValueUnit semantics (important)
- BigRational operations are used broadly for dosing math. Respect existing helpers in `Informedica.GenUnits.Lib`.
- removeBigRationalMultiples semantics: it keeps the smallest positive BigRational representatives and removes later values that are integer multiples of a previously kept value. Example: [1/3; 1/2; 1] -> keep 1/2 and 1/3 (both non-multiples of each other), but if 1/2 and 1 are present, keep 1/2 and remove 1 (1 is multiple of 1/2).
- Use `BigRational.isMultiple` when reasoning about integer multiples.
- Prefer using existing helpers like `ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, etc., when manipulating units.

## Tests & test-writing conventions
- Use Expecto for unit tests; follow existing patterns in `tests/*/Tests.fs`.
- Add a happy-path test and 1–2 edge-case tests per change. Include clear failure messages with input and expected vs actual values to ease debugging.
- For property-based tests, prefer FsCheck and constrain shrinking by making domain-specific generators.
- When changing shared algorithms, update and run the related tests: BigRational, ValueUnit, GenSolver tests.

## Commit messages and branching
- Follow Conventional Commit prefixes: feat, fix, refactor, docs, tests, chore.
- Scope commits to the area changed, e.g., `fix(ValueUnit): correct removeBigRationalMultiples2 sieve`.
- Keep PRs small and focused. Reference issue numbers when relevant.

## Safety, MDR and documentation
- This project targets clinical medication workflows. Any change that affects dosing, rules, parsing, or resource mapping must include: unit tests, changelog entry, and an update to `docs/mdr/design-history/genpres_resource_requirements.md` if spreadsheet columns or semantics changed.
- Add notes to CONTRIBUTING.md if the change introduces a new external dependency or changes deployment behavior.

## When editing spreadsheet mappers
- Check `genpres_resource_requirements.md` for expected sheet and column names.
- Update the mapper to read columns by name using the `get` delegate (e.g., `let get = getColumn row in get "Generic"`), parse with `BigRational.toBrs` / `getFloat` as appropriate.
- If adding optional numeric columns, use `getFloatOptionColumn` and `Option.bind BigRational.fromFloat`.

## ResourceConfig helper patterns (examples)
- Default config provides functions like `Mapping.getUnitMapping dataUrlId |> delay` to match `unit -> GenFormResult<_>` signatures.
- To provide test configuration, use `Api.createTestResourceConfig` or create a `ResourceConfig` where each getter returns `Ok value` or `Error msgs` as needed.

## Code style & conventions
- Keep F# idiomatic style: small modules, use `result { }` CE for Results, avoid side effects in pure mappers.
- Prefer reusing existing helpers rather than adding duplicate code.
- Fail early with clear errors for unexpected sheet formats; return `Error` with `ErrorMsg` messages.

## Where to look for examples
- Resource loading and tests: `src/Informedica.GenForm.Lib/Api.fs` and `tests/`.
- Sheet parsers: `Mapping.fs`, `Product.fs`, `DoseRule.fs`, `SolutionRule.fs`, `RenalRule.fs`.
- Unit and BigRational helpers: `src/Informedica.GenUnits.Lib/ValueUnit.fs`.
- Sheet documentation: `docs/mdr/design-history/genpres_resource_requirements.md`.

## Checklist for automated edits
- [ ] Small, focused change with < 300 LOC modified when possible.
- [ ] Add or update unit tests covering the change.
- [ ] Ensure `dotnet test` passes locally for affected projects.
- [ ] Update `genpres_resource_requirements.md` if spreadsheet column names or semantics change.
- [ ] Use `GenFormResult` and handle errors explicitly.
- [ ] Use conventional commit message with scope and short description.


---

Notes: If the repo already contains `.github/instructions` files or other agent guidance, prefer merging relevant content rather than duplicating. This copy is a concise baseline. For larger changes, ask a human reviewer and include an MDR impact assessment.
