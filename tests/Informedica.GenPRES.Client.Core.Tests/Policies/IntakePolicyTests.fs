namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// What the TPN intake slider shows, and when the slot moves the dose to the whole orderable.
module IntakePolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types


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

                        test "the slider of a locked order cannot set the dose, by itself or by its group" {
                            let scenario =
                                { Informedica.GenPRES.Client.Core.Tests.OrderFixtures.scenario "o-1" "tpn" with
                                    Order = started
                                }

                            // a TPN with an electrolyte line linked to it, each calculated or not
                            let ctx id linkedTo marked =
                                { Informedica.GenPRES.Shared.Models.OrderContext.empty with
                                    Id = id
                                    Category = OrderCategory.Nutrition NutritionCategory.TPN
                                    LinkedTo = linkedTo
                                    Scenarios = [| { scenario with NotRecalculated = marked } |]
                                }

                            let main = ctx "c-1" None
                            let linked = ctx "c-2" (Some "c-1")

                            let planOf contexts =
                                Informedica.GenPRES.Shared.Models.OrderPlan.create
                                    Informedica.GenPRES.Shared.Models.Patient.empty
                                    contexts

                            main false
                            |> PlanContextPolicy.canSetDoseShare (planOf [| main false; linked false |])
                            |> Expect.isTrue "not locked"

                            main true
                            |> PlanContextPolicy.canSetDoseShare (planOf [| main true; linked false |])
                            |> Expect.isFalse "locked by itself"

                            main false
                            |> PlanContextPolicy.canSetDoseShare (planOf [| main false; linked true |])
                            |> Expect.isFalse "locked by its group"
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


    /// An order of the category given in a plan of one, calculated or not.
    let planOf category order marked =
        let scenario =
            { Informedica.GenPRES.Client.Core.Tests.OrderFixtures.scenario "o-1" "tpn" with
                Order = order
                NotRecalculated = marked
            }

        let ctx =
            { Informedica.GenPRES.Shared.Models.OrderContext.empty with
                Id = "c-1"
                Category = category
                Scenarios = [| scenario |]
            }

        Informedica.GenPRES.Shared.Models.OrderPlan.create Informedica.GenPRES.Shared.Models.Patient.empty [| ctx |]


    /// A TPN in a plan of one, calculated or not.
    let tpnPlan = planOf (OrderCategory.Nutrition NutritionCategory.TPN)


    [<Tests>]
    let doseShareTests =
        testList
            "the dose share of the order dialog"
            [
                test "a started TPN in the plan has its dose share settable" {
                    PlanContextPolicy.editingOf (tpnPlan started false) "c-1"
                    |> Expect.equal
                        "settable"
                        (PlanContextPolicy.Editing.PlanContext PlanContextPolicy.DoseShare.Settable)
                }

                test "a medication of the same shape, such as one paracetamol tablet, has it fixed" {
                    PlanContextPolicy.editingOf (planOf OrderCategory.Medication started false) "c-1"
                    |> Expect.equal "fixed" (PlanContextPolicy.Editing.PlanContext PlanContextPolicy.DoseShare.Fixed)
                }

                test "a TPN not composed has its dose share fixed" {
                    PlanContextPolicy.editingOf (tpnPlan notComposed false) "c-1"
                    |> Expect.equal "fixed" (PlanContextPolicy.Editing.PlanContext PlanContextPolicy.DoseShare.Fixed)
                }

                test "a TPN not calculated is locked, and only a settable share is set" {
                    PlanContextPolicy.editingOf (tpnPlan started true) "c-1"
                    |> Expect.equal "locked" PlanContextPolicy.Editing.Locked

                    [
                        PlanContextPolicy.Editing.Workbench
                        PlanContextPolicy.Editing.PlanContext PlanContextPolicy.DoseShare.Settable
                        PlanContextPolicy.Editing.PlanContext PlanContextPolicy.DoseShare.Fixed
                        PlanContextPolicy.Editing.Locked
                    ]
                    |> List.map PlanContextPolicy.setsDoseShare
                    |> Expect.equal "the settable share only" [ false; true; false; false ]
                }
            ]
