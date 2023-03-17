module Global

open Feliz
open Feliz.Router
open Feliz.UseElmish
open Elmish
open Shared

type Pages =
    | LifeSupport
    | ContinuousMeds
    | Prescribe
    | Formulary
    | Parenteralia


let pageToString locale page =
    match page with
    | LifeSupport ->
        Localization.getTerm locale Localization.Terms.``Emergency List``
    | ContinuousMeds ->
        Localization.getTerm
            locale
            Localization.Terms.``Continuous Medication List``
    | Prescribe -> "Voorschrijven"
    | Formulary -> "Formularium"
    | Parenteralia -> "Parenteralia"


let languageContext =
    React.createContext (name = "language", defaultValue = Localization.Dutch)



module Speech =

    open Fable.Core

    [<Emit("window.speechSynthesis.speak(new SpeechSynthesisUtterance($0));")>]
    let speak s = ()

