# Implementation plan for issue 1665

## Problem description

Issue [#1665](https://github.com/informedica/GenPRES/issues/1665) asks for F# static analysis as a
blocking CI step. Its done condition is that the analysis blocks CI, every finding is fixed or
suppressed with a documented reason, and SW-TPL-01 describes the analysis.

GenPRES is IEC 62304 class C. Static analysis counts as unit verification evidence only when each
rule has a stated acceptance criterion, and the strength of that evidence depends on what the rule
protects. A layout rule and a rule against swallowed exceptions cannot share one criterion. This
plan sorts the coding rules into classes, gives each class its criterion and its enforcement, and
records the current number of findings for each rule.

## Approaches considered

- One criterion for every rule, with all existing findings in one baseline. This is the state of
  `scripts/CheckCodeStyle.fsx` today. It blocks new findings, but 1672 baseline entries without a
  reason do not meet the issue's own "documented reason".
- Zero findings for every rule before the gate goes on. This would block the gate behind about
  1700 edits that carry no safety weight.
- Classes by safety relevance, each with its own criterion. Chosen.

## Chosen approach

A rule is in one of three classes.

- **Class S (safety).** A violation can change a calculated value or a clinical text, turn a
  failure into a silent wrong result, or stop a clinical workflow with an unhandled exception.
  Acceptance criterion: no finding without a reason. A finding is kept only as an allowance entry
  that names the file, the symbol and the reason in words, in the form `scripts/CheckDependencyRule.fsx`
  already uses. An allowance that no longer matches fails the run. A new allowance is added only in
  a reviewed pull request that states it. These rules run over every library project, not only the
  three the issue starts with, because a core library inherits the behaviour of the helpers it
  calls.
- **Class M (maintainability).** A violation makes the code harder to read or change but does not
  change what it computes. Acceptance criterion: no new finding. The findings present at adoption
  are kept in one baseline under one stated reason: present at adoption, no effect on a calculated
  value or a clinical text. The baseline can only shrink.
- **Class R (review).** The rule needs judgement, so no tool checks it. It is verified by code
  review, and SW-TPL-01 names review, not static analysis, as its verification method.

The tool that detects a class S rule needs recorded validation evidence: tests with a case that
fires and a case that does not, and mutants of the detector that the tests kill. A detector that
misses a class S finding is a gap in the verification itself, so its evidence carries the same
weight as the rule.

## Confidence

Medium. The classes follow from what each rule protects, but the class S list should be checked
against the hazards in the risk management file, which is in the MDR documentation repository and
was not read for this plan. A hazard that names a cause not listed here adds a class S rule.

## Rules and current findings

The counts are at commit a3218b11 over the compiled files of each project (the Compile items of its
`.fsproj`), with whole-line comments removed. A grep count is a count of matching lines, so it
locates candidates and is not yet a count of violations; the detector that replaces it gives the
real count. The grep patterns are listed at the end of this plan. The DOC, BLANK and COMMENT
counts come from `dotnet fsi scripts/CheckCodeStyle.fsx --report`.

### Class S

| Rule | Detector | GenUNITS | GenSOLVER | GenORDER | Notes |
|---|---|---|---|---|---|
| S1 Compiler warnings are errors | compiler | 0 | 0 | 0 | No project sets `TreatWarningsAsErrors`. A `--no-incremental` build of GenORDER and its dependencies gave 0 warnings, so the switch costs nothing now. Covers incomplete matches (FS0025) and malformed XML documentation (FS3390) |
| S2 No `failwith` or `failwithf` | checker, syntactic | 0 | 0 | 0 | 0 in all of `src/` outside the scripts. The rule keeps it at zero |
| S3 A catch-all handler re-raises, raises a specific exception or returns `Error` | checker, syntactic | 0 | 3 | 11 | Read by hand: 5 convert to a specific exception or `Error`, 4 rethrow with `raise e`, 4 return a fallback value, 1 is in logging. The fallback sites are `Variable.fs:2399`, `Order.fs:1869`, `Order.fs:1960` and `OrderVariable.fs:135`; the last returns the value range unchanged when setting a limit fails, so a limit from a rule can be skipped without a trace. Each needs a fix or a stated reason |
| S4 No partial collection function without a guard | checker, syntactic | 13 | 9 | 15 | `List.head`, `List.find`, `List.reduce`, `List.max`, `Option.get` and their Seq, Array and Map forms. Many sit after a match that excludes the empty case; such a site gets an allowance that names the guard |
| S5 String comparison and case mapping are culture-invariant | checker, syntactic | 1 | 0 | 2 | The counts are calls to the Utils.Lib case helpers; the three libraries call no culture-sensitive BCL method directly. They call `String.toLower` and `String.toUpper` from Utils.Lib, which use `ToLower()` and `ToUpper()` under the current culture; GenUNITS uses `String.toLower` when it parses a unit (`ValueUnit.fs:1474`). One change in Utils.Lib fixes all callers. In all of `src/`, 23 calls to `StartsWith`, `EndsWith`, `IndexOf` or `LastIndexOf` pass no `StringComparison`: 18 in the server, 2 in Client.Core, 2 in Utils.Lib, 1 in Shared |
| S6 No `float` in a calculation library | checker, syntactic | 1 | 2 | 1 | None of the four is in a calculation: the number parser in GenUNITS, the sizing of parallel chunks in GenSOLVER, the type of a list of logged events in GenORDER (`OrderLogging.fs:135`). Each gets an allowance |
| S7 No downcast or `unbox` | checker, syntactic | 0 | 0 | 2 | Both in the JSON converter in `Dtos.fs` |
| S8 No `null` or `Unchecked.defaultof` outside a DTO | checker, syntactic | 0 | 0 | 49 lines | 45 in `Medication.fs` and 4 in `Dtos.fs`. The hits sampled in `Medication.fs` are in its DTO classes, which the coding rules allow; not every line was read. The allowance is per DTO module |
| S9 No IO in a top-level `let` value | `CheckDependencyRule.fsx` for the core; checker for the rest | covered | covered | covered | The core projects reach no IO at all, which the dependency rule already enforces. The infrastructure projects need the syntactic rule |
| S10 The source parses under every `#if` combination | checker (PARSE-001) | 0 | 0 | 0 | Never baselined |

### Class M

| Rule | Detector | Findings in the three libraries | Notes |
|---|---|---|---|
| BLANK-001 Two blank lines between declarations | checker | 311 | 235 with one blank line, 76 with none |
| DOC-001 A `///` comment on every public symbol | checker | 1341 | Also supports the detailed design of units (IEC 62304 5.4.2 and 5.4.3) once #1657 defines the units. After #1656 the interface of a unit is its `.fsi` file, which the checker does not yet read |
| DOC-002 to DOC-004 Plain or XML `///` blocks, well formed | checker | 0 | |
| COMMENT-001 No `(* ... *)` | checker | 0 | |
| COMMENT-002 Comment lines at most 120 columns | checker | 20 | |
| Format specifier in every interpolation hole | checker, syntactic | not measured at this commit | The earlier G-Research run counted 932 in the libraries and the server, at an earlier commit with the client excluded |
| `reraise ()`, not `raise e`, to rethrow | checker, syntactic | 4 | `raise e` loses the stack trace of the original exception |
| `[<RequireQualifiedAccess>]` on DUs and modules | checker, syntactic | not measured | |
| `[<Interface>]` on interfaces | checker, syntactic | not measured | |
| Indexer without the leading dot | checker, syntactic | not measured | |
| `and` only for mutually recursive types | checker, syntactic | not measured | |
| Unused bindings (compiler warning 1182) | compiler, opt-in | not measured | On in ZForm.Lib only |

### Class R

Single responsibility and small functions; a DU rather than a boolean flag; names; comment wording
(plain complete English, no coined terms, no issue numbers); types before logic and illegal states
unrepresentable; `[<NoEquality>]` and `[<NoComparison>]` on aggregates; smart constructors; units
of measure on physical quantities; type annotations only where inference needs them (a typed rule
would need the full compiler, and the stock G-Research rule contradicts the house rule); `private`
only for what is harmful to expose; test names that state the behaviour.

## Steps

1. Review and settle the classes in this plan, including the class S list against the hazards in
   the risk management file.
2. Turn compiler warnings into errors for the library projects (S1). This changes project files only.
3. Fix S5 at its source: `String.toLower` and `String.toUpper` in Utils.Lib use the invariant
   culture. This changes behaviour only for a server whose culture maps case differently from the
   invariant culture. Script first, as the policy requires.
4. Add the class S detectors S2 to S8 to the checker, each with its allowance list and a reason per
   entry, and decide the four fallback handlers of S3 one by one.
5. Move the checker from a script to a compiled project with its tests under `tests/`, so that its
   validation evidence is in the test results CI keeps. Add `.fsi` files to its input.
6. Keep the class M rules on the baseline, and add the not-yet-measured syntactic rules one at a
   time, each with its own count.
7. Transfer the method and the criteria to SW-TPL-01. The draft text is below.

## Draft text for SW-TPL-01

This text describes static analysis as a method of unit verification. It was written without the
current SW-TPL-01 or SW-TPL-11, so its place in them, and whether SW-TPL-11 needs its own text, are
open.

> Static analysis checks each software unit against the coding rules in the repository. Each rule
> is in one of three classes. A safety rule (class S) protects a calculated value, a clinical text
> or the handling of a failure; its acceptance criterion is no finding without a reason, and each
> accepted finding is listed with its location and its reason, reviewed in the pull request that
> adds it. A maintainability rule (class M) has the acceptance criterion of no new finding; the
> findings present when the rule was adopted are listed in a baseline that can only shrink. A
> review rule (class R) needs judgement and is verified by code review. The compiler runs with
> warnings as errors. The analysis runs on every pull request on Linux, Windows and macOS, and a
> failed check blocks the merge. The tool that performs the analysis is validated by its own
> tests, which include a case that fires and a case that does not for each rule; its results are
> kept with the test results of the build.

## Grep patterns for the class S counts

Each pattern was run with `grep -cE` over the Compile items of a project after
`grep -vE '^\s*//'` removed the whole-line comments.

| Rule | Pattern |
|---|---|
| S2 | `\bfailwithf?\b` |
| S3 | `with\s*(_\|e\|ex\|exn\|err)\s*->` |
| S4 | `\b(List\|Seq\|Array)\.(head\|last\|find\|item\|exactlyOne\|reduce\|pick)\b\|\bMap\.find\b\|\bOption\.get\b`, plus `\b(List\|Seq\|Array)\.(max\|min\|maxBy\|minBy\|average)\b` |
| S5 | `\.To(Upper\|Lower)\(\)\|String\.to(Upper\|Lower)\b`; for all of `src/`, `\.(StartsWith\|EndsWith\|IndexOf\|LastIndexOf)\(` on lines without `StringComparison` |
| S6 | `\b(float\|double\|float32)\b` |
| S7 | `:\?>\|\bunbox\b` |
| S8 | `Unchecked\.defaultof\|\bnull\b` |
