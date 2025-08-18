
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Aether"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: Validus"
#r "nuget: FParsec"


#r "../../Informedica.Utils.Lib/bin/Debug/net9.0/Informedica.Utils.Lib.dll"

#load "../Agent.fs"
#load "../FileWriterAgent.fs"

open System
open Informedica.Utils.Lib

fsi.AddPrinter<DateTime>(fun dt -> dt.ToString("dd-MMM-yy"))

