/// Keeps the prescribing filter, the formulary and the parenteralia pages in step: a choice on
/// one page carries over to the others, and a new generic, route or form on the formulary or
/// parenteralia page starts the prescribing workbench afresh.
module FilterSync

open Shared.Types


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


/// The order context with the choices of the formulary. The diluent and the selected components
/// are kept only when the indication, generic, route and form are unchanged; the scenarios go.
let syncFormularyToFilter (form: Formulary) (ctx: OrderContext) : OrderContext =
    ctx
    |> Shared.Models.OrderContext.seedFilter
        SeedSource.Formulary
        form.Indication
        form.Generic
        form.Route
        form.Form
        form.DoseType


/// The order context with the generic, form and route of the parenteralia page; the indication
/// and dose type are cleared. The diluent and the selected components are kept only when the
/// generic, route and form are unchanged; the scenarios go.
let syncParenteraliaToFilter (par: Parenteralia) (ctx: OrderContext) : OrderContext =
    ctx
    |> Shared.Models.OrderContext.seedFilter SeedSource.Parenteralia None par.Generic par.Route par.Form None
