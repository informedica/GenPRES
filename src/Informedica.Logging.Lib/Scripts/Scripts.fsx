
#load "load.fsx"

open System

open Informedica.Logging.Lib


type StringMessage = 
    { Text : string }
    interface IMessage



let agentLogger = AgentLogging.createAgentLogger (fun e -> sprintf "%A" e)


agentLogger.Start None Level.Debug

let message = { Text = "This is a test message" } :> IMessage


agentLogger.Logger.Log { TimeStamp = DateTime.Now; Level = Level.Debug; Message = message
} 

agentLogger.Report ()


agentLogger.Start None Level.Debug


agentLogger.Stop ()


agentLogger.Report ()


1 + 1


