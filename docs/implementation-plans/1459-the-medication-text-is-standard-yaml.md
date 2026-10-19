# Implementation plan for issue #1459: the medication text is standard YAML

2026-10-19. The medication text becomes a YAML 1.2 document. A Medication Dto, nested under the
type as `Medication.Dto` in the style of the order's, is the one description of it: YamlDotNet's
serializer writes and reads the Dto, and NJsonSchema generates the document's schema from the
same Dto. Every value is structured: the products, the dose and solution limits, every range and
every value with its unit are mappings, and the numbers are exact. The old text format is not
read afterwards; the texts in the repository are converted once, and the code the old format
leaves unused is removed. Closes #1459 and #355. Line numbers are from `master` at `6f0c2b19`.

## Problem description

The medication text looks like YAML but is not: it is indented with tabs, which YAML forbids;
`Components:` and `Substances:` are bare markers and a repeated `Name:` key starts the next item;
a product is packed onto one `Product:` line and a dose or solution limit onto one line of
`[label] value` pairs, parsed by a regular expression. No YAML tool can read, write or validate
it, and every change of the format is a hand-written change of the parser.

## What the code does today

**The types.** `Medication` (`GenORDER Types.fs:396`) holds its `ProductComponent`s (`:433`), each
with its `SubstanceItem`s (`:456`) and its `ComponentProduct`s (`:167`), each product with its
`ProductStrength`s (`:157`). The limits are GenFORM's: `DoseLimit` (`GenFORM Types.fs:291`) with
its `LimitTarget` (`:283`), a dose unit, an optional adjust unit and six ranges; `SolutionLimit`
(`:550`) with a target, three ranges, optional quantities and the products of the solution rule.
A range is GenCORE's `MinMax` (`MinMax.fs:8`): an optional minimum and maximum, each `Inclusive`
or `Exclusive` of a value with a unit. None of these types has `NoEquality`, so records compare
structurally.

**The writer.** `Medication.toString` (`Medication.fs:1039`) returns a `string list`, one line per
field; its callers join the lines. Values go through `ValueUnit.toStringDecimalEngShortWithoutGroup`
(`ValueUnit.fs:1352`), so through `BigRational.toDecimal` and `Array.distinct`; ranges through
`MinMax.toString`; limits through `DoseLimit.toString` (`DoseLimit.fs:174`) and
`SolutionLimit.toString` (`SolutionLimit.fs:39`), which write the labels of their `FieldLabels`
modules; products through `ProductLabels` (`Medication.fs:123`).

**What the text loses.** Whether a limit is inclusive or exclusive: `MinMax.toString` writes
"min " and "max " for both. The adjust unit of a dose limit; the target of a solution limit; the
products of a solution limit; a value that has no exact decimal form; the repetition and order of
values. The round trip holds on the fixtures because their values happen to survive this.

**The reader.** `Parser` (`Medication.fs:133-828`): `parseLine` (`:138`) reads the indent as the
tab count or groups of four spaces; `fromString` (`:632`) groups the lines by indent and starts a
new item at a repeated `Name`; `parseLabeledFields` (`:260`) splits the labeled limits;
`parseDutchDecimal` (`:183`) reads a decimal comma; the ranges are read by GenCORE's
`MinMax.parseMinMax` (`MinMax.fs:1001`), which nothing else in the source calls. Errors are a
`string list`; only the limit errors name their label, the rest name no field, and an unknown key
is ignored. `Medication.fromString` is `Parser.fromString` (`:1103`).

**The strength check, twice.** The reader refuses a product whose strength names no substance of
its component, by name (`Medication.fs:605-613`). The rule path drops such a product:
`createComponents` filters the products with `madeOf`, a local function (`:1192`, applied at
`:1280`), which also asks that the substance's concentrations have the strength's unit group.

**Code that is already unused.** `UnitValidation` (`Medication.fs:21-122`), which checks whether
a unit has an adjust unit, kg or m², or a time unit below the line, is called by no source code,
only by `MedicationTextTests.fs:969-1000`. GenUNITS' `Units.hasGroup` looks for a group anywhere
in a unit, not below the line, so it is no duplicate. `minMaxToString` (`:842`) has no caller.
`SubstanceItem.toString` (`:969`) is called only by `ProductComponent.toString` (`:1006`), which
has no caller; they are the only callers of `valueUnitOptToString` (`:837`), `limitOptToString`
(`:849`) beyond the old writer, and `solutionLimitOptToString` (`:859`).

**The log event.** `Medication.fromRule` logs every medication it creates as text
(`Medication.fs:1445`): `Events.MedicationCreated of string` (`Types.fs:850`), formatted in the
domain code and kept lazy, as a comment there says, so the formatting is skipped when the logger
is off. `OrderLogging.fs:105` prints the event.

**Who reads and writes the text.** In the library only that log event. No server, MCP, client or
benchmark code. The tests:

- `Informedica.GenORDER.Tests`: `TestOrder.parse` (`Tests.fs:96`); `mkOnceTimedOrder` (`:166`),
  which parses `kaliumchlorideOnceTimedText`; the products tests (`:816`), which call `toString`
  and `fromString` at `:972-1009`; the `MedicationParserTests` module (`:1809`) over
  `parseBigRationalOpt`, `parseDutchDecimal`, `parseOrderType` and `parseLine`;
  `MedicationTextTests.fs` with its round trips (`:1005`), its `parseLine` tests (`:1133`) and
  `doseLimitText` (`:895`).
- `Informedica.GenPRES.Server.Tests`: `MappersOrderContextTests.fs:463`.
- `Informedica.GenFORM.Tests`: the `DoseLimit.toString` and `printMinMaxDose` tests
  (`Tests.fs:1592-1742`).
- `Informedica.GenCORE.Tests`: the `MinMax.parseMinMax` tests (`Tests.fs:409-523`).

The scripts `Medication.fsx`, `Constraints.fsx:451`, `Issue1425.fsx:29`, `Issue1425Measure.fsx:34`,
`Scratch/Medication.fsx:125` and `MedicationYaml.fsx`.

**The text is written by hand.** Eight fixtures in `Scenarios.fs` are text literals
(`pcmSuppText` `:18`, `fullMedicationText` `:1060`, `kaliumchlorideOnceTimedText` `:1104`,
`amoxClavText` `:1176` and its two variants, `prednisolonText` `:1365`, `naclLargeRateText`
`:1744`), and nineteen in the `MedicationTexts` module of `MedicationTextTests.fs`. No medication
text is stored outside the repository.

**The Dto pattern.** ADR-0008 gives each aggregate that crosses a boundary a Dto, "nested as
`module Dto` under that type, in the library that owns the type" (invariant 1): `toDto` total,
`fromDto` a `Result` with `DtoError` cases (`Types.fs:822`), absent fields read through
`DtoResult.orEmpty` and `orBlank` (`Dtos.fs:18`). The order's Dtos are classes with `member val`
(`Order.fs:753`). The stored JSON writes a BigRational as `"n/d"` (`Canonical.BigRationalConverter`,
`Dtos.fs:280`). `ValueUnit.Dto` (`ValueUnit.fs:1453`) carries `Value` and `Unit` and also
`Group`, `Short`, `Language` and `Json`. `Order.OrderType.toString` is at `Order.fs:2769`. Rule
R8: domain code never constructs or reads a Dto.

**The compile order** of GenORDER: `Types.fs`, `Utils.fs`, …, `Totals.fs`, `Dtos.fs`,
`Medication.fs`, `Nutrition.fs`, `Patient.fs`, `OrderLogging.fs`, … An F# module cannot be spread
over two files, so everything under `Medication` lives in `Medication.fs`.

**The earlier attempt.** #355 asked for the same and is still open. #356 added
`Scripts/MedicationYaml.fsx`: YamlDotNet's `RepresentationModel`, components and substances as
sequences, but every value still a quoted string in today's notation, read through
`Parser.parseDoseLimitOpt`. It was never migrated, and it predates the products.

**The packages.** YamlDotNet is in no Paket file. NJsonSchema 11.6.1 is in the Main group, used
by NLP.Lib. The GenORDER test project takes its packages from the Test group only.

## Decisions

**Table 1. Taken by the maintainer, 2026-10-19**

| Question | Decision |
|---|---|
| How far the structure goes | Everything: products, dose limits, solution limits, ranges and values with their unit are mappings. |
| One description | A Medication Dto. YamlDotNet's serializer writes and reads it; NJsonSchema generates the schema from it. No hand-built writer, reader or schema. |
| Where the Dto lives | As ADR-0008 has it: `module Dto` nested under `Medication`, in `Medication.fs`, with the Dto classes, `toDto` and `fromDto`. The shapes of the GenFORM and GenCORE types it holds, the limits and the ranges, are nested in it as the text's shapes of those types, not general Dtos of them: GenFORM and GenCORE learn no text format. That departs from invariant 1, "in the library that owns the type", on purpose. |
| Where the text lives | `MedicationText.fs`, compiled after `Medication.fs` and before `OrderLogging.fs`: the serializer settings, the BigRational converter, `toString` and `fromString`. |
| R8 | `MedicationText` builds and reads the Dto, as a boundary module inside the library; domain code calls neither it nor `Medication.Dto`. The log line is printed in `OrderLogging`, which writes events as text, an output step of the same kind. |
| The log event | Carries the medication: `Events.MedicationCreated of Medication`. `OrderLogging.fs:105` writes it with `MedicationText.toString`. The domain code formats nothing: `logInfoLazy` with the formatting at `Medication.fs:1445-1456` becomes `logInfo`, and the comment on the expensive message goes. |
| A string field | Quoted only when another reader would misread it: YamlDotNet's `WithQuotingNecessaryStrings`, with the YAML 1.1 strings included, quotes digits, `yes`, `no`, `on`, `off`, `null` and the like. `Id: "12345"`, `Name: paracetamol`. |
| A number | Plain where it can be: `500`, `0.5`; a value without a finite decimal form as the string `"1/3"`. The writer never produces `"500/1"`: a document the code writes holds only plain numbers and reduced fractions without a finite decimal form. The reader also reads `"500/1"`, the form of the stored JSON. |
| Errors | Typed: new `DtoError` cases, each with the field's path, `UnknownUnit ("Components[0].Dose.DoseUnit", "mgg")`. A structural error, a wrong type or an unknown key, is YamlDotNet's one exception with line, column and property, turned into one case. |
| A limit target | Not in the document: the place of the limit gives it, as the old reader does (`Medication.fs:477`, `:588`, `:788`). A dose limit at medication level targets the orderable, as the rule path's form limit does (`GenFORM DoseRule.fs:367`); one under a component targets that component by its name; one under a substance targets that substance by its generic name, the name the rule path matches on (`Medication.fs:1181`). The same holds for a solution limit. The prototype checks that every fixture's targets equal what their place gives; if one does not, the name comes back into the document by the maintainer's decision. |
| The schema | Generated by a script and committed as `docs/domain/medication.schema.json`, beside the other GenORDER specifications, with the one line `!/docs/domain/medication.schema.json` in `.gitignore`; no new folder, no row in `docs/README.md`, no `.dockerignore` line, as only a test reads it. The file lands in step 4 with the test that fails when it differs from what the code generates. |
| Where the test and the script find the file | The test project names the file as `Content` with a `Link` and `CopyToOutputDirectory`, as the other test projects copy their fixtures; the file stays in `docs/domain/`, and the test reads the copy beside its assembly (`AppContext.BaseDirectory`). The script writes it relative to `__SOURCE_DIRECTORY__`. Not `AppPath.rootPath`: without a `.env` or `data/zindex`, as in a fresh worktree, it falls back to the current directory, and a `GENPRES_ROOT` set to another checkout points at that checkout's file. |
| NJsonSchema in the test project | Listed above `group Test` in the test project's `paket.references`, so it comes from the Main group: one pin. The library itself does not reference it. |
| Unused code | As little as possible stays: what the old format leaves unused goes, and so does what was unused before. |
| `UnitValidation` | Stays where it is, in `Medication.fs`, and `fromDto` uses it: a limit's `QuantityAdjust`, `PerTimeAdjust` or `RateAdjust` needs an adjust unit, its `PerTime` or `Rate` a time unit. |
| The strength check | Stays in the reader only, which refuses; the rule path keeps dropping. Both ask one predicate, `madeOf`, lifted out of `createComponents` into `Medication.ProductComponent` beside `create`. |
| GenFORM's labeled limit text and GenCORE's range reader | `DoseLimit.toString`, `SolutionLimit.toString`, their `FieldLabels` and `MinMax.parseMinMax` go, with their tests, in a step of their own. `printMinMaxDose` stays: `DoseRule.fs:60` prints the dose rules with it. |

**Table 2. Proposed, to confirm**

| Question | Proposal |
|---|---|
| The Dto classes | Classes with `member val`, as the order's, in `Medication.Dto`: `Dto` for the medication, `ComponentDto`, `SubstanceDto`, `ProductDto`, `StrengthDto`, `DoseLimitDto`, `SolutionLimitDto`, `MinMaxDto`, `LimitDto`, `ValueDto`. Strings, bools, `BigRational[]`, nested Dtos and arrays; absent is `null` or an empty array, read through `orBlank` and `orEmpty`. No option, no union. |
| A value with a unit | Its own `ValueDto` with `Value` and `Unit` only, a deliberate second shape beside `ValueUnit.Dto`: the group, the verbosity, the language and the JSON blob are noise in a text people write. |
| A unit | English short without group, as today, read with `UnitsParse.fromString`. The prototype checks every unit of every fixture; where one does not come back, the group form is written. |
| A range | `MinMaxDto` with an optional `Min` and `Max`, each a `LimitDto`: a `ValueDto` and `Inclusive`. The empty range is left out. |
| The order type | The string of `Order.OrderType.toString`, read back by matching over the same cases. The schema lists the allowed values: the schema script reads the cases of the `OrderType` union by reflection and sets them as the string's `enum`, so the union stays the one list. |
| The lossless test record | `losslessMedication`, built as a record in the test, not as a text: an exclusive minimum and maximum, the value 1/3, a value list with a repeat in an unsorted order, an adjust unit on a dose limit. No fixture holds these, as the old reader makes every limit inclusive and the old writer rounds and removes repeats. A record of its own, so the fixtures other tests calculate with stay as they are. |
| The products of a solution limit | Not in the document. GenFORM fills them from the solution rule when it builds the prescription rule (`PrescriptionRule.fs:244-258`), and nothing that uses a medication reads them. They read back empty, as in the old text, so the round trip is exact for a medication without them and not for one built from the rules. |
| The serializer | `SerializerBuilder` leaving out nulls and empty arrays, `WithQuotingNecessaryStrings`, one `IYamlTypeConverter` for BigRational, an emitter that writes `Value` arrays in flow style, `Value: [60, 120, 240]`. `DeserializerBuilder` without `IgnoreUnmatchedProperties`, so an unknown key is an error. Keys are the member names, PascalCase. |
| The strength check in the reader | `madeOf` is stricter than today's check: it also asks for the unit group, and a substance without concentrations fails it. The prototype shows whether every fixture passes. |
| The round-trip test | Whole records compared with `Expect.equal`; on a failure the message shows the two records written as text, so the first field that differs is found by its key. Today's tests partly compare field by field. |
| The old format | Not read afterwards, as #355 accepted. The script converts every literal in the repository once: the old reader, then the new writer. |

## Approaches considered

1. **Quoted scalars in today's notation**, as #356 has it. Valid YAML, but every value is still a
   string only GenPRES can read, and the labeled limits keep their regular expression. Rejected
   by the maintainer's decision.
2. **YamlDotNet's object serializer over the domain records.** F# options, unions and BigRational
   need a converter for every type, and the document's shape would follow the records'
   internals. Rejected.
3. **Hand-built nodes**: a writer and a reader over YamlDotNet's representation model and a
   hand-written schema. Full control over every error and its path, but three descriptions of
   one document, held in step only by a test after the fact. Rejected.
4. **Keep both readers**, the old one for the literals. Two formats to keep in step for texts
   that are all in the repository and can be converted once. Rejected.
5. **The Dto in its own file**, flat classes beside the text code. Departs from ADR-0008 for no
   gain once the text module compiles after `Medication.fs`. Rejected.
6. **A Dto as the one description**, nested under `Medication`, serialized by YamlDotNet in
   `MedicationText`, the schema generated from it, the literals converted, the unused code
   removed. The repository's own pattern for a serializable shape of an aggregate. Chosen.

## Chosen approach

### The document

```yaml
Id: "1"
Name: paracetamol
Route: rect
OrderType: DiscontinuousOrder
Quantity:
  Min: { Value: [1], Unit: stuk, Inclusive: true }
Frequencies: { Value: [2, 3, 4], Unit: x/dag }
Dose:
  DoseUnit: mg
  AdjustUnit: kg
  PerTimeAdjust:
    Max: { Value: [90], Unit: mg/kg/dag, Inclusive: true }
Div: 1
Components:
  - Name: paracetamol
    Form: zetpil
    Products:
      - Id: "12345"
        FormQuantities: { Value: [1], Unit: stuk }
        Strengths:
          - Substance: paracetamol
            Strength: { Value: [500], Unit: mg/stuk }
    Substances:
      - Name: paracetamol
        Concentrations: { Value: [60, 120, 240, 500, 1000], Unit: mg/stuk }
```

The example shows the shape; the prototype's output for `pcmSupp` replaces it in step 1. Whether
the small mappings come out in flow style as shown, or in block style, is the emitter's; the
prototype decides how far that is worth code.

### The Dto

`Medication.Dto` sits in `Medication.fs` after `UnitValidation` and `ProductComponent`, which it
uses. `toDto` is total, one function per type. `fromDto` is one `Result` per type, its errors
collected per level: a unit that does not parse, an order type that names no case, an adjusted
limit without its adjust unit, a product that `ProductComponent.madeOf` refuses. Each limit gets
its target from the component or substance it sits under, or the orderable at medication level.

### The text

`MedicationText.toString` is `Medication.Dto.toDto` then the serializer; `fromString` is the
deserializer then `Medication.Dto.fromDto`, a YamlDotNet exception turned into one `DtoError`.
The callers are the tests, the scripts and `OrderLogging`.

### The schema

A script, `Scripts/MedicationSchema.fsx`, generates the schema with `JsonSchema.FromType` over
`Medication.Dto.Dto`: a type mapper gives a BigRational as a number or a string matching
`^-?\d+/\d+$`; the cases of the `OrderType` union become the `enum` of `OrderType`; the settings
allow an absent member, refuse a `null` and give `additionalProperties: false`. In step 1 the script prints the schema and the prototype validates
the fixtures against it in memory; from step 4 the script writes `docs/domain/medication.schema.json`.
The test reads the file beside its assembly, generates the schema again and compares the two; turns
each fixture's text into
JSON with YamlDotNet and validates it with NJsonSchema, `losslessMedication` among them; and
makes sure three broken documents fail: one with a wrong type and one with an unknown key inside
a nested Dto, else a reference the validator skips would let every fixture pass, and one with
`OrderType: Banana`, which the reader refuses as well.

### What goes

After the switch nothing in the source reads or writes the old format. 3d removes from
`Medication.fs` about 830 lines, 696 of them the `Parser` module (`:133-828`), `parseDutchDecimal`
with it; the rest are `ProductLabels`, the old `toString` and `fromString` (`:1039-1103`),
`SubstanceItem.toString`, `ProductComponent.toString`, `valueUnitOptToString`, `limitOptToString`,
`solutionLimitOptToString` and `minMaxToString`; with them the tests of what it removes:
`MedicationParserTests`, the `parseLine` tests and `doseLimitText` where nothing else uses it.
3e removes `DoseLimit.toString`, `SolutionLimit.toString` and their `FieldLabels` from GenFORM,
`MinMax.parseMinMax` from GenCORE, and their tests.

## Checked, no change needed

- **The client and the server** never read or write the text.
- **The stored session rows** hold the order's Dto JSON, not medication texts; the medication's
  Dto is not stored.
- **The dependency rule**: `scripts/CheckDependencyRule.fsx` has no package rules, so a new
  package moves no allowance; it runs anyway after the steps that change or delete code.
- **The Docker build** runs `Bundle`, which does not depend on the tests, so the schema needs no
  `.dockerignore` line.

## Confidence

High for the round trip: the Dto pattern is the repository's own, and exact numbers remove
today's loss through decimals. High for the schema: generated from the Dto, in the draft
NJsonSchema speaks, with a test against drift of the committed file and a negative test for the
validator. Medium for the layout: flow style for the small mappings and the quoting depend on
YamlDotNet's emitter, which the prototype shows. Medium for the units without group: the
prototype decides. Medium for `UnitValidation` and `madeOf` in the reader: they make `fromString`
stricter than today, and every fixture has to pass.

## Steps

One PR per step, after the maintainer's go. Source lines are shipped code under `src/`, tests and
scripts excluded. Every `.fs` change is prototyped in a script and migrated by the maintainer.

0. **This plan.** `docs(genorder): plan the medication text as YAML`.

1. **The prototype, script only.** `Scripts/MedicationYaml.fsx` rewritten: a shadowed `Medication`
   with its `Dto` module, `toDto` and `fromDto` with their typed errors, `ProductComponent.madeOf`
   lifted out of `createComponents`, `UnitValidation` used; the serializer settings with the
   BigRational converter, the quoting and the flow style for values. `Scripts/MedicationSchema.fsx`
   prints the schema. Expecto tests: the round trip over the eight `Scenarios` fixtures and every
   text literal, read with today's reader, written as text, read back, record equal; every
   fixture passes `madeOf` and `UnitValidation`; an Id of digits and a name `yes` come back as
   strings through the JSON conversion; `"500/1"` reads as 500, and no fixture's text holds a
   fraction that has a finite decimal form, no `"500/1"`, `"1/2"` or `"1/4"`; every fixture's
   limit targets equal what their place gives; `losslessMedication` comes back as the same record,
   its text holding `Inclusive: false` and `"1/3"`; each error case gives its `DtoError`,
   `OrderType: Banana` among them; every fixture and `losslessMedication` validate against the
   schema in memory and the three broken documents do not. The script prints every literal converted, the
   input of 3c, and counts the source lines of each function, so the budgets of 3a and 3b are
   measured. No source change. `docs(genorder): prototype the medication text as YAML`; no
   Greptile review.

2. **The dependency.** YamlDotNet in `paket.dependencies`, `paket.lock` and GenORDER's
   `paket.references`; a CONTRIBUTING.md note. `build(deps): add YamlDotNet to GenORDER`.

3. **The source.**
   - **3a, the writer.** `Medication.Dto` with the Dto classes and `toDto`; `MedicationText.fs`
     with the serializer and `toString`, in the project between `Medication.fs` and
     `Nutrition.fs`. Tests of the shape: they start from the `Scenarios` records, not from the
     text literals, which stay in the old format until 3c. Additive. `feat(genorder): write a
     medication as YAML`.
   - **3b, the reader.** `Medication.Dto.fromDto` with its `DtoError` cases; `madeOf` lifted into
     `Medication.ProductComponent` and `createComponents` calling it from there; the deserializer
     and `fromString`. Tests of the round trip from the `Scenarios` records through `toString`
     and `fromString` back to the same record, and of every error case with small texts written
     in the test. The same round trip over `losslessMedication`, with its text holding
     `Inclusive: false` and `"1/3"`, so what the new text keeps and the old lost stays tested.
     Additive beside the old reader. `feat(genorder): read a medication from YAML`.
   - **3c, the switch.** The literals of `Scenarios.fs`, `MedicationTextTests.fs` and `Tests.fs`
     replaced by their converted text; `Tests.fs:96`, `:166` and `:972-1009` in
     `Informedica.GenORDER.Tests` and `MappersOrderContextTests.fs:463` in
     `Informedica.GenPRES.Server.Tests` calling `MedicationText`; `Events.MedicationCreated of
     Medication` (`Types.fs:850`); the log event at `Medication.fs:1445-1456` as
     `logInfo logger (Events.MedicationCreated med)`, the lazy formatting and its comment gone;
     `OrderLogging.fs:105` writing it with `MedicationText.toString`; the scripts
     that parse a text following. About 25 source lines. `refactor(genorder): the medication text
     is standard YAML`. Closes #1459.
   - **3d, the old reader and the unused code go**, in GenORDER, as listed under "What goes":
     about 830 lines, 696 of them the `Parser` module, all removed. This knowingly exceeds the
     200-line limit, as a PR of deletions only; it splits in two, the `Parser` module and the
     rest, if the maintainer prefers. `refactor(genorder): remove the old medication text`.
   - **3e, the old text helpers go** from GenFORM and GenCORE, as listed under "What goes".
     `refactor(genform): remove the labeled limit text`.

   The order 3a, 3b, 3c, 3d, 3e is fixed: each PR is green on its own only in this order. 3c needs
   the reader of 3b for the converted literals, 3d can only go once 3c has converted them, and 3e
   once 3d has removed the old reader that uses the labels and `parseMinMax`. `Medication.fs`
   grows by the Dto in 3a and 3b and shrinks by about 830 lines in 3d.

4. **The schema.** `Scripts/MedicationSchema.fsx` writes `docs/domain/medication.schema.json`,
   committed with `!/docs/domain/medication.schema.json` in `.gitignore`; the test project's first
   item outside its own folder,
   `<Content Include="..\..\docs\domain\medication.schema.json" Link="medication.schema.json" CopyToOutputDirectory="PreserveNewest" />`,
   so the file keeps its one home; NJsonSchema above `group Test` in
   `tests/Informedica.GenORDER.Tests/paket.references`; the tests: the file matches what the code
   generates, every fixture and `losslessMedication` validate, the three broken documents fail. `test(genorder): validate
   the medication text against its schema`. Closes #355.

## Left out

- **The products of a solution limit in the document.** GenFORM fills them from the solution
  rule when it builds the prescription rule (`PrescriptionRule.fs:244-258`), and nothing that
  uses a medication reads them; only GenFORM's `SolutionRule.fs:410` reads them, from the rules.
  They read back empty, as in the old text. So the round trip is exact for a medication without
  them, the fixtures and `losslessMedication` included, and not for one built from the rules.
- **A lean variant of `ValueUnit.Dto`.** The order's stored JSON depends on its fields; the text
  takes its own `ValueDto` instead.

## Verification

- `dotnet run build`, `dotnet run servertests`, `CI=true` in a worktree
- `dotnet fantomas --check`
- `dotnet fsi scripts/CheckDependencyRule.fsx` after 3c, 3d and 3e
- the script of step 1 run again against the source after 3c, through `MedicationText`
- every converted literal loads in a YAML tool outside .NET and validates against the committed
  schema
