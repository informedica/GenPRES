module Global


type Pages =
    | LifeSupport
    | ContinuousMeds
    | NormalValues
    | PewsCalculator
    | GCSCalculator


let parsePages =
    function
    | LifeSupport -> "Noodlijst"
    | ContinuousMeds -> "Continue Medicatie"
    | NormalValues -> "Normaal Waarden"
    | PewsCalculator -> "PEWS Calculator"
    | GCSCalculator -> "GCS Calculator"

