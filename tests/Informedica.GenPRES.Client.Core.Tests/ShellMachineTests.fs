module Informedica.GenPRES.Client.Core.Tests.ShellMachineTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open Shared.Localization
open ServerErrorPolicy
open ShellMachine


let initial = ShellState.initial


let run msgs =
    msgs |> List.fold (fun (state, _) msg -> state |> transition msg) (initial, [])


let idle =
    {
        SigningUnderWay = false
        Out = []
        UnsignedWork = false
        Launched = false
    }


let pat =
    { Shared.Models.Patient.empty with Age = Some { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> } }


let patientUrl = { Url.none with Patient = Some pat }


let launch = Shared.Types.Launch "abc"


let launchUrl = { Url.none with Launch = Some(Url.LaunchUrl.Launch launch) }


/// The shell after a page load of these segments, with no patient.
let loaded sl = initial |> transition (ShellMsg.PageLoaded(sl, Url.none)) |> fst


let settings language =
    {
        Language = language
        IsDemo = true
        Departments = [||]
        DefaultDepartment = ""
    }


[<Tests>]
let tests =
    testList
        "ShellMachine"
        [
            testList
                "the page"
                [
                    test "a page chosen shows and is told to the loads" {
                        let state, effects = run [ ShellMsg.PageChosen(Page.Page.Formulary, false) ]

                        state.Page |> Expect.equal "shown" Page.Page.Formulary
                        effects |> Expect.equal "told" [ ShellEffect.PageShown Page.Page.Formulary ]
                    }

                    test "the settings page is refused to a user not logged in" {
                        run [ ShellMsg.PageChosen(Page.Page.Settings, false) ]
                        |> Expect.equal "unchanged" (initial, [])
                    }

                    test "the settings page shows to a user logged in" {
                        let state, _ = run [ ShellMsg.PageChosen(Page.Page.Settings, true) ]
                        state.Page |> Expect.equal "shown" Page.Page.Settings
                    }

                    test "a page set by another part shows and is told to no one" {
                        let state, effects = run [ ShellMsg.MovedToPage Page.Page.OrderPlan ]

                        state.Page |> Expect.equal "shown" Page.Page.OrderPlan
                        effects |> Expect.isEmpty "nothing told"
                    }

                    test "a logout leaves the settings page, and no other" {
                        let state, _ = run [ ShellMsg.PageChosen(Page.Page.Settings, true); ShellMsg.LoggedOut ]

                        state.Page |> Expect.equal "left" Page.Page.LifeSupport

                        let state, _ = run [ ShellMsg.PageChosen(Page.Page.Prescribe, true); ShellMsg.LoggedOut ]

                        state.Page |> Expect.equal "kept" Page.Page.Prescribe
                    }
                ]

            testList
                "the language, the hospital and the disclaimer"
                [
                    test "a language chosen shows the disclaimer again" {
                        let state, _ = run [ ShellMsg.DisclaimerAccepted; ShellMsg.LanguageChosen English ]

                        state.Language.Current |> Expect.equal "chosen" English
                        state.ShowDisclaimer |> Expect.isTrue "shown again"
                    }

                    test "a hospital chosen shows the disclaimer again" {
                        let state, _ = run [ ShellMsg.DisclaimerAccepted; ShellMsg.HospitalChosen "WKZ" ]

                        state.Hospital |> Expect.equal "chosen" "WKZ"
                        state.ShowDisclaimer |> Expect.isTrue "shown again"
                    }

                    test "the server default sets the language unless it was chosen" {
                        let state, _ = run [ ShellMsg.SettingsLanded(settings German) ]

                        (state.Language.Current, state.IsDemo)
                        |> Expect.equal "the default and the demo flag" (German, true)

                        let state, _ = run [ ShellMsg.LanguageChosen English; ShellMsg.SettingsLanded(settings German) ]

                        state.Language.Current |> Expect.equal "the choice" English
                    }

                    test "a url sets the page, the disclaimer and a language it names" {
                        let url =
                            { Url.none with
                                Page = Some Page.Page.Prescribe
                                Language = Some French
                                Disclaimer = false
                            }

                        let state, _ = run [ ShellMsg.UrlChanged([ "patient?pag=pr" ], url, idle) ]

                        (state.Page, state.Language.Current, state.ShowDisclaimer)
                        |> Expect.equal "from the url" (Page.Page.Prescribe, French, false)
                    }
                ]

            testList
                "the page load"
                [
                    test "a url without a patient resumes the Session and shows the url" {
                        let state, effects = initial |> transition (ShellMsg.PageLoaded([ "a" ], Url.none))

                        (UrlPolicy.UrlState.shown state.Url, effects)
                        |> Expect.equal
                            "shown, resumed"
                            ([ "a" ], [ ShellEffect.ResumeSession; ShellEffect.PatientFromUrl None ])
                    }

                    test "a url with a patient leaves the Session for the patient" {
                        initial
                        |> transition (ShellMsg.PageLoaded([ "p" ], patientUrl))
                        |> snd
                        |> Expect.equal "left" [ ShellEffect.LeaveSession; ShellEffect.PatientFromUrl(Some pat) ]
                    }

                    test "a launch url is erased, shown as the session url and presented" {
                        let state, effects = initial |> transition (ShellMsg.PageLoaded([ "l" ], launchUrl))

                        (UrlPolicy.UrlState.shown state.Url, effects)
                        |> Expect.equal
                            "erased, presented"
                            ([ "session" ],
                             [
                                 ShellEffect.EraseLaunch
                                 ShellEffect.PatientFromUrl None
                                 ShellEffect.PresentLaunch launch
                             ])
                    }

                    test "a medication in the url seeds the workbench with its parts" {
                        let url =
                            Url.parse
                                (System.DateTime(2026, 1, 1))
                                [ "patient"; "?agd=10&ind=pijn&med=paracetamol&rte=or" ]

                        initial
                        |> transition (ShellMsg.PageLoaded([ "m" ], url))
                        |> snd
                        |> List.choose (
                            function
                            | ShellEffect.SeedWorkbench seed ->
                                Some(seed.Source, seed.Indication, seed.Generic, seed.Route)
                            | _ -> None
                        )
                        |> Expect.equal
                            "seeded"
                            [ Shared.Types.SeedSource.Url, Some "pijn", Some "paracetamol", Some "or" ]
                    }
                ]

            testList
                "a url change"
                [
                    test "the url shown changes nothing" {
                        let state = loaded [ "a" ]

                        state
                        |> transition (ShellMsg.UrlChanged([ "a" ], patientUrl, idle))
                        |> Expect.equal "unchanged" (state, [])
                    }

                    test "a page alone is applied" {
                        let state, effects =
                            loaded [ "a" ]
                            |> transition (
                                ShellMsg.UrlChanged([ "b" ], { Url.none with Page = Some Page.Page.Formulary }, idle)
                            )

                        (UrlPolicy.UrlState.shown state.Url, state.Page, effects)
                        |> Expect.equal "applied" ([ "b" ], Page.Page.Formulary, [])
                    }

                    test "a page alone is put back while a request is out" {
                        let busy = { idle with Out = [ Busy.Request.Plan ] }

                        loaded [ "a" ]
                        |> transition (ShellMsg.UrlChanged([ "b" ], Url.none, busy))
                        |> snd
                        |> Expect.equal "put back" [ ShellEffect.PutBackUrl [ "a" ] ]
                    }

                    test "a launch put back is erased and told" {
                        let state, effects =
                            loaded [ "a" ]
                            |> transition (
                                ShellMsg.UrlChanged([ "l" ], launchUrl, { idle with SigningUnderWay = true })
                            )

                        (state.Snackbar, effects)
                        |> Expect.equal
                            "erased, told, put back"
                            (Some Alert.Alert.LaunchNotOpened,
                             [ ShellEffect.EraseLaunch; ShellEffect.PutBackUrl [ "a" ] ])
                    }

                    test "a patient over work not signed is asked about" {
                        let state, effects =
                            loaded [ "a" ]
                            |> transition (ShellMsg.UrlChanged([ "p" ], patientUrl, { idle with UnsignedWork = true }))

                        (UrlPolicy.UrlState.asked state.Url, UrlPolicy.UrlState.shown state.Url, effects)
                        |> Expect.equal "asked" (Some [ "p" ], [ "a" ], [])
                    }

                    test "a patient over nothing starts the lanes over on it" {
                        let state, effects =
                            loaded [ "a" ] |> transition (ShellMsg.UrlChanged([ "p" ], patientUrl, idle))

                        (UrlPolicy.UrlState.shown state.Url, effects)
                        |> Expect.equal
                            "started over"
                            ([ "p" ], [ ShellEffect.StartOver(Some pat); ShellEffect.PatientFromUrl(Some pat) ])
                    }

                    test "a refused launch is applied in full, whatever holds" {
                        let refused =
                            { Url.none with Launch = Some(Url.LaunchUrl.Refused Shared.Types.LaunchRefusal.NoRole) }

                        loaded [ "a" ]
                        |> transition (ShellMsg.UrlChanged([ "r" ], refused, { idle with SigningUnderWay = true }))
                        |> snd
                        |> Expect.equal
                            "refused"
                            [
                                ShellEffect.EraseLaunch
                                ShellEffect.PatientFromUrl None
                                ShellEffect.LaunchRefused Shared.Types.LaunchRefusal.NoRole
                            ]
                    }

                    test "a later url change closes the question" {
                        loaded [ "a" ]
                        |> transition (ShellMsg.UrlChanged([ "p" ], patientUrl, { idle with Launched = true }))
                        |> fst
                        |> transition (ShellMsg.UrlChanged([ "a" ], Url.none, idle))
                        |> fst
                        |> _.Url
                        |> Expect.equal "closed" (UrlPolicy.UrlState.Shown [ "a" ])
                    }
                ]

            testList
                "the question"
                [
                    let asked =
                        loaded [ "a" ]
                        |> transition (ShellMsg.UrlChanged([ "p" ], patientUrl, { idle with Launched = true }))
                        |> fst

                    test "yes starts the lanes over on the url asked about" {
                        let state, effects = asked |> transition ShellMsg.LeftForUrl

                        (state.Url, effects)
                        |> Expect.equal
                            "started over"
                            (UrlPolicy.UrlState.Shown [ "p" ],
                             [ ShellEffect.StartOver(Some pat); ShellEffect.PatientFromUrl(Some pat) ])
                    }

                    test "yes without a question does nothing" {
                        let state = loaded [ "a" ]

                        state |> transition ShellMsg.LeftForUrl |> Expect.equal "unchanged" (state, [])
                    }

                    test "no puts the url shown back" {
                        let state, effects = asked |> transition ShellMsg.UrlKept

                        (state.Url, effects)
                        |> Expect.equal
                            "put back"
                            (UrlPolicy.UrlState.Shown [ "a" ], [ ShellEffect.PutBackUrl [ "a" ] ])
                    }
                ]

            testList
                "the snackbar and the error banner"
                [
                    test "a failure raises the banner and shows that something went wrong" {
                        let state, _ = run [ ShellMsg.ServerErrorRaised(ErrorSource.Formulary, [| "down" |]) ]

                        state.ServerError
                        |> Expect.equal "raised" (Some(raised ErrorSource.Formulary [| "down" |]))

                        state.Snackbar |> Expect.equal "told" (Some Alert.Alert.RequestFailed)
                    }

                    test "the server down raises the banner alone" {
                        let state, _ = run [ ShellMsg.ServerErrorRaised(ErrorSource.Server, [| "refused" |]) ]

                        state.ServerError |> Expect.isSome "raised"
                        state.Snackbar |> Expect.isNone "no snackbar"
                    }

                    test "the next success of the source clears the banner, another source does not" {
                        let raisedState, _ = run [ ShellMsg.ServerErrorRaised(ErrorSource.Formulary, [| "down" |]) ]

                        let state, _ = raisedState |> transition (ShellMsg.ServerErrorCleared ErrorSource.Parenteralia)
                        state.ServerError |> Expect.isSome "kept"

                        let state, _ = raisedState |> transition (ShellMsg.ServerErrorCleared ErrorSource.Formulary)
                        state.ServerError |> Expect.isNone "cleared"
                    }

                    test "the banner dismissed goes" {
                        let state, _ =
                            run
                                [
                                    ShellMsg.ServerErrorRaised(ErrorSource.Formulary, [| "down" |])
                                    ShellMsg.ServerErrorDismissed
                                ]

                        state.ServerError |> Expect.isNone "gone"
                    }

                    test "an alert shown, then closed" {
                        let state, _ = run [ ShellMsg.AlertRaised Alert.Alert.PinNotSent ]
                        state.Snackbar |> Expect.equal "shown" (Some Alert.Alert.PinNotSent)

                        let state, _ = state |> transition ShellMsg.SnackbarClosed
                        state.Snackbar |> Expect.isNone "closed"
                    }

                    test "the interactions notice is withdrawn, never another alert" {
                        let state, _ =
                            run
                                [
                                    ShellMsg.AlertRaised(Alert.Alert.InteractionsFound 2)
                                    ShellMsg.WithdrawInteractionsFound
                                ]

                        state.Snackbar |> Expect.isNone "withdrawn"

                        let state, _ =
                            run
                                [
                                    ShellMsg.AlertRaised Alert.Alert.PinNotSent
                                    ShellMsg.WithdrawInteractionsFound
                                ]

                        state.Snackbar |> Expect.equal "kept" (Some Alert.Alert.PinNotSent)
                    }
                ]

            testList
                "the rest"
                [
                    test "the list filters set, and cleared for a new patient" {
                        let state, _ =
                            run
                                [
                                    ShellMsg.EmergencyListFiltered [| "a" |]
                                    ShellMsg.ContinuousMedsFiltered [| "b" |]
                                ]

                        (state.EmergencyListFilter, state.ContinuousMedsFilter)
                        |> Expect.equal "set" ([| "a" |], [| "b" |])

                        let state, _ = state |> transition ShellMsg.ListFiltersCleared

                        (state.EmergencyListFilter, state.ContinuousMedsFilter)
                        |> Expect.equal "cleared" ([||], [||])
                    }

                    test "the counting and the start-up ended" {
                        let state, _ = run [ ShellMsg.CountingChanged true; ShellMsg.StartupEnded ]

                        (state.Counting, state.Started) |> Expect.equal "both" (true, true)
                    }
                ]
        ]
