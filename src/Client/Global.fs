module Global

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
