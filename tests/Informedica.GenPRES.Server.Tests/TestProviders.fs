/// The resource providers the tests of the server share.
module Informedica.GenPRES.Server.Tests.TestProviders

open System

module Resources = Informedica.GenForm.Lib.Resources
module GenFormTypes = Informedica.GenForm.Lib.Types


/// A provider that holds only the solution rules solutionRules, the predilution rules predilutions and a
/// route mapping for the route INTRAVENEUS. The rule lookup reads the additives, which answer false; every
/// other resource raises.
type SolutionRulesProvider(solutionRules: GenFormTypes.SolutionRule[], predilutions: GenFormTypes.ProductPredilution[])
    =
    interface Resources.IResourceProvider with
        member _.Get(key: Resources.ResourceKey<'T>) : 'T =
            if key.Name = Resources.Keys.isAdditive.Name then
                box (fun (_: string) -> false) :?> 'T
            elif key.Name = Resources.Keys.productPredilutions.Name then
                box predilutions :?> 'T
            else
                raise (Collections.Generic.KeyNotFoundException key.Name)

        member _.GetData() = raise (NotImplementedException())
        member _.GetUnitMappings() = raise (NotImplementedException())
        member _.GetRouteMappings() =
            [|
                {
                    GenFormTypes.Data.RouteMapping.Long = "INTRAVENEUS"
                    Short = "iv"
                }
            |]
        member _.GetValidForms() = raise (NotImplementedException())
        member _.GetFormRoutes() = [||]
        member _.GetFormularyProducts() = raise (NotImplementedException())
        member _.GetReconstitution() = raise (NotImplementedException())
        member _.GetParenteralMeds() = raise (NotImplementedException())
        member _.GetEnteralFeeding() = raise (NotImplementedException())
        member _.GetProducts() = raise (NotImplementedException())
        member _.GetDoseRules() = [||]
        member _.GetSolutionRules() = solutionRules
        member _.GetRenalRules() = [||]
        member _.GetTotals() = [||]
        member _.GetGStandProvider() = raise (NotImplementedException())
        member _.GetDepartments() = Resources.distinctNames []
        member _.GetLocations() = [||]
        member _.GetResourceInfo() = raise (NotImplementedException())
