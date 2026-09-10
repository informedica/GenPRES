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
            find = fun _ -> async { return None }
            close = fun _ -> async { return () }
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
    open SessionStub


    let lifetime = TimeSpan.FromMinutes 2.0

    let t0 = DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)

    let keyA = PublicKey "key-A"
    let keyB = PublicKey "key-B"


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


    /// A port with a settable clock, a counting id source and the seal check bound to the clock.
    let makePort () =
        let clock = ref t0
        let count = ref 0

        let port =
            makeSessionPort
                (fun () -> clock.Value)
                (fun () ->
                    count.Value <- count.Value + 1
                    $"session-{count.Value}"
                )
                (fun launch -> LaunchSeal.verify clock.Value sealKey launch)
                Patient.empty

        port, clock


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


    let stubTests =
        testList
            "Session stub"
            [
                testList
                    "a Launch the seal refuses opens nothing and records nothing"
                    [
                        for name, launch, refusal in
                            [
                                "not sealed under the key",
                                LaunchSeal.mint
                                    otherSealKey
                                    {
                                        PatientId = "p"
                                        Nonce = "n"
                                        Expiry = t0 + lifetime
                                    },
                                LaunchRefusal.LaunchInvalid
                                "garbage", Launch "not-a-launch", LaunchRefusal.LaunchInvalid
                                "already expired",
                                LaunchSeal.mint
                                    sealKey
                                    {
                                        PatientId = "p"
                                        Nonce = "n"
                                        Expiry = t0 - TimeSpan.FromSeconds 1.0
                                    },
                                LaunchRefusal.LaunchExpired
                            ] do
                            testAsync $"{name} refuses with {refusal}" {
                                let port, _ = makePort ()

                                let! first = port.present (launch, keyA)
                                // another key gets the same answer: nothing was recorded
                                let! second = port.present (launch, keyB)

                                first |> Expect.equal "first" (LaunchResult.Refused refusal)
                                second |> Expect.equal "second, other key" (LaunchResult.Refused refusal)

                                let! found = port.find "session-1"
                                found |> Expect.isNone "no session opened"
                            }
                    ]

                testAsync "a sealed Launch opens a session with the stub prescriber for its PatientId" {
                    let port, _ = makePort ()

                    match! port.present (launch1, keyA) with
                    | LaunchResult.Opened(id, session) ->
                        id |> Expect.equal "session id" "session-1"

                        session.User
                        |> Option.map _.Role
                        |> Expect.equal "role" (Some UserRole.Prescriber)

                        session.PatientContext
                        |> Option.map _.PatientId
                        |> Expect.equal "patient" (Some "stub-patient")

                        session.OpenedToken |> Expect.isSome "opened token"

                        session.KeyThumbprint
                        |> Expect.equal "thumbprint of the presented key" (Some(PublicKey.thumbprint keyA))

                        let! found = port.find id
                        found |> Expect.equal "find returns the session" (Some session)
                    | other -> failtest $"expected Opened, got {other}"
                }

                testAsync "a second presentation with the same key within the lifetime is answered as the first" {
                    let port, clock = makePort ()

                    let! first = port.present (launch1, keyA)
                    clock.Value <- t0 + TimeSpan.FromSeconds 90.0
                    let! second = port.present (launch1, keyA)

                    second |> Expect.equal "same session, same content" first

                    let! second' = port.find "session-2"
                    second' |> Expect.isNone "nothing opened twice"
                }

                testAsync "a second presentation with another key is LaunchSpent and opens nothing" {
                    let port, _ = makePort ()

                    let! _ = port.present (launch1, keyA)
                    let! other = port.present (launch1, keyB)

                    other |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)

                    let! first = port.find "session-1"
                    first |> Expect.isSome "the first session is untouched"

                    let! second = port.find "session-2"
                    second |> Expect.isNone "no second session"
                }

                testAsync "after the lifetime the launch is LaunchExpired, even for the same key" {
                    let port, clock = makePort ()

                    let! _ = port.present (launch1, keyA)
                    clock.Value <- t0 + lifetime + TimeSpan.FromSeconds 1.0
                    let! late = port.present (launch1, keyA)

                    late
                    |> Expect.equal "expired" (LaunchResult.Refused LaunchRefusal.LaunchExpired)
                }

                testAsync "a second Launch with another nonce opens a second session" {
                    let port, _ = makePort ()

                    let! _ = port.present (launch1, keyA)

                    match! port.present (launch2, keyB) with
                    | LaunchResult.Opened(id, session) ->
                        id |> Expect.equal "second id" "session-2"

                        session.PatientContext
                        |> Option.map _.PatientId
                        |> Expect.equal "its own patient" (Some "p-2")
                    | other -> failtest $"expected Opened, got {other}"
                }

                testAsync "close removes the session" {
                    let port, _ = makePort ()

                    let! _ = port.present (launch1, keyA)
                    do! port.close "session-1"

                    let! found = port.find "session-1"
                    found |> Expect.isNone "closed"
                }

                testAsync "find of an unknown id is None" {
                    let port, _ = makePort ()
                    let! found = port.find "nope"
                    found |> Expect.isNone "unknown"
                }

                test "present is pure: the same state and input give the same answer" {
                    let ids = ref 0

                    let newId () =
                        ids.Value <- ids.Value + 1
                        $"s{ids.Value}"

                    let verify = LaunchSeal.verify t0 sealKey

                    let state1, r1 = present t0 newId verify Patient.empty emptyState (launch1, keyA)

                    let _, r2 = present t0 newId verify Patient.empty state1 (launch1, keyA)

                    r2 |> Expect.equal "recorded answer" r1
                    state1.Launches |> Map.count |> Expect.equal "one record" 1
                    state1.Sessions |> Map.count |> Expect.equal "one session" 1
                }
            ]


    /// An in-memory cookie: what the browser would hold after the response.
    let memoryCookie (initial: string option) =
        let value = ref initial

        {
            read = fun () -> value.Value
            write = fun id -> value.Value <- Some id
            delete = fun () -> value.Value <- None
        },
        value


    /// The stub env with a fresh, counting session stub.
    let envWithStub () =
        let port, _ = makePort ()

        { makeEnv
              (formularyAlwaysOk Formulary.empty)
              (orderContextAlwaysOk OrderContext.empty)
              (orderPlanAlwaysOk (OrderPlan.create Patient.empty [||]))
              (nutritionPlanAlwaysOk (NutritionPlan.create Patient.empty [||])) with
            session = port
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

                test "the page has no inline script and posts to its own path" {
                    StubLaunch.page.Contains "<script" |> Expect.isFalse "no script"

                    StubLaunch.page.Contains $"action=\"{StubLaunch.path}\""
                    |> Expect.isTrue "posts to itself"

                    StubLaunch.page.Contains "name=\"pid\"" |> Expect.isTrue "pid field"
                }
            ]


    let compositionTests =
        testList
            "processLaunch and processSession"
            [
                testAsync "getSettings answers the settings the host composed with" {
                    let settings =
                        {
                            ServerSettings.Language = Shared.Localization.French
                            IsDemo = false
                        }

                    let cookie, _ = memoryCookie None
                    let api = CompositionRoot.compose settings (envWithStub ()) cookie
                    let! answer = api.getSettings ()
                    answer |> Expect.equal "same value" settings
                }

                testAsync "PresentLaunch: Opened writes the session id to the cookie and returns the session without it" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None

                    match! CompositionRoot.processLaunch env cookie (present "demo" keyA) with
                    | LaunchOutcome.Opened session ->
                        held.Value |> Expect.equal "cookie holds the id" (Some "session-1")
                        session.User |> Expect.isSome "a user"

                        session.OpenedToken
                        |> Expect.notEqual "opened token is not the session id" (Some(OpenedToken "session-1"))
                    | other -> failtest $"expected Opened, got {other}"
                }

                testAsync "PresentLaunch: a refusal writes no cookie" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None

                    let! outcome =
                        CompositionRoot.processLaunch
                            env
                            cookie
                            (LaunchCommand.PresentLaunch(Launch "not-a-launch", keyA))

                    outcome
                    |> Expect.equal "refused" (LaunchOutcome.Refused LaunchRefusal.LaunchInvalid)

                    held.Value |> Expect.isNone "no cookie"
                }

                testAsync "GetSession: no cookie is None" {
                    let env = envWithStub ()
                    let cookie, _ = memoryCookie None

                    let! response = CompositionRoot.processSession env cookie SessionCommand.GetSession
                    response |> Expect.equal "anonymous" (SessionResponse.SessionResp None)
                }

                testAsync "GetSession: the cookie of an opened session finds it" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None

                    let! opened = CompositionRoot.processLaunch env cookie (present "demo" keyA)
                    // a later request carries the cookie the first response set
                    let later, _ = memoryCookie held.Value
                    let! found = CompositionRoot.processSession env later SessionCommand.GetSession

                    match opened, found with
                    | LaunchOutcome.Opened s, SessionResponse.SessionResp(Some f) -> f |> Expect.equal "same session" s
                    | _ -> failtest $"expected Opened and Some, got {opened} and {found}"
                }

                testAsync "GetSession: a cookie for an unknown session is None" {
                    let env = envWithStub ()
                    let cookie, _ = memoryCookie (Some "stale")

                    let! response = CompositionRoot.processSession env cookie SessionCommand.GetSession
                    response |> Expect.equal "unknown id" (SessionResponse.SessionResp None)
                }

                testAsync "CloseSession: closes the session and deletes the cookie" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None

                    let! _ = CompositionRoot.processLaunch env cookie (present "demo" keyA)
                    let! closed = CompositionRoot.processSession env cookie SessionCommand.CloseSession

                    closed |> Expect.equal "closed" SessionResponse.SessionClosed
                    held.Value |> Expect.isNone "cookie deleted"

                    let! found = env.session.find "session-1"
                    found |> Expect.isNone "session closed"
                }

                testAsync "CloseSession without a cookie still deletes (idempotent)" {
                    let env = envWithStub ()
                    let deleted = ref false

                    let cookie =
                        {
                            read = fun () -> None
                            write = fun _ -> ()
                            delete = fun () -> deleted.Value <- true
                        }

                    let! _ = CompositionRoot.processSession env cookie SessionCommand.CloseSession
                    deleted.Value |> Expect.isTrue "delete called"
                }

                testAsync "CloseSession deletes the cookie even when the port's close throws" {
                    let deleted = ref false

                    let env =
                        { envWithStub () with
                            session =
                                { Adapters.sessionDisabled with
                                    close = fun _ -> async { return raise (InvalidOperationException "store down") }
                                }
                        }

                    let cookie =
                        {
                            read = fun () -> Some "session-1"
                            write = fun _ -> ()
                            delete = fun () -> deleted.Value <- true
                        }

                    let! outcome =
                        CompositionRoot.processSession env cookie SessionCommand.CloseSession
                        |> Async.Catch

                    match outcome with
                    | Choice2Of2(:? InvalidOperationException) -> ()
                    | other -> failtest $"expected the close exception to propagate, got {other}"

                    deleted.Value |> Expect.isTrue "cookie deleted regardless"
                }

                testAsync "sessionDisabled refuses every launch as invalid and finds nothing" {
                    let env = { envWithStub () with session = Adapters.sessionDisabled }

                    let cookie, held = memoryCookie (Some "any")

                    let! outcome = CompositionRoot.processLaunch env cookie (present "demo" keyA)

                    outcome
                    |> Expect.equal "invalid" (LaunchOutcome.Refused LaunchRefusal.LaunchInvalid)

                    held.Value |> Expect.equal "cookie untouched" (Some "any")

                    let! response = CompositionRoot.processSession env cookie SessionCommand.GetSession
                    response |> Expect.equal "nothing" (SessionResponse.SessionResp None)
                }
            ]


    let tests =
        testList
            "Session"
            [
                thumbprintTests
                sealTests
                stubTests
                stubLaunchTests
                compositionTests
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
