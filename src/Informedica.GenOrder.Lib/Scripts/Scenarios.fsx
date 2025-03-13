// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

#time

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib
open Informedica.GenSolver.Lib.Variable.Operators

// TODO: could be used to precalc all possible
// prescriptions for a patient
let createScenarios (pr: PrescriptionResult) =
    let getRules filter =
        { pr with Filter = filter } |> PrescriptionResult.getRules

    let print pr =
        let g = pr.Filter.Generic
        let i = pr.Filter.Indication
        let r = pr.Filter.Route
        let d = pr.Filter.DoseType
        printfn $"=== no evalution of {g} {i} {r} {d} ==="
        pr

    let eval pr s =
        printfn $"evaluting {s}..."
        try
            let pr = pr |> Api.evaluate

            if pr.Scenarios |> Array.isEmpty |> not then pr
            else pr |> print
        with
        | e ->
            printfn $"\n=== ERROR\n{e}===\n"
            pr

    [
        for g in pr.Filter.Generics do
            let pr, rules = { pr.Filter with Generic = Some g } |> getRules

            if rules |> Array.isEmpty |> not then
                $"evaluting {g}..." |> eval pr

            else
                for i in pr.Filter.Indications do
                    let pr, rules = { pr.Filter with Indication = Some i } |> getRules

                    if rules |> Array.isEmpty |> not then
                        $"evaluting {g} {i}..." |> eval pr

                    else
                        for r in pr.Filter.Routes do
                            let pr, rules = { pr.Filter with Route = Some r } |> getRules

                            if rules |> Array.isEmpty |> not then
                                $"evaluting {g} {i} {r}..." |> eval pr

                            else
                                for d in pr.Filter.DoseTypes do
                                    let pr, rules = { pr.Filter with DoseType = Some d } |> getRules

                                    if rules |> Array.isEmpty |> not then
                                        $"evaluting {g} {i} {r} {d}..." |> eval pr

                                    else pr |> print
    ]



let pr = Patient.infant |> PrescriptionResult.create


{ pr with Filter = { pr.Filter with Generic = pr.Filter.Generics |> Array.tryItem 10 } }
|> PrescriptionResult.getRules

let scenarios =
    pr |> createScenarios


{ pr with
    Filter =
        { pr.Filter with
            Indication = pr.Filter.Indications |> Array.tryItem 0
            Generic = Some "tacrolimus" } }
|> PrescriptionResult.getRules
|> snd
|> Array.head
|> DrugOrder.fromRule

{ pr with
    Filter =
        { pr.Filter with
            Generic = Some "natriumfosfaat" } }
|> Api.evaluate