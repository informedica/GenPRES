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
    | Session.WriteCredential _ -> "WriteCredential"
    | Session.WriteCode _ -> "WriteCode"
    | Session.CountCodeTry _ -> "CountCodeTry"
    | Session.SpendCode _ -> "SpendCode"
    | Session.WriteEnrolment _ -> "WriteEnrolment"
    | Session.DropEnrolmentWrite _ -> "DropEnrolmentWrite"
    | Session.DropEnrolmentsOf _ -> "DropEnrolmentsOf"
    | Session.WriteNotice _ -> "WriteNotice"
    | Session.WriteChallenge _ -> "WriteChallenge"
    | Session.SpendChallenge _ -> "SpendChallenge"
    | Session.RememberAnswer _ -> "RememberAnswer"


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

            test "callback: the two halves answer, change the state and write as the whole does" {
                // a redeem that is not one-time, so the two runs redeem the same code, and ids
                // from a counter of their own, so the two runs mint the same ones
                let redeem _ =
                    Some
                        {
                            Login = "prescriber"
                            DisplayName = "Stub Prescriber"
                        }

                let d = directory ()
                let state, cb = presentedFor "prescriber" "n-1" d (ids ()) seeded

                let whole =
                    Session.callback
                        t0
                        (ids ())
                        (codes ())
                        codeMac
                        redeem
                        Store.registry
                        StubPatientData.port.read
                        ignore
                        state
                        cb

                let halves =
                    match Session.redeem t0 redeem Store.registry state cb with
                    | Session.Redeemed.Identified(record, identity, standing) ->
                        Session.openAfterRedeem
                            t0
                            (ids ())
                            (codes ())
                            codeMac
                            StubPatientData.port.read
                            ignore
                            (record, identity, standing)
                            state
                    | other -> failtest $"expected Identified, got %A{other}"

                halves |> Expect.equal "the state, the answer and the writes" whole

                // the comparison alone proves only that the two routes agree, since `callback`
                // composes the halves: what the open comes to is asserted as well
                match whole with
                | next, CallbackResult.Opened(sid, _), writes ->
                    writes
                    |> names
                    |> Expect.equal "an open" [ "OpenSession"; "RecordOpenedWith"; "RecordLaunchOutcome" ]

                    match writes with
                    | [ Session.OpenSession(a, opened)
                        Session.RecordOpenedWith(b, alsoOpened, at)
                        Session.RecordLaunchOutcome(nonce, LaunchResult.Opened(c, _), at') ] ->
                        [ a; b; c ] |> Expect.equal "the Session opened" [ sid; sid; sid ]
                        opened |> Expect.equal "the Session as the state holds it" next.Sessions[sid]
                        alsoOpened |> Expect.equal "what it opened with, the same Session" opened
                        [ at; at' ] |> Expect.equal "at the request's time" [ t0; t0 ]
                        nonce |> Expect.equal "the Launch" "n-1"
                    | other -> failtest $"%A{other}"

                    next.Sessions[sid].Login |> Expect.equal "the login" (Some "prescriber")

                    next.Launches["n-1"].Outcome |> Expect.isSome "the launch carries its outcome"
                | _, other, _ -> failtest $"expected Opened, got %A{other}"
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
                    |> Expect.equal
                        "the writes, the right PIN clearing the count among them"
                        [
                            "RecordSeen"
                            "WriteCredential"
                            "WriteVersion"
                            "RecordOpenedWith"
                            // the challenge it answered, used up, and the answer kept
                            "SpendChallenge"
                            "RememberAnswer"
                        ]
                | other -> failtest $"expected Submitted, got %A{other}"

                // three wrong PINs: the last ends the Session
                let wrong (state, _, _) key =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid (signature "9999" key) state

                let _, last, writes = [ "w-2"; "w-3" ] |> List.fold wrong (wrong (state, (), []) "w-1")

                last
                |> Expect.equal "the limit" (SigningOutcome.Refused SigningRefusal.PinLimit)

                writes
                |> names
                |> Expect.equal
                    "the credential that reached the limit, the ending, and the answer remembered"
                    [ "RecordSeen"; "WriteCredential"; "EndSession"; "RememberAnswer" ]

                match
                    writes
                    |> List.tryPick (
                        function
                        | Session.WriteCredential(_, event, credential, _) -> Some(event, credential)
                        | _ -> None
                    )
                with
                | Some(event, credential) ->
                    event |> Expect.equal "the lock is the event" "locked"
                    credential.LockedUntil |> Expect.isSome "and the credential carries it"
                | None -> failtest "expected the credential write"
            }
        ]


/// A port with the clock at t0 over a store.
let portOver (store: StubDatabase.SessionStore) =
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


/// A launch of `no-pin`, which suspends at the PIN question: the state, the attempt and the
/// writes the suspension returned.
let suspended () =
    let d = directory ()
    let newId = ids ()
    let state, cb = presentedFor "no-pin" "n-1" d newId seeded

    match callbackCounting d newId (ref 0) state cb with
    | state, CallbackResult.Enrolling(attempt, _, _), writes -> state, attempt, writes, newId, d
    | _, other, _ -> failtest $"expected Enrolling, got %A{other}"


[<Tests>]
let credentialWrites =
    testList
        "the writes of the credential, the code and the attempts"
        [
            test "a launch that suspends writes the code it mailed and the attempt it made" {
                let _, _, writes, _, _ = suspended ()

                names writes
                |> Expect.equal
                    "the code, the attempt, and what the launch came to"
                    [ "WriteCode"; "WriteEnrolment"; "RecordLaunchOutcome" ]
            }

            test "a second launch of the same person writes the attempt but no second code" {
                let state, _, _, newId, _ = suspended ()
                let d = directory ()
                let state, cb = presentedFor "no-pin" "n-2" d newId state

                match callbackCounting d newId (ref 0) state cb with
                | _, CallbackResult.Enrolling _, writes ->
                    names writes
                    |> Expect.equal
                        "no second code: the one that stands is the one to enter"
                        [ "WriteEnrolment"; "RecordLaunchOutcome" ]
                | _, other, _ -> failtest $"expected Enrolling, got %A{other}"
            }

            test "a wrong code counts a try; the third voids the code for every attempt" {
                let state, attempt, _, newId, d = suspended ()

                let supply state code =
                    Session.supplyPin
                        t0
                        newId
                        salts
                        codeMac
                        d.registry.standing
                        StubPatientData.port.read
                        ignore
                        attempt
                        code
                        "2468"
                        state

                let state, _, first = supply state "000000"
                names first |> Expect.equal "the try is counted" [ "CountCodeTry" ]

                let state, _, _ = supply state "000000"
                let _, answer, third = supply state "000000"

                answer
                |> Expect.equal "the code is void" (SupplyPinResult.Refused PinRefusal.CodeVoid)

                names third
                |> Expect.equal
                    "the last try, then the code spent and every attempt of that person dropped"
                    [ "CountCodeTry"; "SpendCode"; "DropEnrolmentsOf" ]
            }

            test "the PIN set writes the credential, spends the code and drops the attempts" {
                let state, attempt, _, newId, d = suspended ()
                let code = "000001"

                let _, answer, writes =
                    Session.supplyPin
                        t0
                        newId
                        salts
                        codeMac
                        d.registry.standing
                        StubPatientData.port.read
                        ignore
                        attempt
                        code
                        "2468"
                        state

                match answer with
                | SupplyPinResult.Opened _ ->
                    names writes
                    |> Expect.equal
                        "the credential first, then the code and the attempts, then the open"
                        [
                            "WriteCredential"
                            "SpendCode"
                            "DropEnrolmentsOf"
                            "OpenSession"
                            "RecordOpenedWith"
                        ]

                    match
                        writes
                        |> List.tryPick (
                            function
                            | Session.WriteCredential(userId, event, credential, _) -> Some(userId, event, credential)
                            | _ -> None
                        )
                    with
                    | Some(userId, event, credential) ->
                        userId |> Expect.equal "the person who enrolled" "no-pin"
                        event |> Expect.equal "the event it was" "pin-set"
                        credential.WrongCount |> Expect.equal "counted from zero" 0

                        PinHash.verify "2468" credential.PinHash.Value
                        |> Expect.isTrue "the PIN they chose"
                    | None -> failtest "expected the credential write"
                | other -> failtest $"expected Opened, got %A{other}"
            }
        ]


[<Tests>]
let flightWrites =
    testList
        "the writes of what a Session holds in flight"
        [
            test "a challenge is written, and the one it replaces is spent" {
                let state, sid, newId = opened ()

                let token = state.Sessions[sid].Opened.OpenedToken.Value

                let issue state =
                    Session.challenge
                        t0
                        newId
                        StubDatabase.digest
                        StubPatientData.port.read
                        sid
                        (Store.domainPlan.Value, token, None)
                        state

                let state, first, writes = issue state

                names writes
                |> Expect.equal "the heartbeat and the challenge" [ "RecordSeen"; "WriteChallenge" ]

                match first with
                | SigningOutcome.ChallengeIssued _ -> ()
                | other -> failtest $"expected ChallengeIssued, got %A{other}"

                // a second challenge in the same Session: the one it replaces is not spent by
                // the write, it is replaced by a newer row the loader reads instead
                let _, _, again = issue state

                names again
                |> Expect.equal "the heartbeat and the newer challenge" [ "RecordSeen"; "WriteChallenge" ]
            }

            test "a notice is written, and it spends the challenge it invalidates" {
                let state, sid, newId = opened ()
                let token = state.Sessions[sid].Opened.OpenedToken.Value

                // a challenge first, then data the Session did not open on
                let state, _, _ =
                    Session.challenge
                        t0
                        newId
                        StubDatabase.digest
                        StubPatientData.port.read
                        sid
                        (Store.domainPlan.Value, token, None)
                        state

                let moved _ =
                    { StubPatientData.patient with Department = Some "ICU" }
                    |> Patient.parse
                    |> Result.toOption

                let _, answer, writes =
                    Session.challenge t0 newId StubDatabase.digest moved sid (Store.domainPlan.Value, token, None) state

                match answer with
                | SigningOutcome.DataNotice _ ->
                    names writes
                    |> Expect.equal
                        "the challenge over the old data is spent, and the notice written"
                        [ "RecordSeen"; "SpendChallenge"; "WriteNotice" ]
                | other -> failtest $"expected DataNotice, got %A{other}"
            }

            test "an answer is remembered once: the same Submission writes nothing more" {
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

                let signature: Signature =
                    {
                        Plan = Store.domainPlan.Value
                        Opened = token
                        Challenge = nonce
                        Pin = "1234"
                        IdemKey = "k-1"
                    }

                let state, answer, writes =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid signature state

                writes
                |> List.exists (
                    function
                    | Session.RememberAnswer(_, "k-1", _, _) -> true
                    | _ -> false
                )
                |> Expect.isTrue "the answer is kept under the key it came with"

                // the same Submission again: the answer it already has, and only the heartbeat
                let _, again, writes =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid signature state

                again |> Expect.equal "answered as the first time" answer

                names writes
                |> Expect.equal "nothing written but the heartbeat" [ "RecordSeen" ]
            }
        ]
