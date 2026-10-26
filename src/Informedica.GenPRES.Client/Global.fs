module Global

open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Client.Core.Helpers
open Feliz
open Informedica.GenPRES.Shared


type Pages = Page.Page


let getLocalizedTerm (localizationTerms: Deferred<string[][]>) (lang: Localization.Locales) defVal term =
    localizationTerms
    |> Deferred.map (fun terms -> LocalizationText.getTerm terms lang term |> Option.defaultValue defVal)
    |> Deferred.defaultValue defVal


/// The birthdate as the title bar and the panel show it: day, month and year as the EHR that
/// launched the Session writes them, from the three integers the context carries, so that no
/// time zone can move it by a day.
let birthDateText (who: Types.NameAndBirthDate) = $"%02i{who.BirthDay}-%02i{who.BirthMonth}-%04i{who.BirthYear}"


let pageToString terms locale page =
    let getTerm term = getLocalizedTerm terms locale $"{term}" term

    match page with
    | Pages.LifeSupport -> Terms.``Emergency List`` |> getTerm
    | Pages.ContinuousMeds -> Terms.``Continuous Medication List`` |> getTerm
    | Pages.Prescribe -> Terms.``Prescribe`` |> getTerm
    | Pages.Nutrition -> Terms.``Nutrition`` |> getTerm
    | Pages.OrderPlan -> Terms.``Order Plan`` |> getTerm
    | Pages.Formulary -> Terms.``Formulary`` |> getTerm
    | Pages.Parenteralia -> Terms.``Parenteralia`` |> getTerm
    | Pages.Interactions -> Terms.``Interactions`` |> getTerm
    | Pages.Settings -> Terms.Settings |> getTerm


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


/// Whether a quantity field is counting step clicks now, and how a field reports that it starts
/// counting or has sent its clicks. While one field counts, every other field is disabled.
type Counting =
    {
        Counting: bool
        Report: bool -> unit
    }

/// No field counting, and nothing to report to: the value outside the App.
let defCounting =
    {
        Counting = false
        Report = ignore
    }

/// The quantity fields' count, provided at the root of the App.
let counting = React.createContext (defaultValue = defCounting)


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
