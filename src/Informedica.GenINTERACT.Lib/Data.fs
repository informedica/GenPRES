namespace Informedica.GenInteract.Lib

open Newtonsoft.Json


module Data =

    /// Interaction data with no drug classes and no interactions.
    /// What an absent cache amounts to.
    let empty =
        {
            InteractionData.DrugClasses = []
            Interactions = []
        }


    /// Read interaction data from the cached JSON the caller supplies.
    /// Absent JSON yields empty data: finding the cache is the caller's
    /// responsibility, and None already says it was not found.
    let fromCache (json: string option) =
        match json with
        | Some s -> JsonConvert.DeserializeObject<InteractionData>(s)
        | None -> empty


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
