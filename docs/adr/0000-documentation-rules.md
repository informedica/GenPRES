# ADR-0000: Documentation Rules

**Date**: 2026-09-05

**Status**: Accepted, amended (2026-09-06)

**Related Issues**: [#411 — Only use ADRs if no other suitable documentation option is available](https://github.com/informedica/GenPRES/issues/411),
[#522 — Reorganize the docs folder](https://github.com/informedica/GenPRES/issues/522)

## Context

By mid-2026 `docs/` held some twenty Architecture Decision Records plus requirements, risk,
usability, validation and post-market documents. A reviewer who read all of the ADRs
([#411](https://github.com/informedica/GenPRES/issues/411)) found that most of them described how
the code was structured at the time of writing, or tracked work in progress. Both kinds go stale
silently: one ADR referred to code that no longer existed on `master`. Reading them cost hours and
made onboarding harder, which is the opposite of what documentation is for.

Two things followed. PR #440 deleted the ADRs that were not decisions. Issue
[#522](https://github.com/informedica/GenPRES/issues/522) then moved the regulatory (MDR, Medical
Device Regulation) documentation out of this repository altogether: it is maintained in a separate,
proprietary MDR documentation repository, and `docs/` here holds technical documentation only.

Neither change wrote the underlying rule down. This ADR does, so that the folder does not refill.

## Decision

### 1. Documentation is the last resort

Guide the reader in this order, and stop at the first level that does the job:

1. **Types** — make illegal states unrepresentable; give APIs distinct types.
2. **Names** — functions and modules whose names say what they do.
3. **Comments** — XML documentation on public APIs, `//` comments for the *why* of an implementation.
4. **Tests** — an illustrative example belongs in an Expecto test, not in prose.
5. **Commit messages** — a code change is explained in the commit that makes it
   (see `.github/instructions/commit-message.instructions.md`).
6. **Documents** — only for what none of the above can carry.

A document that restates what the code, its tests or `git log` already say is a liability, not an asset.

### 2. When to write an ADR

An ADR records a decision that is **hard to reverse**: the programming language, a framework, a
storage mechanism, a third-party dependency, an integration standard, an architectural foundation
(client-server, shared type-safe contract, constraint-solver core). It records *what* was decided,
*why*, and *what else was considered*.

An ADR is **not** written for:

- how the code is structured today — the repository is the authoritative record of its own layout;
- ongoing or planned work — that is an issue, or an implementation plan under
  `docs/implementation-plans/`;
- a code change — that is a commit message;
- a status report, a review, an analysis or a benchmark — those are documents in the folder that
  matches their subject, or better, tests.

### 3. ADR form and lifecycle

- One file per decision in `docs/adr/`, named `NNNN-short-title.md`. Take the next free number.
  Gaps are deliberate (deleted ADRs) and are never refilled.
  *Amended 2026-09-06*: the numbering is contiguous. On that date the surviving ADRs were
  renumbered (0009 → 0002, 0019 → 0003, 0020 → 0004, 0021 → 0005) and every reference outside
  `CHANGELOG.md` was repointed; the changelog keeps the numbers as they were at release time.
  A deleted or merged ADR still leaves no gap to refill: renumber the ones after it and repoint
  their references in the same change. The old number stays mentioned in the renumbered ADR
  so `git log` and old release notes can be followed.
- Spine: `# ADR-NNNN: Title`, `**Date**`, `**Status**`, optional `**Related Issue**` /
  `**Related PRs**`, then `## Context`, `## Decision`, `## Consequences`,
  `## Alternatives considered`, `## References`. Keep it short; link out rather than restating.
- Status is `Proposed`, `Accepted`, `Superseded` or `Deprecated`. A later change to an accepted
  decision is an amendment: a dated `### … — amended YYYY-MM-DD` section in place, as
  [ADR-0005](0005-build-system-versioning-and-release.md) does. A superseded ADR stays as the record
  of a road not taken ([ADR-0004](0004-fhir-r4-integration.md)); it is not deleted.
- There is no hand-maintained index or change log. The folder listing and `git log docs/adr` are the index.

### 4. What lives where in `docs/`

| Folder | Holds |
| ------ | ----- |
| `adr/` | Architecture Decision Records, as defined above |
| `domain/` | The ubiquitous language: Core Definitions, GenFORM / GenORDER / GenSOLVER specifications, algorithm explainers |
| `scenarios/` | Use cases, preferably executable (an `.fsx` model whose trace the diagrams are read off) |
| `data-extraction/` | The dose-rule extraction pipeline: bounded-context glossary, prompt and flowchart specifications |
| `implementation-plans/` | Per-issue plans (`<issue>-<title>.md`, from `template.md`); short-lived, remove or archive when the issue closes |
| `security/` | Security reviews and the baseline in force, updated in place with dated sections |
| `code-reviews/` | Conformance analyses of the code against external standards and references |
| `roadmap/` | Backlog, feature requests and fit-gap analyses that have not yet become issues |
| `literature/` | Research background |
| `user-guide/` | End-user documentation (English and Dutch) and manual test workflows |

What does **not** live here: MDR technical-file artifacts — requirements, risk analysis, usability
engineering, validation reports, post-market surveillance, traceability. Those are maintained in the
separate, proprietary MDR documentation repository. A document in this repository may state that
fact but does not link to it.

### 5. Naming and cross-references

- Root community files are `UPPERCASE.md` (`README.md`, `SECURITY.md`). Everything under `docs/`
  is `lowercase-with-hyphens.md`. No spaces in file names.
- Library and project names use the folder casing in prose: GenFORM, GenORDER, GenSOLVER,
  GenUNITS, GenCORE (`src/Informedica.GenFORM.Lib`). The F# namespace casing
  (`Informedica.GenForm.Lib`) appears only in code spans and code blocks.
- Prose is written in American English. Dutch proper nouns keep their spelling
  (G-Standaard, Kinderformularium); microgram is written `mcg`.
- Link to a file instead of restating its content. Name a source file by path only when the
  reader has to go there.
- Every `docs` change runs the markdown linter (`dotnet run MarkdownLint`) and leaves no
  relative link dangling.

## Consequences

- `docs/adr/` stays small: five decisions at the time of writing (then numbered 0001, 0009, 0019, 0020, 0021; since 2026-09-06 numbered 0001 to 0005),
  each of which would be expensive to reverse.
- Explainers, plans and reviews that used to be ADRs live in the folder that matches their subject
  and are free to change without an amendment ceremony.
- The MDR change log (former ADR-0000) is gone; `git log` is the change log for this repository,
  and the design history file is kept where the regulation is served.
- Reviewers can reject a documentation PR by pointing at a rule in this file instead of arguing
  from taste.

## Alternatives considered

- **Keep the change-log ADR-0000 as an index of ADRs.** Rejected: it duplicated `git log`, its
  reason for existing (the MDR design history file) is served in the MDR repository, and a
  hand-maintained index is exactly the kind of document that goes stale.
- **Renumber the surviving ADRs contiguously.** Rejected at the time: `ADR-0021` was referenced
  from `DEVELOPMENT.md`, the release workflows, `CHANGELOG.md` and source comments; a renumbering
  would break all of them for no gain. Reversed 2026-09-06 (see rule 3): the references were few
  enough to repoint in one change, and a folder of six files numbered 0000, 0001, 0009, 0019,
  0020, 0021 misled readers into looking for the missing fifteen.
- **Write these rules into `CONTRIBUTING.md` instead of an ADR.** Rejected: how the project
  documents itself is a foundational, hard-to-reverse choice, which is what an ADR is for; making
  it the first entry in the folder means it is the first thing a new contributor reads there.

## References

- Mark Seemann, *Code That Fits in Your Head* (2021), on the hierarchy of communication.
- Michael Nygard, [Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions) (2011).
- `.github/instructions/commit-message.instructions.md` — commit message conventions.
- [`docs/README.md`](../README.md) — the folder map.
