// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

#time

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

Product.Enteral.get ()

// TODO: could be used to precalc all possible
// prescriptions for a patient
let createScenarios (pr: PrescriptionContext) =
    let getRules filter =
        { pr with Filter = filter } |> PrescriptionContext.getRules

    let print pr =
        let g = pr.Filter.Generic
        let i = pr.Filter.Indication
        let r = pr.Filter.Route
        let d = pr.Filter.DoseType
        printfn $"=== no evaluation of {g} {i} {r} {d} ==="
        pr

    let eval pr s =
        printfn $"evaluating {s}"
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
                g |> eval pr

            else
                for i in pr.Filter.Indications do
                    let pr, rules = { pr.Filter with Indication = Some i } |> getRules

                    if rules |> Array.isEmpty |> not then
                        $"{g}, {i}" |> eval pr

                    else
                        for r in pr.Filter.Routes do
                            let pr, rules = { pr.Filter with Route = Some r } |> getRules

                            if rules |> Array.isEmpty |> not then
                                $"{g}, {i}, {r}" |> eval pr

                            else
                                for d in pr.Filter.DoseTypes do
                                    let pr, rules = { pr.Filter with DoseType = Some d } |> getRules

                                    if rules |> Array.isEmpty |> not then
                                        $"{g}, {i}, {r}, {d}" |> eval pr

                                    else pr |> print
    ]


let pr =
    { Patient.infant with
        Weight = Some ([| 10N |] |> ValueUnit.withUnit Units.Weight.kiloGram)
    }|> PrescriptionContext.create


{ pr with PrescriptionContext.Filter.Generic = pr.Filter.Generics |> Array.tryItem 10 }
|> PrescriptionContext.getRules

let scenarios =
    pr |> createScenarios |> ignore


scenarios
|> List.filter (_.Scenarios >> Array.isEmpty)
|> List.choose _.Filter.Generic
|> List.distinct
|> List.length

// cidofovir
{ pr with PrescriptionContext.Filter.Generic = Some "cidofovir" }
|> PrescriptionContext.getRules
|> fun (pr, _) ->
    { pr with PrescriptionContext.Filter.Indication = pr.Filter.Indications |> Array.tryHead }
    |> PrescriptionContext.getRules
|> fun (pr, _) ->
    { pr with PrescriptionContext.Filter.DoseType = pr.Filter.DoseTypes |> Array.tryHead }
    |> PrescriptionContext.getRules
|> snd
|> Array.tryHead
|> Option.map DrugOrder.fromRule
|> Option.defaultValue [||]
|> Array.map DrugOrder.toOrderDto
|> Array.map Order.Dto.fromDto
|> Array.map Order.applyConstraints
|> Array.iter (Order.toString >> String.concat "\n" >> printfn "%s")


// TPV
{ pr with PrescriptionContext.Filter.Indication = Some "TPV" }
|> PrescriptionContext.getRules
|> fun (pr, _) ->
    { pr with PrescriptionContext.Filter.Indication = pr.Filter.Indications |> Array.tryHead }
    |> PrescriptionContext.getRules
|> fun (pr, _) ->
    { pr with PrescriptionContext.Filter.DoseType = pr.Filter.DoseTypes |> Array.tryHead }
    |> PrescriptionContext.getRules
|> fun (pr, _) ->
    { pr with PrescriptionContext.Filter.SelectedComponents = pr.Filter.Components |> Array.skip 1 }
    |> PrescriptionContext.getRules
|> snd
|> Array.tryHead
|> Option.map DrugOrder.fromRule
|> Option.defaultValue [||]
|> Array.map DrugOrder.toOrderDto
|> Array.map Order.Dto.fromDto
|> Array.map Order.applyConstraints
|> Array.iter (Order.toString >> String.concat "\n" >> printfn "%s")


{ pr with
    PrescriptionContext.Filter.Generic = Some "natriumfosfaat" }
|> Api.evaluate


let tpvRule =
    { pr with PrescriptionContext.Filter.Indication = Some "TPV" }
    |> PrescriptionContext.getRules
    |> fun (pr, _) ->
        { pr with PrescriptionContext.Filter.Indication = pr.Filter.Indications |> Array.tryHead }
        |> PrescriptionContext.getRules
    |> fun (pr, _) ->
        { pr with PrescriptionContext.Filter.DoseType = pr.Filter.DoseTypes |> Array.tryHead }
        |> PrescriptionContext.getRules
    |> snd
    |> Array.head


tpvRule.DoseRule.DoseLimits
|> Array.map _.Component

let norRule =
    { pr with PrescriptionContext.Filter.Generic = Some "noradrenaline" }
    |> PrescriptionContext.getRules
    |> fun (pr, _) ->
        { pr with PrescriptionContext.Filter.Indication = pr.Filter.Indications |> Array.tryHead }
        |> PrescriptionContext.getRules
    |> fun (pr, _) ->
        { pr with PrescriptionContext.Filter.DoseType = pr.Filter.DoseTypes |> Array.tryHead }
        |> PrescriptionContext.getRules
    |> snd
    |> Array.head

norRule.DoseRule.DoseLimits
|> Array.map _.Component