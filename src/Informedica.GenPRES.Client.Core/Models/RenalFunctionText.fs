namespace Informedica.GenPRES.Client.Core.Models

/// The renal function as the patient panel offers it: one text per option.
module RenalFunctionText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    let options =
        [|
            "> 50 mL/min/1,73 m2"
            "30 - 50 mL/min/1,73 m2"
            "10 - 30 mL/min/1,73 m2"
            "< 10 mL/min/1,73 m2"
            "Intermitterende Hemodialyse"
            "Continue Hemodialyse"
            "Peritioneaal dialyse"
        |]


    let renalToOption =
        function
        | EGFR(min, max) ->
            match min, max with
            | _, Some max when max <= 10 -> options[3]
            | _, Some max when max <= 30 -> options[2]
            | _, Some max when max <= 50 -> options[1]
            | _ -> options[0]
        | IntermittentHemodialysis -> options[4]
        | ContinuousHemodialysis -> options[5]
        | PeritonealDialysis -> options[6]


    let optionToRenal s =
        match s with
        | s when s = options[1] -> EGFR(Some 30, Some 50)
        | s when s = options[2] -> EGFR(Some 10, Some 30)
        | s when s = options[3] -> EGFR(None, Some 10)
        | s when s = options[4] -> IntermittentHemodialysis
        | s when s = options[5] -> ContinuousHemodialysis
        | s when s = options[6] -> PeritonealDialysis
        | _ -> EGFR(Some 50, None)
