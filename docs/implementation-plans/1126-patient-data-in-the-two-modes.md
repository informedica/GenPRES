# Implementation plan for issue #1126

Patient data in the two GenPRES modes: an identified patient's data is changed, never cleared;
an anonymous patient's data may be cleared, and missing data is a warning, never a server
error. Builds on [the two patient modes plan](976-two-patient-modes.md) and amends one of its
decisions (see [Chosen approach](#chosen-approach)).

- **Identified**: a patient the EHR identifies, with a patient id, a name and a birthdate. The
  data belongs to a real patient; this is the mode in which orders are prescribed and signed.
- **Anonymous**: a patient entered at the keyboard or by url parameters, or a launch without an
  identity. The patient is fictitious; this mode gives decision support only.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)
- [Found on the way](#found-on-the-way)
- [As built](#as-built)

## Problem description

After the patient panel of a launched, identified patient is reset, a reload of the page shows
the banner "Server fout: Gewicht en lengte onbekend: voer ze in", while the panel shows a weight
and a height. Reproduced on the demo launch (patient 123456, 32 kg and 140 cm): the reset itself
works, the reload fails. Clearing only the weight or the height field has the same effect.

## What the code does today

- **The reset.** `Views/Patient.fs` offers Reset in both modes. For an identified patient it
  keeps the age and discards the rest (`Clear of kept: Age option`); the weight, height and
  gestational age fields carry a clear cross in both modes.
- **The Session records the clearing.** Every computing request marks the Session seen and
  records what the request's patient measures (`Session.seen`, `Measurements.record`). A cleared
  value is recorded as a row that says none, as the two patient modes plan decided, so that a
  cleared weight does not return at the next resume.
- **The resume returns an age-only patient.** `SessionMapper.toOpened` returns the EHR patient
  with the Session's measurements on it (`toModel >> Measurements.apply`). The EHR data had a
  measured weight and height, so no estimate was stored; with both cleared, the patient carries
  an age and no weight or height at all.
- **Estimates are made on the client only.** `App.updatePatient` applies the normal values
  (`applyNormalValues`) only once the client has loaded them. The resumed patient arrives first;
  it passes `Patient.validate`, since an age alone is a patient, and is sent to the server
  without estimates.
- **The server refuses it.** `ServerApi.Patient.patient` requires a weight and a height,
  measured or estimated, and answers "Gewicht en lengte onbekend". For the order plan the client
  shows this as the "Server fout" banner, which stays until dismissed. Once the normal values
  have loaded the client sends the patient again, now estimated, and the requests succeed.
- **The server can estimate.** It holds the same tables and already estimates EHR readings at
  its inbound boundary (`Patient.estimated`, `Patient.estimating` in
  `ServerApi.Mappers.Patient.fs`), but not the patients that requests carry.

## Decisions

Taken on #1126 before this plan:

1. **Identified: change, never clear.** No reset and no clear cross on weight, height and
   gestational age; a measured value is only replaced by another value.
2. **Anonymous: clear freely.** Reset and the clear crosses stay; a patient with too little data
   gets a warning that there is not enough data to calculate medication, never a server error.
3. **Whether the data suffices is decided by the server**, as a domain decision; the client
   renders the outcome.
4. **No change to how the Session records measurements.** A cleared row stays possible in code;
   the panel no longer produces one for an identified patient.

## Approaches considered

- **The client holds back an incomplete patient** until its own estimates are there. Rejected:
  it puts the domain decision in the client, and in `Client.Core`, whose job is UI state.
- **A typed "insufficient data" outcome on the contract**, returned by the server and shown by
  the client as a warning. Not needed: with the approach below the server has the data it needs
  in every case the client can send, and the one remaining refusal is the resources not being
  loaded, which is refused for every command already.
- **The server estimates every request patient.** Rejected: the client reads its normal values
  from its own sheet and the server from the provider; estimating a patient the client already
  estimated could make the panel and the calculation disagree.
- **The server fills in the estimates a request patient lacks** (chosen).

## Chosen approach

One path for both modes:

```text
panel edit → draft → Patient.validate (the client: is it a patient at all)
          → request → server: the Session's age (identified only) → missing estimates → gate
          → compute
```

The modes differ only in what the panel allows (identified: no reset, no clear, the age
read-only) and in what the Session adds (the age and the measurements). Everything after the
draft is the same.

**The server fills in the estimates a request patient lacks**, right after the Session's age is
put on it and before the gate. Then:

- an age-only patient is always calculable while the resources are loaded: the resumed patient,
  a url patient with only an age on the first load, and Sessions already stored with cleared
  measurements all compute on the estimate. `applyNormalValues` takes the nearest table row, so
  every age gets a value while the tables have rows for the patient's sex, and for both sexes
  when the gender is unknown, since that estimate is the average of the two. A missing `weight`
  or `height` sheet, or a table with rows for one sex only, leaves the estimate out and the gate
  still refuses; both are tested, not fixed;
- the gate "no weight and height" otherwise fires only while the resources are not loaded, which
  `requireLoaded` refuses for every command already;
- a draft below the minimum (no age, and not both a measured weight and height) never leaves the
  client: `App.updatePatient` maps it to no patient, and the panel and the prescribing page warn
  already ("Voer een leeftijd in, of een gewicht en een lengte", "Voer eerst patient gegevens
  in"). That is the anonymous warning: no new term, no contract change.

**The panel follows the two modes.** For an identified patient there is no reset and no clear
cross on weight, height and gestational age. Renal function keeps its cross: its options have no
"unknown" entry, so a renal function once set could otherwise never return to none.

**Amends the two patient modes plan.** Its bedside measurements section lets an identified user
clear a measurement ("Clearing a value writes a row that says none"). The server keeps that
behaviour and its test; the panel no longer offers the clearing for an identified patient.

**Known and accepted.** On a resume, the server may compute on its estimate while the panel still
shows no weight and height and the prescribing page says "Gewicht en lengte onbekend", until the
client's tables have loaded and the panel shows the same estimate. The issue's proposal to show
the values after a reset as estimates is dropped: an identified patient has no reset any more,
and an anonymous patient's fields render estimates as estimates already.

## Confidence

High for the panel: it removes controls, and the path to the bug goes with them. Medium-high for
the server: the estimate function exists and is tested; the new part is applying it to every
request patient, through the per-command mappers that already put the Session's age on them.

## Steps

One pull request at a time, each merged before the next. Code outside the client is prototyped in
a script and migrated by the maintainer; client code is committed locally and pushed after the
maintainer has checked it in the browser.

1. **This plan** (docs).
2. **The panel** (client, `Views/Patient.fs`), with the docs that go with it.
   - Reset and its confirmation dialog only when the patient is not identified; the dialog's
     identified text and the held branch of the reset button go (held implies identified).
   - `Clear of kept: Age option` becomes `Clear`, which discards the whole draft.
   - The clear cross is passed per field: none on weight, height and gestational age when
     identified; renal function keeps it; the age fields are read-only when identified already.
   - `docs/implementation-plans/976-two-patient-modes.md`: a dated amendment to the bedside
     measurements section. `docs/user-guide/testing-workflows.md`: the panel reset in anonymous
     use only. The changelog entry names the amendment.
   - Fable compile and `npx vite build`.
3. **The server fills in missing estimates** (script,
   `src/Informedica.GenPRES.Server/Scripts/EstimateOnRequest.fsx`, then the maintainer's
   migration).
   - `Patient.estimate` in `ServerApi.Mappers.Patient`, per measure: a weight or a height that
     is neither measured nor estimated gets the server's estimate when the normal values are
     there; a measured value and an estimate already on the patient are kept. The estimates come
     from `Shared.Models.NormalValues.apply` on a copy, and only the missing measure is taken
     from it: `apply` writes both estimates, so applying it to the patient itself would replace
     the client's estimate of the other measure with the server's, and the panel and the
     calculation could differ.
   - The per-command `aged: Age option -> 'cmd -> 'cmd` becomes
     `patients: (Patient -> Patient) -> 'cmd -> 'cmd` in the order context, formulary,
     parenteralia, order plan (with `agedPlan`), interaction and signing commands; each maps
     `Patient.aged age` over the patients it carries today, so only the function passed changes.
   - `Compute.bound` applies `Patient.aged age >> estimate`, the age first, since the estimate
     reads it. The normal values are asked inside the per-patient function, so a command without
     a patient never asks the provider, as `bound` promises for an open command.
   - `SigningCommand.processCmd` applies the same composition, so a plan whose patient carries
     only an age is not refused `NoPatient`.
   - `AppEnv` gets `normalValues: unit -> NormalValues option`, wired in `ServerApi.Adapters.fs`
     from the expression `estimating` uses, extracted into one function for both.
   - The script cannot add the `AppEnv` field (a new record type that the composition root and
     the test fixtures do not take), so it passes the estimate as a parameter of a shadowed
     `Compute.bound` and `SigningCommand.processCmd`; the field is a migration-time change.
   - Tests, homed in `AgeOnRequestTests.fs`: an age-only request patient computes on the
     estimate (order context, order plan); a measured weight is kept and the height estimated;
     a client estimate of the weight is kept when only the height is filled in; the Session's
     age is put on before the estimate; a resumed Session with weight and height
     cleared computes; a command without a patient never asks for the normal values; empty
     tables, and an unknown gender over a table with rows for one sex only, still refuse with
     "Gewicht en lengte onbekend"; signing a plan with an age-only
     patient is not refused. The comment on the cleared-weight test in `MeasurementsTests.fs`
     is reworded: the panel sends a cleared weight in anonymous use only.
4. **Closing docs**: the as-built table below, and #1126 closed.

## Verification

- Step 2: in the browser, on `GENPRES_PROD=0 dotnet run` and the demo launch (patient 123456,
  prescriber): no Reset button and no cross on weight, height and gestational age; the cross on
  renal function; a changed weight survives a reload without a banner. Without a launch: Reset,
  clearing fields and a reload behave as before.
- Step 3: the script's tests; after the migration `dotnet run servertests` (`CI=true` in a
  worktree), `dotnet fsi scripts/CheckDependencyRule.fsx` (the shape of `AppEnv` changes), and
  the benchmark build, since a signature change can pass locally and fail CI's benchmark job. In
  the browser: a reload right after the launch shows no banner (the results may appear a moment
  before the panel fills weight and height); without a launch, a url patient with only an age
  loads without an error. Changed source lines under `src/` stay under 200.

## Found on the way

Not part of this plan; each worth its own issue if wanted:

- **"Identified" has two meanings.** The panel calls a patient identified when the Session has
  an identity; the server records measurements for any Session with a patient id, so a launch
  without data is anonymous in the panel yet has its measurements recorded.
- **The banner stays.** Order plan errors go to the "Server fout" banner, which stays until
  dismissed; order context errors are a passing warning.
- **An unused term.** `Patient Reset Dialog Text Identified` in `Shared/Localization.fs` is no
  longer used after step 2; removing it is an edit in `Shared`.

## As built

| Step | Pull request | Note |
|---|---|---|
| 1, the plan | #1133 | One review round: the server fills in only the missing measure, so a client estimate of the other is kept; an unknown gender over a table with rows for one sex only still refuses. |
| 2, the panel | #1134 | No reset and no clear cross on weight, height and gestational age for an identified patient; renal function keeps its cross. The two patient modes plan amended, the held patient context scenario and the launch workflow updated. |
| 3, the server | #1135 | `Patient.estimate` and `AppEnv.normalValues`; each command's `aged` became `patients`. Prototyped in a script and migrated in the same pull request; thirteen tests in `AgeOnRequestTests.fs`. |
| 4, the closing docs | #1139 | This table; #1126 closed. |

Deviation from the plan, from the review of #1135: signing takes no estimate. The challenge is a
digest of the plan as sent, and estimates filled in at the challenge and again at the submission,
from tables reloaded in between, would change the plan under it and refuse a legitimate
signature. The client signs the plan the server answered, which carries the server's estimates
already; an age-only plan is refused as it was before.

Found while testing #1135 and fixed there: the sign dialog put the order rows in a paragraph and
the PIN field outside a form. The rows now render in a `div`, the cells of a row have a key, and
the PIN field sits in a form that submits on Enter.
