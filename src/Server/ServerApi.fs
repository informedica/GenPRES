[<AutoOpen>]
module ServerApiImpl

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenOrder.Lib


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

        getScenarioResult =
            fun (sc: ScenarioResult) ->

                async {
                    let msg stage (sc: ScenarioResult)=
                        $"""
{stage}:
Patient: {sc.Age} days, {sc.Weight} kg, {sc.Height} cm
Indications: {sc.Indications |> Array.length}
Medications: {sc.Medications |> Array.length}
Routes: {sc.Routes |> Array.length}
Indication: {sc.Indication |> Option.defaultValue ""}
Medication: {sc.Medication |> Option.defaultValue ""}
Route: {sc.Route |> Option.defaultValue ""}
Scenarios: {sc.Scenarios |> Array.length}
"""

                    printfn $"""{msg "processing" sc}"""

                    let pat =
                        { Patient.patient with
                            Department = "ICK"
                            Age =
                                sc.Age
                                |> Option.bind BigRational.fromFloat
                            Weight =
                                sc.Weight
                                |> Option.map (fun w -> w * 1000.)
                                |> Option.bind BigRational.fromFloat
                            Height =
                                sc.Height
                                |> Option.bind BigRational.fromFloat
                        }

                    try
                        let newSc =
                            let r = Demo.scenarioResult pat
                            { Demo.scenarioResult pat with
                                Indications =
                                    if sc.Indication |> Option.isSome then
                                        [| sc.Indication |> Option.defaultValue "" |]
                                    else
                                        r.Indications
                                Generics =
                                    if sc.Medication |> Option.isSome then
                                        [| sc.Medication |> Option.defaultValue "" |]
                                    else
                                        r.Generics
                                Shapes = 
                                    if sc.Shape |> Option.isSome then
                                        [| sc.Shape |> Option.defaultValue "" |]
                                    else
                                        r.Shapes
                                Routes =
                                    if sc.Route |> Option.isSome then
                                        [| sc.Route |> Option.defaultValue "" |]
                                    else
                                        r.Routes
                                Indication = sc.Indication
                                Generic = sc.Medication
                                Shape = sc.Shape
                                Route = sc.Route
                            }
                            |> Demo.filter

                        let sc =
                            { sc with
                                Indications = newSc.Indications 
                                Medications = newSc.Generics 
                                Routes = newSc.Routes 
                                Indication = newSc.Indication
                                Medication = newSc.Generic
                                Shape = newSc.Shape
                                Route = newSc.Route
                                Scenarios = 
                                    newSc.Scenarios
                                    |> Array.map (fun sc ->
                                        Shared.ScenarioResult.createScenario sc.Shape sc.Prescription sc.Preparation sc.Administration
                                    )
                            }
                        printfn $"""{msg "finished" sc}"""
                        let s =
                            sc.Scenarios
                            |> Array.collect (fun sc -> [| sc.Prescription; sc.Preparation; sc.Administration |])
                            |> String.concat "\n"
                        printfn $"{s}"
                        return sc |> Ok
                    with
                    | e ->
                        printfn $"errored:\n{e}"
                        return sc |> Ok
                }
    }