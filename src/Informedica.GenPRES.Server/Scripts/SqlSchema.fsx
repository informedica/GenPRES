// The session store on SQLite (plan 516), step 2: the migration runner and migration 1.
//
// Script-first draft (script-only policy) of the module `SqlSchema` of the new source file
// `ServerApi.SqlAdapters.fs`, and of the tests of `SqlSchemaTests.fs`. In the source the
// migrations are the `Sql/*.sql` files embedded in the server assembly, read by `embedded`;
// here they are read from the `Sql` folder next to this script by `fromFolder`, under the
// same names, so that `ofScripts` and `applyAll` run unchanged. The runner uses nothing of the
// server, so this script does not load `load.fsx`. Run: `dotnet fsi SqlSchema.fsx` from this
// directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Microsoft.Data.Sqlite, 10.0.12"


// ---------------------------------------------------------------------------------------------
// The runner (→ ServerApi.SqlAdapters.fs, module SqlSchema)
// ---------------------------------------------------------------------------------------------

/// <summary>
/// The SQL schema of the session store, applied as numbered migrations. Each migration is a
/// script `Sql/NNN-name.sql`; its number is the leading integer of the file name. The runner
/// records every migration it applied in `schema_version` and applies each script above the
/// highest recorded number, each in a transaction of its own with its `schema_version` row.
/// </summary>
module SqlSchema =

    open System
    open System.IO
    open System.Reflection
    open Microsoft.Data.Sqlite


    /// A migration: its number, the name it is known by, and its SQL.
    type Migration =
        {
            Number: int
            Name: string
            Sql: string
        }


    /// The folder, in resource names and in the source tree, that holds the migrations.
    let prefix = "Sql/"


    /// The number of a migration from its name: the leading integer of the file name.
    let numberOf (name: string) =
        let file = Path.GetFileName name
        let digits = file |> Seq.takeWhile Char.IsDigit |> Seq.toArray |> String

        match Int32.TryParse digits with
        | true, n when n > 0 -> Ok n
        | _ -> Error $"the migration %s{name} does not start with a positive number"


    /// <summary>
    /// The migrations of named scripts, in the order they apply: by name, ordinal, so that
    /// zero-padded numbers sort as numbers. Every name must start with a positive number, and
    /// the numbers must rise strictly; a script that breaks either is a build defect, raised.
    /// </summary>
    let ofScripts (scripts: (string * string) list) =
        let migrations =
            scripts
            |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
            |> List.map (fun (name, sql) ->
                match numberOf name with
                | Ok n ->
                    {
                        Number = n
                        Name = name
                        Sql = sql
                    }
                | Error e -> invalidOp e
            )

        migrations
        |> List.pairwise
        |> List.iter (fun (a, b) ->
            if b.Number <= a.Number then
                invalidOp $"the migrations %s{a.Name} and %s{b.Name} do not rise in number"
        )

        migrations


    /// The migrations embedded in an assembly under `Sql/`.
    let embedded (assembly: Assembly) =
        assembly.GetManifestResourceNames()
        |> Array.filter (fun n -> n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith ".sql")
        |> Array.toList
        |> List.map (fun name ->
            use stream = assembly.GetManifestResourceStream name
            use reader = new StreamReader(stream)
            name, reader.ReadToEnd()
        )
        |> ofScripts


    let private execute (conn: SqliteConnection) (tx: SqliteTransaction) (sql: string) =
        use cmd = conn.CreateCommand()
        cmd.Transaction <- tx
        cmd.CommandText <- sql
        cmd.ExecuteNonQuery() |> ignore


    /// <summary>
    /// Applies to the database of the connection string every migration above the highest
    /// number `schema_version` records, and returns the numbers it applied, in order. Each
    /// migration runs in one transaction with its `schema_version` row, and the highest number
    /// is read inside that transaction, so a migration applied meanwhile by another process is
    /// skipped, not applied twice. A script that fails rolls its migration back and raises.
    /// </summary>
    let applyAll (connectionString: string) (migrations: Migration list) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()

        execute
            conn
            null
            "create table if not exists schema_version (migration integer primary key, applied_at integer not null)"

        [
            for m in migrations do
                use tx = conn.BeginTransaction()

                use current = conn.CreateCommand()
                current.Transaction <- tx
                current.CommandText <- "select coalesce(max(migration), 0) from schema_version"
                let highest = current.ExecuteScalar() :?> int64

                if int64 m.Number > highest then
                    execute conn tx m.Sql

                    use row = conn.CreateCommand()
                    row.Transaction <- tx
                    row.CommandText <- "insert into schema_version (migration, applied_at) values ($m, $at)"
                    row.Parameters.AddWithValue("$m", m.Number) |> ignore
                    row.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) |> ignore
                    row.ExecuteNonQuery() |> ignore

                    tx.Commit()
                    m.Number
        ]


    /// Applies the migrations embedded in the server assembly.
    let apply (connectionString: string) =
        Assembly.GetExecutingAssembly() |> embedded |> applyAll connectionString


// ---------------------------------------------------------------------------------------------
// The script's stand-in for the embedded resources
// ---------------------------------------------------------------------------------------------

/// The migrations in a folder, named as they are embedded: `Sql/<file>`.
let fromFolder (dir: string) =
    System.IO.Directory.GetFiles(dir, "*.sql")
    |> Array.toList
    |> List.map (fun path -> SqlSchema.prefix + System.IO.Path.GetFileName path, System.IO.File.ReadAllText path)
    |> SqlSchema.ofScripts


let migrations =
    fromFolder (System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "Sql"))


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlSchemaTests.fs)
// ---------------------------------------------------------------------------------------------

open System
open System.IO
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite


/// Runs `f` on the connection string of a fresh temporary database file, without pooling so
/// that the file can be deleted on every OS, and deletes the file afterwards.
let withDb (f: string -> 'a) =
    let path = Path.Combine(Path.GetTempPath(), $"genpres-%s{Guid.NewGuid().ToString()}.db")

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


let tests =
    testList
        "SqlSchema"
        [
            test "a fresh file gets migration 1 and the order_plan table" {
                withDb (fun cs ->
                    SqlSchema.applyAll cs migrations
                    |> Expect.equal "migration 1 applied" [ 1 ]

                    scalar cs "select group_concat(migration) from schema_version"
                    |> string
                    |> Expect.equal "schema_version records it" "1"

                    scalar cs "select count(*) from sqlite_master where type = 'table' and name = 'order_plan'"
                    |> unbox<int64>
                    |> Expect.equal "the table exists" 1L
                )
            }

            test "a second apply applies nothing" {
                withDb (fun cs ->
                    SqlSchema.applyAll cs migrations |> ignore

                    SqlSchema.applyAll cs migrations
                    |> Expect.isEmpty "nothing above the highest number"

                    scalar cs "select count(*) from schema_version"
                    |> unbox<int64>
                    |> Expect.equal "still one row" 1L
                )
            }

            test "a second order plan version with the same number is a unique violation" {
                withDb (fun cs ->
                    SqlSchema.applyAll cs migrations |> ignore
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
                        SqlSchema.ofScripts [ ("Sql/001-broken.sql", "create table t (a integer); create table t (a integer);") ]

                    Expect.throwsT<SqliteException> "the script fails" (fun () ->
                        SqlSchema.applyAll cs broken |> ignore
                    )

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

                Expect.throwsT<InvalidOperationException> "a name without a number" (fun () ->
                    SqlSchema.ofScripts [ ("Sql/order-plan.sql", "") ] |> ignore
                )

                Expect.throwsT<InvalidOperationException> "two scripts with one number" (fun () ->
                    SqlSchema.ofScripts [ ("Sql/001-a.sql", ""); ("Sql/001-b.sql", "") ] |> ignore
                )
            }
        ]


runTestsWithCLIArgs [] [||] tests
