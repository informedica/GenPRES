
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

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime



    // Server-specific logging message types and helpers
    module ServerLogging =

        open Informedica.Logging.Lib

        /// Messages used by the Server that can be logged
        type Message =
            | Request of method_: string * path: string * clientIP: string
            | Info of string
            | Warning of string
            | Error of string
            interface IMessage

        /// Log a request line as Informative
        let logRequest (logger: AgentLogging.AgentLogger) (method_: string) (path: string) (clientIP: string) =
            Request(method_, path, clientIP)
            |> Logging.logInfo logger.Logger



    let [<Literal>] MAX_LOG_FILES = 10_000



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
        let timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
        let shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4)
        let fileName = $"genpres_{componentName}_{timestamp}_{shortGuid}.log"
        
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


    let loggingEnabled =
        Env.getItem "GENPRES_LOG"
        |> Option.map (fun s ->
            match s.Trim().ToLowerInvariant() with
            | "1" | "true" | "yes" | "on" -> true
            | _ -> false)
        |> Option.defaultValue false


    let setComponentName (componentName: string option) (logger: AgentLogging.AgentLogger) =

        if loggingEnabled then

            let path = getRecommendedLogPath componentName

            async {
                let dirAgent = getDirAgent path
                let! pruned = FileDirectoryAgent.pruneAsync path dirAgent
                match pruned with
                | Ok n when n > 0 -> writeInfoMessage $"🧹 Pruned {n} old log file(s)\n"
                | Ok _ -> ()
                | Error s -> writeErrorMessage $"❌ Log path prune errored with: {s}\n"
                dirAgent |> Agent.dispose

                let! started = logger.StartAsync (Some path) config.DefaultLevel

                started
                |> function
                | Ok _-> 
                    writeDebugMessage $"💾 Logger for {componentName} activated - Writing to: {path}\n"
                    //| None -> printfn "🖥️  Logger activated - Console only"
                | Error s -> writeErrorMessage $"❌ Logger for {componentName} could not be activated:\n{s}\n"
            }
        
        else async { () }


