
#time

#load "load.fsx"

open System
let dataUrlId = "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"
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
#load "../Check.fs"
#load "../SolutionRule.fs"
#load "../RenalRule.fs"
#load "../PrescriptionRule.fs"
#load "../FormLogging.fs"
#load "../Api.fs"


open FsToolkit.ErrorHandling
open Informedica.GenForm.Lib


module GenFormResult =

    let defaultValue value res =
        res
        |> Result.map fst
        |> Result.defaultValue value


let provider : Resources.IResourceProvider = Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


let checkedRules =
    Api.getDoseRules provider
    |> Array.filter (fun dr -> true
    //    dr.Generic = "abatacept" &&
    //    dr.Form = "" &&
    //    dr.Route = "iv"
    )
    |> Check.checkAll
            (provider.GetRouteMappings ())
            Patient.patient


checkedRules
|> Array.iter (printfn "%s")