# Implementation plan for issue 903

## Problem description

The browser's back button leaves GenPRES for the previous website instead of showing the
previous GenPRES page ([#903](https://github.com/informedica/GenPRES/issues/903)). A user who
has prescribed something and presses back loses the prescribing workbench and whatever the
plan holds beyond the version last signed.

The cause is that a page switch never touches the url. `state.Page` in
`src/Informedica.GenPRES.Client/App.fs` is the only record of the page shown: the side menu
(`UpdatePage`), the move to the plan page after an order is prescribed (`GoToPlanPage`), the
return to the emergency list after a reset (`GoToLifeSupport`) and the login redirect all set
the field and nothing else. The router (`Components/FelizRouter.fs`) only reads the url, on
mount and on a history move; the one writer is `eraseLaunch`, which *replaces* the entry so
that the launch token survives neither a reload nor the back button. The document therefore
lives on exactly one history entry, and back is the previous document.

Two rules of the signing model bound the fix
([uc-03](../scenarios/integration/uc-03-prescribe-and-sign.md),
[uc-01](../scenarios/integration/uc-01-launch.md)):

- a signature is always put on top of the order plan version last signed; a client never
  works on an older state of the plan than the one it holds;
- a Session that was closed, or ended by a newer launch, is not reopened: the only way on is a
  relaunch, which opens a new Session.

Whatever back does, it must not rewind the plan, the Session, the workbench or the patient to
an earlier state.

## Approaches considered

1. **Put the page in the url.** Every page switch pushes a history entry carrying only the page
   (`pg=` on the current url); back and forward change the page shown and nothing else, the
   way the side menu does. Honours both rules by construction, since no state is ever stored
   in or restored from history. Deferred, not rejected: `UrlChanged` was written for the mount
   case and re-applies everything the url carries on every fire, so a history move would
   clear the patient (a url without `by=`/`ad=`), show the disclaimer again, flip the language
   back to `la=` and re-seed the workbench from `md=`. Making it page-only after mount, adding
   the missing page codes (nutrition, plan, interactions, settings), carrying `pg` on
   `#/session` urls, deciding what a history move does to an open PIN dialog, and choosing
   push or replace for the automatic moves is a change of a hundred lines and a few decisions
   for a convenience nobody asked for yet: the issue asks that work is not lost.

2. **Disable history browsing.** Not possible: the back button is the user's, not the page's.
   The trap (push a sentinel entry and push it again on every `popstate`) is a dark pattern,
   Chrome's back-navigation intervention skips entries pushed without a user gesture so it
   does not reliably work, and it would break a legitimate back to MainEHR after a launch.
   Rejected.

3. **Ask before leaving while there is unsigned work.** A `beforeunload` listener that, when
   the page holds work that leaving would lose, lets the browser show its own "leave this
   page?" dialog. It cannot block, only ask; it fires for back, a closed tab and a reload
   alike; browsers show it only after the user interacted with the page, and the text is
   theirs. Nothing about the plan or the Session changes: after the user chooses to leave,
   the next visit resumes on the cookie and opens the version last signed, as a reload does.
   Chosen.

4. **Do nothing.** Today's behaviour already satisfies both rules: back leaves, a relaunch or
   a reload resumes the Session with the version last signed. It is an annoyance, not a safety
   hole. Rejected because the annoyance is real: the workbench and any unsigned order are gone
   without a word.

## Chosen approach

Approach 3. The guard reads the state the app already holds; the one addition is a baseline to
tell unsigned work from the plan as it was signed.

**Unsigned work** is any of:

- a medication on the prescribing workbench (`ctx.Filter.Generic` set on the context the
  order-context machine holds);
- a signature under way (`Signing` not `Idle`: a challenge requested, a data notice shown, the
  PIN asked, a Submission in flight);
- a plan changed since the version last opened or signed. An anonymous plan is never signed,
  so any order put in it is a change.

**What "changed" means** is told by the commands that went out, not by comparing plans. A
plan command that changes what the plan holds — a navigation within an order (a dose or
frequency stepped in a signed order, which keeps the order's context id), an order added, a
nutrition workbench opened in the plan, contexts removed — marks the plan changed; a version
opened (`OrderPlanMsg.Version`, from a resume, a launch or "Open the newest version") or signed
(`SigningEffect.TellSigned`) marks it as signed; a recalculation of the totals and the open
command itself do neither. The state is a two-case `PlanWork` (`AsSigned | Changed`) on the app
state. The Session's own `Head` is not used: it is the version the Session opened on and is not
refreshed by a signature.

Commands, not content, because a context that comes back from the server after a version was
opened is re-evaluated, and a comparison of plans would either ask about work the user did not
do (compare everything) or miss the work they did (compare ids). The command is the one thing
that says a user changed the plan.

**Where it lives**: `UnsignedWorkPolicy.fs` in the client, beside `SigningPolicy` and
`SessionGatePolicy`: pure F#, no React, linked into `Informedica.GenPRES.Shared.Tests` the way
the other client policies are, so every command and every branch of the predicate has a test.
`App.fs` keeps `PlanWork` as the plan messages pass and asks the policy from the listener.

What the guard leaves alone:

- The identity hop (`location.assign "/authorize"`) fires `beforeunload` too; at that point
  plan and workbench are empty, so no dialog.
- A Session that ends, or is closed, while the plan holds a change: the plan's work is not
  reset with the Session, so leaving still asks. What was not signed is still not signed.
- No page-in-url routing. Back still leaves the app when the user confirms; the next visit is a
  resume. Approach 1 stays available if a page history is wanted later, and this guard would
  keep working beside it, since a page-only history move does not unload the document.

## Confidence

High that the guard is correct and honours the two rules: it stores nothing, restores nothing,
and only asks. High that a change is never missed, since every change to the plan goes through
one dispatch site and the policy names every command. Medium on the definition of unsigned
work being the one users expect: a nutrition workbench opened in the plan and left empty counts
as a change, and a patient edit that re-evaluates the plan does not.

## Steps

One PR, client only (`src/Informedica.GenPRES.Client/`), about 100 changed source lines plus
the tests. Fable UI code is the one place the script-first policy does not apply.

1. `UnsignedWorkPolicy.fs`: `PlanWork` (`AsSigned | Changed`), `PlanWork.changedBy` over every
   `OrderPlanCommand`, `PlanWork.afterCommand`, and
   `hasUnsignedWork : OrderContext option -> Signing -> PlanWork -> bool` as defined above.
   Compiled after `SigningPolicy.fs`; linked into the Shared tests.
2. `UnsignedWorkPolicyTests.fs` in `tests/Informedica.GenPRES.Shared.Tests`: a test per
   command for `changedBy`, the three `afterCommand` cases, and the five branches of the
   predicate (nothing; a workbench without a generic; a medication on the workbench; a
   signature under way; a plan changed).
3. `PlanWork` on the app state, `AsSigned` in `initialState`; `afterCommand` on
   `OrderPlanMsg.Command`, `AsSigned` on `OrderPlanMsg.Version` and on
   `SigningEffect.TellSigned`; not touched when the Session changes.
4. In `View`, one `beforeunload` listener registered with `React.useEffectOnce`, reading the
   latest state through a ref, calling `preventDefault` and setting `returnValue` only when the
   policy says there is work; removed on unmount.
5. A line in the user guide (en, nl) under prescribing: leaving the page with unsigned work
   asks first.

## Verification

`dotnet test tests/Informedica.GenPRES.Shared.Tests/` runs the policy tests: which commands
change the plan, what a command does to the plan's work, and every branch of the predicate.

Fable compiles the client (`dotnet fable` from the client folder); the generated `App.jsx`
registers the listener once and returns its removal.

By hand, with `dotnet run` and the stub launch as `prescriber`:

- prescribe something; back, reload, or close the tab: the browser asks. Cancel: nothing
  changed.
- sign; back: no question. Add another order: the question is back. Sign, then step the dose
  of the signed order in the plan: the question is back. Sign, then remove an order: the same.
- with a newer version signed in another browser, press "Open the newest version"; back: no
  question.
- no PIN yet: press Ondertekenen so the PIN dialog is open; reload: the browser asks.
- open the app anonymously (no launch), prescribe: the question; reload and confirm: the
  workbench is empty, as before.
- launch as `prescriber` in a fresh browser: the hop to `/authorize` shows no dialog.

## As built

| Step | PR | Notes |
|---|---|---|
| 1–5 | [#905](https://github.com/informedica/GenPRES/pull/905) | `UnsignedWorkPolicy.fs`, its tests (14), `PlanWork` on the app state, the listener in `View`, the user-guide line. Browser checks run by the maintainer: the dialog appears on back, reload and tab close with a medication on the workbench, after a dose stepped in a signed order and after an order removed; not after a signature nor after "Open the newest version". |

### Deviations from the text above

Two refinements from the review on #905, neither changing the approach:

- `PlanWork.Changed` carries a count of the changes, and the app keeps the work as it was when a
  signature was asked for. A signature is over the plan as it was at Sign; the plan's own
  buttons stay live while the challenge is fetched, so an order removed in that moment is not in
  the signature. The work becomes as signed only when it is still what the signature was asked
  over; a change made meanwhile stays.
- No patient, no plan: the plan is dropped when the patient goes (a Session closed, a patient
  cleared), so the work is as signed then too, with nothing left to ask about.

Left as is, deliberately: a change is counted when its command goes out, not when it lands, so
a change the server refused leaves the guard asking once more than needed until the next open
or signature. Counting at landing would leave a change in flight unguarded, and leaving the page
is what cancels it.

The first cut of #905 compared context ids against the version last signed; the review on the
plan found that a navigation within a signed order keeps its id, and the second commit replaced
the comparison with the command-told `PlanWork` this plan describes.
