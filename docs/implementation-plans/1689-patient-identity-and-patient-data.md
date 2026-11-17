# Implementation plan for issue #1689

A patient is a patient identity with patient data. GenCORE owns both, and GenFORM and
GenORDER work with the patient data only. Drafted 2026-11-17. Line numbers are from `master` at
`0d4f0a4a`.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)

## Problem description

Three libraries and the server each model the patient, under names that do not say what a value
holds. GenFORM and GenORDER carry concepts that are not theirs: the identity of a patient,
the EHR, and the signing of an order plan. Issue #1689 describes the problem; its text predates
the split of the GenCORE patient into an identity and data, so this plan replaces its steps.

## What the code does today

**GenCORE** (`src/Informedica.GenCORE.Lib/Patient.fs`)

- `Patient` is `Identity: PatientIdentity option` and `Data: PatientData` (lines 267-272).
  `PatientIdentity` is the id, the name and the birth date (lines 223-231).
- `PatientData` holds the department, the diagnoses, the gender, the age as an `AgeValue`, the
  dated weights and heights, the gestational age, the post-menstrual age, and the fields
  `EnteralAccess` and `VenousAccess` (lines 235-262).
- The types `EnteralAccess` and `VenousAccess` (lines 32-44) are only ever given the values
  `UnknownEnteral` and `UnknownVenous`: in `PatientData.unknown` and in
  `ServerApi.StubAdapters.fs` (lines 211-212 and 324-325). No code outside `Patient.fs` reads them.
- `Gender` is `Male | Female | AnyGender | UnknownGender` (lines 57-61).
- `PatientData` has no renal function, no location, no mark as an adult, and nothing that says
  whether a weight or a height is measured or estimated.
- `Calculations.Renal` (`Calculations.fs:122`) calculates an eGFR with four formulas and classifies
  it in six classes. No code under `src/` outside that file uses it.

**GenFORM** (`src/Informedica.GenFORM.Lib`)

- `Patient` (`Types.fs:371-403`) is the patient data the rules match: the location, the
  department as a string, the diagnoses, the gender, the age, weight, height, gestational age and
  post-menstrual age each as a single `ValueUnit option`, the flags `WeightMeasured` and
  `HeightMeasured`, `MarkedAdult`, a list of `AccessDevice`, and a `RenalFunction option`.
- `EhrPatientData` (`Types.fs:433-441`, module at `Patient.fs:565`) wraps the GenCORE `Patient`,
  identity included, and adds a renal function and an access list. `Patient.ofEhr`
  (`Patient.fs:729`) and `Patient.withAgesOf` (`Patient.fs:717`) take it.
- `Gender` is `Male | Female | AnyGender`, `AccessDevice` is `PVL | CVL | EnteralTube | AnyAccess`,
  and `RenalFunction` is `EGFR of int option * int option` or one of three dialysis kinds
  (`Types.fs:54-76`). The same three types serve the patient and the rule (`PatientCategory`,
  `RenalRule`).
- A patient without a known gender is given `AnyGender`: `Patient.ofEhr` and the server mapper
  (`ServerApi.Mappers.Patient.fs:183`) both map `UnknownGender` to it. `Gender.filter`
  (`Patient.fs:64`) then matches such a patient only to a rule whose gender is `AnyGender`.
- `RenalRule.filter` (`RenalRule.fs:379-397`) matches a patient eGFR to a rule when both bounds
  are equal to the bounds of the rule. A second branch handles a patient eGFR whose two bounds
  are the same value. The client offers four eGFR bands and three dialysis kinds
  (`Client.Core/Models/RenalFunctionText.fs:24-60`), none with two equal bounds.
- Patient logic that is not rule matching: the empty patient `Patient.patient`
  (`Patient.fs:668`), `meetsMinimumData` and `validate` (`Patient.fs:833-858`), the string forms
  of gender, access and renal function (`Patient.fs:23-563`), the patient Dto (`Patient.fs:862`),
  and `Utils.Calculations.calcDuBois` (`Utils.fs:121`), which repeats the body surface area
  calculation over value units that GenCORE has in `ValueUnit.fs:97`.
- `Adult` (`Patient.fs:73`) already takes the adult rule from the GenCORE `AdultMark`.

**GenORDER** (`src/Informedica.GenORDER.Lib`)

- `Patient.fs` aliases the GenFORM `Patient`, repeats the empty patient field by field (line 23),
  and defines its own `Weight` and `Height` unions (lines 92-140) beside the GenCORE `WeightValue`
  and `HeightValue`. Its age functions already call GenCORE.
- `OrderPlanVersion` and `Signer` (`Types.fs:1064-1101`) with their Dtos (`OrderPlan.fs:622-722`)
  hold a patient id, the signer, the time of signing, a verified flag and
  `EhrData: EhrPatientData option`. GenORDER only defines and serializes them. The server builds
  every version: `ServerApi.Session.fs:1695`, `ServerApi.Mappers.Session.fs:38` and
  `ServerApi.SqlAdapters.fs:285`.
- `DtoError.EhrData` (`Types.fs:1153`) is raised only by `OrderPlanVersion.Dto.fromDto`.
- A version is stored as one row of the table `order_plan` (`ServerApi.SqlAdapters.fs:358-384`).
  The id, the number, the patient id, the base, the signer, the time of signing, the verified
  flag and the patient identity each have a column. The column `plan` holds the JSON of the whole
  `OrderPlanVersion.Dto`, which repeats all of those except the identity and adds the order plan
  Dto and the EHR data Dto. `readRow` (line 219) refuses a row whose JSON disagrees with the
  columns.
- The EHR data of a version is written at the signing (`ServerApi.Session.fs:1703`) and exists
  only inside that JSON. No code under `src/` reads it back from a version.
- The records `OrderContext` and `OrderPlan` have a field `Patient: Patient`
  (`Types.fs:938`, `Types.fs:1038`), as do the GenFORM records `DoseFilter`, `SolutionFilter` and
  `PrescriptionRule` (`Types.fs:768`, `800`, `811`).

**Server and contract**

- The EHR port returns `EhrPatientData` and projects it to the GenFORM `Patient`
  (`ServerApi.Ports.fs:129-134`). The server stores `EhrPatientData` as JSON in three tables
  (`Sql/006-ehr-data.sql`, `Sql/007-ehr-flight.sql`) and inside the stored version.
- `ServerApi.Mappers.Patient.fs` maps gender, access and renal function between the contract and
  GenFORM by hand (lines 179-222) and builds the GenFORM patient Dto from the contract patient
  (`ofModel`, line 233).
- The contract (`src/Informedica.GenPRES.Shared/Types.fs`) names the patient data `Patient`
  (line 88) and the identity `NameAndBirthDate` (line 706), in the field
  `PatientContext.Identity` (line 722). Its `Gender` is `Male | Female | UnknownGender` and its
  `Access` is `CVL | PVL | EnteralTube` (lines 128-136).

## Decisions

The maintainer decided, on 2026-11-17:

1. Everything about the patient lives in GenCORE. Code elsewhere that repeats it is replaced by a
   call to GenCORE. `Calculations.Renal` stays.
2. The type names are `Patient`, `PatientIdentity` and `PatientData`, in every library, in
   the contract, in the client and in every Dto.
3. GenFORM and GenORDER know patient data only. The EHR is not a concept of GenFORM or GenORDER.
4. `OrderPlanVersion` and `Signer` move to the server.
5. `PatientData` gets an access list of `PVL | CVL | EnteralTube`, without `AnyAccess`.
6. `PatientData` gets a renal function: an eGFR band or a dialysis kind, with a conversion from a
   calculated eGFR to a band.
7. The gender of a patient is `Male | Female | UnknownGender`. `AnyGender` is a value of a rule
   only.
8. `Location` and `MarkedAdult` move to `PatientData`.
9. The contract and the client are in scope.
10. The server keeps storing the EHR data it reads: for a session, a data notice, a challenge and
    a signed version. That answers #1698.
11. GenCORE says whether a weight or a height is measured or estimated. Today only the GenFORM
    flags `WeightMeasured` and `HeightMeasured` and the contract say so.
12. The weight and the height of an anonymous patient get the current date, which the server
    passes in.
13. The department of a patient is the GenCORE `Department` union. A rule names a department as a
    string, and the match compares that string with `Department.name`.
14. An eGFR band includes its maximum: an eGFR of exactly 50 is in the band 30 to 50, of exactly
    30 in the band 10 to 30, and of exactly 10 in the band below 10.
15. The column `plan` of a stored version holds the order plan Dto alone. The EHR data of a
    version gets its own two columns, as the other three tables have.

## Approaches considered

- **Keep the GenFORM patient and rename it to `PatientData`**, as #1689 proposed. This leaves two
  types for patient data and the patient logic in two libraries.
- **One `PatientData` type in GenCORE that the rules read.** This is decisions 1 and 3.

## Chosen approach

One `PatientData` type in GenCORE. The types that describe a rule stay in GenFORM:
`PatientCategory`, the GenFORM `Gender` with `AnyGender`, `AccessDevice` with `AnyAccess`, and the
eGFR range of a `RenalRule`. GenFORM maps the patient data to those types where it matches a rule.

No upgrader is written for JSON stored before a step: the application is in alpha and holds no
production data.

## Confidence

Medium. The moves and renames are mechanical. Step 4 changes the type the rules match: the
rules must select the same rules as before for every patient the client and the EHR can give.

## Steps

Each step goes through a script with its tests, is reviewed, and migrates in one pull request. A
step marked *rename* or *move* adds no behaviour and exceeds the limit of 200 lines.

1. **Move `OrderPlanVersion` and `Signer` to the server** (*move*). Nothing stored changes.
   - The two types leave GenORDER with their Dtos and `DtoError.EhrData`. GenORDER keeps
     `OrderPlan` and its Dto. The types go in the server project before the first file that uses
     them, `ServerApi.Ports.fs`.
   - A stored version whose EHR data do not parse is reported as a text, as `readEhr`
     (`ServerApi.SqlAdapters.fs:628`) reports it today.
   - The tests of the version Dto in `tests/Informedica.GenORDER.Tests/Tests.fs` (from line 4398)
     move to the server tests.
2. **Store the order plan Dto alone.**
   - The version has no Dto any more. The column `plan` holds the JSON of `OrderPlan.Dto` alone;
     the other fields of a version are read from their columns, and the check that the JSON
     agrees with the columns goes.
   - The version keeps its field `EhrData`. A migration adds the nullable columns
     `ehr_json_version` and `ehr_data` to `order_plan`, the same two columns that
     `session_opened_with`, `data_notice` and `challenge` have. The insert fills them with
     `ehrJsonWritten` and `ehrJson` (`ServerApi.SqlAdapters.fs:848-852`), and `readRow` reads them
     with `readEhr`. A row whose EHR data do not parse loads as unreadable, as today.
   - The JSON structure version of the column goes from 13 to 14. A row written under version 13
     is not converted and loads as unreadable.
   - The tests of the version Dto go; the server tests cover the stored row.
   - The stored record stays the Dto of a domain value, as ADR-0008 decides; that value is now
     the order plan.
3. **Complete the GenCORE `PatientData`.** Replace the fields `EnteralAccess` and `VenousAccess`
   with an access list; delete the two types. Add the renal function, the location, the mark as an
   adult, and whether a weight or a height is measured or estimated. Remove `AnyGender` from the
   GenCORE `Gender`. Add the string forms and the Dto fields, and the conversion from a calculated
   eGFR to a band.
4. **GenFORM reads the GenCORE `PatientData`.** The GenFORM `Patient` record, its Dto, the empty
   patient, `meetsMinimumData`, `validate`, `ofEhr`, `withAgesOf`, `EhrPatientData`, the string
   forms of gender, access and renal function, and `Utils.Calculations.calcDuBois` go; their
   logic is in GenCORE. The matching in `PatientCategory` and `RenalRule` maps the patient data to
   the rule types. The second eGFR branch in `RenalRule.filter` goes. The EHR port, the session and
   the store of the server take the GenCORE `Patient` where they took `EhrPatientData`. The stored
   EHR JSON becomes the Dto of the GenCORE `Patient`, so its structure version goes from 1 to 2 in
   all four tables; a row written under version 1 is not converted.
5. **GenORDER reads the GenCORE `PatientData`.** The copy of the empty patient and the `Weight`
   and `Height` unions in `Patient.fs` go.
6. **Rename the `Patient` fields** (*rename*) of `DoseFilter`, `SolutionFilter`,
   `PrescriptionRule`, `OrderContext` and `OrderPlan`, their Dtos, and the bindings that hold
   patient data, to `PatientData`. The call sites in the server and in `Informedica.MCP.Lib`
   follow.
7. **Server names** (*rename*). Ports, adapters, session and mappers name each value for what it
   holds: `Patient`, `PatientIdentity` or `PatientData`. The hand-written mappings of
   gender, access and renal function in `ServerApi.Mappers.Patient.fs` go.
8. **Contract and client** (*rename*). The contract `Patient` becomes `PatientData`,
   `NameAndBirthDate` becomes `PatientIdentity`, `PatientContext.Identity` and the `Patient`
   fields follow. Client.Core goes through a script; the client UI code is edited directly.
9. **Documentation.** `docs/domain/core-domain.md` defines *Patient*, *Patient Identity* and
   *Patient Data*, and says that GenFORM and GenORDER work with patient data. Issue #1689 is
   rewritten to this plan.

After every step `dotnet run servertests` and `scripts/CheckDependencyRule.fsx` pass. After step 6
the word `ehr` and the words `signed`, `signing` and `signer` do not occur in
`src/Informedica.GenFORM.Lib` and `src/Informedica.GenORDER.Lib`, and nothing there names the
identity of a patient. The identity of a rule is another thing and stays.
