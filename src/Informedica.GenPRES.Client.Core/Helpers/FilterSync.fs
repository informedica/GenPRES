namespace Informedica.GenPRES.Client.Core.Helpers

/// Keeps the formulary and parenteralia pages in step with the prescribing filter. A choice on
/// either page reaches the prescribing filter as a seed of the filter, which the server applies.
module FilterSync =

    open Informedica.GenPRES.Shared.Types


    /// Copies the choices of the prescribing filter to the formulary.
    let syncFilterToFormulary (filter: Filter) (form: Formulary) : Formulary =
        { form with
            Indication = filter.Indication
            Generic = filter.Generic
            Route = filter.Route
            Form = filter.Form
            DoseType = filter.DoseType
        }


    /// Copies the generic, form and route of the prescribing filter to the parenteralia page.
    let syncFilterToParenteralia (filter: Filter) (par: Parenteralia) : Parenteralia =
        { par with
            Generic = filter.Generic
            Form = filter.Form
            Route = filter.Route
        }
