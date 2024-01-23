

#load "load.fsx"

open System

let dataUrlId = "16ftzbk2CNtPEq3KAOeP7LEexyg3B-E5w52RPOyQVVks"
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.SetEnvironmentVariable("GENPRES_URL_ID", dataUrlId)



#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"

#time


open Informedica.GenForm.Lib


open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Utils


open DoseRule



Product.get () |> fun xs -> printfn $"{xs |> Array.length}"; xs
|> Array.filter (fun p -> p.Generic |> String.startsWithCapsInsens "morfine")


let data = getData dataUrlId


data
|> Array.filter (fun xs -> xs.Generic |> String.containsCapsInsens "glyco")


module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct


let rules =
    data
    |> Array.groupBy (fun d -> d.Generic, d.Route)
    |> Array.collect (fun ((gen, rte), rs) ->
        let shapes =
            rs
            |> Array.map _.Shape
            |> Array.filter String.notEmpty
            |> Array.distinct

        rs
        |> Array.collect (fun r ->
            if r.Shape |> String.notEmpty then
                {| r with
                    Products =
                        GenPresProduct.filter gen r.Shape rte
                        |> Array.collect _.GenericProducts
                |}
                |> Array.singleton
            else
                GenPresProduct.filter gen "" rte
                |> Array.filter (fun gpp ->
                    shapes |> Array.isEmpty ||
                    shapes
                    |> Array.exists ((String.equalsCapInsens gpp.Shape) >> not)
                )
                |> Array.map (fun gpp ->
                    {| r with
                        Shape = gpp.Shape |> String.toLower
                        Products = gpp.GenericProducts
                    |}
                )
        )
    )


rules |> Array.length


rules |> Array.iteri (fun i r ->
    printfn $"{i + 1}. {r.Generic} {r.Shape} {r.Route} {r.DoseText}"
)



dataUrlId
|> DoseRule.get_
|> Array.filter (fun r -> r.Generic |> String.containsCapsInsens "glyco")
|> Array.iteri (fun i r ->
    printfn $"{i + 1}. {r.Generic} {r.Shape} {r.Route} {r.DoseText}"
)


Product.get ()
|> Product.filter
    { Filter.filter with
         Generic = "glycopyrronium" |> Some
         Shape = "drank" |> Some
         Route = "or" |> Some
    }
|> Array.filter (fun p ->
    p.Synonyms
    |> Array.exists (String.containsCapsInsens "rybrila")
)
