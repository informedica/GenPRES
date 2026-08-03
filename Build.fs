open Fake.Core
open Fake.IO

open Helpers


initializeContext ()


let sln = "GenPRES.sln"

let sharedPath = Path.getFullName "src/Informedica.GenPRES.Shared"
let serverPath = Path.getFullName "src/Informedica.GenPRES.Server"
let clientPath = Path.getFullName "src/Informedica.GenPRES.Client"
let dataPath = Path.getFullName "data"

let deployPath = Path.getFullName "deploy"

let clientTestsPath = Path.getFullName "tests/Client"

Target.create
    "Clean"
    (fun _ ->
        Shell.cleanDir deployPath
        Shell.cleanDir (Path.combine clientPath "dist")
        run dotnet [ "fable"; "clean"; "--yes"; "-e"; ".jsx" ] clientPath // Delete *.fs.js files created by Fable
    )


Target.create "RestoreClient" (fun _ -> run npm [ "ci" ] clientPath)


Target.create
    "Bundle"
    (fun _ ->
        [
            "server", dotnet [ "publish"; "-c"; "Release"; "-o"; deployPath ] serverPath
            "client",
            dotnet
                [
                    "fable"
                    //                    "--test:MSBuildCracker"
                    "-o"
                    "output"
                    "-s"
                    "-e"
                    ".jsx"
                    "--run"
                    "npx"
                    "vite"
                    "build"
                    "--emptyOutDir"
                ]
                clientPath
        ]
        |> runParallel

        let deployDataPath = Path.combine deployPath "data"
        printfn $"Copying data to {deployDataPath} ..."

        // Copy only the curated subset needed at runtime (the cache).
        [ "cache" ]
        |> List.iter (fun sub ->
            Shell.copyDir (Path.combine deployDataPath sub) (Path.combine dataPath sub) (fun _ -> true)
        )

        let logPath = Path.combine deployDataPath "logs"
        Shell.cleanDir logPath

        let result = System.IO.Directory.Exists(deployDataPath)
        printfn $"Copying data ... done: {result}"
    )


Target.create
    "Build"
    (fun _ ->
        run dotnet [ "restore"; sln ] "."
        run dotnet [ "build"; sln; "--no-restore" ] "."
    )


Target.create
    "Run"
    (fun _ ->
        [
            "server", dotnet [ "run"; "--no-restore" ] serverPath
            "client",
            dotnet
                [
                    "fable"
                    "watch" (*"--test:MSBuildCracker";*)
                    "-o"
                    "output"
                    "-s"
                    "-e"
                    ".jsx"
                    "--run"
                    "npx"
                    "vite"
                ]
                clientPath
        ]
        |> runParallel
    )


Target.create
    "ServerTests"
    (fun _ ->
        let totalPassed = ref 0
        let totalFailed = ref 0
        let totalSkipped = ref 0
        let totalTests = ref 0

        let started = ref false

        // Capture all output so we can surface the failing tests on a non-zero
        // exit. The progress dots replace the raw `dotnet test` output, so
        // without this the CI log shows no indication of *what* failed.
        let captured = System.Collections.Generic.List<string>()

        let parseLine (line: string) =
            captured.Add line

            if line.Contains("Passed:") && line.Contains("Failed:") && line.Contains("Total:") then
                let grab (key: string) =
                    let i = line.IndexOf(key)

                    if i >= 0 then
                        let start = i + key.Length

                        line
                            .Substring(start)
                            .TrimStart()
                            .Split([| ','; ' ' |], System.StringSplitOptions.RemoveEmptyEntries)
                        |> Array.tryHead
                        |> Option.bind (fun s ->
                            match System.Int32.TryParse(s) with
                            | true, n -> Some n
                            | _ -> None
                        )
                        |> Option.defaultValue 0
                    else
                        0

                totalFailed.Value <- totalFailed.Value + grab "Failed:"
                totalPassed.Value <- totalPassed.Value + grab "Passed:"
                totalSkipped.Value <- totalSkipped.Value + grab "Skipped:"
                totalTests.Value <- totalTests.Value + grab "Total:"

                if not started.Value then
                    started.Value <- true
                    printf "Running tests "

                printf "."

        // Build the process directly rather than via the `dotnet` helper: that
        // helper attaches an `addOnExited` that throws on a non-zero exit code
        // from inside `Proc.run`, which would pre-empt the result handler below
        // (so the captured output would never be printed and the test summary
        // never shown). Here we handle the exit code ourselves.
        CreateProcess.fromRawCommand
            "dotnet"
            [
                "test"
                sln
                "--no-restore"
                "--verbosity"
                "quiet"
                "--logger"
                "console;verbosity=minimal"
                "--logger"
                "trx;LogFileName=test-results.trx"
            ]
        |> CreateProcess.withWorkingDirectory "."
        |> CreateProcess.redirectOutputIfNotRedirected
        |> CreateProcess.withOutputEventsNotNull
            parseLine
            (fun line ->
                captured.Add line
                eprintfn "%s" line
            )
        |> Proc.run
        |> fun result ->
            printfn ""
            printfn "====================================================================="

            printfn
                "Test Summary: %d passed, %d failed, %d skipped, %d total"
                totalPassed.Value
                totalFailed.Value
                totalSkipped.Value
                totalTests.Value

            printfn "====================================================================="

            if result.ExitCode <> 0 then
                // The progress dots replace the raw `dotnet test` output, so dump
                // the captured output to reveal *what* failed. At minimal verbosity
                // this is just the per-project summaries plus the failure blocks
                // (no passing-test noise), so it stays readable.
                printfn "------------------------- dotnet test output ----------------------------"
                captured |> Seq.iter (printfn "%s")
                printfn "-------------------------------------------------------------------------"

                failwithf "Tests failed with exit code %d" result.ExitCode

            if totalTests.Value = 0 then
                failwith
                    "No tests were discovered or run. The solution was likely not built/restored before 'dotnet test'."
    )

Target.create "CheckVersions" (fun _ -> run dotnet [ "fsi"; "scripts/CheckSolutionVersions.fsx" ] ".")


Target.create
    "TestHeadless"
    (fun _ ->
        run dotnet [ "test"; sln; "--no-build"; "--no-restore" ] "."

        run
            dotnet
            [
                "fable"
                "-o"
                "output"
                "-s"
                "-e"
                ".jsx"
                "--run"
                "npx"
                "vite"
            ]
            clientPath

    //    run dotnet [ "fable"; "-o"; "output"; "-e"; ".jsx" ] clientTestsPath
    //    run npx [ "mocha"; "output" ] clientTestsPath
    )


Target.create
    "WatchTests"
    (fun _ ->
        [
            //        "server", dotnet [ "watch"; "run"; "--no-restore" ] serverTestsPath
            "client",
            dotnet
                [
                    "fable"
                    "watch"
                    "-o"
                    "output"
                    "-s"
                    "-e"
                    ".jsx"
                    "--run"
                    "npx"
                    "vite"
                ]
                clientTestsPath
        ]
        |> runParallel
    )


Target.create "Format" (fun _ -> run dotnet [ "fantomas"; "." ] ".")


Target.create
    "MarkdownLint"
    (fun _ ->
        try
            run npx [ "--yes"; "markdownlint-cli2"; "**/*.md"; "#node_modules" ] "."
        with ex ->
            Trace.traceImportant $"⚠️  MarkdownLint: {ex.Message}"
    )


let requireEnvVar name =
    match System.Environment.GetEnvironmentVariable name with
    | null
    | "" -> failwithf "%s is not set. Load it from .env first (see DEVELOPMENT.md)." name
    | v -> v


// Override via DOCKER_IMAGE if you're pushing to your own registry/namespace
// rather than the project's `halcwb/genpres`.
let dockerImage =
    match System.Environment.GetEnvironmentVariable "DOCKER_IMAGE" with
    | null
    | "" -> "halcwb/genpres"
    | image -> image


Target.create
    "DockerBuild"
    (fun _ ->
        let version =
            System.Xml.Linq.XDocument.Load("Directory.Build.props").Descendants(System.Xml.Linq.XName.Get "Version")
            |> Seq.map (fun e -> e.Value)
            |> Seq.tryHead
            |> Option.defaultWith (fun () -> failwith "Directory.Build.props: <Version> element not found")

        // Cross-build for a different target platform, e.g. amd64 from Apple
        // Silicon, via: DOCKER_PLATFORM=linux/amd64 dotnet run DockerBuild
        let platformArgs =
            match System.Environment.GetEnvironmentVariable "DOCKER_PLATFORM" with
            | null
            | "" -> []
            | platform -> [ "--platform"; platform ]

        run
            docker
            ([ "build" ]
             @ platformArgs
             @ [
                 "--build-arg"
                 $"APP_VERSION={version}"
                 "-t"
                 dockerImage
                 "."
             ])
            "."
    )


Target.create
    "DockerRun"
    (fun _ ->
        // Fail fast with a clear message, but don't pass the values as `-e NAME=value` args: `createProcess`
        // (Helpers.fs) renders the full argument list into its failure message on any non-zero docker exit,
        // which would leak GENPRES_URL_ID/GENPRES_PASSWORD as plain text. `-e NAME` (no `=value`) makes docker
        // forward the variable from its own environment instead, so the secrets never appear in the args.
        requireEnvVar "GENPRES_URL_ID" |> ignore
        requireEnvVar "GENPRES_PASSWORD" |> ignore

        run
            docker
            [
                "run"
                "-it"
                "--rm"
                "-p"
                "8080:8085"
                "-e"
                "GENPRES_URL_ID"
                "-e"
                "GENPRES_PASSWORD"
                dockerImage
            ]
            "."
    )


open Fake.Core.TargetOperators


let dependencies =
    [
        // Two independent prongs: a self-sufficient server build (Build restores
        // and builds GenPRES.sln itself, no npm involved) and a client toolchain
        // (Clean clears stale Fable/.jsx output, then RestoreClient runs npm ci).
        // Each leaf target below declares only the prong(s) its body actually uses,
        // rather than chaining everything through one sequence.
        "Clean" ==> "RestoreClient"

        "RestoreClient" ==> "Bundle"

        "Build" ==> "Run"
        "RestoreClient" ==> "Run"

        "Build" ==> "TestHeadless"
        "RestoreClient" ==> "TestHeadless"

        "Build" ==> "WatchTests"
        "RestoreClient" ==> "WatchTests"

        "Build" ==> "ServerTests"
        "Build" ==> "CheckVersions"
    ]


[<EntryPoint>]
let main args = runOrDefault args
