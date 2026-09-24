namespace Informedica.GenPRES.Client.Core.Tests


/// Why a value is marked: the bound it crosses, read from what the client receives.
module SeverityReasonTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open SeverityReason


    let private vu unit (values: decimal list) : ValueUnit =
        {
            Value = values |> List.map (fun v -> string v, v) |> List.toArray
            Unit = unit
            Group = ""
            Short = true
            Language = ""
            Json = ""
        }


    let private variable min minIncl max maxIncl vals : Variable =
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


    let private defined = variable (Some(vu "mg" [ 2m ])) true (Some(vu "mg" [ 15m ])) true None

    let private values xs = variable None true None true (Some(vu "mg" xs))


    let private ovar level (var: Variable) : OrderVariable =
        {
            Name = "dose"
            DefinedConstraints = defined
            CalculatedConstraints = defined
            Variable = var
            LargeIncr = None
            Level = level
        }


    let private kindOf reason =
        reason
        |> Option.map (
            function
            | Reason.AboveMax _ -> "above"
            | Reason.BelowMin _ -> "below"
            | Reason.Outside -> "outside"
        )


    [<Tests>]
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

                            (ofConstraints exclusive (values [ 15m ])).IsSome
                            |> Expect.isTrue "on the maximum, excluded"

                            (ofConstraints exclusive (values [ 2m ])).IsSome
                            |> Expect.isTrue "on the minimum, excluded"
                        }

                        test "a range is judged by its top against the maximum and its bottom against the minimum" {
                            ofConstraints defined (values [ 5m; 20m ])
                            |> kindOf
                            |> Expect.equal "the top spills over" (Some "above")

                            ofConstraints defined (values [ 1m; 10m ])
                            |> kindOf
                            |> Expect.equal "the bottom spills under" (Some "below")
                        }

                        test "a range over both bounds is reported by its top" {
                            ofConstraints defined (values [ 1m; 20m ])
                            |> kindOf
                            |> Expect.equal "the top first" (Some "above")
                        }

                        test "values within both bounds give no reason" {
                            ofConstraints defined (values [ 5m; 10m ]) |> Expect.equal "within" None
                        }

                        test "no values give no reason" {
                            ofConstraints defined (variable None true None true None)
                            |> Expect.equal "none" None

                            ofConstraints defined (values []) |> Expect.equal "empty" None
                        }

                        test "a bound in another unit is not read" {
                            let inGrams = variable None true (Some(vu "g" [ 1m ])) true None

                            ofConstraints inGrams (values [ 20m ])
                            |> Expect.equal "mg against g: not compared" None
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
                            ovar IsNormal (values [ 20m ])
                            |> ofOrderVariable
                            |> Expect.equal "not marked" None
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
