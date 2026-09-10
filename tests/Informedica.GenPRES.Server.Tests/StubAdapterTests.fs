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


    /// A port with a settable clock, a counting id source, the seal check bound to the clock and
    /// a stub directory of its own.
    let makePort () =
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
                (fun launch -> LaunchSeal.verify clock.Value sealKey launch)
                directory.idp
                directory.registry
                StubPatientData.port

        port, clock, directory


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


        let run (ids: unit -> string) (directory: StubDirectory.Directory) state cb =
            Hop.callback t0 ids directory.idp.redeem directory.registry.standing StubPatientData.port.read state cb


        let presentTests =
            testList
                "Hop.present"
                [
                    test "a sealed Launch is recorded under its nonce and sent to the IdentityProvider" {
                        let ids, d = fixture ()

                        let state, result =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)

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
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)

                        let state2, again =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyA)

                        again |> Expect.equal "same redirect" first
                        state2 |> Expect.equal "same state" state
                    }

                    test "another key is spent" {
                        let ids, d = fixture ()

                        let state, _ =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)

                        let _, other =
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyB)

                        other |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)
                    }

                    test "after the hop opened, the same key gets the opened Session (Rule 2)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
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
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (Launch "junk", keyA)

                        invalid
                        |> Expect.equal "invalid" (LaunchResult.Refused LaunchRefusal.LaunchInvalid)

                        let late = t0 + lifetime + TimeSpan.FromSeconds 1.0

                        let state, expired =
                            Hop.present late ids (verifyAt late) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)

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
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
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

                            state.Launches["n-1"].Outcome |> Expect.isSome "outcome appended"
                        | other -> failtest $"expected Opened, got {other}"
                    }

                    test "reader: opens without a PIN (ext 5c)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "reader"
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
                                    "no-pin", LaunchRefusal.EnrolmentRequired
                                ] do
                                test choice {
                                    let ids, d = fixture ()
                                    let state, cb = hop ids d Hop.emptyState launch1 keyA choice
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
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                        let state, first = run ids d state cb
                        // the code was consumed by the first redeem; the replay must not need it
                        let state2, again = run ids d state cb
                        again |> Expect.equal "same answer" first
                        state2 |> Expect.equal "same state" state
                    }

                    test "a state cookie that does not match is invalid, and nothing is redeemed" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"

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
                                Hop.emptyState
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
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                        let _, result = run ids d state { cb with Code = Some "forged" }

                        result
                        |> Expect.equal
                            "no identity"
                            (CallbackResult.Refused(LaunchRefusal.NoBrowserIdentity, "/#/session?refused=no-identity"))
                    }

                    test "after the lifetime the callback is invalid: the record is gone (Rule 29)" {
                        let ids, d = fixture ()
                        let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                        let late = t0 + lifetime + TimeSpan.FromSeconds 1.0

                        let state2, result =
                            Hop.callback late ids d.idp.redeem d.registry.standing StubPatientData.port.read state cb

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
                            Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch, keyA)

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
                        let state, cb1 = hop ids d Hop.emptyState launch1 keyA "prescriber"
                        let state, first = run ids d state cb1
                        let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                        let state, second = run ids d state cb2

                        match first, second with
                        | CallbackResult.Opened(id1, _), CallbackResult.Opened(id2, _) ->
                            state.Sessions |> Map.containsKey id1 |> Expect.isFalse "first closed"
                            state.Sessions |> Map.containsKey id2 |> Expect.isTrue "second open"

                            state.Endings
                            |> Map.tryFind id1
                            |> Expect.equal "marked" (Some Hop.SessionEnding.SupersededByLaunch)
                        | other -> failtest $"expected two Opened, got {other}"
                    }

                    test "two logins keep two Sessions" {
                        let ids, d = fixture ()
                        let state, cb1 = hop ids d Hop.emptyState launch1 keyA "prescriber"
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
                            Hop.makeSessionPort (fun () -> t0) ids (verifyAt t0) d.idp d.registry StubPatientData.port

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
                            let! found = port.find id

                            found
                            |> Option.bind _.User
                            |> Option.map _.UserId
                            |> Expect.equal "found" (Some "prescriber")

                            do! port.close id
                            let! gone = port.find id
                            gone |> Expect.isNone "closed"
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
                    test "a code redeems once" {
                        let d = StubDirectory.make (fun () -> t0) (counter "code")
                        let code = d.issue "prescriber" "p"

                        d.idp.redeem code
                        |> Option.map _.Login
                        |> Expect.equal "first" (Some "prescriber")

                        d.idp.redeem code |> Expect.isNone "second"
                    }

                    test "past the lifetime a code does not redeem, and the next issue prunes it" {
                        let clock = ref t0
                        let d = StubDirectory.make (fun () -> clock.Value) (counter "code")
                        let stale = d.issue "prescriber" "p"
                        clock.Value <- t0 + StubDirectory.codeLifetime + TimeSpan.FromSeconds 1.0
                        d.idp.redeem stale |> Expect.isNone "stale"
                        let fresh = d.issue "reader" "p"
                        d.idp.redeem fresh |> Option.map _.Login |> Expect.equal "fresh" (Some "reader")
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


        let tests =
            testList
                "Hop"
                [
                    presentTests
                    callbackTests
                    portTests
                    directoryTests
                    identityCookieTests
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


    /// An in-memory state cookie: what the browser would hold after the redirect.
    let memoryStateCookie (initial: string option) =
        let value = ref initial

        {
            LaunchStateCookie.read = fun state -> value.Value |> Option.filter ((=) state)
            write = fun state -> value.Value <- Some state
        },
        value


    /// The stub env with a fresh session port over its own stub directory.
    let envWithStub () =
        let port, _, directory = makePort ()

        directory,

        { makeEnv
              (formularyAlwaysOk Formulary.empty)
              (orderContextAlwaysOk OrderContext.empty)
              (orderPlanAlwaysOk (OrderPlan.create Patient.empty [||]))
              (nutritionPlanAlwaysOk (NutritionPlan.create Patient.empty [||])) with
            session = port
        }


    /// Runs the whole hop for `choice`: present, the stub IdentityProvider, the callback.
    /// Returns the redirect the callback answered with.
    let openVia
        (directory: StubDirectory.Directory)
        env
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
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

                return! CompositionRoot.processCallback env cookie stateCookie cb
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
                    let api = CompositionRoot.compose settings env cookie stateCookie
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
                        openVia directory env cookie stateCookie (mintFor "n-1" "stub-patient") keyA "prescriber"

                    redirect |> Expect.equal "to the app" "/#/session"
                    held.Value |> Expect.isSome "cookie holds the id"
                    let! found = env.session.find held.Value.Value

                    found
                    |> Option.bind _.User
                    |> Option.map _.UserId
                    |> Expect.equal "the opened session" (Some "prescriber")
                }

                testAsync "processCallback: a refusal writes no session cookie and names the reason in the url" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None

                    let! redirect =
                        openVia directory env cookie stateCookie (mintFor "n-1" "stub-patient") keyA "unknown"

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

                    let! _ = CompositionRoot.processCallback env cookie stateCookie cb1
                    let first = held.Value.Value

                    // the same login launches again in another tab: the first Session is replaced
                    let stateCookie2, _ = memoryStateCookie None
                    let! _ = openVia directory env cookie stateCookie2 (mintFor "n-2" "stub-patient") keyB "prescriber"
                    let second = held.Value.Value
                    second |> Expect.notEqual "a newer session" first

                    // the first tab reloads its callback
                    let! redirect = CompositionRoot.processCallback env cookie stateCookie cb1
                    redirect |> Expect.equal "to the app" "/#/session"
                    held.Value |> Expect.equal "the newer cookie stays" (Some second)
                }

                testAsync "GetSession: no cookie is None" {
                    let _, env = envWithStub ()
                    let cookie, _ = memoryCookie None
                    let! response = CompositionRoot.processSession env cookie SessionCommand.GetSession
                    response |> Expect.equal "none" (SessionResponse.SessionResp None)
                }

                testAsync "GetSession: the cookie of an opened session finds it, without the id" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let! _ = openVia directory env cookie stateCookie (mintFor "n-1" "stub-patient") keyA "prescriber"

                    let later, _ = memoryCookie held.Value

                    match! CompositionRoot.processSession env later SessionCommand.GetSession with
                    | SessionResponse.SessionResp(Some session) ->
                        session.User
                        |> Option.map _.DisplayName
                        |> Expect.equal "user" (Some "Stub Prescriber")
                    | other -> failtest $"expected the session, got {other}"
                }

                testAsync "GetSession: a cookie for an unknown session is None" {
                    let _, env = envWithStub ()
                    let cookie, _ = memoryCookie (Some "stale")
                    let! response = CompositionRoot.processSession env cookie SessionCommand.GetSession
                    response |> Expect.equal "none" (SessionResponse.SessionResp None)
                }

                testAsync "CloseSession: closes the session and deletes the cookie" {
                    let directory, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let stateCookie, _ = memoryStateCookie None
                    let! _ = openVia directory env cookie stateCookie (mintFor "n-1" "stub-patient") keyA "prescriber"
                    let id = held.Value.Value
                    let! response = CompositionRoot.processSession env cookie SessionCommand.CloseSession
                    response |> Expect.equal "closed" SessionResponse.SessionClosed
                    held.Value |> Expect.isNone "cookie deleted"
                    let! found = env.session.find id
                    found |> Expect.isNone "session gone"
                }

                testAsync "CloseSession without a cookie still deletes (idempotent)" {
                    let _, env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let! response = CompositionRoot.processSession env cookie SessionCommand.CloseSession
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
                        CompositionRoot.processSession env cookie SessionCommand.CloseSession
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
                            {
                                State = "st"
                                StateCookie = None
                                Code = Some "c"
                                Error = None
                            }

                    redirect |> Expect.equal "invalid" "/#/session?refused=invalid"
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
                HopTests.tests
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
