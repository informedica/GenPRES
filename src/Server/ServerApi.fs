[<AutoOpen>]
module ServerApiImpl


open Informedica.GenOrder.Lib

open Shared.Types
open Shared.Api



/// An implementation of the Shared IServerApi protocol.
let serverApi: IServerApi =
    {
        getScenarioResult =
            fun (sc: ScenarioResult) ->
                printfn $"processing: {sc}"

                async {
                    return
                        { sc with
                            Indications =
                                Demo.filterIndications sc.Medication sc.Route
                            Medications =
                                Demo.filterMedications sc.Indication sc.Route
                            Routes =
                                Demo.filterRoutes sc.Indication sc.Medication
                            Scenarios =
                                match sc.Weight, sc.Indication with
                                | Some w, Some ind ->
                                    Demo.create
                                        w
                                        (Some ind)
                                        sc.Medication
                                        sc.Route
                                | _ -> []
                        }
                        |> Ok
                }
    }