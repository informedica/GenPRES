/// The launch and session rows of the session store's SQLite database: what the loader reads
/// back from them, and the words the columns hold a refusal and a Role under.
module Informedica.GenPRES.Server.Tests.SqlSessionTests

open System
open System.IO
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Shared.Types
open ServerApi
// the canonical serializer the stored patient is written with
open Informedica.GenOrder.Lib

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests
open Informedica.GenPRES.Server.Tests.SqlSchemaTests


/// A fresh database with every migration applied.
let withSessions f =
    withDb (fun cs ->
        SqlSchema.apply cs |> Expect.equal "every migration" [ 1; 2; 3; 4; 5 ]
        f cs
    )


let connect (cs: string) =
    let conn = new SqliteConnection(cs)
    conn.Open()
    conn


let t0 = DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc)


/// The writes of a request, run on the fixture's clock: the audit of a write with no time of
/// its own is timed by the request.
let runWrites (cs: string) (writes: Session.Persist list) =
    SqlSessions.runWrites cs (fun () -> t0) writes


/// The rows a presented Launch and its callback leave behind; the writer that returns them
/// from the machine's writes is the next step, so the test writes them itself.
let insertLaunch (cs: string) (nonce: string) (expiry: DateTime) =
    use conn = connect cs

    use cmd =
        SqlSessions.command
            conn
            null
            "insert into launch_record (nonce, state, patient_id, public_key, expiry) values ($n, $s, 'stub-patient', 'key-a', $e)"
            [
                "$n", box nonce
                "$s", box $"state-%s{nonce}"
                "$e", box (SqlSessions.ms expiry)
            ]

    cmd.ExecuteNonQuery() |> ignore


let insertOutcome (cs: string) (nonce: string) (outcome: string) (attempt: string option) =
    use conn = connect cs

    use cmd =
        SqlSessions.command
            conn
            null
            "insert into launch_outcome (nonce, outcome, session_id, attempt, at) values ($n, $o, null, $a, $at)"
            [
                "$n", box nonce
                "$o", box outcome
                "$a", attempt |> Option.map box |> Option.defaultValue (box DBNull.Value)
                "$at", box (SqlSessions.ms t0)
            ]

    cmd.ExecuteNonQuery() |> ignore


/// The patient a Session shows, as the store holds it and as the domain holds it.
let patient =
    Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests.parsePatient StubPatientData.patient

let patientJson =
    patient |> Informedica.GenForm.Lib.Patient.Dto.toDto |> Canonical.serialize


/// The rows an open leaves behind; the writer that returns them from the machine's writes is
/// the next step, so the test writes them itself.
let insertSession (cs: string) (sid: string) (login: string option) (role: string) =
    use conn = connect cs

    use cmd =
        SqlSessions.command
            conn
            null
            """
            insert into session
                (session_id, login, user_id, user_display, user_role, patient_id, key_thumbprint, opened_at)
            values ($sid, $login, 'user-1', 'Stub Prescriber', $role, 'stub-patient', 'thumb', $at)
            """
            [
                "$sid", box sid
                "$login", login |> Option.map box |> Option.defaultValue (box DBNull.Value)
                "$role", box role
                "$at", box (SqlSessions.ms t0)
            ]

    cmd.ExecuteNonQuery() |> ignore


let insertOpenedWith (cs: string) (sid: string) (versionId: string option) (jsonVersion: int) (json: string option) =
    use conn = connect cs

    use cmd =
        SqlSessions.command
            conn
            null
            """
            insert into session_opened_with
                (session_id, version_id, head_id, opened_token, json_version, patient, at)
            values ($sid, $v, $v, $token, $jv, $patient, $at)
            """
            [
                "$sid", box sid
                "$v", versionId |> Option.map box |> Option.defaultValue (box DBNull.Value)
                "$token", box $"opened-%s{sid}"
                "$jv",
                json
                |> Option.map (fun _ -> box jsonVersion)
                |> Option.defaultValue (box DBNull.Value)
                "$patient", json |> Option.map box |> Option.defaultValue (box DBNull.Value)
                "$at", box (SqlSessions.ms t0)
            ]

    cmd.ExecuteNonQuery() |> ignore


let insertRow (cs: string) (sql: string) (parameters: (string * obj) list) =
    use conn = connect cs
    use cmd = SqlSessions.command conn null sql parameters
    cmd.ExecuteNonQuery() |> ignore


let loadedSession (cs: string) (sid: string) =
    use conn = connect cs
    SqlSessions.loadSession conn (fun _ -> None) sid


[<Tests>]
let tests =
    testList
        "the launch rows"
        [
            test "a Launch is found by its nonce and by its state, with what its callback came to" {
                withSessions (fun cs ->
                    insertLaunch cs "n-1" (t0.AddMinutes 2.0)
                    insertOutcome cs "n-1" "enrolling" (Some "attempt-1")

                    use conn = connect cs

                    match SqlSessions.loadLaunch conn t0 "state" "state-n-1" with
                    | Some row ->
                        row.Nonce |> Expect.equal "found by its state" "n-1"
                        row.PatientId |> Expect.equal "the patient it was launched on" "stub-patient"
                        row.PublicKey |> Expect.equal "the browser's key" (PublicKey "key-a")
                        row.Outcome |> Expect.equal "the outcome" (Some "enrolling")
                        row.Attempt |> Expect.equal "the attempt it named" (Some "attempt-1")
                    | None -> failtest "expected the Launch"

                    match SqlSessions.loadLaunch conn t0 "nonce" "n-1" with
                    | Some row -> row.State |> Expect.equal "found by its nonce" "state-n-1"
                    | None -> failtest "expected the Launch"

                    SqlSessions.loadLaunch conn t0 "nonce" "n-2"
                    |> Expect.isNone "a nonce the store does not know"
                )
            }

            test "a Launch without a callback yet, and one past its lifetime" {
                withSessions (fun cs ->
                    insertLaunch cs "n-1" (t0.AddMinutes 2.0)

                    use conn = connect cs

                    match SqlSessions.loadLaunch conn t0 "nonce" "n-1" with
                    | Some row -> row.Outcome |> Expect.isNone "nothing came of it yet"
                    | None -> failtest "expected the Launch"

                    // the row is still there, but a Launch past its expiry is no Launch
                    SqlSessions.loadLaunch conn (t0.AddMinutes 3.0) "nonce" "n-1"
                    |> Expect.isNone "past its lifetime it is absent"

                    SqlSessions.loadLaunch conn (t0.AddMinutes 2.0) "nonce" "n-1"
                    |> Expect.isSome "at its expiry it still stands"
                )
            }

            test "every refusal is stored by its word and reads back as itself" {
                for refusal in
                    [
                        LaunchRefusal.LaunchExpired
                        LaunchRefusal.LaunchSpent
                        LaunchRefusal.LaunchInvalid
                        LaunchRefusal.NoBrowserIdentity
                        LaunchRefusal.NoRole
                        LaunchRefusal.WrongActivePatient
                        LaunchRefusal.EnrolmentRequired
                    ] do
                    refusal
                    |> Session.refusalWord
                    |> SqlSessions.refusalOf
                    |> Expect.equal $"the refusal of %A{refusal}" (Some refusal)

                SqlSessions.refusalOf "no-such-word" |> Expect.isNone "a word no refusal has"
            }

            test "a refusal reaches the column as its word, prefixed" {
                withSessions (fun cs ->
                    insertLaunch cs "n-1" (t0.AddMinutes 2.0)

                    insertOutcome cs "n-1" $"refused:%s{Session.refusalWord LaunchRefusal.WrongActivePatient}" None

                    use conn = connect cs
                    let row = (SqlSessions.loadLaunch conn t0 "nonce" "n-1").Value

                    row.Outcome
                    |> Option.map (fun word -> word.Substring "refused:".Length)
                    |> Option.bind SqlSessions.refusalOf
                    |> Expect.equal "the refusal it was refused with" (Some LaunchRefusal.WrongActivePatient)
                )
            }

            test "every Role is stored by its word, and a word no Role has is no Role" {
                for role in [ UserRole.Prescriber; UserRole.Reader ] do
                    role
                    |> SqlSessions.roleWord
                    |> SqlSessions.roleOf
                    |> Expect.equal $"the Role of %A{role}" (Some role)

                // the column is free text: a word this release does not know must not come
                // back as the Role that may sign
                SqlSessions.roleOf "administrator" |> Expect.isNone "a word no Role has"

                SqlSessions.roleOf "Prescriber"
                |> Expect.isNone "the word of a Role, spelled otherwise"
            }

            test "a time reaches the column as Unix milliseconds and back" {
                t0
                |> SqlSessions.ms
                |> SqlSessions.at
                |> Expect.equal "the same instant, in UTC" t0
            }
        ]


[<Tests>]
let sessionTests =
    testList
        "the session rows"
        [
            test "a Session loads back as the state holds it, with its patient and its token" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" (Some "plan-1") 1 (Some patientJson)

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok session)) ->
                        session.Login |> Expect.equal "the login it belongs to" (Some "prescriber")
                        session.OpenedWith |> Expect.equal "the version it opened with" (Some "plan-1")
                        session.Seen |> Expect.equal "seen when it opened" t0

                        session.Opened.PatientId
                        |> Expect.equal "the patient it was launched on" (Some "stub-patient")

                        session.Opened.Patient |> Expect.equal "the data it shows" (Some patient)

                        session.Opened.OpenedToken
                        |> Expect.equal "its token" (Some(OpenedToken "opened-s-1"))

                        session.Opened.KeyThumbprint
                        |> Expect.equal "the key it signs with" (Some "thumb")

                        session.Opened.User
                        |> Option.map _.Role
                        |> Expect.equal "the Role it was opened with" (Some UserRole.Prescriber)
                    | other -> failtest $"expected the Session, got %A{other}"

                    loadedSession cs "s-2" |> Expect.isNone "an id the store does not know"
                )
            }

            test "the newest heartbeat and the newest opened-with count" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" (Some "plan-1") 1 (Some patientJson)
                    insertOpenedWith cs "s-1" (Some "plan-2") 1 (Some patientJson)

                    let later = t0.AddMinutes 5.0

                    insertRow
                        cs
                        "insert into session_seen (session_id, at) values ('s-1', $at)"
                        [ "$at", box (SqlSessions.ms later) ]

                    insertRow
                        cs
                        "insert into session_seen (session_id, at) values ('s-1', $at)"
                        [ "$at", box (SqlSessions.ms (t0.AddMinutes 1.0)) ]

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok session)) ->
                        session.OpenedWith |> Expect.equal "the newest opened-with" (Some "plan-2")
                        // the newest row, by id and not by the time it carries
                        session.Seen |> Expect.equal "the newest heartbeat" (t0.AddMinutes 1.0)
                    | other -> failtest $"expected the Session, got %A{other}"
                )
            }

            test "a newer Session of the login supersedes the older, whatever became of it" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" None 1 (Some patientJson)
                    insertSession cs "s-2" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-2" None 1 (Some patientJson)

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.SupersededByLaunch, at))) ->
                        at |> Expect.equal "at the newer open" t0
                    | other -> failtest $"expected superseded, got %A{other}"

                    // closing the newer one does not hand the login back to the older
                    insertRow
                        cs
                        "insert into session_ending (session_id, ending, at) values ('s-2', 'closed', $at)"
                        [ "$at", box (SqlSessions.ms t0) ]

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.SupersededByLaunch, _))) -> ()
                    | other -> failtest $"expected superseded still, got %A{other}"

                    // and a Session of another login is untouched by either
                    insertSession cs "s-3" (Some "prescriber-b") "prescriber"
                    insertOpenedWith cs "s-3" None 1 (Some patientJson)

                    match loadedSession cs "s-3" with
                    | Some(Choice1Of2(Ok _)) -> ()
                    | other -> failtest $"expected an open Session, got %A{other}"
                )
            }

            test "the endings that are acts, and an acknowledged one told no more" {
                withSessions (fun cs ->
                    for sid, ending in
                        [
                            "s-1", "wrong-pin-limit"
                            "s-2", "unreadable"
                            "s-3", "closed"
                        ] do
                        insertSession cs sid (Some $"user-%s{sid}") "prescriber"
                        insertOpenedWith cs sid None 1 (Some patientJson)

                        insertRow
                            cs
                            "insert into session_ending (session_id, ending, at) values ($sid, $e, $at)"
                            [
                                "$sid", box sid
                                "$e", box ending
                                "$at", box (SqlSessions.ms t0)
                            ]

                    insertRow
                        cs
                        "insert into session_acknowledged (session_id, at) values ('s-3', $at)"
                        [ "$at", box (SqlSessions.ms t0) ]

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.WrongPinLimit, _))) -> ()
                    | other -> failtest $"expected the PIN limit, got %A{other}"

                    match loadedSession cs "s-2" with
                    | Some(Choice2Of2(Some(SessionEnding.Unreadable, _))) -> ()
                    | other -> failtest $"expected unreadable, got %A{other}"

                    match loadedSession cs "s-3" with
                    | Some(Choice2Of2 None) -> ()
                    | other -> failtest $"expected a closed Session, told nothing, got %A{other}"
                )
            }

            test "an ending the User acknowledged leaves no Session, whatever it ended of" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" None 1 (Some patientJson)

                    insertRow
                        cs
                        "insert into session_ending (session_id, ending, at) values ('s-1', 'wrong-pin-limit', $at)"
                        [ "$at", box (SqlSessions.ms t0) ]

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.WrongPinLimit, _))) -> ()
                    | other -> failtest $"expected the PIN limit, got %A{other}"

                    // the User was told and asked to leave: the Session is gone, not open again
                    insertRow
                        cs
                        "insert into session_acknowledged (session_id, at) values ('s-1', $at)"
                        [ "$at", box (SqlSessions.ms t0) ]

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2 None) -> ()
                    | other -> failtest $"expected no Session, told nothing, got %A{other}"
                )
            }

            test "an ending word this release does not know ends the Session all the same" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" None 1 (Some patientJson)

                    insertRow
                        cs
                        "insert into session_ending (session_id, ending, at) values ('s-1', 'left-the-building', $at)"
                        [ "$at", box (SqlSessions.ms t0) ]

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.Unreadable, at))) -> at |> Expect.equal "when it ended" t0
                    | other -> failtest $"expected unreadable, got %A{other}"
                )
            }

            test "a patient this release cannot read makes the Session unreadable" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" None 9 (Some patientJson)

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Error reason)) ->
                        reason.Contains "newer than this release knows"
                        |> Expect.isTrue $"the reason: %s{reason}"
                    | other -> failtest $"expected unreadable, got %A{other}"

                    insertSession cs "s-2" (Some "reader") "prescriber"
                    insertOpenedWith cs "s-2" None 1 (Some "{ not json")

                    match loadedSession cs "s-2" with
                    | Some(Choice1Of2(Error _)) -> ()
                    | other -> failtest $"expected unreadable, got %A{other}"
                )
            }

            test "a Role this release cannot read makes the Session unreadable, never a signer" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "administrator"
                    insertOpenedWith cs "s-1" None 1 (Some patientJson)

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Error reason)) ->
                        reason.Contains "administrator" |> Expect.isTrue $"the reason: %s{reason}"
                    | other -> failtest $"expected unreadable, got %A{other}"
                )
            }

            test "a Session that opened on no patient data, and on nothing at all" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "reader"
                    insertOpenedWith cs "s-1" None 1 None

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok session)) ->
                        session.Opened.Patient |> Expect.isNone "no patient data"
                        session.Opened.Head |> Expect.isNone "no head either"

                        session.Opened.User
                        |> Option.map _.Role
                        |> Expect.equal "a Reader" (Some UserRole.Reader)
                    | other -> failtest $"expected the Session, got %A{other}"

                    // a Session with no opened-with row at all: nothing came after the open
                    insertSession cs "s-2" (Some "prescriber-b") "prescriber"

                    match loadedSession cs "s-2" with
                    | Some(Choice1Of2(Ok session)) ->
                        session.OpenedWith |> Expect.isNone "it opened with no version"
                        session.Opened.OpenedToken |> Expect.isNone "and holds no token"
                        session.Seen |> Expect.equal "seen when it opened" t0
                    | other -> failtest $"expected the Session, got %A{other}"
                )
            }

            test "the head it opened on is the record's, through the headOf it is loaded with" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" (Some "plan-1") 1 (Some patientJson)

                    let head =
                        Store.domainPlan.Value
                        |> Store.versionOf 1 Store.prescriber Store.t0
                        |> fun v -> StoredVersion.Readable { v with Id = "plan-1" }

                    use conn = connect cs

                    match SqlSessions.loadSession conn (fun id -> if id = "plan-1" then Some head else None) "s-1" with
                    | Some(Choice1Of2(Ok session)) ->
                        session.Opened.Head |> Expect.equal "the head of its own id" (Some head)
                    | other -> failtest $"expected the Session, got %A{other}"
                )
            }
        ]


/// The Session an open writes: a Prescriber on the stub patient, with a token and a key.
let sessionOf sid login openedWith head : Session.SessionRecord =
    {
        Opened =
            {
                User =
                    Some
                        {
                            UserId = "user-1"
                            DisplayName = "Stub Prescriber"
                            Role = UserRole.Prescriber
                        }
                PatientId = Some "stub-patient"
                Patient = Some patient
                OpenedToken = Some(OpenedToken $"opened-%s{sid}")
                KeyThumbprint = Some "thumb"
                Head = head
            }
        Login = Some login
        OpenedWith = openedWith
        Seen = t0
    }


let launchOf nonce : Session.LaunchRecord =
    {
        Nonce = nonce
        State = $"state-%s{nonce}"
        PatientId = "stub-patient"
        PublicKey = PublicKey "key-a"
        Expiry = t0.AddMinutes 2.0
        Outcome = None
    }


[<Tests>]
let writerTests =
    testList
        "the writes of a request"
        [
            test "an open is written and loads back as the Session the machine held" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok loaded)) -> loaded |> Expect.equal "the Session as it was" session
                    | other -> failtest $"expected the Session, got %A{other}"
                )
            }

            test "a heartbeat, a move to another version, and an ending, all as the loader reads them" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None
                    let later = t0.AddMinutes 5.0

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                        ]
                    |> ignore

                    let moved =
                        { session with
                            OpenedWith = Some "plan-1"
                            Opened = { session.Opened with OpenedToken = Some(OpenedToken "opened-again") }
                        }

                    runWrites
                        cs
                        [
                            Session.RecordSeen("s-1", later)
                            Session.RecordOpenedWith("s-1", moved, later)
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok loaded)) ->
                        loaded.OpenedWith |> Expect.equal "the newest opened-with" (Some "plan-1")

                        loaded.Opened.OpenedToken
                        |> Expect.equal "its token" (Some(OpenedToken "opened-again"))

                        loaded.Seen |> Expect.equal "the newest heartbeat" later
                    | other -> failtest $"expected the Session, got %A{other}"

                    runWrites
                        cs
                        [
                            Session.EndSession("s-1", Session.StoredEnding.Ended SessionEnding.WrongPinLimit, later)
                        ]
                    |> ignore

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.WrongPinLimit, at))) ->
                        at |> Expect.equal "when it ended" later
                    | other -> failtest $"expected the PIN limit, got %A{other}"
                )
            }

            test "a Launch and its outcome are written and read back as the launch they were" {
                withSessions (fun cs ->
                    runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-1")
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Enrolling "attempt-1", t0)
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    use conn = connect cs

                    match SqlSessions.loadLaunch conn t0 "state" "state-n-1" with
                    | Some row ->
                        row.PublicKey |> Expect.equal "the browser's key" (PublicKey "key-a")
                        row.Outcome |> Expect.equal "the outcome" (Some "enrolling")
                        row.Attempt |> Expect.equal "the attempt it named" (Some "attempt-1")
                    | None -> failtest "expected the Launch"

                    runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-2")
                            Session.RecordLaunchOutcome(
                                "n-2",
                                LaunchResult.Refused LaunchRefusal.WrongActivePatient,
                                t0
                            )
                        ]
                    |> ignore

                    (SqlSessions.loadLaunch conn t0 "nonce" "n-2").Value.Outcome
                    |> Option.map (fun word -> word.Substring "refused:".Length)
                    |> Option.bind SqlSessions.refusalOf
                    |> Expect.equal "the refusal it was refused with" (Some LaunchRefusal.WrongActivePatient)
                )
            }

            test "a request's writes are one transaction: one fails, none lands" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    // the second write names a Session that was never opened
                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordSeen("s-9", t0)
                        ]
                    |> function
                        | Session.StoreOutcome.Failed _ -> ()
                        | other -> failtest $"expected Failed, got %A{other}"

                    loadedSession cs "s-1" |> Expect.isNone "the open rolled back with it"
                )
            }

            test "another server's version of the same number is a conflict, and the rest rolls back" {
                withSessions (fun cs ->
                    let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0

                    // the Session opened on the version it signed, which the rows name by id
                    let session =
                        sessionOf "s-1" "prescriber" (Some v1.Id) (Some(StoredVersion.Readable v1))

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.WriteVersion v1
                        ]
                    |> Expect.equal "the first lands" Session.StoreOutcome.Written

                    let rival = { v1 with Id = "plan-rival" }

                    match
                        runWrites
                            cs
                            [
                                Session.WriteVersion rival
                                Session.RecordSeen("s-1", t0)
                            ]
                    with
                    | Session.StoreOutcome.Conflict head ->
                        head |> StoredVersion.id |> Expect.equal "the row that won" v1.Id
                    | other -> failtest $"expected Conflict, got %A{other}"

                    use conn = connect cs
                    use cmd = SqlSessions.command conn null "select count(*) from session_seen" []

                    cmd.ExecuteScalar()
                    |> unbox<int64>
                    |> Expect.equal "the heartbeat rolled back" 0L
                )
            }

            test "a store that cannot be written is Failed, with its reason" {
                withSessions (fun cs ->
                    let readOnly = $"%s{cs};Mode=ReadOnly"

                    runWrites readOnly [ Session.RecordLaunch(launchOf "n-1") ]
                    |> function
                        | Session.StoreOutcome.Failed reason -> reason |> Expect.isNotEmpty "the reason it failed"
                        | other -> failtest $"expected Failed, got %A{other}"
                )
            }

            test "a store that cannot be reached at all is Failed too, not an exception" {
                // the file cannot be opened: the folder holding it does not exist
                let missing =
                    SqliteConnectionStringBuilder(
                        DataSource = Path.Combine(Path.GetTempPath(), $"genpres-%s{Guid.NewGuid().ToString()}", "x.db"),
                        Pooling = false
                    )
                        .ToString()

                runWrites missing [ Session.RecordLaunch(launchOf "n-1") ]
                |> function
                    | Session.StoreOutcome.Failed reason -> reason |> Expect.isNotEmpty "the reason it failed"
                    | other -> failtest $"expected Failed, got %A{other}"
            }

            test "the patient a Session shows is written as its Dto under the version it is written with" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                        ]
                    |> ignore

                    use conn = connect cs

                    use cmd =
                        SqlSessions.command conn null "select json_version, patient from session_opened_with" []

                    use r = cmd.ExecuteReader()
                    r.Read() |> Expect.isTrue "the row is there"

                    r.GetInt32 0
                    |> Expect.equal "the version it was written under" SqlSessions.patientJsonWritten

                    r.GetString 1 |> Expect.equal "the canonical form of its Dto" patientJson
                )
            }
        ]


/// A state as a server holds it before a request: nothing loaded, the demo credentials seeded.
let emptyState = Session.initialState Map.empty


let warnings () =
    let said = ResizeArray<string>()
    said, (fun (w: string) -> said.Add w)


[<Tests>]
let sliceTests =
    testList
        "the rows a request can touch"
        [
            test "a Session and the record of the patient it was opened on" {
                withSessions (fun cs ->
                    let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0

                    // the Session opened on the version that was signed, which the rows name
                    // by its id alone
                    let session =
                        sessionOf "s-1" "prescriber" (Some v1.Id) (Some(StoredVersion.Readable v1))

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                            Session.WriteVersion v1
                        ]
                    |> ignore

                    use conn = connect cs
                    let _, warn = warnings ()
                    let loaded = SqlSessions.withSession warn cs conn t0 "s-1" emptyState

                    loaded.Sessions
                    |> Map.containsKey "s-1"
                    |> Expect.isTrue "the Session is in the state"

                    loaded.Records
                    |> Map.tryFind v1.PatientId
                    |> Option.map List.length
                    |> Expect.equal "with the record of its patient" (Some 1)

                    // the record is read before the Session, so the version it opened with is
                    // among the rows by the time the Session names it
                    loaded.Sessions
                    |> Map.tryFind "s-1"
                    |> Option.bind _.Opened.Head
                    |> Option.map StoredVersion.id
                    |> Expect.equal "and the head it opened on" (Some v1.Id)
                )
            }

            test "the login's Sessions are replaced, not added to" {
                withSessions (fun cs ->
                    let older = sessionOf "s-1" "prescriber" None None
                    let newer = sessionOf "s-2" "prescriber" None None

                    for sid, session in [ "s-1", older; "s-2", newer ] do
                        runWrites
                            cs
                            [
                                Session.OpenSession(sid, session)
                                Session.RecordOpenedWith(sid, session, t0)
                            ]
                        |> ignore

                    // the server still holds the Session it opened first
                    let held = { emptyState with Sessions = emptyState.Sessions |> Map.add "s-1" older }

                    use conn = connect cs
                    let _, warn = warnings ()
                    let loaded = SqlSessions.withLogin warn cs conn t0 "prescriber" held

                    loaded.Sessions |> Map.containsKey "s-2" |> Expect.isTrue "the newest stands"

                    loaded.Sessions
                    |> Map.containsKey "s-1"
                    |> Expect.isFalse "the one it superseded is gone, not findable by its own id"
                )
            }

            test "a Launch the rows no longer give is gone from the state as well" {
                withSessions (fun cs ->
                    let launch = launchOf "n-1"

                    runWrites cs [ Session.RecordLaunch launch ] |> ignore

                    let held =
                        { emptyState with Launches = emptyState.Launches |> Map.add "n-1" launch }

                    use conn = connect cs
                    let _, warn = warnings ()

                    // past its lifetime the loader calls it absent, and so must the state, or
                    // a callback would still find the patient it was launched on
                    SqlSessions.withLaunch warn cs conn (t0.AddMinutes 3.0) "state" "state-n-1" held
                    |> fun s -> s.Launches |> Expect.isEmpty "no Launch at all"

                    SqlSessions.withLaunch warn cs conn (t0.AddMinutes 3.0) "nonce" "n-1" held
                    |> fun s -> s.Launches |> Expect.isEmpty "found by its nonce either way"

                    // within it, the row is what the state holds
                    SqlSessions.withLaunch warn cs conn t0 "nonce" "n-1" held
                    |> fun s -> s.Launches |> Map.containsKey "n-1" |> Expect.isTrue "the Launch stands"
                )
            }

            test "the rows are what stands: a Session ended elsewhere does not survive in memory" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                        ]
                    |> ignore

                    // the state of the server that opened it, which still holds it
                    let held =
                        { emptyState with Sessions = emptyState.Sessions |> Map.add "s-1" session }

                    // another server ends it
                    runWrites
                        cs
                        [
                            Session.EndSession("s-1", Session.StoredEnding.Ended SessionEnding.WrongPinLimit, t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let _, warn = warnings ()
                    let loaded = SqlSessions.withSession warn cs conn t0 "s-1" held

                    loaded.Sessions
                    |> Map.containsKey "s-1"
                    |> Expect.isFalse "the Session it held is gone"

                    loaded.Endings
                    |> Map.tryFind "s-1"
                    |> Option.map fst
                    |> Expect.equal "the ending the rows give" (Some SessionEnding.WrongPinLimit)
                )
            }

            test "a Session this release cannot read ends as unreadable, and says why" {
                withSessions (fun cs ->
                    insertSession cs "s-1" (Some "prescriber") "prescriber"
                    insertOpenedWith cs "s-1" None 9 (Some patientJson)

                    use conn = connect cs
                    let said, warn = warnings ()
                    let loaded = SqlSessions.withSession warn cs conn t0 "s-1" emptyState

                    loaded.Endings
                    |> Map.tryFind "s-1"
                    |> Option.map fst
                    |> Expect.equal "ended as unreadable" (Some SessionEnding.Unreadable)

                    said
                    |> Seq.exists (fun w -> w.Contains "newer than this release knows")
                    |> Expect.isTrue $"the reason was said: %A{List.ofSeq said}"
                )
            }

            test "the newest Session of a login, whatever became of it" {
                withSessions (fun cs ->
                    for sid in [ "s-1"; "s-2" ] do
                        let session = sessionOf sid "prescriber" None None

                        runWrites
                            cs
                            [
                                Session.OpenSession(sid, session)
                                Session.RecordOpenedWith(sid, session, t0)
                            ]
                        |> ignore

                    use conn = connect cs

                    SqlSessions.newestOfLogin conn "prescriber"
                    |> Expect.equal "the newer one" (Some "s-2")

                    SqlSessions.newestOfLogin conn "nobody"
                    |> Expect.isNone "a login with no Session"

                    let _, warn = warnings ()
                    let loaded = SqlSessions.withLogin warn cs conn t0 "prescriber" emptyState

                    loaded.Sessions
                    |> Map.containsKey "s-2"
                    |> Expect.isTrue "the newest is in the state"

                    loaded.Sessions
                    |> Map.containsKey "s-1"
                    |> Expect.isFalse "the one it superseded is not"
                )
            }

            test "a Launch, what its callback came to, and the Session it opened" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-1")
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Opened("s-1", session.Opened), t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let _, warn = warnings ()
                    let loaded = SqlSessions.withLaunch warn cs conn t0 "state" "state-n-1" emptyState

                    match loaded.Launches |> Map.tryFind "n-1" with
                    | Some record ->
                        record.State |> Expect.equal "the state its callback carries" "state-n-1"

                        match record.Outcome with
                        | Some(LaunchResult.Opened(sid, opened)) ->
                            sid |> Expect.equal "the Session it opened" "s-1"

                            opened.User
                            |> Expect.equal "with what that Session was opened with" session.Opened.User
                        | other -> failtest $"expected the open it came to, got %A{other}"
                    | None -> failtest "expected the Launch"

                    loaded.Sessions
                    |> Map.containsKey "s-1"
                    |> Expect.isTrue "and the Session itself"
                )
            }

            test "a Launch that opened a Session answers with it once that Session has ended" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-1")
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Opened("s-1", session.Opened), t0)
                            Session.EndSession("s-1", Session.StoredEnding.Ended SessionEnding.WrongPinLimit, t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let _, warn = warnings ()
                    let loaded = SqlSessions.withLaunch warn cs conn t0 "nonce" "n-1" emptyState

                    // the browser presenting it again goes to the app, not through the hop
                    match loaded.Launches |> Map.tryFind "n-1" |> Option.bind _.Outcome with
                    | Some(LaunchResult.Opened(sid, _)) -> sid |> Expect.equal "the Session it opened" "s-1"
                    | other -> failtest $"expected the open it came to, got %A{other}"

                    loaded.Endings
                    |> Map.tryFind "s-1"
                    |> Option.map fst
                    |> Expect.equal "and the Session itself has ended" (Some SessionEnding.WrongPinLimit)
                )
            }

            test "a refused launch, an enrolling one, and one past its lifetime" {
                withSessions (fun cs ->
                    runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-1")
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Refused LaunchRefusal.NoRole, t0)
                            Session.RecordLaunch(launchOf "n-2")
                            Session.RecordLaunchOutcome("n-2", LaunchResult.Enrolling "attempt-1", t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let _, warn = warnings ()

                    let outcomeOf nonce state =
                        SqlSessions.withLaunch warn cs conn t0 "nonce" nonce state
                        |> fun s -> s.Launches |> Map.tryFind nonce |> Option.bind _.Outcome

                    outcomeOf "n-1" emptyState
                    |> Expect.equal "the refusal it came to" (Some(LaunchResult.Refused LaunchRefusal.NoRole))

                    outcomeOf "n-2" emptyState
                    |> Expect.equal "the attempt it suspended into" (Some(LaunchResult.Enrolling "attempt-1"))

                    // past the lifetime the record is absent, as the dropped row will be
                    SqlSessions.withLaunch warn cs conn (t0.AddMinutes 3.0) "nonce" "n-1" emptyState
                    |> fun s -> s.Launches |> Expect.isEmpty "no Launch at all"
                )
            }
        ]


[<Tests>]
let raceTests =
    testList
        "the rows decide the races"
        [
            test "two opens of one login that met the same predecessor: the newer stands" {
                withSessions (fun cs ->
                    // both servers load before either writes, so neither sees the other's open
                    use conn = connect cs
                    let _, warn = warnings ()
                    let a = SqlSessions.withLogin warn cs conn t0 "prescriber" emptyState
                    let b = SqlSessions.withLogin warn cs conn t0 "prescriber" emptyState

                    a.Sessions |> Expect.isEmpty "nothing for the login yet"
                    b.Sessions |> Expect.isEmpty "for either of them"

                    for sid in [ "s-a"; "s-b" ] do
                        let session = sessionOf sid "prescriber" None None

                        runWrites
                            cs
                            [
                                Session.OpenSession(sid, session)
                                Session.RecordOpenedWith(sid, session, t0)
                            ]
                        |> Expect.equal "both opens land" Session.StoreOutcome.Written

                    // the ordering of the rows decides it, not a lock either server held
                    match loadedSession cs "s-a" with
                    | Some(Choice2Of2(Some(SessionEnding.SupersededByLaunch, _))) -> ()
                    | other -> failtest $"expected the older Session superseded, got %A{other}"

                    match loadedSession cs "s-b" with
                    | Some(Choice1Of2(Ok _)) -> ()
                    | other -> failtest $"expected the newer Session open, got %A{other}"

                    // and the login has exactly one Session a request can find
                    SqlSessions.newestOfLogin conn "prescriber"
                    |> Expect.equal "the newer one" (Some "s-b")
                )
            }
        ]


let credentialOf pin =
    Credential.withPin (fun n -> Array.init n byte) pin


let pendingOf userId mac : Session.PendingCode =
    {
        UserId = userId
        MailAddress = "n***@stub.example"
        CodeMac = mac
        Expiry = t0.AddMinutes 15.0
        Tries = 0
    }


let enrolmentOf attempt userId : Session.Enrolment =
    {
        Attempt = attempt
        UserId = userId
        Login = userId
        DisplayName = "Stub Prescriber"
        PatientId = "stub-patient"
        PublicKey = PublicKey "key-a"
    }


/// The mac of the live code of a person: the newest row that is not spent.
module SqlCredentialsLoad =

    let liveMac (cs: string) (userId: string) =
        use conn = connect cs

        use cmd =
            SqlSessions.command
                conn
                null
                """
                select c.code_mac from confirmation_code c
                where c.user_id = $u and not exists (select 1 from code_spent s where s.code_id = c.id)
                order by c.id desc limit 1
                """
                [ ("$u", box userId) ]

        match cmd.ExecuteScalar() with
        | null -> None
        | value -> Some(unbox<byte[]> value)


let count (cs: string) (sql: string) =
    use conn = connect cs
    use cmd = SqlSessions.command conn null sql []
    cmd.ExecuteScalar() |> unbox<int64>


[<Tests>]
let credentialWriteTests =
    testList
        "the credential, code and enrolment rows a request writes"
        [
            test "the writes of a launch that suspends, and of the PIN that follows" {
                withSessions (fun cs ->
                    let mac = [| 1uy; 2uy; 3uy |]

                    runWrites
                        cs
                        [
                            Session.WriteCode(pendingOf "no-pin" mac, t0)
                            Session.WriteEnrolment(enrolmentOf "a-1" "no-pin", t0)
                        ]
                    |> Expect.equal "the suspension lands" Session.StoreOutcome.Written

                    count cs "select count(*) from confirmation_code" |> Expect.equal "the code" 1L
                    count cs "select count(*) from enrolment" |> Expect.equal "the attempt" 1L

                    runWrites
                        cs
                        [
                            Session.CountCodeTry("no-pin", mac, t0)
                            Session.WriteCredential("no-pin", "pin-set", credentialOf "2468", t0)
                            Session.SpendCode("no-pin", mac, t0)
                            Session.DropEnrolmentsOf("no-pin", t0)
                        ]
                    |> Expect.equal "the PIN lands" Session.StoreOutcome.Written

                    count cs "select count(*) from code_try" |> Expect.equal "the try counted" 1L
                    count cs "select count(*) from code_spent" |> Expect.equal "the code spent" 1L

                    count cs "select count(*) from enrolment_dropped"
                    |> Expect.equal "the attempt dropped with it" 1L

                    count cs "select count(*) from credential_event"
                    |> Expect.equal "and the credential written" 1L
                )
            }

            test "a try names the code the request read, not whatever is newest" {
                withSessions (fun cs ->
                    let read = [| 1uy |]
                    let newer = [| 2uy |]

                    // one server reads a code; another mails a newer one before the first writes
                    runWrites cs [ Session.WriteCode(pendingOf "no-pin" read, t0) ] |> ignore

                    runWrites cs [ Session.WriteCode(pendingOf "no-pin" newer, t0) ] |> ignore

                    runWrites
                        cs
                        [
                            Session.CountCodeTry("no-pin", read, t0)
                            Session.SpendCode("no-pin", read, t0)
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    use conn = connect cs

                    use cmd =
                        SqlSessions.command
                            conn
                            null
                            "select c.code_mac from code_spent s join confirmation_code c on c.id = s.code_id"
                            []

                    cmd.ExecuteScalar()
                    |> unbox<byte[]>
                    |> Expect.equal "the code that was read is the one spent" read

                    // the newer code still stands: nobody has entered it yet
                    SqlCredentialsLoad.liveMac cs "no-pin"
                    |> Expect.equal "the newer code is still the live one" (Some newer)
                )
            }

            test "a code already spent takes no further try, and dropping twice is no error" {
                withSessions (fun cs ->
                    let mac = [| 9uy |]

                    runWrites cs [ Session.WriteCode(pendingOf "no-pin" mac, t0) ] |> ignore

                    runWrites cs [ Session.SpendCode("no-pin", mac, t0) ] |> ignore
                    runWrites cs [ Session.CountCodeTry("no-pin", mac, t0) ] |> ignore

                    count cs "select count(*) from code_try"
                    |> Expect.equal "nothing to count a try against" 0L

                    runWrites cs [ Session.WriteEnrolment(enrolmentOf "a-1" "no-pin", t0) ]
                    |> ignore

                    for _ in 1..2 do
                        runWrites cs [ Session.DropEnrolmentWrite("a-1", t0) ]
                        |> Expect.equal "written" Session.StoreOutcome.Written

                    count cs "select count(*) from enrolment_dropped"
                    |> Expect.equal "dropped once" 1L
                )
            }
        ]


[<Tests>]
let credentialLoadTests =
    testList
        "the credential, code and enrolment rows a request reads"
        [
            test "a credential is its newest event, and a person without one has none" {
                withSessions (fun cs ->
                    use conn = connect cs
                    SqlSessions.loadCredential conn "no-pin" |> Expect.isNone "never enrolled"

                    let withPin = credentialOf "1234"

                    runWrites
                        cs
                        [
                            Session.WriteCredential("prescriber", "pin-set", withPin, t0)
                            Session.WriteCredential(
                                "prescriber",
                                "wrong",
                                { withPin with WrongCount = 1 },
                                t0.AddMinutes 1.0
                            )
                        ]
                    |> ignore

                    match SqlSessions.loadCredential conn "prescriber" with
                    | Some c ->
                        c.WrongCount |> Expect.equal "the count of the newest event" 1
                        PinHash.verify "1234" c.PinHash.Value |> Expect.isTrue "the PIN it still holds"
                    | None -> failtest "expected the credential"
                )
            }

            test "the live code is the newest unspent one, with its tries counted" {
                withSessions (fun cs ->
                    let mac = [| 7uy |]
                    use conn = connect cs

                    runWrites
                        cs
                        [
                            Session.WriteCode(pendingOf "no-pin" mac, t0)
                            Session.CountCodeTry("no-pin", mac, t0)
                        ]
                    |> ignore

                    SqlSessions.loadCode conn t0 "no-pin"
                    |> Option.map _.Tries
                    |> Expect.equal "the wrong code counted" (Some 1)

                    SqlSessions.loadCode conn (t0.AddMinutes 16.0) "no-pin"
                    |> Expect.isNone "past its fifteen minutes it is no code"

                    runWrites cs [ Session.SpendCode("no-pin", mac, t0) ] |> ignore

                    SqlSessions.loadCode conn t0 "no-pin"
                    |> Expect.isNone "spent, and never an older one in its place"
                )
            }

            test "an attempt brings its person's other attempts, their code and their credential" {
                withSessions (fun cs ->
                    let mac = [| 8uy |]

                    runWrites
                        cs
                        [
                            Session.WriteCode(pendingOf "no-pin" mac, t0)
                            Session.WriteEnrolment(enrolmentOf "a-1" "no-pin", t0)
                            Session.WriteEnrolment(enrolmentOf "a-2" "no-pin", t0)
                            Session.WriteEnrolment(enrolmentOf "a-3" "someone-else", t0)
                            Session.WriteCredential("no-pin", "seeded", Credential.empty, t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let state = SqlSessions.withEnrolment conn t0 "a-1" emptyState

                    state.Enrolments
                    |> Map.toList
                    |> List.map fst
                    |> List.sort
                    |> Expect.equal "both attempts of that person, and no other's" [ "a-1"; "a-2" ]

                    state.Codes
                    |> Map.containsKey "no-pin"
                    |> Expect.isTrue "the code they are bound to"

                    state.Credentials
                    |> Map.containsKey "no-pin"
                    |> Expect.isTrue "and the credential the PIN will be set on"

                    // the attempt given up is not found again
                    runWrites cs [ Session.DropEnrolmentWrite("a-1", t0) ] |> ignore

                    SqlSessions.withEnrolment conn t0 "a-1" emptyState
                    |> fun s -> s.Enrolments |> Expect.isEmpty "nothing to continue"

                    // and a server that still held it does not continue it either: an attempt
                    // another server dropped is gone, whatever this one has in memory
                    let held =
                        { emptyState with Enrolments = Map.ofList [ "a-1", enrolmentOf "a-1" "no-pin" ] }

                    SqlSessions.withEnrolment conn t0 "a-1" held
                    |> fun s ->
                        s.Enrolments
                        |> Map.containsKey "a-1"
                        |> Expect.isFalse "the attempt it held is gone"

                    SqlSessions.loadEnrolments conn "user" "no-pin"
                    |> List.length
                    |> Expect.equal "the other attempt still stands" 1
                )
            }

            test "a Session brings the credential of the person it belongs to" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                            Session.WriteCredential("user-1", "seeded", credentialOf "1234", t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let _, warn = warnings ()
                    let state = SqlSessions.withSession warn cs conn t0 "s-1" emptyState

                    match state.Credentials |> Map.tryFind "user-1" with
                    | Some c -> PinHash.verify "1234" c.PinHash.Value |> Expect.isTrue "the PIN it signs with"
                    | None -> failtest "expected the credential of the signer"
                )
            }

            test "the seed writes a login once, and never over a PIN the User set" {
                withSessions (fun cs ->
                    let seeded = StubCredentials.seed (fun n -> Array.init n byte)

                    SqlSessions.seed cs t0 seeded
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    let rows () =
                        count cs "select count(*) from credential_event"

                    let first = rows ()
                    first |> Expect.equal "one row per seeded login" (int64 seeded.Count)

                    // the User changes their PIN, and the server is started again
                    runWrites
                        cs
                        [
                            Session.WriteCredential("prescriber", "pin-set", credentialOf "9999", t0)
                        ]
                    |> ignore

                    SqlSessions.seed cs t0 seeded |> ignore
                    rows () |> Expect.equal "the second start adds nothing" (first + 1L)

                    // the PIN was set while this start was seeding: the seed asks and writes in
                    // one statement, so the demo PIN cannot land after it and become the newest
                    runWrites
                        cs
                        [
                            Session.WriteCredential("no-pin", "pin-set", credentialOf "5555", t0)
                        ]
                    |> ignore

                    SqlSessions.seed cs t0 seeded |> ignore

                    use conn = connect cs

                    match SqlSessions.loadCredential conn "no-pin" with
                    | Some c ->
                        PinHash.verify "5555" c.PinHash.Value
                        |> Expect.isTrue "the PIN they enrolled with"
                    | None -> failtest "expected the credential"

                    match SqlSessions.loadCredential conn "prescriber" with
                    | Some c ->
                        PinHash.verify "9999" c.PinHash.Value |> Expect.isTrue "the PIN they set"
                        PinHash.verify "1234" c.PinHash.Value |> Expect.isFalse "not the seeded one"
                    | None -> failtest "expected the credential"
                )
            }
        ]


/// A Session for the rows that hang off one.
let withOpenSession (cs: string) =
    let session = sessionOf "s-1" "prescriber" None None
    runWrites cs [ Session.OpenSession("s-1", session) ] |> ignore


let noticeOf nonce data : Session.Notice =
    {
        Nonce = nonce
        Data = data
        Expiry = t0.AddMinutes 2.0
    }


let challengeOf nonce : Session.Challenge =
    {
        Nonce = nonce
        Digest = "digest-1"
        Reading = None
        Expiry = t0.AddMinutes 2.0
    }


let scalarOf (cs: string) (sql: string) =
    use conn = connect cs
    use cmd = SqlSessions.command conn null sql []
    cmd.ExecuteScalar()


[<Tests>]
let flightWriteTests =
    testList
        "the rows of what a Session holds in flight"
        [
            test "a notice and a challenge are written with what they carry" {
                withSessions (fun cs ->
                    withOpenSession cs

                    runWrites
                        cs
                        [
                            Session.WriteNotice("s-1", noticeOf "n-1" (Some patient), t0)
                            Session.WriteChallenge("s-1", challengeOf "c-1", t0)
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    scalarOf cs "select data from data_notice"
                    |> unbox<string>
                    |> Expect.equal "the patient it showed, as its Dto" patientJson

                    scalarOf cs "select json_version from data_notice"
                    |> unbox<int64>
                    |> Expect.equal "under the version it was written with" (int64 SqlSessions.patientJsonWritten)

                    scalarOf cs "select digest from challenge"
                    |> unbox<string>
                    |> Expect.equal "the digest it was issued over" "digest-1"

                    // a reading that could not be taken is no reading, and no structure version
                    scalarOf cs "select count(*) from challenge where reading is null and json_version is null"
                    |> unbox<int64>
                    |> Expect.equal "the challenge carries neither" 1L
                )
            }

            test "a challenge is spent by the nonce the request read" {
                withSessions (fun cs ->
                    withOpenSession cs

                    runWrites
                        cs
                        [
                            Session.WriteChallenge("s-1", challengeOf "c-1", t0)
                            Session.WriteChallenge("s-1", challengeOf "c-2", t0)
                        ]
                    |> ignore

                    runWrites cs [ Session.SpendChallenge("s-1", "c-1", t0) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    scalarOf cs "select c.nonce from challenge_spent s join challenge c on c.id = s.challenge_id"
                    |> unbox<string>
                    |> Expect.equal "the one the request answered, not the newer" "c-1"

                    // spending it again, and spending one that is not there, are no error
                    for nonce in [ "c-1"; "nothing" ] do
                        runWrites cs [ Session.SpendChallenge("s-1", nonce, t0) ]
                        |> Expect.equal "written" Session.StoreOutcome.Written

                    count cs "select count(*) from challenge_spent" |> Expect.equal "spent once" 1L
                )
            }

            test "an answer names the version it signed and is not written over" {
                withSessions (fun cs ->
                    withOpenSession cs
                    let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0
                    runWrites cs [ Session.WriteVersion v1 ] |> ignore

                    runWrites
                        cs
                        [
                            Session.RememberAnswer(
                                "s-1",
                                "k-1",
                                SigningOutcome.Submitted(v1, OpenedToken "opened-again"),
                                t0
                            )
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    scalarOf cs "select version_id from submission_answer"
                    |> unbox<string>
                    |> Expect.equal "the version it signed" v1.Id

                    scalarOf cs "select opened_token from submission_answer"
                    |> unbox<string>
                    |> Expect.equal "and the token minted with it" "opened-again"

                    // the same key again: the answer that was given stands
                    runWrites
                        cs
                        [
                            Session.RememberAnswer("s-1", "k-1", SigningOutcome.Refused SigningRefusal.StaleToken, t0)
                        ]
                    |> ignore

                    scalarOf cs "select answer from submission_answer"
                    |> unbox<string>
                    |> Expect.equal "the first answer, not the second" "submitted"

                    count cs "select count(*) from submission_answer" |> Expect.equal "one row" 1L
                )
            }

            test "every refusal reaches a column, by a word of its own and with what it carries" {
                withSessions (fun cs ->
                    withOpenSession cs
                    let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0
                    runWrites cs [ Session.WriteVersion v1 ] |> ignore
                    let until = t0.AddMinutes 1.0

                    let refusals =
                        [
                            SigningRefusal.NoSession
                            SigningRefusal.NoPatient
                            SigningRefusal.NotPrescriber
                            SigningRefusal.Blocked(StoredVersion.head (StoredVersion.Readable v1))
                            SigningRefusal.StaleToken
                            SigningRefusal.ChallengeMismatch
                            SigningRefusal.ChallengeExpired
                            SigningRefusal.PinWrong 2
                            SigningRefusal.PinLimit
                            SigningRefusal.Locked until
                            SigningRefusal.StoreFailed
                            SigningRefusal.PlanUnreadable
                        ]

                    for i, refusal in refusals |> List.indexed do
                        runWrites
                            cs
                            [
                                Session.RememberAnswer("s-1", $"k-%i{i}", SigningOutcome.Refused refusal, t0)
                            ]
                        |> Expect.equal $"%A{refusal} written" Session.StoreOutcome.Written

                    scalarOf cs "select count(distinct answer) from submission_answer"
                    |> unbox<int64>
                    |> Expect.equal "a word of its own for every refusal" (int64 refusals.Length)

                    // the three that carry something keep it beside the word
                    scalarOf cs "select version_id from submission_answer where answer = 'refused:blocked'"
                    |> unbox<string>
                    |> Expect.equal "the head that blocked" v1.Id

                    scalarOf cs "select attempts_left from submission_answer where answer = 'refused:pin-wrong'"
                    |> unbox<int64>
                    |> Expect.equal "the tries that remain" 2L

                    scalarOf cs "select locked_until from submission_answer where answer = 'refused:locked'"
                    |> unbox<int64>
                    |> Expect.equal "until when it is locked" (SqlSessions.ms until)
                )
            }
        ]


[<Tests>]
let newestOnlyTests =
    testList
        "the newest row is the only candidate"
        [
            test "a challenge another replaced does not come back when the newer one is spent" {
                withSessions (fun cs ->
                    withOpenSession cs

                    runWrites
                        cs
                        [
                            Session.WriteChallenge("s-1", challengeOf "c-1", t0)
                            Session.WriteChallenge("s-1", challengeOf "c-2", t0)
                        ]
                    |> ignore

                    use conn = connect cs

                    match SqlSessions.loadChallenge conn t0 "s-1" with
                    | Some(Ok c) -> c.Nonce |> Expect.equal "the newer one" "c-2"
                    | other -> failtest $"expected the newer challenge, got %A{other}"

                    // the data changed, so the newer challenge is spent: the Session has no
                    // challenge at all, and not the one that was replaced before it
                    runWrites cs [ Session.SpendChallenge("s-1", "c-2", t0) ] |> ignore

                    SqlSessions.loadChallenge conn t0 "s-1"
                    |> Expect.isNone "no challenge, and never the replaced one"
                )
            }

            test "a code another replaced does not come back when the newer one is spent" {
                withSessions (fun cs ->
                    let first = [| 1uy |]
                    let second = [| 2uy |]

                    runWrites
                        cs
                        [
                            Session.WriteCode(pendingOf "no-pin" first, t0)
                            Session.WriteCode(pendingOf "no-pin" second, t0)
                        ]
                    |> ignore

                    use conn = connect cs

                    SqlSessions.loadCode conn t0 "no-pin"
                    |> Option.map _.CodeMac
                    |> Expect.equal "the code that was mailed last" (Some second)

                    runWrites cs [ Session.SpendCode("no-pin", second, t0) ] |> ignore

                    SqlSessions.loadCode conn t0 "no-pin"
                    |> Expect.isNone "no code, and never the one it replaced"
                )
            }

            test "the newest challenge past its lifetime is no challenge, not an older one" {
                withSessions (fun cs ->
                    withOpenSession cs

                    runWrites
                        cs
                        [
                            Session.WriteChallenge("s-1", challengeOf "c-1", t0)
                            Session.WriteChallenge("s-1", challengeOf "c-2", t0)
                        ]
                    |> ignore

                    use conn = connect cs

                    SqlSessions.loadChallenge conn (t0.AddMinutes 3.0) "s-1"
                    |> Expect.isNone "both are past it, and the older is no fallback"
                )
            }
        ]


/// What the audit rows of a database say, oldest first.
let auditRows (cs: string) =
    use conn = connect cs

    SqlSessions.rows
        conn
        "select action, outcome, session_id, actor, at from audit_entry order by id"
        []
        (fun r -> r.GetString 0, r.GetString 1, SqlSessions.textOrNull r 2, SqlSessions.textOrNull r 3, r.GetInt64 4)


let actions (cs: string) =
    auditRows cs |> List.map (fun (action, _, _, _, _) -> action)


/// The audit of one request, as the store derives it.
let audited writes =
    SqlSessions.auditOf t0 writes |> List.map (fun e -> e.Action, e.Outcome)


[<Tests>]
let auditTests =
    testList
        "the audit of what a request did"
        [
            test "an open is audited once, by the act and not by its facts nor by the launch" {
                let session = sessionOf "s-1" "prescriber" None None

                let entries =
                    SqlSessions.auditOf
                        t0
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Opened("s-1", session.Opened), t0)
                        ]

                entries
                |> List.map (fun e -> e.Action, e.SessionId, e.Actor)
                |> Expect.equal
                    "the open alone, by whom and to which Session"
                    [ "session-opened", Some "s-1", Some "user-1" ]
            }

            test "a launch is timed by the request, not by the Launch's expiry" {
                let launch = launchOf "n-1"

                SqlSessions.auditOf t0 [ Session.RecordLaunch launch ]
                |> List.map (fun e -> e.Action, e.At)
                |> Expect.equal "the time the launch was made" [ "launched", t0 ]

                launch.Expiry |> Expect.notEqual "which is not when it runs out" t0
            }

            test "what a launch came to is audited, refused or suspended into enrolment" {
                SqlSessions.auditOf
                    t0
                    [
                        Session.RecordLaunchOutcome("n-1", LaunchResult.Refused LaunchRefusal.WrongActivePatient, t0)
                    ]
                |> List.map (fun e -> e.Action, e.Outcome, e.Detail)
                |> Expect.equal
                    "the refusal, by the word the store holds it under"
                    [
                        "launch", "refused", Some """{"refusal":"wrong-patient"}"""
                    ]

                audited
                    [
                        Session.RecordLaunchOutcome("n-1", LaunchResult.Enrolling "a-1", t0)
                    ]
                |> Expect.equal "the launch suspended into enrolment" [ "enrolling", "ok" ]
            }

            test "a signature is audited by the version it wrote, in the Session that made it" {
                let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0
                let session = sessionOf "s-1" "prescriber" None None

                SqlSessions.auditOf
                    t0
                    [
                        Session.WriteVersion v1
                        Session.RecordOpenedWith("s-1", session, t0)
                        Session.SpendChallenge("s-1", "c-1", t0)
                        Session.RememberAnswer("s-1", "k-1", SigningOutcome.Submitted(v1, OpenedToken "t"), t0)
                    ]
                |> List.map (fun e -> e.Action, e.SessionId, e.Actor)
                |> Expect.equal
                    "the signature, once, naming the Session the rest of the request named"
                    [ "signed", Some "s-1", Some Store.prescriber.UserId ]
            }

            test "a refused signature is audited by itself, by the word of the refusal" {
                SqlSessions.auditOf
                    t0
                    [
                        Session.RememberAnswer("s-1", "k-1", SigningOutcome.Refused SigningRefusal.PinLimit, t0)
                    ]
                |> List.map (fun e -> e.Action, e.Outcome, e.Detail)
                |> Expect.equal "the refusal" [ "sign", "refused", Some """{"refusal":"pin-limit"}""" ]
            }

            test "a PIN set, a code mailed and a code entered wrongly each say who" {
                SqlSessions.auditOf
                    t0
                    [
                        Session.WriteCredential("no-pin", "pin-set", credentialOf "1234", t0)
                        Session.WriteCode(pendingOf "no-pin" [| 1uy |], t0)
                        Session.CountCodeTry("no-pin", [| 1uy |], t0)
                    ]
                |> List.map (fun e -> e.Action, e.Outcome, e.Actor)
                |> Expect.equal
                    "each act, by the person it was for"
                    [
                        "credential-pin-set", "ok", Some "no-pin"
                        "code-mailed", "ok", Some "no-pin"
                        "code-entered", "refused", Some "no-pin"
                    ]
            }

            test "the end of a Session is audited with the ending it was" {
                SqlSessions.auditOf
                    t0
                    [
                        Session.EndSession("s-1", Session.StoredEnding.Ended SessionEnding.WrongPinLimit, t0)
                        Session.AcknowledgeEnding("s-1", t0)
                    ]
                |> List.map (fun e -> e.Action, e.SessionId, e.Detail)
                |> Expect.equal
                    "the ending, once"
                    [
                        "session-ended", Some "s-1", Some """{"ending":"wrong-pin-limit"}"""
                    ]
            }

            test "a challenge issued and a notice told are acts of their own" {
                audited
                    [
                        Session.RecordSeen("s-1", t0)
                        Session.WriteChallenge("s-1", challengeOf "c-1", t0)
                    ]
                |> Expect.equal "the challenge" [ "challenge-issued", "ok" ]

                audited
                    [
                        Session.RecordSeen("s-1", t0)
                        Session.WriteNotice("s-1", noticeOf "c-1" None, t0)
                    ]
                |> Expect.equal "the notice" [ "notice-told", "ok" ]
            }

            test "a version opened is audited, though no write of it is its own" {
                let session = sessionOf "s-1" "prescriber" None None

                // what openVersion writes: the heartbeat, the challenge it used up, and what the
                // Session now opens with. An open and a signature write that too, and are
                // audited by what they write besides it
                SqlSessions.auditOf
                    t0
                    [
                        Session.RecordSeen("s-1", t0)
                        Session.SpendChallenge("s-1", "c-1", t0)
                        Session.RecordOpenedWith("s-1", session, t0)
                    ]
                |> List.map (fun e -> e.Action, e.SessionId, e.Actor)
                |> Expect.equal "the version opened" [ "version-opened", Some "s-1", Some "user-1" ]
            }

            test "a request that only says it was seen is no act, and no entry" {
                audited [ Session.RecordSeen("s-1", t0) ] |> Expect.isEmpty "nothing was done"
            }

            test "the entries land with the writes they describe" {
                withSessions (fun cs ->
                    runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-1")
                            Session.OpenSession("s-1", sessionOf "s-1" "prescriber" None None)
                        ]
                    |> Expect.equal "both wrote" Session.StoreOutcome.Written

                    actions cs
                    |> Expect.equal "one entry per act, in the order of the writes" [ "launched"; "session-opened" ]
                )
            }

            test "a request that fails leaves no entry behind" {
                withSessions (fun cs ->
                    // the Session the second write names was never opened, so its row is refused
                    // and the whole transaction rolls back, the audit with it
                    let outcome =
                        runWrites
                            cs
                            [
                                Session.RecordLaunch(launchOf "n-1")
                                Session.RecordSeen("s-gone", t0)
                            ]

                    (outcome = Session.StoreOutcome.Written) |> Expect.isFalse "the request failed"

                    auditRows cs |> Expect.isEmpty "nothing was done, so nothing is audited"
                )
            }
        ]
