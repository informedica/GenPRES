// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

#time

open Informedica.Utils.Lib.BCL
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

    let pr = eval pr ""

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


open Patient.Optics


let checkCtx msg ctx =
    printfn $"\n\n=== {msg |> String.capitalize} ===\n"
    ctx |> PrescriptionContext.changeDiluent |> printfn "changeDiluent: %b"
    ctx |> PrescriptionContext.changeComponents |> printfn "changeComponents: %b"
    ctx.Scenarios |> Array.length |> printfn "scenarios: %i"
    ctx.Scenarios
    |> Array.tryExactlyOne
    |> Option.map _.Order
    |> Option.iter (fun o ->
            o |> Order.toString |> String.concat "\n" |> printfn "%s\n\n"
            o |> Order.doseIsSolved |> printfn "order is solved: %b"
            o |> Order.doseHasValues |> printfn "order has values: %b"

        )
    printfn $"\n===\n"

    ctx


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> PrescriptionContext.create
|> PrescriptionContext.setFilterIndication "Ernstige infectie, gram negatieve microorganismen"
|> PrescriptionContext.setFilterGeneric "gentamicine"
|> PrescriptionContext.setFilterShape "injectievloeistof"
|> PrescriptionContext.setFilterRoute "intraveneus"
|> checkCtx "inital setup"
|> Api.evaluate
|> checkCtx "first evaluation" //|> ignore
|> Api.evaluate //|> ignore
|> checkCtx "second evaluation"
|> fun ctx ->
    let ctx =
        { ctx with
            Scenarios =
                ctx.Scenarios
                |> Array.map (fun s ->
                    { s with
                        Order =
                            let dto = s.Order |> Order.Dto.toDto
                            dto.Prescription.Frequency.Variable.ValsOpt.Value.Value <-
                                dto.Prescription.Frequency.Variable.ValsOpt.Value.Value
                                |> Array.take 1

                            dto |> Order.Dto.fromDto
                    }
                )
        }
    ctx
|> checkCtx "frequency set"
|> Api.evaluate
|> checkCtx "frequency evaluated"
|> Api.evaluate
|> fun ctx ->
    let ctx =
        { ctx with
            Scenarios =
                ctx.Scenarios
                |> Array.map (fun s ->
                    { s with
                        Order =
                            let dto = s.Order |> Order.Dto.toDto
                            dto.Orderable.Components <-
                                dto.Orderable.Components
                                |> List.mapi (fun i cmp ->
                                    if i > 0 then cmp
                                    else
                                        cmp.Dose.Quantity.Variable.ValsOpt.Value.Value <-
                                            cmp.Dose.Quantity.Variable.ValsOpt.Value.Value
                                            |> Array.take 1
                                        cmp
                                )
                            dto |> Order.Dto.fromDto
                    }
                )
        }
    ctx
|> checkCtx "dose quantity set"
|> Api.evaluate
|> checkCtx "dose quantity evaluated"
|> ignore