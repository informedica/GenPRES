// The session store on SQLite (plan 516), step 8: what a Session has in flight — the data
// notice it was told, the signing challenge it holds, and the answer a Submission was given.
// `Sql/004-notices.sql` is migration 4 and ships with this script.
//
// Script-first draft (script-only policy) of what `module SqlSessions` of
// `ServerApi.SqlAdapters.fs` gains, and of the tests it needs. As in step 7's prototype the
// writes are drafted as a DU of their own, which the migrate turns into `Session.Persist`
// cases returned by `challenge`, `commit` and `openVersion`. Build first (`dotnet run Build`),
// then run `dotnet fsi SessionNotices.fsx` from this directory.

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


/// <summary>
/// What a Session has in flight, as values. In the migrate these become cases of
/// `Session.Persist`: `challenge` writes a notice or a challenge, `commit` spends the
/// challenge and remembers its answer, and `openVersion` spends the challenge too.
/// </summary>
[<RequireQualifiedAccess>]
type FlightWrite =
    | WriteNotice of sessionId: string * Session.Notice * at: DateTime
    | WriteChallenge of sessionId: string * Session.Challenge * at: DateTime
    | SpendChallenge of sessionId: string * nonce: string * at: DateTime
    | RememberAnswer of sessionId: string * idemKey: string * SigningOutcome * at: DateTime


module SqlFlight =

    open Microsoft.Data.Sqlite

    let ms = SqlSessions.ms
    let at = SqlSessions.at


    // ---- writing ------------------------------------------------------------------------------

    let private nullable (value: 'a option) =
        value |> Option.map box |> Option.defaultValue (box DBNull.Value)


    /// The word a refusal is stored under, and what of it the row keeps beside the word. Every
    /// refusal matched, so that one without a word fails to compile; `Blocked` keeps the id of
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


    let run (conn: SqliteConnection) (tx: SqliteTransaction) (write: FlightWrite) =
        let exec sql parameters =
            use cmd = SqlSessions.command conn tx sql parameters
            cmd.ExecuteNonQuery() |> ignore

        match write with
        | FlightWrite.WriteNotice(sid, notice, at) ->
            exec
                """
                insert into data_notice (session_id, nonce, json_version, data, expiry, at)
                values ($sid, $n, $jv, $data, $e, $at)
                """
                [
                    "$sid", box sid
                    "$n", box notice.Nonce
                    "$jv", nullable (notice.Data |> Option.map (fun _ -> SqlSessions.patientJsonWritten))
                    "$data", nullable (notice.Data |> Option.map SqlSessions.patientJson)
                    "$e", box (ms notice.Expiry)
                    "$at", box (ms at)
                ]
        | FlightWrite.WriteChallenge(sid, challenge, at) ->
            exec
                """
                insert into challenge (session_id, nonce, digest, json_version, reading, expiry, at)
                values ($sid, $n, $d, $jv, $reading, $e, $at)
                """
                [
                    "$sid", box sid
                    "$n", box challenge.Nonce
                    "$d", box challenge.Digest
                    "$jv", nullable (challenge.Reading |> Option.map (fun _ -> SqlSessions.patientJsonWritten))
                    "$reading", nullable (challenge.Reading |> Option.map SqlSessions.patientJson)
                    "$e", box (ms challenge.Expiry)
                    "$at", box (ms at)
                ]
        // the challenge a commit used, named by its nonce: another server may have issued a
        // newer one, and spending that one would take a challenge the User is answering
        | FlightWrite.SpendChallenge(sid, nonce, at) ->
            exec
                """
                insert or ignore into challenge_spent (challenge_id, at)
                select c.id, $at from challenge c
                where c.session_id = $sid and c.nonce = $n
                  and not exists (select 1 from challenge_spent s where s.challenge_id = c.id)
                order by c.id desc limit 1
                """
                [ "$sid", box sid; "$n", box nonce; "$at", box (ms at) ]
        | FlightWrite.RememberAnswer(sid, key, outcome, at) ->
            let answer, versionId, token, left, until =
                match outcome with
                | SigningOutcome.Submitted(v, OpenedToken t) -> "submitted", Some v.Id, Some t, None, None
                | SigningOutcome.Refused refusal ->
                    let word, blocked, left, until = refusalRow refusal
                    $"refused:%s{word}", blocked, None, left, until
                // a challenge and a notice are answers of `challenge`, which remembers nothing
                | other -> invalidOp $"a Submission is not answered with %A{other}"

            exec
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


    let runWrites (cs: string) (writes: FlightWrite list) =
        try
            use conn = new SqliteConnection(cs)
            conn.Open()
            use tx = conn.BeginTransaction()

            try
                for write in writes do
                    run conn tx write

                tx.Commit()
                Session.StoreOutcome.Written
            with e ->
                tx.Rollback()
                Session.StoreOutcome.Failed e.Message
        with e ->
            Session.StoreOutcome.Failed e.Message


    // ---- reading ------------------------------------------------------------------------------

    /// The live data notice of a Session: the newest row within its two minutes. A reading this
    /// release cannot read makes the Session unreadable, as an opened-with does.
    let loadNotice (conn: SqliteConnection) (now: DateTime) (sid: string) =
        SqlSessions.rows
            conn
            """
            select nonce, json_version, data, expiry
            from data_notice where session_id = $sid order by id desc limit 1
            """
            [ ("$sid", box sid) ]
            (fun r ->
                let data =
                    match r.IsDBNull 1, r.IsDBNull 2 with
                    | false, false -> SqlSessions.readPatient (r.GetInt32 1) (r.GetString 2) |> Result.map Some
                    | _ -> Ok None

                data
                |> Result.map (fun data ->
                    {
                        Nonce = r.GetString 0
                        Data = data
                        Expiry = at (r.GetInt64 3)
                    }: Session.Notice
                )
            )
        |> List.tryHead
        |> Option.filter (fun notice ->
            match notice with
            | Ok n -> now <= n.Expiry
            // an unreadable row is answered whatever its lifetime says: the Session ends on it
            | Error _ -> true
        )


    /// The live challenge of a Session: the newest row that is neither spent nor past its
    /// lifetime.
    let loadChallenge (conn: SqliteConnection) (now: DateTime) (sid: string) =
        SqlSessions.rows
            conn
            """
            select c.nonce, c.digest, c.json_version, c.reading, c.expiry
            from challenge c
            where c.session_id = $sid
              and not exists (select 1 from challenge_spent s where s.challenge_id = c.id)
            order by c.id desc limit 1
            """
            [ ("$sid", box sid) ]
            (fun r ->
                let reading =
                    match r.IsDBNull 2, r.IsDBNull 3 with
                    | false, false -> SqlSessions.readPatient (r.GetInt32 2) (r.GetString 3) |> Result.map Some
                    | _ -> Ok None

                reading
                |> Result.map (fun reading ->
                    {
                        Nonce = r.GetString 0
                        Digest = r.GetString 1
                        Reading = reading
                        Expiry = at (r.GetInt64 4)
                    }: Session.Challenge
                )
            )
        |> List.tryHead
        |> Option.filter (fun challenge ->
            match challenge with
            | Ok c -> now <= c.Expiry
            | Error _ -> true
        )


    /// The refusal a stored word names, with what the row kept beside it. `headOf` rebuilds
    /// the head that blocked from the record the caller loaded.
    let refusalOf (headOf: string -> StoredVersion option) (word: string) versionId left until =
        match word with
        | "no-session" -> Some SigningRefusal.NoSession
        | "no-patient" -> Some SigningRefusal.NoPatient
        | "not-prescriber" -> Some SigningRefusal.NotPrescriber
        | "blocked" -> versionId |> Option.bind headOf |> Option.map (StoredVersion.head >> SigningRefusal.Blocked)
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
        SqlSessions.rows
            conn
            """
            select answer, version_id, opened_token, attempts_left, locked_until, at
            from submission_answer where session_id = $sid and idem_key = $k
            """
            [ ("$sid", box sid); ("$k", box key) ]
            (fun r ->
                let word = r.GetString 0
                let versionId = SqlSessions.textOrNull r 1
                let token = SqlSessions.textOrNull r 2
                let left = if r.IsDBNull 3 then None else Some(r.GetInt32 3)
                let until = if r.IsDBNull 4 then None else Some(at (r.GetInt64 4))
                let answeredAt = at (r.GetInt64 5)

                let outcome =
                    if word = "submitted" then
                        match versionId |> Option.bind headOf, token with
                        | Some(StoredVersion.Readable v), Some t -> Some(SigningOutcome.Submitted(v, OpenedToken t))
                        // the version it names cannot be read: there is no answer to repeat
                        | _ -> None
                    elif word.StartsWith "refused:" then
                        refusalOf headOf (word.Substring "refused:".Length) versionId left until
                        |> Option.map SigningOutcome.Refused
                    else
                        None

                outcome |> Option.map (fun outcome -> outcome, answeredAt)
            )
        |> List.tryHead
        |> Option.flatten


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlFlightTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenPRES.Server.Tests.SqlSchemaTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests


let now = DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)


let connect (cs: string) =
    let conn = new SqliteConnection(cs)
    conn.Open()
    conn


/// A database with every migration and one Session to hang the rows on.
let withSession f =
    withDb (fun cs ->
        SqlSchema.apply cs |> ignore

        let session: Session.SessionRecord =
            {
                Opened =
                    {
                        User = None
                        PatientId = Some "stub-patient"
                        Patient = None
                        OpenedToken = Some(OpenedToken "opened-s-1")
                        KeyThumbprint = None
                        Head = None
                    }
                Login = Some "prescriber"
                OpenedWith = None
                Seen = now
            }

        SqlSessions.runWrites cs [ Session.OpenSession("s-1", session) ] |> ignore
        f cs
    )


let patient = parsePatient StubPatientData.patient


let noticeOf nonce data : Session.Notice =
    {
        Nonce = nonce
        Data = data
        Expiry = now.AddMinutes 2.0
    }


let challengeOf nonce reading : Session.Challenge =
    {
        Nonce = nonce
        Digest = "digest-1"
        Reading = reading
        Expiry = now.AddMinutes 2.0
    }


let tests =
    testList
        "what a Session has in flight"
        [
            test "the live notice is the newest within its two minutes, with the data it showed" {
                withSession (fun cs ->
                    SqlFlight.runWrites cs [ FlightWrite.WriteNotice("s-1", noticeOf "n-1" None, now) ]
                    |> ignore

                    SqlFlight.runWrites
                        cs
                        [ FlightWrite.WriteNotice("s-1", noticeOf "n-2" (Some patient), now) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    use conn = connect cs

                    match SqlFlight.loadNotice conn now "s-1" with
                    | Some(Ok notice) ->
                        notice.Nonce |> Expect.equal "the newest one" "n-2"
                        notice.Data |> Expect.equal "the data it showed" (Some patient)
                    | other -> failtest $"expected the notice, got %A{other}"

                    SqlFlight.loadNotice conn (now.AddMinutes 3.0) "s-1"
                    |> Expect.isNone "past its two minutes it is gone"
                )
            }

            test "a notice this release cannot read is answered, so the Session ends on it" {
                withSession (fun cs ->
                    SqlFlight.runWrites cs [ FlightWrite.WriteNotice("s-1", noticeOf "n-1" (Some patient), now) ]
                    |> ignore

                    use conn = connect cs

                    use cmd =
                        SqlSessions.command conn null "update data_notice set json_version = 9" []

                    cmd.ExecuteNonQuery() |> ignore

                    match SqlFlight.loadNotice conn now "s-1" with
                    | Some(Error reason) ->
                        reason.Contains "newer than this release knows" |> Expect.isTrue $"the reason: %s{reason}"
                    | other -> failtest $"expected unreadable, got %A{other}"
                )
            }

            test "the live challenge is the newest unspent one, with the reading it was issued on" {
                withSession (fun cs ->
                    SqlFlight.runWrites cs [ FlightWrite.WriteChallenge("s-1", challengeOf "c-1" (Some patient), now) ]
                    |> ignore

                    use conn = connect cs

                    match SqlFlight.loadChallenge conn now "s-1" with
                    | Some(Ok challenge) ->
                        challenge.Digest |> Expect.equal "the digest it was issued over" "digest-1"
                        challenge.Reading |> Expect.equal "the reading at that moment" (Some patient)
                    | other -> failtest $"expected the challenge, got %A{other}"

                    SqlFlight.runWrites cs [ FlightWrite.SpendChallenge("s-1", "c-1", now) ] |> ignore

                    SqlFlight.loadChallenge conn now "s-1"
                    |> Expect.isNone "spent, and never the live one again"

                    SqlFlight.runWrites cs [ FlightWrite.WriteChallenge("s-1", challengeOf "c-2" None, now) ]
                    |> ignore

                    SqlFlight.loadChallenge conn (now.AddMinutes 3.0) "s-1"
                    |> Expect.isNone "past its two minutes it is gone too"
                )
            }

            test "a commit spends the challenge it used, not one issued meanwhile" {
                withSession (fun cs ->
                    SqlFlight.runWrites
                        cs
                        [
                            FlightWrite.WriteChallenge("s-1", challengeOf "c-1" None, now)
                            FlightWrite.WriteChallenge("s-1", challengeOf "c-2" None, now)
                        ]
                    |> ignore

                    // the request answered c-1; c-2 came after it read
                    SqlFlight.runWrites cs [ FlightWrite.SpendChallenge("s-1", "c-1", now) ] |> ignore

                    use conn = connect cs

                    match SqlFlight.loadChallenge conn now "s-1" with
                    | Some(Ok challenge) -> challenge.Nonce |> Expect.equal "the one still standing" "c-2"
                    | other -> failtest $"expected the newer challenge, got %A{other}"
                )
            }

            test "an answer is remembered once, and read back as the answer it was" {
                withSession (fun cs ->
                    let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0
                    SqlSessions.runWrites cs [ Session.WriteVersion v1 ] |> ignore

                    let submitted = SigningOutcome.Submitted(v1, OpenedToken "opened-again")

                    SqlFlight.runWrites cs [ FlightWrite.RememberAnswer("s-1", "k-1", submitted, now) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    // the same Submission again: the answer stands, it is not replaced
                    SqlFlight.runWrites
                        cs
                        [
                            FlightWrite.RememberAnswer("s-1", "k-1", SigningOutcome.Refused SigningRefusal.StaleToken, now)
                        ]
                    |> ignore

                    use conn = connect cs
                    let headOf id = SqlDatabase.loadRecords cs "stub-patient" |> List.tryFind (fun v -> StoredVersion.id v = id)

                    match SqlFlight.loadAnswer conn headOf "s-1" "k-1" with
                    | Some(SigningOutcome.Submitted(v, token), _) ->
                        v.Id |> Expect.equal "the version it signed" v1.Id
                        token |> Expect.equal "and the token minted with it" (OpenedToken "opened-again")
                    | other -> failtest $"expected the answer it was, got %A{other}"

                    SqlFlight.loadAnswer conn headOf "s-1" "k-2"
                    |> Expect.isNone "a key nothing was answered under"
                )
            }

            test "every refusal is remembered by its word, and read back as itself" {
                withSession (fun cs ->
                    let v1 = Store.domainPlan.Value |> Store.versionOf 1 Store.prescriber Store.t0
                    SqlSessions.runWrites cs [ Session.WriteVersion v1 ] |> ignore
                    let headOf id = SqlDatabase.loadRecords cs "stub-patient" |> List.tryFind (fun v -> StoredVersion.id v = id)

                    let refusals =
                        [
                            SigningRefusal.NoSession
                            SigningRefusal.NoPatient
                            SigningRefusal.NotPrescriber
                            SigningRefusal.Blocked(StoredVersion.head (StoredVersion.Readable v1))
                            SigningRefusal.StaleToken
                            SigningRefusal.ChallengeMismatch
                            SigningRefusal.ChallengeExpired
                            SigningRefusal.PinWrong 2
                            SigningRefusal.PinLimit
                            SigningRefusal.Locked(now.AddMinutes 1.0)
                            SigningRefusal.StoreFailed
                            SigningRefusal.PlanUnreadable
                        ]

                    use conn = connect cs

                    for i, refusal in refusals |> List.indexed do
                        let key = $"k-{i}"

                        SqlFlight.runWrites
                            cs
                            [ FlightWrite.RememberAnswer("s-1", key, SigningOutcome.Refused refusal, now) ]
                        |> ignore

                        match SqlFlight.loadAnswer conn headOf "s-1" key with
                        | Some(SigningOutcome.Refused back, _) ->
                            back |> Expect.equal $"the refusal %A{refusal}" refusal
                        | other -> failtest $"expected %A{refusal}, got %A{other}"
                )
            }
        ]


runTestsWithCLIArgs [] [||] tests
