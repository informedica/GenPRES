module Informedica.GenPRES.Client.Core.Tests.LoaderMachineTests

open Expecto
open Expecto.Flip
open Shared.Types
open Busy
open LoaderMachine


let bolus hospital : BolusMedication =
    {
        Hospital = hospital
        Category = "cat"
        Generic = "adrenaline"
        MinWeight = 0.
        MaxWeight = 0.
        NormDose = 0.
        MinDose = 0.
        MaxDose = 0.
        Concentration = 0.
        Unit = "mg"
        Remark = ""
        TemplateGeneric = ""
        TemplateRoute = ""
        TemplateDoseType = ""
        TemplateIndication = ""
    }


let run msgs =
    msgs
    |> List.fold (fun (state, _) msg -> state |> transition msg) (LoaderState.initial, [])


[<Tests>]
let tests =
    testList
        "LoaderMachine"
        [
            testList
                "a start sets the reading out and makes the call"
                [
                    for load, effect in
                        [
                            Load.Settings, LoaderEffect.FetchSettings
                            Load.Localization, LoaderEffect.FetchLocalization
                            Load.NormalValues, LoaderEffect.FetchNormalValues
                            Load.BolusMedication, LoaderEffect.FetchBolusMedication
                            Load.ContinuousMedication, LoaderEffect.FetchContinuousMedication
                            Load.Products, LoaderEffect.FetchProducts
                        ] do
                        test $"%A{load}" {
                            let state, effects = run [ LoaderMsg.Start load ]

                            effects |> Expect.equal "the call" [ effect ]
                            state |> out |> Expect.equal "out" [ load ]
                        }
                ]

            test "a start while the same load runs changes nothing" {
                let state, _ = run [ LoaderMsg.Start Load.Products ]

                state
                |> transition (LoaderMsg.Start Load.Products)
                |> Expect.equal "unchanged" (state, [])
            }

            test "a start of a load this machine does not hold changes nothing" {
                LoaderState.initial
                |> transition (LoaderMsg.Start Load.Formulary)
                |> Expect.equal "unchanged" (LoaderState.initial, [])
            }

            test "an answer resolves the reading, and the load is loaded" {
                let state, effects = run [ LoaderMsg.Start Load.Products; LoaderMsg.Landed(Landing.Products(Ok [])) ]

                effects |> Expect.isEmpty "no call"
                state.Products |> Expect.equal "resolved" (Resolved [])
                state |> out |> Expect.isEmpty "nothing out"
                state |> loaded |> Expect.equal "loaded" [ Load.Products ]
            }

            test "a failed required load goes back to not started and is named" {
                let state, _ =
                    run
                        [
                            LoaderMsg.Start Load.Products
                            LoaderMsg.Landed(Landing.Products(Error "down"))
                        ]

                state.Products |> Expect.equal "not started" HasNotStartedYet
                state.Failed |> Expect.equal "named" [ Load.Products ]
            }

            test "the settings failing are not named, since the application runs without them" {
                let state, _ =
                    run
                        [
                            LoaderMsg.Start Load.Settings
                            LoaderMsg.Landed(Landing.Settings(Error "down"))
                        ]

                state.Settings |> Expect.equal "not started" HasNotStartedYet
                state.Failed |> Expect.isEmpty "not named"
            }

            test "the emergency medication gives the hospitals, each once, a blank one left out" {
                let state, _ =
                    run
                        [
                            LoaderMsg.Start Load.BolusMedication
                            LoaderMsg.Landed(
                                Landing.BolusMedication(Ok [ bolus "UMCU"; bolus "AMC"; bolus "UMCU"; bolus " " ])
                            )
                        ]

                state.Hospitals |> Expect.equal "the hospitals" (Resolved [| "UMCU"; "AMC" |])

                state
                |> loaded
                |> Expect.equal "both loaded" [ Load.Hospitals; Load.BolusMedication ]
            }

            test "the emergency medication failing leaves the hospitals as they were" {
                let state, _ =
                    run
                        [
                            LoaderMsg.Start Load.BolusMedication
                            LoaderMsg.Landed(Landing.BolusMedication(Error "down"))
                        ]

                state.Hospitals |> Expect.equal "not started" HasNotStartedYet
                state.Failed |> Expect.equal "named" [ Load.BolusMedication ]
            }
        ]
