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
    let unchanged =
        form.Indication = ctx.Filter.Indication
        && form.Generic = ctx.Filter.Generic
        && form.Route = ctx.Filter.Route
        && form.Form = ctx.Filter.Form

    { ctx with
        Filter =
            { ctx.Filter with
                Indication = form.Indication
                Generic = form.Generic
                Form = form.Form
                Route = form.Route
                DoseType = form.DoseType
                Diluent = if unchanged then ctx.Filter.Diluent else None
                SelectedComponents = if unchanged then ctx.Filter.SelectedComponents else [||]
            }
        Scenarios = [||]
    }


/// The order context with the generic, form and route of the parenteralia page; the indication
/// and dose type are cleared. The diluent and the selected components are kept only when the
/// generic, route and form are unchanged; the scenarios go.
let syncParenteraliaToFilter (par: Parenteralia) (ctx: OrderContext) : OrderContext =
    let unchanged =
        par.Generic = ctx.Filter.Generic
        && par.Route = ctx.Filter.Route
        && par.Form = ctx.Filter.Form

    { ctx with
        Filter =
            { ctx.Filter with
                Indication = None
                Generic = par.Generic
                Form = par.Form
                Route = par.Route
                DoseType = None
                Diluent = if unchanged then ctx.Filter.Diluent else None
                SelectedComponents = if unchanged then ctx.Filter.SelectedComponents else [||]
            }
        Scenarios = [||]
    }
