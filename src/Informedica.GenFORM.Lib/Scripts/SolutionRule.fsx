#load "load.fsx"

open System
Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID")


#load "../Types.fs"
#load "../Utils.fs"
#load "../Logging.fs"
#load "../Mapping.fs"
#load "../Patient.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../LimitTarget.fs"
#load "../DoseLimit.fs"
#load "../DoseType.fs"
#load "../GenericLabel.fs"
#load "../PharmaceuticalForm.fs"
#load "../ProductId.fs"
#load "../Generic.fs"
#load "../Source.fs"
#load "../DoseRule.fs"
#load "../DoseRuleData.fs"
#load "../DoseRuleLoader.fs"
#load "../Check.fs"
#load "../SolutionLimit.fs"
#load "../SolutionRule.fs"
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"
#load "../FormLogging.fs"
#load "../Api.fs"


open MathNet.Numerics
open FsToolkit.ErrorHandling
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib

let inline resultGet r =
    match r with
    | Ok x -> x
    | Error _ -> failwith "Failed to get result"

let routeMapping = Mapping.getRouteMapping dataUrlId |> resultGet


let unitMapping = Mapping.getUnitMapping dataUrlId |> resultGet

let parenterals =
    Product.getFormularyProducts dataUrlId
    |> Result.map (Product.Parenteral.get FormLogging.noOp unitMapping)
    |> resultGet


SolutionRule.getData dataUrlId
|> function
    | Ok data ->
        data
        |> Array.filter (_.Generic >> (=) "Samenstelling C")
        |> SolutionRule.map routeMapping parenterals [||]
    | Error _ -> failwith "could not getData"


let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


{ Filter.doseFilter with
    Generic = Some "Samenstelling C"
    DoseType = Timed "dag 1" |> Some
    DoseFilter.Patient.Department = Some "ICK"
    DoseFilter.Patient.Weight = 10N |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some

}
|> Api.filterPrescriptionRules provider
