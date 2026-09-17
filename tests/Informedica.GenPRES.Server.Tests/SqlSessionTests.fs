/// The launch and session rows of the session store's SQLite database: what the loader reads
/// back from them, and the words the columns hold a refusal and a Role under.
module Informedica.GenPRES.Server.Tests.SqlSessionTests

open System
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Shared.Types
open ServerApi
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

            test "every Role is stored by its word and reads back as itself" {
                for role in [ UserRole.Prescriber; UserRole.Reader ] do
                    role
                    |> SqlSessions.roleWord
                    |> SqlSessions.roleOf
                    |> Expect.equal $"the Role of %A{role}" role
            }

            test "a time reaches the column as Unix milliseconds and back" {
                t0
                |> SqlSessions.ms
                |> SqlSessions.at
                |> Expect.equal "the same instant, in UTC" t0
            }
        ]
