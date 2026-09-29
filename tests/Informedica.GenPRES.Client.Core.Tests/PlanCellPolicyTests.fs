namespace Informedica.GenPRES.Client.Core.Tests


/// Which cells of the order plan table the user can step, and which order variable a cell steps.
module PlanCellPolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open PlanCellPolicy


    let solvedVar = QuantityModePolicyTests.solvedVar
    let unsolvedVar = QuantityModePolicyTests.unsolvedVar

    /// A solved discontinuous order with one component.
    let discontinuous = QuantityModePolicyTests.order [ solvedVar ] solvedVar

    /// The order with its schedule set by f; every schedule flag is cleared first.
    let scheduled (f: Schedule -> Schedule) (ord: Order) =
        let cleared =
            { ord.Schedule with
                IsOnce = false
                IsOnceTimed = false
                IsContinuous = false
                IsDiscontinuous = false
                IsTimed = false
            }

        { ord with Schedule = f cleared }

    let timed = discontinuous |> scheduled (fun s -> { s with IsTimed = true })
    let once = discontinuous |> scheduled (fun s -> { s with IsOnce = true })
    let onceTimed = discontinuous |> scheduled (fun s -> { s with IsOnceTimed = true })
    let continuous = discontinuous |> scheduled (fun s -> { s with IsContinuous = true })

    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
    let patient = { Shared.Models.Patient.empty with Age = Some ten }

    /// A context in the plan contributing the order.
    let contextOf (ord: Order) =
        { Shared.Models.OrderContext.empty with
            Id = "c-1"
            Patient = patient
            Scenarios = [| { OrderPlanMachineTests.Fixtures.scenario "o-1" "test" with Order = ord } |]
        }

    let planOf ctx = Shared.Models.OrderPlan.create patient [| ctx |]

    /// The field a cell of the order steps, if any.
    let stepsOf (ord: Order) column =
        let ctx = contextOf ord
        stepable (planOf ctx) ctx column |> Option.map fst


    [<Tests>]
    let tests =
        testList
            "PlanCellPolicy"
            [
                testList
                    "fieldOf"
                    [
                        for name, ord, column, exp in
                            [
                                "discontinuous",
                                discontinuous,
                                Column.Frequency,
                                Some QuantityModePolicy.Field.Frequency
                                "discontinuous",
                                discontinuous,
                                Column.Solution,
                                Some QuantityModePolicy.Field.DoseQuantity
                                "timed", timed, Column.Frequency, Some QuantityModePolicy.Field.Frequency
                                "timed", timed, Column.Solution, Some QuantityModePolicy.Field.DoseQuantity
                                "once", once, Column.Frequency, None
                                "once", once, Column.Solution, Some QuantityModePolicy.Field.DoseQuantity
                                "once timed", onceTimed, Column.Frequency, None
                                "once timed", onceTimed, Column.Solution, Some QuantityModePolicy.Field.DoseQuantity
                                "continuous", continuous, Column.Frequency, Some QuantityModePolicy.Field.DoseRate
                                "continuous", continuous, Column.Solution, None
                            ] do
                            test $"the %A{column} column of a %s{name} order steps %A{exp}" {
                                fieldOf column ord |> Option.map fst |> Expect.equal "as the schedule says" exp
                            }

                        for column in [ Column.Medication; Column.Route; Column.Quantity; Column.Dose ] do
                            test $"the %A{column} column steps nothing" {
                                [ discontinuous; timed; once; onceTimed; continuous ]
                                |> List.choose (fieldOf column)
                                |> Expect.isEmpty "no step command for it"
                            }
                    ]

                testList
                    "stepable"
                    [
                        test "a solved discontinuous order steps its frequency and its dose quantity" {
                            [ Column.Frequency; Column.Solution ]
                            |> List.map (stepsOf discontinuous)
                            |> Expect.equal
                                "both open a field"
                                [
                                    Some QuantityModePolicy.Field.Frequency
                                    Some QuantityModePolicy.Field.DoseQuantity
                                ]
                        }

                        test "a solved continuous order steps its rate" {
                            stepsOf continuous Column.Frequency
                            |> Expect.equal "the rate opens a field" (Some QuantityModePolicy.Field.DoseRate)
                        }

                        test "an unsolved order steps nothing" {
                            let unsolved = QuantityModePolicyTests.order [ solvedVar ] unsolvedVar

                            [ Column.Frequency; Column.Solution ]
                            |> List.choose (stepsOf unsolved)
                            |> Expect.isEmpty "an unsolved value is not stepped"
                        }

                        test "a solved order with two components steps its dose quantity" {
                            let two = QuantityModePolicyTests.order [ solvedVar; solvedVar ] solvedVar

                            stepsOf two Column.Solution
                            |> Expect.equal "every component is solved" (Some QuantityModePolicy.Field.DoseQuantity)
                        }

                        test "an order with a component unsolved does not step its dose quantity" {
                            let two = QuantityModePolicyTests.order [ solvedVar; unsolvedVar ] solvedVar

                            stepsOf two Column.Solution
                            |> Expect.isNone "the dose quantity waits for the components"
                        }

                        test "a nutrition order steps as a drug order does" {
                            let ctx =
                                { contextOf discontinuous with
                                    Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
                                }

                            stepable (planOf ctx) ctx Column.Frequency
                            |> Option.map fst
                            |> Expect.equal "the rule applies to every row" (Some QuantityModePolicy.Field.Frequency)
                        }

                        test "a locked context steps nothing" {
                            let ctx = contextOf discontinuous

                            let moved = { planOf ctx with Patient = { patient with Department = Some "ICK" } }

                            [ Column.Frequency; Column.Solution ]
                            |> List.choose (stepable moved ctx)
                            |> Expect.isEmpty "its patient data differ from the plan's"
                        }

                        test "a context without an order steps nothing" {
                            let ctx = { contextOf discontinuous with Scenarios = [||] }

                            stepable (planOf ctx) ctx Column.Frequency |> Expect.isNone "nothing to step"
                        }
                    ]
            ]
