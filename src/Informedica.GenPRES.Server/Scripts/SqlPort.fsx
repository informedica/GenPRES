// The session store on SQLite (plan 516), step 4a: the session port over the record store.
//
// Script-first draft (script-only policy) of the changes to `StubDatabase` in
// `ServerApi.StubAdapters.fs` (the port generalised over a `RecordStore`), of the additions to
// `SqlDatabase` in `ServerApi.SqlAdapters.fs` (`store`, `makeSessionPort`, `connectionString`),
// and of the tests of `SqlAdapterTests.fs`. It references the built server and server test
// assemblies, as `SqlRecord.fsx` did, to share the test helpers. Build first
// (`dotnet run Build`), then run `dotnet fsi SqlPort.fsx` from this directory.

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
// The port over a record store (→ ServerApi.StubAdapters.fs, module StubDatabase)
// ---------------------------------------------------------------------------------------------

module StubDatabase =

    open ServerApi.StubDatabase


    /// <summary>
    /// Where the record lives: `load` puts a patient's order plan versions into the state
    /// before a request runs, `persist` runs the write of a commit. The in-memory store keeps
    /// the record in the state itself; the SQL store reads and writes the database.
    /// </summary>
    type RecordStore =
        {
            load: string -> Session.State -> Session.State
            persist: Session.Persist -> Session.StoreOutcome
        }


    /// The record kept in the state: nothing to load, a write lands by being in it.
    let inMemory =
        {
            load = fun _ s -> s
            persist = persistNothing
        }


    /// The patient of the launch a callback returns to.
    let patientOfCallback (cb: Callback) (s: Session.State) =
        s.Launches |> Map.tryPick (fun _ r -> if r.State = cb.State then Some r.PatientId else None)


    /// The patient of an enrolment attempt.
    let patientOfAttempt (attempt: string) (s: Session.State) =
        s.Enrolments |> Map.tryFind attempt |> Option.map _.PatientId


    /// The patient of a Session.
    let patientOfSession (sid: string) (s: Session.State) =
        s.Sessions |> Map.tryFind sid |> Option.bind _.Opened.PatientId


    /// <summary>
    /// The session port over a record store: every request runs under one lock; a request
    /// that can touch a patient's record loads it first. A load that throws leaves the state
    /// as it was; a signing request answers it as a store failure, any other request fails.
    /// </summary>
    let makeSessionPortWith
        (store: RecordStore)
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

        let loaded patientOf =
            match patientOf state with
            | Some pid -> store.load pid state
            | None -> state

        let update patientOf f =
            lock
                gate
                (fun () ->
                    let next, result = f (loaded patientOf)
                    state <- next
                    result
                )

        let signing patientOf f =
            lock
                gate
                (fun () ->
                    match
                        (try
                            Ok(loaded patientOf)
                         with _ ->
                             Error())
                    with
                    | Error() -> SigningOutcome.Refused SigningRefusal.StoreFailed
                    | Ok s ->
                        let next, result = f s
                        state <- next
                        result
                )

        let none _ = None

        {
            present =
                fun launch ->
                    async {
                        return update none (fun s -> Session.present (now ()) newId verify idp.authorizeUrl s launch)
                    }
            callback =
                fun cb ->
                    async {
                        return
                            update
                                (patientOfCallback cb)
                                (fun s ->
                                    Session.callback
                                        (now ())
                                        newId
                                        newCode
                                        codeMac
                                        idp.redeem
                                        registry.standing
                                        patientData.read
                                        mail.send
                                        s
                                        cb
                                )
                    }
            find = fun id -> async { return update none (Session.find (now ()) id) }
            close = fun id -> async { return update none (fun s -> Session.close id s, ()) }
            findEnrolment = fun attempt -> async { return update none (Session.findEnrolment (now ()) attempt) }
            supplyPin =
                fun attempt code pin ->
                    async {
                        return
                            update
                                (patientOfAttempt attempt)
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
            dropEnrolment = fun attempt -> async { return update none (fun s -> Session.dropEnrolment attempt s, ()) }
            challenge =
                fun sid request ->
                    async {
                        return
                            signing
                                (patientOfSession sid)
                                (fun s -> Session.challenge (now ()) newId digest patientData.read sid request s)
                    }
            submit =
                fun sid signature ->
                    async {
                        return
                            signing
                                (patientOfSession sid)
                                (submitWith
                                    store.persist
                                    (fun s ->
                                        Session.commit
                                            (now ())
                                            newId
                                            digest
                                            registry.standing
                                            mail.send
                                            sid
                                            signature
                                            s
                                    ))
                    }
            seen = fun sid opened -> async { return update (patientOfSession sid) (Session.seen (now ()) sid opened) }
            openVersion =
                fun sid id -> async { return update (patientOfSession sid) (Session.openVersion (now ()) newId sid id) }
        }


    /// The session port over an in-memory store.
    let makeSessionPort = makeSessionPortWith inMemory


// ---------------------------------------------------------------------------------------------
// The SQL record store (→ ServerApi.SqlAdapters.fs, module SqlDatabase)
// ---------------------------------------------------------------------------------------------

module SqlDatabase =

    open System.IO
    open Microsoft.Data.Sqlite
    open ServerApi.SqlDatabase


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


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SqlAdapterTests.fs)
// ---------------------------------------------------------------------------------------------

open System.IO
open System.Threading
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests
open Informedica.GenPRES.Server.Tests.SqlSchemaTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests


/// A port over a record store, with the clock at t0, a directory and an outbox of its own.
let portOver (store: StubDatabase.RecordStore) =
    let outbox = StubMail.make ()
    let directory = StubDirectory.make (fun () -> t0) (fun () -> $"code-{Guid.NewGuid()}")

    let port =
        StubDatabase.makeSessionPortWith
            store
            (fun () -> t0)
            (fun () -> $"id-{Guid.NewGuid()}")
            (codes ())
            salts
            codeMac
            (fun launch -> LaunchSeal.verify t0 sealKey launch)
            directory.idp
            directory.registry
            StubPatientData.port
            outbox.port
            seeded

    port, directory, outbox


/// A store over the file that fails its loads while `failing` is set.
let failingOver (cs: string) (failing: bool ref) : StubDatabase.RecordStore =
    let inner = SqlDatabase.store ignore cs

    { inner with
        load =
            fun pid s ->
                if failing.Value then
                    raise (IOException "the database file is locked")
                else
                    inner.load pid s
    }


/// Presents a Launch for the stub patient and returns the callback for a login.
let presented (port: SessionPort) (directory: StubDirectory.Directory) nonce login =
    async {
        match! port.present (mintFor nonce "stub-patient", keyA) with
        | LaunchResult.RedirectTo(_, st) ->
            return
                {
                    State = st
                    StateCookie = Some st
                    Code = Some(directory.issue login "stub-patient")
                    Error = None
                }
        | other -> return failtest $"expected RedirectTo, got %A{other}"
    }


/// Opens a Session as the login; the session id and the Session.
let openAs (port: SessionPort) directory nonce login =
    async {
        let! cb = presented port directory nonce login

        match! port.callback cb with
        | CallbackResult.Opened(sid, _) ->
            match! port.find sid with
            | SessionLookup.Found opened -> return sid, opened
            | other -> return failtest $"expected Found, got %A{other}"
        | other -> return failtest $"expected Opened, got %A{other}"
    }


let plan = lazy Store.domainPlan.Value


/// Challenges the plan for the Session and returns the signature to submit.
let challenged (port: SessionPort) sid (opened: OpenedSession) key =
    async {
        match! port.challenge sid (plan.Value, opened.OpenedToken.Value, None) with
        | SigningOutcome.ChallengeIssued nonce ->
            return
                {
                    Plan = plan.Value
                    Opened = opened.OpenedToken.Value
                    Challenge = nonce
                    Pin = "1234"
                    IdemKey = key
                }
        | other -> return failtest $"expected ChallengeIssued, got %A{other}"
    }


let rows (cs: string) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "select count(*) from order_plan"
    cmd.ExecuteScalar() :?> int64


let withRecord f =
    withDb (fun cs ->
        SqlSchema.apply cs |> ignore
        f cs
    )


let noOf =
    function
    | SigningOutcome.Submitted(v, _) -> Some v.No
    | _ -> None


let submittedId =
    function
    | SigningOutcome.Submitted(v, _) -> v.Id
    | other -> failtest $"expected Submitted, got %A{other}"


let fails (a: Async<'a>) =
    async {
        match! Async.Catch a with
        | Choice2Of2 _ -> return true
        | Choice1Of2 _ -> return false
    }


/// A test over a fresh database file with the schema applied.
let testOnFile name (f: string -> Async<unit>) =
    testCase name (fun () -> withRecord (fun cs -> f cs |> Async.RunSynchronously))


let tests =
    testList
        "the session port over SQLite"
        [
            testOnFile
                "a signed version survives a restart; the relaunch opens on it and the next sign is 2"
                (fun cs ->
                    async {
                        let port, directory, _ = portOver (SqlDatabase.store ignore cs)
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"
                        let! first = port.submit sid signature
                        first |> noOf |> Expect.equal "order plan version 1" (Some 1)
                        let firstId = submittedId first

                        // the restart: a second port over the same file, from the seed
                        let port, directory, _ = portOver (SqlDatabase.store ignore cs)
                        let! sid, opened = openAs port directory "n-2" "prescriber"

                        match opened.Head with
                        | Some(StoredVersion.Readable v) ->
                            (v.Id, v.No) |> Expect.equal "opened on the stored head" (firstId, 1)
                        | other -> failtest $"expected a readable head, got %A{other}"

                        let! signature = challenged port sid opened "k-2"
                        let! second = port.submit sid signature
                        second |> noOf |> Expect.equal "order plan version 2" (Some 2)
                        rows cs |> Expect.equal "two rows" 2L
                    }
                )

            testOnFile
                "after a crash between the insert and the reply, the retry is refused as stale and nothing more is written"
                (fun cs ->
                    async {
                        let inner = SqlDatabase.store ignore cs
                        let written = ref None

                        // the insert lands, the reply is lost: the port keeps its old state
                        let crashing =
                            { inner with
                                persist =
                                    fun write ->
                                        match inner.persist write with
                                        | Session.StoreOutcome.Written ->
                                            let (Session.WriteVersion v) = write
                                            written.Value <- Some v.Id
                                            Session.StoreOutcome.Failed "the reply was lost"
                                        | other -> other
                            }

                        let port, directory, _ = portOver crashing
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"
                        let! lost = port.submit sid signature

                        lost
                        |> Expect.equal "answered as a store failure" (SigningOutcome.Refused SigningRefusal.StoreFailed)

                        let! retry = port.submit sid { signature with IdemKey = "k-2" }

                        match retry with
                        | SigningOutcome.Refused(SigningRefusal.Blocked head) ->
                            Some head.Id |> Expect.equal "the row that was written" written.Value
                        | other -> failtest $"expected Blocked, got %A{other}"

                        rows cs |> Expect.equal "one row" 1L

                        match! port.openVersion sid written.Value.Value with
                        | Some reopened ->
                            reopened.Head
                            |> Option.map StoredVersion.id
                            |> Expect.equal "opens the row" written.Value
                        | None -> failtest "openVersion should open the row"
                    }
                )

            testOnFile
                "a write the database refuses answers StoreFailed through the port"
                (fun cs ->
                    async {
                        let readOnly =
                            SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly).ToString()

                        let store =
                            { SqlDatabase.store ignore cs with
                                persist = ServerApi.SqlDatabase.persist readOnly
                            }

                        let port, directory, _ = portOver store
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"

                        let! answer = port.submit sid signature
                        answer |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)
                        rows cs |> Expect.equal "nothing written" 0L
                    }
                )

            testList
                "a load that throws"
                [
                    testOnFile
                        "challenge answers StoreFailed and changes nothing"
                        (fun cs ->
                            async {
                                let failing = ref false
                                let port, directory, _ = portOver (failingOver cs failing)
                                let! sid, opened = openAs port directory "n-1" "prescriber"

                                failing.Value <- true
                                let! answer = port.challenge sid (plan.Value, opened.OpenedToken.Value, None)
                                answer |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)

                                failing.Value <- false
                                let! signature = challenged port sid opened "k-1"
                                let! signed = port.submit sid signature
                                signed |> noOf |> Expect.equal "the Session signs as before" (Some 1)
                            }
                        )

                    testOnFile
                        "submit answers StoreFailed and changes nothing"
                        (fun cs ->
                            async {
                                let failing = ref false
                                let port, directory, _ = portOver (failingOver cs failing)
                                let! sid, opened = openAs port directory "n-1" "prescriber"
                                let! signature = challenged port sid opened "k-1"

                                failing.Value <- true
                                let! answer = port.submit sid signature
                                answer |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)
                                rows cs |> Expect.equal "nothing written" 0L

                                failing.Value <- false
                                let! signed = port.submit sid signature
                                signed |> noOf |> Expect.equal "the challenge still stands" (Some 1)
                            }
                        )

                    testOnFile
                        "callback fails, and the Launch still opens once the load works"
                        (fun cs ->
                            async {
                                let failing = ref false
                                let port, directory, _ = portOver (failingOver cs failing)
                                let! cb = presented port directory "n-1" "prescriber"

                                failing.Value <- true
                                let! failed = fails (port.callback cb)
                                failed |> Expect.isTrue "the call fails"

                                failing.Value <- false

                                match! port.callback cb with
                                | CallbackResult.Opened _ -> ()
                                | other -> failtest $"expected Opened, got %A{other}"
                            }
                        )

                    testOnFile
                        "supplyPin fails, and the enrolment still stands"
                        (fun cs ->
                            async {
                                let failing = ref false
                                let port, directory, outbox = portOver (failingOver cs failing)
                                let! cb = presented port directory "n-1" "no-pin"

                                let! attempt =
                                    async {
                                        match! port.callback cb with
                                        | CallbackResult.Enrolling(attempt, _, _) -> return attempt
                                        | other -> return failtest $"expected Enrolling, got %A{other}"
                                    }

                                let body =
                                    (outbox.sent () |> List.find (fun m -> m.Subject.Contains "confirmation code")).Body

                                let code = body.Substring(body.IndexOf "code is " + 8, 6)

                                failing.Value <- true
                                let! failed = fails (port.supplyPin attempt code "2468")
                                failed |> Expect.isTrue "the call fails"

                                let! pending = port.findEnrolment attempt
                                pending |> Expect.isSome "the attempt stands"

                                failing.Value <- false

                                match! port.supplyPin attempt code "2468" with
                                | SupplyPinResult.Opened _ -> ()
                                | other -> failtest $"expected Opened, got %A{other}"
                            }
                        )

                    testOnFile
                        "openVersion fails, and the Session keeps what it opened with"
                        (fun cs ->
                            async {
                                let failing = ref false
                                let port, directory, _ = portOver (failingOver cs failing)
                                let! sid, opened = openAs port directory "n-1" "prescriber"
                                let! signature = challenged port sid opened "k-1"
                                let! signed = port.submit sid signature
                                let id = submittedId signed

                                let! before = port.find sid
                                failing.Value <- true
                                let! failed = fails (port.openVersion sid id)
                                failed |> Expect.isTrue "the call fails"

                                let! after = port.find sid
                                after |> Expect.equal "the Session as it was" before
                            }
                        )

                    testOnFile
                        "seen fails, and the Session is as it was"
                        (fun cs ->
                            async {
                                let failing = ref false
                                let port, directory, _ = portOver (failingOver cs failing)
                                let! sid, opened = openAs port directory "n-1" "prescriber"

                                let! before = port.find sid
                                failing.Value <- true
                                let! failed = fails (port.seen sid opened.OpenedToken)
                                failed |> Expect.isTrue "the call fails"

                                let! after = port.find sid
                                after |> Expect.equal "the Session as it was" before
                            }
                        )
                ]

            testOnFile
                "the store warns once per failed load and once per unreadable version"
                (fun cs ->
                    async {
                        let warnings = ResizeArray<string>()
                        let v1 = Store.versionOf 1 Store.prescriber Store.t0 plan.Value
                        ServerApi.SqlDatabase.persist cs (Session.WriteVersion v1) |> ignore

                        let fixture =
                            File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "../../../tests/Informedica.GenPRES.Server.Tests/fixtures/order_plan_v1.json"))

                        Informedica.GenPRES.Server.Tests.SqlRecordTests.insertRow cs "plan-2" 2 9 fixture

                        let store = SqlDatabase.store warnings.Add cs
                        let loaded = store.load "stub-patient" seeded
                        loaded.Records["stub-patient"] |> List.length |> Expect.equal "both versions" 2
                        warnings.Count |> Expect.equal "one unreadable version" 1

                        let missing =
                            SqliteConnectionStringBuilder(
                                DataSource = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.db"),
                                Mode = SqliteOpenMode.ReadOnly
                            )
                                .ToString()

                        Expect.throws "the load throws" (fun () ->
                            (SqlDatabase.store warnings.Add missing).load "stub-patient" seeded |> ignore
                        )

                        warnings.Count |> Expect.equal "and one failed load" 2
                    }
                )

            test "a relative data source is rooted and its folder created" {
                let root = Path.Combine(Path.GetTempPath(), $"genpres-root-{Guid.NewGuid()}")

                try
                    let cs = SqlDatabase.connectionString root "Data Source=data/db/genpres.db;Pooling=False"

                    SqliteConnectionStringBuilder(cs).DataSource
                    |> Expect.equal "rooted" (Path.Combine(root, "data/db/genpres.db"))

                    Directory.Exists(Path.Combine(root, "data/db")) |> Expect.isTrue "the folder exists"

                    use conn = new SqliteConnection(cs)
                    conn.Open()
                    SqlSchema.apply cs |> Expect.equal "the schema applies" [ 1 ]
                finally
                    if Directory.Exists root then
                        Directory.Delete(root, true)
            }

            testOnFile
                "two ports over one file sign the same number at once: one lands, the other is blocked"
                (fun cs ->
                    async {
                        let barrier = new Barrier(2)

                        let meeting () =
                            let inner = SqlDatabase.store ignore cs

                            { inner with
                                persist =
                                    fun write ->
                                        if not (barrier.SignalAndWait(TimeSpan.FromSeconds 10.0)) then
                                            raise (TimeoutException "the other port never reached its write")

                                        inner.persist write
                            }

                        let portA, directoryA, _ = portOver (meeting ())
                        let portB, directoryB, _ = portOver (meeting ())
                        let! sidA, openedA = openAs portA directoryA "n-1" "prescriber"
                        let! sidB, openedB = openAs portB directoryB "n-2" "prescriber-b"
                        let! signatureA = challenged portA sidA openedA "k-a"
                        let! signatureB = challenged portB sidB openedB "k-b"

                        let! answers =
                            [ portA.submit sidA signatureA; portB.submit sidB signatureB ]
                            |> List.map (fun a -> Async.StartAsTask a |> Async.AwaitTask)
                            |> Async.Parallel

                        let submitted = answers |> Array.filter (noOf >> Option.isSome)

                        let blocked =
                            answers
                            |> Array.choose (
                                function
                                | SigningOutcome.Refused(SigningRefusal.Blocked head) -> Some head
                                | _ -> None
                            )

                        submitted.Length |> Expect.equal "one Submitted" 1
                        blocked.Length |> Expect.equal "one Blocked" 1
                        rows cs |> Expect.equal "one row" 1L

                        let winner = submittedId submitted[0]
                        blocked[0].Id |> Expect.equal "blocked by the row that landed" winner

                        let loserPort, loserSid = if noOf answers[0] = None then portA, sidA else portB, sidB

                        match! loserPort.openVersion loserSid winner with
                        | Some reopened ->
                            reopened.Head |> Option.map StoredVersion.id |> Expect.equal "opens the winner" (Some winner)
                        | None -> failtest "the losing Session should open the winning row"
                    }
                )
        ]


runTestsWithCLIArgs [] [||] tests
