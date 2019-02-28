module Global

open Fable.MaterialUI.Core
open GenPres
open Fable.Helpers.Isomorphic

type Pages =
    | EmergencyList
    | ContinuousMeds
    | NormalValues
    | PewsCalculator
    | GCSCalculator

let parsePages =
    function
    | EmergencyList -> "Noodlijst"
    | ContinuousMeds -> "Continue Medicatie"
    | NormalValues -> "Normaal Waarden"
    | PewsCalculator -> "PEWS Calculator"
    | GCSCalculator -> "GCS Calculator"
