

#time

#load "load.fsx"

#load "../Utils.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open Informedica.KinderFormularium.Lib


WebSiteParser.getFormulary ()
|> Export.map
|> Export.writeToFile "kinderformularium.csv"


WebSiteParser.getEmptyRules ()
|> Array.length


WebSiteParser.getFormulary ()
|> Array.filter (fun d -> d.Generic = "Paracetamol")


WebSiteParser.getFormulary ()
|> Array.collect (fun drug ->
    drug.Doses
    |> List.toArray
    |> Array.collect (fun dose ->
        dose.Routes
        |> List.toArray
        |> Array.collect (fun route ->
            route.Schedules
            |> List.toArray
            |> Array.filter (fun schedule ->
                match schedule.Target with
                | Drug.Target.Unknown _ -> true
                | _ -> false
            )
        )
    )
)


WebSiteParser.getFormulary ()
|> Array.filter (fun drug ->
    drug.Generic = "Paracetamol"
)
|> Export.map

