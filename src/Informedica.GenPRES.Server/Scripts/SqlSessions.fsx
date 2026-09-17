// The session store on SQLite (plan 516), step 6b: the launch and session tables, the loader
// that reads them into the state, and the writer that runs a request's writes in one
// transaction.
//
// Script-first draft (script-only policy) of the module `SqlSessions` of
// `ServerApi.SqlAdapters.fs` and of the tests of `SqlSessionTests.fs`. `Sql/002-session.sql` is
// migration 2 and ships with this script. The port that loads a slice per request and the race
// tests are 6c; here the loader and the writer are exercised directly. Build first
// (`dotnet run Build`), then run `dotnet fsi SqlSessions.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Microsoft.Data.Sqlite, 10.0.12"
#I "../../../tests/Informedica.GenPRES.Server.Tests/bin/Debug/net10.0"
#r "Newtonsoft.Json.dll"
#r "Informedica.Utils.Lib.dll"
#r "Informedica.GenUNITS.Lib.dll"
#r "Informedica.GenSOLVER.Lib.dll"
#r "Informedica.GenCORE.Lib.dll"
#r "Informedica.GenFORM.Lib.dll"
#r "Informedica.GenORDER.Lib.dll"
#r "Informedica.GenPRES.Shared.dll"
#r "Informedica.GenPRES.Server.dll"
#r "Informedica.GenPRES.Server.Tests.dll"

open System
open Shared.Types
open ServerApi

module GenForm = Informedica.GenForm.Lib.Types


// ---------------------------------------------------------------------------------------------
// The launch and session rows (→ ServerApi.SqlAdapters.fs, module SqlSessions)
// ---------------------------------------------------------------------------------------------

/// <summary>
/// The launches, the Sessions and their endings in the database: the rows a request can touch
/// read into its state, and the writes it returned appended in one transaction. Nothing is ever
/// changed: an ending, a heartbeat and what a Session opened with are rows of their own, and a
/// supersession is no row at all, since the newer Session of a login tells it.
/// </summary>
module SqlSessions =

    open Microsoft.Data.Sqlite
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    /// The highest JSON structure version this release reads for a stored patient, and the one
    /// it writes. As for an order plan version, a change ships as expand, then contract.
    let patientJsonRead = 1

    let patientJsonWritten = 1


    /// Brings stored patient JSON to the structure this release reads. There are no steps yet.
    let upgradePatient (version: int) (json: string) : Result<string, string> =
        if version < 1 then
            Error $"JSON structure version %i{version} does not exist"
        elif version > patientJsonRead then
            Error $"JSON structure version %i{version} is newer than this release knows"
        else
            Ok json


    let patientJson (patient: GenForm.Patient) =
        patient |> Patient.Dto.toDto |> Canonical.serialize


    let readPatient (version: int) (json: string) : Result<GenForm.Patient, string> =
        upgradePatient version json
        |> Result.bind (fun json ->
            try
                json
                |> Canonical.deserialize<Patient.Dto.Dto>
                |> Patient.Dto.fromDto
                |> Result.mapError (fun errs -> $"the patient does not parse: %A{errs}")
            with e ->
                Error $"the JSON does not read: %s{e.Message}"
        )


    let ms (at: DateTime) =
        DateTimeOffset(at, TimeSpan.Zero).ToUnixTimeMilliseconds()

    let at (ms: int64) =
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime


    /// The word a refusal is stored under, and back. Every refusal matched, so that one without
    /// a word fails to compile.
    let refusalWord =
        function
        | LaunchRefusal.LaunchExpired -> "expired"
        | LaunchRefusal.LaunchSpent -> "spent"
        | LaunchRefusal.LaunchInvalid -> "invalid"
        | LaunchRefusal.NoBrowserIdentity -> "no-identity"
        | LaunchRefusal.NoRole -> "no-role"
        | LaunchRefusal.WrongActivePatient -> "wrong-patient"
        | LaunchRefusal.EnrolmentRequired -> "enrolment"


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
        |> List.tryFind (fun r -> refusalWord r = word)


    let roleWord =
        function
        | UserRole.Prescriber -> "prescriber"
        | UserRole.Reader -> "reader"


    let roleOf word =
        if word = roleWord UserRole.Reader then
            UserRole.Reader
        else
            UserRole.Prescriber


    let endingWord =
        function
        | Session.StoredEnding.Closed -> "closed"
        | Session.StoredEnding.Ended SessionEnding.WrongPinLimit -> "wrong-pin-limit"
        | Session.StoredEnding.Ended SessionEnding.Unreadable -> "unreadable"
        // a supersession is never written: the newer Session of the login tells it
        | Session.StoredEnding.Ended SessionEnding.SupersededByLaunch -> "superseded"


    // ---- reading ----------------------------------------------------------------------------

    let command (conn: SqliteConnection) (tx: SqliteTransaction) (sql: string) (parameters: (string * obj) list) =
        let cmd = conn.CreateCommand()
        cmd.Transaction <- tx
        cmd.CommandText <- sql

        for name, value in parameters do
            cmd.Parameters.AddWithValue(name, value) |> ignore

        cmd


    let private rows (conn: SqliteConnection) sql parameters (read: SqliteDataReader -> 'a) =
        use cmd = command conn null sql parameters
        use r = cmd.ExecuteReader()

        [
            while r.Read() do
                read r
        ]


    let private textOrNull (r: SqliteDataReader) (i: int) =
        if r.IsDBNull i then None else Some(r.GetString i)


    /// The Launch of a nonce or of a callback's `state`, with what its callback came to, when
    /// it is still within its lifetime; past it the record loads as absent. The Session an
    /// outcome names is rebuilt by the caller, which has the session rows.
    let loadLaunch (conn: SqliteConnection) (now: DateTime) (by: string) (value: string) =
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


    /// What a Session opened with, newest row first, and the heartbeat: the token, the id of
    /// the version it opened with, the head it saw and the patient data it shows. A patient
    /// this release cannot read makes the Session unreadable, which ends it.
    let private loadOpenedWith (conn: SqliteConnection) (sid: string) =
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
                    | _ -> Ok None

                {|
                    VersionId = textOrNull r 0
                    HeadId = textOrNull r 1
                    OpenedToken = textOrNull r 2 |> Option.map OpenedToken
                    Patient = patient
                |}
            )
        |> List.tryHead


    let private loadSeen (conn: SqliteConnection) (sid: string) =
        rows conn "select at from session_seen where session_id = $sid order by id desc limit 1" [ "$sid", box sid ] (fun r ->
            at (r.GetInt64 0)
        )
        |> List.tryHead


    /// The ending of a Session, when it has one: the row of an act, or the supersession a newer
    /// session row of its login is. An acknowledged ending is hidden, and a closed Session is
    /// gone: neither is told again.
    let private loadEnding (conn: SqliteConnection) (sid: string) (login: string option) (id: int64) =
        let acknowledged =
            rows conn "select 1 from session_acknowledged where session_id = $sid" [ "$sid", box sid ] (fun _ -> ())
            |> List.isEmpty
            |> not

        let ending =
            rows conn "select ending, at from session_ending where session_id = $sid" [ "$sid", box sid ] (fun r ->
                r.GetString 0, at (r.GetInt64 1)
            )
            |> List.tryHead

        let superseded =
            match login with
            | None -> None
            | Some login ->
                rows
                    conn
                    "select opened_at from session where login = $login and id > $id order by id desc limit 1"
                    [ "$login", box login; "$id", box id ]
                    (fun r -> at (r.GetInt64 0))
                |> List.tryHead
                |> Option.map (fun at -> SessionEnding.SupersededByLaunch, at)

        if acknowledged then
            None, ending |> Option.map fst = Some "closed"
        else
            match ending with
            | Some("closed", _) -> None, true
            | Some("wrong-pin-limit", at) -> Some(SessionEnding.WrongPinLimit, at), false
            | Some("unreadable", at) -> Some(SessionEnding.Unreadable, at), false
            | Some _
            | None -> superseded, false


    /// The Session of an id as the state holds it: the row, what it opened with, its heartbeat
    /// and the head it opened on, or its ending. A Session whose stored patient this release
    /// cannot read is ended as unreadable.
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
            let ending, closed = loadEnding conn sid row.Login row.Id

            match ending, closed with
            | Some ending, _ -> Choice2Of2(Some ending)
            | None, true -> Choice2Of2 None
            | None, false ->
                let opened = loadOpenedWith conn sid

                match opened |> Option.map _.Patient with
                | Some(Error reason) -> Choice1Of2(Error reason)
                | _ ->
                    let user =
                        match row.UserId, row.UserDisplay with
                        | Some id, Some name ->
                            Some
                                {
                                    UserId = id
                                    DisplayName = name
                                    Role = row.UserRole |> Option.map roleOf |> Option.defaultValue UserRole.Prescriber
                                }
                        | _ -> None

                    let session: Session.SessionRecord =
                        {
                            Opened =
                                {
                                    User = user
                                    PatientId = row.PatientId
                                    Patient = opened |> Option.bind (fun o -> o.Patient |> Result.toOption |> Option.flatten)
                                    OpenedToken = opened |> Option.bind _.OpenedToken
                                    KeyThumbprint = row.KeyThumbprint
                                    Head = opened |> Option.bind _.HeadId |> Option.bind headOf
                                }
                            Login = row.Login
                            OpenedWith = opened |> Option.bind _.VersionId
                            Seen = loadSeen conn sid |> Option.defaultValue row.OpenedAt
                        }

                    Choice1Of2(Ok session)
        )


    // ---- writing ----------------------------------------------------------------------------

    let private exec (conn: SqliteConnection) (tx: SqliteTransaction) sql parameters =
        use cmd = command conn tx sql parameters
        cmd.ExecuteNonQuery() |> ignore


    let private nullable (value: 'a option) =
        value |> Option.map box |> Option.defaultValue (box DBNull.Value)


    /// The one row of a write. Every case matched, so that a case without a row fails to
    /// compile.
    let private run (conn: SqliteConnection) (tx: SqliteTransaction) (write: Session.Persist) =
        match write with
        | Session.WriteVersion v ->
            exec
                conn
                tx
                """
                insert into order_plan
                    (version_id, no, patient_id, base, signed_by_user_id, signed_by_display_name,
                     signed_at, verified, json_version, plan)
                values ($id, $no, $patient, $base, $user, $name, $at, $verified, $jv, $plan)
                """
                [
                    "$id", box v.Id
                    "$no", box v.No
                    "$patient", box v.PatientId
                    "$base", nullable v.Base
                    "$user", box v.SignedBy.UserId
                    "$name", box v.SignedBy.DisplayName
                    "$at", box (ms v.SignedAt)
                    "$verified", box (if v.Verified then 1 else 0)
                    "$jv", box SqlDatabase.jsonVersionWritten
                    "$plan", box (SqlDatabase.toJson v)
                ]
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
                | LaunchResult.Refused refusal -> $"refused:%s{refusalWord refusal}", None, None
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
            let token =
                session.Opened.OpenedToken |> Option.map (fun (OpenedToken t) -> t)

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
            exec conn tx "insert into session_seen (session_id, at) values ($sid, $at)" [ "$sid", box sid; "$at", box (ms at) ]
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


    /// <summary>
    /// Runs the writes of a request in one transaction: all of them land, or none does. A
    /// violated `unique (patient_id, no)` on the record is another server's sign of the same
    /// number, answered with the head as it stands; any other failure is `Failed` with its
    /// reason, and the caller keeps the state it had.
    /// </summary>
    let runWrites (connectionString: string) (writes: Session.Persist list) : Session.StoreOutcome =
        use conn = new SqliteConnection(connectionString)
        conn.Open()
        use tx = conn.BeginTransaction()

        try
            for write in writes do
                run conn tx write

            tx.Commit()
            Session.StoreOutcome.Written
        with
        | :? SqliteException as e when
            e.SqliteExtendedErrorCode = 2067
            && e.Message.Contains "order_plan.patient_id, order_plan.no"
            ->
            tx.Rollback()

            let patient =
                writes
                |> List.tryPick (
                    function
                    | Session.WriteVersion v -> Some v.PatientId
                    | _ -> None
                )

            match patient |> Option.bind (fun pid -> SqlDatabase.loadRecords connectionString pid |> List.tryHead) with
            | Some head -> Session.StoreOutcome.Conflict head
            | None -> Session.StoreOutcome.Failed e.Message
        | e ->
            tx.Rollback()
            Session.StoreOutcome.Failed e.Message


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlSessionTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests
open Informedica.GenPRES.Server.Tests.SqlSchemaTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests


let withSessions f =
    withDb (fun cs ->
        SqlSchema.apply cs |> Expect.equal "both migrations" [ 1; 2 ]
        f cs
    )


let connect (cs: string) =
    let conn = new SqliteConnection(cs)
    conn.Open()
    conn


let noHead (_: string) : StoredVersion option = None


/// The Session an open writes: the stub patient, a token and a key, seen at t0.
let sessionOf sid login (patient: GenForm.Patient option) openedWith head : Session.SessionRecord =
    {
        Opened =
            {
                User =
                    Some
                        {
                            UserId = login
                            DisplayName = "Stub Prescriber"
                            Role = UserRole.Prescriber
                        }
                PatientId = Some "stub-patient"
                Patient = patient
                OpenedToken = Some(OpenedToken $"opened-{sid}")
                KeyThumbprint = Some "thumb"
                Head = head
            }
        Login = Some login
        OpenedWith = openedWith
        Seen = t0
    }


let opened sid login = sessionOf sid login (Some(parsePatient StubPatientData.patient)) None None


let launchOf nonce : Session.LaunchRecord =
    {
        Nonce = nonce
        State = $"state-{nonce}"
        PatientId = "stub-patient"
        PublicKey = keyA
        Expiry = t0.AddMinutes 2.0
        Outcome = None
    }


let loadedSession cs sid =
    use conn = connect cs
    SqlSessions.loadSession conn noHead sid


let tests =
    testList
        "the launch and session rows"
        [
            test "an open is written and loads back as the Session the machine held" {
                withSessions (fun cs ->
                    let session = opened "s-1" "prescriber"

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

                    loadedSession cs "s-2" |> Expect.isNone "an id the store does not know"
                )
            }

            test "the newest heartbeat and the newest opened-with count" {
                withSessions (fun cs ->
                    let session = opened "s-1" "prescriber"

                    SqlSessions.runWrites
                        cs
                        [
                            Session.OpenSession("s-1", session)
                            Session.RecordOpenedWith("s-1", session, t0)
                        ]
                    |> ignore

                    let later = t0.AddMinutes 5.0

                    let moved =
                        { session with
                            OpenedWith = Some "plan-1"
                            Opened =
                                { session.Opened with
                                    OpenedToken = Some(OpenedToken "opened-again")
                                }
                        }

                    SqlSessions.runWrites
                        cs
                        [
                            Session.RecordSeen("s-1", later)
                            Session.RecordOpenedWith("s-1", moved, later)
                        ]
                    |> ignore

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok loaded)) ->
                        loaded.OpenedWith |> Expect.equal "the newest opened-with" (Some "plan-1")

                        loaded.Opened.OpenedToken
                        |> Expect.equal "its token" (Some(OpenedToken "opened-again"))

                        loaded.Seen |> Expect.equal "the newest heartbeat" later
                    | other -> failtest $"expected the Session, got %A{other}"
                )
            }

            test "a newer Session of the login supersedes the older, whatever became of it" {
                withSessions (fun cs ->
                    let first = opened "s-1" "prescriber"
                    let second = opened "s-2" "prescriber"
                    let later = t0.AddMinutes 1.0

                    SqlSessions.runWrites cs [ Session.OpenSession("s-1", first); Session.RecordOpenedWith("s-1", first, t0) ]
                    |> ignore

                    SqlSessions.runWrites
                        cs
                        [
                            Session.OpenSession("s-2", { second with Seen = later })
                            Session.RecordOpenedWith("s-2", second, later)
                        ]
                    |> ignore

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.SupersededByLaunch, at))) ->
                        at |> Expect.equal "at the newer open" later
                    | other -> failtest $"expected superseded, got %A{other}"

                    // and the newer Session ending does not hand the login back
                    SqlSessions.runWrites cs [ Session.EndSession("s-2", Session.StoredEnding.Closed, later) ]
                    |> ignore

                    match loadedSession cs "s-1" with
                    | Some(Choice2Of2(Some(SessionEnding.SupersededByLaunch, _))) -> ()
                    | other -> failtest $"expected superseded still, got %A{other}"
                )
            }

            test "the endings that are acts, and an acknowledged one told no more" {
                withSessions (fun cs ->
                    for sid in [ "s-1"; "s-2"; "s-3" ] do
                        let session = opened sid $"user-{sid}"

                        SqlSessions.runWrites cs [ Session.OpenSession(sid, session); Session.RecordOpenedWith(sid, session, t0) ]
                        |> ignore

                    SqlSessions.runWrites cs [ Session.EndSession("s-1", Session.StoredEnding.Ended SessionEnding.WrongPinLimit, t0) ]
                    |> ignore

                    SqlSessions.runWrites cs [ Session.EndSession("s-2", Session.StoredEnding.Ended SessionEnding.Unreadable, t0) ]
                    |> ignore

                    SqlSessions.runWrites
                        cs
                        [
                            Session.EndSession("s-3", Session.StoredEnding.Closed, t0)
                            Session.AcknowledgeEnding("s-3", t0)
                        ]
                    |> ignore

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

            test "a Launch and its outcome; past the lifetime it loads as absent" {
                withSessions (fun cs ->
                    let launch = launchOf "n-1"

                    SqlSessions.runWrites
                        cs
                        [
                            Session.RecordLaunch launch
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Enrolling "attempt-1", t0)
                        ]
                    |> ignore

                    use conn = connect cs

                    match SqlSessions.loadLaunch conn t0 "state" "state-n-1" with
                    | Some row ->
                        row.Nonce |> Expect.equal "found by its state" "n-1"
                        row.PublicKey |> Expect.equal "the browser's key" keyA
                        row.Outcome |> Expect.equal "the outcome" (Some "enrolling")
                        row.Attempt |> Expect.equal "the attempt it named" (Some "attempt-1")
                    | None -> failtest "expected the Launch"

                    SqlSessions.loadLaunch conn t0 "nonce" "n-1" |> Expect.isSome "found by its nonce"

                    SqlSessions.loadLaunch conn (t0.AddMinutes 3.0) "nonce" "n-1"
                    |> Expect.isNone "past its lifetime it is absent"
                )
            }

            test "a refusal is stored by its word, and reads back as the refusal" {
                withSessions (fun cs ->
                    SqlSessions.runWrites
                        cs
                        [
                            Session.RecordLaunch(launchOf "n-1")
                            Session.RecordLaunchOutcome("n-1", LaunchResult.Refused LaunchRefusal.WrongActivePatient, t0)
                        ]
                    |> ignore

                    use conn = connect cs
                    let row = (SqlSessions.loadLaunch conn t0 "nonce" "n-1").Value

                    row.Outcome
                    |> Option.map (fun w -> w.Substring("refused:".Length))
                    |> Option.bind SqlSessions.refusalOf
                    |> Expect.equal "the refusal" (Some LaunchRefusal.WrongActivePatient)
                )
            }

            test "a request's writes are one transaction: one fails, none lands" {
                withSessions (fun cs ->
                    let session = opened "s-1" "prescriber"

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
                    let session = opened "s-1" "prescriber"
                    let v1 = Store.versionOf 1 Store.prescriber Store.t0 Store.domainPlan.Value

                    SqlSessions.runWrites cs [ Session.OpenSession("s-1", session); Session.WriteVersion v1 ]
                    |> Expect.equal "the first lands" Session.StoreOutcome.Written

                    let rival = { v1 with Id = "plan-rival" }

                    match SqlSessions.runWrites cs [ Session.WriteVersion rival; Session.RecordSeen("s-1", t0) ] with
                    | Session.StoreOutcome.Conflict head ->
                        head |> StoredVersion.id |> Expect.equal "the row that won" v1.Id
                    | other -> failtest $"expected Conflict, got %A{other}"

                    use conn = connect cs
                    use cmd = SqlSessions.command conn null "select count(*) from session_seen" []
                    cmd.ExecuteScalar() |> unbox<int64> |> Expect.equal "the heartbeat rolled back" 0L
                )
            }

            test "the patient a Session shows is stored as its Dto and read back" {
                withSessions (fun cs ->
                    let patient = parsePatient StubPatientData.patient
                    let session = opened "s-1" "prescriber"

                    SqlSessions.runWrites cs [ Session.OpenSession("s-1", session); Session.RecordOpenedWith("s-1", session, t0) ]
                    |> ignore

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Ok loaded)) -> loaded.Opened.Patient |> Expect.equal "the patient as shown" (Some patient)
                    | other -> failtest $"expected the Session, got %A{other}"

                    // a structure version this release does not know: the Session is unreadable
                    use conn = connect cs

                    use cmd =
                        SqlSessions.command
                            conn
                            null
                            "update session_opened_with set json_version = 9 where session_id = $sid"
                            [ "$sid", box "s-1" ]

                    cmd.ExecuteNonQuery() |> ignore

                    match loadedSession cs "s-1" with
                    | Some(Choice1Of2(Error reason)) ->
                        reason.Contains "newer than this release knows" |> Expect.isTrue $"the reason: %s{reason}"
                    | other -> failtest $"expected unreadable, got %A{other}"
                )
            }

            test "L5: the patient of the stored fixture upgrades and parses" {
                let json = SqlSessions.patientJson (parsePatient StubPatientData.patient)

                match SqlSessions.readPatient 1 json with
                | Ok patient ->
                    patient
                    |> SqlSessions.patientJson
                    |> Expect.equal "the same form" json
                | Error reason -> failtest reason
            }
        ]


runTestsWithCLIArgs [] [||] tests
