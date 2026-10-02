# Implementation plan for issue #507

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)

## Problem description

The prescribe page can show the same scenario twice, or two scenarios that differ slightly, with
nothing on the card to say why.

The cause is in the SolutionRules sheet and in how scenarios are made from it:

- The sheet can hold one row for a central line (CVL) and one for a peripheral line (PVL), with the
  same medication, route, dose type and weight range.
- A patient with no access device ticked matches both rows: a rule that names an access device
  applies to every patient whose access is not known (`VenousAccess.check` in GenFORM
  `Patient.fs`).
- `Medication.fromRule` makes one order per matching solution rule, so both rows become a
  scenario.
- The scenario does not say which access device its solution rule was for, so the card cannot
  show it.

Two terms are kept apart throughout:

- **Patient access**: the lines and tubes the patient has, ticked in the patient panel. It
  filters the rules.
- **Scenario access**: the line a solution rule makes its preparation for. It is read from the
  rule the scenario was made from and never from the patient.

Found for a newborn in no department and with no access device ticked:

| Medication | Weight | Rows that match | What the page shows |
| --- | --- | --- | --- |
| adrenaline, continuous IV | 3.9 kg | CVL up to 4 kg, PVL up to 4 kg, any access 2 to 6 kg | The CVL and PVL rows are identical apart from the access device, so two of the three cards are the same |
| noradrenaline, continuous IV | 8 kg | CVL and PVL, 6 to 11 kg, for each of two forms | Four cards; the CVL syringe holds 2 mg in 50 mL, the PVL syringe 1 mg in 50 mL; nothing says which is which |

## Approaches considered

1. **Change the sheet only.** Merge the two identical adrenaline rows into one without an access
   device. This removes the identical card, but the noradrenaline cards stay unlabelled, and the
   same can happen again with any new pair of rows.
2. **Label the scenario with its access.** Carry the access device of the solution rule to the
   scenario and show it on the card. This works for every medication and every future row.
3. **Drop or merge scenarios that come out identical.** Rejected:
   - it leaves the noradrenaline cards unlabelled unless it is combined with approach 2;
   - it hides that the sheet holds two rows, where two cards labelled "(CVL)" and "(PVL)" with
     the same content point to them;
   - two scenarios with the same text are not proven to hold the same order;
   - a scenario for any access and one for CVL with the same text would merge into one labelled
     CVL only.

For approach 2 there are three options to know which solution rule an order was made from:

- **a. Pair each medication with its solution rule.** `Medication.fromRule` makes one medication
  per solution rule, in the order of `pr.SolutionRules`, or one medication when there are none.
  The access devices can be read in the same order and paired with the medications before they are
  evaluated.
- **b. Split the prescription rule per solution rule** before `Medication.fromRule`, so that each
  scenario's rule holds the one solution rule it was made from. Rejected: `OrderScenario.fromRule`
  collects the diluent options from all solution rules of the rule, so splitting would change the
  diluents a scenario offers.
- **c. Put the access on the Medication.** Rejected: the access device is not a property of an
  order and the solver never reads it; it would be carried through the order only to be read back.

## Chosen approach

Approach 2 with option a, and the sheet change of approach 1 for the two identical adrenaline
rows. The code labels every scenario; the sheet change removes the one duplicate found.

- `OrderScenario` gets an `Access` field of the existing GenFORM type `AccessDevice`, the type
  `SolutionRule.PatientCategory.Access` already has.
  - **A device** (CVL, PVL, enteral tube) when the scenario's solution rule names one.
  - **`AnyAccess`** when the access is not relevant: the scenario was made without a solution
    rule, or its solution rule is for any access.
- The card's caption shows the access after the form, "injectievloeistof (CVL)", and the form
  alone for `AnyAccess`.

### The GenORDER changes

All in `OrderContext.fs`:

- `OrderScenario.accessOfRule`, the one new function: one access device per medication that
  `Medication.fromRule` makes, in the same order. That is `PatientCategory.Access` of each solution
  rule, or a single `AnyAccess` when the rule has no solution rules. It goes in the `OrderScenario`
  module because that module already derives a scenario's fields from its `PrescriptionRule`.
- `OrderScenario.create` sets `Access = AnyAccess`, as it sets the empty text blocks. Its
  parameters and those of `fromRule` do not change.
- `Helpers.evaluateRules` pairs the medications of a rule with `accessOfRule` through `Array.zip`
  and adds the access to the result of `evaluateOrder`. `Array.zip` throws when the two differ in
  length, so the pairing cannot go wrong in silence. `evaluateOrder` itself does not change.
- `Helpers.processEvaluationResults` sets the access on the scenario `fromRule` returns.

### The shared contract

`Shared.Types.OrderScenario` gets `Access: Access option`. It reuses the contract's `Access` type
(CVL, PVL, enteral tube), the type the patient's access uses; that type has no case for any
access, so none stands for it. The field's `///` comment states that it is the access the
scenario's solution rule makes the preparation for, not the patient's access. The server mapper
reuses `access` and `accessBack` from `ServerApi.Mappers.Patient.fs`, which already map between
the two types and already map `AnyAccess` to none.

### The stored form

The order context Dto is stored with the order plan. The scenario Dto gets `Access: string`,
written with `AccessDevice.toString` and read with `AccessDevice.tryFromString`. A plan stored
before this change has no such field; null, like any string that is no device, reads as
`AnyAccess`.

The new field changes the JSON of a stored plan, so the JSON structure version of `order_plan`
goes from 4 to 5 (`SqlDatabase.jsonVersionRead` and `jsonVersionWritten` in
`ServerApi.SqlAdapters.fs`). The upgrade step leaves the JSON as it is, as the earlier steps do.
The tests get a version 5 fixture, `order_plan_v5.json`, beside the version 4 one. A release
before this change refuses a version 5 row as newer than it knows, so a rollback after plans
were signed leaves those plans unreadable until the release is put back.

## Confidence

High that this is the cause: the server log of the browser case shows the two adrenaline orders
identical apart from their Id, and a prototype script against the sheet data reproduced both
cases.

High on the change: one field carried from the rule to the card, with the pairing covered by a
test and guarded by `Array.zip`.

## Steps

1. **GenORDER.** Add `Access: AccessDevice` to `OrderScenario` in `Types.fs`, with a `///`
   comment that keeps it apart from the patient's access. Make the changes to `OrderContext.fs`
   listed under [The GenORDER changes](#the-genorder-changes). Add the field to the scenario Dto
   as described under [The stored form](#the-stored-form).
2. **Shared contract and server.** Add `Access: Access option` to `Shared.Types.OrderScenario`
   and to `Models.OrderScenario.create`. Map it both ways in `ServerApi.Mappers.OrderContext.fs`
   (`scenario` and `scenarioBack`) with the patient mapper's `access` and `accessBack`. Raise the
   JSON structure version to 5 as described under [The stored form](#the-stored-form).
3. **Client.** In `Views/Prescribe.fs`, `displayScenario` puts the access after the form in the
   caption, and nothing when there is none. The labels are those of the patient panel: "CVL",
   "PVL", and the term `Patient Enteral Tube` for the tube.
4. **Sheet.** Separately from the code, merge the two identical adrenaline rows in the
   SolutionRules sheet. The changelog needs no step: the release automation writes it from the
   commit message.

The tests that build an `OrderScenario` get the new field:

- the record literals in `tests/Informedica.GenOrder.Tests` (`Tests.fs`, `OrderPlanTests.fs`);
- the calls of `Shared.Models.OrderScenario.create` in `tests/Informedica.GenPRES.Server.Tests`
  (`MappersOrderContextTests.fs`, `MappersOrderPlanTests.fs`, `StubAdapterTests.fs`).

The change to shipped code is estimated at 40 to 60 lines.

## Verification, per step

1. Unit tests in GenORDER: `accessOfRule` gives `[| CVL; PVL |]` for a rule with a CVL and a PVL
   solution rule, `AnyAccess` for a solution rule for any access, and a single `AnyAccess` for a
   rule without solution rules; it gives as many devices as `Medication.fromRule` makes
   medications. The scenario Dto round-trips the access, and a Dto without it reads as
   `AnyAccess`.
2. The mapper tests round-trip a scenario with CVL, with PVL, with the tube and with no access.
   The stored-row tests read the version 4 fixture with any access on every scenario and write
   it as the version 5 fixture.
3. In the browser, for a newborn with no access device ticked, before the sheet change of step 4:
   adrenaline at 3.9 kg shows three cards, "(CVL)", "(PVL)" and the 50 mL syringe without a
   label. Noradrenaline at 8 kg shows "(CVL)" and "(PVL)" on each form. With CVL ticked in the
   patient panel, the PVL cards are gone.
4. After the sheet change and a resource reload, adrenaline at 3.9 kg shows two cards, neither
   with a label.

`dotnet run servertests` passes after steps 1 and 2.
