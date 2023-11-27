
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
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib

module Dto = Informedica.ZForm.Lib.Dto


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


doseRules
|> Array.take 1
|> Array.map (fun dr ->
    {|
        doseRule = dr
        zformRules =
            ZForm.getRules dr.Generic dr.Route
    |}
)