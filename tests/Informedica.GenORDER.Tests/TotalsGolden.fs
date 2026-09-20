/// The totals the scenarios contribute, as they are today. The totals are what the intake
/// panel shows and what a nutrition order plan is judged on, so a change here is a change in
/// a dose. Regenerate them only deliberately, and say in the commit what moved and why.
///
/// The reference intake rows come from a Google sheet in production. These tests carry their
/// own, so they need no sheet and no GENPRES_URL_ID.
module TotalsGolden

open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types

open Expecto
open Expecto.Flip


/// The reference intake rows the totals are computed against: one per total a scenario can
/// contribute to, with no age or weight bounds, so that every row applies to every scenario.
module Reference =


    let row name unt : Types.Data.TotalsData =
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


    /// A row's name is what getTotals looks the total up by, and those names are Dutch:
    /// the Chloride field reads "chloor", the Carbohydrate field "koolhydraat". A row named
    /// anything else matches no item and leaves its field empty, which is a test that holds
    /// nothing rather than a test that fails.
    let all =
        [|
            row "volume" Units.Volume.milliLiter
            row "energie" Units.Energy.kiloCalorie
            row "eiwit" Units.Mass.gram
            row "koolhydraat" Units.Mass.gram
            row "natrium" Units.Molar.milliMole
            row "kalium" Units.Molar.milliMole
            row "chloor" Units.Molar.milliMole
            row "calcium" Units.Molar.milliMole
            row "fosfaat" Units.Molar.milliMole
            row "magnesium" Units.Molar.milliMole
        |]


    /// The weight the totals are adjusted to. Without a weight there are no totals at all,
    /// so this is what makes the numbers below exist.
    let weight = ValueUnit.singleWithUnit Units.Weight.kiloGram 15N


/// A scenario, and the totals it contributed when they were recorded. A field that is not
/// named contributed nothing.
type GoldenTotals =
    {
        /// The name the scenario has in Scenarios
        Name: string
        /// The Medication the order is built from
        Medication: Medication
        /// The totals, by the name of the field that holds each
        Totals: (string * string) list
    }


/// Every scenario the totals are recorded for. fullMedication is not among them: it does not
/// process cleanly, so its totals are unbounded and would be rewritten by the fix for that
/// rather than by a change worth failing on.
let all: GoldenTotals list =
    [
        {
            Name = "pcmSupp"
            Medication = Scenarios.pcmSupp
            Totals = []
        }
        {
            Name = "amfo"
            Medication = Scenarios.amfo
            Totals =
                [
                    "Volume", "#1,47# - #22,7# mL||/kg||/dag||"
                    "Energy", "#0,13# - #8,77# kCal||/kg||/dag||"
                    "Carbohydrate", "#0,03# - #2,19# g||/kg||/dag||"
                ]
        }
        {
            Name = "morfCont"
            Medication = Scenarios.morfCont
            Totals =
                [
                    "Volume", "#1,12# - #4,48# mL||/kg||/dag||"
                    "Energy", "#0,36# - #1,76# kCal||/kg||/dag||"
                    "Carbohydrate", "#0,09# - #0,44# g||/kg||/dag||"
                ]
        }
        {
            Name = "pcmDrink"
            Medication = Scenarios.pcmDrink
            Totals = [ ("Volume", "#2# mL||/kg||/dag||") ]
        }
        {
            Name = "cotrim"
            Medication = Scenarios.cotrim
            Totals = [ ("Volume", "#0,93# mL||/kg||/dag||") ]
        }
        {
            Name = "tpn"
            Medication = Scenarios.tpn
            Totals =
                [
                    "Volume", "#13,3# - #55,3# mL||/kg||/dag||"
                    "Protein", "#0,59# - #1,47# g||/kg||/dag||"
                    "Carbohydrate", "#0,01# - #4,27# g||/kg||/dag||"
                    "Sodium", "#2,07# - #2,58# mmol||/kg||/dag||"
                    "Potassium", "#1,48# - #1,97# mmol||/kg||/dag||"
                ]
        }
        {
            Name = "tpnComplete"
            Medication = Scenarios.tpnComplete
            Totals =
                [
                    "Volume", "#13,3# - #55,3# mL||/kg||/dag||"
                    "Energy", "#2,37# - #22,9# kCal||/kg||/dag||"
                    "Protein", "#0,59# - #1,47# g||/kg||/dag||"
                    "Carbohydrate", "#0,01# - #4,27# g||/kg||/dag||"
                    "Sodium", "#2,07# - #2,58# mmol||/kg||/dag||"
                    "Potassium", "#1,48# - #1,97# mmol||/kg||/dag||"
                    "Chloride", "#3,85# - #5,28# mmol||/kg||/dag||"
                    "Calcium", "#0,22# - #0,55# mmol||/kg||/dag||"
                    "Phosphate", "#0,15# - #0,37# mmol||/kg||/dag||"
                    "Magnesium", "#0,07# - #0,18# mmol||/kg||/dag||"
                ]
        }
    ]


/// The totals over all the recorded scenarios at once, the way an order plan takes them.
let together =
    [
        "Volume", "#32,2# - #141# mL||/kg||/dag||"
        "Energy", "#2,87# - #33,5# kCal||/kg||/dag||"
        "Protein", "#1,17# - #2,93# g||/kg||/dag||"
        "Carbohydrate", "#0,14# - #11,2# g||/kg||/dag||"
        "Sodium", "#4,15# - #5,17# mmol||/kg||/dag||"
        "Potassium", "#2,96# - #3,93# mmol||/kg||/dag||"
        "Chloride", "#3,85# - #5,28# mmol||/kg||/dag||"
        "Calcium", "#0,22# - #0,55# mmol||/kg||/dag||"
        "Phosphate", "#0,15# - #0,37# mmol||/kg||/dag||"
        "Magnesium", "#0,07# - #0,18# mmol||/kg||/dag||"
    ]


/// The order a scenario reaches the totals path as: built, and run to its bounds.
let order (med: Medication) =
    match med |> Medication.toOrder with
    | Error msg -> failtest $"no order for the scenario: %A{msg}"
    | Ok ord ->
        match OrderProcessor.processPipeline OrderLogging.noOp (CalcMinMax ord) with
        | Ok solved -> solved
        | Error(_, errs) -> failtest $"the scenario does not process: %A{errs}"


/// The totals of the given orders, by the name of the field that holds each. A field without
/// a value is left out, so that a total that appears where there was none fails too.
let totalsOf (ords: Order[]) =
    let t =
        ords
        |> Array.map Order.Dto.toDto
        |> Totals.getTotals Reference.all None (Some Reference.weight)

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
    |> List.choose (fun (n, v) -> v |> Option.map (fun v -> n, v))


[<Tests>]
let tests =
    testList
        "the totals the scenarios contribute"
        [
            for g in all do
                test $"%s{g.Name} contributes the totals it did" {
                    [| g.Medication |> order |]
                    |> totalsOf
                    |> Expect.equal $"the totals of %s{g.Name}" g.Totals
                }

            test "the scenarios together contribute the sum of them" {
                all
                |> List.map (fun g -> g.Medication |> order)
                |> Array.ofList
                |> totalsOf
                |> Expect.equal "the totals over all the scenarios at once" together
            }
        ]
