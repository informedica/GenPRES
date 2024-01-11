

#time

#load "load.fsx"

#load "../Utils.fs"
#load "../OpenAI.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open Informedica.Utils.Lib.BCL
open Informedica.KinderFormularium.Lib


WebSiteParser.getFormulary ()
//|> Array.filter (fun d -> d.Generic |> String.equalsCapInsens "paracetamol")
|> Export.map
|> Export.addMaxDoses
|> Export.checkDoseTypes
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