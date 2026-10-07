namespace ServerApi


/// The interaction member: the drug names and the check, over the interaction port.
module InteractionCommand =

    open Shared.Api


    /// The drug names come from the interaction source, not the formulary; a check over a
    /// plan's drugs needs the formulary loaded.
    let gate =
        function
        | InteractionCommand.GetDrugNames -> Gate.Open
        | InteractionCommand.CheckInteractions _ -> Gate.RequiresLoaded


    let processCmd (env: AppEnv) (cmd: InteractionCommand) =
        match cmd with
        | InteractionCommand.GetDrugNames ->
            async {
                let! result = env.interaction.getDrugNames ()
                return result |> Result.map (List.toArray >> InteractionResponse.DrugNamesLoaded)
            }
        | InteractionCommand.CheckInteractions drugs ->
            async {
                let! result = env.interaction.checkInteractions drugs
                return result |> Result.map (List.toArray >> InteractionResponse.InteractionsChecked)
            }
