// Implementation plan for issue #1495, step 3c: a renal rule that mixes per kg and per m2, or has a per kg
// or per m2 value without an adjust unit, is refused at load, together with the dose rules it applies to.
// A dose rule without an adjust unit takes the unit of an absolute renal rule's per kg or per m2 bounds.
// Step 3a, the dose rule checks and the BSA range, is migrated.
// docs/implementation-plans/1495-solve-without-the-adjust-equations.md

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"

#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"
#r "nuget: Expecto"

#load "../../../tests/Informedica.GenFORM.Tests/FixtureJson.fs"

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenForm.Tests


module RenalRule =

    open Informedica.GenForm.Lib.RenalRule
    open Utils


    /// Whether a renal rule applies to a dose rule: the generic, and the route, indication and
    /// dose type when the renal rule names them. Shared by the warnings and the refusal.
    let appliesToDoseRule routeMapping (rr: RenalRule) (dr: DoseRule) =
        rr
        |> appliesTo
            routeMapping
            (dr.Generic |> Generic.genericName |> Some)
            (Some dr.Route)
            (Some dr.Indication)
            (Some dr.DoseType)


    // The key that RenalRule.map groups the rows of one renal rule by. In the library map uses
    // this function.
    let groupKey (r: RenalRuleData) =
        r.Generic,
        r.Route,
        r.Indication,
        r.Source,
        r.MinAge,
        r.MaxAge,
        DoseType.fromString r.DoseType r.DoseText,
        r.FreqUnit,
        r.Frequencies,
        r.MinInterval,
        r.MaxInterval,
        r.IntervalUnit,
        createRenalFunction r.ContDial r.IntDial r.PerDial r.MinGFR r.MaxGFR


    let isAbsolute (r: RenalRuleData) = r.DoseRed |> String.equalsCapInsens DoseReduction.ABS


    /// Whether a row has a value in a per kg or per m2 column.
    let hasAdjustedValue (r: RenalRuleData) =
        [ r.MinQtyAdj; r.MaxQtyAdj; r.MinPerTimeAdj; r.MaxPerTimeAdj; r.MinRateAdj; r.MaxRateAdj ]
        |> List.exists Option.isSome
        || r.NormQtyAdj |> Array.isEmpty |> not
        || r.NormPerTimeAdj |> Array.isEmpty |> not


    /// The reasons a renal rule, given as its rows, is refused at load. Only the rows with an
    /// absolute reduction are checked: the relative rows hold reduction factors without a unit in
    /// the same columns.
    let unitRefusals (rows: RenalRuleData[]) =
        let rows = rows |> Array.filter isAbsolute |> Array.filter hasAdjustedValue

        [
            let units = rows |> Array.choose (_.AdjustUnit >> Units.adjustUnit) |> Array.distinct

            if units |> Array.length > 1 then
                "it has both per kg and per m2 bounds"

            for r in rows |> Array.filter (_.AdjustUnit >> Units.adjustUnit >> Option.isNone) do
                $"the row for %s{r.Substance} has a per kg or per m2 value without an adjust unit"
        ]


    let describe (rr: RenalRule) =
        let rf = rr.RenalFunction |> RenalFunction.toString
        $"the renal rule %s{rr.Generic} %s{rr.Route} %s{rr.Indication} (%s{rr.Source}, %s{rf})"


    let refusedRows (data: RenalRuleData[]) =
        data
        |> Array.groupBy groupKey
        |> Array.choose (fun (_, rows) ->
            match rows |> unitRefusals with
            | [] -> None
            | reasons -> Some(rows, reasons)
        )


    /// The renal rules refused at load, each with the reasons.
    let refused logger (data: RenalRuleData[]) =
        data
        |> refusedRows
        |> Array.collect (fun (rows, reasons) ->
            rows
            |> RenalRule.map logger
            |> Result.defaultValue [||]
            |> Array.map (fun rr -> rr, reasons)
        )


    /// The renal rules of the sheet without the refused ones.
    let map logger (data: RenalRuleData[]) =
        let refused = data |> refusedRows |> Array.collect fst |> Set.ofArray
        data |> Array.filter (refused.Contains >> not) |> RenalRule.map logger


    /// The dose rules without those a refused renal rule applies to, and one message per refused
    /// renal rule. A refused renal rule matches no patient, so without this a patient in its eGFR
    /// range would get the dose rule's full dose.
    let withoutRefused routeMapping (refused: (RenalRule * string list)[]) (doseRules: DoseRule[]) =
        let applies dr =
            refused |> Array.exists (fun (rr, _) -> appliesToDoseRule routeMapping rr dr)

        doseRules |> Array.filter (applies >> not),
        [
            for rr, reasons in refused do
                let n = doseRules |> Array.filter (appliesToDoseRule routeMapping rr) |> Array.length
                let reasons = reasons |> String.concat "; "

                Warning
                    $"%s{describe rr} is left out: %s{reasons}; %i{n} dose rules of %s{rr.Generic} are left out with it, until the sheet is fixed"
        ]


    /// Under an absolute reduction, a dose limit without an adjust unit takes the unit of the renal
    /// rule's per kg or per m2 bounds, so the order applies them at that measurement.
    let adjustDoseLimit (pat: Patient) (renalRule: RenalRule) (dl: DoseLimit) =
        let renalUnit =
            renalRule.RenalLimits
            |> Array.tryFind (fun rl -> rl.DoseLimitTarget = dl.DoseLimitTarget && rl.DoseReduction = Absolute)
            |> Option.bind (fun rl -> [ rl.QuantityAdjust; rl.PerTimeAdjust; rl.RateAdjust ] |> List.tryPick measureOf)

        let adjusted = dl |> RenalRule.adjustDoseLimit pat renalRule

        { adjusted with AdjustUnit = dl.AdjustUnit |> Option.orElse renalUnit }


    let adjustDoseRule pat (renalRule: RenalRule) (doseRule: DoseRule) =
        let dr = doseRule |> RenalRule.adjustDoseRule pat renalRule

        let limits =
            doseRule.ComponentLimits
            |> Array.map (fun cl ->
                { cl with SubstanceLimits = cl.SubstanceLimits |> Array.map (adjustDoseLimit pat renalRule) }
            )

        { dr with
            ComponentLimits = limits
            AdjustUnit =
                doseRule.AdjustUnit
                |> Option.orElse (limits |> Array.collect _.SubstanceLimits |> Array.tryPick _.AdjustUnit)
        }


module DoseRuleLoader =

    /// The registry entry for the dose rules: the dose rules without those a refused renal rule
    /// applies to.
    let fromDataWithRenal routeMapping formRoutes prods refusedRenal data =
        let drs, msgs = data |> DoseRuleLoader.fromData routeMapping formRoutes prods
        let drs, renalMsgs = drs |> RenalRule.withoutRefused routeMapping refusedRenal
        drs, msgs @ renalMsgs


module Resources =

    open Informedica.GenForm.Lib.Resources


    module Keys =

        let refusedRenalRules =
            ResourceKey.create<(RenalRule * string list)[]> "refusedRenalRules"


    /// The loader of the renal rule data. A renal sheet that fails to load fails the whole load:
    /// served empty, it would give every patient with a reduced kidney function the full dose.
    let renalRuleDataLoader (fetch: unit -> Result<RenalRuleData[], Message list>) = ofResult fetch


    /// The registry entries for the renal and dose rules. In the library these replace the
    /// entries of the renal rule data, the renal rules and the dose rules in defaultRegistry, and
    /// add the refused renal rules.
    let renalEntries logger fetchRenal =
        [
            Keys.renalRuleData.Name, renalRuleDataLoader fetchRenal

            Keys.refusedRenalRules.Name, derive (fun r -> r.Get Keys.renalRuleData |> RenalRule.refused logger)

            Keys.renalRules.Name,
            (fun r ->
                RenalRule.map logger (r.Get Keys.renalRuleData)
                |> Result.map (fun v -> box v, [])
            )

            Keys.doseRules.Name,
            deriveWith (fun r ->
                DoseRuleLoader.fromDataWithRenal
                    (r.Get Keys.routeMappings)
                    (r.Get Keys.formRoutes)
                    (r.Get Keys.products)
                    (r.Get Keys.refusedRenalRules)
                    (r.Get Keys.doseRuleData)
            )
        ]


open Expecto
open Expecto.Flip


let mg = Units.Mass.milliGram
let kg = Units.Weight.kiloGram
let m2 = Units.BSA.m2
let day = Units.Time.day
let perKg u = u |> ValueUnit.per kg
let perM2 u = u |> ValueUnit.per m2
let perDay u = u |> ValueUnit.per day

let range u min max =
    {
        Min = min |> Option.map (ValueUnit.singleWithUnit u >> Inclusive)
        Max = max |> Option.map (ValueUnit.singleWithUnit u >> Inclusive)
    }

let patient weight height =
    { Patient.patient with
        Weight = weight |> Option.map (ValueUnit.singleWithUnit kg)
        Height = height |> Option.map (ValueUnit.singleWithUnit Units.Height.centiMeter)
    }

let measured = patient (Some 70N) (Some 175N)

let fixture<'T> name =
    System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../../tests/Informedica.GenFORM.Tests/fixtures", name)
    |> System.IO.File.ReadAllText
    |> FixtureJson.deSerialize<'T>

let routeMappings = lazy (fixture<RouteMapping[]> "routemappings.json")
let products = lazy (fixture<ProductComponent[]> "products.json")
let doseRuleData = lazy (fixture<DoseRuleData[]> "doserules.json")

let todayLoad data =
    Informedica.GenForm.Lib.DoseRuleLoader.fromData routeMappings.Value [||] products.Value data

let substanceLimits (drs: DoseRule[]) =
    drs |> Array.collect _.ComponentLimits |> Array.collect _.SubstanceLimits

let renalRow : RenalRuleData =
    {
        Generic = "amoxicilline"
        Route = "INTRAVENEUS"
        Indication = ""
        Source = "test"
        MinAge = None
        MaxAge = None
        ContDial = ""
        IntDial = ""
        PerDial = ""
        MinGFR = Some 10N
        MaxGFR = Some 30N
        DoseType = ""
        DoseText = ""
        Frequencies = [||]
        MinInterval = None
        MaxInterval = None
        IntervalUnit = ""
        Substance = "amoxicilline"
        DoseRed = "abs"
        DoseUnit = "mg"
        AdjustUnit = "kg"
        FreqUnit = ""
        RateUnit = ""
        MinQty = None
        MaxQty = None
        NormQtyAdj = [||]
        MinQtyAdj = None
        MaxQtyAdj = Some 25N
        MinPerTime = None
        MaxPerTime = None
        NormPerTimeAdj = [||]
        MinPerTimeAdj = None
        MaxPerTimeAdj = None
        MinRate = None
        MaxRate = None
        MinRateAdj = None
        MaxRateAdj = None
    }

let noLogger = Informedica.Logging.Lib.Logging.noOp

let renalLimit qty qtyAdj perTimeAdj : RenalLimit =
    {
        DoseLimitTarget = SubstanceLimitTarget "paracetamol"
        DoseReduction = Absolute
        Quantity = qty
        NormQuantityAdjust = None
        QuantityAdjust = qtyAdj
        PerTime = MinMax.empty
        NormPerTimeAdjust = None
        PerTimeAdjust = perTimeAdj
        Rate = MinMax.empty
        RateAdjust = MinMax.empty
    }

let renalRule limit : RenalRule =
    {
        Generic = "paracetamol"
        Route = ""
        Indication = ""
        Source = "test"
        Age = MinMax.empty
        RenalFunction = EGFR(Some 10, Some 30)
        DoseType = NoDoseType
        Frequencies = None
        IntervalTime = MinMax.empty
        RenalLimits = [| limit |]
    }

let doseLimit adjustUnit =
    { Informedica.GenForm.Lib.DoseLimit.limit with
        DoseLimitTarget = SubstanceLimitTarget "paracetamol"
        DoseUnit = mg
        AdjustUnit = adjustUnit
    }

/// A demo dose rule with a single substance limit and the given adjust unit.
let doseRuleWith adjustUnit dl =
    let dr = todayLoad doseRuleData.Value |> fst |> Array.head

    { dr with
        AdjustUnit = adjustUnit
        ComponentLimits =
            [|
                { dr.ComponentLimits[0] with
                    Limit = None
                    SubstanceLimits = [| dl |]
                }
            |]
    }

let bsa = Patient.calcBSA measured |> Option.get

let maxOf (mm: MinMax) = mm.Max |> Option.map Limit.getValueUnit

let minOf (mm: MinMax) = mm.Min |> Option.map Limit.getValueUnit


let tests =
    testList
        "Issue 1495 step 3c, the renal rule fixes"
        [
            testList
                "renal rules"
                [
                    test "a renal rule with both kg and m2 bounds is refused, with its reason" {
                        let rows = [| renalRow; { renalRow with Substance = "other"; AdjustUnit = "m2" } |]

                        rows
                        |> RenalRule.refused noLogger
                        |> Array.map snd
                        |> Expect.equal "refused" [| [ "it has both per kg and per m2 bounds" ] |]

                        rows |> RenalRule.map noLogger |> Expect.equal "not mapped" (Ok [||])
                    }

                    test "an absolute renal row with a per kg value and no adjust unit is refused" {
                        [| { renalRow with AdjustUnit = "" } |]
                        |> RenalRule.refused noLogger
                        |> Array.map snd
                        |> Expect.equal
                            "refused"
                            [| [ "the row for amoxicilline has a per kg or per m2 value without an adjust unit" ] |]
                    }

                    test "a relative renal rule is not refused" {
                        let rows =
                            [|
                                { renalRow with
                                    DoseRed = "rel"
                                    AdjustUnit = ""
                                    MaxQtyAdj = Some(1N / 2N)
                                }
                            |]

                        rows |> RenalRule.refused noLogger |> Expect.isEmpty "not refused"
                        rows |> RenalRule.map noLogger |> Result.map Array.length |> Expect.equal "mapped" (Ok 1)
                    }

                    test "a renal rule with kg bounds only is mapped as today" {
                        let rows = [| renalRow |]

                        rows
                        |> RenalRule.map noLogger
                        |> Expect.equal "the same" (rows |> Informedica.GenForm.Lib.RenalRule.map noLogger)
                    }

                    test "a refused renal rule leaves out the dose rules of its generic and route, with one message" {
                        let doseRules = todayLoad doseRuleData.Value |> fst
                        let refused = [| { renalRow with AdjustUnit = "" } |] |> RenalRule.refused noLogger
                        let rr = refused |> Array.head |> fst

                        let isAmoxIV (dr: DoseRule) =
                            dr.Generic |> Generic.genericName = "amoxicilline" && dr.Route = "INTRAVENEUS"

                        let n = doseRules |> Array.filter isAmoxIV |> Array.length
                        n > 0 |> Expect.isTrue "the demo has amoxicillin IV"

                        let kept, msgs = doseRules |> RenalRule.withoutRefused routeMappings.Value refused

                        kept |> Array.exists isAmoxIV |> Expect.isFalse "amoxicillin IV is left out for every patient"

                        kept
                        |> Array.length
                        |> Expect.equal "every other dose rule stays" (doseRules.Length - n)

                        msgs
                        |> Expect.equal
                            "one message"
                            [
                                Warning
                                    $"%s{RenalRule.describe rr} is left out: the row for amoxicilline has a per kg or per m2 value without an adjust unit; %i{n} dose rules of amoxicilline are left out with it, until the sheet is fixed"
                            ]
                    }

                    test "a dose rule of the same generic on another route stays" {
                        let doseRules = todayLoad doseRuleData.Value |> fst

                        let refused =
                            [| { renalRow with AdjustUnit = ""; Route = "ORAAL" } |] |> RenalRule.refused noLogger

                        let kept, _ = doseRules |> RenalRule.withoutRefused routeMappings.Value refused

                        kept
                        |> Array.exists (fun dr -> dr.Generic |> Generic.genericName = "amoxicilline" && dr.Route = "INTRAVENEUS")
                        |> Expect.isTrue "amoxicillin IV stays"
                    }

                    test "the matching of a renal rule to dose rules, for the route, the indication and the dose type" {
                        let dr = todayLoad doseRuleData.Value |> fst |> Array.find (fun dr -> dr.Generic |> Generic.genericName = "amoxicilline")
                        let rr = renalRule (renalLimit MinMax.empty MinMax.empty MinMax.empty)
                        let rr = { rr with Generic = "amoxicilline" }
                        let applies rr = RenalRule.appliesToDoseRule routeMappings.Value rr dr

                        applies rr |> Expect.isTrue "naming none takes every dose rule of the generic"
                        applies { rr with Route = dr.Route } |> Expect.isTrue "the named route"
                        applies { rr with Route = "RECTAAL" } |> Expect.isFalse "another route"
                        applies { rr with Indication = dr.Indication } |> Expect.isTrue "the named indication"
                        applies { rr with Indication = "another" } |> Expect.isFalse "another indication"
                        applies { rr with DoseType = dr.DoseType } |> Expect.isTrue "the named dose type"
                        applies { rr with DoseType = Continuous "other" } |> Expect.isFalse "another dose type"
                    }

                    test "a renal sheet that fails to load fails the whole load, as today" {
                        let reg =
                            Map
                                [
                                    Resources.Keys.renalRuleData.Name,
                                    Resources.renalRuleDataLoader (fun () -> Error [ ErrorMsg("the renal sheet is offline", None) ])
                                ]

                        (fun () -> Informedica.GenForm.Lib.Resources.LoadEngine(reg).ForceAll())
                        |> Expect.throws "the load fails"
                    }

                    test "through the registry a refused renal rule is gone from the renal and the dose rules, with one message" {
                        let valid = { renalRow with MinGFR = Some 30N; MaxGFR = Some 50N }
                        let wrong = { renalRow with AdjustUnit = "" }

                        let fixtures =
                            [
                                Resources.Keys.routeMappings.Name, Resources.ofResult (fun () -> Ok routeMappings.Value)
                                Resources.Keys.formRoutes.Name, Resources.ofResult (fun () -> Ok([||]: FormRoute[]))
                                Resources.Keys.products.Name, Resources.ofResult (fun () -> Ok products.Value)
                                Resources.Keys.doseRuleData.Name, Resources.ofResult (fun () -> Ok doseRuleData.Value)
                                // today's warning, which reads the dose rules and the renal rules
                                Resources.Keys.renalRuleChecks.Name,
                                (Resources.defaultRegistry noLogger "")[Resources.Keys.renalRuleChecks.Name]
                            ]

                        let reg =
                            Resources.renalEntries noLogger (fun () -> Ok [| valid; wrong |]) @ fixtures |> Map

                        let eng = Informedica.GenForm.Lib.Resources.LoadEngine reg
                        eng.ForceAll()

                        let isAmoxIV (dr: DoseRule) =
                            dr.Generic |> Generic.genericName = "amoxicilline" && dr.Route = "INTRAVENEUS"

                        eng.Resolve Resources.Keys.doseRules
                        |> Array.exists isAmoxIV
                        |> Expect.isFalse "no amoxicillin IV dose rule"

                        eng.Resolve Resources.Keys.renalRules
                        |> Array.map _.RenalFunction
                        |> Expect.equal "only the valid renal rule" [| EGFR(Some 30, Some 50) |]

                        eng.Warnings
                        |> List.filter (
                            function
                            | Warning s -> s.Contains "are left out with it"
                            | _ -> false
                        )
                        |> List.length
                        |> Expect.equal "one message" 1
                    }
                ]

            testList
                "a renal rule with per kg or per m2 bounds on a dose rule without an adjust unit"
                [
                    let dl =
                        { doseLimit None with Quantity = range mg (Some 500N) (Some 1000N) }

                    let rr = renalRule (renalLimit MinMax.empty (range (perM2 mg) None (Some 400N)) MinMax.empty)

                    test "on today's code the rule keeps no adjust unit, so the order falls back to kg" {
                        let dr = doseRuleWith None dl |> Informedica.GenForm.Lib.RenalRule.adjustDoseRule measured rr

                        dr.AdjustUnit |> Expect.isNone "no adjust unit"
                    }

                    test "the dose limit and the rule take m2: at least 500 mg and at most 400 mg/m2" {
                        let dr = doseRuleWith None dl |> RenalRule.adjustDoseRule measured rr
                        let dl = dr |> Array.singleton |> substanceLimits |> Array.head

                        dr.AdjustUnit |> Expect.equal "the rule" (Some m2)
                        dl.AdjustUnit |> Expect.equal "the limit" (Some m2)
                        dl.Quantity |> minOf |> Expect.equal "at least 500 mg" (Some(ValueUnit.singleWithUnit mg 500N))
                        dl.Quantity |> maxOf |> Expect.isNone "the 1,000 mg goes"

                        dl.QuantityAdjust
                        |> maxOf
                        |> Expect.equal "at most 400 mg/m2" (Some(ValueUnit.singleWithUnit (perM2 mg) 400N))
                    }

                    test "a renal maximum of 600 mg/m2 per day on a per kg rule gives the absolute maximum, and kg stays" {
                        let dl =
                            { doseLimit (Some kg) with PerTimeAdjust = range (perDay (perKg mg)) None (Some 30N) }

                        let rr = renalRule (renalLimit MinMax.empty MinMax.empty (range (perDay (perM2 mg)) None (Some 600N)))
                        let dr = doseRuleWith (Some kg) dl |> RenalRule.adjustDoseRule measured rr
                        let dl = dr |> Array.singleton |> substanceLimits |> Array.head

                        dr.AdjustUnit |> Expect.equal "the rule keeps kg" (Some kg)
                        dl.AdjustUnit |> Expect.equal "the limit keeps kg" (Some kg)

                        dl.PerTime
                        |> maxOf
                        |> Option.map (ValueUnit.convertTo (perDay mg))
                        |> Expect.equal "600 mg/m2 at the BSA" (Some((ValueUnit.singleWithUnit (perDay (perM2 mg)) 600N * bsa) |> ValueUnit.convertTo (perDay mg)))
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
