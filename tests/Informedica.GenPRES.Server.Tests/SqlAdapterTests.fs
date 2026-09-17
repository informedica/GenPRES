/// The session port over the record on SQLite: a signed version that survives a restart, a crash
/// between the write and the reply, a refused write, a load that fails for each member that
/// loads, the store's warnings, the rooted connection string, and two ports signing the same
/// number at once.
module Informedica.GenPRES.Server.Tests.SqlAdapterTests

open System
open System.IO
open System.Threading
open Expecto
open Expecto.Flip
open Microsoft.Data.Sqlite
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests
open Informedica.GenPRES.Server.Tests.SqlSchemaTests
open Informedica.GenPRES.Server.Tests.SqlRecordTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests


/// A port over a record store, with the clock at t0, a directory and an outbox of its own.
let portOver (store: StubDatabase.RecordStore) =
    let outbox = StubMail.make ()

    let directory =
        StubDirectory.make (fun () -> t0) (fun () -> $"code-{Guid.NewGuid()}")

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


/// A resource provider that never loads: the session port does not use it.
let unloadedProvider () =
    Informedica.GenForm.Lib.Resources.CachedResourceProvider((fun () -> Error []), None)
    :> Informedica.GenForm.Lib.Resources.IResourceProvider


let schemaVersions (cs: string) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "select migration from schema_version order by migration"
    use r = cmd.ExecuteReader()

    [
        while r.Read() do
            r.GetInt64 0
    ]


/// Opens a Session over a port whose seal key and clock are the real ones: a Launch minted now
/// under a fresh key, verified by the env the key was given to.
let openLive (key: LaunchSeal.Key) (port: SessionPort) (directory: StubDirectory.Directory) login =
    let launch =
        LaunchSeal.mint
            key
            {
                PatientId = "stub-patient"
                Nonce = $"n-{Guid.NewGuid()}"
                Expiry = DateTime.UtcNow + lifetime
            }

    async {
        match! port.present (launch, keyA) with
        | LaunchResult.RedirectTo(_, st) ->
            let cb =
                {
                    State = st
                    StateCookie = Some st
                    Code = Some(directory.issue login "stub-patient")
                    Error = None
                }

            match! port.callback cb with
            | CallbackResult.Opened(sid, _) ->
                match! port.find sid with
                | SessionLookup.Found opened -> return sid, opened
                | other -> return failtest $"expected Found, got %A{other}"
            | other -> return failtest $"expected Opened, got %A{other}"
        | other -> return failtest $"expected RedirectTo, got %A{other}"
    }


let rows (cs: string) =
    use conn = new SqliteConnection(cs)
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- "select count(*) from order_plan"
    cmd.ExecuteScalar() :?> int64


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


[<Tests>]
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
                                    fun writes ->
                                        match inner.persist writes with
                                        | Session.StoreOutcome.Written ->
                                            for Session.WriteVersion v in writes do
                                                written.Value <- Some v.Id

                                            Session.StoreOutcome.Failed "the reply was lost"
                                        | other -> other
                            }

                        let port, directory, _ = portOver crashing
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"
                        let! lost = port.submit sid signature

                        lost
                        |> Expect.equal
                            "answered as a store failure"
                            (SigningOutcome.Refused SigningRefusal.StoreFailed)

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
                            { SqlDatabase.store ignore cs with persist = SqlDatabase.persist readOnly }

                        let port, directory, _ = portOver store
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"

                        let! answer = port.submit sid signature

                        answer
                        |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)

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

                                answer
                                |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)

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

                                answer
                                |> Expect.equal "store failed" (SigningOutcome.Refused SigningRefusal.StoreFailed)

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
                                    (outbox.sent () |> List.find (fun m -> m.Subject.Contains "confirmation code"))
                                        .Body

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
                        SqlDatabase.persist cs [ Session.WriteVersion v1 ] |> ignore

                        let fixture = Informedica.GenPRES.Server.Tests.SqlRecordTests.fixtureText ()

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

                        Expect.throws
                            "the load throws"
                            (fun () -> (SqlDatabase.store warnings.Add missing).load "stub-patient" seeded |> ignore)

                        warnings.Count |> Expect.equal "and one failed load" 2
                    }
                )

            test "a relative data source is rooted and its folder created" {
                let root = Path.Combine(Path.GetTempPath(), $"genpres-root-{Guid.NewGuid()}")

                try
                    let cs =
                        SqlDatabase.connectionString root "Data Source=data/db/genpres.db;Pooling=False"

                    SqliteConnectionStringBuilder(cs).DataSource
                    |> Expect.equal "rooted" (Path.Combine(root, "data/db/genpres.db"))

                    Directory.Exists(Path.Combine(root, "data/db"))
                    |> Expect.isTrue "the folder exists"

                    use conn = new SqliteConnection(cs)
                    conn.Open()
                    SqlSchema.apply cs |> Expect.equal "the schema applies" [ 1 ]
                finally
                    if Directory.Exists root then
                        Directory.Delete(root, true)
            }

            testOnFile
                "makeAppEnvWith with a connection string applies the migrations and signs into the file"
                (fun cs ->
                    async {
                        let directory = StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId
                        let key = LaunchSeal.newKey Security.Cryptography.RandomNumberGenerator.GetBytes

                        let env =
                            Adapters.makeAppEnvWith
                                true
                                (Some cs)
                                key
                                directory
                                (StubMail.make ()).port
                                (unloadedProvider ())

                        schemaVersions cs |> Expect.equal "migration 1 applied" [ 1L ]

                        let! sid, opened = openLive key env.session directory "prescriber"
                        let! signature = challenged env.session sid opened "k-1"
                        let! signed = env.session.submit sid signature
                        signed |> noOf |> Expect.equal "order plan version 1" (Some 1)
                        rows cs |> Expect.equal "one row in the file" 1L
                    }
                )

            test "makeAppEnvWith without a connection string keeps the record in memory" {
                let directory = StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId
                let key = LaunchSeal.newKey Security.Cryptography.RandomNumberGenerator.GetBytes

                let env =
                    Adapters.makeAppEnvWith true None key directory (StubMail.make ()).port (unloadedProvider ())

                async {
                    let! sid, opened = openLive key env.session directory "prescriber"
                    let! signature = challenged env.session sid opened "k-1"
                    let! signed = env.session.submit sid signature
                    signed |> noOf |> Expect.equal "order plan version 1, in memory" (Some 1)
                }
                |> Async.RunSynchronously

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
                                    fun writes ->
                                        if not (barrier.SignalAndWait(TimeSpan.FromSeconds 10.0)) then
                                            raise (TimeoutException "the other port never reached its write")

                                        inner.persist writes
                            }

                        let portA, directoryA, _ = portOver (meeting ())
                        let portB, directoryB, _ = portOver (meeting ())
                        let! sidA, openedA = openAs portA directoryA "n-1" "prescriber"
                        let! sidB, openedB = openAs portB directoryB "n-2" "prescriber-b"
                        let! signatureA = challenged portA sidA openedA "k-a"
                        let! signatureB = challenged portB sidB openedB "k-b"

                        let! answers =
                            [
                                portA.submit sidA signatureA
                                portB.submit sidB signatureB
                            ]
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

                        let loserPort, loserSid =
                            if noOf answers[0] = None then portA, sidA else portB, sidB

                        match! loserPort.openVersion loserSid winner with
                        | Some reopened ->
                            reopened.Head
                            |> Option.map StoredVersion.id
                            |> Expect.equal "opens the winner" (Some winner)
                        | None -> failtest "the losing Session should open the winning row"
                    }
                )
        ]


/// The temporary files the SQLite store factory made, deleted once every suite over them ran.
let minted = System.Collections.Concurrent.ConcurrentBag<string>()


/// A SQLite store over a fresh file with the schema applied, one per port: the suites run in
/// parallel and sign for the same patient.
let newSqliteStore () =
    let path = Path.Combine(Path.GetTempPath(), $"genpres-{Guid.NewGuid()}.db")
    minted.Add path

    let cs =
        SqliteConnectionStringBuilder(DataSource = path, Pooling = false).ToString()

    SqlSchema.apply cs |> ignore
    SqlDatabase.store ignore cs


/// The composition suites of the in-memory port, run again over SQLite, so that the two stores
/// cannot drift on the record. Sequenced, so that the deletion of the files runs after the
/// suites that opened them.
[<Tests>]
let compositionOverSqlite =
    testSequenced
    <| testList
        "the composition suites over SQLite"
        [
            StubAdapterTests.SessionStubTests.compositionSuites "suites" newSqliteStore

            test "the files the store factory made are deleted" {
                for path in minted do
                    File.Delete path

                minted |> Seq.filter File.Exists |> Seq.isEmpty |> Expect.isTrue "no file left"
            }
        ]
