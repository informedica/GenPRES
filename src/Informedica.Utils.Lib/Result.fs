namespace Informedica.Utils.Lib



[<RequireQualifiedAccess>]
module Result =


    let get = function
        | Ok r -> r
        | Error _ -> failwith "cannot get result from Error"

