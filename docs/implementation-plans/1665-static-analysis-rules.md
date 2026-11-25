# Implementation plan for issue 1665

## Problem description

Issue [#1665](https://github.com/informedica/GenPRES/issues/1665) asks for static analysis as a
blocking CI step, because the MDR documentation names static analysis as a unit verification method
(IEC 62304 5.5.2 and 5.5.3) and SW-TPL-01 states that GenPRES uses no static analyser. Two kinds of
rule in `.github/instructions/fsharp-coding.instructions.md` are checked only by review, because
Fantomas does not enforce them: blank lines between declarations, and comment style.

## Decision so far

The maintainer agreed to start with GenUNITS and work upward through the libraries that change less
often first. This plan follows that, and takes the smallest step that makes the gate real.

## First step: GenUNITS

- A checker script, `scripts/CheckCodeStyle.fsx`, in the style of `scripts/CheckDependencyRule.fsx`. It
  reads the untyped syntax tree with Fantomas.FCS, so it needs no build and has no dependency on the
  compiler version of the .NET SDK, which an analyzer built against the SDK would have.
- The rules are BLANK-001 (two blank lines between declarations), DOC-001 (a `///` comment on every
  public symbol), DOC-002 to DOC-004 (a `///` block is plain prose or well-formed XML), COMMENT-001
  (no `(* ... *)`) and COMMENT-002 (comment lines at most 120 columns).
- The rules run over GenUNITS only.
- The findings present at adoption are kept in `scripts/CheckCodeStyle.baseline.tsv`: 453 findings,
  263 DOC-001, 185 BLANK-001 and 5 COMMENT-002. The reason for all of them is the same: they were
  present when the rule was adopted, and none changes a calculated value or a clinical text. The
  baseline can only shrink. A new finding fails the build, and so does a baseline entry that no
  longer matches a finding.
- The step changes no file under `src/`. The checker validates itself: `dotnet fsi scripts/CheckCodeStyle.fsx --test`
  runs its tests, which have a case that fires and a case that passes for each rule, and the CI step
  runs them before it checks the source.

## Second step: GenSOLVER

GenSOLVER is added to the project list of the checker. The rules are unchanged, and no file under
`src/` or `tests/` changes. The only change to the checker besides the project list is the baseline
header it writes, which now matches the header of the file. GenSOLVER has 209 findings: 154 DOC-001,
43 BLANK-001 and 12 COMMENT-002. They are added to `scripts/CheckCodeStyle.baseline.tsv` for the same
reason as the GenUNITS entries, which are untouched: the baseline now holds 662 entries, 453 for
GenUNITS and 209 for GenSOLVER. The header names both libraries and allows new lines only when a
library joins the check. GenORDER is the next library to join.

## Not in these steps

- GenORDER. It is added to the project list in the checker, with its baseline, in its own step.
- Rules on the safety of a calculation, such as `failwith`, catch-all exception handlers that return
  a fallback value, partial collection functions and culture-sensitive string functions. These need a
  decision per finding and should be checked against the hazards in the risk management file, which
  was not read for this plan.
- Treating compiler warnings as errors. No project sets `TreatWarningsAsErrors` today.
- Moving the checker from a script to a compiled project with tests under `tests/`.
- The text of SW-TPL-01 and SW-TPL-11, which lives in the MDR documentation repository. It should
  describe this check as a layout and comment check on GenUNITS and GenSOLVER, and add each library as
  it joins.

## Open questions

- Whether the 662 baseline entries are fixed before more libraries join, or stay in the baseline.
  Fixing the DOC-001 entries means writing comments, which the AGENTS.md policy allows without a
  script; the BLANK-001 entries are whitespace edits to `.fs` files, which the policy does not list.
