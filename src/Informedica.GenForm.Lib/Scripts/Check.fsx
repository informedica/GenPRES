
#time

open System
Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

#load "load.fsx"

#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
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


open Informedica.GenForm.Lib


let ``checked`` =
    DoseRule.get ()
    |> Array.filter (fun dr -> true
    //    dr.Generic = "abatacept" &&
    //    dr.Shape = "" &&
    //    dr.Route = "iv"
    )
    |> Check.checkAll Patient.patient


``checked``
|> Array.iter (printfn "%s")

DoseRule.get ()
|> Array.item 2
|> Check.matchWithZIndex Patient.patient |> ignore
|> fun r ->
    r.zindex.
    Array.length


DoseRule.get ()
|> Array.distinct
|> Array.length


DoseRule.get ()
|> DoseRule.Print.toMarkdown
|> printfn "%s"