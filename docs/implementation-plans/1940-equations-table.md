# Implementation plan for issue #1940: select the order equations from a declarative equations table

2026-11-21. Line numbers are from `master` at `df8b72e7`.

## Problem description

`EquationMapping.getEqsMapping` (`src/Informedica.GenORDER.Lib/EquationMapping.fs:276`) recognises
equations by their text to decide which ones an order gets:

- an equation that contains `_orb_qty_adj` only with an orderable quantity adjust constraint (line 287);
- any other equation that contains `_adj` only with an adjusted dose constraint (line 288);
- the tie equations (`isTieEquation`, line 111) only with more than one component (line 293);
- when the order leaves out the item concentration products, no equation with `[itm]_orb_cnc` except
  the tie equations, and a timed or once timed order gets the component rate equation appended
  (`componentRateEquations`, line 202; lines 301 to 311);
- the sum equations are the equations that contain the text `sum` (line 313).

The rules depend on the text and on the order of the match cases: the orderable quantity adjust case
must come before the `_adj` case, because its equation also contains `_adj`. A new equation can fall
under the wrong rule because of its text.

The Equations sheet of "GenPRES - NewSolutionRules" now holds the rules as mark columns: the five dose
types, and dosAdjust, orbQtyAdjust, tie, itmCnc, noTimedCnc, noOnceTimedCnc and sum. The issue describes
what each mark means.

## Approaches considered

1. Keep the text matching and document it. The risk of a new equation under the wrong rule stays.
2. Read the Equations sheet at runtime. GenORDER is in the core ring and cannot read a sheet, so GenFORM
   would load the table as a resource and pass it to GenORDER. An edit of the sheet would then change
   the equations of every order after the next reload.
3. Give each row of the `equations` list in the code the mark columns of the sheet, and select the
   equations from the marks.

## Chosen approach

Approach 3, with the decisions made when the issue was picked up:

- **The table lives in code.** `EquationMapping.equations` (line 126) gets the marks of the sheet for
  each equation. The tests compare it with a copy of the sheet, as `EquationsTests.golden` does now
  (`tests/Informedica.GenORDER.Tests/Tests.fs:3435`). The sheet stays documentation; nothing reads it.
- **The order stays as it is.** The table keeps the order of the current list, and the equations added
  by noTimedCnc and noOnceTimedCnc still come last. The solver sorts its queue by value count with a
  stable sort (`src/Informedica.GenSOLVER.Lib/Solver.fs:77`), so the input order decides between
  equations with the same count. With the same order the solver takes the same path. The sheet lists
  the equations in another, grouped order; the golden table follows the order of the code.
- **The conditions stay in code.** `usesAdjust`, `usesOrderableQuantityAdjust`, the component count and
  `leavesOutItemConcentrationProducts` stay. Each mark column maps to one condition. The text matching
  in `getEqsMapping`, and `isTieEquation`, `isOrderableQuantityAdjustEquation`, `isComponentRateEquation`
  and `componentRateEquations` go. The split of the product equations into item, component and
  orderable equations stays on the placeholders `[itm]` and `[cmp]`.

Decided in the script review: a row is a record of the equation, its dose types and a list of marks,
one union case per mark column of the sheet. The selection is a function of the dose type and the
condition marks that hold for an order, so that a test can run every combination of conditions.

## Confidence

High. The selected sets are already shown equal for 60 cases outside the code, and the order of the
equations does not change. The golden orders (`Tests.fs:1659`) check that every solved order stays the
same.

## Steps

### Step 1: the marks in the equations table (GenORDER)

- Every row of `EquationMapping.equations` gets the marks of the Equations sheet: dosAdjust,
  orbQtyAdjust, tie, itmCnc, noTimedCnc, noOnceTimedCnc and sum. The 60 rows stay in their current
  order. The three rows of the sheet without a dose type (8, 21 and 37) stay out of the code list.
- `getEqsMapping` keeps an equation of the dose type when every mark of the row holds for the order:
  - dosAdjust: the order has an adjusted dose constraint (`usesAdjust`);
  - orbQtyAdjust: the order has an orderable quantity adjust constraint (`usesOrderableQuantityAdjust`);
  - tie: the order has more than one component;
  - itmCnc: the order does not leave out the item concentration products.
- When the order leaves out the item concentration products, a timed order also gets the rows marked
  noTimedCnc and a once timed order the rows marked noOnceTimedCnc, appended after the other equations.
- The sum equations are the rows marked sum; the other rows are product equations.
- The helpers `isTieEquation`, `isOrderableQuantityAdjustEquation`, `isComponentRateEquation` and
  `componentRateEquations` are deleted. The tests that use `isOrderableQuantityAdjustEquation`
  (`Tests.fs:3934`, `:4253`, `:4269`, `:4280`) take the orderable quantity adjust equations from the
  marks instead.
- Tests:
  - the golden table follows the sheet: the 63 rows of the sheet with all mark columns, in the order of
    the code; the unused sum equations, which the sheet no longer has, go;
  - the marks of every row equal the golden table;
  - for every dose type and every combination of the four conditions, the selection is the one the
    current `getEqsMapping` gives, in the same order. The script runs the current code and the new code
    side by side to show this; the migrated test checks the selection against the golden table;
  - the existing equation mapping tests and the golden orders pass unchanged.
- Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1940EquationsTable.fsx`.
- Estimate: 60 to 120 lines.
- Built: 307 changed lines under `src/`, 132 of them deleted. The 60 rows of the table each count twice,
  as a deleted and an added line. The step is a pure refactor: every order gets the same equations in
  the same order, so it stays one pull request.
- The tests that filtered the mapped equations with `isOrderableQuantityAdjustEquation` use a test
  helper that looks for the `_orb_qty_adj` variable. A mapped equation has the names of the order in it
  and no row, so the marks cannot be read there.
