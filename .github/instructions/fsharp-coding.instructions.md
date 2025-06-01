# F# Coding Instructions

## General F# Guidelines

### Code Style and Formatting
- Use 4 spaces for indentation (no tabs)
- Keep lines under 120 characters when possible
- Use meaningful names for functions, types, and variables:
    - Make variable names short when used in function bodies or functions not intended for public use
- Follow F# naming conventions:
  - PascalCase for types, modules, and public members
  - camelCase for local bindings and private members
  - Use descriptive names over abbreviations

### Type Definitions
- Define types at the module level before functions that use them
- Use discriminated unions for modeling domain concepts
- Prefer records over tuples for data with multiple fields
- Use option types instead of null values
- Create wrapper types for primitive values to ensure type safety
- Use active patterns for complex pattern matching scenarios

```fsharp
// Good
type Patient = {
    Id: PatientId
    Name: string
    DateOfBirth: DateTime option
}

type MedicationStatus =
    | Active
    | Discontinued
    | Suspended of reason: string
```

### Function Design
- Keep functions small and focused on a single responsibility
- Use partial application and currying effectively
- Prefer immutable data structures
- Use pattern matching instead of if-else chains when appropriate
- Design for function composition and piping (`|>`)
- Separate pure business logic from I/O operations

```fsharp
// Good
let calculateDosage bodyWeight medication =
    match medication with
    | Paracetamol -> bodyWeight * 10.0<mg/kg>
    | Ibuprofen -> bodyWeight * 5.0<mg/kg>
    | Custom dose -> dose
```

### Error Handling
- Use Result<'T, 'Error> for operations that can fail:
    - Use exceptions when dealing with truly unexpected or unrecoverable errors (e.g., system failures, programming errors)
- Avoid throwing exceptions in business logic
- Use Option<'T> for values that might not exist
- Create specific error types for different failure modes
- Chain error handling using `Result.bind` and similar combinators

```fsharp
// Good
let validateDosage dose maxDose =
    if dose <= maxDose then
        Ok dose
    else
        Error "Dose exceeds maximum safe limit"
```

### Module Organization
- Group related functionality in modules
- Use explicit module declarations
- Keep modules focused and cohesive
- Expose only necessary functions (use `internal` when appropriate)
- Place types at the top of modules before functions
- Use nested modules for related functionality
- Create separate modules for DTOs, validation, and business logic
- Create consistent API modules that expose main functionality

### Assembly and Project Structure
- Use `AssemblyInfo.fs` files for consistent versioning across libraries
- Include assembly metadata like title, product, company, and description
- Use semantic versioning (Major.Minor.Patch)
- Include git hash and release channel metadata for traceability
- Use the `Informedica.{Domain}.Lib` naming convention for library projects
- Organize code into domain-specific libraries
- Keep shared types and utilities in separate libraries

```fsharp
[<assembly: AssemblyTitleAttribute("Informedica.GenSolver.Lib")>]
[<assembly: AssemblyProductAttribute("Informedica.GenSolver.Lib")>]
[<assembly: AssemblyCompanyAttribute("halcwb")>]
[<assembly: AssemblyVersionAttribute("0.2.2")>]
```

### Units of Measure
- Define units of measure for all physical quantities
- Use consistent unit handling patterns across libraries
- Ensure calculations preserve unit safety
- Create conversion functions between compatible units

### Testing
- Write unit tests for all public functions
- Use property-based testing for complex logic
- Test edge cases and error conditions
- Keep tests readable and maintainable
- Create separate test projects for each library
- Test both success and failure paths
- Create test utilities for common setup operations

```fsharp
[<Test>]
let ``calculateDosage should return correct dose for paracetamol`` () =
    let result = calculateDosage 10.0<kg> Paracetamol
    result |> should equal 100.0<mg>
```

### Documentation
- Use XML documentation for public APIs
- Include examples in documentation when helpful
- Document complex algorithms or business rules
- Keep comments focused on "why" rather than "what"

### Performance Considerations
- Use sequences (seq) for large datasets that don't need to be fully materialized
- Consider using async/task for I/O operations
- Profile before optimizing
- Prefer functional approaches but be pragmatic about performance
- Use `seq` for lazy evaluation of large datasets
- Implement memoization for expensive pure functions
- Consider async patterns for I/O-bound operations

### Logging and Observability
- Implement structured logging throughout the application
- Use dependency injection for logger instances
- Log at appropriate levels (Debug, Info, Warning, Error)
- Include correlation IDs for tracking requests

### Configuration Management
- Use environment variables for configuration
- Provide sensible defaults for optional settings
- Separate development, test, and production configurations
- Make configuration immutable once loaded

## Project-Specific Guidelines

### Domain Modeling
- Model the domain using F# types before implementing logic
- Use units of measure for quantities (mg, kg, ml, etc.)
- Make illegal states unrepresentable through type design
- Leverage F#'s type system to encode business rules

### API Design
- Use Railway Oriented Programming for complex workflows
- Validate inputs at API boundaries
- Return structured errors with helpful messages
- Use async for all I/O operations
- Design APIs that support method chaining and fluent interfaces

### Data Access Patterns
- Separate data models from business logic
- Use mapping functions between different representations
- Implement caching strategies for expensive data operations
- Design for both local and remote data sources

### Solver Pattern (for Mathematical Libraries)
- Separate constraint definition from solving logic
- Use variable and equation abstractions for mathematical modeling
- Implement logging and debugging capabilities for complex algorithms
- Design for extensibility with different solving strategies

### Code Generation
- Use code generation for repetitive data access code
- Generate types from external schemas when appropriate
- Maintain generated code in separate files
- Document the generation process clearly

### Dependencies
- Minimize external dependencies
- Prefer pure functions over stateful operations
- Use dependency injection for external services
- Mock external dependencies in tests