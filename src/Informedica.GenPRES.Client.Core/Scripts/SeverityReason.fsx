// Why a value is marked: the bound it crosses (plan 981, step C4).
//
// The server marks an order variable with a level when its values are not within the
// constraints the rules define, and sends the level with no reason. The client already
// receives the defined constraints beside the values, so the reason can be read there: which
// bound the value crosses, and what that bound is. That is what a mark can say on hover
// (#402), without a popup and without a new server field.
//
// Script-first draft (script-only policy: Client.Core is not the client UI) of the reading, →
// `Client.Core/SeverityReason.fs`, with its tests → `Client.Core.Tests/SeverityReasonTests.fs`.
// The mark of step C4, `Components/SeverityMark.fs`, shows what it answers.
//
// Run: `dotnet fsi SeverityReason.fsx` from this directory, or via the FSI MCP after
// `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../../Informedica.GenPRES.Shared/Types.fs"

open Shared.Types


// ---------------------------------------------------------------------------------------------
// → Client.Core/SeverityReason.fs
// ---------------------------------------------------------------------------------------------

/// Why a value is marked, read from the bounds the rules define and the values the variable
/// has, both of which the client receives with every order variable. Pure F#, no React, so it
/// runs under Expecto; the mark shows what it answers.
module SeverityReason =

    /// A bound a value crossed: what it is, in what unit, and whether the bound itself is
    /// still allowed.
    type Bound =
        {
            Value: decimal
            Unit: string
            Inclusive: bool
        }


    /// What a value crossed: the maximum the rules define, their minimum, or something the
    /// bounds do not show, such as an increment the value is not a multiple of.
    [<RequireQualifiedAccess>]
    type Reason =
        | AboveMax of Bound
        | BelowMin of Bound
        | Outside


    let private bound inclusive (vu: ValueUnit) (pick: (string * decimal)[] -> decimal) =
        match vu.Value with
        | [||] -> None
        | values ->
            Some
                {
                    Value = values |> pick
                    Unit = vu.Unit
                    Inclusive = inclusive
                }


    let private highest (values: (string * decimal)[]) = values |> Array.maxBy snd |> snd
    let private lowest (values: (string * decimal)[]) = values |> Array.minBy snd |> snd


    /// Why the variable's values stand outside the constraints defined for it, or nothing
    /// when the client cannot tell: no values, no bound in the values' unit, or values within
    /// both bounds. The highest value against the maximum first, then the lowest against the
    /// minimum, so a range that spills over both is reported by its top.
    let ofConstraints (defined: Variable) (var: Variable) =
        match var.Vals with
        | None -> None
        | Some vals when vals.Value |> Array.isEmpty -> None
        | Some vals ->
            let sameUnit (b: ValueUnit) = b.Unit = vals.Unit

            let max =
                defined.Max
                |> Option.filter sameUnit
                |> Option.bind (fun m -> bound defined.MaxIncl m lowest)

            let min =
                defined.Min
                |> Option.filter sameUnit
                |> Option.bind (fun m -> bound defined.MinIncl m highest)

            let top = vals.Value |> highest
            let bottom = vals.Value |> lowest

            let above (b: Bound) = if b.Inclusive then top > b.Value else top >= b.Value
            let below (b: Bound) = if b.Inclusive then bottom < b.Value else bottom <= b.Value

            match max, min with
            | Some b, _ when above b -> Some(Reason.AboveMax b)
            | _, Some b when below b -> Some(Reason.BelowMin b)
            | _ -> None


    /// The reason for a marked order variable: read from its defined constraints when they
    /// explain the mark, and Outside when the server marked it and the bounds do not say why.
    let ofOrderVariable (ovar: OrderVariable) =
        match ovar.Level with
        | IsNormal -> None
        | IsCaution
        | IsWarning
        | IsAlert ->
            ofConstraints ovar.DefinedConstraints ovar.Variable
            |> Option.orElse (Some Reason.Outside)


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open SeverityReason

let vu unit (values: decimal list) : ValueUnit =
    {
        Value = values |> List.map (fun v -> string v, v) |> List.toArray
        Unit = unit
        Group = ""
        Short = true
        Language = ""
        Json = ""
    }

let variable min minIncl max maxIncl vals : Variable =
    {
        Name = "dose"
        IsNonZeroPositive = true
        Min = min
        MinIncl = minIncl
        Incr = None
        Max = max
        MaxIncl = maxIncl
        Vals = vals
    }

let defined = variable (Some(vu "mg" [ 2m ])) true (Some(vu "mg" [ 15m ])) true None
let values xs = variable None true None true (Some(vu "mg" xs))

let ovar level (var: Variable) : OrderVariable =
    {
        Name = "dose"
        DefinedConstraints = defined
        CalculatedConstraints = defined
        Variable = var
        LargeIncr = None
        Level = level
    }

let tests =
    testList
        "SeverityReason"
        [
            testList
                "ofConstraints"
                [
                    test "a value above the maximum names the maximum" {
                        ofConstraints defined (values [ 20m ])
                        |> Expect.equal
                            "above 15 mg"
                            (Some(
                                Reason.AboveMax
                                    {
                                        Value = 15m
                                        Unit = "mg"
                                        Inclusive = true
                                    }
                            ))
                    }

                    test "a value below the minimum names the minimum" {
                        ofConstraints defined (values [ 1m ])
                        |> Expect.equal
                            "below 2 mg"
                            (Some(
                                Reason.BelowMin
                                    {
                                        Value = 2m
                                        Unit = "mg"
                                        Inclusive = true
                                    }
                            ))
                    }

                    test "a value on an inclusive bound is within" {
                        ofConstraints defined (values [ 15m ]) |> Expect.equal "on the maximum" None
                        ofConstraints defined (values [ 2m ]) |> Expect.equal "on the minimum" None
                    }

                    test "a value on an exclusive bound is outside it" {
                        let exclusive = variable (Some(vu "mg" [ 2m ])) false (Some(vu "mg" [ 15m ])) false None

                        (ofConstraints exclusive (values [ 15m ])).IsSome |> Expect.isTrue "on the maximum, excluded"
                        (ofConstraints exclusive (values [ 2m ])).IsSome |> Expect.isTrue "on the minimum, excluded"
                    }

                    test "a range is judged by its top against the maximum and its bottom against the minimum" {
                        ofConstraints defined (values [ 5m; 20m ])
                        |> Option.map (
                            function
                            | Reason.AboveMax _ -> "above"
                            | _ -> "other"
                        )
                        |> Expect.equal "the top spills over" (Some "above")

                        ofConstraints defined (values [ 1m; 10m ])
                        |> Option.map (
                            function
                            | Reason.BelowMin _ -> "below"
                            | _ -> "other"
                        )
                        |> Expect.equal "the bottom spills under" (Some "below")
                    }

                    test "a range over both bounds is reported by its top" {
                        ofConstraints defined (values [ 1m; 20m ])
                        |> Option.map (
                            function
                            | Reason.AboveMax _ -> "above"
                            | _ -> "other"
                        )
                        |> Expect.equal "the top first" (Some "above")
                    }

                    test "values within both bounds give no reason" {
                        ofConstraints defined (values [ 5m; 10m ]) |> Expect.equal "within" None
                    }

                    test "no values give no reason" {
                        ofConstraints defined (variable None true None true None) |> Expect.equal "none" None

                        ofConstraints defined (values []) |> Expect.equal "empty" None
                    }

                    test "a bound in another unit is not read" {
                        let inGrams = variable None true (Some(vu "g" [ 1m ])) true None
                        ofConstraints inGrams (values [ 20m ]) |> Expect.equal "mg against g: not compared" None
                    }

                    test "no bounds give no reason" {
                        ofConstraints (variable None true None true None) (values [ 20m ])
                        |> Expect.equal "nothing to cross" None
                    }
                ]

            testList
                "ofOrderVariable"
                [
                    test "a normal variable has no reason, whatever its values" {
                        ovar IsNormal (values [ 20m ]) |> ofOrderVariable |> Expect.equal "not marked" None
                    }

                    test "a marked variable above its maximum names it" {
                        ovar IsWarning (values [ 20m ])
                        |> ofOrderVariable
                        |> Option.map (
                            function
                            | Reason.AboveMax b -> $"max {b.Value} {b.Unit}"
                            | _ -> "other"
                        )
                        |> Expect.equal "the maximum" (Some "max 15 mg")
                    }

                    test "a marked variable the bounds do not explain is outside" {
                        ovar IsAlert (values [ 10m ])
                        |> ofOrderVariable
                        |> Expect.equal "marked, within the bounds shown" (Some Reason.Outside)
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
