namespace Informedica.GenInteract.Lib

open Newtonsoft.Json


module Data =

    /// Read interaction data from the cached JSON the caller supplies.
    /// Finding the cache is the caller's responsibility: there is no value
    /// standing for an absent one, so a deployment missing its interaction
    /// data cannot be mistaken here for one that knows of no interactions.
    let fromCache (json: string) = JsonConvert.DeserializeObject<InteractionData>(json)


    let cacheToInteractions (json: string) : Interaction list =
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
