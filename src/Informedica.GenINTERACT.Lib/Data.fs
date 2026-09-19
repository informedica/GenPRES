namespace Informedica.GenInteract.Lib

open System.IO
open Newtonsoft.Json


module Data =

    let fromCache (json: string option) =
        match json with
        | Some s -> JsonConvert.DeserializeObject<InteractionData>(s)
        | None ->
            let path = "data/cache/interactions/Data.JSON"
            File.ReadAllText(path) |> JsonConvert.DeserializeObject<InteractionData>


    let cacheToInteractions (json: string option) : Interaction list =
        fromCache json
        |> fun d ->
            let getDrugs n =
                d.DrugClasses |> List.filter (fun dc -> dc.Name = n) |> List.collect _.Drugs

            d.Interactions
            |> List.map
                (fun
                    {
                        CacheInteraction.DrugClass1 = c1
                        DrugClass2 = c2
                    } ->
                    {
                        Interaction.DrugClass1 =
                            {
                                DrugClass.Name = c1
                                Drugs = c1 |> getDrugs
                            }
                        DrugClass2 =
                            {
                                DrugClass.Name = c2
                                Drugs = c2 |> getDrugs
                            }
                    }
                )
