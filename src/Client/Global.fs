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
    | TreatmentPlan
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
    | TreatmentPlan -> Terms.``Treatment Plan`` |> getTerm
    | Formulary -> Terms.``Formulary`` |> getTerm
    | Parenteralia -> Terms.``Parenteralia`` |> getTerm


type Context = { Localization : Localization.Locales; Hospital : string }

let defContext = { Localization = Localization.Dutch; Hospital = "" }

let context =
    React.createContext (name = "context", defaultValue = defContext)


module Speech =

    open Fable.Core

    [<Emit("window.speechSynthesis.speak(new SpeechSynthesisUtterance($0));")>]
    let speak s = ()

