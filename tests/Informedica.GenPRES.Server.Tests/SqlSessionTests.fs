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
        SqlSchema.apply cs |> Expect.equal "migrations 1 and 2" [ 1; 2 ]
        f cs
    )


let connect (cs: string) =
    let conn = new SqliteConnection(cs)
    conn.Open()
    conn


let t0 = DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc)


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

                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
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
                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
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
                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.WriteVersion v1
                        ]
                    |> Expect.equal "the first lands" Session.StoreOutcome.Written

                    let rival = { v1 with Id = "plan-rival" }

                    match
                        SqlSessions.runWrites
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

                    SqlSessions.runWrites readOnly [ Session.RecordLaunch(launchOf "n-1") ]
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

                SqlSessions.runWrites missing [ Session.RecordLaunch(launchOf "n-1") ]
                |> function
                    | Session.StoreOutcome.Failed reason -> reason |> Expect.isNotEmpty "the reason it failed"
                    | other -> failtest $"expected Failed, got %A{other}"
            }

            test "the patient a Session shows is written as its Dto under the version it is written with" {
                withSessions (fun cs ->
                    let session = sessionOf "s-1" "prescriber" None None

                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
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
                        SqlSessions.runWrites
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

                    SqlSessions.runWrites cs [ Session.RecordLaunch launch ] |> ignore

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

                    SqlSessions.runWrites
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
                    SqlSessions.runWrites
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

                        SqlSessions.runWrites
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

                    SqlSessions.runWrites
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

                    SqlSessions.runWrites
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
                    SqlSessions.runWrites
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

                        SqlSessions.runWrites
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
