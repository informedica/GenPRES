/// The migration runner of the session store's SQLite database: the embedded scripts applied in
/// order, once each, each with its `schema_version` row, and the `order_plan` table's unique
/// constraint as the adapter will meet it.
module Informedica.GenPRES.Server.Tests.SqlSchemaTests

open System
open System.IO
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open ServerApi


/// Runs `f` on the connection string of a fresh temporary database file, without pooling so
/// that the file can be deleted on every OS, and deletes the file afterwards.
let withDb (f: string -> 'a) =
    let path =
        Path.Combine(Path.GetTempPath(), $"genpres-%s{Guid.NewGuid().ToString()}.db")

    let cs =
        SqliteConnectionStringBuilder(DataSource = path, Pooling = false).ToString()

    try
        f cs
    finally
        File.Delete path


let scalar (cs: string) (sql: string) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- sql
    cmd.ExecuteScalar()


let insertVersion (cs: string) (versionId: string) (no: int) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()

    cmd.CommandText <-
        """
        insert into order_plan
            (version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
             signed_at, verified, json_version, plan)
        values ($id, $no, 'patient-1', null, 'user-1', 'Prescriber', 0, 1, 1, '{}')
        """

    cmd.Parameters.AddWithValue("$id", versionId) |> ignore
    cmd.Parameters.AddWithValue("$no", no) |> ignore
    cmd.ExecuteNonQuery() |> ignore


[<Tests>]
let tests =
    testList
        "SqlSchema"
        [
            test "a fresh file gets the embedded migrations, in order, and their tables" {
                withDb (fun cs ->
                    SqlSchema.apply cs |> Expect.equal "migrations 1, 2 and 3" [ 1; 2; 3 ]

                    scalar cs "select group_concat(migration) from schema_version"
                    |> string
                    |> Expect.equal "schema_version records them" "1,2,3"

                    // named one by one, so that a migration missing from the assembly's
                    // resources fails here instead of at the first request that needs its rows
                    let tables =
                        scalar
                            cs
                            "select group_concat(name) from (select name from sqlite_master where type = 'table' order by name)"
                        |> string
                        |> fun names -> names.Split ','

                    for table in
                        [
                            "order_plan"
                            "launch_record"
                            "launch_outcome"
                            "session"
                            "session_opened_with"
                            "session_seen"
                            "session_ending"
                            "session_acknowledged"
                            "credential_event"
                            "confirmation_code"
                            "code_try"
                            "code_spent"
                            "enrolment"
                            "enrolment_dropped"
                        ] do
                        tables |> Expect.contains $"the table %s{table}" table
                )
            }

            test "a second apply applies nothing" {
                withDb (fun cs ->
                    let applied = SqlSchema.apply cs |> List.length

                    SqlSchema.apply cs |> Expect.isEmpty "nothing above the highest number"

                    scalar cs "select count(*) from schema_version"
                    |> unbox<int64>
                    |> Expect.equal "the same rows" (int64 applied)
                )
            }

            test "a second order plan version with the same number is a unique violation" {
                withDb (fun cs ->
                    SqlSchema.apply cs |> ignore
                    insertVersion cs "plan-1" 1

                    try
                        insertVersion cs "plan-2" 1
                        failtest "the insert should be refused"
                    with :? SqliteException as e ->
                        e.SqliteErrorCode |> Expect.equal "SQLITE_CONSTRAINT" 19
                        e.SqliteExtendedErrorCode |> Expect.equal "SQLITE_CONSTRAINT_UNIQUE" 2067

                        e.Message.Contains "order_plan.patient_id, order_plan.no"
                        |> Expect.isTrue "the message names the two columns"
                )
            }

            test "a failing migration rolls back and records nothing" {
                withDb (fun cs ->
                    let broken =
                        SqlSchema.ofScripts
                            [
                                ("Sql/001-broken.sql", "create table t (a integer); create table t (a integer);")
                            ]

                    Expect.throwsT<SqliteException>
                        "the script fails"
                        (fun () -> SqlSchema.applyAll cs broken |> ignore)

                    scalar cs "select count(*) from sqlite_master where type = 'table' and name = 't'"
                    |> unbox<int64>
                    |> Expect.equal "the first statement, which ran, is rolled back" 0L

                    scalar cs "select count(*) from schema_version"
                    |> unbox<int64>
                    |> Expect.equal "nothing recorded" 0L
                )
            }

            test "the file is deleted after" {
                let mutable kept = ""
                withDb (fun cs -> kept <- SqliteConnectionStringBuilder(cs).DataSource)

                File.Exists kept |> Expect.isFalse "withDb deletes the file"
            }

            test "migrations apply by name, ordinal, and the numbers must rise" {
                SqlSchema.ofScripts [ ("Sql/010-b.sql", ""); ("Sql/002-a.sql", "") ]
                |> List.map _.Number
                |> Expect.equal "zero-padded names sort as numbers" [ 2; 10 ]

                Expect.throwsT<InvalidOperationException>
                    "a name without a number"
                    (fun () -> SqlSchema.ofScripts [ ("Sql/order-plan.sql", "") ] |> ignore)

                Expect.throwsT<InvalidOperationException>
                    "two scripts with one number"
                    (fun () -> SqlSchema.ofScripts [ ("Sql/001-a.sql", ""); ("Sql/001-b.sql", "") ] |> ignore)
            }
        ]
