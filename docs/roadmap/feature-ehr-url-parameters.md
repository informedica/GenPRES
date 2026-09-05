# Feature Request: Extend URL parameters to accept patient data from an external EHR

## Is your feature request related to a problem? Please describe.

GenPRES can be launched with a patient context pre-filled via URL query
parameters (e.g. `#patient?by=2020&bm=3&bd=1&wt=12000&cv=y`). This is the
integration point used when an external Electronic Health Record (EHR) links
into GenPRES for a specific patient.

The current parameter set (parsed in
[App.fs:237](../../src/Informedica.GenPRES.Client/App.fs#L237)) only covers
demographic/clinical values needed for dose calculation:

| Param | Meaning |
| ----- | ------- |
| `by` / `bm` / `bd` | birth date |
| `ad` | age in days |
| `wt` | weight (gram) |
| `ht` | height (cm) |
| `gw` / `gd` | gestational age weeks / days |
| `cv` | central venous line (`y`) |
| `dp` | department |
| `pg` `la` `dc` `in` `md` `rt` `fr` `dt` | UI / prescription context |

It **cannot** carry the patient identity or the identity of the ordering user
from the EHR. As a result:

- The patient shown in GenPRES is anonymous — there is no patient identifier,
  first name, or last name to confirm the clinician is prescribing for the
  correct patient (a patient-safety concern).
- Venous access is limited to a single boolean CVL flag; peripheral lines and
  enteral tubes cannot be conveyed even though the domain already models them.
- There is no way to record *who* is prescribing (login/user context) for
  audit / traceability, which MDR-regulated workflows require.

## Describe the solution you'd like

Two changes, delivered together:

1. **Add** the new EHR fields (patient identity, user context, admission
   date, bed id, full venous-access list).
2. **Redesign** the whole URL query-parameter scheme to consistent
   **three-letter** keys.

The current scheme mixes two-letter keys (`by`, `wt`, `cv`, …) that are terse,
inconsistent, and already colliding (`ad` = *age in days*, so an admission
`ad` is impossible). Moving to a uniform three-letter convention makes the
contract self-documenting for EHR integrators and frees up a clean namespace
for the new fields.

### Redesign: full three-letter parameter scheme

Every parameter — existing and new — under one convention. `Legacy`
shows the current key (blank = new field).

| Key | Field | Type | Legacy | Notes |
| --- | ----- | ---- | ------ | ----- |
| `byr` | Birth year | int | `by` | |
| `bmo` | Birth month | int | `bm` | default 1 |
| `bdy` | Birth day | int | `bd` | default 1 |
| `agd` | Age in days | int | `ad` | alternative to birth date |
| `wgt` | Weight (gram) | int | `wt` | |
| `hgt` | Height (cm) | int | `ht` | |
| `gaw` | Gestational age weeks | int | `gw` | |
| `gad` | Gestational age days | int | `gd` | |
| `sex` | Gender | `m`/`f` | — | not currently settable via URL |
| `cvl` | Central venous line | `y` | `cv` | see Venous access |
| `pvl` | Peripheral venous line | `y` | — | new |
| `ent` | Enteral tube | `y` | — | new |
| `dep` | Department | string | `dp` | → `Patient.Department` |
| `bed` | Bed Id | string | — | new |
| `adm` | Admission date | ISO `yyyy-mm-dd` | — | new; single ISO value avoids the `ad`/age collision |
| `pid` | Patient Id | string | — | new; external EHR identifier (e.g. MRN) |
| `fnm` | First name | string | — | new; confirmation display only |
| `lnm` | Last name | string | — | new; confirmation display only |
| `usr` | User context | string | — | new; ordering clinician login, for audit |
| `pag` | Page | `el`/`cm`/`pr`/`fm`/`pe` | `pg` | |
| `lan` | Language | `en`/`du`/`fr`/`gr`/`sp`/`it` | `la` | |
| `dsc` | Show disclaimer | `n` | `dc` | |
| `ind` | Indication | string | `in` | |
| `med` | Medication | string | `md` | |
| `rte` | Route | string | `rt` | |
| `frm` | Form | string | `fr` | |
| `dst` | Dose type | string | `dt` | |

Example redesigned launch URL:

```text
#patient?byr=2020&bmo=3&bdy=1&wgt=12000&cvl=y&dep=NICU&pid=1234567&fnm=Jan&lnm=Jansen&usr=jdoe&adm=2026-07-10&bed=12
```

### Venous access

The domain already models access as a list
([Types.fs:129-132](../../src/Informedica.GenPRES.Shared/Types.fs#L129)):

```fsharp
and Access =
    | CVL          // central venous line
    | PVL          // peripheral venous line
    | EnteralTube
```

but only `CVL` is settable (`cv=y`). Under the three-letter scheme, each
access type gets its own boolean flag — `cvl=y`, `pvl=y`, `ent=y` — which
compose into the `Access list`. (Alternative: a single `acc=cvl,pvl`
comma-separated list; the per-flag form is preferred for consistency with the
rest of the scheme and simpler EHR string-building.)

### Migration / backwards compatibility

A full key rename is a **breaking change** for any existing EHR deep-links.
Options, in preference order:

1. **Dual-read transition.** `parseUrl` accepts both new three-letter keys and
   the legacy two-letter keys (legacy → new alias map), logs a deprecation
   warning when a legacy key is seen, and drops legacy support after a
   published date once EHR integrators have migrated.
2. **Hard cutover.** Coordinate a single switch-over with EHR integrators;
   simplest code, but requires all consumers to change at once.

Recommendation: option 1. The alias map lives only in the parser and is cheap
to remove later.

### Required supporting changes

These fields do not exist on the domain `Patient` today
([Types.fs:93-104](../../src/Informedica.GenPRES.Shared/Types.fs#L93)) — the
record has no identifier and no name. Delivering this needs:

- Extend `Patient` (Shared) with optional `Id`, `FirstName`, `LastName`,
  `AdmissionDate` (`DateTime option`), and `BedId` (`string option`) fields.
  `Department` already exists as `Patient.Department`
  ([Types.fs:103](../../src/Informedica.GenPRES.Shared/Types.fs#L103)); a
  `Location` (`string option`) field also already exists
  ([Types.fs:102](../../src/Informedica.GenPRES.Shared/Types.fs#L102)) and may
  be a fit for bed/ward location — decide whether `bed` maps to a new `BedId`
  field or reuses `Location`.
- Introduce a user/session-context value for `usr`. There is currently **no**
  per-user identity model — the only auth is a single password gate for the
  settings page ([App.fs:48-49](../../src/Informedica.GenPRES.Client/App.fs#L48)).
  Decide whether `usr` is display/audit-only metadata or feeds a future
  identity model.
- Update `parseUrl` / `parsePatientParams`
  ([App.fs:225-349](../../src/Informedica.GenPRES.Client/App.fs#L225)) to read
  the new keys.
- Update the parameter doc comment
  ([App.fs:206-224](../../src/Informedica.GenPRES.Client/App.fs#L206)).
- Surface Id + name in the UI patient header so the clinician can confirm the
  right patient.

Per the script-only policy, non-UI Shared type changes are prototyped in
`.fsx` and migrated by a maintainer; the Client parsing/UI is edited directly.

## Describe alternatives you've considered

- **Fable.Remoting handshake instead of URL params.** A server call keyed by a
  short-lived launch token would keep PII out of the URL/browser history.
  Heavier to integrate; the existing EHR link mechanism is URL-based.
- **POST the patient context** rather than GET query string — avoids logging
  PII in access logs, but breaks the simple deep-link launch model EHRs use.
- **Do nothing / manual entry.** Clinician re-types name and identity — error
  prone and defeats the point of EHR integration.

## Additional context

**Privacy / MDR.** `pid`, `fnm`, `lnm`, `usr` are PII. URLs land in browser
history, referer headers, and server access logs. Coding standards already
require redacting PII in logs
([fsharp-coding.instructions.md](../../.github/instructions/fsharp-coding.instructions.md)
— "Avoid logging PII; redact sensitive data"). Note the current warning path
logs the raw URL on parse failure
([App.fs:313](../../src/Informedica.GenPRES.Client/App.fs#L313)) — this must
not leak the new fields. Consider:

- Redacting `pid/fnm/lnm/usr` (and `bed`, `adm` together with `dep` since
  bed+ward+date is identifying) from any URL logging.
- Documenting that transport should be HTTPS.
- Whether the identity fields belong in the URL at all vs. a token exchange.

**Backwards compatibility.** The three-letter redesign renames existing keys,
so it is a breaking change — see [Migration](#migration--backwards-compatibility)
above. The dual-read transition keeps existing two-letter EHR deep-links
working until legacy support is retired.
