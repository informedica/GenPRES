# DoseRules Extraction Flowchart

## 1. Purpose

Create a generic dose rule extraction pipeline where free text is converted to dose rule data. The first concrete implementation targets the FTK XML "preparaat teksten".
Per-generic FTK XML is converted to `DoseRuleData` TSV via three sequential **NuExtract Online** passes plus a fourth **pure post-processing** pass, with a **human validation/edit checkpoint** after each pass.
Each pass writes a TSV (and optionally uploads it as a Google Sheet); the user edits the file, and the next pass reads the edited version.
Pass 3 is the terminal LLM-driven pass and emits the canonical TSV with structured Patient Category, Component / Substance, and DoseLimit columns filled.
Pass 4 is a deterministic post-processing pass that fills the `Id` / `GrpId` / `SortNo` columns from a stable hash of the canonical key fields — no NuExtract spend.
Pass 5 (validation) is an optional bridge that feeds the Pass-4 artefact through the production GenFORM `DoseRule` parser and renders the resulting `DoseRule` records as markdown (also written next to the script as `<stem>_doserules.md`), confirming the extracted TSV is consumable as real operational rules — no NuExtract spend, no column changes.

The whole pipeline is implemented as a single self-contained FSI script (`ftk_extract_v2.fsx`) that bundles all phase modules, NuExtract HTTP plumbing, Drive helpers, the FTK XML reader / decomposer, and the TSV writer. NuExtract 2.0's `verbatim-string` typing keeps Dutch source text intact across the round-trip; raw FTK XML is fed directly to NuExtract (no curation in front).

### Pipeline at a glance

```mermaid
flowchart LR
    SRC([Source text])
    AI[/"AI extraction pass<br/>(NuExtract)"/]
    DVAL[/"Deterministic<br/>validation"/]
    HUM{{"Human validation<br/>& correction"}}
    DET[/"Deterministic<br/>finalisation"/]
    OUT([Final DoseRule artifact])

    SRC --> AI
    AI --> DVAL
    DVAL --> HUM
    HUM -->|next pass| AI
    HUM -->|all phases done| DET
    DET --> OUT

    classDef ai fill:#1f4e8c,stroke:#0d2b54,color:#ffffff;
    classDef human fill:#8a6d00,stroke:#4d3d00,color:#ffffff;
    classDef det fill:#1f6b3a,stroke:#0d3d1f,color:#ffffff;
    class AI ai;
    class HUM human;
    class DVAL,DET det;
```

Each AI pass extends the artifact one level deeper; each human step refines what the prior pass produced. The cycle repeats per phase until all levels are populated, then a deterministic pass assigns stable identifiers.

Legend: blue = AI extraction, yellow = human validation/correction, green = deterministic post-process.

## 2. Scope assumptions

1. **Input**: the `<doseringen>` section of one generic's FTK preparaattekst XML, decomposed by `Ftk.decompose` / `Ftk.decomposeFromGeneric` and passed directly to NuExtract — no curation in front. Other formularies plug in by reusing the same decomposed-Block shape.
2. **Output of Pass 3**: one row per `(generic, indication, route, patient category, doseType, doseText, component, substance)` tuple, with PC + DoseLimit numerics and units filled in place. **Output of Pass 4**: same row set with `Id`, `GrpId`, `SortNo` filled by deterministic hash + ranking.
3. **The TSV file is the validation gate.** Each `Ux` step is a manual human pass — no in-app UI, no automated diff between passes. The user edits rows freely (add / delete / modify / reorder).
4. `Brand`, `Form`, `GPKs` (specific-dose-rule identifiers) are filled by the user at **U1**, right after Pass 1; all three enter the GrpId hash. `Dep` (department) is part of L4 PatientCategory and is also filled at U1 where applicable (optional — may stay empty). `Component` / `Substance` are filled by deterministic preprocessing inside Phase 3, driven by the user-edited `CmpBased` column — not by the model (see §6.3).
5. **NuExtract instructions** live as markdown under `docs/data-extraction/instructions/` and are loaded eagerly at script-load time. Edit-and-reload picks up changes.

## 3. Hierarchical data model

Every TSV row in the pipeline materialises one full path through a strict hierarchy of facts about a drug. Each pass extends the tree one level deeper; each user step refines the level the previous pass populated. Pass 4 hashes prefixes of the path to assign stable identifiers.

```text
L0  Source                                — formulary identifier (FTK, NKF, ...)
└── L1  Generic                           — canonical drug name
    │   ▷ Product axes (specific dose-rule identifiers, filled at U1):
    │       Form, Brand, GPKs
    └── L2  Indication                    — clinical indication
        └── L3  Route                     — administration route
            └── L4  PatientCategory       — verbatim PatientText + structured
                │                            (Gender, IsAdult, MinAge / MaxAge,
                │                             Weight, BSA, GestAge, PMAge,
                │                             Dep — user-derived department,
                │                             optional, may stay empty)
                └── L5  DoseType          — DoseType + DoseText + per-DoseType ScheduleText slice
                    └── L6  DoseTarget    — granularity selector:
                        │                    Component-based OR Substance-based
                        └── L7  DoseLimit — numeric ranges + units
                                              (Freqs, MinQty / MaxQty,
                                               MinPerTime / MaxPerTime,
                                               MinRate / MaxRate, ...)
```

**Product axes** (`Form`, `Brand`, `GPKs`) sit alongside L1 Generic and identify the specific drug product the dose rule applies to. They are filled by the user at U1 (right after Pass 1) so every downstream pass already sees the final dose-rule identity. All three enter the GrpId hash (see §3.2).

**`Dep` (department)** is a user-derived attribute of L4 PatientCategory, captured at U1 alongside the verbatim `PatientText` refinement. It can be left empty and is **spec'd** to enter the GrpId hash with the rest of L4(structured) — see the spec/code drift note in §3.2.

### 3.1 Levels and key axes

Each level is populated by a sequence of alternating **S** (system: deterministic code or NuExtract call) and **U** (user: manual TSV edit at a validation gate) actions across the pipeline. Rows are ordered by **pipeline step** first, then by level. The `#` column gives the global sequence.

Pipeline steps in order: Pass 1 → U1 → Pass 2 → U2 → Pass 3 (fan-out) → Pass 3 (PC sub-call) → Pass 3 (Limits sub-call) → U3 → Pass 4 → U4. Product axes (`Form` / `Brand` / `GPKs`) and the `Dep` axis at L4 are filled at U1; there is no separate "Manual refinement" step.

| # | Step | Actor | Level | Action | TSV columns affected | Driver |
|---|---|---|---|---|---|---|
| 1 | Pass 1 | S | L0 Source | Stamp `Source` from input metadata (literal `FTK` today). | `Source` | Pass 1 (input metadata) |
| 2 | Pass 1 | S | L1 Generic | Stamp `Generic` from input metadata. | `Generic` | Pass 1 (input metadata) |
| 3 | Pass 1 | S | L2 Indication | Extract verbatim Dutch indication phrase. | `Indication` | Pass 1 NuExtract |
| 4 | Pass 1 | S | L3 Route | Extract route, canonicalise to enum (`ORAAL`, `INTRAVENEUS`, …); apply default-Oraal post-process. | `Route` | Pass 1 NuExtract + `PostProcess.defaultOralRoute` |
| 5 | Pass 1 | S | L4 PatientCategory | Fill verbatim Dutch `PatientText`; stamp preliminary `IsAdult` via `Tsv.isAdultByKeyword` (StartsWith `volwassen` / `ouderen` / `bejaarden`). A positive-only `"x"`/`""` heuristic, later confirmed in Pass 3. | `PatientText`, `IsAdult` (preliminary) | Pass 1 NuExtract + `Tsv.isAdultByKeyword` |
| 6 | Pass 1 | S | L5 DoseType | Fill verbatim Dutch `ScheduleText`. | `ScheduleText` | Pass 1 NuExtract |
| 6a | Pass 1 | S | (synthesised) | Compose `OriginalText` from `PatientText` + `ScheduleText` (`PatientText: ScheduleText` join). | `OriginalText` | `Tsv.composeOriginalText` |
| 7 | U1 | U | L2 Indication | Split / correct indication cells if needed. | `Indication` | manual TSV edit |
| 8 | U1 | U | L3 Route | Correct misclassified routes. | `Route` | manual TSV edit |
| 9 | U1 | U | L4 PatientCategory | Split coarse `PatientText` cells into finer rows. | `PatientText` | manual TSV edit |
| 10 | U1 | U | L4 PatientCategory | Set `Dep` (department) where applicable. Optional — leave empty when not relevant. | `Dep` | manual TSV edit |
| 11 | U1 | U | L5 DoseType | Split coarse `ScheduleText` cells into finer rows. | `ScheduleText` | manual TSV edit |
| 12 | U1 | U | Product axes (alongside L1) | Identify the specific dose rule by filling `Form`, `Brand`, `GPKs`. All three enter the GrpId hash. | `Form`, `Brand`, `GPKs` | manual TSV edit |
| 13 | Pass 2 | S | L5 DoseType | DoseType split: 1 schedule row → M rows, one per dose type; overwrite `ScheduleText` with the per-DoseType slice. | `DoseType`, `DoseText`, `ScheduleText` | Pass 2 NuExtract |
| 14 | U2 | U | L5 DoseType | Correct the `DoseType` / `DoseText` splits. | `DoseType`, `DoseText`, `ScheduleText` | manual TSV edit |
| 15 | U2 | U | L6 DoseTarget | Mark `CmpBased` (component-based) or leave empty (substance-based). | `CmpBased` | manual TSV edit |
| 16 | Pass 3 — fan-out | S | L6 DoseTarget | Fan-out 1 row → N expansions; fill `Component` / `Substance`. | `Component`, `Substance` | `Phase3.expandRowsByGranularity` |
| 17 | Pass 3 — PC sub-call | S | L4 PatientCategory | Extract structured PC fields (incl. the model's `isAdult` verdict); cached by `(Generic, PatientText)`. `Phase3Pure.resolveIsAdultCell` then resolves the flag **positive-only** (any positive signal ⇒ `"x"`; absence ⇒ `""`, never a negative). When `IsAdult = "x"` the clearing guard blanks `MinAge`/`MaxAge`. | `Gender`, `IsAdult`, `MinAge`/`MaxAge`, `MinWeight`/`MaxWeight`, `MinBSA`/`MaxBSA`, `MinGestAge`/`MaxGestAge`, `MinPMAge`/`MaxPMAge` | Pass 3 PC NuExtract sub-call + `resolveIsAdultCell` |
| 18 | Pass 3 — Limits sub-call | S | L7 DoseLimit | Extract numerics + units (LimitsKind-dispatched); apply `validateMinMax`, AdjustUnit-pairing guard, `extractedFor` self-check. | `DoseUnit`, `AdjustUnit`, `RateUnit`, `Freqs`, `FreqUnit`, `MinTime`/`MaxTime` + `TimeUnit`, `MinInt`/`MaxInt` + `IntUnit`, `MinDur`/`MaxDur` + `DurUnit`, `MinQty`/`MaxQty`, `MinQtyAdj`/`MaxQtyAdj`, `MinPerTime`/`MaxPerTime`, `MinPerTimeAdj`/`MaxPerTimeAdj`, `MinRate`/`MaxRate`, `MinRateAdj`/`MaxRateAdj` | Pass 3 Limits NuExtract sub-call |
| 19 | U3 | U | L4 PatientCategory | Validate / correct structured PC fields **except `IsAdult`** (system-resolved at step 17 — see **IsAdult policy** below). | `Gender`, `MinAge`/`MaxAge`, `MinWeight`/`MaxWeight`, `MinBSA`/`MaxBSA`, `MinGestAge`/`MaxGestAge`, `MinPMAge`/`MaxPMAge` | manual TSV edit |
| 20 | U3 | U | L7 DoseLimit | Validate / correct numerics + units. | as step 18 | manual TSV edit |
| 21 | Pass 4 | S | Identity | Assign `Id` / `GrpId` / `SortNo` (deterministic SHA1-12 + rank); no NuExtract spend. | `Id`, `GrpId`, `SortNo` | Pass 4 (`Phase4Pure.assignIds`) |
| 22 | U4 | U | Identity | Final review of the TSV before the downstream typed-emit step (TBD, §8). | — | manual TSV edit |

> **IsAdult policy (spec).** `IsAdult` is a **positive-only confirmation marker** and is **fully system-resolved** — never a U3 user task.
>
> - `"x"` means age is *irrelevant for this rule*. **Absence (`""`) carries no meaning** — it never asserts "not an adult". Resolution may only ever turn the flag *on*; there is no negative verdict (a model `"no"` or an out-of-range bound simply leaves the flag unconfirmed/blank).
> - Confirmed `"x"` on any positive signal: numeric `MinAge ≥ 6570 days` (~18 yr), OR model `isAdult = "yes"`, OR the Pass-1 `Tsv.isAdultByKeyword` cell already `"x"`. (`Phase3Pure.resolveIsAdultCell`.)
> - **Clearing guard** (`Phase3Pure.applyPcToCells`): resolve the flag from the freshly-written numeric bounds first, then — only if it resolved to `"x"` — blank the now-irrelevant `MinAge` / `MaxAge` cells. An unconfirmed flag clears nothing.
>
> Operators can still override the cell as a last resort during U3, but the spec contract is that no manual override should be needed.

### 3.2 Identity keys (Pass 4)

Pass 4 hashes two **prefixes of the path through the tree**, plus the product axes that identify the specific dose rule:

- **GrpId** = SHA1-12 over L0..L4(structured) + product axes:
  `Source · Generic · Form · Brand · Route · GPKsCanon · Indication · Gender · IsAdult · MinAge · MaxAge · MinWeight · MaxWeight · MinBSA · MaxBSA · MinGestAge · MaxGestAge · MinPMAge · MaxPMAge · Dep`
  (`IsAdult` is a real `Phase4Pure.buildGrpFields` entry — no spec/code drift for it, unlike `Dep` below. It is optional in the input header, read via `Tsv.cellAtOpt`, so older TSVs predating the column hash an empty slot.)
- **Id** = SHA1-12 over the GrpId fields ++ L5: `… · DoseType · DoseText`

> **Spec/code drift — `Dep`.** `Dep` is part of L4 PatientCategory and is therefore included in the GrpId key in this spec. The current `Phase4Pure.buildGrpFields` implementation does **not** include `Dep` (the field list ends at `MaxPMAge`). This is a known gap: rows that share the entire L0..L4(non-Dep) prefix but differ only in `Dep` will currently collide on GrpId. Reconciliation requires adding `Dep` to `buildGrpFields` and re-running Phase 4 on affected TSVs.

L6 DoseTarget (`Component`, `Substance`) is **intentionally excluded from Id**. Multi-substance fan-outs of one logical rule (rows that differ only at L6) share a single Id, GrpId, and SortNo. L7 DoseLimit numerics are excluded too — they are downstream consequences of the rule, not part of its identity.

`SortNo` ranks distinct Ids inside one GrpId by `originalRowIndex` — first-appearance order, i.e. the row order shown per GrpId (no DoseType reordering). Rows that share an Id share a SortNo. **The row order is the user's manual ordering established at the U checkpoints** (§2.3 — the user may freely reorder rows); reordering rows at a `Ux` edit *is* the lever that sets SortNo. Pass 4 only mechanises that order — SortNo is never system- or DoseType-derived.

### 3.3 Cardinality at each pipeline boundary

| Boundary | Cardinality | Driver |
|---|---|---|
| Source XML → L0..L4(verbatim) + L5(verbatim ScheduleText) | 1 doc → N rows | Pass 1 (`Phase1.extractToTsv`) |
| L4(verbatim) / L5(verbatim) → refined splits | N → N′ | U1 (manual) |
| L5(verbatim) → L5(per-DoseType slice + DoseType + DoseText) | 1 row → M rows | Pass 2 (`Phase2.extractToTsv`) |
| L5 → L5(refined) + `CmpBased` | M → M′ | U2 (manual) |
| L5 + `CmpBased` → L6 expansions | M′ → P rows | `Phase3.expandRowsByGranularity` (deterministic, no NuExtract) |
| L4(verbatim) → L4(structured) | shared across L5/L6 (cached by `(Generic, PatientText)`) | Pass 3 PC sub-call |
| L6 → L7 | P → P (one Limits emit per expansion) | Pass 3 Limits sub-call |
| L4(structured) / L7 → validated | P → P′ | U3 (manual) |
| L0..L5 → GrpId, Id, SortNo | P′ → P′ (no row count change) | Pass 4 (pure) |

### 3.4 Mapping to TSV columns

Each TSV row materialises one full L0..L7 path. Cells belonging to a level not yet populated by the current pass are blank; cells that the user must supply (`Form`, `Brand`, `GPKs` at U1; `Dep` at U1 if applicable; `CmpBased` at U2) stay blank until the corresponding user step. `Dep` may also stay empty when the source rule has no department scope. The "TSV columns filled" / "still blank after Px" rows in §6 are the column-level projection of this hierarchical view.

## 4. Pipeline shape

```text
FTK preparaattekst XML — <doseringen> section (one generic)
   │
   ▼
[Pass 1: NuExtract Online — verbatim record extraction]   §6.P1
   │
   ▼   write <generic>_pass1.tsv (+ optional Drive upload)
[User validates pass1: split L2..L5 verbatim cells; fill `Form` / `Brand` / `GPKs` (specific dose-rule identifiers); set `Dep` (optional, part of L4 PatientCategory)]
   │
   ▼
[Pass 2: NuExtract Online — DoseType split]               §6.P2
   │
   ▼   write <generic>_pass2.tsv (1 schedule row → M DoseType rows)
[User validates + possible additional dose schedule text splits + determine component based pass2]
   │
   ▼
[Pass 3: granularity preprocessing + 10-project NuExtract dispatch] §6.P3
   │   (CmpBased preprocessing fans out 1 DoseType row → N expansions;
   │    Per row: PC + ScheduleForm classifier (disc/timed only) + dose-type-specific Limits;
   │    PC sub-call cached by (Generic, PatientText);
   │    ScheduleForm sub-call cached by (Generic, ScheduleText);
   │    Limits sub-call cached by (Generic, ScheduleText, SubstanceHint, LimitsKind)
   │    — substance hint is in the key so compact multi-substance shorthand
   │    (e.g. "1000/100-2000/200 mg/dosis" for amoxi/clavulaan) yields
   │    per-substance numerics, hinted via a "Substance: <name>" prefix;
   │    LimitsKind picks one of 7 per-DoseType limits templates plus a CatchAll
   │    fallback for `unfinished` rows. Each limits template emits a
   │    self-declared `extractedFor` label that the driver cross-checks
   │    against the substance hint — mismatches log audit failures.)
   │
   ▼   write <generic>_pass3.tsv (1 DoseType row → N expansions per CmpBased)
[User validates pass3 — terminal LLM-driven artefact]
   │
   ▼
[Pass 4: assign Id / GrpId / SortNo (pure post-processing)]   §6.P4
   │   (no NuExtract spend; deterministic short-hash IDs over
   │    Source + Generic + Form + Brand + Route + GPKs + Indication
   │    + structured PatientCategory (incl. IsAdult) + DoseType + DoseText
   │    for Id; same key minus DoseType/DoseText for GrpId.
   │    SortNo ranks unique Ids per GrpId by original Pass-3
   │    row order (first-appearance; no DoseType reordering).
   │    Multi-substance fan-outs of one logical rule share Id, GrpId, SortNo.)
   │
   ▼   write <generic>_pass4.tsv (1 row in → 1 row out, IDs filled)
[User validates pass4 — final TSV before downstream typed-emit (TBD)]
   │
   ├──▶ [Pass 5 (validation): GenFORM DoseRule parser bridge]   §6.5
   │        (no NuExtract, no column changes; pulls the latest
   │         <stem>_pass4 Drive Sheet, swaps GetDoseRules to read it,
   │         runs the real processDoseRuleData / mapToDoseRule /
   │         addDoseLimits, prints + writes <stem>_doserules.md.
   │         Phase5.diagnose replays the pipeline stage-by-stage
   │         to expose the two silent drops in DoseRule.getFromGetData.)
   ▼
[Downstream typed-emit — TBD]   §8
   │
   ▼
[GenFORM ingest]
```

Pass shape: **NuExtract Online call → fill / fan out TSV columns → write to disk → optional Drive upload → human edit → next pass reads the edited file**.

When a pass finishes, the script prints a delimited next-step banner to the console with exactly what the user must do at the following checkpoint (U1..U4) and which `Run.runPhaseN ()` to call next. The banner text is loaded from `instructions/next-steps-passN.md` (the canonical source mirrored in §6.1–§6.4).

Column set is the live `DoseRules` Google Sheet header (currently includes `PatientText` and `CmpBased` as input columns to Pass 3; both are dropped by the downstream typed-emit step (TBD, §8) before the production `doserules.tsv` emit). See §7.

## 5. Flowchart

```mermaid
%%{init: {'theme': 'neutral'}}%%
flowchart TD
    CTX_IN(["Input: FTK preparaattekst XML<br/>&lt;doseringen&gt; section, one generic"])
    CTX_IN --> P1

    %% Pass 1 — verbatim record extraction
    P1["P1  NuExtract Online: flat V2 template<br/>indication / patientCategory / doseSchedule = verbatim-string;<br/>route = 32-value route enum"]
    P1 -->|"N rows per doc"| T1["T1  Write &lt;generic&gt;_pass1.tsv<br/>fills Source=FTK, Generic, Route, Indication,<br/>PatientText, ScheduleText,<br/>preliminary IsAdult (keyword, positive-only)"]
    T1 --> U1{"U1  User validates pass1<br/>split L2..L5 verbatim cells;<br/>fill Form / Brand / GPKs (specific dose-rule);<br/>set Dep (optional, part of L4 PatientCategory)<br/>disk or Drive Sheet"}

    %% Pass 2 — DoseType split
    U1 --> P2["P2  NuExtract Online on ScheduleText<br/>→ DoseType + DoseText + per-DoseType slice<br/>1 schedule row → M DoseType rows"]
    P2 -->|"fan-out: M rows per input row"| T2["T2  Write &lt;generic&gt;_pass2.tsv<br/>fills DoseType, DoseText;<br/>ScheduleText = per-DoseType slice"]
    T2 --> U2{"U2  User validates pass2<br/>fills CmpBased per row"}

    %% Pass 3 — combined PC + ScheduleForm + per-DoseType DoseLimits, granularity from CmpBased
    U2 --> PRE["PRE  Phase3.expandRowsByGranularity<br/>CmpBased=x ⇒ component-based: 1 row, Substance=&quot;&quot;<br/>CmpBased=&quot;&quot; ⇒ substance-based: split Generic on /,<br/>1 row per substance (mono ⇒ Substance=Generic)"]
    PRE -->|"1 input row → N expansions"| P3["P3  Three NuExtract sub-calls (cached) per row<br/>a) PC template (17 fields, incl. isAdult) on PatientText,<br/>cached by (Generic, PatientText); resolveIsAdultCell resolves<br/>the flag positive-only, then the clearing guard blanks<br/>age (MinAge/MaxAge) when IsAdult = 'x'<br/>b) ScheduleForm classifier on ScheduleText (only when DoseType ∈ {discontinuous, timed}),<br/>cached by (Generic, ScheduleText) → frequency / interval / mixed / none<br/>c) Limits template chosen by Phase3Pure.limitsKindOf (DoseType, ScheduleForm)<br/>→ once / onceTimed / discFreq / discInt / timedFreq / timedInt /<br/>continuous / catchAll (each emits a focused field subset)<br/>cached by (Generic, ScheduleText, SubstanceHint, LimitsKind)<br/>multi-substance fan-outs prepend 'Substance: name' hint;<br/>each template emits self-declared 'extractedFor' label;<br/>extractLimitElement + runLimitsCall guards<br/>(validateMinMax + AdjustUnit pairing + extractedFor self-check)<br/>repair common model errors before the row is written."]
    P3 --> T3["T3  Write &lt;generic&gt;_pass3.tsv<br/>union of DoseLimitFields columns;<br/>per-LimitsKind templates leave non-applicable cells blank<br/>(e.g. once row has no Freqs/Time/Int/Rate)"]
    T3 --> U3{"U3  User validates pass3<br/>terminal artifact"}

    %% Pass 4 — Id / GrpId / SortNo assignment (pure)
    U3 --> P4["P4  Phase4Pure.assignIds (no NuExtract spend)<br/>Id    = sha1Short(Source + Generic + Form + Brand + Route + GPKs<br/>                  + Indication + PatientCategory(structured, incl. IsAdult)<br/>                  + DoseType + DoseText) [12 hex]<br/>GrpId = sha1Short(same key minus DoseType/DoseText) [12 hex]<br/>(IsAdult hashed via cellAtOpt; spec/code drift: Dep still<br/>not hashed by buildGrpFields — see §3.2)<br/>SortNo ranks unique Ids per GrpId by original Pass-3 row order<br/>(first-appearance; no DoseType reordering);<br/>multi-substance fan-outs share Id, GrpId, SortNo."]
    P4 --> T4["T4  Write &lt;generic&gt;_pass4.tsv<br/>fills Id, GrpId, SortNo;<br/>all other columns pass through verbatim"]
    T4 --> U4{"U4  User validates pass4<br/>final TSV before downstream typed-emit (TBD)"}

    %% Pass 5 (validation) — GenFORM DoseRule parser bridge (no NuExtract)
    U4 -.optional validation.-> P5["P5  Pass 5 (validation) — GenFORM DoseRule parser bridge<br/>pull latest &lt;stem&gt;_pass4 Drive Sheet;<br/>override Resources GetDoseRules with<br/>DoseRule.getFromGetData Phase5Pure.getDataFromTsv;<br/>real processDoseRuleData / mapToDoseRule / addDoseLimits;<br/>render per-generic toMarkdown → stdout + &lt;stem&gt;_doserules.md.<br/>runPhase5Diag = stage-by-stage trace exposing the<br/>getFromGetData Ok/Error swallow + 'no products found'"]

    %% Downstream typed-emit (TBD)
    U4 --> EMIT["TBD — downstream typed-emit<br/>drop extraction-only columns (PatientText, CmpBased);<br/>quarantine 'unfinished' rows;<br/>emit canonical doserules.tsv<br/>(typed string→BigRational/Units parse already done<br/>by the production DoseRule parser)"]
    EMIT --> GF["GenFORM ingest"]
```

## 6. Pass schemas

Every pass calls NuExtract Online via the shared HTTP plumbing in `module NuExtract` of the extraction script (`createProject` + `extractText` + `deleteProject`, Bearer auth from `NUEXTRACT_API_KEY`). Per-call payloads are sent **unchunked**. Verbatim-typed fields are used wherever possible so Dutch source text is preserved verbatim across the round-trip; numeric fields are typed `number` / `integer` and converted to canonical units client-side.

### 6.1 Pass 1 — verbatim record extraction (fills L0..L4 verbatim + L5 ScheduleText)

| | |
|---|---|
| **Status** | Live |
| **Template** | `Schema.nuExtractFlatTemplate` — `{"doses": [{"indication": "verbatim-string", "route": ["ORAAL", "INTRAVENEUS", …] (32-value route enum), "patientCategory": "verbatim-string", "doseSchedule": "verbatim-string"}]}`. Note: `route` is a closed enum array, **not** a verbatim-string — NuExtract picks the nearest canonical route token; `PostProcess.defaultOralRoute` then defaults the missing/empty case to `ORAAL`. |
| **Instructions** | `docs/data-extraction/instructions/phase1-ftk.md` (loaded eagerly via `Schema.ftkInstructions`) |
| **Driver** | `Phase1.extractToTsv` (gated on `FTK_EXTRACT_RUN=1`); single project per run |
| **Input** | The `<doseringen>` section of the FTK preparaattekst XML for one generic — passed directly to NuExtract; no curation, no normalisation |
| **Output** | An array of records `(indication, route, patientCategory, doseSchedule)` |
| **Cardinality** | 1 doc → N rows |
| **TSV columns filled** | `Source` (literal `"FTK"`), `Generic` (input metadata), `Route`, `Indication`, `PatientText` (verbatim Dutch PC), `ScheduleText` (verbatim Dutch dose-schedule), `OriginalText` (`PatientText: ScheduleText` join via `Tsv.composeOriginalText`), `IsAdult` (preliminary positive-only keyword heuristic via `Tsv.isAdultByKeyword` — `"x"` when `PatientText` StartsWith `volwassen`/`ouderen`/`bejaarden`, else `""`; later confirmed/cleared by Pass 3's `resolveIsAdultCell`) |
| **TSV columns blank** | All structured PC numeric columns (`Gender`, `MinAge` / `MaxAge`, weight, BSA, gestAge, PMAge), `DoseType`, `DoseText`, `Component`, `Substance`, `CmpBased`, all numeric limit columns, all unit columns, `Brand`, `Form`, `GPKs`, `Dep`, `Id`, `GrpId`, `SortNo` |

**User checkpoint U1 — what is expected.** Split / correct `Indication` (L2); correct misclassified `Route` (L3); split coarse `PatientText` cells into finer rows (L4); set `Dep` where applicable (optional, may stay empty); split coarse `ScheduleText` cells (L5); fill the product axes `Form`, `Brand`, `GPKs` (all three enter the GrpId hash — fill them now so downstream passes see the final dose-rule identity). Do **not** touch `IsAdult` (system-resolved) or fill `CmpBased` (U2) / `Id` / `GrpId` / `SortNo` (Pass 4). Then call `Run.runPhase2 ()`. Canonical text: [`instructions/next-steps-pass1.md`](instructions/next-steps-pass1.md) — printed to the console by the script when Pass 1 finishes.

### 6.2 Pass 2 — DoseType split (slices L5 verbatim into M per-DoseType rows)

> **Terminology.** There is no "dose phase". Pass 2 splits one verbatim
> schedule into M rows, **one per dose type** — each a Dose Rule row acquiring
> its `DoseType` / `DoseText` (its `ScheduleText` sliced to match). The F#
> surface is named accordingly (`Pass2DoseTypeRow` / `synthesiseDoseTypeRows`
> / `parseDoseTypeRowsJson` / `applyDoseTypeRowToRow`; `pass2.json` audit key
> `doseTypeRows`). Only the **wire** JSON key stays `phases` — that is the
> NuExtract API contract and its hand-tuned Dutch prompt, deliberately left
> unchanged. "Phase" alone always means an *extraction phase* (= a Pass).
> See [CONTEXT.md](CONTEXT.md).

| | |
|---|---|
| **Status** | Live |
| **Template** | `Schema.nuExtractDoseTypeTemplate` — `{"phases": [{"doseType": "verbatim-string", "doseText": "verbatim-string", "text": "verbatim-string"}]}` |
| **Instructions** | `docs/data-extraction/instructions/phase2-dose-type.md` (loaded via `Schema.doseTypeInstructions`) |
| **Driver** | `Phase2.extractFromDisk` / `Phase2.extractFromDrive` / `Phase2.upload`; single project per run; one HTTP call per Pass-1 row |
| **Input** | Each row's `ScheduleText` column from `<generic>_pass1.tsv` |
| **Output** | An array of `(doseType, doseText, text)` per input row. `doseType` is canonicalised client-side against `{once, onceTimed, discontinuous, timed, continuous}`; unknown tokens fold to `"unfinished"` (`Phase2.canonicalizeDoseType`) |
| **Cardinality** | 1 input row → M output rows (fan-out: one row per dose type). When NuExtract returns zero entries in the `phases` array (wire key) for a non-empty `ScheduleText`, `Phase2Pure.synthesiseDoseTypeRows` emits M = 1 with `doseType = "unfinished"`, `doseText = ""`, `ScheduleText` unchanged. Empty `ScheduleText` rows are still emitted with M = 1 — a single placeholder row with `DoseType = ""`, `DoseText = ""`, and an empty `ScheduleText` slice (cell value remains empty); no NuExtract call is made for these rows. |
| **TSV columns filled** | `DoseType`, `DoseText`. The verbatim per-DoseType slice `text` overwrites the `ScheduleText` column on the fanned-out row so subsequent dose-limit extraction operates on the per-DoseType text only. |
| **TSV columns blank after P2** | All structured PC numeric columns, `Component`, `Substance`, all numeric limit columns, all unit columns, `Id`, `GrpId`, `SortNo`. `Brand` / `Form` / `GPKs` are expected to be filled at U1 (specific-dose-rule identifiers); `Dep` is expected to be filled at U1 where applicable, otherwise intentionally empty. |

**User checkpoint U2 — what is expected.** Correct misclassified `DoseType` / `DoseText` / per-DoseType `ScheduleText` (L5); mark `CmpBased` = `x` for a component-based rule or leave empty for a substance-based rule (L6). `CmpBased` is a **required** Pass-3 column — Phase 3 fails fast if it is missing. Do **not** touch `IsAdult` or fill `Id` / `GrpId` / `SortNo`. Then call `Run.runPhase3 ()`. Canonical text: [`instructions/next-steps-pass2.md`](instructions/next-steps-pass2.md) — printed to the console by the script when Pass 2 finishes.

### 6.3 Pass 3 — PatientCategory (L4 structured) + DoseTarget expansion (L6) + DoseLimits (L7) — CmpBased-driven granularity, LimitsKind-dispatched per DoseType + ScheduleForm

| | |
|---|---|
| **Status** | Live |
| **Templates** | `Schema.pcTemplate` (17 fields — `gender`, `isAdult`, plus Min/Max + unit triples for age / weight / BSA / gestAge / PMAge); `Schema.scheduleFormTemplate` (1 enum field — `frequency` / `interval` / `mixed` / `none`); 7 per-LimitsKind limits templates (`onceTemplate` 7 fields, `onceTimedTemplate` 10, `discFreqTemplate` 16, `discIntTemplate` 19, `timedFreqTemplate` 19, `timedIntTemplate` 22, `continuousTemplate` 11) plus the legacy `limitsTemplate` (27 fields, used only as the `CatchAll` fallback for `unfinished` and unrecognised DoseType). Every limits template carries `extractedFor` as its FIRST field (verbatim-string self-declared label: the hinted substance name when a `Substance: <name>` header is present, or `"Form"` when no header) — see the **`extractedFor` self-check** row below. All template field counts derive their categorical-unit enums from the live `DoseRules` Google Sheet via the shared `Schema.unitField` / `Schema.doseRulesSheet` helpers (one HTTP GET at script-load, snapshot reused). |
| **Instructions** | `phase3-patient-category.md` (`Schema.pcInstructions`); `phase3-schedule-form.md` (`Schema.scheduleFormInstructions`); per-LimitsKind prompts `phase3-dose-limits-once.md`, `-once-timed.md`, `-disc-freq.md`, `-disc-int.md`, `-timed-freq.md`, `-timed-int.md`, `-continuous.md` (all loaded via `Schema.<kind>Instructions`); legacy `phase3-dose-limits.md` (`Schema.limitsInstructions`, the CatchAll prompt). All substance-aware prompts share a uniform SUBSTANCE-HINT block: inline-name takes precedence over ratio-shorthand, and a hint that does not match a numeric's owner forces `null` for that numeric (no copy-across-substances). |
| **Driver** | `Phase3.extractFromDisk` / `Phase3.extractFromDrive` / `Phase3.upload`. **10 NuExtract projects created upfront** (PC, ScheduleForm, 7 per-LimitsKind limits, CatchAll) via the `Project.withProjects` combinator (built from `Phase3.projectSpecs runStamp`), which threads creation, the orchestrator body, and best-effort cleanup of every successfully-created project through one `try / finally`. Per-row fanout runs under `AsyncThrottle.parallelThrottled` (degree from `FTK_EXTRACT_PARALLEL`, default 8). |
| **Granularity preprocessing** | `Phase3.expandRowsByGranularity` runs before any NuExtract call. Per Pass-2 row: non-empty `CmpBased` ⇒ ONE expansion with `Component = Generic`, `Substance = ""`. Empty `CmpBased` ⇒ split `Generic` on `'/'` (trim, drop empties), ONE expansion per segment with `Component = Generic`, `Substance = <segment>`. Mono-substance generics yield ONE expansion with `Component = Substance = Generic`. Required Pass-2 columns include `DoseType` (read by `limitsKindOf`) — fails fast otherwise. |
| **PC sub-call** | Input: `PatientText`. Output: PC numerics + verbatim Dutch units (min/max of each pair share one unit — UNIT INVARIANT) plus the model's positive-only `isAdult` verdict. Code-side converts to canonical days / grams / m² via `convertAgePairToDays` / `convertWeightPairToGrams` / `convertBsaPairToM2`. `Phase3Pure.resolveIsAdultCell` confirms `IsAdult = "x"` on any positive signal (`MinAge ≥ 6570 d`, model `isAdult="yes"`, or Pass-1 keyword `"x"`) — positive-only, absence never asserts a negative. `Phase3Pure.applyPcToCells` then runs the **clearing guard**: `IsAdult="x"` blanks `MinAge`/`MaxAge`. Cached by `(Generic, PatientText)`. |
| **ScheduleForm sub-call** | `Phase3.runScheduleFormCall` invoked **only when DoseType ∈ {`discontinuous`, `timed`}**; other DoseTypes bypass the classifier (form passed as `None`). Output: a single `scheduleForm` enum, parsed by `Phase3Pure.parseScheduleForm` to a `ScheduleForm` DU (`Frequency` / `Interval` / `Mixed` / `None_`); transport / parse failures default to `Frequency` so the row still gets extracted by the broadest template. Cached by `(Generic, ScheduleText)` — classification is LimitsKind-independent and substance-agnostic. |
| **Routing matrix (`Phase3Pure.limitsKindOf`)** | `once` → `Once`; `onceTimed` → `OnceTimed`; `continuous` → `Continuous`; `discontinuous` + `Frequency` / `Mixed` / `None_` → `DiscFreq`; `discontinuous` + `Interval` → `DiscInt`; `timed` + `Frequency` / `Mixed` / `None_` → `TimedFreq`; `timed` + `Interval` → `TimedInt`; any other DoseType (incl. `unfinished`) → `CatchAll`. `Mixed` and `None_` fall back to the frequency-form template because it captures BOTH `freqs` / `freqUnit` and `minInt` / `maxInt` / `intUnit`. The lower-camel label of a `LimitsKind` (`once`, `onceTimed`, `discFreq`, …) is produced by `Phase3Pure.labelOfKind` and used in audit JSON; the Pascal-case label (`Once`, `OnceTimed`, `DiscFreq`, …) is produced by `Phase3.labelForKind` and used as the project-map key. |
| **Limits sub-call** | `Phase3.runLimitsCall projectId substance scheduleText` (the second parameter is named `substance` in code but always carries the resolved substance hint) where `projectId = projectIds.ByKind kind` (the resolved kind from `limitsKindOf`). The substance hint is computed once via `Phase3Pure.resolveSubstanceHint generic substance` (empty when the substance equals the generic or is missing). Input: per-DoseType `ScheduleText`, optionally prefixed with `Substance: <name>\n---\n` (built by `Phase3Pure.buildLimitsPayload`); omitted for component-based and single-substance rows. Output: a single object whose field set depends on the LimitsKind. Numerics validated by `Phase3Pure.validateMinMax` (inverted pairs swap, negatives drop); units canonicalised against `Phase3Pure.canonical{Genders,DoseUnits,AdjustUnits,TimeUnits}` (unknown tokens fall through verbatim — required for sheet-only units like `AXa.E`/`mmol` and composite freq intervals like `"36 uur"`). `freqs` is parsed from a verbatim Dutch phrase by `Phase3Pure.parseFreqs`, then unioned with integer frequencies extracted from the schedule text by `Phase3Pure.augmentFreqsFromSchedule` (regex-driven: matches `N×/`, `N x /`, ranges expand). **AdjustUnit pairing guard**: if `AdjustUnit` is non-empty but every adjusted-dose numeric is None, `AdjustUnit` is cleared and a failure note is appended (defends against the common model bias of tagging `kg` reflexively when patient-context mentions weight). **`extractedFor` self-check guard** (`Phase3Pure.checkExtractedFor`): the model's self-declared `extractedFor` label is compared (case-insensitive) against the substance hint passed in the call — when the hint is empty, `extractedFor` MUST be `"Form"`; when the hint is non-empty, `extractedFor` MUST match it verbatim. Mismatches do NOT mutate the numerics — they only log a failure note so the operator can triage from the audit JSON; the row still gets written. Cached by `(Generic, ScheduleText, SubstanceHint, LimitsKind)` — LimitsKind is in the key because the same `(generic, scheduleText, substanceHint)` tuple can produce different field shapes through different per-LimitsKind prompts. |
| **Cardinality** | 1 Pass-2 row → N output rows where N = expansions produced by `expandRowsByGranularity`. Empty `ScheduleText` still expands per `CmpBased`; Limits cells stay blank. |
| **TSV columns filled** | All structured PC columns (`Gender`, `IsAdult` (when present), `MinAge` / `MaxAge`, weight, BSA, gestAge, PMAge), `Component`, `Substance`, all unit columns, `Freqs`, every `Min*` / `Max*` numeric — but only the subset that the chosen LimitsKind's template actually emits; non-applicable cells stay blank (e.g. an `Once` row leaves `Freqs` / `FreqUnit` / `MinTime` / `MinInt` / `MinRate` etc. empty). `IsAdult` is resolved positive-only by `Phase3Pure.resolveIsAdultCell` (any positive signal ⇒ `"x"`, else `""`; never a negative); a confirmed flag then blanks `MinAge`/`MaxAge` via the `applyPcToCells` clearing guard. |
| **TSV columns still blank after P3** | `Id`, `GrpId`, `SortNo` (filled by Pass 4). `Brand` / `Form` / `GPKs` and (where applicable) `Dep` are expected to have been filled at U1; the spec contract is that no new manual refinement is required between U3 and Pass 4. |

**User checkpoint U3 — what is expected.** Validate / correct the structured PatientCategory fields (`Gender`, `MinAge` / `MaxAge`, weight, BSA, gestAge, PMAge) and all DoseLimit numerics + unit columns the chosen LimitsKind emitted. `IsAdult` is **not a U3 task** — it is system-resolved positive-only; when `IsAdult = "x"` the `MinAge` / `MaxAge` cells were deliberately blanked by the clearing guard (correct — do not refill). Override `IsAdult` only as a last resort. Do **not** fill `Id` / `GrpId` / `SortNo`. Then call `Run.runPhase4 ()`. Canonical text: [`instructions/next-steps-pass3.md`](instructions/next-steps-pass3.md) — printed to the console by the script when Pass 3 finishes.

### 6.4 Pass 4 — Id / GrpId / SortNo assignment (hashes prefixes of the L0..L5 path)

| | |
|---|---|
| **Status** | Live |
| **Driver** | `Phase4.extractFromDisk` / `Phase4.extractFromDrive` / `Phase4.upload`. Pure post-processing — no NuExtract calls, no NUEXTRACT_API_KEY required. |
| **Input** | `<generic>_pass3.tsv` (or the latest `<generic>_pass3` Drive Sheet). |
| **Output** | `<generic>_pass4.tsv` with `Id`, `GrpId`, `SortNo` filled; all other columns pass through verbatim. |
| **Cardinality** | 1 input row → 1 output row. |
| **GrpId key** (spec) | `Source + Generic + Form + Brand + Route + GPKsCanon + Indication + Gender + IsAdult + MinAge + MaxAge + MinWeight + MaxWeight + MinBSA + MaxBSA + MinGestAge + MaxGestAge + MinPMAge + MaxPMAge + Dep`. Each field passes through `Phase4Pure.normaliseField` (trim, drop tabs/newlines, collapse whitespace, lowercase); `GPKs` first goes through `normaliseGpks` (split on `,`/`;`, trim, sort, rejoin). `IsAdult` is **optional** in the input (read via `Tsv.cellAtOpt`) — older Pass-3 TSVs predating the column hash an empty slot; it is genuinely present in `Phase4Pure.buildGrpFields`. **Spec/code drift:** only `Dep` is still not yet included by `buildGrpFields`; see §3.2 for the reconciliation note. |
| **Id key** | GrpId key concatenated with `DoseType` and `DoseText`. Substance / Component are intentionally excluded — multi-substance fan-outs of one logical rule (e.g. amoxicilline + clavulaanzuur for the same PC and DoseType) share one Id; Substance just identifies which `SubstanceLimit` they populate. |
| **Hash** | SHA-1 over the tab-joined normalised key, first 6 bytes rendered as 12 lowercase hex chars (~16M before 50% birthday collision; well over the FTK + NKF + future-formularies row count). |
| **SortNo** | Ranks **unique Ids** within each GrpId by `originalRowIndex` (first-appearance / row order shown per GrpId — no DoseType reordering). The row order is the user's manual ordering at the U checkpoints (reordering rows at `Ux` is the lever); never system- or DoseType-derived. Rows that share an Id (multi-substance fan-outs) share a SortNo. |
| **TSV columns filled** | `Id`, `GrpId`, `SortNo`. |
| **TSV columns still blank after P4** | None mandatorily blank — `Brand` / `Form` / `GPKs` were filled at U1, structured PC at Pass 3, identifiers at Pass 4. `Dep` may remain empty when the source rule has no department scope. |
| **Idempotency** | Re-running Phase 4 on a Pass-4 TSV is byte-identical to the first run **provided the GrpId / Id key cells are unchanged** (`Source`, `Generic`, `Form`, `Brand`, `Route`, `GPKs`, `Indication`, structured PatientCategory incl. `Dep`, `DoseType`, `DoseText`). User-driven refinements that touch any of those cells between runs will produce different hashes — by design. With `Form` / `Brand` / `GPKs` / `Dep` now filled at U1, the typical flow stabilises hashes by Pass 4 first run. |
| **Audit JSON** | `<jsonDir>/<generic>.pass4.json` — one entry per row carrying `inputRowIndex`, `id`, `grpId`, `sortNo`, `doseType`, `grpFields[]`, `idFields[]`, `generic`. Lets a human reproduce any hash by hand. |

**User checkpoint U4 — what is expected.** Light final review of the whole TSV before the downstream typed-emit step; all domain columns should already be correct (product axes at U1, structured PC at Pass 3, identifiers at Pass 4). `Id` / `GrpId` / `SortNo` are deterministic — if any GrpId / Id key cell is edited during U4, re-run `Run.runPhase4 ()` (the hashes will, and must, change); editing non-key cells needs no re-run. The TSV is then ready for the downstream typed-emit step → GenFORM ingest (typed-emit is TBD — see §8). Canonical text: [`instructions/next-steps-pass4.md`](instructions/next-steps-pass4.md) — printed to the console by the script when Pass 4 finishes.

### 6.5 Pass 5 (validation) — GenFORM DoseRule parser bridge (no NuExtract, no column changes)

> **Depends on a maintainer-authored `.fs` seam.** Pass 5 (validation) requires
> `DoseRule.getFromGetData` in `src/Informedica.GenFORM.Lib/DoseRule.fs` — a
> seam that parameterises the dose-rule data reader so the extraction bridge
> can swap the Google-Sheet reader for a Pass-4 TSV reader. This is a
> maintainer-authored, in-scope single-function refactor of the production
> parser (`let get = getFromGetData getData` preserves the production call
> site). It is **not** covered by the script-only policy's `.fsx`-only
> constraint; the `IsAdult` future-work note in §8 concerns a *different*,
> not-yet-made `.fs` change.

| | |
|---|---|
| **Status** | Live |
| **Driver** | `Phase5.run` / `Phase5.runFromDrive` / `Phase5.parseToDoseRules`; diagnostic `Phase5.diagnose` / `Phase5.diagnoseFromDrive`. Entry points: `Run.runPhase5 ()` (Drive-sourced render) and `Run.runPhase5Diag ()` (stage-by-stage trace). No NuExtract, no `NUEXTRACT_API_KEY`. |
| **Input** | The latest `<stem>_pass4` Drive Sheet (downloaded to a temp TSV via `Drive.downloadLatestByPrefix`). `Phase5.run`/`diagnose` also accept a local TSV path directly. |
| **What it does** | Reuses the **production** GenFORM pipeline: builds `Resources.defaultResourceConfig dataUrlId`, overrides only `GetDoseRules` with `DoseRule.getFromGetData Phase5Pure.getDataFromTsv <pass4Tsv>`, then `Resources.loadAllResourcesWithConfig`. So the real route mappings, form routes, and products are used; only the dose-rule data source is swapped to the extracted TSV. |
| **`Phase5Pure.getDataFromTsv`** | Mirrors `DoseRule.getData` (`DoseRule.fs`) field-for-field, sourcing rows from `File.ReadAllLines` + tab-split instead of the Google sheet. The Pass-4 header is exactly the canonical `DoseRules` column set, so `Csv.getStringColumn` resolves every column by name. Pure; returns `Result<DoseRuleData[], Message list>`. |
| **Output** | Markdown of the parsed `DoseRule` records, printed to stdout **and** written next to the script as `<stem>_doserules.md` (stem derived from the Pass-4 filename, stripping the `_pass4…`/`_drive…` suffix). Rendered **one generic at a time** — `DoseRule.Print.toMarkdown` is per-generic by contract (its outer `Array.groupBy _.Generic` fold replaces, not appends, so a multi-generic array would yield only the last generic; `DoseRule.Print.printGenerics` is the equivalent wrapper). |
| **Cardinality** | Read-only consumer — does not alter the TSV or row count. |
| **Diagnostic** (`Phase5.diagnose`) | Replays the pipeline stage-by-stage **without** the silent swallows, printing per-stage survival counts and what each stage drops: (1) raw rows, (2) `doseRuleDataIsValid` filter, (3) `processDoseRuleData` (product match / synthetic substitution), (4) `mapToDoseRule` **with every `Error` printed** — `DoseRule.getFromGetData` (`DoseRule.fs:923-927`) partitions Ok/Error and discards the Error rows with no log, and `processDoseRuleData` collects a "no products found" warnings dict it never returns; the diagnostic surfaces both. |
| **Dependencies** | The script `#r`s a single consistent DLL set from the **GenForm bin** (`Informedica.GenFORM.Lib.dll` + its co-located `Utils` / `Logging` / `GenUNITS` / `GenCORE` / `ZIndex` / `ZForm` / `OTS` / `Agents`) so the Phase-5 parser and the rest of the pipeline share one `Informedica.Utils.Lib` assembly (no FSI type-identity conflict), and pins `MathNet.Numerics.FSharp 5.0.0` to match the GenForm build (`BigRational` identity). |
| **How to run** | `cd src/Informedica.NLP.Lib/Scratch && echo 'Run.runPhase5 ();;' \| dotnet fsi --use:ftk_extract_v2.fsx` (needs Drive ADC auth). Use `--use:` (not `#load` of the script from a wrapper — FSI does not expose a `#load`ed script's modules and the nested nuget refs trip NU1504). `Run.runPhase5Diag ()` for the stage trace. |

Pass 5 (validation) is **not** a column-filling pass and has no `Ux` checkpoint — it is a validation/preview that the Pass-4 artefact ingests cleanly as real `DoseRule` records, ahead of the TBD downstream typed-emit step (§8).

> **Structurally blind to `IsAdult` — a clean Pass 5 is NOT safety clearance.** Pass 5 bridges the **legacy** GenFORM parser, which has no `IsAdult` field (`Phase5Pure.getDataFromTsv` mirrors `DoseRule.getData` field-for-field and never reads the column). An `IsAdult = "x"` row therefore parses *because* the facet is silently dropped, and renders as an **age-unbounded rule that matches every patient** (its `MinAge`/`MaxAge` were blanked by the clearing guard). A clean Pass 5 render carries **no** assurance for such rows — they remain gated by the §8 blocking precondition until GenFORM consumes the facet. This is inherent to bridging the legacy parser, not a Pass 5 defect. See the CONTEXT.md example dialogue.

> **Tracked hazard — silent rule drop in production ingest (maintainer task, out of scope for this branch).** `DoseRule.getFromGetData` partitions rows into Ok/Error and discards the Error rows **with no log**; `processDoseRuleData` builds a "no products found" warnings dict it **never returns**. In a CDSS this is a completeness hazard: a dose rule silently dropped at ingest means a clinician sees fewer/no options with no signal (violates the `core-domain.md` completeness guarantee). Pass 5's `diagnose` path only *exposes* this on demand — production still swallows it. Fixing the production swallow is a separate maintainer-owned `.fs` change with its own risk surface; it must be carried as a risk-register entry / tracked issue so it is not lost behind an opt-in diagnostic.

### 6.6 Verbatim invariant

Each pass's text output is a strict substring of the previous pass's output (and ultimately of the original `<doseringen>` text). Client-side touches content only via unit canonicalisation and numeric conversion (days / grams / m²); the original Dutch tokens are recoverable from the per-generic audit JSON.

## 7. TSV checkpoint format

Column set is the live `DoseRules` Google Sheet header (read at script-load time by `Tsv.canonicalColumns`). Three columns are extraction-only inputs (filled by Pass 1 / 2 from NuExtract output or by the user, then dropped by the downstream typed-emit step (TBD, §8) before the production `doserules.tsv` emit):

- **`PatientText`** — verbatim Dutch PC text. Filled by Pass 1, read by Pass 3's PC sub-call.
- **`ScheduleText`** — verbatim Dutch dose-schedule. Filled by Pass 1, overwritten by Pass 2 with the per-DoseType slice, read by Pass 3's ScheduleForm + Limits sub-calls.
- **`CmpBased`** — granularity flag. Emitted blank by Pass 1 / 2, **filled by the user during U2** (typically `"x"` for component-based, empty for substance-based). Phase 3 reads it to fan out rows; missing column ⇒ fails fast with `required column 'CmpBased' not in Pass-2 TSV header`.

User-filled product axes (`Form`, `Brand`, `GPKs`) are filled at **U1**, right after Pass 1, because they identify the specific dose rule and feed the GrpId hash. `Dep` (department) is part of L4 PatientCategory, also filled at U1 where applicable; it can be left empty when the source rule has no department scope.

`IsAdult` is an **optional, system-resolved, positive-only** column (preliminary keyword at Pass 1, confirmed at Pass 3; not a U3 user task). It enters the GrpId/Id hash via `Tsv.cellAtOpt`, so a header that lacks the column is tolerated (empty slot). **Clearing invariant:** when `IsAdult = "x"` the rule's `MinAge`/`MaxAge` cells are blank (age irrelevant). Absence of the flag asserts nothing.

`DoseType` is a Phase-2 output (canonical enum `once` / `onceTimed` / `discontinuous` / `timed` / `continuous` / `unfinished`) that is **also a Pass-3 required column**: read by `Phase3Pure.limitsKindOf` to dispatch each row to the matching per-LimitsKind limits template. Missing or unknown values fall through to the `CatchAll` kind. Unlike the three extraction-only columns above, `DoseType` is a real domain field — preserved through the downstream typed-emit step into the production output.

Per-pass column-fill detail lives in §6.1 / §6.2 / §6.3 / §6.4 ("TSV columns filled" / "still blank" rows). Field-level source of truth: `DoseRuleData` (`src/Informedica.GenFORM.Lib/Types.fs:359-411`); Pass 3 Limits target: `DoseLimit` (`Types.fs:264-284`).

## 8. Downstream (TBD)

Pass 3 is terminal for the LLM-driven pipeline; Pass 4 is a deterministic ID-assignment pass with no NuExtract spend. Remaining steps:

- **Downstream typed-emit (TBD, unnamed).** Drop the extraction-only columns (`PatientText`, `CmpBased`); quarantine `unfinished` rows from Pass 2; emit `data/sources/Rules/doserules.tsv`. Note: the `string` → `Gender` DU / `string` → `Informedica.GenUnits.Lib.Units.*` / `float`/`int` → `BigRational` elevation is **already performed by the production `DoseRule` parser** (`DoseRule.getData`), which Pass 5 (validation) exercises against the Pass-4 TSV — so this step is column-drop + quarantine, not a separate primitive-validation pass.
- **GenFORM ingest.** Standard pipeline downstream of `doserules.tsv`.

> **Blocking precondition — `IsAdult` in GenFORM `.fs` source (not yet implemented).** `IsAdult` does not exist in any GenFORM `.fs` source today (`DoseRuleData` in `Types.fs`, the `DoseRule.getData` sheet parser, and the `PatientCategory` domain type all use numeric age ranges only). Because the extraction pipeline empties `MinAge`/`MaxAge` when `IsAdult = "x"`, such a row has **no age bound and an unconsumed flag** — it would match every age and violate the safety-by-construction / completeness guarantees in [`core-domain.md`](../domain/core-domain.md). Therefore **no `IsAdult = "x"` row may reach GenFORM ingest** until the maintainer either (a) carries `IsAdult` as a `bool option` on `DoseRuleData` parsed in `DoseRule.getData` **and** patient-matching enforces "adult patients only" for such rules, or (b) treats the flag as a typed-emit-time assertion that the age columns are empty *with an equivalent matching guard*. This is owned by the maintainer per the script-only policy — and is a *different*, not-yet-made `.fs` change from the `DoseRule.getFromGetData` seam that Pass 5 (validation) already depends on (see §6.5). **Pass 5 (validation) cannot catch this** — it is structurally blind to `IsAdult` (bridges the legacy parser; see §6.5), so an `IsAdult = "x"` row renders cleanly as age-unbounded with no warning. A green Pass 5 is not evidence such a row is safe to ingest.

## 9. Implementation hooks

All modules live in the extraction script (`ftk_extract_v2.fsx`). Its `#r` preamble references one consistent DLL set from the **GenForm bin** (so Pass 5's parser and the rest of the script share a single `Informedica.Utils.Lib` assembly) and pins `MathNet.Numerics.FSharp 5.0.0` to match that build — see §6.5 "Dependencies".

| Module | Key functions / values |
|---|---|
| `Init` | `Informedica.Utils.Lib.Env.loadDotEnv ()` at script-load time so `GENPRES_URL_ID`, `NUEXTRACT_API_KEY`, etc. are available without per-call env juggling. |
| `Types` | `Block` (decomposed source-document node), `GenericFile` (canonical generic + filename stem). |
| `JObject` | `getJsonString`, `getJsonFloat`, `setJsonStr`, `setOpt`, `setJsonIntOpt`, `setJsonFloatOpt` — single canonical place for the null / null-token / value pattern shared across modules. |
| `NuExtract` | `createProject`, `extractText`, `deleteProject`, `getJobStatus`, `getJobResult` (Bearer auth from `NUEXTRACT_API_KEY`); shared `httpClient` with a 5-minute HTTP timeout; private `parseJobStatus` / `isTerminalJobStatus` for the polling loop. Note: the per-job completion poll inside `extractText` has its own shorter cap of `maxWaitMs = 120_000L` (~120 s) — a job that does not reach a terminal status within ~120 s is abandoned well before the 5-minute HTTP client timeout. |
| `AsyncThrottle` | `parallelDegree` (read once from `FTK_EXTRACT_PARALLEL`, default 8), `parallelThrottled` (semaphore-bounded `Async.Parallel`). |
| `Drive` | `createService` (ADC), `escapeQ`, `tryFindFolder`, `findOrCreateFolder`, `resolveFolderPath`, `resolveTargetFolder` (env override `GENPRES_DRIVE_FOLDER_ID` or `defaultFolderPath = ["GenPRES"; "data"; "extraction"]`), `uploadTsvAsSheet`, `upload`, `findLatestSheetByPrefix`, `downloadSheetAsTsv`, `downloadLatestByPrefix` (find-latest + temp-export combinator). |
| `Project` | `withProject` (single-project create / run / best-effort-delete combinator) and `withProjects` (N-project version threading a `Map<'k, projectId>` into the body) — used by Phase1 / Phase2 / Phase3 to hoist the create-iter-delete boilerplate. |
| `Ftk` | `decompose`, `decomposeFromGeneric`, `decomposeDosering`, `decomposeOtherChild`, `headerOfDosering`, `leeftijdsBlocks`, `renderNode`, `writeStructuredText`, `normalizeWhitespace`, `parseXmlIgnoringDtd`, `readXmlIgnoringDtd`, `readXml`, `readDoseringen`, `xmlPath`, `xmlExists`, `listGenerics` (resolves XML under `data/sources/FTK/FK/Teksten/preparaatteksten`). |
| `PostProcess` | `postProcess` (indication forward-fill across the `doses` array) + `defaultOralRoute` (default missing routes to `ORAAL` when no parenteral / topical keyword appears in the source). Both pure JSON rewriters; both return rewritten JSON + counters. |
| `Extract` | `renderXmlForExtraction`, `parseDosesArray`, `wrapDoses`, `applyPostProcessChain`, `callOnce`, `runUnchunked` — Phase-1 unchunked driver decomposed into pure renderer / parser / post-process + a thin effectful single-call. |
| `Tsv` | `canonicalColumns` (live `DoseRules` sheet header), `columns`, `header`, `flattenCell`, `rowFromRecord`, `composeOriginalText`, `isAdultByKeyword`, `openWriter`, `writeAll`, `getStr` (alias for `JObject.getJsonString`); shared column-index plumbing: `Indices` record (with optional `IsAdultIdx`), `requiredColumns` (master list; `IsAdult` deliberately excluded → optional), `indexOf`, `assertColumns`, `resolveIndices`, `cellAt`, `cellAtOpt` (used by every Phase{N}Pure module). |
| `Schema` | Shared sheet helpers `doseRulesSheet : Lazy<string[][]>` (one HTTP GET) and `unitField : string -> JToken`; `loadInstructions` reader; `instructionsDir`. Templates: `pcTemplate` (17 fields incl. `isAdult`), `scheduleFormTemplate`, `onceTemplate`, `onceTimedTemplate`, `discFreqTemplate`, `discIntTemplate`, `timedFreqTemplate`, `timedIntTemplate`, `continuousTemplate`, `limitsTemplate` (catchAll), `nuExtractFlatTemplate` (Pass 1), `nuExtractDoseTypeTemplate` (Pass 2). Instructions (one per active template): `ftkInstructions`, `doseTypeInstructions`, `pcInstructions`, `scheduleFormInstructions`, `<kind>Instructions`, `limitsInstructions`. User-checkpoint banners: `nextStepsPass1` / `nextStepsPass2` / `nextStepsPass3` / `nextStepsPass4` (loaded from `instructions/next-steps-passN.md`, printed by `UserSteps.print`). **Unused / legacy:** `limitsSlimTemplate` and `limitsSlimIntervalInstructions` are defined but not referenced by `Phase3.projectSpecs`; they are inert until reactivated. |
| `Phase1Pure` / `Phase1` | Pure: `rowsFromExtractionJson`, `Bundle` record. Effectful shell: `extractToTsv`, `runOneGeneric`, `partitionByExistence`, `writeOutputs` — uses `Project.withProject` + `AsyncThrottle.parallelThrottled`. |
| `Phase2Pure` / `Phase2` | Pure: `canonicalizeDoseType`, `parseDoseTypeRowsJson`, `synthesiseDoseTypeRows`, `applyDoseTypeRowToRow`, `groupResultsByGeneric`, `buildAuditEntry`; types `Pass1Row`, `Pass2DoseTypeRow`, `Pass2RowResult`, `Pass2RowOutcome`; `requiredColumns` / `resolveIndices` (thin wrapper over `Tsv.resolveIndices`); `Indices = Tsv.Indices` re-export. Effectful shell: `extractFromDisk`, `extractFromDrive`, `extractToTsv`, `upload`, `runRow`, `runOneRow`, `writeOutputs`, `readPass1Tsv` — uses `Project.withProject`. |
| `Phase3Pure` / `Phase3` | Pure: `parseScheduleForm`, `limitsKindOf`, `labelOfKind`, `scheduleFormLabel`, `canonicalize`, `canonical{Genders,DoseUnits,AdjustUnits,TimeUnits}`, `daysPerUnit`, `gramsPerWeightUnit`, `convertAgePairToDays`, `convertWeightPairToGrams`, `convertBsaPairToM2`, `parseFreqs`, `extractFreqIntsFromSchedule`, `augmentFreqsFromSchedule`, `validateMinMax`, `readRawPc`, `convertRawPc`, `extractLimitElement` (with `validateMinMax` + AdjustUnit pairing guard; emits `ExtractedFor`), `buildLimitsPayload`, `checkExtractedFor`, `resolveSubstanceHint`, `resolveIsAdultCell` (positive-only), `expandRowsByGranularity`, `applyPcToCells` (resolves `IsAdult` then runs the clearing guard: `IsAdult="x"`⇒blank `MinAge`/`MaxAge`), `applyLimitsToCells`, `applyExpansionToCells`, `assembleRowCells`, `buildAuditEntry`, `fmtIntOpt`, `fmtFloatOpt`, `requiredColumns` / `resolveIndices`; types `PatientCategoryFields` (carries `IsAdult`), `DoseLimitFields` (carries `ExtractedFor`), `Pass3RowResult` (carries `LimitsLabel` + `ScheduleForm`), `Expansion`, `ScheduleForm`, `LimitsKind`. Effectful shell: `extractFromDisk`, `extractFromDrive`, `extractToTsv`, `upload`, `runPcCall`, `runLimitsCall` (with inline `checkExtractedFor` cross-check vs substance hint), `runScheduleFormCall`, `runOneExpansion`, `getOrCallPc`, `getOrCallScheduleForm`, `getOrCallLimits`, `makeCaches`, `projectSpecs`, `labelForKind`, `writeOutputs`, `Caches`, `ExpansionOutcome` — uses `Project.withProjects`. |
| `Phase4Pure` / `Phase4` | Pure: `normaliseField`, `normaliseGpks`, `sha1Short`, `buildGrpFields`, `buildIdFields`, `assignIds`, `requiredColumns` / `resolveIndices`, `buildAuditEntry`; `Indices = Tsv.Indices` re-export. Effectful shell: `extractToTsv`, `extractFromDisk`, `extractFromDrive`, `upload`. |
| `Phase5Pure` / `Phase5` | Pure: `getDataFromTsv` (mirrors `DoseRule.getData`; local Pass-4 TSV → `Result<DoseRuleData[], Message list>`). Effectful shell (GenFORM parser bridge — no NuExtract): `parseToDoseRules` (override `Resources.defaultResourceConfig`'s `GetDoseRules` with `DoseRule.getFromGetData getDataFromTsv <pass4Tsv>`, then `loadAllResourcesWithConfig`), `run` (render per-generic via `DoseRule.Print.toMarkdown` + write `<stem>_doserules.md` next to the script), `runFromDrive` (pull latest `<stem>_pass4` Sheet then `run`), `diagnose` / `diagnoseFromDrive` (stage-by-stage replay exposing the `getFromGetData` Ok/Error swallow and the `processDoseRuleData` "no products found" warnings). |
| `UserSteps` | `print` — emits a delimited next-step banner (loaded from `Schema.nextStepsPassN`) to the console at the end of each `Run.runPhaseN`. |
| `Run` | `sampleGenerics`, derived bindings (`firstSample`, `generic`, `pathStem`, `passNTsv`, `passNJsonDir`); `runPhase1` (gated on `FTK_EXTRACT_RUN=1`; optional Drive upload on `FTK_EXTRACT_UPLOAD=1`), `runPhase2`, `runPhase3`, `runPhase4`, `runPhase5` (Drive-sourced GenFORM DoseRule render + `<stem>_doserules.md`), `runPhase5Diag` (stage-by-stage parse trace). |

### Driver bindings (FSI session)

The script auto-defines, from the first entry in `Run.sampleGenerics`:

```fsharp
let firstSample   = sampleGenerics |> List.head             // e.g. { Generic = "carbamazepine"; Filename = "carbamazepine" }
let generic       = firstSample.Generic                     // canonical generic name
let pathStem      = firstSample.Filename                    // filename stem; drives TSV / Drive sheet names
let pass1Tsv      = Path.Combine(__SOURCE_DIRECTORY__, $"{pathStem}_pass1.tsv")
let pass1JsonDir  = "/tmp/ftk-extract-pass1"
let pass2Tsv      = Path.Combine(__SOURCE_DIRECTORY__, $"{pathStem}_pass2.tsv")
let pass2JsonDir  = "/tmp/ftk-extract-pass2"
let pass3Tsv      = Path.Combine(__SOURCE_DIRECTORY__, $"{pathStem}_pass3.tsv")
let pass3JsonDir  = "/tmp/ftk-extract-pass3"
let pass4Tsv      = Path.Combine(__SOURCE_DIRECTORY__, $"{pathStem}_pass4.tsv")
let pass4JsonDir  = "/tmp/ftk-extract-pass4"
```

Drive Sheet names follow `<pathStem>_passN_<yyyyMMdd-HHmmss>` and are picked up across passes by `Drive.findLatestSheetByPrefix svc folderId "<pathStem>_passN"` (sorted by `modifiedTime desc`, so an in-place edit of an existing Sheet is what later passes see). `pathStem` and `generic` are usually identical, but kept separate so generics with `/` (multi-substance) or other unsafe filename characters can pick a sanitised stem.

> **Note on parameter naming:** `Phase2.extractFromDrive`, `Phase3.extractFromDrive`, and `Phase4.extractFromDrive` declare their first parameter as `generic` in code, but `Run.runPhase2 / 3 / 4` always pass `pathStem`. The Drive prefix is therefore `<pathStem>_passN`, regardless of how the parameter is named at the call site.

### Per-generic audit JSON

Each pass writes a per-generic JSON dump to its `passNJsonDir`:

- Pass 1: `<filename>.pass1.json` — the post-processed `{"doses": [...]}` JSON (after indication forward-fill + default-Oraal). Raw NuExtract output is not preserved; post-process counters (`Filled`, `Defaulted`) are emitted to stdout via `printfn` only, not into the JSON file.
- Pass 2: `<generic>.pass2.json` (one entry per Pass-1 row: `inputRowIndex`, `originalScheduleText`, `doseTypeRows`, `failures`)
- Pass 3: `<generic>.pass3.json` (one entry per output expansion: `inputRowIndex`, `expansionIndex`, `cmpBased`, `substances`, `component`, `substance`, `originalPatientText`, `originalScheduleText`, `patientCategory`, `limits` (carries the model's self-declared `extractedFor` label as a top-level key), `limitsKind` (lower-camel label via `Phase3Pure.labelOfKind`), `scheduleForm` (string or null; `null` for non-disc/timed kinds), `failures` (includes `ExtractedFor=...` cross-check notes when the model's self-declared label disagrees with the upstream substance hint))
- Pass 4: `<generic>.pass4.json` (one entry per output row: `inputRowIndex`, `id`, `grpId`, `sortNo`, `doseType`, `grpFields[]` (the normalised key segments hashed into GrpId), `idFields[]` (the normalised key segments hashed into Id), `generic`)
- Pass 5: no JSON. `Phase5.run` writes a single markdown artefact `<stem>_doserules.md` **next to the script** (`src/Informedica.NLP.Lib/Scratch/`) — the rendered `DoseRule` records, one generic section after another. It is a generated artefact (untracked under the opt-in `.gitignore`, like `<stem>_pass4.tsv`). `Phase5.diagnose` prints its stage trace to stdout only.

## 10. References

- **Live implementation**: the FTK extraction FSI script (`ftk_extract_v2.fsx`) — leading point for the whole pipeline.
- **NuExtract prompts**: [`docs/data-extraction/instructions/`](instructions/) — `phase1-ftk.md`, `phase2-dose-type.md`, `phase3-patient-category.md`, `phase3-schedule-form.md` (classifier), `phase3-dose-limits.md` (CatchAll), `phase3-dose-limits-once.md`, `phase3-dose-limits-once-timed.md`, `phase3-dose-limits-disc-freq.md`, `phase3-dose-limits-disc-int.md`, `phase3-dose-limits-timed-freq.md`, `phase3-dose-limits-timed-int.md`, `phase3-dose-limits-continuous.md`. `phase3-dose-limits-slim-interval.md` exists on disk but is **legacy / unused** — its loader (`Schema.limitsSlimIntervalInstructions`) and `Schema.limitsSlimTemplate` are defined but not referenced by `Phase3.projectSpecs` (see §9). Edit-and-reload picks up changes.
- **Per-checkpoint user instructions**: [`instructions/next-steps-pass1.md`](instructions/next-steps-pass1.md), [`next-steps-pass2.md`](instructions/next-steps-pass2.md), [`next-steps-pass3.md`](instructions/next-steps-pass3.md), [`next-steps-pass4.md`](instructions/next-steps-pass4.md) — canonical U1..U4 checkpoint text, loaded eagerly via `Schema.nextStepsPassN` and printed to the console by `UserSteps.print` at the end of each `Run.runPhaseN`.
- [`drive-upload-setup.md`](drive-upload-setup.md) — one-time ADC auth setup (`gcloud auth application-default login`) used by `module Drive`.
- `src/Informedica.GenFORM.Lib/Types.fs:359-411` — `DoseRuleData` (canonical column source of truth).
- `src/Informedica.GenFORM.Lib/Types.fs:264-284` — `DoseLimit` (Pass 3 Limits target).
- `src/Informedica.GenFORM.Lib/DoseType.fs:46-98` — `DoseType.fromString` / `toDescription` (canonical `doseType` enum mirrored by Pass 2).
- `src/Informedica.GenFORM.Lib/DoseRule.fs` — `getFromGetData` / `getData` / `processDoseRuleData` / `mapToDoseRule` / `addDoseLimits` / `Print.toMarkdown` / `Print.printGenerics`; the production parser Pass 5 (§6.5) bridges into. `Resources.defaultResourceConfig` / `loadAllResourcesWithConfig` in `src/Informedica.GenFORM.Lib/Api.fs`.
- `data/sources/Rules/doserules.tsv` — final downstream typed-emit target (TBD, §8).
- [`src/Informedica.GenFORM.Lib/DoseRuleData.fs`](../../src/Informedica.GenFORM.Lib/DoseRuleData.fs) — `headers` (canonical DoseRules column order) and `parseDoseRuleData`, including the `IsAdult` column and its clearing invariant.
- [`docs/domain/genform-free-text-to-operational-rules.md`](../domain/genform-free-text-to-operational-rules.md) §3, §5, §6.1, §6.2, **Addendum C.2** (DoseRule field spec; canonical source for `Gender = male / female` etc.).
- [`docs/domain/core-domain.md`](../domain/core-domain.md) — OKRs and rule hierarchy.
