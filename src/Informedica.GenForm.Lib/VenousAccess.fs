namespace Informedica.GenForm.Lib



module VenousAccess =


    let check location venousAccess =
        match location, venousAccess with
        | AnyAccess, _ -> true
        | _, xs when xs |> List.isEmpty  -> true
        | _ ->
            venousAccess
            |> List.exists ((=) location)






