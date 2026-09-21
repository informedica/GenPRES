#I __SOURCE_DIRECTORY__

// # MedicationToOrder
//
// Plan 831 (issue #831, plan 725 trailing phase O2): build an Order from a Medication
// without going out to a Dto and back.
//
// This script holds two things:
//
// The pipeline was grown here and now lives in `Medication.OrderBuilder`, so what is left is
// the proof: both paths over every scenario fixture, compared unsolved, solved, and through
// the nutrition processing. It holds as long as `toOrderDto` exists to be compared against.
//
// Run it after `dotnet run Build`. The FSI session must be restarted after a rebuild,
// because a referenced dll cannot be unloaded.

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types
open Informedica.GenSolver.Lib.Variable.ValueRange



/// The fixtures the proof runs over: every scenario in the test project, which between them
/// cover all five order types, single and multiple components, solution limits and
/// divisibility.
module Fixtures =


    /// A medication parsed from one of the text fixtures, or a failure to parse it.
    let ofText name text =
        match text |> Medication.fromString with
        | Ok med -> Some(name, med)
        | Error errs ->
            printfn $"could not parse %s{name}: %A{errs}"
            None


    /// Every fixture, by name.
    let all =
        [
            "pcmSupp", Scenarios.pcmSupp
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "pcmDrink", Scenarios.pcmDrink
            "cotrim", Scenarios.cotrim
            "tpn", Scenarios.tpn
            "tpnComplete", Scenarios.tpnComplete
            "fullMedication", Scenarios.fullMedication
        ]
        @ ([
            "pcmSuppText", Scenarios.pcmSuppText
            "kaliumchlorideOnceTimedText", Scenarios.kaliumchlorideOnceTimedText
            "fullMedicationText", Scenarios.fullMedicationText
           ]
           |> List.choose (fun (n, t) -> ofText n t))



/// The equivalence proof: the old path and the new one, compared over every fixture.
module Harness =


    /// The old path, the one plan 831 replaces: a Medication out to an Order Dto and back.
    let viaDto (med: Medication) =
        med |> Medication.toOrderDto System.DateTime.UtcNow |> Order.Dto.fromDto


    /// Both paths read the clock, so an order's start and stop say nothing about the build
    /// that made it. Normalising them leaves a comparison of everything that does.
    let normalize (ord: Order) =
        { ord with
            StartStop = DateTime.MinValue |> StartStop.Start
        }


    /// Whether two orders are the same order, the start and stop aside.
    let sameOrder (a: Order) (b: Order) = (a |> normalize) = (b |> normalize)


    /// The canonical serialization of an order, the form the signing digest is taken over.
    /// Two of these diffed name the variable that moved.
    let canonical (ord: Order) =
        ord |> normalize |> Order.Dto.toDto |> Canonical.serialize


    /// The first line on which two canonical forms differ, with a little of both around it.
    let firstDifference (a: string) (b: string) =
        let split (s: string) = s |> String.replace "," ",\n" |> String.split "\n"
        let xs, ys = a |> split, b |> split

        List.zip3 [ 0 .. (min xs.Length ys.Length) - 1 ] (xs |> List.truncate ys.Length) (ys |> List.truncate xs.Length)
        |> List.tryFind (fun (_, x, y) -> x <> y)
        |> function
            | Some(i, x, y) -> $"line %i{i}:\n  old: %s{x |> String.trim}\n  new: %s{y |> String.trim}"
            | None -> "no line differs; the forms differ in length"


    /// The full solve every scenario goes through before it reaches a prescriber.
    let solve (ord: Order) =
        let run cmd o =
            o |> cmd |> OrderProcessor.processPipeline Logging.noOp |> function
                | Ok o -> o
                | Error(o, _) -> o

        ord
        |> run CalcMinMax
        |> run IncreaseIncrements
        |> run CalcValues
        |> run SolveOrder


    /// Compare one fixture through both paths, unsolved and then solved, and say what was
    /// found. `toOrder` is the new path under test.
    let check toOrder (name: string, med: Medication) =
        match med |> viaDto, med |> toOrder with
        | Error e, _ -> $"%-28s{name} SKIP  the old path fails: %A{e}"
        | _, Error e -> $"%-28s{name} FAIL  the new path fails: %A{e}"
        | Ok oldOrd, Ok newOrd ->
            if oldOrd |> sameOrder newOrd |> not then
                let d = firstDifference (canonical oldOrd) (canonical newOrd)
                $"%-28s{name} FAIL  unsolved orders differ\n%s{d}"
            else
                let oldSolved, newSolved = oldOrd |> solve, newOrd |> solve

                if oldSolved |> sameOrder newSolved then
                    $"%-28s{name} ok    unsolved and solved equal"
                else
                    let a = oldSolved |> normalize |> Order.toString
                    let b = newSolved |> normalize |> Order.toString

                    let d =
                        List.zip (a |> List.truncate b.Length) (b |> List.truncate a.Length)
                        |> List.filter (fun (x, y) -> x <> y)
                        |> List.map (fun (x, y) -> $"  old: %s{x}\n  new: %s{y}")
                        |> String.concat "\n"

                    $"%-28s{name} FAIL  solved orders differ\n%s{d}"


    /// Run the proof over every fixture and print the result of each.
    let run toOrder =
        Fixtures.all |> List.map (check toOrder) |> List.iter (printfn "%s")


    /// The proof run against the old path itself: every line must read ok. This is the
    /// self-test of the harness, and the baseline the new path is measured against.
    let selfTest () = run viaDto



/// A gauge for the steps between the shape pass and the flip: which order variables still
/// carry constraints the old path gives them and the new one does not, or the other way
/// round. The list shrinks as each constraint pass lands; when it is empty, the pipeline is
/// done and the harness above is the proof.
module Gauge =


    /// Every order variable of an order, by name.
    let byName (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.map (fun ovar -> ovar |> OrderVariable.getName |> Name.toString, ovar)


    /// The constraints of an order variable, as the one line that says all four bounds.
    let constraintsOf (ovar: OrderVariable) =
        let vuStr = ValueUnit.toStringDutchShort

        ovar
        |> OrderVariable.getConstraints
        |> fun cs ->
            let incl b = if b then "=" else " "

            let min =
                cs.Min
                |> Option.map (fun m -> $"%s{m |> Minimum.toValueUnit |> vuStr}%s{m |> Minimum.isIncl |> incl}")
                |> Option.defaultValue "."

            let max =
                cs.Max
                |> Option.map (fun m -> $"%s{m |> Maximum.toValueUnit |> vuStr}%s{m |> Maximum.isIncl |> incl}")
                |> Option.defaultValue "."
            let incr = cs.Incr |> Option.map (Increment.toValueUnit >> vuStr) |> Option.defaultValue "."
            let vals = cs.Values |> Option.map (ValueSet.toValueUnit >> vuStr) |> Option.defaultValue "."

            $"min %s{min} | incr %s{incr} | max %s{max} | vals %s{vals}"


    /// The order variables whose constraints the two paths disagree about, by name.
    let diff (oldOrd: Order) (newOrd: Order) =
        let olds = oldOrd |> byName |> Map.ofList
        let news = newOrd |> byName |> Map.ofList

        olds
        |> Map.toList
        |> List.choose (fun (n, o) ->
            match news |> Map.tryFind n with
            | None -> Some(n, o |> constraintsOf, "(no such variable)")
            | Some v ->
                let a, b = o |> constraintsOf, v |> constraintsOf
                if a = b then None else Some(n, a, b)
        )


    /// Print, per fixture, how many order variables still disagree and the first few of them.
    let report toOrder =
        Fixtures.all
        |> List.iter (fun (name, med) ->
            match med |> Harness.viaDto, med |> toOrder with
            | Ok oldOrd, Ok newOrd ->
                let d = diff oldOrd newOrd
                printfn $"%-28s{name} %3i{d |> List.length} of %3i{oldOrd |> byName |> List.length} variables differ"

                d
                |> List.truncate 4
                |> List.iter (fun (n, a, b) -> printfn $"    %s{n}\n      old: %s{a}\n      new: %s{b}")
            | Error e, _ -> printfn $"%-28s{name} the old path fails: %A{e}"
            | _, Error e -> printfn $"%-28s{name} the new path fails: %A{e}"
        )


/// The nutrition path, the one place where an order has property changes applied and is
/// solved the moment it is built, rather than going through the processing pipeline.
module NutritionCheck =


    /// What `Nutrition.proc` does to an order once it has one.
    let proc (ord: Order) =
        ord
        |> Order.OrderPropertyChange.proc Nutrition.tpnConstraints
        |> Order.solveMinMax "check" true Logging.noOp
        |> function
            | Ok ord -> ord
            | Error(ord, _) -> ord


    /// Both paths through the nutrition processing, compared.
    let run toOrder =
        [ "tpn"; "tpnComplete" ]
        |> List.iter (fun name ->
            let med = Fixtures.all |> List.find (fst >> (=) name) |> snd

            match med |> Harness.viaDto, med |> toOrder with
            | Ok oldOrd, Ok newOrd ->
                let a, b = oldOrd |> proc |> Harness.normalize, newOrd |> proc |> Harness.normalize

                if a = b then
                    printfn $"%-28s{name} ok    the nutrition path agrees"
                else
                    let d =
                        List.zip (a |> Order.toString) (b |> Order.toString)
                        |> List.filter (fun (x, y) -> x <> y)
                        |> List.map (fun (x, y) -> $"  old: %s{x}\n  new: %s{y}")
                        |> String.concat "\n"

                    printfn $"%-28s{name} FAIL  the nutrition path differs\n%s{d}"
            | _ -> printfn $"%-28s{name} SKIP  a path failed"
        )


// The proof, printed when the script is run: how many order variables still disagree, then
// the orders themselves unsolved and solved, then the nutrition path.

Gauge.report Medication.toOrder

Harness.run Medication.toOrder

NutritionCheck.run Medication.toOrder
