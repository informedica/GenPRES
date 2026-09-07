module Informedica.GenPRES.Server.Tests.HttpTests

open Expecto
open Expecto.Flip


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


[<Tests>]
let tests = testList "Http Tests" [ cacheControlTests ]
