# Use Case: Order Nutrition in GenPRES

## Use Case Summary

- **Use Case ID**: UC-GENPRES-004
- **Name**: Order enteral and parenteral nutrition
- **Primary Actor**: Clinical user (prescriber / dietitian / nurse),
  depending on authorization
- **Secondary Actors**: GenPRES application, Pharmacy (for parenteral
  preparation, under [UC-GENPRES-003](UC-GENPRES-003.md))
- **Goal**: Let a user compose a patient's nutrition plan — enteral
  feeding with supplements and/or parenteral lines — from the scenarios
  the knowledge rules allow, and see the resulting intake totals against
  the reference ranges for that patient.
- **Scope**: GenPRES nutrition view
- **Level**: User goal
- **Trigger**: User opens the nutrition view in a running GenPRES session
- **Preconditions**:
  1. A GenPRES session is running with a patient context, either from a
     redeemed launch token
     ([UC-GENPRES-001](UC-GENPRES-001.md)) or entered manually
     ([UC-GENPRES-002](UC-GENPRES-002.md)).
  2. Patient weight and age are known. They select the applicable dose
     rules and the reference ranges the totals are compared against;
     without them the plan can still be built, but unbanded.
  3. The knowledge rules are loaded.
  4. For parenteral lines, the patient's vascular access is known. Access
     type does not decide whether a parenteral line may be ordered; it
     selects which solution constraints apply, peripheral access bounding
     concentrations far more tightly than central. The legacy application
     assumes central access for the whole parenteral section, stating it
     as a heading ("intravenously via CVL"); GenPRES instead carries
     access as patient context and lets it constrain the mixture.
- **Postconditions (success)**: The nutrition plan holds one or more
  nutrition lines, each resolved to a single order scenario, and the
  intake totals reflect every line in the plan.
- **Postconditions (fail)**: No line is added or changed; the plan and
  totals stand as they were; the user is informed.
- **Priority**: High
- **Frequency of use**: Daily per patient, revised as weight, age and
  clinical state change
- **Assumptions**: A nutrition line is an order like any other — the
  same rules, solver and dose-check severities apply. What is specific
  here is that lines are composed side by side and evaluated together
  through shared intake totals.

## Main Flow — composing a nutrition plan

Each step below is one step of the happy path. The heading gives the
step number, the actor, and the action / trigger; the bullets give the
system response, the authorization required, the alternative or
exception flows, and any notes.

**Steps 3 to 8 repeat per nutrition line.** A plan is built by adding
lines one at a time; each addition re-solves nothing but its own line
and recomputes the totals for the whole plan.

### 1 — User: open the nutrition view

- **System Response**: Opens the nutrition view for the patient of the
  current session.
- **Authorization**: As the session; prescribing authorization is
  required to change anything, otherwise the plan is read-only.
- **Alternative / Exception flow**: No patient context in the session → the
  plan cannot be banded to a patient; see [UC-GENPRES-002](UC-GENPRES-002.md).
- **Notes**: Nutrition is one view onto the same order model used for
  medication, not a separate subsystem.

### 2 — GenPRES: initialise the nutrition plan

- **System Response**: Creates an empty nutrition plan for the patient
  and computes the initial (empty) intake totals.
- **Authorization**: System step.
- **Alternative / Exception flow**: Initialisation fails → report and
  leave the view empty.
- **Notes**: The plan carries the patient, its nutrition lines and the
  totals as one unit, so totals can never drift from the lines they were
  computed from.

### 3 — User: add a nutrition line

- **System Response**: Offers the nutrition categories that may still be
  added and adds the chosen one to the plan.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**: A category that is already present
  and may occur only once is not offered.
- **Notes**: Five categories exist, with different multiplicity:
  - **Enteral feeding** — at most one.
  - **Enteral supplement** (powders) — only offered once a feeding
    exists; zero or more, added one at a time.
  - **Total parenteral nutrition (TPN)** — at most one.
  - **Lipids** — at most one.
  - **Electrolytes / glucose** — zero or more.

  Enteral and parenteral lines are presented as two groups, so the plan
  reads the way it is administered.

### 4 — GenPRES: seed and resolve the line's order context

- **System Response**: Seeds a fresh order context for the line with the
  indications and compositions configured for that category, evaluates
  it against the knowledge rules for this patient, and keeps only the
  options that both the rules and the configuration allow. The resolved
  line is appended to the plan and the totals are recomputed.
- **Authorization**: System step.
- **Alternative / Exception flow**: No options survive the intersection
  → the line cannot be added; tell the user which category yielded
  nothing for this patient.
- **Notes**: The configured set is what makes a nutrition category a
  category: it is the fixed, configurable list of compositions that may
  be combined for that role — for example the TPN compositions, or the
  enteral feeds and the supplement powders.

### 5 — User: choose the composition for the line

- **System Response**: Offers the compositions, indications and — where
  more than one exists — the dose types the rules allow for this patient
  and category. The selection narrows the remaining options as it is
  made.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**: A selection leaves no scenario →
  tell the user which choice emptied the options so it can be revised.
- **Notes**: For TPN the **dose type carries the stage** of the build-up
  protocol — day 1, day 2, day 3 and onwards. Choosing a stage chooses
  the dose limits and the solution constraints that apply, so stage
  selection is a clinical decision expressed as a filter, not a separate
  calculation. The dose type selector is hidden when a line has only
  one. The legacy application offers the same choice as a "TPN day"
  radio group; the difference is that there it is a mode switch on one
  fixed form, while here it selects a different rule set.

### 6 — GenPRES: compute the scenario and the remaining options

- **System Response**: Applies the dose rules and solution rules of the
  selection to the patient and computes the single order scenario for
  the line, together with the values still open on it — as selectable
  option sets or as quantitative ranges.
- **Authorization**: System step.
- **Alternative / Exception flow**: The constraints cannot be satisfied
  together → no scenario; the line stays unconfigured and contributes
  nothing to the totals.
- **Notes**: Solution rules are what make a parenteral line
  self-limiting: bounding each component's quantity, volume and
  concentration forces a solvent volume, so a rate cannot be lowered
  past the point where the mixture would become too concentrated.

### 7 — User: adjust the line

- **System Response**: Accepts changes to what the scenario leaves open
  — frequency, total volume, infusion rate, and the quantity of each
  component — in two phases. While a value is still a range, it can be
  set to its minimum, median or maximum; once every value is set, the
  same controls step a value up or down by its increment. Each component
  is shown both as a prepared volume and as a dose per kilogram, and the
  administration time follows from volume and rate. The user can also
  ask for the line to run over its intended administration period, which
  sets the rate to match the prepared volume.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**:
  - The user steps a value past its rule-defined limit → the value is
    accepted and flagged by dose-check severity, as in
    [UC-GENPRES-001](UC-GENPRES-001.md). This holds for derived values
    too: an administration time that no longer fits the intended period
    is flagged, not blocked.
  - The user resets the line → every value returns to the ranges the
    rules originally allowed, discarding the adjustments made.
  - No intended administration period is defined for the stage → the
    run-over-period action is not offered, and the administration time
    is shown without being judged.
- **Notes**: Volume and rate are adjustable independently of each other,
  so a mixture can be made to run over the intended period without
  changing what is in it. That period comes from the dose rule for the
  stage, not from a fixed 24 hours: a build-up stage may be intended to
  run over a different span, and the rule is what says so. For
  parenteral lines, per-component adjustment is how electrolytes are
  titrated within the line. Raising a component
  against a maximum concentration forces solvent volume up rather than
  being refused — the mixture stays administrable by construction.

### 8 — GenPRES: re-solve the line and recompute the totals

- **System Response**: Re-solves the line around the change, then
  recomputes the intake totals across every line in the plan and
  presents each total with the reference range for this patient's weight
  and age band.
- **Authorization**: System step.
- **Alternative / Exception flow**: A line has no scenario → it
  contributes nothing; the totals stay valid for the rest of the plan.
- **Notes**: Totals cover a fixed set of items — fluid volume, energy,
  protein, carbohydrate, fat, the six electrolytes (sodium, potassium,
  chloride, calcium, phosphate, magnesium), iron, vitamin D, and the
  excipients ethanol, propylene glycol, benzyl alcohol and boric acid.
  Reference ranges are selected by weight and age band; a total outside
  its range is shown next to that range but is **not** flagged — see the
  implementation status. Some totals are clinically read in a different
  unit from the one they are prescribed in — carbohydrate above all,
  which is dosed per day but judged as a glucose infusion rate in
  mg/kg/min — so a total may need presenting in both.

## Extension — remove a component from a parenteral line

- **Trigger**: The user removes a component from a line — an electrolyte
  no longer wanted in the mixture, say.
- **System Response**: Drops the component, re-solves the line without
  it, and recomputes the totals.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**:
  - The component is the **principal** one — what the line exists to
    deliver, such as the protein solution in a TPN line → it cannot be
    removed; removing the line is the way to be rid of it.
  - The component is a **solvent** that some other component's maximum
    concentration depends on → it cannot be removed while that component
    is present, because without it the mixture would exceed the
    concentration the rules allow.
  - Removing it leaves the line unsatisfiable → say which constraint
    fails, and leave the line as it was.
- **Notes**: Components fall into three roles, and only the third is
  freely removable: the **principal** component the line delivers, a
  **solvent** required to keep a concentration limit satisfiable, and
  **additional** components — sodium, potassium, calcium, magnesium,
  phosphate — added on top. The roles are not a UI classification; they
  follow from the rules, since what makes a solvent mandatory is another
  component's concentration limit.

## Extension — remove a nutrition line

- **Trigger**: The user removes a line from the plan.
- **System Response**: Removes the line and recomputes the totals.
  Removing the enteral feeding also removes the supplements that hang
  off it, so the user is asked to confirm first.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**: User does not confirm → nothing is
  removed.
- **Notes**: Supplements have no meaning without a feeding to be added
  to, so the cascade is deliberate rather than incidental.

## Extension — print the parenteral overview

- **Trigger**: The user prints the parenteral part of the plan.
- **System Response**: Renders the TPN, lipid and electrolyte/glucose
  lines as a preparation document: per line, each component with its
  prepared volume and its weight-adjusted dose, the total volume, the
  pump rate and the administration time; then the intake totals with
  their reference ranges. It carries a patient header, states that the
  route is central venous, and ends in a prescriber signature line.
- **Authorization**: As the session.
- **Alternative / Exception flow**: A line is not yet configured → it is
  printed as such rather than omitted, so nothing looks complete when it
  is not.
- **Notes**: This is a paper hand-off. Sending the same request to the
  pharmacy electronically is [UC-GENPRES-003](UC-GENPRES-003.md).

## Implementation status

This use case describes **intended** behaviour, but the nutrition
workflow is among the better-covered parts of GenPRES. Status per item
is tracked in the
[AP2019 vs GenPRES fit-gap analysis](../roadmap/fit-gap-ap2019-vs-genpres.md),
sections 5 (TPN / parenteral), 6 (enteral) and 7 (totals).

- **Built**: enteral feed selection (6.1), multiple supplements with
  cascade-removal confirmation (6.2, 6.3), frequency and dose per
  administration (6.4), continuous rate-based feeding (6.5), TPN
  composition selection (5.1), independent rate and volume control
  (5.2, 5.3, 5.9), per-component electrolyte stepping (5.6), stage
  progression via dose type (5.8), solution-rule volume floor (5.14),
  and the fixed totals set (7.1, 7.2, 7.4–7.7, 7.11).
- **Built, fit-gap not yet updated**: the parenteral print view exists,
  including patient header, per-component volumes and doses, pump rate,
  administration time, totals with reference ranges, and a signature
  line. Item 5.11 still records print and pharmacy mail together as a
  Gap; the print half is done, the electronic hand-off is not.
- **Built, fit-gap understates it**: administration time is derived and
  displayed per line, and is flagged when it no longer fits the intended
  period. Items 5.7 (pump stand calculation) and 5.10 (infusion time
  over 24 hours) are recorded as Gaps, but what is actually missing is
  narrower: the run-over-the-intended-period action, and an intended
  period on the dose rule to drive it. Note that the legacy application
  fixes that period at 24 hours whereas here it belongs to the stage, so
  5.7 should not be implemented by copying the legacy behaviour.
- **Built — reset**: a line can be reset to the constraints the rules
  originally gave it. The legacy equivalent is per-value ("standard");
  in GenPRES it applies to the whole line. Not in the fit-gap.
- **Partial — totals**: reference ranges are shown as text beside each
  total, but nothing flags a total that falls outside its range (11.6 —
  AP2019 did not flag either, so this is a GenPRES-specific intention);
  totals are computed per view rather than across nutrition *and* the
  treatment plan together (7.10). On 7.3 (glucose in mg/kg/min): the
  print already carries the mg/kg/min reference range for carbohydrate
  but still shows the value in g/kg/day. Both belong — the intake is
  prescribed per day and judged as an infusion rate — so what is missing
  is the value in mg/kg/min beside its range, not a change to the range.
- **Partial — parenteral composition**: no dedicated phosphate line
  (5.4); the rest-volume negative-balance flag is not confirmed (5.13);
  lipid weight-band boundary resolution is not confirmed (5.16). On the
  line count in 5.4: the legacy application allows at most four
  parenteral infusions (protein, Mg/Ca, lipid, electrolyte). GenPRES
  covers these with one TPN line, one lipid line and any number of
  electrolyte/glucose lines, Mg/Ca being one of those — so the shapes
  differ and the missing cap is not itself a shortfall.
- **Not built — parenteral safety checks**: the rule coupling a
  non-glucose solvent to protein availability (5.15).
- **Partial — vascular access as a constraint**: precondition 4 above.
  Access type exists in patient context, but it is not confirmed that it
  selects the solution constraints, which is the whole of what it is
  meant to do here. Fit-gap 5.5, downgraded from Fit to Partial on the
  strength of this use case.
- **Not built — component-level removal**: the extension above. Today a
  whole line can be removed but a component within it cannot, and the
  principal / solvent / additional roles that decide what may be removed
  are not represented. Not in the fit-gap; the legacy application has
  this.
- **Not built — plan-level flags**: no per-line "extra" flag to exclude
  a line from the balance (5.12); no NICU side-lines contributing to
  fluid totals (2.7).
- **Not built — context around the totals**: no lab values shown
  alongside them (7.9); gestational and post-menstrual age are not yet
  used to select neonatal intake recommendations (7.8).
- **Not built — persistence**: as for every use case here, the plan
  lives only in the session. See
  [patient-state persistence](../roadmap/feature-patient-persistence.md).
- **Configuration**: the per-category composition lists are currently
  fixed in server code rather than being configurable data, and in-app
  editing of formulary and knowledge base is a partial gap (9.14).

## Related use cases

- **UC-GENPRES-001 — Prescribe & manage patient orders**
  ([main use case](UC-GENPRES-001.md)): the session this is composed
  in, and the source of the dose-check and override behaviour reused
  here.
- **UC-GENPRES-002 — Calculate orders without patient context**
  ([Stand-alone usage](UC-GENPRES-002.md)): nutrition can
  be composed there too, from a manually entered patient, with nothing
  persisted.
- **UC-GENPRES-003 — Notify the pharmacy to prepare orders**
  ([Pharmacy notification](UC-GENPRES-003.md)): how parenteral
  lines reach the pharmacy once electronic dispatch exists.

## Legend

See the Legend in [UC-GENPRES-001](UC-GENPRES-001.md) for how to read
and extend these use cases.
