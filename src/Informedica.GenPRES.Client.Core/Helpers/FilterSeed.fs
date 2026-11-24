namespace Informedica.GenPRES.Client.Core.Helpers


/// Filter choices set from outside the filter fields, and the command that sends them to the server.
module FilterSeed =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api


    /// Filter choices set from outside the filter fields, such as from a url or a page. The
    /// server applies them by the rule of their source.
    type FilterSeed =
        {
            /// Where the choices come from: the url, a page, or a reload.
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
