# Implementation plan for issue #234

## Problem description

The build system has no automated version management: `Directory.Build.props`
carries a single hand-edited `<Version>` element, and `CHANGELOG.md` is
hand-written prose with no tooling behind it. Issue #234 asks for six
improvements: (1) version management, (2) release artifacts/changelog
automation, (3) a Docker image published on release, (4) auto-generated API
documentation, (5) separate build targets for server and client, and (6) a
build system that AI coding agents can understand and drive. The issue
explicitly requires an ADR before implementation.

## Approaches considered

For versioning/changelog (items 1–2), three options were on the table:

- **MinVer / Nerdbank.GitVersioning** — tag-driven version at build time only. Low risk, but leaves changelog and release-PR creation unautomated (Repo Assist's Task 8 would keep doing that manually).
- **EasyBuild.ShipIt** — reads `CHANGELOG.md` front-matter and conventional-commit history, computes the next semver, generates the changelog section, and opens a release PR. Requires squash-merge so each PR maps to one commit.
- **Status quo** — keep the manual `Directory.Build.props` edit and Repo Assist's manual changelog PRs. Rejected: this is exactly what #234 was filed to fix.

For Docker-on-release (item 3) and API docs (item 4): include now vs. defer
as follow-up issues once the versioning foundation lands.

## Chosen approach

- **EasyBuild.ShipIt** for items 1 and 2. It's the only option that covers
  version derivation, changelog generation, *and* release-PR creation in one
  tool, which directly replaces Repo Assist's existing manual Task 8 instead
  of leaving two overlapping mechanisms. The repo maintainer has confirmed
  switching the default merge strategy to squash-only is acceptable, which
  removes the main adoption blocker.
- Items 3 (Docker image on release) and 4 (auto-generated API docs) are
  **out of scope for #234** and will be filed as separate follow-up issues
  once this ADR is accepted, so #234 isn't left open indefinitely for
  lower-priority work.
- Item 5 (separate server/client build targets) is in scope: split the
  `Build` FAKE target into `ServerBuild` and `ClientBuild`, keeping `Build`
  as a thin umbrella target so existing CI/dependency chains
  (`Build ==> ServerTests`, `Build ==> CheckVersions`, etc.) don't need to
  change.
- Item 6 (agent-visible build system) is folded into every implementation PR
  below rather than done as a single separate docs PR — `AGENTS.md` and
  `DEVELOPMENT.md` get updated in the same PR that makes them stale.

Full detail (decisions, trade-offs, MDR/safety notes) lives in
[ADR-0021](../mdr/design-history/0021-build-system-versioning-and-release.md).

## Confidence

Medium. The overall direction (ShipIt + squash-merge + target split) is
sound and matches the issue thread's own analysis, but EasyBuild.ShipIt's
exact CLI/config surface (front-matter schema, how it surfaces the computed
version to MSBuild) is only known second-hand from an AI-bot's issue
comment, not from its actual README. That needs verifying before any code
is written against it — see Step 1 below.

## Steps

1. **Verify EasyBuild.ShipIt's real API** by reading its README/source directly 
   (not the bot summary). Confirm the `CHANGELOG.md` front-matter schema, the CLI invocation, 
   and critically, whether it writes `Directory.Build.props` directly or expects a 
   separate consumer (e.g. MinVer-style git-tag read) to pick up the version it computes. 
   This determines whether `scripts/CheckSolutionVersions.fsx` needs changes.
2. **Flip the repository's default merge method to squash-only** (GitHub
   repo settings — maintainer action, not a PR). Do this immediately before step 3 
   merges, so the ShipIt-adoption PR is the first squash-merged one.
3. **Adopt ShipIt tooling**: add it to `.config/dotnet-tools.json`, add the confirmed 
   front-matter to `CHANGELOG.md`, add a local dry-run entry point 
   (FAKE target or direct `dotnet shipit` invocation): not wired into CI yet.
4. **Wire the version source**: update `Directory.Build.props` and
   `scripts/CheckSolutionVersions.fsx` per step 1's findings; verify `dotnet run CheckVersions` still passes.
5. **CI integration**: add a workflow (new `release.yml` or a job in
   `build.yml`) that runs ShipIt on merges to `master`, decoupled from the
   existing test/format matrix job so a ShipIt failure never blocks CI.
6. **Retire Repo Assist's Task 8** ("Release Preparation") in
   `.github/workflows/repo-assist.md`/`.lock.yml` in the same PR as step 5,
   so there's never a window with two bots proposing release PRs.
7. **Split `Build` into `ServerBuild`/`ClientBuild`** in `Build.fs`, update
   `DEVELOPMENT.md`'s target table and dependency-chain diagram and
   `AGENTS.md`'s quick-start section in the same PR.
8. **File follow-up issues** for items 3 and 4, linked from ADR-0021.

Each of steps 3, 5, 6, and 7 is a separate, independently reviewable PR sized to CONTRIBUTING.md's guidance (~100–200 lines).
