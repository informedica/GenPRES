#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#if !FAKE
#r "netstandard"

// #r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095

#endif

open System
open Fake.Core
open Fake.DotNet
open Fake.IO

let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let sharedPath = Path.getFullName "./src/Shared"
let clientDeployPath = Path.combine clientPath "deploy"
let deployDir = Path.getFullName "./deploy"

let platformTool tool winTool =
    let tool =
        if Environment.isUnix then tool
        else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " + "Please install it and make sure it's available from your path. "
            + "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

let nodeTool = platformTool "node" "node.exe"
let yarnTool = platformTool "yarn" "yarn.cmd"
let fantomasTool = platformTool "fantomas" "fantomas"

let runTool cmd args workingDir =
    let arguments =
        args
        |> String.split ' '
        |> Arguments.OfArgs
    Command.RawCommand(cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result = DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "opening browser failed"
    |> Proc.run
    |> ignore

// Clean the solution
Target.create "Clean" (fun _ -> [ deployDir; clientDeployPath ] |> Shell.cleanDirs)
// Install the client
Target.create "InstallClient" (fun _ ->
    printfn "Node version:"
    runTool nodeTool "--version" __SOURCE_DIRECTORY__
    printfn "Yarn version:"
    runTool yarnTool "--version" __SOURCE_DIRECTORY__
    runTool yarnTool "install --frozen-lockfile" __SOURCE_DIRECTORY__
    runDotNet "restore" clientPath)
// Build the solution
Target.create "Build" (fun _ ->
    runDotNet "build" serverPath
    runTool yarnTool "webpack-cli -p" __SOURCE_DIRECTORY__)
// Run the development environment
Target.create "Run" (fun _ ->
    let server = async { runDotNet "watch run" serverPath }
    let client = async { runTool yarnTool "webpack-dev-server --host 0.0.0.0 --port 8080" __SOURCE_DIRECTORY__ }

    let browser =
        async {
            do! Async.Sleep 5000
            openBrowser "http://localhost:8080"
        }

    let vsCodeSession = Environment.hasEnvironVar "vsCodeSession"
    let safeClientOnly = Environment.hasEnvironVar "safeClientOnly"

    let tasks =
        [ if not safeClientOnly then yield server
          yield client ]
    tasks
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore)

// Build a docker image
let buildDocker tag =
    let args = sprintf "build -t %s ." tag
    runTool "docker" args "."

// Bundle the solution
Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"
    let clientDir = Path.combine deployDir "Client"
    let publicDir = Path.combine clientDir "public"
    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath
    Shell.copyDir publicDir clientDeployPath FileFilter.allFiles)

let dockerUser = "halcwb"
let dockerImageName = "genpres"
let dockerFullName = sprintf "%s/%s" dockerUser dockerImageName

// Docker target
Target.create "Docker" (fun _ -> buildDocker dockerFullName)

let runFantomas folder =
    let cmd = sprintf "%s --recurse --profile" folder
    runTool fantomasTool cmd __SOURCE_DIRECTORY__

// Run the fantomas tool
Target.create "Fantomas" (fun _ -> "build.fsx" |> runFantomas)
// serverPath |> runFantomas
// sharedPath |> runFantomas
// clientPath |> runFantomas)

open Fake.Core.TargetOperators

"Fantomas" ==> "Clean" ==> "InstallClient" ==> "Build" ==> "Bundle" ==> "Docker"
"Fantomas"
"Fantomas" ==> "Clean" ==> "InstallClient" ==> "Run"
Target.runOrDefaultWithArguments "Build"
