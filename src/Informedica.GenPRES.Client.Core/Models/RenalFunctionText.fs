namespace Informedica.GenPRES.Client.Core.Models

/// The renal functions the patient panel offers: the value of each option of the select, and the
/// term that labels it.
module RenalFunctionText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// An option of the renal function select.
    type RenalOption =
        {
            /// The value of the option, the same in every language.
            Value: string
            /// The renal function the option stands for.
            RenalFunction: RenalFunction
            /// The term that labels the option.
            Term: Terms
        }


    /// The options of the select, from the highest eGFR down, then the dialyses.
    let renalOptions =
        [|
            {
                Value = "> 50 mL/min/1,73 m2"
                RenalFunction = EGFR(Some 50, None)
                Term = Terms.``Patient Renal eGFR Above 50``
            }
            {
                Value = "30 - 50 mL/min/1,73 m2"
                RenalFunction = EGFR(Some 30, Some 50)
                Term = Terms.``Patient Renal eGFR 30 To 50``
            }
            {
                Value = "10 - 30 mL/min/1,73 m2"
                RenalFunction = EGFR(Some 10, Some 30)
                Term = Terms.``Patient Renal eGFR 10 To 30``
            }
            {
                Value = "< 10 mL/min/1,73 m2"
                RenalFunction = EGFR(None, Some 10)
                Term = Terms.``Patient Renal eGFR Below 10``
            }
            {
                Value = "Intermitterende Hemodialyse"
                RenalFunction = IntermittentHemodialysis
                Term = Terms.``Patient Renal Intermittent Hemodialysis``
            }
            {
                Value = "Continue Hemodialyse"
                RenalFunction = ContinuousHemodialysis
                Term = Terms.``Patient Renal Continuous Hemodialysis``
            }
            {
                Value = "Peritioneaal dialyse"
                RenalFunction = PeritonealDialysis
                Term = Terms.``Patient Renal Peritoneal Dialysis``
            }
        |]


    /// The values of the select.
    let options = renalOptions |> Array.map _.Value


    /// Returns the select option for a renal function. An eGFR goes to the band that contains its maximum,
    /// or to above 50 when it has no maximum.
    let optionOf renal =
        let band =
            match renal with
            | EGFR(_, Some max) when max <= 10 -> EGFR(None, Some 10)
            | EGFR(_, Some max) when max <= 30 -> EGFR(Some 10, Some 30)
            | EGFR(_, Some max) when max <= 50 -> EGFR(Some 30, Some 50)
            | EGFR _ -> EGFR(Some 50, None)
            | _ -> renal

        renalOptions |> Array.find (fun o -> o.RenalFunction = band)


    /// The value of the option a renal function falls in.
    let renalToOption renal = (optionOf renal).Value


    /// The renal function of a value of the select; an unknown value is an eGFR above 50.
    let optionToRenal s =
        renalOptions
        |> Array.tryFind (fun o -> o.Value = s)
        |> Option.map _.RenalFunction
        |> Option.defaultValue (EGFR(Some 50, None))


    /// The term of a renal function, as the patient panel offers it.
    let term renal = (optionOf renal).Term


    /// Returns the English text of a renal function term, used when the sheet has no translation or has
    /// not loaded. Returns the name of any other term.
    let english =
        function
        | Terms.``Patient Renal eGFR Above 50`` -> "> 50 mL/min/1.73 m2"
        | Terms.``Patient Renal eGFR 30 To 50`` -> "30 - 50 mL/min/1.73 m2"
        | Terms.``Patient Renal eGFR 10 To 30`` -> "10 - 30 mL/min/1.73 m2"
        | Terms.``Patient Renal eGFR Below 10`` -> "< 10 mL/min/1.73 m2"
        | Terms.``Patient Renal Intermittent Hemodialysis`` -> "Intermittent hemodialysis"
        | Terms.``Patient Renal Continuous Hemodialysis`` -> "Continuous hemodialysis"
        | Terms.``Patient Renal Peritoneal Dialysis`` -> "Peritoneal dialysis"
        | term -> $"{term}"


    /// Returns each select value with its translated label. optionToRenal reads a value back in any
    /// language.
    let labelled tr = renalOptions |> Array.map (fun o -> o.Value, tr o.Term)
