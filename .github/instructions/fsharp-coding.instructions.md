---
description: "Fsharp-instructions"
applyTo: "**/*.fs,**/*.fsx"
---

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

Additional style guidance:
- Namespace and opens:
  - Place `namespace` (or a single top-level `module`) at the top of the file.
  - Keep `open` statements minimal and as close as possible to where they’re needed; prefer local `open` inside modules over file-wide opens.
  - Avoid `open` on very broad namespaces (e.g., `open System`); prefer targeted opens (e.g., `open System.Text`).
- One top-level module or namespace per file; the file name should match the top-level module/namespace for discoverability.
- Prefer modules and functions over classes; use object-oriented constructs only for interop or framework integration.
- Prefer qualified access:
  - Use `[<RequireQualifiedAccess>]` on DUs and modules to reduce name collisions and make call sites explicit.

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
  - Include examples (`<example>`) and remarks (`<remarks>`) for non-trivial APIs

```fsharp
// Good - XML documentation for types and public APIs
/// <summary>
/// Represents a patient with their medical information
/// </summary>
/// <remarks>
/// Enforces that illegal states (e.g., empty name) are prevented by smart constructors.
/// </remarks>
type Patient =
    {
        // The unique identifier for the patient
        Id: PatientId
        // The patient's full name
        Name: string
        // Optional date of birth
        DateOfBirth: DateTime option
    }

/// <summary>Calculates the appropriate dosage for a patient.</summary>
/// <param name="bodyWeight">Body weight in kg.</param>
/// <param name="medication">Medication type used to determine factor.</param>
/// <returns>Dose in mg.</returns>
/// <example>
/// let dose = calculateDosage 70.0<kg> Paracetamol
/// </example>
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
- Create wrapper types for primitive values to ensure type safety (single-case DUs)
- Use active patterns for complex pattern matching scenarios
- Use `[<NoEquality>]` and `[<NoComparison>]` on aggregates that should not be compared structurally
- Use `[<RequireQualifiedAccess>]` for DUs and modules to avoid unqualified usage
- Keep domain types immutable; prefer private constructors with smart constructors in modules
- Use `[<CLIMutable>]` only for DTOs, not for domain types

```fsharp
// Good
[<Struct>]
type PatientId = private PatientId of string

[<RequireQualifiedAccess>]
type MedicationStatus =
    | Active
    | Discontinued
    | Suspended of reason: string

type Patient = {
    Id: PatientId
    Name: string
    DateOfBirth: DateTime option
}
```

### Type and Module Shadowing Pattern
- Create specific modules for each domain type that shadow the type name
- Define the type first at the top level, then create a module with the same name
- This enables clean API usage like `Patient.create ...`
- Place constructor and core operations in the shadowing module
- Prefer smart constructors returning `Result<_,_>` if validation is required

```fsharp
// Good - Type-first with shadowing module
/// Represents a patient in the medical system
[<NoEquality; NoComparison>]
type Patient = private {
    Id: PatientId
    Name: NonEmptyString
    BirthDate: DateTime option
}

/// Functions for working with Patient instances
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Patient =
    /// Creates a new patient with validation
    let create id name birthDate : Result<Patient, PatientError> =
        result {
            let! id = PatientId.create id
            let! name = NonEmptyString.create name
            return { Id = id; Name = name; BirthDate = birthDate }
        }

    /// Calculates the patient's age
    let calculateAge currentDate (patient: Patient) =
        // implementation

    /// Validates patient data (returns unit on success)
    let validate (patient: Patient) : Result<unit, PatientError list> =
        // implementation

// Usage:
let patientRes =
  result {
    let! p = Patient.create "ABC12345" "John Doe" (Some birthDate)
    return Patient.calculateAge DateTime.UtcNow p
  }
```

#### Benefits of Type Shadowing
- Discoverability: IntelliSense shows both type and related functions together
- Consistency: Follows .NET and F# core library patterns (`List`, `Map`, etc.)
- Type Safety: Explicit return types in module functions ensure correctness
- Clean APIs: Natural, readable code that expresses intent clearly
- Encapsulation: Private constructors + smart constructors enforce invariants

### Function Design
- Keep functions small and focused on a single responsibility
- Use partial application and currying effectively
- Prefer immutable data structures
- Use pattern matching instead of if-else chains when appropriate
- Design for function composition and piping (`|>`) and avoid deep indentation
- Avoid boolean flags; model choices as discriminated unions
- Prefer total functions; avoid partial pattern matches—validate inputs early
- Separate pure business logic from I/O operations; pass dependencies as parameters

```fsharp
// Good
let calculateDosage bodyWeight medication =
    match medication with
    | Paracetamol -> bodyWeight * 10.0<mg/kg>
    | Ibuprofen -> bodyWeight * 5.0<mg/kg>
    | Custom dose -> dose

// Model choices as DUs instead of boolean flags
type Query = ById of PatientId | ByName of NonEmptyString

let handleQuery fetchById fetchByName = function
| ById id -> fetchById id
| ByName name -> fetchByName name
```

### Error Handling
- Use `Result<'T,'Error>` for operations that can fail
  - Use exceptions only for unexpected or unrecoverable errors (system failures, programming errors)
- Avoid throwing exceptions in business logic
- Use `Option<'T>` for values that might not exist
- Prefer specific error types (DUs) over strings
- Aggregate validation errors using a DU or non-empty collection
- Chain error handling using `Result.bind`, computation expressions, or helper modules
- For async workflows, standardize on `Task<Result<'T,'Error>>` with helper functions (AsyncResult)

```fsharp
// Good - typed errors
type DosageError =
    | ExceedsMaximum of max: float<mg>
    | NegativeDose

let validateDosage dose maxDose =
    if dose < 0.0<mg> then Error NegativeDose
    elif dose <= maxDose then Ok dose
    else Error (ExceedsMaximum maxDose)

// Result computation expression
type ResultBuilder() =
    member _.Bind(x,f) = Result.bind f x
    member _.Return x = Ok x
    member _.ReturnFrom x = x
let result = ResultBuilder()

// AsyncResult helpers based on Task<Result<_,_>>
module AsyncResult =
    let bind (f: 'a -> Task<Result<'b,'e>>) (t: Task<Result<'a,'e>>) = task {
        let! r = t
        match r with
        | Ok v -> return! f v
        | Error e -> return Error e
    }
    let map f (t: Task<Result<'a,'e>>) = task {
        let! r = t
        return Result.map f r
    }
```

### Module Organization
- Group related functionality in modules
- Use explicit module declarations
- Keep modules focused and cohesive
- Expose only necessary functions (prefer `internal` for non-public API)
- Place types at the top of modules before functions
- Use nested modules for related functionality
- Create separate modules for DTOs, validation, and business logic
- Create consistent API modules that expose main functionality
- Use `.fsi` signature files in libraries to control visibility and hide constructors and fields for domain types
- Use `[<RequireQualifiedAccess>]` for DUs and modules to keep call sites explicit

### Assembly and Project Structure
- Prefer SDK-style projects; avoid manual `AssemblyInfo.fs` in new projects
- Centralize common settings in `Directory.Build.props` and enable SourceLink
- Use semantic versioning via git tags with MinVer or Nerdbank.GitVersioning
- Include assembly metadata via SDK properties (Title, Description, Company)
- Keep shared types and utilities in separate libraries
- Organize code into domain-specific libraries using `Informedica.{Domain}.Lib` naming
- Enable deterministic builds and repository metadata for traceability

Example SourceLink setup (Directory.Build.props):
```xml
<Project>
  <PropertyGroup>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <Deterministic>true</Deterministic>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.*" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

If you need explicit attributes, you can still include an `AssemblyInfo.fs`:
```fsharp
[<assembly: AssemblyTitleAttribute("Informedica.GenSolver.Lib")>]
[<assembly: AssemblyProductAttribute("Informedica.GenSolver.Lib")>]
[<assembly: AssemblyCompanyAttribute("halcwb")>]
[<assembly: AssemblyVersionAttribute("0.2.2")>]
do ()
```

Tooling and quality gates:
- Enforce formatting with Fantomas (configure via `.editorconfig` or `fantomas-config.json`)
- Use FSharpLint for code smells and consistency
- Treat warnings as errors in libraries; use pragmas sparingly
- Add BenchmarkDotNet projects for hot paths

### Units of Measure
- Define units of measure for all physical quantities
- Use consistent unit handling patterns across libraries
- Ensure calculations preserve unit safety
- Create explicit conversion functions between compatible units
- Prefer `decimal` for financial values; use `float`/`float32` with explicit tolerances for scientific values
- Validate ranges via smart constructors

```fsharp
[<Measure>] type mg
[<Measure>] type kg
[<Measure>] type mgkg = mg/kg

module Dose =
    let calc (bw: float<kg>) (factor: float<mgkg>) : float<mg> = bw * factor
```

### Testing
- Write unit tests for all public functions
- Use property-based testing for complex logic
- Test edge cases and error conditions
- Keep tests readable and maintainable
- Create separate test projects for each library
- Test both success and failure paths
- Create test utilities for common setup operations
- Avoid `DateTime.Now` in tests; inject time via an `IClock`/provider
- Add golden tests for serialization/deserialization stability

Example preferred test setup
```fsharp
open Expecto
open Expecto.Flip

let run = 
    runTestsWithCLIArgs [] [||] 

// preferred test setup using Expecto.Flip
// enabling pipelining of the actual value to 
// the expecto test with the message and expected value
test "Example test" {
    // GOOD
    // explicit expected result
    let exp = 1

    // GOOD
    1
    // pipeline actual to test with
    // message as interpolated string with exp
    |> Expect.equal $"1 should be equal to {exp}" exp
}
|> run
```

#### Testing Framework and Structure
- Use Expecto as the primary testing framework
- Use `runTestsInAssemblyWithCLIArgs [] argv` in Main.fs for test discovery
- Organize tests in nested modules that mirror the library structure
- Use `[<Tests>]` attribute to mark test collections
- Use `testList` to group related tests together
- Provide an async testing pattern for `Task`/`Async` return values

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

// Async test example
testTask "async workflow returns Ok" {
    let! res = workflowUnderTest ()
    res |> Expect.equal "should succeed" (Ok 42)
}
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
type Generators =
    static member NonEmptyString() =
        Arb.from<string>
        |> Arb.filter (fun s -> not (System.String.IsNullOrWhiteSpace s))

let config = {
    FsCheckConfig.defaultConfig with
        maxTest = 1000
        endSize = 100
        arbitrary = [ typeof<Generators> ]
}

testPropertyWithConfig config "round-trip serialization" <| fun input ->
    input
    |> serialize
    |> deserialize
    = input
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
- Use `for` loops in `testList` for generating multiple similar tests

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
    { Name = name; Age = age; (* other fields *) }
```

#### Integration and System Testing
- Separate unit tests from integration tests
- Use TestServer for API testing when applicable
- Mock external dependencies appropriately
- Test configuration and environment setup
- Make time and randomness explicit dependencies (inject IClock/IRng) for deterministic tests

#### Performance and Mathematical Testing
- Use appropriate precision for floating-point comparisons
- Test mathematical operations with edge cases (zero, negative, infinity)
- Include performance benchmarks for critical algorithms (BenchmarkDotNet)
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
- Consider literate programming or script-based samples for runnable docs when appropriate

### Performance Considerations
- Use sequences (`seq`) for large datasets that don't need to be fully materialized
- Consider `async`/`task` for I/O operations
- Profile before optimizing
- Prefer functional approaches but be pragmatic about performance
- Use `seq` for lazy evaluation of large datasets
- Implement memoization for expensive pure functions
- Consider async patterns for I/O-bound operations
- Prefer `ValueOption` (`voption`) in hot paths to reduce allocations
- Prefer arrays for tight numeric work; prefer structs (`[<Struct>]` single-case DUs) for small wrappers in hot paths
- Ensure tail recursion or use folds to avoid stack growth

### Logging and Observability
- Implement structured logging throughout the application
- Use dependency injection for logger instances
- Log at appropriate levels (Debug, Info, Warning, Error)
- Include correlation IDs for tracking requests
- Use message templates (e.g., Serilog style) instead of string interpolation
- Avoid logging PII; redact sensitive data (especially in medical contexts)

### Configuration Management
- Use environment variables for configuration
- Provide sensible defaults for optional settings
- Separate development, test, and production configurations
- Make configuration immutable once loaded
- Represent configuration as typed records and validate at startup
- Treat time and randomness as dependencies (inject IClock/IRng)

## Project-Specific Guidelines

### Domain Modeling
- Model the domain using F# types before implementing logic
- Use units of measure for quantities (mg, kg, ml, etc.)
- Make illegal states unrepresentable through type design
- Leverage F#'s type system to encode business rules
- Avoid primitive obsession: prefer value objects (single-case DUs) and non-empty collections
- Model workflows explicitly (e.g., state machines with DUs for states and transitions)

```fsharp
[<RequireQualifiedAccess>]
type PrescriptionState =
    | Draft of DraftData
    | Signed of SignedData
    | Dispensed of DispensedData

module Prescription =
    let sign draft : Result<PrescriptionState, Error> =
        // validate…
        Ok (PrescriptionState.Signed signedData)
```

### API Design
- Use Railway Oriented Programming for complex workflows
- Validate inputs at API boundaries
- Return structured errors with helpful messages
- Use async for all I/O operations
- Design APIs that support method chaining and fluent interfaces
- Provide Result/AsyncResult helpers and computation expressions to simplify composition

### Data Access Patterns
- Separate data models from business logic
- Use mapping functions between different representations (DTO ↔ Domain)
- Implement caching strategies for expensive data operations
- Design for both local and remote data sources
- Keep persistence concerns out of domain types; map at boundaries

### Solver Pattern (for Mathematical Libraries)
- Separate constraint definition from solving logic
- Use variable and equation abstractions for mathematical modeling
- Implement logging and debugging capabilities for complex algorithms
- Design for extensibility with different solving strategies
- Provide reproducibility via explicit seed/control of randomness

### Code Generation
- Use code generation for repetitive data access code
- Generate types from external schemas when appropriate
- Maintain generated code in separate files
- Document the generation process clearly
- Keep generated code isolated from handwritten domain code

### Dependencies
- Minimize external dependencies
- Prefer pure functions over stateful operations
- Use dependency injection for external services
- Mock external dependencies in tests
- Keep boundaries thin and map exceptions to domain errors at the edge

## References
- F# for Fun and Profit: Domain Modeling and Railway Oriented Programming — [https://fsharpforfunandprofit.com/](https://fsharpforfunandprofit.com/)
- Domain Modeling Made Functional (Scott Wlaschin) — [https://pragprog.com/titles/swdddf/domain-modeling-made-functional/](https://pragprog.com/titles/swdddf/domain-modeling-made-functional/)
- Official F# Style Guide — [https://learn.microsoft.com/dotnet/fsharp/style-guide/](https://learn.microsoft.com/dotnet/fsharp/style-guide/)
- Fantomas (F# formatter) — [https://github.com/fsprojects/fantomas](https://github.com/fsprojects/fantomas)
- FSharpLint — [https://github.com/fsprojects/FSharpLint](https://github.com/fsprojects/FSharpLint)
- BenchmarkDotNet — [https://benchmarkdotnet.org/](https://benchmarkdotnet.org/)