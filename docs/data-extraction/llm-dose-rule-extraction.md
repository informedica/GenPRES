# LLM-Based Dose-Rule Extraction Pipeline

> Formerly ADR-0018 (proposed 2026-04-26). Moved out of `docs/adr/` under issue #411 because it describes a data-preparation pipeline that is still evolving, not a hard-to-reverse decision; see [ADR-0000](../adr/0000-documentation-rules.md).

**Date**: 2026-04-26

**Status**: Proposed

**Related PRs**:

- [#317 — DoseRuleExtract.fsx pipeline overhaul](https://github.com/informedica/GenPRES/pull/317)
- [#321 — Improve dose-rule extraction prompt and flowchart](https://github.com/informedica/GenPRES/pull/321)

## Context

GenPRES dose rules are sourced from a Google Spreadsheet managed by clinical
pharmacists. The source material for these rules comes from Dutch-language
reference publications — primarily:

- **NKF** (Nederlands Kinderformularium) — the Dutch national pediatric
  formulary, published by the Erasmus MC; contains free-text dosing schedules
  per age band, weight band, and indication
- **FTK** (Farmacotherapeutisch Kompas) — the Dutch national drug reference,
  used widely by adult-care prescribers; structured similarly but in adult
  language conventions

Each source document contains dose schedules written in **natural Dutch prose**
with embedded numeric ranges, unit specifications, dose-type labels (start /
maintenance / maximum), and patient-category constraints. Manual extraction of
this information into the spreadsheet is:

- **Slow**: a single drug entry can require 30–60 minutes of careful reading
- **Error-prone**: unit misreadings and missed conditions are the most common
  mistake class
- **Inconsistent**: different pharmacists apply different conventions for edge
  cases such as weight-adjusted doses, continuous infusions, and reconstitution
  steps

As of Q1 2026 the spreadsheet contains dose rules for approximately 350
generics. Expanding coverage to the full NKF/FTK corpus requires a more
scalable extraction approach.

### Previous approach

Dose rules were entered entirely by hand, with no automated tooling. The only
tooling that existed was a `DoseRuleExtraction.fsx` stub script that read the
prompt markdown and called an Ollama model with unstructured text output,
requiring manual JSON cleanup.

### Forces

- GenPRES is MDR-regulated; any data that drives dose recommendations must be
  traceable to a validated source and reviewed by a competent person before use
  in a live deployment
- The repository policy (`AGENTS.md`) prohibits direct LLM writes to `.fs`
  source files and dose-rule spreadsheets without human review
- The extraction pipeline is used during *data preparation*, not at *run time*;
  the LLM output never reaches a running GenPRES instance without passing
  through the human review gate

## Decision

Implement a multi-stage, LLM-assisted dose-rule extraction pipeline as a set of
FSI scripts in `src/Informedica.NLP.Lib/Scripts/`. The pipeline takes free-text
Dutch dose schedules as input and produces structured `DoseRuleExtracted` JSON
objects that are validated, reviewed interactively by a pharmacist, and then
transcribed into the GenFORM spreadsheet.

The pipeline is **not** integrated into the production runtime. It is a data
preparation tool only, operating offline under human supervision.

### Key design choices

| # | Choice | Rationale |
|---|--------|-----------|
| 1 | Structured JSON output schema validated with NJsonSchema | Eliminates ambiguous free-text output; allows automated validation before human review |
| 2 | Hierarchical schema: `DoseRuleExtracted → doseTypes[] → doseLimits[]` | Mirrors the GenFORM shape; a single LLM call per schedule text (age/weight band) produces one complete record |
| 3 | Multi-pass pipeline: extract → grammar check → validate → interactive REPL | Each stage has an independent exit/save point; a pharmacist can stop and correct at any step without restarting |
| 4 | Mandatory human review gate before spreadsheet entry | Safety requirement: AI-generated clinical data must be reviewed by a competent person; the pipeline has no automated write path |
| 5 | Provider-agnostic sender abstraction | Allows Ollama (local, offline), OpenAI GPT-4o/o3-mini, and Fireworks Llama-4 to be swapped with a single parameter; each has different cost/latency/accuracy trade-offs |
| 6 | JSON ↔ TSV round-trip via `Conversion.toTsv` / `Conversion.fromTsv` | Extracted output can be saved as a TSV row matching `data/sources/Rules/doserules.tsv`; enables automated benchmark scoring against ground-truth rows |
| 7 | Recursive validation-retry loop (up to 2 attempts) | LLMs occasionally produce malformed JSON; a single automatic retry with the error message reduces the failure rate without requiring manual intervention |
| 8 | FSX script only; no source-file modifications | Follows `AGENTS.md` script-only policy for all new features; migration to `.fs` deferred to human reviewer |

### Pipeline architecture

```
                       ┌─────────────────────┐
                       │  NKF / FTK          │
                       │  free-text schedule │
                       └──────────┬──────────┘
                                  │ scheduleText
                                  ▼
                       ┌─────────────────────┐
                       │  Stage 1: Extract   │
                       │  LLM JSON call      │
                       │  (with retry ≤ 2)   │
                       └──────────┬──────────┘
                                  │ DoseRuleExtracted (raw)
                                  ▼
                       ┌─────────────────────┐
                       │  Stage 2: Grammar   │
                       │  checkGrammar LLM   │
                       │  call (Ollama)      │
                       └──────────┬──────────┘
                                  │ DoseRuleExtracted (cleaned)
                                  ▼
                       ┌─────────────────────┐
                       │  Stage 3: Validate  │
                       │  RuleValidation     │
                       │  .validate          │
                       └──────────┬──────────┘
                                  │ DoseRuleExtracted (validated)
                                  ▼
                       ┌─────────────────────┐
                       │  Stage 4: Review    │
                       │  Interactive REPL   │
                       │  (Pharmacist)       │
                       └──────────┬──────────┘
                                  │ Saved / Aborted
                                  ▼
                       ┌─────────────────────┐
                       │  Stage 5: Transcribe│
                       │  TSV row →          │
                       │  doserules.tsv      │
                       └─────────────────────┘
```

At each stage the pharmacist sees the current record state and chooses:

- **(P) Proceed** — advance to the next stage
- **(S) Save and exit** — save the record as-is and stop (useful when the
  current stage output is correct but subsequent stages would lose information)
- **(E) Exit** — discard and abort (for records that cannot be salvaged)

### LLM provider selection

| Provider | Model | Notes |
|----------|-------|-------|
| **Ollama** (local) | `qwen3-coder:30b` | Default; offline; requires ≥ 24 GB VRAM; no API key needed |
| **OpenAI** | `gpt-4o`, `o3-mini` | Best accuracy; requires `OPENAI_API_KEY`; incurs cost |
| **Fireworks** | `llama-4-*` | Alternative remote option; requires `FIREWORKS_API_KEY` |

The sender is passed as a parameter to `Extraction.extractDoseRule`, allowing
the caller to switch providers without changing the pipeline.

### Structured schema (abridged)

> **Superseded.** The record shapes below are the April 2026 design. The live output
> contract of `DoseRuleExtract.fsx` — dose types `once` / `onceTimed` / `discontinuous` /
> `timed` / `continuous`, and the full `Min*`/`Max*` quantity, per-time and rate fields — is
> [`doserule-extraction-prompt.md`](doserule-extraction-prompt.md) §3.

```fsharp
type DoseLimit = {|
    ``component``: string    // drug component name (e.g. "Paracetamol")
    substance: string        // active substance (e.g. "paracetamol")
    doseUnit: string         // e.g. "mg", "IE", "mmol"
    adjustUnit: string       // e.g. "kg", "m2", ""
    rateUnit: string         // e.g. "/dag", "/keer", ""
    minQty: float option
    maxQty: float option
    minQtyAdj: float option  // weight/BSA-adjusted minimum
    maxQtyAdj: float option
    minPerDose: float option
    maxPerDose: float option
    minTotal: float option   // total-dose minimum
    maxTotal: float option
|}

type DoseType = {|
    doseType: string         // "start" | "maintenance" | "max"
    doseText: string         // raw text for the dose type section
    doseLimits: DoseLimit[]
|}

type DoseRuleExtracted = {|
    scheduleText: string     // normalised source text
    gender: string           // "M" | "F" | ""
    minAge: float option     // days
    maxAge: float option     // days
    doseTypes: DoseType[]
|}
```

The JSON output schema is derived from these anonymous record types via
`NJsonSchema.JsonSchema.FromType<DoseRuleExtracted>()` and embedded in the
extraction prompt so the LLM knows exactly what to produce.

### Benchmark scoring

`DoseRuleTests.fsx` contains a growing set of ground-truth test cases with
expected `DoseRuleExtracted` values. After each extraction run, the benchmark
compares the LLM output against the expected values and reports:

- **Exact match** — field values match within tolerance
- **Partial match** — some fields match, others differ
- **Miss** — field is absent or completely wrong

This allows tracking extraction quality across model and prompt versions.

## Consequences

### Positive

- Pharmacists can extract dose rules 5–10× faster than manual entry.
- The pipeline produces consistently structured output; unit misreading is
  caught by the grammar-check and validation stages.
- Provider-agnostic design allows cost/accuracy trade-offs to be made at the
  call site.
- Benchmark scoring enables systematic prompt improvement.
- No production runtime changes; extraction errors cannot reach a live deployment.

### Negative / Trade-offs

- Requires a running LLM server (Ollama locally or an API key); not available
  in the standard CI environment.
- The 30B Ollama model requires significant hardware; smaller models have
  noticeably worse extraction accuracy on edge cases.
- Grammar-check and validation stages add latency (typically 30–90 s per record
  with a local 30B model).
- The pipeline is not a source file; it cannot be directly called from tests or
  the production server.

### Safety constraints

This pipeline handles **data preparation only**, not run-time clinical
decisions. The following safety constraints apply:

1. **No automated write path**: The pipeline cannot modify `doserules.tsv` or
   the Google Spreadsheet without a pharmacist completing Stage 4 (interactive
   review) and manually transcribing the TSV row.

2. **Provenance annotation**: Every rule extracted via this pipeline must be
   annotated with `Source = "NKF"` or `Source = "FTK"` (as appropriate) when
   entered into the spreadsheet, so the origin is traceable in the formulary
   view.

3. **Competent reviewer required**: The interactive REPL is designed for use by
   a pharmacist or clinical pharmacologist who can verify numeric values,
   units, and patient-category constraints against the original source text.

4. **Validation stage is not sufficient alone**: `RuleValidation.validate`
   performs structural checks (required fields present, units recognized) but
   cannot verify clinical correctness. Human review remains mandatory.

5. **No PII**: The input text is drawn from published reference works; no
   patient-identifiable information is processed by the pipeline.

## References

- [DoseRuleExtract.fsx — full pipeline](../../src/Informedica.NLP.Lib/Scripts/DoseRuleExtract.fsx)
- [DoseRuleExtractInteractiveDemo.fsx — interactive REPL demo](../../src/Informedica.NLP.Lib/Scripts/DoseRuleExtractInteractiveDemo.fsx)
- [DoseRuleTests.fsx — benchmark test cases](../../src/Informedica.NLP.Lib/Scripts/DoseRuleTests.fsx)
- [DoseRuleValidation.fsx — validation logic](../../src/Informedica.NLP.Lib/Scripts/DoseRuleValidation.fsx)
- [doserule-extraction-prompt.md](doserule-extraction-prompt.md)
- [PR #317 — NLP pipeline overhaul](https://github.com/informedica/GenPRES/pull/317)
- [PR #321 — Extraction prompt improvement](https://github.com/informedica/GenPRES/pull/321)
- [ADR-0002: MCP Server Architecture](../adr/0002-mcp-server-architecture.md)
- [Implementation plan for issue #307: G-Standaard dose rule fallback](../implementation-plans/307-gstand-dose-rule-fallback.md) (formerly ADR-0016)
- [Nederlands Kinderformularium](https://www.kinderformularium.nl/)
- [Farmacotherapeutisch Kompas](https://www.farmacotherapeutischkompas.nl/)
