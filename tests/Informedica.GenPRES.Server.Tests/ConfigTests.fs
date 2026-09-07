module Informedica.GenPRES.Server.Tests.ConfigTests

open Expecto
open Expecto.Flip
open Server


let loopback =
    [|
        System.Net.IPAddress.Loopback
        System.Net.IPAddress.IPv6Loopback
    |]


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
            test "demo mode accepts no password" {
                Config.validateProductionPassword false None |> Expect.equal "Ok" (Ok())
            }

            test "production without a password is refused" {
                match Config.validateProductionPassword true None with
                | Error msg -> msg |> Expect.stringContains "names the cause" "not set"
                | Ok() -> failtest "expected Error"
            }

            test "production with a blank password is refused as not set" {
                match Config.validateProductionPassword true (Some "  ") with
                | Error msg -> msg |> Expect.stringContains "names the cause" "not set"
                | Ok() -> failtest "expected Error"
            }

            test "production with 15 characters is refused" {
                match Config.validateProductionPassword true (Some fifteen) with
                | Error msg -> msg |> Expect.stringContains "names the minimum" "16"
                | Ok() -> failtest "expected Error"
            }

            test "production with 16 characters is accepted" {
                Config.validateProductionPassword true (Some sixteen)
                |> Expect.equal "Ok" (Ok())
            }
        ]


let validateStartupTests =
    let settings (m: Map<string, string>) =
        Config.fromEnv (fun key -> m |> Map.tryFind key)

    let sixteen = String.replicate 16 "x"

    testList
        "validateStartup"
        [
            test "demo with a url id starts" {
                Map [ "GENPRES_URL_ID", "sheet-id" ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok "sheet-id")
            }

            test "demo without a url id is refused" {
                match Map.empty |> settings |> Config.validateStartup with
                | Error msg -> msg |> Expect.stringContains "names the setting" "GENPRES_URL_ID"
                | Ok _ -> failtest "expected Error"
            }

            test "production without a password is refused before the url id is checked" {
                match
                    Map [ "GENPRES_PROD", "1"; "GENPRES_URL_ID", "sheet-id" ]
                    |> settings
                    |> Config.validateStartup
                with
                | Error msg -> msg |> Expect.stringContains "names the cause" "not set"
                | Ok _ -> failtest "expected Error"
            }

            test "production with a valid password and a url id starts" {
                Map
                    [
                        "GENPRES_PROD", "1"
                        "GENPRES_PASSWORD", sixteen
                        "GENPRES_URL_ID", "sheet-id"
                    ]
                |> settings
                |> Config.validateStartup
                |> Expect.equal "Ok with the url id" (Ok "sheet-id")
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
            }

            test "blank secrets are treated as unset" {
                let env = Map [ "GENPRES_URL_ID", " "; "GENPRES_PASSWORD", "" ]
                let s = Config.fromEnv (getEnv env)
                s.UrlId |> Expect.isNone "blank url id"
                s.Password |> Expect.isNone "blank password"
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
        ]
