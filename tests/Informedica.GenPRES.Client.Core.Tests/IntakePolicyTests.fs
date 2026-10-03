namespace Informedica.GenPRES.Client.Core.Tests


/// What the TPN intake slider shows, and when the slot moves the dose to the whole orderable.
module IntakePolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types


    let one v =
        QuantityModePolicyTests.variable None None None (Some [| v |])
        |> QuantityModePolicyTests.withIncr


    let range = QuantityModePolicyTests.range |> QuantityModePolicyTests.withIncr


    /// A TPN order: its component orderable quantities, total, dose count and dose quantity.
    let tpn cmpQtys total doseCount doseQty =
        let ord = QuantityModePolicyTests.order cmpQtys doseQty

        { ord with
            Orderable =
                { ord.Orderable with
                    OrderableQuantity = total
                    DoseCount = doseCount
                }
        }


    let composed = tpn [ one 100m; one 900m ] (one 1000m) range range
    let notComposed = tpn [ one 100m; range ] range range range
    let started = tpn [ one 100m; one 900m ] (one 1000m) (one 1m) (one 1000m)


    [<Tests>]
    let tests =
        testList
            "IntakePolicy"
            [
                testList
                    "doseShare"
                    [
                        test "the dose quantity as a percentage of the total" {
                            tpn [ one 100m; one 900m ] (one 1000m) (one 2m) (one 500m)
                            |> IntakePolicy.doseShare
                            |> Expect.equal "should be 50%" (Some 50m)
                        }

                        test "no share while the dose quantity is a range" {
                            composed |> IntakePolicy.doseShare |> Expect.isNone "should be none"
                        }
                    ]

                testList
                    "sliderStep"
                    [
                        for share, exp in [ 100m, Some 100; 10m, Some 10; 50.4m, Some 50; 14m, None; 5m, None ] do
                            test $"%M{share}%% is step %A{exp}" {
                                share |> IntakePolicy.sliderStep |> Expect.equal "should be the step" exp
                            }
                    ]

                testList
                    "canSetDoseQuantityPerc"
                    [
                        test "a composed TPN with one dose count can set the dose" {
                            started |> IntakePolicy.canSetDoseQuantityPerc |> Expect.isTrue "should be true"
                        }

                        test "a composed TPN with a dose count range cannot" {
                            composed
                            |> IntakePolicy.canSetDoseQuantityPerc
                            |> Expect.isFalse "should be false"
                        }

                        test "a TPN that is not composed cannot" {
                            notComposed
                            |> IntakePolicy.canSetDoseQuantityPerc
                            |> Expect.isFalse "should be false"
                        }
                    ]

                testList
                    "isCompositionLocked"
                    [
                        test "a dose that is a part of the total locks the composition" {
                            tpn [ one 100m; one 900m ] (one 1000m) (one 2m) (one 500m)
                            |> IntakePolicy.isCompositionLocked
                            |> Expect.isTrue "should be locked"
                        }

                        test "a dose of the whole total does not" {
                            started
                            |> IntakePolicy.isCompositionLocked
                            |> Expect.isFalse "should not be locked"
                        }
                    ]

                testList
                    "start"
                    [
                        test "a composed TPN with a dose count range sends the move" {
                            composed
                            |> IntakePolicy.start None
                            |> Expect.equal
                                "should send"
                                (IntakePolicy.Start.Send(composed.Id, composed |> IntakePolicy.composition))
                        }

                        test "the move is sent once for an order and its composition" {
                            composed
                            |> IntakePolicy.start (Some(composed.Id, composed |> IntakePolicy.composition))
                            |> Expect.equal "should wait" IntakePolicy.Start.Wait
                        }

                        test "another order with the same composition sends the move again" {
                            let other = { composed with Id = "other" }

                            other
                            |> IntakePolicy.start (Some(composed.Id, composed |> IntakePolicy.composition))
                            |> Expect.equal
                                "should send"
                                (IntakePolicy.Start.Send(other.Id, other |> IntakePolicy.composition))
                        }

                        test "a dose count with one value needs no move" {
                            started
                            |> IntakePolicy.start None
                            |> Expect.equal "should wait" IntakePolicy.Start.Wait
                        }

                        test "a TPN that is not composed clears the order the move was sent for" {
                            notComposed
                            |> IntakePolicy.start (Some(composed.Id, composed |> IntakePolicy.composition))
                            |> Expect.equal "should clear" IntakePolicy.Start.Clear
                        }
                    ]
            ]
