# Implementation plan for issue #1075

Hold the patient context from the first new or changed order to the sign, so that every order a
signed version adds rests on one patient context. The use case is
[UC-12](../scenarios/integration/uc-12-held-patient-context.md); this implementation plan builds
it.

Builds on [the two patient modes](976-two-patient-modes.md), which fixes an identified patient's
age from the open to the sign, and on the idle end of a Session (#1061).

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [To settle in review](#to-settle-in-review)
- [Related issues](#related-issues)

## Problem description

A change to the patient context after an order is on the order plan re-evaluates the order
plan's totals and leaves each order as it was computed. The order plan's patient, its totals and
the signed version's patient data follow the edit; nothing compares them with the patient each
order was computed on. So a User can sign orders composed on different data, and the version
says they rest on the last of it.

## What the code does today

- **Client.** `updatePatient` in `App.fs` sends `PatientChanged` to the order context and the
  order plan. `OrderPlanMachine.step` puts the new patient on the order plan and asks
  `Recalculate`; the contexts keep the patient they were created with. `PlanWork`
  (`PlanWorkPolicy.fs`) counts the commands that changed the order plan since the version last
  opened or signed, for the guard that asks before leaving the page. It does not say which
  contexts are new or changed.
- **Server.** Every order context of the order plan states the patient it was computed on:
  `OrderPlan.OrderContexts[].Patient` on the wire, `PlanContext.Context.Patient` in the domain.
  `Session.challenge` and `Session.commit` check the Session, the Role, the token, the head and
  the challenge, and refuse an order plan that names an order twice; the commit compares the
  order plan with the challenge by digest. Nothing compares a context's patient with the order
  plan's. The Server keeps nothing of the order plan between requests.
- **Data notice.** When the EHR reads other data at the sign, the accept sets the draft to the
  notice's data and challenges over it, orders or not.
- **Verified.** A signed version's `Verified` is whether the platform could be read at the
  challenge (`challenge.Reading.IsSome` in `Session.commit`).
- **Age.** An identified patient's age is fixed by the Server from the open to the sign and put
  on every request; the Client's age does not count.

## Approaches considered

### Where the hold lives

**Chosen: derived from the order plan, by the Client for the panel and by the Server at the
challenge.** Only the order plan is signed, as a version. A new or changed context is one the
version last opened or signed does not hold as it is. The Client knows that version and the
order plan; the Server gets both at the challenge, the head from its store and the order plan
from the request.

- *A row when the first new or changed order arrives*: rejected. The Server would keep a fact
  about the order plan it otherwise never keeps, and still has to check the order plan at the
  signature, since the row cannot say what the order plan holds.
- *In the Client alone*: rejected. The Server signs; a Client that does not lock, or an old
  Client, must not get a version with mixed contexts past it.

### What the Server checks

**Chosen: every new or changed context states the order plan's patient context**, compared on
the data the rules read, at the challenge. Contexts the head holds unchanged are exempt: they
are part of a signed version, computed on the data it was signed on. The check compares what the
contexts state and does not compute them again: it catches orders composed on different data,
not a Client that states one patient on a context computed on another. The commit needs no check
of its own, since the challenge's digest ties the order plan signed to the one checked.

- *Every context, the head's included*: rejected for now. A head's orders were signed on the
  data of that sign; refusing them would refuse every sign after a weight change until the
  replacement of #672 exists.
- *Against the Session's patient instead of the order plan's*: rejected. The Server already puts
  the Session's age on the order plan, so comparing with the order plan's patient keeps the
  check a function of the request and the head.
- *Computing every new or changed context again on the order plan's patient*: rejected for the
  first build. It would prove the contexts correct, not only in agreement, at the cost of the
  rules run once more per sign.

### A change while a signature is under way

**Chosen: none can happen.** The Server signs atomically: the commit signs the order plan the
challenge's digest names. The Client's signature runs in steps, though, and the sign dialog is
modal only once a challenge stands, so an order can be added while the challenge is requested,
and a pending dialog step can go out. The Client then kept the contexts of the version before
the signature, so an order signed stayed marked changed and the context stayed held until the
next signature: safe, but wrong. Signing now blocks the order plan from the sign to the answer.

- *Carry the order plan the signature was asked over to the order plan state*: rejected. It
  keeps a case the user should not be able to make, and the machinery that tells work signed
  from work done meanwhile.

### The data notice under the hold

**Chosen: signed on the held context, recorded as resting on data other than the EHR's
reading.** The notice is shown as today; while the context is held the accept does not replace
the draft. How the version records the difference is to settle; not `Verified`, which keeps its
meaning for old and new versions alike. The new data applies once the context is released.

- *The accept replaces the draft, as today*: rejected; it changes the context under the new and
  changed orders, which is the problem.
- *Refuse the sign until the User drops those orders*: rejected; the EHR moving must not make a
  correct order plan unsignable.

### The age

**Chosen: unchanged.** The #976 rule fixes the age from the open to the sign whatever the order
plan holds. Letting it follow the clock while released would change a rule that just landed, for
a case the idle end of #1061 already bounds.

## Chosen approach

- **Hold** (Client.Core), steps 1 and 2.
- **Signing blocks the order plan** (Client.Core and Client), step 3.
- **Patient data fields disabled while held** (Client), step 4.
- **Check at the challenge** (Server), refusal `ContextDiffers`, steps 5 and 6.
- **Data notice under the hold**, step 7.
- **Refresh**, step 8.

## Confidence

Medium. The check at the challenge is a pure function of the order plan and the head and carries
the safety of the change; it proves the contexts agree, not that they are correct. Its riskiest
part is what "changed" means: whether a context of the head survives the round trip through the
Client and the wire unchanged, compared by content after the same Dto, so that an untouched
order of the head is not taken for a changed one. Step 5 settles that first. The Client's hold
needs the order plan state to keep the version it opened or signed, which it does not today. The
refresh adds a Session command and a second place the EHR is read.

## Steps

One pull request per step unless the step says two. Everything outside
`src/Informedica.GenPRES.Client/` is first a script, then a migration.

1. **The hold (script).** `src/Informedica.GenPRES.Client.Core/Scripts/HeldContext.fsx`: the
   order plan state keeps the contexts of the version last opened or signed; `changed` names the
   contexts that version does not hold as it is; `held` is whether any are left.

   Tests: a launch on a head is released; an added order holds; a changed order of the head
   holds; removing every new and changed order releases; a sign releases; a version opened
   releases; removing an order of the head alone does not hold; a change to the filter alone
   does not hold.

2. **The hold migrated** (done with step 1 in #1090, the script removed) into `OrderPlanMachine.fs` and a policy module beside
   `PlanWorkPolicy.fs`, tests in `Informedica.GenPRES.Client.Core.Tests`.

3. **Signing blocks the order plan (script, then migration).**
   `src/Informedica.GenPRES.Client.Core/Scripts/SigningBlocksPlan.fsx`. The signing act is
   atomic on the Client as it is on the Server: from the sign until the signature is answered or
   cancelled, the order plan takes no change from a page. A command and a filter are dropped
   while a signature is under way; the answer to a request under way, the patient a data
   notice accepted sets, a version opened and the signature told still reach it. The sign is
   offered only over an order plan with nothing under way, so no dialog step waits at the
   sign. The sign dialog is modal from the sign on, not only once a challenge stands. Nothing
   can then change the order plan between the sign and the answer, so a signature signs the
   order plan the Client shows and the kept contexts become exactly it.
   This removes what only served a change made meanwhile: `AskedOver` and the work carried by
   the signing machine's constructors, `Sign` and `TellSigned`; the count in
   `PlanWork.Changed`; and the condition on the order plan state's `Signed`, which then sets
   the work as signed and the kept contexts to the order plan's.

   Tests: a command and a filter while a signature is under way are dropped, in every phase;
   they go out while idle; what is not a page's change is admitted; an order added before the
   sign is released by the signature, nothing added meanwhile.

4. **The patient data fields.** `Views/Patient.fs`, for an identified patient only: while held
   the fields show their values but take no change, without a clear cross. An attempt to change
   one, or the reset, opens a question: why the patient data cannot change now, with the way
   out that removes the new and changed orders, or cancel. Signing stays the order plan's own
   button; the refresh comes with step 8. The leave-page guard unchanged. The panel's edit is
   ignored while held, where it enters the App (`IPatient.UpdatePatient`); the Session's
   patient and a data notice accepted do not come that way. New terms for the question.

5. **The check (script).** `src/Informedica.GenPRES.Server/Scripts/HeldContext.fsx`: `changed`
   against the head, by id and by content after the same Dto; the patient context each changed
   context states compared with the order plan's on the data the rules read; `challenge`
   shadowed to refuse `ContextDiffers` before the challenge is issued.

   Tests over the order plan scenarios: every context of the head, sent back unchanged through
   the Dto, is not changed; an order plan whose orders share its patient context passes; a new
   order on another weight, department or renal function is refused; an order of the head on
   an older context passes; an order plan without a head, every context new, is checked whole;
   a refusal costs no PIN attempt.

6. **The check migrated.** `SigningRefusal.ContextDiffers` into `GenPRES.Shared`, the check
   into `ServerApi.Session.fs`, the refusal's sentence into `SigningPolicy.fs`; tests in both
   test projects.

7. **The data notice under the hold (script, then migration; two pull requests).** The accept
   keeps the draft while held; the version signed over an accepted notice records that it rests
   on data other than the EHR's reading, as settled in review.

   Tests: a notice accepted while held leaves the draft; the version records the difference and
   keeps `Verified` as the platform's reading; a notice accepted while released replaces the
   draft as today.

8. **Refresh (script, then migration; two pull requests).** A Session command that reads the EHR
   again, projects it at the date of the refresh with the user's measurements over it, and
   returns the Session's patient with a fresh token; the Client asks first, drops the new and
   changed orders and reopens the head on the new patient. Closes #1075.

   Tests: a refresh reads the EHR once and projects at the date of the refresh; the age is the
   one at the refresh; the measurements stand; a refresh with no EHR data leaves the patient as
   it was.

## Verification, per step

- Script steps: `dotnet fsi` on the script, checking that Expecto reports `Status: Ok`; the
  script stays in the repository.
- Migration steps: `dotnet run build`, `dotnet run servertests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line.
- Client steps: `dotnet run clientbuild`, the generated JSX checked for the changed element,
  then the browser, by the user.

| Step | Check |
|------|-------|
| 3 | In the browser: click sign and, before the challenge comes back, try to add an order; nothing can be added until the signature is answered or cancelled. |
| 4 | In the browser, for an identified patient: add an order, a change to the weight asks the question and changes nothing; remove the new orders from the question, the weight can be changed; add one and sign, it can be changed. |
| 6 | An order plan with a new order on another weight, sent by hand, is refused `ContextDiffers` at the challenge. |
| 7 | With the stub's EHR data changed after the open and an order on the order plan, the version is signed on the held data and records the difference. |
| 8 | A refresh after a change of the stub's EHR data shows the new data and no new or changed orders. |

## To settle in review

- **Which fields.** Weight, height, gestational age, gender, department, renal function and
  access: the data the rules read that the User can change. The age is fixed by the Server.
  Confirm, or name the ones to leave out.
- **Which Sessions.** Settled: an identified patient only. Anonymous use and the url mode are
  never held; nothing is signed for them.
- **What counts as changed.** An order context whose id the head does not hold, or whose content
  differs from the head's after the same Dto; a change to the order plan's filter alone is not.
  Confirm.
- **Re-prescribing on a new context.** Deferred to #672; until then a new bedside weight means
  signing the order plan or dropping its new and changed orders first. Accept the cost, or pull
  #672 in.
- **Recording the difference from the EHR.** A field of its own on the version; `Verified` keeps
  its meaning. Name the field.
- **Measurements.** Recorded per request as now; while the context is held the panel sends none;
  they stand over a refresh, as they stand over a sign. Confirm.

## Related issues

- **#672** the replacement of a context for new patient data, and a guard at the signature for
  the head's contexts.
- **#976** the age fixed from the open to the sign; unchanged here.
- **#1061** the idle end of a Session, which bounds a hold left behind.
- **#598** client testing: would let step 4 be tested without the browser.
- **#518** the carry-over of an order plan's changes into a relaunched tab, which would carry
  the hold with it.
