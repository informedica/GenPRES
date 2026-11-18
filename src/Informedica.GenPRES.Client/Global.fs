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


/// Formats the birth date as dd-mm-yyyy for the title bar and the panel. Uses the three integers of
/// the patient identity as the EHR wrote them, so no time zone can move the date by a day.
let birthDateText (who: Types.PatientIdentity) = $"%02i{who.BirthDay}-%02i{who.BirthMonth}-%04i{who.BirthYear}"


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


/// Whether a quantity field is counting step clicks, and the function a field calls when it starts
/// counting or has sent its clicks. While one field counts, every other field is disabled.
type Counting =
    {
        Counting: bool
        Report: bool -> unit
    }

/// The default outside the App: no field counting, and reports are ignored.
let defCounting =
    {
        Counting = false
        Report = ignore
    }

/// The counting state of the quantity fields, provided at the root of the App.
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


/// Returns the print labels, which the print components get as props. The labels are Dutch text,
/// because the localization sheet has no terms for them; this is the one place to change when it does.
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
