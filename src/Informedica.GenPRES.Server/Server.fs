module Server

open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System.Threading.Tasks

open Informedica.Utils.Lib

open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.RateLimiting
open System.Threading.RateLimiting


/// <summary>
/// Everything the server reads from its environment, parsed once into a
/// <c>Settings</c> record. Nothing in this module performs IO: <c>fromEnv</c>
/// takes the getter, so <c>main</c> passes <c>Env.getItem</c> and tests pass
/// a <c>Map</c>. Validation returns <c>Result</c>; deciding to abort is the
/// entry point's job.
/// </summary>
module Config =

    /// The GENPRES_* settings (and SERVER_PORT) as the server uses them.
    type Settings =
        {
            // SERVER_PORT, default 8085
            Port: uint16
            // GENPRES_PROD = "1"
            IsProd: bool
            // GENPRES_URL_ID; None when unset or blank
            UrlId: string option
            // GENPRES_PASSWORD; None when unset or blank
            Password: string option
            // GENPRES_TRUSTED_PROXIES, parsed; loopback pair when unset
            TrustedProxies: System.Net.IPAddress[]
            // GENPRES_LOG, raw, banner only (Logging.fs reads it itself)
            Log: string
            // GENPRES_DEBUG, raw, banner only
            Debug: string
            // GENPRES_LANG, raw; None when unset or blank. Parsed by `language`.
            Lang: string option
        }


    // Empty/whitespace is treated as not-set so an empty
    // `ENV GENPRES_PASSWORD=` / `ENV GENPRES_URL_ID=` from a Dockerfile is
    // reported truthfully instead of looking injected, and so an empty
    // Docker env never flows into getCachedProviderWithDataUrlId to
    // surface much later as a confusing "cannot find column" error.
    let nonBlank (raw: string option) =
        raw |> Option.filter (System.String.IsNullOrWhiteSpace >> not)


    /// Banner display string for the password: never the value itself.
    let displayPassword (raw: string option) =
        raw
        |> nonBlank
        |> Option.map (fun _ -> "***")
        |> Option.defaultValue "NOT SET (admin operations disabled)"


    /// Banner display string for the Sheet ID: only the last 5 chars, so it
    /// never lands in logs or screenshots intact. Built here, not in the
    /// banner template, so the `NOT SET` path doesn't render as `***NOT SET`.
    let redactUrlId (raw: string option) =
        raw
        |> nonBlank
        |> Option.map (fun s ->
            if s.Length > 5 then
                $"***%s{s.Substring(s.Length - 5)}"
            else
                "***<redacted>"
        )
        |> Option.defaultValue "NOT SET"


    // B3 — Trusted reverse-proxy allow-list for ForwardedHeadersMiddleware.
    // Default = loopback only (matches the Plesk → Kestrel hop on the
    // public demo deployments and any local-dev setup). Override with
    // GENPRES_TRUSTED_PROXIES as a comma-separated list of IPs, e.g.
    //     GENPRES_TRUSTED_PROXIES="10.0.0.5, 10.0.0.6"
    // for a hospital LAN behind a known nginx fleet. Unparseable values
    // are silently dropped — fail-open on the parser, fail-closed on the
    // allow-list (no entry = no XFF trust).
    let parseTrustedProxies (raw: string option) =
        raw
        |> nonBlank
        |> Option.map (fun s ->
            s.Split(',')
            |> Array.choose (fun part ->
                match System.Net.IPAddress.TryParse(part.Trim()) with
                | true, ip -> Some ip
                | false, _ -> None
            )
        )
        |> Option.defaultValue
            [|
                System.Net.IPAddress.Loopback
                System.Net.IPAddress.IPv6Loopback
            |]


    // SECURITY: in production mode (GENPRES_PROD=1) a GENPRES_PASSWORD shorter
    // than minProductionPasswordLength characters refuses the start: a weak
    // secret would otherwise stay live in the admin commands, which read the
    // variable themselves. A missing or blank password does not refuse
    // (issue #590): the server starts on the configured data with admin
    // operations disabled, which those same commands enforce by failing
    // closed on the unset variable, and it prints a warning. Demo/dev mode
    // accepts any value (or none).
    let minProductionPasswordLength = 16


    /// <summary>
    /// The production password policy. <c>Ok None</c>: nothing to say.
    /// <c>Ok (Some warning)</c>: the server starts with admin operations
    /// disabled and prints the warning. <c>Error</c>: the message the server
    /// refuses to start with.
    /// </summary>
    let validateProductionPassword (isProd: bool) (password: string option) : Result<string option, string> =
        if not isProd then
            Ok None
        else
            // Blank = unset, so a forgotten Docker env hits the "not set"
            // branch instead of "shorter than 16 characters".
            match password |> nonBlank with
            | None ->
                Ok(
                    Some
                        "GENPRES_PROD=1 but GENPRES_PASSWORD is not set (or is empty). \
                         Starting with admin operations disabled (settings page, log analysis, resource reload). \
                         Set a password of at least 16 characters to enable them; generate one with `openssl rand -base64 32` \
                         and inject it via a secret store. See DEVELOPMENT.md → Password policy."
                )
            | Some pwd when pwd.Length < minProductionPasswordLength ->
                Error
                    $"GENPRES_PROD=1 but GENPRES_PASSWORD is shorter than %i{minProductionPasswordLength} characters. \
                     Refusing to start in production with a weak admin password. \
                     Generate a stronger one with `openssl rand -base64 32`. \
                     See DEVELOPMENT.md → Password policy."
            | Some _ -> Ok None


    /// What a start-up needs: the url id the host is built with, and the
    /// warnings to print before hosting (today at most the password warning).
    type Startup =
        {
            UrlId: string
            Warnings: string list
        }


    /// The default UI language: Dutch, the client's own default until now.
    let defaultLanguage = Shared.Localization.Dutch


    /// <summary>
    /// The UI language from <c>GENPRES_LANG</c>: <c>defaultLanguage</c> when
    /// unset, the parsed language when the value is one (ISO code, display
    /// name or legacy url code, any case), otherwise the start-up error naming
    /// the setting and the value.
    /// </summary>
    let language (settings: Settings) : Result<Shared.Localization.Locales, string> =
        match settings.Lang with
        | None -> Ok defaultLanguage
        | Some raw ->
            match Shared.Localization.tryParse raw with
            | Some l -> Ok l
            | None ->
                let accepted =
                    Shared.Localization.languages
                    |> Array.map (Shared.Localization.toShortCode >> _.ToLower())
                    |> String.concat ", "

                let fallback = defaultLanguage |> Shared.Localization.toShortCode |> _.ToLower()

                Error
                    $"GENPRES_LANG=%s{raw} is not a language. Accepted: %s{accepted} \
                      (or a display name such as Nederlands). Unset it for the default (%s{fallback})."


    /// Banner display string for the language: the derived value, or the raw
    /// one flagged when it is not a language (validateStartup then refuses).
    let displayLanguage (settings: Settings) =
        match language settings with
        | Ok l -> $"{l |> Shared.Localization.toShortCode |> _.ToLower()} ({l |> Shared.Localization.toString})"
        | Error _ ->
            let raw = settings.Lang |> Option.defaultValue ""
            $"%s{raw} (NOT A LANGUAGE)"


    /// <summary>
    /// Every start-up guard in one place: the production password policy, the
    /// language, then the presence of <c>GENPRES_URL_ID</c>. <c>Ok</c> carries
    /// the URL ID the host needs and the warnings to print; <c>Error</c> is the
    /// message the server exits with.
    /// </summary>
    let validateStartup (settings: Settings) : Result<Startup, string> =
        validateProductionPassword settings.IsProd settings.Password
        |> Result.bind (fun warning ->
            language settings
            |> Result.bind (fun _ ->
                match settings.UrlId with
                | Some urlId ->
                    Ok
                        {
                            UrlId = urlId
                            Warnings = warning |> Option.toList
                        }
                | None -> Error "No GENPRES_URL_ID (or value is empty)"
            )
        )


    /// <summary>
    /// The settings the client learns, mapped here in the DMZ from the
    /// env-shaped record. Call after <c>validateStartup</c>: an invalid
    /// language never reaches this point, so the fallback is dead.
    /// </summary>
    let toServerSettings (settings: Settings) : Shared.Api.ServerSettings =
        {
            Language = language settings |> Result.defaultValue defaultLanguage
            IsDemo = not settings.IsProd
        }


    /// Reads every setting through <c>getEnv</c>. Pure: pass <c>Env.getItem</c>
    /// for the real environment, a <c>Map.tryFind</c> in tests.
    let fromEnv (getEnv: string -> string option) : Settings =
        {
            Port = "SERVER_PORT" |> getEnv |> Option.map uint16 |> Option.defaultValue 8085us
            IsProd =
                getEnv "GENPRES_PROD"
                |> Option.map (fun v -> v = "1")
                |> Option.defaultValue false
            UrlId = getEnv "GENPRES_URL_ID" |> nonBlank
            Password = getEnv "GENPRES_PASSWORD" |> nonBlank
            TrustedProxies = getEnv "GENPRES_TRUSTED_PROXIES" |> parseTrustedProxies
            Log = getEnv "GENPRES_LOG" |> Option.defaultValue "0"
            Debug = getEnv "GENPRES_DEBUG" |> Option.defaultValue "i"
            Lang = getEnv "GENPRES_LANG" |> nonBlank
        }


    /// The start-up banner. Secrets are redacted; <c>systemInfo</c> is passed
    /// in because collecting it is an effect.
    let banner (systemInfo: string) (settings: Settings) =
        $"""

=== Environmental variables ===
GENPRES_URL_ID = {settings.UrlId |> redactUrlId}
GENPRES_LOG ={settings.Log}
GENPRES_PROD = {if settings.IsProd then "1" else "0"}
GENPRES_DEBUG = {settings.Debug}
GENPRES_LANG = {settings |> displayLanguage}
GENPRES_PASSWORD = {settings.Password |> displayPassword}

=== System Info ===

{systemInfo}

"""


/// HTTP handlers, middleware and the hosted service: everything that runs
/// per request or per host lifetime. Nothing here reads the environment;
/// the values it needs arrive as parameters from <c>Host.build</c>.
module Http =

    // B3 — Returns the immediate peer IP. After UseForwardedHeaders runs
    // (registered via app_config in Host.build) this is the real client IP
    // for requests that arrived through a known proxy, and the actual peer
    // for direct connections. The previous version trusted X-Forwarded-For
    // from any source (finding B3); that path is now obsolete and the
    // rate limiter's partition cardinality is bounded by real ingress IPs.
    let getClientIP (context: HttpContext) =
        match context.Connection.RemoteIpAddress with
        | null -> "unknown"
        | ip -> ip.ToString()


    let sessionCookieName = "genpres_session"


    /// The attributes of the session cookie (uc-01 step 6, Rule 12): HttpOnly, SameSite=Strict,
    /// Path=/, Secure when the request came in over HTTPS. Behind the TLS-terminating proxy
    /// that is Request.IsHttps as set by ForwardedHeadersMiddleware from X-Forwarded-Proto
    /// (Host.build). Host-only on purpose: no Domain, so the Vite dev proxy passes it
    /// unchanged and it never reaches a sibling host.
    let sessionCookieOptions (isHttps: bool) =
        CookieOptions(HttpOnly = true, Secure = isHttps, SameSite = SameSiteMode.Strict, Path = "/")


    /// The session cookie of this request as the port the composition root uses.
    let sessionCookie (ctx: HttpContext) : SessionCookie =
        {
            read =
                fun () ->
                    match ctx.Request.Cookies.TryGetValue sessionCookieName with
                    | true, value when not (System.String.IsNullOrWhiteSpace value) -> Some value
                    | _ -> None
            write =
                fun id -> ctx.Response.Cookies.Append(sessionCookieName, id, sessionCookieOptions ctx.Request.IsHttps)
            delete = fun () -> ctx.Response.Cookies.Delete(sessionCookieName, sessionCookieOptions ctx.Request.IsHttps)
        }


    /// <summary>
    /// Cache-Control value for a response. Vite emits the client as
    /// content-hashed files under /assets/, so a successful response for one
    /// of those can be cached forever; everything else — "/", index.html,
    /// icons, the Fable.Remoting routes, and any error — is "no-cache": the
    /// browser may keep a copy but must revalidate it on every load. The ETag
    /// that StaticFileMiddleware already emits then answers with a 304 when
    /// nothing changed and a full fetch after a deploy.
    /// </summary>
    /// <remarks>
    /// Without a Cache-Control header browsers apply heuristic freshness
    /// (RFC 9111 §4.2.2, typically 10 % of now − Last-Modified), which is how
    /// a long-running container ends up with clients that never see a new
    /// index.html and keep loading the previous hashed bundle (#568).
    ///
    /// The status check matters: a 404 for an asset that this instance does
    /// not have yet (a rolling deploy, a stale index.html) must never be
    /// cached for a year, or the client stays broken after the asset arrives.
    /// The path can be null for a request without one, such as OPTIONS *.
    /// </remarks>
    let cacheControlFor (statusCode: int) (path: string) =
        let isAsset =
            not (System.String.IsNullOrEmpty path)
            && path.StartsWith("/assets/", System.StringComparison.OrdinalIgnoreCase)

        if isAsset && statusCode >= 200 && statusCode < 300 then
            "public, max-age=31536000, immutable"
        else
            "no-cache"


    // B2 — Security response header baseline. ASP.NET middleware (wired via
    // app_config) using Response.OnStarting so headers land on every flushed
    // response: static files, Giraffe routes, the 404 fallback, and
    // Fable.Remoting error responses alike. Also owns the Cache-Control
    // policy (cacheControlFor above), because this is the one hook that
    // runs on static responses and Saturn's use_static does not expose
    // StaticFileOptions.OnPrepareResponse.
    //
    // CSP allow-list reflects the SPA's actual fetches: same-origin scripts
    // (Fable bundle), maxcdn + Google Fonts for CSS, gstatic for fonts,
    // docs.google.com for the runtime Sheet fetches. Drop docs.google.com
    // once Sheet access is proxied server-side. X-Powered-By is stripped in
    // case nginx/Plesk injects it.
    //
    // style-src includes 'unsafe-inline' because MUI's styling engine
    // (Emotion) injects per-component <style> tags at runtime. Without it
    // every MUI component renders unstyled. script-src remains strict
    // ('self' only) so XSS exposure is bounded to CSS injection, which
    // cannot execute code. Tightening this further requires wiring an
    // Emotion CacheProvider with a per-request nonce — tracked as a
    // follow-up to the security review.
    let securityHeadersMiddleware (ctx: HttpContext) (next: System.Func<Task>) : Task =
        ctx.Response.OnStarting(fun () ->
            let h = ctx.Response.Headers
            h["Strict-Transport-Security"] <- "max-age=31536000; includeSubDomains"
            h["X-Content-Type-Options"] <- "nosniff"
            h["X-Frame-Options"] <- "DENY"
            h["Referrer-Policy"] <- "no-referrer"
            h["Permissions-Policy"] <- "geolocation=(), camera=(), microphone=()"
            h["Cache-Control"] <- cacheControlFor ctx.Response.StatusCode ctx.Request.Path.Value

            h["Content-Security-Policy"] <-
                "default-src 'self'; \
                 script-src 'self'; \
                 style-src 'self' 'unsafe-inline' https://maxcdn.bootstrapcdn.com https://fonts.googleapis.com; \
                 font-src 'self' https://fonts.gstatic.com https://maxcdn.bootstrapcdn.com; \
                 img-src 'self' data:; \
                 connect-src 'self' https://docs.google.com; \
                 frame-ancestors 'none'"

            h.Remove "X-Powered-By" |> ignore
            Task.CompletedTask
        )

        next.Invoke()


    // A2 — Per-IP fixed-window rate limiter applied to every HTTP request.
    // 60 requests / 10 s window / IP (= 6 r/s sustained, 60-request burst),
    // no queue: overflow = 429 instantly.
    //
    // Sized for actual SPA usage: a single Gender radio click fans out to
    // ~4 RPCs, a clinician filling a form chains ~10 such actions in a few
    // seconds — 60-burst absorbs it. Sustained 6 r/s still cuts scripted
    // brute force on ValidatePassword by an order of magnitude.
    //
    // Partition key uses getClientIP, which now returns the real client IP
    // resolved by ASP.NET's ForwardedHeadersMiddleware (configured with the
    // trustedProxies allow-list). XFF is honoured only when the immediate
    // connection comes from a known proxy, so spoofed XFF cannot bypass
    // the limiter and cannot inflate partition cardinality (finding B3
    // addressed for C1, configurable via GENPRES_TRUSTED_PROXIES for C2).
    //
    // QueueLimit = 0 = no queue, no QueueProcessingOrder needed (overflow
    // is rejected with 429 immediately).
    //
    // Proper per-attempt auth lockout — which would only touch the password
    // path — needs Remoting.fromContext to lift client-IP into
    // validatePassword and is still deferred.
    let addRateLimiting (services: IServiceCollection) =
        services.AddRateLimiter(fun (opts: RateLimiterOptions) ->
            opts.RejectionStatusCode <- 429

            opts.GlobalLimiter <-
                PartitionedRateLimiter.Create<HttpContext, string>(fun ctx ->
                    let ip = getClientIP ctx

                    RateLimitPartition.GetFixedWindowLimiter(
                        ip,
                        fun _ ->
                            FixedWindowRateLimiterOptions(
                                PermitLimit = 60,
                                Window = System.TimeSpan.FromSeconds(10.0),
                                QueueLimit = 0
                            )
                    )
                )
        )


    let logClientIP: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            match Logging.loggingLevel with
            | None -> next ctx
            | Some level ->
                let clientIP = getClientIP ctx
                let path = ctx.Request.Path.ToString()
                let method = ctx.Request.Method
                let logger = Logging.getLogger level Logging.RequestLogger

                async {
                    do! logger |> Logging.setComponentName (Some "Client_Request")

                    Logging.ServerLogging.logRequest logger method path clientIP
                    return ()
                }
                |> Async.Start

                // Continue with the next handler
                next ctx


    // L1 — Defense-in-depth wrapper. The original Fable.Remoting.Giraffe 5.24
    // ABI drift against Giraffe 7+ (MissingMethodException / TypeLoadException
    // from Giraffe.Core.setBodyFromString, leaking full .NET type signatures)
    // is resolved upstream in Fable.Remoting.Giraffe 6.1.0. This wrapper is
    // retained as belt-and-braces so any future reflection/ABI fault returns
    // a clean 400 instead of a raw exception body.
    let safeWebApi (webApi: HttpHandler) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                try
                    return! webApi next ctx
                with ex when (ex :? System.MissingMethodException || ex :? System.TypeLoadException) ->
                    // Record the ABI fault so neither branch can fail silently.
                    // Stderr fires unconditionally; logger fires only if
                    // GENPRES_LOG is set. The mid-stream branch (HasStarted = true)
                    // cannot rewrite the response, so this log is the only signal
                    // the caller will ever get.
                    let msg =
                        $"safeWebApi caught %s{ex.GetType().Name} on %s{ctx.Request.Path.ToString()}: %s{ex.Message}"

                    eprintfn $"{msg}"

                    match Logging.loggingLevel with
                    | Some level ->
                        let logger = Logging.getLogger level Logging.RequestLogger

                        async {
                            do! logger |> Logging.setComponentName (Some "safeWebApi")

                            Logging.ServerLogging.Error msg
                            |> Informedica.Logging.Lib.Logging.logError logger.Logger
                        }
                        |> Async.Start
                    | None -> ()

                    if not ctx.Response.HasStarted then
                        ctx.Response.StatusCode <- 400
                        ctx.Response.ContentType <- "application/json; charset=utf-8"
                        do! ctx.Response.WriteAsync "\"Bad Request\""

                    return Some ctx
            }


    /// Stops every logger agent when the host shuts down.
    type LoggerShutdown() =
        interface IHostedService with
            member _.StartAsync _ = Task.CompletedTask

            member _.StopAsync _ =
                lock
                    Logging.loggerLock
                    (fun () ->
                        [|
                            for kv in Logging.loggers do
                                let logger = kv.Value

                                writeInfoMessage $"Trying to Stop {kv.Key}"

                                try
                                    logger.StopAsync()
                                with ex ->
                                    writeDebugMessage $"Logger shutdown failed: {ex.Message}"
                                    async { return () }
                        |]
                        |> Async.Parallel
                        |> Async.StartAsTask
                        :> Task
                    )


/// The composition root: wires settings, the resource provider and the
/// Http pieces into a Saturn application. Only <c>main</c> calls this.
module Host =

    /// The cached GenFORM resource provider for a Sheet ID, with the
    /// resources logger attached when GENPRES_LOG is set.
    let resourceProvider (urlId: string) =
        let logger =
            Logging.loggingLevel
            |> Option.map (fun level ->
                Logging.getLogger level Logging.ResourcesLogger
                |> (fun logger ->
                    logger |> Logging.setComponentName (Some "Provider") |> Async.RunSynchronously
                    logger
                )
            )
            |> Option.map _.Logger
            |> Option.defaultValue Informedica.GenOrder.Lib.Logging.noOp

        urlId |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger


    let build (settings: Config.Settings) (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        // Built once per host: the session stub's state lives in it. Remoting.fromContext
        // runs its function per request, so the env must not be built in there.
        // the key the stub LaunchScript seals Launches under (plan 605): per host start, so a
        // token from an earlier run is "not sealed under the key"
        let launchKey =
            LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes

        let env =
            let env = Adapters.makeAppEnvWith launchKey provider

            // Stop-gap until the scope switch (#580): a production server never opens a
            // stub Session. Drop this swap when #580 decides what production exposes.
            if settings.IsProd then
                { env with session = Adapters.sessionDisabled }
            else
                env

        // what the client learns: computed once here, answered per request
        let serverSettings = Config.toServerSettings settings

        let webApi =
            Remoting.createApi ()
            |> Remoting.fromContext (fun ctx -> createServerApi serverSettings env (Http.sessionCookie ctx))
            |> Remoting.withRouteBuilder routerPaths
            |> Remoting.buildHttpHandler

        // Security headers come from securityHeadersMiddleware (app_config
        // below), so this stays handler-only. The 404 arm replaces a legacy
        // "GenInteractions App. Use localhost: 8080 for the GUI" string that
        // leaked an old app name and hinted at port 8080 (L2 / B5).
        // the stub LaunchScript page (uc-01 step 1 stand-in, plan 605): full scope only, so a
        // production server never mints a Launch. GET shows the form, POST mints and redirects.
        let stubLaunch =
            if settings.IsProd then
                []
            else
                [
                    GET >=> route StubLaunch.path >=> htmlString StubLaunch.page
                    POST
                    >=> route StubLaunch.path
                    >=> fun next ctx ->
                        task {
                            let! form = ctx.Request.ReadFormAsync()

                            let pid =
                                match form.TryGetValue "pid" with
                                | true, values -> values.ToString()
                                | _ -> ""

                            let launch = StubLaunch.mint System.DateTime.UtcNow PublicKey.randomId launchKey pid
                            return! redirectTo false (StubLaunch.launchUrl launch) next ctx
                        }
                ]

        let webApp =
            choose
                [
                    // the stub page is logged like the api, so a minted Launch leaves the same trail
                    Http.logClientIP >=> choose stubLaunch
                    Http.logClientIP >=> Http.safeWebApi webApi
                    setStatusCode 404 >=> text "Not Found"
                ]

        application {
            url ("http://*:" + settings.Port.ToString() + "/")
            use_mime_types [ ".svg", "image/svg+xml"; ".png", "image/png" ]
            use_static "public" //publicPath
            use_router webApp

            service_config (fun services ->
                services.AddHostedService<Http.LoggerShutdown>() |> ignore

                // B3 — Configure ForwardedHeadersMiddleware so XFF is only
                // honoured for connections from the trustedProxies allow-list
                // (loopback by default, overridable via GENPRES_TRUSTED_PROXIES).
                // X-Forwarded-Proto from the same proxies sets Request.IsHttps,
                // which is what makes the session cookie Secure behind a
                // TLS-terminating proxy (Kestrel itself only listens on http).
                services.Configure<ForwardedHeadersOptions>(fun (opts: ForwardedHeadersOptions) ->
                    opts.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto
                    opts.KnownProxies.Clear()

                    for ip in settings.TrustedProxies do
                        opts.KnownProxies.Add(ip)
                )
                |> ignore

                Http.addRateLimiting services |> ignore
                services
            )

            // B3 ForwardedHeaders → B2 security headers → A2 rate limiter.
            // UseForwardedHeaders must run first so the rate limiter sees the
            // real client IP via ctx.Connection.RemoteIpAddress.
            // UseRateLimiter activates the limiter registered via
            // addRateLimiting; without it the service is registered but
            // never invoked.
            app_config (fun app ->
                app
                    .UseForwardedHeaders()
                    .Use(System.Func<HttpContext, System.Func<Task>, Task>(Http.securityHeadersMiddleware))
                    .UseRateLimiter()
            )

            memory_cache
            use_gzip
        }


[<EntryPoint>]
let main _ =
    // Load .env so GENPRES_* variables are available even when the server
    // binary is launched directly (e.g. via Rider/VS Code) without first
    // sourcing .env in the shell. loadDotEnv only sets variables that are
    // not already present, preserving the shell/CI/Docker > .env > defaults
    // override chain.
    Env.loadDotEnv () |> ignore

    let settings = Config.fromEnv Env.getItem

    settings |> Config.banner (Env.getSystemInfo ()) |> writeInfoMessage

    // Fail-closed, before any listener binds. A refused configuration is
    // reported with an exit code, not an exception: in the Docker image the
    // runtime used to be PID 1, and the SIGABRT it sends itself after an
    // unhandled exception was dropped, leaving the container "running" with
    // nothing listening (issue #572). A genuine crash elsewhere is still an
    // unhandled exception on purpose; tini as PID 1 turns it into exit 134.
    match Config.validateStartup settings with
    | Error msg ->
        writeErrorMessage msg
        1
    | Ok startup ->
        // a degraded but permitted configuration (issue #590) is said once, before hosting
        startup.Warnings |> List.iter writeWarningMessage
        Host.build settings (Host.resourceProvider startup.UrlId) |> run
        0
