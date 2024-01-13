

#time

#load "load.fsx"

#load "../Utils.fs"
#load "../OpenAI.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open Informedica.ZIndex.Lib
open Informedica.Utils.Lib.BCL
open Informedica.KinderFormularium.Lib


let formulary = WebSiteParser.getFormulary ()


formulary
|> Array.map Export.cleanGenericName

|> Array.filter (fun d ->
    d.Doses
    |> List.exists (fun dose ->
        dose.Routes
        |> List.exists (fun r ->
            Export.findGenPresProducts d.Generic r.Name
            |> List.isEmpty
        )
    )
)
|> Array.filter (fun drug -> drug.Generic |> String.equalsCapInsens "bcg vaccin")

|> Array.collect (Drug.mapDrug >> List.toArray)
|> Export.map

|> List.filter (fun export -> export.shape |> String.isNullOrWhiteSpace)

|> List.map _.generic
|> List.distinct
|> List.sort
|> List.iter (printfn "%s")



formulary
|> Export.map
|> List.filter (fun d ->
    d.shape |> String.isNullOrWhiteSpace
)
|> List.map _.generic
|> List.distinct
|> List.length


// do the actual export
formulary
//|> Array.filter (fun drug -> drug.Generic |> String.containsCapsInsens "bcg")
|> Array.map Export.cleanGenericName
|> Array.collect (Drug.mapDrug >> List.toArray)
|> Export.map
|> Export.addMaxDoses
|> Export.checkDoseTypes
|> Export.writeToFile "kinderformularium.csv"


open Drug

let mapDrug (drug : Drug) =
    let drug =
        { drug with
            Doses =
                drug.Doses
                |> List.map (fun dose ->
                    { dose with
                        Routes =
                            dose.Routes
                            |> List.map (fun route ->
                                { route with
                                    Name =
                                        route.Name
                                        |> Mapping.mapRoute
                                        |> Option.defaultValue route.Name
                                        |> String.toUpper
                                }
                            )
                    }
                )
        }

    Mapping.productMapping
    |> Array.filter (fun pm ->
        pm.medication |> String.equalsCapInsens drug.Generic
    )
    |> Array.toList
    |> function
        | [] -> [ drug ]
        | pms ->
            pms
            |> List.map (fun pm ->
                let drug =
                    if pm.generic |> String.isNullOrWhiteSpace then drug
                    else
                        { drug with Generic = pm.generic |> String.toLower |> String.trim }

                Mapping.productMapping
                |> Array.tryFind (fun pm ->
                    pm.generic |> String.equalsCapInsens drug.Generic &&
                    drug.Doses
                    |> List.exists (fun dose ->
                        dose.Routes
                        |> List.map _.Name
                        |> List.exists (String.equalsCapInsens pm.route)
                    )
                )
                |> function
                    | None    -> drug
                    | Some pm ->
                        printfn $"found: {pm}"
                        { drug with
                            Shape = pm.shape
                            Brand = pm.brand
                            Doses =
                                drug.Doses
                                |> List.map (fun dose ->
                                    { dose with
                                        Routes =
                                            dose.Routes
                                            |> List.map (fun r ->
                                                if pm.altRoute |> String.isNullOrWhiteSpace then r
                                                else
                                                    { r with
                                                        ProductRoute = pm.altRoute |> String.toUpper |> String.trim
                                                    }
                                            )
                                    }
                                )
                        }
            )


let drug =
    formulary
    |> Array.filter (fun drug -> drug.Generic |> String.containsCapsInsens "bcg")
    |> Array.head


"Intracutaan" |> Mapping.mapRoute
drug |> mapDrug


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
|> Array.collect (fun drug ->
    drug.Doses
    |> List.toArray
    |> Array.collect (fun dose ->
        dose.Routes
        |> List.toArray
        |> Array.collect (fun r ->
            r.Schedules
            |> List.toArray
            |> Array.map _.ScheduleText
            |> Array.map (fun s ->
                s
                |> String.split "\n"
                |> List.tryHead
                |> Option.defaultValue ""
            )
        )
    )
)
|> Array.filter (String.contains "max")
|> Array.map String.removeBrackets
|> Array.map
    (String.replace "\t" " " >>
     String.replace $"{char(34)}" " ")
|> Array.distinct
|> Array.take 5
|> Array.map (fun s ->
    {|
        doseText = s
        maxPerTime = s |> OpenAI.getAbsMaxDose
        maxQty = s |> OpenAI.getMaxDose
    |}
)
|> Array.iter (printfn "%A")



"6 maanden tot 18 jaar en ≥ 6 kg   Startdosering:Dag 1: 3  mg/kg/dosis,  éénmalig. Max: 125 mg/dag. Onderhoudsdosering:Dag 2 en 3: 2  mg/kg/dag  in 1 dosis. Max: 80 mg/dag. in de ochtend innemen."
|> OpenAI.getAbsMaxDose
|> Async.RunSynchronously


"6 maanden tot 18 jaar en ≥ 6 kg   Startdosering:Dag 1: 3  mg/kg/dosis,  éénmalig. Max: 125 mg/dag. Onderhoudsdosering:Dag 2 en 3: 2  mg/kg/dag  in 1 dosis. Max: 80 mg/dag. in de ochtend innemen."
|> fun doseText ->
    OpenAI.callAI $"wat is de maximale dosering die per keer gegeven kan worden, geef alleen de dosering als antwoord: {doseText}"
|> Async.RunSynchronously


"6 maanden tot 18 jaar en ≥ 6 kg   Startdosering:Dag 1: 3  mg/kg/dosis,  éénmalig. Max: 125 mg/dag. Onderhoudsdosering:Dag 2 en 3: 2  mg/kg/dag  in 1 dosis. Max: 80 mg/dag. in de ochtend innemen."
|> fun doseText ->
    OpenAI.callAI $"is dit een start of een eenmalige dosering: geef alleen 'start' of 'eenmalig' als antwoord: {doseText}"
|> Async.RunSynchronously


WebSiteParser.getFormulary ()
|> Array.filter (fun d ->
    d.Generic |> String.equalsCapInsens "sulfametrol + trimethoprim"
)


WebSiteParser.medications ()
|> List.filter (fun d -> d.Generic |> String.startsWith "Sulfa")