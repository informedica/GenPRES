module Informedica.GenPRES.Server.Tests.ConfigTests

open Expecto
open Expecto.Flip
open Server


let loopback = [| System.Net.IPAddress.Loopback; System.Net.IPAddress.IPv6Loopback |]


let trustedProxiesTests =
    testList
        "parseTrustedProxies"
        [
            test "unset falls back to the loopback pair" {
                None |> Config.parseTrustedProxies |> Expect.equal "loopback pair" loopback
            }

            test "whitespace only falls back to the loopback pair" {
                Some "   "
                |> Config.parseTrustedProxies
                |> Expect.equal "loopback pair" loopback
            }

            test "keeps the parseable entries and drops the rest" {
                let exp =
                    [|
                        System.Net.IPAddress.Parse "10.0.0.5"
                        System.Net.IPAddress.Parse "10.0.0.6"
                    |]

                Some "10.0.0.5, bad, 10.0.0.6"
                |> Config.parseTrustedProxies
                |> Expect.equal "two addresses" exp
            }
        ]


let passwordTests =
    let sixteen = String.replicate 16 "x"
    let fifteen = String.replicate 15 "x"

    testList
        "validateProductionPassword"
        [
            test "demo mode accepts no password, nothing to say" {
                Config.validateProductionPassword false None |> Expect.equal "Ok None" (Ok None)
            }

            test "production without a password starts with a warning naming the setting and what is disabled" {
                match Config.validateProductionPassword true None with
                | Ok(Some warning) ->
                    warning |> Expect.stringContains "names the setting" "GENPRES_PASSWORD"
                    warning |> Expect.stringContains "names the cause" "not set"
                    warning |> Expect.stringContains "names the effect" "admin operations disabled"
                | other -> failtest $"expected Ok (Some warning), got {other}"
            }

            test "production with a blank password is the same as none" {
                Config.validateProductionPassword true (Some "  ")
                |> Expect.equal "same warning" (Config.validateProductionPassword true None)
            }

            test "production with 15 characters is refused" {
                match Config.validateProductionPassword true (Some fifteen) with
                | Error msg -> msg |> Expect.stringContains "names the minimum" "16"
                | other -> failtest $"expected Error, got {other}"
            }

            test "production with 16 characters is accepted, nothing to say" {
                Config.validateProductionPassword true (Some sixteen)
                |> Expect.equal "Ok None" (Ok None)
            }
        ]


/// A start-up with nothing to warn about.
let startup urlId : Config.Startup =
    {
        UrlId = urlId
        Warnings = []
    }


let validateStartupTests =
    let settings (m: Map<string, string>) = Config.fromEnv (fun key -> m |> Map.tryFind key)

    let sixteen = String.replicate 16 "x"

    testList
        "validateStartup"
        [
            test "demo with a url id starts without warnings" {
                Map [ "GENPRES_URL_ID", "sheet-id" ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok(startup "sheet-id"))
            }

            test "demo without a url id is refused" {
                match Map.empty |> settings |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_URL_ID"
                | Ok _ -> failtest "expected Error"
            }

            test "production without a password starts with one warning and the url id" {
                match
                    Map [ "GENPRES_PROD", "1"; "GENPRES_URL_ID", "sheet-id" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Ok s ->
                    s.UrlId |> Expect.equal "url id" "sheet-id"
                    s.Warnings |> List.length |> Expect.equal "one warning" 1

                    s.Warnings.Head
                    |> Expect.stringContains "the password warning" "admin operations disabled"
                | Error msg -> failtest $"expected Ok, got Error {msg}"
            }

            test "the password warning does not mask a missing url id" {
                match Map [ "GENPRES_PROD", "1" ] |> settings |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_URL_ID"
                | Ok _ -> failtest "expected Error"
            }

            test "production with a short password is refused before the url id is checked" {
                match
                    Map [ "GENPRES_PROD", "1"; "GENPRES_PASSWORD", "short" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_PASSWORD"
                | Ok _ -> failtest "expected Error"
            }

            test "demo with the session store set starts without warnings" {
                Map
                    [
                        "GENPRES_URL_ID", "sheet-id"
                        "GENPRES_DB_CONNECTION", "Data Source=data/db/genpres.db"
                    ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok(startup "sheet-id"))
            }

            test "production with the session store set is refused, naming the setting" {
                match
                    Map
                        [
                            "GENPRES_PROD", "1"
                            "GENPRES_PASSWORD", sixteen
                            "GENPRES_URL_ID", "sheet-id"
                            "GENPRES_DB_CONNECTION", "Data Source=data/db/genpres.db"
                        ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_DB_CONNECTION"
                | Ok _ -> failtest "expected Error"
            }

            test "production with a valid password and a url id starts without warnings" {
                Map
                    [
                        "GENPRES_PROD", "1"
                        "GENPRES_PASSWORD", sixteen
                        "GENPRES_URL_ID", "sheet-id"
                    ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok(startup "sheet-id"))
            }
        ]


let redactionTests =
    testList
        "banner redaction"
        [
            test "urlId unset" { None |> Config.redactUrlId |> Expect.equal "NOT SET" "NOT SET" }

            test "urlId of five characters or fewer is fully redacted" {
                Some "abcde" |> Config.redactUrlId |> Expect.equal "redacted" "***<redacted>"
            }

            test "urlId keeps only the last five characters" {
                Some "1234567890" |> Config.redactUrlId |> Expect.equal "last five" "***67890"
            }

            test "password unset" {
                None
                |> Config.displayPassword
                |> Expect.equal "not set" "NOT SET (admin operations disabled)"
            }

            test "password set is never shown" { Some "secret" |> Config.displayPassword |> Expect.equal "stars" "***" }
        ]


let fromEnvTests =
    let getEnv (m: Map<string, string>) key = m |> Map.tryFind key

    testList
        "fromEnv"
        [
            test "defaults when nothing is set" {
                let s = Config.fromEnv (getEnv Map.empty)
                s.Port |> Expect.equal "port" 8085us
                s.IsProd |> Expect.isFalse "not prod"
                s.UrlId |> Expect.isNone "no url id"
                s.Password |> Expect.isNone "no password"
                s.TrustedProxies |> Expect.equal "loopback" loopback
                s.Log |> Expect.equal "log" "0"
                s.Lang |> Expect.isNone "no language"
                s.DbConnection |> Expect.isNone "no session store"
            }

            test "reads every setting" {
                let env =
                    Map
                        [
                            "SERVER_PORT", "9000"
                            "GENPRES_PROD", "1"
                            "GENPRES_URL_ID", "sheet-id"
                            "GENPRES_PASSWORD", "pw"
                            "GENPRES_TRUSTED_PROXIES", "10.0.0.5"
                            "GENPRES_LOG", "d"
                            "GENPRES_DEBUG", "1"
                            "GENPRES_LANG", "en"
                            "GENPRES_DB_CONNECTION", "Data Source=data/db/genpres.db"
                        ]

                let s = Config.fromEnv (getEnv env)
                s.Port |> Expect.equal "port" 9000us
                s.IsProd |> Expect.isTrue "prod"
                s.UrlId |> Expect.equal "url id" (Some "sheet-id")
                s.Password |> Expect.equal "password" (Some "pw")

                s.TrustedProxies
                |> Expect.equal "proxy" [| System.Net.IPAddress.Parse "10.0.0.5" |]

                s.Log |> Expect.equal "log" "d"
                s.Debug |> Expect.equal "debug" "1"
                s.Lang |> Expect.equal "language" (Some "en")

                s.DbConnection
                |> Expect.equal "session store" (Some "Data Source=data/db/genpres.db")
            }

            test "blank secrets are treated as unset" {
                let env = Map [ "GENPRES_URL_ID", " "; "GENPRES_PASSWORD", "" ]
                let s = Config.fromEnv (getEnv env)
                s.UrlId |> Expect.isNone "blank url id"
                s.Password |> Expect.isNone "blank password"
            }

            test "a blank session store is unset" {
                let s = Config.fromEnv (getEnv (Map [ "GENPRES_DB_CONNECTION", " " ]))
                s.DbConnection |> Expect.isNone "blank connection string"
            }

            test "a blank language is unset" {
                let s = Config.fromEnv (getEnv (Map [ "GENPRES_LANG", "  " ]))
                s.Lang |> Expect.isNone "blank language"
            }
        ]


let languageTests =
    let settings (m: Map<string, string>) = Config.fromEnv (fun key -> m |> Map.tryFind key)

    testList
        "GENPRES_LANG"
        [
            test "unset is Dutch, the client's default until now" {
                Map.empty
                |> settings
                |> Config.language
                |> Expect.equal "default" (Ok Informedica.GenPRES.Shared.Localization.Dutch)
            }

            testList
                "accepted spellings: an ISO 639-1 code, any case"
                [
                    for raw, lang in
                        [
                            "en", Informedica.GenPRES.Shared.Localization.English
                            "NL", Informedica.GenPRES.Shared.Localization.Dutch
                            " fr ", Informedica.GenPRES.Shared.Localization.French
                            "de", Informedica.GenPRES.Shared.Localization.German
                            "es", Informedica.GenPRES.Shared.Localization.Spanish
                            "it", Informedica.GenPRES.Shared.Localization.Italian
                        ] do
                        test $"'{raw}'" {
                            Map [ "GENPRES_LANG", raw ]
                            |> settings
                            |> Config.language
                            |> Expect.equal raw (Ok lang)
                        }
                ]

            test "a value that is not a language refuses the start, naming the setting and the value" {
                match
                    Map [ "GENPRES_LANG", "klingon"; "GENPRES_URL_ID", "sheet-id" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg ->
                    msg |> Expect.stringContains "setting" "GENPRES_LANG"
                    msg |> Expect.stringContains "value" "klingon"
                    msg |> Expect.stringContains "accepted values" "nl"
                | Ok _ -> failtest "expected Error"
            }

            test "the language is checked after the password and before the url id" {
                match
                    Map [ "GENPRES_PROD", "1"; "GENPRES_PASSWORD", "short"; "GENPRES_LANG", "klingon" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "password first" "GENPRES_PASSWORD"
                | Ok _ -> failtest "expected Error"

                // a missing password is a warning, so it does not mask the language error
                match
                    Map [ "GENPRES_PROD", "1"; "GENPRES_LANG", "klingon" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "language after the warning" "GENPRES_LANG"
                | Ok _ -> failtest "expected Error"

                match Map [ "GENPRES_LANG", "klingon" ] |> settings |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "language before the url id" "GENPRES_LANG"
                | Ok _ -> failtest "expected Error"
            }

            test "a valid language starts with the url id" {
                Map [ "GENPRES_LANG", "en"; "GENPRES_URL_ID", "sheet-id" ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok(startup "sheet-id"))
            }

            test "toServerSettings carries the language, the demo flag and the departments" {
                Map [ "GENPRES_LANG", "en" ] |> settings |> Config.toServerSettings
                <| Informedica.GenForm.Lib.Resources.Departments.ofNamed []
                |> Expect.equal
                    "demo, English"
                    {
                        Informedica.GenPRES.Shared.Api.ServerSettings.Language =
                            Informedica.GenPRES.Shared.Localization.English
                        IsDemo = true
                        Departments = [| "ICK" |]
                        DefaultDepartment = "ICK"
                    }

                Map [ "GENPRES_PROD", "1" ] |> settings |> Config.toServerSettings
                <| Informedica.GenForm.Lib.Resources.Departments.ofNamed []
                |> Expect.equal
                    "production, Dutch"
                    {
                        Informedica.GenPRES.Shared.Api.ServerSettings.Language =
                            Informedica.GenPRES.Shared.Localization.Dutch
                        IsDemo = false
                        Departments = [| "ICK" |]
                        DefaultDepartment = "ICK"
                    }
            }

            test "the banner shows the derived language, or flags the raw value" {
                Map [ "GENPRES_LANG", "EN" ]
                |> settings
                |> Config.displayLanguage
                |> Expect.equal "derived" "en (English)"

                Map [ "GENPRES_LANG", "klingon" ]
                |> settings
                |> Config.displayLanguage
                |> Expect.equal "flagged" "klingon (NOT A LANGUAGE)"
            }
        ]


let sessionIdleTests =
    let settings (m: Map<string, string>) = Config.fromEnv (fun key -> m |> Map.tryFind key)

    testList
        "GENPRES_SESSION_IDLE_MINUTES"
        [
            for raw, expected in
                [
                    None, Ok(System.TimeSpan.FromHours 1.0)
                    Some "", Ok(System.TimeSpan.FromHours 1.0)
                    Some "   ", Ok(System.TimeSpan.FromHours 1.0)
                    Some "30", Ok(System.TimeSpan.FromMinutes 30.0)
                    Some " 90 ", Ok(System.TimeSpan.FromMinutes 90.0)
                ] do
                test $"%A{raw} is %A{expected}" {
                    raw |> Config.parseSessionIdle |> Expect.equal "the lifetime" expected
                }

            for raw in [ "0"; "-5"; "1.5"; "an hour"; "60m" ] do
                test $"'%s{raw}' is refused" {
                    Some raw
                    |> Config.parseSessionIdle
                    |> Expect.isError "not a positive whole number of minutes"
                }

            test "fromEnv reads it, blank as unset" {
                (Map [ "GENPRES_SESSION_IDLE_MINUTES", "45" ] |> settings).SessionIdle
                |> Expect.equal "raw" (Some "45")

                (Map [ "GENPRES_SESSION_IDLE_MINUTES", " " ] |> settings).SessionIdle
                |> Expect.isNone "blank"
            }

            test "a lifetime that does not parse refuses the start, naming the setting" {
                match
                    Map [ "GENPRES_URL_ID", "sheet-id"; "GENPRES_SESSION_IDLE_MINUTES", "an hour" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_SESSION_IDLE_MINUTES"
                | Ok _ -> failtest "expected a refused start"
            }
        ]


let productionDebugTests =
    let settings (m: Map<string, string>) = Config.fromEnv (fun key -> m |> Map.tryFind key)

    let sixteen = String.replicate 16 "x"

    testList
        "validateProductionDebug"
        [
            testList
                "demo accepts every level and debug mode"
                [
                    for log in [ "0"; "d"; "i"; "w"; "e" ] do
                        for debug in [ "0"; "1" ] do
                            test $"GENPRES_LOG=%s{log}, GENPRES_DEBUG=%s{debug}" {
                                Config.validateProductionDebug false log debug |> Expect.equal "Ok" (Ok())
                            }
                ]

            testList
                "production accepts the levels that keep the calculation record"
                [
                    for log in [ "0"; "i"; "w"; "e" ] do
                        test $"GENPRES_LOG=%s{log}" {
                            Config.validateProductionDebug true log "0" |> Expect.equal "Ok" (Ok())
                        }
                ]

            testList
                "production refuses debug logging, whatever the case or the spaces"
                [
                    for log in [ "d"; "D"; " d " ] do
                        test $"GENPRES_LOG='%s{log}'" {
                            match Config.validateProductionDebug true log "0" with
                            | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_LOG=d"
                            | Ok() -> failtest "expected Error"
                        }
                ]

            test "production refuses debug mode" {
                match Config.validateProductionDebug true "i" "1" with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_DEBUG=1"
                | Ok() -> failtest "expected Error"
            }

            test "the start-up refuses production with debug logging, before anything else is checked" {
                match
                    Map
                        [
                            "GENPRES_PROD", "1"
                            "GENPRES_URL_ID", "sheet-id"
                            "GENPRES_PASSWORD", sixteen
                            "GENPRES_LOG", "d"
                        ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_LOG=d"
                | Ok _ -> failtest "expected Error"
            }

            test "the start-up accepts production with info logging and debug mode off" {
                Map
                    [
                        "GENPRES_PROD", "1"
                        "GENPRES_URL_ID", "sheet-id"
                        "GENPRES_PASSWORD", sixteen
                        "GENPRES_LOG", "i"
                        "GENPRES_DEBUG", "0"
                    ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok(startup "sheet-id"))
            }
        ]


[<Tests>]
let tests =
    testList
        "Config Tests"
        [
            trustedProxiesTests
            passwordTests
            validateStartupTests
            redactionTests
            fromEnvTests
            languageTests
            sessionIdleTests
            productionDebugTests
        ]
