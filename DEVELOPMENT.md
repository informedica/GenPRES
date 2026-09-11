# Development on GenPRES

## Getting Started

### Toolchain Requirements

Before contributing, ensure you have the following installed (this section is the canonical source for toolchain versions):

- **.NET SDK**: pinned via [`global.json`](global.json) (currently `10.0.302`, `rollForward: latestPatch`) — see [Why the SDK is pinned tightly](#why-the-sdk-is-pinned-tightly) below
- **Node.js**: 18.x, 22.x, or 23.x (LTS versions recommended)
- **npm**: 10.x or later

#### Why the SDK is pinned tightly

`global.json` used to read `"version": "10.0.0", "rollForward": "latestFeature"`, which lets the
SDK resolver jump to a newer *feature band* (the hundreds digit of the patch version, e.g.
`10.0.3xx` -> `10.0.4xx`) with no corresponding change reviewed in this repo. CI's
`actions/setup-dotnet` step compounded this: it pinned `dotnet-version: '10.0.x'`, which always
installs the newest available `10.0.x` SDK on the runner regardless of `global.json`.

This combination caused [issue #447](https://github.com/informedica/GenPRES/issues/447): between
11 and 12 August 2026, GitHub's hosted runners started shipping `10.0.400` instead of the
previously-installed `10.0.302`, with no dependency or lock-file change in this repo (`paket.lock`
pins `Aether 8.3.1` and `FSharp.Core 10.1.203`, and `paket restore` uses the lock file as-is). The
newer SDK's F# compiler changed code generation around the `^=`/`Optic.set` custom operators that
`Informedica.GenORDER.Lib`/`Informedica.ZForm.Lib`'s optics code gets via `open Aether.Operators`:
under `10.0.400`, `Patient`'s and `DoseRule`'s module type initializers throw
`Dynamic invocation of op_HatEquals is not supported` the first time anything touches those
modules — not just under test discovery, so a build compiled with the bad SDK band would crash at
runtime too — even though the exact same source compiled and ran fine under `10.0.302`. 11 tests
across `GenORDER.Tests`, `GenCORE.Tests`, `ZForm.Tests`, and `GenFORM.Tests` failed identically, on
every PR, regardless of what the PR actually changed. Root-causing and fixing the `Aether`
incompatibility itself (rather than just avoiding the bad SDK band) is tracked as follow-up.

The fix applied: `global.json` now pins an exact `version` with `rollForward: "latestPatch"`, so
the resolver only ever picks up patches within the same feature band (e.g. `10.0.303`), never a
band jump. Both `.github/workflows/build.yml` and `.github/workflows/commit-lint.yml` now pass
`global-json-file: global.json` to `actions/setup-dotnet` instead of a separate `dotnet-version:
'10.0.x'`, so `global.json` is the single source of truth for CI's SDK version and a future
feature-band bump requires a deliberate, reviewed edit to that file. `Dockerfile`'s build stage is
pinned to the matching exact SDK image tag (`mcr.microsoft.com/dotnet/sdk:10.0.302`, not the
floating `10.0` tag) for the same reason — a Docker build is exactly the kind of compile that
would otherwise silently pick up a newer, broken feature band outside of CI's control.

### Setting Up the Development Environment

1. Fork this repository
2. Clone your fork locally
3. Configure the demo environment variables as described in the
  [Environment Configuration](#environment-configuration) section below.

If you prefer, you can use `direnv`, as documented in the [Environment Configuration](#environment-configuration) section below.

### Start the application

```bash
dotnet run
```

Open your browser to `http://localhost:5173`

### Simulating the launch sequence

In production a User reaches GenPRES from MainEHR: the LaunchScript opens the browser on a
sealed Launch, the browser is signed on at the IdentityProvider, and the server opens a Session
for the launched patient (steps 1 to 6 of [uc-01](docs/scenarios/integration/uc-01-launch.md)).
In demo mode the server hosts stand-ins for every party outside GenPRES, so the whole sequence
runs on one machine with nothing to install
([plan 605](docs/implementation-plans/605-launch-with-server-stubs.md)). The stand-ins are
mounted only when `GENPRES_PROD=0`; a production server answers 404 on their routes and refuses
every launch as invalid until the scope switch
([#580](https://github.com/informedica/GenPRES/issues/580)) decides what production exposes.

#### Walkthrough

1. Start the application with `GENPRES_PROD=0` (the `.env.example` default): `dotnet run`.
2. Open `http://localhost:5173/stub/launch`. This is the stub LaunchScript page, served by the
   server on port 8085 and reached through the Vite proxy. It has two fields:
   - **PatientId**, default `stub-patient`. Any text works; `no-data` stands for a patient the
     PatientDataPlatform has no record for (ext 6a).
   - **Identity at the browser**: who the stub IdentityProvider will say is signed on. The
     table below lists the choices.
3. Press **Launch**. The server mints a Launch sealed under a key it made at start-up, valid for
   two minutes, and redirects to `#/session?launch=<token>`. The client erases the token from
   the address bar and history, generates a key pair, and presents the Launch.
4. The server answers with a redirect to `/authorize` (the stub IdentityProvider), which sends
   the browser straight back to `/callback` with a one-time code. The server redeems the code,
   asks the stub UserRegistry for the role and the active patient, reads the patient data, and
   opens the Session in one act.
5. The browser lands on `#/session`. With `prescriber` the title bar shows a person button with
   **Stub Prescriber** and the role; the session menu offers **Close session**.

What each identity choice ends in:

| Identity | Stands for | Ends in | uc-01 |
|---|---|---|---|
| `prescriber` | a Prescriber whose active patient is the launched one | an open Session as Prescriber | main path |
| `prescriber-b` | a second Prescriber on the same patient, for two browsers (UC-3, Rule 20) | an open Session as Stub Prescriber B | main path |
| `reader` | a Reader; no PIN needed | an open Session as Reader | ext 5c |
| `prescriber-other-patient` | a Prescriber with another patient active in MainEHR | the gate: wrong patient, relaunch | ext 5b |
| `no-pin` | a Prescriber without a PIN | the enrolment form: a confirmation code by mail, then a PIN (UC-2, below) | ext 5d |
| `unknown` | a login the UserRegistry does not know | the gate: no role, with "continue without launch" | ext 5a |
| `none` | nobody signed on at the browser | the gate: no browser identity, relaunch only | ext 3c |

A refusal arrives as `#/session?refused=<word>` with the words `expired`, `spent`, `invalid`,
`no-identity`, `no-role`, `wrong-patient` and `enrolment`; the client erases the parameter and
shows the gate for it.

#### Enrolment: the first launch of a Prescriber without a PIN

A Prescriber has to set a PIN before prescribing
([uc-02](docs/scenarios/integration/uc-02-enrolment.md), Rules 24, 25, 37). The demo stands in
for the MailService too ([plan 615](docs/implementation-plans/615-enrolment-with-server-stubs.md)):

1. Launch with the identity `no-pin`. The hop runs as above, but instead of opening a Session
   the server mails a six-digit confirmation code and the browser lands on the gate **Set a PIN
   to continue**, which greets the user and says where the code went (`n***@stub.example`).
2. Open `http://localhost:5173/stub/mail` in another tab: the stub MailService's outbox, newest
   mail first. Copy the code from "GenPRES: your confirmation code".
3. Enter the code, a PIN of four to six digits, and the PIN again, and press **Set PIN**. The
   Session opens as **Stub Prescriber (no PIN)**, and the outbox shows a second mail, "GenPRES:
   your PIN was set".
4. Launch `no-pin` again, in this or another browser: the Session opens directly. The PIN lives
   as long as the server runs. The seeded Prescribers (`prescriber`, `prescriber-b`,
   `prescriber-other-patient`) start with the PIN `1234`.

Things worth trying here:

- **A wrong code**: the form stays, with the tries left. The third wrong code voids it; a fresh
  launch mails a fresh one.
- **A second launch while the code stands** (another tab or browser, `no-pin` again): no second
  mail; either browser can enter the code, and the Session opens in the one that did.
- **Waiting**: the code lives fifteen minutes; after that the form is gone at the next reload
  and a fresh launch mails a new code.
- **Leaving**: **Close session** is not offered while enrolling; a relaunch replaces the
  attempt, and closing the tab abandons it.

#### Signing an order plan

A Prescriber signs the order plan with the PIN; signing is the only way anything reaches the
record ([uc-03](docs/scenarios/integration/uc-03-prescribe-and-sign.md), Rules 42, 43). The
demo keeps the record in memory
([plan 622](docs/implementation-plans/622-signing-with-server-stubs.md)):

1. Launch with the identity `prescriber`. The stub patient has no data, so give it an age in the
   patient panel (a weight is estimated from it).
2. Open **Voorschrijven** from the menu, pick a medication, a route, a form and an indication
   (paracetamol, oral, tablet, mild pain will do), and press **Voorschrijven** on a scenario.
3. Open **Order Plan**: the order is in the plan, with an **Ondertekenen** button above it.
   Press it. The dialog lists the orders exactly as they will be signed and asks the PIN.
4. Enter a wrong PIN: the dialog stays and says two tries are left. Enter `1234`: the dialog
   closes and the snackbar says version 1 was signed by Stub Prescriber. Sign again: version 2.

Things worth trying here:

- **Three wrong PINs**: the dialog closes and the gate says the Session ended at the PIN limit;
  the Sign button and the person menu are gone. Launch again: a right PIN inside the next minute
  is refused as locked until a time, after that it signs. Every wrong entry past the third
  doubles the delay, up to a day.
- **Launch again**: the cart opens on the version just signed, before anything is entered
  (Rule 19); so does a reload.
- **Two browsers on one patient** ([uc-04](docs/scenarios/integration/uc-04-two-users.md),
  [plan 635](docs/implementation-plans/635-session-bound-compute.md)): launch `prescriber-b` in
  another browser profile, prescribe and sign there. The first browser's next action that
  reaches the server (a change in the patient panel, a scenario) shows a snackbar once: Stub
  Prescriber B signed a newer version at that time, and the **Order Plan** page a bar with
  **Open the newest version** (Rule 21). Nothing is blocked by it (Rule 22): pressing
  **Ondertekenen** instead is refused with the same words (Rule 20) and shows the same bar. The
  button loads B's orders into the cart, says version N by Stub Prescriber B is now open, and
  signing works again: version N+1 has B's as its base. A page switch alone sends nothing, so
  it tells nothing.
- **The same identity twice**: launch `prescriber` again in another browser profile. The first
  browser's next action shows the gate: a newer launch ended the Session (Rule 11). In the same
  profile the second launch replaces the session cookie instead, and the older tab simply
  continues on the new Session.
- **The patient without data**: launch with the PatientId `no-data`. The first **Ondertekenen**
  is a notice instead of the dialog: the data could not be verified. **Doorgaan** asks the
  challenge again with the notice accepted, and the version is signed as unverified (Rule 44).
- **A Reader**: `reader` sees no Sign button.
- **Watch the wire**: `RequestSignChallenge` answers `ChallengeIssued`, `Submit` answers
  `Submitted` with the version and a fresh OpenedToken; the PIN travels in the Submission and
  nowhere else, and never appears in the log. A Submission sent twice under the same key is
  answered the same way once. Every `processCommand` call sends `{ Opened; Command }` and gets
  `{ Response; Notice }`: the notice names the newer version or the ending, and is empty
  otherwise; `OpenVersion` answers the Session with a fresh OpenedToken over the version.
- **Restart the server**: the record is gone with everything else; the next signature is
  version 1 again.

#### Things worth trying

- **Reload after the launch**: the Session resumes from the `genpres_session` cookie.
- **Replay the Launch**: copy the `#/session?launch=…` URL from the Network tab (it never stays
  in the address bar) and open it in another browser profile or an incognito window within two
  minutes: `spent`. After two minutes: `expired`. A token from an earlier server run: `invalid`,
  the key is new at every start.
- **Reload the callback**: reload `/callback?code=…&state=…` from the Network tab within two
  minutes: the same answer as the first time (Rule 45), no second Session.
- **Two launches of the same user**: launch as `prescriber` in tab A, then again in tab B. B is
  open; A's next request is told that a newer launch ended its Session and offers to continue
  without a launch (Rules 8 and 11). Once A acknowledged, the notice is gone.
- **Watch the wire**: in the Network tab, the `PresentLaunch` call answers `RedirectTo`, then
  two 302s (`/authorize`, `/callback`), then `GetSession`. The Launch appears in none of the
  responses after the first request.
- **Production**: `GENPRES_PROD=1 GENPRES_PASSWORD=<16+ chars> dotnet run`; `/stub/launch`
  and `/authorize` are 404, `/callback` redirects to `refused=invalid`.

The stand-ins keep everything in memory: launches by nonce, sessions, endings, one-time codes,
credentials and confirmation codes, the outbox, the signed versions of every order plan. A
restart forgets all of it: the browser's session cookie no longer finds a Session, `no-pin` has
to enrol again, and the record starts from nothing.

#### Cookies and the development proxy

| Cookie | Set by | Attributes | Purpose |
|---|---|---|---|
| `genpres_session` | the callback, on an open | HttpOnly, Strict, `Path=/`, Secure over HTTPS | names the Session (Rule 12) |
| `genpres_launch_state.<state>` | the answer to `PresentLaunch` | HttpOnly, Lax, `Path=/callback`, `Max-Age` 2 min | proves the callback comes from the browser that started the hop; one per hop so two tabs can launch at once |
| `genpres_stub_identity` | the stub launch page | HttpOnly, Lax, `Path=/`, `Max-Age` 2 min | carries the identity choice and the PatientId to the stub IdentityProvider; demo only |
| `genpres_enrolment` | the callback, when the launch suspends into enrolment | HttpOnly, Strict, `Path=/`, `Max-Age` what remains of the code's fifteen minutes | names the enrolment attempt this browser made; the form's `SupplyPin` works on it and nothing else (UC-2) |

`vite.config.js` proxies `/api`, `/stub`, `/authorize` and `/callback` to the server on port
8085, so in development the browser talks to one origin (`localhost:5173`) and the cookies,
which are host-only and port-agnostic, reach both. In production the server serves the client
itself and no proxy is involved.

## Build System Architecture

### How `dotnet run` Interacts with FAKE

GenPRES uses [FAKE](https://fake.build/) (F# Make) as its build automation tool. The build configuration lives in two files at the repository root:

- **`Build.fs`** – defines all FAKE build targets (tasks) and their dependency chains
- **`Helpers.fs`** – helper functions for running processes (dotnet, npm, docker) in the build

When you type `dotnet run` from the repository root, .NET executes `Build.fsproj`, which is an F# console application that initializes the FAKE execution context. FAKE then reads the target name from the command-line arguments (defaulting to `Run` when none is given) and executes the corresponding target and all of its declared dependencies.

`Build.fsproj` has to live in the repository root: bare `dotnet run` resolves its project only
from the current directory (there is no configuration or environment variable to redirect it),
so moving it into a subfolder would turn every invocation into `dotnet run --project <dir> <target>`.
The same root placement is why plain `dotnet build` / `dotnet test` fail with MSB1011 — both the
solution and this project file are candidates — and must be given `GenPRES.sln` explicitly.

`Build.fsproj` is listed in `GenPRES.sln` so that editors that load projects from the solution
(Ionide, Rider) give `Build.fs` and `Helpers.fs` IntelliSense. It is deliberately **not** part of
the solution build: its solution entry has `ActiveCfg` lines only, no `Build.0` lines, so
`dotnet build GenPRES.sln` — which the `Build` target runs from inside the running build
executable — never tries to overwrite `Build.dll` while it is executing. `dotnet run` builds
the project itself, and `scripts/CheckSolutionVersions.fsx` skips it as a non-shipped project.

```text
dotnet run [target]
     │
     └─► Build.fsproj (F# console app)
              │
              └─► FAKE target engine
                       │
                       ├─► resolves target dependency chain
                       └─► executes each target step
```

For example, `dotnet run` (no target) runs the `Run` target, which depends on
two independent prerequisites: `Build` (compiles the server, no npm involved)
and `Clean → RestoreClient` (clears stale Fable output, then restores npm
packages for the Fable/Vite dev server).

### FAKE Build Targets Reference

| Command | Target | Description |
|---|---|---|
| `dotnet run` | `Run` | Start server + Fable/Vite dev server with hot reload (default). Creates `.env` from `.env.example` when it is missing |
| `dotnet run list` | *(special)* | List all available FAKE targets |
| `dotnet run Build` | `Build` | Compile the entire solution (`GenPRES.sln`) — libraries, server, tests, and the client `.fsproj`. No npm involved |
| `dotnet run ServerBuild` | `ServerBuild` | Compile only the server and the libraries it depends on. Skips test projects and the client toolchain |
| `dotnet run BenchmarkBuild` | `BenchmarkBuild` | Compile the four benchmark projects under `benchmark/` in Release. They are excluded from `GenPRES.sln`; CI runs this target in a separate `benchmark` job |
| `dotnet run ClientBuild` | `ClientBuild` | Compile the client: Fable (F# → `.jsx`) then a production Vite bundle. Runs `npm ci` first via `RestoreClient` |
| `dotnet run Clean` | `Clean` | Remove `deploy/` and `dist/` artifacts, delete Fable-generated `.jsx` files |
| `dotnet run Bundle` | `Bundle` | Production build: publish server, compile client, copy data |
| `dotnet run ServerTests` | `ServerTests` | Run all F# unit tests (Expecto) with quiet logging |
| `dotnet run CheckVersions` | `CheckVersions` | Verify every built DLL's version matches the root `Directory.Build.props` |
| `dotnet run TestHeadless` | `TestHeadless` | Build and run tests without launching a browser |
| `dotnet run WatchTests` | `WatchTests` | Run tests in watch mode (re-runs on file changes) |
| `dotnet run Format` | `Format` | Format all F# source files using Fantomas |
| `dotnet run ApiDocs` | `ApiDocs` | Build the fsdocs API reference for the `Informedica.*.Lib` libraries in Release into `./output/`. CI (`docs.yml`) publishes it to GitHub Pages on push to `master`. Set `FSDOCS_ROOT` to the site base URL (CI passes `https://informedica.github.io/GenPRES/`) |
| `dotnet run ApiDocsWatch` | `ApiDocsWatch` | Local live-preview server for the API reference; rebuilds on changes to `docs/reference/` or the libraries' XML doc comments |
| `dotnet run DockerBuild` | `DockerBuild` | Build the production image (`informedica/genpres` by default, override with `DOCKER_IMAGE`), labeling it with the version from the root `Directory.Build.props` |
| `dotnet run DockerRun` | `DockerRun` | Run the built image locally, using `GENPRES_URL_ID`/`GENPRES_PASSWORD` from the current environment (source `.env` first) |

#### Target Dependency Chains

```text
Clean ──► RestoreClient ──► Bundle
Clean ──► RestoreClient ──► ClientBuild

ServerBuild            (no prerequisites — restores itself)
BenchmarkBuild         (no prerequisites — restores itself)
ApiDocs                (no prerequisites — restores and builds Release itself)
ApiDocsWatch           (no prerequisites — restores and builds Release itself)

Build ──► Run
RestoreClient ──► Run

Build ──► TestHeadless
RestoreClient ──► TestHeadless

Build ──► WatchTests
RestoreClient ──► WatchTests

Build ──► ServerTests
Build ──► CheckVersions
```

`ServerBuild` and `ClientBuild` are **additive**: nothing depends on them, and `Build`
does not use them. `Build` still builds the test projects because `ServerTests` runs
`dotnet test --no-restore`. It also stays npm-free so CI does not run `npm ci` and a
Fable compile for every test run. Thus, `Build` remains the full-solution build,
while the new targets compile either side separately.

### Paket groups

Packages are managed by [Paket](https://fsprojects.github.io/Paket/) from the single root
`paket.dependencies` / `paket.lock`; each project lists what it uses in its own `paket.references`.
The dependencies file is split into five groups, each resolved independently so that packages
needed only by one side of the repo never influence the version resolution of the shipped code:

| Group | Used by | Contents |
|---|---|---|
| `Main` (the unnamed first section) | `src/` libraries, server, shared contract | everything shipped, including `Unquote` and `IcedTasks`, which the libraries use |
| `Client` | `src/Informedica.GenPRES.Client` | Fable, Elmish, Feliz |
| `Test` | `tests/*` | Expecto, FsCheck, the test SDK and adapter |
| `Build` | the root `Build.fsproj` | FAKE |
| `Benchmark` | `benchmark/*` | BenchmarkDotNet (see [#513](https://github.com/informedica/GenPRES/issues/513)) |

A `paket.references` file names a group with a `group <Name>` line; everything above the first
such line is `Main`. Each project references exactly one group: the `src/` projects `Main`, the
client `Client`, the test projects `Test`, the root build project `Build`. The `benchmark/`
projects are the one exception and list `Main` and `Benchmark`.

The one-group-per-project rule is not cosmetic. Paket emits one `PackageReference` (and one
`PackageVersion`) per group and per package and never de-duplicates across groups, so a project
that listed `Main` and `Test` would get `FSharp.Core` twice and NuGet would warn `NU1504` /
`NU1506` on every restore. Test projects therefore do **not** list `Unquote`,
`MathNet.Numerics.FSharp` or `IcedTasks` themselves: those flow to them transitively through their
`ProjectReference` to the `src/` library under test, exactly as they would in any SDK-style
project, at the version `Main` pins. `FSharp.Core` they list from the `Test` group.

`FSharp.Core` is pinned to the same version in every group on purpose, so that the copy a test or
client project gets from its own group agrees with the copy that flows in from `Main` through the
project reference. Bump the pin in all four places together.

To add a package: put the `nuget` line in the group that matches its consumer, add it to the
consuming project's `paket.references` under that group, run `dotnet paket install`, and commit
`paket.dependencies`, `paket.lock` and the touched `paket.references` files.

### Changelog & Release Automation (EasyBuild.ShipIt)

GenPRES uses [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt) to derive
the next semantic version and changelog entries from conventional-commit history — see
[ADR-0005](docs/adr/0005-build-system-versioning-and-release.md) for the full
design. It is registered as a local dotnet tool (`.config/dotnet-tools.json`) and configured via
YAML front matter at the top of the root `CHANGELOG.md`.

ShipIt runs in CI on every push to `master` (see [Release Automation](#release-automation-github-actions)
below) and owns the version number: the `updaters:` block in the `CHANGELOG.md` front matter points at 
`/Project/PropertyGroup/Version` in the root `Directory.Build.props`, so the release PR bumps that
element as well as adding the changelog section. A second, `regex` updater rewrites the default
image tag in the root `compose.yaml` (`informedica/genpres:${GENPRES_IMAGE_TAG:-<version>}`) so a
`git pull && docker compose pull && docker compose up -d` after a release runs the version that was
just shipped. Do not hand-edit `<Version>` or that compose default.

The `regex` updater writes the version verbatim, whereas `tag-release.yml` folds any `+` in the
version to `-` because `+` is not a legal Docker tag character. The two agree only as long as
`<Version>` carries no SemVer build metadata. ShipIt never generates build metadata (versions come
from conventional commits plus the `alpha.N` pre-release counter), so this holds unless someone
hand-edits `<Version>` with a `+`, which is already disallowed above. If build metadata is ever
introduced deliberately, replace the `regex` updater with a `command` updater that applies the same
fold before writing `compose.yaml`.

To preview locally what ShipIt would generate:

```bash
dotnet tool restore
dotnet shipit --dry-run --allow-branch master --skip-merge-commit
```

`--allow-branch` defaults to `main`; GenPRES's default branch is `master`, so it must be passed
explicitly (`release.yml` passes it too). `--skip-merge-commit` is required for every invocation. 
All three merge methods are enabled on the repo, so `Merge pull request ...` commits will keep 
appearing in history, and ShipIt throws on the first one it hits instead of skipping it. `--dry-run`
never modifies files or opens a pull request, so it's safe to run against a dirty tree.

#### What reaches the changelog

The behavior below was established by running ShipIt 3.0.1 against a throwaway branch of this
repo. ShipIt's own documentation covers none of it.

- **`docs`, `build`, and `chore` commits never render.** Only types like `feat` and `fix` produce
  entries, and no flag or escape hatch changes that. A change that must appear in the release notes
  has to ride on a rendering commit type.
- **Commits that change no files are ignored**, whatever their type.
- **A `=== changelog ===` block adds detail to an entry that already renders.** It is read from the
  **commit message body**, not the pull request body, and needs both an opening *and* a closing
  `=== changelog ===` marker. An unterminated block is dropped silently, with no warning:

  ```text
  fix(server): correct the infusion rate rounding

  === changelog ===
  Rates were rounded to whole mL/h, truncating paediatric doses below 1 mL/h.
  === changelog ===
  ```

  That renders the prose indented beneath the commit's bullet.

Putting the block in a PR body only works when the merge method copies that body into the commit
message, which squash-merging does by default and merge-commit merging never does. Since all three
merge methods are enabled here, put it in the commit message.

### What Happens During `dotnet run` (the `Run` target)

The `Run` target first makes sure a `.env` file exists: when there is none, it copies
`.env.example` (public demo sheet ID, `GENPRES_PROD=0`, empty `GENPRES_PASSWORD`) and prints a
notice, so a fresh clone or `git worktree` runs the demo without a manual `cp`. The seeded server
has admin operations disabled, because the password is empty; set `GENPRES_PASSWORD` in `.env` to
enable them locally. An existing `.env` is never touched. Only
`Run` does this; `Build`, `ServerTests` and `Bundle` keep working without a `.env`, as they do in CI.

Then it starts two long-running processes **in parallel**:

1. **Server** – `dotnet run --no-restore` in `src/Informedica.GenPRES.Server/`
   - Saturn/Giraffe HTTP server on port `8085`
2. **Client** – `dotnet fable watch … --run npx vite` in `src/Informedica.GenPRES.Client/`
   - Fable compiles F# → JavaScript, Vite serves the client on `http://localhost:5173` with Hot Module Replacement (HMR)

Output from both processes is printed concurrently with color-coded prefixes (`server:`, `client:`).

### Helper Shell Scripts

The project uses a small number of bash helper scripts to wrap common `dotnet run`, `docker build`, `docker run`, and Fantomas-hook invocations. They fall into **two categories**:

1. **Tracked scripts** — committed to the repo. Available immediately after `git clone`.
2. **Optional local scripts** — recipes you can paste into your working copy as a personal convenience. They are deliberately **not** committed: the opt-in `.gitignore` strategy (`*` followed by explicit `!path` allow-lines) excludes them so each developer can keep their own variants without polluting the repo.

Common conventions for both categories:

- Every script starts with `#!/usr/bin/env bash` so it stays portable across Linux and macOS.
- After creating a local script, mark it executable: `chmod +x scriptname.sh`.
- Run from the **repo root** (e.g. `./debug.sh`), with the single exception of `benchmark/run.sh`, which is invoked from the `benchmark/` directory.
- Scripts that use environment variables source the repo-root `.env` file via `set -a; source .env; set +a`. See [Environment Configuration](#environment-configuration) for what `.env` contains and how the priority order works.

#### Tracked scripts (in the repo)

These three scripts ship with the repository and are listed explicitly in `.gitignore` with `!` allow-entries.

- **`debugTests.sh`** — sources `.env`, then iterates through eight test projects (`Utils`, `Agents`, `Logging`, `GenUnits`, `GenCore`, `GenSolver`, `GenForm`, `GenOrder`, plus the `Server` test project) and runs each with `dotnet run --project <proj> -- --debug --summary --sequenced`. Exits non-zero on the first failure. Similar to `dotnet run ServerTests` but with per-project isolation, debug output, and forced sequential execution — useful when chasing flaky tests or test interactions. The project list is hardcoded; if you add a new test project, update both this script and the `ServerTests` FAKE target.
- **`benchmark/run.sh`** — runs `sudo dotnet run -c Release "$@"`. Must be invoked from the `benchmark/` directory; it does not `cd` for you. The `sudo` is required because some BenchmarkDotNet diagnostics need elevated privileges. Extra arguments are forwarded to `dotnet run`. The `benchmark/` projects are part of the root paket root, with their packages in a separate `group Benchmark` in `paket.dependencies` so that BenchmarkDotNet's transitive tree never influences the `Main` resolution (see [#513](https://github.com/informedica/GenPRES/issues/513) and [Paket groups](#paket-groups) above); `dotnet run BenchmarkBuild` compiles all four benchmark projects without running them.
- **`.husky/scripts/format-staged.sh`** — invoked by the Husky pre-commit hook. Receives staged F# files as positional arguments, warns about partially-staged files (Fantomas formats the *full working-tree* version of each file, not just the staged hunks), runs `dotnet fantomas` on them, and re-stages the formatted output. You normally never call this directly; it runs automatically on `git commit`. See also [CONTRIBUTING.md](CONTRIBUTING.md#code-formatting-pre-commit-hook).

#### Optional local scripts (not in the repo — paste into your working copy)

Everything in this subsection is a **template**. Nothing here exists after a fresh `git clone` — `git status` will not show these files even after you create them, because the opt-in `.gitignore` excludes them by design. Save each block at the path indicated, run `chmod +x` once, and you're done.

##### Run-mode wrappers (`dotnet run`)

Five wrappers launch the full stack with different `GENPRES_*` presets. They all source `.env` first and then export overrides — the exported values **win** over anything coming from `.env`. For the full priority order, see [Environment Configuration](#environment-configuration).

| File | Mode | `GENPRES_LOG` | `GENPRES_PROD` | `GENPRES_DEBUG` | Purpose |
|---|---|---|---|---|---|
| `debug.sh` | Demo, info logging | `i` | `0` | `1` | Default for local development against the demo dataset. |
| `debugprod.sh` | Production data, debug logging | `d` | `1` | `1` | Clears the log folder first. Requires a real `GENPRES_URL_ID` in `.env`. |
| `infoprod.sh` | Production data, info logging | `i` | `1` | `1` | Clears the log folder first. Less verbose than `debugprod.sh`. |
| `logprod.sh` | Production data, info logging, no debug | `i` | `1` | `0` | Same logging level as `infoprod.sh` but with the debug flag off. |
| `prod.sh` | Production data, no logging | `0` | `1` | `0` | Mirrors a real production launch locally. |

**`debug.sh`** — save at the repo root:

```bash
#!/usr/bin/env bash
# Load env vars from .env (GENPRES_URL_ID etc.)
set -a; source .env; set +a

# Override for debug mode
export GENPRES_LOG=i
export GENPRES_PROD=0
export GENPRES_DEBUG=1

dotnet run
```

**`debugprod.sh`** — save at the repo root:

```bash
#!/usr/bin/env bash
# clear ./data/logs folder
if [ -d "./data/logs" ]; then
    echo "Clearing logs folder..."
    rm -rf ./data/logs/*
    echo "Logs folder cleared."
else
    echo "Logs folder does not exist, creating it..."
    mkdir -p ./data/logs
fi

# Load env vars from .env (GENPRES_URL_ID etc.)
set -a; source .env; set +a

# Override for debug-production mode
export GENPRES_LOG="d"
export GENPRES_PROD=1
export GENPRES_DEBUG=1

dotnet run
```

**`infoprod.sh`** — save at the repo root. Same shape as `debugprod.sh`, but with `GENPRES_LOG="i"`:

```bash
#!/usr/bin/env bash
# clear ./data/logs folder
if [ -d "./data/logs" ]; then
    echo "Clearing logs folder..."
    rm -rf ./data/logs/*
    echo "Logs folder cleared."
else
    echo "Logs folder does not exist, creating it..."
    mkdir -p ./data/logs
fi

# Load env vars from .env (GENPRES_URL_ID etc.)
set -a; source .env; set +a

# Override for info-production mode
export GENPRES_LOG="i"
export GENPRES_PROD=1
export GENPRES_DEBUG=1

dotnet run
```

**`logprod.sh`** — save at the repo root:

```bash
#!/usr/bin/env bash
# Load env vars from .env (GENPRES_URL_ID etc.)
set -a; source .env; set +a

# Override for log-production mode
export GENPRES_LOG=i
export GENPRES_PROD=1
export GENPRES_DEBUG=0

dotnet run
```

**`prod.sh`** — save at the repo root:

```bash
#!/usr/bin/env bash
# Load env vars from .env (GENPRES_URL_ID etc.)
set -a; source .env; set +a

# Override for production mode
export GENPRES_LOG=0
export GENPRES_PROD=1
export GENPRES_DEBUG=0

dotnet run
```

##### Docker wrappers

Building and running the image no longer needs a hand-copied shell script: the `DockerBuild` and `DockerRun` FAKE targets (see [FAKE Build Targets Reference](#fake-build-targets-reference)) cover both, work identically from PowerShell, Git Bash, or any POSIX shell, and are tracked in `Build.fs` rather than living only as documentation. Neither target bakes `GENPRES_URL_ID` into the image — that constraint is enforced by the `Dockerfile` itself and described in [Environment Configuration](#environment-configuration).

**Build** — `dotnet run DockerBuild` reads the app's single curated version number from the root `Directory.Build.props` and passes it to `docker build --build-arg APP_VERSION=...`, so the image's `org.opencontainers.image.version` label always matches what was built. To cross-build for a different platform set `DOCKER_PLATFORM`; to tag/push under your own name instead of the project's `informedica/genpres` default, set `DOCKER_IMAGE` (both `DockerBuild` and `DockerRun` read it).

```bash
# local architecture
dotnet run DockerBuild

# cross-build amd64
DOCKER_PLATFORM=linux/amd64 dotnet run DockerBuild
```

```powershell
# cross-build amd64 (PowerShell)
$env:DOCKER_PLATFORM = "linux/amd64"
dotnet run DockerBuild
```

**Run** — `dotnet run DockerRun` reads `GENPRES_URL_ID` and `GENPRES_PASSWORD` from the current environment and fails fast with an error if either is missing, rather than starting an unauthenticated container that the in-server `validateProductionPassword` would refuse later. Source `.env` first (single source of truth — same as `prod.sh` / `debug.sh`):

```bash
set -a; source .env; set +a
dotnet run DockerRun
```

```powershell
Get-Content .env | ForEach-Object {
    if ($_ -match '^\s*([^#=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1].Trim(), $Matches[2].Trim())
    }
}
dotnet run DockerRun
```

**Run a published image** — the `compose.yaml` at the repo root (tracked; see the `!compose.yaml` allow-line in `.gitignore`) runs an image pulled from Docker Hub with port, secrets and mode read from `.env`, so nothing has to be retyped after a new release. Compose interpolates `${...}` from the `.env` in the project directory by itself; no `source` needed. The image tag defaults to the current release: ShipIt bumps it in `compose.yaml` as part of every release PR (see [Changelog & Release Automation](#changelog--release-automation-easybuildshipit)), so `git pull` brings the new tag along. Set `GENPRES_IMAGE_TAG` in `.env` only to pin a different version. If you added `GENPRES_IMAGE_TAG` to `.env` while it was still required (between [#550](https://github.com/informedica/GenPRES/pull/550) and [#552](https://github.com/informedica/GenPRES/pull/552)), remove it: a leftover value silently overrides the ShipIt-maintained default and keeps `docker compose pull` on the old image.

```bash
cp .env.example .env            # once; for production, set the secrets
git pull && docker compose pull # after each release
docker compose up -d            # (re)creates container "genpres" on http://localhost:8080
docker compose logs -f genpres
```

The image is published by `tag-release.yml` a few minutes *after* the release PR merges, so a `docker compose pull` in that window fails with "manifest unknown"; retry shortly after.

Demo or production is whatever `GENPRES_PROD` says in `.env`. The image itself defaults to demo (`GENPRES_PROD=0`, public demo sheet ID, no password — issue [#541](https://github.com/informedica/GenPRES/issues/541)), so a bare `docker run -p 8080:8085 informedica/genpres:<tag>` or the Docker Desktop "Run" button also works with no flags. `GENPRES_PROD=1` additionally needs the proprietary `GENPRES_URL_ID` and the `data/cache` bind mount that `compose.yaml` already declares: production reads `*.cache`, and the image ships only the `*.demo` files. A 16+ character `GENPRES_PASSWORD` enables the admin operations; without one the server starts with them disabled and warns (issue #590). `compose.yaml` forwards only the `GENPRES_*` keys, not the whole `.env`, so unrelated local secrets stay out of the container. Unlike `dotnet run DockerRun`, this needs no .NET SDK on the host, runs the exact published image rather than a local build, and includes the cache mount.

**Process 1 and exit codes** — the image runs [`tini`](https://github.com/krallin/tini) as PID 1
and starts `dotnet` under it (issue [#572](https://github.com/informedica/GenPRES/issues/572)).
With `dotnet` itself as PID 1, the `SIGABRT` the runtime sends itself after an unhandled exception
was dropped, so a container whose server refused to start stayed "running" with nothing listening.
Now a refused start-up (a production password shorter than 16 characters, an unknown
`GENPRES_LANG`, or no `GENPRES_URL_ID`) prints one message
and exits `1`, a crash exits `134`, and `docker stop` still reaches Kestrel for a graceful shutdown.
`compose.yaml`'s `restart: unless-stopped` and any orchestrator act on those codes; read them with
`docker ps -a`. No `--init` or `init: true` is needed. The release workflow proves this on every
image it publishes by starting it with `GENPRES_PROD=1` and no password and requiring exit code
`1` within 30 seconds.

**Browser caching after an update** — the server sets `Cache-Control` on every response
(`securityHeadersMiddleware` in `src/Informedica.GenPRES.Server/Server.fs`, issue
[#568](https://github.com/informedica/GenPRES/issues/568)): `no-cache` for `index.html` and
everything else, `public, max-age=31536000, immutable` for a successful response of a content-hashed
bundle under `/assets/` (a 404 there stays `no-cache`, so a bundle this instance does not have yet is
retried). A browser therefore revalidates the entry document on every load (a cheap `ETag` 304
when nothing changed) and picks up a new container without a hard refresh. Two caveats: a browser
that cached `index.html` *before* this header existed still needs one hard reload
(Cmd/Ctrl+Shift+R); and on a Plesk host with "Serve static files directly by nginx" including
`html`, nginx answers `index.html` from disk and Kestrel's header never reaches the browser, so
either drop `html`/`htm` from that list or add `location = / { add_header Cache-Control "no-cache"; }`
and the same for `location = /index.html` to the per-site nginx directives. Check with
`curl -sI https://<host>/ | grep -i cache-control`.

If you find yourself wanting to commit one of these local scripts (e.g. because the team agrees it should be standardized), add a `!`-prefixed allow-line for the file to `.gitignore` in the same PR — otherwise the opt-in strategy will silently keep it untracked.

### CI/CD Pipeline (GitHub Actions)

The CI pipeline is defined in `.github/workflows/build.yml` and runs on every push or pull request to `master` across three operating systems:

| Matrix | OS |
|---|---|
| ubuntu-latest | Linux |
| windows-latest | Windows |
| macOS-latest | macOS |

**Pipeline steps:**

1. **Checkout** – `actions/checkout@v4`
2. **Install .NET SDK** – installs .NET 10.0 via `actions/setup-dotnet`
3. **Tool restore** – `dotnet tool restore` (installs paket, fable, fantomas, husky from `.config/dotnet-tools.json`)
4. **Format check** – `dotnet fantomas --check .` (fails the build on unformatted code)
5. **Test execution** – `dotnet run ServerTests` (runs all Expecto tests)

Environment variables set in CI (from `.github/workflows/build.yml`):

```yaml
env:
  CI: true          # Disables interactive prompts
  GENPRES_DEBUG: 1  # Enables debug logging during test runs
```

The pipeline does **not** set `GENPRES_URL_ID`, so tests run against demo/cached data only. Production data is never accessed in CI.

A fourth job, `benchmark`, runs `dotnet run BenchmarkBuild` on `ubuntu-latest` alongside the matrix.
The benchmark projects are outside `GenPRES.sln`, so the matrix never compiles them, and they rotted
unnoticed until [#513](https://github.com/informedica/GenPRES/issues/513). The job is separate from the
matrix on purpose: it runs in parallel and finishes inside the windows leg's duration, so it adds no
wall-clock time to a run, only about three runner-minutes. It compiles only; BenchmarkDotNet runs stay a
local activity (see `benchmark/run.sh`).

### API Documentation (GitHub Actions)

`.github/workflows/docs.yml` runs `dotnet run ApiDocs` on every push to `master` and publishes the fsdocs 
output to GitHub Pages at `https://informedica.github.io/GenPRES/`
([#460](https://github.com/informedica/GenPRES/issues/460)). Like `release.yml` it is a separate workflow 
rather than a job in `build.yml`: a docs-build failure must not block the test/format matrix, and a red 
test run must not stop the reference from refreshing. It does not run on pull requests — there is nowhere 
to publish a PR build and the Release solution build is not worth spending per branch push.

**One-time repo setup (admin)**: Settings → Pages → Build and deployment → Source = "GitHub
Actions". Until that is set, the `deploy` job fails with "Pages site not found".

### Release Automation (GitHub Actions)

`.github/workflows/release.yml` runs [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt)
on every push to `master`, opening or updating a draft release PR with the next derived version and changelog 
section. It is deliberately a separate workflow from `build.yml`, not a job within it: a ShipIt failure must 
never block the test/format matrix that already gated the PR which produced the push. See 
[ADR-0005](docs/adr/0005-build-system-versioning-and-release.md) for the full design and the
[implementation plan](docs/implementation-plans/234-improve-build-system.md) for status.

This replaces the "Repo Assist" bot's former Task 8 ("Release Preparation", `.github/workflows/repo-assist.md`), 
retired in the same change to avoid two bots proposing competing release PRs on the same merge.

**One-time repo setting required**: ShipIt opens PRs using the workflow's own `GITHUB_TOKEN`, which requires 
**Settings → Actions → General → "Allow GitHub Actions to create and approve pull requests"** to be enabled. 
Without it, `release.yml` runs but fails to open the PR.

#### Tagging and publishing the Release

`.github/workflows/tag-release.yml` turns a merged release PR into the immutable artifact ShipIt itself
cannot produce — ShipIt 3.0.1 has no tag or Release capability in any mode, verified against the installed
assembly rather than its documentation (see [ADR-0005](docs/adr/0005-build-system-versioning-and-release.md)
and [issue #470](https://github.com/informedica/GenPRES/issues/470)). The workflow:

1. Checks out the **merge commit** (`pull_request.merge_commit_sha`) — the state `master` was actually in
   when the version shipped, and a commit that stays reachable after ShipIt reuses `release/master`.
2. Runs `scripts/ReleaseNotes.fsx`, which reads `<Version>` from the root `Directory.Build.props` (that the
   merged PR just updated) and extracts that version's `CHANGELOG.md` section.
3. Creates an annotated tag `v<version>` (e.g. `v0.1.2-alpha.4`) on that commit.
4. Creates a GitHub Release for the tag, with the extracted section as the body, flagged pre-release when
   the version is a SemVer pre-release (`0.1.2-alpha.4` is, `0.1.3` is not).

Both steps are idempotent: an existing tag or Release is left alone, so re-running is safe. The tag and
Release carry no attached build output — the Docker image built from the same merge commit is published
separately by the `publish-docker-image` job; see [Publishing the Docker image](#publishing-the-docker-image).

The tag record starts at the first release after this workflow landed. `0.1.2-alpha.2`, `.3` and `.4`
shipped before it existed and are deliberately not backfilled, so they have no tag and no Release page;
`CHANGELOG.md` and the merge commits it links remain the record for those three.

The parsing lives in a script rather than in the workflow so that CI and a local dry run before merging a
release PR run the same code. `ReleaseNotes.fsx` resolves the
version through `scripts/Versioning.fsx`, which is also what `dotnet run CheckVersions` uses, so
`Directory.Build.props` has exactly one parser (the lesson of [#447](https://github.com/informedica/GenPRES/issues/447)).
The changelog grammar it relies on — which headings delimit a section, and that a missing, empty or
duplicated section is an error rather than a silently odd Release — is pinned by
`scripts/ChangelogTests.fsx` (`dotnet fsi scripts/ChangelogTests.fsx`).

Note that the pre-release flag comes from the version itself, not from `CHANGELOG.md`'s `pre_release:`
front matter. The front matter says what ShipIt generates *next*, so reading it would give the same shipped
version a different answer depending on when the question was asked — a dry run against an older version
after the key is dropped would report it as stable.

To preview what a release will publish before merging the release PR:

```bash
# current version's Release body, to stdout; version/tag/pre-release facts to stderr
dotnet fsi scripts/ReleaseNotes.fsx

# any shipped version, written to a file
dotnet fsi scripts/ReleaseNotes.fsx 0.1.2-alpha.2 --out notes.md
```

**Trigger, and why it is not ShipIt's documented one.** The workflow fires on `pull_request: types: [closed]`
against `master`, gated on `merged == true && head.ref == 'release/master'`. ShipIt's README instead suggests
gating a downstream job on the push event:

```yaml
if: startsWith(github.event.head_commit.message, 'chore: release ')
```

That condition would never have fired here. All three merge methods stay enabled (ADR-0005, design choice 2),
and every release PR so far (#455, #458, #464) merged as a true merge commit, so the push event's
`head_commit.message` was `Merge pull request #NNN from informedica/release/master`, never
`chore: release ...` — 0 for 3. The head ref is merge-method independent, so the trigger keeps working if a
release PR is ever squash- or rebase-merged. ShipIt's `easybuild-release:pending` label is the equivalent
fallback signal.

**Consequence for downstream workflows.** The tag and Release are created with the workflow's own
`GITHUB_TOKEN`, and events generated by that token do not start further workflow runs. This was confirmed on
this repo rather than taken from the documentation: none of the three ShipIt release PRs, all opened by
`github-actions[bot]`, ran its checks automatically. #455 and #458 had runs created but parked at
`action_required` until a maintainer re-ran them; #464 got no `pull_request` runs at all until it was closed
and reopened by hand. A workflow keyed on `on: release` or `on: push: tags:` therefore will not fire. The
options for anything downstream are a job inside `tag-release.yml`, a `workflow_dispatch` /
`repository_dispatch` call (the two events explicitly exempt from the rule), or a PAT / GitHub App token.

#### Publishing the Docker image

A `publish-docker-image` job in `tag-release.yml`, gated on `needs: tag-and-release`, closes
[#234](https://github.com/informedica/GenPRES/issues/234) item 3
([#459](https://github.com/informedica/GenPRES/issues/459)) — see
[ADR-0005's Docker image publishing amendment](docs/adr/0005-build-system-versioning-and-release.md)
for the full design rationale. It only runs once tagging and the Release have both succeeded, and reuses
that job's `version`/`tag`/`prerelease` outputs. For a given release it:

1. Checks out the same merge commit `tag-and-release` tagged.
2. Builds the `Dockerfile` with `--build-arg APP_VERSION=<version>` (same as the local `DockerBuild` FAKE
   target), `linux/amd64` only, tagging every tag the release needs in one `docker build -t ... -t ...` call.
3. Starts the built image with the public demo `GENPRES_URL_ID` (from `.env.example`) and a random
   per-run `GENPRES_PASSWORD`, and requires `/` to return 200 within 60 seconds before treating the image as good.
4. Pushes `docker.io/informedica/genpres:<version>`, and also `:latest` when the version is a stable release
   (currently we only ship alphas, so `:latest` stays unpublished). Any `+` in `<version>` is folded to `-`
   first: `Versioning.fsx` allows SemVer build metadata in `<Version>`, but a raw `+` isn't a legal Docker
   tag character.

Registry is Docker Hub (`docker.io/informedica/genpres`); the `informedica` org is on the Docker Team plan.
This started on GHCR as an interim step and moved once the org existed. The registry/namespace is a single
`IMAGE_NAME` job-level env var in `tag-release.yml`.

**Authentication is Docker Hub OIDC — there is no stored registry credential.** `docker/login-action`
exchanges the job's GitHub OIDC token for a short-lived Docker Hub token, so nothing to rotate and nothing
to leak. `docker build` and `docker push` still run as plain CLI (matching `Build.fs`); `docker/login-action`
is the one marketplace action, because the OIDC token exchange cannot be done with `docker login` alone.

One-time setup (Docker Team org admin + repo admin):

1. **Docker Home → `informedica` → OIDC connections → Create OIDC connection.** Add a ruleset with subject
   `repo:informedica/GenPRES:environment:docker-publish` (scoped to the GitHub environment, not a bare
   `pull_request` subject). Copy the connection ID.
2. **GitHub repo → Settings → Environments → New environment** named `docker-publish`. Optionally add
   required reviewers here for a manual gate before every Docker Hub push.
3. **GitHub repo → Settings → Secrets and variables → Actions → Variables → New repository variable**
   `DOCKERHUB_OIDC_CONNECTIONID` = the connection ID from step 1. It is a variable, not a secret: an
   identifier, useless without the matching ruleset.
4. If the connection's **Failures** tab in Docker Home shows a rejected claim on the first run, copy the
   exact `sub` it logged into the ruleset — repos created after 2026-07-15 use immutable identifiers
   (`repo:informedica@<id>/GenPRES@<id>:...`); GenPRES predates that and uses the plain form.

**Repository visibility is a manual step.** The first push creates `informedica/genpres` as a **private**
Docker Hub repository. Since GenPRES is public and the image must be pullable without credentials, a Docker
Hub org admin needs to set the repository to public in its settings after the first successful push — the
OIDC token cannot change repository visibility itself.

To build and smoke test the same image locally before relying on the workflow, use the existing
`DockerBuild`/`DockerRun` FAKE targets (see [Docker wrappers](#docker-wrappers) above); they build
`informedica/genpres` by default (override with `DOCKER_IMAGE`), the same name the workflow publishes,
though the local build is never pushed.

### IDE Integration

#### Visual Studio Code

The repository ships a `.vscode/settings.json` with Ionide (F# language support) settings. To work effectively:

1. Install the **Ionide for F#** extension (`ionide.ionide-fsharp`)
2. Open the repository root folder in VS Code
3. Ionide will use `GenPRES.sln` to discover projects and provide IntelliSense

**Running from VS Code terminal:**

```bash
# Start full application (server + client)
dotnet run

# Run tests
dotnet run ServerTests

# Build only
dotnet run Build
```

You can also add custom VS Code tasks in `.vscode/tasks.json` if you want keyboard-shortcut access to build targets.

#### JetBrains Rider

1. Open `GenPRES.sln` in Rider (not the folder — open the `.sln` file)
2. Rider will restore packages and index the solution automatically

**Running the application from Rider:**

The most reliable approach in Rider is to use the integrated terminal:

```bash
dotnet run
```

Alternatively, you can create a **Run Configuration** manually:

- **Type**: .NET Project
- **Project**: `Build` (the root `Build.fsproj`)
- **Program arguments**: *(leave empty to start with the default `Run` target)*

**Running individual targets:**

Add the target name as a program argument, for example `ServerTests` to run the tests.

#### Debug Mode in Rider

Because the application starts the server process indirectly through FAKE, attaching the Rider debugger requires a two-step approach:

**Option 1 – Attach to running process (recommended):**

1. Start the server normally: `dotnet run` in the terminal
2. In Rider: **Run → Attach to Process** and select the `Informedica.GenPRES.Server` process
3. Set breakpoints in the server source files; Rider will break when they are hit

**Option 2 – Run server directly:**

1. In Rider, create a **Run/Debug Configuration** of type **.NET Project**:
   - **Project**: `Informedica.GenPRES.Server`
   - **Working directory**: `src/Informedica.GenPRES.Server`
2. Start the client separately in a terminal: `dotnet fable watch -o output -s -e .jsx --run npx vite` from `src/Informedica.GenPRES.Client/`
3. Use Rider's **Debug** button to launch the server with the full debugger attached

> **Note**: When running the server directly (Option 2), environment variables from `.env` are loaded automatically by `Env.loadDotEnv()` in the server startup code, so no additional IDE configuration is needed for environment variables.

#### Debug Mode in VS Code

1. Create a `.vscode/launch.json` file (if it does not exist):

   ```json
   {
     "version": "0.2.0",
     "configurations": [
       {
         "name": "Launch GenPRES Server",
         "type": "coreclr",
         "request": "launch",
         "preLaunchTask": "dotnet: build",
         "program": "${workspaceFolder}/src/Informedica.GenPRES.Server/bin/Debug/net10.0/Informedica.GenPRES.Server.dll",
         "args": [],
         "cwd": "${workspaceFolder}/src/Informedica.GenPRES.Server",
         "stopAtEntry": false,
         "serverReadyAction": {
           "action": "openExternally",
           "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
         }
       }
     ]
   }
   ```

2. Press **F5** to start the server with the debugger attached
3. Start the client in a separate terminal: `dotnet fable watch -o output -s -e .jsx --run npx vite` from `src/Informedica.GenPRES.Client/`

> **Tip**: The C# Dev Kit or the **.NET Install Tool** extension may be required depending on your VS Code setup.

## Project Folder Structure

### Root Level

```text
GenPRES/
├── .github/                   # GitHub configuration and workflows
│   ├── ISSUE_TEMPLATE/        # Issue templates
│   ├── PULL_REQUEST_TEMPLATE/ # PR templates
│   ├── instructions/          # Development instructions
│   └── workflows/             # CI/CD workflows
├── .husky/                    # Git hooks
├── .idea/                     # JetBrains IDE configuration
├── .vscode/                   # VS Code configuration
├── benchmark/                 # Performance benchmarks
├── data/                      # Application data
│   ├── cache/                 # Cached data files
│   ├── config/                # Configuration files
│   ├── data/                  # JSON data files
│   └── zindex/                # Z-Index drug database files
├── deploy/                    # Deployment scripts and configurations
├── docs/                      # Documentation (see docs/README.md)
│   ├── adr/                   # Architecture Decision Records
│   ├── code-reviews/          # Conformance analyses against external standards
│   ├── data-extraction/       # Dose-rule extraction pipeline documentation
│   ├── domain/                # Domain model specifications and explainers
│   ├── implementation-plans/  # Per-issue implementation plans
│   ├── literature/            # Research literature
│   ├── roadmap/               # Backlog, feature requests, fit-gap analyses
│   ├── scenarios/             # Use cases (executable integration model)
│   ├── security/              # Security reviews and baseline
│   └── user-guide/            # End-user guide (en/nl) and manual test workflows
├── scripts/                   # Utility scripts
└── src/                       # Source code
    ├── Informedica.Agents.Lib/           # Agent-based concurrency library
    ├── Informedica.FTK.Lib/              # Adult formulary parsing library
    ├── Informedica.GenCORE.Lib/          # Core domain library
    ├── Informedica.GenFORM.Lib/          # Formulary management library
    ├── Informedica.GenINTERACT.Lib/      # Drug interaction rules
    ├── Informedica.GenORDER.Lib/         # Order processing library
    ├── Informedica.GenPRES.Client/       # Frontend application
    │   ├── Components/        # UI components
    │   ├── Pages/             # Page components
    │   ├── Views/             # View components
    │   ├── output/            # Compiled JavaScript output
    │   └── public/            # Static assets
    ├── Informedica.GenPRES.Server/       # Backend application
    │   ├── Properties/        # Server properties
    │   ├── Scripts/           # Server scripts
    │   └── data/              # Server data directory
    ├── Informedica.GenPRES.Shared/       # Shared types and API protocol
    ├── Informedica.GenSOLVER.Lib/        # Constraint solver library
    ├── Informedica.GenUNITS.Lib/         # Units of measurement library
    ├── Informedica.Logging.Lib/          # Logging utilities
    ├── Informedica.MCP.Lib/              # Model Context Protocol for LLM integration
    ├── Informedica.MCP.Server/           # Standalone stdio MCP host
    ├── Informedica.NKF.Lib/              # Pediatric formulary parsing library
    ├── Informedica.NLP.Lib/              # Natural Language Processing for rule extraction
    ├── Informedica.Utils.Lib/            # Utility functions
    ├── Informedica.ZForm.Lib/            # Z-Index form library
    └── Informedica.ZIndex.Lib/           # Z-Index database library
```

### Key Configuration Files

- `Build.fs` / `Build.fsproj` - Build automation
- `GenPRES.sln` - Solution file
- `Dockerfile` - Docker containerization
- `paket.dependencies` - Package management
- `global.json` - .NET SDK version

### Documentation Files

- `README.md` - Project overview
- `CHANGELOG.md` - Version history
- `CONTRIBUTING.md` - Contribution guidelines
- `CODE_OF_CONDUCT.md` - Code of conduct
- `DEVELOPMENT.md` - Development guide (this file)
- `GOVERNANCE.md` - Project governance
- `MAINTAINERS.md` - Maintainer information
- `ROADMAP.md` - Project roadmap
- `SECURITY.md` - Security policy
- `SUPPORT.md` - Support information
- `WARP.md` - Warp AI agent documentation
- `docs/adr/0001-system-architecture.md` - Technical architecture
- `docs/domain/` - Domain model specifications
- `docs/user-guide/` - Multilingual user guide ([English](docs/user-guide/en/user-guide.md), [Nederlands](docs/user-guide/nl/gebruikershandleiding.md))

## Directory Descriptions

### Core Directories

- **`.github/`** - GitHub configurations (issue/PR templates, workflows, development instructions)
- **`benchmark/`** - Performance benchmarking suite
- **`data/`** - Application data (drug cache, configuration, clinical data, Z-Index database)
- **`docs/`** - Comprehensive documentation:
  - `docs/domain/` - Domain model specifications (Core Domain, GenFORM, GenORDER, GenSOLVER)
  - `docs/adr/` - Architecture Decision Records
  - `docs/scenarios/` - Clinical scenarios
- **`src/`** - Source code (client, server, and F# libraries)

### Library Modules

Each `Informedica.*.Lib` directory contains:

- Core F# source files
- `Scripts/` - Interactive F# scripts for testing
- `Notebooks/` - Jupyter/Polyglot notebooks (where applicable)
- `paket.references` - Package dependencies
- `*.fsproj` - F# project file

## Project Architecture

For complete architectural documentation, see:

- **[Architecture Overview](docs/adr/0001-system-architecture.md)**: Technical stack, server/client structure, Docker hosting, and build configuration
- **[Core Domain Model](docs/domain/core-domain.md)**: Transformation pipeline, constraint-based architecture, and domain concepts
- **[GenFORM](docs/domain/genform-free-text-to-operational-rules.md)**: Free text to Operational Knowledge Rules (OKRs)
- **[GenORDER](docs/domain/genorder-operational-rules-to-orders.md)**: OKRs to Order Scenarios
- **[GenSOLVER](docs/domain/gensolver-from-orders-to-quantitative-solutions.md)**: Constraint solving engine

### Technology Stack

This project is built on the [SAFE Stack](https://safe-stack.github.io/):

- **Informedica.GenPRES.Server**: F# with [Saturn](https://saturnframework.org/)
- **Informedica.GenPRES.Client**: F# with [Fable](https://fable.io/docs/) and [Elmish](https://elmish.github.io/elmish/)
- **Testing**: Expecto with FsCheck for property-based testing
- **Build**: .NET 10.0

### Core Libraries

For complete library specifications including capabilities and dependencies, see [GenFORM Appendix B.3](docs/domain/genform-free-text-to-operational-rules.md#appendix-b3-genform-libraries).

Key libraries in dependency order:

- **Informedica.Utils.Lib**: Shared utilities, common functions  
- **Informedica.Agents.Lib**: Agent-based execution (MailboxProcessor)  
- **Informedica.Logging.Lib**: Concurrent logging  
- **Informedica.NLP.Lib**: Natural Language Processing for structured rule extraction
- **Informedica.GenUNITS.Lib**: Unit-safe calculations  
- **Informedica.GenSOLVER.Lib**: Quantitative constraint solving  
- **Informedica.GenCORE.Lib**: Core domain model  
- **Informedica.ZIndex.Lib**: Medication and product database  
- **Informedica.ZForm.Lib**: Z-Index dosing reference data  
- **Informedica.NKF.Lib**: Kinderformularium dose rule extraction
- **Informedica.FTK.Lib**: Farmacotherapeutisch Kompas dose rule extraction
- **Informedica.GenFORM.Lib**: Operational Knowledge Rules (OKRs)  
- **Informedica.GenORDER.Lib**: Clinical order scenarios and execution  
- **Informedica.GenINTERACT.Lib**: Drug interaction rules
- **Informedica.MCP.Lib**: Model Context Protocol for LLM integration
- **Informedica.MCP.Server**: Standalone stdio MCP host
- **Informedica.GenPRES.Shared**: Shared types and API protocol
- **Informedica.GenPRES.Server**: Server API and orchestration
- **Informedica.GenPRES.Client**: Web-based clinical UI

## Code Contribution Guidelines

### Repository Structure

**Important: an opt-in strategy is used** in the `.gitignore` file, i.e. you have to specifically define what should be included instead of the other way around!!

This project follows specific organizational patterns:

- **Library Structure**: Use the `Informedica.{Domain}.{Lib/Server/Client}` naming convention
- **Domain Libraries**: GenSOLVER, GenORDER, GenUNITS, GenCORE
- **Separate Test Projects**: Each library has its own test project
- **Opt-in .gitignore**: *You must explicitly define what should be included!!*

### Coding Standards

Follow the [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md) for code style, formatting, type design, error handling, testing, and documentation guidelines.

Follow the [Commit Message Instructions](.github/instructions/commit-message.instructions.md) for conventional commit format, types, scopes, and examples.

## Domain-Specific Guidelines

### Medical Safety Considerations

When contributing to medical functionality:

- **Patient Safety First**: All changes affecting dosage calculations, medication lookup, or clinical decision support must be thoroughly tested
- **Precision Matters**: Use appropriate units of measure and maintain calculation accuracy
- **Validation Required**: Implement comprehensive input validation for medical data
- **Error Handling**: Provide clear, actionable error messages for medical professionals
- **MDR Compliance**: Ensure all medical-related changes align with Medical Device Regulation requirements

For mathematical operations, units of measure, performance, and testing guidelines, see [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md).

## Development Workflow

### Git Workflow

1. **Fork** the repository
2. **Clone** your fork locally: `git clone https://github.com/your-username/GenPRES.git`
3. **Set up upstream remote**: `git remote add upstream https://github.com/informedica/GenPRES.git`
4. **Before starting work**, sync your fork:

   ```bash
   git checkout master
   git fetch upstream
   git merge upstream/master
   git push origin master
   ```

5. **Create a feature branch**: `git checkout -b feat/your-feature-name`
6. **Make changes** following our coding guidelines
7. **Commit** using conventional commit messages `git commit -m "feat(scope): description"`
8. **Check** that you are still in sync with upstream:

   ```bash
   git fetch upstream
   git merge upstream/master
   ```

9. **Push** to your fork `git push origin feat/your-feature-name`
10. **Create a pull request** to the main repository
11. **After PR is merged**, delete your feature branch locally and remotely:

    ```bash
    git checkout master
    git pull upstream master
    git push origin --delete feat/your-feature-name
    git branch -d feat/your-feature-name
    ```

12. **Repeat** for new features or fixes

### Opt-in .gitignore Strategy

This project uses an opt-in strategy for `.gitignore`:

- You must explicitly define what should be included
- When adding new files, ensure they're properly included in Git
- Proprietary medication cache files are excluded for licensing reasons

### Environment Configuration

This project uses a `.env` file at the project root as the single source of truth for environment variables. The `.env` file is excluded from git by the opt-in `.gitignore` strategy, so secrets are never committed.

#### Quick Setup

1. Nothing, for the demo: the first `dotnet run` copies `.env.example` to `.env` when no `.env` exists (see [What Happens During `dotnet run`](#what-happens-during-dotnet-run-the-run-target)). To create it by hand instead: `cp .env.example .env`
2. `.env.example` ships with the public demo sheet ID and an empty `GENPRES_PASSWORD`, so the copy works as-is in demo mode with admin operations disabled. Set `GENPRES_PASSWORD` to use the admin pages locally. For production data, edit `.env` and replace `GENPRES_URL_ID` (ask a team member for the production URL ID)

Put a `git worktree` **next to** the main checkout, not inside it: the root resolver (`AppPath`) and `Env.loadDotEnv` search upward for `.env`, so a worktree nested under the repo would pick up the main checkout's `.env` and `data/` instead of its own.

The `.env` file uses standard `KEY=VALUE` format:

```bash
GENPRES_URL_ID=<your-url-id>   # Google Sheets data URL ID (required; .env.example ships the public demo ID)
GENPRES_LOG=i                  # Logging level: 0=off, d=debug, i=info, w=warning, e=error
GENPRES_PROD=0                 # Production mode: 0=demo (safe default), 1=production data
GENPRES_DEBUG=1                # Debug mode: 0=off, 1=on
GENPRES_LANG=nl                # Default UI language: en, nl, fr, de, es, it — see below
GENPRES_PASSWORD=<password>    # Admin password — see policy below
```

#### Default language

`GENPRES_LANG` is the UI language a browser starts in. The client asks the server for it at
start-up (`getSettings`); until then, and when the server cannot be reached, the client falls
back to Dutch, as it always did. Accepted values are the ISO 639-1 codes `en`, `nl`, `fr`, `de`,
`es`, `it` in any case (the display names such as `Nederlands` work too). Unset or blank means
`nl`; any other value makes the server refuse to start with a message naming the setting.

The server default is the lowest rung: an `la=` parameter in the url wins over it, and so does a
language the user picks in the title bar or the disclaimer. Navigating between pages keeps the
current language; only a new `la=` changes it.

#### Password policy

`GENPRES_PASSWORD` gates all admin operations (settings page, log analysis,
resource reload). The server enforces a length policy at startup:

- **Development (`GENPRES_PROD=0`)**: any value is accepted, including the
  trivial `genpres` used by some local setups. Convenient for development;
  unsafe anywhere else.
- **Production (`GENPRES_PROD=1`)**: when `GENPRES_PASSWORD` is missing or
  blank the server **starts with admin operations disabled** and prints a
  warning saying so (issue #590); the data set is still the production one.
  When the password is set but shorter than 16 characters the server
  **refuses to start**: a weak secret would stay live. Generate a strong
  value with a CSPRNG, e.g. `openssl rand -base64 32`, and inject it via a
  secret store (Docker secret, Kubernetes secret, vault, ...).

Never reuse a development password in production. Never commit a real
password to the repository — `.env` is gitignored.

#### How It Works

Environment variables are resolved in this priority order (highest first):

1. **Already-set environment variable** (from shell, CI, Docker) — takes precedence
2. **`.env` file** — loaded by shell scripts or `Env.loadDotEnv()` in F#
3. **Hardcoded default in source code** — safe fallback (demo data)

This means you can always override `.env` values by setting an environment variable directly.

#### Loading in Different Contexts

- **Shell**: Source `.env` manually with `set -a; source .env; set +a` before running commands.
- **F# scripts (FSI)**: Scripts call `Informedica.Utils.Lib.Env.loadDotEnv()` which searches upward for `.env` from the current directory.
- **IDEs (Rider, VS Code)**: The `Env.loadDotEnv()` call in scripts ensures variables are available even when the IDE doesn't inherit shell environment.
- **Docker**: The image defaults to demo mode with the public demo sheet ID, so no variables are needed for a demo run. For production, inject `GENPRES_PROD=1`, `GENPRES_URL_ID` and `GENPRES_PASSWORD` at *container runtime*, not at build time, and mount `data/cache` — the repo-root `compose.yaml` does all of this from `.env` (`docker compose up -d`; see [Docker wrappers](#docker-wrappers)). Manual equivalent: `docker run -e GENPRES_PROD=1 -e GENPRES_URL_ID="$GENPRES_URL_ID" -e GENPRES_PASSWORD="$GENPRES_PASSWORD" -v "$PWD/data/cache:/app/data/cache" -p 8080:8085 informedica/genpres:<tag>`. In real production, use a Docker or Kubernetes secret. **Do not** use `--build-arg`: the value would be persisted as image metadata and visible to anyone who can pull the image.

#### Common Environment Variable Issues

**Missing GENPRES_URL_ID**: Will cause "cannot find column" errors when the application tries to load resources from Google Sheets. Make sure your `.env` file exists and contains a valid `GENPRES_URL_ID`.

**Incorrect GENPRES_PROD value**: Setting this to anything other than `0` in development may cause authentication or data access issues.

For background on this approach, see [Issue #44](https://github.com/informedica/GenPRES/issues/44).
