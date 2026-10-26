namespace Informedica.GenPRES.Client.Core.Helpers


/// The filter seed: a filter's choices set from outside its own fields, and the command that
/// sends it.
module FilterSeed =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api


    /// A filter's choices set from outside its own fields, by name, with where they come from: the
    /// server applies them with the rule of their source.
    type FilterSeed =
        {
            /// Where the choices come from: the url, a medication list, a page, or a reload.
            Source: SeedSource
            Indication: string option
            Generic: string option
            Route: string option
            Form: string option
            DoseType: DoseType option
        }


    /// The order context command that sends the seed.
    let command (seed: FilterSeed) =
        OrderViewCommand.SeedFilter(seed.Source, seed.Indication, seed.Generic, seed.Route, seed.Form, seed.DoseType)


    /// The seed of an item chosen on a medication list: its generic, and its indication, route and
    /// dose type where the list gives one.
    let ofListItem generic indication route doseType =
        let given s = if s = "" then None else Some s

        {
            Source = SeedSource.MedicationList
            Indication = indication |> given
            Generic = Some generic
            Route = route |> given
            Form = None
            DoseType = doseType |> given |> Option.map DoseTypeText.doseTypeFromString
        }
