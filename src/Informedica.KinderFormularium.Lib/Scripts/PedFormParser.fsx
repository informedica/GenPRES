

#time

#load "load.fsx"

#load "../Utils.fs"
#load "../OpenAI.fs"
#load "../Mapping.fs"
#load "../Drug.fs"
#load "../FormularyParsers.fs"
#load "../WebSiteParser.fs"
#load "../Export.fs"


open Informedica.KinderFormularium.Lib


let addMaxDoses (mapped : {| adjustUnit: string; doseType: string; doseUnit: string; freqUnit: string; freqs: string; gender: string; generic: string; indication: string; maxAge: string; maxBSA: string; maxGestAge: string; maxPMAge: string; maxPerTime: string; maxPerTimeAdj: string; maxQty: string; maxQtyAdj: string; maxWeight: string; minAge: string; minBSA: string; minGestAge: string; minPMAge: string; minPerTime: string; minPerTimeAdj: string; minQty: string; minQtyAdj: string; minWeight: string; normPerTimeAdj: string; normQtyAdj: string; route: string; scheduleText: string; shape: string; substance: string |} list) =
    let batches =
        mapped
        |> List.toArray
        |> Array.chunkBySize 10
    let count = batches |> Array.length
    let n = ref 0

    batches
    |> Array.collect (fun chunked ->
        n.Value <- n.Value + 1
        printfn $"processed {n.Value} of total {count}"

        chunked
        |> Array.map  OpenAI.mapMaxDoses
        |> Async.Parallel
        |> fun p ->
            async {
                do! Async.Sleep(1000)
                return! p
            }
        |> Async.RunSynchronously
    )


WebSiteParser.getFormulary ()
|> Export.map
|> addMaxDoses
|> Array.toList
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



open Informedica.Utils.Lib.BCL


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