
#load "load.fsx"
#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"


open System
open MathNet.Numerics
open Informedica.GenCore.Lib.Ranges
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib


module GStand = Informedica.ZForm.Lib.GStand


Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

let mapRoute s =
    Mapping.routeMapping
    |> Array.tryFind(fun r -> r.Short |> String.equalsCapInsens s)
    |> Option.map _.Long
    |> Option.defaultValue ""


DoseRule.get ()
|> Array.take 1
|> Array.map (fun dr ->
    {|
        doseRule = dr
        gstand =
            dr.Products
            |> Array.map _.GPK
            |> Array.collect (fun gpk ->
                [
                    if dr.PatientCategory.Age.Min.IsSome then
                        dr.PatientCategory.Age.Min.Value
                        |> Limit.getValueUnit
                    if dr.PatientCategory.Age.Max.IsSome then
                        dr.PatientCategory.Age.Max.Value
                        |> Limit.getValueUnit
                ]
                GStand.createDoseRules
                    GStand.config
                    None
                    None
                    None
                    (Int32.tryParse gpk)
                    ""
                    ""
                    ""
                |> Seq.toArray
            )

    |}
)


DoseRule.get()
|> Array.filter (fun dr -> dr.Generic = "baclofen")
|> DoseRule.Print.toMarkdown