namespace Shared


module Localization =

    type Locales =
        | English
        | Dutch
    // | German
    // | French
    // | Spanish
    // | Italian


    let toString =
        function
        | English -> "English"
        | Dutch -> "Nederlands"


    let fromString (s: string) =
        let s = s.Trim().ToLower()

        match s with
        | _ when s = "english" -> English
        | _ when s = "nederlands" -> Dutch
        | _ -> failwith $"{s} is not a known language"


    let languages = [ English; Dutch ]


    module Terms =

        type Terms =
            | Unknown
            | ``Patient enter patient data``
            | ``Patient Age``
            | ``Patient Age year``
            | ``Patient Age years``
            | ``Patient Age month``
            | ``Patient Age months``
            | ``Patient Age week``
            | ``Patient Age weeks``
            | ``Patient Age day``
            | ``Patient Age days``
            | ``Patient Estimated``
            | ``Patient Years``
            | ``Patient Months``
            | ``Patient Weeks``
            | ``Patient Days``
            | ``Patient Weight``
            | ``Patient Length``
            | ``Patient remove patient data``
            | ``Emergency List``
            | ``Emergency List show when patient data``
            | ``Emergency List Indication``
            | ``Emergency List Intervention``
            | ``Emergency List Calculated``
            | ``Emergency List Preparation``
            | ``Emergency List Advice``
            | ``Continuous Medication List``
            | ``Continuous Medication List show when patient data``
            | ``Continuous Medication Indication``
            | ``Continuous Medication Medication``
            | ``Continuous Medication Quantity``
            | ``Continuous Medication Solution``
            | ``Continuous Medication Dose``
            | ``Continuous Medication Advice``
            | ``Sort By``

    open Terms

    let getTerm locale term =
        match locale with
        | English ->
            match term with
            | Unknown -> "unknown"
            | ``Patient enter patient data`` -> "Enter patient data ..."
            | ``Patient Age`` -> "Age"
            | ``Patient Age year`` -> "year"
            | ``Patient Age years`` -> "years"
            | ``Patient Age month`` -> "month"
            | ``Patient Age months`` -> "months"
            | ``Patient Age week`` -> "week"
            | ``Patient Age weeks`` -> "weeks"
            | ``Patient Age day`` -> "day"
            | ``Patient Age days`` -> "days"
            | ``Patient Estimated`` -> "estimated"
            | ``Patient Years`` -> "Year"
            | ``Patient Months`` -> "Month"
            | ``Patient Weeks`` -> "Week"
            | ``Patient Days`` -> "Day"
            | ``Patient Weight`` -> "Weight"
            | ``Patient Length`` -> "Length"
            | ``Patient remove patient data`` -> "Remove"
            | ``Emergency List`` -> "Emergency List"
            | ``Emergency List show when patient data`` ->
                "Will be shown when patient data is entered"
            | ``Emergency List Indication`` -> "Indication"
            | ``Emergency List Intervention`` -> "Intervention"
            | ``Emergency List Calculated`` -> "Calculation"
            | ``Emergency List Preparation`` -> "Preparation"
            | ``Emergency List Advice`` -> "Advised"
            | ``Continuous Medication List`` -> "Continuous Medication"
            | ``Continuous Medication List show when patient data`` ->
                "Will be shown when patient data is entered"
            | ``Continuous Medication Indication`` -> "Indication"
            | ``Continuous Medication Medication`` -> "Medication"
            | ``Continuous Medication Quantity`` -> "Quantity"
            | ``Continuous Medication Solution`` -> "Solution"
            | ``Continuous Medication Dose`` -> "Dose"
            | ``Continuous Medication Advice`` -> "Advice"
            | ``Sort By`` -> "Sort By"

        | Dutch ->
            match term with
            | Unknown -> "onbekend"
            | ``Patient enter patient data`` -> "Voer patient gegevens in ..."
            | ``Patient Age`` -> "Leeftijd"
            | ``Patient Age year`` -> "jaar"
            | ``Patient Age years`` -> "jaren"
            | ``Patient Age month`` -> "maand"
            | ``Patient Age months`` -> "maanden"
            | ``Patient Age week`` -> "week"
            | ``Patient Age weeks`` -> "weken"
            | ``Patient Age day`` -> "dag"
            | ``Patient Age days`` -> "dagen"
            | ``Patient Estimated`` -> "geschat"
            | ``Patient Years`` -> "Jaar"
            | ``Patient Months`` -> "Maand"
            | ``Patient Weeks`` -> "Week"
            | ``Patient Days`` -> "Dag"
            | ``Patient Weight`` -> "Gewicht"
            | ``Patient Length`` -> "Lengte"
            | ``Patient remove patient data`` -> "Verwijder"
            | ``Emergency List`` -> "Noodlijst"
            | ``Emergency List show when patient data`` ->
                "Wordt getoond na invoer van patient gegevens"
            | ``Emergency List Indication`` -> "Indicatie"
            | ``Emergency List Intervention`` -> "Interventie"
            | ``Emergency List Calculated`` -> "Berekend"
            | ``Emergency List Preparation`` -> "Bereiding"
            | ``Emergency List Advice`` -> "Advies"
            | ``Continuous Medication List`` -> "Continue Medicatie"
            | ``Continuous Medication List show when patient data`` ->
                "Wordt getoond na invoer van patient gegevens"
            | ``Continuous Medication Indication`` -> "Indicatie"
            | ``Continuous Medication Medication`` -> "Medicatie"
            | ``Continuous Medication Quantity`` -> "Hoeveelheid"
            | ``Continuous Medication Solution`` -> "Oplossing"
            | ``Continuous Medication Dose`` -> "Dosering"
            | ``Continuous Medication Advice`` -> "Advies"
            | ``Sort By`` -> "Sorteren op"