// Plan docs/implementation-plans/1210-1211-emergency-and-continuous-rules.md, step 3 (#1210): the DoseRules rows
// for the entries of the emergency list. Reads the list (the fixture emergencylist.csv) and the live sheets
// DoseRules and SolutionRules of "GenPRES - NewSolutionRules" as CSV, and writes the new rows as TSV for review.
// No sheet is changed.
//
// Usage: dotnet fsi Issue1210EmergencyRows.fsx <directory with DoseRules.csv> <output directory>

open System
open System.IO
open System.Text


/// Splits CSV text into rows of fields; a quoted field may hold commas, quotes and line breaks.
let parseCsv (text: string) =
    let rows = ResizeArray<string[]>()
    let row = ResizeArray<string>()
    let field = StringBuilder()
    let mutable quoted = false
    let mutable i = 0

    let endField () =
        row.Add(field.ToString())
        field.Clear() |> ignore

    while i < text.Length do
        let c = text[i]

        if quoted then
            if c = '"' && i + 1 < text.Length && text[i + 1] = '"' then
                field.Append '"' |> ignore
                i <- i + 1
            elif c = '"' then
                quoted <- false
            else
                field.Append c |> ignore
        else
            match c with
            | '"' -> quoted <- true
            | ',' -> endField ()
            | '\n' ->
                endField ()
                rows.Add(row.ToArray())
                row.Clear()
            | '\r' -> ()
            | _ -> field.Append c |> ignore

        i <- i + 1

    if field.Length > 0 || row.Count > 0 then
        endField ()
        rows.Add(row.ToArray())

    rows |> Seq.toList


/// Reads a CSV file as a header and a list of maps from column name to value.
let readTable path =
    match path |> File.ReadAllText |> parseCsv with
    | header :: rows ->
        let rows =
            rows
            |> List.map (fun r ->
                header
                |> Array.mapi (fun i h -> h, (if i < r.Length then r[i].Trim() else ""))
                |> Map.ofArray
            )

        header, rows
    | [] -> [||], []


let inputDir, outputDir =
    match fsi.CommandLineArgs with
    | [| _; i; o |] -> i, o
    | _ -> __SOURCE_DIRECTORY__, __SOURCE_DIRECTORY__


let listPath =
    Path.Combine(__SOURCE_DIRECTORY__, "../../../tests/Informedica.GenPRES.Shared.Tests/fixtures/emergencylist.csv")


let doseRuleHeader, doseRules = readTable (Path.Combine(inputDir, "DoseRules.csv"))
let _, listRows = readTable listPath


/// An entry of the emergency list.
type Entry =
    {
        Hospital: string
        Category: string
        Name: string
        MinWeight: decimal
        MaxWeight: decimal
        Dose: decimal
        Min: decimal
        Max: decimal
        Conc: decimal
        Unit: string
        Remark: string
        Generic: string
        Route: string
        DoseType: string
    }


let dec (s: string) =
    match Decimal.TryParse(s.Trim(), Globalization.NumberStyles.Any, Globalization.CultureInfo.InvariantCulture) with
    | true, d -> d
    | false, _ -> 0m


let entries =
    listRows
    |> List.filter (fun r -> r["medication"] <> "")
    |> List.map (fun r ->
        {
            Hospital = r["hospital"]
            Category = r["indication"]
            Name = r["medication"]
            MinWeight = dec r["minWeight"]
            MaxWeight = dec r["maxWeight"]
            Dose = dec r["dose"]
            Min = dec r["min"]
            Max = dec r["max"]
            Conc = dec r["conc"]
            Unit = r["unit"]
            Remark = r["remark"]
            Generic = r["template-generic"]
            Route = r["template-route"]
            DoseType = r["template-dose-type"]
        }
    )


/// The generic as GenFORM names it, where the list writes it differently.
let genericOf (e: Entry) =
    match e.Generic with
    | "NaBic" -> "natriumwaterstofcarbonaat"
    | "fentytoine" -> "fenytoine"
    | "valproaat" -> "valproinezuur"
    | "intralipid" -> "Intralipid 20%"
    | "coffeine" -> "coffeine citraat"
    | g -> g


/// The dose unit as the existing rules write it.
let unitOf (e: Entry) =
    match e.Unit with
    | "E" -> "IE"
    | "mcg" -> "microg"
    | "gram" -> "g"
    | "ml" -> "mL"
    | u -> u


/// The routes of an entry: the routes in its name when it has no route or a route that its name contradicts,
/// else its route.
let routesOf (e: Entry) =
    let name = e.Name.ToLowerInvariant()

    let fromName =
        [
            if name.Contains "nas" then "NASAAL"
            if name.Contains "buc" then "OROMUCOSAAL"
            if name.Contains "/im" || name.EndsWith " im" then "INTRAMUSCULAIR"
        ]

    match e.Route, fromName with
    | _, (_ :: _ as rts) when e.Route = "" || not (rts |> List.contains (e.Route.ToUpperInvariant())) -> rts
    | r, _ -> [ r.ToUpperInvariant() ]


/// The dose type and the dose text: the first word of the dose type column, with discontinuous as once and once
/// when the column is empty, and the rest of the column as dose text.
let doseTypeOf (e: Entry) =
    match e.DoseType.Trim().Split(' ', 2) with
    | [| "" |] -> "once", ""
    | [| "discontinuous" |] -> "once", ""
    | [| "discontinuous"; text |] -> "once", text
    | [| dt |] -> dt, ""
    | [| dt; text |] -> dt, text
    | _ -> "once", ""


let str (d: decimal) =
    d.ToString("0.############", Globalization.CultureInfo.InvariantCulture)


let grams (kg: decimal) = if kg > 0m then str (kg * 1000m) else ""


let positive (d: decimal) = if d > 0m then str d else ""


/// The columns of the dose of an entry: a per kg dose when the dose is above 0, else a fixed dose.
let doseColumns (e: Entry) =
    if e.Dose > 0m then
        [
            "AdjustUnit", "kg"
            "MinQtyAdj", str e.Dose
            "MaxQtyAdj", str e.Dose
            "MinQty", positive e.Min
            "MaxQty", positive e.Max
        ]
    else
        [
            "AdjustUnit", ""
            "MinQtyAdj", ""
            "MaxQtyAdj", ""
            "MinQty", positive e.Min
            "MaxQty", positive e.Max
        ]


let col (r: Map<string, string>) c =
    r |> Map.tryFind c |> Option.defaultValue ""


/// The existing rules of a generic and route.
let rulesOf generic route =
    doseRules
    |> List.filter (fun r -> col r "Generic" = generic && col r "Route" = route)


/// The patient columns other than the weight that limit a rule.
let otherPatientColumns =
    [
        "Dep"
        "IsAdult"
        "Gender"
        "MinAge"
        "MaxAge"
        "MinBSA"
        "MaxBSA"
        "MinGestAge"
        "MaxGestAge"
        "MinPMAge"
        "MaxPMAge"
    ]


/// An existing rule matches an entry exactly when it has the same generic, route, dose unit and dose, and the
/// weight range of the entry as its only patient limit.
let matchesExactly (e: Entry) route (r: Map<string, string>) =
    let same c v = (col r c |> dec |> positive) = v

    col r "Generic" = genericOf e
    && col r "Route" = route
    && col r "DoseUnit" = unitOf e
    && (doseColumns e |> List.forall (fun (c, v) -> if c = "AdjustUnit" then col r c = v else same c v))
    && same "MinWeight" (grams e.MinWeight)
    && same "MaxWeight" (grams e.MaxWeight)
    && otherPatientColumns |> List.forall (fun c -> col r c = "" || (c = "IsAdult" && col r c = "FALSE"))


/// The substance of the dose: none for a dose in mL, which limits the component; else the substance of an
/// existing rule of the generic, route and dose unit, else the first generic of a combination.
let substanceOf (e: Entry) route =
    if unitOf e = "mL" then
        "", "volume"
    else
        rulesOf (genericOf e) route
        |> List.filter (fun r -> col r "DoseUnit" = unitOf e)
        |> List.map (fun r -> col r "Substance")
        |> List.filter ((<>) "")
        |> List.countBy id
        |> List.sortByDescending snd
        |> List.tryHead
        |> Option.map (fst >> fun s -> s, "rule")
        |> Option.defaultWith (fun () -> (genericOf e).Split('/')[0], "generic")


/// The administration time of a onceTimed entry: the time in the remark, such as "in 10-20 min", else the time of
/// an existing onceTimed rule of the generic and route.
let timeOf (e: Entry) route =
    let fromRule =
        rulesOf (genericOf e) route
        |> List.filter (fun r -> col r "DoseType" = "onceTimed" && col r "TimeUnit" <> "")
        |> List.map (fun r ->
            let unit =
                match (col r "TimeUnit").Trim() with
                | "minuut" -> "min"
                | u -> u

            col r "MinTime", col r "MaxTime", unit
        )
        |> List.countBy id
        |> List.sortByDescending snd
        |> List.tryHead
        |> Option.map (fst >> fun t -> t, "rule")

    let fromRemark () =
        let m =
            RegularExpressions.Regex.Match(e.Remark, @"in (\d+)(?:\s*-\s*(\d+))?\s*min")

        if m.Success then
            let min = m.Groups[1].Value
            let max = if m.Groups[2].Success then m.Groups[2].Value else min
            Some((min, max, "min"), "remark")
        else
            None

    fromRemark () |> Option.orElse fromRule


let report = ResizeArray<string>()


/// The magnesiumsulfaat bolus for an arrhythmia is left out; the entry given in 10 to 20 minutes takes its place.
let isLeftOut (e: Entry) =
    e.Generic = "magnesiumsulfaat" && e.Category = "anti-arrythmica" && e.Remark.Contains "bolus"


let newRows =
    [
        for e in entries |> List.filter (isLeftOut >> not) do
            let doseType, doseText = doseTypeOf e

            for route in routesOf e do
                match doseRules |> List.tryFind (matchesExactly e route) with
                | Some r ->
                    let rule = $"""%s{col r "Source"} %s{col r "Indication"}"""
                    report.Add($"MATCH\t%s{e.Hospital}\t%s{e.Category}\t%s{e.Name}\t%s{route}\t%s{rule}")
                | None ->
                    let substance, substanceFrom = substanceOf e route

                    let time =
                        if doseType = "onceTimed" then
                            match timeOf e route with
                            | Some(t, from) ->
                                report.Add($"TIME\t%s{e.Hospital}\t%s{e.Name}\t%s{route}\t%A{t} from %s{from}")
                                Some t
                            | None ->
                                report.Add($"NO TIME\t%s{e.Hospital}\t%s{e.Name}\t%s{route}")
                                None
                        else
                            None

                    if substanceFrom = "generic" then
                        report.Add($"SUBSTANCE\t%s{e.Hospital}\t%s{e.Name}\t%s{route}\t%s{substance} from the generic")

                    let minTime, maxTime, timeUnit = time |> Option.defaultValue ("", "", "")

                    [
                        "Source", "NDLST"
                        "Generic", genericOf e
                        "Route", route
                        "Indication", $"Noodlijst %s{e.Category}"
                        "Loc", e.Hospital
                        "MinWeight", grams e.MinWeight
                        "MaxWeight", grams e.MaxWeight
                        "DoseType", doseType
                        "DoseText", doseText
                        "Component", genericOf e
                        "Substance", substance
                        "DoseUnit", unitOf e
                        "MinTime", minTime
                        "MaxTime", maxTime
                        "TimeUnit", timeUnit
                        yield! doseColumns e
                    ]
                    |> Map.ofList
    ]
    // Entries that differ only in the concentration give the same dose rule; the concentration comes from the
    // products or from a solution rule.
    |> List.distinct


let toTsv (header: string[]) (rows: Map<string, string> list) =
    [
        header |> String.concat "\t"
        for r in rows do
            header |> Array.map (col r) |> String.concat "\t"
    ]


Directory.CreateDirectory outputDir |> ignore
File.WriteAllLines(Path.Combine(outputDir, "DoseRules-noodlijst.tsv"), toTsv doseRuleHeader newRows)
File.WriteAllLines(Path.Combine(outputDir, "report.tsv"), report)

printfn $"entries: %i{entries.Length}"
printfn $"""exact matches: %i{report |> Seq.filter (fun s -> s.StartsWith "MATCH") |> Seq.length}"""
printfn $"new rows: %i{newRows.Length}"

for line in report do
    printfn $"%s{line}"
