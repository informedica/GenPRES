open Fake.Core
open Fake.IO

open Helpers


initializeContext ()


let sln = "GenPRES.sln"

let sharedPath = Path.getFullName "src/Informedica.GenPRES.Shared"
let serverPath = Path.getFullName "src/Informedica.GenPRES.Server"
let clientPath = Path.getFullName "src/Informedica.GenPRES.Client"
let dataPath = Path.getFullName   "src/Informedica.GenPRES.Server/data"

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

        Shell.copyDir deployDataPath dataPath (fun _ -> true)

        let logPath = Path.combine deployDataPath "logs"
        Shell.cleanDir logPath

        let result = System.IO.Directory.Exists(deployDataPath)
        printfn $"Copying data ... done: {result}"
    )


Target.create "Build" (fun _ -> run dotnet [ "build"; sln ] ".")


Target.create
    "Run"
    (fun _ ->
        [
            "server", dotnet [ "run"; "--no-restore" ] serverPath
            "client",
            dotnet [ "fable"; "watch"; (*"--test:MSBuildCracker";*) "-o"; "output"; "-s"; "-e"; ".jsx"; "--run"; "npx"; "vite" ] clientPath
        ]
        |> runParallel
    )


Target.create "ServerTests" (fun _ -> run dotnet [ "test"; sln; "--no-restore"; "--verbosity"; "quiet"; "--logger"; "console;verbosity=minimal" ] ".")


Target.create
    "TestHeadless"
    (fun _ ->
        run dotnet [ "test"; sln; "--no-build"; "--no-restore" ] "."
        run dotnet [ "fable"; "-o"; "output"; "-s"; "-e"; ".jsx"; "--run"; "npx"; "vite" ] clientPath

    //    run dotnet [ "fable"; "-o"; "output"; "-e"; ".jsx" ] clientTestsPath
    //    run npx [ "mocha"; "output" ] clientTestsPath
    )


Target.create
    "WatchTests"
    (fun _ ->
        [
            //        "server", dotnet [ "watch"; "run"; "--no-restore" ] serverTestsPath
            "client",
            dotnet [ "fable"; "watch"; "-o"; "output"; "-s"; "-e"; ".jsx"; "--run"; "npx"; "vite" ] clientTestsPath
        ]
        |> runParallel
    )


Target.create "Format" (fun _ -> run dotnet [ "fantomas"; "." ] ".")


Target.create "DockerRun" (fun _ -> run docker [ "run"; "-it"; "p"; "8080:8085"; "halcwb/genpres"] ".")


open Fake.Core.TargetOperators


let dependencies =
    [
        "Clean" ==> "RestoreClient" ==> "Bundle"
        "Clean" ==> "RestoreClient" ==> "Build" ==> "Run"

        "RestoreClient" ==> "Build" ==> "TestHeadless"
        "RestoreClient" ==> "Build" ==> "WatchTests"
    ]


[<EntryPoint>]
let main args = runOrDefault args
