module Informedica.GenPRES.Shared.Tests.ApiTests

open Informedica.GenPRES.Shared.Api
open Expecto
open Expecto.Flip


[<Tests>]
let tests =
    testList
        "Api"
        [
            testList
                "OrderViewCommand"
                [
                    testList
                        "offersLinked"
                        [
                            test
                                "a change of the choices or the scenario offers the linked orders again, a value step does not" {
                                [
                                    OrderViewCommand.ClearAllFilterProperty
                                    OrderViewCommand.SelectNthOrderScenario 0
                                    OrderViewCommand.SetNthDiluentProperty 0
                                    OrderViewCommand.ResetOrderScenario
                                ]
                                |> List.map OrderViewCommand.offersLinked
                                |> Expect.equal "filter and scenario only" [ true; true; false; false ]
                            }
                        ]
                ]
        ]
