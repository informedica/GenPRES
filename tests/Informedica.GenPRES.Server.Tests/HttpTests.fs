module Informedica.GenPRES.Server.Tests.HttpTests

open System
open Expecto
open Expecto.Flip
open Microsoft.AspNetCore.Http


let immutable = "public, max-age=31536000, immutable"


/// Status code, request path, expected Cache-Control (#568).
let cacheControlCases =
    [
        200, "/", "no-cache"
        200, "/index.html", "no-cache"
        200, "/genpres.png", "no-cache"
        200, "/api/IServerApi/processCommand", "no-cache"
        200, "/assets/index-abc123.js", immutable
        200, "/assets/index-abc123.css", immutable
        200, "/ASSETS/x.js", immutable
        206, "/assets/index-abc123.js", immutable
        // an asset this instance does not have must not be cached for a year
        404, "/assets/index-abc123.js", "no-cache"
        500, "/assets/index-abc123.js", "no-cache"
        304, "/assets/index-abc123.js", "no-cache"
        // Request.Path.Value is null for a request without a path (OPTIONS *)
        200, null, "no-cache"
        200, "", "no-cache"
    ]


let cacheControlTests =
    testList
        "cacheControlFor"
        [
            for status, path, expected in cacheControlCases do
                test $"%i{status} %A{path} -> %s{expected}" {
                    path
                    |> Server.Http.cacheControlFor status
                    |> Expect.equal $"%i{status} %A{path} should get {expected}" expected
                }
        ]


let setCookieHeader (ctx: HttpContext) =
    ctx.Response.Headers.SetCookie.ToString()


/// The session cookie adapter over a bare HttpContext (uc-01 step 6, Rule 12).
let sessionCookieTests =
    testList
        "sessionCookie"
        [
            test "write sets HttpOnly, SameSite=Strict, Path=/ and no Secure over http" {
                let ctx = DefaultHttpContext()
                (Server.Http.sessionCookie ctx).write "abc"
                let header = setCookieHeader ctx

                header |> Expect.stringStarts "name=value" "genpres_session=abc"
                header |> Expect.stringContains "httponly" "httponly"
                header |> Expect.stringContains "strict" "samesite=strict"
                header |> Expect.stringContains "path" "path=/"

                header.Contains("secure", StringComparison.OrdinalIgnoreCase)
                |> Expect.isFalse "not secure over http"
            }

            test "write sets Secure over https" {
                let ctx = DefaultHttpContext()
                ctx.Request.IsHttps <- true
                (Server.Http.sessionCookie ctx).write "abc"

                setCookieHeader ctx |> Expect.stringContains "secure" "secure"
            }

            test "read returns the cookie value, None when absent or blank" {
                let withCookie (value: string) =
                    let ctx = DefaultHttpContext()
                    ctx.Request.Headers.Cookie <- Microsoft.Extensions.Primitives.StringValues value
                    (Server.Http.sessionCookie ctx).read ()

                withCookie "genpres_session=abc; other=1" |> Expect.equal "present" (Some "abc")
                withCookie "other=1" |> Expect.isNone "absent"
                withCookie "genpres_session=" |> Expect.isNone "blank"

                (Server.Http.sessionCookie (DefaultHttpContext())).read ()
                |> Expect.isNone "no header"
            }

            test "delete expires the cookie on the same path" {
                let ctx = DefaultHttpContext()
                (Server.Http.sessionCookie ctx).delete ()
                let header = setCookieHeader ctx

                header |> Expect.stringStarts "name emptied" "genpres_session=;"
                header |> Expect.stringContains "expired" "expires="
                header |> Expect.stringContains "path" "path=/"
            }
        ]


let launchStateCookieTests =
    let state = "AbC-_123"

    testList
        "launchStateCookie"
        [
            test
                "write names the cookie by its state, HttpOnly, SameSite=Lax, Path=/callback, one Launch lifetime, no Secure over http" {
                let ctx = DefaultHttpContext()
                (Server.Http.launchStateCookie ctx).write state
                let header = setCookieHeader ctx

                header
                |> Expect.stringStarts "name=value" $"genpres_launch_state.{state}={state}"

                header |> Expect.stringContains "httponly" "httponly"
                header |> Expect.stringContains "lax" "samesite=lax"
                header |> Expect.stringContains "path" "path=/callback"
                header |> Expect.stringContains "lifetime" "max-age=120"

                header.Contains("secure", StringComparison.OrdinalIgnoreCase)
                |> Expect.isFalse "not secure over http"
            }

            test "write sets Secure over https" {
                let ctx = DefaultHttpContext()
                ctx.Request.IsHttps <- true
                (Server.Http.launchStateCookie ctx).write state

                setCookieHeader ctx |> Expect.stringContains "secure" "secure"
            }

            test "read answers the cookie of the asked state only" {
                let withCookie (value: string) (asked: string) =
                    let ctx = DefaultHttpContext()
                    ctx.Request.Headers.Cookie <- Microsoft.Extensions.Primitives.StringValues value
                    (Server.Http.launchStateCookie ctx).read asked

                withCookie $"genpres_launch_state.{state}={state}; other=1" state
                |> Expect.equal "own state" (Some state)

                withCookie $"genpres_launch_state.{state}={state}" "other-state"
                |> Expect.isNone "another tab's state"

                withCookie $"genpres_launch_state.{state}=" state |> Expect.isNone "blank"

                (Server.Http.launchStateCookie (DefaultHttpContext())).read state
                |> Expect.isNone "no header"
            }

            test "the stub identity cookie is HttpOnly, SameSite=Lax, Path=/, one Launch lifetime" {
                let ctx = DefaultHttpContext()

                ctx.Response.Cookies.Append(
                    "genpres_stub_identity",
                    "prescriber.p",
                    Server.Http.stubIdentityCookieOptions false
                )

                let header = setCookieHeader ctx

                header |> Expect.stringContains "httponly" "httponly"
                header |> Expect.stringContains "lax" "samesite=lax"
                header |> Expect.stringContains "path" "path=/"
                header |> Expect.stringContains "lifetime" "max-age=120"
            }
        ]


/// The enrolment cookie adapter (UC-2): the attempt a suspended launch left in the browser.
let enrolmentCookieTests =
    testList
        "enrolmentCookie"
        [
            test "write is HttpOnly, SameSite=Strict, Path=/, Max-Age what remains of the code, no Secure over http" {
                let ctx = DefaultHttpContext()
                let now = DateTime.UtcNow
                (Server.Http.enrolmentCookie ctx).write "attempt-1" (now + TimeSpan.FromMinutes 15.0)
                let header = setCookieHeader ctx

                header |> Expect.stringStarts "name=value" "genpres_enrolment=attempt-1"
                header |> Expect.stringContains "httponly" "httponly"
                header |> Expect.stringContains "strict" "samesite=strict"
                header |> Expect.stringContains "path" "path=/"

                let maxAge =
                    header.Split(';')
                    |> Array.map _.Trim()
                    |> Array.pick (fun part ->
                        if part.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase) then
                            Some(int (part.Substring 8))
                        else
                            None
                    )

                (maxAge > 14 * 60 && maxAge <= 15 * 60)
                |> Expect.isTrue $"about 15 minutes, was {maxAge}"

                header.Contains("secure", StringComparison.OrdinalIgnoreCase)
                |> Expect.isFalse "not secure over http"
            }

            test "a code already expired writes a Max-Age of zero, never a negative one" {
                let ctx = DefaultHttpContext()
                (Server.Http.enrolmentCookie ctx).write "attempt-1" (DateTime.UtcNow - TimeSpan.FromMinutes 1.0)
                setCookieHeader ctx |> Expect.stringContains "zero" "max-age=0"
            }

            test "write sets Secure over https; read and delete work on the named cookie" {
                let ctx = DefaultHttpContext()
                ctx.Request.IsHttps <- true
                (Server.Http.enrolmentCookie ctx).write "attempt-1" (DateTime.UtcNow + TimeSpan.FromMinutes 15.0)
                setCookieHeader ctx |> Expect.stringContains "secure" "secure"

                let ctx = DefaultHttpContext()

                ctx.Request.Headers.Cookie <-
                    Microsoft.Extensions.Primitives.StringValues "genpres_enrolment=a-1; other=1"

                (Server.Http.enrolmentCookie ctx).read () |> Expect.equal "read" (Some "a-1")
                (Server.Http.enrolmentCookie ctx).delete ()

                setCookieHeader ctx
                |> Expect.stringContains "deleted" "genpres_enrolment=; expires="
            }
        ]


[<Tests>]
let tests =
    testList
        "Http Tests"
        [
            cacheControlTests
            sessionCookieTests
            launchStateCookieTests
            enrolmentCookieTests
        ]
