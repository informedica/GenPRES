// Tests for release-tooling parsing in scripts/Changelog.fsx and Versioning.isPreRelease.

#r "nuget: Expecto"

#load "Versioning.fsx"
#load "Changelog.fsx"

open System.IO
open Expecto
open Expecto.Flip


let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))


let headingTests =
    testList
        "headingVersion"
        [
            test "reads a ShipIt heading" {
                "## 0.1.2-alpha.4 - 2026-08-17"
                |> Changelog.headingVersion
                |> Expect.equal "should name the version" (Some "0.1.2-alpha.4")
            }

            test "strips the brackets of a hand-written heading" {
                "## [0.1.2-alpha.1] - 2026-03-23"
                |> Changelog.headingVersion
                |> Expect.equal "should name the version" (Some "0.1.2-alpha.1")
            }

            test "tolerates extra whitespace after the hashes" {
                "##   0.1.3  -  2026-09-01"
                |> Changelog.headingVersion
                |> Expect.equal "should name the version" (Some "0.1.3")
            }

            test "a bare H2 names no version" {
                "## "
                |> Changelog.headingVersion
                |> Expect.isNone "should be None"
            }

            test "an H3 inside a section body is not a heading" {
                "### 🐞 Bug Fixes"
                |> Changelog.headingVersion
                |> Expect.isNone "should be None"
            }

            test "a non-version H2 still counts as a heading" {
                "## About This Changelog"
                |> Changelog.headingVersion
                |> Expect.equal "should name its first token" (Some "About")
            }
        ]


let sectionTests =
    let changelog =
        [|
            "## 0.2.0 - 2026-09-01"
            ""
            "### 🐞 Bug Fixes"
            ""
            "* fixed a thing"
            ""
            "## 0.1.0 - 2026-08-01"
            ""
            "* first release"
            ""
            "## About This Changelog"
            ""
            "prose"
        |]

    testList
        "sectionFor"
        [
            test "returns the body between two version headings" {
                changelog
                |> Changelog.sectionFor "0.2.0"
                |> Expect.equal "should be trimmed of blank lines" (Ok "### 🐞 Bug Fixes\n\n* fixed a thing")
            }

            test "stops at a non-version heading" {
                changelog
                |> Changelog.sectionFor "0.1.0"
                |> Expect.equal "should not swallow the trailing prose" (Ok "* first release")
            }

            test "reports a missing version" {
                changelog
                |> Changelog.sectionFor "9.9.9"
                |> Expect.equal "should be Missing" (Error Changelog.SectionError.Missing)
            }

            test "reports an empty section rather than publishing nothing" {
                [| "## 0.3.0 - 2026-10-01"; ""; ""; "## 0.2.0 - 2026-09-01"; "* x" |]
                |> Changelog.sectionFor "0.3.0"
                |> Expect.equal "should be Empty" (Error Changelog.SectionError.Empty)
            }

            test "reports duplicate headings rather than picking one" {
                [| "## 0.2.0 - 2026-09-01"; "* a"; "## 0.2.0 - 2026-09-02"; "* b" |]
                |> Changelog.sectionFor "0.2.0"
                |> Expect.equal "should be Duplicate 2" (Error(Changelog.SectionError.Duplicate 2))
            }
        ]


let preReleaseTests =
    testList
        "isPreRelease"
        [
            for version, expected in
                [
                    "0.1.2-alpha.4", true
                    "1.0.0-rc.1", true
                    "1.0.0-rc.1+build.5", true
                    "0.1.3", false
                    // Build metadata may contain hyphens of its own; this is a stable release
                    "1.0.0+build-3", false
                ] do
                test $"%s{version} is pre-release: %b{expected}" {
                    version
                    |> Versioning.isPreRelease
                    |> Expect.equal $"should be %b{expected}" expected
                }
        ]


// Guards against the shipped CHANGELOG.md drifting from the grammar above, e.g. if ShipIt
// changes its heading format.
let currentReleaseTests =
    testList
        "the shipped changelog"
        [
            test "has a section for the version in Directory.Build.props" {
                let version =
                    Path.Combine(repoRoot, "Directory.Build.props")
                    |> Versioning.readVersion

                Path.Combine(repoRoot, "CHANGELOG.md")
                |> File.ReadAllLines
                |> Changelog.sectionFor version
                |> Result.isOk
                |> Expect.isTrue $"should find a section for %s{version}"
            }
        ]


runTestsWithCLIArgs
    []
    [| "--summary" |]
    (testList
        "release tooling"
        [
            headingTests
            sectionTests
            preReleaseTests
            currentReleaseTests
        ])
|> exit
