# Implementation plan for issue #982

G1 of the ten UI/UX groups: the prescribing page's fields, the cascade behind them, and the
control that resets them. Companion to [the grouping index](ux-issue-grouping.md) and to
[the foundation plan](981-ux-foundation-and-common-components.md), whose components this group
is the first to spend.

Held to [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md): a choice with one possible
value is made rather than asked, and an action that discards is not the one the hand lands on.

- [Problem description](#problem-description)
- [What the foundation already settled](#what-the-foundation-already-settled)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [To settle in review](#to-settle-in-review)
- [Related, not a member](#related-not-a-member)

## Problem description

Five reported issues, all on the fields a prescription is built from.

- **#498** A dropdown with a single option stays open-able and shows a cross that clears
  nothing, so the user clicks twice to learn there was never a choice.
- **#398** On a dose field the cross does nothing at all.
- **#403** and **#501** A medication, an indication, a route or a form cannot be changed by
  picking another one. The user has to clear the field first, and only then pick. Comparing two
  medications, which is what a prescriber does, costs a clear and a rebuild each way.
- **#394** The control that resets the prescribing filter is a full-width text button, hit by
  accident and hard to find on purpose, and it is named *Verwijder* where it resets.

## What the foundation already settled

Four of the five were partly answered by the components of #981, so what is left is narrower
than the umbrella issue states.

- `PickPolicy` and `PickField` decide what a pick field offers: nothing to choose means
  disabled and empty, one option means shown chosen and disabled, more means enabled when the
  page says so. Every field built by `ViewHelpers.filterSelect` is on it, which is #498 for the
  indication, the generic, the route, the form and the dose type.
- `SimpleSelect` clears whether or not a stepper stands beside the field, so the adornment
  conflict behind #398 is gone as a component question.
- `ActionBar` carries the button convention #394 asks for, and `QuantityField` holds the dose
  field #398 is about.

What remains of the five is four things.

1. **The cascade.** In the `OrderContext` module of `Shared/Models.fs`, `indicationChange`,
   `medicationChange`, `routeChange`, `formChange` and `doseTypeChange` clear the fields below
   them when the user clears a field, and write nothing but the field itself when the user
   picks a value. So after picking another generic the route, the form and the dose type still
   hold the previous medication's answers. The server then looks for rules matching the whole
   filter, finds none, and answers that no dose rules were found for the selected filter.
   Clearing first works because clearing is the branch that resets. That is #403 and #501.
2. **The cross on a dose field.** Whether a dose field offers one is an argument passed at each
   call site in `Views/Order.fs`: false for the dose quantity, true for the adjusted dose and
   the dose per time, with no rule behind the difference. Where it is passed, it works: clearing
   sets the variable back to unnarrowed and the solver picks again. That is #398.
3. **The reset.** `Views/Prescribe.fs` draws a full-width text button that dispatches an empty
   order context. That is #394.
4. **The autocomplete where it is a pick.** `ViewHelpers.autoComplete` disables on an empty
   list but not on a list of one, so the one-option rule holds for the pick fields and not for
   the fields that type instead of scroll: the indication and the generic on a narrow screen,
   and the formulary and parenteralia pages. That is the remainder of #498. The interactions
   page calls the autocomplete component directly and is a different control: it never shows a
   choice, it adds the drug that was typed to a list and empties itself. The pick rule must not
   reach it, or a search that matched one drug would show that drug chosen and refuse to add it.

## Approaches considered

**Where the cascade rule lives.**

- *In the views.* Each change handler clears the fields below before dispatching. Rejected: five
  handlers in two views, the rule written twice and provable nowhere.
- *In the client's state machine.* Rejected: the machine carries a context to the server and
  back; what a filter means is not its subject.
- *In the cascade functions of `Shared/Models.fs`,* beside the clearing branch that already does
  exactly this. **Chosen.** One place, one rule per field, and `Shared.Tests` already reaches it.
- *In the server.* Rejected: the server would be guessing which of the user's picks to discard,
  and until the reply arrives the page would still show values the user did not choose.

**The cross on a dose field.** The issue offers two: remove it and say the reset below is the
way, or make it do what the reset does. The second is refused under the first design rule, since
a small cross beside one field would discard every change in the dialog. Between removing it and
ruling it, ruling it is chosen: the cross clears its own field, which is how a user undoes one
narrowing without losing the rest, and the dialog's Reset stays the way to discard them all.

**The reset control.** A bounded outlined button on the left of an action bar, as the ADR
decides, and no confirmation before it. A confirmation would cost a click every time to protect
work that is neither signed nor lost: the filter is rebuilt by picking again. The umbrella issue
lists `ConfirmDialog` among what this group spends; on this reading it does not spend it.

## Chosen approach

- A field that is given a new value clears the fields below it in the chain and the scenarios,
  exactly as clearing that field does today. The chain is indication, generic, route, form, dose
  type. The rule is written once per field in the `OrderContext` module of `Shared/Models.fs`
  and proved in `Shared.Tests`, and the two views keep calling what they already call.
- A dose field offers the cross when it has a value and can be used, decided in
  `ViewHelpers.orderSelect` rather than passed by each call site, and the argument that carried
  it goes.
- The prescribing page's reset becomes a bounded secondary action on an `ActionBar`, named
  *Reset*.
- A field that types instead of scrolls answers the same pick rule as one that scrolls. The rule
  and the effect that tells the page its single option stay in the one component that holds
  them, which gains the shape it is drawn in; the plain autocomplete component stays as it is
  for the interactions page, whose control adds rather than picks.

## Confidence

Medium-high. The cascade is the one piece that changes what the server is asked, and it is the
piece that is provable: each rule is an Expecto test over a context record, and the script comes
first. The other three are visual and go to the browser.

The risk is in the cascade's blast radius. The same functions serve the nutrition page, where a
category's indication and dose type behave differently from a drug's, so the tests carry a
nutrition context as well as a drug one.

## Steps

One pull request each, in this order.

1. **The cascade rule, as a script.** `src/Informedica.GenPRES.Shared/Scripts/` gets a script
   that shadows the `OrderContext` module, states the rule per field and proves it with Expecto:
   picking a generic clears the route, the form, the dose type and the scenarios; picking a
   route clears the form, the dose type and the scenarios; picking a dose type clears the
   scenarios alone; clearing keeps doing what it does; a nutrition context behaves as a drug
   context does. Script only, no shipped code.
2. **The cascade migrated.** The rule moves into the `OrderContext` module of
   `Shared/Models.fs` and its tests into `tests/Informedica.GenPRES.Shared.Tests/ModelsTests.fs`;
   the script goes. The two views are untouched: they already call these functions.
   Landed: a change to a choice empties the choices below it in the order its page offers them,
   writes the change, and drops the scenarios; nothing at all happens when the field already
   holds what it is given. Clearing a field empties that field too, options and all, so a list
   of one cannot choose itself again the moment the user empties it. The order is the order
   category's, since the two pages do not offer the same one: a drug is found by indication and
   then by medication, a nutrition composition is picked first and its indication follows, which
   the nutrition page used to patch by hand after every composition change. When no choice is
   left anywhere, no options are kept either, so nothing stays narrowed by a filter that is
   gone.
3. **The reset, on all three of the pages that offer one.** The word is localized rather than
   written at the call site: the control is spelled out in three places today and translated in
   none, the prescribing page saying what a delete says and the dose dialog and the nutrition
   page saying an English word whatever language is being read. One term for the act, with no
   area before it, as `Delete` and the term for OK are. Two pull requests, since the terms are
   not client UI: the case and its row drafted in the localization script, then the case, the
   row and the three controls.

   The three, named, and what becomes of each:

   - `Views/Prescribe.fs`, which resets the filter being built: a full-width text button reading
     the delete term. It becomes a bounded secondary action on an `ActionBar` reading the reset
     term. This is #394.
   - `Views/Order.fs`, the dose dialog, which discards the changes to a scenario: already a
     bounded secondary action on an `ActionBar`, with the word written in English at the call
     site. The word becomes the term; nothing else changes.
   - `Views/Nutrition.fs`, which discards the changes to a nutrition order: an outlined button
     with the word written in English, stretched by the column it sits in, so it is as wide as
     the panel and invites the same accident #394 reports. It becomes a bounded secondary action
     on an `ActionBar` reading the term.

   Nothing is deferred: leaving one of the three would put the same word on the same act in two
   languages at once.

   Landed: one term beside the delete term, and the three controls on it. The prescribing page
   and the nutrition page each draw their reset as a lone secondary action on an action bar, so
   it is as wide as its word and stands to the left; the dose dialog keeps the bar it had. The
   sheet the terms are read from is not in the repository, so the row is added there by hand and
   the English word stands until it is.
4. **One rule for the cross on a dose field.** `ViewHelpers.orderSelect` decides it from the
   field's own state; the argument and the flags at the call sites in `Views/Order.fs` and
   `Views/Nutrition.fs` go. Landed: a field offers the cross when it can be used and holds a
   value, so the eighteen fields of the dose dialog and the nutrition page's controls answer one
   rule instead of a flag each. Clearing puts that one value back to unnarrowed and the solver
   picks again, which is how a single narrowing is undone; the dialog's reset is still the way
   to discard them all. Two kinds of field, not one: a value that can be put back, and a field
   there is nothing to clear in, which is the choice of a component or a substance, of which the
   dialog always holds one, and a value that is only shown. The second kind never offers the
   cross, since a cross there would say the value can be taken away when it cannot.
5. **The pick rule where the field types instead of scrolls.** `Components/PickField.fs` takes
   the shape it is drawn in, a list to scroll or a box to type in, so that one component holds
   the rule, the single option and the effect that tells the page that option is chosen. A
   single option that is only shown and never told would leave the filter empty and the fields
   below it shut, which is the failure this step exists to avoid, so the check is that the next
   field opens. `ViewHelpers.autoComplete` builds that component in its typing shape and stops
   deciding anything itself; its four callers keep their call.

   `Components/Autocomplete.fs` stays what it is, and the interactions page keeps calling it
   directly: that control adds a drug to a list and holds no choice, so the pick rule would
   disable it exactly when it matched one drug.

   Landed: the pick field takes the shape it is drawn in, a list to scroll or a box to type in,
   and the rule, the single option and the effect that tells the page are the same for both. The
   fields that type are the indication and the generic on a narrow screen and the four of the
   formulary and the three of the parenteralia page; each of them now shuts on a single option
   and tells the page that option, where they stayed open and said nothing. The interactions
   page is untouched.

## Verification, per step

- `dotnet run Build`, and `dotnet run ServerTests` for the steps that touch `GenPRES.Shared`.
- After a client change, run Fable and read the generated JSX: the structure, not only that it
  compiles.
- By hand in the browser for everything visual, since no browser harness exists (#598). For the
  cascade the check is the one the issues describe: pick a medication, build the filter out, then
  pick another medication from the dropdown without clearing, and see the dose appear rather than
  the message that no dose rules were found.

## To settle in review

- Whether a change that clears the fields below should also empty their option lists. Decided
  for now that it should: a list narrowed by a choice that is gone offers a smaller world than
  there is and says nothing about it, and a pick from it builds a filter no rule matches. The
  cost is that those fields stand empty for the length of one request.
- Whether a dose field whose single value the solver determined, rather than the user, should
  offer the cross at all.
- The word on the reset, which is a terminology decision and not this group's alone.

## What the group came to

All five issues are answered. The one-option rule reaches every field that picks, whether it
scrolls or types. The cross on a dose field follows one rule instead of a flag per call site,
and is not offered where there is nothing to clear. A field can be changed by picking in it,
in the order its own page offers its choices. The reset is one word in six languages on a
bounded button, in the three places that offer one.

## Related, not a member

- **#598** No test harness for the client's rendering, which is why four of the five steps end
  in the browser rather than in a test.
- **#977** With the cascade fixed, the message that no dose rules were found becomes rare enough
  to be a real finding; it is that issue that decides how the page says so.
- **#400** and **#503** The same fields, searched rather than picked, in G2.
