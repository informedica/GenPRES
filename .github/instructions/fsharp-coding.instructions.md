---
description: "F# coding conventions for GenPRES"
applyTo: "**/*.fs,**/*.fsx"
---

# F# Coding Instructions

Layout is Fantomas' job, see the formatting instructions. These are the rules it cannot enforce.

## Style

- Two blank lines between top-level constructs and between functions in a module; one between logical sections in a function
- PascalCase for types, modules and public members; camelCase for local bindings. Descriptive names in the public API, short names inside function bodies
- Do not use an F# reserved word as an identifier. Besides the keywords, that is the OCaml tokens `asr`, `land`, `lor`, `lsl`, `lsr`, `lxor`, `mod`, `sig` and the reserved tokens `break`, `checked`, `component`, `const`, `constraint`, `continue`, `event`, `external`, `include`, `mixin`, `parallel`, `process`, `protected`, `pure`, `sealed`, `tailcall`, `trait`, `virtual`
- One `namespace` or top-level `module` per file, named after the file. Keep `open` statements few and close to their use; prefer a targeted open over `open System`
- Modules and functions over classes; classes only for interop or framework integration
- `[<RequireQualifiedAccess>]` on DUs and modules

## Documentation and Comments

`///` documents what a caller can name: modules, types, functions, members, record fields and union cases. Only `///` reaches IntelliSense and the API reference. `//` goes inside a function body and explains the why of an implementation choice. A short trailing `//` beside a union case is fine for notation (`| MinIncr of Minimum * Increment // <min .. incr ..>`) but does not replace the `///` line.

```fsharp
/// A patient with the medical information an order is calculated from.
type Patient =
    {
        /// The unique identifier for the patient.
        Id: PatientId
        /// Absent for a patient whose age is estimated rather than known.
        DateOfBirth: DateTime option
    }
```

**A `///` block is plain prose or all XML, never both.** A block that starts with `<` is emitted as XML verbatim; any other block is escaped and wrapped in a `<summary>`, so a tag in it shows as literal text and prose after a tag never reaches the popup. Default to plain prose. Use XML only when a tag carries what prose cannot (a unit, a bound, the behaviour on empty input, the exception raised), and then the whole block is elements, starting with `<summary>`:

```fsharp
/// <summary>Creates an Increment from a ValueUnit.</summary>
/// <param name="vu">The value and unit; must hold at least one non-zero value.</param>
/// <exception cref="Exceptions.ValueRangeEmptyIncrementException">When the ValueUnit is empty.</exception>
let createIncrement vu = ...
```

In an XML block: escape every literal angle bracket (`70.0&lt;kg&gt;`), document all parameters or none, and keep each `<param name>` matching a real parameter. Only four library projects report a malformed block (FS3390); elsewhere it fails silently, which is one more reason to stay with prose.

- No backticks and no `<c>` in a plain block: comments are not Markdown, a backtick reaches the reader as a backtick
- Never `(* … *)`, not for documentation and not for commented-out code; delete the code, git holds the history
- A comment says the thing in words and does not cite a rule, plan step or issue number, which goes stale. Two exceptions: a comment on code not built yet keeps the rule it will be built to, and a stop-gap keeps the issue number that will remove it
- Wording, for every comment and document in the repository: plain complete English that stands without the surrounding code or conversation; say a thing in full ("a single value", "the value 1", not "its one value"); state the condition a statement holds under; write a value as a value; repeat the noun when a pronoun could point at two things; no coined terms

## Types

- Types at the top of a module, before the functions that use them. Define the type, then a module of the same name with its constructor and core operations, so call sites read `Patient.create ...`
- Domain concepts as discriminated unions; records over tuples; `option` over null; a primitive value wrapped in a single-case DU (`[<Struct>]` for small ones on hot paths)
- `[<NoEquality>]` and `[<NoComparison>]` on aggregates that must not be compared structurally
- Domain types immutable; validation through a private constructor and a smart constructor returning `Result` in the module
- `[<CLIMutable>]` only for DTOs
- Do not mark a type or function `private` unless told to. A pure function is never private: it has no state or side effects, and hiding it only blocks reuse and tests. `private` is for what is harmful to expose: smart-constructor invariants, mutable state, IO wiring

```fsharp
[<Struct>]
type PatientId = private PatientId of string

[<NoEquality; NoComparison>]
type Patient = private { Id: PatientId; Name: NonEmptyString }

module Patient =
    /// Creates a patient, validating the id and the name.
    let create id name =
        result {
            let! id = PatientId.create id
            let! name = NonEmptyString.create name
            return { Id = id; Name = name }
        }
```

## Functions

- Small functions with one responsibility, composed with `|>`; no deep nesting
- Pattern matching over if-else chains; total functions over partial matches; validate inputs early
- A choice is a DU, never a boolean flag
- Pure logic apart from IO; dependencies passed as parameters

**Type annotations.** F# infers types; annotate only where it cannot: a member access on a parameter whose type is not yet known (`provider.Get`), an overloaded method or operator, or a record expression the compiler resolves to the wrong type. Inference reads top to bottom and never looks at call sites, so let the body fix the type first: pipe the known value in (`xs |> List.map _.Length`) or use a module function (`String.length s`) over a member (`s.Length`). Do not annotate to document; the reference shows the inferred type. When changing existing code, remove the annotations it does not need.

```fsharp
// Bad - every type can be inferred
let getFilterOptions (provider: IResourceProvider) (filter: DoseFilter) : Result<FilterOptions, Message list> =
    Api.filterPrescriptionRules provider filter |> Result.map PrescriptionRule.filterOptions

// Good
let getFilterOptions provider filter =
    Api.filterPrescriptionRules provider filter |> Result.map PrescriptionRule.filterOptions

// Good - the member access needs the type
let departments (provider: IResourceProvider) = provider.Get Keys.departments
```

## Errors

- `Result<'T,'Error>` for what can fail, `Option<'T>` for what may be absent; exceptions only for the unexpected or unrecoverable
- Error types as DUs, not strings; validation errors aggregated in a DU or non-empty collection
- Chain with `Result.bind`, the `result` computation expression (FsToolkit.ErrorHandling) or the AsyncResult helpers
- Never `failwith` / `failwithf`, which throw a bare `System.Exception`. Use `invalidArg (nameof x)` for an argument, `invalidOp` for invalid state, `raise` with a specific exception type (`KeyNotFoundException`, `FormatException`, `TimeoutException`, `SolverException`, ...), `reraise ()` to propagate, and Expecto's `failtest` in tests

## Units and Numbers

Units of measure on every physical quantity, with explicit conversion functions between compatible units. BigRational for all medication calculations; `float` with explicit tolerances only for scientific values that need it.

## Performance

Profile before optimizing. `seq` for large data that need not be materialized; arrays and `[<Struct>]` wrappers in tight numeric work; `voption` on hot paths; memoize expensive pure functions; `async`/`task` for IO; tail recursion or folds over unbounded recursion.

## Testing

Expecto with Expecto.Flip, the actual value piped into the assertion: `actual |> Expect.equal "message" expected`.

- One test project per library, `runTestsInAssemblyWithCLIArgs [] argv` in `Main.fs`, nested `testList`s that mirror the library, marked `[<Tests>]`
- Every public function: success and failure paths, edge cases (zero, negative, empty), round trips (`serialize >> deserialize = id`)
- Names that state the expected behaviour; `testTask` for `Task`/`Async` code
- Properties with FsCheck through `testPropertyWithConfig` and custom generators; data-driven cases in a `testList` `for` loop
- Time and randomness injected (`IClock`, `IRng`), never `DateTime.Now`; floats compared with `Accuracy.areClose`; exceptions with `Expect.throws`

```fsharp
let config = { FsCheckConfig.defaultConfig with maxTest = 1000; arbitrary = [ typeof<Generators> ] }

testPropertyWithConfig config "round-trip serialization" <| fun input ->
    input |> serialize |> deserialize = input

testList "parameterized" [
    for input, expected in testCases do
        test $"case %A{input}" { processInput input |> Expect.equal "matches" expected }
]
```

## Domain Modeling

Model the domain in types before logic, and make illegal states unrepresentable: value objects as single-case DUs, non-empty collections where emptiness is invalid, workflows as state machines (a DU for the states, functions for the transitions). Validate at API boundaries and return structured errors. Keep persistence and wire shapes out of domain types; map at the boundary.

## Fable JSX Interpolated Strings

Nothing non-trivial inside a JSX template (`$"""..."""`). Every `sx` object or other anonymous record is a named `let` before the template, shared at module level across components where possible; a one-property record used once may stay inline. Every event handler is a named `let`; only a one-line dispatch (`fun _ -> Close |> dispatch`) may stay inline, never a multi-line lambda, one with a type annotation, or one that reads `e.target`.

```fsharp
// Bad
JSX.jsx $"""<TextField sx={ {| alignItems = "flex-end"; gap = 2 |} } onChange={fun (e: Browser.Types.Event) -> setPassword (e.target?value: string)} />"""

// Good
let fieldSx = {| alignItems = "flex-end"; gap = 2 |}
let handlePasswordChange (e: Browser.Types.Event) = setPassword (e.target?value: string)

JSX.jsx $"""<TextField sx={fieldSx} onChange={handlePasswordChange} />"""
```
