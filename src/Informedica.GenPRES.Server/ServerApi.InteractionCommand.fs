namespace ServerApi


/// Handles the interaction commands.
module InteractionCommand =

    open Informedica.GenPRES.Shared.Api


    /// Returns which commands need the loaded resources: only the interaction check.
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
