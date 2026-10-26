namespace Informedica.GenPRES.Client.Core.Helpers

/// Keeps the formulary and parenteralia pages in step with the prescribing filter. A choice on
/// either page reaches the prescribing filter as a seed of the filter, which the server applies.
module FilterSync =

    open Informedica.GenPRES.Shared.Types


    /// The formulary with the choices of the prescribing filter.
    let syncFilterToFormulary (filter: Filter) (form: Formulary) : Formulary =
        { form with
            Indication = filter.Indication
            Generic = filter.Generic
            Route = filter.Route
            Form = filter.Form
            DoseType = filter.DoseType
        }


    /// The parenteralia page with the generic, form and route of the prescribing filter.
    let syncFilterToParenteralia (filter: Filter) (par: Parenteralia) : Parenteralia =
        { par with
            Generic = filter.Generic
            Form = filter.Form
            Route = filter.Route
        }
