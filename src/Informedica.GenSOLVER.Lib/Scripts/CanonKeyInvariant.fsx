#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// CanonKeyInvariant.fsx — Property-based tests for the CanonKey invariant
// =============================================================
//
// The canonical-key algorithm (from MemoCanon.fsx / LRUCache.fsx):
//   1. Collect all variable names from the equation.
//   2. Sort them alphabetically.
//   3. Assign each a symbol x0, x1, x2, …
//   4. Replace every original name in Equation.toString with its symbol.
//
// Core invariant
// --------------
// Two equations that share the same structure (operator, arity, value
// ranges) but differ only in variable names produce the SAME canonical
// key, provided the alphabetical ordering of those names is preserved.
//
// This script verifies that invariant with FsCheck property-based tests,
// completing step 3 of the W2 LRU-cache roadmap:
//
//   ✅ LRUCache.fsx     — session-level LRU cache implementation + unit tests
//   ✅ LRUCacheProps.fsx — 8 FsCheck property tests for the LRU cache
//   ✅ CanonKeyInvariant.fsx (this file) — CanonKey renaming invariant properties
//   □  LRUSolverIntegration.fsx — integrate LRU cache into Solver.fs prototype
//
// Properties
// ----------
//   1. Idempotence         — ofEquation is deterministic: same call → same key
//   2. Name-count          — sortedNames returns exactly one entry per variable
//   3. Symbol assignment   — first alphabetically → x0, second → x1, …
//   4. Rename invariant    — alpha-order-preserving rename → same canonical key
//   5. Value sensitivity   — identical structure, different values → different key
//   6. Structure sensitivity — product vs sum equation → different key
//   7. Longer name first   — canonicalise handles longer names before shorter ones
//   8. Partial overlap safe — "dose" / "dosePerKg" don't collide after substituton
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi CanonKeyInvariant.fsx
// =============================================================

#r "nuget: Expecto, 9.0.4"
#r "nuget: FsCheck, 2.16.6"
#r "nuget: Expecto.FsCheck, 9.0.4"

open System
open Informedica.Utils.Lib.BCL
open Expecto
open Expecto.Flip
open FsCheck
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib


// ------------------------------------------------------------------
// CanonKey implementation (verbatim copy from MemoCanon.fsx/LRUCache.fsx
// so this script is self-contained)
// ------------------------------------------------------------------

module CanonKey =

    let sortedNames (eq: Types.Equation) =
        eq
        |> Equation.toVars
        |> List.map (Variable.getName >> Variable.Name.toString)
        |> List.sort

    let symbol i = $"x{i}"

    let nameMap (eq: Types.Equation) =
        eq |> sortedNames |> List.mapi (fun i n -> n, symbol i) |> Map.ofList

    let canonicalise (nmap: Map<string, string>) (s: string) =
        nmap
        |> Map.toSeq
        |> Seq.sortByDescending (fun (name, _) -> name.Length)
        |> Seq.fold (fun (acc: string) (name, sym) -> acc.Replace(name, sym)) s

    let ofEquation (eq: Types.Equation) =
        let nmap = nameMap eq
        eq |> Equation.toString true |> canonicalise nmap


// ------------------------------------------------------------------
// Equation helpers (same pattern as MemoCanon.fsx / LRUCache.fsx)
// ------------------------------------------------------------------

let setValues u n vs eqs =
    let nm = n |> Variable.Name.createExc

    let prop =
        vs |> ValueUnit.create u |> Variable.ValueRange.ValueSet.create |> Types.ValsProp

    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs


/// Build a 3-variable product equation: lhs = rhs1 * rhs2
let makeMultEq lhs rhs1 rhs2 values =
    [ $"{lhs} = {rhs1} * {rhs2}" ]
    |> Api.init
    |> setValues Units.Count.times rhs1 values
    |> setValues Units.Count.times rhs2 values

/// Build a 3-variable sum equation: lhs = rhs1 + rhs2
let makeSumEq lhs rhs1 rhs2 values =
    [ $"{lhs} = {rhs1} + {rhs2}" ]
    |> Api.init
    |> setValues Units.Count.times rhs1 values
    |> setValues Units.Count.times rhs2 values


// ------------------------------------------------------------------
// FsCheck configuration: 1 000 cases per property
// ------------------------------------------------------------------

let fsCheckConfig =
    { FsCheckConfig.defaultConfig with maxTest = 1000 }


// ------------------------------------------------------------------
// Property helpers
// ------------------------------------------------------------------

/// Generate a list of n distinct strings drawn from a fixed alphabet so
/// that every generated set of names has well-defined alphabetical order.
///
/// Alphabet: "aa", "ab", "ac", … "ba", "bb", …  (two-char strings, no
/// conflicts with the canonical symbols x0, x1, …)
let distinctNames (n: int) (seed: int) : string list =
    let alphabet = "abcdefghijklmnopqrstuvwxyz"

    seq {
        for c1 in alphabet do
            for c2 in alphabet do
                yield $"{c1}{c2}"
    }
    |> Seq.skip (seed % 400)  // vary start point by seed
    |> Seq.take n
    |> Seq.toList


// ------------------------------------------------------------------
// Unit tests — concrete cases that pin down key properties
// ------------------------------------------------------------------

let unitTests =
    testList
        "CanonKey — unit tests"
        [

            test "1. idempotence — same equation produces the same key each time" {
                let eqs = makeMultEq "result" "factorA" "factorB" [| 1N .. 5N |]
                let eq = List.head eqs
                let key1 = CanonKey.ofEquation eq
                let key2 = CanonKey.ofEquation eq
                key1 |> Expect.equal "key must be deterministic" key2
            }

            test "2. sortedNames returns all variable names, sorted" {
                let eqs = makeMultEq "result" "factorA" "factorB" [| 1N .. 3N |]
                let names = List.head eqs |> CanonKey.sortedNames
                names |> Expect.equal "sorted names" [ "factorA"; "factorB"; "result" ]
            }

            test "3. symbol assignment — first alphabetically becomes x0" {
                let eqs = makeMultEq "result" "factorA" "factorB" [| 1N .. 3N |]
                let nmap = List.head eqs |> CanonKey.nameMap
                nmap |> Map.find "factorA" |> Expect.equal "factorA → x0" "x0"
                nmap |> Map.find "factorB" |> Expect.equal "factorB → x1" "x1"
                nmap |> Map.find "result" |> Expect.equal "result → x2" "x2"
            }

            test "4. rename invariant — alpha-order-preserving rename shares key" {
                // eq1: result = factorA * factorB
                // eq2: total  = inputX  * inputY
                // factorA < factorB < result  ←→  inputX < inputY < total
                // Both map to: x2 = x0 * x1
                let vals = [| 1N .. 5N |]
                let eq1 = makeMultEq "result" "factorA" "factorB" vals |> List.head
                let eq2 = makeMultEq "total"  "inputX"  "inputY"  vals |> List.head
                let k1 = CanonKey.ofEquation eq1
                let k2 = CanonKey.ofEquation eq2
                k1 |> Expect.equal "identical structure, same alpha order → same key" k2
            }

            test "5. value sensitivity — same structure, different values → different key" {
                let eq1 = makeMultEq "result" "factorA" "factorB" [| 1N .. 5N |] |> List.head
                let eq2 = makeMultEq "result" "factorA" "factorB" [| 10N .. 15N |] |> List.head
                let k1 = CanonKey.ofEquation eq1
                let k2 = CanonKey.ofEquation eq2
                k1 |> Expect.notEqual "different values → different key" k2
            }

            test "6. structure sensitivity — product vs sum → different key" {
                let vals = [| 1N .. 5N |]
                let eq1 = makeMultEq "result" "factorA" "factorB" vals |> List.head
                let eq2 = makeSumEq  "result" "factorA" "factorB" vals |> List.head
                let k1 = CanonKey.ofEquation eq1
                let k2 = CanonKey.ofEquation eq2
                k1 |> Expect.notEqual "product vs sum → different key" k2
            }

            test "7. longer-name-first — canonicalise avoids partial-match collisions" {
                // 'dose' is a prefix of 'dosePerKg': the longer name must be
                // substituted first so 'dose' in 'dosePerKg' is not replaced separately.
                let nmap = Map.ofList [ "dose", "x0"; "dosePerKg", "x1"; "total", "x2" ]
                let s = "total = dose + dosePerKg"
                let result = CanonKey.canonicalise nmap s
                // Expected: 'dosePerKg' → 'x1' first, then 'dose' → 'x0'
                result |> Expect.equal "no partial-match collision" "x2 = x0 + x1"
            }

            test "8. two-equation system — each equation keyed independently" {
                let eqs =
                    [
                        "dose = weight * dosePerKg"
                        "totalDose = dose * timesPerDay"
                    ]
                    |> Api.init
                    |> setValues Units.Count.times "weight"     [| 10N |]
                    |> setValues Units.Count.times "dosePerKg"  [| 10N; 15N |]
                    |> setValues Units.Count.times "timesPerDay"[| 2N; 3N |]

                let keys = eqs |> List.map CanonKey.ofEquation
                // Keys must be distinct (two different equations)
                let distinct = keys |> List.distinct
                distinct
                |> List.length
                |> Expect.equal "two-equation system has 2 distinct keys" 2
            }

        ]


// ------------------------------------------------------------------
// Property-based tests
// ------------------------------------------------------------------

let propertyTests =
    testList
        "CanonKey — FsCheck properties (1 000 cases each)"
        [

            testPropertyWithConfig fsCheckConfig "P1. idempotence — ofEquation is deterministic" <|
            fun (NonNegativeInt seed) ->
                let names = distinctNames 3 seed
                let lhs, r1, r2 = names[2], names[0], names[1]
                let eq = makeMultEq lhs r1 r2 [| 1N .. 5N |] |> List.head
                CanonKey.ofEquation eq = CanonKey.ofEquation eq

            testPropertyWithConfig fsCheckConfig "P2. name-count invariant — sortedNames = #variables" <|
            fun (NonNegativeInt seed) ->
                let names = distinctNames 3 seed
                let lhs, r1, r2 = names[2], names[0], names[1]
                let eq = makeMultEq lhs r1 r2 [| 1N .. 3N |] |> List.head
                let sorted = CanonKey.sortedNames eq
                sorted |> List.length = 3

            testPropertyWithConfig fsCheckConfig "P3. nameMap assigns a symbol to every variable" <|
            fun (NonNegativeInt seed) ->
                let names = distinctNames 3 seed
                let lhs, r1, r2 = names[2], names[0], names[1]
                let eq = makeMultEq lhs r1 r2 [| 1N .. 3N |] |> List.head
                let nmap = CanonKey.nameMap eq
                nmap |> Map.count = 3 && nmap |> Map.forall (fun _ v -> v.StartsWith "x")

            testPropertyWithConfig fsCheckConfig "P4. rename invariant — alpha-order-preserving rename keeps same key" <|
            fun (NonNegativeInt seed1) (NonNegativeInt seed2) ->
                // Build two equations with different names but identical structure + values.
                // Both name sets are sorted so alphabetical order is preserved.
                // Use modular arithmetic to keep seeds in range (avoids Int32 overflow).
                let names1 = distinctNames 3 (seed1 % 400)
                let names2 = distinctNames 3 ((seed2 % 200) + 300)
                let sortedNames1 = List.sort names1
                let sortedNames2 = List.sort names2

                let lhs1, r1a, r1b = sortedNames1[2], sortedNames1[0], sortedNames1[1]
                let lhs2, r2a, r2b = sortedNames2[2], sortedNames2[0], sortedNames2[1]

                let vals = [| 1N .. 5N |]
                let eq1 = makeMultEq lhs1 r1a r1b vals |> List.head
                let eq2 = makeMultEq lhs2 r2a r2b vals |> List.head

                CanonKey.ofEquation eq1 = CanonKey.ofEquation eq2

            testPropertyWithConfig fsCheckConfig "P5. symbols are x0..xN in name order" <|
            fun (NonNegativeInt seed) ->
                let names = distinctNames 3 seed
                let sorted = List.sort names
                let lhs, r1, r2 = sorted[2], sorted[0], sorted[1]
                let eq = makeMultEq lhs r1 r2 [| 1N .. 3N |] |> List.head
                let nmap = CanonKey.nameMap eq
                // First alphabetically → x0, second → x1, third → x2
                nmap[sorted[0]] = "x0" && nmap[sorted[1]] = "x1" && nmap[sorted[2]] = "x2"

            testPropertyWithConfig fsCheckConfig "P6. canonicalise replaces all occurrences" <|
            fun (NonNegativeInt seed) ->
                let names = distinctNames 3 seed
                let sorted = List.sort names
                let n0, n1, n2 = sorted[0], sorted[1], sorted[2]
                let nmap = Map.ofList [ n0, "x0"; n1, "x1"; n2, "x2" ]
                let s = $"{n2} = {n0} * {n1}"
                let result = CanonKey.canonicalise nmap s
                // Original names must not appear in the result
                not (result.Contains n0) && not (result.Contains n1) && not (result.Contains n2)

        ]


// ------------------------------------------------------------------
// Run all tests
// ------------------------------------------------------------------

printfn "\n=== CanonKey invariant tests ==="
printfn "8 unit tests + 6 property tests (1 000 cases each)"
printfn ""

let allTests = testList "CanonKeyInvariant" [ unitTests; propertyTests ]

runTestsWithCLIArgs [] [||] allTests
|> (fun n ->
    printfn ""
    if n = 0 then
        printfn "✅  All CanonKey invariant tests passed."
        printfn ""
        printfn "=== W2 LRU-cache roadmap ==="
        printfn "   ✅ LRUCache.fsx          — LRU cache implementation + unit tests"
        printfn "   ✅ LRUCacheProps.fsx     — 8 FsCheck property tests for LRU cache"
        printfn "   ✅ CanonKeyInvariant.fsx — 8 unit + 6 property tests for CanonKey"
        printfn "   □  LRUSolverIntegration  — integrate LRU cache into Solver.fs prototype"
    else
        printfn $"❌  {n} test(s) failed."
)
