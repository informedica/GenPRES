// Server settings drafts (script-only policy): GENPRES_LANG (merged, #601) and, for #590, the
// production password policy that starts with admin operations disabled instead of refusing.
//
// #590: `validateProductionPassword` answers `Ok (Some warning)` when GENPRES_PROD=1 and the
// password is missing or blank: the server starts on the configured data, prints the warning,
// and the admin commands stay disabled because they read the (unset) variable themselves.
// A password shorter than 16 characters still refuses (user decision: a weak secret would stay
// live in those env reads). `validateStartup` returns a `Startup` with the url id and the
// warnings to print. To migrate into `Server.fs`, module `Config`, and `main`.
//
// Earlier draft (merged) of:
//   - `Config.Settings.Lang` (raw, `nonBlank`), `Config.language` (derived, `Result`), the
//     `validateStartup` extension, the banner line — to migrate into `Server.fs`, module `Config`;
//   - `ServerSettings` and `getSettings` — to migrate into `Shared/Api.fs` next to `IServerApi`
//     (the record cannot be extended here, so `getSettings` is sketched as a function);
//   - `Config.toServerSettings` — the DMZ mapping from the env-shaped `Settings` to the contract.
// `Server.fs` itself is not loaded (Saturn host); `Config` is restated here with only the fields
// that matter, following the source shape.
//
// Run: `dotnet fsi Settings.fsx` from this directory (build the solution first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Localization


/// One parser for env value, url parameter and sheet header (Shared/Scripts/Localization.fsx).
module Localization =

    let tryParse (s: string) : Locales option =
        if isNull s then
            None
        else
            match s.Trim().ToLower() with
            | "en" -> Some English
            | "nl"
            | "du" -> Some Dutch
            | "fr" -> Some French
            | "de"
            | "gr" -> Some German
            | "es"
            | "sp" -> Some Spanish
            | "it" -> Some Italian
            | s -> tryFromString s


/// What the client learns from the server at start-up (→ Shared/Api.fs). PR 2 adds `Scope`.
type ServerSettings =
    {
        // GENPRES_LANG: the UI language until the url or the User chooses one
        Language: Locales
        // not GENPRES_PROD: demo data, the title suffix
        IsDemo: bool
    }


module Config =

    /// The GENPRES_* settings as the server uses them (only the fields this draft touches).
    type Settings =
        {
            // GENPRES_PROD = "1"
            IsProd: bool
            // GENPRES_URL_ID; None when unset or blank
            UrlId: string option
            // GENPRES_PASSWORD; None when unset or blank
            Password: string option
            // GENPRES_LANG, raw; None when unset or blank. Parsed by `language`.
            Lang: string option
        }


    let nonBlank (raw: string option) =
        raw |> Option.filter (System.String.IsNullOrWhiteSpace >> not)


    let minProductionPasswordLength = 16


    /// <summary>
    /// The production password policy. <c>Ok None</c>: nothing to say. <c>Ok (Some warning)</c>:
    /// the server starts with admin operations disabled and prints the warning (#590: a missing
    /// or blank password no longer refuses the start; the admin commands fail closed on the
    /// unset variable). <c>Error</c>: the message the server refuses to start with, kept for a
    /// password shorter than <c>minProductionPasswordLength</c>, which would otherwise stay live.
    /// </summary>
    let validateProductionPassword (isProd: bool) (password: string option) : Result<string option, string> =
        if not isProd then
            Ok None
        else
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


    /// What a start-up needs: the url id the host is built with, and the warnings to print
    /// before hosting (today at most the password warning).
    type Startup =
        {
            UrlId: string
            Warnings: string list
        }


    /// The default UI language. Unset means Dutch, the client's default until now; a value
    /// that is not a language is the message the server refuses to start with.
    let defaultLanguage = Dutch


    /// <summary>
    /// The UI language from <c>GENPRES_LANG</c>: <c>defaultLanguage</c> when unset, the parsed
    /// language when it is one (ISO code, display name or legacy url code, any case), otherwise
    /// the start-up error naming the setting and the value.
    /// </summary>
    let language (settings: Settings) : Result<Locales, string> =
        match settings.Lang with
        | None -> Ok defaultLanguage
        | Some raw ->
            match Localization.tryParse raw with
            | Some l -> Ok l
            | None ->
                let accepted = languages |> Array.map (toShortCode >> _.ToLower()) |> String.concat ", "

                Error
                    $"GENPRES_LANG=%s{raw} is not a language. Accepted: %s{accepted} \
                      (or a display name such as Nederlands). Unset it for the default (%s{toShortCode defaultLanguage |> _.ToLower()})."


    /// <summary>
    /// Every start-up guard in one place: the production password policy, the language, then
    /// the presence of <c>GENPRES_URL_ID</c>. <c>Ok</c> carries the URL ID the host needs.
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


    let fromEnv (getEnv: string -> string option) : Settings =
        {
            IsProd =
                getEnv "GENPRES_PROD"
                |> Option.map (fun v -> v = "1")
                |> Option.defaultValue false
            UrlId = getEnv "GENPRES_URL_ID" |> nonBlank
            Password = getEnv "GENPRES_PASSWORD" |> nonBlank
            Lang = getEnv "GENPRES_LANG" |> nonBlank
        }


    /// Banner display string for the language: the derived value, or the raw one when it is
    /// not a language (the refusal follows from `validateStartup`).
    let displayLanguage (settings: Settings) =
        match language settings with
        | Ok l -> $"{toShortCode l |> _.ToLower()} ({toString l})"
        | Error _ ->
            let raw = settings.Lang |> Option.defaultValue ""
            $"%s{raw} (NOT A LANGUAGE)"


    /// <summary>
    /// The settings the client learns, mapped in the DMZ from the env-shaped record. Call after
    /// <c>validateStartup</c>: an invalid language never reaches here, so the fallback is dead.
    /// </summary>
    let toServerSettings (settings: Settings) : ServerSettings =
        {
            Language = language settings |> Result.defaultValue defaultLanguage
            IsDemo = not settings.IsProd
        }


/// `IServerApi.getSettings` (→ CompositionRoot.compose settings env cookie): the value the host
/// computed once, answered per request, logged like the other calls.
let getSettings (settings: ServerSettings) : unit -> Async<ServerSettings> =
    fun () ->
        async {
            Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime.writeInfoMessage "Processing settings"
            return settings
        }


open Expecto
open Expecto.Flip


let getEnv (m: Map<string, string>) key = m |> Map.tryFind key

let settingsOf m = Config.fromEnv (getEnv m)


let tests =
    testList
        "GENPRES_LANG"
        [
            test "unset is the default language, Dutch" {
                Map.empty |> settingsOf |> Config.language |> Expect.equal "default" (Ok Dutch)
            }

            test "blank is unset" {
                let s = Map [ "GENPRES_LANG", "  " ] |> settingsOf
                s.Lang |> Expect.isNone "blank"
                s |> Config.language |> Expect.equal "default" (Ok Dutch)
            }

            testList
                "accepted spellings"
                [
                    for raw, l in [ "en", English; "NL", Dutch; " fr ", French; "de", German; "es", Spanish; "it", Italian; "gr", German; "sp", Spanish; "du", Dutch; "Nederlands", Dutch ] do
                        test $"'{raw}'" {
                            Map [ "GENPRES_LANG", raw ] |> settingsOf |> Config.language |> Expect.equal raw (Ok l)
                        }
                ]

            test "a value that is not a language is a start-up error naming the setting and the value" {
                match Map [ "GENPRES_LANG", "klingon"; "GENPRES_URL_ID", "id" ] |> settingsOf |> Config.validateStartup with
                | Error msg ->
                    msg |> Expect.stringContains "setting" "GENPRES_LANG"
                    msg |> Expect.stringContains "value" "klingon"
                    msg |> Expect.stringContains "accepted" "nl"
                | Ok _ -> failtest "expected Error"
            }

            test "the language is checked after the password and before the url id" {
                match
                    Map [ "GENPRES_PROD", "1"; "GENPRES_PASSWORD", "short"; "GENPRES_LANG", "klingon" ]
                    |> settingsOf
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "password first" "GENPRES_PASSWORD"
                | Ok _ -> failtest "expected Error"

                // a missing password is a warning (#590), so it does not mask the language error
                match Map [ "GENPRES_PROD", "1"; "GENPRES_LANG", "klingon" ] |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "language after the warning" "GENPRES_LANG"
                | Ok _ -> failtest "expected Error"

                match Map [ "GENPRES_LANG", "klingon" ] |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "language before url id" "GENPRES_LANG"
                | Ok _ -> failtest "expected Error"
            }

            test "a valid language still starts with the url id" {
                Map [ "GENPRES_LANG", "en"; "GENPRES_URL_ID", "id" ]
                |> settingsOf
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok { Config.Startup.UrlId = "id"; Config.Startup.Warnings = [] })
            }

            test "toServerSettings carries the language and the demo flag" {
                Map [ "GENPRES_LANG", "en" ]
                |> settingsOf
                |> Config.toServerSettings
                |> Expect.equal "demo, English" { Language = English; IsDemo = true }

                Map [ "GENPRES_PROD", "1" ]
                |> settingsOf
                |> Config.toServerSettings
                |> Expect.equal "prod, Dutch" { Language = Dutch; IsDemo = false }
            }

            test "the banner shows the derived language, or flags the raw value" {
                Map [ "GENPRES_LANG", "EN" ] |> settingsOf |> Config.displayLanguage |> Expect.equal "derived" "en (English)"

                Map [ "GENPRES_LANG", "klingon" ]
                |> settingsOf
                |> Config.displayLanguage
                |> Expect.equal "flagged" "klingon (NOT A LANGUAGE)"
            }

            testAsync "getSettings answers what the host computed" {
                let s = { Language = French; IsDemo = false }
                let! answer = getSettings s ()
                answer |> Expect.equal "same value" s
            }
        ]


let sixteen = String.replicate 16 "x"
let fifteen = String.replicate 15 "x"


let passwordTests =
    testList
        "GENPRES_PASSWORD (#590)"
        [
            test "demo mode: no password, nothing to say" {
                Config.validateProductionPassword false None |> Expect.equal "Ok None" (Ok None)
            }

            test "production without a password starts with a warning naming the setting and what is disabled" {
                match Config.validateProductionPassword true None with
                | Ok(Some warning) ->
                    warning |> Expect.stringContains "setting" "GENPRES_PASSWORD"
                    warning |> Expect.stringContains "cause" "not set"
                    warning |> Expect.stringContains "effect" "admin operations disabled"
                | other -> failtest $"expected Ok (Some warning), got {other}"
            }

            test "production with a blank password is the same as none" {
                Config.validateProductionPassword true (Some "  ")
                |> Expect.equal "same warning" (Config.validateProductionPassword true None)
            }

            test "production with 15 characters still refuses, naming the minimum" {
                match Config.validateProductionPassword true (Some fifteen) with
                | Error msg -> msg |> Expect.stringContains "minimum" "16"
                | other -> failtest $"expected Error, got {other}"
            }

            test "production with 16 characters: nothing to say" {
                Config.validateProductionPassword true (Some sixteen) |> Expect.equal "Ok None" (Ok None)
            }
        ]


let startupTests =
    testList
        "validateStartup (#590)"
        [
            test "production without a password starts with one warning and the url id" {
                match Map [ "GENPRES_PROD", "1"; "GENPRES_URL_ID", "id" ] |> settingsOf |> Config.validateStartup with
                | Ok startup ->
                    startup.UrlId |> Expect.equal "url id" "id"
                    startup.Warnings |> List.length |> Expect.equal "one warning" 1
                    startup.Warnings.Head |> Expect.stringContains "the password warning" "admin operations disabled"
                | Error msg -> failtest $"expected Ok, got Error {msg}"
            }

            test "production with a valid password starts without warnings" {
                Map [ "GENPRES_PROD", "1"; "GENPRES_PASSWORD", sixteen; "GENPRES_URL_ID", "id" ]
                |> settingsOf
                |> Config.validateStartup
                |> Expect.equal "no warnings" (Ok { Config.Startup.UrlId = "id"; Config.Startup.Warnings = [] })
            }

            test "the password warning does not mask a missing url id" {
                match Map [ "GENPRES_PROD", "1" ] |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "url id" "GENPRES_URL_ID"
                | Ok _ -> failtest "expected Error"
            }

            test "a short password still refuses before the url id is checked" {
                match Map [ "GENPRES_PROD", "1"; "GENPRES_PASSWORD", fifteen ] |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "password" "GENPRES_PASSWORD"
                | Ok _ -> failtest "expected Error"
            }

            test "demo without a url id is refused, as before" {
                match Map.empty |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "url id" "GENPRES_URL_ID"
                | Ok _ -> failtest "expected Error"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "Settings.fsx" [ tests; passwordTests; startupTests ]) |> ignore
