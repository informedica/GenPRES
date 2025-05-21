
#time

// load demo or product cache

open Informedica.GenOrder.Lib.Types.FilterItem

System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

#load "load.fsx"

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


// TODO: could be used to precalc all possible
// prescriptions for a patient
let createScenarios (ctx: OrderContext) =
    let getRules filter =
        { ctx with Filter = filter } |> OrderContext.getRules

    let print ctx =
        let g = ctx.Filter.Generic
        let i = ctx.Filter.Indication
        let r = ctx.Filter.Route
        let d = ctx.Filter.DoseType
        printfn $"=== no evaluation of {g} {i} {r} {d} ==="
        ctx

    let eval ctx s =
        printfn $"evaluating {s}"
        try
            let ctx =
                ctx
                |> OrderContext.UpdateOrderContext
                |> OrderContext.evaluate
                |> OrderContext.Command.get

            if ctx.Scenarios |> Array.isEmpty |> not then ctx
            else ctx |> print
        with
        | e ->
            printfn $"\n=== ERROR\n{e}===\n"
            ctx

    let pr = eval ctx ""

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



Patient.newBorn
|> PrescriptionRule.get
|> Array.filter (fun pr ->
    pr.DoseRule.Generic |> String.equalsCapInsens "Samenstelling B"
)
|> Array.head
|> DrugOrder.fromRule
|> Array.head
|> DrugOrder.toOrderDto
|> Order.Dto.fromDto
|> Order.print
|> ignore


Patient.teenager
|> Patient.setWeight (33m |> Kilogram |> Some)
|> PrescriptionRule.get
|> Array.filter (fun pr ->
    pr.DoseRule.Generic |> String.equalsCapInsens "Samenstelling E"
)
|> Array.head
|> DrugOrder.fromRule
|> Array.head
|> DrugOrder.toOrderDto
|> Order.Dto.fromDto
|> Order.print
|> ignore



Patient.teenager
|> Patient.setWeight (33m |> Kilogram |> Some)
|> PrescriptionRule.get
|> Array.filter (fun pr ->
    pr.DoseRule.Generic |> String.equalsCapInsens "noradrenaline"
)
|> Array.head
|> DrugOrder.fromRule
|> Array.head
|> DrugOrder.toOrderDto
|> Order.Dto.fromDto
|> Order.print
|> ignore


Patient.infant
|> Patient.setWeight (6m |> Kilogram |> Some)
|> OrderContext.create
|> fun ctx ->
    { ctx with
        OrderContext.Filter.Generic = Some "Samenstelling B"
    }
|> OrderContext.UpdateOrderContext
|> OrderContext.evaluate
|> fun cmd ->
    let ctx = cmd |> OrderContext.Command.get
    ctx.Scenarios
    |> Array.item 0
    |> _.Order
    |> Order.print
    |> ignore


Patient.infant
|> Patient.setWeight (6m |> Kilogram |> Some)
|> OrderContext.create
|> fun ctx ->
    { ctx with
        OrderContext.Filter.Generic = Some "Samenstelling B"
    }
|> OrderContext.getRules