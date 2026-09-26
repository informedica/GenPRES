---
description: "Fsharp-instructions"
applyTo: "**/*.fs,**/*.fsx"
---

# F# Coding Instructions

## Code Style and Formatting

- 4 spaces for indentation, lines under 120 characters
- 2 blank lines between top-level constructs and between functions in a module; 1 blank line between logical sections in a function
- PascalCase for types, modules and public members; camelCase for local bindings. Descriptive names for public API, short names inside function bodies
- Do not use F# reserved keywords as identifiers, including the OCaml-compatibility tokens (`asr`, `land`, `lor`, `lsl`, `lsr`, `lxor`, `mod`, `sig`) and the tokens reserved for future use (`break`, `checked`, `component`, `const`, `constraint`, `continue`, `event`, `external`, `include`, `mixin`, `parallel`, `process`, `protected`, `pure`, `sealed`, `tailcall`, `trait`, `virtual`)
- One `namespace` or top-level `module` per file, named after the file. Keep `open` statements minimal and close to their use; prefer targeted opens over `open System`
- Prefer modules and functions over classes; use classes only for interop or framework integration
- Use `[<RequireQualifiedAccess>]` on DUs and modules

## Documentation and Comments

### `///` documents the API, `//` explains the implementation

- Use `///` on everything a caller can name: modules, types, functions, members,
  **record fields and discriminated union cases**. Only `///` reaches an IntelliSense
  popup and the generated API reference; a `//` above a declaration documents it for
  nobody but the next reader of that file.
- Use `//` inside a function body, for the *why* of an implementation choice — the
  constraint, the ordering, the workaround that the code cannot state itself.
- A short trailing `//` beside a discriminated union case stays allowed for notation that
  is not prose (`| MinIncr of Minimum * Increment // <min .. incr ..>`), but it is no
  longer a reason to skip the `///` line: the same notation is safe inside a plain `///`
  block, where angle brackets are escaped for you.

```fsharp
/// A patient with the medical information an order is calculated from.
/// Illegal states are prevented by the smart constructors in the Patient module.
type Patient =
    {
        /// The unique identifier for the patient.
        Id: PatientId
        /// The patient's full name; never empty.
        Name: string
        /// Absent for a patient whose age is estimated rather than known.
        DateOfBirth: DateTime option
    }
```

### A doc comment is either plain prose or XML — never both

The compiler decides per block. If the block starts with a `<`, the whole block is emitted
as XML verbatim: tags work, and every literal `<` has to be escaped. Otherwise the whole
block is escaped and wrapped in an implicit `<summary>`: angle brackets are safe, and any
tag you write shows up as literal text.

Mixing the two loses information in both directions. Prose before a `<param>` turns the
tags into visible markup; prose after a `<summary>` sits outside every element and never
reaches the popup or the reference page at all.

**Default to plain prose**, one line or several. It needs no tags to produce a summary.

```fsharp
/// Split string s at character c.
let split c s = ...

/// Picks the nearest candidate at or above the target, and the highest
/// candidate below it when there is none at or above.
let pickNearestHigherElseLower target candidates = ...
```

**Reach for XML only when a tag carries information the prose cannot**: a unit, a bound,
what an empty input does, what the function raises. Then the block is *all* elements,
starting with `<summary>`.

```fsharp
/// <summary>Creates an Increment from a ValueUnit.</summary>
/// <param name="vu">The value and unit; must hold at least one non-zero value.</param>
/// <returns>An Increment over the non-zero values of <paramref name="vu"/>.</returns>
/// <exception cref="Exceptions.ValueRangeEmptyIncrementException">When the ValueUnit is empty.</exception>
let createIncrement vu = ...
```

Three things the compiler enforces on such a block, all as FS3390:

- it must be well-formed XML, so a literal angle bracket is `&lt;` / `&gt;` — including a
  unit of measure in an example, `70.0&lt;kg&gt;`;
- `<param>` is all or nothing: document one parameter and the compiler asks for the rest;
- a `<param name="...">` that names no parameter is an error, so a rename has to carry the
  comment with it.

FS3390 is only switched on in four of the nineteen projects under `src/` today — GenSOLVER,
GenUNITS, GenORDER and Utils, plus three test projects and one benchmark. In the other
fifteen a malformed block fails silently, which is the stronger reason to keep a block
plain unless it has earned its tags.

### No backticks, no `<c>` in a plain block

`UsesMarkdownComments` is `false` in the root `Directory.Build.props`, so a `///` comment is
never Markdown: `` `ValueUnit` `` reaches the popup and the reference page as a literal
backtick. Write the identifier bare in a plain block. `<c>` and `<code>` work only inside a
block that is already XML.

### Never `(* … *)`

Not for documentation and not for commented-out code. Delete the code; `git log` is the
record of what was there.

### Comments are self-contained

A comment says the thing in words. It does not cite a rule, concept, extension, use-case
step, implementation plan or issue number: the citation goes stale as soon as that
document is renumbered, moved or closed, and the reader has to leave the file to learn
what the code does. Two exceptions:

- a comment on code that is **not built yet** keeps the rule it will be built to;
- a **stop-gap** keeps the issue number that will remove it.

## Type Definitions

- Define types at the top of a module, before the functions that use them
- Model domain concepts as discriminated unions; prefer records over tuples; use `option` instead of null
- Wrap primitive values in single-case DUs (`[<Struct>]` for small ones in hot paths)
- Use `[<NoEquality>]` and `[<NoComparison>]` on aggregates that must not be compared structurally
- Keep domain types immutable; where validation is needed, use a private constructor with a smart constructor returning `Result` in the module
- `[<CLIMutable>]` only for DTOs, never for domain types

```fsharp
[<Struct>]
type PatientId = private PatientId of string

[<RequireQualifiedAccess>]
type MedicationStatus =
    | Active
    | Discontinued
    | Suspended of reason: string
```

### Scoping and use of `private`

Do not make types or functions private, unless explicitly mentioned in a comment or being told to.

Never mark a pure function `private`. Pure functions have no side effects, no hidden state, and no security implications — hiding them only makes them harder to reuse, test, and compose. Leave them public so they remain available as building blocks. `private` is reserved for cases where exposure is actively harmful (e.g., smart-constructor invariants, mutable state, IO wiring), not as a default.

### Type and Module Shadowing Pattern

Define the type first, then a module with the same name holding its constructor and core operations, so call sites read `Patient.create ...`.

```fsharp
/// Represents a patient in the medical system
[<NoEquality; NoComparison>]
type Patient = private {
    Id: PatientId
    Name: NonEmptyString
    BirthDate: DateTime option
}

/// Functions for working with Patient instances
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
```

## Function Design

- Small functions with one responsibility; design for composition and piping (`|>`), avoid deep nesting
- Pattern matching over if-else chains; total functions over partial matches, validate inputs early
- Model choices as discriminated unions, never as boolean flags
- Keep pure logic separate from IO; pass dependencies as parameters

## Error Handling

- Use `Result<'T,'Error>` for operations that can fail and `Option<'T>` for values that may be absent. Exceptions are for unexpected or unrecoverable errors only
- Prefer specific error types (DUs) over strings; aggregate validation errors in a DU or non-empty collection
- Chain with `Result.bind`, the `result` computation expression (FsToolkit.ErrorHandling), or AsyncResult helpers over `Task<Result<'T,'Error>>`
- Never use `failwith` / `failwithf`: they throw a bare `System.Exception`, which the .NET design guidelines forbid. Use `invalidArg (nameof x)` for argument preconditions, `invalidOp` for invalid state, `raise` with a specific BCL or library exception type (`KeyNotFoundException`, `FormatException`, `TimeoutException`, `SolverException`, ...), `reraise ()` to propagate a caught exception, and Expecto's `failtest` inside tests

```fsharp
type DosageError =
    | ExceedsMaximum of max: float<mg>
    | NegativeDose

let validateDosage dose maxDose =
    if dose < 0.0<mg> then Error NegativeDose
    elif dose <= maxDose then Ok dose
    else Error (ExceedsMaximum maxDose)
```

## Units of Measure

Define units for all physical quantities, keep calculations unit-safe, and write explicit conversion functions between compatible units. Use BigRational for all medication calculations (see AGENTS.md); `float` with explicit tolerances only for scientific values that need it.

## Performance

- Profile before optimizing; prefer functional code and be pragmatic about hot paths
- `seq` for large data that need not be materialized; arrays and `[<Struct>]` wrappers in tight numeric work; `voption` in hot paths
- Memoize expensive pure functions; `async`/`task` for IO; tail recursion or folds over unbounded recursion


## Testing

All tests use Expecto with Expecto.Flip, so the actual value is piped into the assertion:

```fsharp
open Expecto
open Expecto.Flip

test "Example test" {
    let exp = 1

    1
    |> Expect.equal $"1 should be equal to {exp}" exp
}
```

- One test project per library; `runTestsInAssemblyWithCLIArgs [] argv` in `Main.fs`; nested `testList`s that mirror the library, marked `[<Tests>]`
- Test every public function, both success and failure paths, edge cases (zero, negative, empty) and round trips (`serialize >> deserialize = id`)
- Descriptive names that state the expected behaviour; `testTask` for `Task`/`Async` code
- Property-based tests with FsCheck through `testPropertyWithConfig`, with custom generators for domain types
- Data-driven tests as a list of cases in a `testList` `for` loop
- Inject time and randomness (`IClock`, `IRng`); never `DateTime.Now` in a test
- Floating-point comparisons with `Accuracy.areClose`; `Expect.throws` for exceptions

```fsharp
let config = { FsCheckConfig.defaultConfig with maxTest = 1000; arbitrary = [ typeof<Generators> ] }

testPropertyWithConfig config "round-trip serialization" <| fun input ->
    input |> serialize |> deserialize = input

testList "parameterized tests" [
    for input, expected in testCases do
        test $"test with {input}" {
            processInput input
            |> Expect.equal "should match expected" expected
        }
]
```

## Domain Modeling

- Model the domain in types before writing logic; make illegal states unrepresentable
- Avoid primitive obsession: value objects as single-case DUs, non-empty collections where emptiness is invalid
- Model workflows as explicit state machines: a DU for the states, functions for the transitions
- Validate at API boundaries, return structured errors, use async for all IO
- Keep persistence and wire shapes out of domain types; map at the boundary

## Fable JSX Interpolated Strings

- Never create anonymous records inline inside JSX interpolated strings (`$"""..."""`). Extract every `sx` object and other anonymous record to a named `let` binding before the template; share module-level bindings across components (`let private flexEndSx = {| alignItems = "flex-end" |}`). A trivial single-property record used once may stay inline
- Never inline non-trivial lambdas (event handlers such as `onChange`, `onClick`, `onSubmit`) inside JSX strings; extract them to named `let` bindings. A one-line dispatch lambda (`fun _ -> Close |> dispatch`) may stay inline; multi-line lambdas, lambdas with type annotations, or lambdas reading `e.target`/`e.currentTarget` must be extracted

```fsharp
// Bad - inline anonymous record and handler in the JSX string
JSX.jsx
    $"""
    <TextField
        sx={ {| alignItems = "flex-end"; gap = 2 |} }
        onChange={fun (e: Browser.Types.Event) ->
                      setPassword (e.target?value: string)
                      setLoginError false}
    />
    """

// Good - extracted to named bindings
let fieldSx = {| alignItems = "flex-end"; gap = 2 |}

let handlePasswordChange (e: Browser.Types.Event) =
    setPassword (e.target?value: string)
    setLoginError false

JSX.jsx
    $"""
    <TextField sx={fieldSx} onChange={handlePasswordChange} />
    """
```
