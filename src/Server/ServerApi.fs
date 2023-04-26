[<AutoOpen>]
module ServerApiImpl

open Shared.Types
open Shared.Api


/// An implementation of the Shared IServerApi protocol.
let serverApi: IServerApi =
    {
        test =
            fun () ->
                async {
                    return "Hello world!"
                }

        getFormulary =
            fun (form : Formulary) ->
                async {
                    return form |> Formulary.get
                }

        calcMinIncrMax =
            fun (ord : Order) ->
                async {
                    return ord |> ScenarioResult.calcMinIncrMaxToValues
                }

        getScenarioResult =
            fun (sc : ScenarioResult) ->
                async {
                    return sc |> ScenarioResult.get
                }

    }