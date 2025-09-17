# GitHub Copilot instructions for GenPRES

This file is a concise set of instructions for automated coding agents working on the GenPRES repository. Use it to guide safe, consistent, and testable changes. Keep edits small, test-driven, and follow existing repository patterns.

## Goal
- Make minimal, well-scoped changes that build and keep tests green.
- Preserve medical-device safety posture: do not ship breaking changes without tests and documentation (MDR requirements apply).

## Quick start — run & test
- Build / run the app locally: use the repo root and `dotnet run` (requires .NET 9).
- Run tests: `dotnet run servertests` from repo root or run individual test projects under `tests/`.

## Key code locations
- F# libraries under `src/` 
- Tests: `tests/` (Expecto + FsCheck). Look for BigRational and ValueUnit tests.

Also consult: [CONTRIBUTING.md](https://github.com/halcwb/GenPres2?tab=contributing-ov-file)

## Code standards
Follow the project coding standards:
- [F# Coding Instructions](instructions/fsharp-coding.instructions.md)
- [Commit Message Instructions](instructions/commit-message.instructions.md)

## Resource loading pattern

- Docs with sheet specs: `docs/mdr/design-history/genpres_resource_requirements.md`.
- Check `genpres_resource_requirements.md` for expected sheet and column names.
- Resources are loaded from Google Sheets via `Web.getDataFromSheet dataUrlId "SheetName"`.
- Mapping helper functions use `Csv.getStringColumn` / `Csv.getFloatOptionColumn` and call getString/getFloat-style delegates.
- The central `ResourceConfig` (in `Api.fs`) expects functions returning `GenFormResult<'T>` (alias for `Result<'T, Message list>`). Use the `*Result` variants where present (e.g., `Mapping.getRouteMapping` or `Mapping.getRouteMappingResult`) and wrap with `delay` when the signature expects a `unit -> GenFormResult<_>`.
- To add/modify sheet mappings: adjust the mapper in the corresponding module (e.g., `Product.Reconstitution.get`, `DoseRule.get`) and update `genpres_resource_requirements.md` to reflect column names.
- Update the mapper to read columns by name using the `get` delegate (e.g., `let get = getColumn row in get "Generic"`), parse with `BigRational.toBrs` / `getFloat` as appropriate.
- If adding optional numeric columns, use `getFloatOptionColumn` and `Option.bind BigRational.fromFloat`.

## Result and error handling
- IO and parsing functions should return `GenFormResult<'T>` (i.e., Result). Use `FsToolkit.ErrorHandling.ResultCE` computation expression for readability (`result { let! x = ... }`).
- When editing `ResourceConfig` or callers, make sure to handle `Result` values consistently; use `Result.bind`, CE, or `delay` for unit-returning getters.

## BigRational & ValueUnit semantics (important)
- BigRational operations are used broadly for dosing math. Respect existing helpers in `Informedica.GenUnits.Lib`.
- removeBigRationalMultiples semantics: it keeps the smallest positive BigRational representatives and removes later values that are integer multiples of a previously kept value. Example: [1/3; 1/2; 1] -> keep 1/2 and 1/3 (both non-multiples of each other), but if 1/2 and 1 are present, keep 1/2 and remove 1 (1 is multiple of 1/2).
- Use `BigRational.isMultiple` when reasoning about integer multiples.
- Prefer using existing helpers like `ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, etc., when manipulating units.

## BigRational & ValueUnit semantics (important)
- BigRational operations are used broadly for dosing math. Respect existing helpers in `Informedica.GenUnits.Lib`.
- removeBigRationalMultiples semantics: it keeps the smallest positive BigRational representatives and removes later values that are integer multiples of a previously kept value. Example: [1/3; 1/2; 1] -> keep 1/2 and 1/3 (both non-multiples of each other), but if 1/2 and 1 are present, keep 1/2 and remove 1 (1 is multiple of 1/2).
- Use `BigRational.isMultiple` when reasoning about integer multiples.
- Prefer using existing helpers like `ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, etc., when manipulating units.

## Safety, MDR and documentation
- This project targets clinical medication workflows. Any change that affects dosing, rules, parsing, or resource mapping must include: unit tests, changelog entry, and an update to `docs/mdr/design-history/genpres_resource_requirements.md` if spreadsheet columns or semantics changed.
- Add notes to CONTRIBUTING.md if the change introduces a new external dependency or changes deployment behavior.

## Where to look for examples
- Resource loading and tests: `src/Informedica.GenForm.Lib/Api.fs` and `tests/`.
- Sheet parsers: `Mapping.fs`, `Product.fs`, `DoseRule.fs`, `SolutionRule.fs`, `RenalRule.fs`.
- Unit and BigRational helpers: `src/Informedica.GenUnits.Lib/ValueUnit.fs`.
- Sheet documentation: `docs/mdr/design-history/genpres_resource_requirements.md`.

## Checklist for automated edits
- [ ] Small, focused change with < 300 LOC modified when possible.
- [ ] Add or update unit tests covering the change.
- [ ] Ensure `dotnet run servertests` passes locally for affected projects.
- [ ] Update `genpres_resource_requirements.md` if spreadsheet column names or semantics change.
- [ ] Use conventional commit message with scope and short description.
