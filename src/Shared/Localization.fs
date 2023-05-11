namespace Shared


type Terms =
    | ``Patient enter patient data``
    | ``Patient Age``
    | ``Patient GA Age``
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
    | ``Prescribe``
    | ``Formulary``
    | ``Parenteralia``
    | ``Sort By``



module Localization =

    open System


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


    let getTerm (terms : string [][]) locale term =
        let term = $"{term}"
        let indx =
            match locale with
            | English -> 1
            | Dutch -> 2

        terms
        |> Array.tryFind (fun r ->
            r[0] = term
        )
        |> Option.map (fun r -> r[indx])
        |> fun r -> 
            if r.IsNone then printfn $"cannot find term: {term}"
            r
