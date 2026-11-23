module Server

open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Informedica.GenPRES.Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System.Threading.Tasks

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL

open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.RateLimiting
open System.Threading.RateLimiting


/// Parses and validates the server settings from the environment. The functions take the environment
/// getter and return a Result, so this module performs no IO and the entry point decides to stop.
module Config =

    /// The GENPRES_* settings (and SERVER_PORT) as the server uses them.
    type Settings =
        {
            /// SERVER_PORT, default 8085
            Port: uint16
            /// True when GENPRES_PROD is 1.
            IsProd: bool
            /// GENPRES_URL_ID; None when unset or blank
            UrlId: string option
            /// GENPRES_PASSWORD; None when unset or blank
            Password: string option
            /// GENPRES_TRUSTED_PROXIES; the IPv4 and IPv6 loopback addresses when unset
            TrustedProxies: System.Net.IPAddress[]
            /// GENPRES_LOG as given; checked for production and shown in the banner. Logging reads the
            /// variable itself.
            Log: string
            /// GENPRES_DEBUG as given; checked for production and shown in the banner.
            Debug: string
            /// GENPRES_LANG, raw; None when unset or blank. Parsed by language.
            Lang: string option
            /// GENPRES_DB_CONNECTION, the SQLite session store; None when unset or blank
            DbConnection: string option
            /// GENPRES_SESSION_IDLE_MINUTES, raw; None when unset or blank. Parsed by
            /// parseSessionIdle.
            SessionIdle: string option
            /// GENPRES_TIME_ZONE, raw; None when unset or blank. Parsed by parseTimeZone.
            TimeZone: string option
        }


    /// Treats a blank value as unset, so an empty ENV line in a Dockerfile shows as not set and never
    /// reaches the resource loader, where it would fail later with a confusing "cannot find column" error.
    let nonBlank (raw: string option) = raw |> Option.filter String.notEmpty


    /// Banner display string for the password: never the value itself.
    let displayPassword (raw: string option) =
        raw
        |> nonBlank
        |> Option.map (fun _ -> "***")
        |> Option.defaultValue "NOT SET (admin operations disabled)"


    /// Shows only the last 5 characters of the Sheet ID in the banner, so the full ID never appears in
    /// logs or screenshots. Returns "NOT SET" when unset.
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


    /// Parses GENPRES_TRUSTED_PROXIES, a comma-separated list of IP addresses, into the proxies whose
    /// X-Forwarded-For header is trusted. Returns the loopback addresses when unset. An invalid entry is
    /// dropped, so a list without a valid entry trusts no proxy.
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
        |> Option.defaultValue [| System.Net.IPAddress.Loopback; System.Net.IPAddress.IPv6Loopback |]


    /// The shortest admin password allowed in production. A shorter password refuses the start; a missing
    /// password starts the server with admin operations disabled and a warning. Demo and development
    /// accept any password or none.
    let minProductionPasswordLength = 16


    /// Checks the admin password in production. Returns Ok None when all is well, Ok with a warning when
    /// the password is missing, which disables admin operations, and an Error when it is too short.
    let validateProductionPassword (isProd: bool) (password: string option) : Result<string option, string> =
        if not isProd then
            Ok None
        else
            // A blank password counts as unset, so it gets the warning instead of the error.
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


    /// The result of the start-up checks: the Sheet ID to build the host with and the warnings to print
    /// before hosting.
    type Startup =
        {
            UrlId: string
            Warnings: string list
        }


    /// The UI language when GENPRES_LANG is unset: Dutch.
    let defaultLanguage = Informedica.GenPRES.Shared.Localization.Dutch


    /// Parses GENPRES_LANG, an ISO 639-1 code in any case, into the UI language. Returns defaultLanguage
    /// when unset and an error naming the value when it is not a known language.
    let language (settings: Settings) : Result<Informedica.GenPRES.Shared.Localization.Locales, string> =
        match settings.Lang with
        | None -> Ok defaultLanguage
        | Some raw ->
            match Informedica.GenPRES.Shared.Localization.tryParse raw with
            | Some l -> Ok l
            | None ->
                let accepted =
                    Informedica.GenPRES.Shared.Localization.languages
                    |> Array.map (Informedica.GenPRES.Shared.Localization.toShortCode >> _.ToLower())
                    |> String.concat ", "

                let fallback =
                    defaultLanguage
                    |> Informedica.GenPRES.Shared.Localization.toShortCode
                    |> _.ToLower()

                Error
                    $"GENPRES_LANG=%s{raw} is not a language. Accepted: %s{accepted}. \
                      Unset it for the default (%s{fallback})."


    /// Shows the language in the banner, or the given value marked NOT A LANGUAGE, which validateStartup
    /// refuses.
    let displayLanguage (settings: Settings) =
        match language settings with
        | Ok l ->
            $"{l |> Informedica.GenPRES.Shared.Localization.toShortCode |> _.ToLower()} ({l |> Informedica.GenPRES.Shared.Localization.toString})"
        | Error _ ->
            let raw = settings.Lang |> Option.defaultValue ""
            $"%s{raw} (NOT A LANGUAGE)"


    /// Refuses GENPRES_DB_CONNECTION in production, because the SQLite session store is for test and
    /// development only. A stop-gap until #580 decides the session store of production.
    let validateStore (isProd: bool) (dbConnection: string option) : Result<unit, string> =
        match isProd, dbConnection with
        | true, Some _ ->
            Error
                "GENPRES_PROD=1 but GENPRES_DB_CONNECTION is set. \
                 The SQLite session store is for test and development only; unset it to start in production."
        | _ -> Ok()


    /// Refuses a production start with GENPRES_LOG=d, whose solver trace holds patient data and slows every
    /// calculation, or with GENPRES_DEBUG=1. The levels i, w and e are allowed, because the log files are the
    /// only record of how a dose was calculated.
    let validateProductionDebug (isProd: bool) (log: string) (debug: string) : Result<unit, string> =
        let debugLevel = log.Trim().ToLowerInvariant() = "d"
        let debugMode = debug.Trim() = "1"

        match isProd, debugLevel, debugMode with
        | true, true, _ ->
            Error
                "GENPRES_PROD=1 but GENPRES_LOG=d. \
                 Debug logging is for development against the demo data only; \
                 use GENPRES_LOG=i, w or e in production."
        | true, _, true ->
            Error
                "GENPRES_PROD=1 but GENPRES_DEBUG=1. \
                 Debug mode is for development against the demo data only; set GENPRES_DEBUG=0 in production."
        | _ -> Ok()


    /// Parses GENPRES_SESSION_IDLE_MINUTES into the idle lifetime of a session. Returns one hour when unset
    /// and an error for anything but a positive whole number, so a typo cannot set a lifetime nobody chose.
    let parseSessionIdle (raw: string option) : Result<System.TimeSpan, string> =
        match raw |> Option.map _.Trim() |> nonBlank with
        | None -> Ok ServerApi.Session.defaultIdleLifetime
        | Some s ->
            match System.Int32.TryParse s with
            | true, minutes when minutes > 0 -> Ok(System.TimeSpan.FromMinutes(float minutes))
            | _ ->
                Error
                    $"GENPRES_SESSION_IDLE_MINUTES is '%s{s}', not a positive whole number of minutes. \
                      Leave it unset for the default of %i{int ServerApi.Session.defaultIdleLifetime.TotalMinutes} minutes."


    /// Parses GENPRES_TIME_ZONE into the time zone ages are calculated in. Returns Europe/Amsterdam when
    /// unset and an error for a zone id the system does not know, so a typo cannot shift every age.
    let parseTimeZone (raw: string option) : Result<System.TimeZoneInfo, string> =
        let id =
            raw
            |> Option.map _.Trim()
            |> nonBlank
            |> Option.defaultValue ServerApi.LocalDate.defaultZoneId

        ServerApi.LocalDate.zoneOf id
        |> Result.mapError (fun _ ->
            $"GENPRES_TIME_ZONE is '%s{id}', not a time zone this system knows. \
              Use an IANA id such as Europe/Amsterdam, or leave it unset for %s{ServerApi.LocalDate.defaultZoneId}."
        )


    /// Runs the start-up checks in order: session store, production debugging, idle lifetime, time zone,
    /// password, language, then GENPRES_URL_ID. Returns the Sheet ID and the warnings to print, or the
    /// message the server exits with.
    let validateStartup (settings: Settings) : Result<Startup, string> =
        validateStore settings.IsProd settings.DbConnection
        |> Result.bind (fun () -> validateProductionDebug settings.IsProd settings.Log settings.Debug)
        |> Result.bind (fun () -> parseSessionIdle settings.SessionIdle)
        |> Result.bind (fun _ -> parseTimeZone settings.TimeZone)
        |> Result.bind (fun _ -> validateProductionPassword settings.IsProd settings.Password)
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


    /// Maps the settings and the departments to the settings sent to the client. The departments to choose from
    /// are None first, for no department, then the departments the rules name. Call after
    /// validateStartup, which refuses an invalid language, so the default language is never used as a fallback
    /// here.
    let toServerSettings (settings: Settings) departments : Informedica.GenPRES.Shared.Api.ServerSettings =
        {
            Language = language settings |> Result.defaultValue defaultLanguage
            IsDemo = not settings.IsProd
            Departments = departments |> Array.map Some |> Array.append [| None |]
        }


    /// Reads every setting through getEnv. Pure: pass Env.getItem
    /// for the real environment, a Map.tryFind in tests.
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
            Debug = getEnv "GENPRES_DEBUG" |> Option.defaultValue "0"
            Lang = getEnv "GENPRES_LANG" |> nonBlank
            DbConnection = getEnv "GENPRES_DB_CONNECTION" |> nonBlank
            SessionIdle = getEnv "GENPRES_SESSION_IDLE_MINUTES" |> nonBlank
            TimeZone = getEnv "GENPRES_TIME_ZONE" |> nonBlank
        }


    /// The start-up banner. Secrets are redacted; systemInfo is passed
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
GENPRES_DB_CONNECTION = {if settings.DbConnection.IsSome then
                             "set (SQLite session store)"
                         else
                             "unset (in-memory session store)"}
GENPRES_SESSION_IDLE_MINUTES = {settings.SessionIdle |> Option.defaultValue "unset (an hour)"}
GENPRES_TIME_ZONE = {settings.TimeZone
                     |> Option.defaultValue $"unset ({ServerApi.LocalDate.defaultZoneId})"}

=== System Info ===

{systemInfo}

"""


/// The HTTP handlers, the middleware and the hosted service. The settings they need arrive as
/// parameters from Host.build.
module Http =

    /// Returns the IP address of the peer, or "unknown". Behind a trusted proxy, UseForwardedHeaders has
    /// replaced it with the client IP, so a client cannot pick its own rate-limiter partition by sending
    /// an X-Forwarded-For header.
    let getClientIP (context: HttpContext) =
        match context.Connection.RemoteIpAddress with
        | null -> "unknown"
        | ip -> ip.ToString()


    let sessionCookieName = "genpres_session"


    /// Returns the options of the session cookie, a bearer credential: HttpOnly, SameSite=Strict, Path=/,
    /// and Secure over HTTPS, which behind a proxy comes from X-Forwarded-Proto. Without a Domain the cookie
    /// stays on this host and passes the Vite development proxy unchanged.
    let sessionCookieOptions (isHttps: bool) =
        CookieOptions(HttpOnly = true, Secure = isHttps, SameSite = SameSiteMode.Strict, Path = "/")


    /// Names the launch state cookie after the state, so two tabs can launch at once without overwriting
    /// each other's cookie. The state is base64url, which is valid in a cookie name.
    let launchStateCookieName (state: string) = $"genpres_launch_state.{state}"


    /// Returns the options of the launch state cookie: HttpOnly, SameSite=Lax so the redirect back from the
    /// identity provider carries it, Path=/callback, Max-Age the launch lifetime and Secure over HTTPS.
    /// The cookie grants nothing; it only proves which browser started the launch.
    let launchStateCookieOptions (isHttps: bool) =
        CookieOptions(
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/callback",
            MaxAge = StubLaunch.lifetime
        )


    /// Returns the options of the cookie that carries the identity chosen on the stub launch page to the
    /// stub /authorize: HttpOnly, SameSite=Lax, Path=/ and Max-Age the launch lifetime. It is not a credential.
    let stubIdentityCookieOptions (isHttps: bool) =
        CookieOptions(
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = StubLaunch.lifetime
        )


    /// Reads and writes the launch state cookies of this request.
    let launchStateCookie (ctx: HttpContext) : LaunchStateCookie =
        {
            read =
                fun state ->
                    match ctx.Request.Cookies.TryGetValue(launchStateCookieName state) with
                    | true, value when value |> String.notEmpty -> Some value
                    | _ -> None
            write =
                fun state ->
                    ctx.Response.Cookies.Append(
                        launchStateCookieName state,
                        state,
                        launchStateCookieOptions ctx.Request.IsHttps
                    )
        }


    let enrolmentCookieName = "genpres_enrolment"


    /// Returns the options of the enrolment cookie, which holds the enrolment attempt of a launch waiting
    /// for the PIN: HttpOnly, SameSite=Strict, Path=/, Max-Age the time from now to until, or zero, and
    /// Secure over HTTPS. The cookie grants nothing without the mailed confirmation code.
    let enrolmentCookieOptions (isHttps: bool) (now: System.DateTime) (until: System.DateTime) =
        CookieOptions(
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = (if until > now then until - now else System.TimeSpan.Zero)
        )


    /// Reads, writes and deletes the enrolment cookie of this request.
    let enrolmentCookie (ctx: HttpContext) : EnrolmentCookie =
        {
            read =
                fun () ->
                    match ctx.Request.Cookies.TryGetValue enrolmentCookieName with
                    | true, value when value |> String.notEmpty -> Some value
                    | _ -> None
            write =
                fun attempt until ->
                    ctx.Response.Cookies.Append(
                        enrolmentCookieName,
                        attempt,
                        enrolmentCookieOptions ctx.Request.IsHttps System.DateTime.UtcNow until
                    )
            delete =
                fun () ->
                    let now = System.DateTime.UtcNow
                    ctx.Response.Cookies.Delete(enrolmentCookieName, enrolmentCookieOptions ctx.Request.IsHttps now now)
        }


    /// Reads, writes and deletes the session cookie of this request.
    let sessionCookie (ctx: HttpContext) : SessionCookie =
        {
            read =
                fun () ->
                    match ctx.Request.Cookies.TryGetValue sessionCookieName with
                    | true, value when value |> String.notEmpty -> Some value
                    | _ -> None
            write =
                fun id -> ctx.Response.Cookies.Append(sessionCookieName, id, sessionCookieOptions ctx.Request.IsHttps)
            delete = fun () -> ctx.Response.Cookies.Delete(sessionCookieName, sessionCookieOptions ctx.Request.IsHttps)
        }


    /// Returns the Cache-Control value for a response. A successful response for a content-hashed file under
    /// /assets/ is cached for a year; any other response gets no-cache, so the browser revalidates it with
    /// the ETag on every load. Without the header a browser guesses a lifetime and can keep an old index.html.
    /// An error for an asset is never cached, because the asset may still arrive in a rolling deploy.
    /// The path is null for a request without one, such as OPTIONS *.
    let cacheControlFor (statusCode: int) (path: string) =
        let isAsset =
            path |> String.notNullOrEmpty
            && path.StartsWith("/assets/", System.StringComparison.OrdinalIgnoreCase)

        if isAsset && statusCode >= 200 && statusCode < 300 then
            "public, max-age=31536000, immutable"
        else
            "no-cache"


    /// Sets the security headers and Cache-Control on every response, static files and errors included,
    /// through Response.OnStarting. Cache-Control is set here because Saturn's use_static offers no hook for it.
    /// The content security policy allows styles and fonts from maxcdn and Google Fonts and the Sheet fetches
    /// from docs.google.com. Inline styles are allowed because MUI injects style tags at run time; scripts
    /// stay limited to the own origin, so an injection cannot run code. X-Powered-By is removed.
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


    /// Registers a rate limiter per client IP for every request: 60 requests per 10-second window, no queue,
    /// and a 429 for each request over the limit. A form action fans out to a few calls, so normal use fits,
    /// while scripted guessing of the admin password is slowed. The client IP comes from getClientIP, so a
    /// forged forwarded header cannot bypass the limiter.
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
            | Some _ ->
                let clientIP = getClientIP ctx
                let path = ctx.Request.Path.ToString()
                let method = ctx.Request.Method
                let logger = Logging.getLogger Logging.RequestLogger

                Logging.ServerLogging.logRequest logger method path clientIP

                next ctx


    /// Turns a MissingMethodException or TypeLoadException from the API into a plain 400 response, so a
    /// library version mismatch never puts a .NET type signature in the response body.
    let safeWebApi (webApi: HttpHandler) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                try
                    return! webApi next ctx
                with ex when (ex :? System.MissingMethodException || ex :? System.TypeLoadException) ->
                    // Writes the fault to stderr, and to the log when logging is on. A response that has
                    // started cannot be changed, so then this is the only trace of the fault.
                    let msg =
                        $"safeWebApi caught %s{ex.GetType().Name} on %s{ctx.Request.Path.ToString()}: %s{ex.Message}"

                    eprintfn $"{msg}"

                    match Logging.loggingLevel with
                    | Some _ ->
                        let logger = Logging.getLogger Logging.RequestLogger

                        Logging.ServerLogging.Error msg
                        |> Informedica.Logging.Lib.Logging.logError logger
                    | None -> ()

                    if not ctx.Response.HasStarted then
                        ctx.Response.StatusCode <- 400
                        ctx.Response.ContentType <- "application/json; charset=utf-8"
                        do! ctx.Response.WriteAsync "\"Bad Request\""

                    return Some ctx
            }


    /// Stops every logger this process forced into existence when the host shuts down.
    type LoggerShutdown() =
        interface IHostedService with
            member _.StartAsync _ = Task.CompletedTask

            member _.StopAsync _ =
                Logging.disposeAll ()
                Task.CompletedTask


/// The composition root: wires settings, the resource provider and the
/// Http pieces into a Saturn application. Only main calls this.
module Host =

    /// The cached GenFORM resource provider for a Sheet ID, with the
    /// resources logger attached when GENPRES_LOG is set. Logs a warning for
    /// each category in the RuleSets sheet that is not a rule set category.
    let resourceProvider (urlId: string) =
        let logger = Logging.getLogger Logging.ResourcesLogger
        let provider = urlId |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger
        provider |> ServerApi.Adapters.RuleSets.warnUnknownCategories logger
        provider


    let build (settings: Config.Settings) (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        // These values hold the stub session state, so they are built once per host and not inside
        // Remoting.fromContext, which runs per request. A new launch key per start refuses a launch
        // token from an earlier run.
        let launchKey = LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes

        // The stub identity provider and user registry: /authorize issues one-time codes for an identity
        // and patient, and the callback redeems them.
        let directory = StubDirectory.make (fun () -> System.DateTime.UtcNow) PublicKey.randomId

        // The stub mail service: an outbox shown on the stub mail page, where a tester reads the
        // confirmation code.
        let mail = StubMail.make ()

        let env =
            let env =
                Adapters.makeAppEnvWith
                    (not settings.IsProd)
                    settings.DbConnection
                    // validated at the start, so the error cannot happen
                    (Config.parseTimeZone settings.TimeZone |> Result.defaultWith invalidOp)
                    // validated at the start, so the error cannot happen
                    (Config.parseSessionIdle settings.SessionIdle |> Result.defaultWith invalidOp)
                    launchKey
                    directory
                    mail.port
                    provider

            // Stop-gap until #580 decides what production exposes: a production server never opens a
            // stub session.
            if settings.IsProd then
                { env with session = Adapters.sessionDisabled }
            else
                env

        // The settings are mapped once here; the departments are added per request.
        let serverSettings = Config.toServerSettings settings

        let webApi =
            Remoting.createApi ()
            |> Remoting.fromContext (fun ctx ->
                createServerApi
                    serverSettings
                    env
                    (Http.sessionCookie ctx)
                    (Http.launchStateCookie ctx)
                    (Http.enrolmentCookie ctx)
            )
            |> Remoting.withRouteBuilder routerPaths
            |> Remoting.buildHttpHandler

        // The stub launch routes, not mounted in production, so production never creates a launch.
        // GET shows the launch form, POST creates a launch and redirects.
        let stubLaunch =
            if settings.IsProd then
                []
            else
                [
                    GET >=> route StubLaunch.path >=> htmlString StubLaunch.page
                    // The stub mail outbox page.
                    GET
                    >=> route StubMail.path
                    >=> fun next ctx -> htmlString (StubMail.page (mail.sent ())) next ctx
                    POST
                    >=> route StubLaunch.path
                    >=> fun next ctx ->
                        task {
                            let! form = ctx.Request.ReadFormAsync()

                            let field (name: string) =
                                match form.TryGetValue name with
                                | true, values -> values.ToString()
                                | _ -> ""

                            // The chosen stub patient, or the typed patient id when Other is chosen.
                            let pid = StubLaunch.patientIdOf (field "patient") (field "pid")

                            let choice =
                                match form.TryGetValue "identity" with
                                | true, values when StubLaunch.choices |> List.contains (values.ToString()) ->
                                    values.ToString()
                                | _ -> "prescriber"

                            let launch = StubLaunch.mint System.DateTime.UtcNow PublicKey.randomId launchKey pid

                            // Stores the chosen identity and patient for the stub /authorize.
                            ctx.Response.Cookies.Append(
                                StubLaunch.identityCookieName,
                                StubLaunch.identityCookie choice pid,
                                Http.stubIdentityCookieOptions ctx.Request.IsHttps
                            )

                            return! redirectTo false (StubLaunch.launchUrl launch) next ctx
                        }
                    // The stub identity provider: redirects to the callback with the received state and a
                    // one-time code for the chosen identity, or with error=no-identity.
                    GET
                    >=> route "/authorize"
                    >=> fun next ctx ->
                        let state = ctx.TryGetQueryStringValue "state" |> Option.defaultValue ""

                        let identity =
                            match ctx.Request.Cookies.TryGetValue StubLaunch.identityCookieName with
                            | true, value -> StubLaunch.parseIdentityCookie (Some value)
                            | _ -> None

                        let url =
                            match identity with
                            | None
                            | Some("none", _) ->
                                $"/callback?state={System.Uri.EscapeDataString state}&error=no-identity"
                            | Some(choice, pid) ->
                                let code = directory.issue choice pid
                                $"/callback?code={System.Uri.EscapeDataString code}&state={System.Uri.EscapeDataString state}"

                        redirectTo false url next ctx
                ]

        // The return from the identity provider, mounted in every mode. With the session disabled the
        // callback is refused as invalid.
        let callback =
            GET
            >=> route "/callback"
            >=> fun next ctx ->
                task {
                    let query name = ctx.TryGetQueryStringValue name

                    let cb: Callback =
                        {
                            State = query "state" |> Option.defaultValue ""
                            StateCookie = None
                            Code = query "code"
                            Error = query "error"
                        }

                    let! redirect =
                        LaunchCommand.processCallback
                            env
                            (Http.sessionCookie ctx)
                            (Http.launchStateCookie ctx)
                            (Http.enrolmentCookie ctx)
                            cb

                    return! redirectTo false redirect next ctx
                }

        // Logs each request once, before the routes are tried.
        let webApp =
            Http.logClientIP
            >=> choose
                    [
                        yield! stubLaunch
                        callback
                        Http.safeWebApi webApi
                        setStatusCode 404 >=> text "Not Found"
                    ]

        application {
            url ("http://*:" + settings.Port.ToString() + "/")
            use_mime_types [ ".svg", "image/svg+xml"; ".png", "image/png" ]
            use_static "public"
            use_router webApp

            service_config (fun services ->
                services.AddHostedService<Http.LoggerShutdown>() |> ignore

                // Trusts forwarded headers only from the trusted proxies. X-Forwarded-Proto sets
                // Request.IsHttps, which makes the session cookie Secure behind a TLS-terminating proxy;
                // Kestrel itself listens on http only.
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

            // The forwarded headers run first, so the rate limiter sees the real client IP.
            // UseRateLimiter activates the limiter that addRateLimiting registers.
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
    // One culture on every host: a number parses and prints with a decimal point whatever locale the
    // host or the container gives. This sets a default culture instead of switching on invariant
    // globalization, because invariant globalization switches off ICU, and without ICU a Windows host
    // cannot find a time zone by its IANA name.
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture <- System.Globalization.CultureInfo.InvariantCulture

    // Writes the internal failures of Serilog, such as a full disk, to stderr; otherwise Serilog swallows
    // them and the log stops without a sign. Must run before the first Serilog logger is built.
    Serilog.Debugging.SelfLog.Enable(fun msg -> eprintfn $"[Serilog] {msg}")

    // Loads .env, so the GENPRES_* variables are set when the server starts from an IDE.
    // A variable already set in the environment wins over .env.
    Env.loadDotEnv () |> ignore

    let settings = Config.fromEnv Env.getItem

    settings |> Config.banner (Env.getSystemInfo ()) |> writeInfoMessage

    // Refuses a bad configuration before any listener binds, with an exit code and not an exception:
    // a .NET runtime running as PID 1 in Docker can miss its own abort and leave the container up with
    // nothing listening. Other crashes stay unhandled exceptions; tini as PID 1 turns them into exit 134.
    match
        Config.validateStartup settings
        // the store is made ready here, for the same reason: a file that cannot be migrated or
        // seeded is a refused start with a message and an exit code, not a crash while hosting
        |> Result.bind (fun startup -> Adapters.prepareStore settings.DbConnection |> Result.map (fun () -> startup))
    with
    | Error msg ->
        writeErrorMessage msg
        1
    | Ok startup ->
        // a degraded but permitted configuration is written once, before hosting
        startup.Warnings |> List.iter writeWarningMessage
        Host.build settings (Host.resourceProvider startup.UrlId) |> run
        0
