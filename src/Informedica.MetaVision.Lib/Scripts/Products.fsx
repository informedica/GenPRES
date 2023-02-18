

#load "load.fsx"


#load "../Types.fs"
#load "../Data.fs"
#load "../Utils.fs"
#load "../MetaVision.fs"


open Informedica.Utils.Lib.BCL
open Informedica.ZIndex.Lib
open Informedica.MetaVision.Lib
open Informedica.ZIndex.Lib.ATCGroup
open Informedica.ZIndex.Lib.GenericProduct


let prods =
    GenPresProduct.get true
    |> filterGenericProducts


prods
|> Array.tryFind (fun gp ->
    gp.Shape |> String.equalsCapInsens "gel" &&
    gp.Route |> Array.exists (String.equalsCapInsens "endotracheopulmonair")
)
|> function
| Some gp ->
    printfn $"{gp}"
    printfn $"""{gp.Id} {gp.Shape} {gp.Route |> String.concat ","} {gp.Substances[0].ShapeUnit}"""
    gp.Shape |> shapeDoseUnit gp.Route gp.Substances[0].ShapeUnit
| None -> failwith "not found"


prods
|> Array.filter (fun p ->
    p.Name
    |> String.contains "LOPINAVIR"
)

