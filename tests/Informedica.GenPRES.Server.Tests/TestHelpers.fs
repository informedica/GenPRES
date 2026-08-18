module Informedica.GenPRES.Server.Tests.Helpers

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
