// Check Packaging
//
// Fitness test for what may enter the Docker build context and the published output.
//
// Both halves rest on the same rule, and all three bugs below came from missing it:
// the LAST matching pattern wins. An exclusion written above a re-include does nothing,
// so these files are correct only as long as their line order is. That is precisely what
// a later tidy-up undoes without anyone noticing.
//
//   - .dockerignore, data/: `!/data/cache/` re-includes the whole directory, so the
//     `*.demo` line under it narrows nothing. Without the trailing `/data/cache/*.cache`
//     exclusion the proprietary, licence-restricted cache files are baked into the image
//     by any local build on a machine that holds them.
//   - .dockerignore, src/: `!/src/` carries the whole tree, so every project's bin/ and
//     obj/, the client's node_modules and the Fable output/ enter the context unless the
//     trailing exclusions keep them out.
//   - The server project: Microsoft.NET.Sdk.Web publishes every **/*.json and **/*.config
//     found in the project folder. With EnableDefaultContentItems off there is nothing to
//     publish, and no stray file can ship inside the image.
//
// What this asserts is the contract in the two files, not Docker's own behaviour.
// Reimplementing Docker's filepath.Match semantics here would reimplement the very thing
// that was misread three times, and a copy that shared the misreading would happily agree
// with a broken file. The semantics are fixed; the files are what drift.
//
// Needs no Docker, so it runs identically on every CI leg.
//
// Run with: dotnet fsi scripts/CheckPackaging.fsx
// Prototype per the script-only policy in AGENTS.md; no build is required.

#r "nuget: Expecto"

open System
open System.IO
open System.Diagnostics
open System.Text.Json
open Expecto
open Expecto.Flip


let repoRoot =
    Path.Combine(__SOURCE_DIRECTORY__, "..") |> Path.GetFullPath


let serverProject =
    Path.Combine(repoRoot, "src", "Informedica.GenPRES.Server", "Informedica.GenPRES.Server.fsproj")


/// A meaningful line of an ignore file: its 1-based number and its text, with blank
/// lines and comments dropped.
type Line =
    {
        /// The 1-based line number, as an editor shows it.
        Number: int
        /// The trimmed text of the line.
        Text: string
    }


/// The leading `*` that opens an opt-in ignore file. It is the baseline every other
/// line works against, not an exclusion in the sense this test cares about.
[<Literal>]
let OptInBaseline = "*"


let meaningfulLines path =
    File.ReadAllLines(path: string)
    |> Array.mapi (fun i text ->
        {
            Number = i + 1
            Text = text.Trim()
        }
    )
    |> Array.filter (fun l -> l.Text <> "" && not (l.Text.StartsWith "#"))
    |> Array.toList


let dockerIgnoreLines () =
    Path.Combine(repoRoot, ".dockerignore") |> meaningfulLines


/// The patterns that must be present and must stay below every re-include.
/// Each says what it keeps out and which change put it there.
let requiredExclusions =
    [
        // The proprietary cache. The single most important line in the file: without it
        // a local build ships licence-restricted data inside the image.
        "/data/cache/*.cache"
        "/data/data/"
        "/data/db/"
        "/data/localization/"
        "/data/logs/"
        "/data/sources/"
        "/data/zindex/"
        // Build output and local trees, none of which the image uses: npm ci and
        // dotnet publish regenerate them inside the build stage.
        "/src/**/bin/"
        "/src/**/obj/"
        "/src/**/node_modules/"
        "/src/**/output/"
        "/src/**/Scratch/"
        "/src/**/data_/"
    ]


/// Content items MSBuild resolves for the server project. Empty, and meant to stay that
/// way: the project has no appsettings and no wwwroot, so the Web SDK's default glob only
/// ever matched files that happened to sit in the tree. If this project ever gains a file
/// it genuinely needs at run time, declare it in the .fsproj and add it here, deliberately.
let allowedContentItems: string list = []


let runDotnet args =
    let psi =
        ProcessStartInfo(
            "dotnet",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        )

    args |> List.iter psi.ArgumentList.Add

    use proc = Process.Start psi
    let out = proc.StandardOutput.ReadToEnd()
    let err = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failtestf "`dotnet %s` failed with exit code %i:\n%s\n%s" (String.Join(" ", args)) proc.ExitCode out err

    out


/// The Identity of every Content item MSBuild resolves for a project.
let contentItems project =
    let out =
        runDotnet
            [
                "msbuild"
                project
                "-t:Restore"
                "-getItem:Content"
                "-p:Configuration=Release"
            ]

    // -getItem prints a JSON document; anything before the first brace is MSBuild noise.
    let json = out.Substring(out.IndexOf '{')
    use doc = JsonDocument.Parse json

    match doc.RootElement.TryGetProperty "Items" with
    | false, _ -> []
    | true, items ->
        match items.TryGetProperty "Content" with
        | false, _ -> []
        | true, content ->
            content.EnumerateArray()
            |> Seq.map (fun i -> i.GetProperty("Identity").GetString())
            |> Seq.toList


let dockerIgnoreTests =
    testList
        ".dockerignore"
        [
            // The invariant the whole file depends on. A re-include below the exclusions
            // silently re-admits everything they keep out, and every required pattern can
            // still be present while that is true, so no other test here would catch it.
            test "every exclusion comes after every re-include" {
                let lines = dockerIgnoreLines () |> List.filter (fun l -> l.Text <> OptInBaseline)

                let lastReInclude =
                    lines |> List.filter (fun l -> l.Text.StartsWith "!") |> List.tryLast

                let firstExclusion =
                    lines |> List.filter (fun l -> not (l.Text.StartsWith "!")) |> List.tryHead

                match lastReInclude, firstExclusion with
                | Some reInclude, Some exclusion ->
                    reInclude.Number < exclusion.Number
                    |> Expect.isTrue
                        $"the last matching pattern wins, so re-include '%s{reInclude.Text}' on line %i{reInclude.Number} cancels exclusion '%s{exclusion.Text}' on line %i{exclusion.Number}; move every exclusion below every ! line"
                | _ -> failtest "expected .dockerignore to hold both re-includes and exclusions"
            }

            for pattern in requiredExclusions do
                test $"excludes {pattern}" {
                    dockerIgnoreLines ()
                    |> List.exists (fun l -> l.Text = pattern)
                    |> Expect.isTrue
                        $"'%s{pattern}' is missing from .dockerignore; without it that path enters the build context"
                }
        ]


let publishedContentTests =
    testList
        "published content"
        [
            // Guards EnableDefaultContentItems being removed, and equally a stray
            // <Content Include=... /> added by hand. A textual check would see neither.
            test "the server project publishes no unexpected content" {
                contentItems serverProject
                |> List.sort
                |> Expect.equal
                    "the Web SDK default content glob publishes any **/*.json or **/*.config left in the project folder; keep EnableDefaultContentItems false, or declare the file and add it to allowedContentItems"
                    (allowedContentItems |> List.sort)
            }
        ]


runTestsWithCLIArgs
    []
    [| "--summary" |]
    (testList
        "packaging"
        [
            dockerIgnoreTests
            publishedContentTests
        ])
|> exit
