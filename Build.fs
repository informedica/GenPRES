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


let serverProj = Path.combine serverPath "Informedica.GenPRES.Server.fsproj"


// Builds only the server and the libraries it depends on. Skips the test projects
// and the client toolchain entirely, so it is the fastest loop for anyone working on
// just the server or a domain library.
Target.create
    "ServerBuild"
    (fun _ ->
        run dotnet [ "restore"; serverProj ] "."
        run dotnet [ "build"; serverProj; "--no-restore" ] "."
    )


// Builds the client's browser output: Fable compiles F# to .jsx, then Vite bundles
// it into deploy/public (vite.config.js sets the outDir). Depends on RestoreClient
// (npm ci), declared below. `Bundle` keeps its own copy of this because it runs the
// client build in parallel with publishing the server.
Target.create
    "ClientBuild"
    (fun _ ->
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
                "build"
                "--emptyOutDir"
            ]
            clientPath
    )


// Builds the benchmark suite, which is deliberately excluded from GenPRES.sln (issue #513).
// Nothing depends on this target and it is not run in CI; it exists so the directory can be
// compiled with one command instead of rotting silently.
Target.create
    "BenchmarkBuild"
    (fun _ ->
        [
            "benchmark/benchmark.fsproj"
            "benchmark/RationalXBench/RationalXBench.fsproj"
            "benchmark/ScenarioBench/ScenarioBench.fsproj"
            "benchmark/ValueUnitBench/ValueUnitBench.fsproj"
        ]
        |> List.iter (fun proj -> run dotnet [ "build"; proj; "-c"; "Release" ] ".")
    )


// Umbrella target: restores and builds every project in the solution (libraries, server, tests, and client).
// Its body is deliberately unchanged by the ServerBuild/ClientBuild split, so the chains that hang off it
// behave exactly as before. In particular it still involves no npm, which is what keeps `Build ==> ServerTests`
// cheap in CI, and it still builds the test projects, which `ServerTests` needs since it runs with --no-restore.
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

        // Wall-clock since the target began, stamped on each assembly summary below.
        let sw = System.Diagnostics.Stopwatch.StartNew()

        // Capture all output so we can surface the failing tests on a non-zero
        // exit. The per-assembly progress lines replace the raw `dotnet test`
        // output, so without this the CI log shows no indication of *what* failed.
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
                    printfn "Running tests ..."

                // One line per assembly rather than a bare dot, so a slow *assembly* can be
                // told apart from a slow *platform*. `dotnet test` runs the test projects
                // concurrently, so the stamp is when this assembly finished, while VSTest's
                // own `Duration:` inside the line is how long it took.
                printfn "  [%6.1fs] %s" sw.Elapsed.TotalSeconds (line.Trim())

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
                // ServerTests depends on Build, which has just run `dotnet build` over the
                // whole solution (test projects included). Without --no-build, `dotnet test`
                // evaluates the project graph and up-to-date-checks every project a second
                // time: measured locally that second pass is 16 of the target's 29 seconds,
                // more than the test run itself. The `totalTests = 0` guard below turns a
                // genuinely unbuilt tree into a loud failure rather than a green no-op.
                "--no-build"
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
                // The per-assembly progress lines replace the raw `dotnet test` output, so dump
                // the captured output to reveal *what* failed. At minimal verbosity
                // this is just the per-project summaries plus the failure blocks
                // (no passing-test noise), so it stays readable.
                printfn "------------------------- dotnet test output ----------------------------"
                captured |> Seq.iter (printfn "%s")
                printfn "-------------------------------------------------------------------------"

                if totalFailed.Value = 0 then
                    // Results are missing rather than failing: every assembly that did report
                    // reported no failures, so whatever went wrong produced no summary at all.
                    // A discovery failure does this (a static initializer that throws yields no
                    // results — see issue #523), but so does a crashed or cancelled test host,
                    // so name the likely cause without asserting it.
                    invalidOp
                        $"dotnet test exited %i{result.ExitCode}, but no assembly reported a \
                          failing test (%i{totalPassed.Value} passed). Results are missing \
                          rather than failing. Most often an assembly threw during discovery, \
                          before Expecto could enumerate its tests — look for a top-level \
                          `let` VALUE binding that performs IO, and search the dumped output \
                          above for TypeInitializationException (see issue #523). A crashed \
                          or cancelled test host looks the same, so check the output above \
                          for a project that reported no summary line at all."
                else
                    invalidOp $"Tests failed with exit code %d{result.ExitCode}"

            if totalTests.Value = 0 then
                invalidOp
                    "No tests were discovered or run. The solution was likely not built/restored before `dotnet test`."
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
            run npx [ "--yes"; "markdownlint-cli2"; "**/*.md" ] "."
        with ex ->
            Trace.traceImportant $"⚠️  MarkdownLint: {ex.Message}"
    )


let requireEnvVar name =
    match System.Environment.GetEnvironmentVariable name with
    | v when System.String.IsNullOrWhiteSpace v ->
        invalidOp $"%s{name} is not set. Load it from .env first (see DEVELOPMENT.md)."
    | v -> v


// Override via DOCKER_IMAGE if you're pushing to your own registry/namespace
// rather than the project's `informedica/genpres` on Docker Hub (the same image
// tag-release.yml's publish-docker-image job publishes on release).
let dockerImage =
    match System.Environment.GetEnvironmentVariable "DOCKER_IMAGE" with
    | null
    | "" -> "informedica/genpres"
    | image -> image


let buildDockerImage () =
    let version =
        System.Xml.Linq.XDocument.Load("Directory.Build.props").Descendants(System.Xml.Linq.XName.Get "Version")
        |> Seq.tryHead
        |> Option.map (fun e -> e.Value.Trim())
        |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        |> Option.defaultWith (fun () -> invalidOp "Directory.Build.props: <Version> element is missing or empty.")

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


// `docker` wraps CreateProcess with addOnExited, which raises on any non-zero exit.
// This is unusable here since "no such image" is an expected outcome we need to branch on, not a build failure.
let dockerImageExistsLocally () =
    let result =
        CreateProcess.fromRawCommand "docker" [ "image"; "inspect"; dockerImage ]
        |> CreateProcess.redirectOutput
        |> Proc.run

    if result.ExitCode = 0 then
        true
    // Only "no such image" means missing. Any other failure (daemon down, permission
    // denied, wrong context) is a real Docker problem, not something a build can fix,
    // so surface it immediately instead of letting it masquerade as a routine first build.
    elif result.Result.Error.Contains "No such image" then
        false
    else
        invalidOp $"docker image inspect failed:\n%s{result.Result.Error}"


Target.create "DockerBuild" (fun _ -> buildDockerImage ())


Target.create
    "DockerRun"
    (fun _ ->
        // Fail fast with a clear message, but don't pass the values as `-e NAME=value` args: `createProcess`
        // (Helpers.fs) renders the full argument list into its failure message on any non-zero docker exit,
        // which would leak GENPRES_URL_ID/GENPRES_PASSWORD as plain text. `-e NAME` (no `=value`) makes docker
        // forward the variable from its own environment instead, so the secrets never appear in the args.
        requireEnvVar "GENPRES_URL_ID" |> ignore
        requireEnvVar "GENPRES_PASSWORD" |> ignore

        if dockerImageExistsLocally () |> not then
            Trace.traceImportant $"Docker image '{dockerImage}' not found locally, building it..."
            buildDockerImage ()

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
        "RestoreClient" ==> "ClientBuild"

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
