#load "../../../scripts/load-dependencies.fsx"


#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"

#load "../Agent.fs"
#load "../FileWriterAgent.fs"
#load "../FileDirectoryAgent.fs"
#load "../ConsoleFileLogger.fs"
#load "../AgentLogging.fs"

open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(_.ToString("dd-MMM-yy"))
