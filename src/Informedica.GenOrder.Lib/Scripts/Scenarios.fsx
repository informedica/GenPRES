// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1yn6UC1OMJ0A2wAyX3r0AA2qlKJ7vEAB6OO0DjneiknE")

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
            let pr = pr |> OrderContext.evaluate

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


module Orderable = Order.Orderable
module Dose = Orderable.Dose

open Informedica.GenUnits.Lib


let mutable value : Order Option = None


let pipeline ord =
    ord
    |> Order.print
    |> Order.processPipeLine OrderLogger.noLogger None


Patient.infant
|> Patient.setWeight (10m |> Kilogram |> Some)
|> OrderContext.create
|> OrderContext.setFilterGeneric "amoxicilline/clavulaanzuur"
|> OrderContext.setFilterRoute "oraal"
|> OrderContext.evaluate //|> ignore
|> OrderContext.printCtx "1 eval" //|> ignore
|> OrderContext.setFilterItem (FilterItem.Indication 0)
|> OrderContext.evaluate
|> fun ctx ->
    ctx |> OrderContext.printCtx "2 eval" |> ignore

    ctx.Scenarios
    |> Array.head
    |> _.Order
    |> pipeline
    |> Result.bind pipeline
    |> Result.map (Order.setMedianDose (Some "amoxicilline"))
    |> Result.bind pipeline
    |> Result.map (Order.clearItemDoseQuantity "amoxicilline")
    |> Result. map (fun ord ->
        let ord2 =
            ord


        value <- Some ord2
        ord2 |> Order.printState |> printfn "\n\nThe state of the order = %s\n"
        ord
    )
    |> Result.bind pipeline
    |> Result.map Order.toStringWithConstraints
    |> Result.defaultValue []
    |> String.concat "\n"
    |> printfn "%s"


value.Value
|> Order.print
|> (fun ord ->
    ord
    |> Order.doseIsSolved
)