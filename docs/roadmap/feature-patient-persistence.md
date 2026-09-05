# Feature Request: Patient-State Persistence

**Status:** Proposed
**Related fit-gap items:** 9.6 (version control / save history), 9.7 (multi-user conflict detection), 10.4 (per-user audit trail), and the "Persistence" summary gap
**Meeting action item (13 Jul 2026):** "Patient-data persistence + patient management (context from MetaVision → GenPRES; store order snapshots)" — Owner: Casper, Blocking: Yes
**Fit-gap source:** `docs/roadmap/fit-gap-ap2019-vs-genpres.md`

---

## 1. Problem

GenPRES is stateless. Patient context is passed in via URL on every open, which means:

- Patient data and running orders are **re-entered every session** — the single biggest usability regression versus the legacy AfsprakenProgramma (AP2019).
- URL-carried context is **manipulable** (encoded ≠ encrypted; a role or patient id in a URL can be tampered with).
- There is **no record of who prescribed what, for which patient** — an MDR/IGJ traceability gap (fit-gap 10.4).

The MVP must persist patient state and running orders so that reopening a patient restores the last saved treatment plan, and so that each saved version is attributable to a named prescriber.

## 2. What the legacy system does (benchmark)

The behavior to replicate lives in the VBA modules that save a snapshot of patient state:

- **`src/module/ModPatient.bas`** — `Patient_SavePatient` → `SavePatientToDatabase` (orchestration, guards, concurrency check).
- **`src/module/ModDatabase.bas`** — `Database_SavePatient`, `Database_SavePrescriber`, `Database_SaveData` (the actual writes).

Its model, which we adopt:

1. **Keyed on the patient identifier (hospital number).** A save is **refused if there is no hospital number** (`ModPatient.bas` guard). Persistence only happens for identified patients.
2. **Append-only, versioned snapshots.** Every save creates a new version (`versionID`, `versionUTC`, `versionDate`); the *latest* version is the current state. Nothing is updated in place; history accretes.
3. **Optimistic concurrency.** Before writing, it compares the in-hand version against the latest stored version and warns the clinician if the stored one is newer ("the appointments have changed since you loaded them — save anyway and become the latest?"). It does **not** hard-lock.
4. **Prescriber attribution on every row.** Each written record carries the prescriber login plus the version timestamps — this is also the audit trail.
5. **Snapshot = patient demographics + prescriber + the full prescription state** (structured key/value data + free-text notes).

## 3. Proposed behavior for GenPRES (MVP)

### 3.1 Scope of a snapshot

A snapshot is the **complete patient state as a single atomic unit**:

- Patient context (demographics, weight, age, GA/PMA, access type, etc.).
- The **treatment plan**: the list of running orders. Following the legacy/event-sourcing-flavored model, each running order carries **its own patient-context snapshot**, so an order is self-contained and reproducible.

Serialize the snapshot as an opaque payload (JSON blob) — GenPRES's domain owns the shape; the persistence layer treats it as data.

### 3.2 Persist only when a patient id is present

Mirroring the legacy guard: **a snapshot is written only when the patient has an identifier.** Unidentified / scratch state is never persisted. (The legacy app offers a "standard patient" fallback with a synthetic id for teaching cases; GenPRES may add an equivalent later, but it is out of MVP scope — for the MVP, "no id ⇒ no save".)

### 3.3 Append-only, latest-wins

- Each save inserts a **new version** for that patient id; retrieval returns the **latest** version.
- Every version records **metadata**: created-at (UTC), and **who touched it last** (prescriber identity from the user-management work).
- **No explicit user-facing version history / restore-to-point** in the MVP (out of scope per the meeting — versioning with concurrent edits "opens a can of worms"). History is retained in storage for audit, not exposed as a UI feature yet.

### 3.4 Concurrency

- Multi-user conflict is **rare in practice** (one clinician per patient, assigned informally) and is **not an MVP blocker**.
- Implement the **lightweight legacy safeguard**: on save, detect that a newer version exists and surface a soft warning ("this patient was saved more recently by X — save anyway?"). Keep "last touched / by whom" metadata.
- No hard locking, no merge, no real-time conflict resolution in the MVP.

### 3.5 Audit

- The prescriber identity + timestamp carried on each saved version **is** the audit record of who saved which patient state when. This satisfies fit-gap 10.4 for the MVP (who did what, for which patient), reusing the hospital-unique login already established by the user-management work.

## 4. Integration boundary — no MetaVision access

**GenPRES has no direct access to the MetaVision source system.** This constrains the design:

- The **patient identifier and patient/user context arrive at GenPRES via its API**, supplied by the MetaVision-side shell (VB.NET scripting that already gates the user and launches GenPRES with context). GenPRES **trusts the sender** and does not call back into MetaVision.
- GenPRES therefore treats the patient id as an **opaque, trusted key**. It does not, and must not, look the patient up in MetaVision, sync demographics from it, or write back to it.
- All persisted patient data lives in **GenPRES's own store**, populated from what the API delivers and what the clinician enters/prescribes — never from a live MetaVision query.

## 5. Storage — deliberately left open

The **physical storage design is out of scope for this request** and is a separate architectural decision (flagged in the meeting as taking on an external dependency, which matters more than internal code structure — owner: Mark, for review).

Assumptions fixed here:

- Storage is a **relational database**. Exact product, schema, hosting (SQL vs. SQLite/flat-file, container topology, on-prem location) are **to be decided** in the architecture/persistence design step.
- It is **local / on-premise**, inside the hospital firewall. The MVP does **not** target the shared regional data platform.
- The persistence layer exposes, at minimum: `save(patientId, snapshot, prescriber) → version` and `getLatest(patientId) → snapshot | none`, plus `latestVersionMetadata(patientId)` for the concurrency warning.

**Open questions for the architecture step (do not block writing this request):**

- Relational schema shape: one snapshot-blob column per version, vs. normalized order rows. (Legacy normalizes into key/value + text tables; a blob-per-version is simpler and matches the "atomic snapshot" intent.)
- Retention policy for old versions.
- How the prescriber identity is threaded from the API/user-management layer into each save.

## 6. Out of scope (MVP)

- User-facing version history / restore-to-a-previous-version.
- Real-time multi-user conflict resolution, locking, or merge.
- The shared regional (Rotterdam / Leiden / Utrecht) patient-data platform.
- Any MetaVision read/write integration or demographic sync.
- Non-identified ("standard"/teaching) patient persistence.

## 7. Acceptance criteria

1. Reopening an identified patient restores the last-saved patient context **and** treatment plan without re-entry.
2. Saving with **no patient id present** performs no write (and gives clear feedback).
3. Each save creates a new version; retrieval always returns the latest.
4. Each stored version records created-at (UTC) and the prescriber who saved it.
5. Saving over a stored version that is newer than the one loaded surfaces a soft "saved more recently by X — continue?" warning; no data is silently lost.
6. GenPRES performs the entire flow using only the patient id/context delivered via its API, with **zero** MetaVision calls.
7. The storage backend is a relational database reachable only from within the hospital firewall; the concrete schema/hosting is resolved in the separate persistence-architecture task.

## 8. Dependencies

- **User & access management** (prescriber identity/role) — supplies the "who" on each version; audit (10.4) rides on it.
- **Persistence-architecture review** (owner: Mark) — settles the storage decision before implementation.
- **MetaVision API contract** — defines exactly what patient/user context is delivered to GenPRES's API.
