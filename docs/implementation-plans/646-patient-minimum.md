# Implementation plan for issue 646

## Problem description

A patient can be empty. `Patient.empty` in `src/Informedica.GenPRES.Shared/Models.fs` is a
patient with every field `None`, and it is a real value on both sides of the wire: the client
starts a patient from one keystroke in the panel (`Patient.setYear` and its siblings build one
from a single field, `Patient.create` never returns `None`), the server opens a Session on it
when the PatientDataPlatform has no data and no version was signed
(`ServerApi.Session.fs`, `sessionPatient`), and the workbench and the plan are evaluated for it
as for any patient. Issue [#646](https://github.com/informedica/GenPRES/issues/646) names the
consequence: a stub patient that was empty was still a patient, and when data was changed the
absence of data was taken as what it should be.

The patient is part of the filter. Age, weight, BSA, gestational age, gender and department all
select dose rules (`PatientCategory.filter` in `src/Informedica.GenFORM.Lib/Patient.fs`). A
filter chosen before the patient exists, the *seed* of the prescribing workbench
(`OrderContextWorkbench.Seeded` in `src/Informedica.GenPRES.Client.Core/OrderContextMachine.fs`,
from a medication in the url or the menu), is therefore a state the domain has no meaning for:
the patient entered afterwards may exclude the generic. Plan
[691](691-order-lanes-two-stages.md) left that state as decision b; this plan resolves it by
removing the state.

What the exploration for this plan found, beyond the issue:

- **The server's rule gate needs a weight, a height and a department.** `getRules` in
  `src/Informedica.GenORDER.Lib/Api.fs` evaluates dose rules only for `Some w, Some h, Some d`;
  anything else answers no rules and no scenarios, without an error. An age alone works today
  only because the client estimates weight and height first.
- **Department is defaulted to "ICK" silently**, by `ServerApi.Mappers.fs` and by the MCP host's
  `buildPatient`. The panel has no department field; only the url `dp` parameter or a Session
  sets it. Because the mapped patient is never the empty GenFORM patient, the "no patient set,
  skip the patient filter" branch in `GenFORM.Lib/DoseRule.fs` is unreachable from the web
  client. A hidden filter input; not this plan.
- **A missing datum is restrictive, and silent.** `MinMax.inRange` in `GenFORM.Lib/Utils.fs`
  passes a `None` value only against an unbounded range. A patient without an age loses every
  age-bounded rule, and the gestational and post-menstrual age predicates are skipped entirely.
  Safe by construction, invisible to the user. No test pins the weight-bounded case.
- **Estimation is client-only.** `Patient.applyNormalValues` (Shared, called from `App.fs`) reads
  tables the client fetches from the emergency-list sheet. A patient with an age only that
  reaches the server from the platform or the MCP host is filtered without a weight. Not this
  plan.
- **The estimate is promoted into `Measured`** (`applyNormalValues`, `Option.orElse`): the root
  of [#488](https://github.com/informedica/GenPRES/issues/488), and it loses the provenance a
  signed version is meant to record.
- **The docs disagree with the code.** The user guide says weight and height are required
  before any dose; the estimator makes an age enough. No domain document states a minimum
  patient datum.

## Approaches considered

1. **Show the seed as provisional** (decision b of plan 691 as first proposed): the seed stays,
   greyed until a patient evaluates it. Rejected: the state is illegal, not merely unconfirmed;
   greying it keeps a filter the patient may contradict.
2. **Hold a medication from the url as a pending selection** and apply it once a patient
   arrives. Rejected for the same reason: the patient entered afterwards may exclude the generic,
   and the user would have entered data to be told the medication is gone.
3. **Validate at the edges, keep the record public**: a predicate says whether the minimum is
   met, `create` answers `None` below it, and the panel and the server ingress refuse what is
   below. Rejected: a patient could still be built by record syntax without the minimum; the
   guarantee would live in discipline, not in the type.
4. **A private record on the wire**: `Patient` with a private constructor and a smart
   constructor, carried by the wire records as it is. Rejected after a check: the server's
   serializer (Fable.Remoting.Json, Newtonsoft underneath) writes a private record as `{}` and
   cannot read one back ("Unable to find a constructor to use for type Patient"). The Fable side
   would be fine, since Fable erases accessibility, but every reply from the server would break.
5. **A DTO split** (chosen): the wire carries a `PatientDto`, today's record with every field
   optional; the domain `Patient` has a private constructor and is built only by
   `Patient.fromDto`, which answers `Error` below the minimum. This is the pattern the domain
   libraries already use (`module Dto` with `toDto` and `fromDto` in GenSOLVER's `Variable` and
   `Equation`, GenUNITS' `ValueUnit`, ZForm's `DoseRule` and `PatientCategory`, GenORDER's
   `Order` and `OrderVariable`) and the pattern of the API edge, where the Shared records are the
   DTOs of the GenFORM and GenORDER types (`mapFromShared`, `mapToShared` in
   `ServerApi.Mappers.fs`). New here: a private record constructor, which no type in `src` has
   yet, and a `fromDto` that answers `Result` rather than raising as the libraries' do, since the
   outer ring parses at ingress and answers the client with an `Error`.
6. **Weight alone as a minimum**, as #646 first wrote it. Deferred: the server gate needs a height
   too, and no height-from-weight estimate exists. Weight and height together are a minimum now;
   weight alone, and height alone, once an estimate exists.

## Chosen approach

Approach 5, with the minimum decided as: **an age, or a weight and a height**. From an age the
rest is estimated. There is a patient, or there is none; nothing between.

The rule, in full:

1. No empty patient anywhere: not in the client, not in a Session, not in the core. On the
   wire and in the panel there is a `PatientDto`, every field optional, which is a draft or a
   reading, never a patient; a `Patient` exists only through `Patient.fromDto`.
2. A patient exists with an age, or with a measured weight and a measured height. Everything
   else stays optional.
3. Only with a patient is there a workbench, a plan, a filter, an evaluation. The client machines
   have no state for a filter before a patient.
4. A medication in the url without a valid patient is dropped, and the user is told.
5. A patient missing a dimension the rules bound on is told on the page: without an age, only
   rules without an age bound are offered; with an age but no estimate and no measured weight and
   height, the user is asked to enter them.
6. The estimate never becomes `Measured`.
7. A Session the platform has no data for opens on the data the last version was signed on, as
   before ([#640](https://github.com/informedica/GenPRES/issues/640)), and otherwise on no
   patient: the panel asks for data, and nothing is evaluated until it is entered.

## Confidence

High for the Shared types, the Session change and the machines: the states and their tests exist,
and the DTO split follows the libraries' `Dto` modules. The private record constructor is the
first in the code base; Fable erases accessibility, so the client is unaffected, and the
serializer never sees the domain type. Medium for the panel and the notice: the pages
are not under test, and the split between the panel's draft and the patient held is new
behaviour. The test churn of the first step may need a split of its own.

## Steps

Each step is one PR of at most 200 changed lines. Client `.fs` files are edited directly; Shared
and Server changes are prototyped in the libraries' `Scripts/` folders unless direct edits are
authorized for this issue.

1. **Shared: the record renamed, source-compatible** (`refactor(api)`). Today's `Patient`
   record becomes `PatientDto`, the wire and draft shape, with `type Patient = PatientDto` kept
   as an abbreviation so that every caller compiles unchanged; `Patient.empty` becomes
   `PatientDto.empty`, the blank draft, the only place an all-`None` value is legitimate, with
   `Patient.empty` kept as an alias for now. The `Patient` module and its functions (`setYear`,
   `setWeight`, `getAge`, `toString`, ...) stay where they are: an abbreviation covers the type,
   not the module, so nothing moves in this step. Builds and tests green with no caller touched.
1b. **Callers on the DTO name** (`refactor`). The wire records (`OrderContext`, `OrderPlan`,
   `Formulary`, `PatientContext`, `SignedOrderPlan`, the challenge's reading,
   `OrderPlanCommand.Open`), the panel, and every test site that means the wire or the draft say
   `PatientDto`; the mutators and the draft helpers move to a `PatientDto` module with their
   callers, the accessors and `toString` stay for the domain type of 1c; the aliases go.
   Mechanical, about 70 sites, possibly two commits (source, tests).
1c. **The domain patient** (`feat(api)`). A new `Patient` record with a private constructor,
   `Patient.fromDto : PatientDto -> Result<Patient, PatientError>` (`PatientError` a DU: no age
   and no measured weight and height), `Patient.toDto`, and accessors for what the readers need
   (`age`, `weight`, `height`, `gender`, `department`, ...). The client's `State.Patient`, the
   machines' `Patient` cases and `AppEnv.IPatient` hold it; a context or command sent to the wire
   takes `Patient.toDto`. The server's `mapFromSharedPatient` takes it, so the core never sees a
   DTO patient. One valid fixture patient (ten years, through `fromDto`) replaces the empty one
   in the machine and server tests. Tests: `fromDto` on the truth table (age only; weight only is
   `Error`; weight and height; nothing); `toDto` then `fromDto` is the identity.
2. **The estimate stays an estimate** (`fix(client)`, the root of #488). `applyNormalValues` no
   longer promotes the estimate into `Measured`; `getWeight` and `getHeight` already fall back to
   the estimate, and the server mapper reads them, so the server sees the same values. A gender
   change in the panel keeps the measured values and clears only the estimates. Tests: nothing
   entered leaves `Measured` at `None`; a measured weight survives a gender change. The
   notification #488 asks for stays with #488.
3. **Server: every ingress converts, no data is `None`** (`fix(server)`). One function in the
   server, applied where a DTO patient enters: `processOrderContext` (the context's patient),
   every `OrderPlanCommand` case (`Recalculate`, `Navigate`, `AddOrderContext`,
   `NewOrderContext`, `RemoveOrderContexts` carry a plan with a patient; `Open` carries the
   patient itself), and `RequestSignChallenge` (the plan signed, whose patient the version
   stores). Each runs `Patient.fromDto` on what it received before anything else and answers
   the `Error`: the outer ring parses at ingress, no DTO patient reaches the core, and a signed
   version stores only the DTO of a domain patient. The check lives in the three `processCmd`
   dispatchers (`ServerApi.OrderContextCommand.fs`, `ServerApi.OrderPlanCommand.fs`,
   `ServerApi.SigningCommand.fs`), so no member can forward a plan around it.
   A second check at the same place, until estimation runs server-side: the patient handed to
   the core must have a weight and a height, measured or estimated. A patient with an age only
   and no estimate, which the platform or the MCP host can send since only the client estimates,
   is answered with an `Error` naming the missing weight and height, instead of today's silent
   answer of no rules and no scenarios from `getRules`. The follow-up on server-side estimation
   lifts this check.
   `PatientContext.Patient` becomes optional: a PatientId the platform has no data for.
   `sessionPatient` answers the reading, else the head's patient, else `None`; a reading below
   the minimum counts as no data. The stub and the services lose `Patient.empty`; the client's
   session machine binds the optional patient. Tests: `no-data` without a record opens on no
   patient; with a record on the head's patient, as before; each of the three dispatchers refuses
   a DTO below the minimum; an age-only DTO without an estimate is refused with the weight and
   height named.
4. **Client machines: no seed before a patient** (`refactor(client)`, decision b of plan 691).
   `OrderContextWorkbench.Seeded` and its constructor deleted; a seed or a command without a
   patient is dropped; the projections lose the arm. `App.fs`: the initial workbench is always
   `noPatient`; on a url the patient is set before the medication is seeded, so the seed lands on
   a patient held and is evaluated at once; without a patient the medication is dropped with a
   warning in the log and the panel's "enter patient data" on the snackbar. The Prescribe page
   uses the localisation term instead of its Dutch literal. Tests: the seed arms of
   `OrderContextMachineTests.fs` rewritten; no projection is `Resolved` before an evaluation.
5. **Client panel: a draft is not a patient** (`feat(client)`). The panel edits a `PatientDto`
   draft and hands the App `Patient.fromDto draft |> Result.toOption`; the `Error` is what the
   panel shows. The panel says what is missing: an age, or a weight and a height. The accordion
   collapses once a patient exists. A draft with a gender only can be lost when the patient held
   goes from some to none; accepted and noted in the doc comment.
6. **The notice for a missing dimension** (`feat(client)`). On the Prescribe page, derived from
   the patient held alone since the server rule is deterministic: no age, only rules without an
   age bound; an age but no estimate and no measured weight and height, enter them. Two terms in
   `Localization.fs`; the workbook rows are added by hand.
7. **Docs** (`docs`). The Patient definition in `docs/domain/core-domain.md` and the Order
   Context section of the GenORDER document state the minimum; `uc-01` step 5.4 and
   `DEVELOPMENT.md` say "no patient data" instead of "an empty patient"; the user guide says an
   age, or a weight and a height. Follow-up issues, filed:
   [#716](https://github.com/informedica/GenPRES/issues/716) server-side estimation for patients
   from the platform or the MCP host, which also lifts the weight-and-height check of step 3;
   [#717](https://github.com/informedica/GenPRES/issues/717) the department default as a hidden
   filter input; [#718](https://github.com/informedica/GenPRES/issues/718) weight alone and
   height alone once an estimate exists; [#719](https://github.com/informedica/GenPRES/issues/719)
   GenFORM tests for a `None` value against a bounded range and for a weight-bounded category;
   [#720](https://github.com/informedica/GenPRES/issues/720) the MCP tool refusing a patient below
   the minimum.

## Verification

- `dotnet run build` and `dotnet run servertests` after every step; the Fable compile of the
  client after steps 4 to 6, with the JSX output inspected.
- The demo walkthrough with `GENPRES_PROD=0`: a launch with `no-data` shows the panel asking for
  data, no workbench and no plan; an age of ten opens both, with weight and height shown as
  estimated; the age cleared and 32 kg entered alone is no patient, the panel naming the height;
  140 cm added makes a patient, with the notice that the age is unknown; the url
  `#patient?pg=pr&md=paracetamol` without a patient shows the snackbar and seeds nothing; the
  same url with `ad=3650` evaluates paracetamol.

## As built

Built in the order proposed, one PR at a time, each under review before the next started: the
Shared and Server changes prepared as patches in a scratch worktree and applied on the word,
the client edited directly. Every step left the build, the Shared and Server tests, the Fable
compile, Fantomas and the dependency-rule check green.

| Step | PR | Landed |
|---|---|---|
| plan | #704 | this document; issue #646 rewritten; plan 691's decision b pointed here; review: buildable stages, every ingress, the age-only path |
| 1, the record renamed | #705 | `PatientDto`, `type Patient = PatientDto` after the `and` group, `PatientDto.empty` with the old name delegating; no caller touched |
| 1b, the callers | #708 | the draft editors in a `PatientDto` module after the `Patient` module; the wire records, ports, session records, the panel and the tests on the DTO name; the abbreviation kept for the machines, the App state and the mapper |
| 1c, the domain patient | #710 | `type Patient = private { Dto: PatientDto }` in `Models.fs`, `fromDto`, `toDto`, `PatientError`; `State.Patient` derived from `State.PatientDraft`; `Evaluated of Patient * OrderContext`, `Opened of Patient * OrderPlan`; the session and signing machines on the DTO |
| 2, the estimate stays an estimate | #711 | `withEstimates` without the promotion into `Measured`, `setGender` out of the panel; `canCalculate` by the minimum, pulled forward from step 5 |
| 3, the server | #712 | `ServerApi.Ingress.fs` in the four dispatchers; the weight-and-height check; `PatientContext.Patient` optional, `sessionPatient` answering none; review: every context of a plan, and the challenge's reread filtered as the open's |
| 4, no seed before a patient | #713 | `Seeded` gone, a seed or a command on `NoPatient` dropped; the url's patient taken before its medication, a medication without a patient dropped with the panel's term on the snackbar; review: the patient update applied directly so its commands go out before the seed |
| 5, the panel | #714 | the summary shows the draft's data and, while it is no patient, what is missing; one term |
| 6, the notice | #715 | an info alert above the selects: age unknown, or the weight, the height or both unknown; four terms; review: the value that is missing named |
| 7, docs | this PR | the Patient definition and the Order Context section, uc-01 5.4, the walkthrough, the user guide; this section; the follow-up issues #716 to #720 |
| 1c undone, 2026-09-16 | | the private wrapper removed again: one record, `Types.Patient`, on the wire and in the panel; `Patient.validate` is the minimum check; the `PatientDto` module merged into `Patient`; `ServerApi.Ingress.fs` is `ServerApi.Patient.fs` |

### Deviations from the text above

- **The domain type lives in `Models.fs`, not `Types.fs`.** A private representation is
  reachable only inside its declaring module, and `Types` already has a `Patient` module for the
  age types, so `fromDto` could not sit next to the type there.
- **The machines hold the patient beside the context or the plan.** The text said the machines'
  patient cases hold the domain type; a context or a plan carries only the data, and the accessor
  that answers the patient held cannot answer a domain patient from one case and data from
  another. So `Evaluated` and `Opened` carry the patient, and `held` and `changing` take it first.
- **The App keeps the draft next to the patient.** `State.PatientDraft` is what the panel edits
  and the lists read, the estimate applied; `State.Patient` derives from it. The panel's draft is
  never lost when the patient held goes from some to none, so the edge the text accepted in step 5
  does not occur, and step 5 came down to the sentence.
- **The ingress covers more than the text named.** Every context a plan carries, on every plan
  command and the challenge; the formulary filter; and the challenge's reread of the platform,
  filtered as the open's is. A plan to sign whose data is no patient answers the existing
  `NoPatient` refusal, its comment widened, rather than a new wire case.
- **The notice names the value that is missing.** Four terms instead of two: with an age, the
  weight and the height can be missing separately.
- **The workbook rows are the user's.** Five terms were added; their rows are printed by
  `printPatientRow` and `printPrescribeRows` in `Shared/Scripts/Localization.fsx` and go into the
  Localization workbook by hand, the Dutch fallback standing until then.
- **Left as found.** `OrderContext.empty` and `OrderPlan.empty` keep a blank draft as their data:
  a draft is a legitimate wire value, and the ingress refuses it. The readers of the draft stay
  in the `Patient` module, typed on `PatientDto`; moving them to `PatientDto` is a cleanup for
  another day.
