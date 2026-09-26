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
let portOver (store: StubDatabase.SessionStore) =
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
            StubAdapterTests.StubAdapters.patientData
            outbox.port
            seeded

    port, directory, outbox


/// The order plan version of a request's writes, when it has one.
let versionOf writes =
    writes
    |> List.tryPick (
        function
        | Session.WriteVersion v -> Some v
        | _ -> None
    )


/// A store over the file that fails its loads while `failing` is set.
let failingOver (cs: string) (failing: bool ref) : StubDatabase.SessionStore =
    let inner = SqlSessions.store ignore cs (fun () -> t0)

    { inner with
        load =
            fun slice s ->
                if failing.Value then
                    raise (IOException "the database file is locked")
                else
                    inner.load slice s
    }


/// A port over the database of a connection string, seeded as the composition root seeds it:
/// the machine reads a credential from the rows now, so a stub Prescriber without one in the
/// file could not sign.
let portOn (cs: string) = portOver (SqlSessions.store ignore cs (fun () -> t0))


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
    Informedica.GenForm.Lib.Resources.CachedResourceProvider(
        Informedica.Logging.Lib.Logging.noOp,
        (fun () -> Error []),
        None
    )
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
    testCase
        name
        (fun () ->
            withRecord (fun cs ->
                // every port in these tests reads its credentials from the file, so the file is
                // seeded the way the composition root seeds it at start-up
                SqlSessions.seed cs t0 (StubCredentials.seed salts) |> ignore
                f cs |> Async.RunSynchronously
            )
        )


[<Tests>]
let tests =
    testList
        "the session port over SQLite"
        [
            testOnFile
                "a signed version survives a restart; the relaunch opens on it and the next sign is 2"
                (fun cs ->
                    async {
                        let port, directory, _ = portOn cs
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"
                        let! first = port.submit sid signature
                        first |> noOf |> Expect.equal "order plan version 1" (Some 1)
                        let firstId = submittedId first

                        // the restart: a second port over the same file, from the seed
                        let port, directory, _ = portOn cs
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
                "after a crash between the insert and the reply, the retry is refused and nothing more is written"
                (fun cs ->
                    async {
                        let inner = SqlSessions.store ignore cs (fun () -> t0)
                        let written = ref None

                        // the insert lands, the reply is lost: the port keeps its old state
                        let crashing =
                            { inner with
                                persist =
                                    fun writes ->
                                        match inner.persist writes, versionOf writes with
                                        | Session.StoreOutcome.Written, Some v ->
                                            // only the signature loses its reply; every other
                                            // request of the Session lands as it would
                                            written.Value <- Some v.Id
                                            Session.StoreOutcome.Failed "the reply was lost"
                                        | outcome, _ -> outcome
                            }

                        let port, directory, _ = portOver crashing
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"
                        let! lost = port.submit sid signature

                        lost
                        |> Expect.equal
                            "answered as a store failure"
                            (SigningOutcome.Refused SigningRefusal.StoreFailed)

                        // the writes landed and the token they re-minted with them, so the
                        // Session the retry loads has moved on from the one the signature names
                        let! retry = port.submit sid { signature with IdemKey = "k-2" }

                        match retry with
                        | SigningOutcome.Refused SigningRefusal.StaleToken -> ()
                        | other -> failtest $"expected the signature refused, got %A{other}"

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
                        let readOnly = SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly).ToString()

                        // everything lands but the signature, whose writes go to a database
                        // that refuses them
                        let inner = SqlSessions.store ignore cs (fun () -> t0)

                        let store =
                            { inner with
                                persist =
                                    fun writes ->
                                        match versionOf writes with
                                        | Some _ -> SqlSessions.runWrites readOnly (fun () -> Store.t0) writes
                                        | None -> inner.persist writes
                            }

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

                                failing.Value <- false
                                let! pending = port.findEnrolment attempt
                                pending |> Expect.isSome "the attempt stands"

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

                                // `find` reads the store too, so the store has to answer again
                                // before the Session can be asked for
                                failing.Value <- false
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
                                let! failed = fails (port.seen sid opened.OpenedToken None)
                                failed |> Expect.isTrue "the call fails"

                                failing.Value <- false
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

                        // a Session over that patient: loading it brings the record with it
                        let port, directory, _ = portOn cs
                        let! sid, _ = openAs port directory "n-1" "prescriber"

                        let store = SqlSessions.store warnings.Add cs (fun () -> t0)
                        let loaded = store.load (StubDatabase.Slice.Session sid) seeded
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
                            (fun () ->
                                (SqlSessions.store warnings.Add missing (fun () -> t0)).load
                                    (StubDatabase.Slice.Session "s-1")
                                    seeded
                                |> ignore
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

                    Directory.Exists(Path.Combine(root, "data/db"))
                    |> Expect.isTrue "the folder exists"

                    use conn = new SqliteConnection(cs)
                    conn.Open()
                    SqlSchema.apply cs |> Expect.isNonEmpty "the schema applies"
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

                        schemaVersions cs |> Expect.isNonEmpty "the migrations applied"

                        let! sid, opened = openLive key env.session directory "prescriber"
                        let! signature = challenged env.session sid opened "k-1"
                        let! signed = env.session.submit sid signature
                        signed |> noOf |> Expect.equal "order plan version 1" (Some 1)
                        rows cs |> Expect.equal "one row in the file" 1L
                    }
                )

            test "the store is prepared before anything is hosted, and says what went wrong" {
                Informedica.GenPRES.Server.Tests.SqlSchemaTests.withDb (fun cs ->
                    // a fresh file: the migrations and the demo credentials land
                    Adapters.prepareStore (Some cs) |> Expect.isOk "prepared"

                    use conn = new SqliteConnection(cs)
                    conn.Open()

                    SqlSessions.loadCredential conn "prescriber"
                    |> Expect.isSome "the demo Prescriber has a credential to sign with"

                    // and again on the same file: the seed writes nothing a second time
                    Adapters.prepareStore (Some cs) |> Expect.isOk "a second start prepares nothing"
                )

                // a migrated file that was never seeded and cannot be written: it is the seed
                // that fails, and it is answered, not raised, so the start is refused with a
                // message and an exit code rather than a crash while hosting
                Informedica.GenPRES.Server.Tests.SqlSchemaTests.withDb (fun cs ->
                    SqlSchema.apply cs |> ignore

                    let readOnly =
                        SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly, Pooling = false).ToString()

                    match Adapters.prepareStore (Some readOnly) with
                    | Error msg -> msg |> Expect.isNotEmpty "the reason it refused"
                    | Ok() -> failtest "the seed cannot have landed on a read-only file"
                )

                Adapters.prepareStore None
                |> Expect.isOk "a server without the setting has no store"
            }

            test "makeAppEnvWith without a connection string keeps the record in memory" {
                let directory = StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId
                let key = LaunchSeal.newKey Security.Cryptography.RandomNumberGenerator.GetBytes

                let env = Adapters.makeAppEnvWith true None key directory (StubMail.make ()).port (unloadedProvider ())

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
                            let inner = SqlSessions.store ignore cs (fun () -> t0)

                            { inner with
                                persist =
                                    fun writes ->
                                        // only a signature meets the other port: every other
                                        // request writes its own rows and passes
                                        if (versionOf writes).IsSome then
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
                            reopened.Head
                            |> Option.map StoredVersion.id
                            |> Expect.equal "opens the winner" (Some winner)
                        | None -> failtest "the losing Session should open the winning row"
                    }
                )

            testOnFile
                "a Session outlives the server that opened it"
                (fun cs ->
                    async {
                        let first, directory, _ = portOn cs
                        let! sid, _ = openAs first directory "n-1" "prescriber"

                        // another server over the same file, holding nothing in memory
                        let second, _, _ = portOn cs

                        match! second.find sid with
                        | SessionLookup.Found opened ->
                            opened.User
                            |> Option.map _.DisplayName
                            |> Expect.equal "the User it opened for" (Some "Stub Prescriber")
                        | other -> failtest $"expected the Session, got %A{other}"
                    }
                )

            testOnFile
                "two launches of the same User: the newer stands, the older is told"
                (fun cs ->
                    async {
                        let a, directoryA, _ = portOn cs
                        let! first, _ = openAs a directoryA "n-1" "prescriber"

                        // a second server, which knows nothing of the first Session but its rows
                        let b, directoryB, _ = portOn cs
                        let! second, _ = openAs b directoryB "n-2" "prescriber"

                        second |> Expect.notEqual "a Session of its own" first

                        match! a.find first with
                        | SessionLookup.Ended SessionEnding.SupersededByLaunch -> ()
                        | other -> failtest $"expected the older Session superseded, got %A{other}"

                        match! b.find second with
                        | SessionLookup.Found _ -> ()
                        | other -> failtest $"expected the newer Session open, got %A{other}"
                    }
                )

            testOnFile
                "the same Launch presented twice is answered as it was the first time"
                (fun cs ->
                    async {
                        let port, directory, _ = portOn cs
                        let! sid, _ = openAs port directory "n-1" "prescriber"

                        // the same browser, the same Launch: the outcome it already came to
                        match! port.present (mintFor "n-1" "stub-patient", keyA) with
                        | LaunchResult.Opened(id, _) -> id |> Expect.equal "the Session it opened" sid
                        | other -> failtest $"expected the open it came to, got %A{other}"

                        // another browser presenting it: spent
                        match! port.present (mintFor "n-1" "stub-patient", PublicKey "key-B") with
                        | LaunchResult.Refused LaunchRefusal.LaunchSpent -> ()
                        | other -> failtest $"expected spent, got %A{other}"
                    }
                )

            testOnFile
                "an ended Session cannot reopen, on this server or another"
                (fun cs ->
                    async {
                        let port, directory, _ = portOn cs
                        let! sid, _ = openAs port directory "n-1" "prescriber"

                        do! port.close sid

                        match! port.find sid with
                        | SessionLookup.NotFound -> ()
                        | other -> failtest $"expected no Session, got %A{other}"

                        let next, _, _ = portOn cs

                        match! next.find sid with
                        | SessionLookup.NotFound -> ()
                        | other -> failtest $"expected no Session on a fresh server either, got %A{other}"
                    }
                )


            testOnFile
                "a request whose writes the store refuses sends no mail"
                (fun cs ->
                    async {
                        let readOnly = SqliteConnectionStringBuilder(cs, Mode = SqliteOpenMode.ReadOnly).ToString()

                        // the launch lands, so the hop reaches the PIN question; the writes of
                        // the callback that suspends into enrolment do not
                        let inner = SqlSessions.store ignore cs (fun () -> t0)
                        let refusing = ref false

                        let store =
                            { inner with
                                persist =
                                    fun writes ->
                                        if refusing.Value then
                                            SqlSessions.runWrites readOnly (fun () -> Store.t0) writes
                                        else
                                            inner.persist writes
                            }

                        let port, directory, outbox = portOver store
                        let! cb = presented port directory "n-1" "no-pin"
                        refusing.Value <- true

                        let! failed = fails (port.callback cb)
                        failed |> Expect.isTrue "the call fails"

                        outbox.sent () |> Expect.isEmpty "the confirmation code was never sent"

                        // a fresh hop, since the one-time code the first one carried is spent:
                        // with the store taking the writes, the code does go out
                        refusing.Value <- false
                        let! next = presented port directory "n-2" "no-pin"

                        match! port.callback next with
                        | CallbackResult.Enrolling _ ->
                            outbox.sent ()
                            |> List.exists (fun m -> m.Subject.Contains "confirmation code")
                            |> Expect.isTrue "the code went out once the writes landed"
                        | other -> failtest $"expected Enrolling, got %A{other}"
                    }
                )


            testOnFile
                "the mail of a request that writes but is not a sign goes out too"
                (fun cs ->
                    async {
                        let port, directory, outbox = portOn cs
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"

                        // three wrong PINs: the Session ends at the limit and the User is told
                        for attempt in [ "0000"; "0001"; "0002" ] do
                            let! _ =
                                port.submit
                                    sid
                                    { signature with
                                        Pin = attempt
                                        IdemKey = attempt
                                    }

                            ()

                        outbox.sent ()
                        |> List.exists (fun m -> m.Subject.Contains "signing")
                        |> Expect.isTrue $"the lock was mailed: %A{outbox.sent () |> List.map _.Subject}"

                        match! port.find sid with
                        | SessionLookup.Ended SessionEnding.WrongPinLimit -> ()
                        | other -> failtest $"expected the Session ended at the limit, got %A{other}"
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

    let cs = SqliteConnectionStringBuilder(DataSource = path, Pooling = false).ToString()

    SqlSchema.apply cs |> ignore
    // the credentials the suites sign with, in the file the port reads them from
    SqlSessions.seed cs t0 (StubCredentials.seed salts) |> ignore
    SqlSessions.store ignore cs (fun () -> t0)


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


[<Tests>]
let flightOverSqlite =
    testList
        "what a Session holds in flight, over SQLite"
        [
            testOnFile
                "a challenge outlives the server that issued it, and is answered once"
                (fun cs ->
                    async {
                        let port, directory, _ = portOn cs
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"

                        // another server, which knows nothing but the rows: the challenge the
                        // first one issued is the one this signature answers
                        let next, _, _ = portOn cs
                        let! answer = next.submit sid signature
                        answer |> noOf |> Expect.equal "order plan version 1" (Some 1)

                        // and the same Submission again, on a third server: answered once, from
                        // the row. What comes back is the version as the store holds it —
                        // rebuilt from its Dto, so equal in identity and in stored form rather
                        // than the same value the first answer carried
                        let third, _, _ = portOn cs
                        let! again = third.submit sid signature

                        again
                        |> submittedId
                        |> Expect.equal "the version it signed before" (submittedId answer)

                        let canonical outcome =
                            match outcome with
                            | SigningOutcome.Submitted(v, token) -> SqlDatabase.toJson v, token
                            | other -> failtest $"expected Submitted, got %A{other}"

                        canonical again
                        |> Expect.equal "the same version and the same token" (canonical answer)

                        rows cs |> Expect.equal "one row in the record" 1L
                    }
                )

            testOnFile
                "a challenge spent by a signature is not answered a second time"
                (fun cs ->
                    async {
                        let port, directory, _ = portOn cs
                        let! sid, opened = openAs port directory "n-1" "prescriber"
                        let! signature = challenged port sid opened "k-1"
                        let! signed = port.submit sid signature

                        let token =
                            match signed with
                            | SigningOutcome.Submitted(_, token) -> token
                            | other -> failtest $"expected Submitted, got %A{other}"

                        // the same challenge under a new key, and the token the signature
                        // minted, so that it is the challenge and not the token that refuses
                        let next, _, _ = portOn cs

                        let! again =
                            next.submit
                                sid
                                { signature with
                                    IdemKey = "k-2"
                                    Opened = token
                                }

                        match again with
                        | SigningOutcome.Refused SigningRefusal.ChallengeExpired -> ()
                        | other -> failtest $"expected the challenge spent, got %A{other}"

                        rows cs |> Expect.equal "still one row in the record" 1L
                    }
                )
        ]
