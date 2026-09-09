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

    open SessionStub


    let lifetime = TimeSpan.FromMinutes 2.0

    let t0 = DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)

    let keyA = PublicKey "key-A"
    let keyB = PublicKey "key-B"


    /// A port with a settable clock and a counting id source.
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
                lifetime
                Patient.empty

        port, clock


    let refusalWords =
        [
            "expired", LaunchRefusal.LaunchExpired
            "spent", LaunchRefusal.LaunchSpent
            "invalid", LaunchRefusal.LaunchInvalid
            "no-identity", LaunchRefusal.NoBrowserIdentity
            "no-role", LaunchRefusal.NoRole
            "wrong-patient", LaunchRefusal.WrongActivePatient
            "enrolment", LaunchRefusal.EnrolmentRequired
        ]


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
                    "refusal mapping"
                    [
                        for word, refusal in refusalWords do
                            testAsync $"launch={word} refuses with {refusal} and records nothing" {
                                let port, _ = makePort ()

                                let! first = port.present (Launch word, keyA)
                                // another key gets the same answer: nothing was recorded
                                let! second = port.present (Launch word, keyB)

                                first |> Expect.equal "first" (LaunchResult.Refused refusal)
                                second |> Expect.equal "second, other key" (LaunchResult.Refused refusal)

                                let! found = port.find "session-1"
                                found |> Expect.isNone "no session opened"
                            }
                    ]

                testAsync "any other launch opens a session with a stub prescriber and patient" {
                    let port, _ = makePort ()

                    match! port.present (Launch "demo", keyA) with
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

                    let! first = port.present (Launch "demo", keyA)
                    clock.Value <- t0 + TimeSpan.FromSeconds 90.0
                    let! second = port.present (Launch "demo", keyA)

                    second |> Expect.equal "same session, same content" first

                    let! second' = port.find "session-2"
                    second' |> Expect.isNone "nothing opened twice"
                }

                testAsync "a second presentation with another key is LaunchSpent and opens nothing" {
                    let port, _ = makePort ()

                    let! _ = port.present (Launch "demo", keyA)
                    let! other = port.present (Launch "demo", keyB)

                    other |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)

                    let! first = port.find "session-1"
                    first |> Expect.isSome "the first session is untouched"

                    let! second = port.find "session-2"
                    second |> Expect.isNone "no second session"
                }

                testAsync "after the lifetime the launch is LaunchExpired, even for the same key" {
                    let port, clock = makePort ()

                    let! _ = port.present (Launch "demo", keyA)
                    clock.Value <- t0 + lifetime + TimeSpan.FromSeconds 1.0
                    let! late = port.present (Launch "demo", keyA)

                    late
                    |> Expect.equal "expired" (LaunchResult.Refused LaunchRefusal.LaunchExpired)
                }

                testAsync "close removes the session" {
                    let port, _ = makePort ()

                    let! _ = port.present (Launch "demo", keyA)
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

                    let state1, r1 =
                        present t0 newId lifetime Patient.empty emptyState (Launch "demo", keyA)

                    let _, r2 = present t0 newId lifetime Patient.empty state1 (Launch "demo", keyA)

                    r2 |> Expect.equal "recorded answer" r1
                    state1.Launches |> Map.count |> Expect.equal "one record" 1
                    state1.Sessions |> Map.count |> Expect.equal "one session" 1
                }
            ]


    let tests = testList "Session" [ thumbprintTests; stubTests ]


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
