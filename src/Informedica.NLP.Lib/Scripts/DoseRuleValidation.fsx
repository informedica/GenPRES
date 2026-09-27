/// NLP dose rule extraction validation.
///
/// Validates the structured `DoseRuleExtracted` output from
/// `DoseRuleExtract.fsx` against the GenFORM formulary.
///
/// Validation steps:
///   1. Generic name (rule level): must match a known generic in the formulary
///   2. Route (rule level): must match a known route mapping (Long or Short)
///   3. AdjustUnit (per dose-limit): must be "kg", "m2", or empty
///   4. DoseType (per dose type): must be one of the five valid dose types
///   5. Unit structure (per dose-limit): compound units must be complete
///       - MinQtyAdj / MaxQtyAdj    : doseUnit / adjustUnit
///       - MinPerTime / MaxPerTime  : doseUnit / freqUnit (from dose type)
///       - MinPerTimeAdj / MaxPerTimeAdj : doseUnit / adjustUnit / freqUnit
///       - MinRate / MaxRate        : doseUnit / rateUnit
///       - MinRateAdj / MaxRateAdj  : doseUnit / adjustUnit / rateUnit
///
/// Usage:
///   let validation = extracted |> Validation.validate routeMappings genericNames
///   Validation.printResult validation
///
/// Mirrors the hierarchical shape in `DoseRuleExtract.fsx`.

#r "nuget: FSharpPlus"
#r "nuget: Newtonsoft.Json"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"

// All GenFORM dependencies are co-located in the GenFORM bin output directory
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../../Informedica.GenFORM.Lib/bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"

open System
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources


/// Hierarchical mirror of `Schema.DoseLimit` / `DoseType` /
/// `DoseRuleExtracted` in `DoseRuleExtract.fsx`. Keep in sync.
type DoseLimit =
    {|
        ``component``: string
        substance: string
        doseUnit: string
        adjustUnit: string
        rateUnit: string
        minQty: Nullable<float>
        maxQty: Nullable<float>
        minQtyAdj: Nullable<float>
        maxQtyAdj: Nullable<float>
        minPerTime: Nullable<float>
        maxPerTime: Nullable<float>
        minPerTimeAdj: Nullable<float>
        maxPerTimeAdj: Nullable<float>
        minRate: Nullable<float>
        maxRate: Nullable<float>
        minRateAdj: Nullable<float>
        maxRateAdj: Nullable<float>
    |}


type DoseType =
    {|
        doseType: string
        doseText: string
        frequencies: int[]
        freqUnit: string
        minTime: Nullable<float>
        maxTime: Nullable<float>
        timeUnit: string
        minInterval: Nullable<float>
        maxInterval: Nullable<float>
        intervalUnit: string
        minDuration: Nullable<float>
        maxDuration: Nullable<float>
        durUnit: string
        doseLimits: DoseLimit[]
    |}


type DoseRuleExtracted =
    {|
        source: string
        sourceText: string
        generic: string
        form: string
        brand: string
        gpks: string[]
        indication: string
        route: string
        department: string
        scheduleText: string
        gender: string
        minAge: Nullable<float>
        maxAge: Nullable<float>
        minWeight: Nullable<float>
        maxWeight: Nullable<float>
        minBSA: Nullable<float>
        maxBSA: Nullable<float>
        minGestAge: Nullable<float>
        maxGestAge: Nullable<float>
        minPMAge: Nullable<float>
        maxPMAge: Nullable<float>
        doseTypes: DoseType[]
    |}


/// Represents the outcome of a single field validation.
type FieldValidation =
    | Valid of fieldName: string * value: string
    | Warning of fieldName: string * value: string * message: string
    | Invalid of fieldName: string * value: string * reason: string


/// Represents the full validation result for a `DoseRuleExtracted` record.
type ValidationResult =
    {
        Extracted: DoseRuleExtracted
        Fields: FieldValidation list
    }

    member this.IsValid =
        this.Fields
        |> List.forall (
            function
            | Invalid _ -> false
            | _ -> true
        )

    member this.Errors =
        this.Fields
        |> List.choose (
            function
            | Invalid(f, v, r) -> Some(f, v, r)
            | _ -> None
        )

    member this.Warnings =
        this.Fields
        |> List.choose (
            function
            | Warning(f, v, m) -> Some(f, v, m)
            | _ -> None
        )


module Validation =

    let private equalsCI (a: string) (b: string) =
        String.Compare(a, b, StringComparison.OrdinalIgnoreCase) = 0


    /// Validate the generic name against a list of known generic names.
    let validateGeneric (genericNames: string list) (extracted: DoseRuleExtracted) =
        if extracted.generic |> String.isNullOrWhiteSpace then
            Invalid("generic", extracted.generic, "Generic name is empty")
        elif genericNames |> List.exists (equalsCI extracted.generic) then
            Valid("generic", extracted.generic)
        else
            let partialMatch =
                genericNames
                |> List.tryFind (fun g ->
                    g |> String.containsCapsInsens extracted.generic
                    || extracted.generic |> String.containsCapsInsens g
                )

            match partialMatch with
            | Some candidate ->
                Warning(
                    "generic",
                    extracted.generic,
                    $"No exact match found; closest formulary entry is '{candidate}'"
                )
            | None ->
                Invalid(
                    "generic",
                    extracted.generic,
                    $"'{extracted.generic}' not found in formulary; check spelling or add to formulary"
                )


    /// Validate the route against the formulary route mappings.
    let validateRoute (routeMappings: RouteMapping array) (extracted: DoseRuleExtracted) =
        if extracted.route |> String.isNullOrWhiteSpace then
            Warning("route", extracted.route, "Route is empty; a route is expected for most dose rules")
        else
            let matched =
                routeMappings
                |> Array.exists (fun rm ->
                    equalsCI rm.Long extracted.route || equalsCI rm.Short extracted.route
                )

            if matched then
                Valid("route", extracted.route)
            else
                let knownRoutes = routeMappings |> Array.map _.Long |> String.concat ", "

                Invalid(
                    "route",
                    extracted.route,
                    $"Route '{extracted.route}' not found in route mappings; valid routes are: {knownRoutes}"
                )


    /// Validate the dose type label from a `DoseType` entry.
    let validateDoseType (fieldName: string) (doseType: string) =
        let valid =
            [
                "once"
                "onceTimed"
                "discontinuous"
                "timed"
                "continuous"
                ""
            ]

        if valid |> List.contains doseType then
            Valid(fieldName, doseType)
        else
            let validList = valid |> List.filter ((<>) "") |> String.concat ", "

            Invalid(
                fieldName,
                doseType,
                $"'{doseType}' is not a valid DoseType; must be one of: {validList}"
            )


    /// Validate the adjust unit from a `DoseLimit` entry.
    let validateAdjustUnit (fieldName: string) (adjustUnit: string) =
        let valid = [ "kg"; "m2"; "" ]

        if valid |> List.contains adjustUnit then
            Valid(fieldName, adjustUnit)
        else
            Invalid(
                fieldName,
                adjustUnit,
                $"'{adjustUnit}' is not a valid adjustUnit; must be 'kg', 'm2', or empty"
            )


    /// Validate that an age value (in days) is plausible.
    let validateAge (fieldName: string) (n: Nullable<float>) =
        if not n.HasValue then
            Valid(fieldName, "null")
        elif n.Value < 0.0 then
            Invalid(fieldName, string n.Value, $"{fieldName} cannot be negative")
        elif n.Value > 36500.0 then
            Warning(fieldName, string n.Value, $"{fieldName} = {n.Value} days seems very large (>100 years)")
        else
            Valid(fieldName, string n.Value)


    /// Validate that a weight value (in grams) is plausible.
    let validateWeight (fieldName: string) (n: Nullable<float>) =
        if not n.HasValue then
            Valid(fieldName, "null")
        elif n.Value < 0.0 then
            Invalid(fieldName, string n.Value, $"{fieldName} cannot be negative")
        elif n.Value > 200000.0 then
            Warning(fieldName, string n.Value, $"{fieldName} = {n.Value}g seems very large (>200 kg)")
        else
            Valid(fieldName, string n.Value)


    /// Validate that a BSA value (in m2) is plausible.
    let validateBSA (fieldName: string) (n: Nullable<float>) =
        if not n.HasValue then
            Valid(fieldName, "null")
        elif n.Value < 0.0 then
            Invalid(fieldName, string n.Value, $"{fieldName} cannot be negative")
        elif n.Value > 3.0 then
            Warning(fieldName, string n.Value, $"{fieldName} = {n.Value} m² seems very large (>3 m²)")
        else
            Valid(fieldName, string n.Value)


    /// Validate that the unit structure for a compound unit field is correct.
    /// For example, PerTimeAdj should be: doseUnit / adjustUnit / freqUnit.
    let validateUnitStructure (fieldName: string) (expectedParts: string list) =
        let nonEmpty s = s |> String.isNullOrWhiteSpace |> not
        let nonEmptyParts = expectedParts |> List.filter nonEmpty

        if nonEmptyParts |> List.isEmpty then
            Valid(fieldName, "(unit parts all empty — skipped)")
        elif expectedParts |> List.forall nonEmpty then
            let composed = expectedParts |> String.concat "/"
            Valid(fieldName, $"unit structure ok: {composed}")
        else
            let missing =
                expectedParts |> List.filter String.isNullOrWhiteSpace |> List.length

            let parts = expectedParts |> String.concat "/"
            Warning(fieldName, "", $"Unit structure incomplete for {fieldName}: {parts}; missing: {missing} part(s)")


    /// Validate the unit structures for all dose-limit fields that carry
    /// a numeric range. `freqUnit` is taken from the owning dose type.
    let validateDoseLimitUnits (pathPrefix: string) (dt: DoseType) (dl: DoseLimit) =
        [
            // QtyAdj: doseUnit / adjustUnit
            if dl.minQtyAdj.HasValue || dl.maxQtyAdj.HasValue then
                yield
                    validateUnitStructure
                        $"{pathPrefix}.QtyAdj units"
                        [ dl.doseUnit; dl.adjustUnit ]

            // PerTime: doseUnit / freqUnit
            if dl.minPerTime.HasValue || dl.maxPerTime.HasValue then
                yield
                    validateUnitStructure
                        $"{pathPrefix}.PerTime units"
                        [ dl.doseUnit; dt.freqUnit ]

            // PerTimeAdj: doseUnit / adjustUnit / freqUnit
            if dl.minPerTimeAdj.HasValue || dl.maxPerTimeAdj.HasValue then
                yield
                    validateUnitStructure
                        $"{pathPrefix}.PerTimeAdj units"
                        [
                            dl.doseUnit
                            dl.adjustUnit
                            dt.freqUnit
                        ]

            // Rate: doseUnit / rateUnit
            if dl.minRate.HasValue || dl.maxRate.HasValue then
                yield
                    validateUnitStructure
                        $"{pathPrefix}.Rate units"
                        [ dl.doseUnit; dl.rateUnit ]

            // RateAdj: doseUnit / adjustUnit / rateUnit
            if dl.minRateAdj.HasValue || dl.maxRateAdj.HasValue then
                yield
                    validateUnitStructure
                        $"{pathPrefix}.RateAdj units"
                        [
                            dl.doseUnit
                            dl.adjustUnit
                            dl.rateUnit
                        ]
        ]


    /// Validate min ≤ max for a numeric range pair.
    let validateMinMax (fieldBase: string) (mn: Nullable<float>) (mx: Nullable<float>) =
        match mn.HasValue, mx.HasValue with
        | false, false -> []
        | true, false -> [ Valid($"{fieldBase}Range", $"min={mn.Value} (no max)") ]
        | false, true -> [ Valid($"{fieldBase}Range", $"(no min) max={mx.Value}") ]
        | true, true ->
            if mn.Value <= mx.Value then
                [
                    Valid($"{fieldBase}Range", $"min={mn.Value} max={mx.Value}")
                ]
            else
                [
                    Invalid($"{fieldBase}Range", $"{mn.Value}–{mx.Value}", $"min ({mn.Value}) > max ({mx.Value})")
                ]


    /// Run all validations on an extracted dose rule.
    let validate
        (routeMappings: RouteMapping array)
        (genericNames: string list)
        (extracted: DoseRuleExtracted)
        : ValidationResult
        =

        let doseTypes =
            if isNull extracted.doseTypes then [||] else extracted.doseTypes

        let ruleLevelFields =
            [
                yield validateGeneric genericNames extracted
                yield validateRoute routeMappings extracted

                // Patient dimensions
                yield validateAge "minAge" extracted.minAge
                yield validateAge "maxAge" extracted.maxAge
                yield! validateMinMax "age" extracted.minAge extracted.maxAge

                yield validateWeight "minWeight" extracted.minWeight
                yield validateWeight "maxWeight" extracted.maxWeight
                yield! validateMinMax "weight" extracted.minWeight extracted.maxWeight

                yield validateBSA "minBSA" extracted.minBSA
                yield validateBSA "maxBSA" extracted.maxBSA
                yield! validateMinMax "BSA" extracted.minBSA extracted.maxBSA

                yield validateAge "minGestAge" extracted.minGestAge
                yield validateAge "maxGestAge" extracted.maxGestAge
                yield! validateMinMax "gestAge" extracted.minGestAge extracted.maxGestAge

                yield validateAge "minPMAge" extracted.minPMAge
                yield validateAge "maxPMAge" extracted.maxPMAge
                yield! validateMinMax "PMAge" extracted.minPMAge extracted.maxPMAge

                if doseTypes.Length = 0 then
                    yield Invalid("doseTypes", "", "No dose types present")
            ]

        let doseTypeFields =
            [
                for j in 0 .. doseTypes.Length - 1 do
                    let dt = doseTypes[j]
                    let dtPath = $"doseTypes[{j}]"

                    yield validateDoseType $"{dtPath}.doseType" dt.doseType
                    yield! validateMinMax $"{dtPath}.time" dt.minTime dt.maxTime
                    yield! validateMinMax $"{dtPath}.interval" dt.minInterval dt.maxInterval
                    yield! validateMinMax $"{dtPath}.duration" dt.minDuration dt.maxDuration

                    let limits =
                        if isNull dt.doseLimits then [||] else dt.doseLimits

                    if limits.Length = 0 then
                        yield Invalid($"{dtPath}.doseLimits", "", "No dose limits present")

                    for k in 0 .. limits.Length - 1 do
                        let dl = limits[k]
                        let dlPath = $"{dtPath}.doseLimits[{k}]"

                        yield validateAdjustUnit $"{dlPath}.adjustUnit" dl.adjustUnit
                        yield! validateMinMax $"{dlPath}.qty" dl.minQty dl.maxQty
                        yield! validateMinMax $"{dlPath}.qtyAdj" dl.minQtyAdj dl.maxQtyAdj
                        yield! validateMinMax $"{dlPath}.perTime" dl.minPerTime dl.maxPerTime
                        yield! validateMinMax $"{dlPath}.perTimeAdj" dl.minPerTimeAdj dl.maxPerTimeAdj
                        yield! validateMinMax $"{dlPath}.rate" dl.minRate dl.maxRate
                        yield! validateMinMax $"{dlPath}.rateAdj" dl.minRateAdj dl.maxRateAdj
                        yield! validateDoseLimitUnits dlPath dt dl
            ]

        {
            Extracted = extracted
            Fields = ruleLevelFields @ doseTypeFields
        }


    /// Print the validation result to stdout.
    let printResult (result: ValidationResult) =
        let status = if result.IsValid then "✓ VALID" else "✗ INVALID"
        printfn $"\n## Validation Result: {status}\n"

        printfn "### Generic: %s | Route: %s" result.Extracted.generic result.Extracted.route

        if result.Errors |> List.isEmpty |> not then
            printfn "\n#### Errors:"

            for (f, v, r) in result.Errors do
                printfn "  ✗ [%s] '%s' → %s" f v r

        if result.Warnings |> List.isEmpty |> not then
            printfn "\n#### Warnings:"

            for (f, v, m) in result.Warnings do
                printfn "  ⚠ [%s] '%s' → %s" f v m

        if result.Errors |> List.isEmpty && result.Warnings |> List.isEmpty then
            printfn "  All checks passed."

        printfn ""


/// -------------------------------------------------------
/// Formulary loading helpers
/// -------------------------------------------------------

module Formulary =

    /// Load a formulary provider using the GENPRES_URL_ID environment variable.
    let loadProvider () =
        Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
        let dataUrlId = System.Environment.GetEnvironmentVariable("GENPRES_URL_ID")

        Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


    /// Extract the sorted list of unique generic names from the formulary dose rules.
    let getGenericNames (provider: IResourceProvider) : string list =
        provider.GetDoseRules()
        |> Array.map (_.Generic >> Generic.toString)
        |> Array.distinct
        |> Array.sort
        |> Array.toList


    /// Get the route mappings from the formulary.
    let getRouteMappings (provider: IResourceProvider) : RouteMapping array =
        provider.GetRouteMappings()


/// -------------------------------------------------------
/// Example usage — uncomment and adapt when running interactively
/// -------------------------------------------------------

(*
open Informedica.Utils.Lib

// Set up environment
let dataPath = __SOURCE_DIRECTORY__ |> Path.combineWith "../../../"
System.Environment.CurrentDirectory <- dataPath

// Load formulary
let provider = Formulary.loadProvider ()
let genericNames = Formulary.getGenericNames provider
let routeMappings = Formulary.getRouteMappings provider

printfn $"Loaded {genericNames |> List.length} generics and {routeMappings |> Array.length} route mappings"

// Example: validate a hypothetical extracted result
let exampleDoseLimit: DoseLimit =
    {|
        ``component`` = ""
        substance = "acetylsalicylzuur"
        doseUnit = "mg"
        adjustUnit = "kg"
        rateUnit = ""
        minQty = Nullable()
        maxQty = Nullable()
        minQtyAdj = Nullable 30.0
        maxQtyAdj = Nullable 50.0
        minPerTime = Nullable()
        maxPerTime = Nullable 3000.0
        minPerTimeAdj = Nullable()
        maxPerTimeAdj = Nullable()
        minRate = Nullable()
        maxRate = Nullable()
        minRateAdj = Nullable()
        maxRateAdj = Nullable()
    |}

let exampleDoseType: DoseType =
    {|
        doseType = "discontinuous"
        doseText = ""
        frequencies = [| 3; 4 |]
        freqUnit = "dag"
        minTime = Nullable()
        maxTime = Nullable()
        timeUnit = ""
        minInterval = Nullable()
        maxInterval = Nullable()
        intervalUnit = ""
        minDuration = Nullable()
        maxDuration = Nullable()
        durUnit = ""
        doseLimits = [| exampleDoseLimit |]
    |}

let exampleExtracted: DoseRuleExtracted =
    {|
        source = "Kinderformularium"
        sourceText = "acetylsalicylzuur 1 maand tot 18 jaar 30-50 mg/kg/dag in 3-4 doses"
        generic = "acetylsalicylzuur"
        form = "tablet"
        brand = ""
        gpks = [||]
        indication = ""
        route = "ORAAL"
        department = ""
        scheduleText = "30-50 mg/kg/dag in 3-4 doses; max 3000 mg/dag"
        gender = ""
        minAge = Nullable 30.0
        maxAge = Nullable 6570.0
        minWeight = Nullable()
        maxWeight = Nullable()
        minBSA = Nullable()
        maxBSA = Nullable()
        minGestAge = Nullable()
        maxGestAge = Nullable()
        minPMAge = Nullable()
        maxPMAge = Nullable()
        doseTypes = [| exampleDoseType |]
    |}

let validationResult =
    exampleExtracted
    |> Validation.validate routeMappings genericNames

Validation.printResult validationResult
*)
