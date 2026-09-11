module Informedica.GenPRES.Server.Tests.StubAdapterTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Shared
open Shared.Types
open Shared.Models
open ServerApi

/// Stub adapters for isolated application-layer testing.
/// No IResourceProvider, no network, no data loading.
module StubAdapters =

    let private notStubbed _ =
        raise (System.NotImplementedException "not stubbed")


    let formularyAlwaysOk (returnForm: Formulary) : FormularyPort =
        {
            getFormulary = fun _ -> async { return Ok returnForm }
            getParenteralia = fun _ -> async { return Ok Parenteralia.empty }
        }


    let formularyAlwaysFails (msgs: string[]) : FormularyPort =
        {
            getFormulary = fun _ -> async { return Error msgs }
            getParenteralia = fun _ -> async { return Error msgs }
        }


    let orderContextAlwaysOk (returnCtx: OrderContext) : OrderContextPort =
        { evaluate = fun _ _ -> async { return Ok returnCtx } }


    let orderContextAlwaysFails (msgs: string[]) : OrderContextPort =
        { evaluate = fun _ _ -> async { return Error msgs } }


    let orderPlanAlwaysOk (returnPlan: OrderPlan) : OrderPlanPort =
        {
            updateOrderPlan = fun _ _ -> async { return Ok returnPlan }
            filterOrderPlan = fun _ -> async { return Ok returnPlan }
        }


    let nutritionPlanAlwaysOk (returnPlan: NutritionPlan) : NutritionPlanPort =
        {
            initNutritionPlan = fun _ -> async { return Ok returnPlan }
            addNutritionContext = fun _ -> async { return Ok returnPlan }
            removeNutritionContext = fun _ -> async { return Ok returnPlan }
            updateNutritionOrderContext = fun _ -> async { return Ok returnPlan }
            selectNutritionOrderScenario = fun _ -> async { return Ok returnPlan }
            navigateNutritionOrderContext = fun _ -> async { return Ok returnPlan }
        }


    /// A session port that opens nothing and knows nothing.
    let sessionNone: SessionPort =
        {
            present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
            callback =
                fun _ ->
                    async { return CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid") }
            find = fun _ -> async { return SessionLookup.NotFound }
            close = fun _ -> async { return () }
            findEnrolment = fun _ -> async { return None }
            supplyPin = fun _ _ _ -> async { return SupplyPinResult.Refused PinRefusal.AttemptExpired }
            dropEnrolment = fun _ -> async { return () }
        }


    let makeEnv
        (formulary: FormularyPort)
        (orderContext: OrderContextPort)
        (orderPlan: OrderPlanPort)
        (nutritionPlan: NutritionPlanPort)
        : AppEnv
        =
        {
            formulary = formulary
            orderContext = orderContext
            orderPlan = orderPlan
            nutritionPlan = nutritionPlan
            interaction =
                {
                    checkInteractions = fun _ -> async { return Ok [] }
                    getDrugNames = fun () -> async { return Ok [] }
                }
            logAnalyzer =
                {
                    listLogFiles = fun () -> async { return Ok [||] }
                    analyzeLogFile = fun _ -> async { return Ok "" }
                }
            requireLoaded = fun () -> None
            session = sessionNone
        }


    let makeEnvNotLoaded (msgs: string[]) : AppEnv =
        {
            formulary = formularyAlwaysFails [| "not loaded" |]
            orderContext = orderContextAlwaysFails [| "not loaded" |]
            orderPlan =
                {
                    updateOrderPlan = fun _ _ -> async { return Error [| "not loaded" |] }
                    filterOrderPlan = fun _ -> async { return Error [| "not loaded" |] }
                }
            nutritionPlan =
                {
                    initNutritionPlan = fun _ -> async { return Error [| "not loaded" |] }
                    addNutritionContext = fun _ -> async { return Error [| "not loaded" |] }
                    removeNutritionContext = fun _ -> async { return Error [| "not loaded" |] }
                    updateNutritionOrderContext = fun _ -> async { return Error [| "not loaded" |] }
                    selectNutritionOrderScenario = fun _ -> async { return Error [| "not loaded" |] }
                    navigateNutritionOrderContext = fun _ -> async { return Error [| "not loaded" |] }
                }
            interaction =
                {
                    checkInteractions = fun _ -> async { return Error [| "not loaded" |] }
                    getDrugNames = fun () -> async { return Error [| "not loaded" |] }
                }
            logAnalyzer =
                {
                    listLogFiles = fun () -> async { return Ok [||] }
                    analyzeLogFile = fun _ -> async { return Ok "" }
                }
            requireLoaded = fun () -> Some msgs
            session = sessionNone
        }


open StubAdapters

let emptyCtx = Models.OrderContext.empty

// Copied to TotalsTests. One more copy/paste, and by the rule of three we should consider deduplication.
// An OrderPlan.empty value seems reasonable.
let emptyPlan: OrderPlan =
    {
        Patient = Models.Patient.empty
        Scenarios = [||]
        Selected = None
        Filtered = [||]
        Totals = Models.Totals.empty
    }

let emptyNutritionPlan = Models.NutritionPlan.create Models.Patient.empty [||]


let commandRoutingTests =
    testList
        "Stub adapter command routing"
        [

            testAsync "FormularyCmd dispatches to formulary.getFormulary" {
                let returnForm = { Formulary.empty with Markdown = "stubbed" }

                let env =
                    makeEnv
                        (formularyAlwaysOk returnForm)
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.FormularyCmd Formulary.empty)

                match result with
                | Ok(Api.FormularyResp f) -> f.Markdown |> Expect.equal "should return stubbed formulary" "stubbed"
                | other -> failtest $"expected Ok FormularyResp, got {other}"
            }

            testAsync "ParenteraliaCmd dispatches to formulary.getParenteralia" {
                let env =
                    makeEnv
                        (formularyAlwaysOk Formulary.empty)
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.ParenteraliaCmd Parenteralia.empty)

                match result with
                | Ok(Api.ParenteraliaResp _) -> ()
                | other -> failtest $"expected Ok ParenteraliaResp, got {other}"
            }

            testAsync "OrderContextCmd dispatches to orderContext.evaluate" {
                let env =
                    makeEnv
                        (formularyAlwaysOk Formulary.empty)
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.OrderContextCmd(Api.UpdateOrderContext, emptyCtx))

                match result with
                | Ok(Api.OrderContextResp(Api.OrderContextResult _)) -> ()
                | other -> failtest $"expected Ok OrderContextResp, got {other}"
            }

            testAsync "NutritionPlanCmd InitNutritionPlan dispatches to nutritionPlan port" {
                let env =
                    makeEnv
                        (formularyAlwaysOk Formulary.empty)
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.NutritionPlanCmd(Api.InitNutritionPlan Models.Patient.empty))

                match result with
                | Ok(Api.NutritionPlanResp(Api.NutritionPlanInitialised _)) -> ()
                | other -> failtest $"expected Ok NutritionPlanInitialised, got {other}"
            }

            testAsync "OrderPlanCmd FilterOrderPlan dispatches to orderPlan port" {
                let env =
                    makeEnv
                        (formularyAlwaysOk Formulary.empty)
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.OrderPlanCmd(Api.FilterOrderPlan emptyPlan))

                match result with
                | Ok(Api.OrderPlanResp(Api.OrderPlanFiltered _)) -> ()
                | other -> failtest $"expected Ok OrderPlanFiltered, got {other}"
            }
        ]


let errorPropagationTests =
    testList
        "Stub adapter error propagation"
        [

            testAsync "FormularyCmd propagates port error" {
                let env =
                    makeEnv
                        (formularyAlwaysFails [| "test error" |])
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.FormularyCmd Formulary.empty)

                match result with
                | Error msgs -> msgs |> Expect.equal "should propagate error messages" [| "test error" |]
                | Ok _ -> failtest "expected Error, got Ok"
            }

            testAsync "OrderContextCmd propagates port error" {
                let env =
                    makeEnv
                        (formularyAlwaysOk Formulary.empty)
                        (orderContextAlwaysFails [| "ctx error" |])
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.OrderContextCmd(Api.UpdateOrderContext, emptyCtx))

                match result with
                | Error msgs -> msgs |> Expect.equal "should propagate order context error" [| "ctx error" |]
                | Ok _ -> failtest "expected Error, got Ok"
            }
        ]


let requireLoadedTests =
    testList
        "Stub adapter requireLoaded guard"
        [

            testAsync "requireLoaded returns Error when not loaded" {
                let env = makeEnvNotLoaded [| "not ready" |]

                let! result = Command.processCmd env (Api.FormularyCmd Formulary.empty)

                match result with
                | Error msgs -> msgs |> Expect.equal "should return requireLoaded error" [| "not ready" |]
                | Ok _ -> failtest "expected Error from requireLoaded, got Ok"
            }

            testAsync "requireLoaded passes when loaded" {
                let env =
                    makeEnv
                        (formularyAlwaysOk Formulary.empty)
                        (orderContextAlwaysOk emptyCtx)
                        (orderPlanAlwaysOk emptyPlan)
                        (nutritionPlanAlwaysOk emptyNutritionPlan)

                let! result = Command.processCmd env (Api.FormularyCmd Formulary.empty)

                match result with
                | Ok _ -> ()
                | Error msgs -> failtest $"expected Ok, got Error {msgs}"
            }
        ]


module SessionStubTests =

    open Shared.Api


    let lifetime = TimeSpan.FromMinutes 2.0

    let t0 = DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)

    let keyA = PublicKey "key-A"
    let keyB = PublicKey "key-B"

    /// A deterministic salt source for the tests.
    let salts (n: int) = Array.init n byte

    /// The state a stub host starts from: the seeded credentials (plan 615).
    let seeded = Hop.initialState (StubCredentials.seed salts)


    /// The seal key of the tests, and another one.
    let sealKey = LaunchSeal.Key(Array.init LaunchSeal.keyLength byte)

    let otherSealKey =
        LaunchSeal.Key(Array.init LaunchSeal.keyLength (fun i -> byte (i + 1)))


    /// A Launch sealed under the test key for the stub patient, valid for the lifetime from t0.
    let mintFor nonce pid =
        LaunchSeal.mint
            sealKey
            {
                PatientId = pid
                Nonce = nonce
                Expiry = t0 + lifetime
            }

    let launch1 = mintFor "n-1" "stub-patient"
    let launch2 = mintFor "n-2" "p-2"


    let codeMac = Hop.codeMac sealKey


    /// Confirmation codes are numbered, so that a test can name the one that was mailed.
    let codes () =
        let n = ref 0

        fun () ->
            n.Value <- n.Value + 1
            $"%06d{n.Value}"


    /// A port with a settable clock, a counting id source, the seal check bound to the clock, a
    /// stub directory of its own and the given outbox.
    let makePortWith (outbox: StubMail.Outbox) =
        let clock = ref t0
        let count = ref 0

        let directory =
            StubDirectory.make (fun () -> clock.Value) (fun () -> $"code-{Guid.NewGuid()}")

        let port =
            Hop.makeSessionPort
                (fun () -> clock.Value)
                (fun () ->
                    count.Value <- count.Value + 1
                    $"session-{count.Value}"
                )
                (codes ())
                salts
                codeMac
                (fun launch -> LaunchSeal.verify clock.Value sealKey launch)
                directory.idp
                directory.registry
                StubPatientData.port
                outbox.port
                seeded

        port, clock, directory


    let makePort () = makePortWith (StubMail.make ())


    let thumbprintTests =
        testList
            "PublicKey.thumbprint"
            [
                test "RFC 7638 section 3.1 example" {
                    // members deliberately out of order and with whitespace: the thumbprint
                    // covers the required members only, in lexicographic order, unpadded
                    let jwk =
                        """{
                          "kty": "RSA",
                          "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
                          "e": "AQAB",
                          "alg": "RS256",
                          "kid": "2011-04-29"
                        }"""

                    PublicKey jwk
                    |> PublicKey.thumbprint
                    |> Expect.equal "should match the RFC example" "NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs"
                }

                test "EC key: only crv, kty, x, y count" {
                    let a = PublicKey """{"kty":"EC","crv":"P-256","x":"X","y":"Y","kid":"one"}"""
                    let b = PublicKey """{"y":"Y","x":"X","crv":"P-256","kty":"EC","use":"sig"}"""

                    PublicKey.thumbprint a
                    |> Expect.equal "should ignore order and extra members" (PublicKey.thumbprint b)
                }

                test "text that is not a JWK still gets a stable thumbprint" {
                    PublicKey.thumbprint keyA
                    |> Expect.equal "should be deterministic" (PublicKey.thumbprint keyA)

                    PublicKey.thumbprint keyA
                    |> Expect.notEqual "should differ per key" (PublicKey.thumbprint keyB)
                }
            ]


    /// The hop over the stubs: the script tests of Server/Scripts/Hop.fsx, unchanged.
    module HopTests =

        let verifyAt (now: DateTime) = LaunchSeal.verify now sealKey
        let launch1 = mintFor "n-1" "patient-1"


        let counter prefix =
            let n = ref 0

            fun () ->
                n.Value <- n.Value + 1
                $"{prefix}-{n.Value}"


        /// A fresh directory and id sources per test.
        let fixture () =
            let ids = counter "id"
            let directory = StubDirectory.make (fun () -> t0) (counter "code")
            ids, directory


        /// Presents, then plays the stub IdP for `choice`, and returns the callback the browser brings.
        let hop (ids: unit -> string) (directory: StubDirectory.Directory) state launch key choice =
            let state, result =
                Hop.present t0 ids (verifyAt t0) directory.idp.authorizeUrl state (launch, key)

            match result with
            | LaunchResult.RedirectTo(_, st) ->
                let cb =
                    if choice = "none" then
                        {
                            State = st
                            StateCookie = Some st
                            Code = None
                            Error = Some "no-identity"
                        }
                    else
                        {
                            State = st
                            StateCookie = Some st
                            Code = Some(directory.issue choice "patient-1")
                            Error = None
                        }

                state, cb
            | other -> failtest $"expected RedirectTo, got {other}"


        /// The callback at t0 with a throwaway outbox: for the tests that never suspend.
        let run (ids: unit -> string) (directory: StubDirectory.Directory) state cb =
            Hop.callback
                t0
                ids
                (codes ())
                codeMac
                directory.idp.redeem
                directory.registry.standing
                StubPatientData.port.read
                (StubMail.make ()).port.send
                state
                cb


        let presentTests =
            testList
                "Hop.present"
                [
                    test "a sealed Launch is recorded under its nonce and sent to the IdentityProvider" {
                        let ids, d = fixture ()

                        let state, result =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl seeded (launch1, keyA)

                        match result with
                        | LaunchResult.RedirectTo(url, st) ->
                            url |> Expect.equal "authorize url carries the state" $"/authorize?state={st}"
                            let record = state.Launches["n-1"]
                            record.State |> Expect.equal "same state in the record" st
                            record.PatientId |> Expect.equal "patient" "patient-1"
                            record.Outcome |> Expect.isNone "no outcome yet"
                        | other -> failtest $"expected RedirectTo, got {other}"
                    }

                    test "the same key while the hop is open gets the same redirect (Rule 2, retry)" {
                        let ids, d = fixture ()

                        let state, first =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl seeded (launch1, keyA)

                        let state2, again =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyA)

                        again |> Expect.equal "same redirect" first
                        state2 |> Expect.equal "same state" state
                    }

                    test "another key is spent" {
                        let ids, d = fixture ()

                        let state, _ =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl seeded (launch1, keyA)

                        let _, other =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyB)

                        other |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)
                    }

                    test "after the hop opened, the same key gets the opened Session (Rule 2)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "prescriber"
                        let state, opened = run ids d state cb

                        let _, again =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyA)

                        match opened, again with
                        | CallbackResult.Opened(id, _), LaunchResult.Opened(id', session) ->
                            id' |> Expect.equal "same session" id

                            session.User
                            |> Option.map _.DisplayName
                            |> Expect.equal "user" (Some "Stub Prescriber")
                        | other -> failtest $"expected Opened twice, got {other}"
                    }

                    test "not sealed, or expired: refused and nothing recorded" {
                        let ids, d = fixture ()

                        let _, invalid =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl seeded (Launch "junk", keyA)

                        invalid
                        |> Expect.equal "invalid" (LaunchResult.Refused LaunchRefusal.LaunchInvalid)

                        let late = t0 + lifetime + TimeSpan.FromSeconds 1.0

                        let state, expired =
                            Hop.present late ids (verifyAt late) d.idp.authorizeUrl seeded (launch1, keyA)

                        expired
                        |> Expect.equal "expired" (LaunchResult.Refused LaunchRefusal.LaunchExpired)

                        state.Launches |> Map.isEmpty |> Expect.isTrue "nothing recorded"
                    }
                ]


        let callbackTests =
            testList
                "Hop.callback"
                [
                    test "prescriber: the Session opens for the Launch's Patient, the outcome is recorded" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "prescriber"
                        let state, result = run ids d state cb

                        match result with
                        | CallbackResult.Opened(id, url) ->
                            url |> Expect.equal "to the app" "/#/session"
                            let record = state.Sessions[id]
                            record.Login |> Expect.equal "login" (Some "prescriber")

                            record.Session.User
                            |> Option.map _.Role
                            |> Expect.equal "role" (Some UserRole.Prescriber)

                            record.Session.PatientContext
                            |> Option.map _.PatientId
                            |> Expect.equal "patient" (Some "patient-1")

                            record.Session.KeyThumbprint
                            |> Expect.equal "thumbprint" (Some(PublicKey.thumbprint keyA))

                            record.OpenedWith |> Expect.isNone "opened from nothing (Rule 19)"
                            state.Launches["n-1"].Outcome |> Expect.isSome "outcome appended"
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "prescriber over a record: the Session opens with the newest version as its head (Rule 19)" {
                        let ids, d = fixture ()

                        let signedAs userId no : SignedOrderPlan =
                            {
                                Head =
                                    {
                                        Id = $"plan-{no}"
                                        No = no
                                        By =
                                            {
                                                UserId = userId
                                                DisplayName = userId
                                                Role = UserRole.Prescriber
                                            }
                                        SignedAt = t0
                                    }
                                PatientId = "patient-1"
                                Base = (if no > 1 then Some $"plan-{no - 1}" else None)
                                Scenarios = [||]
                                Patient = Shared.Models.Patient.empty
                            }

                        let record =
                            { seeded with
                                Records =
                                    Map.ofList
                                        [
                                            "patient-1", [ signedAs "prescriber-b" 2; signedAs "prescriber" 1 ]
                                            "patient-2", [ signedAs "prescriber" 1 ]
                                        ]
                            }

                        Hop.headOf "patient-1" record
                        |> Option.map _.Head.Id
                        |> Expect.equal "newest first" (Some "plan-2")

                        Hop.headOf "patient-3" record |> Expect.isNone "no record"

                        let state, cb = hop ids d record launch1 keyA "prescriber"
                        let state, result = run ids d state cb

                        match result with
                        | CallbackResult.Opened(id, _) ->
                            state.Sessions[id].OpenedWith |> Expect.equal "the head at open" (Some "plan-2")
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "reader: opens without a PIN (ext 5c)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "reader"
                        let state, result = run ids d state cb

                        match result with
                        | CallbackResult.Opened(id, _) ->
                            state.Sessions[id].Session.User
                            |> Option.map _.Role
                            |> Expect.equal "role" (Some UserRole.Reader)
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    testList
                        "refusals, each recorded and each with its word"
                        [
                            for choice, refusal in
                                [
                                    "none", LaunchRefusal.NoBrowserIdentity
                                    "unknown", LaunchRefusal.NoRole
                                    "prescriber-other-patient", LaunchRefusal.WrongActivePatient
                                ] do
                                test choice {
                                    let ids, d = fixture ()
                                    let state, cb = hop ids d seeded launch1 keyA choice
                                    let state, result = run ids d state cb

                                    result
                                    |> Expect.equal
                                        "refused"
                                        (CallbackResult.Refused(
                                            refusal,
                                            $"/#/session?refused={Hop.refusalWord refusal}"
                                        ))

                                    state.Sessions |> Map.isEmpty |> Expect.isTrue "no session (Rule 7)"

                                    state.Launches["n-1"].Outcome
                                    |> Expect.equal "recorded" (Some(LaunchResult.Refused refusal))
                                }
                        ]

                    test "a callback reload gets the same answer without a second redeem (Rule 45)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "prescriber"
                        let state, first = run ids d state cb
                        // the code was consumed by the first redeem; the replay must not need it
                        let state2, again = run ids d state cb
                        again |> Expect.equal "same answer" first
                        state2 |> Expect.equal "same state" state
                    }

                    test "a state cookie that does not match is invalid, and nothing is redeemed" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "prescriber"

                        for cookie in [ None; Some "other" ] do
                            let state2, result = run ids d state { cb with StateCookie = cookie }

                            result
                            |> Expect.equal
                                $"cookie {cookie}"
                                (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))

                            state2.Launches["n-1"].Outcome |> Expect.isNone "hop still open"
                    }

                    test "an unknown state is invalid" {
                        let ids, d = fixture ()

                        let _, result =
                            run
                                ids
                                d
                                seeded
                                {
                                    State = "nope"
                                    StateCookie = Some "nope"
                                    Code = Some "c"
                                    Error = None
                                }

                        result
                        |> Expect.equal
                            "invalid"
                            (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))
                    }

                    test "a code that does not redeem is no browser identity" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "prescriber"
                        let _, result = run ids d state { cb with Code = Some "forged" }

                        result
                        |> Expect.equal
                            "no identity"
                            (CallbackResult.Refused(LaunchRefusal.NoBrowserIdentity, "/#/session?refused=no-identity"))
                    }

                    test "after the lifetime the callback is invalid: the record is gone (Rule 29)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d seeded launch1 keyA "prescriber"
                        let late = t0 + lifetime + TimeSpan.FromSeconds 1.0

                        let state2, result =
                            Hop.callback
                                late
                                ids
                                (codes ())
                                codeMac
                                d.idp.redeem
                                d.registry.standing
                                StubPatientData.port.read
                                (StubMail.make ()).port.send
                                state
                                cb

                        result
                        |> Expect.equal
                            "invalid"
                            (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))

                        state2.Launches |> Map.isEmpty |> Expect.isTrue "dropped"
                    }

                    test "no patient data (ext 6a): the Session opens with the PatientId and an empty Patient" {
                        let ids, d = fixture ()
                        let launch = mintFor "n-nd" "no-data"

                        let state, result =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl seeded (launch, keyA)

                        let st =
                            match result with
                            | LaunchResult.RedirectTo(_, st) -> st
                            | other -> failtest $"{other}"

                        let cb =
                            {
                                State = st
                                StateCookie = Some st
                                Code = Some(d.issue "prescriber" "no-data")
                                Error = None
                            }

                        let state, result = run ids d state cb

                        match result with
                        | CallbackResult.Opened(id, _) ->
                            let ctx = state.Sessions[id].Session.PatientContext
                            ctx |> Option.map _.PatientId |> Expect.equal "patient id" (Some "no-data")
                            ctx |> Option.map _.Patient |> Expect.equal "empty patient" (Some Patient.empty)
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "a second launch of the same login closes the first Session and marks it (Rule 8)" {
                        let ids, d = fixture ()
                        let state, cb1 = hop ids d seeded launch1 keyA "prescriber"
                        let state, first = run ids d state cb1
                        let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                        let state, second = run ids d state cb2

                        match first, second with
                        | CallbackResult.Opened(id1, _), CallbackResult.Opened(id2, _) ->
                            state.Sessions |> Map.containsKey id1 |> Expect.isFalse "first closed"
                            state.Sessions |> Map.containsKey id2 |> Expect.isTrue "second open"

                            state.Endings
                            |> Map.tryFind id1
                            |> Option.map fst
                            |> Expect.equal "marked" (Some SessionEnding.SupersededByLaunch)
                        | other -> failtest $"expected two Opened, got {other}"
                    }

                    test "a callback reload after a newer launch of the same login does not hand back the dead Session" {
                        let ids, d = fixture ()
                        let state, cb1 = hop ids d seeded launch1 keyA "prescriber"
                        let state, _ = run ids d state cb1
                        let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                        let state, _ = run ids d state cb2
                        let state2, again = run ids d state cb1
                        again |> Expect.equal "superseded" (CallbackResult.Superseded "/#/session")
                        state2 |> Expect.equal "unchanged" state
                    }

                    test
                        "the ended Session is told at every lookup that still carries the cookie, until its lifetime (Rule 11)" {
                        let ids, d = fixture ()
                        let state, cb1 = hop ids d seeded launch1 keyA "prescriber"
                        let state, first = run ids d state cb1
                        let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                        let state, _ = run ids d state cb2

                        let id1 =
                            match first with
                            | CallbackResult.Opened(id, _) -> id
                            | other -> failtest $"{other}"

                        let state, told = Hop.find id1 state

                        told
                        |> Expect.equal "told" (SessionLookup.Ended SessionEnding.SupersededByLaunch)

                        // the answer was lost, or the tab comes back much later: the cookie came
                        // again, so is the ending
                        let _, again = Hop.find id1 state

                        again
                        |> Expect.equal "told again" (SessionLookup.Ended SessionEnding.SupersededByLaunch)
                    }

                    test "two logins keep two Sessions" {
                        let ids, d = fixture ()
                        let state, cb1 = hop ids d seeded launch1 keyA "prescriber"
                        let state, _ = run ids d state cb1
                        let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "reader"
                        let state, _ = run ids d state cb2
                        state.Sessions |> Map.count |> Expect.equal "two" 2
                        state.Endings |> Map.isEmpty |> Expect.isTrue "none ended"
                    }
                ]


        let portTests =
            testList
                "makeSessionPort"
                [
                    testAsync "present, callback, find, close over the port" {
                        let ids, d = fixture ()

                        let port =
                            Hop.makeSessionPort
                                (fun () -> t0)
                                ids
                                (codes ())
                                salts
                                codeMac
                                (verifyAt t0)
                                d.idp
                                d.registry
                                StubPatientData.port
                                (StubMail.make ()).port
                                seeded

                        let! redirect = port.present (launch1, keyA)

                        let st =
                            match redirect with
                            | LaunchResult.RedirectTo(_, st) -> st
                            | other -> failtest $"{other}"

                        let! opened =
                            port.callback
                                {
                                    State = st
                                    StateCookie = Some st
                                    Code = Some(d.issue "prescriber" "patient-1")
                                    Error = None
                                }

                        match opened with
                        | CallbackResult.Opened(id, _) ->
                            match! port.find id with
                            | SessionLookup.Found session ->
                                session.User |> Option.map _.UserId |> Expect.equal "found" (Some "prescriber")
                            | other -> failtest $"expected Found, got {other}"

                            do! port.close id
                            let! gone = port.find id
                            gone |> Expect.equal "closed" SessionLookup.NotFound
                        | other -> failtest $"expected Opened, got {other}"
                    }
                ]


        let identityCookieTests =
            testList
                "StubLaunch identity cookie"
                [
                    test "round trip, with characters the cookie cannot carry" {
                        StubLaunch.identityCookie "prescriber" "p 1;2"
                        |> Some
                        |> StubLaunch.parseIdentityCookie
                        |> Expect.equal "same" (Some("prescriber", "p 1;2"))
                    }

                    test "an unknown choice, garbage or nothing is None" {
                        for v in [ Some "hacker.p"; Some "prescriber"; Some ""; None ] do
                            StubLaunch.parseIdentityCookie v |> Expect.isNone $"{v}"
                    }
                ]


        let directoryTests =
            testList
                "StubDirectory"
                [
                    test "past the lifetime a code does not redeem, and the next issue prunes it" {
                        let clock = ref t0
                        let d = StubDirectory.make (fun () -> clock.Value) (counter "code")
                        let stale = d.issue "prescriber" "p"
                        clock.Value <- t0 + StubDirectory.codeLifetime + TimeSpan.FromSeconds 1.0
                        d.idp.redeem stale |> Expect.isNone "stale"
                        let fresh = d.issue "reader" "p"
                        d.idp.redeem fresh |> Option.map _.Login |> Expect.equal "fresh" (Some "reader")
                    }

                    test "a code redeems once" {
                        let d = StubDirectory.make (fun () -> t0) (counter "code")
                        let code = d.issue "prescriber" "p"

                        d.idp.redeem code
                        |> Option.map _.Login
                        |> Expect.equal "first" (Some "prescriber")

                        d.idp.redeem code |> Expect.isNone "second"
                    }

                    test "every choice but none has an identity; unknown has no standing" {
                        let d = StubDirectory.make (fun () -> t0) (counter "code")

                        for choice in StubDirectory.choices |> List.filter ((<>) "none") do
                            let identity = d.idp.redeem (d.issue choice "p") |> Option.get
                            identity.Login |> Expect.equal "login" choice

                            match choice, d.registry.standing identity with
                            | "unknown", None -> ()
                            | "unknown", Some _ -> failtest "unknown has standing"
                            | _, None -> failtest $"{choice} has no standing"
                            | _, Some _ -> ()
                    }
                ]


        let pinHashTests =
            testList
                "PinHash"
                [
                    test "the PIN it was made from verifies" {
                        PinHash.make salts "1234" |> PinHash.verify "1234" |> Expect.isTrue "verifies"
                    }

                    test "another PIN does not" {
                        let hash = PinHash.make salts "1234"
                        hash |> PinHash.verify "1235" |> Expect.isFalse "wrong PIN"
                        hash |> PinHash.verify "" |> Expect.isFalse "empty PIN"
                    }

                    test "the same PIN under another salt is another hash" {
                        let a = PinHash.make salts "1234"
                        let b = PinHash.make (fun n -> Array.init n (fun i -> byte (i + 1))) "1234"
                        a.Hash |> Expect.notEqual "different hashes" b.Hash
                        b |> PinHash.verify "1234" |> Expect.isTrue "still verifies"
                    }

                    test "the hash is never the PIN, and has the declared lengths" {
                        let hash = PinHash.make salts "1234"
                        hash.Salt.Length |> Expect.equal "salt" PinHash.saltLength
                        hash.Hash.Length |> Expect.equal "hash" PinHash.hashLength
                        Text.Encoding.UTF8.GetString hash.Hash |> Expect.notEqual "not the PIN" "1234"
                    }
                ]


        let credentialTests =
            let minutes (n: float) = TimeSpan.FromMinutes n
            let withPin = Credential.withPin salts "1234"

            /// Wrong entries in a row, each a minute after the last; the credential and the
            /// time of the next entry.
            let wrong (n: int) (start: DateTime) (credential: Credential) =
                [ 1..n ]
                |> List.fold
                    (fun (c, at) _ -> Credential.verify at "0000" c |> snd, at + minutes 1.0)
                    (credential, start)

            testList
                "Credential"
                [
                    test "an empty credential has no PIN set (Rule 24)" {
                        Credential.empty |> Credential.pinSet |> Expect.isFalse "no PIN"
                        Credential.empty.LockedUntil |> Expect.isNone "no lock"
                    }

                    test "withPin sets the PIN, a count of zero and no lock (Rules 28, 37)" {
                        let c = Credential.withPin salts "1234"
                        c |> Credential.pinSet |> Expect.isTrue "set"
                        c.WrongCount |> Expect.equal "zero" 0
                        c.LockedUntil |> Expect.isNone "no lock"

                        let locked, _ = wrong 4 t0 withPin
                        locked.WrongCount |> Expect.equal "(four wrong entries)" 4
                        let reset = Credential.withPin salts "2468"
                        reset.WrongCount |> Expect.equal "a new PIN zeroes the count" 0
                        reset.LockedUntil |> Expect.isNone "and clears the lock"

                        c.PinHash
                        |> Option.map (PinHash.verify "1234")
                        |> Expect.equal "verifies" (Some true)
                    }

                    test "the stub seed: the signing Prescribers have the stub PIN, no-pin has none" {
                        let seed = StubCredentials.seed salts

                        for login in [ "prescriber"; "prescriber-other-patient" ] do
                            seed[login] |> Credential.pinSet |> Expect.isTrue $"{login} set"

                            seed[login].PinHash
                            |> Option.map (PinHash.verify StubCredentials.stubPin)
                            |> Expect.equal $"{login} verifies" (Some true)

                        seed["no-pin"] |> Credential.pinSet |> Expect.isFalse "no-pin unset"
                        seed |> Map.containsKey "reader" |> Expect.isFalse "a Reader has no credential"
                    }

                    test "the delay is one minute at the limit and doubles with each further entry (Rule 28)" {
                        Credential.lockFor 0 |> Expect.equal "below the limit: the base" (minutes 1.0)
                        Credential.lockFor 3 |> Expect.equal "at the limit" (minutes 1.0)
                        Credential.lockFor 4 |> Expect.equal "one past" (minutes 2.0)
                        Credential.lockFor 5 |> Expect.equal "two past" (minutes 4.0)
                    }

                    test "a right PIN is accepted, zeroes the count and clears the lock" {
                        let twoWrong, at = wrong 2 t0 withPin
                        twoWrong.WrongCount |> Expect.equal "two" 2
                        twoWrong.LockedUntil |> Expect.isNone "not locked yet"
                        twoWrong |> Credential.attemptsLeft |> Expect.equal "one left" 1

                        let ok, after = Credential.verify at "1234" twoWrong
                        ok |> Expect.isTrue "accepted"
                        after.WrongCount |> Expect.equal "zeroed" 0
                        after.LockedUntil |> Expect.isNone "no lock"
                    }

                    test "the third wrong PIN locks for a minute; a right PIN inside it is refused and counts nothing" {
                        let limit, at = wrong 3 t0 withPin
                        limit.WrongCount |> Expect.equal "three" 3
                        limit.LockedUntil |> Expect.equal "a minute from the third entry" (Some at)

                        limit
                        |> Credential.isLocked (at - minutes 0.5)
                        |> Expect.isTrue "locked inside the minute"

                        limit |> Credential.attemptsLeft |> Expect.equal "none left" 0

                        let ok, same = Credential.verify (at - minutes 0.5) "1234" limit
                        ok |> Expect.isFalse "refused while locked"
                        same |> Expect.equal "unchanged" limit

                        let ok, after = Credential.verify at "1234" limit
                        ok |> Expect.isTrue "accepted once the minute passed"
                        after.WrongCount |> Expect.equal "zeroed" 0
                    }

                    test "a wrong PIN while locked counts, and pushes the delay out and doubles it" {
                        let limit, at = wrong 3 t0 withPin
                        let inside = at - minutes 0.5

                        let ok, fourth = Credential.verify inside "0000" limit
                        ok |> Expect.isFalse "refused"
                        fourth.WrongCount |> Expect.equal "four" 4

                        fourth.LockedUntil
                        |> Expect.equal "two minutes from this entry" (Some(inside + minutes 2.0))

                        let _, fifth = Credential.verify inside "0000" fourth
                        fifth.LockedUntil |> Expect.equal "four minutes" (Some(inside + minutes 4.0))
                    }

                    test "a credential without a PIN accepts nothing and counts the entry" {
                        let ok, after = Credential.verify t0 "1234" Credential.empty
                        ok |> Expect.isFalse "no PIN"
                        after.WrongCount |> Expect.equal "counted" 1
                    }

                    test "credentialOf answers the empty credential for a person the store does not know" {
                        Hop.credentialOf "nobody" seeded |> Expect.equal "empty" Credential.empty
                        Hop.credentialOf "no-pin" seeded |> Expect.equal "seeded" Credential.empty

                        Hop.credentialOf "prescriber" seeded
                        |> Credential.pinSet
                        |> Expect.isTrue "seeded with PIN"
                    }
                ]


        let standingTests =
            testList
                "StubDirectory.standing"
                [
                    test "the registry answers the mail address and no longer the PIN" {
                        let _, d = fixture ()
                        let identity = d.issue "prescriber" "patient-1" |> d.idp.redeem |> Option.get

                        match d.registry.standing identity with
                        | Some standing ->
                            standing.MailAddress |> Expect.equal "address" "prescriber@stub.example"
                            standing.ActivePatientId |> Expect.equal "active" (Some "patient-1")
                            standing.User.Role |> Expect.equal "role" UserRole.Prescriber
                        | None -> failtest "expected a standing"
                    }

                    test "no-pin is a Prescriber with the launch's patient active" {
                        let _, d = fixture ()
                        let identity = d.issue "no-pin" "patient-1" |> d.idp.redeem |> Option.get
                        let standing = d.registry.standing identity |> Option.get
                        standing.User.Role |> Expect.equal "role" UserRole.Prescriber
                        standing.ActivePatientId |> Expect.equal "active" (Some "patient-1")
                    }
                ]


        let credentialStoreTests =
            testList
                "Hop.callback over the credential store"
                [
                    test "a Prescriber the store does not know at all has no PIN either, and suspends" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                        let _, result = run ids d state cb

                        match result with
                        | CallbackResult.Enrolling _ -> ()
                        | other -> failtest $"expected Enrolling, got {other}"
                    }

                    test "a Reader is never asked for a PIN (Rule 26)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "reader"
                        let _, result = run ids d state cb

                        match result with
                        | CallbackResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "a PIN set in the store lets the next launch open" {
                        let ids, d = fixture ()

                        let state =
                            { seeded with
                                Credentials = seeded.Credentials |> Map.add "no-pin" (Credential.withPin salts "2468")
                            }

                        let state, cb = hop ids d state launch1 keyA "no-pin"
                        let _, result = run ids d state cb

                        match result with
                        | CallbackResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"
                    }
                ]


        let mailTests =
            testList
                "StubMail"
                [
                    test "the outbox lists what was sent, newest first" {
                        let outbox = StubMail.make ()
                        outbox.sent () |> Expect.isEmpty "nothing yet"

                        outbox.port.send
                            {
                                To = "a@stub.example"
                                Subject = "first"
                                Body = "1"
                            }

                        outbox.port.send
                            {
                                To = "b@stub.example"
                                Subject = "second"
                                Body = "2"
                            }

                        outbox.sent ()
                        |> List.map _.Subject
                        |> Expect.equal "newest first" [ "second"; "first" ]
                    }

                    test "the page shows every mail, HTML-encoded, and says when there is none" {
                        StubMail.page [] |> Expect.stringContains "empty" "No mail sent yet"

                        let page =
                            StubMail.page
                                [
                                    {
                                        To = "a@stub.example"
                                        Subject = "Your code <b>"
                                        Body = "code 123456\n& more"
                                    }
                                ]

                        page |> Expect.stringContains "subject encoded" "Your code &lt;b&gt;"
                        page |> Expect.stringContains "body encoded" "code 123456\n&amp; more"
                        page |> Expect.stringContains "address" "a@stub.example"
                        page.Contains "<script" |> Expect.isFalse "no script"
                    }
                ]


        type Fixture =
            {
                ids: unit -> string
                d: StubDirectory.Directory
                outbox: StubMail.Outbox
                newCode: unit -> string
            }


        let enrolFixture () =
            {
                ids = counter "id"
                d = StubDirectory.make (fun () -> t0) (counter "code")
                outbox = StubMail.make ()
                newCode = codes ()
            }


        let hopE (f: Fixture) state launch key choice =
            let state, result =
                Hop.present t0 f.ids (verifyAt t0) f.d.idp.authorizeUrl state (launch, key)

            match result with
            | LaunchResult.RedirectTo(_, st) ->
                state,
                {
                    State = st
                    StateCookie = Some st
                    Code = Some(f.d.issue choice "patient-1")
                    Error = None
                }
            | other -> failtest $"expected RedirectTo, got {other}"


        let runAt now (f: Fixture) state cb =
            Hop.callback
                now
                f.ids
                f.newCode
                codeMac
                f.d.idp.redeem
                f.d.registry.standing
                StubPatientData.port.read
                f.outbox.port.send
                state
                cb


        let runE f state cb = runAt t0 f state cb


        /// Launches `choice` and expects the suspension; returns the state and the attempt.
        let suspendVia (f: Fixture) state launch key choice =
            let state, cb = hopE f state launch key choice
            let state, result = runE f state cb

            match result with
            | CallbackResult.Enrolling(attempt, redirect, until) ->
                redirect |> Expect.equal "to the app" "/#/session"
                until |> Expect.equal "until the code expires" (t0 + Hop.codeLifetime)
                state, attempt
            | other -> failtest $"expected Enrolling, got {other}"


        let supplyAt now (f: Fixture) state attempt code pin =
            Hop.supplyPin
                now
                f.ids
                salts
                codeMac
                f.d.registry.standing
                StubPatientData.port.read
                f.outbox.port.send
                attempt
                code
                pin
                state


        let supply f state attempt code pin = supplyAt t0 f state attempt code pin


        /// The code the newest confirmation mail carries, from its body.
        let mailedCode (f: Fixture) =
            let body =
                (f.outbox.sent () |> List.find (fun m -> m.Subject.Contains "confirmation code")).Body

            let i = body.IndexOf "code is " + 8
            body.Substring(i, 6)


        let helperTests =
            testList
                "helpers"
                [
                    test "a PIN is four to six digits" {
                        for ok in [ "1234"; "12345"; "123456"; "0000" ] do
                            Pin.isValid ok |> Expect.isTrue ok

                        for bad in [ "123"; "1234567"; "12a4"; ""; "12 34"; "١٢٣٤" ] do
                            Pin.isValid bad |> Expect.isFalse bad
                    }

                    test "the mail hint keeps the first letter and the domain" {
                        MailHint.ofAddress "no-pin@stub.example"
                        |> Expect.equal "hint" "n***@stub.example"

                        MailHint.ofAddress "x" |> Expect.equal "no at" "***"
                    }

                    test "codes are six digits" {
                        Hop.newCode (fun _ -> 42) () |> Expect.equal "padded" "000042"
                        Hop.newCode (fun _ -> 999_999) () |> Expect.equal "max" "999999"
                    }

                    test "the code mac is keyed" {
                        codeMac "123456"
                        |> Expect.notEqual "another key" (Hop.codeMac (LaunchSeal.Key(Array.zeroCreate 32)) "123456")

                        codeMac "123456" |> Expect.equal "deterministic" (codeMac "123456")
                    }
                ]


        let suspendTests =
            testList
                "the launch suspends (uc-02)"
                [
                    test "a Prescriber without a PIN is not refused: an attempt and a code, one mail (Rules 7, 25, 27)" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        state.Sessions |> Map.isEmpty |> Expect.isTrue "no Session yet (Rule 7)"
                        let e = state.Enrolments[attempt]
                        e.UserId |> Expect.equal "person" "no-pin"
                        e.PatientId |> Expect.equal "patient" "patient-1"
                        e.PublicKey |> Expect.equal "the browser's key" keyA

                        state.Codes["no-pin"].Expiry
                        |> Expect.equal "code lifetime" (t0 + Hop.codeLifetime)

                        state.Launches["n-1"].Outcome
                        |> Expect.equal "recorded" (Some(LaunchResult.Enrolling attempt))

                        match f.outbox.sent () with
                        | [ mail ] ->
                            mail.To |> Expect.equal "the registry's address" "no-pin@stub.example"
                            mail.Body |> Expect.stringContains "the code" (mailedCode f)
                            mail.Body |> Expect.stringContains "the lifetime" "15 minutes"
                        | other -> failtest $"expected one mail, got {other.Length}"
                    }

                    test "a Prescriber with a PIN, a Reader, an unknown login: as before" {
                        let f = enrolFixture ()
                        let state, cb = hopE f seeded launch1 keyA "prescriber"
                        let state, opened = runE f state cb

                        match opened with
                        | CallbackResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"

                        let state, cb = hopE f state (mintFor "n-2" "patient-1") keyB "reader"
                        let state, opened = runE f state cb

                        match opened with
                        | CallbackResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"

                        let state, cb = hopE f state (mintFor "n-3" "patient-1") keyB "unknown"
                        let _, refused = runE f state cb

                        refused
                        |> Expect.equal
                            "no role"
                            (CallbackResult.Refused(LaunchRefusal.NoRole, "/#/session?refused=no-role"))

                        f.outbox.sent () |> Expect.isEmpty "no mail for any of them"
                    }

                    test
                        "a second launch while the code stands gets its own attempt and no second mail (Rule 37, ext 2a)" {
                        let f = enrolFixture ()
                        let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                        let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                        a2 |> Expect.notEqual "another attempt" a1
                        state.Enrolments[a1].PublicKey |> Expect.equal "first keeps its key" keyA
                        state.Enrolments[a2].PublicKey |> Expect.equal "second has its own" keyB
                        state.Codes |> Map.count |> Expect.equal "one code" 1
                        f.outbox.sent () |> List.length |> Expect.equal "one mail" 1
                    }

                    test "a callback reload while the attempt stands is answered with it again (Rule 45)" {
                        let f = enrolFixture ()
                        let state, cb = hopE f seeded launch1 keyA "no-pin"
                        let state, first = runE f state cb
                        let state2, again = runE f state cb
                        again |> Expect.equal "same answer" first
                        state2 |> Expect.equal "same state" state
                        f.outbox.sent () |> List.length |> Expect.equal "still one mail" 1
                    }

                    test "a callback reload after the attempt is gone asks for a relaunch" {
                        let f = enrolFixture ()
                        let state, cb = hopE f seeded launch1 keyA "no-pin"
                        let state, first = runE f state cb

                        let attempt =
                            match first with
                            | CallbackResult.Enrolling(a, _, _) -> a
                            | other -> failtest $"{other}"

                        // within the Launch lifetime, the attempt dropped: enrolment, relaunch
                        let state = Hop.dropEnrolment attempt state
                        let _, relaunch = runE f state cb

                        relaunch
                        |> Expect.equal
                            "enrolment"
                            (CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, "/#/session?refused=enrolment"))
                        // after the code's lifetime the LaunchRecord is long gone too (Rule 29): invalid
                        let late = t0 + Hop.codeLifetime + TimeSpan.FromSeconds 1.0
                        let _, gone = runAt late f state cb

                        gone
                        |> Expect.equal
                            "invalid"
                            (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))
                    }

                    test "a retry of the presentation after the suspension is sent to the app" {
                        let f = enrolFixture ()
                        let state, _ = suspendVia f seeded launch1 keyA "no-pin"

                        let _, again =
                            Hop.present t0 f.ids (verifyAt t0) f.d.idp.authorizeUrl state (launch1, keyA)

                        match again with
                        | LaunchResult.Enrolling _ -> ()
                        | other -> failtest $"expected Enrolling, got {other}"
                    }
                ]


        let findTests =
            testList
                "findEnrolment"
                [
                    test "a standing attempt tells whom and the hinted address" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let _, pending = Hop.findEnrolment t0 attempt state

                        pending
                        |> Expect.equal
                            "pending"
                            (Some
                                {
                                    DisplayName = "Stub Prescriber (no PIN)"
                                    MailHint = "n***@stub.example"
                                })
                    }

                    test "an unknown attempt is nothing" {
                        let _, pending = Hop.findEnrolment t0 "nope" seeded
                        pending |> Expect.isNone "nothing"
                    }

                    test "after the code's lifetime the attempt is gone with it" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let late = t0 + Hop.codeLifetime + TimeSpan.FromSeconds 1.0
                        let state, pending = Hop.findEnrolment late attempt state
                        pending |> Expect.isNone "gone"
                        state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                        state.Enrolments |> Map.isEmpty |> Expect.isTrue "attempt dropped"
                    }
                ]


        let supplyTests =
            testList
                "supplyPin"
                [
                    test
                        "the right code and a PIN: set with a count of zero, both dropped, told, and open on the attempt's key (Rules 37, 40, 28, 27)" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let code = mailedCode f
                        let state, result = supply f state attempt code "2468"

                        match result with
                        | SupplyPinResult.Opened(id, session) ->
                            session.User |> Option.map _.UserId |> Expect.equal "user" (Some "no-pin")

                            session.User
                            |> Option.map _.Role
                            |> Expect.equal "role" (Some UserRole.Prescriber)

                            session.PatientContext
                            |> Option.map _.PatientId
                            |> Expect.equal "patient" (Some "patient-1")

                            session.KeyThumbprint
                            |> Expect.equal "the attempt's key" (Some(PublicKey.thumbprint keyA))

                            state.Sessions |> Map.containsKey id |> Expect.isTrue "open"
                        | other -> failtest $"expected Opened, got {other}"

                        let credential = state.Credentials["no-pin"]

                        credential.PinHash
                        |> Option.map (PinHash.verify "2468")
                        |> Expect.equal "the PIN" (Some true)

                        credential.WrongCount |> Expect.equal "zero" 0
                        state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                        state.Enrolments |> Map.isEmpty |> Expect.isTrue "attempt dropped"

                        match f.outbox.sent () with
                        | [ second; first ] ->
                            first.Subject |> Expect.stringContains "first" "confirmation code"
                            second.Subject |> Expect.stringContains "second" "PIN was set"
                            second.To |> Expect.equal "the registry's address, fresh" "no-pin@stub.example"
                        | other -> failtest $"expected two mails, got {other.Length}"
                    }

                    test "the next launch of that person opens directly" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let state, _ = supply f state attempt (mailedCode f) "2468"
                        let state, cb = hopE f state (mintFor "n-2" "patient-1") keyB "no-pin"
                        let _, result = runE f state cb

                        match result with
                        | CallbackResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "two attempts on one code: whichever supplies opens on its own key, and the other is gone" {
                        let f = enrolFixture ()
                        let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                        let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                        let state, result = supply f state a2 (mailedCode f) "2468"

                        match result with
                        | SupplyPinResult.Opened(_, session) ->
                            session.KeyThumbprint
                            |> Expect.equal "the second browser's key" (Some(PublicKey.thumbprint keyB))
                        | other -> failtest $"expected Opened, got {other}"

                        let _, first = supply f state a1 (mailedCode f) "2468"

                        first
                        |> Expect.equal "the first attempt is gone" (SupplyPinResult.Refused PinRefusal.AttemptExpired)
                    }

                    test "a wrong code counts; the third voids the code for every attempt (ext 2b)" {
                        let f = enrolFixture ()
                        let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                        let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                        let state, r1 = supply f state a1 "000000" "2468"
                        r1 |> Expect.equal "two left" (SupplyPinResult.Refused(PinRefusal.WrongCode 2))
                        let state, r2 = supply f state a2 "000000" "2468"

                        r2
                        |> Expect.equal
                            "one left, counted across attempts"
                            (SupplyPinResult.Refused(PinRefusal.WrongCode 1))

                        let state, r3 = supply f state a1 "000000" "2468"
                        r3 |> Expect.equal "void" (SupplyPinResult.Refused PinRefusal.CodeVoid)
                        state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                        state.Enrolments |> Map.isEmpty |> Expect.isTrue "both attempts dropped"
                        state.Credentials["no-pin"] |> Credential.pinSet |> Expect.isFalse "no PIN set"
                        let _, r4 = supply f state a2 (mailedCode f) "2468"

                        r4
                        |> Expect.equal
                            "even the right code is too late"
                            (SupplyPinResult.Refused PinRefusal.AttemptExpired)

                        f.outbox.sent () |> List.length |> Expect.equal "no second mail" 1
                    }

                    test "a fresh launch after a void code mails a fresh one" {
                        let f = enrolFixture ()
                        let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                        let state, _ = supply f state a1 "000000" "2468"
                        let state, _ = supply f state a1 "000000" "2468"
                        let state, _ = supply f state a1 "000000" "2468"
                        let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                        f.outbox.sent () |> List.length |> Expect.equal "two mails" 2
                        let _, result = supply f state a2 (mailedCode f) "2468"

                        match result with
                        | SupplyPinResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "a PIN without the format spends no try" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let state, result = supply f state attempt (mailedCode f) "12"
                        result |> Expect.equal "format" (SupplyPinResult.Refused PinRefusal.PinFormat)
                        state.Codes["no-pin"].Tries |> Expect.equal "no try spent" 0
                        let _, ok = supply f state attempt (mailedCode f) "1234"

                        match ok with
                        | SupplyPinResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "an expired code is refused and dropped with its attempts" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let code = mailedCode f
                        let late = t0 + Hop.codeLifetime + TimeSpan.FromSeconds 1.0
                        let state, result = supplyAt late f state attempt code "2468"

                        result
                        |> Expect.equal "expired" (SupplyPinResult.Refused PinRefusal.AttemptExpired)

                        state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                        state.Enrolments |> Map.isEmpty |> Expect.isTrue "attempt dropped"
                        state.Credentials["no-pin"] |> Credential.pinSet |> Expect.isFalse "no PIN set"
                    }

                    test "an unknown attempt is expired" {
                        let _, result = supply (enrolFixture ()) seeded "nope" "123456" "2468"

                        result
                        |> Expect.equal "expired" (SupplyPinResult.Refused PinRefusal.AttemptExpired)
                    }

                    test
                        "the registry is asked again at the supply: the Role is re-taken, another active Patient sets the PIN but opens nothing (Rule 6)" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let code = mailedCode f

                        let moved identity =
                            f.d.registry.standing identity
                            |> Option.map (fun s -> { s with ActivePatientId = Some "other-patient" })

                        let state, result =
                            Hop.supplyPin
                                t0
                                f.ids
                                salts
                                codeMac
                                moved
                                StubPatientData.port.read
                                f.outbox.port.send
                                attempt
                                code
                                "2468"
                                state

                        result
                        |> Expect.equal "no Session" (SupplyPinResult.Refused PinRefusal.WrongActivePatient)

                        state.Sessions |> Map.isEmpty |> Expect.isTrue "nothing opened"

                        state.Credentials["no-pin"]
                        |> Credential.pinSet
                        |> Expect.isTrue "the PIN is set (Rule 37)"

                        state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                        f.outbox.sent () |> List.length |> Expect.equal "told" 2

                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"

                        let reader identity =
                            f.d.registry.standing identity
                            |> Option.map (fun s -> { s with User = { s.User with Role = UserRole.Reader } })

                        let _, result =
                            Hop.supplyPin
                                t0
                                f.ids
                                salts
                                codeMac
                                reader
                                StubPatientData.port.read
                                f.outbox.port.send
                                attempt
                                (mailedCode f)
                                "2468"
                                state

                        match result with
                        | SupplyPinResult.Opened(_, session) ->
                            session.User
                            |> Option.map _.Role
                            |> Expect.equal "the fresh Role" (Some UserRole.Reader)
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "the PIN-set mail falls back on the code's address when the registry cannot answer" {
                        let f = enrolFixture ()
                        let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                        let code = mailedCode f

                        let _, result =
                            Hop.supplyPin
                                t0
                                f.ids
                                salts
                                codeMac
                                (fun _ -> None)
                                StubPatientData.port.read
                                f.outbox.port.send
                                attempt
                                code
                                "2468"
                                state

                        match result with
                        | SupplyPinResult.Opened _ -> ()
                        | other -> failtest $"expected Opened, got {other}"

                        (f.outbox.sent () |> List.head).To
                        |> Expect.equal "the code's address" "no-pin@stub.example"
                    }

                    test "the open closes the person's other Sessions (Rule 8)" {
                        let f = enrolFixture ()
                        // a PIN set by an earlier enrolment, then a Session, then a launch that... cannot
                        // suspend any more. So: enrol in one browser while another Session of the same
                        // person, opened before the PIN existed, cannot exist. The Rule 8 close still
                        // runs in the same act; exercise it with a seeded prescriber turned no-pin.
                        let state =
                            { seeded with Credentials = seeded.Credentials |> Map.add "prescriber" Credential.empty }

                        let state, a = suspendVia f state launch1 keyA "prescriber"
                        let state, _ = supply f state a (mailedCode f) "2468"
                        let state, cb = hopE f state (mintFor "n-2" "patient-1") keyB "prescriber"
                        let state, second = runE f state cb

                        match second with
                        | CallbackResult.Opened(id2, _) ->
                            state.Sessions |> Map.count |> Expect.equal "one Session" 1
                            state.Sessions |> Map.containsKey id2 |> Expect.isTrue "the newer"
                            state.Endings |> Map.count |> Expect.equal "the older marked" 1
                        | other -> failtest $"expected Opened, got {other}"
                    }
                ]


        let dropTests =
            testList
                "dropEnrolment"
                [
                    test "the last attempt takes the code along; another attempt keeps it" {
                        let f = enrolFixture ()
                        let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                        let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                        let state = Hop.dropEnrolment a1 state
                        state.Codes |> Map.containsKey "no-pin" |> Expect.isTrue "code stands for a2"
                        let state = Hop.dropEnrolment a2 state
                        state.Codes |> Map.isEmpty |> Expect.isTrue "code gone with the last attempt"
                        Hop.dropEnrolment "nope" state |> Expect.equal "unknown is nothing" state
                    }
                ]


        let tests =
            testList
                "Hop"
                [
                    presentTests
                    callbackTests
                    portTests
                    directoryTests
                    identityCookieTests
                    pinHashTests
                    credentialTests
                    standingTests
                    credentialStoreTests
                    mailTests
                    helperTests
                    suspendTests
                    findTests
                    supplyTests
                    dropTests
                ]


    /// An in-memory cookie: what the browser would hold after the response.
    let memoryCookie (initial: string option) =
        let value = ref initial

        {
            SessionCookie.read = fun () -> value.Value
            write = fun id -> value.Value <- Some id
            delete = fun () -> value.Value <- None
        },
        value


    /// An in-memory state cookie: what the browser would hold after the redirect.
    let memoryStateCookie (initial: string option) =
        let value = ref initial

        {
            LaunchStateCookie.read = fun state -> value.Value |> Option.filter ((=) state)
            write = fun state -> value.Value <- Some state
        },
        value


    /// An in-memory enrolment cookie: the attempt the browser would hold, and until when.
    let memoryEnrolmentCookie (initial: string option) =
        let value = ref initial
        let until = ref None

        {
            EnrolmentCookie.read = fun () -> value.Value
            write =
                fun attempt u ->
                    value.Value <- Some attempt
                    until.Value <- Some u
            delete = fun () -> value.Value <- None
        },
        value,
        until


    /// A browser that holds no attempt.
    let noEnrolment () =
        let cookie, _, _ = memoryEnrolmentCookie None
        cookie


    /// The stub env with a fresh session port over its own stub directory and outbox.
    let envWithStubMail () =
        let outbox = StubMail.make ()
        let port, _, directory = makePortWith outbox

        directory,
        outbox,

        { makeEnv
              (formularyAlwaysOk Formulary.empty)
              (orderContextAlwaysOk OrderContext.empty)
              (orderPlanAlwaysOk (OrderPlan.create Patient.empty [||]))
              (nutritionPlanAlwaysOk (NutritionPlan.create Patient.empty [||])) with
            session = port
        }


    let envWithStub () =
        let directory, _, env = envWithStubMail ()
        directory, env


    /// Runs the whole hop for `choice`: present, the stub IdentityProvider, the callback.
    /// Returns the redirect the callback answered with.
    let openVia
        (directory: StubDirectory.Directory)
        env
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        launch
        key
        choice
        =
        async {
            let! outcome =
                CompositionRoot.processLaunch env cookie stateCookie (LaunchCommand.PresentLaunch(launch, key))

            match outcome with
            | LaunchOutcome.RedirectTo url ->
                let state = url.Substring(url.IndexOf "state=" + 6) |> Uri.UnescapeDataString

                let cb: Callback =
                    if choice = "none" then
                        {
                            State = state
                            StateCookie = None
                            Code = None
                            Error = Some "no-identity"
                        }
                    else
                        {
                            State = state
                            StateCookie = None
                            Code = Some(directory.issue choice "stub-patient")
                            Error = None
                        }

                return! CompositionRoot.processCallback env cookie stateCookie enrolment cb
            | other -> return failtest $"expected RedirectTo, got {other}"
        }


    /// A fresh sealed Launch under `nonce` for the stub patient.
    let present nonce key =
        LaunchCommand.PresentLaunch(mintFor $"n-{nonce}" "stub-patient", key)


    let sealTests =
        let claims: LaunchSeal.Claims =
            {
                PatientId = "p-1"
                Nonce = "n-1"
                Expiry = t0 + lifetime
            }

        testList
            "LaunchSeal"
            [
                test "a minted Launch verifies to its claims" {
                    LaunchSeal.mint sealKey claims
                    |> LaunchSeal.verify t0 sealKey
                    |> Expect.equal "claims" (Ok claims)
                }

                test "the token has two base64url parts and no padding" {
                    let (Launch text) = LaunchSeal.mint sealKey claims
                    let parts = text.Split('.')
                    parts.Length |> Expect.equal "two parts" 2

                    for p in parts do
                        (p.Contains "=" || p.Contains "+" || p.Contains "/")
                        |> Expect.isFalse "no padding, no + or /"
                }

                test "another key does not verify" {
                    LaunchSeal.mint sealKey claims
                    |> LaunchSeal.verify t0 otherSealKey
                    |> Expect.equal "invalid" (Error LaunchRefusal.LaunchInvalid)
                }

                test "a payload swapped under a signature does not verify" {
                    let (Launch text) = LaunchSeal.mint sealKey claims
                    let signature = text.Split('.')[1]

                    let (Launch other) = LaunchSeal.mint sealKey { claims with PatientId = "p-2" }
                    let payload = other.Split('.')[0]

                    Launch $"{payload}.{signature}"
                    |> LaunchSeal.verify t0 sealKey
                    |> Expect.equal "invalid" (Error LaunchRefusal.LaunchInvalid)
                }

                test "garbage, empty, null and one-part texts are invalid" {
                    for text in [ "garbage"; ""; null; "a.b.c"; "!!.??"; "eyJ9.abc" ] do
                        Launch text
                        |> LaunchSeal.verify t0 sealKey
                        |> Expect.equal $"'{text}'" (Error LaunchRefusal.LaunchInvalid)
                }

                test "past the expiry the Launch is expired, not invalid" {
                    LaunchSeal.mint sealKey claims
                    |> LaunchSeal.verify (t0 + lifetime + TimeSpan.FromSeconds 1.0) sealKey
                    |> Expect.equal "expired" (Error LaunchRefusal.LaunchExpired)
                }

                test "empty claims are invalid even under the right seal" {
                    LaunchSeal.mint sealKey { claims with PatientId = "" }
                    |> LaunchSeal.verify t0 sealKey
                    |> Expect.equal "invalid" (Error LaunchRefusal.LaunchInvalid)
                }
            ]


    let stubLaunchTests =
        testList
            "StubLaunch"
            [
                test "mint gives a Launch that verifies to the posted PatientId, one lifetime long" {
                    StubLaunch.mint t0 (fun () -> "nonce-x") sealKey "p-9"
                    |> LaunchSeal.verify t0 sealKey
                    |> Expect.equal
                        "claims"
                        (Ok
                            {
                                LaunchSeal.PatientId = "p-9"
                                LaunchSeal.Nonce = "nonce-x"
                                LaunchSeal.Expiry = t0 + StubLaunch.lifetime
                            })
                }

                test "a blank PatientId falls back to the stub patient" {
                    StubLaunch.mint t0 (fun () -> "n") sealKey "  "
                    |> LaunchSeal.verify t0 sealKey
                    |> Result.map _.PatientId
                    |> Expect.equal "stub-patient" (Ok "stub-patient")
                }

                test "the launch url is the hash form with the token escaped" {
                    StubLaunch.launchUrl (Launch "a.b")
                    |> Expect.equal "url" "/#/session?launch=a.b"

                    StubLaunch.launchUrl (Launch "a+b/c=")
                    |> Expect.equal "escaped" "/#/session?launch=a%2Bb%2Fc%3D"
                }

                test "the page offers every identity choice and names the no-data patient" {
                    StubLaunch.page.Contains "<select name=\"identity\">" |> Expect.isTrue "select"

                    for c in StubDirectory.choices do
                        StubLaunch.page.Contains $"<option value=\"{c}\">" |> Expect.isTrue c

                    StubLaunch.page.Contains "no-data" |> Expect.isTrue "no-data hint"
                }

                test "the page has no inline script and posts to its own path" {
                    StubLaunch.page.Contains "<script" |> Expect.isFalse "no script"

                    StubLaunch.page.Contains $"action=\"{StubLaunch.path}\""
                    |> Expect.isTrue "posts to itself"

                    StubLaunch.page.Contains "name=\"pid\"" |> Expect.isTrue "pid field"
                }
            ]


    let compositionTests =
        testList
            "processLaunch, processCallback and processSession"
            [
                testAsync "getSettings answers the settings the host composed with" {
                    let settings =
                        {
                            ServerSettings.Language = Shared.Localization.French
                            IsDemo = false
                        }

                    let _, env = envWithStub ()
                    let cookie, _ = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let api = CompositionRoot.compose settings env cookie stateCookie (noEnrolment ())
                    let! answer = api.getSettings ()
                    answer |> Expect.equal "same value" settings
                }

                testAsync
                    "PresentLaunch: a sealed Launch answers RedirectTo and writes the state cookie, not the session cookie" {
                    let _, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, heldState = memoryStateCookie None

                    match! CompositionRoot.processLaunch env cookie stateCookie (present "1" keyA) with
                    | LaunchOutcome.RedirectTo url ->
                        url |> Expect.stringStarts "to the IdentityProvider" "/authorize?state="
                        heldState.Value |> Expect.isSome "state cookie written"
                        url |> Expect.stringContains "the same state" heldState.Value.Value
                        held.Value |> Expect.isNone "no session cookie yet"
                    | other -> failtest $"expected RedirectTo, got {other}"
                }

                testAsync "PresentLaunch: a refusal writes no cookie" {
                    let _, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, heldState = memoryStateCookie None

                    let! outcome =
                        CompositionRoot.processLaunch
                            env
                            cookie
                            stateCookie
                            (LaunchCommand.PresentLaunch(Launch "not-a-launch", keyA))

                    outcome
                    |> Expect.equal "refused" (LaunchOutcome.Refused LaunchRefusal.LaunchInvalid)

                    held.Value |> Expect.isNone "no session cookie"
                    heldState.Value |> Expect.isNone "no state cookie"
                }

                testAsync "processCallback: Opened writes the session id to the cookie and sends the browser to the app" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! redirect =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            (noEnrolment ())
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "prescriber"

                    redirect |> Expect.equal "to the app" "/#/session"
                    held.Value |> Expect.isSome "cookie holds the id"

                    match! env.session.find held.Value.Value with
                    | SessionLookup.Found session ->
                        session.User
                        |> Option.map _.UserId
                        |> Expect.equal "the opened session" (Some "prescriber")
                    | other -> failtest $"expected Found, got {other}"
                }

                testAsync "processCallback: a refusal writes no session cookie and names the reason in the url" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! redirect =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            (noEnrolment ())
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "unknown"

                    redirect |> Expect.equal "refused" "/#/session?refused=no-role"
                    held.Value |> Expect.isNone "no cookie"
                }

                testAsync "processCallback: without the state cookie the callback is invalid" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, heldState = memoryStateCookie None

                    let! outcome = CompositionRoot.processLaunch env cookie stateCookie (present "1" keyA)

                    let state =
                        match outcome with
                        | LaunchOutcome.RedirectTo url -> url.Substring(url.IndexOf "state=" + 6)
                        | other -> failtest $"{other}"

                    // another browser: it has no state cookie
                    let noCookie, _ = memoryStateCookie None

                    let! redirect =
                        CompositionRoot.processCallback
                            env
                            cookie
                            noCookie
                            (noEnrolment ())
                            {
                                State = state
                                StateCookie = None
                                Code = Some(directory.issue "prescriber" "stub-patient")
                                Error = None
                            }

                    redirect |> Expect.equal "invalid" "/#/session?refused=invalid"
                    held.Value |> Expect.isNone "no cookie"
                    heldState.Value |> Expect.isSome "the first browser's state cookie is untouched"
                }

                testAsync
                    "processCallback: a reload after a newer launch of the same login keeps the newer session's cookie" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! outcome =
                        CompositionRoot.processLaunch
                            env
                            cookie
                            stateCookie
                            (LaunchCommand.PresentLaunch(mintFor "n-1" "stub-patient", keyA))

                    let state1 =
                        match outcome with
                        | LaunchOutcome.RedirectTo url -> url.Substring(url.IndexOf "state=" + 6)
                        | other -> failtest $"{other}"

                    let cb1: Callback =
                        {
                            State = state1
                            StateCookie = None
                            Code = Some(directory.issue "prescriber" "stub-patient")
                            Error = None
                        }

                    let! _ = CompositionRoot.processCallback env cookie stateCookie (noEnrolment ()) cb1
                    let first = held.Value.Value

                    // the same login launches again in another tab: the first Session is replaced
                    let stateCookie2, _ = memoryStateCookie None

                    let! _ =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie2
                            (noEnrolment ())
                            (mintFor "n-2" "stub-patient")
                            keyB
                            "prescriber"

                    let second = held.Value.Value
                    second |> Expect.notEqual "a newer session" first

                    // the first tab reloads its callback
                    let! redirect = CompositionRoot.processCallback env cookie stateCookie (noEnrolment ()) cb1
                    redirect |> Expect.equal "to the app" "/#/session"
                    held.Value |> Expect.equal "the newer cookie stays" (Some second)
                }

                testAsync "GetSession: no cookie is None" {
                    let _, env = envWithStub ()
                    let cookie, _ = memoryCookie None
                    let! response = CompositionRoot.processSession env cookie (noEnrolment ()) SessionCommand.GetSession
                    response |> Expect.equal "none" (SessionResponse.SessionResp None)
                }

                testAsync "GetSession: the cookie of an opened session finds it, without the id" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! _ =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            (noEnrolment ())
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "prescriber"

                    let later, _ = memoryCookie held.Value

                    match! CompositionRoot.processSession env later (noEnrolment ()) SessionCommand.GetSession with
                    | SessionResponse.SessionResp(Some session) ->
                        session.User
                        |> Option.map _.DisplayName
                        |> Expect.equal "user" (Some "Stub Prescriber")
                    | other -> failtest $"expected the session, got {other}"
                }

                testAsync
                    "GetSession: after a newer launch of the same login the ending is told until the client closes to acknowledge (Rule 11)" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! _ =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            (noEnrolment ())
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "prescriber"

                    let first = held.Value.Value

                    let stateCookie2, _ = memoryStateCookie None
                    let other, _ = memoryCookie None

                    let! _ =
                        openVia
                            directory
                            env
                            other
                            stateCookie2
                            (noEnrolment ())
                            (mintFor "n-2" "stub-patient")
                            keyB
                            "prescriber"

                    // the first browser still holds its cookie
                    let firstBrowser, heldFirst = memoryCookie (Some first)

                    let! told =
                        CompositionRoot.processSession env firstBrowser (noEnrolment ()) SessionCommand.GetSession

                    told
                    |> Expect.equal "told" (SessionResponse.SessionEnded SessionEnding.SupersededByLaunch)

                    heldFirst.Value |> Expect.equal "cookie kept until acknowledged" (Some first)

                    // the answer was lost: the browser asks again and is told again
                    let! second =
                        CompositionRoot.processSession env firstBrowser (noEnrolment ()) SessionCommand.GetSession

                    second
                    |> Expect.equal "told again" (SessionResponse.SessionEnded SessionEnding.SupersededByLaunch)

                    // the client acknowledges with a close: cookie deleted, ending dropped
                    let! closed =
                        CompositionRoot.processSession env firstBrowser (noEnrolment ()) SessionCommand.CloseSession

                    closed |> Expect.equal "closed" SessionResponse.SessionClosed
                    heldFirst.Value |> Expect.isNone "cookie deleted"

                    let stale, _ = memoryCookie (Some first)
                    let! third = CompositionRoot.processSession env stale (noEnrolment ()) SessionCommand.GetSession
                    third |> Expect.equal "nothing left to tell" (SessionResponse.SessionResp None)
                }

                testAsync "GetSession: a cookie for an unknown session is None" {
                    let _, env = envWithStub ()
                    let cookie, _ = memoryCookie (Some "stale")
                    let! response = CompositionRoot.processSession env cookie (noEnrolment ()) SessionCommand.GetSession
                    response |> Expect.equal "none" (SessionResponse.SessionResp None)
                }

                testAsync "CloseSession: closes the session and deletes the cookie" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! _ =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            (noEnrolment ())
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "prescriber"

                    let id = held.Value.Value

                    let! response =
                        CompositionRoot.processSession env cookie (noEnrolment ()) SessionCommand.CloseSession

                    response |> Expect.equal "closed" SessionResponse.SessionClosed
                    held.Value |> Expect.isNone "cookie deleted"
                    let! found = env.session.find id
                    found |> Expect.equal "session gone" SessionLookup.NotFound
                }

                testAsync "CloseSession without a cookie still deletes (idempotent)" {
                    let _, env = envWithStub ()
                    let cookie, held = memoryCookie None

                    let! response =
                        CompositionRoot.processSession env cookie (noEnrolment ()) SessionCommand.CloseSession

                    response |> Expect.equal "closed" SessionResponse.SessionClosed
                    held.Value |> Expect.isNone "still none"
                }

                testAsync "CloseSession deletes the cookie even when the port's close throws" {
                    let _, env = envWithStub ()

                    let env =
                        { env with
                            session =
                                { env.session with
                                    close = fun _ -> async { return raise (InvalidOperationException "boom") }
                                }
                        }

                    let cookie, held = memoryCookie (Some "session-1")

                    let! result =
                        CompositionRoot.processSession env cookie (noEnrolment ()) SessionCommand.CloseSession
                        |> Async.Catch

                    match result with
                    | Choice2Of2 _ -> held.Value |> Expect.isNone "cookie deleted before the exception propagated"
                    | Choice1Of2 _ -> failtest "expected the exception to propagate"
                }

                testAsync "sessionDisabled refuses every launch and callback as invalid and finds nothing" {
                    let _, env = envWithStub ()
                    let env = { env with session = Adapters.sessionDisabled }
                    let cookie, held = memoryCookie (Some "any")
                    let stateCookie, _ = memoryStateCookie (Some "st")

                    let! outcome = CompositionRoot.processLaunch env cookie stateCookie (present "1" keyA)

                    outcome
                    |> Expect.equal "invalid" (LaunchOutcome.Refused LaunchRefusal.LaunchInvalid)

                    let! redirect =
                        CompositionRoot.processCallback
                            env
                            cookie
                            stateCookie
                            (noEnrolment ())
                            {
                                State = "st"
                                StateCookie = None
                                Code = Some "c"
                                Error = None
                            }

                    redirect |> Expect.equal "invalid" "/#/session?refused=invalid"
                    held.Value |> Expect.equal "cookie untouched" (Some "any")

                    let! response = CompositionRoot.processSession env cookie (noEnrolment ()) SessionCommand.GetSession
                    response |> Expect.equal "nothing" (SessionResponse.SessionResp None)
                }
            ]


    /// The code the newest confirmation mail in the outbox carries.
    let mailedCode (outbox: StubMail.Outbox) =
        let body =
            (outbox.sent () |> List.find (fun m -> m.Subject.Contains "confirmation code")).Body

        let i = body.IndexOf "code is " + 8
        body.Substring(i, 6)


    let enrolmentCompositionTests =
        testList
            "processCallback and processSession while enrolling (UC-2)"
            [
                testAsync
                    "no-pin: the callback sets the enrolment cookie, GetSession tells the pending enrolment, SupplyPin opens and swaps the cookies" {
                    let directory, outbox, env = envWithStubMail ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let enrolment, attempt, until = memoryEnrolmentCookie None

                    let! redirect =
                        openVia directory env cookie stateCookie enrolment (mintFor "n-1" "stub-patient") keyA "no-pin"

                    redirect |> Expect.equal "to the app" "/#/session"
                    held.Value |> Expect.isNone "no session cookie"
                    attempt.Value |> Expect.isSome "enrolment cookie"

                    until.Value
                    |> Expect.equal "until the code expires" (Some(t0 + Hop.codeLifetime))

                    let! pending = CompositionRoot.processSession env cookie enrolment SessionCommand.GetSession

                    pending
                    |> Expect.equal
                        "pending"
                        (SessionResponse.EnrolmentPending
                            {
                                DisplayName = "Stub Prescriber (no PIN)"
                                MailHint = "n***@stub.example"
                            })

                    let! wrong =
                        CompositionRoot.processSession env cookie enrolment (SessionCommand.SupplyPin("000000", "2468"))

                    wrong
                    |> Expect.equal "wrong code" (SessionResponse.PinRefused(PinRefusal.WrongCode 2))

                    attempt.Value |> Expect.isSome "cookie kept"

                    let! opened =
                        CompositionRoot.processSession
                            env
                            cookie
                            enrolment
                            (SessionCommand.SupplyPin(mailedCode outbox, "2468"))

                    match opened with
                    | SessionResponse.SessionResp(Some session) ->
                        session.User |> Option.map _.UserId |> Expect.equal "user" (Some "no-pin")
                    | other -> failtest $"expected SessionResp, got {other}"

                    held.Value |> Expect.isSome "session cookie set"
                    attempt.Value |> Expect.isNone "enrolment cookie deleted"
                    outbox.sent () |> List.length |> Expect.equal "two mails" 2

                    let! found = CompositionRoot.processSession env cookie enrolment SessionCommand.GetSession

                    match found with
                    | SessionResponse.SessionResp(Some _) -> ()
                    | other -> failtest $"expected the Session, got {other}"
                }

                testAsync "a void code deletes the enrolment cookie; a gone attempt at GetSession does too" {
                    let directory, _, env = envWithStubMail ()
                    let cookie, _ = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let enrolment, attempt, _ = memoryEnrolmentCookie None

                    let! _ =
                        openVia directory env cookie stateCookie enrolment (mintFor "n-1" "stub-patient") keyA "no-pin"

                    for _ in 1..2 do
                        let! _ =
                            CompositionRoot.processSession
                                env
                                cookie
                                enrolment
                                (SessionCommand.SupplyPin("000000", "2468"))

                        ()

                    let! void' =
                        CompositionRoot.processSession env cookie enrolment (SessionCommand.SupplyPin("000000", "2468"))

                    void' |> Expect.equal "void" (SessionResponse.PinRefused PinRefusal.CodeVoid)
                    attempt.Value |> Expect.isNone "cookie deleted"

                    let stale, attemptRef, _ = memoryEnrolmentCookie (Some "gone")
                    let! nothing = CompositionRoot.processSession env cookie stale SessionCommand.GetSession
                    nothing |> Expect.equal "nothing" (SessionResponse.SessionResp None)
                    attemptRef.Value |> Expect.isNone "stale cookie deleted"
                }

                testAsync
                    "SupplyPin without an enrolment cookie is expired; CloseSession while enrolling drops the attempt" {
                    let directory, _, env = envWithStubMail ()
                    let cookie, _ = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! expired =
                        CompositionRoot.processSession
                            env
                            cookie
                            (noEnrolment ())
                            (SessionCommand.SupplyPin("123456", "2468"))

                    expired
                    |> Expect.equal "expired" (SessionResponse.PinRefused PinRefusal.AttemptExpired)

                    let enrolment, attempt, _ = memoryEnrolmentCookie None

                    let! _ =
                        openVia directory env cookie stateCookie enrolment (mintFor "n-1" "stub-patient") keyA "no-pin"

                    let held = attempt.Value
                    let! closed = CompositionRoot.processSession env cookie enrolment SessionCommand.CloseSession
                    closed |> Expect.equal "closed" SessionResponse.SessionClosed
                    attempt.Value |> Expect.isNone "cookie deleted"
                    let stale, _, _ = memoryEnrolmentCookie held
                    let! nothing = CompositionRoot.processSession env cookie stale SessionCommand.GetSession
                    nothing |> Expect.equal "the attempt is gone" (SessionResponse.SessionResp None)
                }

                testAsync "the Session wins over a stale enrolment cookie" {
                    let directory, _, env = envWithStubMail ()
                    let cookie, _ = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let enrolment, _, _ = memoryEnrolmentCookie (Some "stale")

                    let! _ =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            enrolment
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "prescriber"

                    let! found = CompositionRoot.processSession env cookie enrolment SessionCommand.GetSession

                    match found with
                    | SessionResponse.SessionResp(Some _) -> ()
                    | other -> failtest $"expected the Session, got {other}"
                }

                testAsync "an enrolling callback replaces the Session this browser still held" {
                    let directory, _, env = envWithStubMail ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let enrolment, attempt, _ = memoryEnrolmentCookie None

                    let! _ =
                        openVia
                            directory
                            env
                            cookie
                            stateCookie
                            enrolment
                            (mintFor "n-1" "stub-patient")
                            keyA
                            "prescriber"

                    let old = held.Value
                    old |> Expect.isSome "a Session"

                    let! _ =
                        openVia directory env cookie stateCookie enrolment (mintFor "n-2" "stub-patient") keyB "no-pin"

                    held.Value |> Expect.isNone "session cookie gone"
                    attempt.Value |> Expect.isSome "enrolment cookie"
                    let! pending = CompositionRoot.processSession env cookie enrolment SessionCommand.GetSession

                    match pending with
                    | SessionResponse.EnrolmentPending _ -> ()
                    | other -> failtest $"expected EnrolmentPending, got {other}"

                    let! gone = env.session.find old.Value
                    gone |> Expect.equal "the old Session is closed" SessionLookup.NotFound
                }

                testAsync "the disabled port refuses to enrol" {
                    let cookie, _ = memoryCookie None
                    let enrolment, attempt, _ = memoryEnrolmentCookie (Some "any")

                    let env = { snd (envWithStub ()) with session = Adapters.sessionDisabled }

                    let! refused =
                        CompositionRoot.processSession env cookie enrolment (SessionCommand.SupplyPin("123456", "2468"))

                    refused
                    |> Expect.equal "expired" (SessionResponse.PinRefused PinRefusal.AttemptExpired)

                    attempt.Value |> Expect.isNone "cookie deleted"
                }
            ]


    let tests =
        testList
            "Session"
            [
                thumbprintTests
                sealTests
                HopTests.tests
                stubLaunchTests
                compositionTests
                enrolmentCompositionTests
            ]


[<Tests>]
let tests =
    testList
        "Stub Adapter Tests"
        [
            commandRoutingTests
            errorPropagationTests
            requireLoadedTests
            SessionStubTests.tests
        ]
