/// The launch and session rows of the session store's SQLite database: what the loader reads
/// back from them, and the words the columns hold a refusal and a Role under.
module Informedica.GenPRES.Server.Tests.SqlSessionTests

open System
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
