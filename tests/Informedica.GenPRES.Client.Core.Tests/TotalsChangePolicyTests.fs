namespace Informedica.GenPRES.Client.Core.Tests


/// The items of the totals that changed since they were last shown: a changed or new value of
/// the same source, never an item that is not shown, and never totals from another source.
module TotalsChangePolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open Shared.Models


    let value s unit = [| Bold s; Normal unit |]


    let totals =
        { Totals.empty with
            Sodium = value "3" "mmol/kg/dag"
            Potassium = value "1" "mmol/kg/dag"
        }


    [<Tests>]
    let tests =
        testList
            "TotalsChangePolicy"
            [
                testList
                    "changedItems"
                    [
                        test "equal totals have no changed items" {
                            TotalsChangePolicy.changedItems totals totals
                            |> Expect.isEmpty "should be empty"
                        }

                        test "a changed value lists only that item" {
                            { totals with Sodium = value "4" "mmol/kg/dag" }
                            |> TotalsChangePolicy.changedItems totals
                            |> Expect.equal "should be natrium" [| "natrium" |]
                        }

                        test "an item that appears is listed" {
                            { totals with Volume = value "100" "ml/kg/dag" }
                            |> TotalsChangePolicy.changedItems totals
                            |> Expect.equal "should be volume" [| "volume" |]
                        }

                        test "an item that disappears is not listed" {
                            { totals with Sodium = [||] }
                            |> TotalsChangePolicy.changedItems totals
                            |> Expect.isEmpty "should be empty"
                        }

                        test "an item that is not shown is not listed" {
                            { totals with Volume = [| Normal "ml/kg/dag" |] }
                            |> TotalsChangePolicy.changedItems totals
                            |> Expect.isEmpty "should be empty"
                        }

                        test "changed items come in the order of the rows" {
                            { totals with
                                Potassium = value "2" "mmol/kg/dag"
                                Sodium = value "4" "mmol/kg/dag"
                                VitaminD = value "5" "mmol/kg/dag"
                                Phosphate = value "1" "mmol/kg/dag"
                            }
                            |> TotalsChangePolicy.changedItems totals
                            |> Expect.equal "should follow the rows" [| "natrium"; "kalium"; "fosfaat"; "vit D" |]
                        }

                        testProperty "totals never differ from themselves"
                        <| fun (t: Totals) -> TotalsChangePolicy.changedItems t t |> Array.isEmpty
                    ]

                testList
                    "observe"
                    [
                        let seen = TotalsChangePolicy.initial "plan-1" totals

                        let changed = { totals with Sodium = value "4" "mmol/kg/dag" }

                        test "the same source reports the changed items" {
                            seen
                            |> TotalsChangePolicy.observe "plan-1" changed
                            |> snd
                            |> Expect.equal "should be natrium" [| "natrium" |]
                        }

                        test "another source reports no changed items" {
                            seen
                            |> TotalsChangePolicy.observe "plan-2" changed
                            |> snd
                            |> Expect.isEmpty "should be empty"
                        }

                        test "the totals observed are the ones seen next" {
                            seen
                            |> TotalsChangePolicy.observe "plan-2" changed
                            |> fst
                            |> Expect.equal
                                "should hold the new source and totals"
                                (TotalsChangePolicy.initial "plan-2" changed)
                        }

                        test "a change is reported once" {
                            seen
                            |> TotalsChangePolicy.observe "plan-1" changed
                            |> fst
                            |> TotalsChangePolicy.observe "plan-1" changed
                            |> snd
                            |> Expect.isEmpty "should be empty"
                        }
                    ]
            ]
