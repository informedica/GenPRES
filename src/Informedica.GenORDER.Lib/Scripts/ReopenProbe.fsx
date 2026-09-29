// Probe for #1195, step 2 of docs/implementation-plans/1034-the-clear-cross.md: on every fixture,
// does a clear and a solve give the one value back, and does a reset with the other picks applied
// again give the list back? The pipeline is the one the server runs: CalcMinMax on evaluation,
// CalcValues on selection, SolveOrder on an update, ReCalcValues on a reset.
// Run from this directory: dotnet fsi ReopenProbe.fsx (build first, so load.fsx finds the DLLs).

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"
#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"

open Informedica.Utils.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib


module Probe =

    module Quantity = OrderVariable.Quantity
    module Frequency = OrderVariable.Frequency
    module PerTime = OrderVariable.PerTime
    module Rate = OrderVariable.Rate
    module Dose = Order.Orderable.Dose
    module Time = OrderVariable.Time


    let run cmd ord =
        ord
        |> cmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> function
            | Ok ord -> ord
            | Error(ord, _) -> ord


    /// A field the dialog shows: its name, how to read it, how to pick its first value and how to
    /// clear it.
    type Field =
        {
            Name: string
            Get: Order -> OrderVariable option
            Pick: Order -> Order
            Clear: Order -> Order
        }


    let item (f: Dose -> Dose) = Order.OrderPropertyChange.proc [ ItemDose("", "", f) ]
    let firstItem (ord: Order) = ord.Orderable.Components |> List.tryHead |> Option.bind (_.Items >> List.tryHead)

    let frequency =
        {
            Name = "frequency"
            Get = _.Schedule >> Order.Schedule.getFrequency >> Option.map Frequency.toOrdVar
            Pick = Order.OrderPropertyChange.proc [ ScheduleFrequency Frequency.setMinValue ]
            Clear = Order.OrderPropertyChange.proc [ ScheduleFrequency Frequency.clear ]
        }

    let itemDose =
        {
            Name = "item dose"
            Get = firstItem >> Option.map (_.Dose.Quantity >> Quantity.toOrdVar)
            Pick = item (fun d -> { d with Quantity = d.Quantity |> Quantity.setMinValue })
            Clear = item (fun d -> { d with Quantity = d.Quantity |> Quantity.clear })
        }

    let orderableDose =
        {
            Name = "orderable dose"
            Get = _.Orderable.Dose.Quantity >> Quantity.toOrdVar >> Some
            Pick =
                Order.OrderPropertyChange.proc
                    [ OrderableDose(fun d -> { d with Quantity = d.Quantity |> Quantity.setMinValue }) ]
            Clear =
                Order.OrderPropertyChange.proc [ OrderableDose(fun d -> { d with Quantity = d.Quantity |> Quantity.clear }) ]
        }

    let orderableRate =
        {
            Name = "orderable rate"
            Get = _.Orderable.Dose.Rate >> Rate.toOrdVar >> Some
            Pick = Order.OrderPropertyChange.proc [ OrderableDose(fun d -> { d with Rate = d.Rate |> Rate.setMinValue }) ]
            Clear = Order.OrderPropertyChange.proc [ OrderableDose(fun d -> { d with Rate = d.Rate |> Rate.clear }) ]
        }


    /// The number of values a field holds: 0 for a range, None when the order has no such field.
    let count (field: Field) ord =
        field.Get ord |> Option.map (_.Variable >> Variable.count)


    let show label fields ord =
        let counts =
            fields
            |> List.map (fun f ->
                match count f ord with
                | Some n -> $"%s{f.Name} %i{n}"
                | None -> $"%s{f.Name} -"
            )
            |> String.concat ", "

        printfn $"  %-45s{label} %s{counts}"


    /// The probe of one fixture. The user picks field by field, solving after each; a field is
    /// flagged only when it still had a choice when picked, the rest are derived. Then, for each
    /// flagged field: cleared and solved, and reset with the other flagged fields picked again
    /// and solved. The line after the arrow is the count of the field that was cleared.
    let probe (name: string) (fields: Field list) (fixture: Medication) =
        printfn $"\n%s{name}"

        let built = fixture |> Medication.toOrder Scenarios.testStart |> Result.get |> run CalcMinMax |> run CalcValues
        show "built" fields built

        let picked, flagged =
            ((built, []), fields)
            ||> List.fold (fun (ord, flagged) f ->
                match count f ord with
                | Some n when n > 1 -> ord |> f.Pick |> run SolveOrder, flagged @ [ f ]
                | _ -> ord, flagged
            )

        show $"""picked: %s{flagged |> List.map _.Name |> String.concat ", "}""" fields picked

        for f in flagged do
            let others = flagged |> List.filter (fun o -> o.Name <> f.Name)
            let cleared = picked |> f.Clear |> run SolveOrder
            show $"%s{f.Name} cleared, solved" fields cleared

            let reopened =
                (picked |> run ReCalcValues, others)
                ||> List.fold (fun ord o -> ord |> o.Pick)
                |> run SolveOrder

            show $"""reset, %s{others |> List.map _.Name |> String.concat ", "} again""" fields reopened

            let kept =
                others
                |> List.forall (fun o -> count o reopened = Some 1 && (o.Get reopened |> Option.map _.Variable) = (o.Get picked |> Option.map _.Variable))

            printfn $"""  -> %s{f.Name}: cleared %A{count f cleared}, reopened %A{count f reopened}, other picks kept %b{kept}"""

            // the alternative: only the picks made before the cleared one are put back
            let earlier = flagged |> List.takeWhile (fun o -> o.Name <> f.Name)

            let reopenedEarlier =
                (picked |> run ReCalcValues, earlier)
                ||> List.fold (fun ord o -> ord |> o.Pick)
                |> run SolveOrder

            printfn $"""     earlier picks only (%s{earlier |> List.map _.Name |> String.concat ", "}): reopened %A{count f reopenedEarlier}"""


open Probe

let discontinuous = [ frequency; itemDose; orderableDose ]
let continuous = [ orderableRate; itemDose ]

probe "pcmSupp" discontinuous Scenarios.pcmSupp
probe "pcmDrink" discontinuous Scenarios.pcmDrink
probe "cotrim" discontinuous Scenarios.cotrim
probe "amfo" (orderableRate :: discontinuous) Scenarios.amfo
probe "morfCont" continuous Scenarios.morfCont
probe "fullMedication" (orderableRate :: discontinuous) Scenarios.fullMedication
