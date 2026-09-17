namespace ServerApi

// The session store's test and development database, SQLite, beside the in-memory stand-in of
// `ServerApi.StubAdapters.fs`. Production never opens it. The SQL schema lives in the
// `Sql/*.sql` scripts embedded in this assembly.


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

                    row.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                    |> ignore

                    row.ExecuteNonQuery() |> ignore

                    tx.Commit()
                    m.Number
        ]


    /// Applies the migrations embedded in the server assembly.
    let apply (connectionString: string) =
        typeof<Migration>.Assembly |> embedded |> applyAll connectionString
