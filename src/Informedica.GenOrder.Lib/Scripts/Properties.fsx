// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_DEBUG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_LOG", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1yn6UC1OMJ0A2wAyX3r0AA2qlKJ7vEAB6OO0DjneiknE")

#load "load.fsx"

#time

open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Informedica.GenSolver.Lib.Types

open Patient.Optics


let pipeline ord =
    ord
    |> Order.print
    |> Order.processPipeLine OrderLogger.noLogger None


let ord =
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
        |> Result.toOption
        |> Option.get


open OrderVariable


[
    PrescriptionFrequency Frequency.clear
    ItemDose ("amoxicilline/clavulaanzuur", "", Order.Orderable.Dose.setQuantityToNonZeroPositive)
]
|> List.fold Order.OrderPropertyChange.apply ord
|> Order.print
|> ignore