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


let pageToString terms locale page =
    let getTerm term =
        getLocalizedTerm terms locale $"{term}" term

    match page with
    | LifeSupport -> Terms.``Emergency List`` |> getTerm
    | ContinuousMeds -> Terms.``Continuous Medication List`` |> getTerm
    | Prescribe -> Terms.``Prescribe`` |> getTerm
    | Nutrition -> Terms.``Nutrition`` |> getTerm
    | OrderPlan -> Terms.``Treatment Plan`` |> getTerm
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
