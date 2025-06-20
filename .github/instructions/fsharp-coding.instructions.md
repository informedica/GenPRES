# F# Coding Instructions

## General F# Guidelines

### Code Style and Formatting
- Use 4 spaces for indentation (no tabs)
- Keep lines under 120 characters when possible
- Use 2 newlines to separate top-level constructs (types, modules, functions)
- Use 2 newlines to separate function definitions within a module
- Use single blank lines to separate logical sections within a function
- Use meaningful names for functions, types, and variables:
    - Make variable names short when used in function bodies or functions not intended for public use
- Follow F# naming conventions:
  - PascalCase for types, modules, and public members
  - camelCase for local bindings and private members
  - Use descriptive names over abbreviations
  - Avoid using reserved keywords as identifiers, reserved keywords are:

The following tokens are reserved in F# because they are keywords in the OCaml language:

- asr
- land
- lor
- lsl
- lsr
- lxor
- mod
- sig

If you use the `--mlcompatibility` compiler option, the above keywords are available for use as identifiers.

The following tokens are reserved as keywords for future expansion of F#:

- break
- checked
- component
- const
- constraint
- continue
- event
- external
- include
- mixin
- parallel
- process
- protected
- pure
- sealed
- tailcall
- trait
- virtual   

### Documentation and Comments
- Use `///` for XML documentation comments that appear in IntelliSense popups
- Use `//` for regular comments that document code internally but don't appear in popups
- XML documentation should be used for:
  - Type definitions and their purpose
  - Public functions and their behavior
  - Module-level documentation
- Regular comments should be used for:
  - Record fields and discriminated union cases
  - Private implementation details
  - Code clarifications and explanations
- Format XML documentation consistently:
  - Use `<summary>` tags for multi-line descriptions
  - Use single-line `///` for simple descriptions
  - Include parameter and return value documentation when helpful

```fsharp
// Good - XML documentation for types and public APIs
/// <summary>
/// Represents a patient with their medical information
/// </summary>
type Patient =
    {
        // The unique identifier for the patient
        Id: PatientId
        // The patient's full name
        Name: string
        // Optional date of birth
        DateOfBirth: DateTime option
    }

/// Calculates the appropriate dosage for a patient
let calculateDosage bodyWeight medication = ...

// Good - Regular comments for implementation details
let private processData input =
    // Convert input to internal format first
    let normalized = normalizeInput input
    // Apply business rules
    applyRules normalized
```

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

#### Testing Framework and Structure
- Use Expecto as the primary testing framework
- Use `runTestsInAssemblyWithCLIArgs [] argv` in Main.fs for test discovery
- Organize tests in nested modules that mirror the library structure
- Use `[<Tests>]` attribute to mark test collections
- Use `testList` to group related tests together

```fsharp
// Test project structure
[<EntryPoint>]
let main argv =
    runTestsInAssemblyWithCLIArgs [] argv

module Tests =
    module DomainTests =
        let tests = testList "Domain" [
            // tests here
        ]

    [<Tests>]
    let tests = testList "LibraryName Tests" [
        DomainTests.tests
    ]
```

#### Test Naming and Documentation
- Use descriptive test names with backticks for complex scenarios
- Include expected behavior in test names
- Use both `test` and `testCase` syntax consistently
- Write tests that clearly express intent and expected outcomes

```fsharp
test "substance nacl to mmol" {
    // test implementation
}

test "``calculateDosage should return correct dose for paracetamol``" {
    // test implementation
}
```

#### Property-Based Testing
- Use FsCheck integration through Expecto for property-based tests
- Configure custom generators for domain-specific types
- Set appropriate test counts for thorough coverage
- Use `testPropertyWithConfig` for custom FsCheck configurations

```fsharp
let config = {
    FsCheckConfig.defaultConfig with
        maxTest = 10000
        arbitrary = [ typeof<Generators.BigRGenerator> ]
}

testPropertyWithConfig config "property description" <| fun input ->
    // property test implementation
```

#### Assertion Patterns
- Use `Expect.equal` with descriptive failure messages
- Use `Expect.isTrue` and `Expect.isFalse` for boolean assertions
- Use `Expect.throws` for exception testing
- Prefer pipeline syntax with `|>` for readability
- Use Unquote for complex assertions when needed

```fsharp
result
|> Expect.equal "should be equal" expected

someCondition
|> Expect.isTrue "condition should be true"

(fun () -> dangerousOperation())
|> Expect.throws "should throw an exception"
```

#### Data-Driven Testing
- Use lists or arrays of test cases for parameterized testing
- Create helper functions for common test patterns
- Use `for` loops in testList for generating multiple similar tests

```fsharp
let testCases = [
    input1, expected1
    input2, expected2
]

testList "parameterized tests" [
    for input, expected in testCases do
        test $"test with {input}" {
            processInput input
            |> Expect.equal "should match expected" expected
        }
]
```

#### Testing Complex Scenarios
- Test "there and back again" scenarios for serialization/deserialization
- Test boundary conditions and edge cases explicitly
- Create specific tests for error conditions and validation
- Test both positive and negative cases for business rules

```fsharp
test "there and back again, simple dto" {
    let original = createTestData()

    original
    |> serialize
    |> deserialize
    |> Expect.equal "should roundtrip correctly" original
}
```

#### Test Utilities and Helpers
- Create reusable helper functions for common test setup
- Use consistent patterns for test data creation
- Create custom generators for complex domain types
- Share common test utilities across test projects

```fsharp
let equals expected message actual =
    Expect.equal actual expected message

let createTestPatient name age =
    { Name = name; Age = age; /* other fields */ }
```

#### Integration and System Testing
- Separate unit tests from integration tests
- Use TestServer for API testing when applicable
- Mock external dependencies appropriately
- Test configuration and environment setup

#### Performance and Mathematical Testing
- Use appropriate precision for floating-point comparisons
- Test mathematical operations with edge cases (zero, negative, infinity)
- Include performance benchmarks for critical algorithms
- Test with large datasets when relevant

```fsharp
test "floating point comparison with tolerance" {
    let result = complexCalculation()
    let expected = 1.23456789

    Accuracy.areClose Accuracy.veryHigh result expected
    |> Expect.isTrue "should be within tolerance"
}
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