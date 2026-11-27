/// The renal function as the patient panel offers it: the values of the select and their labels.
module Informedica.GenPRES.Client.Core.Tests.Models.RenalFunctionTextTests

open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Client.Core.Models
open Expecto
open Expecto.Flip


/// The renal function options the server sends: the four eGFR bands with the text of their sheet row, and the
/// three dialyses.
let options: PatientDataOptions =
    {
        Weights = [||]
        Heights = [||]
        Years = []
        Months = []
        Weeks = []
        Days = []
        GestationalWeeks = []
        GestationalDays = []
        EGFRBands =
            [
                {
                    RenalFunction = EGFR(Some 50, None)
                    Text = "eGFR > 50"
                }
                {
                    RenalFunction = EGFR(Some 30, Some 50)
                    Text = "eGFR 30 - 50"
                }
                {
                    RenalFunction = EGFR(Some 10, Some 30)
                    Text = "eGFR 10 - 30"
                }
                {
                    RenalFunction = EGFR(None, Some 10)
                    Text = "eGFR < 10"
                }
            ]
        Dialyses = [ IntermittentHemodialysis; ContinuousHemodialysis; PeritonealDialysis ]
    }


let renals =
    [
        EGFR(Some 50, None)
        EGFR(Some 30, Some 50)
        EGFR(Some 10, Some 30)
        EGFR(None, Some 10)
        IntermittentHemodialysis
        ContinuousHemodialysis
        PeritonealDialysis
    ]


[<Tests>]
let tests =
    testList
        "RenalFunctionText"
        [
            test "every renal function has its own term" {
                renals
                |> List.map RenalFunctionText.term
                |> List.distinct
                |> List.length
                |> Expect.equal "seven terms" 7
            }

            test "an option reads back to its renal function" {
                for renal in renals do
                    renal
                    |> RenalFunctionText.renalToOption
                    |> RenalFunctionText.optionToRenal
                    |> RenalFunctionText.term
                    |> Expect.equal $"%A{renal}" (RenalFunctionText.term renal)
            }

            test "the bands and the dialyses of the options are labelled with their translated term" {
                options
                |> RenalFunctionText.ofOptions RenalFunctionText.english
                |> Array.map snd
                |> Expect.equal
                    "the English, not the sheet text"
                    [|
                        "> 50 mL/min/1.73 m2"
                        "30 - 50 mL/min/1.73 m2"
                        "10 - 30 mL/min/1.73 m2"
                        "< 10 mL/min/1.73 m2"
                        "Intermittent hemodialysis"
                        "Continuous hemodialysis"
                        "Peritoneal dialysis"
                    |]
            }

            test "the values from the options read back as their renal function" {
                options
                |> RenalFunctionText.ofOptions string
                |> Array.map (fst >> RenalFunctionText.optionToRenal)
                |> Expect.equal
                    "the bands and the dialyses"
                    ((options.EGFRBands |> List.map _.RenalFunction) @ options.Dialyses
                     |> List.toArray)
            }

        ]
