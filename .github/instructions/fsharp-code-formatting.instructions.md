---
description: "F# code formatting and indentation conventions"
applyTo: "**/*.fs,**/*.fsx"
---

# FSharp Code Formatting Instructions

## Fantomas Owns the Layout

Fantomas formats every `.fs` file: the pre-commit hook formats staged files, CI runs `dotnet fantomas --check`, and `dotnet run format` formats the tree. Its settings live in `.editorconfig`: 4-space indentation, lines under 120 characters, brackets of a multiline list, record or lambda on their own lines aligned with the block, one record field per line, a leading bar on every union case, the pipeline operator at the start of the continuation line, at most two consecutive blank lines. Do not spend effort on any of that, and do not fight the result; write the code and let the formatter lay it out.

Scripts (`.fsx`) are excluded from Fantomas, so code in a script is laid out by hand in the same style; it is formatted when it migrates.

The rules below are the ones Fantomas cannot enforce.

## Declaration Order

Declare types in order without the `and` keyword whenever possible; use `and` only for mutually recursive types.

```fsharp
// Bad - `and` without recursion
type Container = { Items: Item list }
and Item = { Name: string }

// Good - separate declarations in dependency order
type Item = { Name: string }

type Container = { Items: Item list }
```

## Formatting Consistency

When consecutive lines apply the same pattern, format them the same way. If one long expression must be split across several lines, split its sibling expressions the same way.

## Comments

Fantomas does not touch the body of a comment. The rules live in
[F# Coding Instructions](fsharp-coding.instructions.md#documentation-and-comments):
`///` on everything a caller can name, including record fields and discriminated union
cases; `//` inside a body for the *why*; a block that is either plain prose or all XML
elements, never both; no backticks; no `(* … *)`; wording that stands without the surrounding code.

Fantomas will not rewrap a long comment either, so break one by hand at the same 120
columns the rest of the file keeps to.

## Interface Definitions

Always use the `[<Interface>]` attribute on interfaces to prevent accidental conversion to abstract classes. The `IXxx` naming convention alone is not sufficient. A pure marker interface written as `type IMarker = interface end` does not need it.

```fsharp
// Bad - missing attribute
type ILogger =
    abstract member Log: string -> unit

// Good - with attribute
[<Interface>]
type ILogger =
    abstract member Log: string -> unit
```

## F# Idioms

### Unused Self Identifier

When a class member does not use the `this` identifier, discard it with an underscore: `member _.Increment() = 42`, not `member this.Increment() = 42`.

### TryXxx Methods

When using BCL `TryXxx` methods that return `(bool * 'T)`, pattern match instead of checking the boolean:

```fsharp
// Bad
let hasValue = dict.TryGetValue(key, &value)
if hasValue then
    // use value

// Good
match dict.TryGetValue(key) with
| true, value -> // use value
| false, _ -> // handle missing case

// Good - multiple conditions
match obj.TryGetProperty("foo"), obj.TryGetProperty("bar") with
| (true, _), (true, _) -> // both present
| _ -> // at least one missing
```

### Shorthand Lambda (`_.Property`)

Prefer the F# 8 shorthand lambda `_.Property` (or `_.Method()`) over `fun x -> x.Property` when the lambda only projects a chain of members. Chained access (`_.Result.FullName`) and terminal method calls (`_.ToString()`) work. Fall back to `fun x -> ...` when the argument is used more than once, in arithmetic, or in a pattern match.

```fsharp
// Bad - verbose lambda for a simple projection
users |> List.map (fun u -> u.Name)
equations |> List.groupBy (fun eq -> eq.Result.FullName)

// Good - shorthand lambda
users |> List.map _.Name
equations |> List.groupBy _.Result.FullName
```

### Array and List Indexing

Use the indexer without the leading dot: `items[0]`, `s[.. 200]`, never `items.[0]` or `s.[.. 200]`. Applies to arrays, lists, strings and any type with an `Item` indexer, including slicing.

### String Interpolation

Prefer interpolated strings (`$"..."`) over `sprintf`, `String.Format` or concatenation. Always put a format specifier (`%s`, `%i`, `%f`, `%A`) inside the braces: a bare `{x}` risks formatting a whole record when you wanted one field.

```fsharp
// Bad - sprintf
let msg = sprintf "User %s has %i orders" user.Name count

// Bad - interpolation without format specifier (whole record may be formatted)
let msg = $"User: {user} has {count} orders"

// Good - interpolation with explicit specifiers
let msg = $"User %s{user.Name} has %i{count} orders"
```

`sprintf "%A"` (or `$"%A{x}"`) is fine for diagnostic dumps of complex objects where full structural formatting is the intent.
