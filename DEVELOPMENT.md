# Development on GenPRES

## Getting Started

### Toolchain Requirements

This section is the canonical source for toolchain versions.

- **.NET SDK**: pinned in [`global.json`](global.json) (currently `10.0.302`, `rollForward: latestPatch`)
- **Node.js**: 18.x, 22.x, or 23.x (LTS versions recommended)
- **npm**: 10.x or later

The SDK pin is exact on purpose: a newer feature band can change F# code generation. `global.json`
is the single source of truth. CI passes `global-json-file: global.json` to `actions/setup-dotnet`,
and the `Dockerfile` build stage uses the matching exact SDK image tag. Bumping the feature band is
a deliberate, reviewed edit to `global.json`.

### Setting Up the Development Environment

1. Fork this repository
2. Clone your fork locally
3. Configure the environment as described in [Environment Configuration](#environment-configuration)

### Start the application

```bash
dotnet run
```

Open your browser at `http://localhost:5173`.

### The demo launch sequence

In demo mode (`GENPRES_PROD=0`) the server hosts stand-ins for the hospital EHR, the identity
provider, the user registry and the mail service, so the launch, enrolment and signing sequences
run on one machine. The walkthroughs are in
[Testing Workflows](docs/user-guide/testing-workflows.md#workflow-8--launch-sequence), workflows 8 to 11.

#### Cookies and the development proxy

| Cookie | Set by | Attributes | Purpose |
|---|---|---|---|
| `genpres_session` | the callback, on an open | HttpOnly, Strict, `Path=/`, Secure over HTTPS | names the session |
| `genpres_launch_state.<state>` | the answer to `PresentLaunch` | HttpOnly, Lax, `Path=/callback`, `Max-Age` 2 min | proves the callback comes from the browser that started the hop |
| `genpres_stub_identity` | the stub launch page | HttpOnly, Lax, `Path=/`, `Max-Age` 2 min | carries the identity choice and PatientId to the stub identity provider; demo only |
| `genpres_enrolment` | the callback, when the launch suspends into enrolment | HttpOnly, Strict, `Path=/`, `Max-Age` what remains of the code's fifteen minutes | names the enrolment attempt this browser made |

`vite.config.js` proxies `/api`, `/stub`, `/authorize` and `/callback` to the server on port 8085,
so in development the browser talks to one origin and the cookies reach both. In production the
server serves the client itself.

## Build System Architecture

### How `dotnet run` Interacts with FAKE

GenPRES uses [FAKE](https://fake.build/) for build automation. The configuration lives under `build/`:

- **`build/Build.fs`** defines the targets and their dependencies
- **`build/Helpers.fs`** wraps process calls (dotnet, npm, docker)

`dotnet run` from the repository root executes `Build.fsproj`, an F# console application that starts
FAKE. FAKE reads the target name from the arguments (default `Run`) and executes it with its
dependencies.

`Build.fsproj` must stay in the repository root: bare `dotnet run` only finds a project in the
current directory. This is also why plain `dotnet build` and `dotnet test` fail with MSB1011 (two
candidates) and must be given `GenPRES.sln`. The project is listed in `GenPRES.sln` for editor
support but excluded from the solution build, so `dotnet build GenPRES.sln` never overwrites
`Build.dll` while it is running.

### FAKE Build Targets Reference

| Command | Description |
|---|---|
| `dotnet run` | Start server and Fable/Vite dev server with hot reload (default). Creates `.env` from `.env.example` when missing |
| `dotnet run list` | List all targets |
| `dotnet run Build` | Compile the whole solution: libraries, server, tests and the client `.fsproj`. No npm |
| `dotnet run ServerBuild` | Compile only the server and its libraries |
| `dotnet run BenchmarkBuild` | Compile the benchmark projects under `benchmark/` in Release. They are outside `GenPRES.sln`; CI runs this in a separate job |
| `dotnet run ClientBuild` | Compile the client: Fable, then a production Vite bundle. Runs `npm ci` first |
| `dotnet run Clean` | Remove `deploy/` and `dist/`, delete Fable-generated `.jsx` files |
| `dotnet run Bundle` | Production build: publish server, compile client, copy data |
| `dotnet run ServerTests` | Run all Expecto tests with quiet logging |
| `dotnet run DebugTests` | Run every test project one at a time with per-test output and no parallelism; stops at the first failing assembly. Set `CI=true` without the G-Standaard files under `data/zindex` |
| `dotnet run TestHeadless` | Run the suite through plain `dotnet test` |
| `dotnet run CheckVersions` | Verify every built DLL's version matches the root `Directory.Build.props` |
| `dotnet run Format` | Format all F# source with Fantomas |
| `dotnet run MarkdownLint` | Lint the Markdown files |
| `dotnet run ApiDocs` | Build the fsdocs API reference into `./output/`. Set `FSDOCS_ROOT` to the site base URL |
| `dotnet run ApiDocsWatch` | Live preview of the API reference |
| `dotnet run DockerBuild` | Build the production image, labeled with the version from `Directory.Build.props` |
| `dotnet run DockerRun` | Run the built image with `GENPRES_URL_ID`/`GENPRES_PASSWORD` from the environment |

#### Target Dependency Chains

```text
Clean ──► RestoreClient ──► Bundle
Clean ──► RestoreClient ──► ClientBuild

ServerBuild, BenchmarkBuild, ApiDocs, ApiDocsWatch   (no prerequisites)

Build ──► Run
RestoreClient ──► Run

Build ──► TestHeadless
Build ──► ServerTests
Build ──► CheckVersions
```

`ServerBuild` and `ClientBuild` are additive: nothing depends on them. `Build` stays the full,
npm-free solution build so CI does not run `npm ci` and a Fable compile for every test run.

### What Happens During `dotnet run` (the `Run` target)

`Run` first creates `.env` from `.env.example` when there is none (demo sheet ID, `GENPRES_PROD=0`,
empty password). An existing `.env` is never touched. Only `Run` does this; `Build`, `ServerTests`
and `Bundle` work without a `.env`.

It then starts two processes in parallel:

1. **Server**: `dotnet run --no-restore` in `src/Informedica.GenPRES.Server/`, listening on port `8085`
2. **Client**: `dotnet fable watch … --run npx vite` in `src/Informedica.GenPRES.Client/`, served on
   `http://localhost:5173` with hot module replacement

Output of both is printed with `server:` and `client:` prefixes.

### Paket groups

Packages are managed by [Paket](https://fsprojects.github.io/Paket/) from the root
`paket.dependencies` / `paket.lock`; each project lists what it uses in its own `paket.references`.
The dependencies file has five groups, each resolved independently:

| Group | Used by | Contents |
|---|---|---|
| `Main` (unnamed first section) | `src/` libraries, server, shared contract | everything shipped |
| `Client` | `src/Informedica.GenPRES.Client` | Fable, Elmish, Feliz |
| `Test` | `tests/*` | Expecto, FsCheck, the test SDK and adapter |
| `Build` | the root `Build.fsproj` | FAKE |
| `Benchmark` | `benchmark/*` | BenchmarkDotNet |

Each project references exactly one group (the `benchmark/` projects list `Main` and `Benchmark`).
Paket emits one `PackageReference` per group and package without de-duplicating, so a project on two
groups would get `FSharp.Core` twice and NuGet would warn on every restore. Test projects therefore
get `Unquote`, `MathNet.Numerics.FSharp` and `IcedTasks` transitively from the library under test.
`FSharp.Core` is pinned to the same version in every group; bump all four together.

To add a package: put the `nuget` line in the group of its consumer, add it to the consuming
project's `paket.references` under that group, run `dotnet paket install`, and commit
`paket.dependencies`, `paket.lock` and the touched `paket.references` files.

### Changelog & Release Automation (EasyBuild.ShipIt)

[EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt) derives the next version and
changelog section from conventional-commit history ([ADR-0005](docs/adr/0005-build-system-versioning-and-release.md)).
It is a local dotnet tool, configured in the YAML front matter of `CHANGELOG.md`, and runs in CI on
every push to `master` (see [Release Automation](#release-automation-github-actions)).

ShipIt owns the version number. Its updaters write it to `<Version>` in `Directory.Build.props`, to
the default image tag in `compose.yaml`, and to the commented `GENPRES_IMAGE_TAG` example in
`.env.example`. Do not hand-edit any of the three. Never put a `+` in `<Version>`: the Docker tag
step folds it to `-`, and the updaters do not.

Preview locally:

```bash
dotnet tool restore
dotnet shipit --dry-run --allow-branch master --skip-merge-commit --skip-invalid-commit
```

All three flags are required: the default branch is `master`, merge commits appear in history and
ShipIt throws on them, and a commit that does not follow Conventional Commits (a GitHub-UI "commit
suggestion" bypasses the local hook) would otherwise fail the run. `--dry-run` changes nothing.

#### What reaches the changelog

- Only types like `feat` and `fix` render. `docs`, `build` and `chore` commits never do, so a change
  that must appear in the release notes needs a rendering type.
- Commits that change no files are ignored.
- A `=== changelog ===` block in the **commit message body** (not the PR body) adds detail under the
  entry. It needs an opening and a closing marker; an unterminated block is dropped silently.

  ```text
  fix(server): correct the infusion rate rounding

  === changelog ===
  Rates were rounded to whole mL/h, truncating paediatric doses below 1 mL/h.
  === changelog ===
  ```

### Helper Shell Scripts

Two scripts are tracked; the rest are optional recipes for your own working copy. The opt-in
`.gitignore` keeps local scripts untracked. Every script starts with `#!/usr/bin/env bash`, runs from
the repository root (except `benchmark/run.sh`), and needs `chmod +x` once.

#### Tracked scripts (in the repo)

- **`benchmark/run.sh`**: runs `sudo dotnet run -c Release "$@"` from the `benchmark/` directory.
  `sudo` is needed by some BenchmarkDotNet diagnostics.
- **`.husky/scripts/format-staged.sh`**: called by the pre-commit hook. Runs Fantomas on the staged
  F# files and re-stages the output. See [CONTRIBUTING.md](CONTRIBUTING.md#code-formatting-pre-commit-hook).

#### Optional local scripts (not in the repo)

Run-mode wrappers source `.env` and then export overrides, which win over `.env`:

```bash
#!/usr/bin/env bash
set -a; source .env; set +a

export GENPRES_LOG=i
export GENPRES_PROD=0
export GENPRES_DEBUG=1

dotnet run
```

Common variants:

| File | `GENPRES_LOG` | `GENPRES_PROD` | `GENPRES_DEBUG` | Purpose |
|---|---|---|---|---|
| `debug.sh` | `i` | `0` | `1` | Local development against the demo data |
| `debugprod.sh` | `d` | `1` | `1` | Production data, debug logging; clear `data/logs` first |
| `infoprod.sh` | `i` | `1` | `1` | Production data, info logging; clear `data/logs` first |
| `logprod.sh` | `i` | `1` | `0` | Production data, info logging, debug off |
| `prod.sh` | `0` | `1` | `0` | Mirrors a real production launch |

Production modes need a real `GENPRES_URL_ID` in `.env`. To clear the logs first:

```bash
mkdir -p ./data/logs && rm -rf ./data/logs/*
```

If a local script should become standard, add a `!` allow-line for it to `.gitignore` in the same PR.

##### Docker wrappers

The `DockerBuild` and `DockerRun` targets work from any shell.

**Build**: `dotnet run DockerBuild` reads the version from `Directory.Build.props` and passes it as
`APP_VERSION`, so the image label matches what was built. `DOCKER_PLATFORM` cross-builds
(`DOCKER_PLATFORM=linux/amd64 dotnet run DockerBuild`), `DOCKER_IMAGE` overrides the default
`informedica/genpres` name.

**Run**: `dotnet run DockerRun` reads `GENPRES_URL_ID` and `GENPRES_PASSWORD` from the environment
and fails fast if either is missing. It mounts the host's `data/cache` onto `/app/data/cache` and
forwards `GENPRES_PROD`. Source `.env` first:

```bash
set -a; source .env; set +a
dotnet run DockerRun
```

**Run a published image**: the tracked `compose.yaml` runs the image from Docker Hub with port,
secrets and mode read from `.env`. The image tag defaults to the current release; ShipIt bumps it on
every release PR. Set `GENPRES_IMAGE_TAG` in `.env` only to pin a different version.

```bash
cp .env.example .env            # once; for production, set the secrets
git pull && docker compose pull # after each release
docker compose up -d            # http://localhost:8080
docker compose logs -f genpres
```

The image is published a few minutes after the release PR merges; a `docker compose pull` in that
window fails with "manifest unknown". The image itself defaults to demo mode, so a bare
`docker run -p 8080:8085 informedica/genpres:<tag>` works with no flags. `GENPRES_PROD=1` needs the
proprietary `GENPRES_URL_ID` and the `data/cache` bind mount: production reads `*.cache`, the image
ships only `*.demo`. `compose.yaml` forwards only the `GENPRES_*` keys. For a demo that keeps its
signed order plans across a recreated container, set `GENPRES_DB_CONNECTION=Data Source=data/db/genpres.db`
in `.env`; `compose.yaml` mounts `./data/db` for it.

**Exit codes**: the image runs `tini` as PID 1. A refused start-up (short production password,
unknown `GENPRES_LANG`, no `GENPRES_URL_ID`) prints one message and exits `1`, a crash exits `134`,
and `docker stop` reaches Kestrel for a graceful shutdown. Read them with `docker ps -a`.

**Browser caching after an update**: the server sends `Cache-Control: no-cache` on `index.html` and
`immutable` on the content-hashed bundles under `/assets/`, so a browser picks up a new container
without a hard refresh. Behind nginx that serves static `html` files directly (Plesk), the header never reaches the
browser: drop `html` from that list or add `add_header Cache-Control "no-cache"` for `/` and
`/index.html`. Check with `curl -sI https://<host>/ | grep -i cache-control`.

### CI/CD Pipeline (GitHub Actions)

`.github/workflows/build.yml` runs on every push or pull request to `master` on Ubuntu, Windows and
macOS:

1. Checkout
2. Install the .NET SDK from `global.json`
3. `dotnet tool restore`
4. `dotnet fantomas --check .` (fails on unformatted code)
5. `dotnet run ServerTests`

CI sets `CI: true` and `GENPRES_DEBUG: 1`. It does not set `GENPRES_URL_ID`, so tests run against
demo and cached data only. A separate `benchmark` job runs `dotnet run BenchmarkBuild`, because the
benchmark projects are outside `GenPRES.sln` and the matrix never compiles them.

### API Documentation (GitHub Actions)

`.github/workflows/docs.yml` runs `dotnet run ApiDocs` on every push to `master` and publishes to
GitHub Pages at `https://informedica.github.io/GenPRES/`. It is a separate workflow so a docs failure
never blocks the test matrix. It does not run on pull requests.

One-time repo setup (admin): Settings → Pages → Build and deployment → Source = "GitHub Actions".

### Release Automation (GitHub Actions)

`.github/workflows/release.yml` runs ShipIt on every push to `master` and opens or updates a draft
release PR on the branch `release/master`. It is separate from `build.yml` so a ShipIt failure never
blocks the test matrix. It needs the repo setting **Settings → Actions → General → "Allow GitHub
Actions to create and approve pull requests"**.

#### Tagging and publishing the Release

`.github/workflows/tag-release.yml` fires when a PR from `release/master` is merged. It:

1. Checks out the merge commit.
2. Runs `scripts/ReleaseNotes.fsx`, which reads `<Version>` from `Directory.Build.props` and extracts
   that version's `CHANGELOG.md` section.
3. Creates an annotated tag `v<version>` on that commit.
4. Creates a GitHub Release with the section as body, flagged pre-release when the version is one.

Both steps are idempotent, so re-running is safe. The trigger is the merged PR's head ref, which
holds for every merge method.

The tag and Release are created with the workflow's own `GITHUB_TOKEN`, and events from that token
start no further workflow runs. Anything downstream must be a job inside `tag-release.yml`, a
`workflow_dispatch` / `repository_dispatch` call, or use a PAT or GitHub App token.

Preview a release body before merging the release PR:

```bash
dotnet fsi scripts/ReleaseNotes.fsx                                 # current version, to stdout
dotnet fsi scripts/ReleaseNotes.fsx 0.1.2-alpha.2 --out notes.md   # any shipped version, to a file
```

`ReleaseNotes.fsx` resolves the version through `scripts/Versioning.fsx`, the same parser
`dotnet run CheckVersions` uses. The changelog grammar it relies on is pinned by
`scripts/ChangelogTests.fsx`.

#### Publishing the Docker image

A `publish-docker-image` job in `tag-release.yml` runs after tagging succeeded. It builds the
`Dockerfile` for `linux/amd64` with `APP_VERSION=<version>`, smoke-tests the image with the demo
sheet ID and a random password (`/` must return 200 within 60 seconds), and pushes
`docker.io/informedica/genpres:<version>`, plus `:latest` for a stable release.

Authentication is Docker Hub OIDC; there is no stored registry credential. One-time setup:

1. **Docker Home → `informedica` → OIDC connections → Create OIDC connection.** Add a ruleset with
   subject `repo:informedica/GenPRES:environment:docker-publish`. Copy the connection ID.
2. **GitHub repo → Settings → Environments → New environment** named `docker-publish`. Optionally
   add required reviewers for a manual gate before every push.
3. **GitHub repo → Settings → Secrets and variables → Actions → Variables** add
   `DOCKERHUB_OIDC_CONNECTIONID` = the connection ID (a variable, not a secret).
4. If the connection's **Failures** tab shows a rejected claim on the first run, copy the exact
   `sub` it logged into the ruleset.

The first push creates the Docker Hub repository as private. A Docker Hub org admin must set it to
public afterwards.

### IDE Integration

#### Visual Studio Code

1. Install the **Ionide for F#** extension (`ionide.ionide-fsharp`)
2. Open the repository root folder; Ionide uses `GenPRES.sln` to discover projects

Run `dotnet run`, `dotnet run ServerTests` and `dotnet run Build` from the integrated terminal.

#### JetBrains Rider

Open `GenPRES.sln` (the `.sln` file, not the folder). Run `dotnet run` from the integrated terminal,
or create a **.NET Project** run configuration for the root `Build.fsproj` with the target name as
program argument.

#### Debug Mode in Rider

**Option 1, attach (recommended)**: start `dotnet run`, then **Run → Attach to Process** and pick
`Informedica.GenPRES.Server`.

**Option 2, run the server directly**: a **.NET Project** run configuration for
`Informedica.GenPRES.Server` with working directory `src/Informedica.GenPRES.Server`. Start the
client separately: `dotnet fable watch -o output -s -e .jsx --run npx vite` from
`src/Informedica.GenPRES.Client/`. The server loads `.env` itself via `Env.loadDotEnv()`.

#### Debug Mode in VS Code

Create `.vscode/launch.json`:

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

Press **F5** to start the server with the debugger attached and start the client in a separate
terminal as above. The C# Dev Kit or the **.NET Install Tool** extension may be required.

## Project Folder Structure

```text
GenPRES/
├── .github/                   # Issue/PR templates, instructions, workflows
├── .husky/                    # Git hooks
├── benchmark/                 # BenchmarkDotNet projects (outside GenPRES.sln)
├── build/                     # FAKE build sources (Build.fsproj stays in the root)
├── data/                      # Application data
│   ├── cache/                 # Cached data files
│   ├── config/                # Configuration files
│   ├── data/                  # JSON data files
│   └── zindex/                # Z-Index drug database files
├── deploy/                    # Deployment output
├── docs/                      # Documentation (see docs/README.md)
│   ├── adr/                   # Architecture Decision Records
│   ├── code-reviews/          # Conformance analyses against external standards
│   ├── data-extraction/       # Dose-rule extraction pipeline
│   ├── domain/                # Domain model specifications
│   ├── implementation-plans/  # Per-issue implementation plans
│   ├── literature/            # Research literature
│   ├── roadmap/               # Backlog, feature requests, fit-gap analyses
│   ├── scenarios/             # Use cases
│   ├── security/              # Security reviews and baseline
│   └── user-guide/            # End-user guide (en/nl) and manual test workflows
├── scripts/                   # Utility scripts
├── src/                       # Source code
└── tests/                     # Test projects, one per library
```

Each `Informedica.*.Lib` under `src/` holds its source files, a `Scripts/` folder with FSI scripts,
`paket.references` and an `.fsproj`. Key configuration files: `GenPRES.sln`, `Build.fsproj`,
`Dockerfile`, `compose.yaml`, `paket.dependencies`, `global.json`, `Directory.Build.props`.

Top-level documents: `README.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, `DEVELOPMENT.md` (this file),
`GOVERNANCE.md`, `ROADMAP.md`, `SECURITY.md`, `SUPPORT.md`, `AGENTS.md`, `WARP.md`.

## Project Architecture

- [Architecture Overview](docs/adr/0001-system-architecture.md): stack, server/client structure, Docker hosting
- [Core Domain Model](docs/domain/core-domain.md): transformation pipeline and domain concepts
- [GenFORM](docs/domain/genform-free-text-to-operational-rules.md): free text to Operational Knowledge Rules
- [GenORDER](docs/domain/genorder-operational-rules-to-orders.md): rules to order scenarios
- [GenSOLVER](docs/domain/gensolver-from-orders-to-quantitative-solutions.md): constraint solving

### Technology Stack

GenPRES is built on the [SAFE Stack](https://safe-stack.github.io/) with .NET 10.0:

- **Informedica.GenPRES.Server**: F# with [Saturn](https://saturnframework.org/)
- **Informedica.GenPRES.Client**: F# with [Fable](https://fable.io/docs/) and [Elmish](https://elmish.github.io/elmish/). Its state machines live in **Informedica.GenPRES.Client.Core**, plain F# that runs under Expecto as well as Fable
- **Testing**: Expecto with FsCheck

### Core Libraries

In dependency order (see [GenFORM Appendix B.3](docs/domain/genform-free-text-to-operational-rules.md#appendix-b3-genform-libraries) for details):

- **Informedica.Utils.Lib**: shared utilities
- **Informedica.Agents.Lib**: agent-based execution (MailboxProcessor)
- **Informedica.Logging.Lib**: concurrent logging
- **Informedica.NLP.Lib**: rule extraction from free text
- **Informedica.GenUNITS.Lib**: unit-safe calculations
- **Informedica.GenSOLVER.Lib**: quantitative constraint solving
- **Informedica.GenCORE.Lib**: core domain model
- **Informedica.ZIndex.Lib**: medication and product database
- **Informedica.ZForm.Lib**: Z-Index dosing reference data
- **Informedica.NKF.Lib**: Kinderformularium dose rule extraction
- **Informedica.FTK.Lib**: Farmacotherapeutisch Kompas dose rule extraction
- **Informedica.GenFORM.Lib**: Operational Knowledge Rules
- **Informedica.GenORDER.Lib**: clinical order scenarios
- **Informedica.GenINTERACT.Lib**: drug interaction rules
- **Informedica.MCP.Lib**: Model Context Protocol for LLM integration
- **Informedica.MCP.Server**: standalone stdio MCP host
- **Informedica.GenPRES.Shared**: shared types and API contract
- **Informedica.GenPRES.Server**: server API and orchestration
- **Informedica.GenPRES.Client.Core**: the client's state machines and policies
- **Informedica.GenPRES.Client**: web UI

## Code Contribution Guidelines

- Libraries follow the `Informedica.{Domain}.{Lib/Server/Client}` naming convention, each with its own test project
- Follow the [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md) and the [Commit Message Instructions](.github/instructions/commit-message.instructions.md)
- `.gitignore` is opt-in: you must explicitly add new files with a `!` line

## Domain-Specific Guidelines

GenPRES targets clinical medication workflows and is developed toward Medical Device Regulation
compliance. Any change to dosage calculation, medication lookup or clinical decision support must be
tested thoroughly, keep units and precision exact, validate its input, and give clear error messages.

## Development Workflow

### Git Workflow

1. Fork the repository and clone your fork
2. Add the upstream remote: `git remote add upstream https://github.com/informedica/GenPRES.git`
3. Sync before starting:

   ```bash
   git checkout master
   git fetch upstream
   git merge upstream/master
   git push origin master
   ```

4. Create a feature branch: `git checkout -b feat/your-feature-name`
5. Commit with conventional commit messages: `git commit -m "feat(scope): description"`
6. Merge `upstream/master` again before pushing, then `git push origin feat/your-feature-name`
7. Open a pull request against `informedica/GenPRES`
8. After the merge, delete the branch locally and on your fork

### Opt-in .gitignore Strategy

`.gitignore` excludes everything by default; each tracked path is allowed explicitly. When adding
files, add the allow-line. Proprietary medication cache files are excluded for licensing reasons.

### Environment Configuration

A `.env` file at the project root is the single source of truth for environment variables. It is not
tracked, so secrets are never committed.

#### Quick Setup

1. Nothing, for the demo: the first `dotnet run` copies `.env.example` to `.env`. By hand: `cp .env.example .env`
2. `.env.example` ships with the public demo sheet ID and an empty password, so the demo works as-is
   with admin operations disabled. Set `GENPRES_PASSWORD` to use the admin pages locally. For
   production data, replace `GENPRES_URL_ID` (ask a team member)

Put a `git worktree` **next to** the main checkout, not inside it: the root resolver and
`Env.loadDotEnv` search upward for `.env`, so a nested worktree would pick up the main checkout's
`.env` and `data/`.

```bash
GENPRES_URL_ID=<your-url-id>   # Google Sheets data URL ID (required; .env.example ships the demo ID)
GENPRES_LOG=i                  # Logging level: 0=off, d=debug, i=info, w=warning, e=error
GENPRES_PROD=0                 # 0=demo (safe default), 1=production data
GENPRES_DEBUG=1                # Debug mode: 0=off, 1=on
GENPRES_LANG=nl                # Default UI language: en, nl, fr, de, es, it
GENPRES_PASSWORD=<password>    # Admin password, see policy below
GENPRES_ROOT=<path>            # Directory holding data/; unset resolves from .env, then data/zindex, then cwd
GENPRES_DB_CONNECTION=<conn>   # SQLite session store; unset = in-memory
GENPRES_SESSION_IDLE_MINUTES=60 # A Session with no request this long ends; unset = 60, not a positive whole number refuses the start
GENPRES_TRUSTED_PROXIES=<ips>  # Comma-separated IPs whose X-Forwarded-For is believed; unset = loopback only
SERVER_PORT=8085               # Kestrel's listen port (no GENPRES_ prefix); the Vite dev proxy targets it
```

#### Default language

`GENPRES_LANG` is the UI language a browser starts in; the client asks the server for it at start-up
and falls back to Dutch until then. Accepted values are `en`, `nl`, `fr`, `de`, `es`, `it` in any
case (display names such as `Nederlands` work too). Unset means `nl`; any other value makes the
server refuse to start. An `la=` URL parameter and a language the user picks both override it.

#### Password policy

`GENPRES_PASSWORD` gates all admin operations (settings page, log analysis, resource reload).

- **Development (`GENPRES_PROD=0`)**: any value is accepted.
- **Production (`GENPRES_PROD=1`)**: a missing or blank password starts the server with admin
  operations disabled and prints a warning. A password shorter than 16 characters refuses the start.
  Generate one with `openssl rand -base64 32` and inject it via a secret store.

Never reuse a development password in production. Never commit a real password.

#### Request logging: clientIP retention and the audit trail

With `GENPRES_LOG` set, the request log writes the caller's `clientIP` in full to `data/logs`. This
is deliberate: GenPRES is reached only through a hospital's launch sequence, so the address is the
hospital's gateway rather than a person's device, and an administrator needs it to correlate an
incident with a launching site. Revisit this if GenPRES is ever exposed where an untrusted client can
reach it directly.

The same files are the medico-legal audit trail. When investigating a reported dosing discrepancy,
start from the `data/logs/genpres_*.log` file for that time window.

#### The log files

With `GENPRES_LOG` set, the server writes one file per stream to `data/logs` (`genpres_request_*.log`,
`genpres_order_*.log`, `genpres_resources_*.log`, …) and the MCP host writes `genpres_mcp_*.log`.
Every file holds one JSON object per line:

```json
{"@t":"2026-09-21T21:09:33.1384620+02:00","@l":"Information","EventType":"Request","Text":"GET /api/x from ::1"}
```

`@t` is the time, `@l` the level, `EventType` the kind of message and `Text` the rendered message
with line feeds escaped. Filter with `jq`:

```bash
# every warning and error of a run, as text
jq -r 'select(."@l" == "Warning" or ."@l" == "Error") | .Text' data/logs/genpres_resources_*.log

# the solver trace of an order log between two times
jq -r 'select(."@t" >= "2026-09-21T21:09" and ."@t" < "2026-09-21T21:10") | .Text' data/logs/genpres_order_*.log
```

The terminal gets the same events as readable text. The file sink blocks the caller rather than drop
an event when its buffer of 100 000 events is full; rendering costs about 4.5 ms per solver event,
so `GENPRES_LOG=d` is for diagnosing one case, not for production load. The admin log analysis reads
these files and refuses one over 50 MB. The order log holds patient data; handle `data/logs` as such.
`src/Informedica.GenPRES.Server/Scripts/LoggingPerf.fsx` measures the cost of logging; rerun it after
a change to the bridge, the sinks or the formatters.

#### How It Works

Environment variables are resolved in this order, highest first:

1. An already-set environment variable (shell, CI, Docker)
2. The `.env` file, loaded by shell scripts or `Env.loadDotEnv()`
3. The hardcoded default in source (demo data)

So a variable set in the shell always overrides `.env`.

#### Loading in Different Contexts

- **Shell**: `set -a; source .env; set +a` before running commands
- **F# scripts and IDEs**: `Informedica.Utils.Lib.Env.loadDotEnv()` searches upward for `.env`
- **Docker**: the image defaults to demo mode. For production inject `GENPRES_PROD=1`,
  `GENPRES_URL_ID` and `GENPRES_PASSWORD` at container runtime and mount `data/cache`; `compose.yaml`
  does this from `.env`. Never use `--build-arg` for secrets: the value would persist in the image
  metadata

#### Common Environment Variable Issues

- **Missing GENPRES_URL_ID**: "cannot find column" errors when loading resources. Check that `.env`
  exists and holds a valid ID.
- **GENPRES_PROD other than `0` in development**: may cause authentication or data access issues.
