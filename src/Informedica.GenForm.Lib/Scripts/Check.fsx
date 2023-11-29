
#load "load.fsx"
#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../MinMax.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"


open System
open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib

module Dto = Informedica.ZForm.Lib.Dto
module ValueUnit = Informedica.GenUnits.Lib.ValueUnit
module Units = Informedica.GenUnits.Lib.Units


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

let mapRoute s =
    Mapping.routeMapping
    |> Array.tryFind(fun r -> r.Short |> String.equalsCapInsens s)
    |> Option.map (fun r -> r.Long)
    |> Option.defaultValue ""


// get all doserules from genform
let doseRules =
    DoseRule.get()


module ZForm =

    open Informedica.ZForm.Lib

    let config =
        {
            GPKs = []
            IsRate = false
            SubstanceUnit = None
            TimeUnit = None
        }

    let getRules gen rte =
        let rte =
            Mapping.routeMapping
            |> Array.tryFind (fun m -> m.Short |> String.equalsCapInsens rte)
            |> function
                | Some m -> m.Long
                | None   -> rte
        GStand.createDoseRules config None None None None gen "" rte


ZForm.getRules "abacavir" "or"


let toBr u lim =
    lim
    |> Informedica.GenCore.Lib.Ranges.Limit.getValueUnit
    |> ValueUnit.convertTo u
    |> ValueUnit.getValue
    |> Array.head


let filter
    (p1 : Informedica.ZForm.Lib.Types.PatientCategory)
    (p2 : Types.PatientCategory)
    =
    let toDays =
        Option.map (toBr Units.Time.day)

    let toGram =
        Option.map (toBr Units.Weight.gram)

    let ageMin, ageMax =
        p1.Age.Min |> toDays,
        p1.Age.Max |> toDays

    let wghtMin, wghtMax =
        p1.Weight.Min |> toGram,
        p1.Weight.Max |> toGram

    match p2.Age.Minimum, ageMax with
    | Some a1, Some a2 -> a1 <= a2
    | _ -> true
    &&
    match p2.Age.Maximum, ageMin with
    | Some a1, Some a2 -> a1 >= a2
    | _ -> true
    &&
    match p2.Weight.Minimum, wghtMax with
    | Some a1, Some a2 -> a1 <= a2
    | _ -> true
    &&
    match p2.Weight.Maximum, wghtMin with
    | Some a1, Some a2 -> a1 >= a2
    | _ -> true


let checkDoseLimit (range: Informedica.ZForm.Lib.Types.DoseRange) tur (dl : DoseLimit) tul =
    let du = dl.DoseUnit |> Units.fromString

    dl.PerTimeAdjust.Maximum
    |> Option.bind (fun br ->
        dl.DoseUnit
        |> Units.fromString
        |> Option.map (fun u ->
            u
            |> Units.per tul
            |> ValueUnit.withSingleValue br
        )
    )
    |> function
        | None -> true
        | Some vu ->
            let vu =
                let newU =
                    du.Value
                    |> Units.per tur
                vu |> ValueUnit.convertTo newU
            range.NormWeight
            |> fst
            |> Informedica.GenCore.Lib.Ranges.MinIncrMax.inRange vu ||
            (range.AbsWeight
            |> fst
            |> Informedica.GenCore.Lib.Ranges.MinIncrMax.inRange vu)


let checkDoseRule (dr: DoseRule) (rules: Informedica.ZForm.Lib.Types.Dosage seq) =
    let eqs = ValueUnit.eqs
    let dls =
        dr.DoseLimits
        |> Array.filter DoseRule.DoseLimit.isSubstanceLimit
    [
        for dl in dls do
            let rules =
                rules
                |> Seq.filter (fun r ->
                    let s =
                        match dl.DoseLimitTarget with
                        | SubstanceDoseLimitTarget s -> s
                        | _ -> ""
                    r.Name |> String.equalsCapInsens s
                )
            for r in rules do

                let d, f = r.TotalDosage

                let f1 =
                    f.Frequencies
                    |> Seq.toArray
                    |> ValueUnit.withUnit f.TimeUnit
                // check the frequency
                dr.FreqTimeUnit
                |> Units.fromString
                |> Option.map (ValueUnit.withValue dr.Frequencies)
                |> Option.map (fun vu ->
                    let v1 =
                        vu
                        |> ValueUnit.getBaseValue
                        |> Set.ofArray
                    f1
                    |> ValueUnit.getBaseValue
                    |> Set.ofArray
                    |> Set.isSubset v1 &&
                    checkDoseLimit d f.TimeUnit dl (vu |> ValueUnit.getUnit)
                )
                |> Option.defaultValue true
    ]
    |> List.exists id
//    |> List.map (Option.defaultValue false)


let checkPath = $"{__SOURCE_DIRECTORY__}/check.html"


doseRules
|> Array.skip 0
|> Array.take 100
|> Array.map (fun dr ->
    {|
        doseRule = dr
        zformRules =
            ZForm.getRules dr.Generic dr.Route
            |> Seq.collect (fun r ->
                r.IndicationsDosages
                |> Seq.collect (fun id ->
                    id.RouteDosages
                    |> Seq.collect (fun rd ->
                        rd.ShapeDosages
                        |> Seq.collect (fun sd ->
                            sd.PatientDosages
                            |> Seq.filter (fun p ->
                                filter p.Patient dr.PatientCategory
                            )
                            |> Seq.collect (fun pd ->
                                pd.SubstanceDosages
                            )
                        )
                    )

                )
            )
            |> Seq.distinct
    |}
)
|> Array.filter (fun r ->
    r.zformRules |> checkDoseRule r.doseRule
    |> not
)
|> Array.map (fun r ->
    [
        $"{[|r.doseRule|] |> DoseRule.Print.toMarkdown}\n## G-Standaard\n\n"
        let s =
            if r.zformRules |> Seq.isEmpty then "### Geen regels gevonden\n"
            else
                r.zformRules
                |> Seq.map (Informedica.ZForm.Lib.DoseRule.Dosage.toString false)
                |> Seq.sortBy String.toLower
                |> Seq.distinct
                |> Seq.mapi (sprintf "%i. %s")
                |> String.concat "\n"
        $"{s}"
    ]
    |> String.concat "\n"
)
|> String.concat "\n"
|> Informedica.ZForm.Lib.Markdown.toHtml
|> Informedica.Utils.Lib.File.writeTextToFile checkPath


