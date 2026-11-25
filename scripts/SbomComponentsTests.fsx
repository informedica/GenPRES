// Tests for the parsing and the comparison in scripts/SbomScope.fsx.
//
// Run with: dotnet fsi scripts/SbomComponentsTests.fsx

#r "nuget: Expecto"

#load "SbomScope.fsx"

open System.Text.Json.Nodes
open Expecto
open Expecto.Flip
open SbomScope


let parse (json: string) = JsonNode.Parse json


let depsJson =
    """
{
  "libraries": {
    "Informedica.GenPRES.Server/1.0.0": { "type": "project" },
    "Newtonsoft.Json/13.0.3": { "type": "package" },
    "Microsoft.NETCore.App.Ref/10.0.0": { "type": "referencepack" }
  }
}"""

let lockJson =
    """
{
  "packages": {
    "": { "devDependencies": { "vite": "^5.0.0" } },
    "node_modules/react": { "version": "18.3.1" },
    "node_modules/vite": { "version": "5.4.0", "dev": true },
    "node_modules/vite/node_modules/esbuild": { "version": "0.21.5", "dev": true },
    "node_modules/@mui/material": { "version": "6.0.0" }
  }
}"""

let spdxJson =
    """
{
  "packages": [
    { "name": "Newtonsoft.Json", "versionInfo": "13.0.3",
      "externalRefs": [ { "referenceType": "purl", "referenceLocator": "pkg:nuget/Newtonsoft.Json@13.0.3" } ] },
    { "name": "@mui/material", "versionInfo": "6.0.0",
      "externalRefs": [ { "referenceType": "purl", "referenceLocator": "pkg:npm/%40mui/material@6.0.0" } ] },
    { "name": "Root", "versionInfo": "1.0.0" }
  ]
}"""

let paketLines =
    [
        "STORAGE: NONE"
        "NUGET"
        "  remote: https://api.nuget.org/v3/index.json"
        "    Newtonsoft.Json (13.0.3)"
        "      FSharp.Core (>= 4.3.4)"
        "GROUP Client"
        "NUGET"
        "    Fable.Core (4.0.0)"
        "GROUP Test"
        "NUGET"
        "    Expecto (10.2.0)"
        "    Newtonsoft.Json (13.0.3)"
    ]

let pkg name version = PackageId.create name version


let parseDepsTests =
    testList
        "parseDeps"
        [
            test "reads the kind and the identity of an entry" {
                parse depsJson
                |> parseDeps
                |> Expect.equal
                    "libraries"
                    [
                        { Id = pkg "Informedica.GenPRES.Server" "1.0.0"
                          Kind = LibraryKind.Project }
                        { Id = pkg "Newtonsoft.Json" "13.0.3"
                          Kind = LibraryKind.Package }
                        { Id = pkg "Microsoft.NETCore.App.Ref" "10.0.0"
                          Kind = LibraryKind.Other "referencepack" }
                    ]
            }

            test "a key without a version raises a FormatException" {
                (fun () -> parse """{ "libraries": { "NoVersion": { "type": "package" } } }""" |> parseDeps |> ignore)
                |> Expect.throwsT<System.FormatException> "key"
            }

            test "a missing libraries property raises a FormatException" {
                (fun () -> parse "{}" |> parseDeps |> ignore)
                |> Expect.throwsT<System.FormatException> "property"
            }
        ]


let parsePaketLockTests =
    testList
        "parsePaketLock"
        [
            test "puts the lines before the first group header in Main and skips dependency lines" {
                paketLines
                |> parsePaketLock
                |> List.map (fun entry -> entry.Group, entry.PackageName)
                |> Expect.equal
                    "entries"
                    [
                        "Main", "Newtonsoft.Json"
                        "Client", "Fable.Core"
                        "Test", "Expecto"
                        "Test", "Newtonsoft.Json"
                    ]
            }
        ]


let parseLockfileTests =
    testList
        "parseLockfile"
        [
            test "reads the name after the last node_modules segment and the dev mark" {
                parse lockJson
                |> parseLockfile
                |> List.map (fun entry -> entry.Package.Name, entry.IsDev)
                |> Expect.equal
                    "entries"
                    [ "react", false; "vite", true; "esbuild", true; "@mui/material", false ]
            }

            test "a key without a node_modules segment raises a FormatException" {
                let lockfile = parse """{ "packages": { "../shared": { "version": "1.0.0" } } }"""

                (fun () -> lockfile |> parseLockfile |> ignore)
                |> Expect.throwsT<System.FormatException> "segment"
            }
        ]


let removeDevEntriesTests =
    testList
        "removeDevEntries"
        [
            test "removes the dev entries and the devDependencies of the root" {
                let lockfile = parse lockJson
                let removed = removeDevEntries lockfile

                removed |> Expect.equal "removed" 2

                lockfile
                |> parseLockfile
                |> List.map (fun entry -> entry.Package.Name)
                |> Expect.equal "kept" [ "react"; "@mui/material" ]

                lockfile
                |> prop "packages"
                |> prop ""
                |> tryProp "devDependencies"
                |> Expect.isNone "devDependencies"
            }

            test "removeDevDependencies removes the devDependencies of a manifest" {
                let manifest =
                    parse """{ "name": "x", "devDependencies": { "vite": "5" }, "dependencies": { "react": "18" } }"""

                removeDevDependencies manifest

                manifest |> tryProp "devDependencies" |> Expect.isNone "devDependencies"
                manifest |> tryProp "dependencies" |> Expect.isSome "dependencies"
            }
        ]


let parseSpdxTests =
    testList
        "parseSpdx"
        [
            test "reads the ecosystem from the package URL and skips a package without one" {
                parse spdxJson
                |> parseSpdx
                |> Expect.equal
                    "packages"
                    [
                        { Ecosystem = Ecosystem.Nuget
                          Component = pkg "Newtonsoft.Json" "13.0.3" }
                        { Ecosystem = Ecosystem.Npm
                          Component = pkg "@mui/material" "6.0.0" }
                    ]
            }
        ]


let checkScopeTests =
    let shipping = set [ "Main"; "Client" ]
    let libraries = parse depsJson |> parseDeps
    let paket = parsePaketLock paketLines
    let npm = parse lockJson |> parseLockfile
    let spdx = parse spdxJson |> parseSpdx

    let nuget name version =
        { Ecosystem = Ecosystem.Nuget
          Component = pkg name version }

    let npmPackage name version =
        { Ecosystem = Ecosystem.Npm
          Component = pkg name version }

    testList
        "checkScope"
        [
            test "an SBOM of the shipped packages is clean" {
                checkScope shipping libraries paket npm spdx
                |> ScopeReport.isClean
                |> Expect.isTrue "clean"
            }

            test "a shipped package that the SBOM lacks is missing, whatever the case of its name" {
                let report = checkScope shipping libraries paket npm [ nuget "newtonsoft.JSON" "13.0.4" ]

                report.Missing |> Expect.equal "missing" (set [ pkg "newtonsoft.json" "13.0.3" ])
            }

            test "a package only in a non-shipping group is leaked, one also in a shipping group is not" {
                let report = checkScope shipping libraries paket npm (spdx @ [ nuget "Expecto" "10.2.0" ])

                report.LeakedNuget |> Expect.equal "leaked" (set [ pkg "expecto" "10.2.0" ])
            }

            test "an npm package that the lockfile marks dev only is leaked" {
                let report = checkScope shipping libraries paket npm (spdx @ [ npmPackage "vite" "5.4.0" ])

                report.LeakedNpm |> Expect.equal "leaked" (set [ pkg "vite" "5.4.0" ])
            }

            test "an npm package that is dev in one place and not dev in another is not leaked" {
                let npm = npm @ [ { Package = pkg "vite" "5.4.0"; IsDev = false } ]
                let report = checkScope shipping libraries paket npm (spdx @ [ npmPackage "vite" "5.4.0" ])

                report.LeakedNpm |> Expect.isEmpty "leaked"
            }
        ]


runTestsWithCLIArgs
    []
    [| "--summary" |]
    (testList
        "sbom scope"
        [
            parseDepsTests
            parsePaketLockTests
            parseLockfileTests
            removeDevEntriesTests
            parseSpdxTests
            checkScopeTests
        ])
|> exit
