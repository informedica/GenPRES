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

### Docker image publishing — amended 2026-08-25

This ADR pushed #234 item 3 ("publish a Docker image on release") into [issue #459](https://github.com/informedica/GenPRES/issues/459).
Up to now, every release image was built and pushed manually with `dotnet run DockerBuild`, so the published tag didn't always match the
commit that produced the `CHANGELOG.md` entry.

**Decision**: a second job, `publish-docker-image`, is added to `tag-release.yml`. It waits for `tag-and-release`,
builds the `Dockerfile` at the same merge commit that was tagged, smoke-tests the image, and then pushes it. It
uses the `version`/`tag`/`prerelease` outputs from the first job instead of recalculating anything. It only runs
when both tagging and the GitHub Release succeed, so a broken commit never publishes an image.

Key points agreed during implementation:

| Question | Decision |
|---|---|
| Registry | Use `ghcr.io/informedica/genpres` for now. The preferred long-term home is a Docker Hub `informedica` account, requested from `@jennifervdstreek` on 2026-08-21 and still pending. GHCR needs no new secret because it uses the workflow's `GITHUB_TOKEN`. The registry/namespace is set in one `IMAGE_NAME` env var, so switching to Docker Hub later is a simple one-line change. |
| Tags pushed | Always push `:<version>` (e.g. `:0.1.2-alpha.6`). `:latest` only moves on a stable (non-pre-release) version. All releases so far are `0.1.2-alpha.N`, so `:latest` stays empty until the first stable version. The pre-release flag comes from `needs.tag-and-release.outputs.prerelease`, the same string the GitHub Release step already checks. `<version>` has any `+` folded to `-` before it becomes a tag: `Versioning.fsx`'s `isPreRelease` already tolerates SemVer build metadata (`1.0.0+build.7`), and a raw `+` isn't a legal Docker tag character, so an untranslated build-metadata version would fail the image build after the Git tag and Release for it already exist. Caught by Greptile's review of PR. |
| Architecture | Only `linux/amd64`, matching the existing `DockerBuild` FAKE target. Multi-arch is left for later: a multi-platform build produces a manifest list rather than one runnable image, and the smoke test below needs to `docker run` the image locally, so going multi-arch means reworking that step too. |
| Build-time secrets | None. `APP_VERSION` is the only build arg, same as `DockerBuild`. `GENPRES_URL_ID` and `GENPRES_PASSWORD` are runtime-only and left empty in the Dockerfile. They are never passed as build args, keeping the rule from `DEVELOPMENT.md` that build args must not bake secrets into image metadata. |
| Gate before publish | The image is built once, tagged with every tag it needs in a single `docker build -t ...` call, then started using the public demo `GENPRES_URL_ID` from `.env.example` and a random `GENPRES_PASSWORD`. The SPA shell (`/`) must return 200 within 60 seconds. Only then are the tags pushed with `docker push`, so nothing gets built twice. Not a full functional test, but it does catch images that fail to start — something the old manual process never checked. |
| Docker tooling | Plain `docker login` / `docker build` / `docker push`, not `docker/login-action`, `docker/setup-buildx-action`, or `docker/build-push-action`. This matches how `Build.fs`'s `DockerBuild` target already drives Docker everywhere else in this repo, and it's three fewer marketplace actions to pin and keep updated for a job that runs once a release, not once a PR — the GHA layer cache those actions unlock isn't worth much at that frequency. `ubuntu-latest` ships Buildx preinstalled, so `--platform` still works with no setup action. |
| Package visibility | Not decided here. A first-time GHCR push from a workflow creates a **private** package visible only to the repo. Since GenPRES is public and the image must be pullable, `@halcwb` or `@jennifervdstreek` need to set `informedica/genpres` to public in the GitHub org package settings. The workflow can't change visibility itself. |

**Accepted trade-off**: GHCR is temporary. Once the `informedica` Docker Hub account exists, the follow-up is a small PR that changes the `IMAGE_NAME` value and swaps the `docker login` call to Docker Hub credentials — a new secret, unlike GHCR's `GITHUB_TOKEN`. Keeping this amendment focused means the workflow stays simple and uses one registry for now.

### Docker registry moved to Docker Hub — amended 2026-09-01

The `informedica` Docker Hub organisation now exists, so the follow-up anticipated above landed as issue
[#459](https://github.com/informedica/GenPRES/issues/459).

**Decision**: `publish-docker-image` publishes to `docker.io/informedica/genpres` instead of GHCR. This
is the single `IMAGE_NAME` env-var change the 2026-08-25 amendment expected, plus:

| Question | Decision |
|---|---|
| Authentication | Docker Hub OIDC, not a stored token. The `informedica` org is on the Docker Team plan, which supports GitHub OIDC connections. `docker/login-action@v4.6.0` exchanges the job's GitHub OIDC token for a short-lived Docker Hub token via a connection created in Docker Home; the connection ID is a repo *variable* `DOCKERHUB_OIDC_CONNECTIONID` (not a secret, it is an identifier, inert without the ruleset). No credential to rotate or leak. |
| OIDC subject scoping | The job runs on `pull_request: closed`, whose bare OIDC subject (`repo:informedica/GenPRES:pull_request`) is shared by every PR's workflow run, any PR could then mint a push token. The job is given a GitHub Actions `environment: docker-publish`, so the subject becomes `repo:informedica/GenPRES:environment:docker-publish`, and the Docker Hub ruleset matches that. The environment also takes required-reviewer protection for a manual gate before each push. |
| Job permissions | `id-token: write` added (for the OIDC token request), `contents: read` kept (checkout). `packages: write` dropped — that was a GitHub Packages/GHCR grant, useless for Docker Hub. |
| Marketplace actions | `docker/login-action` is now used for the login step only, the OIDC token exchange cannot be done with plain `docker login`. This is a carve-out from the 2026-08-25 "plain CLI throughout" decision; `docker build` and `docker push` stay CLI. |
| GHCR | Not kept as a mirror. Nothing was ever published to `ghcr.io/informedica/genpres` (no release triggered `publish-docker-image` while it targeted GHCR), so there are no existing pulls to preserve. A clean switch keeps the workflow single-registry, matching the 2026-08-25 "one registry for now" position. |
| Repository visibility | Still a manual step, same shape as GHCR's was: the first push creates `informedica/genpres` as a **private** Docker Hub repo. A Docker Hub org admin must set it public afterwards; the OIDC token cannot change repo visibility. |
| Local `DockerBuild` default | `Build.fs`'s `DOCKER_IMAGE` default moves from `ghcr.io/informedica/genpres` to `informedica/genpres` in the same PR, so a local `dotnet run DockerBuild` tags the image with the name releases actually publish. `DockerBuild`/`DockerRun` only build and run locally — they never push — so this is a label change, not a new push target. Agent-facing docs (`AGENTS.md`, `DEVELOPMENT.md`, `.github/copilot-instructions.md`, the `Dockerfile` run example) are updated to match. |

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
- Every published Docker tag now corresponds to the exact merge commit `tag-and-release` tagged and
  passed a startup smoke test, closing the gap where a hand-pushed image had no relationship to a shipped version.

**Negative / Trade-offs**:

- Every ShipIt invocation must pass `--skip-merge-commit`, indefinitely, because
  merge commits remain enabled. Omitting it makes ShipIt throw on the first
  `Merge pull request ...` commit it reaches rather than skipping it. This is
  documented at every invocation site (`release.yml`, `DEVELOPMENT.md`).
- `CHANGELOG.md`'s current rich, hand-written prose entries (see any `[Unreleased]` 
  entry today) become leaner, commit-title-derived entries under ShipIt.
  A `=== changelog ===` block in the commit message body is the escape hatch for
  entries that need more detail than a title provides.
- Item 4 (API docs, [#460](https://github.com/informedica/GenPRES/issues/460)) remains
  unaddressed after #234 closes; it needs its own issue work and, eventually, its own ADR or
  ADR amendment. Item 3 (Docker-on-release) is closed by the "Docker image publishing" amendment above.
- The tag and Release are created with the workflow's own `GITHUB_TOKEN`, so nothing can
  chain off them with `on: release` or `on: push: tags:`. Any future release-time automation
  has to live inside `tag-release.yml`, be dispatched explicitly, or use a PAT / App token.
- Release images publish to Docker Hub (`docker.io/informedica/genpres`) authenticated by OIDC —
  no stored registry credential, but a one-time setup (an OIDC connection in Docker Home, a
  `docker-publish` GitHub environment, a `DOCKERHUB_OIDC_CONNECTIONID` repo variable) and a manual
  "make the repository public" step, both outside the workflow. See the 2026-09-01 amendment above.
  (The workflow started on GHCR; [#459](https://github.com/informedica/GenPRES/issues/459) moved it.)

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
- [Issue #459 — Publish the Docker image automatically on release](https://github.com/informedica/GenPRES/issues/459) (follow-up, item 3, closed by the Docker image publishing amendment above)
- [`.github/workflows/tag-release.yml`](../../../.github/workflows/tag-release.yml) — implements both the tag/Release job and the `publish-docker-image` job
- [Issue #460 — Auto-generate and publish API documentation](https://github.com/informedica/GenPRES/issues/460) (follow-up, item 4)
- [Implementation plan for issue #234](../../implementation-plans/234-improve-build-system.md)
- [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt)
- [Repo Assist workflow](../../../.github/workflows/repo-assist.md)
- [ADR-0000 — Design History Change Log](0000-change-log.md)
- [ADR-0016 — G-Standard Dose Rule Fallback](0016-gstand-dose-rule-fallback.md) (structural reference for this ADR)
