namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Path =

    open System.IO


    let combineWith p2 p1 = 
        Path.Combine(p1, p2)
