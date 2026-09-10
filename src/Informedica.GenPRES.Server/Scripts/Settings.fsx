// Server default language (GENPRES_LANG) and the settings the client learns from the server.
//
// Script-first draft (script-only policy) of:
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


    let validateProductionPassword (isProd: bool) (password: string option) : Result<unit, string> =
        if not isProd then
            Ok()
        else
            match password |> nonBlank with
            | None -> Error "GENPRES_PROD=1 but GENPRES_PASSWORD is not set (or is empty)."
            | Some pwd when pwd.Length < minProductionPasswordLength ->
                Error $"GENPRES_PROD=1 but GENPRES_PASSWORD is shorter than %i{minProductionPasswordLength} characters."
            | Some _ -> Ok()


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
    let validateStartup (settings: Settings) : Result<string, string> =
        validateProductionPassword settings.IsProd settings.Password
        |> Result.bind (fun () -> language settings |> Result.map ignore)
        |> Result.bind (fun () ->
            match settings.UrlId with
            | Some urlId -> Ok urlId
            | None -> Error "No GENPRES_URL_ID (or value is empty)"
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
                match Map [ "GENPRES_PROD", "1"; "GENPRES_LANG", "klingon" ] |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "password first" "GENPRES_PASSWORD"
                | Ok _ -> failtest "expected Error"

                match Map [ "GENPRES_LANG", "klingon" ] |> settingsOf |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "language before url id" "GENPRES_LANG"
                | Ok _ -> failtest "expected Error"
            }

            test "a valid language still starts with the url id" {
                Map [ "GENPRES_LANG", "en"; "GENPRES_URL_ID", "id" ]
                |> settingsOf
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok "id")
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


runTestsWithCLIArgs [] [||] tests |> ignore
