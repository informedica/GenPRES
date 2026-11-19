# Implementation plan for issue #1900

GenCORE calculates every patient value and returns it in the patient data Dto, with the options the user picks
from, as GenORDER does for an order. The server and MCP fill the Dto with plain values and copy the results back.
Drafted 2026-11-19. Line numbers are from `master` at `ea7d4ec8`.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Decisions](#decisions)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

An order goes one way: the server copies the contract order into the order Dto, GenORDER calculates, and the Dto
comes back with the results and the options. The patient does not work this way. `PatientData.Dto.fromDto`
calculates only the post-menstrual age and the body surface area. The server calculates the rest by calling GenCORE
functions one by one, and it has to build GenCORE values to fill the Dto at all. The options the user picks the
patient values from are hard-coded in the client. See #1900.

## What the code does today

### What the server calculates

`Server/ServerApi.Mappers.Patient.fs`:

- `Age.postMenstrualAge` (`:85`), `Age.gestationalAge` (`:95`, with `GestationalAgeNeed.ofAge`) and
  `Age.adultMark` (`:103`, with `AdultMark.ofAge`).
- `Age.estimates` (`:129`) and `Patient.withEstimates` (`:456`) add the 3rd percentile, the mean and the 97th
  percentile of the weight and the height (`Estimates.ofAge`).
- `Patient.estimated` (`:500`) and `Patient.estimating` (`:523`) estimate for EHR patient data, and
  `Patient.estimate` (`:568`) for a draft.
- `Age.ofDays` (`:40`) and `Patient.toModel` (`:319`) turn an age into years, months, weeks and days
  (`AgeValue.fromDays`).
- `PatientDataCommand.processCmd` (`Server/ServerApi.PatientDataCommand.fs:23`) chains these for the patient panel.

### Why the server cannot fill the Dto with plain values

`GenCORE.Lib/Patient.fs`:

- **The department.** The Dto `Department` (`:2212`) holds the kind and the name in a single string, such as
  `"Department NEO"`. `Department.fromString` (`:1563`) refuses a plain name, so `Patient.department`
  (`Server/ServerApi.Mappers.Patient.fs:219`) and MCP (`MCP.Lib/McpTools.GenOrder.fs:148`) build an
  `OtherDepartment` and call `toString`.
- **The weight and the height.** The server builds `WeightAtDate`, `Source` and `Weight` values and calls
  `Weight.Dto.toDto` (`Server/ServerApi.Mappers.Patient.fs:247`); MCP does the same.
- **The adult mark and the need for a gestational age.** `PatientData` has `MarkedAdult: bool` (`:218`) and no place
  for the `AdultMark` or the `GestationalAgeNeed`.
- **The estimates.** `PatientData` holds only the calculation weight and height (`Source<_> option`), so a measured
  patient has no place for the estimates that the contract shows next to the measured value.

### The options

- The client hard-codes every list in `Client/Views/Patient.fs`: weights in gram (`:355`), heights 40 to 220 cm
  (`:361`, again at `:559`), years 0 to 19 with 19 shown as "> 18" (`:501`), months 1 to 11 (`:511`), weeks 1 to 3
  (`:521`), days 1 to 6 (`:531`), gestational age weeks 24 to 42 (`:576`) and days 1 to 6 (`:588`).
- GenCORE has the same weight and height lists as `Estimates.weightSteps` and `heightSteps` (`:1821`, `:1833`).
  `Estimates.ofAge` rounds the estimated mean to the nearest step, so an estimate is always a value the user can
  pick.
- GenCORE validates other ranges than the client offers: years 0 to 120 (`AgeValue.Validation.maxYear`), weeks 0 to
  4, and gestational age weeks 20 to 52 (`AgeWeeksDays.Validation`, also used for the post-menstrual age).
- The departments already come from the server, through the settings.
- The normal-value tables are data: GenFORM reads the sheets `weight`, `height`, `weight neo` and `height neo`
  (`GenFORM.Lib/Resources.fs:291`, `:397`).

### The readers of the patient data Dto

`PatientData.Dto.fromDto` in `src/`: `Server/ServerApi.SqlAdapters.fs:633`, `Server/ServerApi.Mappers.Patient.fs:422`,
`:435`, `:489`, `GenORDER.Lib/Patient.fs:90`, `MCP.Lib/McpTools.GenOrder.fs:125`, and `Patient.Dto.fromDto`
(`GenCORE.Lib/Patient.fs:2417`) with its caller `Server/ServerApi.SqlAdapters.fs:172`. In `tests/`: GenCORE, GenFORM,
GenORDER (`SamplePatients.fs`) and the server patient mapper tests.

## Approaches considered

### Keep the current code

The server keeps calculating patient values in its mapper, and the client keeps its own lists.

### GenCORE calculates, the server copies

The Dto takes plain values. `fromDto` gets the normal-value tables and the weight and height options, calculates
every value and returns the results and the options in the Dto. The server, MCP and the client only copy and read.

## Chosen approach

**GenCORE calculates, the server copies**, as the order does. Every patient rule then lives in GenCORE, and the
options the user picks from are defined once, next to the checks that accept them.

## Decisions

Decided by the maintainer on 2026-11-19.

1. **The estimates live on the patient data.** `PatientData` gets an `Estimates` field with the existing `Estimates`
   type (3rd percentile, mean and 97th percentile of the weight and the height), and so does its Dto. The
   calculation weight and height stay as they are: the measured value, or else the estimated mean.
2. **The adult mark and the need for a gestational age are DUs.** `PatientData.MarkedAdult: bool` becomes
   `AdultMark: AdultMark`, and a `GestationalAgeNeed` field is added. The Dto keeps the plain `MarkedAdult` bool as
   the choice of the user, and adds the mark and the need, which `toDto` writes.
3. **The tables and the weight and height options are passed in.** The Dto creation takes the normal-value tables
   and the lists of weights and heights the user picks from. GenFORM reads the lists like the rules, from a sheet the
   maintainer provides, with the values as lists of options. `Estimates.ofAge` rounds to these lists, and
   `Estimates.weightSteps` and `heightSteps` go.
4. **The department has a Dto field per case.** The Dto has an optional string for `AdultICU`, `PediatricICU`,
   `NeonatalICU`, `AdultDepartment`, `PediatricDepartment` and `OtherDepartment`, and a bool for `AnyDepartment`.
   No field set is `UnknownDepartment`; more than one field set is a validation error.
5. **The Dto offers the age options, the client picks.** The range checks on the age parts stay. GenCORE defines
   the options next to its validators and the Dto returns them, as the order context filter offers its options:
   years 0 to 120, months 0 to 11, weeks 0 to 4, days 0 to 6, gestational age weeks 20 to 52 and days 0 to 6.
6. **The options reach the client on the contract patient data.** The contract `PatientData` gets an options record
   (weights, heights, years, months, weeks, days, gestational age weeks and days), which the server copies from the
   GenCORE Dto.
7. **The patient form waits for the patient data.** The client keeps no lists of its own and shows the form when
   the patient data arrives with its options.
8. **MCP takes the same path as the client.** Its input takes the age parts in place of `AgeMonths`, within the
   options the Dto offers, and fills the Dto with plain values.
9. **Stored data.** This is an alpha without production data, so the stored fixtures change and the structure
   versions stay.

## Confidence

Medium. The direction is decided. The size is not: the patient data Dto has many readers, and steps 4 and 5
may each need splitting to stay within the 200-line limit.

## Steps

Each step builds and passes its tests on its own. Every step after step 1 is script first, as AGENTS.md describes;
step 6 is client UI code.

1. **The department Dto fields.** GenCORE: the Dto department per case (decision 4), with `fromDto` and `toDto`.
   The server `Patient.ofModel` writes the name to `OtherDepartment` and `toModel` reads `Department.name`; MCP
   writes `OtherDepartment`. `Patient.department` in the server goes. The stored fixtures change.
2. **The adult mark and the need for a gestational age.** GenCORE: `AdultMark` replaces `MarkedAdult` on
   `PatientData`, `GestationalAgeNeed` is added, and `fromDto` sets both (decision 2). The server `toModel` copies
   them, and `Age.adultMark` and `Age.gestationalAge` go; `PatientDataCommand` reads them from the Dto.
3. **The weight and height options as data.** After the maintainer has added the sheet: GenFORM reads
   the lists under a resource key, next to the normal-value tables. `Estimates.ofAge` takes the lists, and
   `weightSteps` and `heightSteps` go. The server and MCP pass the lists.
4. **GenCORE calculates the estimates and returns the options.** `PatientData.Dto.fromDto` takes the tables and the
   weight and height options and fills `Estimates` and, without a measured value, the calculation weight and height
   (decisions 1 and 3). `toDto` writes the estimates and every option (decision 5). Every reader of the Dto passes
   them; GenFORM and GenORDER tests pass none.
5. **The server and MCP copy.** The contract `PatientData` gets the options (decision 6). `Patient.ofModel` fills the
   Dto with plain values, and `toModel` copies the results and the options back. `Age.estimates`,
   `Patient.withEstimates`, `estimated`, `estimating` and `estimate` go, and `PatientDataCommand` becomes
   `ofModel`, `fromDto`, `toDto`, `toModel`. MCP takes the age parts in place of `AgeMonths`, and
   `buildPatient` fills the Dto with plain values, as the client does.
6. **The client reads the options.** The patient form takes every list from the patient data options instead of
   its own arrays, and waits for the patient data.

## Verification

- `dotnet run servertests` passes after every step.
- After step 5, `git grep` finds no `AdultMark.ofAge`, `GestationalAgeNeed.ofAge`, `Estimates.ofAge` or
  `AgeValue.fromDays` outside GenCORE.
- After step 6, `git grep` finds no number list for the patient values in the client.
- A manual check in the app: an anonymous patient of 3 months shows the estimates and the options; a measured weight
  keeps the estimates beside it; an EHR patient without a measured weight gets an estimate.
