
module Logging

    open System
    open System.Reflection
    open System.IO

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL

    open Informedica.Agents.Lib
    open Informedica.Logging.Lib.AgentLogging


    let [<Literal>] MAX_LOG_FILES = 100


    let getAssemblyPath () =
        let assembly = Assembly.GetExecutingAssembly()
        let location = assembly.Location
        Path.GetDirectoryName(location)


    let getServerDataPath () =
        let assemblyPath = getAssemblyPath ()
        let currentDir = 
            if assemblyPath |> String.isNullOrWhiteSpace then
                Environment.CurrentDirectory
            else
                assemblyPath
        
        // Navigate up from assembly location to find src/Server directory
        let rec findServerRoot dir =
            if Directory.Exists(Path.Combine(dir, "data")) && 
               File.Exists(Path.Combine(dir, "Server.fs")) then
                dir
            else
                let parent = Directory.GetParent(dir)
                if parent = null then
                    Environment.CurrentDirectory // Fallback if we can't find server root
                else
                    findServerRoot parent.FullName
        
        findServerRoot currentDir

    let getRecommendedLogPath (componentName: string option) =
        let serverRoot = getServerDataPath ()
        
        // Use Server's data directory structure
        let logDir = Path.Combine(serverRoot, "data", "logs")
        Directory.CreateDirectory(logDir) |> ignore
        
        let componentName = componentName |> Option.defaultValue "general"
        let timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm")
        let fileName = $"genpres_{componentName}_{timestamp}.log"
        
        Path.Combine(logDir, fileName)


    let mutable private dirAgent =
        FileDirectoryAgent.create()
        |> Some


    let getDirAgent path =
        let agent =
            match dirAgent with
            | Some agent -> agent
            | None ->
                let agent = FileDirectoryAgent.create()
                dirAgent <- Some agent
                agent

        agent
        |> FileDirectoryAgent.setPolicyWithPattern path MAX_LOG_FILES "*.log"


    let activateLogger (componentName: string option) (logger: AgentLogger) =
        if Env.getItem "GENPRES_LOG"
        |> Option.map (fun s -> s = "1")
        |> Option.defaultValue false then

            let path = getRecommendedLogPath componentName
            getDirAgent path
            |> FileDirectoryAgent.prune path
            |> function
            | Ok n -> () //printfn $"Deleted {n} log files"
            | Error s -> eprintfn $"Log path prune errored with: {s}"


            logger.StartAsync (Some path) Informedica.Logging.Lib.Level.Informative
            |> Async.RunSynchronously
            |> function
            | Ok _-> 
                printfn $"💾 Logger activated - Writing to: {path}"
                //| None -> printfn "🖥️  Logger activated - Console only"
            | Error s -> eprintfn $"❌ Logger could not be activated:\n{s}"


    let inline report (logger: AgentLogger) x =
        async {
            printfn "=== PRINTING REPORT ===\n\n"
            // Give the agent time to process any pending messages
            do! Async.Sleep(100)
            let! msgs = logger.ReportAsync ()
            printfn "Found %d messages to display" msgs.Length
            msgs
            |> Array.iter (printfn "%s")
            printfn "=== END REPORT ===\n\n"
        }
        |> Async.RunSynchronously
        x
