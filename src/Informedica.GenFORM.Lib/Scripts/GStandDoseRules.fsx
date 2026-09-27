(*
    GStandDoseRules.fsx
    ===================
    Prototype: detect GenFORM dose rules that lack an *adult* entry and
    supplement them with G-Standard (ZForm / GStand) data.

    Related issue: #307 — Add G-Standard dose rules

    Usage (from this directory):
        dotnet fsi GStandDoseRules.fsx

    Pre-requisites:
        dotnet run Build   # or dotnet build GenPRES.sln
        export GENPRES_PROD=1          # use demo data
        export GENPRES_URL_ID=<id>     # or real spreadsheet id
*)

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "../bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"

open System
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.ZForm.Lib

// Note: GenForm types are accessible via the 'Informedica.GenForm.Lib' namespace
open Informedica.GenForm.Lib

Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Informedica.Utils.Lib.Env.loadDotEnv () |> ignore

let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")

// ===================================================================
// 1.  Load GenFORM dose rules
// ===================================================================

let provider: IResourceProvider =
    Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId

let genFormDoseRules = Api.getDoseRules provider

printfn "Loaded %d GenFORM dose rules" (genFormDoseRules |> Array.length)

// ===================================================================
// 2.  Adult-category detection
//
//     GenFORM stores PatientCategory.Age as MinMax with ValueUnit in
//     *days*. 18 years ≈ 6574 days.  A rule is 'adult' when its age
//     minimum is at or above that threshold OR when neither bound is
//     set (open-ended rule that covers all ages).
// ===================================================================

let adultAgeThresholdDays = 18.0 * 365.25   // ≈ 6574

let limitValueInDays (lim: Limit) =
    match lim with
    | Inclusive vu | Exclusive vu ->
        vu
        |> ValueUnit.convertTo Units.Time.day
        |> ValueUnit.getValue
        |> Array.tryHead
        |> Option.defaultValue 0.0

let isAdultPatientCategory (cat: Types.PatientCategory) =
    match cat.Age.Min with
    | Some lim -> limitValueInDays lim >= adultAgeThresholdDays
    | None     ->
        // No minimum age: adult if no maximum either (open-ended)
        cat.Age.Max |> Option.isNone

// ===================================================================
// 3.  Find combos that have no adult dose rule in GenFORM
// ===================================================================

let missingAdultCombos =
    genFormDoseRules
    |> Array.groupBy (fun dr -> dr.Generic, dr.Form, dr.Route)
    |> Array.filter (fun (_, rules) ->
        rules |> Array.exists (fun dr -> isAdultPatientCategory dr.PatientCategory) |> not
    )
    |> Array.map fst

printfn "\n%d (generic, form, route) combos lack an adult dose rule"
    (missingAdultCombos |> Array.length)

missingAdultCombos
|> Array.truncate 10
|> Array.iter (fun (gen, frm, rte) ->
    printfn "  • %s | %s | %s" gen frm rte
)

// ===================================================================
// 4.  Query G-Standard (ZForm) for a (generic, form, route) combo
// ===================================================================

let gstandConfig: CreateConfig =
    {
        GPKs        = []
        IsRate      = false
        SubstanceUnit = None
        TimeUnit    = None
    }

let queryGStand (gen: string) (frm: string) (rte: string) =
    GStand.createDoseRules gstandConfig None None None None gen frm rte
    |> Seq.toArray

// ===================================================================
// 5.  Map ZForm DoseType → GenFORM DoseType
//
//     ZForm encodes doseType in the Dosage fields:
//       - RateDosage with a set Norm/Abs → Continuous
//       - TotalDosage with non-empty frequency list → Discontinuous
//       - StartDosage with a set Norm → Once
//       - Otherwise → NoDoseType
// ===================================================================

let dosageIsRate (dosage: Dosage) =
    let rdr, _ = dosage.RateDosage
    rdr.Norm.Min |> Option.isSome
    || rdr.Norm.Max |> Option.isSome
    || (rdr.NormWeight |> fst |> fun mm -> mm.Min |> Option.isSome || mm.Max |> Option.isSome)

let dosageHasFrequency (dosage: Dosage) =
    let _, freq = dosage.TotalDosage
    freq.Frequencies |> List.isEmpty |> not

let zformDosageToGenFormDoseType (dosage: Dosage) : DoseType =
    if dosage |> dosageIsRate then
        DoseType.Continuous dosage.Name
    elif dosage |> dosageHasFrequency then
        DoseType.Discontinuous dosage.Name
    else
        match dosage.StartDosage.Norm.Min, dosage.StartDosage.Norm.Max with
        | Some _, _ | _, Some _ -> DoseType.Once dosage.Name
        | _ -> DoseType.NoDoseType

// ===================================================================
// 6.  Map ZForm.PatientCategory → GenFORM.PatientCategory
//
//     ZForm age is in months; GenFORM age is in days.
//     ZForm weight is in kg;  GenFORM weight is in grams.
//     Gender: ZForm.Undetermined → GenFORM.AnyGender
// ===================================================================

let convertLimitUnit (toUnit: Unit) (lim: Limit) =
    match lim with
    | Inclusive vu -> vu |> ValueUnit.convertTo toUnit |> Inclusive
    | Exclusive vu -> vu |> ValueUnit.convertTo toUnit |> Exclusive

let convertMinMaxUnit (toUnit: Unit) (mm: MinMax) =
    {
        Min = mm.Min |> Option.map (convertLimitUnit toUnit)
        Max = mm.Max |> Option.map (convertLimitUnit toUnit)
    }

let zformGenderToGenForm =
    function
    | Gender.Male          -> Types.Gender.Male
    | Gender.Female        -> Types.Gender.Female
    | Gender.Undetermined  -> Types.Gender.AnyGender

let zformPatCatToGenFormPatCat (zcat: PatientCategory) : Types.PatientCategory =
    {
        Location   = None
        Department = None
        Gender     = zcat.Gender |> zformGenderToGenForm
        // age: months → days
        Age        = zcat.Age    |> convertMinMaxUnit Units.Time.day
        // weight: kg → grams
        Weight     = zcat.Weight |> convertMinMaxUnit Units.Weight.gram
        BSA        = zcat.BSA    // already m²
        // ZForm has GestAge but no PMAge
        GestAge    = zcat.GestAge |> convertMinMaxUnit Units.Time.day
        PMAge      = MinMax.empty
        Access     = Types.AccessDevice.AnyAccess
    }

// ===================================================================
// 7.  Map ZForm.DoseRange → GenFORM.DoseLimit
//
//     For SingleDosage (per-administration):
//       DoseRange.Abs      → DoseLimit.Quantity
//       DoseRange.AbsWeight → DoseLimit.QuantityAdjust
//     For TotalDosage (per time period):
//       DoseRange.Abs      → DoseLimit.PerTime
//       DoseRange.AbsWeight → DoseLimit.PerTimeAdjust
// ===================================================================

let zformDoseRangeToSubstanceLimit
    (name: string)
    (doseUnit: Unit)
    (isTotalDose: bool)
    (dr: DoseRange)
    : Types.DoseLimit =
    let qty, qtyAdj =
        if isTotalDose then MinMax.empty, MinMax.empty
        else dr.Abs, dr.AbsWeight |> fst
    let perTime, perTimeAdj =
        if isTotalDose then dr.Abs, dr.AbsWeight |> fst
        else MinMax.empty, MinMax.empty
    {
        DoseLimitTarget  = Types.LimitTarget.SubstanceLimitTarget name
        AdjustUnit       = None
        DoseUnit         = doseUnit
        Quantity         = qty
        QuantityAdjust   = qtyAdj
        PerTime          = perTime
        PerTimeAdjust    = perTimeAdj
        Rate             = MinMax.empty
        RateAdjust       = MinMax.empty
    }

// ===================================================================
// 8.  Build a GenFORM DoseRule from one ZForm PatientDosage
//
//     Returns None when the dosage is purely continuous (rate-only),
//     because those rules are not meaningful without solution rules.
// ===================================================================

let patientDosageToGenFormDoseRule
    (zdr    : DoseRule)
    (indication : string)
    (route  : string)
    (form   : string)
    (pd     : PatientDosage)
    : Types.DoseRule option =

    // Prefer FormDosage if it has a name; otherwise first SubstanceDosage
    let leadDosage =
        if pd.FormDosage.Name |> String.notEmpty then pd.FormDosage
        elif pd.SubstanceDosages |> List.isEmpty |> not then pd.SubstanceDosages |> List.head
        else pd.FormDosage

    let doseType = leadDosage |> zformDosageToGenFormDoseType

    // Skip purely rate/continuous dosages
    match doseType with
    | DoseType.Continuous _ -> None
    | _ ->

    let patCat = pd.Patient |> zformPatCatToGenFormPatCat
    let isTotalDose = leadDosage |> dosageHasFrequency

    // Build SubstanceLimits from each SubstanceDosage
    let substanceLimits =
        pd.SubstanceDosages
        |> List.toArray
        |> Array.map (fun sd ->
            // Infer unit from the first non-empty MinMax limit
            let inferUnit (mm: MinMax) =
                match mm.Min, mm.Max with
                | Some (Inclusive vu), _ | Some (Exclusive vu), _ | _, Some (Inclusive vu) | _, Some (Exclusive vu) ->
                    Some (vu |> ValueUnit.getUnit)
                | _ -> None

            let doseUnit =
                [ sd.SingleDosage.Abs; sd.TotalDosage |> fst |> fun dr -> dr.Abs ]
                |> List.tryPick inferUnit
                |> Option.defaultValue Units.Mass.milliGram

            let dosage = if isTotalDose then sd.TotalDosage |> fst else sd.SingleDosage

            zformDoseRangeToSubstanceLimit sd.Name doseUnit isTotalDose dosage
        )

    // Extract frequencies from TotalDosage
    let frequencies =
        let _, freq = leadDosage.TotalDosage
        if freq.Frequencies |> List.isEmpty then None
        else
            freq.Frequencies
            |> List.toArray
            |> ValueUnit.withUnit freq.TimeUnit
            |> Some

    let scheduleText =
        match leadDosage.Rules with
        | GStandRule txt :: _ -> txt
        | PedFormRule txt :: _ -> txt
        | [] -> ""

    Some
        {
            Source           = "G-Standaard"
            Indication       = indication
            Generic          = zdr.Generic
            Form             = form
            Brand            = None
            Route            = route
            ScheduleText     = scheduleText
            PatientCategory  = patCat
            DoseType         = doseType
            AdjustUnit       = None
            Frequencies      = frequencies
            AdministrationTime = MinMax.empty
            IntervalTime     = MinMax.empty
            Duration         = MinMax.empty
            FormLimit        = None
            ComponentLimits  =
                [|
                    {
                        Name            = zdr.Generic
                        GPKs            = [||]
                        Limit           = None
                        Products        = [||]
                        SubstanceLimits = substanceLimits
                    }
                |]
            RenalRule        = None
        }

// ===================================================================
// 9.  Flatten one ZForm.DoseRule into GenFORM.DoseRule entries
// ===================================================================

let flattenZFormDoseRule (zdr: DoseRule) : Types.DoseRule seq =
    seq {
        for ind in zdr.IndicationsDosages do
            let indication =
                if ind.Indications |> List.isEmpty then ""
                else ind.Indications |> String.concat ", "

            for rt in ind.RouteDosages do
                for frm in rt.FormDosages do
                    let form =
                        if frm.Form |> List.isEmpty then ""
                        else frm.Form |> String.concat ", "

                    for pd in frm.PatientDosages do
                        match patientDosageToGenFormDoseRule zdr indication rt.Route form pd with
                        | Some dr -> yield dr
                        | None    -> ()
    }

// ===================================================================
// 10. Reconstitution guard
//
//     Skip a combo if any of its products need reconstitution and
//     no reconstitution rule exists for those GPKs.
//     (In this prototype we use a simplified check by generic name.)
//
//     TODO: for production, map generic → GPK list using ZIndex and
//     check provider.GetReconstitution() per GPK.
// ===================================================================

let reconstitutionRules = provider.GetReconstitution()

let genericNeedsReconstitution (generic: string) =
    // Reconstitution rules are keyed by GPK.
    // This simplified check looks for any rule whose GPK appears in
    // a product whose generic name matches.  The full implementation
    // should resolve GPK → generic via ZIndex.Product.
    // For now we conservatively return false (don't skip anything).
    false   // TODO: integrate GPK→generic lookup

// ===================================================================
// 11. Demo run — query G-Standard for the first few missing combos
// ===================================================================

printfn "\n=== Demo: G-Standard fallback rules for first 5 missing combos ==="

let demoRules =
    missingAdultCombos
    |> Array.truncate 5
    |> Array.collect (fun (gen, frm, rte) ->
        if genericNeedsReconstitution gen then
            printfn "\nSKIP %s (reconstitution guard)" gen
            [||]
        else
            let zformRules = queryGStand gen frm rte
            printfn "\n--- %s | %s | %s ---" gen frm rte
            printfn "  ZForm DoseRules found  : %d" (zformRules |> Array.length)

            let fallback =
                zformRules
                |> Array.collect (flattenZFormDoseRule >> Seq.toArray)

            printfn "  GenFORM rules generated: %d" (fallback |> Array.length)

            fallback
            |> Array.iter (fun dr ->
                let limStr lim = lim |> Limit.getValueUnit |> ValueUnit.toStringDecimalDutchShort
                let ageStr =
                    match dr.PatientCategory.Age.Min, dr.PatientCategory.Age.Max with
                    | None, None     -> "all ages"
                    | Some mn, None  -> $"≥ {mn |> limStr}"
                    | None, Some mx  -> $"< {mx |> limStr}"
                    | Some mn, Some mx -> $"{mn |> limStr} – {mx |> limStr}"

                printfn "    • %s | %s | age: %s"
                    dr.Indication
                    (dr.DoseType |> DoseType.toString)
                    ageStr
            )

            fallback
    )

printfn "\nTotal GenFORM fallback rules from G-Standard: %d" (demoRules |> Array.length)

// ===================================================================
// 12. Next steps for migration to source files
//
//     When the maintainer is happy with the prototype, the following
//     changes are needed in the source files:
//
//     a) Add `flattenZFormDoseRule` + supporting helpers to a new
//        module `Informedica.GenForm.Lib.GStandAdapter` (.fs).
//
//     b) In `Api.fs`, after `getDoseRules provider` loads the Google
//        Sheet rules, call:
//
//          let gstandFallbacks =
//              missingAdultCombos doseRules
//              |> Array.collect (fun (gen, frm, rte) ->
//                  GStand.createDoseRules gstandConfig None None None None gen frm rte
//                  |> Seq.collect flattenZFormDoseRule
//                  |> Seq.toArray
//              )
//          Array.append doseRules gstandFallbacks
//
//     c) Source = "G-Standaard" ensures the UI can show a badge and
//        link to the G-Standard monograph.
//
//     d) Performance note: GStand.createDoseRules hits the in-process
//        ZIndex cache, so calls are fast after the first load.
//        Batch all missing combos in a single pass to avoid repeated
//        cache warming.
// ===================================================================
