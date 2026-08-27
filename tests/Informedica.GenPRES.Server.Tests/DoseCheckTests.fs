module Informedica.GenPRES.Server.Tests.DoseCheckTests

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Shared.Types
open ServerApi.FormularyService

module Check = Check


/// Minimal TextItem parser used by the build function under test.
/// Avoids pulling in the Mappers module-level formatting state.
let parseTextItem (s: string) =
    if System.String.IsNullOrWhiteSpace s then
        [||]
    else
        [| Normal s |]


let tab (target: string) (route: string) (pat: string) (msg: string) =
    $"%s{target}\t%s{route}\t%s{pat}\t%s{msg}"


let ctorName =
    function
    | Valid _ -> "Valid"
    | Caution _ -> "Caution"
    | Warning _ -> "Warning"
    | Alert _ -> "Alert"


/// A graded dose-check signal (Severity, raw tab-separated line).
let sigOf sev target route pat msg = sev, tab target route pat msg


let doseCheckTests =
    testList
        "DoseCheck.build severity classification"
        [

            test "no check lines → single Valid 'Ok!'" {
                let result = [||] |> DoseCheck.build parseTextItem true

                result.Length |> Expect.equal "one block" 1
                result[0] |> ctorName |> Expect.equal "Valid" "Valid"
            }

            test "only 'geen doseer bewaking' sentinel → Caution (blue info)" {
                let sentinel = Check.NoMonitoring, "geen doseer bewaking gevonden voor paracetamol"

                let result = [| sentinel |] |> DoseCheck.build parseTextItem true

                result
                |> Array.forall (fun tb -> ctorName tb = "Caution")
                |> Expect.isTrue "sentinel signals 'no rules to check', must be Caution not Valid"
            }

            test "multiple 'geen doseer bewaking' sentinels → all Caution" {
                let lines =
                    [|
                        Check.NoMonitoring, "geen doseer bewaking gevonden voor aciclovir"
                        Check.NoMonitoring, "geen doseer bewaking gevonden voor paracetamol"
                    |]

                let result = lines |> DoseCheck.build parseTextItem false

                result.Length |> Expect.equal "two blocks" 2

                result
                |> Array.forall (fun tb -> ctorName tb = "Caution")
                |> Expect.isTrue "both Caution"
            }

            test "frequency mismatch → Warning" {
                let lines =
                    [|
                        sigOf Check.FrequencyMismatch "paracetamol" "oraal" "0-1 jaar" "frequenties tekst 24"
                    |]

                let result = lines |> DoseCheck.build parseTextItem false

                result
                |> Array.forall (fun tb -> ctorName tb = "Warning")
                |> Expect.isTrue "all Warning"
            }

            test "advisory norm-max breach → Warning (orange)" {
                let lines =
                    [|
                        sigOf Check.AdvisoryOverNorm "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"
                    |]

                let result = lines |> DoseCheck.build parseTextItem true

                result
                |> Array.forall (fun tb -> ctorName tb = "Warning")
                |> Expect.isTrue "advisory is orange, not red"
            }

            test "absolute-max breach → Alert (red)" {
                let lines =
                    [|
                        sigOf Check.OverAbsolute "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"
                    |]

                let result = lines |> DoseCheck.build parseTextItem true

                result
                |> Array.forall (fun tb -> ctorName tb = "Alert")
                |> Expect.isTrue "all Alert"
            }

            test "unit mismatch → Caution (blue)" {
                let lines =
                    [|
                        sigOf Check.UnitMismatch "paracetamol" "oraal" "0-1 jaar" "eenheden verschillen (kg vs m2)"
                    |]

                let result = lines |> DoseCheck.build parseTextItem true

                result
                |> Array.forall (fun tb -> ctorName tb = "Caution")
                |> Expect.isTrue "all Caution"
            }

            test "mixed advisory + absolute → graded per line (Warning and Alert)" {
                let lines =
                    [|
                        sigOf Check.AdvisoryOverNorm "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"
                        sigOf Check.OverAbsolute "paracetamol" "oraal" "0-1 jaar" "dosering per kg niet in bereik"
                    |]

                let result = lines |> DoseCheck.build parseTextItem false

                result
                |> Array.map ctorName
                |> Array.sort
                |> Expect.equal "one Warning, one Alert" [| "Alert"; "Warning" |]
            }

            test "violation alongside sentinel → sentinel dropped" {
                let sentinel = Check.NoMonitoring, "geen doseer bewaking gevonden voor paracetamol"

                let breach =
                    sigOf Check.OverAbsolute "paracetamol" "oraal" "0-1 jaar" "keer dosering niet in bereik"

                let result = [| sentinel; breach |] |> DoseCheck.build parseTextItem false

                result.Length |> Expect.equal "sentinel dropped, one block left" 1
                result[0] |> ctorName |> Expect.equal "Alert" "Alert"
            }

            test "isFrequency detects 'frequenties' in the 4th tab field" {
                let freqLine = tab "x" "y" "z" "frequenties 4 x per dag niet gelijk aan 6 x per dag"

                let doseLine = tab "x" "y" "z" "keer dosering per dag niet in bereik"

                DoseCheck.isFrequency freqLine |> Expect.isTrue "frequency"
                DoseCheck.isFrequency doseLine |> Expect.isFalse "not frequency"
            }
        ]


[<Tests>]
let tests = testList "DoseCheck Tests" [ doseCheckTests ]
