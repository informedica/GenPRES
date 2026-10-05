/// Filters written out, and scenarios built from the fixed medications, for the tests of the
/// filter commands. No rules are loaded.
module FilterFixtures

open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


/// Every option list filled, nothing chosen.
let options =
    { OrderContext.emptyFilter with
        Indications = [| "koorts"; "pijn"; "sedatie" |]
        Generics = [| "morfine"; "paracetamol" |]
        Routes = [| "INTRAVENEUS"; "ORAAL"; "RECTAAL" |]
        Forms = [| "drank"; "zetpil" |]
        DoseTypes =
            [|
                Informedica.GenForm.Lib.Types.Discontinuous "onderhoud"
                Informedica.GenForm.Lib.Types.Once "eenmalig"
            |]
        Diluents = [| "NaCl 0,9%"; "gluc 5%" |]
        Components = [| "paracetamol"; "NaCl 0,9%"; "water" |]
    }


/// A filter with nothing chosen, one with the step of the indication and the medication chosen,
/// and one with every choice made.
let filters =
    [
        "nothing chosen", options
        "indication and generic",
        { options with
            Indication = Some "pijn"
            Generic = Some "paracetamol"
        }
        "everything",
        { options with
            Indication = Some "pijn"
            Generic = Some "paracetamol"
            Route = Some "RECTAAL"
            Form = Some "zetpil"
            DoseType = Some(Informedica.GenForm.Lib.Types.Discontinuous "onderhoud")
            Diluent = Some "NaCl 0,9%"
        }
    ]


let scenario no (med: Medication) =
    let ord = med |> Medication.toOrder Scenarios.testStart |> Result.get

    OrderScenario.create
        no
        med.Name
        "pijn"
        "zetpil"
        "RECTAAL"
        (Informedica.GenForm.Lib.Types.Discontinuous "onderhoud")
        None
        None
        None
        [||]
        [||]
        [||]
        ord
        false
        false
        None
        [||]


/// A suppository and a drink.
let scenarios =
    [|
        scenario 1 Scenarios.pcmSupp
        { scenario 2 Scenarios.pcmDrink with Form = "drank" }
    |]


let context filter : OrderContext =
    {
        Filter = filter
        Patient = Patient.patient
        Scenarios = scenarios
        Argumentation = Some "afwijking"
    }


let categories =
    [
        "drug", OrderCategory.Drug
        "nutrition", OrderCategory.Nutrition NutritionCategory.TPN
    ]


/// The number of options of a field.
let count field (filter: Filter) =
    match field with
    | FilterField.Indication -> filter.Indications.Length
    | FilterField.Generic -> filter.Generics.Length
    | FilterField.Route -> filter.Routes.Length
    | FilterField.Form -> filter.Forms.Length
    | FilterField.DoseType -> filter.DoseTypes.Length
    | FilterField.Diluent -> filter.Diluents.Length
    | FilterField.Components -> filter.Components.Length
