// The session store on SQLite (plan 516), step 7: the credential of a person, the confirmation
// code an enrolment is bound to, and the attempts themselves. `Sql/003-credentials.sql` is
// migration 3 and ships with this script.
//
// Script-first draft (script-only policy) of what `module SqlSessions` of
// `ServerApi.SqlAdapters.fs` gains, and of the tests it needs. The writes the machine returns
// for these rows are a second draft: `Session.Persist` has no case for a credential yet, so the
// writer here takes its own `CredentialWrite`, which the migrate turns into `Persist` cases.
// Build first (`dotnet run Build`), then run `dotnet fsi SessionCredentials.fsx` from this
// directory.

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
/// What the machine changes about a credential, a code or an enrolment, as values. In the
/// migrate these become cases of `Session.Persist`, returned by `callback`, `supplyPin`,
/// `dropEnrolment` and `commit` beside the writes they already return.
/// </summary>
[<RequireQualifiedAccess>]
type CredentialWrite =
    // the credential as it stands after the event that changed it
    | WriteCredential of userId: string * event: string * Credential * at: DateTime
    // the code mailed when a launch suspends into enrolment, and what becomes of it
    | WriteCode of Session.PendingCode * at: DateTime
    | CountTry of userId: string * at: DateTime
    | SpendCode of userId: string * at: DateTime
    // the launch suspended at the PIN question, and the attempts given up
    | WriteEnrolment of Session.Enrolment * at: DateTime
    | DropEnrolment of attempt: string * at: DateTime
    | DropEnrolmentsOf of userId: string * at: DateTime


module SqlCredentials =

    open Microsoft.Data.Sqlite

    let ms = SqlSessions.ms
    let at = SqlSessions.at


    // ---- reading ------------------------------------------------------------------------------

    /// The credential of a person: the newest event carries it whole, so a load reads one row
    /// and no history is replayed. A person with no event has no credential at all, which is
    /// what a Prescriber who has never enrolled looks like.
    let loadCredential (conn: SqliteConnection) (userId: string) =
        SqlSessions.rows
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


    /// The live confirmation code of a person: the newest row that is neither spent nor past
    /// its lifetime, with its wrong tries counted. Never an older code in place of a spent one.
    let loadCode (conn: SqliteConnection) (now: DateTime) (userId: string) =
        SqlSessions.rows
            conn
            """
            select c.id, c.mail_address, c.code_mac, c.expiry,
                   (select count(*) from code_try t where t.code_id = c.id)
            from confirmation_code c
            where c.user_id = $u and not exists (select 1 from code_spent s where s.code_id = c.id)
            order by c.id desc limit 1
            """
            [ ("$u", box userId) ]
            (fun r ->
                {
                    UserId = userId
                    MailAddress = r.GetString 1
                    CodeMac = r.GetFieldValue<byte[]> 2
                    Expiry = at (r.GetInt64 3)
                    Tries = r.GetInt32 4
                }: Session.PendingCode
            )
        |> List.tryHead
        |> Option.filter (fun code -> now <= code.Expiry)


    /// An enrolment attempt that was not given up, and every undropped attempt of the person it
    /// names: `dropEnrolment` spends the shared code only when no other attempt of that person
    /// stands, and `supplyPin` drops them all, so both need the wider slice.
    let loadEnrolments (conn: SqliteConnection) (by: string) (value: string) =
        SqlSessions.rows
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
                }: Session.Enrolment
            )


    /// The rows an enrolment attempt can touch, as the state holds them: the attempt, every
    /// other attempt of the person it names, that person's live code and their credential.
    let withEnrolment (conn: SqliteConnection) (now: DateTime) (attempt: string) (state: Session.State) =
        match loadEnrolments conn "attempt" attempt |> List.tryHead with
        | None -> state
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
        | Some c ->
            { state with
                Credentials = state.Credentials |> Map.add userId c
            }


    // ---- writing ------------------------------------------------------------------------------

    let private nullable (value: 'a option) =
        value |> Option.map box |> Option.defaultValue (box DBNull.Value)


    /// The id of the live code of a person, which a try and a spending name.
    let private liveCodeId (conn: SqliteConnection) (tx: SqliteTransaction) (userId: string) =
        use cmd =
            SqlSessions.command
                conn
                tx
                """
                select c.id from confirmation_code c
                where c.user_id = $u and not exists (select 1 from code_spent s where s.code_id = c.id)
                order by c.id desc limit 1
                """
                [ ("$u", box userId) ]

        match cmd.ExecuteScalar() with
        | null -> None
        | value -> Some(unbox<int64> value)


    /// The rows of one write. Every case matched, so that a case without a row fails to compile.
    let run (conn: SqliteConnection) (tx: SqliteTransaction) (write: CredentialWrite) =
        let exec sql parameters =
            use cmd = SqlSessions.command conn tx sql parameters
            cmd.ExecuteNonQuery() |> ignore

        match write with
        | CredentialWrite.WriteCredential(userId, event, credential, at) ->
            exec
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
        | CredentialWrite.WriteCode(code, at) ->
            exec
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
        | CredentialWrite.CountTry(userId, at) ->
            match liveCodeId conn tx userId with
            | Some id -> exec "insert into code_try (code_id, at) values ($c, $at)" [ "$c", box id; "$at", box (ms at) ]
            // no live code to count a try against: the code was spent meanwhile
            | None -> ()
        | CredentialWrite.SpendCode(userId, at) ->
            match liveCodeId conn tx userId with
            | Some id ->
                exec "insert into code_spent (code_id, at) values ($c, $at)" [ "$c", box id; "$at", box (ms at) ]
            | None -> ()
        | CredentialWrite.WriteEnrolment(e, at) ->
            let (PublicKey key) = e.PublicKey

            exec
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
        | CredentialWrite.DropEnrolment(attempt, at) ->
            exec
                "insert or ignore into enrolment_dropped (attempt, at) values ($a, $at)"
                [ "$a", box attempt; "$at", box (ms at) ]
        | CredentialWrite.DropEnrolmentsOf(userId, at) ->
            exec
                """
                insert or ignore into enrolment_dropped (attempt, at)
                select attempt, $at from enrolment where user_id = $u
                """
                [ "$u", box userId; "$at", box (ms at) ]


    let runWrites (cs: string) (writes: CredentialWrite list) =
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


    // ---- the demo seed ------------------------------------------------------------------------

    /// <summary>
    /// The credentials the demo walkthrough needs, written once per login: a login that already
    /// has a credential event is left alone, so a restart adds no row and never resets a PIN the
    /// User changed. Demo servers only; production has no seed and refuses the key today.
    /// </summary>
    let seed (cs: string) (now: DateTime) (credentials: Map<string, Credential>) =
        use conn = new SqliteConnection(cs)
        conn.Open()

        let missing =
            credentials
            |> Map.toList
            |> List.filter (fun (login, _) -> loadCredential conn login |> Option.isNone)

        missing
        |> List.map (fun (login, credential) -> CredentialWrite.WriteCredential(login, "seeded", credential, now))
        |> function
            | [] -> Session.StoreOutcome.Written
            | writes -> runWrites cs writes


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlCredentialTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenPRES.Server.Tests.SqlSchemaTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests


let withStore f =
    withDb (fun cs ->
        SqlSchema.apply cs |> Expect.equal "every migration" [ 1; 2; 3 ]
        f cs
    )


let connect (cs: string) =
    let conn = new SqliteConnection(cs)
    conn.Open()
    conn


let now = DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)


let codeOf userId : Session.PendingCode =
    {
        UserId = userId
        MailAddress = "n***@stub.example"
        CodeMac = codeMac "123456"
        Expiry = now.AddMinutes 15.0
        Tries = 0
    }


let enrolmentOf attempt userId : Session.Enrolment =
    {
        Attempt = attempt
        UserId = userId
        Login = userId
        DisplayName = "Stub Prescriber"
        PatientId = "stub-patient"
        PublicKey = keyA
    }


let tests =
    testList
        "the credential rows"
        [
            test "a credential is its newest event, and a person without one has none" {
                withStore (fun cs ->
                    use conn = connect cs
                    SqlCredentials.loadCredential conn "no-pin" |> Expect.isNone "never enrolled"

                    let withPin = Credential.withPin salts "1234"

                    SqlCredentials.runWrites
                        cs
                        [
                            CredentialWrite.WriteCredential("prescriber", "pin-set", withPin, now)
                            CredentialWrite.WriteCredential(
                                "prescriber",
                                "wrong",
                                { withPin with WrongCount = 1 },
                                now.AddMinutes 1.0
                            )
                        ]
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    match SqlCredentials.loadCredential conn "prescriber" with
                    | Some c ->
                        c.WrongCount |> Expect.equal "the count of the newest event" 1
                        c.PinHash |> Expect.isSome "and the PIN it still holds"

                        PinHash.verify "1234" c.PinHash.Value
                        |> Expect.isTrue "the PIN reads back"
                    | None -> failtest "expected the credential"
                )
            }

            test "the lock is a moment the credential carries" {
                withStore (fun cs ->
                    let locked =
                        { Credential.withPin salts "1234" with
                            WrongCount = 3
                            LockedUntil = Some(now.AddMinutes 1.0)
                        }

                    SqlCredentials.runWrites cs [ CredentialWrite.WriteCredential("prescriber", "locked", locked, now) ]
                    |> ignore

                    use conn = connect cs

                    (SqlCredentials.loadCredential conn "prescriber").Value.LockedUntil
                    |> Expect.equal "locked until the moment it was locked to" (Some(now.AddMinutes 1.0))
                )
            }

            test "the live code is the newest unspent one, with its tries counted" {
                withStore (fun cs ->
                    use conn = connect cs
                    SqlCredentials.runWrites cs [ CredentialWrite.WriteCode(codeOf "no-pin", now) ] |> ignore

                    SqlCredentials.loadCode conn now "no-pin"
                    |> Option.map _.Tries
                    |> Expect.equal "no wrong code yet" (Some 0)

                    SqlCredentials.runWrites
                        cs
                        [
                            CredentialWrite.CountTry("no-pin", now)
                            CredentialWrite.CountTry("no-pin", now)
                        ]
                    |> ignore

                    SqlCredentials.loadCode conn now "no-pin"
                    |> Option.map _.Tries
                    |> Expect.equal "two wrong codes" (Some 2)

                    // spent: the PIN was set, or the tries ran out
                    SqlCredentials.runWrites cs [ CredentialWrite.SpendCode("no-pin", now) ] |> ignore

                    SqlCredentials.loadCode conn now "no-pin"
                    |> Expect.isNone "no live code, and never the older one in its place"

                    // and one past its lifetime is no code either
                    SqlCredentials.runWrites cs [ CredentialWrite.WriteCode(codeOf "no-pin", now) ] |> ignore

                    SqlCredentials.loadCode conn (now.AddMinutes 16.0) "no-pin"
                    |> Expect.isNone "past its fifteen minutes"
                )
            }

            test "an attempt brings every attempt of its person, their code and their credential" {
                withStore (fun cs ->
                    SqlCredentials.runWrites
                        cs
                        [
                            CredentialWrite.WriteEnrolment(enrolmentOf "a-1" "no-pin", now)
                            CredentialWrite.WriteEnrolment(enrolmentOf "a-2" "no-pin", now)
                            CredentialWrite.WriteEnrolment(enrolmentOf "a-3" "someone-else", now)
                            CredentialWrite.WriteCode(codeOf "no-pin", now)
                        ]
                    |> ignore

                    use conn = connect cs
                    let state = SqlCredentials.withEnrolment conn now "a-1" (Session.initialState Map.empty)

                    state.Enrolments
                    |> Map.toList
                    |> List.map fst
                    |> List.sort
                    |> Expect.equal "both attempts of that person, and no other's" [ "a-1"; "a-2" ]

                    state.Codes |> Map.containsKey "no-pin" |> Expect.isTrue "the code they are bound to"
                )
            }

            test "a dropped attempt is gone, and setting a PIN drops them all" {
                withStore (fun cs ->
                    SqlCredentials.runWrites
                        cs
                        [
                            CredentialWrite.WriteEnrolment(enrolmentOf "a-1" "no-pin", now)
                            CredentialWrite.WriteEnrolment(enrolmentOf "a-2" "no-pin", now)
                        ]
                    |> ignore

                    use conn = connect cs

                    SqlCredentials.runWrites cs [ CredentialWrite.DropEnrolment("a-1", now) ] |> ignore

                    SqlCredentials.loadEnrolments conn "attempt" "a-1"
                    |> Expect.isEmpty "the attempt given up is gone"

                    SqlCredentials.loadEnrolments conn "user" "no-pin"
                    |> List.length
                    |> Expect.equal "the other still stands" 1

                    // the PIN is set in one browser: every attempt of that person ends
                    SqlCredentials.runWrites cs [ CredentialWrite.DropEnrolmentsOf("no-pin", now) ] |> ignore

                    SqlCredentials.loadEnrolments conn "user" "no-pin"
                    |> Expect.isEmpty "no attempt of theirs is left"

                    // and dropping one twice is not an error
                    SqlCredentials.runWrites cs [ CredentialWrite.DropEnrolment("a-1", now) ]
                    |> Expect.equal "written" Session.StoreOutcome.Written
                )
            }

            test "the seed writes a login once, and never over a PIN the User set" {
                withStore (fun cs ->
                    let seeded = StubCredentials.seed salts

                    SqlCredentials.seed cs now seeded
                    |> Expect.equal "written" Session.StoreOutcome.Written

                    use conn = connect cs

                    let rows () =
                        use cmd = SqlSessions.command conn null "select count(*) from credential_event" []
                        cmd.ExecuteScalar() |> unbox<int64>

                    let first = rows ()
                    first |> Expect.equal "one row per seeded login" (int64 seeded.Count)

                    // the User changes their PIN, and the server is restarted
                    SqlCredentials.runWrites
                        cs
                        [
                            CredentialWrite.WriteCredential(
                                "prescriber",
                                "pin-set",
                                Credential.withPin salts "9999",
                                now
                            )
                        ]
                    |> ignore

                    SqlCredentials.seed cs now seeded |> ignore

                    rows () |> Expect.equal "the second start adds nothing" (first + 1L)

                    match SqlCredentials.loadCredential conn "prescriber" with
                    | Some c ->
                        PinHash.verify "9999" c.PinHash.Value |> Expect.isTrue "the PIN they set"
                        PinHash.verify "1234" c.PinHash.Value |> Expect.isFalse "not the seeded one"
                    | None -> failtest "expected the credential"
                )
            }
        ]


runTestsWithCLIArgs [] [||] tests
