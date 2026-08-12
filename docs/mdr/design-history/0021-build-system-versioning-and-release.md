# ADR-0021: Build System Versioning and Release Automation

**Date**: 2026-08-05

**Status**: Proposed

**Related Issue**: [#234 — Improve build system](https://github.com/informedica/GenPRES/issues/234)

**Related Plan**: [Implementation plan for issue #234](../../implementation-plans/234-improve-build-system.md)

## Context

GenPRES has no automated version management. `Directory.Build.props` carries a single 
hand-edited `<Version>0.1.2-alpha</Version>` element that every project's own 
`Directory.Build.props` imports; `dotnet run CheckVersions` (`scripts/CheckSolutionVersions.fsx`) 
only asserts that every built DLL matches this hand-set value — it does not derive it. 
`CHANGELOG.md` is hand-written Keep-a-Changelog prose with no front-matter and no tooling
behind it. No MinVer, Nerdbank.GitVersioning, GitVersion, or equivalent exists anywhere in 
the repo (`.config/dotnet-tools.json`, `paket.dependencies`, every `.fsproj`, and 
`Directory.Build.props` were all checked directly).

Issue #234 asks for six improvements and explicitly requires this ADR before implementation:

1. Version management
2. Release artifacts including a changelog of what's included
3. A Docker image published on release
4. Automatically updated API documentation
5. Separate build targets for the server and the client
6. A build system that AI coding agents can understand and drive correctly

A `gh-aw` (GitHub Agentic Workflows) bot, "Repo Assist"
(`.github/workflows/repo-assist.md`), already runs a "Release Preparation" task (Task 8) 
that manually finds merged PRs, proposes a semver bump, updates the changelog by hand, 
and opens a draft release PR. This is the current de facto release process. Any automation 
adopted here must either replace this task outright or coordinate with it, running both in
parallel would produce competing release PRs.

The repo currently merges PRs with merge commits
(`939aec79 Merge pull request #436 from ...`), not squashes, even though
squash-merge is enabled at the GitHub API level. The maintainer initially confirmed
(2026-08-05) that switching the default merge method to squash-only is acceptable.
Concerns were raised that squash-only discards commit-level history on PRs where 
granularity could matter. Since ShipIt's own README treats squash and rebase merging 
as equally valid (both avoid the `Merge pull request ...` commits that break its commit 
parsing — see the verification note below), the revised decision is to disable merge 
commits but leave both squash and rebase merging enabled, so each contributor chooses
per PR instead of one strategy being forced on everyone.

This document is ADR-0021, the next number available in
[the design-history log](0000-change-log.md).

## Decision

Adopt **EasyBuild.ShipIt** to cover items 1 and 2 together: it derives the next semver 
version from conventional-commit history, generates the changelog section, and opens the 
release PR — replacing Repo Assist's Task 8 rather than running alongside it. Scope 
items 3 and 4 out of #234 entirely, tracked as separate follow-up issues. Address 
item 5 with a straightforward FAKE target split. Address item 6 incrementally, one PR 
at a time, rather than as a single documentation pass.

### Key design choices

| # | Choice | Rationale |
|---|--------|-----------|
| 1 | EasyBuild.ShipIt over MinVer/Nerdbank.GitVersioning | Only option that covers versioning **and** changelog generation **and** release-PR creation in one tool; MinVer/Nerdbank would still leave changelog automation and Repo Assist's Task 8 duplication unresolved |
| 2 | Merge commits disabled; squash and rebase merging both left enabled | ShipIt needs each PR to land without a `Merge pull request ...` commit to parse cleanly; squash and rebase both satisfy that per ShipIt's own README, so contributors keep the choice instead of being forced to squash away commit-level history |
| 3 | Retire Repo Assist Task 8 in the same PR that turns on CI-driven ShipIt | Prevents two bots from proposing competing release PRs on the same merge |
| 4 | Docker-on-release (item 3) and API docs (item 4) deferred to new follow-up issues | Both are greenfield efforts (no existing docfx/GitHub Pages/Docker-publish infrastructure) with no dependency on the versioning work landing first being a blocker either way; keeping them separate lets #234 close on a coherent, reviewable scope |
| 5 | `Build` FAKE target split into `ServerBuild`/`ClientBuild`, with `Build` kept as an umbrella target | Existing dependency chains (`Build ==> ServerTests`, `Build ==> CheckVersions`, `Build ==> Run`) keep working unchanged; new targets are additive |
| 6 | Agent-visible docs (`AGENTS.md`/`DEVELOPMENT.md`) updated per-PR, not as a separate pass | Each PR already knows which target/behaviour it changed; batching risks the docs pass lagging behind or being skipped |

### Verification gap to close before implementation

Everything currently known about EasyBuild.ShipIt's CLI and config surface
(a `last_commit_released` changelog front-matter field, a
`dotnet shipit github --allow-branch master --skip-merge-commit` invocation,
`--mode pull-request` vs `--mode push`) comes from an AI-generated issue comment 
summarizing the tool, not from its own README. Before any implementation PR is 
opened, the actual EasyBuild.ShipIt documentation must be read to confirm this 
schema and — specifically — how it expects the computed version to reach MSBuild 
(writing `Directory.Build.props` directly, vs. emitting a tag for a separate reader). 
This determines whether `scripts/CheckSolutionVersions.fsx` needs to change at all.

## Consequences

**Positive**:

- A single, well-defined tool owns version bumps, changelog entries, and release-PR
  creation — no more manual `Directory.Build.props` edits, no competing automation.
- `ServerBuild`/`ClientBuild` let CI and contributors build just the piece
  they're touching without changing any existing target's behaviour.
- Agent-facing docs stay in sync with the build system because each PR
  updates its own corner rather than deferring to a cleanup pass.

**Negative / Trade-offs**:

- Disabling merge commits changes the commit history shape project-wide, not just 
  for build-system PRs — every future PR merge is affected. Squash and rebase 
  remain a per-PR choice, so no one is forced to lose commit-level history, but 
  `Merge pull request ...` commits stop being an option entirely.
- `CHANGELOG.md`'s current rich, hand-written prose entries (see any `[Unreleased]` 
  entry today) become leaner, PR-title-derived entries under ShipIt. 
  The `=== changelog ===` block convention in a PR body is the escape hatch for 
  entries that need more detail than a title provides.
- Items 3 and 4 remain unaddressed after #234 closes; they need their own
  issues and, eventually, their own ADRs or ADR amendments.

**MDR / Safety**:

- Release/version automation is process tooling, not clinical logic, it does not 
  touch dosing, rules, parsing, or resource mapping, so it does not trigger the 
  unit-test/changelog/field-comment requirements that apply to those areas.
- The changelog remains the audit trail referenced by
  `docs/mdr/design-history/0000-change-log.md`; automating its generation must not 
  reduce its usefulness as a Design History File input, this is why the `=== changelog ===` 
  escape hatch matters for anything with MDR-relevant detail.

## References

- [Issue #234 — Improve build system](https://github.com/informedica/GenPRES/issues/234)
- [Implementation plan for issue #234](../../implementation-plans/234-improve-build-system.md)
- [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt)
- [Repo Assist workflow](../../../.github/workflows/repo-assist.md)
- [ADR-0000 — Design History Change Log](0000-change-log.md)
- [ADR-0016 — G-Standard Dose Rule Fallback](0016-gstand-dose-rule-fallback.md) (structural reference for this ADR)
