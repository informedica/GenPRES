

#time

#load "load.fsx"

open System
let dataUrlId = "1yn6UC1OMJ0A2wAyX3r0AA2qlKJ7vEAB6OO0DjneiknE"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)


#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../LimitTarget.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib


Product.get ()
|> Array.filter (fun p ->
    p.Generic = "abacavir" &&
    p.Routes |> Array.exists (String.equalsCapInsens "oraal")
)



module DoseRule =

    open DoseRule

    open System
    open MathNet.Numerics

    open FSharp.Data
    open FSharp.Data.JsonExtensions

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Utils


    let addShapeLimits (dr : DoseRule) =
        let prods =
            dr.ComponentLimits
            |> Array.collect _.Products

        let droplets =
            prods
            |> Array.filter (fun p ->
                p.Shape |> String.containsCapsInsens "druppel"
            )
            |> Array.choose _.Divisible
            |> Array.distinct
            |> Array.tryExactlyOne

        let setDroplet vu =
            let v, u = vu |> ValueUnit.get
            match droplets with
            | None -> vu
            | Some m ->
                u
                |> Units.Volume.dropletSetDropsPerMl m
                |> ValueUnit.withValue v

        if dr.Shape |> String.isNullOrWhiteSpace then dr
        else
            prods
            |> Array.map _.ShapeUnit
            |> Array.tryExactlyOne
            |> Option.defaultValue NoUnit
            |> Mapping.filterRouteShapeUnit dr.Route dr.Shape
            |> Array.map (fun rsu ->
                { DoseLimit.limit with
                    DoseLimitTarget = dr.Shape |> ShapeLimitTarget
                    Quantity =
                        {
                            Min = rsu.MinDoseQty |> Option.map Limit.Inclusive
                            Max = rsu.MaxDoseQty |> Option.map Limit.Inclusive
                        }
                    QuantityAdjust =
                        {
                            Min = rsu.MinDoseQtyPerKg |> Option.map Limit.Inclusive
                            Max = rsu.MaxDoseQtyPerKg |> Option.map Limit.Inclusive
                        }
                }
                |> fun dl ->
                    if droplets |> Option.isNone then dl
                    else
                        { dl with
                            DoseUnit =
                                droplets
                                |> Option.map Units.Volume.dropletWithDropsPerMl
                                |> Option.defaultValue rsu.DoseUnit
                            Quantity =
                                {
                                    Min =
                                        dl.Quantity.Min
                                        |> Option.map (
                                            Limit.apply
                                                setDroplet
                                                setDroplet
                                        )
                                    Max =
                                        dl.Quantity.Max
                                        |> Option.map (
                                            Limit.apply
                                                setDroplet
                                                setDroplet
                                        )
                                }

                        }
            )
            |> Array.distinct
            |> Array.tryExactlyOne
            |> function
                | None -> dr
                | Some shapeLimit ->
                    { dr with ShapeLimit = Some shapeLimit }


let dr =
    DoseRule.get ()
    |> DoseRule.filter
        { Filter.doseFilter with
            Patient =
                { Patient.patient with
                    Locations =  [ CVL ]
                    Department = Some "ICK"
                    Age =
                        Units.Time.year
                        |> ValueUnit.singleWithValue 10N
                        |> Some
                    Weight =
                      Units.Weight.kiloGram
                      |> ValueUnit.singleWithValue (40N)
                      |> Some
                }
            Generic = Some "abacavir"
            Shape = Some "drank"
            Route = Some "oraal"
        }
    |> Array.head

dr
|> DoseRule.addShapeLimits

Mapping.filterRouteShapeUnit dr.Route dr.Shape NoUnit


SolutionRule.get ()
|> Array.filter (fun sr ->
    sr.Generic = "amikacine" &&
    sr.Route =  "intraveneus" &&
    sr.Department = Some "ICK"
)


let pr =
    { Filter.doseFilter with
        Generic = Some "amikacine"
        Route = Some "intraveneus"
        Shape = Some "injectievloeistof"
        Patient =
            { Patient.patient with
                Locations =  [ CVL ]
                Department = Some "ICK"
                Age =
                    Units.Time.year
                    |> ValueUnit.singleWithValue 10N
                    |> Some
                Weight =
                  Units.Weight.kiloGram
                  |> ValueUnit.singleWithValue (40N)
                  |> Some
//                RenalFunction = EGFR(Some 5, Some 5) |> Some
            }
    }
    |> PrescriptionRule.filter


{ Patient.patient with
    Locations =  [ CVL ]
    Department = Some "ICK"
    Age =
        Units.Time.year
        |> ValueUnit.singleWithValue 6N
        |> Some
    Weight =
      Units.Weight.kiloGram
      |> ValueUnit.singleWithValue (22N)
      |> Some
//                RenalFunction = EGFR(Some 5, Some 5) |> Some
}
|> PrescriptionRule.get
|> Array.last
|> fun pr -> pr.DoseRule.DoseLimits |> Array.toList

pr[0].DoseRule.DoseLimits |> Array.length
pr[0].RenalRules

let printAllDoseRules () =
    let rs =
        Filter.doseFilter
        |> PrescriptionRule.filter
        |> Array.map _.DoseRule

    let gs (rs : DoseRule[]) =
        rs
        |> Array.map _.Generic
        |> Array.distinct

    DoseRule.Print.printGenerics gs rs


printAllDoseRules ()
|> String.concat "\n\n  ---\n"
|> Informedica.ZForm.Lib.Markdown.toHtml
|> File.writeTextToFile "doserules.html"


SolutionRule.get ()
|> Array.filter (fun sr -> sr.Generic = "adrenaline")
|> SolutionRule.Print.toMarkdown ""


{ Patient.patient with
    Age =
        Units.Time.year
        |> ValueUnit.singleWithValue 16N
        |> Some
}
|> PrescriptionRule.get
|> Array.filter (_.SolutionRules >> Array.isEmpty >> not)
|> Array.item 1
|> fun pr ->
    pr.SolutionRules
    |> SolutionRule.Print.toMarkdown "\n"


DoseRule.get ()
|> DoseRule.filter
    { Filter.doseFilter with
        Route = Some "oraal"
    }


Web.getDataFromSheet "" "DoseRules"
|> fun data ->
    let getColumn =
        data
        |> Array.head
        |> Csv.getStringColumn

    data
    |> Array.tail
    |> Array.take 10
    |> Array.map (fun r ->
        let get = getColumn r
        let toBrOpt = BigRational.toBrs >> Array.tryHead

        printfn $"{r[22]}"
        printfn $"""{get "Generic"}: {get "Freqs"}"""
    )


DoseRule.get ()
|> Array.filter (fun dr ->
    dr.Route |> String.equalsCapInsens "rectaal"
)