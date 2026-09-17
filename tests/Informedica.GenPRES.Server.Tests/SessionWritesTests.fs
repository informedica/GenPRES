/// What each member of the session machine writes, as the values it returns: the Launch and
/// its outcome, the Session opened and what it opened with, the heartbeat, the ending and its
/// acknowledgement, and the order plan version of a signature. The port runs a request's writes
/// as one and keeps its state only when they land.
module Informedica.GenPRES.Server.Tests.SessionWritesTests

open System
open Expecto
open Expecto.Flip
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests


module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests

let ids () =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"id-{n.Value}"


let verify = LaunchSeal.verify t0 sealKey

let authorize state = $"/authorize?state={state}"


/// A state with the Launch presented, the directory that issues codes, and the callback for a
/// login.
let presentedFor login nonce (directory: StubDirectory.Directory) newId state =
    let state, answer, _ =
        Session.present t0 newId verify authorize state (mintFor nonce "stub-patient", keyA)

    match answer with
    | LaunchResult.RedirectTo(_, st) ->
        state,
        {
            State = st
            StateCookie = Some st
            Code = Some(directory.issue login "stub-patient")
            Error = None
        }
    | other -> failtest $"expected RedirectTo, got %A{other}"


let directory () =
    StubDirectory.make (fun () -> t0) (fun () -> $"code-{Guid.NewGuid()}")


/// The callback over the machine, counting the redemptions.
let callbackCounting (d: StubDirectory.Directory) newId (count: int ref) state cb =
    let redeem code =
        count.Value <- count.Value + 1
        d.idp.redeem code

    Session.callback t0 newId (codes ()) codeMac redeem d.registry.standing StubPatientData.port.read ignore state cb


let caseName (w: Session.Persist) =
    match w with
    | Session.WriteVersion _ -> "WriteVersion"
    | Session.RecordLaunch _ -> "RecordLaunch"
    | Session.RecordLaunchOutcome _ -> "RecordLaunchOutcome"
    | Session.OpenSession _ -> "OpenSession"
    | Session.RecordOpenedWith _ -> "RecordOpenedWith"
    | Session.RecordSeen _ -> "RecordSeen"
    | Session.EndSession _ -> "EndSession"
    | Session.AcknowledgeEnding _ -> "AcknowledgeEnding"


let names writes = writes |> List.map caseName


/// An open Session of `prescriber` over the machine: the state and its id.
let opened () =
    let d = directory ()
    let newId = ids ()
    let state, cb = presentedFor "prescriber" "n-1" d newId seeded

    match callbackCounting d newId (ref 0) state cb with
    | state, CallbackResult.Opened(sid, _), _ -> state, sid, newId
    | _, other, _ -> failtest $"expected Opened, got %A{other}"


[<Tests>]
let machineTests =
    testList
        "the machine returns its writes"
        [
            test "present: a new Launch is recorded, a repeat writes nothing" {
                let state, _, writes =
                    Session.present t0 (ids ()) verify authorize seeded (mintFor "n-1" "stub-patient", keyA)

                match writes with
                | [ Session.RecordLaunch r ] -> r.Nonce |> Expect.equal "the Launch" "n-1"
                | other -> failtest $"expected RecordLaunch, got %A{other}"

                let _, _, again =
                    Session.present t0 (ids ()) verify authorize state (mintFor "n-1" "stub-patient", keyA)

                again |> Expect.isEmpty "a repeat writes nothing"
            }

            test "callback: an open writes the Session, what it opened with and the outcome; the code is redeemed once" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seeded
                let count = ref 0
                let state, answer, writes = callbackCounting d newId count state cb

                match answer with
                | CallbackResult.Opened(sid, _) ->
                    writes
                    |> names
                    |> Expect.equal "the writes" [ "OpenSession"; "RecordOpenedWith"; "RecordLaunchOutcome" ]

                    match writes with
                    | [ Session.OpenSession(a, _)
                        Session.RecordOpenedWith(b, _, _)
                        Session.RecordLaunchOutcome(nonce, LaunchResult.Opened(c, _), _) ] ->
                        [ a; b; c ] |> Expect.equal "one Session named" [ sid; sid; sid ]
                        nonce |> Expect.equal "the Launch's outcome" "n-1"
                    | other -> failtest $"%A{other}"

                    count.Value |> Expect.equal "redeemed once" 1

                    let _, reload, again = callbackCounting d newId count state cb
                    reload |> Expect.equal "a reload is answered as the first time" answer
                    again |> Expect.isEmpty "a reload writes nothing"
                    count.Value |> Expect.equal "and redeems nothing" 1
                | other -> failtest $"expected Opened, got %A{other}"
            }

            test "callback: a refusal writes the outcome only" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seeded

                let _, answer, writes =
                    callbackCounting
                        d
                        newId
                        (ref 0)
                        state
                        { cb with
                            Code = None
                            Error = Some "no-identity"
                        }

                match answer, writes with
                | CallbackResult.Refused(LaunchRefusal.NoBrowserIdentity, _),
                  [ Session.RecordLaunchOutcome(_, LaunchResult.Refused LaunchRefusal.NoBrowserIdentity, _) ] -> ()
                | other -> failtest $"%A{other}"
            }

            test "callback: a newer open of the same login writes no ending for the older" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seeded
                let state, _, _ = callbackCounting d newId (ref 0) state cb
                let state, cb2 = presentedFor "prescriber" "n-2" d newId state
                let _, _, writes = callbackCounting d newId (ref 0) state cb2

                writes
                |> names
                |> Expect.equal "the open only" [ "OpenSession"; "RecordOpenedWith"; "RecordLaunchOutcome" ]
            }

            test "find writes a heartbeat; close ends and acknowledges; an unknown id writes nothing" {
                let state, sid, _ = opened ()
                let state, _, found = Session.find t0 sid state
                found |> Expect.equal "the heartbeat" [ Session.RecordSeen(sid, t0) ]

                let state, closed = Session.close t0 sid state

                closed
                |> Expect.equal
                    "closed and acknowledged"
                    [
                        Session.EndSession(sid, Session.StoredEnding.Closed, t0)
                        Session.AcknowledgeEnding(sid, t0)
                    ]

                let _, again = Session.close t0 sid state
                again |> Expect.isEmpty "nothing left to close"

                let _, _, unknown = Session.find t0 "nobody" state
                unknown |> Expect.isEmpty "no Session, no heartbeat"
            }

            test "commit: a sign writes the heartbeat, the version and what the Session opened with" {
                let state, sid, newId = opened ()

                let token = state.Sessions[sid].Opened.OpenedToken.Value

                let state, challenge, _ =
                    Session.challenge
                        t0
                        newId
                        StubDatabase.digest
                        StubPatientData.port.read
                        sid
                        (Store.domainPlan.Value, token, None)
                        state

                let nonce =
                    match challenge with
                    | SigningOutcome.ChallengeIssued n -> n
                    | other -> failtest $"expected ChallengeIssued, got %A{other}"

                let signature pin key : Signature =
                    {
                        Plan = Store.domainPlan.Value
                        Opened = token
                        Challenge = nonce
                        Pin = pin
                        IdemKey = key
                    }

                let _, answer, writes =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid (signature "1234" "k-1") state

                match answer with
                | SigningOutcome.Submitted _ ->
                    writes
                    |> names
                    |> Expect.equal "the writes" [ "RecordSeen"; "WriteVersion"; "RecordOpenedWith" ]
                | other -> failtest $"expected Submitted, got %A{other}"

                // three wrong PINs: the last ends the Session
                let wrong (state, _, _) key =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid (signature "9999" key) state

                let _, last, writes =
                    [ "w-2"; "w-3" ] |> List.fold wrong (wrong (state, (), []) "w-1")

                last
                |> Expect.equal "the limit" (SigningOutcome.Refused SigningRefusal.PinLimit)

                writes
                |> List.filter (
                    function
                    | Session.EndSession _ -> true
                    | _ -> false
                )
                |> Expect.equal
                    "the ending"
                    [
                        Session.EndSession(sid, Session.StoredEnding.Ended SessionEnding.WrongPinLimit, t0)
                    ]
            }
        ]


/// A port with the clock at t0 over a store.
let portOver (store: StubDatabase.RecordStore) =
    let d = directory ()

    let port =
        StubDatabase.makeSessionPortWith
            store
            (fun () -> t0)
            (ids ())
            (codes ())
            salts
            codeMac
            verify
            d.idp
            d.registry
            StubPatientData.port
            (StubMail.make ()).port
            seeded

    port, d


let openVia (port: SessionPort) (d: StubDirectory.Directory) =
    async {
        match! port.present (mintFor "n-1" "stub-patient", keyA) with
        | LaunchResult.RedirectTo(_, st) ->
            match!
                port.callback
                    {
                        State = st
                        StateCookie = Some st
                        Code = Some(d.issue "prescriber" "stub-patient")
                        Error = None
                    }
            with
            | CallbackResult.Opened(sid, _) -> return sid
            | other -> return failtest $"expected Opened, got %A{other}"
        | other -> return failtest $"expected RedirectTo, got %A{other}"
    }


[<Tests>]
let portTests =
    testList
        "the port runs the writes"
        [
            testAsync
                "writes that do not land: find fails, challenge and submit answer StoreFailed, the state as it was" {
                let failing = ref false

                let store =
                    { StubDatabase.inMemory with
                        persist =
                            fun writes ->
                                if failing.Value then
                                    Session.StoreOutcome.Failed "the database is gone"
                                else
                                    StubDatabase.inMemory.persist writes
                    }

                let port, d = portOver store
                let! sid = openVia port d

                let! opened =
                    async {
                        match! port.find sid with
                        | SessionLookup.Found o -> return o
                        | other -> return failtest $"expected Found, got %A{other}"
                    }

                failing.Value <- true

                let! found = Async.Catch(port.find sid)

                match found with
                | Choice2Of2 _ -> ()
                | Choice1Of2 other -> failtest $"the call should fail, got %A{other}"

                let! challenge = port.challenge sid (Store.domainPlan.Value, opened.OpenedToken.Value, None)

                challenge
                |> Expect.equal "challenge" (SigningOutcome.Refused SigningRefusal.StoreFailed)

                failing.Value <- false

                match! port.find sid with
                | SessionLookup.Found again -> again |> Expect.equal "the Session as it was" opened
                | other -> failtest $"expected Found, got %A{other}"
            }

            testAsync "the in-memory store signs as before" {
                let port, d = portOver StubDatabase.inMemory
                let! sid = openVia port d

                let! opened =
                    async {
                        match! port.find sid with
                        | SessionLookup.Found o -> return o
                        | other -> return failtest $"expected Found, got %A{other}"
                    }

                match! port.challenge sid (Store.domainPlan.Value, opened.OpenedToken.Value, None) with
                | SigningOutcome.ChallengeIssued nonce ->
                    let! answer =
                        port.submit
                            sid
                            {
                                Plan = Store.domainPlan.Value
                                Opened = opened.OpenedToken.Value
                                Challenge = nonce
                                Pin = "1234"
                                IdemKey = "k-1"
                            }

                    match answer with
                    | SigningOutcome.Submitted(v, _) -> v.No |> Expect.equal "version 1" 1
                    | other -> failtest $"expected Submitted, got %A{other}"
                | other -> failtest $"expected ChallengeIssued, got %A{other}"
            }
        ]
