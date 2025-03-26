// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

#load "load.fsx"

#time

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

Informedica.Utils.Lib.Constants.Tests.printAllSymbols ()

Product.Enteral.get ()

// TODO: could be used to precalc all possible
// prescriptions for a patient
let createScenarios (pr: OrderContext) =
    let getRules filter =
        { pr with Filter = filter } |> OrderContext.getRules

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


let printCtx = OrderContext.printCtx



Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "samenstelling c"
|> Api.evaluate
|> printCtx "1 eval"
|> fun ctx ->
    { ctx with
        OrderContext.Filter.DoseType = ctx.Filter.DoseTypes |> Array.tryItem 0
    }
|> Api.evaluate
|> printCtx "2 eval" //|> ignore
(*
|> fun ctx ->
    ctx.Scenarios |> Array.tryExactlyOne
    |> function
        | None -> ()
        | Some sc ->
            sc.Order
            |> Order.increaseIncrements OrderLogger.noLogger 10 10
            |> Result.defaultValue sc.Order
            |> Order.toString
            |> String.concat "\n"
            |> printfn "%s"
*)

|> Api.evaluate
|> printCtx "3 eval"//|> ignore
|> Api.evaluate
|> printCtx "4 eval"
|> fun ctx ->
    ctx.Scenarios |> Array.tryExactlyOne
    |> function
        | None -> ()
        | Some sc ->
            sc.Order
            |> Order.Dto.toDto
            |> fun dto ->
                dto |> Order.Dto.cleanDose
                dto
            |> Order.Dto.fromDto
            |> Order.applyConstraints
            |> Order.solveMinMax false OrderLogger.noLogger
            |> Result.map (Order.maximizeRate OrderLogger.noLogger)
            |> Result.defaultValue sc.Order
            |> Order.toString
            |> String.concat "\n"
            |> printfn "%s"


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterIndication "Ernstige infectie, gram negatieve microorganismen"
|> OrderContext.setFilterGeneric "gentamicine"
|> OrderContext.setFilterShape "injectievloeistof"
|> OrderContext.setFilterRoute "intraveneus"
|> printCtx "inital setup"
|> Api.evaluate
|> printCtx "first evaluation"
|> fun ctx ->
    match ctx.Scenarios |> Array.tryExactlyOne with
    | None -> ctx
    | Some sc ->
        { ctx with
            OrderContext.Filter.Components = sc.Components
            OrderContext.Filter.Diluents = sc.Diluents
            OrderContext.Filter.Diluent = sc.Diluent
        }
|> printCtx "one scenario"
|> fun ctx ->
    { ctx with
        OrderContext.Filter.Diluent = Some "NaCl 0,9%"
    }
|> Api.evaluate
|> printCtx "after diluent change"
|> ignore


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterIndication "Ernstige infectie, gram negatieve microorganismen"
|> OrderContext.setFilterGeneric "gentamicine"
|> OrderContext.setFilterShape "injectievloeistof"
|> OrderContext.setFilterRoute "intraveneus"
|> printCtx "inital setup"
|> Api.evaluate
|> printCtx "first evaluation" //|> ignore
|> Api.evaluate //|> ignore
|> printCtx "second evaluation"
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
|> printCtx "frequency set"
|> Api.evaluate
|> printCtx "frequency evaluated"
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
|> printCtx "dose quantity set"
|> Api.evaluate
|> printCtx "dose quantity evaluated"
|> ignore


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterIndication "Ernstige infectie, gram negatieve microorganismen"
|> OrderContext.setFilterGeneric "gentamicine"
|> OrderContext.setFilterShape "injectievloeistof"
|> OrderContext.setFilterRoute "intraveneus"
//|> OrderContext.getRules
//|> fst
|> Api.evaluate
|> printCtx "first eval"
|> ignore


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterIndication "TPV"
|> OrderContext.setFilterRoute "intraveneus"
|> fun ctx -> { ctx with OrderContext.Filter.DoseType = ("dag 1" |> DoseType.Timed |> Some) }
//|> OrderContext.getRules
//|> fst
|> Api.evaluate
|> printCtx "first eval"
|> fun ctx  ->
    { ctx with
        OrderContext.Filter.SelectedComponents =
            ctx.Filter.Components |> Array.skip 1 |> Array.take 2
    }
|> printCtx "components selected"
|> Api.evaluate
|> printCtx "evaluated selected"
|> ignore


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "noradrenaline"
|> printCtx "init"
|> Api.evaluate
|> printCtx "first eval"
|> fun ctx ->
    ctx.Scenarios
    |> Array.take 1
    |> Array.collect _.Diluents
    |> String.concat ","
    |> printfn "Diluents: %s"

    { ctx with Scenarios = ctx.Scenarios |> Array.take 1 }
|> printCtx "one scenario selected"
|> Api.evaluate
|> printCtx "second eval"
|> fun ctx ->
    { ctx with OrderContext.Filter.Diluent = ctx.Filter.Diluents |> Array.tryItem 1 }
|> Api.evaluate
|> printCtx "after diluent change"
|> ignore