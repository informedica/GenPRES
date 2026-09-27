/// MedicationYaml.fsx
/// Prototype for YAML-compatible Medication.toString / fromString
///
/// This script shadows Informedica.GenOrder.Lib.Medication, replacing the
/// custom tab-indented serialization with a standard YAML document.
///
/// How to run:
///   cd src/Informedica.GenORDER.Lib/Scripts
///   dotnet fsi MedicationYaml.fsx
///
/// ── Migration note for maintainer ────────────────────────────────────────────
/// Once verified here, migrate into Medication.fs:
///   1. Replace Medication.toString with yamlToString.
///   2. Replace Parser.fromString with yamlFromString.
///   3. Wire YamlDotNet via Paket:
///        paket.dependencies  →  nuget YamlDotNet >= 15.3.0 lowest_matching: true
///        src/Informedica.GenORDER.Lib/paket.references  →  add line  YamlDotNet
///   4. Add a note in CONTRIBUTING.md under "External Dependencies":
///        YamlDotNet (>= 15.3.0) — standard YAML parsing in Medication.fromString
/// ─────────────────────────────────────────────────────────────────────────────

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: YamlDotNet, 15.3.0"

open System
open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open YamlDotNet.RepresentationModel
open Expecto
open Expecto.Flip


// ─── Local helpers ────────────────────────────────────────────────────────────

/// Wrap a string in double-quotes for YAML, escaping backslash and quotes.
let yamlQuote (s: string) =
    let s = if s = null then "" else s
    let esc = s.Replace("\\", "\\\\").Replace("\"", "\\\"")
    $"\"{esc}\""


// ─── Shadowed Medication module ───────────────────────────────────────────────

module Medication =

    open Informedica.GenOrder.Lib.Medication


    // ── re-use existing value-to-string helpers ──────────────────────────────

    let private mmToStr =
        MinMax.toString
            ValueUnit.toStringDecimalEngShortWithoutGroup
            ValueUnit.toStringDecimalEngShortWithoutGroup
            "min "
            "min "
            "max "
            "max "

    let private vuOptToStr =
        Option.map ValueUnit.toStringDecimalEngShortWithoutGroup
        >> Option.defaultValue ""

    let private limitOptToStr =
        Option.map (
            DoseLimit.toString
            >> List.map String.trim
            >> List.filter (String.isNullOrWhiteSpace >> not)
            >> String.concat ", "
        )
        >> Option.defaultValue ""

    let private slOptToStr =
        Option.map (SolutionLimit.toString >> String.concat " ")
        >> Option.defaultValue ""


    // ── YAML serialisation ────────────────────────────────────────────────────

    /// Emit "key: <quoted value>" at the given indentation level (spaces).
    let private line (indent: int) (key: string) (value: string) =
        let pad = String.replicate indent " "
        $"{pad}{key}: {yamlQuote value}"

    /// Serialize a Medication to a valid YAML string.
    /// Uses 2-space indentation; Components and Substances are block sequences.
    let yamlToString (med: Medication) : string =
        let buf = Collections.Generic.List<string>()
        let add s = buf.Add s

        add (line 0 "Id" med.Id)
        add (line 0 "Name" med.Name)
        add (line 0 "Quantity" (med.Quantity |> mmToStr))
        add (line 0 "Quantities" (med.Quantities |> vuOptToStr))
        add (line 0 "Route" med.Route)
        add (line 0 "OrderType" (sprintf "%A" med.OrderType))
        add (line 0 "Adjust" (med.Adjust |> vuOptToStr))
        add (line 0 "Frequencies" (med.Frequencies |> vuOptToStr))
        add (line 0 "Time" (med.Time |> mmToStr))
        add (line 0 "Dose" (med.Dose |> limitOptToStr))
        add (line 0 "Div" (med.Div |> Option.map BigRational.toString |> Option.defaultValue ""))
        add (line 0 "DoseCount" (med.DoseCount |> mmToStr))

        if med.Components.IsEmpty then
            add "Components: []"
        else
            add "Components:"

            for cmp in med.Components do
                // sequence item: first field prefixed with "  - "
                add $"  - Name: {yamlQuote cmp.Name}"
                add (line 4 "Form" cmp.Form)
                add (line 4 "Quantities" (cmp.Quantities |> vuOptToStr))
                add (line 4 "Divisible" (cmp.Divisible |> Option.map BigRational.toString |> Option.defaultValue ""))
                add (line 4 "Dose" (cmp.Dose |> limitOptToStr))
                add (line 4 "Solution" (cmp.Solution |> slOptToStr))

                if cmp.Substances.IsEmpty then
                    add "    Substances: []"
                else
                    add "    Substances:"

                    for sub in cmp.Substances do
                        add $"      - Name: {yamlQuote sub.Name}"
                        add (line 8 "Quantities" (sub.Quantities |> vuOptToStr))
                        add (line 8 "Concentrations" (sub.Concentrations |> vuOptToStr))
                        add (line 8 "Dose" (sub.Dose |> limitOptToStr))
                        add (line 8 "Solution" (sub.Solution |> slOptToStr))

        buf |> String.concat "\n"


    // ── YAML parsing ─────────────────────────────────────────────────────────

    /// Extract a scalar string from a YamlMappingNode by key.
    /// Returns "" when absent, null, "~", or "null".
    let private getStr (m: YamlMappingNode) (key: string) =
        match m.Children.TryGetValue(YamlScalarNode key) with
        | false, _ -> ""
        | true, node ->
            match node with
            | :? YamlScalarNode as s ->
                let v = s.Value
                if v = null || v = "~" || v = "null" then "" else v
            | _ -> ""

    /// Extract a sequence child node, or None.
    let private getSeq (m: YamlMappingNode) (key: string) : YamlSequenceNode option =
        match m.Children.TryGetValue(YamlScalarNode key) with
        | true, (:? YamlSequenceNode as s) -> Some s
        | _ -> None

    /// Parse a SubstanceItem from a YAML mapping node.
    let private parseSubstance (m: YamlMappingNode) : Result<SubstanceItem, string list> =
        let errs = Collections.Generic.List<string>()
        let g k = getStr m k

        let parseOpt label parser s =
            if s = "" then None
            else
                match parser s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"{label}: {e}"
                    None

        let name = g "Name"

        let quantities =
            g "Quantities"
            |> parseOpt "Quantities" Parser.parseValueUnitOpt

        let concentrations =
            g "Concentrations"
            |> parseOpt "Concentrations" Parser.parseValueUnitOpt

        let dose =
            g "Dose"
            |> parseOpt "Dose" (Parser.parseDoseLimitOpt SubstanceLimitTarget)

        let solution =
            g "Solution"
            |> parseOpt "Solution" Parser.parseSolutionLimitOpt

        if errs.Count = 0 then
            Ok { Name = name; Quantities = quantities; Concentrations = concentrations; Dose = dose; Solution = solution }
        else
            Error (errs |> Seq.toList)


    /// Parse a ProductComponent from a YAML mapping node.
    let private parseComponent (m: YamlMappingNode) : Result<ProductComponent, string list> =
        let errs = Collections.Generic.List<string>()
        let g k = getStr m k

        let parseOpt label parser s =
            if s = "" then None
            else
                match parser s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"{label}: {e}"
                    None

        let name = g "Name"
        let form = g "Form"

        let quantities =
            g "Quantities"
            |> parseOpt "Quantities" Parser.parseValueUnitOpt

        let divisible =
            match g "Divisible" with
            | "" -> None
            | s -> Parser.parseBigRationalOpt s

        let dose =
            g "Dose"
            |> parseOpt "Dose" (Parser.parseDoseLimitOpt ComponentLimitTarget)

        let solution =
            g "Solution"
            |> parseOpt "Solution" Parser.parseSolutionLimitOpt

        let substances =
            match getSeq m "Substances" with
            | None -> []
            | Some seq ->
                seq.Children
                |> Seq.choose (fun n ->
                    match n with
                    | :? YamlMappingNode as sm ->
                        match parseSubstance sm with
                        | Ok si -> Some si
                        | Error es ->
                            for e in es do errs.Add e
                            None
                    | _ -> None
                )
                |> Seq.toList

        if errs.Count = 0 then
            Ok
                { Name = name
                  Form = form
                  Quantities = quantities
                  Divisible = divisible
                  Dose = dose
                  Solution = solution
                  Substances = substances }
        else
            Error (errs |> Seq.toList)


    /// Parse a YAML string back to a Medication. Returns Ok or aggregated errors.
    let yamlFromString (s: string) : Result<Medication, string list> =
        let stream = YamlStream()

        let parseError =
            try
                stream.Load(new IO.StringReader(s))
                None
            with ex ->
                Some $"YAML parse error: {ex.Message}"

        match parseError with
        | Some e -> Error [ e ]
        | None ->

        if stream.Documents.Count = 0 then
            Error [ "Empty YAML document" ]
        else

        let root =
            match stream.Documents[0].RootNode with
            | :? YamlMappingNode as m -> Ok m
            | _ -> Error [ "YAML root must be a mapping node" ]

        match root with
        | Error e -> Error e
        | Ok root ->

        let errs = Collections.Generic.List<string>()
        let g k = getStr root k

        let parseField label parser s fallback =
            if s = "" then fallback
            else
                match parser s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"{label}: {e}"
                    fallback

        let id = g "Id"
        let name = g "Name"
        let route = g "Route"

        let orderType =
            parseField "OrderType" Parser.parseOrderType (g "OrderType") AnyOrder

        let quantity =
            parseField "Quantity" Parser.parseMinMax (g "Quantity") MinMax.empty

        let quantities =
            match g "Quantities" with
            | "" -> None
            | s ->
                match Parser.parseValueUnitOpt s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"Quantities: {e}"
                    None

        let adjust =
            match g "Adjust" with
            | "" -> None
            | s ->
                match Parser.parseValueUnitOpt s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"Adjust: {e}"
                    None

        let frequencies =
            match g "Frequencies" with
            | "" -> None
            | s ->
                match Parser.parseValueUnitOpt s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"Frequencies: {e}"
                    None

        let time =
            parseField "Time" Parser.parseMinMax (g "Time") MinMax.empty

        let dose =
            match g "Dose" with
            | "" -> None
            | s ->
                match Parser.parseDoseLimitOpt (fun _ -> OrderableLimitTarget) s with
                | Ok v -> v
                | Error e ->
                    errs.Add $"Dose: {e}"
                    None

        let div =
            match g "Div" with
            | "" -> None
            | s -> Parser.parseBigRationalOpt s

        let doseCount =
            parseField "DoseCount" Parser.parseMinMax (g "DoseCount") MinMax.empty

        let components =
            match getSeq root "Components" with
            | None -> []
            | Some seq ->
                seq.Children
                |> Seq.choose (fun n ->
                    match n with
                    | :? YamlMappingNode as m ->
                        match parseComponent m with
                        | Ok pc -> Some pc
                        | Error es ->
                            for e in es do errs.Add e
                            None
                    | _ -> None
                )
                |> Seq.toList

        if errs.Count = 0 then
            Ok
                { Id = id
                  Name = name
                  Components = components
                  Quantity = quantity
                  Quantities = quantities
                  Route = route
                  OrderType = orderType
                  Frequencies = frequencies
                  Time = time
                  Dose = dose
                  Div = div
                  DoseCount = doseCount
                  Adjust = adjust }
        else
            Error (errs |> Seq.toList)


// ─── Smoke test ───────────────────────────────────────────────────────────────

let pcmYaml = Scenarios.pcmSupp |> Medication.yamlToString
printfn "=== YAML output for pcmSupp ===\n%s\n" pcmYaml


// ─── Expecto round-trip tests ─────────────────────────────────────────────────

let roundTripTests =
    testList
        "Medication YAML round-trip"
        [
            let scenarios =
                [
                    "pcmSupp",        Scenarios.pcmSupp
                    "amfo",           Scenarios.amfo
                    "morfCont",       Scenarios.morfCont
                    "pcmDrink",       Scenarios.pcmDrink
                    "cotrim",         Scenarios.cotrim
                    "tpn",            Scenarios.tpn
                    "tpnComplete",    Scenarios.tpnComplete
                    "fullMedication", Scenarios.fullMedication
                ]

            for scenarioName, med in scenarios do
                test $"roundtrip: {scenarioName}" {
                    let yaml = med |> Medication.yamlToString

                    // must not contain tabs
                    yaml.Contains('\t')
                    |> Expect.isFalse "YAML output must not contain tab characters"

                    let result = yaml |> Medication.yamlFromString

                    match result with
                    | Error errs ->
                        let errs = errs |> String.concat "\n"
                        failtest $"Parse failed for %s{scenarioName}:\n%s{errs}"
                    | Ok parsed ->
                        parsed
                        |> Expect.equal $"round-trip: {scenarioName}" med
                }

            test "YAML parses cleanly with YamlDotNet" {
                let yaml = Scenarios.pcmSupp |> Medication.yamlToString
                let stream = YamlDotNet.RepresentationModel.YamlStream()

                try
                    stream.Load(new IO.StringReader(yaml))
                with e ->
                    failtest $"should load without exceptions: %s{e.Message}"
            }
        ]

runTestsWithCLIArgs [] [||] roundTripTests |> ignore
