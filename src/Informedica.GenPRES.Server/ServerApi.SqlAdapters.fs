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


    /// Inserts an order plan version on a connection, within the transaction it is given, so
    /// that a request writing more than this one row writes them all as one.
    let insertVersion (conn: SqliteConnection) (tx: SqliteTransaction) (v: Types.OrderPlanVersion) =
        use cmd = conn.CreateCommand()
        cmd.Transaction <- tx

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


    let private insert (connectionString: string) (v: Types.OrderPlanVersion) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        insertVersion conn null v


    /// True when a failure is another server's sign of the same order plan version number.
    let isSameNumber (e: SqliteException) =
        e.SqliteExtendedErrorCode = 2067
        && e.Message.Contains "order_plan.patient_id, order_plan.no"


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
        | :? SqliteException as e when isSameNumber e ->
            try
                match loadRecords connectionString v.PatientId with
                | head :: _ -> Session.StoreOutcome.Conflict head
                | [] -> Session.StoreOutcome.Failed e.Message
            with reread ->
                Session.StoreOutcome.Failed reread.Message
        | e -> Session.StoreOutcome.Failed e.Message


    /// <summary>
    /// Runs the writes of a request. Only the order plan versions of a list reach the database
    /// while the launch and session rows still live in memory; the first write that does not
    /// land is the request's outcome. One transaction over the whole list, and the other facts
    /// with it, arrives with those tables.
    /// </summary>
    let persist (connectionString: string) (writes: Session.Persist list) : Session.StoreOutcome =
        writes
        |> List.choose (
            function
            | Session.WriteVersion v -> Some v
            | _ -> None
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


/// <summary>
/// The launches, the Sessions and their endings in the database: the rows a request can touch
/// read into its state, and the writes it returned appended in one transaction. Nothing is ever
/// changed: an ending, a heartbeat and what a Session opened with are rows of their own, and a
/// supersession is no row at all, since the newer Session of a login tells it.
/// </summary>
module SqlSessions =

    open System
    open Microsoft.Data.Sqlite
    open Shared.Types
    // the canonical serializer the stored Dtos are written with
    open Informedica.GenOrder.Lib

    module GenForm = Informedica.GenForm.Lib.Types

    /// Unix milliseconds, the form the columns hold, and back.
    let ms (at: DateTime) =
        DateTimeOffset(at, TimeSpan.Zero).ToUnixTimeMilliseconds()

    let at (ms: int64) =
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime


    /// The refusal a stored word names; the word is the one the client reads off the address.
    let refusalOf word =
        [
            LaunchRefusal.LaunchExpired
            LaunchRefusal.LaunchSpent
            LaunchRefusal.LaunchInvalid
            LaunchRefusal.NoBrowserIdentity
            LaunchRefusal.NoRole
            LaunchRefusal.WrongActivePatient
            LaunchRefusal.EnrolmentRequired
        ]
        |> List.tryFind (fun refusal -> Session.refusalWord refusal = word)


    /// The word a Role is stored under. Every Role matched, so that one without a word fails
    /// to compile.
    let roleWord =
        function
        | UserRole.Prescriber -> "prescriber"
        | UserRole.Reader -> "reader"


    /// The Role of a stored word, and nothing for a word no Role has: the column is free text,
    /// and a word this release does not know must not come back as the Role that may sign.
    let roleOf word =
        if word = roleWord UserRole.Prescriber then
            Some UserRole.Prescriber
        elif word = roleWord UserRole.Reader then
            Some UserRole.Reader
        else
            None


    /// A command on a connection, within a transaction when one is given.
    let command (conn: SqliteConnection) (tx: SqliteTransaction) (sql: string) (parameters: (string * obj) list) =
        let cmd = conn.CreateCommand()
        cmd.Transaction <- tx
        cmd.CommandText <- sql

        for name, value in parameters do
            cmd.Parameters.AddWithValue(name, value) |> ignore

        cmd


    let rows (conn: SqliteConnection) sql parameters (read: SqliteDataReader -> 'a) =
        use cmd = command conn null sql parameters
        use r = cmd.ExecuteReader()

        [
            while r.Read() do
                read r
        ]


    let textOrNull (r: SqliteDataReader) (i: int) =
        if r.IsDBNull i then None else Some(r.GetString i)


    /// <summary>
    /// The Launch of a nonce or of a callback's `state`, with what its callback came to, while
    /// it is within its lifetime; past it the record loads as absent, as a dropped row would.
    /// The Session an outcome names is rebuilt by the caller, which has the session rows.
    /// </summary>
    let loadLaunch (conn: SqliteConnection) (now: DateTime) (by: string) (value: string) =
        // the column is chosen here, never interpolated from a caller's string
        let column = if by = "nonce" then "l.nonce" else "l.state"

        rows
            conn
            $"""
            select l.nonce, l.state, l.patient_id, l.public_key, l.expiry, o.outcome, o.session_id, o.attempt
            from launch_record l left join launch_outcome o on o.nonce = l.nonce
            where {column} = $value
            """
            [ "$value", box value ]
            (fun r ->
                {|
                    Nonce = r.GetString 0
                    State = r.GetString 1
                    PatientId = r.GetString 2
                    PublicKey = PublicKey(r.GetString 3)
                    Expiry = at (r.GetInt64 4)
                    Outcome = textOrNull r 5
                    SessionId = textOrNull r 6
                    Attempt = textOrNull r 7
                |}
            )
        |> List.filter (fun row -> now <= row.Expiry)
        |> List.tryHead


    /// The highest JSON structure version this release reads for a stored patient. As for an
    /// order plan version, a change ships as expand, then contract; the version it writes
    /// lands with the writer that stores one.
    let patientJsonRead = 1


    /// Brings stored patient JSON to the structure this release reads. There are no steps yet.
    let upgradePatient (version: int) (json: string) : Result<string, string> =
        if version < 1 then
            Error $"JSON structure version %i{version} does not exist"
        elif version > patientJsonRead then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


    /// The patient a stored row holds, upgraded and parsed; the JSON a release writes for one
    /// lands with the writer that stores it.
    let readPatient (version: int) (json: string) : Result<GenForm.Patient, string> =
        upgradePatient version json
        |> Result.bind (fun json ->
            try
                json
                |> Canonical.deserialize<Informedica.GenForm.Lib.Patient.Dto.Dto>
                |> Informedica.GenForm.Lib.Patient.Dto.fromDto
                |> Result.mapError (fun errs -> $"the patient does not parse: %A{errs}")
            // any exception: a converter meeting a malformed value throws its own type
            with e ->
                Error $"the JSON does not read: %s{e.Message}"
        )


    /// What a Session opened with, the newest row: the token, the id of the version it opened
    /// with, the head it saw, and the patient data it shows, whose reason for not reading
    /// makes the Session unreadable.
    let loadOpenedWith (conn: SqliteConnection) (sid: string) =
        rows
            conn
            """
            select version_id, head_id, opened_token, json_version, patient
            from session_opened_with where session_id = $sid order by id desc limit 1
            """
            [ "$sid", box sid ]
            (fun r ->
                let patient =
                    match r.IsDBNull 3, r.IsDBNull 4 with
                    | false, false -> readPatient (r.GetInt32 3) (r.GetString 4) |> Result.map Some
                    | _ -> Ok None // a Session that opened on no patient data at all

                {|
                    VersionId = textOrNull r 0
                    HeadId = textOrNull r 1
                    OpenedToken = textOrNull r 2 |> Option.map OpenedToken
                    Patient = patient
                |}
            )
        |> List.tryHead


    let loadSeen (conn: SqliteConnection) (sid: string) =
        rows
            conn
            "select at from session_seen where session_id = $sid order by id desc limit 1"
            [ "$sid", box sid ]
            (fun r -> at (r.GetInt64 0))
        |> List.tryHead


    /// The ending of a Session, when it has one: the row of an act, or the supersession that a
    /// newer session row of its login is, read whatever became of that newer row, so that
    /// closing it never hands the login back. The second answer says the Session is gone.
    let loadEnding (conn: SqliteConnection) (sid: string) (login: string option) (id: int64) =
        let acknowledged =
            rows conn "select 1 from session_acknowledged where session_id = $sid" [ "$sid", box sid ] ignore
            |> List.isEmpty
            |> not

        let ending =
            rows
                conn
                "select ending, at from session_ending where session_id = $sid"
                [ "$sid", box sid ]
                (fun r -> r.GetString 0, at (r.GetInt64 1))
            |> List.tryHead

        let superseded =
            login
            |> Option.bind (fun login ->
                rows
                    conn
                    "select opened_at from session where login = $login and id > $id order by id desc limit 1"
                    [ "$login", box login; "$id", box id ]
                    (fun r -> at (r.GetInt64 0))
                |> List.tryHead
            )
            |> Option.map (fun at -> SessionEnding.SupersededByLaunch, at)

        if acknowledged then
            // whatever the Session ended of, the User was told and asked to leave: it is gone
            // and never told again, as the machine drops an ending it acknowledges
            None, true
        else
            match ending with
            | Some("closed", _) -> None, true
            | Some("wrong-pin-limit", at) -> Some(SessionEnding.WrongPinLimit, at), false
            | Some("unreadable", at) -> Some(SessionEnding.Unreadable, at), false
            // free text: a word this release cannot read still ended the Session
            | Some(_, at) -> Some(SessionEnding.Unreadable, at), false
            | None -> superseded, false


    /// The User a session row holds: a Session opened without a launch has none, and a Role
    /// this release cannot read makes the row one it cannot make sense of.
    let userOf (userId: string option) (displayName: string option) (role: string option) =
        match userId, displayName, role with
        | Some userId, Some name, Some word ->
            match roleOf word with
            | Some role ->
                Ok(
                    Some
                        {
                            UserId = userId
                            DisplayName = name
                            Role = role
                        }
                )
            | None -> Error $"the Role %s{word} is not one this release knows"
        | None, None, None -> Ok None
        | _ -> Error "the Session holds a User this release cannot read"


    /// The Session of an id as the state holds it: the row, what it opened with, its heartbeat
    /// and the head it opened on, through the `headOf` its caller loads the record with. One
    /// this release cannot read is answered with the reason, and ends as unreadable; an ended
    /// Session is answered with its ending, a closed one with none at all.
    let loadSession (conn: SqliteConnection) (headOf: string -> StoredVersion option) (sid: string) =
        rows
            conn
            """
            select id, login, user_id, user_display, user_role, patient_id, key_thumbprint, opened_at
            from session where session_id = $sid
            """
            [ "$sid", box sid ]
            (fun r ->
                {|
                    Id = r.GetInt64 0
                    Login = textOrNull r 1
                    UserId = textOrNull r 2
                    UserDisplay = textOrNull r 3
                    UserRole = textOrNull r 4
                    PatientId = textOrNull r 5
                    KeyThumbprint = textOrNull r 6
                    OpenedAt = at (r.GetInt64 7)
                |}
            )
        |> List.tryHead
        |> Option.map (fun row ->
            match loadEnding conn sid row.Login row.Id with
            | Some ending, _ -> Choice2Of2(Some ending)
            | None, true -> Choice2Of2 None
            | None, false ->
                let opened = loadOpenedWith conn sid

                let read =
                    userOf row.UserId row.UserDisplay row.UserRole
                    |> Result.bind (fun user ->
                        opened
                        |> Option.map _.Patient
                        |> Option.defaultValue (Ok None)
                        |> Result.map (fun patient -> user, patient)
                    )

                read
                |> Result.map (fun (user, patient) ->
                    let session: Session.SessionRecord =
                        {
                            Opened =
                                {
                                    User = user
                                    PatientId = row.PatientId
                                    Patient = patient
                                    OpenedToken = opened |> Option.bind _.OpenedToken
                                    KeyThumbprint = row.KeyThumbprint
                                    Head = opened |> Option.bind _.HeadId |> Option.bind headOf
                                }
                            Login = row.Login
                            OpenedWith = opened |> Option.bind _.VersionId
                            Seen = loadSeen conn sid |> Option.defaultValue row.OpenedAt
                        }

                    session
                )
                |> Choice1Of2
        )


    /// The JSON structure version this release writes for the patient a Session shows.
    let patientJsonWritten = 1


    let patientJson (patient: GenForm.Patient) =
        patient |> Informedica.GenForm.Lib.Patient.Dto.toDto |> Canonical.serialize


    /// The word an ending is stored under. Every ending matched, so that one without a word
    /// fails to compile; a supersession is never written, since the newer Session tells it.
    let endingWord =
        function
        | Session.StoredEnding.Closed -> "closed"
        | Session.StoredEnding.Ended SessionEnding.WrongPinLimit -> "wrong-pin-limit"
        | Session.StoredEnding.Ended SessionEnding.Unreadable -> "unreadable"
        | Session.StoredEnding.Ended SessionEnding.SupersededByLaunch -> "superseded"


    let exec (conn: SqliteConnection) (tx: SqliteTransaction) sql parameters =
        use cmd = command conn tx sql parameters
        cmd.ExecuteNonQuery() |> ignore


    let nullable (value: 'a option) =
        value |> Option.map box |> Option.defaultValue (box DBNull.Value)


    /// The rows of one write. Every case matched, so that a case without a row fails to
    /// compile.
    let run (conn: SqliteConnection) (tx: SqliteTransaction) (write: Session.Persist) =
        match write with
        | Session.WriteVersion v -> SqlDatabase.insertVersion conn tx v
        | Session.RecordLaunch r ->
            let (PublicKey key) = r.PublicKey

            exec
                conn
                tx
                "insert into launch_record (nonce, state, patient_id, public_key, expiry) values ($n, $s, $p, $k, $e)"
                [
                    "$n", box r.Nonce
                    "$s", box r.State
                    "$p", box r.PatientId
                    "$k", box key
                    "$e", box (ms r.Expiry)
                ]
        | Session.RecordLaunchOutcome(nonce, outcome, at) ->
            let word, sessionId, attempt =
                match outcome with
                | LaunchResult.Opened(sid, _) -> "opened", Some sid, None
                | LaunchResult.Refused refusal -> $"refused:%s{Session.refusalWord refusal}", None, None
                | LaunchResult.Enrolling attempt -> "enrolling", None, Some attempt
                // a redirect is no outcome: it is what a launch is answered with until one
                | LaunchResult.RedirectTo _ -> invalidOp "a redirect is not an outcome of a launch"

            exec
                conn
                tx
                "insert into launch_outcome (nonce, outcome, session_id, attempt, at) values ($n, $o, $s, $a, $at)"
                [
                    "$n", box nonce
                    "$o", box word
                    "$s", nullable sessionId
                    "$a", nullable attempt
                    "$at", box (ms at)
                ]
        | Session.OpenSession(sid, session) ->
            exec
                conn
                tx
                """
                insert into session
                    (session_id, login, user_id, user_display, user_role, patient_id, key_thumbprint, opened_at)
                values ($sid, $login, $uid, $name, $role, $pid, $key, $at)
                """
                [
                    "$sid", box sid
                    "$login", nullable session.Login
                    "$uid", nullable (session.Opened.User |> Option.map _.UserId)
                    "$name", nullable (session.Opened.User |> Option.map _.DisplayName)
                    "$role", nullable (session.Opened.User |> Option.map (_.Role >> roleWord))
                    "$pid", nullable session.Opened.PatientId
                    "$key", nullable session.Opened.KeyThumbprint
                    "$at", box (ms session.Seen)
                ]
        | Session.RecordOpenedWith(sid, session, at) ->
            let token = session.Opened.OpenedToken |> Option.map (fun (OpenedToken t) -> t)

            exec
                conn
                tx
                """
                insert into session_opened_with
                    (session_id, version_id, head_id, opened_token, json_version, patient, at)
                values ($sid, $version, $head, $token, $jv, $patient, $at)
                """
                [
                    "$sid", box sid
                    "$version", nullable session.OpenedWith
                    "$head", nullable (session.Opened.Head |> Option.map StoredVersion.id)
                    "$token", nullable token
                    "$jv", nullable (session.Opened.Patient |> Option.map (fun _ -> patientJsonWritten))
                    "$patient", nullable (session.Opened.Patient |> Option.map patientJson)
                    "$at", box (ms at)
                ]
        | Session.RecordSeen(sid, at) ->
            exec
                conn
                tx
                "insert into session_seen (session_id, at) values ($sid, $at)"
                [ "$sid", box sid; "$at", box (ms at) ]
        | Session.EndSession(sid, ending, at) ->
            exec
                conn
                tx
                "insert into session_ending (session_id, ending, at) values ($sid, $e, $at)"
                [
                    "$sid", box sid
                    "$e", box (endingWord ending)
                    "$at", box (ms at)
                ]
        | Session.AcknowledgeEnding(sid, at) ->
            exec
                conn
                tx
                "insert into session_acknowledged (session_id, at) values ($sid, $at)"
                [ "$sid", box sid; "$at", box (ms at) ]


    /// <summary>
    /// Runs the writes of a request in one transaction: all of them land, or none does. A
    /// violated `unique (patient_id, no)` on the record is another server's sign of the same
    /// number, answered with the head as it stands; any other failure is `Failed` with its
    /// reason, and the caller keeps the state it had, so that the next request retries.
    /// </summary>
    let runWrites (connectionString: string) (writes: Session.Persist list) : Session.StoreOutcome =
        // opening the file and starting the transaction are inside the try as well: a database
        // that cannot be reached is the store failing, which the caller retries, never an
        // exception escaping the request
        try
            use conn = new SqliteConnection(connectionString)
            conn.Open()
            use tx = conn.BeginTransaction()

            try
                for write in writes do
                    run conn tx write

                tx.Commit()
                Session.StoreOutcome.Written
            with
            | :? SqliteException as e when SqlDatabase.isSameNumber e ->
                tx.Rollback()

                let patient =
                    writes
                    |> List.tryPick (
                        function
                        | Session.WriteVersion v -> Some v.PatientId
                        | _ -> None
                    )

                match patient |> Option.map (SqlDatabase.loadRecords connectionString) with
                | Some(head :: _) -> Session.StoreOutcome.Conflict head
                | _ -> Session.StoreOutcome.Failed e.Message
            | e ->
                tx.Rollback()
                Session.StoreOutcome.Failed e.Message
        with e ->
            Session.StoreOutcome.Failed e.Message
