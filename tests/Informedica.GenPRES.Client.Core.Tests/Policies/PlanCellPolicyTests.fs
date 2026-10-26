namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// Which cells of the order plan table the user can step, and which order variable a cell steps.
module PlanCellPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.Tests

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types
    open PlanCellPolicy


    let solvedVar = QuantityModePolicyTests.solvedVar
    let unsolvedVar = QuantityModePolicyTests.unsolvedVar

    /// A solved order variable holding one value of its own, so a mapping to the wrong variable
    /// shows.
    let solvedAt (v: decimal) =
        QuantityModePolicyTests.variable None None None (Some [| v |])
        |> QuantityModePolicyTests.withIncr

    let frequency = solvedAt 3m
    let doseQuantity = solvedAt 5m
    let doseRate = solvedAt 7m
    let orderableQuantity = solvedAt 11m

    /// The order with the variables the columns show set apart.
    let distinct (ord: Order) =
        { ord with
            Order.Schedule.Frequency = frequency
            Order.Orderable.Dose.Quantity = doseQuantity
            Order.Orderable.Dose.Rate = doseRate
            Order.Orderable.OrderableQuantity = orderableQuantity
        }

    /// A solved discontinuous order with one component.
    let discontinuous = QuantityModePolicyTests.order [ solvedVar ] solvedVar |> distinct

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

    let ten = { Informedica.GenPRES.Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
    let patient = { Informedica.GenPRES.Shared.Models.Patient.empty with Age = Some ten }

    /// A context in the plan contributing the order.
    let contextOf (ord: Order) =
        { Informedica.GenPRES.Shared.Models.OrderContext.empty with
            Id = "c-1"
            Patient = patient
            Scenarios = [| { OrderFixtures.scenario "o-1" "test" with Order = ord } |]
        }

    let planOf ctx =
        Informedica.GenPRES.Shared.Models.OrderPlan.create patient [| ctx |]

    /// The field and the order variable a cell of the order steps, if any.
    let stepsOf (ord: Order) column =
        let ctx = contextOf ord
        stepable (planOf ctx) ctx column

    let steppingFrequency = Some(QuantityModePolicy.Field.Frequency, frequency)
    let steppingDoseQuantity = Some(QuantityModePolicy.Field.DoseQuantity, doseQuantity)
    let steppingDoseRate = Some(QuantityModePolicy.Field.DoseRate, doseRate)


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
                                "discontinuous", discontinuous, Column.Frequency, steppingFrequency
                                "discontinuous", discontinuous, Column.Solution, steppingDoseQuantity
                                "timed", timed, Column.Frequency, steppingFrequency
                                "timed", timed, Column.Solution, steppingDoseQuantity
                                "once", once, Column.Frequency, None
                                "once", once, Column.Solution, steppingDoseQuantity
                                "once timed", onceTimed, Column.Frequency, None
                                "once timed", onceTimed, Column.Solution, steppingDoseQuantity
                                "continuous", continuous, Column.Frequency, steppingDoseRate
                                "continuous", continuous, Column.Solution, None
                            ] do
                            test $"the %A{column} column of a %s{name} order steps %A{exp |> Option.map fst}" {
                                fieldOf column ord |> Expect.equal "the field and its own variable" exp
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
                            |> Expect.equal "both open a field" [ steppingFrequency; steppingDoseQuantity ]
                        }

                        test "a solved continuous order steps its rate" {
                            stepsOf continuous Column.Frequency
                            |> Expect.equal "the rate opens a field" steppingDoseRate
                        }

                        test "an unsolved order steps nothing" {
                            let unsolved = { discontinuous with Order.Orderable.Dose.Quantity = unsolvedVar }

                            [ Column.Frequency; Column.Solution ]
                            |> List.choose (stepsOf unsolved)
                            |> Expect.isEmpty "an unsolved value is not stepped"
                        }

                        test "a solved order with two components steps its dose quantity" {
                            let two = QuantityModePolicyTests.order [ solvedVar; solvedVar ] solvedVar |> distinct

                            stepsOf two Column.Solution
                            |> Expect.equal "every component is solved" steppingDoseQuantity
                        }

                        test "an order with a component unsolved does not step its dose quantity" {
                            let two = QuantityModePolicyTests.order [ solvedVar; unsolvedVar ] solvedVar |> distinct

                            stepsOf two Column.Solution
                            |> Expect.isNone "the dose quantity waits for the components"
                        }

                        test "a nutrition order steps as a drug order does" {
                            let ctx =
                                { contextOf discontinuous with
                                    Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
                                }

                            stepable (planOf ctx) ctx Column.Frequency
                            |> Expect.equal "the rule applies to every row" steppingFrequency
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
