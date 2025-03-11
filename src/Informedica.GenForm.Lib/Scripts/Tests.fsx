
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#r "../../Informedica.ZIndex.Lib/bin/Debug/net9.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net9.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.Utils.Lib/bin/Debug/net9.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net9.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Debug/net9.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Debug/net9.0/Informedica.GenForm.Lib.dll"


//#load "load.fsx"

#time


open System


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__



module Expecto =

    open Expecto

    let run = runTestsWithCLIArgs [] [| "--summary" |]



//Tests.tests
//|> Expecto.run