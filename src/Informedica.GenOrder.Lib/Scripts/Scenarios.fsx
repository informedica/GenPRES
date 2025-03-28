// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
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


let pickScenario n (ctx : OrderContext) =
    { ctx with
        Scenarios =
            if ctx.Scenarios |> Array.length < n then
                ctx.Scenarios |> Array.tryLast
            else
                ctx.Scenarios
                |> Array.tryItem n
            |> Option.map Array.singleton
            |> Option.defaultValue ctx.Scenarios
    }

open Patient.Optics


let printCtx = OrderContext.printCtx



Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "remdesivir"
|> Api.evaluate //|> ignore
|> printCtx "1 eval" //|> ignore
|> fun ctx ->
    { ctx with
        OrderContext.Filter.DoseType = ctx.Filter.DoseTypes |> Array.tryItem 0
    }
|> Api.evaluate
|> printCtx "2 eval" //|> ignore
|> ignore


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "methylprednisolon"
|> Api.evaluate //|> ignore
|> printCtx "1 eval"  //|> ignore
|> fun ctx ->
    { ctx with
        OrderContext.Filter.Indication =
            ctx.Filter.Indications
            |> Array.tryItem 2
        OrderContext.Filter.DoseType =
            ctx.Filter.DoseTypes
            |> Array.tryItem 0
    }
|> Api.evaluate
|> printCtx "2 eval" //|> ignore
|> pickScenario 0
|> Api.evaluate
//|> OrderContext.minimizeDose
|> printCtx "3 eval" //|> ignore
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
|> Api.evaluate
|> printCtx "second evaluation" //|> ignore
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
|> Api.evaluate
|> printCtx "second eval"
|> OrderContext.medianDose
|> Api.evaluate
|> printCtx "third eval"
|> Api.evaluate
|> printCtx "eval with solved dose"
|> fun ctx ->
    ctx.Scenarios
    |> Array.tryExactlyOne
    |> function
        | None -> ()
        | Some sc ->
            sc.Order
            |> Order.toOrdVars
            |> List.map (_.Variable >> _.Name >> Name.toString)
            |> String.concat "\n"
            |> printfn "%s"




Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "Nutrilon Pepti 1"
|> Api.evaluate //|> ignore
|> printCtx "1 eval" //|> ignore
|> fun ctx ->
    { ctx with
        OrderContext.Filter.DoseType = ctx.Filter.DoseTypes |> Array.tryItem 0
    }
|> Api.evaluate
|> printCtx "2 eval" //|> ignore
|> ignore


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "Nutrilon Pepti 1"
|> OrderContext.getRules
|> snd
|> Array.head
|> _.DoseRule
|> _.DoseLimits
|> Array.head
|> DoseRule.DoseLimit.hasNoLimits