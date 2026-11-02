# Implementation plan for issue #1430: what a change of patient data does to the orders

2026-11-01. Draft for review. This plan states the clinical logic first. The code follows the
logic, so where the code differs today, the code changes. Which code changes is worked out per step
after this logic is accepted. Closes #1430 and #672. Follows
[plan 1599](1599-one-patient-signed-once.md), which built rule 3's new-tab path and the moments the
age is taken.

<!-- markdownlint-disable-file MD029 -->

## Problem description

A patient and the data of a patient are two different things. A different patient means that all
orders belong to someone else. Different data of the same patient means that the orders stay, and
that the prescriber must see what the new data mean for them.

Today the orders in a plan stay calculated on the data they were made with, while the totals follow
the new data. The order plan page locks such an order completely. The nutrition page does not, so a
TPN slot shows the composition list for the old data and stays editable. That is the problem #1430
reports, and it is one case of a missing general rule.

## The rules

### Which patient is it

1. **Anonymous mode: any change of patient data is a new patient.** Every field counts, also a value
   that was still empty, such as a height added later. Everything clears: the workbench, the plan
   with all its orders and the nutrition.
2. **Asking before clearing.** With orders in the plan or a selection on the workbench, the change
   asks first: "This starts a new patient. All orders are removed." Cancel puts the panel back to the
   previous data: the patient stays the same and nothing changes. With nothing to lose, the change
   proceeds without asking, so that entering a patient stays smooth.
3. **Identified mode: the identity stays until a launch from the EHR opens another patient.** A
   change of data is applied to that patient. GenPRES itself cannot open another patient. Only one
   tab is active, and it belongs to the patient opened by the last launch. A launch takes one of two
   paths:
   - **In a new tab, the normal path.** A launch script hands a url to the operating system, and the
     browser opens that url in a new tab, since a script cannot address a tab that is already open.
     The other patient opens next to the old tab, and nothing is cleared there. A browser holds one
     session, so the old tab no longer has a session of its own. The old tab is stopped at its next
     request: it shows a gate that says another tab took the session over, and can only go on
     without a patient. A request from a tab that is not the active one changes nothing on the
     session of the other patient and is told so: a computing request gets the gate, and a refresh,
     an opened version or a close is refused, the same check as signing. The unsigned orders of the
     old tab are lost, unless #518 is built.
   - **In a tab where GenPRES already runs, a rare exception.** A launch script does not reach this
     path. A launch url arrives here only in another way, for example when a browser-based EHR
     reuses a named tab or when someone pastes the url into the address bar. This stays as plan 1224
     decided: the question about unsigned work comes first, then the session closes and the launch
     is presented.

### What a change of data does in identified mode

These rules hold for every source of the change: the panel, a data notice from the EHR, or a new
estimate.

4. **The workbench always clears.** A selection on the workbench was made under the old data. There
   is no question first, since no prescribed order is lost.
5. **What the patient gets stays.** The orderable dose quantity, the frequency, the orderable dose
   rate and the preparation stay exactly as prescribed. A change of data never changes what the
   patient gets. Only a prescriber does that.
6. **Every order is calculated on the new data, and its context keeps the patient its rules were
   chosen for.** After this, no order in the plan is calculated on data other than the data shown.
   The order plan holds the current patient. An order context holds the patient as it was when the
   context was made, which rule 8 compares with. Contexts added later are made on the current data.
7. **A change of weight or BSA recalculates the order.** The adjust value of the order is replaced
   and the values per kg or per m2 are calculated again. The order is then compared with its
   calculated and defined constraints, as every order is, and shows the same notifications. Because
   the given amount stays and the weight and height of an identified patient are changed, never
   deleted, this recalculation always gives an answer. A height changes the BSA. A new estimate of
   the weight or height counts as a change of that value.
8. **Every change of data checks the rules.** The same picks are looked up twice, for the patient of
   the order context and for the current patient of the order plan. When the current patient gets no
   rule, or other rules than the patient of the context, the order gets a general notice: the rules
   no longer apply to this order. A weight set back to the data of the context gives the same rules,
   so the notice goes. A lookup that fails says nothing about the rules: it is logged, and the order
   keeps the notice it had.

   - **Which rules count.** The dose and renal rules always count. Rules are compared as rules, not
     by their limits, so another rule row with the same limits also gives a notice. A rule counts as
     the lookup adapts it to the patient: a dose that reaches the maximum of its rule at the new
     weight has other limits, so it gets the notice.
   - **When the solution rules count.** Only when the order holds a dose count of 1. The whole
     preparation is then one dose, so a solution limit is a limit on what the patient gets. With more
     doses the preparation stays as it is.
   - **What is left out.** The amount the lookup works out from a per kg solution limit, since that
     rule is the same at every weight.

   Examples:
   - the weight goes from 9.5 to 10.5 kg and from 10 kg a dose rule with other limits applies
   - the renal function falls and a renal rule now applies
   - the central line is removed and the order needs a central line
   - the department changes and that department has other rules

   The order and its constraints stay. The clinician decides whether to keep the order, or to delete
   it and create a new one under the new rules. The notice does not block signing.
9. **The totals follow,** as they do today.

### What an order in the plan can change

10. **One rule for every order context in the plan, nutrition included, with or without a change of
    data.** The allowed actions are stepping:
    - the frequency
    - the orderable dose quantity
    - the orderable dose rate
    - the percentage of the preparation given as the dose quantity

    These actions do not involve the constraints that the rules set for the patient. Everything else,
    such as a TPN composition, a component or a filter pick, is deleting the order and creating a new
    one. Because this is one rule, there is no state to track, and a wrong weight that is corrected
    back has nothing to lift.
11. **No order is locked any more.** The complete lock of an order calculated on other data goes,
    since rule 6 means that this situation no longer exists.

### Signing

12. **A signed version never changes.** It stays the record of what the prescriber saw.
13. **An order changed by new data is a change in the plan,** shown as a change, and the prescriber
    signs a new version on the new data. The record shows both versions. A wrong weight corrected
    back makes the plan equal to the signed version again, so there is nothing new to sign.
14. **Patient data can always change.** Blocking a change of data while unsigned orders are on the
    plan was needed because orders could not follow new data. Rules 5 to 8 make the block
    unnecessary, so it goes, and the refusal at signing for orders on other data goes with it.
    Until then, a data notice at the sign is a choice: the clinician takes the EHR's data or keeps
    the plan's. With unsigned orders on the plan, taking the EHR's data removes those orders and
    reads the EHR again, as a refresh does.

14a. **Signing warns when the EHR has moved on.** When the patient active in the EHR is not the
     patient of the session, the prescriber is told so before entering the PIN: the name of the
     patient being signed for is shown, and that the EHR now has another patient active. The warning
     does not block the signature, since a prescriber may finish the orders of the previous patient
     on purpose. The record of the session stays correct either way: a launch for another patient
     already ends the session. The integration design gets this as a new rule, since its rule 6
     checks the active patient at each launch only.

### Age and estimates

15. **The age is taken at the start and stays for the whole patient session,** until a plan is signed
    or retrieved, or the session is resumed or refreshed. Then it is taken again.
16. **An estimated weight and height stay fixed with the age.** Nothing moves in the middle of a
    session without someone entering it, with one exception: at a resume the EHR data is projected
    again, so an estimate can move when an age step falls inside the session. The unsigned orders
    are gone at a resume.
17. **A new age is a change of data.** Rule 8 applies, since an age can move the patient to another
    rule. A new estimate that follows from it is a change of that value, so rule 7 applies too. This
    is visible, and signed as a new version.

### Retrieving a version

18. **A retrieved version opens as signed.** When the current data differ from the data of the
    version, that difference is a change of data under these rules: the orders are recalculated, the
    rules checked, the changes shown, and a new version is signed. The stored version stays as it is.

### Telling the prescriber

19. **One summary message at the moment of a change.** It says that the patient data changed, that
    the orders were updated, how many have a notification or a notice, and that the workbench was
    cleared. It does not stop the prescriber. The notifications and notices stay on the orders
    themselves as long as they are true, and one that no longer applies goes. Every change of the
    data or of the order compares the order with its constraints again. Every change of data also
    checks the rules. The order holds the result of that check until the next change of data, and a
    signed version keeps it. So a wrong weight that is corrected back leaves nothing behind.

## Scenarios

| Patient | Change | Result |
|---|---|---|
| Anonymous, 5 orders | Height added, confirmed | Everything clears, a new patient with the data entered |
| Anonymous, 5 orders | Height added, cancelled | The panel shows the previous data, nothing changes |
| Anonymous, no orders | Weight typed | Proceeds without asking |
| Identified | Weight 15 to 20 kg, paracetamol 150 mg, 10 to 15 mg/kg | 150 mg stays, now 7.5 mg/kg, the below-minimum notification shows, can be stepped up |
| Identified | Weight 15 to 20 kg, an order without a per kg dose | Nothing recalculates, the rules are checked |
| Identified | Weight 9.5 to 10.5 kg, other dose rule from 10 kg | Recalculated, general notice that the rules no longer apply |
| Identified | Renal function falls | The order stays, general notice when a renal rule applies |
| Identified, TPN | Weight changes | Amounts stay, per kg values and totals follow, the composition stays. Another TPN is delete and create |
| Identified, signed plan | Weight corrected | Orders show as changed, a new version to sign |
| Identified, signed plan | Weight typed wrong and corrected back | The plan equals the signed version, nothing to sign, the workbench is cleared |
| Identified, weight 9.5 to 10.5 kg | Another dose rule row with the same limits | Recalculated, general notice |
| Identified, weight 9.5 to 10.5 to 11 kg | Rule A up to 10 kg, rule B from 10 kg | The notice stays at 11 kg, the context still has the patient of rule A |
| Identified, weight 9.5 to 10.5 kg and back to 9.5 kg | Rule A up to 10 kg, rule B from 10 kg | The notice goes |
| Identified, TPN at 5 mL/kg | Weight changes, the same rule | No notice: the rule is compared, not the amount it gives |
| Identified, 15 mg/kg with a maximum of 1000 mg | Weight 55 to 70 kg, the maximum is reached | Recalculated, general notice |
| Identified, 3 unsigned orders | The EHR launches another patient in the same tab | The question about unsigned work, then the other patient opens |
| Identified, 3 unsigned orders | The EHR launches another patient in a new tab | The other patient opens in the new tab, the old tab is stopped, its unsigned orders are lost |
| Identified, session on A | The EHR moves on to B without a launch, the prescriber signs for A | A warning before the PIN, the prescriber can still sign for A |
| Identified, the other patient opened in a new tab | A weight is typed in the old tab | Nothing is stored and the other patient's session does not change; the old tab shows the gate |
| Identified, version signed at 15 kg | Retrieved, current weight 16 kg | Opens as signed, then a change of weight: recalculated, a new version to sign |
| Identified, estimated 12 kg | A plan is signed, the age moves on and the estimate is 12.5 kg | Treated as a change of weight: recalculated, a new version to sign |

## What this replaces

- **"The patient of a context never changes"** (plan 667) stays, with a new meaning given by rules 6
  and 8. The patient of a context says which rules the context was made under. The order no longer
  stays calculated on that patient: it is calculated on the current patient of the order plan.
  Rule 12 keeps a signed record unchanged.
- **#672** rejected updating a context in place because a signed context is immutable. Rules 12 and
  13 solve that with a new version, so #672 closes with this plan, without replacing contexts.
- **Plan 1075**, the hold on patient data, is replaced by rule 14.
- **The core domain model is updated.** Plans 667 and 1075 are historic documents and stay as they
  are; this section records what replaces them.

## Not part of this plan

Rule changes in the sheets are the subject of #1594. The check in rule 8 may serve there too.

## Steps

One pull request per step, each within the size limit and script first where the policy asks for it.
The order comes from the logic: the domain first, then what uses it.

1. **Domain documents.** The core domain model.
2. **Recalculation.** Rules 5 to 7 in the domain: new data on an order, the given amounts unchanged,
   the adjust value replaced, the constraints compared.
3. **Rule check.** Rule 8: the check and the general notice.
4. **The plan follows the data.** Rules 4, 6, 7, 8, 9 and 19 for a whole plan through the patient
   update, and rule 13 for what counts as changed. An order context keeps the patient its rules were
   chosen for, so what reads that patient as the current one takes the patient of the plan: the
   intake of a context, its totals per kg with it, and the reconciliation of its filter. A stored
   absolute limit from a per kg solution limit follows the weight.
5. **One edit rule.** Rules 10 and 11 on the plan page, the order dialog and the nutrition page.
6. **Patient identity.** Rules 1 and 2: the question in anonymous mode and the clearing; rule 14a,
   the warning at signing. The stopped old tab of rule 3 is built by plan 1599. With this step #1430
   as reported is solved, so this step can go earlier if the maintainer wants that.
7. **The hold goes.** Rule 14. Taking the EHR's data at a data notice then becomes a change of data
   under rules 4 to 8, so it no longer removes the unsigned orders.
8. **Age, estimates and retrieval.** Rules 15 to 18. The age at the open, a resume, a refresh and a
   sign is built by plan 1599.

## Verification

Every scenario in the table above is a test at the level where its rule lives, and a run in the
client for the panel question and the nutrition page. A medical check of recalculated orders is
needed, at least one per kg order, one per m2 order, a continuous infusion and a TPN.
