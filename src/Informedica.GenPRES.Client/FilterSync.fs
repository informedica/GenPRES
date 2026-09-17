/// The prescribing filter, the formulary and the parenteralia pages kept in step: what the
/// User chose on one page carries over to the others, and a change of generic, route or form
/// on the formulary or the parenteralia page starts the prescribing workbench afresh. UI state
/// between two contract model records, no rule of the domain.
module FilterSync

open Shared.Types


let syncFilterToFormulary (filter: Filter) (form: Formulary) : Formulary =
    { form with
        Indication = filter.Indication
        Generic = filter.Generic
        Route = filter.Route
        Form = filter.Form
        DoseType = filter.DoseType
    }


let syncFilterToParenteralia (filter: Filter) (par: Parenteralia) : Parenteralia =
    { par with
        Generic = filter.Generic
        Form = filter.Form
        Route = filter.Route
    }


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
