# Implementation plan for issue #1884

GenCORE dates a weight or a height by where the value comes from. A value from the EHR keeps the date
the EHR gives. A value from a request has no date. Drafted 2026-11-18. Line numbers are from `master`
at `dc2863ad`.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Decisions](#decisions)
- [Open points](#open-points)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

The calculation weight and the calculation height of GenCORE patient data need a date. A weight or
height in a request has no date, so the server gives it the date of the request. For anonymous
patient data nothing reads that date. For identified patient data it ends up in a signed version,
where the date of signing is already known, and code exists only to keep it the same between the
signing challenge and the submission. See #1884.

## What the code does today

### The types

`GenCORE.Lib/Patient.fs`:

- `Weight` (`:141`) holds `Actual: WeightAtDate list`, `Birth: WeightValue option`,
  `Admission: WeightAtDate list` and `Calculation: Source<WeightAtDate> option`. `Height` (`:157`) is
  the same without the admission list.
- `WeightAtDate` (`:150`) and `HeightAtDate` (`:165`) are a value with a date.
- The Dto of a dated value has a `DateTime` that defaults to `DateTime.MinValue`.
  `WeightAtDate.Dto.fromDto today` refuses a missing date and a date after `today`. The height works
  the same way.

### The two sources

- **From the EHR.** The stub EHR builds the patient with dated measurements and `Patient.createAt`
  (`Server/ServerApi.StubAdapters.fs:180`, `:295`). The fixed stub patient is measured on a fixed
  date; a test patient is measured on the day of the read (`:277`). Stored EHR data is read with
  `Patient.Dto.fromDto today` (`Server/ServerApi.SqlAdapters.fs:166`). Only EHR data fills the dated
  lists.
- **Estimated by the server.** For an EHR patient without a measured weight or height, the server
  estimates them from the age when it projects the EHR patient to patient data. `Patient.estimating`
  (`Server/ServerApi.Mappers.Patient.fs:513`) wraps the `patientData` member of the EHR port, not
  `read` (`Server/ServerApi.Adapters.fs:555`), and `Patient.estimated` (`:486`) dates each estimate
  with the local date of the projection. `PatientData.fromAgeType` dates the normal weight and height
  of an age type.
- **From a request.** The wire `Weight` and `Height` (`Shared/Types.fs`) have no dates, in both
  modes. `Patient.ofModel today` (`Server/ServerApi.Mappers.Patient.fs:245`) dates them with `today`
  and uses the date for nothing else. The GenORDER setters `setWeightFrom` and `setHeightFrom`
  (`GenORDER.Lib/Patient.fs:101`, `:126`) take a date, and so do `setWeight`, `setEstimatedWeight`,
  `setHeight` and `setEstimatedHeight` (`:112`, `:116`, `:137`, `:141`), which pass it on. MCP calls
  these four with today (`MCP.Lib/McpTools.GenOrder.fs:130-155`); the tests pass a fixed date. Step 2
  deletes these setters.

A request does not say which mode the patient is in: the identity is on the session, not on the
patient data of a request. Each code path does know its source.

### Who reads the date

- The rules read only the value: `PatientData.weightInKg` (`:2078`), `heightInCm` (`:2084`) and
  `calcBsa` (`:2091`).
- `WeightAtDate.toString` (`:926`) and `HeightAtDate.toString` (`:1237`) print the date;
  `Weight.toString` (`:1031`) and `Height.toString` (`:1317`) sort the lists by it. Only
  `Patient.toString` calls them, and nothing outside GenCORE calls that.
- `Session.Reads.sameDataApartFromAges` (`Server/ServerApi.Session.fs:510`) compares two EHR readings,
  apart from the ages, so a new measurement date counts as a change. It compares what `read` returns
  (`:1505-1509`, `:536`), which has no estimates, so the dates of the estimates never reach it.
- The check against the read date, in `WeightAtDate.Dto.fromDto` (`:949`) and
  `HeightAtDate.Dto.fromDto` (`:1260`), with the validator `notAfterValidator` (`GenCORE.Lib/Validus.fs:39`),
  which also checks the birth date (`:778`). For a value from a request it cannot fail, because the date
  was set to the read date just before.

### Code that exists only because a request value has a date

`Session.challengeDate` (`Server/ServerApi.Session.fs:1566`), the port member `challengeDate`
(`Server/ServerApi.Ports.fs:400`) and its adapters, and the branch in `SigningCommand.processCmd`
that reads a submission on the date of its challenge. A signing challenge is a digest of the parsed
plan, and the parsed plan holds the dates of the request. A signed version already records its date
in `SignedAt` (`Server/ServerApi.OrderPlanVersion.fs:43`).

### Stored data

Three stores hold dated weights and heights. An older structure version loads as unreadable.

- The order plan JSON (`SqlDatabase.jsonVersion = 16`, `Server/ServerApi.SqlAdapters.fs:204`), from a
  request. Its fixture `order_plan_v16.json` holds the date of the request 6 times.
- The patient data of a data notice (`patientDataJsonVersion = 2`, `:622`), written at `:1085-1086`
  and read with `readPatientData` (`:627`). It is the EHR patient projected through `patientData`
  (`Server/ServerApi.Session.fs:1513`), so its measurements have the EHR dates and its estimates the
  local date of the projection. It has no file fixture.
- The stored EHR data (`ehrPatientJsonVersion = 2`, `:151`), from the EHR. It has no file fixture; its
  tests build the JSON from the stub EHR. The fixture `ehr_data_v1.json` is version 1 and only tests
  that version 1 is refused.

## Approaches considered

### A split by mode

Anonymous patient data without a date, identified patient data with one. A request does not carry
the mode, so each of the about 20 places in 8 server files that read the patient data of a request would
have to find the mode in the session, or the wire patient would need a mode. Identified patient data
from a request would still only get the date of the request, which `SignedAt` already records, and
`challengeDate` would stay.

### A split by source

A value from the EHR keeps its date; a value from a request has none, in both modes. Each code path
knows its source, so nothing has to find the mode. `challengeDate` and the code around it can go.

### No date on the calculation value

The calculation value has no date at all; only the dated lists keep EHR dates. The EHR date of the
measurement in use would no longer be on the calculation value.

## Chosen approach

**A split by source.** It needs no knowledge of the mode, keeps the dates that mean something, and
removes the code that keeps the date of a request the same between two requests.

## Decisions

Decided by the maintainer on 2026-11-18.

1. **The representation of an undated value.** The date of `WeightAtDate` and `HeightAtDate` becomes
   an option, `Date: DateTime option`. A value from the EHR has `Some` date, a value from a request
   `None`. The dated lists use the same records, so an entry could be undated, although only the EHR
   fills them. The dates are kept for a future EHR adapter. Today the only EHR is the stub
   (`StubEhrPatient.portAt`, `Server/ServerApi.StubAdapters.fs:307`, wired in
   `Server/ServerApi.Adapters.fs:554`), so after this change only stub EHR data has a date: the fixed
   stub patient on its own date, a test patient on the day of the read. Every other value is `None`
   until a real EHR adapter gives measurement dates.
2. **Stored data.** The structure versions of the three stores stay; the order plan fixture changes.
   This is an alpha without a production store, so there are no stored rows to keep readable.
3. **The estimates.** An estimate is not measured on a date, so it has no date: `Patient.estimated`
   (`Server/ServerApi.Mappers.Patient.fs:486`) and the `fromAgeType` functions set `None`, and the
   `fromAgeType` functions lose their date parameter.

## Open points

- `PatientData.Dto.fromDto` returns a `Result`. What `Patient.estimated` and
  `Session.signedPatientDataAt` return when it refuses the changed Dto is open. Falling back on the
  data before the change is wrong for `signedPatientDataAt`: that data still holds the estimates this
  branch removes.
- The GenFORM test helpers (`TestPatient`, `tests/Informedica.GenFORM.Tests/Tests.fs`) set the age,
  the gestational age, the post-menstrual age, the weight and the height by record update.
  `withPostMenstrualAgeInDays` sets a post-menstrual age that `fromDto` would calculate from the age
  and the gestational age. Whether these helpers go through the Dto is open.

- The client never sends or receives a date for the weight or the height. Nothing on the client
  changes.
- The canonical form of a parsed plan changes, so a signing challenge made before the change does not
  match a submission after it. With no production data this only matters for a running test session.

## Confidence

Medium. The readers of the date are few and known. The size of the test changes is the main risk:
about 50 lines in the tests build a dated weight or height, or call a setter or a `fromAgeType`
function, and about 200 lines when the uses of the helpers are counted. Most changes go through a few
helpers: `SamplePatients` in GenORDER, `TestPatient` in GenFORM, and the fixtures of the GenCORE and
server tests.

## Steps

Each step is one pull request within the limit of 200 changed source lines.

1. **GenCORE.** `Date: DateTime option` in `WeightAtDate` and `HeightAtDate` (decision 1) and their
   Dtos. `WeightAtDate.Dto.fromDto` and `HeightAtDate.Dto.fromDto` check a date against the read date
   only when there is one; the shared `notAfterValidator` stays as it is for the birth date.
   `WeightAtDate.toString` and `HeightAtDate.toString` print an undated value without a date, and
   `Weight.toString` and `Height.toString` sort the lists with it. The six `fromAgeType` functions
   (`:906`, `:1002`, `:1217`, `:1292`, `:2117`, `:2275`) set estimates without a date and lose their
   date parameter (decision 3); their callers are the GenCORE tests and `Scripts/Scripts.fsx`. The
   GenCORE tests follow.
2. **GenORDER and MCP: patient data only through the Dto.** Delete the setters of the GenORDER
   `Patient` module (`GenORDER.Lib/Patient.fs`): `setAge` (`:89`), `setGestAge` (`:144`), `withPMAge`
   (`:85`), `setWeightFrom` (`:101`), `setWeight` (`:112`), `setEstimatedWeight` (`:116`),
   `setHeightFrom` (`:126`), `setHeight` (`:137`), `setEstimatedHeight` (`:141`), `setGender` (`:75`)
   and `setDepartment` (`:154`). A setter changes patient data by a record update, so it has to
   calculate the derived values again itself; through the Dto, `PatientData.Dto.fromDto` calculates
   them. This is step 3 of #1885, which was closed after its step 1 although the setters were still
   there. The getters stay; they cannot make a value stale. In `src/` only MCP calls the setters: MCP
   `buildPatient` (`MCP.Lib/McpTools.GenOrder.fs:140`) and `estimated` (`:115`) build a patient data Dto
   from their input, with undated values (step 1), and read it with `PatientData.Dto.fromDto`; the
   comments "dated today" go. The tests that call the setters build the Dto the same way, about 80
   calls: in GenORDER `SamplePatients`, `OrderPlanTests`, `Tests`, `PlanOnDataTests`,
   `StrictPreparationTests` and `RuleCheckTests`, and in the server `MappersPatientTests` and
   `NutritionRuleSetsTests`.
3. **Server.** `ofModel` sets undated values, and `estimated` sets undated estimates (decision 3), so
   the patient data of a data notice has undated estimates too; the comment of `estimating` loses
   "dated today". `Patient.estimated` (`Server/ServerApi.Mappers.Patient.fs:486`) and
   `Session.signedPatientDataAt` (`Server/ServerApi.Session.fs:751`) change the patient data through the
   Dto instead of a record update with `calculateBodySurfaceArea`: they write the Dto, set or remove
   the calculation weight and height on it, and read it back with `PatientData.Dto.fromDto`. The
   `CoreSource` alias of `Session` (`:414`) then has no use and goes. Remove `challengeDate`, its port member and adapters, the branch in `SigningCommand`
   that reads a submission on the date of its challenge, and their tests. Delete
   `SessionMapper.noticeData` (`Server/ServerApi.Mappers.Session.fs:93`): the server only sends a data
   notice, and only tests call it (`MappersOrderPlanTests.fs:186`, `:191`); that test keeps checking
   `SessionMapper.notice`. Update the fixture
   `order_plan_v16.json` (decision 2). The stub EHR keeps its measurement dates, and the comparison of
   EHR readings does not change.

## Verification

- The patient data of a request reads into GenCORE without a date and gives the same rules, doses and
  body surface area as before.
- A signing challenge made before midnight and submitted after it still matches, without
  `challengeDate`.
- EHR data keeps the dates of its measurements; a value dated after the read date is still refused.
- `dotnet run servertests` passes.
