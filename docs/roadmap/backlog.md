# GenPRES Backlog

Feature backlog covering the path from data extraction to EMR-integrated, multi-user
production deployment. Items are numbered as originally proposed; the
[Dependency Graph](#dependency-graph) and [Suggested Phasing](#suggested-phasing)
sections below reorder them by what must be built first.

> Status legend: 🔲 Not started · 🚧 In progress · ✅ Done

---

## Dependency Graph

```text
        ┌─────────────────────────────┐
        │ 2. Extend extraction to all │
        │    rule types               │
        └──────────────┬──────────────┘
                       │ (validated rules)
                       ▼
 ┌──────────────┐   ┌─────────────────────────┐
 │ 1. Extraction│──▶│ 3. Publish mechanism    │
 │  & validation│   │    (rules + deps bundle)│
 │  UI          │   └───────┬─────────────────┘
 └──────────────┘           │
                            ▼
                 ┌─────────────────────────┐    ┌──────────────────────┐
                 │ 5. Storage for rule     │    │ 8. AuthN/AuthZ +     │
                 │    publications         │    │    logging           │
                 └───────┬─────────────────┘    └──────────┬───────────┘
                         │                                  │ (cross-cutting:
                         ▼                                  │  gates 4,6,7)
                 ┌─────────────────────────┐               │
                 │ 4. Load publication &   │◀──────────────┘
                 │    attach orders        │
                 └───────┬─────────────────┘
                         ▼
                 ┌─────────────────────────┐
                 │ 6. Storage for running  │
                 │    patient orders       │
                 └───────┬─────────────────┘
                         ▼
                 ┌─────────────────────────┐
                 │ 7. EMR integration      │
                 └─────────────────────────┘
```

Key chains:

- **Data**: 1 + 2 → 3 → 5 → 4 → 6 → 7
- **Cross-cutting**: 8 (AuthN/AuthZ/logging) underpins 4, 6, 7 and should land before any
  persistent or outward-facing state.

---

## Suggested Phasing

| Phase | Items | Theme |
| ----- | ----- | ----- |
| P1 | 1, 2 | Author + validate the full rule set (all rule types) through a UI |
| P2 | 3, 5 | Make rules a versioned, storable artifact |
| P3 | 8 | Identity + audit foundation (needed before persistent orders / EMR) |
| P4 | 4, 6 | Bind orders to a publication; persist running orders |
| P5 | 7 | Integrate with external EMR systems |

---

## Backlog Items

### 1. UI for data extraction and validation 🔲

**Description.** Web UI (Fable/Elmish client) to drive the NLP/AI extraction pipeline
from free text → structured rules, and to review/validate the result before it becomes an
Operational Knowledge Rule (OKR). Surfaces validation warnings/`Messages` that are
currently produced but not displayed.

**Rationale.** Extraction today runs through `.fsx` scripts
(`Informedica.NLP.Lib.fsx`, `DoseRuleExtract.fsx`) with validation messages going to
`Messages` and never surfaced. A human-in-the-loop UI is the safety gate that turns
semi-automated extraction into an auditable workflow — required for MDR.

**Dependencies.** None (entry point). Feeds 2 and 3.

**Affected areas.**
- `src/Informedica.GenPRES.Client/` (new extraction/validation pages — UI is the one
  allowed `.fs` edit zone)
- `src/Informedica.NLP.Lib/`, `Informedica.GenFORM.Lib/` (expose extraction + validation
  as API)
- `src/Informedica.GenPRES.Shared/Api.fs` (new commands/responses)

**Acceptance criteria.**
- Paste/upload free text and see extracted DoseRules side-by-side with source.
- All validation `Messages` surfaced (no silent drops); each flagged item is
  accept/reject/edit-able.
- Validated output exportable to the same TSV/data shape the source pipeline consumes.
- Round-trips through `DoseRule.getFromGetData` without loss.

---

### 2. Extend extraction and validation to other rule types 🔲

**Description.** Generalize the extraction + validation pipeline (today focused on
DoseRule) to **Renal Rule, Solution Rule, Reconstitution Rule, and Formulary**, plus
remaining OKR types.

**Rationale.** A publication (item 3) is only complete if it contains every rule type an
order depends on. DoseRule alone cannot produce a valid order for reconstituted or
solution-based medications.

**Dependencies.** Shares the UI from 1; can proceed per-rule-type in parallel.

**Affected areas.**
- `src/Informedica.NLP.Lib/`, `Informedica.GenFORM.Lib/` (per-rule extractors +
  validators: `SolutionRule.fs`, `RenalRule.fs`, reconstitution in `Product.fs`)
- `src/Informedica.GenFORM.Lib/Types.fs` `Data` records + the column-contract test (column/semantics updates)

**Acceptance criteria.**
- Each rule type has an extractor + validator with surfaced `Messages`.
- Each round-trips through its `getFromGetData`/`toData` (cf. DoseRule roundtrip work).
- Formulary entries extractable and validatable.
- `Data` record comments and the column-contract test updated per rule type.

---

### 3. Publish mechanism: rules + dependencies bundle 🔲

**Description.** Mechanism that snapshots **all** validated rules and their dependencies
(products, mappings, reconstitution, units, formulary) into a single, versioned,
immutable **publication** artifact with a content hash.

**Rationale.** Orders must be reproducible. To attach an order to "the rules it was
created with" (item 4), those rules must exist as a frozen, identifiable unit — not a
live Google Sheet that changes underneath running orders.

**Dependencies.** Needs 1 + 2 (validated rule set across all types). Feeds 4 and 5.

**Affected areas.**
- New publication/bundling module (prototype in a `Scripts/` dir first)
- `src/Informedica.GenFORM.Lib/Api.fs` (`ResourceConfig` is the dependency surface)
- Serialization of the full resource graph

**Acceptance criteria.**
- One command produces a self-contained publication (all rule types + product/mapping
  deps).
- Publication carries a stable version id + content hash; identical inputs → identical
  hash.
- Publication loadable offline (no live Google Sheets dependency).
- Manifest lists every included resource + source provenance.

---

### 4. Load a publication and attach orders created with it 🔲

**Description.** Load a specific publication into the running system, and bind every order
created in that session to the publication's version id, so an order always knows the
exact rule set that produced it.

**Rationale.** Clinical traceability + MDR: given any past order, you must reconstruct the
exact constraints under which it was computed. Also enables safe rule upgrades without
mutating in-flight orders.

**Dependencies.** Needs 3 (publication artifact) and 5 (somewhere to load from).
Gated by 8 (who loaded/changed which publication must be audited).

**Affected areas.**
- `src/Informedica.GenPRES.Server/` (load publication into resource layer at runtime)
- Order model: stamp publication version id onto each order
- `Informedica.GenFORM.Lib/Api.fs` `ResourceConfig` (load from publication, not sheets)

**Acceptance criteria.**
- Server can boot/switch to a named publication version.
- Every created order records its publication id.
- Re-opening an order re-loads its publication's rules (not the current live set).
- Switching publication does not retroactively alter existing orders.

---

### 5. Storage for rule publications 🔲

**Description.** Persistent store for publication artifacts: list, fetch-by-version,
retain history, mark active/deprecated.

**Rationale.** Publications must outlive a process and be retrievable by id for the life of
any order that references them. Backing store for item 4.

**Dependencies.** Needs 3 (defines the artifact). Storage tech choice is **open** —
see [Open Questions](#open-questions).

**Affected areas.**
- New storage adapter (server-side); interface kept thin per coding standards
- `src/Informedica.GenPRES.Server/`

**Acceptance criteria.**
- Save/list/get publications by version id.
- Immutability enforced (published version never mutated).
- History + active/deprecated status queryable.
- Backup/restore story documented.

---

### 6. Storage for running patient orders 🔲

**Description.** Persistent store for live patient orders and their state (draft → signed →
dispensed → administered), each referencing its publication id (item 4).

**Rationale.** Orders currently live only in client/server session state. Production needs
durable, queryable order state across sessions and users.

**Dependencies.** Needs 4 (order↔publication binding) and 8 (orders are patient data →
access control + audit mandatory). Storage tech choice is **open**.

**Affected areas.**
- New order persistence adapter (server-side)
- Order model state machine (DU per `core-domain.md` Order Management Cycle)

**Acceptance criteria.**
- Create/read/update/query orders by patient + status.
- Each persisted order references its publication id.
- State transitions enforced (no illegal transitions).
- All access audited (ties to 8).
- Patient data handling meets privacy requirements (no PII in logs).

---

### 7. Interface with existing EMR systems 🔲

**Description.** Integration layer to exchange orders/patient context with external EMR
systems, building on existing integration libs (`Informedica.FHIR.Lib`,
`Informedica.HIXConnect.Lib`, `Informedica.MetaVision.Lib`,
`Informedica.DataPlatform.Lib`).

**Rationale.** GenPRES must consume patient context from and return validated orders to the
EMR of record to be clinically useful in situ.

**Dependencies.** Needs 6 (durable orders to exchange) and 8 (identity + audit on every
inbound/outbound exchange). Highest-risk item — defer until data + auth foundation solid.

**Affected areas.**
- `src/Informedica.FHIR.Lib/` (MedicationRequest mapping already noted in commit
  conventions), `Informedica.HIXConnect.Lib/`, `Informedica.MetaVision.Lib/`
- Inbound: patient demographics → Order Context; outbound: order → FHIR/EMR format

**Acceptance criteria.**
- Pull patient context from at least one EMR → populate Order Context.
- Push a validated order to at least one EMR as a FHIR MedicationRequest (unit-safe
  quantities).
- Mapping errors mapped to domain errors at the boundary (no leaking exceptions).
- End-to-end exchange audited.

---

### 8. User authentication, authorization, and logging 🔲

**Description.** Real identity layer: authenticate users, authorize by role
(physician / nurse / pharmacist per `core-domain.md`), and audit-log every clinically
significant action. Replaces the current single `GENPRES_PASSWORD` admin gate.

**Rationale.** Cross-cutting prerequisite for anything that persists patient data or talks
to an EMR (items 4, 6, 7). Required for MDR + privacy. Current model is a single shared
admin password — insufficient for multi-user clinical use.

**Dependencies.** None technically, but should land **before** 4/6/7 go live. Best started
in parallel with P1/P2.

**Affected areas.**
- `src/Informedica.GenPRES.Server/` (auth middleware, session, role checks)
- `src/Informedica.GenPRES.Client/` (login, role-aware UI)
- `Informedica.Logging.Lib/` (structured audit log; message templates, redact PII)

**Acceptance criteria.**
- Per-user authentication (not a shared password).
- Role-based authorization on every command (physician/nurse/pharmacist + admin).
- Structured, tamper-evident audit log of clinically significant actions (who/what/when/
  which publication).
- No PII / secrets in logs (per logging standards).

---

## Open Questions

- **Storage backends (items 5, 6).** Embedded (SQLite/LiteDB) vs. server DB (Postgres) vs.
  document store? Publications are immutable blobs + manifest; orders are mutable,
  queryable, privacy-sensitive — may warrant different stores.
- **Publication format (item 3).** JSON graph vs. existing CSV/TSV resource shapes vs. a
  packed archive? Must round-trip losslessly through `getFromGetData`/`toData`.
- **Identity provider (item 8).** Self-hosted vs. OIDC/hospital SSO? EMR integration (7)
  may dictate this.
- **EMR scope (item 7).** Which EMR(s) first? Existing libs hint at HIX + MetaVision —
  confirm priority + available test environments.
