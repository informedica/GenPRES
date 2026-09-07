module Informedica.GenPRES.Server.Tests.HttpTests

open Expecto
open Expecto.Flip


let immutable = "public, max-age=31536000, immutable"


/// Request path, expected Cache-Control (#568).
let cacheControlCases =
    [
        "/", "no-cache"
        "/index.html", "no-cache"
        "/genpres.png", "no-cache"
        "/api/IServerApi/processCommand", "no-cache"
        "/assets/index-abc123.js", immutable
        "/assets/index-abc123.css", immutable
        "/ASSETS/x.js", immutable
    ]


let cacheControlTests =
    testList
        "cacheControlFor"
        [
            for path, expected in cacheControlCases do
                test $"%s{path} -> %s{expected}" {
                    path
                    |> Server.cacheControlFor
                    |> Expect.equal $"%s{path} should get {expected}" expected
                }
        ]


[<Tests>]
let tests = testList "Http Tests" [ cacheControlTests ]
