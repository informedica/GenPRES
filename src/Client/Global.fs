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


let pageToString terms locale page =
    let getTerm term = 
        terms
        |> Deferred.map (fun terms ->
            term
            |> Localization.getTerm terms locale
            |> Option.defaultValue $"{term}"
        )
        |> Deferred.defaultValue $"{term}"

    match page with
    | LifeSupport -> Terms.``Emergency List`` |> getTerm
    | ContinuousMeds -> Terms.``Continuous Medication List`` |> getTerm
    | Prescribe -> Terms.``Prescribe`` |> getTerm
    | Formulary -> Terms.``Formulary`` |> getTerm
    | Parenteralia -> Terms.``Parenteralia`` |> getTerm


let languageContext =
    React.createContext (name = "language", defaultValue = Localization.Dutch)


module Speech =

    open Fable.Core

    [<Emit("window.speechSynthesis.speak(new SpeechSynthesisUtterance($0));")>]
    let speak s = ()

