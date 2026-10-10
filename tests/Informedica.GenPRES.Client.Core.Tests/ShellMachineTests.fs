module Informedica.GenPRES.Client.Core.Tests.ShellMachineTests

open Expecto
open Expecto.Flip
open Shared.Api
open Shared.Localization
open ServerErrorPolicy
open ShellMachine


let initial = ShellState.initial Url.none


let run msgs =
    msgs |> List.fold (fun (state, _) msg -> state |> transition msg) (initial, [])


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

                        let state, _ = run [ ShellMsg.UrlApplied url ]

                        (state.Page, state.Language.Current, state.ShowDisclaimer)
                        |> Expect.equal "from the url" (Page.Page.Prescribe, French, false)
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
