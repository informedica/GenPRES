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


let drugNames names notice =
    Landing.DrugNames(
        Some(OpenedToken "t"),
        Ok
            {
                Response = InteractionResponse.DrugNamesLoaded names
                Notice = notice
            }
    )


let patient = PatientData.empty


/// Two patients at different departments, for a reply that lands after the patient changed.
let patientA = { PatientData.empty with Department = Some "ICK" }
let patientB = { PatientData.empty with Department = Some "NEO" }


/// The parenteralia the server answers for the patient, with markdown that names the department.
let parenteraliaFor (patient: PatientData) =
    { Parenteralia.empty with
        PatientData = Some patient
        Markdown = patient.Department |> Option.defaultValue ""
    }


/// The formulary the server answers for the patient, with markdown that names the department.
let formularyFor (patient: PatientData) =
    { Formulary.empty with
        PatientData = Some patient
        Markdown = patient.Department |> Option.defaultValue ""
    }


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
                            Load.DrugNames, LoaderEffect.FetchDrugNames
                        ] do
                        test $"%A{load}" {
                            let state, effects = run [ LoaderMsg.Start load ]

                            effects |> Expect.equal "the call" [ effect ]
                            state |> out |> Expect.equal "out" [ load ]
                        }
                ]

            test "a start while the same load runs changes nothing" {
                let state, _ = run [ LoaderMsg.Start Load.Localization ]

                state
                |> transition (LoaderMsg.Start Load.Localization)
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
                let state, effects =
                    run
                        [
                            LoaderMsg.Start Load.Localization
                            LoaderMsg.Landed(Landing.Localization(Ok [||]))
                        ]

                effects |> Expect.isEmpty "no call"
                state.Localization |> Expect.equal "resolved" (Resolved [||])
                state |> out |> Expect.isEmpty "nothing out"
                state |> loaded |> Expect.equal "loaded" [ Load.Localization ]
            }

            test "a failed required load goes back to not started and is named" {
                let state, _ =
                    run
                        [
                            LoaderMsg.Start Load.Localization
                            LoaderMsg.Landed(Landing.Localization(Error "down"))
                        ]

                state.Localization |> Expect.equal "not started" HasNotStartedYet
                state.Failed |> Expect.equal "named" [ Load.Localization ]
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

            testList
                "the formulary and parenteralia pages"
                [
                    test "the patient set asks both pages again for that patient" {
                        let state, effects = run [ LoaderMsg.PatientSet(Some patient) ]

                        effects
                        |> Expect.equal
                            "both asked"
                            [
                                LoaderEffect.FetchFormulary { Formulary.empty with PatientData = Some patient }
                                LoaderEffect.FetchParenteralia { Parenteralia.empty with PatientData = Some patient }
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

                    test "with a new patient on its way the clear asks no page, and the patient does" {
                        let state, effects = run [ LoaderMsg.PatientExpected; LoaderMsg.PatientSet None ]

                        effects |> Expect.isEmpty "a request for no patient would be out of date"
                        state |> out |> Expect.isEmpty "nothing out"

                        state
                        |> transition (LoaderMsg.PatientSet(Some patient))
                        |> snd
                        |> Expect.equal
                            "one request for each page"
                            [
                                LoaderEffect.FetchFormulary { Formulary.empty with PatientData = Some patient }
                                LoaderEffect.FetchParenteralia { Parenteralia.empty with PatientData = Some patient }
                            ]
                    }

                    test "a clear after a new patient that never landed asks the pages as usual" {
                        let state, _ = run [ LoaderMsg.PatientExpected; LoaderMsg.PatientSet None ]

                        state
                        |> transition (LoaderMsg.PatientSet None)
                        |> snd
                        |> Expect.isNonEmpty "only the one clear is skipped"
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
                            |> transition (
                                LoaderMsg.Landed(
                                    Landing.Formulary(
                                        None,
                                        answered { Formulary.empty with PatientData = Some patient }
                                    )
                                )
                            )
                            |> fst
                            |> transition (
                                LoaderMsg.Landed(
                                    Landing.Parenteralia(
                                        None,
                                        answered { Parenteralia.empty with PatientData = Some patient }
                                    )
                                )
                            )
                            |> fst

                        let _, effects = state |> transition (LoaderMsg.FormularyChanged form)

                        effects
                        |> Expect.equal
                            "asked and seeded"
                            [
                                LoaderEffect.FetchFormulary { form with PatientData = Some patient }
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
                                        PatientData = Some patient
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
                                    LoaderMsg.Landed(
                                        Landing.Formulary(
                                            None,
                                            answered { Formulary.empty with PatientData = Some patient }
                                        )
                                    )
                                    LoaderMsg.Landed(
                                        Landing.Parenteralia(
                                            None,
                                            answered { Parenteralia.empty with PatientData = Some patient }
                                        )
                                    )
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

                    test "a parenteralia reply for the previous patient that lands last is ignored" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.PatientSet(Some patientA)
                                    LoaderMsg.PatientSet(Some patientB)
                                    LoaderMsg.Landed(Landing.Parenteralia(None, answered (parenteraliaFor patientB)))
                                    LoaderMsg.Landed(Landing.Parenteralia(None, answered (parenteraliaFor patientA)))
                                ]

                        state.Parenteralia
                        |> Expect.equal "the rules of patient B" (Resolved(parenteraliaFor patientB))
                    }

                    test "a parenteralia reply for the previous patient that lands first leaves the page loading" {
                        let state, effects =
                            run
                                [
                                    LoaderMsg.PatientSet(Some patientA)
                                    LoaderMsg.PatientSet(Some patientB)
                                    LoaderMsg.Landed(Landing.Parenteralia(None, answered (parenteraliaFor patientA)))
                                ]

                        effects |> Expect.isEmpty "no effect"

                        state.Parenteralia
                        |> isOut
                        |> Expect.isTrue "the request for patient B is still out"
                    }

                    test "a formulary reply for the previous patient that lands last is ignored" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.PatientSet(Some patientA)
                                    LoaderMsg.PatientSet(Some patientB)
                                    LoaderMsg.Landed(Landing.Formulary(None, answered (formularyFor patientB)))
                                    LoaderMsg.Landed(Landing.Formulary(None, answered (formularyFor patientA)))
                                ]

                        state.Formulary
                        |> Expect.equal "the rules of patient B" (Resolved(formularyFor patientB))
                    }

                    test "a parenteralia reply for the current patient is set" {
                        let state, _ =
                            run
                                [
                                    LoaderMsg.PatientSet(Some patientB)
                                    LoaderMsg.Landed(Landing.Parenteralia(None, answered (parenteraliaFor patientB)))
                                ]

                        state.Parenteralia
                        |> Expect.equal "the rules of patient B" (Resolved(parenteraliaFor patientB))
                    }

                    test "a parenteralia reply without a patient is set when there is no patient" {
                        let par = { Parenteralia.empty with Markdown = "every patient" }

                        let state, _ =
                            run
                                [
                                    LoaderMsg.Start Load.Parenteralia
                                    LoaderMsg.Landed(Landing.Parenteralia(None, answered par))
                                ]

                        state.Parenteralia |> Expect.equal "the rules of every patient" (Resolved par)
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


/// A totals query over a feeding, as the client sends one while a feeding slot is narrowed.
module TotalsFixtures =

    let feeding =
        { Informedica.GenPRES.Client.Core.Tests.OrderFixtures.context "" "Nutrilon Nenatal 1" with
            Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
        }

    let query: TotalsQuery =
        {
            PatientData = Informedica.GenPRES.Client.Core.Tests.OrderFixtures.draft
            OrderContexts = [| feeding |]
        }

    let landedTotals n result =
        LoaderMsg.Landed(
            Landing.Totals(
                n,
                None,
                result
                |> Result.map (fun t ->
                    {
                        Response = t
                        Notice = None
                    }
                )
            )
        )

    let someTotals = Informedica.GenPRES.Shared.Models.Totals.empty


open TotalsFixtures


[<Tests>]
let totalsTests =
    testList
        "the totals query"
        [
            test "a query asks for the totals under its number, the totals shown staying" {
                LoaderState.initial
                |> LoaderMachine.transition (LoaderMsg.QueryTotals(Some query))
                |> fun (state, effects) ->
                    effects
                    |> Expect.equal "fetched as query 1" [ LoaderEffect.FetchTotals(1, query) ]
                    state.Totals |> Expect.equal "refreshing" InProgress
            }

            test "the answer to the query lands, and an answer to an earlier one is dropped" {
                let twice =
                    LoaderState.initial
                    |> LoaderMachine.transition (LoaderMsg.QueryTotals(Some query))
                    |> fst
                    |> LoaderMachine.transition (LoaderMsg.QueryTotals(Some query))
                    |> fst

                twice
                |> LoaderMachine.transition (landedTotals 1 (Ok someTotals))
                |> Expect.equal "query 1 dropped" (twice, [])

                twice
                |> LoaderMachine.transition (landedTotals 2 (Ok someTotals))
                |> fun (state, effects) ->
                    state.Totals |> Expect.equal "query 2 landed" (Resolved someTotals)

                    effects
                    |> Expect.equal
                        "the banner cleared"
                        [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Totals ]
            }

            test "a failed query clears the totals and names them on the banner" {
                LoaderState.initial
                |> LoaderMachine.transition (LoaderMsg.QueryTotals(Some query))
                |> fst
                |> LoaderMachine.transition (landedTotals 1 (Error [| "down" |]))
                |> fun (state, effects) ->
                    state.Totals |> Expect.equal "cleared" HasNotStartedYet

                    effects
                    |> Expect.equal
                        "the banner"
                        [
                            LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Totals, [| "down" |])
                        ]
            }

            test "the empty query clears the totals, asks nothing and drops an answer still out" {
                let cleared =
                    LoaderState.initial
                    |> LoaderMachine.transition (LoaderMsg.QueryTotals(Some query))
                    |> fst
                    |> LoaderMachine.transition (LoaderMsg.QueryTotals None)

                cleared |> snd |> Expect.isEmpty "nothing asked"
                (fst cleared).Totals |> Expect.equal "cleared" HasNotStartedYet

                fst cleared
                |> LoaderMachine.transition (landedTotals 1 (Ok someTotals))
                |> Expect.equal "the answer still out dropped" (fst cleared, [])
            }
        ]


/// Patient data for the emergency list: with an age, with a weight, and the rows of an answer.
module EmergencyListFixtures =

    /// Patient data with an age of 2 years and nothing else.
    let withAge = { PatientData.empty with Age = Some { PatientData.Age.ageZero with Age.Years = 2<year> } }


    /// Patient data with a measured weight of 10 kg and nothing else.
    let withWeight =
        { PatientData.empty with Weight = { PatientData.empty.Weight with Measured = Some 10000<gram> } }


    let row name = { Intervention.emptyIntervention with Name = name }


    let landedList number result =
        LoaderMsg.Landed(Landing.EmergencyList(number, Some(OpenedToken "t"), result))


    let fetches effects =
        effects
        |> List.filter (
            function
            | LoaderEffect.FetchEmergencyList _ -> true
            | _ -> false
        )


open EmergencyListFixtures


[<Tests>]
let emergencyListTests =
    testList
        "the emergency list query"
        [
            test "a query requests the list as query 1" {
                let state, effects = run [ LoaderMsg.QueryEmergencyList(Some withAge) ]

                effects
                |> Expect.equal "fetched" [ LoaderEffect.FetchEmergencyList(1, withAge) ]
                state.EmergencyList |> Expect.equal "loading" InProgress
            }

            test "no query clears the rows and requests nothing" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (answered [| row "adrenaline" |])
                            LoaderMsg.QueryEmergencyList None
                        ]

                effects |> Expect.isEmpty "no request"
                state.EmergencyList |> Expect.equal "cleared" HasNotStartedYet
            }

            test "the rows shown stay while the list is queried again" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (answered [| row "adrenaline" |])
                            LoaderMsg.QueryEmergencyList(Some withWeight)
                        ]

                state.EmergencyList
                |> Expect.equal "refreshing" (Refreshing [| row "adrenaline" |])
            }

            test "the answer sets the rows and reports the success" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (answered [| row "adrenaline" |])
                        ]

                state.EmergencyList |> Expect.equal "the rows" (Resolved [| row "adrenaline" |])

                effects
                |> Expect.equal
                    "succeeded"
                    [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.EmergencyList ]
            }

            test "an answer to an earlier query is ignored" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            LoaderMsg.QueryEmergencyList(Some withWeight)
                            landedList 1 (answered [| row "old" |])
                        ]

                state.EmergencyList |> Expect.equal "still loading" InProgress
                effects |> Expect.isEmpty "nothing"
            }

            test "an answer after the query was cleared is ignored" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            LoaderMsg.QueryEmergencyList None
                            landedList 1 (answered [| row "old" |])
                        ]

                state.EmergencyList |> Expect.equal "cleared" HasNotStartedYet
            }

            test "a failed request clears the rows and reports the errors" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (Error [| "down" |])
                        ]

                state.EmergencyList |> Expect.equal "cleared" HasNotStartedYet

                effects
                |> Expect.equal
                    "failed"
                    [
                        LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.EmergencyList, [| "down" |])
                    ]
            }

            test "a resource reload queries the list again for the current query" {
                let _, effects =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (answered [| row "adrenaline" |])
                            LoaderMsg.ResourcesReloaded
                        ]

                effects
                |> fetches
                |> Expect.equal "fetched again" [ LoaderEffect.FetchEmergencyList(2, withAge) ]
            }

            test "a resource reload without a query does not query the list" {
                let _, effects = run [ LoaderMsg.ResourcesReloaded ]

                effects |> fetches |> Expect.isEmpty "no request"
            }

            test "checked rows are stored by their numbers" {
                let state, effects = run [ LoaderMsg.CheckEmergencyListRows [| 2; 3 |] ]

                state.EmergencyListChecked |> Expect.equal "the numbers" [| 2; 3 |]
                effects |> Expect.isEmpty "no effects"
            }

            test "a query for the patient clears the checked rows" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (answered [| row "adrenaline" |])
                            LoaderMsg.CheckEmergencyListRows [| 1 |]
                            LoaderMsg.QueryEmergencyList(Some withWeight)
                        ]

                state.EmergencyListChecked |> Expect.isEmpty "cleared"
            }

            test "no query clears the checked rows with the rows" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryEmergencyList(Some withAge)
                            landedList 1 (answered [| row "adrenaline" |])
                            LoaderMsg.CheckEmergencyListRows [| 1 |]
                            LoaderMsg.QueryEmergencyList None
                        ]

                state.EmergencyListChecked |> Expect.isEmpty "cleared"
            }

            test "the emergency list load is not required to start" {
                Loads.required
                |> List.contains Load.EmergencyList
                |> Expect.isFalse "not required"
            }
        ]


/// Patient data for the continuous medication list: with an age and a Location, older, and the rows of an answer.
module ContinuousListFixtures =

    /// Patient data with an age of 2 years and the Location UMCU.
    let located = { withAge with Location = Some "UMCU" }


    /// Patient data with an age of 3 years and the Location UMCU.
    let older = { located with Age = Some { PatientData.Age.ageZero with Age.Years = 3<year> } }


    let landedContinuous number result =
        LoaderMsg.Landed(Landing.ContinuousList(number, Some(OpenedToken "t"), result))


    let continuousFetches effects =
        effects
        |> List.filter (
            function
            | LoaderEffect.FetchContinuousList _ -> true
            | _ -> false
        )


open ContinuousListFixtures


[<Tests>]
let continuousListTests =
    testList
        "the continuous list query"
        [
            test "a query requests the list as query 1" {
                let state, effects = run [ LoaderMsg.QueryContinuousList(Some located) ]

                effects
                |> Expect.equal "fetched" [ LoaderEffect.FetchContinuousList(1, located) ]
                state.ContinuousList |> Expect.equal "loading" InProgress
            }

            test "no query clears the rows and requests nothing" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                            LoaderMsg.QueryContinuousList None
                        ]

                effects |> Expect.isEmpty "no request"
                state.ContinuousList |> Expect.equal "cleared" HasNotStartedYet
            }

            test "the rows shown stay while the list is queried again" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                            LoaderMsg.QueryContinuousList(Some older)
                        ]

                state.ContinuousList
                |> Expect.equal "refreshing" (Refreshing [| row "morfine" |])
            }

            test "the answer sets the rows and reports the success" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                        ]

                state.ContinuousList |> Expect.equal "the rows" (Resolved [| row "morfine" |])

                effects
                |> Expect.equal
                    "succeeded"
                    [ LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.ContinuousList ]
            }

            test "an answer to an earlier query is ignored" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            LoaderMsg.QueryContinuousList(Some older)
                            landedContinuous 1 (answered [| row "old" |])
                        ]

                state.ContinuousList |> Expect.equal "still loading" InProgress
                effects |> Expect.isEmpty "nothing"
            }

            test "an answer after the query was cleared is ignored" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            LoaderMsg.QueryContinuousList None
                            landedContinuous 1 (answered [| row "old" |])
                        ]

                state.ContinuousList |> Expect.equal "cleared" HasNotStartedYet
            }

            test "a failed request clears the rows and reports the errors" {
                let state, effects =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (Error [| "down" |])
                        ]

                state.ContinuousList |> Expect.equal "cleared" HasNotStartedYet

                effects
                |> Expect.equal
                    "failed"
                    [
                        LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.ContinuousList, [| "down" |])
                    ]
            }

            test "an emergency list query leaves the continuous list alone" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                            LoaderMsg.QueryEmergencyList None
                        ]

                state.ContinuousList |> Expect.equal "the rows" (Resolved [| row "morfine" |])
            }

            test "a resource reload queries the list again for the current query" {
                let _, effects =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                            LoaderMsg.ResourcesReloaded
                        ]

                effects
                |> continuousFetches
                |> Expect.equal "fetched again" [ LoaderEffect.FetchContinuousList(2, located) ]
            }

            test "a resource reload without a query does not query the list" {
                let _, effects = run [ LoaderMsg.ResourcesReloaded ]

                effects |> continuousFetches |> Expect.isEmpty "no request"
            }

            test "checked rows are stored by their numbers" {
                let state, effects = run [ LoaderMsg.CheckContinuousListRows [| 2; 3 |] ]

                state.ContinuousListChecked |> Expect.equal "the numbers" [| 2; 3 |]
                effects |> Expect.isEmpty "no effects"
            }

            test "a query for the patient clears the checked rows" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                            LoaderMsg.CheckContinuousListRows [| 1 |]
                            LoaderMsg.QueryContinuousList(Some older)
                        ]

                state.ContinuousListChecked |> Expect.isEmpty "cleared"
            }

            test "no query clears the checked rows with the rows" {
                let state, _ =
                    run
                        [
                            LoaderMsg.QueryContinuousList(Some located)
                            landedContinuous 1 (answered [| row "morfine" |])
                            LoaderMsg.CheckContinuousListRows [| 1 |]
                            LoaderMsg.QueryContinuousList None
                        ]

                state.ContinuousListChecked |> Expect.isEmpty "cleared"
            }

            test "checked emergency list rows leave the checked continuous list rows alone" {
                let state, _ =
                    run
                        [
                            LoaderMsg.CheckContinuousListRows [| 1 |]
                            LoaderMsg.CheckEmergencyListRows [| 2 |]
                        ]

                state.ContinuousListChecked |> Expect.equal "the numbers" [| 1 |]
            }

            test "the continuous list load is not required to start" {
                Loads.required
                |> List.contains Load.ContinuousList
                |> Expect.isFalse "not required"
            }
        ]
