/// The order context path on the domain-typed port: the contract model parsed in, the verb
/// mapped, the port's plan context mapped out with the environment's demo flag.
module Informedica.GenPRES.Server.Tests.OrderContextSwitchTests

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


let patient = StubPatientData.patient

let ctx: OrderContext =
    { Shared.Models.OrderContext.empty with
        Id = "c-1"
        Category = OrderCategory.Nutrition NutritionCategory.TPN
        DemoVersion = true
        Filter = { Shared.Models.OrderContext.filter with Generic = Some "glucose" }
        Patient = patient
        Intake = { Shared.Models.Totals.empty with Volume = [| Normal "10 ml" |] }
    }

let echo: OrderContextPort = { evaluate = fun _ pc -> async { return Ok pc } }

let envOver demo (port: OrderContextPort) =
    { makeEnv (formularyAlwaysOk Shared.Models.Formulary.empty) port with demo = demo }


[<Tests>]
let tests =
    testList
        "the order context switch-over"
        [
            testAsync "the answer is the port's plan context as the contract model, the demo flag the environment's" {
                let! answer =
                    OrderContextCommand.processCmd
                        (envOver false echo)
                        (Shared.Api.OrderContextCommand.UpdateOrderContext, ctx)

                match answer with
                | Ok a ->
                    a
                    |> Expect.equal "the context as sent, but for the demo flag" { ctx with DemoVersion = false }
                | Error e -> failtest $"refused: %A{e}"

                let! demo =
                    OrderContextCommand.processCmd
                        (envOver true echo)
                        (Shared.Api.OrderContextCommand.UpdateOrderContext, ctx)

                demo
                |> Result.map _.DemoVersion
                |> Expect.equal "demo as the environment says" (Ok true)
            }

            testAsync "the port receives the verb over the parsed context, id and category included" {
                let seen = ref None

                let port: OrderContextPort =
                    {
                        evaluate =
                            fun cmd pc ->
                                seen.Value <- Some(cmd pc.Context, pc.Id, pc.Category)
                                async { return Ok pc }
                    }

                let! _ =
                    OrderContextCommand.processCmd
                        (envOver false port)
                        (Shared.Api.OrderContextCommand.SelectOrderScenario, ctx)

                match seen.Value with
                | Some(OrderContext.SelectOrderScenario domainCtx, id, category) ->
                    domainCtx.Filter.Generic |> Expect.equal "the context parsed" (Some "glucose")
                    id |> Expect.equal "the plan's id" "c-1"

                    category
                    |> Expect.equal
                        "the plan's category"
                        (Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition
                            Informedica.GenOrder.Lib.Types.NutritionCategory.TPN)
                | other -> failtest $"the port saw %A{other}"
            }

            testAsync "a draft that is no patient is refused before the port, with the server's words" {
                let asked = ref false

                let port: OrderContextPort =
                    {
                        evaluate =
                            fun _ pc ->
                                asked.Value <- true
                                async { return Ok pc }
                    }

                let! answer =
                    OrderContextCommand.processCmd
                        (envOver false port)
                        (Shared.Api.OrderContextCommand.UpdateOrderContext,
                         { ctx with Patient = Shared.Models.Patient.empty })

                answer |> Expect.equal "no patient" (Error [| Patient.noPatient |])
                asked.Value |> Expect.isFalse "the port never asked"
            }

            testAsync "the port's refusal is the answer" {
                let port: OrderContextPort = { evaluate = fun _ _ -> async { return Error [| "ctx error" |] } }

                let! answer =
                    OrderContextCommand.processCmd
                        (envOver false port)
                        (Shared.Api.OrderContextCommand.UpdateOrderContext, ctx)

                answer |> Expect.equal "propagated" (Error [| "ctx error" |])
            }

            test "the words for a Dto that is no context" {
                DtoError.Patient PatientError.NoAgeOrMeasuredWeightAndHeight
                |> OrderContextMapper.words
                |> Expect.equal "the patient's" Patient.noPatient

                DtoError.OrderNotCreated "x"
                |> OrderContextMapper.words
                |> Expect.stringContains "the order's" "x"
            }
        ]
