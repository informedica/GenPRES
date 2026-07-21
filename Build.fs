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


Target.create "DockerRun" (fun _ -> run docker [ "run"; "-it"; "p"; "8080:8085"; "halcwb/genpres" ] ".")


open Fake.Core.TargetOperators


let dependencies =
    [
        "Clean" ==> "RestoreClient" ==> "Bundle"
        "Clean" ==> "RestoreClient" ==> "Build" ==> "Run"

        "RestoreClient" ==> "Build" ==> "TestHeadless"
        "RestoreClient" ==> "Build" ==> "WatchTests"

        "Build" ==> "ServerTests"
        "Build" ==> "CheckVersions"
    ]


[<EntryPoint>]
let main args = runOrDefault args
