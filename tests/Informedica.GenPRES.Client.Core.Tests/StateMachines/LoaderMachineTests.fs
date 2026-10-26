module Informedica.GenPRES.Client.Core.Tests.StateMachines.LoaderMachineTests

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Shared.Api
open Loads
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


let drugNames names notice =
    Landing.DrugNames(
        Some(OpenedToken "t"),
        Ok
            {
                Response = InteractionResponse.DrugNamesLoaded names
                Notice = notice
            }
    )


let patient = Patient.empty


let filter generic = { OrderContext.empty.Filter with Generic = Some generic }


let answered (reply: 'a) =
    Ok
        {
            Response = reply
            Notice = None
        }


let drugNamesFailed = Landing.DrugNames(None, Error [| "down" |])


let interaction =
    {
        Name = "a", "b"
        Drug1 = "a"
        Drug2 = "b"
    }


let interactions check rows =
    Landing.Interactions(check, None, answered (InteractionResponse.InteractionsChecked rows))


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
                            Load.DrugNames, LoaderEffect.FetchDrugNames
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

            test "a start over loaded data keeps it shown while it is asked again" {
                let state, effects =
                    run
                        [
                            LoaderMsg.Start Load.DrugNames
                            LoaderMsg.Landed(drugNames [| "a" |] None)
                            LoaderMsg.Start Load.DrugNames
                        ]

                effects |> Expect.equal "the call" [ LoaderEffect.FetchDrugNames ]
                state.DrugNames |> Expect.equal "still shown" (Refreshing [| "a" |])
                state |> out |> Expect.equal "out" [ Load.DrugNames ]
            }

            test "a start of a load this machine does not hold changes nothing" {
                LoaderState.initial
                |> transition (LoaderMsg.Start Load.LogFiles)
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

            testList
                "the formulary and parenteralia pages"
                [
                    test "the patient set asks both pages again, the formulary for that patient" {
                        let state, effects = run [ LoaderMsg.PatientSet(Some patient) ]

                        effects
                        |> Expect.equal
                            "both asked"
                            [
                                LoaderEffect.FetchFormulary { Formulary.empty with Patient = Some patient }
                                LoaderEffect.FetchParenteralia Parenteralia.empty
                            ]

                        state |> out |> Expect.equal "both out" [ Load.Formulary; Load.Parenteralia ]
                    }

                    test "a filter answered puts its choices on both pages and asks them again" {
                        let _, effects = run [ LoaderMsg.FilterAnswered(filter "paracetamol") ]

                        effects
                        |> Expect.equal
                            "both asked with the generic"
                            [
                                LoaderEffect.FetchFormulary { Formulary.empty with Generic = Some "paracetamol" }
                                LoaderEffect.FetchParenteralia { Parenteralia.empty with Generic = Some "paracetamol" }
                            ]
                    }

                    test "a filter answered during a load is asked again once, the latest one" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.Start Load.Formulary
                                    LoaderMsg.FilterAnswered(filter "paracetamol")
                                    LoaderMsg.FilterAnswered(filter "morfine")
                                ]

                        effects |> Expect.isEmpty "both pages out, nothing asked"
                        state.FormularyAskAgain
                        |> Expect.equal "the latest held" (Some(filter "morfine"))

                        let state, effects =
                            state
                            |> transition (LoaderMsg.Landed(Landing.Formulary(None, answered Formulary.empty)))

                        effects
                        |> Expect.equal
                            "asked again with the latest"
                            [
                                LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Formulary
                                LoaderEffect.FetchFormulary { Formulary.empty with Generic = Some "morfine" }
                            ]

                        let _, effects =
                            state
                            |> transition (LoaderMsg.Landed(Landing.Formulary(None, answered Formulary.empty)))

                        effects
                        |> Expect.equal
                            "not again"
                            [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Formulary ]
                    }

                    test "the patient set drops a filter held for the earlier patient" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.Start Load.Formulary
                                    LoaderMsg.Start Load.Parenteralia
                                    LoaderMsg.FilterAnswered(filter "morfine")
                                    LoaderMsg.PatientSet None
                                ]

                        let state, formulary =
                            state
                            |> transition (LoaderMsg.Landed(Landing.Formulary(None, answered Formulary.empty)))

                        formulary
                        |> Expect.equal
                            "not asked again"
                            [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Formulary ]

                        let _, parenteralia =
                            state
                            |> transition (LoaderMsg.Landed(Landing.Parenteralia(None, answered Parenteralia.empty)))

                        parenteralia
                        |> Expect.equal
                            "not asked again"
                            [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Parenteralia ]
                    }

                    test "a failed page raises the error under its source" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.Start Load.Parenteralia
                                    LoaderMsg.Landed(Landing.Parenteralia(None, Error [| "down" |]))
                                ]

                        effects
                        |> Expect.equal
                            "raised"
                            [
                                LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Parenteralia, [| "down" |])
                            ]

                        state.Parenteralia |> Expect.equal "not started" HasNotStartedYet
                    }

                    test "a formulary change with a patient seeds the workbench with the formulary's rule" {
                        let form =
                            { Formulary.empty with
                                Generic = Some "paracetamol"
                                Route = Some "oraal"
                            }

                        let state, _ = run [ LoaderMsg.PatientSet(Some patient) ]

                        let state =
                            state
                            |> transition (LoaderMsg.Landed(Landing.Formulary(None, answered Formulary.empty)))
                            |> fst
                            |> transition (LoaderMsg.Landed(Landing.Parenteralia(None, answered Parenteralia.empty)))
                            |> fst

                        let _, effects = state |> transition (LoaderMsg.FormularyChanged form)

                        effects
                        |> Expect.equal
                            "asked and seeded"
                            [
                                LoaderEffect.FetchFormulary { form with Patient = Some patient }
                                LoaderEffect.SeedWorkbench
                                    {
                                        Source = SeedSource.Formulary
                                        Indication = None
                                        Generic = Some "paracetamol"
                                        Route = Some "oraal"
                                        Form = None
                                        DoseType = None
                                    }
                                LoaderEffect.FetchParenteralia
                                    { Parenteralia.empty with
                                        Generic = Some "paracetamol"
                                        Route = Some "oraal"
                                    }
                            ]
                    }

                    test "a parenteralia change puts its choices on the formulary and clears the rest" {
                        let form =
                            { Formulary.empty with
                                Indication = Some "pijn"
                                Generic = Some "paracetamol"
                                Route = Some "oraal"
                                Form = Some "tablet"
                                DoseType = Some(Once "")
                            }

                        let par =
                            { Parenteralia.empty with
                                Generic = Some "morfine"
                                Route = Some "iv"
                                Form = Some "injectievloeistof"
                            }

                        let state, _ =
                            run
                                [
                                    LoaderMsg.Start Load.Formulary
                                    LoaderMsg.Landed(Landing.Formulary(None, answered form))
                                    LoaderMsg.ParenteraliaChanged par
                                ]

                        state.Formulary
                        |> Expect.equal
                            "the parenteralia's choices, no indication nor dose type"
                            (Refreshing
                                { form with
                                    Indication = None
                                    Generic = Some "morfine"
                                    Route = Some "iv"
                                    Form = Some "injectievloeistof"
                                    DoseType = None
                                })
                    }

                    test "a parenteralia change without a patient seeds nothing" {
                        let par = { Parenteralia.empty with Generic = Some "morfine" }
                        let _, effects = run [ LoaderMsg.ParenteraliaChanged par ]

                        effects
                        |> List.exists (
                            function
                            | LoaderEffect.SeedWorkbench _ -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "no seed"
                    }

                    test "the resources reloaded with a patient seed the workbench, without one ask both pages" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.PatientSet(Some patient)
                                    LoaderMsg.Landed(Landing.Formulary(None, answered Formulary.empty))
                                    LoaderMsg.Landed(Landing.Parenteralia(None, answered Parenteralia.empty))
                                ]

                        state
                        |> transition LoaderMsg.ResourcesReloaded
                        |> snd
                        |> Expect.equal
                            "seeded"
                            [
                                LoaderEffect.SeedWorkbench
                                    {
                                        Source = SeedSource.Reload
                                        Indication = None
                                        Generic = None
                                        Route = None
                                        Form = None
                                        DoseType = None
                                    }
                            ]

                        LoaderState.initial
                        |> transition LoaderMsg.ResourcesReloaded
                        |> snd
                        |> Expect.equal
                            "both asked"
                            [
                                LoaderEffect.FetchFormulary Formulary.empty
                                LoaderEffect.FetchParenteralia Parenteralia.empty
                            ]
                    }

                    test "a page shown asks its page and the drug names that gave up" {
                        LoaderState.initial
                        |> transition (LoaderMsg.PageShown Page.Page.Formulary)
                        |> snd
                        |> Expect.equal
                            "the drug names and the formulary"
                            [ LoaderEffect.FetchDrugNames; LoaderEffect.FetchFormulary Formulary.empty ]
                    }
                ]

            testList
                "the server check"
                [
                    test "a failure raises the error with the server source and checks again, with no alert" {
                        let state, effects =
                            run [ LoaderMsg.CheckServer; LoaderMsg.ServerChecked(Error "connection refused") ]

                        effects
                        |> Expect.equal
                            "raised and checked again"
                            [
                                LoaderEffect.RequestFailed(
                                    ServerErrorPolicy.ErrorSource.Server,
                                    [| "connection refused" |]
                                )
                                LoaderEffect.CheckServerLater serverWait
                            ]

                        state.Server |> Expect.equal "down" (Resolved false)
                    }

                    test "a success clears the error and is not checked again" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.Start Load.DrugNames
                                    LoaderMsg.CheckServer
                                    LoaderMsg.ServerChecked(Ok())
                                ]

                        effects
                        |> Expect.equal "cleared" [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Server ]

                        state.Server |> Expect.equal "up" (Resolved true)
                    }

                    test "a success asks the drug names that are not loaded nor out" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.Start Load.DrugNames
                                    LoaderMsg.Landed drugNamesFailed
                                    LoaderMsg.CheckServer
                                    LoaderMsg.ServerChecked(Ok())
                                ]

                        effects
                        |> Expect.equal
                            "cleared and asked"
                            [
                                LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Server
                                LoaderEffect.FetchDrugNames
                            ]

                        state |> out |> Expect.equal "out" [ Load.DrugNames ]
                    }

                    test "a check while one runs changes nothing" {
                        let state, _ = run [ LoaderMsg.CheckServer ]

                        state
                        |> transition LoaderMsg.CheckServer
                        |> Expect.equal "unchanged" (state, [])
                    }
                ]

            testList
                "the drug names"
                [
                    test "a failure asks again after a wait" {
                        let state, effects = run [ LoaderMsg.Start Load.DrugNames; LoaderMsg.Landed drugNamesFailed ]

                        effects
                        |> Expect.equal "asked again" [ LoaderEffect.AskAgainLater(Load.DrugNames, drugNamesWait) ]

                        state.DrugNames |> Expect.equal "not started" HasNotStartedYet
                        state.Failed |> Expect.isEmpty "not named, the application runs without them"
                    }

                    test "the third failure gives up with an alert" {
                        let tries =
                            [
                                for _ in 1..drugNameLimit do
                                    LoaderMsg.Start Load.DrugNames
                                    LoaderMsg.Landed drugNamesFailed
                            ]

                        let state, effects = run tries

                        effects
                        |> Expect.equal "the alert" [ LoaderEffect.Alert Alert.Alert.DrugNamesNotLoaded ]

                        state.DrugNameFailures |> Expect.equal "three failures" drugNameLimit
                    }

                    test "an answer resolves the names and counts the failures again from none" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.Start Load.DrugNames
                                    LoaderMsg.Landed drugNamesFailed
                                    LoaderMsg.Start Load.DrugNames
                                    LoaderMsg.Landed(drugNames [| "a"; "b" |] None)
                                ]

                        effects |> Expect.isEmpty "nothing told"
                        state.DrugNames |> Expect.equal "resolved" (Resolved [| "a"; "b" |])
                        state.DrugNameFailures |> Expect.equal "no failures" 0
                    }

                    test "a notice on the answer is passed on with the token the request was sent with" {
                        let notice = RecordNotice.Ended SessionEnding.SupersededByLaunch

                        let _, effects =
                            run
                                [
                                    LoaderMsg.Start Load.DrugNames
                                    LoaderMsg.Landed(drugNames [||] (Some notice))
                                ]

                        effects
                        |> Expect.equal "the notice" [ LoaderEffect.NoticeReceived(Some(OpenedToken "t"), notice) ]
                    }
                ]

            testList
                "the interactions"
                [
                    test "a check of two drugs asks for them under the next number" {
                        let state, effects = run [ LoaderMsg.CheckInteractions [ "a"; "b" ] ]

                        effects
                        |> Expect.equal "asked" [ LoaderEffect.FetchInteractions(1, [ "a"; "b" ]) ]
                        state |> out |> Expect.equal "out" [ Load.Interactions ]
                    }

                    test "an answer to an earlier check is dropped" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.CheckInteractions [ "a"; "b" ]
                                    LoaderMsg.CheckInteractions [ "a"; "c" ]
                                ]

                        state
                        |> transition (LoaderMsg.Landed(interactions 1 [| interaction |]))
                        |> Expect.equal "unchanged" (state, [])

                        let state, effects = state |> transition (LoaderMsg.Landed(interactions 2 [||]))

                        state.Interactions |> Expect.equal "the later answer" (Resolved [||])

                        effects
                        |> Expect.equal
                            "succeeded, the notice withdrawn"
                            [
                                LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Interactions
                                LoaderEffect.WithdrawInteractionsFound
                            ]
                    }

                    test "interactions found are told by their number" {
                        let _, effects =
                            run
                                [
                                    LoaderMsg.CheckInteractions [ "a"; "b" ]
                                    LoaderMsg.Landed(interactions 1 [| interaction; interaction |])
                                ]

                        effects
                        |> Expect.equal
                            "told"
                            [
                                LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Interactions
                                LoaderEffect.Alert(Alert.Alert.InteractionsFound 2)
                            ]
                    }

                    test "the rows shown stay while the drugs are checked again" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.CheckInteractions [ "a"; "b" ]
                                    LoaderMsg.Landed(interactions 1 [| interaction |])
                                    LoaderMsg.CheckInteractions [ "a"; "b"; "c" ]
                                ]

                        state.Interactions |> Expect.equal "still shown" (Refreshing [| interaction |])
                    }

                    test "one drug clears the rows, withdraws the notice and drops the check out" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.CheckInteractions [ "a"; "b" ]
                                    LoaderMsg.CheckInteractions [ "a" ]
                                ]

                        effects |> Expect.equal "withdrawn" [ LoaderEffect.WithdrawInteractionsFound ]
                        state.Interactions |> Expect.equal "cleared" HasNotStartedYet

                        state
                        |> transition (LoaderMsg.Landed(interactions 1 [| interaction |]))
                        |> Expect.equal "the check out dropped" (state, [])
                    }

                    test "a failed check raises the error under its source" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.CheckInteractions [ "a"; "b" ]
                                    LoaderMsg.Landed(Landing.Interactions(1, None, Error [| "down" |]))
                                ]

                        effects
                        |> Expect.equal
                            "raised"
                            [
                                LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Interactions, [| "down" |])
                            ]

                        state.Interactions |> Expect.equal "not started" HasNotStartedYet
                    }
                ]
        ]
