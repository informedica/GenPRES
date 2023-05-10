

#time

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
open System.IO


open MathNet.Numerics

open Informedica.Utils.Lib
open Informedica.GenForm.Lib



{ Patient.patient with
    Department = "ICK"
    Age = 365N * 10N |> Some
    Weight = 1000N * 33N |> Some
    VenousAccess =  CVL
}
|> PrescriptionRule.get //|> Array.length
|> Array.filter (fun r -> r.DoseRule.Generic = "adrenaline" && r.DoseRule.Route = "iv")
|> Array.collect (fun r -> r.DoseRule.DoseLimits
)
//|> DoseRule.Print.toMarkdown
|> Array.item 0


Mapping.filterRouteShapeUnit "rect" "zetpil" ""

let x = System.Diagnostics.Process.GetCurrentProcess()
for item in x.Modules do
    if item.FileName.Contains("fsi.dll") then
        printfn "%A" item.FileName

Product.get ()
|> Array.filter (fun p -> p.Generic = "amikacine")


Informedica.ZIndex.Lib.GenPresProduct.search "amikacine"
|> Array.collect (fun gpp ->
    gpp.Routes
)
|> Array.distinct
|> Array.map Mapping.mapRoute


Product.generics