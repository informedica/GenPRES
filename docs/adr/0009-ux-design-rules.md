# ADR-0009: UX Design Rules

**Date**: 2026-09-24

**Status**: Accepted, amended (2026-09-26); § 4 removed

**Related Issues**: [#979 — Record the three UX design rules as an ADR](https://github.com/informedica/GenPRES/issues/979)

**Related Discussions**: [#477 — UX design: 3 basic rules](https://github.com/informedica/GenPRES/discussions/477),
[#406 — UI and UX design](https://github.com/informedica/GenPRES/discussions/406)

## Context

GenPRES has three rules that every UX decision is held to. They were written in GitHub
discussion [#477](https://github.com/informedica/GenPRES/discussions/477) on 2026-08-21 and have
been unanswered since. Nothing in this repository states them: no pull request can cite them, no
implementation plan can trace a design choice to them, and no review can point at them. A
contributor who has not read that discussion does not know they exist.

Some forty open UI and UX issues — the twenty-nine still open of the thirty-two the UX designer
filed, plus thirteen from the maintainer and the clinical tester — are about to be planned in
groups rather than one plan per reported symptom, and every one of those plans needs the rules as
its first reference. Four of the groups have already had to settle a design question before their
plan could be written (a button convention, an empty-result behavior, who decides a dialog's field
order, what a launched patient's panel shows), and each was settled by applying these rules. The
rules therefore have to be citable before the plans are written, not after.

The rules are a decision, not a description. They are **ranked**: efficiency may not be bought by
giving up safety, and following the user may not be bought by giving up either. A ranking that is
only recited cannot be applied when two rules pull in opposite directions, which is when it
matters. And reversing one of them after the interface is built means rebuilding the interface,
not editing a page. That is what an ADR records.

## Decision

### 1. The three rules, in rank order

1. **Safe by default.** The interface always offers a safe default the user can take. The user
   may deviate; deviating is an explicit act that moves away from the default.
2. **Efficient.** The interaction needed to reach a goal is the least that will do. Where a smart
   or quick choice exists, the interface shows it or makes it possible. This may not violate
   rule 1.
3. **The UX follows the user, not the other way around.** The user has as much control as
   possible over the path taken to a goal. This may not violate rules 1 and 2.

The numbers are the ranking. A lower-numbered rule wins; the three are not weighed against each
other case by case.

The wording above is the discussion's, with one correction: rule 3 there reads "this should not
violate rule 2 and 3", which would have the rule constrain itself. It is read as rules 1 and 2.

### 2. What each rule means on a screen

**Safe by default.** What the screen already holds when the user arrives is a value the rules
allow: a dose the constraint solver computed, a route the dose rule covers, a patient value read
from the platform. Nothing the user has to correct before it is safe. Deviating is a visible,
deliberate act — a field the user changes, a confirmation the user answers — never a side effect
of arriving on a page or of moving between fields. A value the rules do not allow is marked with
its reason, not merely colored.

**Efficient.** The path to a goal has as few interactions as it can and still be explicit. A
choice with one possible value is made, not asked. A value the server can compute is computed, not
typed. A field the user will need next is the one under the cursor. Work already done is not lost
to a page change, a re-pick or a reload.

**The UX follows the user.** The order in which a user reaches a goal is theirs. The interface
does not move the user, does not take the page away, and does not decide for a whole site what a
site can decide for itself. Where a habit differs between settings — which field a clinician
reaches for first — the difference is configuration, not code.

### 3. When two rules conflict

The ranking decides, and what the losing rule gives up is stated rather than hidden. Three shapes
recur:

- **Safety over efficiency.** An interaction that could be saved is kept because dropping it would
  let an unsafe value through unremarked. The cost is a click, and it is paid: a deviation stays
  an explicit act even when the system is confident which deviation the user wants.
- **Safety over control.** A path the user would like to take is not offered because it ends in an
  order nobody can check. The cost is a refusal, and it is paid with its reason: what is missing
  and whom to tell, on the page the user is on.
- **Efficiency over control.** Where the user's freedom to choose a path would mean rebuilding
  work already done, the shorter path wins and the user keeps their place. The cost is a choice
  the interface makes on the user's behalf; it is made once, visibly, and where it differs between
  sites it is configuration rather than a client decision.

Losing does not mean being ignored. A rule that loses is honored in whatever is left: the refusal
carries its reason (control), the kept interaction is a single explicit act rather than a form
(efficiency), the made choice is one a site can change (control).

### 4. Decisions taken under the rules — amended 2026-09-26

This section recorded four decisions taken under the rules, as worked applications: the button
convention, the in-place reason when no dose can be shown, the dose dialog's field list as
configuration, and the two patient modes. It is removed. An ADR states the rules; a decision
taken under them is a domain decision, and it is recorded where it is taken and can change
without touching the rules: in its issue, in the plan that builds it, and in that plan's
as-built section once built. The four live in
[#404](https://github.com/informedica/GenPRES/issues/404) and
[#394](https://github.com/informedica/GenPRES/issues/394),
[#977](https://github.com/informedica/GenPRES/issues/977),
[#978](https://github.com/informedica/GenPRES/issues/978) and
[#976](https://github.com/informedica/GenPRES/issues/976), and in
[the grouping index](../implementation-plans/ux-issue-grouping.md).

## Consequences

- An implementation plan for a UI or UX group opens by citing this ADR, and names which rule each
  design choice serves. A choice that serves none is a choice nobody has to accept.
- A reviewer can reject a design in a pull request by pointing at a rule here instead of arguing
  from taste, and an author can defend one the same way.
- A design that has to break a rule says so, names the rule and says what is given up. It is not
  silently inconsistent with the other screens.
- A decision taken under the rules is not recorded here. It is recorded in its issue and in the
  plan that builds it, which cites the rule it serves by number; this ADR changes only when a
  rule does.
- Discussion [#477](https://github.com/informedica/GenPRES/discussions/477) is closed with a link
  to this ADR. The wider UI and UX thread
  ([#406](https://github.com/informedica/GenPRES/discussions/406)) stays open as a discussion.
- Reversing a rule means rebuilding the interface, which is the reason this is an ADR and not a
  page; an amendment to the ranking is expected to be at least as expensive to apply as the
  original.

## Alternatives considered

- **Leave the rules in discussion #477.** Rejected: a discussion is not where a decision is
  recorded, it cannot be pointed at from a pull request as a rule in force, and the discussion in
  question had been unanswered for a month while the interface it governs was being built.
- **A page under `docs/user-guide/` or `docs/roadmap/`.** Rejected: the guide describes the
  interface to its users and the roadmap holds what has not been decided yet; neither is a record
  of a decision, and the folder table in [ADR-0000](0000-documentation-rules.md) has no row that
  fits. A reviewer does not point at a guide when a pull request violates a rule.
- **A design-system document.** Rejected for now, and not for good: a design system records
  components, spacing and color — what the interface is made of — and would be the right home for
  the catalogue of shared components the groups build. It is not the home for the ranking those
  components are built under. If a design system is written, it cites this ADR rather than
  replacing it.
- **Write the rules into `CONTRIBUTING.md`.** Rejected for the reason
  [ADR-0000](0000-documentation-rules.md) gives for its own rules: how the project decides is
  itself a decision, and it belongs with the decisions.
- **Record the applications here beside the ranking.** The first version did, in a section 4 of
  four worked decisions, so that the rules could be read through their consequences. Reversed on
  2026-09-26: a decision under the rules is a domain decision, and holding it here made the ADR
  the place to amend when the decision moved, which is the plan's and the issue's job. The
  plans show the rules being applied, each citing the rule it serves.

## References

- [ADR-0000: Documentation Rules](0000-documentation-rules.md) — why this is an ADR and not a page,
  and the form it takes.
- [ADR-0001: System Architecture](0001-system-architecture.md) — the client is outside the DMZ and
  computes no advice of its own, which is why a design choice such as the dose dialog's field list
  is the server's to send.
- Discussion [#477 — UX design: 3 basic rules](https://github.com/informedica/GenPRES/discussions/477),
  the source of the three rules.
- Discussion [#406 — UI and UX design](https://github.com/informedica/GenPRES/discussions/406),
  the wider design thread.
