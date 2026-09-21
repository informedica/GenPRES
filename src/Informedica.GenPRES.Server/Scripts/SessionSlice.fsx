// The session store on SQLite (plan 516), step 6c: the port loads the rows a request can
// touch before the pure machine runs, and appends what it wrote in one transaction. The launch
// and session rows, their loader and the writer landed in 6b; what is new here is the slice a
// request is keyed by, and the two loads a callback needs, one around each half of it
// (decision 1 of the plan).
//
// Script-first draft (script-only policy) of the changes to `module StubDatabase` in
// `ServerApi.StubAdapters.fs` and of the store `module SqlSessions` gains, with the tests both
// need. The changed parts of `StubDatabase` are copied here and shadow the built ones. Build
// first (`dotnet run Build`), then run `dotnet fsi SessionSlice.fsx` from this directory.

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


// ---------------------------------------------------------------------------------------------
// The slice a request is keyed by (→ ServerApi.StubAdapters.fs, module StubDatabase)
// ---------------------------------------------------------------------------------------------

/// <summary>
/// What a request carries that names the rows it can touch. The port names it, the store reads
/// the rows under it, and the pure machine runs over what came back. A store that keeps the
/// state in memory ignores it.
/// </summary>
[<RequireQualifiedAccess>]
type Slice =
    // nothing to load: the request acts on what it brings
    | Nothing
    // a Launch presented, by the nonce sealed in it
    | LaunchNonce of string
    // a callback, by the state it carries
    | LaunchState of string
    // the Sessions of a login, so that an open can see the one it supersedes
    | Login of string
    // a Session, by the id in the cookie
    | Session of string
    // an enrolment attempt (its rows arrive in step 7; the record of its patient is loaded)
    | Enrolment of string


/// <summary>
/// Where the session state lives: `load` puts the rows a slice names into the state before a
/// request runs, `persist` appends what it wrote. The in-memory store keeps everything in the
/// state itself, so its `load` is the state unchanged; the SQL store reads and writes the
/// database.
/// </summary>
type SessionStore =
    {
        load: Slice -> Session.State -> Session.State
        persist: Session.Persist list -> Session.StoreOutcome
    }


let inMemoryStore =
    {
        load = fun _ s -> s
        persist = StubDatabase.persistNothing
    }


// ---------------------------------------------------------------------------------------------
// The slice on SQLite (→ ServerApi.SqlAdapters.fs, module SqlSessions)
// ---------------------------------------------------------------------------------------------

module SqlSlice =

    open Microsoft.Data.Sqlite


    /// The record of a patient, as the state holds it.
    let withRecord (cs: string) (patientId: string option) (state: Session.State) =
        match patientId with
        | None -> state
        | Some pid ->
            { state with
                Records = state.Records |> Map.add pid (SqlDatabase.loadRecords cs pid)
            }


    /// A Session and its ending, and the record of the patient it was opened on. A Session
    /// this release cannot read ends as unreadable, which is the ending its next request is
    /// told; the ending is a row of its own, appended by the caller's writes.
    let withSession (cs: string) (conn: SqliteConnection) (now: DateTime) (sid: string) (state: Session.State) =
        let headOf id =
            state.Records
            |> Map.toSeq
            |> Seq.collect snd
            |> Seq.tryFind (fun v -> StoredVersion.id v = id)

        // the rows are what stands: whatever this server still held for the id is dropped
        // first, so that a Session ended elsewhere cannot survive in the memory of a server
        // that opened it
        let state =
            { state with
                Sessions = state.Sessions |> Map.remove sid
                Endings = state.Endings |> Map.remove sid
            }

        match SqlSessions.loadSession conn headOf sid with
        | None
        | Some(Choice2Of2 None) -> state
        | Some(Choice2Of2(Some ending)) ->
            { state with
                Endings = state.Endings |> Map.add sid ending
            }
        | Some(Choice1Of2(Error _)) ->
            { state with
                Endings = state.Endings |> Map.add sid (SessionEnding.Unreadable, now)
            }
        | Some(Choice1Of2(Ok session)) ->
            { state with
                Sessions = state.Sessions |> Map.add sid session
            }
            |> withRecord cs session.Opened.PatientId


    /// The id of the newest Session of a login, whatever became of it: the row the loader
    /// reads a supersession off, and the one an open supersedes in turn.
    let newestOfLogin (conn: SqliteConnection) (login: string) =
        SqlSessions.rows
            conn
            "select session_id from session where login = $login order by id desc limit 1"
            [ ("$login", box login) ]
            (fun r -> r.GetString 0)
        |> List.tryHead


    /// <summary>
    /// What a Session was opened with, whatever became of it since. A Launch that opened a
    /// Session keeps that answer for its lifetime, so that a browser presenting it again is
    /// sent to the app rather than through the hop a second time, and the cookie decides which
    /// Session it lands on. `loadSession` answers an ended Session with its ending and no
    /// `OpenedSession` at all, so the launch's outcome is rebuilt from the rows directly.
    /// </summary>
    let openedOf (conn: SqliteConnection) (headOf: string -> StoredVersion option) (sid: string) =
        SqlSessions.rows
            conn
            "select user_id, user_display, user_role, patient_id, key_thumbprint from session where session_id = $sid"
            [ ("$sid", box sid) ]
            (fun r ->
                {|
                    UserId = SqlSessions.textOrNull r 0
                    UserDisplay = SqlSessions.textOrNull r 1
                    UserRole = SqlSessions.textOrNull r 2
                    PatientId = SqlSessions.textOrNull r 3
                    KeyThumbprint = SqlSessions.textOrNull r 4
                |}
            )
        |> List.tryHead
        |> Option.map (fun row ->
            let opened = SqlSessions.loadOpenedWith conn sid

            {
                User = SqlSessions.userOf row.UserId row.UserDisplay row.UserRole |> Result.toOption |> Option.flatten
                PatientId = row.PatientId
                Patient = opened |> Option.bind (fun o -> o.Patient |> Result.toOption |> Option.flatten)
                OpenedToken = opened |> Option.bind _.OpenedToken
                KeyThumbprint = row.KeyThumbprint
                Head = opened |> Option.bind _.HeadId |> Option.bind headOf
            }
        )


    /// The newest Session of a login, so that an open sees the one it supersedes, and the
    /// ending of that Session when it has one.
    let withLogin (cs: string) (conn: SqliteConnection) (now: DateTime) (login: string) (state: Session.State) =
        newestOfLogin conn login
        |> Option.map (fun sid -> withSession cs conn now sid state)
        |> Option.defaultValue state


    /// The Launch of a nonce or of a callback's state, with what its callback came to, and the
    /// record of the patient it was launched on.
    let withLaunch (cs: string) (conn: SqliteConnection) (now: DateTime) (by: string) (value: string) state =
        match SqlSessions.loadLaunch conn now by value with
        | None -> state
        | Some row ->
            let state = state |> withRecord cs (Some row.PatientId)

            let headOf id =
                state.Records
                |> Map.toSeq
                |> Seq.collect snd
                |> Seq.tryFind (fun v -> StoredVersion.id v = id)

            let outcome =
                match row.Outcome, row.SessionId, row.Attempt with
                | Some "opened", Some sid, _ ->
                    openedOf conn headOf sid |> Option.map (fun opened -> LaunchResult.Opened(sid, opened))
                | Some "enrolling", _, Some attempt -> Some(LaunchResult.Enrolling attempt)
                | Some word, _, _ when word.StartsWith "refused:" ->
                    word.Substring "refused:".Length
                    |> SqlSessions.refusalOf
                    |> Option.map LaunchResult.Refused
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

            let state =
                { state with
                    Launches = state.Launches |> Map.add row.Nonce record
                }

            // the Session it opened, so that a callback reloaded against it, and a request that
            // follows, see it as the state holds it
            match row.SessionId with
            | Some sid -> withSession cs conn now sid state
            | None -> state


    /// The rows a slice names, read into the state a request runs over.
    let load (cs: string) (now: unit -> DateTime) (slice: Slice) (state: Session.State) =
        use conn = new SqliteConnection(cs)
        conn.Open()

        match slice with
        | Slice.Nothing -> state
        | Slice.LaunchNonce nonce -> withLaunch cs conn (now ()) "nonce" nonce state
        | Slice.LaunchState value -> withLaunch cs conn (now ()) "state" value state
        | Slice.Login login -> withLogin cs conn (now ()) login state
        | Slice.Session sid -> withSession cs conn (now ()) sid state
        // the attempt's own rows arrive in step 7; its patient's record is what it needs now
        | Slice.Enrolment attempt ->
            state.Enrolments
            |> Map.tryFind attempt
            |> Option.map _.PatientId
            |> fun pid -> withRecord cs pid state


    let store (cs: string) (now: unit -> DateTime) =
        {
            load = load cs now
            persist = SqlSessions.runWrites cs
        }


// ---------------------------------------------------------------------------------------------
// The port over a slice store (→ ServerApi.StubAdapters.fs, module StubDatabase)
// ---------------------------------------------------------------------------------------------

/// The two halves of a callback, with the login's rows loaded between them: the machine learns
/// the login only when the code is redeemed, and the open decides supersession from the newest
/// Session of that login.
let callbackOver
    (store: SessionStore)
    (now: DateTime)
    newId
    newCode
    codeMac
    (idp: IdentityProviderPort)
    (registry: UserRegistryPort)
    (patientData: PatientDataPort)
    (mail: MailPort)
    (cb: Callback)
    (state: Session.State)
    =
    match Session.redeem now idp.redeem registry.standing state cb with
    | Session.Redeemed.Answered(state, result, writes) -> state, result, writes
    | Session.Redeemed.Identified(record, identity, standing) ->
        let state = store.load (Slice.Login identity.Login) state

        Session.openAfterRedeem
            now
            newId
            newCode
            codeMac
            patientData.read
            mail.send
            (record, identity, standing)
            state


/// <summary>
/// The session port over a slice store: every request runs under one lock, and loads the rows
/// its slice names before the pure machine runs. The in-memory store loads nothing, so the port
/// behaves exactly as it did. A load that throws leaves the state as it was; a signing request
/// answers it as a store failure, any other request fails.
/// </summary>
let makeSessionPortWith
    (store: SessionStore)
    (now: unit -> DateTime)
    (newId: unit -> string)
    (newCode: unit -> string)
    (newSalt: int -> byte[])
    (codeMac: string -> byte[])
    (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
    (idp: IdentityProviderPort)
    (registry: UserRegistryPort)
    (patientData: PatientDataPort)
    (mail: MailPort)
    (initial: Session.State)
    : SessionPort
    =
    let gate = obj ()
    let mutable state = initial

    let update slice f =
        lock
            gate
            (fun () ->
                let next, result = StubDatabase.failWith store.persist f (store.load slice state)
                state <- next
                result
            )

    let signing slice f =
        lock
            gate
            (fun () ->
                match
                    (try
                        Ok(store.load slice state)
                     with _ ->
                         Error())
                with
                | Error() -> SigningOutcome.Refused SigningRefusal.StoreFailed
                | Ok s ->
                    let next, result = f s
                    state <- next
                    result
            )

    // the Launch names its own rows only once its seal is read; a seal that does not verify
    // names nothing, and the machine refuses it in a moment anyway
    let launchSlice (launch, _) =
        match verify launch with
        | Ok claims -> Slice.LaunchNonce claims.Nonce
        | Error _ -> Slice.Nothing

    let asUnit (state, writes) = state, (), writes
    let noWrites (state, answer) = state, answer, []

    {
        present =
            fun launch ->
                async {
                    return
                        update (launchSlice launch) (fun s ->
                            Session.present (now ()) newId verify idp.authorizeUrl s launch
                        )
                }
        callback =
            fun cb ->
                async {
                    return
                        update
                            (Slice.LaunchState cb.State)
                            (callbackOver store (now ()) newId newCode codeMac idp registry patientData mail cb)
                }
        find = fun id -> async { return update (Slice.Session id) (Session.find (now ()) id) }
        close = fun id -> async { return update (Slice.Session id) (Session.close (now ()) id >> asUnit) }
        findEnrolment =
            fun attempt ->
                async { return update (Slice.Enrolment attempt) (Session.findEnrolment (now ()) attempt >> noWrites) }
        supplyPin =
            fun attempt code pin ->
                async {
                    return
                        update
                            (Slice.Enrolment attempt)
                            (fun s ->
                                Session.supplyPin
                                    (now ())
                                    newId
                                    newSalt
                                    codeMac
                                    registry.standing
                                    patientData.read
                                    mail.send
                                    attempt
                                    code
                                    pin
                                    s
                            )
                }
        dropEnrolment =
            fun attempt -> async { return update (Slice.Enrolment attempt) (fun s -> Session.dropEnrolment attempt s, (), []) }
        challenge =
            fun sid request ->
                async {
                    return
                        signing
                            (Slice.Session sid)
                            (StubDatabase.challengeWith
                                store.persist
                                (fun s -> Session.challenge (now ()) newId StubDatabase.digest patientData.read sid request s))
                }
        submit =
            fun sid signature ->
                async {
                    return
                        signing
                            (Slice.Session sid)
                            (StubDatabase.submitWith
                                store.persist
                                (fun s ->
                                    Session.commit
                                        (now ())
                                        newId
                                        StubDatabase.digest
                                        registry.standing
                                        mail.send
                                        sid
                                        signature
                                        s
                                ))
                }
        seen = fun sid opened -> async { return update (Slice.Session sid) (Session.seen (now ()) sid opened) }
        openVersion =
            fun sid id -> async { return update (Slice.Session sid) (Session.openVersion (now ()) newId sid id) }
    }


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlSessionPortTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Server.Tests.SqlSchemaTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests


/// A port over the database of a connection string, with the stub directory behind it. Every
/// port is a server of its own: two of them over one file are two servers.
let portOver (cs: string) =
    let clock = ref t0
    let count = ref 0

    let directory =
        StubDirectory.make (fun () -> clock.Value) (fun () -> $"code-{Guid.NewGuid()}")

    let port =
        makeSessionPortWith
            (SqlSlice.store cs (fun () -> clock.Value))
            (fun () -> clock.Value)
            (fun () ->
                count.Value <- count.Value + 1
                $"session-{Guid.NewGuid()}-{count.Value}"
            )
            (codes ())
            salts
            codeMac
            (fun launch -> LaunchSeal.verify clock.Value sealKey launch)
            directory.idp
            directory.registry
            StubPatientData.port
            (StubMail.make ()).port
            (Session.initialState (StubCredentials.seed salts))

    port, directory


/// The whole hop through a port: the Launch presented, the stub IdentityProvider's code, the
/// callback. Answers what the callback answered.
let hopThrough (port: SessionPort) (directory: StubDirectory.Directory) launch key choice =
    async {
        match! port.present (launch, key) with
        | LaunchResult.RedirectTo(_, st) ->
            let cb: Callback =
                {
                    State = st
                    StateCookie = Some st
                    Code = Some(directory.issue choice "stub-patient")
                    Error = None
                }

            return! port.callback cb
        | other -> return failtest $"expected RedirectTo, got %A{other}"
    }
    |> Async.RunSynchronously


let openedId result =
    match result with
    | CallbackResult.Opened(sid, _) -> sid
    | other -> failtest $"expected an open Session, got %A{other}"


let withStore f =
    withDb (fun cs ->
        SqlSchema.apply cs |> ignore
        f cs
    )


let tests =
    testList
        "the session port over the database"
        [
            test "a Session outlives the server that opened it" {
                withStore (fun cs ->
                    let first, directory = portOver cs
                    let sid = hopThrough first directory (mintFor "n-1" "stub-patient") keyA "prescriber" |> openedId

                    // another server over the same file, holding nothing in memory
                    let second, _ = portOver cs

                    match second.find sid |> Async.RunSynchronously with
                    | SessionLookup.Found opened ->
                        opened.User |> Option.map _.DisplayName |> Expect.equal "the User it opened for" (Some "Stub Prescriber")
                    | other -> failtest $"expected the Session, got %A{other}"
                )
            }

            test "two launches of the same User: the newer stands, the older is told" {
                withStore (fun cs ->
                    let a, directory = portOver cs
                    let first = hopThrough a directory (mintFor "n-1" "stub-patient") keyA "prescriber" |> openedId

                    let b, directoryB = portOver cs
                    let second = hopThrough b directoryB (mintFor "n-2" "stub-patient") keyA "prescriber" |> openedId

                    second |> Expect.notEqual "a Session of its own" first

                    match a.find first |> Async.RunSynchronously with
                    | SessionLookup.Ended SessionEnding.SupersededByLaunch -> ()
                    | other -> failtest $"expected the older Session superseded, got %A{other}"

                    match b.find second |> Async.RunSynchronously with
                    | SessionLookup.Found _ -> ()
                    | other -> failtest $"expected the newer Session open, got %A{other}"
                )
            }

            test "the same Launch presented twice is answered as it was the first time" {
                withStore (fun cs ->
                    let port, directory = portOver cs
                    let launch = mintFor "n-1" "stub-patient"
                    let sid = hopThrough port directory launch keyA "prescriber" |> openedId

                    // the same browser, the same Launch: the outcome it already came to
                    match port.present (launch, keyA) |> Async.RunSynchronously with
                    | LaunchResult.Opened(id, _) -> id |> Expect.equal "the Session it opened" sid
                    | other -> failtest $"expected the open it came to, got %A{other}"

                    // another browser presenting it: spent
                    match port.present (launch, PublicKey "key-B") |> Async.RunSynchronously with
                    | LaunchResult.Refused LaunchRefusal.LaunchSpent -> ()
                    | other -> failtest $"expected spent, got %A{other}"
                )
            }

            test "an ended Session cannot reopen, and its login has none" {
                withStore (fun cs ->
                    let port, directory = portOver cs
                    let sid = hopThrough port directory (mintFor "n-1" "stub-patient") keyA "prescriber" |> openedId

                    port.close sid |> Async.RunSynchronously

                    let next, _ = portOver cs

                    match next.find sid |> Async.RunSynchronously with
                    | SessionLookup.NotFound -> ()
                    | other -> failtest $"expected no Session, got %A{other}"
                )
            }
        ]


runTestsWithCLIArgs [] [||] tests
