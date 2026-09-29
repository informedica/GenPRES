# Implementation plan for issues #1191 and #1138

Two layout bugs that move or misalign what the user reads: the order plan table jumps when a row
is checked, and the totals bar is narrower than the page above it. Client rendering only.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)
- [As built](#as-built)

## Problem description

- **#1191** Above the order plan table (`Views/OrderPlan.fs`) the sign button sits on a row of its
  own. `deleteBtn` renders `null` until a row is checked, then a `Box` with `marginTop = 2` and a
  full-width button, "Verwijder Geselecteerde Voorschriften". The bars above the table change
  height with the selection, and the table, which takes the remaining height, moves with them: the
  row under the cursor jumps and the next click can land on the wrong row.
- **#1138** `Views/Totals.fs` lays the three totals cards out in a `Stack` with
  `justifyContent = "center"`. Each card takes its own width, so the row is as wide as its three
  tables and sits centred in the container, while the grid above it fills the container. The
  containers in `Pages/GenPres.fs` are already the same width.

## Decisions

Taken 2026-09-29 by the maintainer.

| Question | Decision |
|---|---|
| Where the delete button goes (#1191) | On the left, set apart, with the sign button on the right: the action bar convention, not the order the issue asks for. |
| Delete without a selection (#1191) | Shown and disabled, not hidden. |

## Approaches considered

For the buttons (#1191):

- **A hand-built flex row, delete to the right of sign**, as the issue proposes. Places the
  buttons by hand, against the convention the shared action bar already applies.
- **`Components.ActionBar.View` with the sign action as `Primary` and the delete action as
  `Destructive`.** The bar puts the destructive action on the left, set apart, and the primary
  action on the right, each bounded, never full width. It renders the same whether or not the sign
  action is there, so the row keeps its height. Chosen: this is the convention of ADR-0009 that
  G0 built the bar for, and it is the one deviation from the issue text, which asks for delete to
  the right of sign.

For the totals (#1138):

- **`justifyContent = "space-between"`** aligns the outer edges but leaves the gaps uneven when the
  tables differ in width.
- **Each card grows, `flex = 1` with `minWidth = 0`.** The cards share the row, the stack spacing
  stays the gap, and a long value wraps rather than pushing the row past the container. Chosen.

## Chosen approach

### One action row above the plan (#1191)

`deleteBtn` and `signBtn` become two `ActionBar.Action` values in one `ActionBar.View`:

| Action | Kind | Disabled when | Present when |
|---|---|---|---|
| Sign | `Primary` | `signRests`, as today | `SigningPolicy.canSign session.Session tp`, as today |
| Delete | `Destructive` | nothing is checked (`tp.Filtered` empty), or `isRecalculating` | the plan holds a plan (`Settled` or `Changing`) |

- The delete action is always there while there is a plan, disabled without a selection, so the
  row has the same height checked or not. The sign button already stays, disabled, while the plan
  changes, for the same reason.
- Its label goes through `Terms.Delete`, which the confirm dialog already uses; the Dutch literal
  goes.
- `onDelete` (`unit -> unit`) is already the shape `Action.onClick` takes; `onSign` gets a
  `unit -> unit` wrapper.
- `movedOnBar` stays above the row: it is a notice, not an action. The bar's own `marginTop`
  replaces the two `Box`es with `marginTop = 2`.
- With no plan (`NoPatient`) the row is not rendered, as today.

### The totals fill the row (#1138)

In `Totals.fs` the stack loses `justifyContent = "center"`, and each card is wrapped in a `Box`
with `flex = 1; minWidth = 0`. Nothing changes in `GenPres.fs`; the bar stays hidden below
1200 px.

## Confidence

High. Both steps are rendering only, and the action bar is in use on prescribe, nutrition and the
order dialog.

## Steps

1. **#1191, the action row.** `Views/OrderPlan.fs`: the two actions in one `ActionBar.View`, the
   delete action always present with a plan, the label through `Terms.Delete`. Named `let`
   bindings for the actions and their handlers, none inline in the JSX string.
2. **#1138, the totals width.** `Views/Totals.fs`: cards grow to fill the row.

One pull request per step, each committed locally and pushed after a check in the browser.

## Verification

- Fable compiles; inspect the generated JSX for the new row, and `npx vite build` passes.
- Order plan: checking and unchecking rows does not move the table; delete is disabled without a
  selection and while the plan recalculates; the confirm dialog and the removal work as before;
  without a Prescriber or open Session the row still holds the delete action in the same place;
  at mobile width the buttons wrap without the table moving on a selection change.
- Totals on prescribe, nutrition and order plan, side menu open and closed: first and last card
  on the edges of the page content; hidden below 1200 px.

## As built

| Step | Pull request | Note |
|---|---|---|
| 1, the action row | #1196 | As planned. |
| 2, the totals width | #1198 | A child selector on the stack, `& > *` with `flex: 1; minWidth: 0`, in place of a wrapper around each card. |
