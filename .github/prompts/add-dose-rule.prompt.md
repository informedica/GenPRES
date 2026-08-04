---
description: "Add a new dose rule or medication configuration to GenPRES"
---

Add or modify a medication rule (dose rule, solution rule, product, etc.) in GenPRES.

**Important:** GenPRES stores all medication rules in Google Spreadsheets. Do not hardcode values in source files.

## Steps

1. **Understand the sheet structure** — read the matching `Data` record in `src/Informedica.GenFORM.Lib/Types.fs` (its XML summary names the sheet and parser; the field comments carry column names, units and encodings) and the declared column lists in `DoseRuleToDataTests.ColumnContract` (`tests/Informedica.GenFORM.Tests/Tests.fs`).

2. **Locate the parser** — find the corresponding module:
   - Dose rules → `src/Informedica.GenForm.Lib/DoseRule.fs`
   - Solution rules → `src/Informedica.GenForm.Lib/SolutionRule.fs`
   - Products → `src/Informedica.GenForm.Lib/Product.fs`
   - Mappings → `src/Informedica.GenForm.Lib/Mapping.fs`

3. **Prototype changes in a script** — create a `.fsx` script in `src/Informedica.GenForm.Lib/Scripts/`:
   ```fsharp
   #I __SOURCE_DIRECTORY__
   Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
   #load "load.fsx"

   open Informedica.GenForm.Lib
   // Shadow the module and add/modify parsing logic
   ```

4. **Write tests** in the script to verify parsing and rule evaluation.

5. **Update documentation** — if you add or rename spreadsheet columns, update `docs/mdr/design-history/genpres_resource_requirements.md`.

6. **Do not modify `.fs` source files** — leave migration to the user.

## Key Patterns

- Use `Csv.getStringColumn` / `Csv.getFloatOptionColumn` for column access
- Return `GenFormResult<'T>` (i.e., `Result<'T, Message list>`) from parsing functions
- Use `BigRational` for all numeric medication values (absolute precision required)
- Use existing helpers: `BigRational.toBrs`, `getFloat`, `Option.bind BigRational.fromFloat`
