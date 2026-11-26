# Implementation plan for issue #1834: lay out the parenteralia view as clear preparation instructions

2026-11-26. Line numbers are from `master` at `8b91909b`.

## Problem description

The parenteralia view shows the solution rules of a medication as one block of Markdown that
`SolutionRule.Print.toMarkdown` (`src/Informedica.GenFORM.Lib/SolutionRule.fs:1090`) builds. The text follows
the grouping of the rules (generic, department, substance, patient category, access) with six heading levels,
not the order of the work. Reconstitution is part of the product label, and a product that requires
reconstitution without a reconstitution rule prints "oplossen in ..." with nothing after it. The client
renders the whole text in one colour (`src/Informedica.GenPRES.Client/Views/Parenteralia.fs:149`).

Two callers use the text: the parenteralia view (`ParenteraliaService.get`,
`src/Informedica.GenPRES.Server/ServerApi.Services.fs:264`) and `PrescriptionRule.toMarkdown`
(`src/Informedica.GenFORM.Lib/PrescriptionRule.fs:460`), which gives the text the title "verdunnen".

## Approaches considered

1. Restyle the Markdown in the client only. The order and the wording of the rule dump stay.
2. Send structured data and let the client lay it out. The contract in `Shared` and the client change, and
   `PrescriptionRule.toMarkdown` still needs a text.
3. Keep sending Markdown, and rewrite `SolutionRule.Print` so the text is a preparation instruction in
   the sections of a monograph of the hospital parenteralia handbook. Restyle the Markdown in the client.

## Chosen approach

Approach 3, with the decisions made when the issue was picked up:

- **The server keeps sending Markdown**, and the text stays Dutch.
- **The prototype is the reviewed code.** The prototype worktree for #2006 holds the new `SolutionRule.Print`,
  its tests (`SolutionRulePrintTests` in `tests/Informedica.GenFORM.Tests/Tests.fs`) and the restyled client view.
  The prototype shows the right layout of the parenteralia view. Each step ports one part of it; nothing else in
  that worktree belongs to this plan.
- **#2006 is migrated first.** The text uses eight fields that #2006 adds to `SolutionRule`: ShelfLife, MinPh,
  MaxPh, SameLine, NotSameLine, Notes, PrepText and AdminText. They are read from the new columns of the
  SolutionRules sheet, so this plan starts after #2006 is on master and the server reads the spreadsheet
  with those columns.
- **The split is by section.** Step 1 replaces the text with the sections that the current fields fill, step 2
  adds the sections of the #2006 fields, step 3 restyles the client.

The text has one fixed sequence of sections; a section without data is left out:

1. The title (the medication), the routes and the departments
2. Handelsproducten: the products, the lowest concentration first
3. Oplossen: per product the solvent, the volume and the route, department, age and weight it applies to
4. Bereiden en toedienen: per condition (department, route when there are more, indication, dose type,
   patient, dose and access) the preparation, the dilution step, the concentration and the administration;
   rules with the same condition are alternatives under one heading
5. Houdbaarheid na bereiding, pH, Via dezelfde lijn and Bijzonderheden. A value that every rule gives the same
   is a section of its own; a value that differs between rules is a line of the instruction of each rule.

The code follows the guidelines of the migration:

- Every helper of the text has a single caller in `SolutionRule.Print`, so it stays there and no core API is
  widened for it: the department name, the access word, the enumeration with "of", the age and weight texts.
- `Print.quantity` and `Print.volume` of the prototype have the same shape: a range, else the possible values,
  with the value per adjust unit beside it. One helper takes the three values and serves both.
- Dead code goes: `Print.vuToStr`, `Print.printDoseFraction` and `Print.printDilutionLimit`, which only the old
  `toMarkdown` uses, and `Print.printGenerics` (`SolutionRule.fs:1277`), which nothing calls.
- The viewer script of the prototype, `Scripts/Parenteralia.fsx`, is a tool for reviewing the prototype and is
  not migrated.

## Confidence

High. The text is built from the same rules as now, and the prototype shows it on the spreadsheet with the
new columns. The numbers in the text are the numbers of the rules; only the layout and the wording change.

## Steps

### Step 1: the sections of the current fields (GenFORM)

- `SolutionRule.Print` gets the new text: `condition`, `diluents`, the range-or-values helper, `dilutionStep`,
  `doseFraction`, `instruction`, `section`, `products`, `reconstitutionLines`, `genericToMarkdown` and
  `toMarkdown`. `instruction` gives the dilution, the concentration and the administration of a rule.
- A product that requires reconstitution without a reconstitution rule gives "geen oplosvoorschrift bekend".
- The old `toMarkdown`, `vuToStr`, `printDoseFraction`, `printDilutionLimit` and `printGenerics` are deleted.
- Tests:
  - the `SolutionRulePrintTests` of the prototype that use the current fields: the title, the route and an
    undiluted administration; the condition with the department and the access; the route in the condition only
    with more routes; rules with the same condition under one heading; the dilution step; the product without a
    reconstitution rule;
  - the access test that uses `printDilutionLimit` (`Tests.fs:4203`) checks the access word of
    `Print.condition` instead.
- Estimate: about 300 added and 267 deleted lines under `src/`. The text is one function with its helpers and
  replaces the old module; the pull request says so.

### Step 2: the sections of the #2006 fields (GenFORM)

- `instruction` gives the preparation text and the administration text of a rule, and the extra lines for a
  value that differs between rules.
- `genericToMarkdown` gets the sections Houdbaarheid na bereiding, pH, Via dezelfde lijn and Bijzonderheden,
  with `shelfLifeToString`, `phToString`, `phRange`, `valueLines` and `valueItem`.
- Tests: the sections in the order of the work; a value the same for every rule given once; a value that
  differs given in the instruction of each rule; a shelf life of 0 minutes; the pH with a decimal comma.
- Estimate: about 130 lines.

### Step 3: the style of the text (client)

- `Views/Parenteralia.fs` gives the Markdown three heading levels, the medication, the sections and the
  conditions, each a step smaller, in the colour of the body text, with the spacing of the other pages.
- Estimate: about 46 lines.
