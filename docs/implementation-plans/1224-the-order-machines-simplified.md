# Implementation plan for issue #1224: the order machines simplified

Plan B ([the client sends commands only](1224-the-client-sends-commands-only.md)) left both order
machines sending commands only, one request at a time, each over the last answer. The structure the
machines had for waiting and replayed commands stayed, and beside it a second line of defence: a
machine drops or replaces what reaches it while a request runs, although the page is already
greyed. This plan takes both out. The machines exist so that two actions that conflict cannot be
under way at the same time; once the pages make that so, nothing in a machine has to catch it.

It is a refactor with visible changes, each decided below:

- a few more controls are disabled while a request runs;
- the formulary and parenteralia pages follow the workbench when its answer lands;
- a prescription clears the workbench and opens the plan page at the click;
- a url change starts the client over;
- the one Refresh becomes two.

The work hangs under [#1224](https://github.com/informedica/GenPRES/issues/1224).

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [Out of scope](#out-of-scope)

## Problem description

The machines barely shrank over plan B: `OrderContextMachine.fs` went from 579 to 623 lines,
`OrderPlanMachine.fs` from 685 to 594. What went was the request logic for a command that waits
(`Pending`, `carries`, `replay`, `replaced`) and the argumentation writes. What stayed:

- **Five layers per message.** A message passes `step` (the workbench or the cart), which returns
  intents; `apply` folds the intents into the request under way and the effects; `run` sets the
  selection and the refusal per message; `move` takes the cases that bypass `step`; `transition`
  takes the reopen and the restore. The intents existed so that a command could wait and go out
  over another context. With one request at a time and every command over the last answer, almost
  every intent is "put the request in flight and send it": `Open`, `Clear`, `SeedFilter` and `Call`
  all send a view command; `PatientChanged` and the plan's `UpdatePatient` send the other request.
- **Guards for what the pages already prevent.** Each of these catches a second action during a
  request:
  - `admitted` and `transitionWhile` drop a page's command during a patient change or a signature;
  - `apply` drops a `Call` while a request runs;
  - `landing` and the Session's token guards drop an answer to a request since replaced.

  The pages grey their controls for the same cases, so in the normal flow none of these fires
  ([#1327](https://github.com/informedica/GenPRES/issues/1327)). Where a control is not greyed,
  the guard decides what happens, and it decides differently per place: a dropped command, a
  replaced request, a dropped answer. `viewWhile` and `dialogWhile` are not guards: they are how
  the pages grey during a patient change. They return the last answer unchanged, labelled
  `Changing` while the patient machine has a request out, and App applies them once for the nine
  views that read the order views.
- **A context the client computed, sent to the server.** In `OrderContextState.move`, a patient
  change and a seed that arrive while a request runs go out over `OrderContextWorkbench.shown`, the
  client's preview of the command under way, and replace it; the answer to that command is
  dropped. The server never answers the command the preview stands for, and evaluates the next one
  over a context it did not make. For a pick of an order value the preview sets the variable
  without solving the order. The reset after a prescription (`Reset`) replaces the request under
  way the same way, through the `Clear` intent, which `apply` does not guard.
- **A prescription reaches into the workbench from the plan.** The plan's answer to
  `AddOrderContext` emits `GoToPlanPage` and `ResetWorkbench`, and `App` turns the second into a
  workbench `Reset`. While the plan answers, the prescribe page's filter is not greyed (it greys on
  a workbench request only), so a pick made in that second goes out as a workbench request, and
  the reset arriving with the plan's answer replaces it: the pick is lost.
- **The pages follow the preview, not the answer.** A filter command puts the filter of its
  preview on the formulary and parenteralia pages as it goes out (`evaluate`), a failure puts the
  held filter back (`restore`), and an answer puts nothing. A choice the server makes itself, such
  as a route picked because it is the only one, never reaches the pages. Two more things:
  - each sync starts a fetch of both pages (`LoadFormulary Started`, `LoadParenteralia Started` in
    `App.fs`), which is dropped while a fetch of that page runs, so the synced filter is then
    overwritten by the fetch's answer
    ([#1326](https://github.com/informedica/GenPRES/issues/1326), item 6);
  - the pages' selects are greyed while the workbench changes, not while their own fetch runs, so
    a pick during that fetch is dropped.
- **A patient change drops a plan command under way.** The plan's `UpdatePatient` replaces the
  request in flight; a prescription (`AddOrderContext`) or a removal under way is lost without a
  word (#1326, item 1). A patient change during an open sends the version's contexts again for the
  new patient, as given, nothing evaluated.
- **One Refresh does two things.** The held dialog's "Ververs uit het EPD" (`Views/Patient.fs`)
  reads the patient from the EHR again and reopens the last signed order plan, dropping the new
  and changed orders. Its answer sends the patient to the patient machine and the signed plan to
  the plan machine at once, so the plan opens for the old patient and is opened again for the new
  one.
- **Plan commands carry a plan the machine throws away.** The pages build
  `AddOrderContext(tp, ctx)`, `NewOrderContext(tp, category)` and `RemoveOrderContexts(tp, ids)`
  over the plan they show; `OrderPlanCart.rebase` replaces that plan with the one held. Its
  `UpdatePatient` and `FilterRows` branches are never reached, since the machine builds those
  commands itself. `OrderPlanMsg.Filter` and `OrderPlanCartMsg.Filter` are two paths for one
  command. The prescribe button narrows the workbench it reads from `OrderContextView.Settled` to
  the chosen scenario and its form, and sends it: the last page that hands a context it got from
  the server back to a machine.
- **Smaller things.**
  - `OrderPlanMachine.Dialog` holds only `shown`, and the order context machine depends on the
    plan machine for it.
  - The refusal is a field of its own, set and cleared per message in `run`, beside a workbench
    that could carry it.
  - `context` and `view` repeat one match.
  - A reopen while a request runs goes through `move` only to be dropped by `apply`.
  - `OrderContextEffect.CallContext` and `CallPatientChanged` are two effects for one wire
    command, where the plan's `CallPlan` carries its wire command whole.
  - The seed that waits for a patient has a slot of its own, `NoPatient of awaiting: FilterSeed
    option`.

What can start a request, and what each depends on:

- **The patient** changes from the panel, and from the Session's answers: a launch, a resume, a
  refresh, a renewed token after a signature, an accepted data notice.
- **The workbench** depends on the patient. It changes from the prescribe page, the order dialog,
  the medication lists, the formulary and parenteralia pages, the url's medication, the reload of
  the resources, and the reset after a prescription.
- **The plan** depends on the patient, on the workbench for a prescription, and on the Session for
  the open of a signed version. It changes from the plan page, the nutrition pages, the order
  dialog, the held dialog's remove, and the prescribe button.
- **The signature** depends on the plan.
- **The formulary and parenteralia pages** depend on the workbench's filter and fetch for
  themselves.
- **The url** changes from the browser: a navigation, back or forward.

## Decisions

1. **No two conflicting actions at the same time; the pages make it so, the machines do not catch
   it.** Decided (user, 2026-10-08).
   - A control that starts a request on a machine is disabled while that machine has a request
     out; while a machine whose state that request reads when it is sent has a request out; and
     while a request that reads this machine's state is out. The dependencies are per request,
     not per machine: a workbench request and a plan request read the patient, a plan request
     never reads the workbench, a workbench request never reads the plan. The one cross-reading,
     the prescription, reads the workbench at the click and nowhere else (decision 8), so a plan
     request greys no workbench control and a workbench request greys only the prescribe button.
   - A request whose answer changes the patient, the panel's refresh and the signature, starts
     only while the workbench and the plan are idle and greys both while it is out, since both
     depend on the patient. The pages switch by the tab bar, not by the url, so a request sent
     from one page is still out on the next; the sign button therefore reads the workbench as the
     panel's busy does, and the signature greys the workbench as it greys the plan.
   - A change not from the user is the answer to the one request out, so it reaches machines that
     are idle.
   - Nothing then reaches a machine while it waits, and a machine holds no second guard for it:
     `admitted`, `transitionWhile`, a dropped `Call`, a replaced request, a context sent over the
     preview, all go.
   - A transition is written for the states that can be reached; one closing arm leaves the state
     as it is, and the trail, which records every message, is where an unreachable one would show.
   - The request id stays as the one check that an answer is the one awaited (decided by the user,
     2026-10-08): it is the invariant the tests prove, not a second guard.
   - **Per machine, as today** (user, 2026-10-08): each page reads the machines it depends on and
     greys on them, as the patient panel reads the patient, the workbench and the plan now. The
     workbench and the plan are read through `viewWhile` and `dialogWhile`, which stay (user,
     2026-10-08) and take, beside the patient:
     - whether the Session has a refresh or an open of a signed version out;
     - whether a signature is under way (decision 4).

     Then every workbench and plan control greys during any of them, and the panel's Refresh
     through the panel's existing busy, without a list per button. The gaps are filled, not the
     rule changed:
     - the medication lists (step 1);
     - the formulary and parenteralia pages during their own fetch (step 2);
     - the Session's refresh and open and the signature as inputs of `viewWhile`, and the reload
       of the resources while the workbench changes (step 4);
     - the order dialog's fields while a step is being counted (step 9).
   - Greying is visible: a disabled control, as the formulary's selects. A click is never
     swallowed in silence.
2. **A url change starts the client over.** Decided (user, 2026-10-08): every change of the url,
   with or without an open Session, is handled as a page load: `init` with the url, the Session
   resumed when there is one.
   - What starts over: the five lanes (the Session, the patient, the workbench, the plan, the
     signature) and the state the url sets (the page, the language, the disclaimer), the url's
     patient and medication applied as at launch. What is kept: the fetched resources (settings,
     localization, normal values, the medication lists, products, drug names), the admin login and
     the rest of the UI state. A Back press does not log the admin out or fetch every resource
     again.
   - An answer to a request from before finds no request under its id and is dropped, which is the
     one use the request ids have beyond the tests. The Session's resume gets one for this: today
     its answer carries no id and lands on any resume out, so a url change during a resume would
     take the first resume's answer for the second's and drop the second's. The Session's other
     requests need none: starting over leaves no presentation, no close and no PIN request for
     their answers to match.
   - A signature under way counts as unsigned work for the question below, since the sign button
     needs orders, not differences. On yes the signing lane starts over with the others, as a
     browser reload during a submission would: the submission's answer finds no request, and the
     resumed Session shows whether the plan was signed.
   - The one exception is the url change the router fires on mount, which is the launch itself
     and may arrive while a resume is out: it applies the url as today and starts nothing over.
   - A url change is the one change not from the user that is not an answer, and this takes it
     out of the running machines; without a Session it is today applied at once, which after
     step 8 would meet a request with nothing to catch it.
   - **With new or changed orders, the client asks first.** Decided (user, 2026-10-08, (a) of
     three). Starting over resumes the Session, which reopens the last signed order plan, so the
     new and changed orders would go; on a page load the browser asks about them through the
     leave-page guard, but a change of the url's hash fires no `beforeunload`, so the client asks
     the same question itself.
     - Yes starts over.
     - No puts the previous url back and changes nothing, and that restore is marked so that the
       `UrlChanged` it fires is not taken as a change.
     - Ignoring the url while there is unsigned work would swallow a navigation in silence;
       starting over without asking would lose orders on a Back press.
3. **No waiting change.** The patient and the seed that the machines hold today while a request
   runs (`move` over the preview), and the slot an earlier draft of this plan proposed for them,
   are not needed under decision 1:
   - a patient change not from the user is the answer to a Session request, during which the pages
     that would start a workbench or plan request are greyed;
   - a seed not from the user is the answer to a reload, during which the workbench is idle, or
     comes from the url, which starts the client over;
   - the one wait that stays is at launch and resume, where the signed plan arrives before the
     patient and waits for it (`NoPatient of awaiting`): an initial state, not a guard beside a
     request. The seed that waits for the first patient stays with it, for the url's medication
     at launch.
   - **Two of these rest on something other than a disabled control.** Neither may be removed
     without a greying in its place.
     - A token renewal comes only from a signature's answer and reaches an idle workbench and plan
       because the plan shows as changing from the sign click on (decision 4) and the sign dialog
       is modal.
     - The reload seed reaches an idle workbench because the Settings page shows a full-screen
       backdrop while the resources reload (`Views/Settings.fs`). The backdrop lifting on any
       workbench answer (#1326, item 8) goes with step 4, since it lifts the one thing that keeps
       the workbench idle.
   - **The data notice's patient reaches the panel after the signature.** Decided (user,
     2026-10-08, (a) of two). Today, when the user accepts a data notice and the patient context
     is not held, `SigningMachine.accepted` sends the new patient to the panel and asks the new
     challenge at once, so the patient command and then the plan's recalculation run during the
     signature: a request the signature starts itself, which no greying stops, and the plan
     marked as signed is then the recalculated one or the one before it. The held case already
     keeps the new data until the signed answer brings it with the token renewal. That becomes the
     rule for both: `accepted` asks the challenge only, over the plan with the new data when not
     held, and the patient follows the signature. The alternative, a wait in the signing machine
     until the recalculation lands, was dropped.
     - The plan the challenge carries then has the new patient data with totals calculated for
       the old; the server's commit does not recalculate (`ServerApi.Adapters.fs`). This is as
       today, since the recalculation beside the challenge never reached the signature, and every
       open recalculates, so the user never sees the stored totals. Step 8 checks whether a stored
       version's totals are read anywhere else.
4. **The plan holds no patient change, and no version replaces a request.**
   - A patient change during a plan request cannot happen under decision 1, so the plan's
     `UpdatePatient` neither replaces nor waits; the special case of a patient change during an
     open goes with it.
   - A signed version arrives when nothing is out, so `Version` opens, it does not replace.
   - #1326 item 1 and item 2 (a patient change during a signature) are closed by the greying, not
     by the machine; the one patient change a signature starts itself, the accepted data notice,
     is closed by decision 3, which moves it after the signature.
   - **A signature under way greys the plan.** Today only the plan page's cells read the signature
     (`cellsRest`); the rest of the page, the prescribe button and the nutrition pages grey only
     behind the modal sign dialog, which opens once the challenge is answered. While the challenge
     request is out (`SigningView.Requesting`) a plan command can still go. So a signature under
     way becomes the third input of `OrderPlanState.viewWhile`, beside the patient and the
     Session's refresh or open, and the plan shows as `Changing` from the sign click to the
     answer. That is what lets step 8 remove the signing half of `admitted`.
   - **A signature under way greys the workbench too, and the sign button reads the workbench.**
     The signed answer renews the token with the patient, which the panel sends on to the
     workbench. The pages switch by the tab bar, so the user can send a filter request from the
     prescribe page, switch to the plan page and click Sign; or click Sign and, while the
     challenge request is out and the dialog not yet modal, switch to the prescribe page and send
     one. Either way the patient would land on a workbench with a request out. So the signature
     is the third input of `OrderContextState.viewWhile` and `dialogWhile` as well, and the sign
     button is disabled while the workbench view is `Changing`, read through `IOrderContext` as
     the panel's busy reads it.
   - **The two buttons that open a signed version read the plan.** The newer-version notice's
     action and the plan page's refresh (decision 9) send `OpenVersion`; neither reads
     `viewWhile`, and `Components.Notice` has no disabled on its action. Both are disabled while
     the plan view is `Changing`, so a signed version never lands on a busy plan.
5. **The pages follow the answer.** Decided (user, 2026-10-06): the formulary and parenteralia
   pages take the filter of the answer when it lands, and no longer the filter of the preview when
   the request goes out. The pages are greyed while the request runs, so the user sees the old
   choices greyed and then the answer; a choice the server made itself now reaches them. A failure
   leaves them as they were, so the re-sync in `restore` goes.
   - **Not every answer.** A sync starts a fetch of both pages. Synced on every answer, every step
     of a dose in the dialog would fetch both pages again. The machine syncs on the answer to a
     command that changes the filter, and on the answer to a patient update: `changesFilter` moves
     from the send to the landing, where the request sent is still in `InFlight`.
   - **The patient update syncs.** Decided (user, 2026-10-06). A patient change empties the pages'
     filter (`setPatient`), and it cannot keep it, since the filter's options depend on the
     patient. The answer to the workbench's patient update is its filter evaluated for the new
     patient, which the pages take. Today they get the filter from before the change, as the
     update goes out.
   - **The first evaluation for a patient syncs as well.** It sends `ClearAllFilterProperty`, as a
     reset does, so the landing cannot tell the two apart without a marker. Its answer is the empty
     filter for the patient, which the pages already show, so the sync changes nothing the user
     sees and costs one fetch of each page per new patient; no marker.
   - **The pages' own fetch counts as a request.** Their selects are greyed while it runs, and a
     fetch asked while one runs is asked again when that one lands, instead of dropped (#1326,
     item 6). Decided (user, 2026-10-06, the second half; the first follows decision 1).
6. **One layer per message.**
   - Each machine gets one `transition` over the message and the state; the intents, `apply`,
     `run` and the stage modules (`OrderContextWorkbench.step`, `OrderPlanCart.step`) go.
   - Each machine keeps a local helper that puts a command in flight and emits its call, since
     every sending arm does the same.
   - The state stays private, the views stay as they are, and the test constructors (`opening`,
     `held`, `changing`, ...) stay.
   - No shared request module: plan B step 9 weighed that and dropped it.
7. **A page sends what it wants, never what it got.** The plan machine builds every wire command
   over the plan it holds. The prescribe message names the order chosen: `Add of orderId`
   (decided by the user, 2026-10-06). `App` narrows the workbench the order context machine holds
   to the scenario with that order and its form, as the prescribe button does now, and hands it to
   the plan machine, so no page passes a context or a plan to a machine any more.
8. **A prescription clears the workbench and opens the plan page at the click.** Decided (user,
   2026-10-06).
   - The prescribe click sends `Add` to the plan, the reset to the workbench and opens the plan
     page, at once; the plan's answer to `AddOrderContext` no longer emits `GoToPlanPage` and
     `ResetWorkbench`.
   - The reset goes out over an idle workbench, since the prescribe button is disabled while a
     workbench request runs, and the workbench then stays greyed until its answer, so no pick can
     come between the prescription and the reset.
   - The plan page shows the plan greyed until the order lands.
   - Accepted: a prescription that fails, a server error or a Session problem, leaves an empty
     workbench with the error told, where today the workbench stays.
   - **The Session's refresh and open of a signed version count as requests.** Decided (user,
     2026-10-08). Both change the patient or the plan, so the prescribe button, the sign button,
     the held dialog's buttons and the newer-version notice's button are disabled while one runs.
     The Session machine does not track them today (#1326, item 7): `Refresh` and `OpenVersion` set
     no `InFlight`. They cannot go into `InFlight`, which also hides the session and its token,
     gates every command and `TokenRenewed`, and is matched by the two answers. So a field of its
     own, `Reopening`:
     - set by the two commands;
     - cleared by their answers, by a failed answer and when the Session leaves the open phase;
     - with one query the views read; `session`, `token` and `view` untouched.
9. **Two refreshes.** Decided (user, 2026-10-08).
   - The patient panel's Refresh reads the patient from the EHR again and nothing else: a fresh
     opened token, the standing challenge spent, the data notice dropped, the patient to the
     panel, which the plan and the workbench follow with `UpdatePatient` as after any edit. It is
     disabled while the plan has a new or changed order, as the panel's fields are.
   - The plan page's refresh opens the last signed order plan and nothing else: `OpenVersion` on
     the head; the new and changed orders go with it. The held dialog's second way out becomes
     that open.
   - A patient with no signed order plan yet has no head (`SessionOpened.Head` is an option), and
     the user can add orders there. Then the plan page's refresh is disabled, since there is
     nothing to open, and the held dialog shows the remove button alone: removing the new orders
     is what the refresh would have done. Today's `refresh` covers this case by clearing the
     patient and setting it again, so the plan opens empty; that goes with the head reopen.
   - The server's `refresh` keeps its name and loses the head reopen; no new command.
   - ADR-0007 and the use case record the two buttons.
10. **A step being counted counts as a request.** Decided (user, 2026-10-08). A step button
    collects clicks for 700 ms and then sends one command; the field shows the stepped value
    meanwhile. During those 700 ms nothing is greyed, so another field's pick can go out first,
    and the collected clicks then find the field disabled and are dropped: the value snaps back
    (#1326, item 5). Under decision 1 the dialog's other fields are greyed while a step is being
    counted, so the command that follows is the only one, and the check at the moment the timer
    fires (`QuantityField.fs`, `disabledRef`) goes.

## Steps

One pull request per step, one open at a time.

- Every step in `Client.Core` or on the server starts as a script with its tests, unless the user
  asks for source; steps 1, 7 and 9 are client view and App code alone.
- Steps 1 to 9 and 12 fit the 200-line limit; steps 10 and 11 rewrite one file each and exceed it
  by nature, which the pull request says.
- Every step that changes a message or an effect changes `Trail.fs` and its tests in the same pull
  request.

1. **The medication lists are disabled while the workbench changes.** `Views/EmergencyList.fs` and
   `Views/ContinuousMeds.fs` read the workbench through `AppEnv.IOrderContext` and grey their rows
   while it is `Changing` or the patient is changing, as the formulary page's selects. Decision 1.
2. **The pages follow the answer and grey during their own fetch.** Decision 5.
   - The order context machine syncs the formulary and parenteralia pages from the context
     answered, evaluated or refused, when the request sent changes the filter or updates the
     patient; `evaluate`, the `Sync` intent and the re-sync in `restore` go, `changesFilter` moves
     to the landing, and `SyncFormulary` and `SyncParenteralia` become one effect.
   - In `App.fs`, a page fetch asked while one runs is asked again when that one lands.
   - `Views/Formulary.fs` and `Views/Parenteralia.fs` grey their selects while their own fetch
     runs.
   - Tests: a filter command syncs on its answer and not before; a patient update syncs on its
     answer with the filter answered; a value pick syncs nothing; a failure syncs nothing.
3. **Plan commands from the pages without the plan.** Decision 7.
   - `OrderPlanMsg` gets `Add of orderId`, `New of category` and `Remove of string[]`, as
     `Navigate` names a context and a command only; the machine builds the wire command over the
     plan it holds, and `App` narrows the workbench held to the order's scenario for `Add`.
   - The `Command` case, `OrderPlanCart.rebase` and the cart's own `Filter` go.
   - `AppEnv.IOrderPlan` follows, and the five views that build plan commands: `OrderPlan.fs`,
     `Prescribe.fs`, `Patient.fs`, `EnteralNutrition.fs`, `ParenteralNutrition.fs`.
4. **The Session's refresh and open count as requests** (a fix under #1326 item 7). Decisions 1, 4
   and 8.
   - `SessionState` gets `Reopening`, an option of a refresh or an open, beside `InFlight`;
     `Refresh` and `OpenVersion` set it; `Refreshed` and `Reopened` clear it, on any answer, `Ok
     None` and `Error` included; a second `Refresh` or `OpenVersion` while it is set is dropped,
     and leaving the open phase clears it.
   - `SessionState.reopening` is the query; `SessionRequest`, `SessionView`, `session` and `token`
     do not change.
   - `OrderContextState.viewWhile`, `dialogWhile` and `OrderPlanState.viewWhile` take it as a
     second input beside the patient, and the signature under way as a third; App passes them.
     Every workbench and plan control then greys while one is set: the prescribe button, the sign
     button, the held dialog's buttons, the row filter, the dialog's fields and the nutrition
     pages among them, and the panel's Refresh through the panel's busy.
   - `Views/OrderPlan.fs` disables the sign button while the workbench view is `Changing`, read
     through `IOrderContext` as `Views/Patient.fs` reads it for the panel's busy.
   - `Components.Notice` gets a disabled on its action, and the newer-version notice's button is
     disabled while the plan view is `Changing`.
   - `Views/Settings.fs` disables the reload of the resources while the workbench changes, and the
     reload backdrop no longer lifts on a workbench answer.
   - Tests: a workbench request during a refresh carries the token; the workbench and plan views
     are `Changing` while it is set; the workbench, dialog and plan views are `Changing` while a
     signature is requesting; a second `Refresh` during the first is dropped; a failed `Reopened`
     clears it.
5. **Two refreshes.** Decision 9.
   - Server: `refresh` no longer reopens the head, as a script first.
   - `SessionMachine.Refreshed` emits `SetPatient` only.
   - `Views/Patient.fs` moves Refresh out of the held dialog onto the panel, disabled while the
     plan has a new or changed order and through the panel's busy while the patient, the workbench
     or the plan has a request out, and gives the dialog "open the last signed order plan" through
     `OpenVersion` on the head.
   - `Views/OrderPlan.fs` gets the same button, disabled while the plan view is `Changing` and
     while the Session has no signed order plan; the held dialog then shows remove alone.
   - ADR-0007 amended.
   - Tests: a refresh answered sends the patient and nothing to the plan; the plan follows the
     patient answer with `UpdatePatient`.
6. **The prescription at the click.** Decision 8. The prescribe click sends `Add`, resets the
   workbench and opens the plan page in one update of `App`; `GoToPlanPage` and `ResetWorkbench`
   leave `OrderPlanEffect` and the plan's `answered`. Tests: an answer to `AddOrderContext` emits
   only the interaction check; the trail shows the three at the click.
7. **A url change starts the client over.** Decision 2.
   - Every `UrlChanged` but the one the router fires on mount puts every lane in its initial
     state, `init` with the url, the Session resumed when there is one, the url's patient and
     medication applied as at launch; the url seed and the url patient during a running request
     go with it.
   - `SessionMsg.Resume` and `Resumed` carry a request id, as the order machines' requests do, and
     `SessionRequest.Resuming` holds it: a `Resumed` under another id falls to the closing arm.
     The App's resume call passes the id back with the answer, and the trail shows it.
   - With new or changed orders the client asks the leave-page question first. No puts the
     previous url back through `Router.navigate`, which fires `UrlChanged`, so the restore is
     marked and that `UrlChanged` is not a change. `Router.navigate` pushes a new history entry:
     after no, the url the user left is the newest entry, what was forward of it is gone, and
     another Back asks again. `history.replaceState` would fire nothing and need no mark, but
     after Back it would overwrite the entry the user went back to, so it is not used.
   - Tests: a url change during a workbench request drops that request's answer; a url change
     during a resume drops the first resume's answer and takes the second's; the five lanes after
     it equal the lanes after `init` with that url, and the fetches and the admin login are as
     before; the mount's url change starts nothing over; with unsigned work or a signature under
     way nothing starts over until yes.
8. **The guards out of the order machines.** Decisions 1, 3 and 4.
   - `admitted` and `transitionWhile` go from both machines, with their tests, the signing half of
     the plan's included, since step 4 greys the plan from the sign click on; `viewWhile` and
     `dialogWhile` stay.
   - The two cases of `move` that send over `OrderContextWorkbench.shown` go, and the unguarded
     `Clear`; `NoPatient of awaiting` keeps the seed for launch only.
   - The plan's `UpdatePatient` sends over the plan held with nothing out, the patient-during-open
     case goes, `Version` opens without replacing; a `Call` with a request out falls to the
     closing arm.
   - `App.update` calls `transition`.
   - `SigningMachine.accepted` no longer emits `SetPatient`: the new data reaches the panel with
     the signed answer, as when the patient context is held (decision 3). The step checks whether
     a stored version's totals are read anywhere, since the plan signed carries totals for the
     patient before the notice.
   - #1327 is closed as removed.
   - Tests: an accepted notice with new data emits the challenge alone, over the plan with the new
     data when not held. Every test that sent a second message during a request and asserted it
     was dropped is replaced by a test on `viewWhile` and `dialogWhile` in `Client.Core`, where
     the greying lives: for each input (the patient changing, the Session's refresh or open, the
     signature under way) and each view case (`Settled`, `Refused`, `Changing`, `NoPatient`), the
     view is `Changing` or unchanged as the rule says. There is no view test project (#598), so
     the wiring from `viewWhile` to a disabled control is checked in the browser list below, not
     under Expecto. The pull request names each test it replaces.
9. **A step being counted counts as a request.** Decision 10. The order dialog greys its other
   fields while a step button counts clicks; the `disabledRef` check in `QuantityField.fs` goes.
10. **One layer: the order context machine.** Decision 6, over the machine steps 2 and 8 left. The
    refusal becomes a case of the workbench, `CallContext` and `CallPatientChanged` become one
    effect over the wire command, `context` and `view` share one match. Tests: the machine tests
    through `transition` only, every case kept.
11. **One layer: the order plan machine.** Decision 6, over the machine steps 3, 6 and 8 left.
12. **The smaller things.** `Dialog.shown` leaves `OrderPlanMachine` for a module of its own in
    `Client.Core`, so the order context machine no longer depends on the plan machine for it. This
    plan's As built, with the line counts before and after.

Steps 10 and 11 are the rewrites that exceed the limit; step 12 is the last. Step 9 can land
anywhere after step 1.

Line counts:

- Before: `OrderContextMachine.fs` 623, `OrderPlanMachine.fs` 594.
- The five layers are some 250 lines of the first and some 200 of the second, the guards some 30
  of each; one transition is expected to take back about 150 and 120.
- Expected after: around 470 and 460.
- Steps 10 and 11 must each leave their file shorter, and the plan as a whole must leave both
  shorter than before (decided by the user, 2026-10-06).

## Verification, per step

- **Every code step:** `dotnet test tests/Informedica.GenPRES.Client.Core.Tests/`,
  `dotnet run servertests`, `scripts/CheckDependencyRule.fsx`, Fable and `npx vite build`.
- **Every code step, in the browser with the trail.** This list is where the wiring from
  `viewWhile` to a disabled control is checked, since the views have no tests (#598). In each of
  these, the control that would start a second request is greyed, and the trail shows no message
  dropped by a machine:
  - a filter pick, a scenario choice, picks and clears with the picks kept, a reopen closed without
    a pick;
  - a patient edit followed at once by the next field;
  - a formulary and a parenteralia page change, an emergency and a continuous list item;
  - a resource reload;
  - a prescription, a removal, a row filter, the plan dialog, the nutrition slot;
  - a signature, the patient refresh, the plan refresh;
  - a url change during a request.
- **Step 2:** a filter whose answer picks a choice itself (a single route) shows that choice on the
  formulary page; a patient change shows the filter evaluated for the new patient on both pages; a
  dose step in the dialog fetches neither page; the formulary's selects grey while it fetches.
- **Step 4:** the held dialog's refresh, then at once the prescribing page: the prescribe button
  is greyed until the refresh answers; after the sign click, the row filter, the prescribe
  button and the prescribe page's filter are greyed until the challenge answers; a filter pick
  on the prescribe page, then the plan page at once: the sign button is greyed until the filter
  answers; the newer-version notice's button is greyed while the plan changes.
- **Step 5:** the patient refresh after the weight changed in the EHR: the plan shows the orders
  for the new weight, the orders kept; the plan refresh: the last signed orders, the new and
  changed ones gone; a patient with no signed order plan: the plan refresh is greyed and the
  held dialog offers remove alone.
- **Step 6:** a prescription empties the workbench and shows the plan page at once, greyed until
  the order lands; a prescription that fails leaves an empty workbench and tells the error.
- **Step 7:** a url with another medication during a filter request: the client starts over on
  that url, the first request's answer is nowhere; Back with unsigned work asks, and no leaves
  the page as it was.
- **Step 8:** a data notice accepted with the patient context not held: the plan stays as it is
  until the signature lands, then the panel takes the new data and the plan recalculates.

## Out of scope

- The picks and the dialog's tab in a machine, so that the dialog keeps no state of its own: the
  picks stay in the client by plan B's decision.
- The reopen's `Kept` and `Restore`: a client-side undo that puts an earlier answer back and drops
  the answer to the clear. It is neither a command nor an answer, but what it puts back is an
  answer, so the model holds. Kept as plan B decided.
- The Session machine's token guards (`when current.OpenedToken = from` on `Reopened`,
  `Refreshed`, `Told`): the same kind of guard decision 1 removes from the order machines, in a
  machine this plan does not rewrite. Under decision 1 they catch nothing; whether they go is a
  decision for the Session machine.
- The views' own Elmish programs (the order dialog, the nutrition slot, the interactions page, the
  page menu) and machines for what `App.update` still decides itself.
- Replay of a trail back into the machines.
