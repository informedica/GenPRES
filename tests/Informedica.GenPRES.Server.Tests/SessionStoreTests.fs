/// The session store on domain values: the records on stored versions, readable or not; the
/// challenge on the digest of the plan; the write as a value the adapter runs; the signing
/// handler parsing the plan in and mapping the answer out.
module Informedica.GenPRES.Server.Tests.SessionStoreTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


let t0 = DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc)
let seconds (n: float) = TimeSpan.FromSeconds n
let minutes (n: float) = TimeSpan.FromMinutes n

let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"

let stubPatient = StubPatientData.patient
let token sid = OpenedToken $"opened-{sid}"

let prescriber: UserContext =
    {
        UserId = "prescriber"
        DisplayName = "Stub Prescriber"
        Role = UserRole.Prescriber
    }

let other: UserContext =
    {
        UserId = "prescriber-b"
        DisplayName = "Stub Prescriber B"
        Role = UserRole.Prescriber
    }

let registry (identity: BrowserIdentity) : UserStanding option =
    match identity.Login with
    | "prescriber"
    | "prescriber-b" ->
        Some
            {
                User =
                    {
                        UserId = identity.Login
                        DisplayName = identity.DisplayName
                        Role = UserRole.Prescriber
                    }
                ActivePatientId = Some "stub-patient"
                MailAddress = $"{identity.Login}@stub.example"
            }
    | _ -> None

let seeded = SessionStubTests.seeded
let parsed = SessionStubTests.parsed

let plan =
    SessionStubTests.planOf
        stubPatient
        [|
            SessionStubTests.scenarioWithOrder "o-1"
            SessionStubTests.scenarioWithOrder "o-2"
        |]

// parsed on first use, never at type initialization: a failure there would end test discovery
let domainPlan = lazy (parsed plan)

let session sid (user: UserContext) openedWith =
    sid, SessionStubTests.sessionOf sid (Some user) (Some("stub-patient", Some stubPatient)) openedWith

let versionOf no (by: UserContext) (at: DateTime) (p: Types.OrderPlan) : Types.OrderPlanVersion =
    {
        Id = $"plan-{no}"
        No = no
        PatientId = "stub-patient"
        Base = if no > 1 then Some $"plan-{no - 1}" else None
        SignedBy =
            {
                UserId = by.UserId
                DisplayName = by.DisplayName
            }
        SignedAt = at
        Plan = p
        Verified = true
    }

let unreadable no (by: UserContext) (at: DateTime) : StoredVersion =
    StoredVersion.Unreadable
        {
            Id = $"plan-{no}"
            No = no
            PatientId = "stub-patient"
            Base = if no > 1 then Some $"plan-{no - 1}" else None
            SignedBy =
                {
                    UserId = by.UserId
                    DisplayName = by.DisplayName
                }
            SignedAt = at
            Reason = "json_version 9 is newer than this release knows"
        }

let stateOf sessions records =
    { seeded with
        Sessions = Map.ofList sessions
        Records = Map.ofList records
    }

let ask now nonces state sid (p, opened) =
    Machine.challenge now nonces StubDatabase.digest StubPatientData.port.read sid (p, opened, None) state

let signature sid pin key (p: Types.OrderPlan) : Signature =
    {
        Plan = p
        Opened = token sid
        Challenge = $"c-{sid}"
        Pin = pin
        IdemKey = key
    }

let challenged sid (at: DateTime) (p: Types.OrderPlan) : string * Session.Challenge =
    sid,
    {
        Nonce = $"c-{sid}"
        Digest = StubDatabase.digest p
        Reading = Some(SessionStubTests.parsePatient stubPatient)
        Expiry = at + Session.challengeLifetime
    }

/// The order plan version of a request's writes, when it has one.
let versionWritten writes =
    writes
    |> List.tryPick (
        function
        | Session.WriteVersion v -> Some v
        | _ -> None
    )


let commitAt now ids state sid s =
    Session.commit now ids StubDatabase.digest registry ignore sid s state

let submitWith persist now ids state sid s =
    StubDatabase.submitWith persist (fun st -> commitAt now ids st sid s) state

let opened = session "s-1" prescriber None

let ready =
    lazy ({ stateOf [ opened ] [] with Challenges = Map.ofList [ challenged "s-1" t0 domainPlan.Value ] })


[<Tests>]
let tests =
    testList
        "the session store on domain values"
        [
            test "the challenge keeps the digest of the plan, never the plan" {
                let state, answer =
                    ask t0 (counter "n") (stateOf [ opened ] []) "s-1" (domainPlan.Value, token "s-1")

                answer |> Expect.equal "issued" (SigningOutcome.ChallengeIssued "n-1")

                state.Challenges["s-1"].Digest
                |> Expect.equal "the digest of the plan" (StubDatabase.digest domainPlan.Value)
            }

            test
                "the commit over the plan as challenged signs: the version stores the whole plan, the write is the value returned" {
                let state, answer, write =
                    commitAt t0 (counter "id") ready.Value "s-1" (signature "s-1" "1234" "k-1" domainPlan.Value)

                match answer, versionWritten write with
                | SigningOutcome.Submitted(version, token), Some written ->
                    version |> Expect.equal "the write is the version" written
                    version.No |> Expect.equal "first" 1

                    version.Plan
                    |> Expect.equal "the plan as signed, contexts, filter and totals" domainPlan.Value

                    version.SignedBy.UserId |> Expect.equal "by" "prescriber"
                    version.Verified |> Expect.isTrue "the reading stood"

                    Session.headOf "stub-patient" state
                    |> Expect.equal "the head" (Some(StoredVersion.Readable version))

                    state.Sessions["s-1"].OpenedWith
                    |> Expect.equal "opened with it" (Some version.Id)

                    state.Sessions["s-1"].Opened.Head
                    |> Expect.equal "the head held" (Some(StoredVersion.Readable version))

                    state.Sessions["s-1"].Opened.OpenedToken
                    |> Expect.equal "re-minted" (Some token)

                    state.Challenges |> Expect.isEmpty "spent"
                | other -> failtest $"expected Submitted, got %A{other}"
            }

            test "a plan changed since the challenge is a mismatch: a context re-ordered or a row filtered too" {
                let changed =
                    parsed (
                        SessionStubTests.planOf
                            stubPatient
                            [|
                                SessionStubTests.scenarioWithOrder "o-1"
                                SessionStubTests.scenarioWithOrder "o-3"
                            |]
                    )

                let reordered =
                    parsed (
                        SessionStubTests.planOf
                            stubPatient
                            [|
                                SessionStubTests.scenarioWithOrder "o-2"
                                SessionStubTests.scenarioWithOrder "o-1"
                            |]
                    )

                for p in
                    [
                        changed
                        reordered
                        { domainPlan.Value with Filtered = [| "c-0" |] }
                    ] do
                    let _, answer, write =
                        commitAt t0 (counter "id") ready.Value "s-1" (signature "s-1" "1234" "k-1" p)

                    answer
                    |> Expect.equal "mismatch" (SigningOutcome.Refused SigningRefusal.ChallengeMismatch)

                    write |> versionWritten |> Expect.isNone "no version to write"
            }

            test "two plans equal as domain values digest equal; a different plan does not" {
                let roundTripped =
                    domainPlan.Value
                    |> OrderPlan.Dto.toDto
                    |> Canonical.serialize
                    |> Canonical.deserialize<OrderPlan.Dto.Dto>
                    |> OrderPlan.Dto.fromDto
                    |> Result.defaultWith (fun e -> failtest $"%A{e}")

                StubDatabase.digest (parsed plan)
                |> Expect.equal "built twice" (StubDatabase.digest domainPlan.Value)

                StubDatabase.digest roundTripped
                |> Expect.equal "through the store's form" (StubDatabase.digest domainPlan.Value)

                SessionStubTests.planOf
                    stubPatient
                    [|
                        SessionStubTests.scenarioWithOrder "o-2"
                        SessionStubTests.scenarioWithOrder "o-1"
                    |]
                |> parsed
                |> StubDatabase.digest
                |> Expect.notEqual "re-ordered" (StubDatabase.digest domainPlan.Value)
            }

            test "a failed write leaves the state unchanged and answers StoreFailed; a retry then lands" {
                let ids = counter "id"

                let state, answer =
                    submitWith
                        (fun _ -> Session.StoreOutcome.Failed "disk full")
                        t0
                        ids
                        ready.Value
                        "s-1"
                        (signature "s-1" "1234" "k-1" domainPlan.Value)

                answer
                |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)

                state |> Expect.equal "unchanged" ready.Value

                let state, retried =
                    submitWith
                        StubDatabase.persistNothing
                        t0
                        ids
                        state
                        "s-1"
                        (signature "s-1" "1234" "k-1" domainPlan.Value)

                match retried with
                | SigningOutcome.Submitted _ ->
                    state
                    |> SessionStubTests.versionsOf "stub-patient"
                    |> List.length
                    |> Expect.equal "landed" 1
                | other -> failtest $"expected Submitted, got %A{other}"
            }

            test
                "a violated constraint is another server's sign: a stale sign against the version that won, not StoreFailed" {
                let winner =
                    StoredVersion.Readable(versionOf 1 other (t0 - minutes 1.0) domainPlan.Value)

                let state, answer =
                    submitWith
                        (fun _ -> Session.StoreOutcome.Conflict winner)
                        t0
                        (counter "id")
                        ready.Value
                        "s-1"
                        (signature "s-1" "1234" "k-1" domainPlan.Value)

                answer
                |> Expect.equal
                    "blocked by the winner"
                    (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner)))

                state |> Expect.equal "unchanged" ready.Value
            }

            test
                "an unreadable row is the head when it is the newest; a sign against it is refused whatever the base; it cannot be opened" {
                let readable =
                    StoredVersion.Readable(versionOf 1 prescriber (t0 - minutes 5.0) domainPlan.Value)

                let newest = unreadable 2 other (t0 - minutes 1.0)
                let records = [ "stub-patient", [ newest; readable ] ]

                Session.headOf "stub-patient" (stateOf [] records)
                |> Expect.equal "the newest, unreadable" (Some newest)

                for openedWith in [ Some "plan-1"; Some "plan-2"; None ] do
                    let s = session "s-1" prescriber openedWith

                    let _, answer =
                        ask t0 (counter "n") (stateOf [ s ] records) "s-1" (domainPlan.Value, token "s-1")

                    answer
                    |> Expect.equal
                        $"refused over {openedWith}"
                        (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head newest)))

                    let st =
                        { stateOf [ s ] records with Challenges = Map.ofList [ challenged "s-1" t0 domainPlan.Value ] }

                    let _, answer, write =
                        commitAt t0 (counter "id") st "s-1" (signature "s-1" "1234" "k-1" domainPlan.Value)

                    answer
                    |> Expect.equal
                        $"refused at commit over {openedWith}"
                        (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head newest)))

                    write |> versionWritten |> Expect.isNone "no version written"

                let s = session "s-1" prescriber (Some "plan-1")

                let st, answer =
                    Machine.openVersion t0 (counter "id") "s-1" "plan-2" (stateOf [ s ] records)

                answer |> Expect.equal "the Session as it is" (Some (snd s).Opened)

                st.Sessions["s-1"].OpenedWith
                |> Expect.equal "still on the readable one" (Some "plan-1")
            }

            test
                "after a crash between the write and the reply the row is the head: a retry from the same base is stale, and the version opens" {
                let written = versionOf 1 prescriber t0 domainPlan.Value
                let s = session "s-1" prescriber None

                let st =
                    { stateOf [ s ] [ "stub-patient", [ StoredVersion.Readable written ] ] with
                        Challenges = Map.ofList [ challenged "s-1" t0 domainPlan.Value ]
                    }

                let _, answer, write =
                    commitAt (t0 + seconds 10.0) (counter "id") st "s-1" (signature "s-1" "1234" "k-2" domainPlan.Value)

                answer
                |> Expect.equal
                    "stale"
                    (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head (StoredVersion.Readable written))))

                write |> versionWritten |> Expect.isNone "no version written"

                let st, opened =
                    Machine.openVersion (t0 + seconds 10.0) (counter "id") "s-1" "plan-1" st

                opened
                |> Option.bind _.Head
                |> Expect.equal "the version signed" (Some(StoredVersion.Readable written))

                st.Sessions["s-1"].OpenedWith |> Expect.equal "opened with it" (Some "plan-1")
            }

            testAsync
                "the handler parses the plan in, refuses one the domain does not read before the port, and maps the answer out with the demo flag" {
                let seen = ref None

                let port =
                    { sessionNone with
                        challenge =
                            fun _ (p, _, _) ->
                                seen.Value <- Some p
                                async { return SigningOutcome.ChallengeIssued "n-1" }
                        submit =
                            fun _ s ->
                                seen.Value <- Some s.Plan
                                async { return SigningOutcome.Submitted(versionOf 1 prescriber t0 s.Plan, token "s-2") }
                    }

                let envOver demo =
                    { makeEnv
                          (formularyAlwaysOk Shared.Models.Formulary.empty)
                          (orderContextAlwaysOk Shared.Models.OrderContext.empty) with
                        session = port
                        demo = demo
                    }

                let cookie: SessionCookie =
                    {
                        read = fun () -> Some "s-1"
                        write = ignore
                        delete = ignore
                    }

                let! issued =
                    SigningCommand.processCmd
                        (envOver true)
                        cookie
                        (Shared.Api.SigningCommand.RequestSignChallenge(plan, token "s-1", None))

                issued |> Expect.equal "issued" (SigningResponse.ChallengeIssued "n-1")
                seen.Value |> Expect.equal "the plan parsed" (Some domainPlan.Value)

                let submission: Submission =
                    {
                        Plan = plan
                        Opened = token "s-1"
                        Challenge = "n-1"
                        Pin = "1234"
                        IdemKey = "k-1"
                    }

                let! submitted =
                    SigningCommand.processCmd (envOver false) cookie (Shared.Api.SigningCommand.Submit submission)

                match submitted with
                | SigningResponse.Submitted(signed, _) ->
                    signed.Head.Id |> Expect.equal "the version" "plan-1"

                    signed.OrderContexts
                    |> Array.map _.DemoVersion
                    |> Expect.equal "demo as the environment says" [| false; false |]

                    signed.OrderContexts
                    |> Array.map _.Id
                    |> Expect.equal "the contexts as signed" [| "c-0"; "c-1" |]
                | other -> failtest $"expected Submitted, got %A{other}"

                // a plan the domain does not read: refused before the port, never stored
                seen.Value <- None

                let bogus: ValueUnit =
                    {
                        Value = [| "1", 1m |]
                        Unit = "bogus"
                        Group = "bogus"
                        Short = false
                        Language = ""
                        Json = ""
                    }

                let unreadable =
                    { plan with
                        OrderContexts =
                            plan.OrderContexts
                            |> Array.map (fun c ->
                                { c with
                                    Scenarios =
                                        c.Scenarios
                                        |> Array.map (fun sc ->
                                            { sc with
                                                Order =
                                                    { sc.Order with
                                                        Duration =
                                                            { sc.Order.Duration with
                                                                Variable =
                                                                    { sc.Order.Duration.Variable with
                                                                        Vals = Some bogus
                                                                    }
                                                            }
                                                    }
                                            }
                                        )
                                }
                            )
                    }

                let! refused =
                    SigningCommand.processCmd
                        (envOver true)
                        cookie
                        (Shared.Api.SigningCommand.RequestSignChallenge(unreadable, token "s-1", None))

                refused
                |> Expect.equal "unreadable" (SigningResponse.Refused SigningRefusal.PlanUnreadable)

                seen.Value |> Expect.isNone "the port never asked"

                let! refusedAtCommit =
                    SigningCommand.processCmd
                        (envOver true)
                        cookie
                        (Shared.Api.SigningCommand.Submit { submission with Plan = unreadable })

                refusedAtCommit
                |> Expect.equal "unreadable at commit" (SigningResponse.Refused SigningRefusal.PlanUnreadable)

                seen.Value |> Expect.isNone "the port never asked"
            }
        ]
