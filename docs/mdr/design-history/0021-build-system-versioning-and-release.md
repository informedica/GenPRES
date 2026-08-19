# ADR-0021: Build System Versioning and Release Automation

**Date**: 2026-08-05

**Status**: Accepted (2026-08-17), amended (2026-08-19)

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
granularity could matter. On acceptance (2026-08-17) that restriction was dropped 
entirely: all three merge methods stay enabled, and `--skip-merge-commit` handles the
`Merge pull request ...` commits ShipIt cannot parse. See design choice 2 below.

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
| 2 | No repo merge-method restriction: merge, squash, and rebase all stay enabled | `--skip-merge-commit` makes ShipIt tolerate `Merge pull request ...` commits, so restricting merge methods was not actually needed to adopt it. The cost is that merge-commit PRs contribute no changelog entries of their own; the individual commits underneath them are still parsed |
| 3 | Retire Repo Assist Task 8 in the same PR that turns on CI-driven ShipIt | Prevents two bots from proposing competing release PRs on the same merge |
| 4 | Docker-on-release (item 3) and API docs (item 4) deferred to new follow-up issues — filed as [#459](https://github.com/informedica/GenPRES/issues/459) and [#460](https://github.com/informedica/GenPRES/issues/460) | Both are greenfield efforts (no existing docfx/GitHub Pages/Docker-publish infrastructure) with no dependency on the versioning work landing first being a blocker either way; keeping them separate lets #234 close on a coherent, reviewable scope |
| 5 | `Build` FAKE target split into `ServerBuild`/`ClientBuild`, with `Build` kept as an umbrella target | Existing dependency chains (`Build ==> ServerTests`, `Build ==> CheckVersions`, `Build ==> Run`) keep working unchanged; new targets are additive |
| 6 | Agent-visible docs (`AGENTS.md`/`DEVELOPMENT.md`) updated per-PR, not as a separate pass | Each PR already knows which target/behaviour it changed; batching risks the docs pass lagging behind or being skipped |
| 7 | Release artifact created by a separate `tag-release.yml` workflow triggered by the release PR merging, not by ShipIt | ShipIt 3.0.1 cannot create a tag or Release in any mode. See the amendment below |

### Verification gap — closed 2026-08-17

This section originally recorded that everything known about EasyBuild.ShipIt's CLI
and config surface came from an AI-generated issue comment summarizing the tool rather 
than its own README, and required that gap be closed before any implementation PR was 
opened. Step 1 of the implementation plan closed it. What was confirmed against the tool:

- The `CHANGELOG.md` front matter carries `last_commit_released`, `pre_release`,
  `name`, and an `updaters:` list.
- The computed version reaches MSBuild **directly**: an `xml` updater with
  `file: Directory.Build.props` and `selector: /Project/PropertyGroup/Version`
  rewrites the `<Version>` element as part of the release PR. No git-tag intermediary, 
  and therefore no change to `scripts/CheckSolutionVersions.fsx`, it keeps asserting 
  that every built DLL matches whatever ShipIt wrote.
- The invocation is `dotnet shipit --allow-branch master --skip-merge-commit`
  (no `github` subcommand); `--mode` defaults to `pull-request`.
- `docs`-, `build`-, and `chore`-typed commits are silently omitted from the
  generated changelog, as are commits that change no files. Nothing overrides
  this: a change that must appear in release notes has to ride on a rendering
  commit type such as `feat` or `fix`.
- The `=== changelog ===` block enriches an entry that already renders; it cannot
  add one. It is read from the **commit message body**, not the pull request body,
  and requires both an opening and a closing marker — an unterminated block is
  discarded without warning. A PR body only reaches the block parser when the merge
  method copies it into the commit message, which squash-merging does and
  merge-commit merging does not.

The last two points were established by running ShipIt 3.0.1 against a throwaway
branch of this repo (probe commits of each type, with and without terminated
blocks) rather than from its documentation, which describes none of this
behaviour. See `DEVELOPMENT.md` for the contributor-facing version.

### Release artifact — amended 2026-08-19

This ADR as accepted covered #234 item 2 ("release artifacts including the list of changes") only as
far as the changelog: ShipIt derives the version and writes the `CHANGELOG.md` section, then stops.
`master` carried zero git tags and zero GitHub Releases across all three versions shipped under it
(`0.1.2-alpha.2`, `.3`, `.4`), so no immutable ref named a shipped version — the traceability record
an MDR project depends on. [Issue #470](https://github.com/informedica/GenPRES/issues/470) tracked
closing that half.

ShipIt cannot close it. Its 3.0.1 assembly contains the git/`gh` argument strings it shells out with
(`commit`, `push`, `rev-parse`, `rev-list`, `status`, `remote.origin.url`, `--label`, `--title`,
`--body`, `--base`, `--head`, `--json`, `--jq`, `--state`, `--limit`, `statusCheckRollup`) and no
`tag`, no `refs/`, and no releases endpoint. The `shipit github` subcommand forces the GitHub
provider for pull-request creation and adds only `--token`; `--mode push` changes how the changelog
commit reaches `master` and would discard this ADR's review gate. Both were checked against the
installed tool, not its documentation.

**Decision**: a separate `.github/workflows/tag-release.yml`, triggered by the ShipIt release PR
merging, creates an annotated tag on the merge commit and publishes a GitHub Release carrying that
version's `CHANGELOG.md` section. It is its own workflow for the same reason `release.yml` is: a
failure must not block the test/format matrix. The version, pre-release flag and Release body come
from `scripts/ReleaseNotes.fsx`, not from parsing inside the workflow, so CI and a local dry run before
merging a release PR execute the same code; that script resolves `<Version>` through
`scripts/Versioning.fsx`, which `dotnet run CheckVersions` also uses, keeping
`Directory.Build.props` to a single parser, and its changelog grammar is pinned by
`scripts/ChangelogTests.fsx`. Points settled during implementation:

| Question | Decision |
|---|---|
| Tag format | `v`-prefixed (`v0.1.2-alpha.4`), so downstream workflows can filter on `v*` |
| What the tag points at | The merge commit on `master`, not ShipIt's `chore: release ...` commit, which lives on the reused `release/master` branch |
| Backfill | None. `0.1.2-alpha.2`/`.3`/`.4` shipped before this workflow existed and stay untagged; the tag record starts at the next release. Retroactive tags would carry a tagger date unrelated to when the version shipped, and those three versions remain reconstructable from `CHANGELOG.md` and the merge commits it links |
| Pre-release flag | Derived from the version itself (SemVer: `0.1.2-alpha.4` is a pre-release, `0.1.3` is not), not hardcoded and not read from `CHANGELOG.md`'s `pre_release:` front matter — the front matter describes what ShipIt generates next, so it would answer differently for the same shipped version depending on when it was asked |
| Attached build output | None. The tag plus the changelog body is the artifact; publishing built images stays [#459](https://github.com/informedica/GenPRES/issues/459)'s scope |

Two behaviours were verified against this repo rather than assumed from documentation. ShipIt's
README recommends gating the downstream release job on
`startsWith(github.event.head_commit.message, 'chore: release ')`; that is 0 for 3 here, because
every release PR merged as a true merge commit whose push event carries
`Merge pull request #NNN from informedica/release/master`. The trigger uses the head ref
(`release/master`) instead, which survives any merge method. And events generated by the default
`GITHUB_TOKEN` do not start workflow runs: none of the three release PRs, all opened by
`github-actions[bot]`, ran its checks automatically — two had runs created but parked at
`action_required`, one got no runs at all until it was closed and reopened by hand. #459 therefore
cannot be an `on: release` workflow; it must be a job in `tag-release.yml`, a `workflow_dispatch` /
`repository_dispatch` call, or use a PAT / GitHub App token.

## Consequences

**Positive**:

- A single, well-defined tool owns version bumps, changelog entries, and release-PR
  creation — no more manual `Directory.Build.props` edits, no competing automation.
- `ServerBuild`/`ClientBuild` let CI and contributors build just the piece
  they're touching without changing any existing target's behaviour.
- Agent-facing docs stay in sync with the build system because each PR
  updates its own corner rather than deferring to a cleanup pass.
- Every version shipped under ShipIt has an immutable tag and a Release page carrying its
  changelog section, so "what was 0.1.2-alpha.3" is answerable from a ref rather than by
  hand-resolving commit SHAs out of `CHANGELOG.md`.

**Negative / Trade-offs**:

- Every ShipIt invocation must pass `--skip-merge-commit`, indefinitely, because
  merge commits remain enabled. Omitting it makes ShipIt throw on the first
  `Merge pull request ...` commit it reaches rather than skipping it. This is
  documented at every invocation site (`release.yml`, `DEVELOPMENT.md`).
- `CHANGELOG.md`'s current rich, hand-written prose entries (see any `[Unreleased]` 
  entry today) become leaner, commit-title-derived entries under ShipIt.
  A `=== changelog ===` block in the commit message body is the escape hatch for
  entries that need more detail than a title provides.
- Items 3 and 4 remain unaddressed after #234 closes; they need their own
  issues and, eventually, their own ADRs or ADR amendments.
- The tag and Release are created with the workflow's own `GITHUB_TOKEN`, so nothing can
  chain off them with `on: release` or `on: push: tags:`. Any future release-time automation
  has to live inside `tag-release.yml`, be dispatched explicitly, or use a PAT / App token.

**MDR / Safety**:

- Release/version automation is process tooling, not clinical logic, it does not 
  touch dosing, rules, parsing, or resource mapping, so it does not trigger the 
  unit-test/changelog/field-comment requirements that apply to those areas.
- The changelog remains the audit trail referenced by
  `docs/mdr/design-history/0000-change-log.md`; automating its generation must not 
  reduce its usefulness as a Design History File input, this is why the `=== changelog ===` 
  escape hatch matters for anything with MDR-relevant detail.
- A known gap follows from the omission of `docs`, `build`, and `chore` commits: a
  change of one of those types cannot reach the generated changelog at all, and no
  escape hatch overrides that. Design-history-relevant changes are therefore tracked
  through the ADRs in `docs/mdr/design-history/` and their entry in
  `0000-change-log.md`, which do not depend on ShipIt's output. This ADR is itself an
  instance: it lands as a `docs` commit and will not appear in any release section.

## References

- [Issue #234 — Improve build system](https://github.com/informedica/GenPRES/issues/234)
- [Issue #470 — Tag and publish a GitHub Release when the ShipIt release PR merges](https://github.com/informedica/GenPRES/issues/470) (follow-up, item 2, second half)
- [Issue #459 — Publish the Docker image automatically on release](https://github.com/informedica/GenPRES/issues/459) (follow-up, item 3)
- [Issue #460 — Auto-generate and publish API documentation](https://github.com/informedica/GenPRES/issues/460) (follow-up, item 4)
- [Implementation plan for issue #234](../../implementation-plans/234-improve-build-system.md)
- [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt)
- [Repo Assist workflow](../../../.github/workflows/repo-assist.md)
- [ADR-0000 — Design History Change Log](0000-change-log.md)
- [ADR-0016 — G-Standard Dose Rule Fallback](0016-gstand-dose-rule-fallback.md) (structural reference for this ADR)
