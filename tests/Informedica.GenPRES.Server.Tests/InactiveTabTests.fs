/// A tab that is not the active one: the session cookie is shared by the tabs of a browser, so a tab
/// can hold a token that is no longer the Session's. Such a tab is told so, and the commands that
/// change a Session refuse its token.
module Informedica.GenPRES.Server.Tests.InactiveTabTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open ServerApi

module AR = Informedica.GenPRES.Server.Tests.AgeOnRequestTests


let own = Some(OpenedToken $"opened-{AR.identified}")

let oldTab = Some(OpenedToken "opened-OLD-TAB")

let ended =
    { AR.state with
        Sessions = Map.empty
        Endings = Map.ofList [ AR.identified, (SessionEnding.SupersededByLaunch, AR.today) ]
    }

let seen opened = Session.seen AR.today AR.identified opened AR.state

let closing opened state =
    let close state =
        let state, writes = Session.close AR.today AR.identified state
        state, (), writes

    state |> Session.whenOwnToken AR.identified opened close


[<Tests>]
let tests =
    testList
        "a tab that is not the active one"
        [
            testList
                "a computing request"
                [
                    test "the Session's own token is told nothing" {
                        let _, told, _ = seen own
                        told |> Expect.isNone "nothing to tell"
                    }

                    test "another token is told that the tab is inactive" {
                        let _, told, _ = seen oldTab
                        told |> Expect.equal "told" (Some(RecordNotice.Ended SessionEnding.InactiveTab))
                    }

                    test "no token is told nothing: a tab without a session" {
                        let _, told, _ = seen None
                        told |> Expect.isNone "nothing to tell"
                    }

                    test "the heartbeat is all that another token writes" {
                        let state, _, writes = seen oldTab
                        writes |> List.length |> Expect.equal "the one touch" 1

                        state.Sessions[AR.identified].Opened
                        |> Expect.equal "the Session as it was" AR.state.Sessions[AR.identified].Opened
                    }

                    test "an ended Session is told its ending, whatever the token" {
                        let _, told, _ = Session.seen AR.today AR.identified oldTab ended

                        told
                        |> Expect.equal "the ending" (Some(RecordNotice.Ended SessionEnding.SupersededByLaunch))
                    }
                ]

            testList
                "a command that changes the Session"
                [
                    test "its own token runs" {
                        match closing own AR.state with
                        | state, OnOwnToken.Done(), _ ->
                            state.Sessions |> Map.containsKey AR.identified |> Expect.isFalse "closed"
                        | _ -> failtest "refused"
                    }

                    test "another token, or none, is refused and the Session stays" {
                        for opened in [ oldTab; None ] do
                            let state, answer, writes = closing opened AR.state

                            (state.Sessions |> Map.containsKey AR.identified, answer, writes)
                            |> Expect.equal $"%A{opened}" (true, OnOwnToken.InactiveTab, [])
                    }

                    test "a close without a token still acknowledges an ending" {
                        let state, answer, _ = closing None ended

                        (state.Endings |> Map.isEmpty, answer)
                        |> Expect.equal "acknowledged" (true, OnOwnToken.Done())
                    }
                ]
        ]
