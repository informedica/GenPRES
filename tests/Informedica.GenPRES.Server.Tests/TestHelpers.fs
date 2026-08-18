module Informedica.GenPRES.Server.Tests.Helpers

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources

/// Stub registry: every resource resolves to a typed empty value. The typed
/// empties matter — the boxed value's runtime type must match the key's `'T`
/// for the engine's downcast to succeed.
let okRegistry: ResourceRegistry =
    Map
        [
            Keys.unitMappings.Name, ofResult (fun () -> Ok([||]: UnitMapping[]))
            Keys.routeMappings.Name, ofResult (fun () -> Ok([||]: RouteMapping[]))
            Keys.validForms.Name, ofResult (fun () -> Ok([||]: string[]))
            Keys.formRoutes.Name, ofResult (fun () -> Ok([||]: FormRoute[]))
            Keys.formularyProducts.Name, ofResult (fun () -> Ok([||]: FormularyProduct[]))
            Keys.genPresProducts.Name, ofResult (fun () -> Ok([||]: Informedica.ZIndex.Lib.Types.GenPresProduct[]))
            Keys.reconstitution.Name, ofResult (fun () -> Ok([||]: Reconstitution[]))
            Keys.parenteralMeds.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
            Keys.enteralFeeding.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
            Keys.doseRuleData.Name, ofResult (fun () -> Ok([||]: DoseRuleData[]))
            Keys.solutionRuleData.Name, ofResult (fun () -> Ok([||]: SolutionRuleData[]))
            Keys.renalRuleData.Name, ofResult (fun () -> Ok([||]: RenalRuleData[]))
            Keys.totalsData.Name, ofResult (fun () -> Ok([||]: TotalsData[]))
            Keys.products.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
            Keys.doseRules.Name, ofResult (fun () -> Ok([||]: DoseRule[]))
            Keys.solutionRules.Name, ofResult (fun () -> Ok([||]: SolutionRule[]))
            Keys.renalRules.Name, ofResult (fun () -> Ok([||]: RenalRule[]))
            Keys.gStandProvider.Name, derive (fun r -> Check.gStandProvider (r.Get Keys.routeMappings))
        ]


let emptyFormulary: Shared.Types.Formulary =
    {
        Generics = [||]
        Indications = [||]
        Routes = [||]
        Forms = [||]
        DoseTypes = [||]
        PatientCategories = [||]
        Products = [||]
        Generic = None
        Indication = None
        Route = None
        Form = None
        DoseType = None
        PatientCategory = None
        Patient = None
        Markdown = ""
        DoseCheck = [||]
    }


let emptyParenteralia: Shared.Types.Parenteralia =
    {
        Generics = [||]
        Forms = [||]
        Routes = [||]
        PatientCategories = [||]
        Generic = None
        Form = None
        Route = None
        PatientCategory = None
        Markdown = ""
    }
