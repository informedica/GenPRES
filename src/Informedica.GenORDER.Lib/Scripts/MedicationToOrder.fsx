#I __SOURCE_DIRECTORY__

/// # MedicationToOrder
///
/// Plan 831 (issue #831, plan 725 trailing phase O2): build an Order from a Medication
/// without going out to a Dto and back.
///
/// This script holds two things:
///
/// 1. `Harness` — the equivalence proof. Both paths over every scenario fixture, compared
///    unsolved and solved. It is written before any of the new code and is the gate every
///    later step is measured against.
/// 2. `ToOrder` — the new pipeline, grown one step at a time (plan steps B0 to B5).
///
/// Run it after `dotnet run Build`. The FSI session must be restarted after a rebuild,
/// because a referenced dll cannot be unloaded.

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
        med |> Medication.toOrderDto |> Order.Dto.fromDto


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


Harness.selfTest ()
