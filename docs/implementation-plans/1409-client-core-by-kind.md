# Implementation plan for issue #1409

Structure Client.Core by kind, and move the client-only code out of Shared. Drafted
2026-10-10. Line numbers are from
`docs/1224-by-responsibility-as-built` at `3464c7c7`.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [What the code does today](#what-the-code-does-today)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Left out](#left-out)
- [Verification](#verification)

## Problem description

`Informedica.GenPRES.Client.Core` grew from 29 files and 5k lines to 45 files and 8.2k lines.
All core UI logic now lives there: five lane machines, the Loader, Admin and Shell machines,
the Lanes and Client wiring, and the Trail. Three things make it harder to read than it
should be:

1. **The project is flat.** The fsproj shows the kinds only through `<!-- -->` block comments,
   and the blocks are no longer clean: "what is out, and what it disables" mixes machines,
   policies and helpers.
2. **Some files are misnamed or mix kinds.** `Busy.fs` holds types, Deferred readers and a
   policy over all five lane machines; machines read it, so a machine depends on a policy over
   machines. `PatientReadiness` and `PickList` are policies without the suffix. `canCalculate`
   is defined twice.
3. **Shared still carries client behaviour.** About 1,200 lines of `Shared` are used by the
   client alone and stay client-side: the patient display and form editors, the `Terms`
   vocabulary, display formatting and the continuous-medication calculation. Another 500 lines
   are used by nothing.

## Decisions

Taken 2026-10-10 by the maintainer.

| Question | Decision |
|---|---|
| Folders and namespaces | A folder is a namespace: `Informedica.GenPRES.Client.Core.<Folder>`. The root stays long, so no namespace is called `Client` and the `Wiring/Client` module keeps its name |
| Opening namespaces | Code opens the folder namespaces only, never the root `Informedica.GenPRES.Client.Core`. With the root and Shared both opened, a name found in both (`Models.Severity`) resolves to whichever was opened last, without an error or warning. The rule lives in this plan and the 1157 addendum; no CI check |
| Folders | `Helpers/`, `Models/` (the code from Shared), `Policies/`, `StateMachines/`, `MachinePolicies/` (the policies over machines, a namespace of their own), `Wiring/` (Lanes, Client) and `Diagnostics/` (Trail) |
| `Models/` and `Shared.Models` | The folder keeps its name. Moved modules get names of their own (`PatientText`, `SeverityText`, ...), and code that means Shared opens `Informedica.GenPRES.Shared` and writes `Models.X`, the same in the Client, Client.Core and the tests. Proved on 2026-10-10 with a scratch build of this layout: F# does not bring parent or sibling namespaces into scope (only the file's own namespace), so with Shared opened and the root not, `Models.X` always resolves to Shared, also for a name both have. `Shared.Models.X` without an open does not compile |
| The command preview chain and `NutritionCategory.label` | Stay in Shared, so their Server.Tests agreement tests stay where they are and no test project references across the rings |
| `OrderContext.label` | Moves with the display helpers; it calls Shared's `NutritionCategory.label` |
| The Busy split | `Helpers/Load`: `Load`, `Request`, `outOf`, `loadedOf`, `isOut`, `StartupPolicy.required`. `Policies/BusyPolicy`: `changes`, `any`, `page`. `MachinePolicies/OutPolicy`: `out` |
| `Deferred` | Keeps its AutoOpen; every file that uses its cases opens `Helpers` |
| PR size | A PR that only moves or renames code may exceed the 200-source-line limit; it holds no logic change and is reviewed with `git diff --color-moved` |
| `PatientDraftPolicy` | Stays under `Policies/`: it owns no state and emits no effects |
| Shared namespace | `namespace Shared` becomes `namespace Informedica.GenPRES.Shared`, like the other libraries (`Informedica.GenOrder.Lib`, `Informedica.MCP.Lib`) and the Client.Core folders |
| ADR-0003 | Its unused formulas are deleted, and ADR-0003 gets a dated amendment section saying so: what is left in Shared, and that `calcDuBois` moves to Client.Core with the patient display |
| ADR-0008 | No change: R1 limits what `Shared.Models` may hold and does not require client code to live there; only its `Shared.` names are updated |
| Group A and #582, #1208 | Group A moves only the list calculation and display code; the sheet parsers and the non-medication interventions stay in Shared, for #582 to move to the server |
| `Models/` and #1127 | The folder keeps its name; #1127, which reserves "model" for the contract model, settles the wording later |
| #1209 | Step 5 delivers #1209's baseline and closes it, see step 5 |
| Issue | #1409 carries this plan (#1157 and #1224 are closed) |
| Source changes | For this plan the Client.Core and Shared `.fs` source is changed directly, not prototyped in scripts first |

Earlier decisions this plan keeps:

- **1157**: every decision module is named `*Policy`; `FilterSync` and `Deferred` decide
  nothing and sit with "the rest"; `english` stays in each policy; the patient module is
  `PatientDraftPolicy`.
- **1224-the-app-as-wiring**: `Client` and `Lanes` compose machines and keep their names;
  `Trail` needs `Client`; `Alert.fs` is in Client.Core and its text in `Views/AlertText.fs`.
- **1224-the-order-machines-simplified**: every Msg or Effect change updates `Trail.fs` and its
  tests in the same PR.

## What the code does today

### Client.Core files by kind

Every file is a top-level `module X` with no namespace. The Client and the tests reach them by
bare module name.

| Kind | Files |
|---|---|
| Helpers | Deferred, FilterSync, TermText, Page, Alert, Url (parser), CommandPreview |
| Policies over the contract | Language, OrderContextRefusal, PatientDraft, PatientReadiness, Pick, Step, DialogTab, SeverityReason, Argumentation, QuantityMode, Intake, FieldOpen, PickList, PlanContext, PlanCell, SectionFold, TotalsChange, PlanWork, HeldContext, ServerError, UrlPolicy |
| State machines | Patient, Session, Signing, OrderPlan, OrderContext, Loader, Admin, Shell |
| Policies over machines | SessionGate, Signing, UnsignedWork, HeldPanel, Busy, Startup |
| Wiring | Lanes, Client |
| Diagnostics | Trail (1164 lines, needs Client) |

### Edges that cross the kinds

| Edge | Through |
|---|---|
| LoaderMachine → Busy, StartupPolicy | `Busy.Load`, `outOf`, `loadedOf`, `isOut`; `StartupPolicy.required` |
| AdminMachine → Busy | `Busy.Load`, `outOf`, `isOut` |
| ShellMachine → Busy, UrlPolicy | `Busy.Request` in `UrlCheck`; UrlPolicy reads `Busy.any` (`UrlPolicy.fs:99`) |
| LoaderMachine, ShellMachine → OrderContextMachine | `FilterSeed` only (`OrderContextMachine.fs:22-55`) |
| OrderContextMachine → OrderPlanMachine | `OrderContextView.dialog`; machine to machine, fine within one folder |

Only `Busy.out` reads the machine states; `changes`, `any` and `page` use only `Request` and
`Page`. Helpers inside policies: `SessionGatePolicy.digits`, `SigningPolicy.time`
(`Trail.fs:47` has a `time` of its own, in another format). Policy inside a machine:
`ShellMachine.seeds` is the url decision `UrlPolicy.change` takes as a bool.

### Shared code by user

Server side is the Server, MCP.Lib and Server.Tests. Client side is the Client, Client.Core
and its tests.

| Group | Shared source | Lines | Users |
|---|---|---|---|
| A. List calculations and display | `ContinuousMedication.calculate` (Models 1304–1376); `TextBlock.fromString` (Utils 481–532) | ~125 | client only; no tests. What stays and why: below the table |
| B. Display helpers | `Variable.renderValue(s)`, `OrderVariable` display, `Order.isSolved`, `OrderLoader` (and type), `Totals.intakeRows`/`substanceToField`, `DoseType`, `Severity` (and type), `TextBlock.flatten`, `OrderContext.label` | ~320 | client only |
| C. Patient display and editors, Terms | `Localization.Terms`, `getTerm`, `toFlag`; `Patient.toString`, `calcBSA` (and `Calculations.BSA.calcDuBois`, `Conversions`), the client getters, `toggle*`, `set*`, `edit*`; `Age.toString`, `gestAgeToString`, `fromBirthDate`; the `RenalFunction` module | ~760 | client only |
| D. Command preview chain | Api `preview` and `module Ctx`; `OrderContext` `filterFields`, `chain`, `clear*`, `applyChange`, `*Change`; the `Target` module, `values`, `Picks.withinOrder`, scenario … clear; `OrderVariable.setVu`/`setVar`/`setOvar` | ~500 | client; three Server.Tests agreement files; **stays in Shared** |

Group A is small because the emergency and continuous code is headed for the server
(#582, #1208). The sheet parsers (`EmergencyTreatment.parse`, `ContinuousMedication.parse`,
`Products.parse`, their `create*`, `Csv.parseCSV`) and the non-medication interventions (tube
size and length, defibrillation and cardioversion joules) stay in Shared for #582 to move to
the server. `EmergencyTreatment.calculate` builds the interventions and the bolus medication in
one list, so it, `calcBolusMedication` and `calcDoseVol` stay with the interventions; splitting
them would be a logic change, not a move. The types they use (`BolusMedication`,
`ContinuousMedication`, `Product`, `Intervention`) and the helpers (`Math`, `Decimal`,
`List.findNearestMax`, `String.contains`) stay too. #1214 later deletes the calculations from
where they are.

Used by nothing (about 500 lines): `Calculations.fs` except `calcDuBois`, `Conversions` and
`weeksToDays`; Localization `TranslationMap`, `parseCSV`, `getTermFromMap`,
`mergeTranslations`, `getTermOrDefault`, `fromString`, `tryFromString`; `Utils.DateTime`,
`List.create`, `List.removeDuplicates`, `Decimal.Ten`, the unused `String` helpers; Types
`Configuration`/`Setting`, `LoadedOrder`; Models `Age.create`, `getAgeInMonths`, `calcBMI`,
`updateWeightGram`, `ValueUnit.toStringDecimalDutchShortWithPrec`, `LoadedOrder`,
`OrderScenario.eqs`, `OrderContext.setScenarios`.

Stays in Shared: the wire types, `Api.fs`, `Locales`, `Measures`, `Csv` except `parseCSV` (`NormalValues.ofRows` and
`CsvTests` use `tryParseFloat` and the column readers), `NormalValues`,
Patient `create`/`validate`/readers/`withEstimates`/`applyNormalValues`, the Age readers,
`Order.*.create`, `Totals.empty`, `OrderScenario.create`, the whole of group D,
`NutritionCategory.label`, `Argumentation`, and the `String` functions the
server reaches through `open Shared` (`split`, `notEmpty`, `isNullOrWhiteSpace`, `trim`, in
`ServerApi.Services.fs` and `ServerApi.Mappers.Order.fs`).

## Approaches considered

1. **Folders only, module names unchanged.** No ripple outside the two fsproj files and
   `load.fsx`, but the namespace says nothing about the kind.
2. **A folder is a namespace.** Every Client and test file opens the namespaces it uses; the
   kind shows in every `open`.
3. **Interleave folders in the fsproj** to keep today's edges, against **breaking the edges**
   so each folder compiles as one block.

## Chosen approach

Approach 2, with the edges broken, by the maintainer's decision.

### Target layout

Compile order, top to bottom. Each file stays one module:
`module Informedica.GenPRES.Client.Core.Policies.PickPolicy`.

| Folder | Files |
|---|---|
| `Helpers/` | Deferred (AutoOpen kept), TermText, FilterSync, Page, Alert, Url, CommandPreview; new: `Load` (`Load`, `Request`, `outOf`, `loadedOf`, `isOut`, `StartupPolicy.required`), `FilterSeed`, and a text module for `SessionGatePolicy.digits` and `SigningPolicy.time` (Trail keeps its own `time`) |
| `Models/` | the code from Shared, groups A to C |
| `Policies/` | the policies over the contract; `PatientReadiness` → `PatientReadinessPolicy`, `PickList` → `PickListPolicy`; new `BusyPolicy` (`changes`, `any`, `page`); `UrlPolicy` takes `anyOut: bool` instead of a `Busy.Request` list, and takes over `ShellMachine.seeds` |
| `StateMachines/` | Patient, Session, Signing, OrderPlan, OrderContext, Loader, Admin, Shell; `ShellMachine` computes `anyOut` with `BusyPolicy.any` |
| `MachinePolicies/` | the policies over machines: SessionGate, Signing, UnsignedWork, HeldPanel, `OutPolicy` (`Busy.out`), Startup |
| `Wiring/` | Lanes, Client |
| `Diagnostics/` | Trail |

### Names of moved Shared code

A Client.Core module `Patient` would shadow `Shared.Models.Patient` where both are open, not
merge with it. The moved code gets names of its own, for example `PatientText`,
`PatientEdit`, `SeverityText`, `OrderDisplay`, `Terms`. Code that still means Shared opens
`Informedica.GenPRES.Shared` and writes `Models.X`: today 18 `Models.OrderContext`, 5
`Models.Severity`, 3 `Models.Totals` and 1 `Models.Patient` in the client code. They stay as
they are; the Severity and Totals sites change with group B, because that code moves.

### Tests

The test project mirrors the folders and namespaces. The fixtures other test files borrow from
`OrderPlanMachineTests` and `SigningMachineTests` move into `*Fixtures.fs` files, so the test
compile order no longer depends on them. Tests move with their code, in the same step:
`SeverityTests`, `Tests.fs` (the render tests) and the label tests of `ModelsTests` with group
B; the toString part of `AgeTests` (lines 233–4202) and the setter tests of `ModelsTests` with
group C. The cascade and mayAdd tests, Csv, Localization, validate, estimates and ofRows stay
in Shared.Tests.

## Confidence

Medium-high. Every step is a move or a rename the compiler checks. The risks are the size of
the diffs and group A, which moves clinical calculations that no test covers today.

## Steps

One PR at a time, in this order. Each step waits for the maintainer's go. When a step moves
code that an open issue names by path (#1244, #1390, #1208–#1214, #582), that issue gets a
comment with the new location.

1. **Delete the unused Shared code.** No behaviour change. The same PR adds the dated
   amendment to `docs/adr/0003-shared-clinical-calculations.md` (status line
   `Accepted, amended (date)`, per ADR-0000) for the formulas it removes. The amendment says
   plainly that the server never used these formulas, because GenCORE has its own, and what is
   left in Shared. The PR also fixes the repeated
   doc-comment line on `OrderContext.nutritionCategory` (`Models.fs:1999-2000`).
2. **Shared namespace.** `namespace Shared` → `namespace Informedica.GenPRES.Shared` in the six
   Shared files, and every `open Shared…` and `Shared.` qualifier in Server, MCP.Lib, Client,
   Client.Core, the three test projects and the scripts (`scripts/CheckLocalization.fsx`, the
   Server `Scripts/` and `Scratch/` files). `CheckDependencyRule.fsx` finds contract use by the
   token `"Shared."` (`contractToken`, line 160, and the domain-library check, line 102), and
   accepts a match after any character that is not a letter or digit. So
   `Informedica.GenPRES.Shared.Types` still matches and the checks keep working after the
   rename; only a bare `open Informedica.GenPRES.Shared` slips through, as a bare `open Shared`
   does today. The token becomes `"GenPRES.Shared"` in the same PR, which catches the bare open
   too. Nothing outside the source sees the
   namespace: the Fable.Remoting routes are built from the interface name
   (`routerPaths typeName method`), and the store writes the domain DTOs with System.Text.Json,
   without type names. A rename, no logic change.
3. **Break the cross-kind edges in Client.Core.** Split Busy into `Load`, `BusyPolicy` and
   `OutPolicy`; move `StartupPolicy.required`, `FilterSeed`, `digits` and `time`; `UrlPolicy`
   with `anyOut` and `seeds`; rename `PatientReadiness` and `PickList`; keep one
   `canCalculate` (PatientDraftPolicy's; the readiness one delegates, and
   `PatientReadinessTests.fs:182` already pins them equal). Trail and its tests in the same PR.
4. **Folders and namespaces.** `git mv` into the folders, the namespace top lines, the fsproj in
   folder blocks, `Scripts/load.fsx`. Client files qualify Core modules by name today, so each
   gains one `open Informedica.GenPRES.Client.Core.<Folder>` per folder it uses; files that use
   the `Deferred` cases open `Helpers`. The test project mirrored and its fixtures split out; the 26 stale
   `.fs.js` files removed. No logic change.
5. **Group A and the #1209 baseline.** First, #1209: a script runs `EmergencyTreatment.calculate`
   and `ContinuousMedication.calculate` once, over the live `emergencylist`, `continuousmeds` and
   `products` sheets, a weight grid and relevant ages, and writes the input rows and the TSV
   output into fixture files in the repo, marking the tenfold-dilution fallback, the min/max
   clamps and the morphine case. Tests never load the live sheets: Expecto tests read the
   fixture, run the calculation on the stored input rows and compare with the stored output.
   The emergency tests go to Shared.Tests (the code stays), the continuous tests to
   Client.Core.Tests. Then `ContinuousMedication.calculate` and `TextBlock.fromString` move,
   with their tests green before and after. Closes #1209; the TSV stays the reference #1208
   diffs against.
6. **Group B**, with its tests.
7. **Group C**, with its tests. `scripts/CheckLocalization.fsx` loads `Shared/Types.fs`,
   `Utils.fs` and `Localization.fs` with `#load`; it loads the moved `Terms` file in the same
   PR.
8. **Docs.** The Client.Core paragraph in AGENTS.md, ARCHITECTURE.md, DEVELOPMENT.md (it names
   `Trail`), the `Shared.` names in AGENTS.md and ADR-0008 (including R1's mention of the
   ADR-0003 formulas), and an addendum to
   `docs/implementation-plans/1157-client-core-refactoring.md` with the folders, the namespace
   rule and the rule never to open the root namespace.

## Left out

- **Server-only code in Shared**: the `*Command.toString` log helpers except
  `OrderViewCommand.toString` are used by the Server alone, and the `SeedSource` cases name
  client pages. A move to the Server is a plan of its own.
- **`Calculations.fs` copies GenCORE.** After step 1 only `calcDuBois`, `Conversions` and
  `weeksToDays` remain; `calcDuBois` moves with group C, as the ADR-0003 amendment records.
- **Pure logic still in the Client**, owned by other plans: the ViewHelpers stepping arithmetic
  (about 160 lines), `Interactions.getPlanDrugs`, which repeats `OrderPlanState.interactions`,
  `Global.pageToString`, the Order and NutritionSlot dialog updates and their trail describers.
- **Untested today**: `FilterSync` and `Alert.severity`.
- **#1390** (a dose below 0.1 shows four significant digits) is a bug in `Decimal.fixPrecision`,
  which stays in Shared. Its fix is a logic change and stays out of the move PRs.
- **#1127** names the layers; it may rename the `Models/` folder later.
- **The ring rule covers `src/` only.** `CheckDependencyRule.fsx` and `ProjectGraph.fsx` read
  the projects under `src/` (`srcProjects`), so a test project that references across the rings
  passes CI. This plan adds no such reference.

## Verification

Per step:

- `dotnet run build` and `dotnet run servertests`, or `dotnet test` of Shared.Tests,
  Client.Core.Tests and Server.Tests
- `dotnet fsi scripts/CheckDependencyRule.fsx`; `dotnet fsi scripts/ProjectGraph.fsx` only when a
  project reference changes
- `dotnet fantomas --check`
- Fable compile of the Client and `npx vite build`; Fable writes output despite type errors, so
  also `dotnet build` the Client fsproj
- `Client.Core/Scripts/load.fsx` loads in FSI
- the maintainer checks the app in the browser before any push
