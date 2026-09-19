module Logging

open System
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

    module Logging = Informedica.Logging.Lib.Logging

    /// Messages used by the Server that can be logged
    type Message =
        | Request of method_: string * path: string * clientIP: string
        | Info of string
        | Warning of string
        | Error of string

        interface IMessage

    /// Log a request line as Informative
    let logRequest (logger: AgentLogging.AgentLogger) (method_: string) (path: string) (clientIP: string) =
        Request(method_, path, clientIP) |> Logging.logInfo logger.Logger


[<Literal>]
let MAX_LOG_FILES = 10_000


let getRecommendedLogPath (componentName: string option) =
    let logDir = AppPath.logsDir ()
    Directory.CreateDirectory(logDir) |> ignore

    let componentName = componentName |> Option.defaultValue "general"
    let timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
    let shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4)
    let fileName = $"genpres_{componentName}_{timestamp}_{shortGuid}.log"

    Path.Combine(logDir, fileName)


let getDirAgent (path: string) =
    let agent = FileDirectoryAgent.create ()
    // Ensure policy is set on the directory (not the file path)
    let dir =
        match Path.GetDirectoryName path with
        | null
        | "" -> AppPath.rootPath ()
        | d -> d

    agent |> FileDirectoryAgent.setPolicyWithPattern dir MAX_LOG_FILES "*.log"


let getConfig level =
    AgentLogging.AgentLoggerDefaults.config
    |> AgentLogging.AgentLoggerDefaults.withLevel level
    |> AgentLogging.AgentLoggerDefaults.withMaxMessages (Some 10_000)
    |> AgentLogging.AgentLoggerDefaults.withFlushInterval (TimeSpan.FromSeconds 10.)
    |> AgentLogging.AgentLoggerDefaults.withMinFlushInterval (TimeSpan.FromMilliseconds 10.)
    |> AgentLogging.AgentLoggerDefaults.withMaxFlushInterval (TimeSpan.FromSeconds 20.)
    |> AgentLogging.AgentLoggerDefaults.withFlushThreshold 100


type LoggerType =
    | RequestLogger
    | OrderLogger
    | ResourcesLogger
    | FormularyLogger
    | OrderPlanLogger
    | ParenteraliaLogger


let internal loggerLock = obj ()


let mutable loggers: Map<LoggerType * Level, AgentLogging.AgentLogger> =
    [] |> Map.ofList


let getLogger (level: Level) (loggerType: LoggerType) =
    lock
        loggerLock
        (fun () ->
            match loggers |> Map.tryFind (loggerType, level) with
            | Some logger -> logger
            | None ->
                let logger = level |> getConfig |> OrderLogging.createAgentLogger
                loggers <- loggers.Add((loggerType, level), logger)
                logger
        )


let loggingEnabled =
    Env.getItem "GENPRES_LOG"
    |> Option.map (fun s -> s |> String.trim |> String.notEmpty)
    |> Option.defaultValue false


let loggingLevel =
    Env.getItem "GENPRES_LOG"
    |> Option.bind (fun (s: string) ->
        match s.Trim().ToLowerInvariant() with
        | "d" -> Level.Debug |> Some
        | "i" -> Level.Informative |> Some
        | "w" -> Level.Warning |> Some
        | "e" -> Level.Error |> Some
        | _ -> None
    )


let setComponentName (componentName: string option) (logger: AgentLogging.AgentLogger) =
    match loggingLevel with
    | Some level ->

        let path = getRecommendedLogPath componentName

        async {
            let dirAgent = getDirAgent path
            let! pruned = FileDirectoryAgent.pruneAsync path dirAgent

            match pruned with
            | Ok n when n > 0 -> writeInfoMessage $"🧹 Pruned {n} old log file(s)\n"
            | Ok _ -> ()
            | Error s -> writeErrorMessage $"❌ Log path prune errored with: {s}\n"

            dirAgent |> Agent.dispose

            logger.Start (Some path) level
        }
    | None -> async { () }
