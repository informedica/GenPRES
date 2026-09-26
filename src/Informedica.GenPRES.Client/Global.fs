module Global

open Feliz
open Shared


type Pages =
    | LifeSupport
    | ContinuousMeds
    | Prescribe
    | Nutrition
    | OrderPlan
    | Formulary
    | Parenteralia
    | Interactions
    | Settings


let getLocalizedTerm (localizationTerms: Deferred<string[][]>) (lang: Localization.Locales) defVal term =
    localizationTerms
    |> Deferred.map (fun terms -> Localization.getTerm terms lang term |> Option.defaultValue defVal)
    |> Deferred.defaultValue defVal


/// The birthdate as the title bar and the panel show it: day, month and year as the EHR that
/// launched the Session writes them, from the three integers the context carries, so that no
/// time zone can move it by a day.
let birthDateText (who: Types.NameAndBirthDate) = $"%02i{who.BirthDay}-%02i{who.BirthMonth}-%04i{who.BirthYear}"


let pageToString terms locale page =
    let getTerm term = getLocalizedTerm terms locale $"{term}" term

    match page with
    | LifeSupport -> Terms.``Emergency List`` |> getTerm
    | ContinuousMeds -> Terms.``Continuous Medication List`` |> getTerm
    | Prescribe -> Terms.``Prescribe`` |> getTerm
    | Nutrition -> Terms.``Nutrition`` |> getTerm
    | OrderPlan -> Terms.``Order Plan`` |> getTerm
    | Formulary -> Terms.``Formulary`` |> getTerm
    | Parenteralia -> Terms.``Parenteralia`` |> getTerm
    | Interactions -> Terms.``Interactions`` |> getTerm
    | Settings -> Terms.Settings |> getTerm


type Context =
    {
        Localization: Localization.Locales
        Hospital: string
    }

let defContext =
    {
        Localization = Localization.Dutch
        Hospital = ""
    }

let context = React.createContext (defaultValue = defContext)


module Speech =

    open Fable.Core

    [<Emit("window.speechSynthesis.speak(new SpeechSynthesisUtterance($0));")>]
    let speak s = ()


/// The labels of the print header and the signature line, and the word for a value not known.
type PrintLabels =
    {|
        date: string
        patientNumber: string
        department: string
        name: string
        bed: string
        birthDate: string
        physician: string
        weight: string
        pager: string
        signature: string
        unknown: string
    |}


/// The print labels, resolved once here and handed to the print components as props. None of
/// them has a term in the localization sheet yet, so they are the Dutch the print always had;
/// when a term arrives, this is the one place that changes.
let printLabels () : PrintLabels =
    {|
        date = "D.D."
        patientNumber = "Patientnummer"
        department = "Afdeling"
        name = "Naam"
        bed = "Bed"
        birthDate = "Geboorte datum"
        physician = "Arts"
        weight = "Gewicht"
        pager = "Zoemer"
        signature = "Paraaf arts:"
        unknown = "onbekend"
    |}
