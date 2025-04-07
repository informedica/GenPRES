

#load "load.fsx"

open System
open MathNet.Numerics

let dataUrlId = "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)



#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"

#time


open Informedica.GenForm.Lib


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL


let headers =
    [
        "SortNo"
        "Source"
        "Generic"
        "Shape"
        "Brand"
        "Route"
        "GPKs"
        "Indication"
        "ScheduleText"
        "Dep"
        "Gender"
        "MinAge"
        "MaxAge"
        "MinWeight"
        "MaxWeight"
        "MinBSA"
        "MaxBSA"
        "MinGestAge"
        "MaxGestAge"
        "MinPMAge"
        "MaxPMAge"
        "DoseType"
        "DoseText"
        "Component"
        "UseGenericName"
        "Substance"
        "Freqs"
        "DoseUnit"
        "AdjustUnit"
        "FreqUnit"
        "RateUnit"
        "MinTime"
        "MaxTime"
        "TimeUnit"
        "MinInt"
        "MaxInt"
        "IntUnit"
        "MinDur"
        "MaxDur"
        "DurUnit"
        "MinQty"
        "MaxQty"
        "NormQtyAdj"
        "MinQtyAdj"
        "MaxQtyAdj"
        "MinPerTime"
        "MaxPerTime"
        "NormPerTimeAdj"
        "MinPerTimeAdj"
        "MaxPerTimeAdj"
        "MinRate"
        "MaxRate"
        "MinRateAdj"
        "MaxRateAdj"
    ]
    |> String.concat "\t"
    |> List.singleton


let distinctByDoseLimit (d : DoseRuleDetails) =
        d.DoseType,
        d.Substance |> String.isNullOrWhiteSpace,
        d.AdjustUnit |> String.isNullOrWhiteSpace,
        d.MinQty.IsSome,
        d.MaxQty.IsSome,
        d.MinQtyAdj.IsSome,
        d.MaxQtyAdj.IsSome,
        d.NormQtyAdj.IsSome,
        d.MinPerTime.IsSome,
        d.MaxPerTime.IsSome,
        d.NormPerTimeAdj.IsSome,
        d.MinPerTimeAdj.IsSome,
        d.MaxPerTimeAdj.IsSome,
        d.MinRate.IsSome,
        d.MaxRate.IsSome,
        d.MinRateAdj.IsSome,
        d.MaxRateAdj.IsSome


let dataToCsv distBy dataUrlId =
    let grouped =
        dataUrlId
        |> DoseRule.getDoseRuleDetails
        |> Array.groupBy DoseRule.mapToDoseRule
        |> Array.filter (fst >> Option.isSome)
        |> Array.map (fun (dr, details) -> dr.Value, details)

    let distinct =
        grouped
        |> Array.collect snd
        |> Array.filter (_.Products >> Array.isEmpty >> not)
        |> Array.filter (_.Shape >> String.notEmpty)
        |> Array.distinctBy distBy

    grouped
    |> Array.filter (snd >> Array.exists(fun d -> distinct |> Array.exists ((=) d)))
    |> Array.collect snd
    |> Array.toList
    |> List.mapi (fun i d ->
        let bigRatToStringList =
            Array.map BigRational.toDouble
            >> Array.map string
            >> String.concat ";"

        let bigRatOptToString =
            Option.map (BigRational.toDouble >> string)
            >> Option.defaultValue ""
        [
            $"{i}"
            d.Source
            d.Generic
            d.Shape
            d.Brand
            d.Route
            (d.GPKs |> String.concat ";")
            d.Indication
            d.ScheduleText
            d.Department
            (d.Gender |> Gender.toString)
            (d.MinAge |> bigRatOptToString)
            (d.MaxAge |> bigRatOptToString)
            (d.MinWeight |> bigRatOptToString)
            (d.MaxWeight |> bigRatOptToString)
            (d.MinBSA |> bigRatOptToString)
            (d.MaxBSA |> bigRatOptToString)
            (d.MinGestAge |> bigRatOptToString)
            (d.MaxGestAge |> bigRatOptToString)
            (d.MinPMAge |> bigRatOptToString)
            (d.MaxPMAge |> bigRatOptToString)
            d.DoseType
            d.DoseText
            d.Component
            ""
            d.Substance
            (d.Frequencies |> bigRatToStringList)
            d.DoseUnit
            d.AdjustUnit
            d.FreqUnit
            d.RateUnit
            (d.MinTime |> bigRatOptToString)
            (d.MaxTime |> bigRatOptToString)
            d.TimeUnit
            "" //d.MinInt
            "" //d.MaxInt
            "" //d.IntUnit
            (d.MinDur |> bigRatOptToString)
            (d.MaxDur |> bigRatOptToString)
            d.DurUnit
            (d.MinQty |> bigRatOptToString)
            (d.MaxQty |> bigRatOptToString)
            (d.NormQtyAdj |> bigRatOptToString)
            (d.MinQtyAdj |> bigRatOptToString)
            (d.MaxQtyAdj |> bigRatOptToString)
            (d.MinPerTime |> bigRatOptToString)
            (d.MaxPerTime |> bigRatOptToString)
            (d.NormPerTimeAdj |> bigRatOptToString)
            (d.MinPerTimeAdj |> bigRatOptToString)
            (d.MaxPerTimeAdj |> bigRatOptToString)
            (d.MinRate |> bigRatOptToString)
            (d.MaxRate |> bigRatOptToString)
            (d.MinRateAdj |> bigRatOptToString)
            (d.MaxRateAdj |> bigRatOptToString)

        ]
        |> String.concat "\t"
    )
    |> List.append headers


let csvData =
    "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
    |> dataToCsv distinctByDoseLimit

csvData
|> List.iter (printfn "%s")


DoseRule.get ()
|> Array.tryFind (_.Generic >> String.equalsCapInsens "nutrilon pepti 1")
|> Option.map _.ComponentLimits


Web.getDataUrlIdGenPres ()
|> DoseRule.get_
|> Array.tryFind (fst >> _.Generic >> String.equalsCapInsens "nutrilon pepti 1")
|> Option.map (fun (dr, rs) ->
    dr
    |> DoseRule.addDoseLimits rs
)
|> Option.get


DoseRule.get ()
|> Array.tryFind (_.Generic >> String.equalsCapInsens "noradrenaline")
//|> Option.map _.ComponentLimits