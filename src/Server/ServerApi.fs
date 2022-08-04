[<AutoOpen>]
module ServerApiImpl


open Informedica.Utils.Lib.BCL
open Informedica.GenOrder.Lib


open Shared.Types
open Shared.Api


module Demo =


    let toString (sc : Informedica.GenOrder.Lib.Types.Scenario) =
        $"""
- #### {sc.No}. {sc.Name} {sc.Shape} {sc.Route}
- *Voorschrift*: {sc.Prescription}
- *Bereiding*: {sc.Preparation}
- *Toediening*: {sc.Administration |> String.replace "hr" "uur"} {sc.Route}

"""

    let create w ind med rte =
        Demo.filter ind med rte
        |> List.collect (fun (_, m, r, d, ns) ->
            try
                r
                |> Demo.mapRoute
                |> Examples.getOrders m 
                |> List.collect (Examples.calculate w ns d)
                |> List.map (fun sc -> { sc with Route = r })
            with
            | _ -> []
        )
        // correct for roundig problem. admin should not be rounded!
        |> List.distinctBy (fun sc -> sc.Route, sc.Administration)
        |> List.map Demo.translate
        |> List.mapi (fun i sc -> { sc with No = i + 1 })
        |> List.map toString


/// An implementation of the Shared IServerApi protocol.
let serverApi: IServerApi =
    {
        getScenarioResult =
            fun (sc: ScenarioResult) ->

                async {
                    printfn $"processing: {sc}"

                    let inds = Demo.filterIndications sc.Medication sc.Route |> List.sort
                    let meds = Demo.filterMedications sc.Indication sc.Route |> List.sort
                    let rtes = Demo.filterRoutes sc.Indication sc.Medication |> List.sort

                    let someIfOne a = function
                        | [x] when a |> Option.isNone -> Some x
                        | _ -> a

                    let sc = 
                            { sc with
                                Indications = inds
                                Medications = meds
                                Routes = rtes
                                Indication = inds |> someIfOne sc.Indication
                                Medication = meds |> someIfOne sc.Medication
                                Route = rtes |> someIfOne sc.Route
                            }
                            |> fun sc ->
                                { sc with
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


                    return sc |> Ok
                }
    }