open System.Text.RegularExpressions

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

let envPath = Path.getFullName ".env"
let envExamplePath = Path.getFullName ".env.example"

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
// Nothing depends on this target; the `benchmark` job in .github/workflows/build.yml runs it so
// the directory cannot rot silently again, and it compiles everything with one command locally.
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


// Generates the HTML API reference for the libraries with fsdocs (issue #460). fsdocs finds GenPRES.sln,
// documents every project in it that sets <GenerateDocumentationFile>true</...> and is not a test project
// (all 16 src/Informedica.*.Lib today), and renders their /// XML doc comments alongside the literate
// content under docs/reference/. Output goes to ./output/ (gitignored); .github/workflows/docs.yml
// publishes it to GitHub Pages on push to master.
//
// Self-contained rather than chained off Build: fsdocs cracks the projects and reads their compiled .dll + .xml,
// and defaults to the Release configuration, so this builds Release explicitly instead of reusing Build's Debug
// output. Set FSDOCS_ROOT to the site's base URL (docs.yml passes https://informedica.github.io/GenPRES/ for
// the project Pages site); left unset the links are site-root relative, which is what a local `--output` preview wants.
let fsdocsRootArgs () =
    match System.Environment.GetEnvironmentVariable "FSDOCS_ROOT" with
    | root when System.String.IsNullOrWhiteSpace root -> []
    | root -> [ "--parameters"; "root"; root ]


Target.create
    "ApiDocs"
    (fun _ ->
        run dotnet [ "tool"; "restore" ] "."
        run dotnet [ "restore"; sln ] "."
        run dotnet [ "build"; sln; "-c"; "Release"; "--no-restore" ] "."

        run
            dotnet
            ([
                "fsdocs"
                "build"
                "--input"
                "docs/reference"
                "--output"
                "output"
                "--clean"
             ]
             @ fsdocsRootArgs ())
            "."
    )


// Local live-preview server for iterating on the API reference: rebuilds and reloads the  browser on changes
// to docs/reference/ or to the libraries' XML doc comments. Serves on http://localhost:8901 by default.
Target.create
    "ApiDocsWatch"
    (fun _ ->
        run dotnet [ "tool"; "restore" ] "."
        run dotnet [ "build"; sln; "-c"; "Release" ] "."
        run dotnet [ "fsdocs"; "watch"; "--input"; "docs/reference" ] "."
    )


// A fresh clone or worktree has no .env (it is gitignored), and the server refuses to start
// without GENPRES_URL_ID. .env.example ships the public demo sheet ID and GENPRES_PROD=0, so
// seeding .env from it gives a working demo run with no manual step. .env.example leaves
// GENPRES_PASSWORD empty, so the seeded server has admin operations disabled (fail-closed)
// rather than a repository-visible password. An existing .env is never touched, so local or
// production settings stay as they are. Only the dev-server launch needs this: Build,
// ServerTests and Bundle keep running without a .env, as they do in CI.
let ensureEnvFile () =
    if not (File.exists envPath) then
        Shell.copyFile envPath envExamplePath
        Trace.logfn "No .env found; created %s from .env.example (demo settings)." envPath


Target.create
    "Run"
    (fun _ ->
        ensureEnvFile ()

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


// Every test project in the solution, in the order the solution lists them. Read from
// GenPRES.sln rather than kept as a list here, because a list here would rot: the
// debugTests.sh this replaced carried nine projects and had silently fallen six behind.
// `scripts/DependencyRule.fsx` reads the solution the same way, for the same reason.
let testProjects () =
    File.readAsString sln
    |> fun text -> Regex.Matches(text, "\"([^\"]+\\.fsproj)\"")
    |> Seq.map (fun m -> m.Groups[1].Value.Replace('\\', '/'))
    |> Seq.filter (fun path -> path.StartsWith "tests/")
    |> Seq.distinct
    |> List.ofSeq


// Runs each test assembly on its own, with Expecto's own runner rather than `dotnet test`:
// --debug names the test that is running, --summary lists the outcome per test, and
// --sequenced stops the assembly running its tests in parallel. That combination is what
// makes a flaky test or an interaction between two tests findable, which `ServerTests`
// (quiet, parallel, one `dotnet test` over the solution) is not meant for. Slow by design.
//
// Other Expecto options worth swapping in while chasing something:
//   --summary-location    the source location of each test in the summary
//   --filter <substring>  only the tests whose name starts with it
//   --list-tests          name them without running them
//
// `run` throws on a non-zero exit, so the first failing assembly ends the target and the
// ones after it do not run -- the same behaviour debugTests.sh had. On a checkout without
// the G-Standaard files under data/zindex that first failure is ZIndex.Tests, which reads
// them; set CI=true to make those tests skip instead, as they do on the CI runners.
Target.create
    "DebugTests"
    (fun _ ->
        let projects = testProjects ()

        Trace.logfn "Running %i test projects in debug mode ..." projects.Length

        projects
        |> List.iter (fun proj ->
            Trace.logfn "\n--- %s" proj
            run dotnet [ "run"; "--project"; proj; "--"; "--debug"; "--summary"; "--sequenced" ] "."
        )
    )

Target.create "CheckVersions" (fun _ -> run dotnet [ "fsi"; "scripts/CheckSolutionVersions.fsx" ] ".")


// The whole suite through `dotnet test`, with its own output rather than the per-assembly
// summary ServerTests prints. It used to start the Vite dev server after the tests, which
// never returns, so the target ran the tests and then hung; nothing could call it.
Target.create "TestHeadless" (fun _ -> run dotnet [ "test"; sln; "--no-build"; "--no-restore" ] ".")


Target.create "Format" (fun _ -> run dotnet [ "fantomas"; "." ] ".")


// The Markdown files the repository holds, asked of git rather than globbed off disk.
// A `**/*.md` glob also reports on whatever untracked folders a contributor keeps in
// their working copy, so the count depended on who ran it and buried the tracked files
// it was meant to be about. An entry staged as deleted is dropped, since it has an index
// entry but no file to read.
let trackedMarkdown () =
    CreateProcess.fromRawCommand "git" [ "ls-files"; "*.md" ]
    |> CreateProcess.withWorkingDirectory "."
    |> CreateProcess.redirectOutput
    |> Proc.run
    |> fun result -> result.Result.Output.Split('\n')
    |> Array.map (fun line -> line.Trim())
    |> Array.filter (fun line -> line <> "" && File.exists line)
    |> List.ofArray


Target.create
    "MarkdownLint"
    (fun _ ->
        try
            match trackedMarkdown () with
            | [] -> Trace.traceImportant "MarkdownLint: git reported no tracked Markdown files."
            | files ->
                // markdownlint-cli2 exits non-zero as soon as it reports anything, and this
                // target is advisory: the pre-commit hook never blocks a commit on a Markdown
                // finding. So the exit code becomes a notice. Built here rather than run through
                // `run`, whose failure message repeats the argument list back -- which is now
                // every tracked Markdown file.
                let result =
                    CreateProcess.fromRawCommand (findOnPath "npx") ([ "--yes"; "markdownlint-cli2" ] @ files)
                    |> CreateProcess.withWorkingDirectory "."
                    |> Proc.run

                if result.ExitCode <> 0 then
                    Trace.traceImportant "⚠️  MarkdownLint reported issues; see the summary above."
        with ex ->
            // npx or git missing, say. The target reports on documentation and must not be the
            // reason a build fails.
            Trace.traceImportant $"⚠️  MarkdownLint could not run: {ex.Message}"
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
         @ [ "--build-arg"; $"APP_VERSION={version}"; "-t"; dockerImage; "." ])
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
                // The image ships only the *.demo cache files, so production data has to come
                // from the host. compose.yaml mounts the same folder; without it a run started
                // with GENPRES_PROD=1 finds no *.cache and is production in name only.
                "-v"
                $"{dataPath}/cache:/app/data/cache"
                "-e"
                "GENPRES_URL_ID"
                "-e"
                "GENPRES_PASSWORD"
                // optional: forwarded only when set in the caller's environment
                "-e"
                "GENPRES_PROD"
                "-e"
                "GENPRES_LANG"
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

        "Build" ==> "ServerTests"
        "Build" ==> "CheckVersions"
    ]


[<EntryPoint>]
let main args = runOrDefault args
