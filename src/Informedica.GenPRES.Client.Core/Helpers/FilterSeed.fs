namespace Informedica.GenPRES.Client.Core.Helpers


/// Filter choices set from outside the filter fields, and the command that sends them to the server.
module FilterSeed =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api


    /// Filter choices set from outside the filter fields, such as from a url or a medication list. The
    /// server applies them by the rule of their source.
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


    /// Creates the seed for an item chosen on a medication list. An empty indication, route or dose type
    /// gives None.
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
