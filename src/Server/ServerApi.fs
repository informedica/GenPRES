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
        getScenarioResult =
            fun (sc: ScenarioResult) ->

                async {
                    let msg (sc: ScenarioResult)=
                        $"""
processing:
Patient: {sc.Age} days, {sc.Weight} kg, {sc.Height} cm
Indications: {sc.Indications |> List.length}
Medications: {sc.Medications |> List.length}
Routes: {sc.Routes |> List.length}
Indication: {sc.Indication |> Option.defaultValue ""}
Medication: {sc.Medication |> Option.defaultValue ""}
Route: {sc.Route |> Option.defaultValue ""}
Scenarios: {sc.Scenarios |> List.length}
"""

                    printfn $"{msg sc}"

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
                            Height = Some 100N
                        }

                    let newSc =
                        { Demo.scenarioResult pat with
                            Indications =
                                if sc.Indication |> Option.isSome then [| sc.Indication |> Option.defaultValue "" |]
                                else
                                    sc.Indications |> List.toArray
                            Generics =
                                if sc.Medication |> Option.isSome then [| sc.Medication |> Option.defaultValue "" |]
                                else
                                sc.Medications |> List.toArray
                            Routes =
                                if sc.Route |> Option.isSome then [| sc.Route |> Option.defaultValue "" |]
                                else
                                    sc.Routes |> List.toArray
                            Indication = sc.Indication
                            Generic = sc.Medication
                            Route = sc.Route
                        }
                        |> Demo.filter

                    let sc =
                        { sc with
                            Indications = newSc.Indications |> Array.toList
                            Medications = newSc.Generics |> Array.toList
                            Routes = newSc.Routes |> Array.toList
                            Indication = newSc.Indication
                            Medication = newSc.Generic
                            Route = newSc.Route
                            Scenarios = newSc.Scenarios |> Array.toList
                        }
                    printfn $"finished:\{msg sc}"
                    return sc |> Ok
                }
    }