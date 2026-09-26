namespace ServerApi

// The session store's test and development database, SQLite, beside the in-memory stand-in of
// `ServerApi.StubAdapters.fs`. Production never opens it. The SQL schema lives in the
// `Sql/*.sql` scripts embedded in this assembly.


/// <summary>
/// The SQL schema of the session store, applied as numbered migrations. Each migration is a
/// script <c>Sql/NNN-name.sql</c>; its number is the leading integer of the file name. The runner
/// records every migration it applied in <c>schema_version</c> and applies each script above the
/// highest recorded number, each in a transaction of its own with its <c>schema_version</c> row.
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


    /// The migrations embedded in an assembly under Sql/.
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
    /// number <c>schema_version</c> records, and returns the numbers it applied, in order. Each
    /// migration runs in one transaction with its <c>schema_version</c> row, and the highest number
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
/// The record on SQLite: the order plan versions of a patient loaded from <c>order_plan</c>, and
/// the write of a commit inserted there. The plan is stored as the canonical JSON of
/// <c>OrderPlanVersion.Dto</c> under a JSON structure version; a row this release cannot read
/// loads as an unreadable entry built from the identity columns, never dropped.
/// </summary>
module SqlDatabase =

    open System
    open System.IO
    open Microsoft.Data.Sqlite
    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.BCL


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
    let toJson (v: Types.OrderPlanVersion) = v |> OrderPlanVersion.Dto.toDto |> Canonical.serialize


    /// An order_plan row as read, before its JSON is parsed.
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
    /// A row as the record holds it: upgraded, parsed with <c>fromDto</c>, and checked against its
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

        // the identity columns come with the store's next migration
        match parsed, reason with
        | Ok v, _ -> StoredVersion.Readable(v, None)
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
                    Identity = None
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

        let add (name: string) (value: obj) = cmd.Parameters.AddWithValue(name, value) |> ignore

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
    /// <c>unique (patient_id, no)</c> is another server's sign of the same number, answered with
    /// the head as it stands now; any other failure, other constraints included, is <c>Failed</c>
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
            | Session.WriteVersion(v, _) -> Some v
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
    /// A connection string with a relative data source rooted at <c>root</c>, and the folder of the
    /// file created when it is missing: SQLite creates the file, never its folder.
    /// </summary>
    let connectionString (root: string) (value: string) =
        let builder = SqliteConnectionStringBuilder value

        if
            builder.DataSource |> String.notEmpty
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
    let ms (at: DateTime) = DateTimeOffset(at, TimeSpan.Zero).ToUnixTimeMilliseconds()

    let at (ms: int64) = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime


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


    let textOrNull (r: SqliteDataReader) (i: int) = if r.IsDBNull i then None else Some(r.GetString i)


    /// <summary>
    /// The Launch of a nonce or of a callback's <c>state</c>, with what its callback came to, while
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


    /// The highest JSON structure version this release reads for stored EHR data.
    let ehrJsonRead = 1


    /// Brings stored EHR data JSON to the structure this release reads. There are no steps yet.
    let upgradeEhr (version: int) (json: string) : Result<string, string> =
        if version < 1 then
            Error $"JSON structure version %i{version} does not exist"
        elif version > ehrJsonRead then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


    /// The EHR data a stored row holds, upgraded and parsed.
    let readEhr (version: int) (json: string) : Result<GenForm.EhrPatientData, string> =
        upgradeEhr version json
        |> Result.bind (fun json ->
            try
                json
                |> Canonical.deserialize<Informedica.GenForm.Lib.EhrPatientData.Dto.Dto>
                |> Informedica.GenForm.Lib.EhrPatientData.Dto.fromDto
                |> Result.mapError (fun errs -> $"the EHR data does not parse: %A{errs}")
            with e ->
                Error $"the JSON does not read: %s{e.Message}"
        )


    /// The EHR data of a row, from the two columns that hold its structure version and its JSON:
    /// both present reads it, both null is none (the read was none, or the row is from before
    /// the columns), one of them null is a reason. The columns were added later and cannot
    /// carry the check the patient's have, so the loader holds them to it.
    let readEhrColumns (r: SqliteDataReader) (version: int) (json: int) =
        match r.IsDBNull version, r.IsDBNull json with
        | false, false -> readEhr (r.GetInt32 version) (r.GetString json) |> Result.map Some
        | true, true -> Ok None
        | _ -> Error "the EHR data and its JSON structure version are not both present"


    /// What the user measured in a Session, its rows oldest first, the latest per kind
    /// deciding; a kind this release does not know, or a gestational age with its weeks and
    /// not its days, is a reason.
    let loadMeasurements (conn: SqliteConnection) (sid: string) : Result<Measurements, string> =
        rows
            conn
            "select kind, value, days, at from measurement where session_id = $sid order by id"
            [ "$sid", box sid ]
            (fun r ->
                let value = if r.IsDBNull 1 then None else Some(r.GetInt32 1)
                let days = if r.IsDBNull 2 then None else Some(r.GetInt32 2)
                let at = at (r.GetInt64 3)

                match r.GetString 0, value, days with
                | "weight", _, _ -> Ok(Measurement.Weight(value |> Option.map Shared.Measures.toGram), at)
                | "height", _, _ -> Ok(Measurement.Height(value |> Option.map Shared.Measures.toCm), at)
                | "gestage", Some w, Some d ->
                    let gestAge: GestAge =
                        {
                            Weeks = Shared.Measures.toWeek w
                            Days = Shared.Measures.toDay d
                        }

                    Ok(Measurement.GestAge(Some gestAge), at)
                | "gestage", None, None -> Ok(Measurement.GestAge None, at)
                | "gestage", _, _ -> Error "a gestational age has its weeks and its days, or neither"
                | kind, _, _ -> Error $"the measurement kind %s{kind} is not known"
            )
        |> List.fold
            (fun acc row ->
                match acc, row with
                | Ok rows, Ok row -> Ok(row :: rows)
                | Error e, _
                | _, Error e -> Error e
            )
            (Ok [])
        |> Result.map (List.rev >> Measurements.ofRows)


    /// What a Session opened with, the newest row: the token, the id of the version it opened
    /// with, the head it saw, the EHR data it opened on and the patient data it shows, whose
    /// reasons for not reading make the Session unreadable.
    let loadOpenedWith (conn: SqliteConnection) (sid: string) =
        rows
            conn
            """
            select version_id, head_id, opened_token, json_version, patient, ehr_json_version, ehr_data
            from session_opened_with where session_id = $sid order by id desc limit 1
            """
            [ "$sid", box sid ]
            (fun r ->
                let patient =
                    match r.IsDBNull 3, r.IsDBNull 4 with
                    | false, false -> readPatient (r.GetInt32 3) (r.GetString 4) |> Result.map Some
                    | _ -> Ok None // a Session that opened on no patient data at all

                let ehr = readEhrColumns r 5 6

                {|
                    VersionId = textOrNull r 0
                    HeadId = textOrNull r 1
                    OpenedToken = textOrNull r 2 |> Option.map OpenedToken
                    Patient = patient
                    EhrData = ehr
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
    /// and the head it opened on, through the headOf its caller loads the record with. One
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
                        |> Result.bind (fun patient ->
                            opened
                            |> Option.map _.EhrData
                            |> Option.defaultValue (Ok None)
                            |> Result.bind (fun ehr ->
                                loadMeasurements conn sid
                                |> Result.map (fun measured -> user, patient, ehr, measured)
                            )
                        )
                    )

                read
                |> Result.map (fun (user, patient, ehr, measured) ->
                    let session: Session.SessionRecord =
                        {
                            Opened =
                                {
                                    User = user
                                    PatientId = row.PatientId
                                    EhrData = ehr
                                    Patient = patient
                                    Measured = measured
                                    OpenedToken = opened |> Option.bind _.OpenedToken
                                    KeyThumbprint = row.KeyThumbprint
                                    Head = opened |> Option.bind _.HeadId |> Option.bind headOf
                                }
                            Login = row.Login
                            OpenedWith = opened |> Option.bind _.VersionId
                            OpenedAt = row.OpenedAt
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


    /// The JSON structure version this release writes for the EHR data a Session opened on.
    let ehrJsonWritten = 1


    let ehrJson (ehr: GenForm.EhrPatientData) =
        ehr |> Informedica.GenForm.Lib.EhrPatientData.Dto.toDto |> Canonical.serialize


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


    /// The word a refusal is stored under, and what of it the row keeps beside the word. Every
    /// refusal matched, so that one without a word fails to compile; Blocked keeps the id of
    /// the head that blocked, which is an order_plan row the answer is rebuilt from.
    let refusalRow (refusal: SigningRefusal) =
        match refusal with
        | SigningRefusal.NoSession -> "no-session", None, None, None
        | SigningRefusal.NoPatient -> "no-patient", None, None, None
        | SigningRefusal.NotPrescriber -> "not-prescriber", None, None, None
        | SigningRefusal.Blocked head -> "blocked", Some head.Id, None, None
        | SigningRefusal.StaleToken -> "stale-token", None, None, None
        | SigningRefusal.ChallengeMismatch -> "challenge-mismatch", None, None, None
        | SigningRefusal.ChallengeExpired -> "challenge-expired", None, None, None
        | SigningRefusal.PinWrong left -> "pin-wrong", None, Some left, None
        | SigningRefusal.PinLimit -> "pin-limit", None, None, None
        | SigningRefusal.Locked until -> "locked", None, None, Some until
        | SigningRefusal.StoreFailed -> "store-failed", None, None, None
        | SigningRefusal.PlanUnreadable -> "plan-unreadable", None, None, None


    /// The rows of one write. Every case matched, so that a case without a row fails to
    /// compile.
    let run (conn: SqliteConnection) (tx: SqliteTransaction) (write: Session.Persist) =
        match write with
        | Session.WriteVersion(v, _) -> SqlDatabase.insertVersion conn tx v
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
                    (session_id, version_id, head_id, opened_token, json_version, patient,
                     ehr_json_version, ehr_data, at)
                values ($sid, $version, $head, $token, $jv, $patient, $ejv, $ehr, $at)
                """
                [
                    "$sid", box sid
                    "$version", nullable session.OpenedWith
                    "$head", nullable (session.Opened.Head |> Option.map StoredVersion.id)
                    "$token", nullable token
                    "$jv", nullable (session.Opened.Patient |> Option.map (fun _ -> patientJsonWritten))
                    "$patient", nullable (session.Opened.Patient |> Option.map patientJson)
                    "$ejv", nullable (session.Opened.EhrData |> Option.map (fun _ -> ehrJsonWritten))
                    "$ehr", nullable (session.Opened.EhrData |> Option.map ehrJson)
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
                [ "$sid", box sid; "$e", box (endingWord ending); "$at", box (ms at) ]
        | Session.AcknowledgeEnding(sid, at) ->
            exec
                conn
                tx
                "insert into session_acknowledged (session_id, at) values ($sid, $at)"
                [ "$sid", box sid; "$at", box (ms at) ]
        | Session.WriteCredential(userId, event, credential, at) ->
            exec
                conn
                tx
                """
                insert into credential_event
                    (user_id, event, pin_salt, pin_hash, wrong_count, locked_until, at)
                values ($u, $e, $salt, $hash, $wrong, $locked, $at)
                """
                [
                    "$u", box userId
                    "$e", box event
                    "$salt", nullable (credential.PinHash |> Option.map _.Salt)
                    "$hash", nullable (credential.PinHash |> Option.map _.Hash)
                    "$wrong", box credential.WrongCount
                    "$locked", nullable (credential.LockedUntil |> Option.map ms)
                    "$at", box (ms at)
                ]
        | Session.WriteCode(code, at) ->
            exec
                conn
                tx
                """
                insert into confirmation_code (user_id, mail_address, code_mac, expiry, at)
                values ($u, $mail, $mac, $expiry, $at)
                """
                [
                    "$u", box code.UserId
                    "$mail", box code.MailAddress
                    "$mac", box code.CodeMac
                    "$expiry", box (ms code.Expiry)
                    "$at", box (ms at)
                ]
        // a try and a spending name the very code the request read, by its mac: another server
        // may have mailed a newer one meanwhile, and a try of the older must not void it. A
        // code already spent matches nothing, and then there is nothing to write either
        | Session.CountCodeTry(userId, codeMac, at) ->
            exec
                conn
                tx
                """
                insert into code_try (code_id, at)
                select c.id, $at from confirmation_code c
                where c.user_id = $u and c.code_mac = $mac
                  and not exists (select 1 from code_spent s where s.code_id = c.id)
                order by c.id desc limit 1
                """
                [ "$u", box userId; "$mac", box codeMac; "$at", box (ms at) ]
        | Session.SpendCode(userId, codeMac, at) ->
            exec
                conn
                tx
                """
                insert or ignore into code_spent (code_id, at)
                select c.id, $at from confirmation_code c
                where c.user_id = $u and c.code_mac = $mac
                  and not exists (select 1 from code_spent s where s.code_id = c.id)
                order by c.id desc limit 1
                """
                [ "$u", box userId; "$mac", box codeMac; "$at", box (ms at) ]
        | Session.WriteEnrolment(e, at) ->
            let (PublicKey key) = e.PublicKey

            exec
                conn
                tx
                """
                insert into enrolment (attempt, user_id, login, display_name, patient_id, public_key, at)
                values ($a, $u, $l, $d, $p, $k, $at)
                """
                [
                    "$a", box e.Attempt
                    "$u", box e.UserId
                    "$l", box e.Login
                    "$d", box e.DisplayName
                    "$p", box e.PatientId
                    "$k", box key
                    "$at", box (ms at)
                ]
        // an attempt already given up is not an error: one request can name it twice
        | Session.DropEnrolmentWrite(attempt, at) ->
            exec
                conn
                tx
                "insert or ignore into enrolment_dropped (attempt, at) values ($a, $at)"
                [ "$a", box attempt; "$at", box (ms at) ]
        | Session.DropEnrolmentsOf(userId, at) ->
            exec
                conn
                tx
                """
                insert or ignore into enrolment_dropped (attempt, at)
                select attempt, $at from enrolment where user_id = $u
                """
                [ "$u", box userId; "$at", box (ms at) ]
        | Session.WriteNotice(sid, notice, at) ->
            exec
                conn
                tx
                """
                insert into data_notice
                    (session_id, nonce, json_version, data, ehr_json_version, ehr_data, expiry, at)
                values ($sid, $n, $jv, $data, $ejv, $ehr, $e, $at)
                """
                [
                    "$sid", box sid
                    "$n", box notice.Nonce
                    "$jv", nullable (notice.Data |> Option.map (fun _ -> patientJsonWritten))
                    "$data", nullable (notice.Data |> Option.map patientJson)
                    "$ejv", nullable (notice.Ehr |> Option.map (fun _ -> ehrJsonWritten))
                    "$ehr", nullable (notice.Ehr |> Option.map ehrJson)
                    "$e", box (ms notice.Expiry)
                    "$at", box (ms at)
                ]
        | Session.WriteMeasurement(sid, measurement, at) ->
            let kind, value, days =
                match measurement with
                | Measurement.Weight g -> "weight", g |> Option.map int, None
                | Measurement.Height cm -> "height", cm |> Option.map int, None
                | Measurement.GestAge ga ->
                    "gestage", ga |> Option.map (fun g -> int g.Weeks), ga |> Option.map (fun g -> int g.Days)

            exec
                conn
                tx
                "insert into measurement (session_id, kind, value, days, at) values ($sid, $k, $v, $d, $at)"
                [
                    "$sid", box sid
                    "$k", box kind
                    "$v", nullable value
                    "$d", nullable days
                    "$at", box (ms at)
                ]
        | Session.WriteChallenge(sid, challenge, at) ->
            exec
                conn
                tx
                """
                insert into challenge
                    (session_id, nonce, digest, json_version, reading, ehr_json_version, ehr_data, expiry, at)
                values ($sid, $n, $d, $jv, $reading, $ejv, $ehr, $e, $at)
                """
                [
                    "$sid", box sid
                    "$n", box challenge.Nonce
                    "$d", box challenge.Digest
                    "$jv", nullable (challenge.Reading |> Option.map (fun _ -> patientJsonWritten))
                    "$reading", nullable (challenge.Reading |> Option.map patientJson)
                    "$ejv", nullable (challenge.Ehr |> Option.map (fun _ -> ehrJsonWritten))
                    "$ehr", nullable (challenge.Ehr |> Option.map ehrJson)
                    "$e", box (ms challenge.Expiry)
                    "$at", box (ms at)
                ]
        // the challenge a commit used, named by its nonce: another server may have issued a
        // newer one, and spending that one would take a challenge the User is answering
        | Session.SpendChallenge(sid, nonce, at) ->
            exec
                conn
                tx
                """
                insert or ignore into challenge_spent (challenge_id, at)
                select c.id, $at from challenge c
                where c.session_id = $sid and c.nonce = $n
                  and not exists (select 1 from challenge_spent s where s.challenge_id = c.id)
                order by c.id desc limit 1
                """
                [ "$sid", box sid; "$n", box nonce; "$at", box (ms at) ]
        | Session.RememberAnswer(sid, key, outcome, at) ->
            let answer, versionId, token, left, until =
                match outcome with
                | SigningOutcome.Submitted(v, _, OpenedToken t, _) -> "submitted", Some v.Id, Some t, None, None
                | SigningOutcome.Refused refusal ->
                    let word, blocked, left, until = refusalRow refusal
                    $"refused:%s{word}", blocked, None, left, until
                // a challenge and a notice are answers of `challenge`, which remembers nothing
                | other -> invalidOp $"a Submission is not answered with %A{other}"

            exec
                conn
                tx
                """
                insert or ignore into submission_answer
                    (session_id, idem_key, answer, version_id, opened_token, attempts_left, locked_until, at)
                values ($sid, $k, $a, $v, $t, $left, $until, $at)
                """
                [
                    "$sid", box sid
                    "$k", box key
                    "$a", box answer
                    "$v", nullable versionId
                    "$t", nullable token
                    "$left", nullable left
                    "$until", nullable (until |> Option.map ms)
                    "$at", box (ms at)
                ]


    /// One line of the audit: when, to which Session, by whom, what was done and how it came
    /// out, with what the action needs beside its name.
    type AuditEntry =
        {
            At: DateTime
            SessionId: string option
            Actor: string option
            Action: string
            Outcome: string
            Detail: string option
        }


    let auditEntry at action outcome =
        {
            At = at
            SessionId = None
            Actor = None
            Action = action
            Outcome = outcome
            Detail = None
        }


    /// A string as a JSON literal, quoted and escaped. The detail of an entry is JSON, and a
    /// patient id or an attempt is whatever the launch carried, so nothing is pasted in raw.
    let jsonText (text: string) = Newtonsoft.Json.JsonConvert.ToString text


    /// <summary>
    /// What a write says about the request it belongs to: the Session it acts on, and the
    /// person it acts for. Every case matched, as <c>auditOf</c> matches them, so that a write that
    /// comes to name a Session or a person cannot quietly stop naming it in the audit.
    /// </summary>
    let namedBy (write: Session.Persist) : string option * string option =
        match write with
        | Session.OpenSession(sid, session)
        | Session.RecordOpenedWith(sid, session, _) -> Some sid, session.Opened.User |> Option.map _.UserId
        | Session.RecordSeen(sid, _)
        | Session.EndSession(sid, _, _)
        | Session.AcknowledgeEnding(sid, _)
        | Session.WriteNotice(sid, _, _)
        | Session.WriteChallenge(sid, _, _)
        | Session.SpendChallenge(sid, _, _)
        | Session.RememberAnswer(sid, _, _, _)
        | Session.WriteMeasurement(sid, _, _) -> Some sid, None
        | Session.WriteVersion(v, _) -> None, Some v.SignedBy.UserId
        | Session.WriteCredential(userId, _, _, _)
        | Session.CountCodeTry(userId, _, _)
        | Session.SpendCode(userId, _, _)
        | Session.DropEnrolmentsOf(userId, _) -> None, Some userId
        | Session.WriteCode(code, _) -> None, Some code.UserId
        | Session.WriteEnrolment(enrolment, _) -> None, Some enrolment.UserId
        | Session.RecordLaunch _
        | Session.RecordLaunchOutcome _
        | Session.DropEnrolmentWrite _ -> None, None


    /// <summary>
    /// The audit of one write. Every case matched, so that a write nobody thought to audit
    /// fails to compile; the writes that carry only a fact of an act another write already
    /// audits return nothing, said here rather than left out. What the write does not know —
    /// the Session a signature was made in, the person whose Session ended — <c>auditOf</c> fills in
    /// from the rest of the request.
    /// </summary>
    let entryOf (now: DateTime) (write: Session.Persist) : AuditEntry option =
        match write with
        // a launch record carries the Launch's expiry, not the time it was made: the entry is
        // timed by the request that wrote it
        | Session.RecordLaunch r ->
            Some { auditEntry now "launched" "ok" with Detail = Some $"""{{"patient":%s{jsonText r.PatientId}}}""" }
        | Session.RecordLaunchOutcome(_, outcome, at) ->
            match outcome with
            // the Session this launch opened is audited by the open itself, which the same
            // request writes; a second entry would count one open twice
            | LaunchResult.Opened _ -> None
            | LaunchResult.Refused refusal ->
                Some
                    { auditEntry at "launch" "refused" with
                        Detail = Some $"""{{"refusal":%s{jsonText (Session.refusalWord refusal)}}}"""
                    }
            | LaunchResult.Enrolling attempt ->
                Some { auditEntry at "enrolling" "ok" with Detail = Some $"""{{"attempt":%s{jsonText attempt}}}""" }
            // a redirect is no outcome: it is what a launch is answered with until one
            | LaunchResult.RedirectTo _ -> None
        | Session.OpenSession(sid, session) ->
            Some
                { auditEntry session.Seen "session-opened" "ok" with
                    SessionId = Some sid
                    Actor = session.Opened.User |> Option.map _.UserId
                }
        | Session.EndSession(sid, ending, at) ->
            Some
                { auditEntry at "session-ended" "ok" with
                    SessionId = Some sid
                    Detail = Some $"""{{"ending":%s{jsonText (endingWord ending)}}}"""
                }
        | Session.WriteVersion(v, _) ->
            Some
                { auditEntry v.SignedAt "signed" "ok" with
                    Actor = Some v.SignedBy.UserId
                    Detail =
                        Some $"""{{"version":%s{jsonText v.Id},"no":%i{v.No},"patient":%s{jsonText v.PatientId}}}"""
                }
        | Session.WriteCredential(userId, event, _, at) ->
            Some { auditEntry at $"credential-%s{event}" "ok" with Actor = Some userId }
        | Session.WriteCode(code, at) -> Some { auditEntry at "code-mailed" "ok" with Actor = Some code.UserId }
        | Session.CountCodeTry(userId, _, at) ->
            Some { auditEntry at "code-entered" "refused" with Actor = Some userId }
        | Session.WriteNotice(sid, _, at) -> Some { auditEntry at "notice-told" "ok" with SessionId = Some sid }
        | Session.WriteChallenge(sid, _, at) -> Some { auditEntry at "challenge-issued" "ok" with SessionId = Some sid }
        // what was measured is the value's business: the audit names the kind alone
        | Session.WriteMeasurement(sid, measurement, at) ->
            let kind =
                match measurement with
                | Measurement.Weight _ -> "weight"
                | Measurement.Height _ -> "height"
                | Measurement.GestAge _ -> "gestational-age"

            Some
                { auditEntry at "measurement-recorded" "ok" with
                    SessionId = Some sid
                    Detail = Some $"""{{"kind":%s{jsonText kind}}}"""
                }
        | Session.RememberAnswer(sid, _, outcome, at) ->
            match outcome with
            // the signature itself is audited by the version it wrote
            | SigningOutcome.Submitted _ -> None
            | SigningOutcome.Refused refusal ->
                let word, _, _, _ = refusalRow refusal

                Some
                    { auditEntry at "sign" "refused" with
                        SessionId = Some sid
                        Detail = Some $"""{{"refusal":%s{jsonText word}}}"""
                    }
            // a challenge and a notice are answers of `challenge`, which remembers nothing
            | _ -> None
        // facts of an act that another of its writes audits: the heartbeat of a request, what
        // a Session opened with, an ending acknowledged, a code or a challenge used up, and the
        // enrolment attempts an act of the person's already tells
        | Session.RecordOpenedWith _
        | Session.RecordSeen _
        | Session.AcknowledgeEnding _
        | Session.SpendCode _
        | Session.WriteEnrolment _
        | Session.DropEnrolmentWrite _
        | Session.DropEnrolmentsOf _
        | Session.SpendChallenge _ -> None


    /// <summary>
    /// What a request did, read off the writes it returned. The request is the unit, not the
    /// single write: an act is told apart by the writes it brings together, and what one write
    /// knows about the Session and the person is known to every entry of the request.
    /// </summary>
    let auditOf (now: DateTime) (writes: Session.Persist list) : AuditEntry list =
        let named = writes |> List.map namedBy

        // a version opened is the one act with no write of its own: it writes what the Session
        // now opens with, as an open and a signature also do, and nothing else. It is told
        // apart by what it does not write.
        let versionOpened =
            let opensWith =
                writes
                |> List.exists (
                    function
                    | Session.RecordOpenedWith _ -> true
                    | _ -> false
                )

            let openedOrSigned =
                writes
                |> List.exists (
                    function
                    | Session.OpenSession _
                    | Session.WriteVersion _ -> true
                    | _ -> false
                )

            if opensWith && not openedOrSigned then
                [ auditEntry now "version-opened" "ok" ]
            else
                []

        versionOpened @ (writes |> List.choose (entryOf now))
        |> List.map (fun e ->
            { e with
                SessionId = e.SessionId |> Option.orElse (named |> List.tryPick fst)
                Actor = e.Actor |> Option.orElse (named |> List.tryPick snd)
            }
        )


    /// The entries of a request, in the transaction that carries its writes.
    let appendAudit (conn: SqliteConnection) (tx: SqliteTransaction) (now: DateTime) (writes: Session.Persist list) =
        for e in auditOf now writes do
            exec
                conn
                tx
                """
                insert into audit_entry (at, session_id, actor, action, outcome, detail)
                values ($at, $sid, $actor, $action, $outcome, $detail)
                """
                [
                    "$at", box (ms e.At)
                    "$sid", nullable e.SessionId
                    "$actor", nullable e.Actor
                    "$action", box e.Action
                    "$outcome", box e.Outcome
                    "$detail", nullable e.Detail
                ]


    /// <summary>
    /// Runs the writes of a request in one transaction: all of them land, or none does. A
    /// violated <c>unique (patient_id, no)</c> on the record is another server's sign of the same
    /// number, answered with the head as it stands; any other failure is <c>Failed</c> with its
    /// reason, and the caller keeps the state it had, so that the next request retries.
    /// </summary>
    let runWrites
        (connectionString: string)
        (now: unit -> DateTime)
        (writes: Session.Persist list)
        : Session.StoreOutcome
        =
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

                appendAudit conn tx (now ()) writes
                tx.Commit()
                Session.StoreOutcome.Written
            with
            | :? SqliteException as e when SqlDatabase.isSameNumber e ->
                tx.Rollback()

                let patient =
                    writes
                    |> List.tryPick (
                        function
                        | Session.WriteVersion(v, _) -> Some v.PatientId
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


    // ---- the credential, the code and the attempts ------------------------------------------

    /// The credential of a person: the newest event carries it whole, so a load reads one row
    /// and no history is replayed. A person with no event has no credential at all, which is
    /// what a Prescriber who has never enrolled looks like.
    let loadCredential (conn: SqliteConnection) (userId: string) =
        rows
            conn
            """
            select pin_salt, pin_hash, wrong_count, locked_until
            from credential_event where user_id = $u order by id desc limit 1
            """
            [ ("$u", box userId) ]
            (fun r ->
                {
                    PinHash =
                        if r.IsDBNull 0 then
                            None
                        else
                            Some
                                {
                                    Salt = r.GetFieldValue<byte[]> 0
                                    Hash = r.GetFieldValue<byte[]> 1
                                }
                    WrongCount = r.GetInt32 2
                    LockedUntil = if r.IsDBNull 3 then None else Some(at (r.GetInt64 3))
                }
            )
        |> List.tryHead


    /// <summary>
    /// The live confirmation code of a person: the newest row, and only that row, with its
    /// wrong tries counted. It is the code unless it is spent or past its lifetime, in which
    /// case the person has none — never an older code in place of the one that was mailed last.
    /// </summary>
    let loadCode (conn: SqliteConnection) (now: DateTime) (userId: string) =
        rows
            conn
            """
            select c.id, c.mail_address, c.code_mac, c.expiry,
                   (select count(*) from code_try t where t.code_id = c.id),
                   exists (select 1 from code_spent s where s.code_id = c.id)
            from confirmation_code c
            where c.user_id = $u
            order by c.id desc limit 1
            """
            [ ("$u", box userId) ]
            (fun r ->
                r.GetInt64 5 = 1L,
                ({
                    UserId = userId
                    MailAddress = r.GetString 1
                    CodeMac = r.GetFieldValue<byte[]> 2
                    Expiry = at (r.GetInt64 3)
                    Tries = r.GetInt32 4
                }
                : Session.PendingCode)
            )
        |> List.tryHead
        |> Option.bind (fun (spent, code) -> if spent || now > code.Expiry then None else Some code)


    /// An enrolment attempt that was not given up, and every undropped attempt of the person it
    /// names: dropEnrolment spends the shared code only when no other attempt of that person
    /// stands, and supplyPin drops them all, so both need the wider slice.
    let loadEnrolments (conn: SqliteConnection) (by: string) (value: string) =
        rows
            conn
            $"""
            select e.attempt, e.user_id, e.login, e.display_name, e.patient_id, e.public_key
            from enrolment e
            where e.{if by = "attempt" then "attempt" else "user_id"} = $v
              and not exists (select 1 from enrolment_dropped d where d.attempt = e.attempt)
            """
            [ ("$v", box value) ]
            (fun r ->
                {
                    Attempt = r.GetString 0
                    UserId = r.GetString 1
                    Login = r.GetString 2
                    DisplayName = r.GetString 3
                    PatientId = r.GetString 4
                    PublicKey = PublicKey(r.GetString 5)
                }
                : Session.Enrolment
            )


    /// The rows an enrolment attempt can touch, as the state holds them: the attempt, every
    /// other attempt of the person it names, that person's live code and their credential.
    let withEnrolment (conn: SqliteConnection) (now: DateTime) (attempt: string) (state: Session.State) =
        match loadEnrolments conn "attempt" attempt |> List.tryHead with
        // the rows are what stands here too: an attempt another server dropped is gone, and
        // must not be continued from what this server still held for it
        | None -> { state with Enrolments = state.Enrolments |> Map.remove attempt }
        | Some e ->
            let attempts = loadEnrolments conn "user" e.UserId

            { state with
                Enrolments = attempts |> List.map (fun a -> a.Attempt, a) |> Map.ofList
                Codes =
                    loadCode conn now e.UserId
                    |> Option.map (fun code -> Map.ofList [ e.UserId, code ])
                    |> Option.defaultValue Map.empty
                Credentials =
                    loadCredential conn e.UserId
                    |> Option.map (fun c -> Map.ofList [ e.UserId, c ])
                    |> Option.defaultValue Map.empty
            }


    /// The credential of the person a Session belongs to, which a signature is checked against.
    let withCredential (conn: SqliteConnection) (userId: string) (state: Session.State) =
        match loadCredential conn userId with
        | None -> { state with Credentials = state.Credentials |> Map.remove userId }
        | Some c -> { state with Credentials = state.Credentials |> Map.add userId c }


    // ---- what a Session holds in flight -------------------------------------------------------

    /// The live data notice of a Session: the newest row within its two minutes. A reading this
    /// release cannot read makes the Session unreadable, as an opened-with does. A notice told
    /// over patient data before the store kept the read is no live notice: it cannot be matched
    /// to any read, so the Session tells a fresh one.
    let loadNotice (conn: SqliteConnection) (now: DateTime) (sid: string) =
        rows
            conn
            """
            select nonce, json_version, data, expiry, ehr_json_version, ehr_data
            from data_notice where session_id = $sid order by id desc limit 1
            """
            [ ("$sid", box sid) ]
            (fun r ->
                let data =
                    match r.IsDBNull 1, r.IsDBNull 2 with
                    | false, false -> readPatient (r.GetInt32 1) (r.GetString 2) |> Result.map Some
                    | _ -> Ok None

                data
                |> Result.bind (fun data -> readEhrColumns r 4 5 |> Result.map (fun ehr -> data, ehr))
                |> Result.map (fun (data, ehr) ->
                    {
                        Nonce = r.GetString 0
                        Ehr = ehr
                        Data = data
                        Expiry = at (r.GetInt64 3)
                    }
                    : Session.Notice
                )
            )
        |> List.tryHead
        |> Option.filter (fun notice ->
            match notice with
            // patient data without the read it was projected from: a row from before the
            // columns, which a read that has since become none would wrongly match
            | Ok n when n.Data.IsSome && n.Ehr.IsNone -> false
            | Ok n -> now <= n.Expiry
            // an unreadable row is answered whatever its lifetime says: the Session ends on it
            | Error _ -> true
        )


    /// The live challenge of a Session: the newest row that is neither spent nor past its
    /// lifetime.
    let loadChallenge (conn: SqliteConnection) (now: DateTime) (sid: string) =
        rows
            conn
            """
            select c.nonce, c.digest, c.json_version, c.reading, c.expiry,
                   exists (select 1 from challenge_spent s where s.challenge_id = c.id),
                   c.ehr_json_version, c.ehr_data
            from challenge c
            where c.session_id = $sid
            order by c.id desc limit 1
            """
            [ ("$sid", box sid) ]
            (fun r ->
                let reading =
                    match r.IsDBNull 2, r.IsDBNull 3 with
                    | false, false -> readPatient (r.GetInt32 2) (r.GetString 3) |> Result.map Some
                    | _ -> Ok None

                let spent = r.GetInt64 5 = 1L

                reading
                |> Result.bind (fun reading -> readEhrColumns r 6 7 |> Result.map (fun ehr -> reading, ehr))
                |> Result.map (fun (reading, ehr) ->
                    spent,
                    ({
                        Nonce = r.GetString 0
                        Digest = r.GetString 1
                        Ehr = ehr
                        Reading = reading
                        Expiry = at (r.GetInt64 4)
                    }
                    : Session.Challenge)
                )
            )
        |> List.tryHead
        |> Option.bind (fun challenge ->
            match challenge with
            | Ok(spent, c) -> if spent || now > c.Expiry then None else Some(Ok c)
            // a row this release cannot read ends the Session, whatever became of it
            | Error reason -> Some(Error reason)
        )


    /// The signing refusal a stored word names, with what the row kept beside it. headOf
    /// rebuilds the head that blocked from the record the caller loaded.
    let signingRefusalOf (headOf: string -> StoredVersion option) (word: string) versionId left until =
        match word with
        | "no-session" -> Some SigningRefusal.NoSession
        | "no-patient" -> Some SigningRefusal.NoPatient
        | "not-prescriber" -> Some SigningRefusal.NotPrescriber
        | "blocked" ->
            versionId
            |> Option.bind headOf
            |> Option.map (StoredVersion.head >> SigningRefusal.Blocked)
        | "stale-token" -> Some SigningRefusal.StaleToken
        | "challenge-mismatch" -> Some SigningRefusal.ChallengeMismatch
        | "challenge-expired" -> Some SigningRefusal.ChallengeExpired
        | "pin-wrong" -> left |> Option.map SigningRefusal.PinWrong
        | "pin-limit" -> Some SigningRefusal.PinLimit
        | "locked" -> until |> Option.map SigningRefusal.Locked
        | "store-failed" -> Some SigningRefusal.StoreFailed
        | "plan-unreadable" -> Some SigningRefusal.PlanUnreadable
        | _ -> None


    /// What the Submission under this key was answered, when it was answered at all. A row the
    /// release cannot rebuild — an answer word it does not know, or a version that is gone —
    /// is no answer, and the Submission is run again, which a signature may do.
    let loadAnswer
        (conn: SqliteConnection)
        (headOf: string -> StoredVersion option)
        (sid: string)
        (key: string)
        : (SigningOutcome * DateTime) option
        =
        rows
            conn
            """
            select answer, version_id, opened_token, attempts_left, locked_until, at
            from submission_answer where session_id = $sid and idem_key = $k
            """
            [ ("$sid", box sid); ("$k", box key) ]
            (fun r ->
                let word = r.GetString 0
                let versionId = textOrNull r 1
                let token = textOrNull r 2
                let left = if r.IsDBNull 3 then None else Some(r.GetInt32 3)
                let until = if r.IsDBNull 4 then None else Some(at (r.GetInt64 4))
                let answeredAt = at (r.GetInt64 5)

                let outcome =
                    if word = "submitted" then
                        // the Session's patient at the answer is not kept: a retry after a lost
                        // answer is answered with the plan just signed
                        match versionId |> Option.bind headOf, token with
                        | Some(StoredVersion.Readable(v, whom)), Some t ->
                            Some(SigningOutcome.Submitted(v, whom, OpenedToken t, v.Plan.Patient))
                        // the version it names cannot be read: there is no answer to repeat
                        | _ -> None
                    elif word.StartsWith "refused:" then
                        signingRefusalOf headOf (word.Substring "refused:".Length) versionId left until
                        |> Option.map SigningOutcome.Refused
                    else
                        None

                outcome |> Option.map (fun outcome -> outcome, answeredAt)
            )
        |> List.tryHead
        |> Option.flatten


    // ---- the rows a request can touch -------------------------------------------------------

    /// <summary>
    /// The record of a patient, as the state holds it. <c>warn</c> hears of every version this
    /// release cannot read, so that a head a sign is refused against says why in the log.
    /// </summary>
    let withRecord (warn: string -> unit) (cs: string) (patientId: string option) (state: Session.State) =
        match patientId with
        | None -> state
        | Some pid ->
            let versions = SqlDatabase.loadRecords cs pid

            for v in versions do
                match v with
                | StoredVersion.Unreadable u ->
                    warn $"order plan version %s{u.Id} (number %i{u.No}) cannot be read: %s{u.Reason}"
                | StoredVersion.Readable _ -> ()

            { state with Records = state.Records |> Map.add pid versions }


    /// A Session and its ending, and the record of the patient it was opened on. A Session
    /// this release cannot read ends as unreadable, which is the ending its next request is
    /// told; the ending is a row of its own, appended by the caller's writes.
    /// The patient a Session was opened on, read before the Session itself so that its record
    /// is in the state when the head it opened with is looked up there.
    let patientOfSession (conn: SqliteConnection) (sid: string) =
        rows
            conn
            "select patient_id from session where session_id = $sid"
            [ ("$sid", box sid) ]
            (fun r -> textOrNull r 0)
        |> List.tryHead
        |> Option.flatten


    /// The versions of every record in the state, the head of a Session looked up among them.
    let headIn (state: Session.State) id =
        state.Records
        |> Map.toSeq
        |> Seq.collect snd
        |> Seq.tryFind (fun v -> StoredVersion.id v = id)


    let withSession
        (warn: string -> unit)
        (cs: string)
        (conn: SqliteConnection)
        (now: DateTime)
        (sid: string)
        (state: Session.State)
        =
        // the rows are what stands: whatever this server still held for the id is dropped
        // first, so that a Session ended elsewhere cannot survive in the memory of a server
        // that opened it
        let state =
            { state with
                Sessions = state.Sessions |> Map.remove sid
                Endings = state.Endings |> Map.remove sid
            }
            // the record first: the Session names the version it opened with by id, and that
            // id is only a version once the patient's rows are there to find it among
            |> withRecord warn cs (patientOfSession conn sid)

        match loadSession conn (headIn state) sid with
        | None
        | Some(Choice2Of2 None) -> state
        | Some(Choice2Of2(Some ending)) -> { state with Endings = state.Endings |> Map.add sid ending }
        | Some(Choice1Of2(Error reason)) ->
            warn $"the Session %s{sid} cannot be read and ends: %s{reason}"

            { state with Endings = state.Endings |> Map.add sid (SessionEnding.Unreadable, now) }
        | Some(Choice1Of2(Ok session)) ->
            // what it holds in flight, read the same way: a notice or a challenge this
            // release cannot read ends the Session, as an opened-with does
            let inFlight (state: Session.State) =
                match loadNotice conn now sid, loadChallenge conn now sid with
                | Some(Error reason), _
                | _, Some(Error reason) ->
                    warn $"the Session %s{sid} cannot be read and ends: %s{reason}"

                    { state with
                        Sessions = state.Sessions |> Map.remove sid
                        Endings = state.Endings |> Map.add sid (SessionEnding.Unreadable, now)
                    }
                | notice, challenge ->
                    { state with
                        Notices =
                            notice
                            |> Option.bind Result.toOption
                            |> Option.map (fun n -> state.Notices |> Map.add sid n)
                            |> Option.defaultValue (state.Notices |> Map.remove sid)
                        Challenges =
                            challenge
                            |> Option.bind Result.toOption
                            |> Option.map (fun c -> state.Challenges |> Map.add sid c)
                            |> Option.defaultValue (state.Challenges |> Map.remove sid)
                    }

            { state with Sessions = state.Sessions |> Map.add sid session }
            // the credential of the person it belongs to, which its next signature is checked
            // against, and which the wrong-PIN count and the lock live on
            |> match session.Opened.User with
               | Some user -> withCredential conn user.UserId
               | None -> id
            |> inFlight


    /// The id of the newest Session of a login, whatever became of it: the row the loader
    /// reads a supersession off, and the one an open supersedes in turn.
    let newestOfLogin (conn: SqliteConnection) (login: string) =
        rows
            conn
            "select session_id from session where login = $login order by id desc limit 1"
            [ ("$login", box login) ]
            (fun r -> r.GetString 0)
        |> List.tryHead


    /// <summary>
    /// What a Session was opened with, whatever became of it since. A Launch that opened a
    /// Session keeps that answer for its lifetime, so that a browser presenting it again is
    /// sent to the app rather than through the hop a second time, and the cookie decides which
    /// Session it lands on. <c>loadSession</c> answers an ended Session with its ending and no
    /// <c>OpenedSession</c> at all, so the launch's outcome is rebuilt from the rows directly.
    /// </summary>
    let openedOf (conn: SqliteConnection) (headOf: string -> StoredVersion option) (sid: string) =
        rows
            conn
            "select user_id, user_display, user_role, patient_id, key_thumbprint from session where session_id = $sid"
            [ ("$sid", box sid) ]
            (fun r ->
                {|
                    UserId = textOrNull r 0
                    UserDisplay = textOrNull r 1
                    UserRole = textOrNull r 2
                    PatientId = textOrNull r 3
                    KeyThumbprint = textOrNull r 4
                |}
            )
        |> List.tryHead
        |> Option.map (fun row ->
            let opened = loadOpenedWith conn sid

            {
                User =
                    userOf row.UserId row.UserDisplay row.UserRole
                    |> Result.toOption
                    |> Option.flatten
                PatientId = row.PatientId
                EhrData = opened |> Option.bind (fun o -> o.EhrData |> Result.toOption |> Option.flatten)
                Patient = opened |> Option.bind (fun o -> o.Patient |> Result.toOption |> Option.flatten)
                Measured =
                    loadMeasurements conn sid
                    |> Result.toOption
                    |> Option.defaultValue Measurements.none
                OpenedToken = opened |> Option.bind _.OpenedToken
                KeyThumbprint = row.KeyThumbprint
                Head = opened |> Option.bind _.HeadId |> Option.bind headOf
            }
        )


    /// The newest Session of a login, so that an open sees the one it supersedes, and the
    /// ending of that Session when it has one.
    let withLogin warn (cs: string) (conn: SqliteConnection) (now: DateTime) (login: string) (state: Session.State) =
        // the login's rows are what stands: every Session this server still held for it goes
        // first, so that one superseded or ended elsewhere cannot be found by its own id
        let held =
            state.Sessions
            |> Map.toSeq
            |> Seq.filter (fun (_, s) -> s.Login = Some login)
            |> Seq.map fst
            |> Seq.toList

        let state =
            { state with
                Sessions = state.Sessions |> Map.filter (fun sid _ -> held |> List.contains sid |> not)
                Endings = state.Endings |> Map.filter (fun sid _ -> held |> List.contains sid |> not)
            }

        newestOfLogin conn login
        |> Option.map (fun sid -> withSession warn cs conn now sid state)
        |> Option.defaultValue state


    /// The Launch of a nonce or of a callback's state, with what its callback came to, and the
    /// record of the patient it was launched on.
    let withLaunch
        warn
        (cs: string)
        (conn: SqliteConnection)
        (now: DateTime)
        (by: string)
        (value: string)
        (state: Session.State)
        =
        // the rows are what stands here too: a Launch past its lifetime, or one another server
        // dropped, is gone from the state before the database is asked, so that nothing reads
        // a record the loader itself calls absent
        let state =
            { state with
                Launches =
                    state.Launches
                    |> Map.filter (fun nonce r -> (if by = "nonce" then nonce else r.State) <> value)
            }

        match loadLaunch conn now by value with
        | None -> state
        | Some row ->
            let state = state |> withRecord warn cs (Some row.PatientId)

            let headOf id =
                state.Records
                |> Map.toSeq
                |> Seq.collect snd
                |> Seq.tryFind (fun v -> StoredVersion.id v = id)

            let outcome =
                match row.Outcome, row.SessionId, row.Attempt with
                | Some "opened", Some sid, _ ->
                    openedOf conn headOf sid
                    |> Option.map (fun opened -> LaunchResult.Opened(sid, opened))
                | Some "enrolling", _, Some attempt -> Some(LaunchResult.Enrolling attempt)
                | Some word, _, _ when word.StartsWith "refused:" ->
                    word.Substring "refused:".Length |> refusalOf |> Option.map LaunchResult.Refused
                | _ -> None

            let record: Session.LaunchRecord =
                {
                    Nonce = row.Nonce
                    State = row.State
                    PatientId = row.PatientId
                    PublicKey = row.PublicKey
                    Expiry = row.Expiry
                    Outcome = outcome
                }

            let state = { state with Launches = state.Launches |> Map.add row.Nonce record }

            // the Session it opened, so that a callback reloaded against it, and a request that
            // follows, see it as the state holds it
            match row.SessionId with
            | Some sid -> withSession warn cs conn now sid state
            | None -> state


    /// <summary>
    /// The rows a slice names, read into the state a request runs over. Every load is a fresh
    /// connection: the rows a request works on are the rows as they stand when it starts, and
    /// the transaction it writes in is its own.
    /// </summary>
    let load (warn: string -> unit) (cs: string) (now: unit -> DateTime) (slice: StubDatabase.Slice) state =
        // a request with no rows to read never opens the file: a Launch whose seal does not
        // verify is refused as invalid whatever the database is doing
        let withConnection f =
            use conn = new SqliteConnection(cs)
            conn.Open()
            f conn

        match slice with
        | StubDatabase.Slice.Nothing -> state
        | StubDatabase.Slice.LaunchNonce nonce ->
            withConnection (fun conn -> withLaunch warn cs conn (now ()) "nonce" nonce state)
        | StubDatabase.Slice.LaunchState value ->
            withConnection (fun conn -> withLaunch warn cs conn (now ()) "state" value state)
        | StubDatabase.Slice.Login login -> withConnection (fun conn -> withLogin warn cs conn (now ()) login state)
        | StubDatabase.Slice.Session sid -> withConnection (fun conn -> withSession warn cs conn (now ()) sid state)
        | StubDatabase.Slice.Submission(sid, key) ->
            withConnection (fun conn ->
                let state = withSession warn cs conn (now ()) sid state

                let headOf id =
                    state.Records
                    |> Map.toSeq
                    |> Seq.collect snd
                    |> Seq.tryFind (fun v -> StoredVersion.id v = id)

                // the answer this key was already given, when it was given one at all
                match loadAnswer conn headOf sid key with
                | Some answer -> { state with Answered = state.Answered |> Map.add (sid, key) answer }
                | None -> { state with Answered = state.Answered |> Map.remove (sid, key) }
            )
        // the attempt, every other attempt of the person it names, their code and their
        // credential, and the record of the patient an open out of it would show
        | StubDatabase.Slice.Enrolment attempt ->
            withConnection (fun conn ->
                let state = withEnrolment conn (now ()) attempt state

                state.Enrolments
                |> Map.tryFind attempt
                |> Option.map _.PatientId
                |> fun pid -> withRecord warn cs pid state
            )


    /// <summary>
    /// The session state in the database: a load reads the rows a slice names, a write appends
    /// what a request did, in one transaction. <c>warn</c> hears of every row the release cannot
    /// read; a load that throws is rethrown, which the port answers as the store failing.
    /// </summary>
    let store (warn: string -> unit) (cs: string) (now: unit -> DateTime) : StubDatabase.SessionStore =
        {
            load =
                fun slice s ->
                    try
                        load warn cs now slice s
                    with e ->
                        warn $"the session store could not be read: %s{e.Message}"
                        reraise ()
            persist = runWrites cs now
        }


    /// <summary>
    /// The credentials the demo walkthrough needs, written once per login: a login that already
    /// has a credential event is left alone, so a restart adds no row and never resets a PIN the
    /// User changed. Demo servers only; production has no seed and refuses the key today.
    /// </summary>
    let seed (cs: string) (now: DateTime) (credentials: Map<string, Credential>) =
        try
            use conn = new SqliteConnection(cs)
            conn.Open()
            use tx = conn.BeginTransaction()

            try
                for login, credential in credentials |> Map.toList do
                    // one statement asks and writes: a credential another server set between a
                    // question and an answer of ours would otherwise be replaced by the demo
                    // PIN, and the newest event is the one that signs
                    exec
                        conn
                        tx
                        """
                        insert into credential_event
                            (user_id, event, pin_salt, pin_hash, wrong_count, locked_until, at)
                        select $u, 'seeded', $salt, $hash, $wrong, null, $at
                        where not exists (select 1 from credential_event where user_id = $u)
                        """
                        [
                            "$u", box login
                            "$salt", nullable (credential.PinHash |> Option.map _.Salt)
                            "$hash", nullable (credential.PinHash |> Option.map _.Hash)
                            "$wrong", box credential.WrongCount
                            "$at", box (ms now)
                        ]

                tx.Commit()
                Session.StoreOutcome.Written
            with e ->
                tx.Rollback()
                Session.StoreOutcome.Failed e.Message
        with e ->
            Session.StoreOutcome.Failed e.Message


    /// The session port over the state in the database.
    let makeSessionPort warn cs now = StubDatabase.makeSessionPortWith (store warn cs now)
