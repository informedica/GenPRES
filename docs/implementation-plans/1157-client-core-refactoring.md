# Implementation plan for issue #1157

The refactorings of `src/Informedica.GenPRES.Client.Core` that #1157 collects, together with
one it does not list yet: moving the patient panel's edit rules from the view into
`Client.Core`, where #1152 prototyped them in a script.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [What the code does today](#what-the-code-does-today)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Left out](#left-out)
- [Verification](#verification)
- [As built](#as-built)

## Problem description

`Client.Core` holds the client's pure state machines and policies: plain F# over the shared
contract, tested under Expecto and compiled by Fable into the client. Four things make it
harder to read and trust than it should be:

1. **One rule lives outside it, untested.** The patient panel decides what an edit does to the
   draft and when the weight and height are estimated again. That rule decides what the doses
   rest on after a weight is cleared. It lives in `Views/Patient.fs`, where no test reaches it;
   its only tests run against a copy in `Client.Core/Scripts/PatientPanel.fsx`, which can drift
   from the view.
2. **File names do not say what a file is.** `SeverityReason.fs`, `QuantityMode.fs` and
   `SectionFold.fs` are decision modules like the `*Policy` files, but not named so.
3. **The compile order does not show the kinds.** The project lists machines, policies and the
   rest interleaved, in the order they were added, so the file list says nothing about how the
   project is built up.
4. **The comments say too much or too little.** Twelve module comments repeat that the file is
   pure F# without React; several module and type comments do not say in one sentence what the
   module or type is; `SessionView`, `SessionPhase` and `SessionState` read as duplicates.

## Decisions

Taken 2026-09-28 by the maintainer.

| Question | Decision |
|---|---|
| The name of the migrated module | `PatientDraftPolicy`: it decides the draft after an edit and when the estimates are renewed. |
| `canCalculate` | Moves along; the view keeps only rendering. |
| `FilterSync.fs` | Stays as it is, with `Deferred.fs` among the rest: it keeps two contract records in step and decides nothing. |
| The text helpers of the session gate | `fill` and `sentences` move to a module of their own, `TermText`. `english` stays in `SessionGatePolicy`: it is the gate's own terms. |
| The scripts whose code and tests live in `Client.Core` | Deleted: `PatientPanel.fsx`, `Argumentation.fsx`, `ArgumentationReset.fsx`, `OrderContextRefusal.fsx`, `OrderContextRefusalPolicy.fsx`. |
| Issue #1157 | Stays as written; this plan records the items it does not list. |

## What the code does today

### The panel's edit rules

`Views/Patient.fs`, inside `module private Elmish`, holds six pure pieces over the shared
`Patient` type:

| Piece | What it does |
|---|---|
| `Msg` | the fifteen edits the panel makes |
| `setDepartment` | the department chosen, or none |
| `update` | the draft after one edit, through the `Patient` setters of `Shared/Models.fs` |
| `Estimates` (`Renewed` / `Kept`) and `estimates` | which edits estimate the weight and height again: the age parts, the gender, the gestational age and Clear |
| `keepEstimates` | after any other edit, the weight and height the edit does not set, copied back with their estimates |
| `canCalculate` | whether the draft meets the minimum for a patient |

`show`, which builds the summary as Markdown, is rendering and stays in the view. The dispatch
that sends the result to `UpdatePatient` or `EditPatient`, the held guard and the open state
stay in the view too.

`Client.Core/Scripts/PatientPanel.fsx` holds the same module, without `canCalculate`, and 40
Expecto tests.

### The three decision modules

| Module | Used by |
|---|---|
| `SeverityReason` | `ArgumentationPolicy.fs`, `Views/ViewHelpers.fs`, `SeverityReasonTests.fs`, `Scripts/load.fsx`, `Scripts/Argumentation.fsx`, `Scripts/ArgumentationReset.fsx` |
| `QuantityMode` | `Views/ViewHelpers.fs`, `Views/Order.fs`, `Views/Nutrition.fs`, `QuantityModeTests.fs`, `Scripts/load.fsx` |
| `SectionFold` | `Views/Order.fs`, `SectionFoldTests.fs`, `Scripts/load.fsx` |

`FilterSync.fs` is the one other file that is neither a machine nor a policy; #1157 does not
name it (see the open questions).

### The compile order

Today's order in both project files, with what each file needs (found by the module names and
the opened modules in the code, comments left out):

| File | Kind | Needs |
|---|---|---|
| `Deferred.fs` | other | |
| `LanguagePolicy.fs` | policy | |
| `PickPolicy.fs` | policy | |
| `SeverityReason.fs` | policy | |
| `ArgumentationPolicy.fs` | policy | `SeverityReason` |
| `QuantityMode.fs` | policy | |
| `SectionFold.fs` | policy | |
| `SessionMachine.fs` | machine | |
| `SessionGatePolicy.fs` | policy | `SessionMachine` |
| `OrderContextRefusalPolicy.fs` | policy | `SessionGatePolicy` (only `fill` and `sentences`) |
| `PlanWorkPolicy.fs` | policy | |
| `HeldContextPolicy.fs` | policy | |
| `SigningMachine.fs` | machine | |
| `SigningPolicy.fs` | policy | `SessionMachine`, `SigningMachine`, `SessionGatePolicy` |
| `UnsignedWorkPolicy.fs` | policy | `PlanWorkPolicy`, `SigningMachine` |
| `OrderPlanMachine.fs` | machine | `ArgumentationPolicy`, `PlanWorkPolicy`, `HeldContextPolicy`, `SigningMachine`, `SigningPolicy` (only `underWay`) |
| `OrderContextMachine.fs` | machine | `ArgumentationPolicy`, `OrderPlanMachine` |
| `FilterSync.fs` | other | |

Two dependencies keep the kinds from forming just two blocks, policies then machines: four
policies read the session and signing machines, and `OrderPlanMachine` reads `SigningPolicy`.
Without changing code, the order can still be grouped in five blocks, each of one kind:

| Block | Files, in order |
|---|---|
| The rest | `Deferred.fs`, `FilterSync.fs`, `TermText.fs` (new) |
| Policies over the contract alone | `LanguagePolicy.fs`, `OrderContextRefusalPolicy.fs`, `PatientDraftPolicy.fs` (new), `PickPolicy.fs`, `SeverityReasonPolicy.fs`, `ArgumentationPolicy.fs`, `QuantityModePolicy.fs`, `SectionFoldPolicy.fs`, `PlanWorkPolicy.fs`, `HeldContextPolicy.fs` |
| The session and signing machines | `SessionMachine.fs`, `SigningMachine.fs` |
| Policies over those machines | `SessionGatePolicy.fs`, `SigningPolicy.fs`, `UnsignedWorkPolicy.fs` |
| The order machines | `OrderPlanMachine.fs`, `OrderContextMachine.fs` |

Within a block the files are alphabetical where the dependencies allow it; `ArgumentationPolicy`
follows `SeverityReasonPolicy`, which it reads, and `OrderContextMachine` follows
`OrderPlanMachine`. The test project follows the same order, `Main.fs` last.

`OrderContextRefusalPolicy` reads `SessionGatePolicy` only for two text helpers, `fill` and
`sentences`, which have nothing to do with the session. Moved to `TermText`, they let the
refusal notice join the policies over the contract alone; `SigningPolicy` and
`SessionGatePolicy` call them there too.

### The comments

The items of #1157, checked against the code:

- "Pure F#, no React" in twelve module comments, as the issue lists. `SectionFold.fs` breaks it
  over a line (`Pure` / `F#, no React`), so a search for the phrase misses it.
- The module comments of `Deferred.fs`, `PickPolicy.fs`, `ArgumentationPolicy.fs` and
  `SessionMachine.fs`, the comment on `Deferred.bind`, the comments on `SessionView`,
  `SessionPhase` and `SessionState`, and the one on `SessionEffect.LoadCart`: as the issue
  describes them.

Found on the way, not in the issue:

- **Backticks in `///` comments**, in eight files: `Deferred.fs` (3), `OrderContextMachine.fs`
  (4), `OrderPlanMachine.fs` (4), `SessionGatePolicy.fs` (4), `OrderContextRefusalPolicy.fs`
  (2), `LanguagePolicy.fs`, `SessionMachine.fs` and `SigningPolicy.fs` (1 each). A `///`
  comment is never Markdown in this repository, so the backticks reach the popup as literal
  characters.
- **`SessionMachine.fs` mixes XML and prose** in its module comment (`<summary>` then
  `<remarks>`); the issue replaces it with one plain sentence, which settles this too.
  `LanguagePolicy.fs` and `SessionGatePolicy.fs` are all XML, which is allowed; they lose their
  "Pure F#" sentence and stay XML.

## Approaches considered

1. **One pull request for everything.** Simple to review in one go, but it mixes a move of
   clinical logic with renames and comment edits, and it passes the 200-line limit.
2. **One pull request per kind of change**: the migration, the renames, the comments. Each is
   small and reviewable on its own terms: the migration against its tests, the renames against
   the build, the comments against the issue's text.
3. **One pull request per issue item.** Eleven pull requests for what is mostly comment text.

## Chosen approach

Approach 2, in the order migration, renames, comments. The migration comes first because it is
the one change with behaviour behind it, and its new file is named by the rule the renames
introduce, so it needs no rename after. The renames come before the comments so that the
comment step edits the files under their final names.

## Confidence

High. The migration moves code that is already tested in a script, into a project whose tests
run in CI; the view's copy and the script's copy are the same by construction. The renames are
mechanical and the compiler checks them. The comments change no behaviour.

## Steps

One pull request each, one at a time.

1. **Migrate the panel's edit rules** (`refactor(client)`).
   - `Client.Core/PatientDraftPolicy.fs`, before `PickPolicy.fs`, its place in the grouped order: `Msg`,
     `setDepartment`, `update`, `Estimates`, `estimates`, `keepEstimates` and `canCalculate`,
     as the script has them. The module comment says in one sentence what it decides.
   - `tests/Informedica.GenPRES.Client.Core.Tests/PatientDraftPolicyTests.fs`: the script's 40
     tests, with a test for `canCalculate`.
   - `Views/Patient.fs` calls the module and loses its copy; `module private Elmish` keeps
     `show`.
   - `Scripts/PatientPanel.fsx` is deleted: the tests it held now run in CI.
2. **Name every decision module a policy, and group the files** (`refactor(client)`).
   - `TermText.fs` with `fill` and `sentences`, moved from `SessionGatePolicy` as they are; the
     calls in `SessionGatePolicy`, `SigningPolicy` and `OrderContextRefusalPolicy` follow. They
     are tested only through the texts that use them today, so `TermTextTests.fs` gets a test
     of each: the arguments filled in order, and the empty sentences dropped.
   - `SeverityReason` → `SeverityReasonPolicy`, `QuantityMode` → `QuantityModePolicy`,
     `SectionFold` → `SectionFoldPolicy`: the files with `git mv`, the modules, the test files
     and their test list names, both project files, the call sites in the views and in
     `ArgumentationPolicy.fs`, and `Scripts/load.fsx`.
   - Both project files in the grouped order above, with a `<!-- -->` comment heading each
     block, so that the next file added lands in its block.
   - The scripts whose code and tests live in `Client.Core` are deleted: `Argumentation.fsx`
     and `ArgumentationReset.fsx`, which load `SeverityReason`, and `OrderContextRefusal.fsx`
     and `OrderContextRefusalPolicy.fsx`. `Scripts/load.fsx` follows the new names and order.
3. **Clean up the comments** (`docs(client)` is not a scope commit-lint knows for code files, so
   `refactor(client)`).
   - The items of #1157 bar the argumentation one, in the words the issue gives.
   - The backticks in `///` comments, in the eight files above: the identifier written bare.
   - Comments only: no code line changes, which the diff shows.

## Left out

- **Whether an argumentation is needed is a domain question.** The issue says the solution is
  still to be decided. It moves logic from the client to the domain, so it gets its own plan.
- **Comments outside `Client.Core`.** The views have their own; they are not in #1157.

## Verification

For each step: `dotnet run build`; `dotnet test tests/Informedica.GenPRES.Client.Core.Tests/`;
`dotnet run ClientBuild` for the client, which restores the npm packages, compiles the F# with
Fable and bundles with Vite (Vite alone would bundle the JavaScript Fable last wrote); and
`dotnet fantomas --check` on the changed files. While `dotnet run` is up, its Fable watcher
recompiles on save and serves as a quick check between the builds. Step 1 also: the patient panel in the browser, anonymous mode, the checks of #1152
(clear a weight, change the department, change the age). Step 2 also: the order dialog, the
quantity field and a marked dose in the browser, since those views call the renamed modules.

## As built

| Step | Pull request | Note |
|---|---|---|
| The plan | #1167 | From the review: the client is verified through Fable (`dotnet run ClientBuild`), not Vite alone. |
| 1, the panel's edit rules | #1168 | `Msg` takes qualified access, so the view's dispatch sites read `Msg.UpdateWeight` and the like; every case got a `///` comment. 44 tests, four of them new for `canCalculate`. 143 lines added and 122 removed in shipped code, over the guideline, nearly all a move. The client check ran Fable and Vite without `npm ci`, which would have emptied `node_modules` under the running dev server. |
| 2, policies and the file order | #1169 | As planned, with one deviation: `ArgumentationPolicyTests.fs` stays after the order machines in the test project, since it reads the order plan machine's test fixtures; moving those fixtures to a file of their own would let it join its block. `TermText` got seven direct tests. 70 lines added and 64 removed in shipped code. |
| 3, the comments | #1170 | Widened on the maintainer's request from the issue's items to every comment in Client.Core: plain, succinct English, domain nouns in lowercase, `///` on every union case and record field, the machines' invariants one line each, no backticks in doc comments. Comments only, checked file by file with a script that compares the code without comments and blank lines. 658 lines added and 670 removed, in one pull request at the maintainer's request. The argumentation item of the issue stays open. |


## Addendum: Client.Core by kind (#1409)

Added 2026-10-10. Plan [1409](1409-client-core-by-kind.md) gave Client.Core the layout the file
order of step 2 above was heading for:

- One folder per kind, each a namespace `Informedica.GenPRES.Client.Core.<Folder>`: `Models` (the
  code from Shared that only the client uses), `Helpers`, `Policies`, `StateMachines` and
  `MachinePolicies`, compiled in that order. `Models` comes first because it reads only Shared and
  two helpers read it.
- The entry files `Lanes.fs`, `Client.fs` and `Trail.fs` sit at the project root, in the root
  namespace `Informedica.GenPRES.Client.Core`, so the root shows where the client starts.
- Every file declares its namespace explicitly, `namespace ...` on its own line, followed by its
  types and modules, with the opens inside each module.
- Code opens the folder namespaces only, never the root namespace: with the root and Shared both
  open, a name in both would resolve to whichever was opened last, without an error or warning.
  A root file is reached through a module alias, `module Client = Informedica.GenPRES.Client.Core.Client`,
  or opened by its full name; an alias cannot be opened.
- A module moved from Shared keeps its name when Shared keeps nothing of that name (`Severity`,
  `Terms`) and gets one of its own when it does (`PatientText`, `DoseTypeText`).
- The test project mirrors the folders and namespaces.

