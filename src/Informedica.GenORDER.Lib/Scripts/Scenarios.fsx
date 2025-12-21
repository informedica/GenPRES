
#time

// load demo or product cache

open System

Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM")

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


#load "load.fsx"


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Patient.Optics


module GenFormResult =

    let defaultValue value res =
        res
        |> Result.map fst
        |> Result.defaultValue value

    let get res =
        res
        |> Result.map fst
        |> Result.get


let provider : Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId
        OrderLogging.noOp
        "1JHOrasAZ_2fcVApYpt1qT2lZBsqrAxN-9SvBisXkbsM"



// TODO: could be used to precalc all possible
// prescriptions for a patient
let createScenarios (ctx: OrderContext) =
    let getRules filter =
        { ctx with Filter = filter }
        |> OrderContext.getRules OrderLogging.noOp provider
        |> fun (ctx, res) ->
            ctx,
            res
            |> GenFormResult.get

    let noEval ctx =
        let g = ctx.Filter.Generic
        let i = ctx.Filter.Indication
        let r = ctx.Filter.Route
        let d = ctx.Filter.DoseType
        ($"=== no evaluation of {g} {i} {r} {d} ===", ctx)
        |> Error

    let eval ctx (s : string) =
        // printfn $"evaluating {s}"
        try
            let ctx =
                ctx
                |> OrderContext.UpdateOrderContext
                |> OrderContext.evaluate OrderLogging.noOp provider
                |> GenFormResult.get
                |> OrderContext.Command.get

            if ctx.Scenarios |> Array.isEmpty |> not then Ok ctx
            else ctx |> noEval
        with
        | e ->
            ($"\n=== ERROR\n{e}===\n", ctx)
            |> Error

    [
        for g in ctx.Filter.Generics do
            let pr, rules = { ctx.Filter with Generic = Some g } |> getRules

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

                                    else
                                        pr |> noEval
    ]



let printScenarios path pat (scs: Result<OrderContext,(string * OrderContext)> list) =
    let append s = File.appendTextToFile path $"{s}\n"

    let printMd sl =
        sl
        |> Array.collect id
        |> Array.map Order.Print.unwrap
        |> String.concat " "
        |> String.replace "#" "**"
        |> String.replace "|" "*"

    $"# %s{pat |> Patient.toString}\n" |> append

    scs
    |> List.filter _.IsOk
    |> List.filter (fun r ->
        match r with
        | Ok ctx -> ctx.Scenarios |> Array.length > 0
        | _ -> false
    )
    |> List.map (function | Ok ctx -> ctx | Error _ -> failwith "no ctx")
    |> List.toArray
    |> Array.collect _.Scenarios
    |> Array.groupBy _.Name
    |> Array.map (fun (n, scs) ->
        {|
            name = n
            indications =
                scs
                |> Array.groupBy _.Indication
                |> Array.map (fun (i, scs) ->
                    {|
                        indication = i
                        routes =
                            scs
                            |> Array.groupBy _.Route
                            |> Array.map (fun (r, scs) ->
                                {|
                                    route = r
                                    doseTypes =
                                        scs
                                        |> Array.groupBy _.DoseType
                                        |> Array.map (fun (dt, scs) ->
                                            {|
                                                doseType = dt |> DoseType.toDescription
                                                shapes =
                                                    scs
                                                    |> Array.groupBy _.Form
                                                    |> Array.map (fun (s, scs) ->
                                                        {|
                                                            shape = s
                                                            orders =
                                                                scs
                                                                |> Array.map (fun sc ->
                                                                    {|
                                                                        pres = sc.Prescription |> printMd
                                                                        prep = sc.Preparation |>  printMd
                                                                        adms = sc.Administration |> printMd
                                                                    |}
                                                                )
                                                        |}
                                                    )
                                            |}
                                        )
                                |}
                            )
                    |}
                )
        |}
    )
    |> fun rs ->
        "<details>" |> append
        "<summary>Inhoudsopgave</summary>\n" |> append

        rs
    |> Array.map (fun r ->
        let l =
            r.name
            |> String.replace " " "-"
            |> String.replace "/" ""
            |> String.replace "(" ""
            |> String.replace ")" ""
            |> String.replace "." ""
            |> String.replace "," ""
            |> String.replace "%" ""
            |> String.replace ":" ""
            |> String.replace ";" ""
            |> String.toLower
            |> String.trim

        $"- [{r.name}](#{l})" |> append
        "" |> append
        r
    )
    |> fun rs ->
        "</details>\n" |> append

        rs
    |> Array.iter (fun r ->
        $"\n## {r.name}" |> append

        r.indications
        |> Array.iter (fun r ->
            $"\n### {r.indication}" |> append

            r.routes
            |> Array.iter (fun r ->
                $"\n#### {r.route}" |> append

                r.doseTypes
                |> Array.iter (fun r ->
                    $"\n##### {r.doseType}" |> append

                    r.shapes
                    |> Array.iter (fun r ->
                        if r.shape |> String.notEmpty then
                            $"\n*{r.shape}*" |> append

                        r.orders
                        |> Array.iter (fun r ->
                            "" |> append
                            if r.pres |> String.notEmpty then $"- 💊 {r.pres}" |> append
                            if r.prep |> String.notEmpty then $"- 🧪 {r.prep}" |> append
                            if r.adms |> String.notEmpty then $"- 💉 {r.adms}" |> append
                            "" |> append                        )
                    )
                )

            )
        )

        "---" |> append
    )


let scenarios =
    [
        "Newborn",
        Patient.newBorn
        |> Patient.setWeight (3m |> Kilogram |> Some)

        "Infant",
        Patient.infant
        |> Patient.setWeight (10m |> Kilogram |> Some)

        "Toddler",
        Patient.toddler

        "Child",
        Patient.child
        |> Patient.setWeight (20m |> Kilogram |> Some)

        "Teenager",
        Patient.teenager

        "Adult",
        Patient.adult
    ]
    |> List.map (fun (s, p) ->
        async {
            let scs =
                p
                |> OrderContext.create OrderLogging.noOp provider
                |> createScenarios
            return
                s, scs
        }
    )
    |> Async.Parallel
    |> Async.RunSynchronously


scenarios
|> Array.iter (fun (n, ctxs) ->
    let pat =
        match n with
        | _ when n = "Newborn" ->
            Patient.newBorn
            |> Patient.setWeight (3m |> Kilogram |> Some)
        | _ when n = "Infant" ->
            Patient.infant
            |> Patient.setWeight (10m |> Kilogram |> Some)
        | _ when n = "Toddler" ->
            Patient.toddler
        | _ when n = "Child" ->
            Patient.child
            |> Patient.setWeight (20m |> Kilogram |> Some)
        | _ when n = "Teenager" ->
            Patient.teenager
        | _ when n = "Adult" ->
            Patient.adult
        | _ -> failwith $"not recognized: {n}"

    ctxs
    |> printScenarios $"{n}.md" pat
)


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



let printCtx = OrderContext.toString



// shadow the original function to just get the result
module Order =

    module Dto =

        let fromDto : (Order.Dto.Dto -> Order) = Order.Dto.fromDto >> (function | Ok ord -> ord | Error _ -> failwith "couldn not get result")


let dro =
    Patient.newBorn
    |> Api.getPrescriptionRules provider
    |> GenFormResult.get
    |> Array.filter (fun pr ->
        pr.DoseRule.Generic |> String.equalsCapInsens "MM met BMF"
    )
    |> Array.head
    |> Medication.fromRule Logging.noOp
    |> Array.head


dro.Dose


let ord =
    Patient.newBorn
    |> Api.getPrescriptionRules provider
    |> GenFormResult.get
    |> Array.filter (fun pr ->
        pr.DoseRule.Generic |> String.equalsCapInsens "MM met BMF"
    )
    |> Array.head
    |> Medication.fromRule Logging.noOp
    |> Array.head
    |> Medication.toOrderDto
    |> Order.Dto.fromDto


ord |> Order.printTable ConsoleTables.Format.Minimal


Patient.teenager
|> Patient.setWeight (33m |> Kilogram |> Some)
    |> Api.getPrescriptionRules provider
    |> GenFormResult.get
|> Array.filter (fun pr ->
    pr.DoseRule.Generic |> String.equalsCapInsens "Samenstelling E"
)
|> Array.head
|> Medication.fromRule Logging.noOp
|> Array.head
|> Medication.toOrderDto
|> Order.Dto.fromDto
|> Order.print
|> ignore



Patient.teenager
|> Patient.setWeight (33m |> Kilogram |> Some)
    |> Api.getPrescriptionRules provider
    |> GenFormResult.get
|> Array.filter (fun pr ->
    pr.DoseRule.Generic |> String.equalsCapInsens "noradrenaline"
)
|> Array.head
|> Medication.fromRule Logging.noOp
|> Array.head
|> Medication.toOrderDto
|> Order.Dto.fromDto
|> Order.print
|> ignore


Patient.infant
|> Patient.setWeight (6m |> Kilogram |> Some)
|> OrderContext.create OrderLogging.noOp provider
|> fun ctx ->
    { ctx with
        OrderContext.Filter.Generic = Some "Samenstelling B"
    }
|> OrderContext.UpdateOrderContext
|> OrderContext.evaluate OrderLogging.noOp provider
|> fun res ->
    let ctx =
        res
        |> GenFormResult.get
        |> OrderContext.Command.get

    ctx.Scenarios
    |> Array.item 0
    |> _.Order
    |> Order.print
    |> ignore