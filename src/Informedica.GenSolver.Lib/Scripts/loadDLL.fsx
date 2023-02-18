

#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net6.0/Informedica.GenSolver.Lib.dll"


let (|>>) r f =
    match r with
    | Ok x -> x |> f
    | Error _ -> r
