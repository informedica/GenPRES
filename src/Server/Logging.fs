
module Logging

    open System
    open System.Reflection
    open System.IO

    open IcedTasks.Polyfill.Async

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL

    open Informedica.Agents.Lib
    open Informedica.Logging.Lib
    open Informedica.GenOrder.Lib

    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime

    let [<Literal>] MAX_LOG_FILES = 5


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
        
        // Navigate up from assembly location to find server root directory
        let rec findServerRoot dir =
            // First priority: Check for development scenario (Server.fs exists)
            if File.Exists(Path.Combine(dir, "Server.fs")) then
                dir
            // Second priority: Check if we're in a typical development structure with data folder and Server.fs
            elif Directory.Exists(Path.Combine(dir, "data")) && 
                 File.Exists(Path.Combine(dir, "Server.fs")) then
                dir
            else
                let parent = Directory.GetParent(dir)
                if parent <> null then
                    findServerRoot parent.FullName
                else
                    // Last resort: Check for production scenario (Server.dll exists, but no Server.fs)
                    if File.Exists(Path.Combine(currentDir, "Server.dll")) && 
                       not (File.Exists(Path.Combine(currentDir, "Server.fs"))) then
                        currentDir
                    else
                        Environment.CurrentDirectory // Final fallback
        
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


    let getDirAgent (path: string) =
        let agent = FileDirectoryAgent.create()
        // Ensure policy is set on the directory (not the file path)
        let dir =
            match Path.GetDirectoryName path with
            | null | "" -> getServerDataPath ()
            | d -> d
        agent |> FileDirectoryAgent.setPolicyWithPattern dir MAX_LOG_FILES "*.log"


    let config =
        AgentLogging.AgentLoggerDefaults.config
        |> AgentLogging.AgentLoggerDefaults.withLevel Level.Informative
        |> AgentLogging.AgentLoggerDefaults.withMaxMessages (Some 10_000)
        |> AgentLogging.AgentLoggerDefaults.withFlushInterval (TimeSpan.FromSeconds 5.)
        |> AgentLogging.AgentLoggerDefaults.withMinFlushInterval (TimeSpan.FromSeconds 1.)
        |> AgentLogging.AgentLoggerDefaults.withMaxFlushInterval (TimeSpan.FromSeconds 60.)


    let mutable private logger = None //Informedica.GenOrder.Lib.OrderLogging.createAgentLogger config


    let getLogger () =
        if logger.IsNone then
            let instance = OrderLogging.createAgentLogger config
            logger <- instance |> Some
        logger.Value


    let activateLogger (componentName: string option) (logger: AgentLogging.AgentLogger) =
        let loggingEnabled =
            Env.getItem "GENPRES_LOG"
            |> Option.map (fun s ->
                match s.Trim().ToLowerInvariant() with
                | "1" | "true" | "yes" | "on" -> true
                | _ -> false)
            |> Option.defaultValue false

        if loggingEnabled then

            let path = getRecommendedLogPath componentName

            // Prune asynchronously to avoid blocking startup
            async {
                use agent = getDirAgent path
                let! res = FileDirectoryAgent.pruneAsync path agent
                match res with
                | Ok n when n > 0 -> writeInfoMessage $"🧹 Pruned {n} old log file(s)\n"
                | Ok _ -> ()
                | Error s -> writeErrorMessage $"❌ Log path prune errored with: {s}\n"

                let! res = logger.StartAsync (Some path) Level.Informative
                res
                |> function
                | Ok _-> 
                    writeInfoMessage $"💾 Logger for {componentName} activated - Writing to: {path}\n"
                    //| None -> printfn "🖥️  Logger activated - Console only"
                | Error s -> writeErrorMessage $"❌ Logger for {componentName} could not be activated:\n{s}\n"
            }
        
        else async { () }


    let inline report (logger: AgentLogging.AgentLogger) x =
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
        |> Async.Start
        x
