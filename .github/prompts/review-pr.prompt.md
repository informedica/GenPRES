---
description: "Review and explain a pull request's changes in the GenPRES repository"
---

Review the changes in this pull request for correctness, safety, and adherence to repository conventions.

## Checklist

### Script-Only Policy
- [ ] New logic is implemented in `.fsx` scripts, not `.fs` source files
- [ ] Any `.fs` changes are limited to: documentation/comments, client-side UI (`src/Informedica.GenPRES.Client/`), or explicitly requested targeted refactoring

### Code Quality
- [ ] F# code follows the style guidelines in `.github/instructions/fsharp-coding.instructions.md`
- [ ] Code formatting follows `.github/instructions/fsharp-code-formatting.instructions.md`
- [ ] Commit messages follow `.github/instructions/commit-message.instructions.md`

### Testing
- [ ] Tests are added or updated for changed logic
- [ ] Tests use Expecto with Expecto.Flip fluent assertions
- [ ] `dotnet run ServerTests` passes

### Medical Safety
- [ ] Changes to dosing logic, rules, or parsing are accompanied by unit tests
- [ ] Spreadsheet column name/semantic changes are reflected in the `Data` record field comments in `src/Informedica.GenFORM.Lib/Types.fs` and the `ColumnContract` test
- [ ] No hardcoded medication values — all rules come from Google Spreadsheets

### Dependencies
- [ ] No new external dependencies without justification in CONTRIBUTING.md
- [ ] BigRational used for all medication calculations (not float)

## Summary

Provide a summary of:
1. What this PR changes and why
2. Any concerns or questions about correctness
3. Whether the medical safety requirements are met
