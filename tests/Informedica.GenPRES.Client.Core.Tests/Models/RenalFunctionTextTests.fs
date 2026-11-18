/// The renal function as the patient panel offers it: the values of the select and their labels.
module Informedica.GenPRES.Client.Core.Tests.Models.RenalFunctionTextTests

open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Client.Core.Models
open Expecto
open Expecto.Flip


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

            test "the values of the select do not depend on the labels" {
                RenalFunctionText.labelled (fun _ -> "label")
                |> Array.map fst
                |> Expect.equal "the options" RenalFunctionText.options
            }

            test "the labels of the select are the translated terms" {
                RenalFunctionText.labelled RenalFunctionText.english
                |> Array.map snd
                |> Expect.equal
                    "the English"
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

        ]
