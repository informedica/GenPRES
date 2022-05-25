module Global

open Feliz
open Feliz.Router
open Feliz.UseElmish
open Elmish
open Shared

type Pages =
    | LifeSupport
    | ContinuousMeds
// | NormalValues
// | PewsCalculator
// | GCSCalculator


let pageToString locale page =
    match page with
    | LifeSupport ->
        Localization.getTerm locale Localization.Terms.``Emergency List``
    | ContinuousMeds ->
        Localization.getTerm
            locale
            Localization.Terms.``Continuous Medication List``
// | NormalValues -> "Normaal Waarden"
// | PewsCalculator -> "PEWS Calculator"
// | GCSCalculator -> "GCS Calculator"


let languageContext =
    React.createContext (name = "language", defaultValue = Localization.Dutch)


//let renderContext