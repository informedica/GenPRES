
#time

// load demo or product cache


System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")

#load "load.fsx"

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


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
        "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"


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
                |> OrderContext.evaluate OrderLogging.noOp provider
                |> GenFormResult.get
                |> OrderContext.Command.get

            if ctx.Scenarios |> Array.isEmpty |> not then ctx
            else ctx |> print
        with
        | e ->
            printfn $"\n=== ERROR\n{e}===\n"
            ctx

    let ctx = eval ctx ""

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

let printCtx = OrderContext.toString


let dro =
    Patient.newBorn
    |> Api.getPrescriptionRules provider
    |> GenFormResult.get
    |> Array.filter (fun pr ->
        pr.DoseRule.Generic |> String.equalsCapInsens "MM met BMF"
    )
    |> Array.head
    |> Medication.fromRule
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
    |> Medication.fromRule
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
|> Medication.fromRule
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
|> Medication.fromRule
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