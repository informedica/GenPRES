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


/// <summary>
/// The record on SQLite: the order plan versions of a patient loaded from `order_plan`, and
/// the write of a commit inserted there. The plan is stored as the canonical JSON of
/// `OrderPlanVersion.Dto` under a JSON structure version; a row this release cannot read
/// loads as an unreadable entry built from the identity columns, never dropped.
/// </summary>
module SqlDatabase =

    open System
    open System.IO
    open Microsoft.Data.Sqlite
    open Informedica.GenOrder.Lib


    /// The highest JSON structure version this release reads.
    let jsonVersionRead = 1

    /// The JSON structure version this release writes.
    let jsonVersionWritten = 1


    /// Brings plan JSON written under a structure version to the structure this release
    /// reads, one pure step per version on raw JSON. There are no steps yet.
    let upgrade (version: int) (json: string) : Result<string, string> =
        if version < 1 then
            Error $"JSON structure version %i{version} does not exist"
        elif version > jsonVersionRead then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


    /// The JSON this release writes for an order plan version.
    let toJson (v: Types.OrderPlanVersion) =
        v |> OrderPlanVersion.Dto.toDto |> Canonical.serialize


    /// An `order_plan` row as read, before its JSON is parsed.
    type Row =
        {
            VersionId: string
            No: int
            PatientId: string
            Base: string option
            SignedByUserId: string
            SignedByDisplayName: string
            SignedAt: int64
            JsonVersion: int
            Plan: string
        }


    /// <summary>
    /// A row as the record holds it: upgraded, parsed with `fromDto`, and checked against its
    /// identity columns; else unreadable with the reason, its identity from the columns, which
    /// are authoritative.
    /// </summary>
    let readRow (row: Row) : StoredVersion =
        let parsed =
            upgrade row.JsonVersion row.Plan
            |> Result.bind (fun json ->
                try
                    json
                    |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                    |> OrderPlanVersion.Dto.fromDto
                    |> Result.mapError (fun errs -> $"the order plan version does not parse: %A{errs}")
                // any exception: a converter meeting a malformed value throws its own type
                with e ->
                    Error $"the JSON does not read: %s{e.Message}"
            )
            |> Result.bind (fun v ->
                if
                    v.Id = row.VersionId
                    && v.No = row.No
                    && v.PatientId = row.PatientId
                    && v.Base = row.Base
                then
                    Ok v
                else
                    Error "the identity in the JSON disagrees with the columns"
            )

        // a time the column holds but DateTime cannot: the row stays, at the earliest time
        let signedAt, reason =
            try
                DateTimeOffset.FromUnixTimeMilliseconds(row.SignedAt).UtcDateTime, None
            with :? ArgumentOutOfRangeException ->
                DateTime.MinValue, Some $"signed_at %i{row.SignedAt} is out of range"

        match parsed, reason with
        | Ok v, _ -> StoredVersion.Readable v
        | Error parseReason, timeReason ->
            StoredVersion.Unreadable
                {
                    Id = row.VersionId
                    No = row.No
                    PatientId = row.PatientId
                    Base = row.Base
                    SignedBy =
                        {
                            UserId = row.SignedByUserId
                            DisplayName = row.SignedByDisplayName
                        }
                    SignedAt = signedAt
                    Reason =
                        timeReason
                        |> Option.map (fun r -> $"%s{parseReason}; %s{r}")
                        |> Option.defaultValue parseReason
                }


    /// Every order plan version of a patient, newest first.
    let loadRecords (connectionString: string) (patientId: string) : StoredVersion list =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        use cmd = conn.CreateCommand()

        cmd.CommandText <-
            """
            select version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                   signed_at, json_version, plan
            from order_plan where patient_id = $patient order by no desc
            """

        cmd.Parameters.AddWithValue("$patient", patientId) |> ignore
        use r = cmd.ExecuteReader()

        [
            while r.Read() do
                readRow
                    {
                        VersionId = r.GetString 0
                        No = r.GetInt32 1
                        PatientId = r.GetString 2
                        Base = if r.IsDBNull 3 then None else Some(r.GetString 3)
                        SignedByUserId = r.GetString 4
                        SignedByDisplayName = r.GetString 5
                        SignedAt = r.GetInt64 6
                        JsonVersion = r.GetInt32 7
                        Plan = r.GetString 8
                    }
        ]


    let private insert (connectionString: string) (v: Types.OrderPlanVersion) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        use cmd = conn.CreateCommand()

        cmd.CommandText <-
            """
            insert into order_plan
                (version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                 signed_at, verified, json_version, plan)
            values ($id, $no, $patient, $base, $user, $name, $at, $verified, $json_version, $plan)
            """

        let add (name: string) (value: obj) =
            cmd.Parameters.AddWithValue(name, value) |> ignore

        add "$id" v.Id
        add "$no" v.No
        add "$patient" v.PatientId
        add "$base" (v.Base |> Option.map box |> Option.defaultValue DBNull.Value)
        add "$user" v.SignedBy.UserId
        add "$name" v.SignedBy.DisplayName
        add "$at" (DateTimeOffset(v.SignedAt, TimeSpan.Zero).ToUnixTimeMilliseconds())
        add "$verified" (if v.Verified then 1 else 0)
        add "$json_version" jsonVersionWritten
        add "$plan" (toJson v)
        cmd.ExecuteNonQuery() |> ignore


    /// <summary>
    /// Runs one order plan version: inserts it. A violated
    /// `unique (patient_id, no)` is another server's sign of the same number, answered with
    /// the head as it stands now; any other failure, other constraints included, is `Failed`
    /// with the reason, so that the caller keeps its state and the next Submission retries.
    /// </summary>
    let persistVersion (connectionString: string) (v: Types.OrderPlanVersion) : Session.StoreOutcome =
        try
            insert connectionString v
            Session.StoreOutcome.Written
        with
        | :? SqliteException as e when
            e.SqliteExtendedErrorCode = 2067
            && e.Message.Contains "order_plan.patient_id, order_plan.no"
            ->
            try
                match loadRecords connectionString v.PatientId with
                | head :: _ -> Session.StoreOutcome.Conflict head
                | [] -> Session.StoreOutcome.Failed e.Message
            with reread ->
                Session.StoreOutcome.Failed reread.Message
        | e -> Session.StoreOutcome.Failed e.Message


    /// <summary>
    /// Runs the writes of a request: the order plan versions of the list, one by one, the
    /// first that does not land being the request's outcome. The facts the launch and session
    /// rows record, and the transaction over the whole list, arrive with their tables.
    /// </summary>
    let persist (connectionString: string) (writes: Session.Persist list) : Session.StoreOutcome =
        writes
        |> List.choose (
            function
            | Session.WriteVersion v -> Some v
        )
        |> List.fold
            (fun outcome v ->
                match outcome with
                | Session.StoreOutcome.Written -> persistVersion connectionString v
                | refused -> refused
            )
            Session.StoreOutcome.Written


    /// <summary>
    /// The record in the database: a load replaces the state's record with the patient's order
    /// plan versions, a write inserts one. `warn` hears of every load that throws, which is
    /// then rethrown, and of every unreadable version a load finds.
    /// </summary>
    let store (warn: string -> unit) (connectionString: string) : StubDatabase.RecordStore =
        {
            load =
                fun pid s ->
                    let versions =
                        try
                            loadRecords connectionString pid
                        with e ->
                            warn $"the record could not be loaded: %s{e.Message}"
                            reraise ()

                    for v in versions do
                        match v with
                        | StoredVersion.Unreadable u ->
                            warn $"order plan version %s{u.Id} (number %i{u.No}) cannot be read: %s{u.Reason}"
                        | StoredVersion.Readable _ -> ()

                    { s with Records = Map.ofList [ pid, versions ] }
            persist = persist connectionString
        }


    /// The session port over the record in the database.
    let makeSessionPort warn connectionString =
        StubDatabase.makeSessionPortWith (store warn connectionString)


    /// <summary>
    /// A connection string with a relative data source rooted at `root`, and the folder of the
    /// file created when it is missing: SQLite creates the file, never its folder.
    /// </summary>
    let connectionString (root: string) (value: string) =
        let builder = SqliteConnectionStringBuilder value

        if
            not (String.IsNullOrWhiteSpace builder.DataSource)
            && builder.DataSource <> ":memory:"
            && not (Path.IsPathRooted builder.DataSource)
        then
            builder.DataSource <- Path.Combine(root, builder.DataSource)

        match Path.GetDirectoryName builder.DataSource with
        | null
        | "" -> ()
        | dir -> Directory.CreateDirectory dir |> ignore

        builder.ToString()
