//module Server

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


// B3 — Returns the immediate peer IP. After UseForwardedHeaders runs
// (registered via app_config below) this is the real client IP for
// requests that arrived through a known proxy, and the actual peer
// for direct connections. The previous version trusted X-Forwarded-For
// from any source (finding B3); that path is now obsolete and the
// rate limiter's partition cardinality is bounded by real ingress IPs.
let getClientIP (context: HttpContext) =
    match context.Connection.RemoteIpAddress with
    | null -> "unknown"
    | ip -> ip.ToString()


// Load .env so GENPRES_* variables are available even when the server
// binary is launched directly (e.g. via Rider/VS Code) without first
// sourcing .env in the shell. loadDotEnv only sets variables that are
// not already present, preserving the shell/CI/Docker > .env > defaults
// override chain.
let loadEnvironment () = Env.loadDotEnv () |> ignore


loadEnvironment ()
let tryGetEnv key = Env.getItem key


// Banner display strings. Empty/whitespace is treated as not-set so an
// empty `ENV GENPRES_PASSWORD=` / `ENV GENPRES_URL_ID=` from the Dockerfile
// is reported truthfully instead of looking injected.
let password =
    tryGetEnv "GENPRES_PASSWORD"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Option.map (fun _ -> "***")
    |> Option.defaultValue "NOT SET (admin operations disabled)"


// Show only the last 5 chars of the Sheet ID so it never lands in logs or
// screenshots intact. Built here, not in the format string below, so the
// `NOT SET` path doesn't render as `***NOT SET`.
let urlId =
    tryGetEnv "GENPRES_URL_ID"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Option.map (fun s ->
        if s.Length > 5 then
            $"***%s{s.Substring(s.Length - 5)}"
        else
            "***<redacted>"
    )
    |> Option.defaultValue "NOT SET"


$"""

=== Environmental variables ===
GENPRES_URL_ID = {urlId}
GENPRES_LOG ={tryGetEnv "GENPRES_LOG" |> Option.defaultValue "0"}
GENPRES_PROD = {tryGetEnv "GENPRES_PROD" |> Option.defaultValue "0"}
GENPRES_DEBUG = {tryGetEnv "GENPRES_DEBUG" |> Option.defaultValue "i"}
GENPRES_PASSWORD = {password}

=== System Info ===

{Env.getSystemInfo ()}

"""
|> writeInfoMessage


let port =
    "SERVER_PORT" |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us


// B3 — Trusted reverse-proxy allow-list for ForwardedHeadersMiddleware.
// Default = loopback only (matches the Plesk → Kestrel hop on the
// public demo deployments and any local-dev setup). Override with
// GENPRES_TRUSTED_PROXIES as a comma-separated list of IPs, e.g.
//     GENPRES_TRUSTED_PROXIES="10.0.0.5, 10.0.0.6"
// for a hospital LAN behind a known nginx fleet. Unparseable values
// are silently dropped — fail-open on the parser, fail-closed on the
// allow-list (no entry = no XFF trust).
let trustedProxies =
    tryGetEnv "GENPRES_TRUSTED_PROXIES"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
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


// SECURITY: in production mode (GENPRES_PROD=1), refuse to start without
// a GENPRES_PASSWORD of at least minProductionPasswordLength characters.
// Fail-closed, runs before any listener binds. Demo/dev mode accepts any
// value (or none — admin ops just stay disabled).
let private minProductionPasswordLength = 16


let private validateProductionPassword () =
    let isProd =
        tryGetEnv "GENPRES_PROD"
        |> Option.map (fun v -> v = "1")
        |> Option.defaultValue false

    if isProd then
        // Empty/whitespace = unset, so a forgotten Docker env hits the
        // "not set" branch instead of "shorter than 16 characters".
        match
            tryGetEnv "GENPRES_PASSWORD"
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        with
        | None ->
            invalidOp
                "GENPRES_PROD=1 but GENPRES_PASSWORD is not set (or is empty). \
                 Refusing to start in production without an admin password. \
                 Generate one with `openssl rand -base64 32` and inject it via a secret store. \
                 See DEVELOPMENT.md → Password policy."
        | Some pwd when pwd.Length < minProductionPasswordLength ->
            invalidOp
                $"GENPRES_PROD=1 but GENPRES_PASSWORD is shorter than %i{minProductionPasswordLength} characters. \
                 Refusing to start in production with a weak admin password. \
                 Generate a stronger one with `openssl rand -base64 32`. \
                 See DEVELOPMENT.md → Password policy."
        | Some _ -> ()


validateProductionPassword ()


let provider =
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

    // Empty/whitespace = unset, otherwise an empty Docker env would flow
    // into getCachedProviderWithDataUrlId and surface much later as a
    // confusing "cannot find column" error from GenForm.
    tryGetEnv "GENPRES_URL_ID"
    |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
    |> Option.defaultWith (fun () -> invalidOp "No GENPRES_URL_ID (or value is empty)")
    |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger


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


// B2 — Security response header baseline. ASP.NET middleware (wired via
// app_config) using Response.OnStarting so headers land on every flushed
// response: static files, Giraffe routes, the 404 fallback, and
// Fable.Remoting error responses alike.
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
let private securityHeadersMiddleware (ctx: HttpContext) (next: System.Func<Task>) : Task =
    ctx.Response.OnStarting(fun () ->
        let h = ctx.Response.Headers
        h["Strict-Transport-Security"] <- "max-age=31536000; includeSubDomains"
        h["X-Content-Type-Options"] <- "nosniff"
        h["X-Frame-Options"] <- "DENY"
        h["Referrer-Policy"] <- "no-referrer"
        h["Permissions-Policy"] <- "geolocation=(), camera=(), microphone=()"

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


let webApi =
    Remoting.createApi ()
    |> Remoting.fromValue (createServerApi provider)
    |> Remoting.withRouteBuilder routerPaths
    |> Remoting.buildHttpHandler


// L1 — Defense-in-depth wrapper. The original Fable.Remoting.Giraffe 5.24
// ABI drift against Giraffe 7+ (MissingMethodException / TypeLoadException
// from Giraffe.Core.setBodyFromString, leaking full .NET type signatures)
// is resolved upstream in Fable.Remoting.Giraffe 6.1.0. This wrapper is
// retained as belt-and-braces so any future reflection/ABI fault returns
// a clean 400 instead of a raw exception body.
let safeWebApi: HttpHandler =
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


// Security headers come from securityHeadersMiddleware (app_config below),
// so this stays handler-only. The 404 arm replaces a legacy
// "GenInteractions App. Use localhost: 8080 for the GUI" string that
// leaked an old app name and hinted at port 8080 (L2 / B5).
let webApp =
    choose
        [
            logClientIP >=> safeWebApi
            setStatusCode 404 >=> text "Not Found"
        ]


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


let application =
    application {
        url ("http://*:" + port.ToString() + "/")
        use_mime_types [ ".svg", "image/svg+xml"; ".png", "image/png" ]
        use_static "public" //publicPath
        use_router webApp

        service_config (fun services ->
            services.AddHostedService<LoggerShutdown>() |> ignore

            // B3 — Configure ForwardedHeadersMiddleware so XFF is only
            // honoured for connections from the trustedProxies allow-list
            // (loopback by default, overridable via GENPRES_TRUSTED_PROXIES).
            services.Configure<ForwardedHeadersOptions>(fun (opts: ForwardedHeadersOptions) ->
                opts.ForwardedHeaders <- ForwardedHeaders.XForwardedFor
                opts.KnownProxies.Clear()

                for ip in trustedProxies do
                    opts.KnownProxies.Add(ip)
            )
            |> ignore

            addRateLimiting services |> ignore
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
                .Use(System.Func<HttpContext, System.Func<Task>, Task>(securityHeadersMiddleware))
                .UseRateLimiter()
        )

        memory_cache
        use_gzip
    }

run application
