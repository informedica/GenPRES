# Session Decisions - 2026-03-10

## Nutrition Plan Init Fix

### DemoVersion Bug
- **Finding**: Not a bug. `setDemoVersion` in `ServerApi.fs:794-800` correctly returns `false` when `GENPRES_PROD=1`.
- **Root cause of previous confusion**: The `.env` file has `GENPRES_PROD=0`, but the script sets it to `"1"` before `loadDotEnv()`, and `loadDotEnv` only sets vars that aren't already set. So the override works correctly.

### Empty Scenarios Root Cause
- **Finding**: `getRules` in `GenOrder.Lib/Api.fs:534-621` ignores the `Indications`/`Generics` pick list arrays set on the Filter. It rebuilds them from all matching prescription rules.
- **The cascade requires**: `Indication`, `Generic`, `Route`, and `DoseType` all set to `Some` before scenarios are generated (line 606-608 match pattern).
- **The old `initNutritionPlan`** only set the pick list arrays (`Indications`, `Generics`) but not the individual selections — so `getRules` returned all 742 indications and 542 generics, nothing was auto-selected, and no scenarios were produced.

### Fix Approach
- **Two-phase initialization** in `initNutritionPlan`:
  1. **Discovery phase**: Set `Indication` explicitly (from single-element array), evaluate to discover available `Generics` and `DoseTypes`, intersect with configured generics.
  2. **Evaluation phase**: For each matching generic, evaluate with `Indication + Generic + DoseType` (first available) all set → produces scenarios.
- For the TPV rule set with a 1-year-old patient: only "Samenstelling C" matches (of B/C/D/E), with 3 dose types (dag 1/2/3). Using first dose type produces 2 scenarios.

### Key URL ID
- The script uses `1rfOo5UjGoVHT5h-bJxR7FS-Qgz4faRrNGLeu2Yj8SS8` (correct production URL).
- The `.env` file has `1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM` (different spreadsheet, missing `Type` column → errors).

### Deferred: UI Select Boxes
- When multiple `DoseTypes` are available, the nutrition UI will need select boxes.
- Pattern to follow: `Prescribe.fs` filter selects using `SimpleSelect` component.

---

# Session Decisions - 2026-06-27

Three client-UI features this session. All changes are in `src/Informedica.GenPRES.Client/`
(the only `.fs` area the script-only policy permits direct edits). Builds clean via
`dotnet build src/Informedica.GenPRES.Client/Informedica.GenPRES.Client.fsproj`.

## 1. Patient view auto-close should not strand open dropdowns

- **File**: `Views/Patient.fs` (the 5s inactivity timer that collapses the patient accordion).
- **Problem**: collapsing the accordion reflows the whole page (it's the first child of the
  page Stack in `Pages/GenPres.fs`), detaching any open MUI dropdown popup (its own selects
  **or** a sibling child view's DataGrid COLUMNS/FILTERS popup) from its anchor → dangling list.
- **Decision**: gate the timer on a DOM check at fire time and **re-arm a fresh 5s** while any
  overlay is open (full-grace, user-chosen), rather than collapsing.
  - `anyOverlayOpen ()` = `document.querySelectorAll(".MuiModal-root:not(.MuiModal-hidden), .MuiPopper-root").length > 0`.
  - **Critical**: must exclude `.MuiModal-hidden`. The language menu (`Components/Localization.fs`)
    uses `keepMounted`, so its closed Popover/Modal root stays in the DOM permanently; without
    the `:not(.MuiModal-hidden)` the timer never fired (regression we hit and fixed).
  - Do NOT use an `[aria-expanded="true"]` selector — the accordion summary itself has it.
- DOM check chosen over open/close callbacks because MUI's own DataGrid toolbar popups can't be
  hooked; one DOM query covers patient selects + child-view popups + future popups.

## 2. Optimistic step value not reset when server returns the SAME value

- **Files**: `Views/Order.fs`, `Views/Nutrition.fs` (compute `revision`); `Views/ViewHelpers.fs`
  (`createNav` carries it); `Components/SimpleSelect.fs` (consumes it).
- **Problem (PR #372 follow-up)**: `SimpleSelect` reset its optimistic step delta only when the
  displayed `valueKey` changed. When a step overflowed and the server returned the *same* value,
  `valueKey` didn't change → the stale optimistic value stuck in the OrderView (background
  Prescribe view showed the correct value).
- **Decision**: add a monotonic `revision` counter, bumped on every new `Resolved` orderContext
  (Order.fs) / new `ctx` reference (Nutrition.fs), computed **during render via a ref compare**
  (`obj.ReferenceEquals`) so the new value is present on the response frame (no one-frame flash).
  Threaded through the `navigate` record (only stepping selects need it; `navigate=None` callers
  untouched). `SimpleSelect` adds `box revision` to its reset `useLayoutEffect` deps.
- **Note**: `navigate` records are built in 3+ places — `ViewHelpers.createNav` and **inline**
  records in Order.fs *and* Nutrition.fs (the `doseQtyNav`). All must carry every field or the
  structural anon-record type fails to compile. Easy to miss the inline Nutrition one.

## 3. Multi-component dose quantity: feasibility ceiling + saturate at max

- **Scope (important, narrowed twice with the user)**: applies ONLY to `orderable.dose.quantity`,
  and ONLY for **multi-component** orderables. Single component → orderable quantity follows the
  dose, no ceiling. The existing `canIncr` (`Components.Length = 1 || all DoseCount > 1`) and the
  "totale hoeveelheid" field being gated on `Components.Length > 1` both already encode this.
- **Server semantics (confirmed, drove terminology)**:
  - `OrderVariable.step` (`OrderVariable.fs:980`) moves **freely** along the increment grid with
    NO upper bound (comment: "may fall outside min/max … intentional"), then the order re-solves.
  - The downward correction on overflow is the **constraint re-solve being infeasible**, then
    `Api.fs:856` `Result.defaultValue sc.Order` **reverts to the previous order**. NOT a clamp.
  - Therefore "clamp" was the wrong word and the client should NOT bound the optimistic value to
    `DefinedConstraints.Max/Min` (that makes it *undershoot* the server). Only genuine bound =
    the structural feasibility ceiling.
- **Decisions / implementation** (all in `ViewHelpers.fs` + the two views):
  - `ovarStepTo (ceiling: decimal option) format ovar` — free stepping; applies only the
    feasibility `ceiling` + a one-increment floor (mirrors server non-zero-positive). `ovarStep`
    is the no-ceiling delegate. Replaced the old `DefinedConstraints`-range "clamp".
  - `orderableDoseQuantityCeiling ord` → `Some (max OrderableQuantity vals)` when `Components > 1`,
    else `None`. (For multi-component, max dose qty = orderable qty because `DoseCount_min = 1`,
    set by `setToMinIsOne` in `OrderProcessor.fs:93`.)
  - `incrementStepsToCeiling ceiling ovar` → how many defined-increment steps fit below the
    ceiling. Used by `saturateInc n` (in both views' dose-qty `increase` callback) to cap the
    DISPATCHED step count so an overflow **lands on the max** (feasible → no revert) instead of
    overshooting and reverting.
  - **Delta-saturation fix** (`SimpleSelect.fs`): `bumpInner`/`bumpOuter` now only accumulate a
    click if it actually changes the predicted value (`changesValue` compares step output
    before/after). Without this the raw delta grew past the visible ceiling, so reversing had a
    "dead zone" until the delta unwound below the max. General fix (also helps the floor).
- **Terminology retired**: "clamp" → "feasibility ceiling" / "saturate" / "revert" / "free
  stepping". `clampInc` renamed `saturateInc`. Swept all comments (Order/Nutrition revision
  comments, SimpleSelect, ViewHelpers) for residual "clamp"/"clamped back" wording.

### Open / deferred
- The outer `last` button (solved branch) dispatches `IncreaseDoseQuantityProperty(n, true)`
  **uncapped** — saturation only applied to the inner `increase`. Outer uses the calculated
  increment × server multiplier (`stepQuantity`), trickier to mirror client-side. Left as-is.
- A fully general server-side fix (saturate on infeasible step instead of `Result.defaultValue`
  revert at `Api.fs:856`) would cover fields where the client can't know the true max — but
  that's `.fs` server code → would need the `.fsx` prototype-and-migrate workflow, not done.
- Unit assumption in the ceiling `min`: dose qty and orderable qty share a unit (mL volume);
  holds for the orderable dose quantity, would be wrong if ever different-unit.

---

# Session Decisions - 2026-09-05 (macOS CI leg investigation)

Branch `perf/ci-no-build-test`, PR open against informedica/GenPRES master.

## Established facts (measured, not inferred)

- Runner hardware (now echoed by a `Runner hardware` step in build.yml): macos-latest arm64 = 3 cores / 7 GB;
  ubuntu-latest = 4 / 15 GB; windows-latest = 4 / 16 GB.
- Restore+build costs the same on all three OSes (~105 s). The macOS gap was entirely in the `ServerTests` target.
- `dotnet test` without `--no-build` re-evaluated the whole solution: 16 of 29 s locally; ~28 s on every CI leg. Fixed (c3cec9e5).
- `DOTNET_EnableWriteXorExecute` has no effect on arm64 macOS (13.3 vs 13.4 s). Do not re-suggest.
- GenSOLVER.Tests (4748 tests): per-test *durations* summed to ~3 s on macOS, but the tests window was 49 s.
  The time was harness overhead between tests (~10 ms/test on macOS, ~2.5 ms ubuntu, ~0.2 ms local) × 4624 golden
  MinMax scenario tests. Throughput ~65 tests/s while other testhosts ran, ~170/s alone → contention amplifies it.
- Agents.Tests 13 → 25 s on macOS: three FileWriterAgent FsCheck properties each do `Thread.Sleep 100` per case
  (`waitForFileWrite`) × 100 cases + temp-file IO. Not addressed yet.
- Expecto worker count = ProcessorCount (max in-flight tests 3 on macOS, 4 on ubuntu, 16 locally).

## Changes on the branch

1. c3cec9e5 `Build.fs`: `--no-build` on ServerTests; per-assembly progress line with wall-clock + VSTest Duration.
2. a6208fe0 `build.yml`: `Runner hardware` step; upload `**/TestResults/**/*.trx` per OS (7 days, always()).
3. 36411726 `tests/Informedica.GenSOLVER.Tests/Tests.fs`: 4624 per-line scenario tests → 4 per-operator tests
   (`scenarioTest`), mismatches listed by index. User explicitly authorised the .fs edit. Mutation-checked.
4. 9889a752 `Build.fs`: `-m:(max 2 (ProcessorCount - 1))` — MEASURED WORSE and reverted (see Rejected).

## Rejected

- W^X env var (no effect). Trimming macOS from the PR matrix (user chose to keep full matrix).
- Lowering FsCheck `maxTest=1000` in GenSOLVER: the properties were the *fast* part on macOS.
- MSBuild `-m` cap on `dotnet test` (run 33973552546): macOS ServerTests 32.5 → 56.6 s, ubuntu 22.9 → 27.2 s,
  windows 36.0 → 32.3 s. Once the 4624-test overhead was gone, less overlap only serialised the sleep-bound
  assemblies. Uncapped default stays.

## Analysis tooling (scratchpad, not in repo)

- `trx_profile.py <file.trx>`: run window / discovery gap / per-bucket sums / top tests from trx startTime+endTime.
- `wait_and_profile.sh <label> <sha>`: waits for the build.yml run on a commit, prints per-assembly lines, downloads trx.

## Results

| ServerTests | ubuntu | windows | macOS |
|---|---|---|---|
| original (5-run avg) | 57 s | 61 s | 116 s |
| + --no-build (33972679870) | 26 s | 39 s | 60 s |
| + scenario collapse (33973502710) | 23 s | 36 s | 32.5 s |

Follow-ups after #543 merged (all user-authorised `tests/*.fs` edits, each its own PR off master):

- #544 (merged) `test(agents)`: dropped `waitForFileWrite` (`Thread.Sleep 100` after every `flush`) — flush is
  PostAndReply and replies after StreamWriter.Flush, so the sleep guarded nothing. Agents.Tests 13/15/26 s →
  2/15/4 s (ubuntu/windows/macOS). Windows unchanged because its cost was elsewhere.
- #545 `ci(github)`: `overwrite: true` on the trx upload — v4+ upload-artifact rejects a duplicate name, so
  since #543 a job re-run failed at that step.
- #546 `test(agents)`: the two Agent FsCheck properties polled with `Thread.Sleep 5` (~15 ms granularity on
  Windows → 8.5 s each there); now a `ManualResetEventSlim` set from the handler on processed COUNT (the sum
  poll could return early on e.g. `[5; 0]`), 30 s hang guard. Invalid path is now a child of a regular file
  (the `//invalid//...` UNC form cost a 3-4 s lookup on Windows). Sync `waitUntil` removed.
  Merged. CI: Agents.Tests 2 s on all three legs (run 33976752836); a second Windows sample (33976951986,
  merge commit) showed 12 s with discovery itself 8× slower — CPU contention with GenCORE/Logging testhosts on
  the 4-core box, not the tests. Logging.Tests has the same Windows-only signature (4 agent-logging tests at
  5–12 s each vs 4 s total elsewhere): next Windows target if wanted, not started.

Final ServerTests per leg (fast samples): ubuntu ~28–30 s, windows ~33–38 s, macOS ~23 s; from 57 / 61 / 116 s.

Principle that held throughout: replace timing guesses with the protocol's own completion signal (reply
channel / event); keep timeouts only as hang guards (≥ 30 s).
