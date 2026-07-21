# Development on GenPRES

## Getting Started

### Toolchain Requirements

Before contributing, ensure you have the following installed (this section is the canonical source for toolchain versions):

- **.NET SDK**: 10.0.0 or later
- **Node.js**: 18.x, 22.x, or 23.x (LTS versions recommended)
- **npm**: 10.x or later

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

## Build System Architecture

### How `dotnet run` Interacts with FAKE

GenPRES uses [FAKE](https://fake.build/) (F# Make) as its build automation tool. The build configuration lives in two files at the repository root:

- **`Build.fs`** – defines all FAKE build targets (tasks) and their dependency chains
- **`Helpers.fs`** – helper functions for running processes (dotnet, npm, docker) in the build

When you type `dotnet run` from the repository root, .NET executes `Build.fsproj`, which is an F# console application that initialises the FAKE execution context. FAKE then reads the target name from the command-line arguments (defaulting to `Run` when none is given) and executes the corresponding target and all of its declared dependencies.

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

For example, `dotnet run` (no target) runs the `Run` target, which depends on:
`Clean → RestoreClient → Build → Run` (server + Fable watcher in parallel).

### FAKE Build Targets Reference

| Command | Target | Description |
|---|---|---|
| `dotnet run` | `Run` | Start server + Fable/Vite dev server with hot reload (default) |
| `dotnet run list` | *(special)* | List all available FAKE targets |
| `dotnet run Build` | `Build` | Compile the entire solution (`GenPRES.sln`) |
| `dotnet run Clean` | `Clean` | Remove `deploy/` and `dist/` artefacts, delete Fable-generated `.jsx` files |
| `dotnet run Bundle` | `Bundle` | Production build: publish server, compile client, copy data |
| `dotnet run ServerTests` | `ServerTests` | Run all F# unit tests (Expecto) with quiet logging |
| `dotnet run CheckVersions` | `CheckVersions` | Verify every built DLL's version matches the root `Directory.Build.props` |
| `dotnet run TestHeadless` | `TestHeadless` | Build and run tests without launching a browser |
| `dotnet run WatchTests` | `WatchTests` | Run tests in watch mode (re-runs on file changes) |
| `dotnet run Format` | `Format` | Format all F# source files using Fantomas |
| `dotnet run DockerRun` | `DockerRun` | Run the pre-built Docker image locally |

#### Target Dependency Chains

```text
Clean ──► RestoreClient ──► Bundle
Clean ──► RestoreClient ──► Build ──► Run

RestoreClient ──► Build ──► TestHeadless
RestoreClient ──► Build ──► WatchTests
```

### What Happens During `dotnet run` (the `Run` target)

The `Run` target starts two long-running processes **in parallel**:

1. **Server** – `dotnet run --no-restore` in `src/Informedica.GenPRES.Server/`
   - Saturn/Giraffe HTTP server on port `8085`
2. **Client** – `dotnet fable watch … --run npx vite` in `src/Informedica.GenPRES.Client/`
   - Fable compiles F# → JavaScript, Vite serves the client on `http://localhost:5173` with Hot Module Replacement (HMR)

Output from both processes is printed concurrently with colour-coded prefixes (`server:`, `client:`).

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
- **`benchmark/run.sh`** — runs `sudo dotnet run -c Release "$@"`. Must be invoked from the `benchmark/` directory; it does not `cd` for you. The `sudo` is required because some BenchmarkDotNet diagnostics need elevated privileges. Extra arguments are forwarded to `dotnet run`.
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
# clear ./src/Informedica.GenPRES.Server/data/logs folder
if [ -d "./src/Informedica.GenPRES.Server/data/logs" ]; then
    echo "Clearing logs folder..."
    rm -rf ./src/Informedica.GenPRES.Server/data/logs/*
    echo "Logs folder cleared."
else
    echo "Logs folder does not exist, creating it..."
    mkdir -p ./src/Informedica.GenPRES.Server/data/logs
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
# clear ./src/Informedica.GenPRES.Server/data/logs folder
if [ -d "./src/Informedica.GenPRES.Server/data/logs" ]; then
    echo "Clearing logs folder..."
    rm -rf ./src/Informedica.GenPRES.Server/data/logs/*
    echo "Logs folder cleared."
else
    echo "Logs folder does not exist, creating it..."
    mkdir -p ./src/Informedica.GenPRES.Server/data/logs
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

None of these wrappers bake `GENPRES_URL_ID` into the image — that constraint is enforced by the `Dockerfile` itself and described in [Environment Configuration](#environment-configuration).

**`docker-local.sh`** — build for the local processor architecture (Apple Silicon → arm64; Intel/Linux → amd64). Save at the repo root:

```bash
#!/usr/bin/env bash
docker build -t halcwb/genpres .
```

**`docker-amd64.sh`** — cross-build an amd64 image on Apple Silicon for deployment to a typical Linux host. Save at the repo root:

```bash
#!/usr/bin/env bash
docker build --platform linux/amd64 -t halcwb/genpres .
```

**`docker-run.sh`** — source `.env`, validate that both `GENPRES_URL_ID` and `GENPRES_PASSWORD` are set (the `:` parameter expansion fails fast if either is missing), then run the container with the right `-e` flags. Save at the repo root:

```bash
#!/usr/bin/env bash
# Load env vars from .env (single source of truth — same as prod.sh / debug.sh).
set -a; source .env; set +a

# Fail fast if .env did not provide the secrets, so we don't start an
# unauthenticated container that the in-server validateProductionPassword
# would refuse later anyway.
: "${GENPRES_URL_ID:?GENPRES_URL_ID is not set in .env}"
: "${GENPRES_PASSWORD:?GENPRES_PASSWORD is not set in .env}"

docker run -e GENPRES_URL_ID="${GENPRES_URL_ID}" \
           -e GENPRES_PASSWORD="${GENPRES_PASSWORD}" \
           -p 8080:8085 halcwb/genpres
```

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
├── docs/                      # Documentation
│   ├── code-reviews/          # Code review documents
│   ├── data-extraction/       # Data extraction documentation
│   ├── domain/                # Domain documentation
│   ├── implementation-plans/  # Implementation plans
│   ├── literature/            # Research literature
│   ├── mdr/                   # Medical Device Regulation documentation
│   │   ├── design-history/    # Design history files
│   │   ├── interface/         # Interface specifications
│   │   ├── post-market/       # Post-market surveillance
│   │   ├── requirements/      # Requirements documentation
│   │   ├── risk-analysis/     # Risk management
│   │   ├── usability/         # Usability engineering
│   │   └── validation/        # Validation documentation
│   ├── roadmap/               # Project roadmap
│   └── scenarios/             # Clinical scenarios
├── scripts/                   # Utility scripts
└── src/                       # Source code
    ├── Informedica.Agents.Lib/           # Agent-based concurrency library
    ├── Informedica.DataPlatform.Lib/     # Data Platform integration
    ├── Informedica.FHIR.Lib/             # FHIR resource conversion
    ├── Informedica.FTK.Lib/              # Adult formulary parsing library
    ├── Informedica.GenCORE.Lib/          # Core domain library
    ├── Informedica.GenFORM.Lib/          # Formulary management library
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
    ├── Informedica.HIXConnect.Lib/       # HIX Connect integration
    ├── Informedica.Logging.Lib/          # Logging utilities
    ├── Informedica.MCP.Lib/              # Model Context Protocol for LLM integration
    ├── Informedica.MetaVision.Lib/       # MetaVision integration
    ├── Informedica.NKF.Lib/              # Pediatric formulary parsing library
    ├── Informedica.NLP.Lib/              # Natural Language Processing for rule extraction
    ├── Informedica.OTS.Lib/              # Ontology Terminology Server integration
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
- `docs/mdr/design-history/0001-system-architecture.md` - Technical architecture
- `docs/domain/` - Domain model specifications
- `docs/user-guide/` - Multilingual user guide ([English](docs/user-guide/en/user-guide.md), [Nederlands](docs/user-guide/nl/gebruikershandleiding.md))

## Directory Descriptions

### Core Directories

- **`.github/`** - GitHub configurations (issue/PR templates, workflows, development instructions)
- **`benchmark/`** - Performance benchmarking suite
- **`data/`** - Application data (drug cache, configuration, clinical data, Z-Index database)
- **`docs/`** - Comprehensive documentation:
  - `docs/domain/` - Domain model specifications (Core Domain, GenFORM, GenORDER, GenSOLVER)
  - `docs/mdr/` - MDR compliance (design history, requirements, risk analysis, validation)
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

- **[Architecture Overview](docs/mdr/design-history/0001-system-architecture.md)**: Technical stack, server/client structure, Docker hosting, and build configuration
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

For complete library specifications including capabilities and dependencies, see [GenFORM Appendix B.3](docs/domain/genform-free-text-to-operational-rules.md#addendum-b3-genform-libraries).

Key libraries in dependency order:

- **Informedica.Utils.Lib**: Shared utilities, common functions  
- **Informedica.Agents.Lib**: Agent-based execution (MailboxProcessor)  
- **Informedica.Logging.Lib**: Concurrent logging  
- **Informedica.NLP.Lib**: Natural Language Processing for structured rule extraction
- **Informedica.OTS.Lib**: Google Sheets/CSV and Ontology Terminology Server integration
- **Informedica.GenUNITS.Lib**: Unit-safe calculations  
- **Informedica.GenSOLVER.Lib**: Quantitative constraint solving  
- **Informedica.GenCORE.Lib**: Core domain model  
- **Informedica.ZIndex.Lib**: Medication and product database  
- **Informedica.ZForm.Lib**: Z-Index dosing reference data  
- **Informedica.NKF.Lib**: Kinderformularium dose rule extraction
- **Informedica.FTK.Lib**: Farmacotherapeutisch Kompas dose rule extraction
- **Informedica.GenFORM.Lib**: Operational Knowledge Rules (OKRs)  
- **Informedica.GenORDER.Lib**: Clinical order scenarios and execution  
- **Informedica.MCP.Lib**: Model Context Protocol for LLM integration
- **Informedica.FHIR.Lib**: FHIR resource conversion
- **Informedica.DataPlatform.Lib**: Data Platform integration
- **Informedica.HIXConnect.Lib**: HIX Connect integration
- **Informedica.MetaVision.Lib**: MetaVision integration
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

1. Copy the example file: `cp .env.example .env`
2. Edit `.env` and fill in the `GENPRES_URL_ID` value (ask a team member for the production URL ID)

The `.env` file uses standard `KEY=VALUE` format:

```bash
GENPRES_URL_ID=<your-url-id>   # Google Sheets data URL ID (required)
GENPRES_LOG=i                  # Logging level: 0=off, d=debug, i=info, w=warning, e=error
GENPRES_PROD=0                 # Production mode: 0=demo (safe default), 1=production data
GENPRES_DEBUG=1                # Debug mode: 0=off, 1=on
GENPRES_PASSWORD=<password>    # Admin password — see policy below
```

#### Password policy

`GENPRES_PASSWORD` gates all admin operations (settings page, log analysis,
resource reload). The server enforces a length policy at startup:

- **Development (`GENPRES_PROD=0`)**: any value is accepted, including the
  trivial `genpres` used by some local setups. Convenient for development;
  unsafe anywhere else.
- **Production (`GENPRES_PROD=1`)**: the server **refuses to start** when
  `GENPRES_PASSWORD` is missing or shorter than 16 characters. Generate a
  strong value with a CSPRNG, e.g. `openssl rand -base64 32`, and inject it
  via a secret store (Docker secret, Kubernetes secret, vault, ...).

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
- **Docker**: Inject `GENPRES_URL_ID` (and `GENPRES_PASSWORD` for admin operations) at *container runtime*, not at build time. Example: `docker run -e GENPRES_URL_ID="$GENPRES_URL_ID" -e GENPRES_PASSWORD="$GENPRES_PASSWORD" -p 8080:8085 halcwb/genpres`. For production, use a Docker or Kubernetes secret. **Do not** use `--build-arg`: the value would be persisted as image metadata and visible to anyone who can pull the image.

#### Common Environment Variable Issues

**Missing GENPRES_URL_ID**: Will cause "cannot find column" errors when the application tries to load resources from Google Sheets. Make sure your `.env` file exists and contains a valid `GENPRES_URL_ID`.

**Incorrect GENPRES_PROD value**: Setting this to anything other than `0` in development may cause authentication or data access issues.

For background on this approach, see [Issue #44](https://github.com/informedica/GenPRES/issues/44).
