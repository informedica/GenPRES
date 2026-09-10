# ADR-0006: Accredited Scope and Maintenance Lines

**Date**: 2026-09-10

**Status**: Proposed

**Related Issue**: [#580 — Scope switch to expose only the accredited parts in production](https://github.com/informedica/GenPRES/issues/580)

**Related Plan**: [Implementation plan for issue #580](https://github.com/informedica/GenPRES/pull/581) (PR, not yet merged)

## Context

GenPRES will carry an MDR certification for part of its features while development of the rest
continues on `master`. Two things have to hold at the same time:

1. A production server exposes only the certified features.
2. A defect in a certified feature can be corrected and shipped without disturbing development on
   `master`, and without shipping every change that was merged into `master` since the
   certification.

The certified set must also be able to grow: a later certification adds features, and the
software that ships with it is a new baseline.

What exists today. `master` is the only long-lived branch. ShipIt opens a release PR from its
reused `release/master` branch on every push, the release manager merges it, and
`tag-release.yml` tags the merge commit and publishes a Docker image ([ADR-0005](0005-build-system-versioning-and-release.md)).
Every version so far is a pre-release (`0.1.2-alpha.N`); the security review already states that
such versions are not appropriate for an MDR-regulated production deployment. `GENPRES_PROD`
selects the data set. Issue #580 and its plan (PR #581) add `GENPRES_SCOPE=accredited|full`, a
server-enforced switch with the accredited feature list as code in `Informedica.GenPRES.Shared`,
so that a production server withholds the features that are not accredited. That plan frames
the switch as the way to avoid a release branch: "one trunk, two surfaces".
[GOVERNANCE.md](../../GOVERNANCE.md) describes change control and release authority, but says
nothing about the maintenance of a released version, hotfixes, or which version production runs.
The MDR technical file is maintained in a separate, proprietary repository that this repository
never links ([ADR-0000](0000-documentation-rules.md)).

### What the standards and guidance say

- **IEC 62304** requires configuration management with identifiable baselines, a software
  maintenance process and a problem-resolution process. It prescribes no branching model.
- **Guidance on Git for regulated software** converges on the same shape: a maintenance branch
  per released line, so that urgent corrections are made on the released code while development
  continues on the trunk; tags as the immutable baselines an auditor retrieves; trunk-based
  development with feature toggles is accepted on the condition that the trunk is always
  releasable.
- **MDCG 2020-3 Rev.1**, section 4.3.2.3 and Chart C, classifies software changes. Non-significant,
  under the manufacturer's own change control: the correction of an error that brings the device
  back to its specification (a bug fix), a security update, a change of the appearance of the
  user interface such as new languages or layouts that add no function, and "a software change
  that disables a feature that does not interact with other features". Significant, assessed by
  the notified body before implementation: a new medical feature or functionality that may
  change the diagnosis or therapy delivered, a change of an algorithm that impacts the control of
  the device, a new or major modification of the architecture, a new channel of interoperability.
  The guidance applies to legacy devices under Article 120, and its classification is the one
  notified bodies use for change assessment in general.
- **Disabled code.** IEC 62304 does not forbid code that is present but not reachable. The FDA's
  software validation guidance expects dead or unreachable code to be found in structural testing
  and controlled. A feature that is disabled by configuration is therefore not free: the switch
  is a configuration item under verification, and the disabled code must be shown to be
  unreachable.

### The gap in the current plan

The scope switch decides which features a production server exposes. It does not decide which
code runs. Nearly every feature developed on `master` touches the shared libraries that the
accredited features run through (the solver, the order and formulary libraries, the shared
contract). A correction released from `master` therefore ships every change merged since the
certified release, most of them unverified against the certified behaviour, whether or not the
features that introduced them are reachable. The switch keeps unaccredited features out of
reach; it cannot keep unaccredited changes to accredited paths out of the binary.

## Decision

Three mechanisms, each answering one question.

### 1. Which features are reachable in production: the scope switch

The scope switch of #580 stands as designed in its plan. `GENPRES_SCOPE` is read once by the
server, defaults to `accredited` when `GENPRES_PROD=1` and to `full` otherwise, and is enforced
in the command dispatcher, so a withheld feature is unreachable through the API, not merely
hidden in the client. The accredited feature list is code in `Informedica.GenPRES.Shared`, so it
is versioned with the release it belongs to. The switch is a configuration item under
verification.

### 2. Which code runs in production: a maintenance line per certified release

- When a release is certified, its version is a stable SemVer without pre-release marker, tagged
  and published as today. A branch `maintenance/<major.minor>` is cut at that tag. The name
  follows IEC 62304's own term for the phase; `release/` is taken by ShipIt's PR branch.
- A production server runs only a tag from a maintenance line, with `GENPRES_PROD=1` and
  `GENPRES_SCOPE=accredited`. Test and acceptance servers run the line's release candidates
  and, separately, `master` alphas with `GENPRES_SCOPE=full`, so that the full suite keeps being
  exercised on production data ahead of its certification.
- Development stays on `master`. Alphas keep flowing from `master` as they do now; they are
  never deployed to production.
- The version number at the first certification stays in the `0.x` range; the moment to move to
  `1.0.0` is a separate decision. The line takes its name from whatever version is certified.

### 3. How a change reaches a line: MDCG 2020-3 as the routing rule

- A **non-significant change** in the sense of Chart C (a correction that brings the software back
  to its specification, a security update, a language or appearance change) is made on `master`
  first, while the code there still matches, and cherry-picked to the maintenance line. It is
  verified on the line and released from the line as a **patch** version under the
  manufacturer's change control. A correction that no longer applies to `master`, because the
  code has moved on, is made on the line alone and the reason is recorded in the PR.
- A **significant change** never enters a maintenance line. It ships as the next certified
  release: a new line, cut from `master`, after the notified body's assessment.
- When the accredited set grows, the accredited feature list changes on `master` in the release
  that is certified, so the list and the code it describes are one baseline.
- The rule of thumb in a review: a patch is a non-significant change, a minor or major version
  is a significant one. The reviewer of a cherry-pick PR classifies the change against Chart C
  in the PR, and the classification is part of the release record.

### The assumption to confirm

This decision assumes that non-accredited code may be present, disabled, in the production
binary, provided the switch is verified and the disabled features are shown to be unreachable
through the API. The regulatory track has not yet taken a position. If it refuses, the fallback
is a build-time exclusion of the non-accredited code from the production binary. The maintenance
line makes that feasible, because the set to exclude is fixed per line; it is not the default
because acceptance would then test a different binary from the one in production.

## Consequences

- **The release automation must learn the line.** `release.yml` runs on pushes to `master` only,
  and `tag-release.yml` fires on a merged PR whose head is `release/master`. A maintenance line
  needs ShipIt allowed on `maintenance/*` (ShipIt names its PR branch after the base branch, so
  `release/maintenance/<major.minor>`), the `pre_release` front matter dropped on the line so
  that it produces stable patch versions while `master` keeps producing alphas, a `CHANGELOG.md`
  that diverges per line (cherry-picks leave it out), a Docker tag per patch, and `:latest`
  moving only on a line's stable release. This is the implementation issue that follows this
  ADR; it does not change the decision.
- **Governance and contributor documents follow.** GOVERNANCE.md gains a maintenance and
  hotfix section next to release authority; CONTRIBUTING.md gains the cherry-pick rule and the
  Chart C classification in the PR template; DEVELOPMENT.md gains the environment table of
  plan #581 with the line named. The sentence in plan #581 that the switch avoids a release
  branch becomes: no release branch is needed for feature isolation; the maintenance line exists
  for the code baseline.
- **Production becomes a defined configuration.** A production server is a maintenance-line
  tag, production data, accredited scope. The statement in the security review that
  pre-release versions are not appropriate for production becomes enforceable rather than
  advisory.
- **Cherry-picks are work.** Every correction to a certified feature is made twice, on `master`
  and on the line, and verified on the line. This is the cost of not shipping unverified
  changes; it is bounded by the number of certified lines kept alive, which should be one, at
  most two during a transition.
- **Data is outside this decision.** The rule base in Google Sheets changes the behaviour of a
  running server without a deployment (a consequence recorded in [ADR-0001](0001-system-architecture.md)),
  and the cache files are per environment. Which sheet a production line reads, and how a
  change to it is controlled, is its own decision.
- **The dependency rule keeps the switch honest.** Because the domain libraries take no
  configuration ([ADR-0001](0001-system-architecture.md), dependency rule), the scope can only be
  read in the server; the accredited list in Shared is data the server enforces, not behaviour
  the libraries branch on.

## Alternatives considered

- **Trunk only: the scope switch plus tags, as plan #581 proposes.** A correction to a certified
  feature is then a release of `master` as it stands, under change control. Rejected: the switch
  selects features, not code, and every such release carries every change to shared code merged
  since certification, which either re-verifies far more than the correction or ships it
  unverified.
- **GitFlow with `develop` and `master`.** `master` would hold the released code and `develop`
  the integration of ongoing work. Rejected: it doubles the integration work for every change,
  contradicts the one-`master` workflow the project has settled on, and gives nothing a
  maintenance line does not give.
- **Build-time exclusion of non-accredited code.** A production build that contains only the
  accredited features. Kept as the fallback if the regulatory track refuses disabled code in the
  production binary; not the default, because the binary that is accepted would then differ from
  the one that runs in production, and because the switch already makes the features
  unreachable.
- **Long-lived feature branches** that keep unaccredited work off `master` until it is
  certified. Rejected: merge debt grows with the time to certification, and the full suite would
  never run on the acceptance server against production data.
- **Moving to `1.0.0` at the first certification.** Deferred; the decision here holds for any
  certified version, and the line is named after the version that is certified.

## References

- [ADR-0000: Documentation Rules](0000-documentation-rules.md) — what an ADR is for; the MDR
  technical file lives elsewhere and is not linked.
- [ADR-0001: System Architecture](0001-system-architecture.md) — the spreadsheet rule base as a
  runtime behaviour change; the dependency rule.
- [ADR-0005: Build System Versioning and Release Automation](0005-build-system-versioning-and-release.md)
  — ShipIt, `release/master`, tags and the Docker image.
- [Issue #580](https://github.com/informedica/GenPRES/issues/580) and its
  [implementation plan, PR #581](https://github.com/informedica/GenPRES/pull/581) — the scope
  switch, the environment table, the accredited list as code.
- [GOVERNANCE.md](../../GOVERNANCE.md) — change control, safety-critical changes, release authority.
- [docs/security/2026-04-10-security-review.md](../security/2026-04-10-security-review.md) —
  pre-release versions and production.
- MDCG 2020-3 Rev.1, *Guidance on significant changes regarding the transitional provision under
  Article 120 of the MDR*, section 4.3.2.3 and Chart C:
  <https://health.ec.europa.eu/system/files/2023-09/mdcg_2020-3_en_1.pdf>
- IEC 62304:2006+AMD1:2015, *Medical device software — Software life cycle processes*, clauses
  6 (maintenance), 8 (configuration management) and 9 (problem resolution).
- Johner Institute, *Configuration management for medical devices*:
  <https://blog.johner-institute.com/iec-62304-medical-software/configuration-management/>
- IntuitionLabs, *Git version control for FDA and IEC 62304 compliance*:
  <https://intuitionlabs.ai/articles/git-workflows-fda-compliance>
- Elsmar forum thread, *Dead or unreachable code in medical devices*:
  <https://elsmar.com/elsmarqualityforum/threads/dead-or-unreachable-code-in-medical-devices-medical-device-software.43343/>
