// Plan 409, step 2b: the session endpoints and the cookie.
//
// Launch sequence: docs/scenarios/integration/uc-01-launch.md (step 6, Rule 12)
// Plan:            docs/implementation-plans/409-client-launch-sequence.md
//
// Script-only draft on top of the step 2a code already in source (#588). Migration targets:
//   LaunchCommand, SessionCommand, SessionResponse, processLaunch/processSession
//                      -> src/Informedica.GenPRES.Shared/Api.fs (next to Command/Response;
//                         the two IServerApi fields join processCommand and testApi)
//   SessionCookie      -> src/Informedica.GenPRES.Server/ServerApi.Ports.fs
//   Adapters.sessionDisabled -> src/Informedica.GenPRES.Server/ServerApi.Adapters.fs
//   CompositionRoot    -> src/Informedica.GenPRES.Server/ServerApi.CompositionRoot.fs
//                         (`compose` takes `AppEnv` and `SessionCookie`; the env is no longer
//                         built inside it) and ServerApi.ApiImpl.fs (`createServerApi env cookie`)
//   Http.sessionCookie -> src/Informedica.GenPRES.Server/Server.fs, module Http
//   Host.build         -> src/Informedica.GenPRES.Server/Server.fs: build the env once,
//                         `Remoting.fromContext`, the IsProd port swap
//   Tests              -> tests/Informedica.GenPRES.Server.Tests/StubAdapterTests.fs (composition)
//                         and HttpTests.fs (cookie adapter over DefaultHttpContext)
//
// Design points, for the review:
//   - The composition root never sees HttpContext. It gets a `SessionCookie` record of three
//     functions built per request in Server.fs (the DMZ, ADR-0001 §4). So the session part of
//     the API is testable with an in-memory cookie, and the cookie attributes are tested once
//     over `DefaultHttpContext`.
//   - `Remoting.fromContext` runs its function per request. The `AppEnv` (and with it the
//     stub's session state) must therefore be built once in `Host.build`, not inside `compose`
//     as today; otherwise every request would get a fresh, empty stub.
//   - Stop-gap for #580: in production the session port is swapped for `sessionDisabled`, which
//     refuses every Launch with `LaunchInvalid` and finds no session. The stub then cannot open
//     a Session on a production server. Remove the swap when the scope switch lands.
//   - A server exception in `processLaunch` is not a refusal: it propagates, Fable.Remoting
//     answers 500, and the client's `Outcome (Error _)` path retries (plan, Session.transition).
//   - `CloseSession` deletes the cookie even when no session is found: an explicit close
//     (Rule 10) always leaves the browser without a credential.
//
// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi Session.fsx
// The ASP.NET Core references below point at the shared framework on this machine; adjust
// the version directory if `dotnet --list-runtimes` shows another 10.0.x.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto, 10.2.1"
#r "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.11/Microsoft.Extensions.Primitives.dll"
#r "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.11/Microsoft.Extensions.Features.dll"
#r "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.11/Microsoft.AspNetCore.Http.Features.dll"
#r "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.11/Microsoft.AspNetCore.Http.Abstractions.dll"
#r "/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/10.0.11/Microsoft.AspNetCore.Http.dll"

open System
open Shared.Types
open ServerApi


/// === Shared.Api additions ===
/// Two command families next to `Command`, cut at the authentication boundary: a launch
/// command arrives without a cookie (step 4), a session command always with one (step 6+).
/// Each family has room to grow: the identity callback for launches; Rule 11 endings, resume
/// and PIN for sessions.
[<RequireQualifiedAccess>]
type LaunchCommand =
    // idempotent per public key (Rule 2); sets the session cookie on Opened
    | PresentLaunch of Launch * PublicKey


[<RequireQualifiedAccess>]
type SessionCommand =
    // the Session of this browser, if any (reload, IdP return)
    | GetSession
    // Rule 10: explicit close; always removes the cookie
    | CloseSession


[<RequireQualifiedAccess>]
type SessionResponse =
    | SessionResp of SessionOpened option
    | SessionClosed


module LaunchCommand =

    /// For the log. Never the Launch or the key: the Launch is a secret, the key is long.
    let toString cmd =
        match cmd with
        | LaunchCommand.PresentLaunch _ -> "PresentLaunch"


module SessionCommand =

    let toString cmd =
        match cmd with
        | SessionCommand.GetSession -> "GetSession"
        | SessionCommand.CloseSession -> "CloseSession"


/// The two `IServerApi` fields, drafted as their own record so the script can build and test
/// them; in Api.fs they join `processCommand` and `testApi`.
type SessionApi =
    {
        processLaunch: LaunchCommand -> Async<LaunchOutcome>
        processSession: SessionCommand -> Async<SessionResponse>
    }


/// === ServerApi.Ports ===
/// The session cookie of one request, as three functions. Built from the HttpContext in
/// Server.fs; the composition root only reads, writes and deletes through it.
type SessionCookie =
    {
        read: unit -> string option
        write: string -> unit
        delete: unit -> unit
    }


/// === ServerApi.Adapters ===
module Adapters =

    /// The session port of a server that does not launch: every Launch is refused as
    /// invalid and no session is ever found. Used in production until the scope switch (#580)
    /// decides what a production server exposes.
    let sessionDisabled: SessionPort =
        {
            present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
            find = fun _ -> async { return None }
            close = fun _ -> async { return () }
        }


/// === ServerApi.CompositionRoot ===
/// `compose` becomes `compose (env: AppEnv) (cookie: SessionCookie) : IServerApi` and gains
/// `processLaunch` and `processSession` next to `processCommand` and `testApi`. Here only the
/// session part; in source, `processLaunch` and `processSession` log the command name the way
/// `processCommand` does (`writeInfoMessage $"Processing launch: {LaunchCommand.toString cmd}"`).
module CompositionRoot =

    let processLaunch (env: AppEnv) (cookie: SessionCookie) (cmd: LaunchCommand) =
        async {
            match cmd with
            | LaunchCommand.PresentLaunch(launch, key) ->
                match! env.session.present (launch, key) with
                | LaunchResult.Opened(id, session) ->
                    // the id goes into the cookie and nowhere else (Rule 12)
                    cookie.write id
                    return LaunchOutcome.Opened session
                | LaunchResult.RedirectTo url -> return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
        }


    let processSession (env: AppEnv) (cookie: SessionCookie) (cmd: SessionCommand) =
        async {
            match cmd with
            | SessionCommand.GetSession ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some id ->
                    let! session = env.session.find id
                    return SessionResponse.SessionResp session
            | SessionCommand.CloseSession ->
                match cookie.read () with
                | Some id -> do! env.session.close id
                | None -> ()

                cookie.delete ()
                return SessionResponse.SessionClosed
        }


    let composeSession (env: AppEnv) (cookie: SessionCookie) : SessionApi =
        {
            processLaunch = processLaunch env cookie
            processSession = processSession env cookie
        }


/// === Server.fs, module Http ===
module Http =

    open Microsoft.AspNetCore.Http


    let sessionCookieName = "genpres_session"


    /// The attributes of the session cookie (Rule 12): HttpOnly, SameSite=Strict, Path=/,
    /// Secure when the request came in over HTTPS. Host-only on purpose: no Domain, so the
    /// Vite dev proxy (changeOrigin) passes it unchanged and it never leaks to a sibling host.
    let sessionCookieOptions (isHttps: bool) =
        CookieOptions(HttpOnly = true, Secure = isHttps, SameSite = SameSiteMode.Strict, Path = "/")


    /// The session cookie of this request as the port the composition root uses.
    let sessionCookie (ctx: HttpContext) : SessionCookie =
        {
            read =
                fun () ->
                    match ctx.Request.Cookies.TryGetValue sessionCookieName with
                    | true, value when not (String.IsNullOrWhiteSpace value) -> Some value
                    | _ -> None
            write = fun id -> ctx.Response.Cookies.Append(sessionCookieName, id, sessionCookieOptions ctx.Request.IsHttps)
            delete = fun () -> ctx.Response.Cookies.Delete(sessionCookieName, sessionCookieOptions ctx.Request.IsHttps)
        }


// === Server.fs, Host.build: the wiring (not executable here; Saturn's `application` is not
// loaded in this script) ===
//
//     let build (settings: Config.Settings) provider =
//         let env =
//             let env = Adapters.makeAppEnv provider
//
//             // stop-gap until the scope switch (#580): a production server never opens a
//             // stub Session. Drop this swap when #580 decides what production exposes.
//             if settings.IsProd then
//                 { env with session = Adapters.sessionDisabled }
//             else
//                 env
//
//         let webApi =
//             Remoting.createApi ()
//             |> Remoting.fromContext (fun ctx -> createServerApi env (Http.sessionCookie ctx))
//             |> Remoting.withRouteBuilder routerPaths
//             |> Remoting.buildHttpHandler
//         ...
//
// and ServerApi.ApiImpl.fs:
//
//     let createServerApi (env: AppEnv) (cookie: SessionCookie) : Shared.Api.IServerApi =
//         CompositionRoot.compose env cookie


/// === tests ===
module Tests =

    open Expecto
    open Expecto.Flip
    open Microsoft.AspNetCore.Http


    /// An in-memory cookie: what the browser would hold after the response.
    let memoryCookie (initial: string option) =
        let value = ref initial

        {
            read = fun () -> value.Value
            write = fun id -> value.Value <- Some id
            delete = fun () -> value.Value <- None
        },
        value


    let t0 = DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc)

    let keyA = PublicKey "key-A"


    /// An AppEnv whose only live port is a fresh session stub with counting ids.
    let envWithStub () =
        let count = ref 0

        let port =
            SessionStub.makeSessionPort
                (fun () -> t0)
                (fun () ->
                    count.Value <- count.Value + 1
                    $"session-{count.Value}"
                )
                (TimeSpan.FromMinutes 2.0)
                Shared.Models.Patient.empty

        let notStubbed _ = raise (NotImplementedException "not stubbed")

        {
            formulary =
                {
                    getFormulary = notStubbed
                    getParenteralia = notStubbed
                }
            orderContext = { evaluate = fun _ -> notStubbed }
            orderPlan =
                {
                    updateOrderPlan = fun _ -> notStubbed
                    filterOrderPlan = notStubbed
                }
            nutritionPlan =
                {
                    initNutritionPlan = notStubbed
                    addNutritionContext = notStubbed
                    removeNutritionContext = notStubbed
                    updateNutritionOrderContext = notStubbed
                    selectNutritionOrderScenario = notStubbed
                    navigateNutritionOrderContext = notStubbed
                }
            interaction =
                {
                    checkInteractions = notStubbed
                    getDrugNames = notStubbed
                }
            logAnalyzer =
                {
                    listLogFiles = notStubbed
                    analyzeLogFile = notStubbed
                }
            requireLoaded = fun () -> None
            session = port
        }


    let compositionTests =
        testList
            "composeSession"
            [
                testAsync "PresentLaunch: Opened writes the session id to the cookie and returns the session without it" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let api = CompositionRoot.composeSession env cookie

                    match! api.processLaunch (LaunchCommand.PresentLaunch(Launch "demo", keyA)) with
                    | LaunchOutcome.Opened session ->
                        held.Value |> Expect.equal "cookie holds the id" (Some "session-1")
                        session.User |> Expect.isSome "a user"
                        // the id is not in the payload: the record has no such field, and the
                        // OpenedToken is the only id-shaped value
                        session.OpenedToken
                        |> Expect.notEqual "opened token is not the session id" (Some(OpenedToken "session-1"))
                    | other -> failtest $"expected Opened, got {other}"
                }

                testAsync "PresentLaunch: a refusal writes no cookie" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let api = CompositionRoot.composeSession env cookie

                    let! outcome = api.processLaunch (LaunchCommand.PresentLaunch(Launch "no-role", keyA))

                    outcome
                    |> Expect.equal "refused" (LaunchOutcome.Refused LaunchRefusal.NoRole)

                    held.Value |> Expect.isNone "no cookie"
                }

                testAsync "GetSession: no cookie is None" {
                    let env = envWithStub ()
                    let cookie, _ = memoryCookie None
                    let api = CompositionRoot.composeSession env cookie

                    let! response = api.processSession SessionCommand.GetSession
                    response |> Expect.equal "anonymous" (SessionResponse.SessionResp None)
                }

                testAsync "GetSession: the cookie of an opened session finds it" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let api = CompositionRoot.composeSession env cookie

                    let! opened = api.processLaunch (LaunchCommand.PresentLaunch(Launch "demo", keyA))
                    // a later request carries the cookie the first response set
                    let later, _ = memoryCookie held.Value
                    let! found = (CompositionRoot.composeSession env later).processSession SessionCommand.GetSession

                    match opened, found with
                    | LaunchOutcome.Opened s, SessionResponse.SessionResp(Some f) -> f |> Expect.equal "same session" s
                    | _ -> failtest $"expected Opened and Some, got {opened} and {found}"
                }

                testAsync "GetSession: a cookie for an unknown session is None" {
                    let env = envWithStub ()
                    let cookie, _ = memoryCookie (Some "stale")
                    let api = CompositionRoot.composeSession env cookie

                    let! response = api.processSession SessionCommand.GetSession
                    response |> Expect.equal "unknown id" (SessionResponse.SessionResp None)
                }

                testAsync "CloseSession: closes the session and deletes the cookie" {
                    let env = envWithStub ()
                    let cookie, held = memoryCookie None
                    let api = CompositionRoot.composeSession env cookie

                    let! _ = api.processLaunch (LaunchCommand.PresentLaunch(Launch "demo", keyA))
                    let! closed = api.processSession SessionCommand.CloseSession

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

                    let! _ = (CompositionRoot.composeSession env cookie).processSession SessionCommand.CloseSession
                    deleted.Value |> Expect.isTrue "delete called"
                }

                testAsync "sessionDisabled refuses every launch as invalid and finds nothing" {
                    let env =
                        { envWithStub () with
                            session = Adapters.sessionDisabled
                        }

                    let cookie, held = memoryCookie (Some "any")
                    let api = CompositionRoot.composeSession env cookie

                    let! outcome = api.processLaunch (LaunchCommand.PresentLaunch(Launch "demo", keyA))

                    outcome
                    |> Expect.equal "invalid" (LaunchOutcome.Refused LaunchRefusal.LaunchInvalid)

                    held.Value |> Expect.equal "cookie untouched" (Some "any")

                    let! response = api.processSession SessionCommand.GetSession
                    response |> Expect.equal "nothing" (SessionResponse.SessionResp None)
                }
            ]


    let setCookieHeader (ctx: HttpContext) = ctx.Response.Headers.SetCookie.ToString()


    let cookieTests =
        testList
            "Http.sessionCookie"
            [
                test "write sets HttpOnly, SameSite=Strict, Path=/ and no Secure over http" {
                    let ctx = DefaultHttpContext()
                    (Http.sessionCookie ctx).write "abc"
                    let header = setCookieHeader ctx

                    header |> Expect.stringStarts "name=value" "genpres_session=abc"
                    header |> Expect.stringContains "httponly" "httponly"
                    header |> Expect.stringContains "strict" "samesite=strict"
                    header |> Expect.stringContains "path" "path=/"
                    header.Contains("secure", StringComparison.OrdinalIgnoreCase) |> Expect.isFalse "not secure over http"
                }

                test "write sets Secure over https" {
                    let ctx = DefaultHttpContext()
                    ctx.Request.IsHttps <- true
                    (Http.sessionCookie ctx).write "abc"

                    setCookieHeader ctx |> Expect.stringContains "secure" "secure"
                }

                test "read returns the cookie value, None when absent or blank" {
                    let withCookie (value: string) =
                        let ctx = DefaultHttpContext()
                        ctx.Request.Headers.Cookie <- Microsoft.Extensions.Primitives.StringValues value
                        (Http.sessionCookie ctx).read ()

                    withCookie "genpres_session=abc; other=1" |> Expect.equal "present" (Some "abc")
                    withCookie "other=1" |> Expect.isNone "absent"
                    withCookie "genpres_session=" |> Expect.isNone "blank"
                    (Http.sessionCookie (DefaultHttpContext())).read () |> Expect.isNone "no header"
                }

                test "delete expires the cookie on the same path" {
                    let ctx = DefaultHttpContext()
                    (Http.sessionCookie ctx).delete ()
                    let header = setCookieHeader ctx

                    header |> Expect.stringStarts "name emptied" "genpres_session=;"
                    header |> Expect.stringContains "expired" "expires="
                    header |> Expect.stringContains "path" "path=/"
                }
            ]


    let tests = testList "Session 2b" [ compositionTests; cookieTests ]


Expecto.Tests.runTestsWithCLIArgs [] [||] Tests.tests
