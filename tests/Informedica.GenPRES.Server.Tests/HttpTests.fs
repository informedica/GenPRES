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


[<Tests>]
let tests = testList "Http Tests" [ cacheControlTests; sessionCookieTests ]
