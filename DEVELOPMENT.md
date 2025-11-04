# Development on GenPRES

## Getting Started

### Prerequisites

Before contributing, ensure you have the following installed:

- **.NET SDK**: 9.0.0 or later
- **Node.js**: 18.x, 22.x, or 23.x (LTS versions recommended)
- **npm**: 10.x or later

### Setting Up the Development Environment

1. Fork this repository
2. Clone your fork locally
3. Set up the demo environment variables:

```bash
export GENPRES_URL_ID=1xhFPiF-e5rMkk7BRSfbOF-XGACeHInWobxRbjYU0_w4
export GENPRES_LOG=0
export GENPRES_PROD=0
export GENPRES_DEBUG=1
```

If you prefer, you can use direnv, as documented below.

### Start the application

```bash
dotnet run
```

Open your browser to `http://localhost:5173`

## Project Architecture

### Technology Stack

This project is built on the [SAFE Stack](https://safe-stack.github.io/):

- **Server**: F# with [Saturn](https://saturnframework.org/)
- **Client**: F# with [Fable](https://fable.io/docs/) and [Elmish](https://elmish.github.io/elmish/)
- **Testing**: Expecto with FsCheck for property-based testing
- **Build**: .NET 9.0

### Core Libraries

- **Informedica.Utils.Lib**: Shared utilities, common functions
- **Informedica.Agents.Lib**: Implementations of the MailboxProcessor as Agent
- **Informedica.Logging.Lib**: Logging library enabling concurrent logging
- **Informedica.GenCore.Lib**: Domain modelling of core concepts
- **Informedica.GenUnits.Lib**: Units of measure, calculations
- **Informedica.GenSolver.Lib**: Constraint solving, equations, variables
- **Informedica.ZIndex.Lib**: Medication database, drug *product* information
- **Informedica.ZForm.Lib**: Medication database, drug *dosing* information
- **Informedica.GenForm.Lib**: Domain library for all order constraint rules
- **Informedica.GenOrder.Lib**: Medical orders, prescriptions
- **Server**: The server library
- **Client**: The webbased client UI

## Code Contribution Guidelines

### Repository Structure

**Important: an opt-in strategy is used** in the `.gitignore` file, i.e. you have to specifically define what should be included instead of the other way around!!

This project follows specific organizational patterns:

- **Library Structure**: Use the `Informedica.{Domain}.Lib` naming convention
- **Domain Libraries**: GenSolver, GenOrder, GenUnits, ZIndex, Utils
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

```
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

```
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
2. **Create a feature branch**: `git checkout -b feat/your-feature-name`
3. **Make changes** following our coding guidelines
4. **Commit** using conventional commit messages
5. **Push** to your fork
6. **Create a pull request** to the main repository

### Opt-in .gitignore Strategy

This project uses an opt-in strategy for `.gitignore`:

- You must explicitly define what should be included
- When adding new files, ensure they're properly included in Git
- Proprietary medication cache files are excluded for licensing reasons

### Environment Configuration

For development, use these environment variables. Proper configuration is essential to avoid resource loading issues (see [Issue #44](https://github.com/informedica/GenPRES/issues/44) for troubleshooting guidance):

```bash
export GENPRES_URL_ID=1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA  # Demo data URL
export GENPRES_LOG=0          # Logging level
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

