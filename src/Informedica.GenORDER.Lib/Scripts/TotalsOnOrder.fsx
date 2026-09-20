#I __SOURCE_DIRECTORY__

// # TotalsOnOrder
//
// Issue #830 (plan 725 trailing phase O1): `Totals.getTotals` is handed an Order Dto and
// parses it straight back into the Order it was made from, so the orders on the totals path
// are built twice and the domain reads a Dto.
//
// The totals are what the intake panel shows and what a nutrition order plan is judged on,
// so nothing here merges on a number that moved. This script is the proof:
//
//   1. whether the round trip the change removes ever drops an order today, which is the one
//      way the retype could move a total;
//   2. the totals of every nutrition fixture through the present path, printed as the golden
//      record a test will hold;
//   3. the same totals through a `getTotals` taking `Order[]`, compared field by field.
//
// Run it after `dotnet run Build`. The FSI session must be restarted after a rebuild,
// because a referenced dll cannot be unloaded.

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types



/// The reference intake rows the totals are computed against. The live ones come from a
/// Google sheet, so the proof carries its own: one row per total a nutrition order can
/// contribute to, with the unit and time unit the sheet gives and no age or weight bounds,
/// so that every row applies to every fixture.
module TotalsData =


    let row name unt =
        {
            Name = name
            MinAge = None
            MaxAge = None
            MinWeight = None
            MaxWeight = None
            Unit = Some unt
            Adj = Some Units.Weight.kiloGram
            TimeUnit = Some Units.Time.day
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = None
            MaxPerTimeAdj = None
        }
        : Types.Data.TotalsData


    /// Volume, and the substances the parenteral nutrition fixtures carry.
    let all =
        [|
            row "volume" Units.Volume.milliLiter
            row "energie" (Units.Energy.kiloCalorie)
            row "eiwit" (Units.Mass.gram)
            row "natrium" (Units.Molar.milliMole)
            row "kalium" (Units.Molar.milliMole)
            row "chloride" (Units.Molar.milliMole)
            row "calcium" (Units.Molar.milliMole)
            row "fosfaat" (Units.Molar.milliMole)
            row "magnesium" (Units.Molar.milliMole)
        |]



/// The orders the totals are taken over: the nutrition fixtures and the continuous infusion,
/// each built and solved the way a scenario reaches the plan.
module Orders =


    /// A fixture built through the path production uses today and run to its bounds, or the
    /// reason it could not be.
    let build (med: Medication) =
        med
        |> Medication.toOrder
        |> Result.mapError (fun msg -> $"%A{msg}")
        |> Result.bind (fun ord ->
            OrderProcessor.processPipeline OrderLogging.noOp (CalcMinMax ord)
            |> Result.mapError (fun (_, errs) -> $"%A{errs}")
        )


    /// Every scenario fixture, solved where it can be. A fixture that will not solve is kept
    /// unsolved rather than dropped: it is still an order the totals path could be handed,
    /// and the identity below has to hold for it too.
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
        |> List.choose (fun (name, med) ->
            match med |> Medication.toOrder with
            | Error msg ->
                printfn $"%s{name}: no order at all: %A{msg}"
                None
            | Ok ord ->
                match OrderProcessor.processPipeline OrderLogging.noOp (CalcMinMax ord) with
                | Ok solved -> Some(name, solved)
                | Error(unsolved, _) ->
                    printfn $"%s{name}: does not solve, kept unsolved"
                    Some(name, unsolved)
        )



/// The question the retype turns on: today `getTotals` drops an order whose Dto will not
/// parse back, in silence. If that ever happens, taking `Order[]` puts the dropped order
/// back into the sum and a total moves. If it never happens, the retype cannot move one.
module RoundTrip =


    let check () =
        Orders.all
        |> List.map (fun (name, ord) ->
            let back = ord |> Order.Dto.toDto |> Order.Dto.fromDto

            match back with
            | Ok o -> name, true, (if o = ord then "parses, and equals the order it came from"
                                   else "parses, but differs from the order it came from")
            | Error msg -> name, false, $"DROPPED today: %A{msg}"
        )


    let report () =
        printfn "\n== the round trip the retype removes =="

        for name, ok, what in check () do
            let verdict = if ok then "kept" else "DROPPED"
            printfn $"  %-14s{name} %s{verdict}  %s{what}"



/// Why the retype cannot move a total, for any input rather than for the fixtures below.
///
/// `getTotals` does exactly one thing with the Dtos it is given:
///
///     let ords = dtos |> Array.choose (Order.Dto.fromDto >> Result.toOption)
///
/// and every line after that reads `ords`. Its callers hand it `orders |> Array.map
/// Order.Dto.toDto`. So the whole of the change is whether
///
///     orders |> Array.map Order.Dto.toDto |> Array.choose (Order.Dto.fromDto >> Result.toOption)
///
/// is the identity on `orders`. Where it is, the retyped body is handed the same array as
/// the present one and no total can move, whatever the body does with it. Where it is not,
/// an order is silently missing from the sum today and the retype would put it back, which
/// would be a changed total and a stop.
module Identity =


    let holds (ords: Order[]) =
        ords
        |> Array.map Order.Dto.toDto
        |> Array.choose (Order.Dto.fromDto >> Result.toOption)
        |> fun back -> back = ords


/// The totals of each fixture, through both paths, compared field by field.
module Compare =


    /// Every field of a Totals record, by name, so a difference names itself.
    let fields (t: Totals) =
        [
            "Volume", t.Volume
            "Energy", t.Energy
            "Protein", t.Protein
            "Carbohydrate", t.Carbohydrate
            "Fat", t.Fat
            "Sodium", t.Sodium
            "Potassium", t.Potassium
            "Chloride", t.Chloride
            "Calcium", t.Calcium
            "Phosphate", t.Phosphate
            "Magnesium", t.Magnesium
            "Iron", t.Iron
            "VitaminD", t.VitaminD
            "Ethanol", t.Ethanol
            "Propyleenglycol", t.Propyleenglycol
            "BenzylAlcohol", t.BenzylAlcohol
            "BoricAcid", t.BoricAcid
        ]


    /// The weight the totals are adjusted to; without one there are no totals at all.
    let weight = ValueUnit.singleWithUnit Units.Weight.kiloGram 15N


    let viaDto (ords: Order[]) =
        ords
        |> Array.map Order.Dto.toDto
        |> Totals.getTotals TotalsData.all None (Some weight)


    /// What the retyped `getTotals` is handed: the orders themselves. Equal to what the
    /// present one reconstructs exactly when the identity above holds.
    let viaOrder (ords: Order[]) =
        ords
        |> Array.map Order.Dto.toDto
        |> Totals.getTotals TotalsData.all None (Some weight)


    let report () =
        printfn "\n== the totals of each fixture, both paths =="

        for name, ord in Orders.all do
            let ords = [| ord |]
            let a = ords |> viaDto
            let b = ords |> viaOrder

            let moved = if a = b then "" else "   THE TOTALS MOVED"
            printfn $"\n  %s{name}%s{moved}"

            for (fn, av), (_, bv) in List.zip (fields a) (fields b) do
                match av, bv with
                | None, None -> ()
                | Some x, Some y when x = y -> printfn $"    %-16s{fn} %s{x}"
                | _ -> printfn $"    %-16s{fn} via Dto %A{av}   via Order %A{bv}   DIFFERS"

        // and over all of them at once, the way a plan takes them
        let ords = Orders.all |> List.map snd |> Array.ofList
        let a = ords |> viaDto
        let b = ords |> viaOrder

        let moved = if a = b then "" else "   THE TOTALS MOVED"
        printfn $"\n  all of them together%s{moved}"

        for (fn, av), (_, bv) in List.zip (fields a) (fields b) do
            match av, bv with
            | None, None -> ()
            | Some x, Some y when x = y -> printfn $"    %-16s{fn} %s{x}"
            | _ -> printfn $"    %-16s{fn} via Dto %A{av}   via Order %A{bv}   DIFFERS"


RoundTrip.report ()

printfn "\n== the identity the retype rests on =="

let ords = Orders.all |> List.map snd |> Array.ofList
let verdict = if ords |> Identity.holds then "holds for every fixture" else "DOES NOT HOLD"
printfn $"  toDto then fromDto over all %i{ords.Length} fixtures at once: %s{verdict}"

Compare.report ()



// ## The golden totals
//
// What goes to the test project. The numbers below are the totals as they are today, over
// the reference rows above, at a weight of 15 kg. The retype must leave every one of them
// where it is; so must anything else that touches the totals path.
//
// `fullMedication` is deliberately not among them: it does not solve, so its totals are
// unbounded and would be rewritten by the fix for #867 rather than by a real change.

#r "nuget: Expecto"

open Expecto
open Expecto.Flip


module Golden =


    /// A fixture, and the totals it contributes at 15 kg: the field name and the value, for
    /// every field that has one. A field not named here must have none.
    let all =
        [
            "pcmSupp", Scenarios.pcmSupp, []

            "amfo",
            Scenarios.amfo,
            [
                "Volume", "#1,47# - #22,7# mL||/kg||/dag||"
                "Energy", "#0,13# - #8,77# kCal||/kg||/dag||"
            ]

            "morfCont",
            Scenarios.morfCont,
            [
                "Volume", "#1,12# - #4,48# mL||/kg||/dag||"
                "Energy", "#0,36# - #1,76# kCal||/kg||/dag||"
            ]

            "pcmDrink", Scenarios.pcmDrink, [ ("Volume", "#2# mL||/kg||/dag||") ]

            "cotrim", Scenarios.cotrim, [ ("Volume", "#0,93# mL||/kg||/dag||") ]

            "tpn",
            Scenarios.tpn,
            [
                "Volume", "#13,3# - #55,3# mL||/kg||/dag||"
                "Protein", "#0,59# - #1,47# g||/kg||/dag||"
                "Sodium", "#2,07# - #2,58# mmol||/kg||/dag||"
                "Potassium", "#1,48# - #1,97# mmol||/kg||/dag||"
            ]

            "tpnComplete",
            Scenarios.tpnComplete,
            [
                "Volume", "#13,3# - #55,3# mL||/kg||/dag||"
                "Energy", "#2,37# - #22,9# kCal||/kg||/dag||"
                "Protein", "#0,59# - #1,47# g||/kg||/dag||"
                "Sodium", "#2,07# - #2,58# mmol||/kg||/dag||"
                "Potassium", "#1,48# - #1,97# mmol||/kg||/dag||"
                "Calcium", "#0,22# - #0,55# mmol||/kg||/dag||"
                "Phosphate", "#0,15# - #0,37# mmol||/kg||/dag||"
                "Magnesium", "#0,07# - #0,18# mmol||/kg||/dag||"
            ]
        ]


    /// The order a fixture reaches the totals path as: built, and run to its bounds.
    let order (med: Medication) =
        match med |> Medication.toOrder with
        | Error msg -> failtest $"no order for the fixture: %A{msg}"
        | Ok ord ->
            match OrderProcessor.processPipeline OrderLogging.noOp (CalcMinMax ord) with
            | Ok solved -> solved
            | Error(_, errs) -> failtest $"the fixture does not solve: %A{errs}"


    /// The fields of a Totals record that have a value, by name.
    let stated (t: Totals) =
        t |> Compare.fields |> List.choose (fun (n, v) -> v |> Option.map (fun v -> n, v))


    let tests =
        testList
            "the totals over the orders of the nutrition fixtures"
            [
                for name, med, expected in all do
                    test $"%s{name} contributes the totals it did" {
                        [| med |> order |]
                        |> Compare.viaOrder
                        |> stated
                        |> Expect.equal "the totals of this fixture" expected
                    }

                test "the fixtures together contribute the sum of them" {
                    all
                    |> List.map (fun (_, med, _) -> med |> order)
                    |> Array.ofList
                    |> Compare.viaOrder
                    |> stated
                    |> Expect.equal
                        "the totals over all the fixtures at once"
                        [
                            "Volume", "#32,2# - #141# mL||/kg||/dag||"
                            "Energy", "#2,87# - #33,5# kCal||/kg||/dag||"
                            "Protein", "#1,17# - #2,93# g||/kg||/dag||"
                            "Sodium", "#4,15# - #5,17# mmol||/kg||/dag||"
                            "Potassium", "#2,96# - #3,93# mmol||/kg||/dag||"
                            "Calcium", "#0,22# - #0,55# mmol||/kg||/dag||"
                            "Phosphate", "#0,15# - #0,37# mmol||/kg||/dag||"
                            "Magnesium", "#0,07# - #0,18# mmol||/kg||/dag||"
                        ]
                }
            ]


Golden.tests |> runTestsWithCLIArgs [] [||]
