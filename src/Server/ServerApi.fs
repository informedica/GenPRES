[<AutoOpen>]
module ServerApiImpl

open Shared.Types
open Shared.Api


/// An implementation of the Shared IServerApi protocol.
let serverApi: IServerApi =
    {
        processMessage =
            fun msg ->
                async {
                    return msg |> Message.processMsg
                }

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

        getParenteralia =
            fun (par: Parenteralia) ->
                async {
                    return par |> Parenteralia.get
                }

        printScenarioResult =
            fun (sc : ScenarioResult) ->
                async {
                    return sc |> ScenarioResult.print |> Ok
                }

        getScenarioResult =
            fun (sc : ScenarioResult) ->
                async {
                    return sc |> ScenarioResult.get
                }

        calcMinIncrMax =
            fun (ord : Order) ->
                async {
                    return ord |> ScenarioResult.calcValues
                }

        solveOrder =
            fun (ord : Order) ->
                async {
                    return ord |> ScenarioResult.solveOrder
                }

        getIntake =
            fun wghtInGram (ords : Order []) ->
                async {
                    return ords |> ScenarioResult.getIntake wghtInGram |> Ok
                }
    }