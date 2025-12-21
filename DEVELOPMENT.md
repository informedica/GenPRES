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
- `docs/mdr/design-history/architecture.md` - Technical architecture
- `docs/domain/` - Domain model specifications

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

- **[Architecture Overview](docs/mdr/design-history/architecture.md)**: Technical stack, server/client structure, Docker hosting, and build configuration
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

#### F# Development

Follow our [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md) which include:

- Use 4 spaces for indentation (no tabs)
- Keep lines under 120 characters
- Use meaningful names with PascalCase for types/modules, camelCase for local bindings
- Use discriminated unions for domain modeling
- Prefer immutable data structures
- Use Result<'T, 'Error> for operations that can fail
- Write comprehensive unit tests using Expecto
- Use property-based testing for complex logic

#### Commit Messages

Follow our [Commit Message Instructions](.github/instructions/commit-message.instructions.md):

Use conventional commits format:

```text
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`

**Scopes for GenPRES**:

- Library scopes: `gensolver`, `genorder`, `genunits`, `zindex`, `utils`
- Application scopes: `client`, `server`, `api`, `ui`, `config`
- Infrastructure scopes: `deps`, `docker`, `github`, `build`, `deploy`

**Examples**:

```text
feat(genorder): add pediatric dosage calculation
fix(genunits): correct mg/ml to mmol/L conversion for NaCl
docs(readme): update installation instructions
test(gensolver): add property-based tests for constraint solving
```

### Testing Requirements

- Write unit tests for all public functions
- Use Expecto as the testing framework
- Include both positive and negative test cases
- Test edge cases and error conditions
- Use property-based testing with FsCheck for mathematical operations
- Ensure tests are readable and maintainable

### Documentation

- Use XML documentation for public APIs
- Include examples in documentation when helpful
- Document complex algorithms or business rules
- Keep comments focused on "why" rather than "what"
- Update README.md if adding new features or changing setup procedures

## Domain-Specific Guidelines

### Medical Safety Considerations

When contributing to medical functionality:

- **Patient Safety First**: All changes affecting dosage calculations, medication lookup, or clinical decision support must be thoroughly tested
- **Precision Matters**: Use appropriate units of measure and maintain calculation accuracy
- **Validation Required**: Implement comprehensive input validation for medical data
- **Error Handling**: Provide clear, actionable error messages for medical professionals
- **MDR Compliance**: Ensure all medical-related changes align with Medical Device Regulation requirements

### Mathematical Operations

- Use units of measure for all physical quantities
- Ensure calculations preserve unit safety
- Test with edge cases (zero, negative, infinity)
- Include precision and accuracy considerations
- Document mathematical properties being maintained

### Performance Considerations

- Use sequences (seq) for large datasets
- Consider async patterns for I/O operations
- Profile before optimizing
- Maintain functional approaches while being pragmatic about performance

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

For development, use these environment variables. Proper configuration is essential to avoid resource loading issues (see [Issue #44](https://github.com/informedica/GenPRES/issues/44) for troubleshooting guidance):

```bash
export GENPRES_URL_ID=1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM  # Default demo data URL
export GENPRES_LOG=1          # Logging level (1 = enabled)
export GENPRES_PROD=0         # Must be 0 for demo version
export GENPRES_DEBUG=1        # Enable debug mode
```

#### Common Environment Variable Issues

**Missing GENPRES_URL_ID**: Will cause "cannot find column" errors when the application tries to load resources from Google Sheets.

**Incorrect GENPRES_PROD value**: Setting this to anything other than `0` in development may cause authentication or data access issues.

#### Alternative Setup Methods

Many developers prefer using tools like `direnv` for automatic environment variable loading:

1. Install direnv: `brew install direnv` (macOS) or equivalent for your OS
2. Create `.envrc` file in project root with the variables above
3. Run `direnv allow` to enable automatic loading
4. Variables will be loaded automatically when entering the project directory

For other environment variable management approaches, see [Issue #44](https://github.com/informedica/GenPRES/issues/44) for community discussions and recommendations.
