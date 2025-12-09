# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

GenPRES is a Clinical Decision Support System (CDSS) for medication prescribing, built entirely in F# using the SAFE Stack (Saturn, Azure, Fable, Elmish). It provides safe and efficient medication order entry, calculation, and validation for medical settings.

## Development Environment

### Prerequisites

- **.NET SDK**, **Node.js**, and **npm**

For the canonical list of supported versions, see the
**Toolchain Requirements** section in [`DEVELOPMENT.md`](DEVELOPMENT.md#toolchain-requirements).

### Required Environment Variables

For demo and development environment variables, see `DEVELOPMENT.md#environment-configuration`.

## Common Development Commands

### Build and Run

- `dotnet run` - Start full application (server + client with hot reload)
- `dotnet run list` - Show all available build targets
- `dotnet run Build` - Build the solution
- `dotnet run Bundle` - Create production bundle
- `dotnet run Clean` - Clean build artifacts
- Access the application at `http://localhost:5173`

### Testing

- `dotnet run ServerTests` - Run all F# unit tests using Expecto
- `dotnet run TestHeadless` - Run tests in headless mode
- `dotnet run WatchTests` - Run tests in watch mode
- `dotnet test GenPres.sln` - Alternative way to run all tests

### Individual Library Testing

Run tests for specific libraries:

```bash
dotnet test tests/Informedica.GenSolver.Tests/
dotnet test tests/Informedica.GenOrder.Tests/
dotnet test tests/Informedica.GenUnits.Tests/
# ... etc for other test projects
```

### Code Quality

- `dotnet run Format` - Format F# code using Fantomas

### Docker

- `docker build --build-arg GENPRES_URL_ARG="your_secret_url_id" -t halcwb/genpres .`
- `docker run -it -p 8080:8085 halcwb/genpres`
- `dotnet run DockerRun` - Run pre-built Docker image

## Architecture Overview

### High-Level Structure

GenPRES follows a client-server web application architecture:

- **Server** (`src/Server/`): F# + Saturn/Giraffe web framework, exposes REST API
- **Client** (`src/Client/`): F# + Fable (compiled to JavaScript) + React + Vite
- **Shared** (`src/Shared/`): Common types and API contracts
- **Libraries** (`src/Informedica.*.Lib/`): Domain-specific F# libraries

### Key Libraries

1. **Informedica.GenOrder.Lib**: Core medication order modeling and calculation
   - Hierarchical order model (Order → Prescription → Orderable → Component → Item)
   - Maps clinical orders to mathematical equations for solving
   - Supports complex dosing scenarios (per kg, per BSA, ranges, restrictions)

2. **Informedica.GenSolver.Lib**: Constraint solving engine
   - Solves systems of equations with order-independent calculations
   - Uses BigRational for absolute precision to avoid medication dosing errors
   - Handles ranges and restrictions (e.g., dose 60-80mg, frequency 2-4 times/day)

3. **Informedica.GenUnits.Lib**: Units of measure system
   - Automatic unit conversion and validation
   - All calculations performed in base units
   - Prevents unit-related medication errors

4. **Informedica.ZIndex.Lib**: Dutch medication database integration
   - Product data and drug information lookup
   - Maps to local cached medication data

5. **Informedica.GenForm.Lib**: Pharmaceutical forms and preparations
   - Handles different medication forms and preparation methods

### Configuration Architecture

- All medication rules and constraints stored in Google Spreadsheets
- Downloaded as CSV and parsed dynamically
- `GENPRES_URL_ID` environment variable controls which spreadsheet to use
- Local cache files provide offline medication data access

### Communication Pattern

- Client-server communication via Fable.Remoting (type-safe RPC)
- API contracts defined in `src/Shared/Api.fs`
- Server processes medication calculations and returns validated results

## Key Development Patterns

### Medical Domain Focus

- All calculations are unit-safe and use absolute precision (BigRational)
- Order-independent calculation engine - any variable can be solved from any combination of knowns
- Comprehensive validation for medication safety
- Support for complex clinical scenarios (pediatric dosing, weight-based calculations, etc.)

### F# Functional Architecture

- Immutable data structures throughout
- Extensive use of discriminated unions for domain modeling  
- Property-based testing with FsCheck for mathematical properties
- Actor model (MailboxProcessor) for concurrent processing

### Testing Strategy

- Uses Expecto test framework across all F# projects
- Property-based tests for mathematical calculations and unit conversions
- Integration tests for medication calculation scenarios
- Test data includes medical scenarios for validation

## Commit Message Conventions

Use conventional commits with specific scopes:

### Library Scopes

- `gensolver`: Constraint solving and equations
- `genorder`: Medical orders and prescriptions  
- `genunits`: Units of measure and calculations
- `zindex`: Medication database integration
- `utils`: Shared utilities

### Application Scopes  

- `client`: Client-side application
- `server`: Server-side application
- `api`: API endpoints or contracts

### Examples

```
feat(genorder): add pediatric dosage calculation
fix(gensolver): resolve infinite loop in constraint propagation  
refactor(genunits): extract unit conversion logic
test(genorder): add property tests for dose calculations
```

## Important Notes

### Medical Safety Considerations

- This system handles medication dosing calculations - precision and safety are critical
- All mathematical operations use BigRational to prevent rounding errors
- Extensive validation prevents dangerous medication combinations or doses
- Changes to calculation logic require thorough testing

### Data Dependencies

- Production requires proprietary medication cache files (not in repository)
- Demo version uses sample medication data included in repository
- Google Spreadsheets contain live configuration - changes affect running systems

### Performance Considerations

- Calculation engine optimized for complex scenarios with large possibility spaces
- Local caching used for medication data to ensure responsive UI
- Mathematical modeling ongoing to improve efficiency for range-based calculations
