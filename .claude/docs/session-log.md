# Session Log

## DoseRule data roundtrip (raw DoseRuleData -> DoseRule -> DoseRuleData)

**Deliverable:** `src/Informedica.GenFORM.Lib/Scripts/DoseRuleRoundtrip.fsx` (script-only,
prototype reverse mapping for maintainer review/migration).

**Built:** `DoseRule.toData : DoseRule -> DoseRuleData[]` — the missing reverse of the
forward pipeline. Inverts `getDoseLimits`, `mapToDoseRule`, `toLabel`; explodes
`ComponentLimits`/`SubstanceLimits` into one row per limit. `FormLimit` is NOT emitted
(it is derived from the external `formRoutes` table, not an input row). Id/GrpId/SortNo
are recomputed deterministically from row content on every pass (sha1Short of canonical
keys), never passed through `DoseRule.DataId`/`GroupId`/`SortNo`.

**Results (real doserules.tsv, full forward incl. product expansion, live provider):**
- **PASS 2 fixpoint = TRUE**: `reverse(forward(out1)) == out1` over the FULL data incl
  Id/GrpId/SortNo (0 diffs). The reverse mapping is sound and idempotent.
- **PASS 1 = 98.4%**: 4794 / 4874 surviving input rows round-trip exactly modulo
  Id/GrpId/SortNo. Residual 80 rows, all forward-side information loss (not reverse bugs):
  - 24 **merge-loss**: source rows colliding on `hashId` identity (same Source/Generic/
    Form/Route/Indication/PatientCategory/DoseType-incl-text) but differing in
    ScheduleText/Frequencies/limit. The forward merges them, keeping one schedule + both
    limits; reverse re-pairs lossily. The distinguishing data (e.g. neonatal birthweight)
    lives only in ScheduleText, not the structured identity.
  - 17 **narrowing-loss**: row-level form/GPK/HPK narrowing is **dissolved** by
    `addProducts`/`groupRows` expansion — the DoseRule ends up generic-wide
    (`label=Canonical`, `Generic.Products=[]`); expansion yields per-form rules. Only
    **Brand** narrowing survives (in the `GenericBrand` label).
  - 39 **truly-other**: near-duplicate rows / empty- or unparseable-limit rows that the
    forward drops or dedups.

**Forward-side losses surfaced (comparison gates these to match forward semantics):**
- A schedule/limit value only survives when its unit is present: PerTime needs FreqUnit,
  Rate needs RateUnit, *Adj needs AdjustUnit, all need DoseUnit. Values lacking their unit
  are dropped by `getDoseLimits` (cannot be expressed).
- `CmpBased` and `PatientCategory.Location` are parsed but never represented on DoseRule
  (unrecoverable). RateUnit/FreqUnit/etc. columns are discarded unless attached to a value.

**Key answer to design question:** form/GPK/HPK narrowing does NOT collapse back to the
original — the expansion phase dissolves it into the generic-wide product group. Whether
that is intended (expansion defines actual clinical products) or a gap is a design call.

## Root cause + prototyped fix (per user's corrected model)

User's model: DoseRules are always per-form (never multiform); narrowing is optional and only
selects matched products, whose distinct forms drive the per-form expansion. Identity must
include the narrowing so the roundtrip is total.

Diagnosis: the GPK/HPK narrowing IS preserved on `DoseRule.Generic.Products` — but `hashId`
(DoseRule.fs:369) excludes it, so two rows narrowed to different products (e.g. nadroparine
GPK 108278 vs 103136) collide on identity and MERGE in `fromData`'s `groupBy hashId` (one wins,
the other is lost). Confirmed: nadroparine had 47 rules, only 6 kept `Products=[Gpk 103136]`;
108278 was absorbed.

Prototyped fix in `DoseRuleRoundtrip.fsx` (`DoseRule.fromDataFixed` / `correctedId`): fold the
sorted `Generic.Products` (GPK/HPK) contents into the identity; empty list contributes nothing.
Real product expansion (`addProducts`) unchanged. Reverse unchanged (already reads
`Generic.Products`). Result: exact 98.4% -> **98.6%**, narrowing-loss 17 -> **4**. The fix is a
small change to `hashId` in DoseRule.fs (source; for maintainer to migrate).

Remaining residual after the fix:
- 4 narrowing-loss = FORM-narrowed rows (fluticason, nevirapine). Different mechanism: the
  forward must emit a `GenericForm` label for form-narrowed rows instead of `Canonical`.
  Separate small forward fix (not hashId+Products).
- 24 merge-loss + 39 truly-other = rows sharing the full 7-point identity but differing only in
  ScheduleText/Frequencies/limit (e.g. neonatal birthweight lives in ScheduleText, not the
  structured PatientCategory) + near-duplicates. The identity genuinely does not capture the
  distinction — needs a richer PatientCategory or is inherently ambiguous; NOT fixed by
  hashId+Products.

## Post-migration test fix + dead-code cleanup (DoseRule group-first pipeline)
- Migration of the group-first build into DoseRule.fs left the test project broken + old
  pipeline as commented dead code. fromData now returns a TUPLE (DoseRule[] * Message list),
  not Result; Api.fs:221 destructures it.
- Tests (tests/Informedica.GenFORM.Tests/Tests.fs): buildRules dropped Result.toOption|>Option.get
  (now |> fst); groupKey→dataGroupKey; rewrote the expandRowByFormAndUnitGroup + processGroup
  white-box tests as behavioral (via buildRules); deleted the redundant processGroup-matching test
  (covered by the existing gpks-narrowing test). Decisions: rewrite-as-behavioral + accept the
  dropped no-products warning (no warning assertion).
- Server.Tests/Tests.fs: mock GetDoseRules Ok([||],[]) → ([||],[]) (field is now the tuple).
- Dead code removed from DoseRule.fs: 3 commented (* *) blocks (old hashId, old per-row
  mapToDoseRule, old addProducts/processGroup/expandRowByFormAndUnitGroup/groupKey/ProductGroupKey
  pipeline) + the unused live addDoseLimits (carried refineLabel — its omission is the intended
  label-preservation). ~447 lines gone. DoseRuleData.fs: removed commented dataToCsv block.
- Stale comments fixed: getFromGetData doc (Result→tuple), candidateProducts doc (per-row→
  per-component), "doset type" typo. Fantomas-formatted.
- Verified: dotnet run build OK; full suite 5476 passed / 0 failed / 2 skipped.
- Deferred (out of scope): restoring the no-products warning diagnostic.

## fromData cold-start deadlock fix (post-migration)
- Symptom: server startup hung "forever" in DoseRule.fromData on full/production data;
  tests + warm FSI never reproduced it.
- Root cause: the migrated fromData ran mapToDoseRule under the SECOND Async.Parallel.
  On a COLD process the ~1155 parallel tasks trigger first-time CLR static/type
  initialization of the unit/dose-limit modules (Units/ValueUnit/DoseLimit/DoseType,
  via mapToComponentLimits->getDoseLimits) CONCURRENTLY from many threads -> mutually-
  dependent .cctor lock cycle -> deadlock (~0% CPU, blocked not spinning). Diagnosed by:
  cold parallel hangs; sequential warm-up then identical parallel run = 233ms.
- Why the OLD pipeline was fine: it ran mapToDoseRule as a plain sequential Array.map
  (warming those modules single-threaded) BEFORE its parallel addDoseLimits stage. The
  migration parallelized that stage and removed the implicit warm-up. The product/mapping
  modules are hit cold+parallel in both old (processGroup) and new (group) and are safe;
  the cycle is only in the unit/dose path the old code warmed sequentially.
- Why the scratch never hung: it was always WARM (long FSI session + my benchmarks ran
  sequential warm-ups first). Same latent bug, just never run cold-first.
- Fix (final): WARM-UP then parallel. In fromData, after the parallel `group` stage, run the
  first chunk (Parallel.totalWorders groups) of mapToDoseRule|>addFormLimits SINGLE-THREADED
  (forces the unit/dose module static init on one thread = whole type-init closure warmed),
  then process the rest in parallel and `Array.append head`. Warming a chunk (not one group)
  so coverage doesn't depend on which group is first.
- Why NOT full serialize: addFormLimits with REAL formRoutes (Mapping.filterFormRoutes over
  ~408 form-routes x ~7579 rules) is ~16-20s CPU. Measured warm in FSI: parallel fromData
  Real 1.38s (CPU 20.5s, ~15x); fully-serial Real 12.7s. So parallelism is worth ~9x and the
  warm-up keeps it. (Earlier "serialize costs ~1s" estimate was wrong — it excluded
  addFormLimits.) Dead `fromDataSerial` variant removed.
- Verified: cold cached-provider build completes (rules=7579, ~17s incl provider+product load;
  previously hung indefinitely). Full suite 5476 passed / 0 failed / 2 skipped.

## Product.fromGenPresProducts parallelized (next startup win)
- After the dose-rule fix, the dominant startup cost was Product.fromGenPresProducts (~8s).
  Profiled: prep (collect/filter/match) = 14ms; the per-product `map` = 7477ms (98.6%).
- Thread-safety audit (clean): Mapping.mapUnit/mapRoute/requiresReconstitution/filterFormRoutes
  are pure (read-only over passed arrays); ATCGroup.get + GenPresProduct caches use the
  Map-based Memoization.memoize (concurrency-safe, lossy not corrupting) AND are already warm
  before product build; NO memoizeOne/memoize2Int (Dictionary, corrupting) in the path. Only
  cold-init concern = GenUnits type-init -> same warm-up pattern.
- Change: in fromGenPresProducts, split the cheap prep (sequential) from the heavy per-product
  build (`buildProduct`), then warm the first chunk single-threaded and run the rest in parallel
  (Async.Parallel), `Array.append head` + parenteral + enteral. Same warm-up pattern as fromData.
- Verified: per-product map 7477ms -> 755ms parallel (~10x); cold cached-provider build now
  9.4s total (was ~17s; the ~7s product win realized), products=3014 rules=7579 (unchanged);
  parallel output BYTE-IDENTICAL to sequential reference (full structural array equality, not
  just count). Full suite 5476 passed / 0 failed / 2 skipped.

## 2026-06-12 — IR Doseringscontrole V-5-0-1 fit-gap analysis

- Read full text of `~/Downloads/IR Doseringen V-5-0-1.pdf` (Z-Index, 16-09-2025, 38 pp.) via macOS Spotlight importer (no PDF tools installed).
- Wrote `docs/code-reviews/genform-check-ir-doseringscontrole-v5-0-1-fit-gap.md`: requirement-by-requirement matrix (IR §§ 1.3–4.6.2) vs current Check.fs + ZIndex/ZForm pipeline; markdownlint clean.
- Confirmed: all 9 findings of the 2026-06-08 review are migrated into source.
- New gap register G-1..G-8; key open items: zorggroep merged instead of selected (RuleFinder.fs:119-120), PRK/HPK level discarded (ZIndex DoseRule.fs:456), gender gate absent, BSA categories never filter (Check.fs:223 passes bsa=None), ICPC aggregation, IR 3.4.2 frequency-signal suppression missing.
- Analysis only — no code changed, per user decision.
- Correction (user): the GenPRES dose check runs at DOSE-RULE level (formulary QA via ServerApi.Services.fs:189-192), NOT at prescription level — a different use case than the IR. Since the solver generates prescriptions that exactly satisfy dose rules, rule-level validation transitively guarantees G-Standaard compliance of prescriptions. Document § 1 rewritten with this framing; all impact statements reworded to rule-validation level.

## 2026-09-05 — Issue #420, PR 1: composition root swap

- Branch `refactor/use-of-agents`, commit 9f96e280 `fix(server): stop serialising requests through domain agents`.
- One-line change: `CompositionRoot.compose` now uses `Adapters.makeAppEnv` instead of `AgentAdapters.makeAppEnv`.
- Framed as `fix` (user decision): per-domain single-thread serialisation is a concurrency defect; `fix` also renders in the ShipIt changelog.
- Per-port `[XAgent]` debug logging dropped deliberately (user decision); reason in commit body. CompositionRoot already logs command name + full exception.
- Verified: `dotnet run build` clean; `CI=true dotnet run servertests` 6141 passed / 0 failed; app smoke test in Chrome with GENPRES_DEBUG=1 exercised all six ports (Formulary, Parenteralia, GetDrugNames, UpdateOrderContext, UpdatedOrderPlan, InitNutritionPlan/AddNutritionContext), 0 "Error processing" lines, commands visibly interleaved in the log.

## 2026-09-05 — Issue #420, PR 2: delete AgentAdapters

- Commit `fix(server): remove the unused agent adapters` on `refactor/use-of-agents` (stacked on 9f96e280).
- Deleted `ServerApi.AgentAdapters.fs` (497 lines) + fsproj entry; removed `agentAdapterGuardTests` (2 tests, direct-adapter twins remain); dropped the `#load` line from server and test `Scripts/load.fsx` (kept `#r Agents.Lib` — `Logging.fs` needs it).
- Docs: `0000-change-log.md` row for the removal (no new ADR; ADR-0008 already deleted in 5f930887); `integration-test-report.md` rows 13/14 removed, counts 14→12, 32→30; historical note in `Agents.Lib/Scripts/AgentLifecycle.fsx`.
- Verified: `dotnet run build` clean; `CI=true dotnet run servertests` 6139 passed / 0 failed (exactly 2 fewer); `dotnet fsi --exec Scripts/load.fsx` in the server Scripts dir exits 0; markdownlint clean on both docs.
- Out of scope, unchanged: `Informedica.Agents.Lib` (#416), shared `OrderLogger` cross-request interleaving.
