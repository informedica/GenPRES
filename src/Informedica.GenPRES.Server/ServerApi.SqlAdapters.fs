namespace ServerApi

// The SQLite session store for tests and development, beside the in-memory store in ServerApi.StubAdapters.fs.
// Production never opens it. The SQL schema lives in the Sql/*.sql scripts embedded in this assembly.

// An alias for the BirthDate module, which has the same name as a union case.
module CoreBirthDate = Informedica.GenCore.Lib.Patients.BirthDate


/// <summary>
/// Applies the SQL schema of the session store as numbered migrations, one script <c>Sql/NNN-name.sql</c>
/// each. Each applied migration is recorded in <c>schema_version</c>.
/// </summary>
module SqlSchema =

    open System
    open System.IO
    open System.Reflection
    open Microsoft.Data.Sqlite


    /// A migration script with its number, name and SQL.
    type Migration =
        {
            Number: int
            Name: string
            Sql: string
        }


    /// The folder, in resource names and in the source tree, that holds the migrations.
    let prefix = "Sql/"


    /// Reads the migration number from the leading integer of the file name. Returns an error when it is
    /// not a positive number.
    let numberOf (name: string) =
        let file = Path.GetFileName name
        let digits = file |> Seq.takeWhile Char.IsDigit |> Seq.toArray |> String

        match Int32.TryParse digits with
        | true, n when n > 0 -> Ok n
        | _ -> Error $"the migration %s{name} does not start with a positive number"


    /// <summary>
    /// Makes the migrations of named scripts, ordered by name with an ordinal comparison, so zero-padded
    /// numbers sort as numbers. Raises InvalidOperationException when a name does not start with a positive
    /// number or the numbers do not rise strictly.
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
    /// Applies every migration above the highest number in <c>schema_version</c> and returns the applied
    /// numbers in order. Each migration runs in one transaction with its <c>schema_version</c> row and reads
    /// the highest number inside it, so a migration another process applied meanwhile is skipped. A failing
    /// script rolls back its migration and raises.
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


/// Reads and writes a stored EHR patient as JSON with its own structure version.
module SqlEhrPatient =

    module Canonical = Informedica.GenOrder.Lib.Canonical
    module CorePatient = Informedica.GenCore.Lib.Patients.Patient


    /// The JSON structure version of stored EHR data: the GenCORE patient Dto. Version 1 is not converted.
    let ehrPatientJsonVersion = 2


    /// Returns the JSON when the version lies from oldest to newest, an error otherwise.
    let checkVersion oldest newest (version: int) (json: string) : Result<string, string> =
        if version < oldest then
            Error $"JSON structure version %i{version} is older than this release reads"
        elif version > newest then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


    /// Parses stored EHR data on date today. Returns an error for an unsupported version, unreadable JSON
    /// or invalid data, such as a weight or height dated after today.
    let readEhrPatient (today: System.DateTime) (version: int) (json: string) : Result<CorePatient, string> =
        checkVersion ehrPatientJsonVersion ehrPatientJsonVersion version json
        |> Result.bind (fun json ->
            try
                json
                |> Canonical.deserialize<CorePatient.Dto.Dto>
                |> CorePatient.Dto.fromDto today None
                |> Result.mapError (fun errs -> $"the EHR data does not parse: %A{errs}")
            with e ->
                Error $"the JSON does not read: %s{e.Message}"
        )


    /// Reads EHR data from its version and JSON columns. Both null gives None; only one null gives an error.
    let readEhrPatientColumns today version json =
        match version, json with
        | Some version, Some json -> readEhrPatient today version json |> Result.map Some
        | None, None -> Ok None
        | _ -> Error "the EHR data and its JSON structure version are not both present"


    /// The JSON of EHR data.
    let ehrPatientJson (ehr: CorePatient) = ehr |> CorePatient.Dto.toDto |> Canonical.serialize


/// Loads the order plan versions of a patient from the order_plan table and inserts new versions. The order
/// plan and the EHR data are stored as JSON, each with a structure version. A row that cannot be read loads
/// as an unreadable version and is not skipped.
module SqlDatabase =

    open System
    open System.IO
    open Microsoft.Data.Sqlite
    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.BCL


    /// The JSON structure version this release reads and writes for the order plan of a version.
    let jsonVersion = 17


    /// Checks that this release reads plan JSON of the structure version. An older version is not
    /// converted and loads as unreadable.
    let upgrade version json = SqlEhrPatient.checkVersion jsonVersion jsonVersion version json


    /// The JSON of the order plan of a version.
    let toJson (v: OrderPlanVersion) = v.Plan |> OrderPlan.Dto.toDto |> Canonical.serialize


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
            Verified: bool
            JsonVersion: int
            Plan: string
            EhrJsonVersion: int option
            EhrData: string option
            PatientName: string option
            BirthYear: int64 option
            BirthMonth: int64 option
            BirthDay: int64 option
        }


    /// Reads a stored version from a row and parses its order plan and EHR data from JSON. A row with any
    /// part that cannot be read gives an unreadable version with every reason. The patient identity comes
    /// from the columns in both cases.
    let readRow (today: DateTime) (row: Row) : StoredVersion =
        let identity: Result<Informedica.GenCore.Lib.Patients.PatientIdentity option, string> =
            match row.PatientName, row.BirthYear, row.BirthMonth, row.BirthDay with
            | Some name, _, _, _ when name |> String.isNullOrWhiteSpace -> Error "the patient's name is empty"
            | Some name, Some y, Some m, Some d ->
                try
                    let date = DateTime(Checked.int y, Checked.int m, Checked.int d)

                    Ok(
                        Some
                            {
                                Id = row.PatientId
                                Name = name
                                BirthDate =
                                    CoreBirthDate.create
                                        (Informedica.GenCore.Lib.Conversions.yearFromInt date.Year)
                                        (Informedica.GenCore.Lib.Conversions.monthFromInt date.Month)
                                        (Informedica.GenCore.Lib.Conversions.dayFromInt date.Day)
                            }
                    )
                with
                | :? ArgumentOutOfRangeException
                | :? OverflowException -> Error $"the birthdate %i{y}-%i{m}-%i{d} is no date"
            | None, None, None, None -> Ok None
            | _ -> Error "the patient's name and birthdate are not all present"

        let plan =
            upgrade row.JsonVersion row.Plan
            |> Result.bind (fun json ->
                try
                    json
                    |> Canonical.deserialize<OrderPlan.Dto.Dto>
                    |> OrderPlan.Dto.fromDto today
                    |> Result.mapError (fun errs -> $"the order plan does not parse: %A{errs}")
                // Catch every exception: a JSON converter throws its own exception type on a malformed value.
                with e ->
                    Error $"the JSON does not read: %s{e.Message}"
            )

        let ehr = SqlEhrPatient.readEhrPatientColumns today row.EhrJsonVersion row.EhrData

        // A signed_at outside the DateTime range makes the row unreadable; the version then gets DateTime.MinValue.
        let signedAt =
            try
                Ok(DateTimeOffset.FromUnixTimeMilliseconds(row.SignedAt).UtcDateTime)
            with :? ArgumentOutOfRangeException ->
                Error $"signed_at %i{row.SignedAt} is out of range"

        let signer: Signer =
            {
                UserId = row.SignedByUserId
                DisplayName = row.SignedByDisplayName
            }

        match plan, ehr, identity, signedAt with
        | Ok plan, Ok ehr, Ok whom, Ok signedAt ->
            StoredVersion.Readable(
                {
                    Id = row.VersionId
                    No = row.No
                    PatientId = row.PatientId
                    Base = row.Base
                    SignedBy = signer
                    SignedAt = signedAt
                    Plan = plan
                    Verified = row.Verified
                    EhrPatient = ehr
                },
                whom
            )
        | _ ->
            let reasonOf r =
                match r with
                | Error reason -> Some reason
                | Ok _ -> None

            StoredVersion.Unreadable
                {
                    Id = row.VersionId
                    No = row.No
                    PatientId = row.PatientId
                    Base = row.Base
                    SignedBy = signer
                    SignedAt = signedAt |> Result.defaultValue DateTime.MinValue
                    Identity = identity |> Result.toOption |> Option.flatten
                    Reason =
                        [ reasonOf plan; reasonOf ehr; reasonOf identity; reasonOf signedAt ]
                        |> List.choose id
                        |> String.concat "; "
                }


    /// Every order plan version of a patient, newest first.
    let loadRecords (today: DateTime) (connectionString: string) (patientId: string) : StoredVersion list =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        use cmd = conn.CreateCommand()

        cmd.CommandText <-
            """
            select version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                   signed_at, verified, json_version, plan, ehr_json_version, ehr_data,
                   patient_name, birth_year, birth_month, birth_day
            from order_plan where patient_id = $patient order by no desc
            """

        cmd.Parameters.AddWithValue("$patient", patientId) |> ignore
        use r = cmd.ExecuteReader()

        let text i = if r.IsDBNull i then None else Some(r.GetString i)
        let whole i = if r.IsDBNull i then None else Some(r.GetInt64 i)
        let number i = if r.IsDBNull i then None else Some(r.GetInt32 i)

        [
            while r.Read() do
                readRow
                    today
                    {
                        VersionId = r.GetString 0
                        No = r.GetInt32 1
                        PatientId = r.GetString 2
                        Base = text 3
                        SignedByUserId = r.GetString 4
                        SignedByDisplayName = r.GetString 5
                        SignedAt = r.GetInt64 6
                        Verified = r.GetInt32 7 = 1
                        JsonVersion = r.GetInt32 8
                        Plan = r.GetString 9
                        EhrJsonVersion = number 10
                        EhrData = text 11
                        PatientName = text 12
                        BirthYear = whole 13
                        BirthMonth = whole 14
                        BirthDay = whole 15
                    }
        ]


    /// Inserts a version in the transaction given: its fields, the order plan and the EHR data as
    /// JSON, and the identity of the patient.
    let insertVersion
        (conn: SqliteConnection)
        (tx: SqliteTransaction)
        (v: OrderPlanVersion)
        (whom: Informedica.GenCore.Lib.Patients.PatientIdentity option)
        =
        use cmd = conn.CreateCommand()
        cmd.Transaction <- tx

        cmd.CommandText <-
            """
            insert into order_plan
                (version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                 signed_at, verified, json_version, plan, ehr_json_version, ehr_data,
                 patient_name, birth_year, birth_month, birth_day)
            values ($id, $no, $patient, $base, $user, $name, $at, $verified, $json_version, $plan,
                    $ehr_json_version, $ehr_data, $patient_name, $birth_year, $birth_month, $birth_day)
            """

        let add (name: string) (value: obj) = cmd.Parameters.AddWithValue(name, value) |> ignore

        let orNull (value: 'a option) =
            value |> Option.map box |> Option.defaultValue (box DBNull.Value)

        add "$id" v.Id
        add "$no" v.No
        add "$patient" v.PatientId
        add "$base" (v.Base |> orNull)
        add "$user" v.SignedBy.UserId
        add "$name" v.SignedBy.DisplayName
        add "$at" (DateTimeOffset(v.SignedAt, TimeSpan.Zero).ToUnixTimeMilliseconds())
        add "$verified" (if v.Verified then 1 else 0)
        add "$json_version" jsonVersion
        add "$plan" (toJson v)
        add
            "$ehr_json_version"
            (v.EhrPatient
             |> Option.map (fun _ -> SqlEhrPatient.ehrPatientJsonVersion)
             |> orNull)
        add "$ehr_data" (v.EhrPatient |> Option.map SqlEhrPatient.ehrPatientJson |> orNull)
        add "$patient_name" (whom |> Option.map _.Name |> orNull)
        add "$birth_year" (whom |> Option.map (fun w -> int w.BirthDate.Year) |> orNull)
        add "$birth_month" (whom |> Option.map (fun w -> int w.BirthDate.Month) |> orNull)
        add "$birth_day" (whom |> Option.map (fun w -> int w.BirthDate.Day) |> orNull)
        cmd.ExecuteNonQuery() |> ignore


    let private insert (connectionString: string) (v: OrderPlanVersion, whom) =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        insertVersion conn null v whom


    /// True when a failure is another server's sign of the same order plan version number.
    let isSameNumber (e: SqliteException) =
        e.SqliteExtendedErrorCode = 2067
        && e.Message.Contains "order_plan.patient_id, order_plan.no"


    /// <summary>
    /// Inserts an order plan version. A violated <c>unique (patient_id, no)</c> means another server wrote
    /// the same number and returns <c>Conflict</c> with the current head. Any other failure returns
    /// <c>Failed</c> with the reason, so the caller keeps its state and the next Submission retries.
    /// </summary>
    let persistVersion
        (today: DateTime)
        (connectionString: string)
        (v: OrderPlanVersion, whom: Informedica.GenCore.Lib.Patients.PatientIdentity option)
        : Session.StoreOutcome
        =
        try
            insert connectionString (v, whom)
            Session.StoreOutcome.Written
        with
        | :? SqliteException as e when isSameNumber e ->
            try
                match loadRecords today connectionString v.PatientId with
                | head :: _ -> Session.StoreOutcome.Conflict head
                | [] -> Session.StoreOutcome.Failed e.Message
            with reread ->
                Session.StoreOutcome.Failed reread.Message
        | e -> Session.StoreOutcome.Failed e.Message


    /// <summary>
    /// Writes only the order plan versions of the list, each on its own, and ignores the other writes.
    /// Stops at the first write that is not <c>Written</c> and returns its outcome.
    /// </summary>
    let persist (today: DateTime) (connectionString: string) (writes: Session.Persist list) : Session.StoreOutcome =
        writes
        |> List.choose (
            function
            | Session.WriteVersion(v, whom) -> Some(v, whom)
            | _ -> None
        )
        |> List.fold
            (fun outcome v ->
                match outcome with
                | Session.StoreOutcome.Written -> persistVersion today connectionString v
                | refused -> refused
            )
            Session.StoreOutcome.Written


    /// <summary>
    /// Roots a relative data source at <c>root</c> and creates the folder of the file when it is missing,
    /// because SQLite creates the file but not its folder.
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
/// Loads the launches, Sessions and endings a request needs into its state, and appends the writes of the
/// request in one transaction. Rows are only inserted, never updated. A supersession has no row: a newer
/// Session of the same login shows it.
/// </summary>
module SqlSessions =

    open System
    open Microsoft.Data.Sqlite
    open Informedica.GenPRES.Shared.Types
    // Opens Canonical, the serializer of the stored Dtos.
    open Informedica.GenOrder.Lib
    open SqlEhrPatient

    module GenForm = Informedica.GenForm.Lib.Types
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    /// Converts a UTC time to the Unix milliseconds the columns hold; at converts back.
    let ms (at: DateTime) = DateTimeOffset(at, TimeSpan.Zero).ToUnixTimeMilliseconds()

    let at (ms: int64) = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime


    /// Returns the launch refusal stored under the word, or None for an unknown word. The client reads the
    /// same word from the URL.
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


    /// Returns the word a Role is stored under.
    let roleWord =
        function
        | UserRole.Prescriber -> "prescriber"
        | UserRole.Reader -> "reader"


    /// Returns the Role stored under the word, or None for an unknown word. The column is free text, and
    /// an unknown word must never become the Prescriber role that may sign.
    let roleOf word =
        if word = roleWord UserRole.Prescriber then
            Some UserRole.Prescriber
        elif word = roleWord UserRole.Reader then
            Some UserRole.Reader
        else
            None


    /// Makes a command with parameters on a connection, in the transaction when one is given.
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
    /// Loads the Launch with the nonce, or with the callback <c>state</c> when by is not "nonce", and its
    /// outcome. A Launch past its expiry loads as None. The caller rebuilds the Session an outcome names.
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


    /// The JSON structure version of the patient data of a data notice. Version 2 is the GenCORE
    /// patient data. Version 1 is not converted.
    let patientDataJsonVersion = 2


    /// Reads the patient data of a stored row on a date. A weight or height dated after that date
    /// is an error.
    let readPatientData today (version: int) (json: string) : Result<CorePatientData, string> =
        checkVersion patientDataJsonVersion patientDataJsonVersion version json
        |> Result.bind (fun json ->
            try
                json
                |> Canonical.deserialize<CorePatientData.Dto.Dto>
                |> CorePatientData.Dto.fromDto today None
                |> Result.mapError (fun errs -> $"the patient data does not parse: %A{errs}")
            // Catch every exception: a JSON converter throws its own exception type on a malformed value.
            with e ->
                Error $"the JSON does not read: %s{e.Message}"
        )


    /// Reads the EHR data of a row from the version and JSON columns at the given indexes. Both null
    /// gives None; only one null gives an error.
    let readEhrPatientColumns (today: DateTime) (r: SqliteDataReader) (version: int) (json: int) =
        SqlEhrPatient.readEhrPatientColumns
            today
            (if r.IsDBNull version then
                 None
             else
                 Some(r.GetInt32 version))
            (if r.IsDBNull json then None else Some(r.GetString json))


    /// Returns the word a Session act is stored under.
    let actWord act =
        match act with
        | SessionAct.Opened -> "opened"
        | SessionAct.VersionOpened -> "version"
        | SessionAct.Refreshed -> "refresh"
        | SessionAct.Signed -> "signed"


    /// Reads a Session act from its stored word. Returns an error for an unknown word.
    let actOf word =
        match word with
        | "opened" -> Ok SessionAct.Opened
        | "version" -> Ok SessionAct.VersionOpened
        | "refresh" -> Ok SessionAct.Refreshed
        | "signed" -> Ok SessionAct.Signed
        | other -> Error $"the act %s{other} is not known to this release"


    /// Loads the newest opened-with row of a Session: the act, the opened token, the version and head ids
    /// and the EHR data. An act or EHR data that cannot be read is an error, which makes the Session
    /// unreadable.
    let loadOpenedWith (conn: SqliteConnection) (today: DateTime) (sid: string) =
        rows
            conn
            """
            select version_id, head_id, opened_token, act, ehr_json_version, ehr_data
            from session_opened_with where session_id = $sid order by id desc limit 1
            """
            [ "$sid", box sid ]
            (fun r ->
                {|
                    VersionId = textOrNull r 0
                    HeadId = textOrNull r 1
                    OpenedToken = textOrNull r 2 |> Option.map OpenedToken
                    Act = r.GetString 3 |> actOf
                    EhrPatient = readEhrPatientColumns today r 4 5
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


    /// Loads the ending of a Session and whether the Session is gone. The ending is a stored ending, or a
    /// supersession when the login has a newer Session, even one that has since closed. A closed or
    /// acknowledged Session is gone and has no ending.
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
            // The User was told about the ending and acknowledged it, so the Session is gone and the
            // ending is not told again.
            None, true
        else
            match ending with
            | Some("closed", _) -> None, true
            | Some("wrong-pin-limit", at) -> Some(SessionEnding.WrongPinLimit, at), false
            | Some("unreadable", at) -> Some(SessionEnding.Unreadable, at), false
            | Some("idle", at) -> Some(SessionEnding.Idle, at), false
            // The column is free text: an unknown word still ends the Session, as unreadable.
            | Some(_, at) -> Some(SessionEnding.Unreadable, at), false
            | None -> superseded, false


    /// Reads the User of a session row. A Session opened without a launch has no User. An unknown Role or
    /// a partly filled User is an error.
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


    /// Loads the Session with the id and finds its head with headOf. Returns Choice1Of2 with the Session, or
    /// with the error when it cannot be read; Choice2Of2 with the ending of an ended Session, or None for a
    /// closed or acknowledged one. Returns None when there is no row.
    let loadSession (conn: SqliteConnection) (today: DateTime) (headOf: string -> StoredVersion option) (sid: string) =
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
                let opened = loadOpenedWith conn today sid

                let read =
                    userOf row.UserId row.UserDisplay row.UserRole
                    |> Result.bind (fun user ->
                        opened
                        |> Option.map _.Act
                        |> Option.defaultValue (Ok SessionAct.Opened)
                        |> Result.bind (fun act ->
                            opened
                            |> Option.map _.EhrPatient
                            |> Option.defaultValue (Ok None)
                            |> Result.map (fun ehr -> user, act, ehr)
                        )
                    )

                read
                |> Result.map (fun (user, act, ehr) ->
                    let session: Session.SessionRecord =
                        {
                            Opened =
                                {
                                    User = user
                                    PatientId = row.PatientId
                                    EhrPatient = ehr
                                    OpenedToken = opened |> Option.bind _.OpenedToken
                                    KeyThumbprint = row.KeyThumbprint
                                    Head = opened |> Option.bind _.HeadId |> Option.bind headOf
                                    Act = act
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


    /// The JSON of the patient data of a data notice.
    let patientDataJson patientData = patientData |> CorePatientData.Dto.toDto |> Canonical.serialize


    /// Returns the word an ending is stored under. A supersession is not stored, because a newer Session
    /// shows it, and an inactive tab is not stored, because only the tab is told.
    let endingWord =
        function
        | Session.StoredEnding.Closed -> "closed"
        | Session.StoredEnding.Ended SessionEnding.WrongPinLimit -> "wrong-pin-limit"
        | Session.StoredEnding.Ended SessionEnding.Unreadable -> "unreadable"
        | Session.StoredEnding.Ended SessionEnding.Idle -> "idle"
        | Session.StoredEnding.Ended SessionEnding.SupersededByLaunch -> "superseded"
        | Session.StoredEnding.Ended SessionEnding.InactiveTab -> "inactive-tab"


    let exec (conn: SqliteConnection) (tx: SqliteTransaction) sql parameters =
        use cmd = command conn tx sql parameters
        cmd.ExecuteNonQuery() |> ignore


    let nullable (value: 'a option) =
        value |> Option.map box |> Option.defaultValue (box DBNull.Value)


    /// Returns the word a signing refusal is stored under, with the id of the blocking head, the attempts
    /// left and the lock time when the refusal has one. The blocking head is an order_plan row.
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


    /// Inserts the rows of one write in the transaction.
    let run (conn: SqliteConnection) (tx: SqliteTransaction) (write: Session.Persist) =
        match write with
        | Session.WriteVersion(v, whom) -> SqlDatabase.insertVersion conn tx v whom
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
                // A redirect is the answer before a launch has an outcome, so it is never stored.
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
                    (session_id, act, version_id, head_id, opened_token, ehr_json_version, ehr_data, at)
                values ($sid, $act, $version, $head, $token, $ejv, $ehr, $at)
                """
                [
                    "$sid", box sid
                    "$act", box (actWord session.Opened.Act)
                    "$version", nullable session.OpenedWith
                    "$head", nullable (session.Opened.Head |> Option.map StoredVersion.id)
                    "$token", nullable token
                    "$ejv", nullable (session.Opened.EhrPatient |> Option.map (fun _ -> ehrPatientJsonVersion))
                    "$ehr", nullable (session.Opened.EhrPatient |> Option.map ehrPatientJson)
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
        // A try and a spend name the code by its mac, because another server may have mailed a newer
        // code meanwhile and a try of the older code must not void it. A spent code matches nothing,
        // so nothing is written.
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
        // Dropping a dropped attempt is ignored, because one request can drop the same attempt twice.
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
                    "$jv", nullable (notice.PatientData |> Option.map (fun _ -> patientDataJsonVersion))
                    "$data", nullable (notice.PatientData |> Option.map patientDataJson)
                    "$ejv", nullable (notice.EhrPatient |> Option.map (fun _ -> ehrPatientJsonVersion))
                    "$ehr", nullable (notice.EhrPatient |> Option.map ehrPatientJson)
                    "$e", box (ms notice.Expiry)
                    "$at", box (ms at)
                ]
        | Session.WriteChallenge(sid, challenge, at) ->
            exec
                conn
                tx
                """
                insert into challenge
                    (session_id, nonce, digest, ehr_json_version, ehr_data, expiry, at)
                values ($sid, $n, $d, $ejv, $ehr, $e, $at)
                """
                [
                    "$sid", box sid
                    "$n", box challenge.Nonce
                    "$d", box challenge.Digest
                    "$ejv", nullable (challenge.EhrPatient |> Option.map (fun _ -> ehrPatientJsonVersion))
                    "$ehr", nullable (challenge.EhrPatient |> Option.map ehrPatientJson)
                    "$e", box (ms challenge.Expiry)
                    "$at", box (ms at)
                ]
        // The commit names its challenge by nonce, because another server may have issued a newer
        // challenge that the User is answering.
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
                // A challenge and a notice answer the challenge request, which stores no answer.
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


    /// One audit line: the time, the Session, the actor, the action, the outcome and an optional JSON detail.
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


    /// Quotes and escapes a string as a JSON literal. The detail of an entry is JSON, and a patient id or
    /// an attempt comes from the launch, so no value is inserted raw.
    let jsonText (text: string) = Newtonsoft.Json.JsonConvert.ToString text


    /// <summary>
    /// Returns the Session id and the user id a write names, when it names them. <c>auditOf</c> adds them to
    /// the entries of the request.
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
        | Session.RememberAnswer(sid, _, _, _) -> Some sid, None
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
    /// Returns the audit entry of one write, or None when another write audits the act. <c>auditOf</c> fills
    /// in the Session and the actor from the other writes of the request.
    /// </summary>
    let entryOf (now: DateTime) (write: Session.Persist) : AuditEntry option =
        match write with
        // The launch record holds the expiry, not the creation time, so the entry gets the request time.
        | Session.RecordLaunch r ->
            Some { auditEntry now "launched" "ok" with Detail = Some $"""{{"patient":%s{jsonText r.PatientId}}}""" }
        | Session.RecordLaunchOutcome(_, outcome, at) ->
            match outcome with
            // The OpenSession write of the same request audits the open.
            | LaunchResult.Opened _ -> None
            | LaunchResult.Refused refusal ->
                Some
                    { auditEntry at "launch" "refused" with
                        Detail = Some $"""{{"refusal":%s{jsonText (Session.refusalWord refusal)}}}"""
                    }
            | LaunchResult.Enrolling attempt ->
                Some { auditEntry at "enrolling" "ok" with Detail = Some $"""{{"attempt":%s{jsonText attempt}}}""" }
            // A redirect is the answer before a launch has an outcome.
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
        | Session.RememberAnswer(sid, _, outcome, at) ->
            match outcome with
            // The WriteVersion write audits the signature.
            | SigningOutcome.Submitted _ -> None
            | SigningOutcome.Refused refusal ->
                let word, _, _, _ = refusalRow refusal

                Some
                    { auditEntry at "sign" "refused" with
                        SessionId = Some sid
                        Detail = Some $"""{{"refusal":%s{jsonText word}}}"""
                    }
            // A challenge and a notice answer the challenge request, which stores no answer.
            | _ -> None
        // These writes record part of an act that another write or auditOf already audits.
        | Session.RecordOpenedWith _
        | Session.RecordSeen _
        | Session.AcknowledgeEnding _
        | Session.SpendCode _
        | Session.WriteEnrolment _
        | Session.DropEnrolmentWrite _
        | Session.DropEnrolmentsOf _
        | Session.SpendChallenge _ -> None


    /// <summary>
    /// Returns the audit entries of a request from its writes. An entry without a Session id or an actor
    /// gets the first one that a write of the request names.
    /// </summary>
    let auditOf (now: DateTime) (writes: Session.Persist list) : AuditEntry list =
        let named = writes |> List.map namedBy

        // A version opened and a refresh have no write of their own; the act of the RecordOpenedWith
        // write shows them. An open and a signature are audited by their own writes.
        let reopened =
            writes
            |> List.choose (
                function
                | Session.RecordOpenedWith(_, session, _) ->
                    match session.Opened.Act with
                    | SessionAct.VersionOpened -> Some(auditEntry now "version-opened" "ok")
                    | SessionAct.Refreshed -> Some(auditEntry now "session-refreshed" "ok")
                    | SessionAct.Opened
                    | SessionAct.Signed -> None
                | _ -> None
            )

        reopened @ (writes |> List.choose (entryOf now))
        |> List.map (fun e ->
            { e with
                SessionId = e.SessionId |> Option.orElse (named |> List.tryPick fst)
                Actor = e.Actor |> Option.orElse (named |> List.tryPick snd)
            }
        )


    /// Inserts the audit entries of a request in the transaction of its writes.
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
    /// Runs the writes of a request and their audit entries in one transaction. A violated
    /// <c>unique (patient_id, no)</c> means another server wrote the same number and returns <c>Conflict</c>
    /// with the current head. Any other failure returns <c>Failed</c> with the reason, so the caller keeps
    /// its state and the next request retries.
    /// </summary>
    let runWrites
        (connectionString: string)
        (zone: TimeZoneInfo)
        (now: unit -> DateTime)
        (writes: Session.Persist list)
        : Session.StoreOutcome
        =
        // The connection and the transaction open inside the try, so an unreachable database returns
        // Failed instead of raising.
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

                match
                    patient
                    |> Option.map (SqlDatabase.loadRecords (now () |> LocalDate.ofUtc zone) connectionString)
                with
                | Some(head :: _) -> Session.StoreOutcome.Conflict head
                | _ -> Session.StoreOutcome.Failed e.Message
            | e ->
                tx.Rollback()
                Session.StoreOutcome.Failed e.Message
        with e ->
            Session.StoreOutcome.Failed e.Message


    // ---- the credential, the code and the attempts ------------------------------------------

    /// Loads the credential of a person from the newest credential event, which holds it whole. A person
    /// without events, such as a Prescriber who never enrolled, has no credential.
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
    /// Loads the newest confirmation code of a person with its count of wrong tries. Returns None when
    /// that code is spent or expired; an older code never takes its place.
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


    /// Loads the enrolment attempts that are not dropped, by attempt id when by is "attempt" and by user id
    /// otherwise. dropEnrolment and supplyPin need every attempt of a person.
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


    /// Loads an enrolment attempt into the state, with the other attempts of the same person, the live
    /// code and the credential of that person.
    let withEnrolment (conn: SqliteConnection) (now: DateTime) (attempt: string) (state: Session.State) =
        match loadEnrolments conn "attempt" attempt |> List.tryHead with
        // The database decides: an attempt another server dropped is removed from this server's state.
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


    /// Loads the credential of a person into the state; a signature is checked against it.
    let withCredential (conn: SqliteConnection) (userId: string) (state: Session.State) =
        match loadCredential conn userId with
        | None -> { state with Credentials = state.Credentials |> Map.remove userId }
        | Some c -> { state with Credentials = state.Credentials |> Map.add userId c }


    // ---- what a Session holds in flight -------------------------------------------------------

    /// Loads the newest data notice of a Session while it has not expired. A notice that cannot be read is
    /// returned as an error whatever its expiry, and makes the Session unreadable. A notice with patient
    /// data but no EHR data is ignored, because it cannot be matched to an EHR read.
    let loadNotice (conn: SqliteConnection) (now: DateTime) (today: DateTime) (sid: string) =
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
                    | false, false -> readPatientData today (r.GetInt32 1) (r.GetString 2) |> Result.map Some
                    | _ -> Ok None

                data
                |> Result.bind (fun data -> readEhrPatientColumns today r 4 5 |> Result.map (fun ehr -> data, ehr))
                |> Result.map (fun (data, ehr) ->
                    {
                        Nonce = r.GetString 0
                        EhrPatient = ehr
                        PatientData = data
                        Expiry = at (r.GetInt64 3)
                    }
                    : Session.Notice
                )
            )
        |> List.tryHead
        |> Option.filter (fun notice ->
            match notice with
            // Patient data without the EHR data it came from could wrongly match a later absent EHR read.
            | Ok n when n.PatientData.IsSome && n.EhrPatient.IsNone -> false
            | Ok n -> now <= n.Expiry
            // An unreadable row is returned whatever its expiry, so the Session ends.
            | Error _ -> true
        )


    /// Loads the newest challenge of a Session unless it is spent or expired. A row that cannot be read is
    /// returned as an error.
    let loadChallenge (conn: SqliteConnection) (now: DateTime) (today: DateTime) (sid: string) =
        rows
            conn
            """
            select c.nonce, c.digest, c.expiry,
                   exists (select 1 from challenge_spent s where s.challenge_id = c.id),
                   c.ehr_json_version, c.ehr_data
            from challenge c
            where c.session_id = $sid
            order by c.id desc limit 1
            """
            [ ("$sid", box sid) ]
            (fun r ->
                let spent = r.GetInt64 3 = 1L

                readEhrPatientColumns today r 4 5
                |> Result.map (fun ehr ->
                    spent,
                    ({
                        Nonce = r.GetString 0
                        Digest = r.GetString 1
                        EhrPatient = ehr
                        Expiry = at (r.GetInt64 2)
                    }
                    : Session.Challenge)
                )
            )
        |> List.tryHead
        |> Option.bind (fun challenge ->
            match challenge with
            | Ok(spent, c) -> if spent || now > c.Expiry then None else Some(Ok c)
            // An unreadable row ends the Session, spent or not.
            | Error reason -> Some(Error reason)
        )


    /// Returns the signing refusal stored under the word, with the values of the extra columns. headOf
    /// finds the blocking head in the loaded record. Returns None for an unknown word or a missing value.
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


    /// Loads the stored answer to the Submission with this key and the time of the answer. A row with an
    /// unknown answer word or a version that cannot be found gives None, and the Submission runs again.
    let loadAnswer
        (zone: TimeZoneInfo)
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
                        // The patient data of the signed plan, at the age on the signing date.
                        match versionId |> Option.bind headOf, token with
                        | Some(StoredVersion.Readable(v, whom)), Some t ->
                            let patient =
                                v.Plan.PatientData
                                |> Session.atBirthDate (v.SignedAt |> LocalDate.ofUtc zone) whom
                            Some(SigningOutcome.Submitted(v, whom, OpenedToken t, patient))
                        // The version is missing or cannot be read, so there is no answer to repeat.
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
    /// Loads the order plan versions of a patient into the state. <c>warn</c> logs every version that cannot
    /// be read, so a signature refused against an unreadable head is explained in the log.
    /// </summary>
    let withRecord
        (warn: string -> unit)
        (today: DateTime)
        (cs: string)
        (patientId: string option)
        (state: Session.State)
        =
        match patientId with
        | None -> state
        | Some pid ->
            let versions = SqlDatabase.loadRecords today cs pid

            for v in versions do
                match v with
                | StoredVersion.Unreadable u ->
                    warn $"order plan version %s{u.Id} (number %i{u.No}) cannot be read: %s{u.Reason}"
                | StoredVersion.Readable _ -> ()

            { state with Records = state.Records |> Map.add pid versions }


    /// Reads the patient id a Session was opened on, or None.
    let patientOfSession (conn: SqliteConnection) (sid: string) =
        rows
            conn
            "select patient_id from session where session_id = $sid"
            [ ("$sid", box sid) ]
            (fun r -> textOrNull r 0)
        |> List.tryHead
        |> Option.flatten


    /// Finds the version with the id among the records in the state.
    let headIn (state: Session.State) id =
        state.Records
        |> Map.toSeq
        |> Seq.collect snd
        |> Seq.tryFind (fun v -> StoredVersion.id v = id)


    /// Loads a Session, its ending and the record of its patient into the state. A Session that cannot be
    /// read gets the ending Unreadable.
    let withSession
        (warn: string -> unit)
        (cs: string)
        (zone: TimeZoneInfo)
        (conn: SqliteConnection)
        (now: DateTime)
        (sid: string)
        (state: Session.State)
        =
        let today = now |> LocalDate.ofUtc zone

        // The database decides: drop what this server has for the id first, so a Session ended elsewhere
        // does not survive here.
        let state =
            { state with
                Sessions = state.Sessions |> Map.remove sid
                Endings = state.Endings |> Map.remove sid
            }
            // Load the record first, so the head id of the Session can be found among its versions.
            |> withRecord warn today cs (patientOfSession conn sid)

        match loadSession conn today (headIn state) sid with
        | None
        | Some(Choice2Of2 None) -> state
        | Some(Choice2Of2(Some ending)) -> { state with Endings = state.Endings |> Map.add sid ending }
        | Some(Choice1Of2(Error reason)) ->
            warn $"the Session %s{sid} cannot be read and ends: %s{reason}"

            { state with Endings = state.Endings |> Map.add sid (SessionEnding.Unreadable, now) }
        | Some(Choice1Of2(Ok session)) ->
            // A notice or a challenge that cannot be read ends the Session.
            let inFlight (state: Session.State) =
                match loadNotice conn now today sid, loadChallenge conn now today sid with
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
            // Load the credential of the Session user; it holds the wrong PIN count and the lock.
            |> match session.Opened.User with
               | Some user -> withCredential conn user.UserId
               | None -> id
            |> inFlight


    /// Returns the id of the newest Session of a login, ended or not. A new open supersedes that Session.
    let newestOfLogin (conn: SqliteConnection) (login: string) =
        rows
            conn
            "select session_id from session where login = $login order by id desc limit 1"
            [ ("$login", box login) ]
            (fun r -> r.GetString 0)
        |> List.tryHead


    /// <summary>
    /// Rebuilds what a Session was opened with, ended or not. A Launch that opened a Session returns that
    /// outcome for its lifetime, so a browser that presents the Launch again goes straight to the app.
    /// <c>loadSession</c> returns no Session for an ended Session, so this reads the rows directly.
    /// </summary>
    let openedOf (conn: SqliteConnection) (today: DateTime) (headOf: string -> StoredVersion option) (sid: string) =
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
            let opened = loadOpenedWith conn today sid

            {
                User =
                    userOf row.UserId row.UserDisplay row.UserRole
                    |> Result.toOption
                    |> Option.flatten
                PatientId = row.PatientId
                EhrPatient =
                    opened
                    |> Option.bind (fun o -> o.EhrPatient |> Result.toOption |> Option.flatten)
                OpenedToken = opened |> Option.bind _.OpenedToken
                KeyThumbprint = row.KeyThumbprint
                Head = opened |> Option.bind _.HeadId |> Option.bind headOf
                Act =
                    opened
                    |> Option.bind (fun o -> o.Act |> Result.toOption)
                    |> Option.defaultValue SessionAct.Opened
            }
        )


    /// Loads the newest Session of a login and its ending, so an open sees the Session it supersedes.
    let withLogin
        warn
        (cs: string)
        (zone: TimeZoneInfo)
        (conn: SqliteConnection)
        (now: DateTime)
        (login: string)
        (state: Session.State)
        =
        // The database decides: drop every Session this server has for the login, so a Session superseded
        // or ended elsewhere cannot be found by its id.
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
        |> Option.map (fun sid -> withSession warn cs zone conn now sid state)
        |> Option.defaultValue state


    /// Loads the Launch with the nonce or callback state, its outcome and the record of its patient.
    let withLaunch
        warn
        (cs: string)
        (zone: TimeZoneInfo)
        (conn: SqliteConnection)
        (now: DateTime)
        (by: string)
        (value: string)
        (state: Session.State)
        =
        let today = now |> LocalDate.ofUtc zone

        // The database decides: drop the Launch from the state before the query, so an expired Launch is
        // not read from memory.
        let state =
            { state with
                Launches =
                    state.Launches
                    |> Map.filter (fun nonce r -> (if by = "nonce" then nonce else r.State) <> value)
            }

        match loadLaunch conn now by value with
        | None -> state
        | Some row ->
            let state = state |> withRecord warn today cs (Some row.PatientId)

            let headOf id =
                state.Records
                |> Map.toSeq
                |> Seq.collect snd
                |> Seq.tryFind (fun v -> StoredVersion.id v = id)

            let outcome =
                match row.Outcome, row.SessionId, row.Attempt with
                | Some "opened", Some sid, _ ->
                    openedOf conn today headOf sid
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

            // Load the Session the Launch opened, so a reloaded callback and the next request see it.
            match row.SessionId with
            | Some sid -> withSession warn cs zone conn now sid state
            | None -> state


    /// <summary>
    /// Loads the rows a slice names into the state of a request. Each load opens a fresh connection, so a
    /// request reads the rows as they are when it starts.
    /// </summary>
    let load
        (warn: string -> unit)
        (cs: string)
        (zone: TimeZoneInfo)
        (now: unit -> DateTime)
        (slice: StubDatabase.Slice)
        state
        =
        // A request without rows to read never opens the database, so a Launch with an invalid seal is
        // refused even when the database fails.
        let withConnection f =
            use conn = new SqliteConnection(cs)
            conn.Open()
            f conn

        match slice with
        | StubDatabase.Slice.Nothing -> state
        | StubDatabase.Slice.LaunchNonce nonce ->
            withConnection (fun conn -> withLaunch warn cs zone conn (now ()) "nonce" nonce state)
        | StubDatabase.Slice.LaunchState value ->
            withConnection (fun conn -> withLaunch warn cs zone conn (now ()) "state" value state)
        | StubDatabase.Slice.Login login ->
            withConnection (fun conn -> withLogin warn cs zone conn (now ()) login state)
        | StubDatabase.Slice.Session sid ->
            withConnection (fun conn -> withSession warn cs zone conn (now ()) sid state)
        | StubDatabase.Slice.Submission(sid, key) ->
            withConnection (fun conn ->
                let state = withSession warn cs zone conn (now ()) sid state

                let headOf id =
                    state.Records
                    |> Map.toSeq
                    |> Seq.collect snd
                    |> Seq.tryFind (fun v -> StoredVersion.id v = id)

                // Load the earlier answer to this key, if there is one.
                match loadAnswer zone conn headOf sid key with
                | Some answer -> { state with Answered = state.Answered |> Map.add (sid, key) answer }
                | None -> { state with Answered = state.Answered |> Map.remove (sid, key) }
            )
        // Load the attempt, the other attempts of the same person, their code and credential, and the
        // record of the patient.
        | StubDatabase.Slice.Enrolment attempt ->
            withConnection (fun conn ->
                let state = withEnrolment conn (now ()) attempt state

                state.Enrolments
                |> Map.tryFind attempt
                |> Option.map _.PatientId
                |> fun pid -> withRecord warn (now () |> LocalDate.ofUtc zone) cs pid state
            )


    /// <summary>
    /// Makes the session store on the database. A load reads the rows a slice names; persist runs the writes
    /// of a request in one transaction. <c>warn</c> logs every row that cannot be read. A load that throws
    /// is logged and rethrown, and the port treats it as a store failure.
    /// </summary>
    let store
        (warn: string -> unit)
        (cs: string)
        (zone: TimeZoneInfo)
        (now: unit -> DateTime)
        : StubDatabase.SessionStore
        =
        {
            load =
                fun slice s ->
                    try
                        load warn cs zone now slice s
                    with e ->
                        warn $"the session store could not be read: %s{e.Message}"
                        reraise ()
            persist = runWrites cs zone now
        }


    /// <summary>
    /// Writes the demo credentials for each login without a credential event, so a restart adds no row and
    /// never resets a PIN the User changed. Used on demo servers only.
    /// </summary>
    let seed (cs: string) (now: DateTime) (credentials: Map<string, Credential>) =
        try
            use conn = new SqliteConnection(cs)
            conn.Open()
            use tx = conn.BeginTransaction()

            try
                for login, credential in credentials |> Map.toList do
                    // One statement checks and inserts, so a credential another server sets meanwhile is not
                    // replaced by the demo PIN.
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


    /// Makes the session port that keeps its state in the database.
    let makeSessionPort warn cs zone now = StubDatabase.makeSessionPortWith (store warn cs zone now) zone
