# Data Extraction — Glossary

The bounded context that turns free-text formulary source (FTK, NKF, …) into
the canonical `DoseRules` TSV. It is currently a **Scratch prototype**
(`src/Informedica.NLP.Lib/Scratch/Informedica.NLP.Lib.fsx`) of the end-result
pipeline; it edits no source. Owns the extraction-pipeline vocabulary only;
clinical-domain terms (Patient Category, OKR, Dose Rule, …) are owned by the
**Core Domain context** — see [`docs/domain/core-domain.md`](../domain/core-domain.md).
This file records terms sharpened during design grilling.

## Language

**Phase** (a.k.a. extraction phase, Pass N):
One step of the **structuring process**. There are exactly N extraction phases
(`Pass 1 … Pass 5`); `Phase{N}` / `Phase{N}Pure` is the code module that
implements `Pass N`. The phases progressively turn free text into a
hierarchical structure (the [[Structuring hierarchy]]), flatten it to one
`DoseRuleData` row per root-to-leaf path, and validate the result against the
`DoseRule` domain model ([[Pass 5 (validation)]] is that validation step).
"Phase" *only* ever means an extraction phase.
_Avoid_: **dose phase / DosePhase** — meaningless; there is no "phase of a
dose". The only first-class things are extraction phases (the process) and
validated Dose Rules (the output). _Avoid_: treating "Phase 3" and "Pass 3"
as different things — the module is just the implementation of the pass.

**Structuring hierarchy** (L0..L7):
The scaffold the [[Phase]]s build: Source → Generic → Indication → Route →
PatientCategory → (DoseType/DoseText) → DoseTarget → DoseLimit. It is
*structuring scaffold, not a set of domain entities* — each level is a
*position* in the free-text→row process, not a thing to name. One TSV row =
one full root-to-leaf path; the hierarchy is flattened, never serialised as a
tree. The L5 level is simply where a row acquires its `DoseType` / `DoseText`
(and its `ScheduleText` is sliced to match) — it is **not** an entity called
"DosePhase".
_Avoid_: naming a level as if it were a domain object (especially "DosePhase").

**Ux checkpoint** (U1..U4):
The manual human edit gate after an extraction [[Phase]]. The user freely
adds / deletes / modifies / **reorders** rows in the TSV; the next phase reads
the edited file. There is no in-app UI and no automated inter-phase diff — the
TSV file *is* the validation gate. Row reordering at a Ux is the lever that
sets [[SortNo]].
_Avoid_: "review step" (it is an authoritative edit, not a read-only review).

**IsAdult**:
An extraction-produced, positive-only boolean facet attached to a rule's
**Patient Category**, asserting "applies to adults, expressed *categorically*
rather than as a numeric age range." `"x"` = confirmed adult; absence asserts
**nothing** (never "not an adult"). Elderly (`ouderen`/`bejaarden`) is
deliberately folded into Adult. A deliberate single exception to the
ranges-only Patient Category model. It is a **prototyping finding**: it does
not yet exist in GenFORM `.fs`. Carrying it into source — so Patient Category
consumes it and matching enforces "adults only" — is a port-time task. Until
then no `IsAdult = "x"` row may reach GenFORM ingest (its `MinAge`/`MaxAge`
are blanked, so it would otherwise match every age).
_Avoid_: AgeCategory, age band, adult flag (it is not a tri-state, does not generalise).

**SortNo**:
The display rank of a rule within its **GrpId**. Its source of truth is the
user's manual row ordering at the [[Ux checkpoint]]s — reordering rows *is* the
lever. Pass 4 only mechanises this: it ranks the distinct `Id`s inside one
GrpId by the first-appearance index of the user-arranged rows. It is **never**
system- or DoseType-derived. Rows sharing an `Id` (multi-substance fan-outs)
share a SortNo.
_Avoid_: "DoseType ordering"/"priority ordering" (SortNo is never
DoseType-derived). _Avoid_: "system-assigned sort order" (the system ranks,
the user orders).

**Downstream typed-emit step**:
The not-yet-built ("TBD") step after Pass 4 that drops the extraction-only
columns (`PatientText`, `CmpBased`), quarantines `unfinished` rows, and emits
the canonical `data/sources/Rules/doserules.tsv`. It does **not** do
string→typed elevation — the production `DoseRule` parser already does that.

**Pass 5 (validation)**:
The optional, terminal, read-only bridge that feeds the Pass-4 artefact
through the production GenFORM `DoseRule` parser to confirm it ingests as real
`DoseRule` records. Unlike Passes 1–4 it extends no artefact and has no
[[Ux checkpoint]]. **Structurally blind to [[IsAdult]]**: it bridges the
*legacy* parser, which has no `IsAdult` field, so an `IsAdult = "x"` row parses
*because* the facet is dropped and renders age-unbounded. A clean Pass 5
result is therefore **not** safety clearance for such rows — they remain a
port-time concern.
_Avoid_: treating "Pass 5 parses" as "ready to ingest" (see Example dialogue).
_Avoid_: the unqualified bare module name "Phase 5" (reserve that for code).

## Relationships

Data Extraction is a **Scratch prototype** of the FTK→`DoseRules` pipeline
(`Informedica.NLP.Lib.fsx`). Its boundary today is the TSV: a prototype edits no
GenFORM `.fs` source. Prototyping findings — `[[IsAdult]]` is the worked
example — are **inputs to a later port into GenFORM source**, where the
`DoseRule` domain model and parser are adjusted to match what the prototype
discovered; safety-critical findings are ADR-gated.
"Never reaches into `.fs`" describes the prototype's data boundary — not a ban
on the prototype shaping the future domain model.

This FTK pipeline is **not** the same thing as the LLM dose-rule extraction pipeline of
[ADR-0018](../mdr/design-history/0018-nlp-dose-rule-extraction.md), which lives in
`src/Informedica.NLP.Lib/Scripts/DoseRuleExtract.fsx` and has its own module structure
(`Config`, `Prompt`, `Extraction`, `Pipeline`, …). Two pipelines target `DoseRules`; the
Pass 1–5 vocabulary defined in this document belongs to the FTK one only.

Note that the Scratch directory is gitignored, so the script is not tracked in the
repository — searching git history for it finds nothing.

## Example dialogue

> **Dev:** "Pass 5 says the TSV parses — are we ready to ingest the
> `IsAdult = "x"` rows?"
> **Maintainer:** "No. Parsing is not matching. Pass 5 bridges the legacy
> parser, which doesn't even read `IsAdult`. Until the port adds the facet to
> Patient Category and matching enforces 'adults only', those rows have empty
> age bounds and would match a neonate. That's a port-time precondition, not a
> parse check."
