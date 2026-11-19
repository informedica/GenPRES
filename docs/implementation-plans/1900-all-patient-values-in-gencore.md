# Implementation plan for issues #1900 and #1911

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
- GenCORE had the same weight and height lists as `Estimates.weightSteps` and `heightSteps`, until step 3 replaced
  them with the sheets `calc_weight` and `calc_height`. `Estimates.ofAge` rounds the estimated mean to the nearest
  value of these lists, so an estimate is always a value the user can pick.
- GenCORE validates other ranges than the client offers: years 0 to 120 (`AgeValue.Validation.maxYear`), weeks 0 to
  4, and gestational age weeks 20 to 52 (`AgeWeeksDays.Validation`, also used for the post-menstrual age).
- The departments already come from the server, through the settings.
- The normal-value tables are data: GenFORM reads the sheets `weight`, `height`, `weight neo` and `height neo`
  of a separate normal-values spreadsheet (`GenFORM.Lib/Resources.fs:291`, `:397`). The rules spreadsheet now has
  the same tables, and the weight, height and eGFR band lists, as sheets the maintainer added on 2026-11-19.

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
   calculation weight and height are the measured value, rounded to the list of calculation values (decision 11),
   or else the estimated mean.
2. **The adult mark and the need for a gestational age are DUs.** `PatientData.MarkedAdult: bool` becomes
   `AdultMark: AdultMark`, and a `GestationalAgeNeed` field is added. The Dto keeps the plain `MarkedAdult` bool as
   the choice of the user, and adds the mark and the need as the GenCORE DUs, which `toDto` writes. A mark under 16
   years is ignored: the patient is not an adult and the patient data is accepted. The patient data keeps only the
   mark GenCORE decided, not the choice of the user: once the age gives an adult or a patient under 16 years, the
   mark of the user is gone, and the user marks the patient again after a change of the age.
3. **The tables and the weight and height options are passed in.** The Dto creation takes the normal-value tables
   and the lists of weights and heights the user picks from. GenFORM reads the lists like the rules, from the sheets
   `calc_weight` (column `WeightKg`) and `calc_height` (column `HeightCm`) of the rules spreadsheet, with the values
   as lists of options. `Estimates.ofAge` rounds to these lists, and `Estimates.weightSteps` and `heightSteps` go.
   The normal-value tables move to the rules spreadsheet as well, to the sheets `norm_weight`, `norm_height`,
   `norm_weight_neo` and `norm_height_neo`, and the separate normal-values spreadsheet is no longer read.
4. **The department has a Dto field per case.** The department gets a case for an outpatient clinic,
   `OutpatientClinic`, with a name. The Dto has an optional string for `AdultICU`, `PediatricICU`, `NeonatalICU`,
   `AdultDepartment`, `PediatricDepartment`, `OutpatientClinic` and `OtherDepartment`, and a bool for
   `AnyDepartment`. A blank name counts as a field that is not set. No field set is `UnknownDepartment`; more than
   one field set is a validation error.
5. **The Dto offers the age options, the client picks.** The range checks on the age parts stay. GenCORE defines
   the options next to its validators and the Dto returns them, as the order context filter offers its options:
   years 0 to 120, months 0 to 11, weeks 0 to 4, days 0 to 6, gestational age weeks 20 to 52 and days 0 to 6.
6. **The options reach the client on the contract patient data.** The contract `PatientData` gets an options record
   (weights, heights, years, months, weeks, days, gestational age weeks and days, and the renal functions), which
   the server copies from the GenCORE Dto.
7. **The patient form waits for the patient data.** The client keeps no lists of its own and shows the form when
   the patient data arrives with its options.
8. **MCP takes the same path as the client.** Its input takes the age parts in place of `AgeMonths`, within the
   options the Dto offers, and fills the Dto with plain values. MCP does not read the normal-value resources or call
   the estimates itself: the server and MCP complete the patient data through one shared function. Letting the MCP
   tools ask the server API, as the client does, is left to issue #1907.
9. **Stored data.** This is an alpha without production data, so the stored fixtures change and the structure
   versions stay.
10. **The renal function options.** The renal functions the user picks from are options too: the eGFR bands and
    the kinds of dialysis. GenCORE keeps `EGFRBand` with its four cases. GenFORM reads the sheet `egfr_bands`
    (`MinEGFR`, `MaxEGFR`, `Band`) of the rules spreadsheet and matches each row to a case by its limits; the text of
    the row is the text of the option. A row that matches no case, or a second row for a case, is a load warning.
11. **A measured calculation value is rounded to the list (#1911).** Decided by the maintainer on 2026-11-19. Every
    measured calculation weight, from the EHR, the user or MCP, becomes the nearest value of the calculation
    weights, and every measured calculation height the nearest value of the calculation heights. The rounding is
    the one of the estimated mean (`Estimates.nearestStep`): the lower value on a tie, and the first or last value
    for a value beyond the list. The rounded value stays `Measured`. The exact value is kept in `Weight.Actual` or
    `Height.Actual`, unless that list already holds it. Every use reads the calculation value, so the order
    adjustment, rule selection by weight range, the body surface area and the other uses all take the rounded value.
    The signing notice compares the calculation weights of the plan and of the EHR.

## Confidence

Medium. The direction is decided. The size is not: the patient data Dto has many readers, and steps 5 to 9
may each need splitting to stay within the 200-line limit.

## Steps

Each step builds and passes its tests on its own. Every step after step 1 is script first, as AGENTS.md describes;
step 10 is client UI code.

1. **The department Dto fields.** GenCORE: the case `OutpatientClinic`, and the Dto department per case
   (decision 4), with `fromDto` and `toDto`. `Department.fromString` goes.
   The server `Patient.ofModel` writes the name to `OtherDepartment` and `toModel` reads `Department.name`; MCP
   writes `OtherDepartment`. `Patient.department` in the server goes. The stored fixtures change.
2. **The adult mark and the need for a gestational age.** GenCORE: `AdultMark` replaces `MarkedAdult` on
   `PatientData`, `GestationalAgeNeed` is added, and `create`, `fromDto` and `Patient.atDate` set both from the age
   (decision 2). `markRefused` and `PatientDataError.MarkedAdultBelowSixteen` go. The server `ofModel` writes the
   mark and the need of the contract on the Dto and `toModel` copies them back. `PatientDataCommand` keeps
   `Age.adultMark` and `Age.gestationalAge` until step 9.
3. **The weight and height options as data.** GenCORE parses the rows of `calc_weight` and `calc_height` into
   `CalculationWeightsAndHeights`, sorted, with a `NormalValueSheetWarning` for a list out of order or a duplicate
   value and a `NormalValueSheetError` for a value that is not a positive number.
   GenFORM reads the rows under a resource key of its own; a sheet that fails to load, has no values or holds a
   value that is not a positive number fails the whole load (decision 3). The normal-value tables are read from the
   `norm_*` sheets of the rules spreadsheet, and `normalValuesUrlId` goes. `Estimates.ofAge` takes the record, and
   `weightSteps` and `heightSteps` go. The server and MCP pass the record.
4. **The renal function options as data.** GenCORE parses the rows of `egfr_bands` into the eGFR band options,
   each a band with the text of its row, and matches a row to a band through `RenalFunction.tryFromString`.
   GenFORM reads the rows under a resource key of its own. A row that matches no band, a second row for a band,
   or a band without a row gives a warning; a sheet that fails to load gives a warning and no eGFR band options
   (decision 10).
5. **GenCORE calculates the estimates and returns the options.** `PatientData` gets the fields `Estimates` and
   `Options` (`PatientDataOptions`). `PatientData.Dto.fromDto` takes the resources as an option
   (`PatientDataResources`: the normal-value tables, the calculation weights and heights and the eGFR band options)
   and fills the estimates, the options and, without a measured value, the calculation weight and height: the
   estimated mean, or none without an estimate (decisions 1 and 3). Without the resources nothing is estimated. The
   age options sit next to their validators; the eGFR bands carry the sheet text and the dialyses are listed without
   a text (decisions 5 and 10). `toDto` writes the estimates as the GenCORE record. The options stay off the Dto, so
   they are not stored with every patient data; they depend only on the resources. Every reader of the Dto passes
   None in this step, so nothing changes in behaviour.
6. **A measured calculation value is rounded to the list.** With the resources, `PatientData.estimate` rounds a
   measured calculation weight and height to the nearest calculation value and keeps the exact value in `Actual`
   (decision 11). Without the resources, the measured value is read as it is.
7. **The server and MCP copy.** Split in three pull requests by the maintainer on 2026-11-19:
   - **7a. The server completes the patient data.** The port field `normalValues` becomes `patientDataResources`,
     the GenCORE `PatientDataResources` with the eGFR band options. Only where the patient data changes does the
     server pass the resources: the panel command completes the contract patient through GenCORE
     (`Patient.complete`) and returns the GenCORE messages when a value is refused, and the EHR read is wrapped by
     `Patient.completing`. The rule lookups, the body surface area and the stored data read with `None`.
     `Patient.ofModel` fills the Dto with plain values, and `toModel` copies the estimates back. `Age.estimates`,
     `Age.gestationalAge`, `Age.postMenstrualAge`, `Age.adultMark`, `Patient.withEstimates`, `estimated`,
     `estimating` and `estimate` go.
   - **7b. The options reach the contract.** The contract `PatientData` gets the options (decision 6), `None`
     when the server sent none. The options depend only on the resources, so `Patient.withOptions` sets them from
     the resources where the server answers with patient data: the panel command, the session and the signing
     answers. Patient data mapped from a Dto, and patient data without the resources, has no options.
   - **7c. MCP.** MCP takes the age parts in place of `AgeMonths`, and `buildPatient` fills the Dto with plain
     values and passes the resources, as the server does.
8. **One function completes the patient data.** The server and MCP complete the patient data through one shared
   function that takes the resource provider and the patient data Dto, reads the normal-value tables and the
   calculation weights and heights, and returns the completed patient data (decision 8). It lives in a library
   that both reference; its place and name are decided in the script review. The server and MCP no longer read
   these resources themselves. `Patient.atDate` sets the age of an identified patient from the birth date but does
   not estimate again, because it has no resources; the shared function estimates after the age is set.
9. **Commands on the patient Dto.** GenCORE applies a command, such as a pick from the options, to the patient
   data Dto and returns the completed Dto, as GenORDER applies a filter pick to an order context. Because every
   value is picked from the options that GenCORE offers, the command cannot fail. `PatientDataCommand` sends the
   command and the Dto, and `Age.adultMark` and `Age.gestationalAge` go.
10. **The client reads the options.** The patient form takes every list from the patient data options instead of
    its own arrays, and waits for the patient data.

## Verification

- `dotnet run servertests` passes after every step.
- After step 9, `git grep` finds no `AdultMark.ofAge`, `GestationalAgeNeed.ofAge`, `Estimates.ofAge` or
  `AgeValue.fromDays` outside GenCORE.
- After step 10, `git grep` finds no number list for the patient values in the client.
- A manual check in the app: an anonymous patient of 3 months shows the estimates and the options; a measured weight
  keeps the estimates beside it; an EHR patient without a measured weight gets an estimate; an EHR weight of 12.34 kg is used as 12.5 kg.
