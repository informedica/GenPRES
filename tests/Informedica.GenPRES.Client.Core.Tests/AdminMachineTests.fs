module Informedica.GenPRES.Client.Core.Tests.AdminMachineTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open Busy
open AdminMachine


let run msgs =
    msgs
    |> List.fold (fun (state, _) msg -> state |> transition msg) (AdminState.initial, [])


let loggedIn =
    [
        AdminMsg.Login "secret"
        AdminMsg.Landed(Landing.Login(1, Ok(AdminResponse.PasswordValidated(true, "token"))))
    ]


let file =
    {
        FileName = "a.log"
        SizeBytes = 1L
        LastModifiedAt = ""
    }


[<Tests>]
let tests =
    testList
        "AdminMachine"
        [
            testList
                "the login"
                [
                    test "a login asks the server under the next attempt" {
                        let state, effects = run [ AdminMsg.Login "secret" ]

                        effects |> Expect.equal "asked" [ AdminEffect.ValidatePassword(1, "secret") ]
                        state.LoginAttempt |> Expect.equal "the attempt" 1
                    }

                    test "a valid password logs in with the token" {
                        let state, effects = run loggedIn

                        effects
                        |> Expect.equal "succeeded" [ AdminEffect.Succeeded ServerErrorPolicy.ErrorSource.Login ]

                        state.IsAuthenticated |> Expect.isTrue "logged in"
                        state.AuthToken |> Expect.equal "the token" "token"
                    }

                    test "an invalid password is told" {
                        let state, effects =
                            run
                                [
                                    AdminMsg.Login "wrong"
                                    AdminMsg.Landed(Landing.Login(1, Ok(AdminResponse.PasswordValidated(false, ""))))
                                ]

                        effects
                        |> Expect.equal
                            "told"
                            [
                                AdminEffect.Succeeded ServerErrorPolicy.ErrorSource.Login
                                AdminEffect.Alert Alert.Alert.InvalidPassword
                            ]

                        state.IsAuthenticated |> Expect.isFalse "not logged in"
                    }

                    test "a login answer of an earlier attempt lands nowhere" {
                        let state, _ = run [ AdminMsg.Login "a"; AdminMsg.Login "b" ]

                        state
                        |> transition (
                            AdminMsg.Landed(Landing.Login(1, Ok(AdminResponse.PasswordValidated(true, "token"))))
                        )
                        |> Expect.equal "unchanged" (state, [])
                    }

                    test "a login answer after a logout lands nowhere" {
                        let state, _ = run [ AdminMsg.Login "a"; AdminMsg.Logout ]

                        state
                        |> transition (
                            AdminMsg.Landed(Landing.Login(1, Ok(AdminResponse.PasswordValidated(true, "token"))))
                        )
                        |> Expect.equal "unchanged" (state, [])
                    }

                    test "a failed login drops the token and raises the error" {
                        let state, effects =
                            run (
                                loggedIn
                                @ [ AdminMsg.Login "b"; AdminMsg.Landed(Landing.Login(2, Error [| "down" |])) ]
                            )

                        effects
                        |> Expect.equal
                            "raised"
                            [ AdminEffect.Failed(ServerErrorPolicy.ErrorSource.Login, [| "down" |]) ]

                        state.IsAuthenticated |> Expect.isFalse "not logged in"
                        state.AuthToken |> Expect.equal "no token" ""
                    }

                    test "a logout clears the login and the readings" {
                        let state, effects =
                            run (
                                loggedIn
                                @ [
                                    AdminMsg.ListLogFiles
                                    AdminMsg.Landed(Landing.LogFiles(Ok(AdminResponse.LogFilesListed [| file |])))
                                    AdminMsg.Logout
                                ]
                            )

                        effects |> Expect.equal "logged out" [ AdminEffect.LoggedOut ]

                        state |> Expect.equal "cleared" { AdminState.initial with LoginAttempt = 2 }
                    }
                ]

            testList
                "the calls under the token"
                [
                    test "a listing asks under the token and is out" {
                        let state, effects = run (loggedIn @ [ AdminMsg.ListLogFiles ])

                        effects |> Expect.equal "asked" [ AdminEffect.FetchLogFiles "token" ]
                        state |> out |> Expect.equal "out" [ Load.LogFiles ]
                    }

                    test "a listing while one runs changes nothing" {
                        let state, _ = run (loggedIn @ [ AdminMsg.ListLogFiles ])

                        state
                        |> transition AdminMsg.ListLogFiles
                        |> Expect.equal "unchanged" (state, [])
                    }

                    test "the files listed are shown and clear the error" {
                        let state, effects =
                            run (
                                loggedIn
                                @ [
                                    AdminMsg.ListLogFiles
                                    AdminMsg.Landed(Landing.LogFiles(Ok(AdminResponse.LogFilesListed [| file |])))
                                ]
                            )

                        effects
                        |> Expect.equal "succeeded" [ AdminEffect.Succeeded ServerErrorPolicy.ErrorSource.LogFiles ]

                        state.LogFiles |> Expect.equal "shown" (Resolved [| file |])
                    }

                    test "an analysis asks under the token for the file" {
                        let state, effects = run (loggedIn @ [ AdminMsg.AnalyzeLogFile "a.log" ])

                        effects
                        |> Expect.equal "asked" [ AdminEffect.FetchLogAnalysis("token", "a.log") ]
                        state |> out |> Expect.equal "out" [ Load.LogAnalysis ]
                    }

                    test "the reload done ends the reload and is told to the loads" {
                        let state, effects =
                            run (
                                loggedIn
                                @ [
                                    AdminMsg.ReloadResources
                                    AdminMsg.Landed(Landing.Reload(Ok AdminResponse.ResourcesReloaded))
                                ]
                            )

                        effects
                        |> Expect.equal
                            "done"
                            [
                                AdminEffect.Succeeded ServerErrorPolicy.ErrorSource.Reload
                                AdminEffect.ReloadDone
                            ]

                        state.Reloading |> Expect.equal "ended" (Resolved())
                    }

                    test "a failed call raises the error under its source and keeps the login" {
                        let state, effects =
                            run (
                                loggedIn
                                @ [
                                    AdminMsg.ReloadResources
                                    AdminMsg.Landed(Landing.Reload(Error [| "down" |]))
                                ]
                            )

                        effects
                        |> Expect.equal
                            "raised"
                            [ AdminEffect.Failed(ServerErrorPolicy.ErrorSource.Reload, [| "down" |]) ]

                        state.Reloading |> Expect.equal "not started" HasNotStartedYet
                        state.IsAuthenticated |> Expect.isTrue "still logged in"
                    }

                    test "a token the server no longer takes logs out" {
                        let state, effects =
                            run (
                                loggedIn
                                @ [
                                    AdminMsg.ListLogFiles
                                    AdminMsg.Landed(Landing.LogFiles(Error [| "Invalid token" |]))
                                ]
                            )

                        effects
                        |> Expect.equal
                            "raised and logged out"
                            [
                                AdminEffect.Failed(ServerErrorPolicy.ErrorSource.LogFiles, [| "Invalid token" |])
                                AdminEffect.LoggedOut
                            ]

                        state |> Expect.equal "cleared" { AdminState.initial with LoginAttempt = 2 }
                    }
                ]
        ]
