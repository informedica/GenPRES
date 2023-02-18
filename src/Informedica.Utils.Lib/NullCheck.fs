namespace Informedica.Utils.Lib

/// Function to perform a safe null check
module NullCheck =

    let nullOrDef f d v =
        if v |> isNull then d
        else v |> f

    let nullOrDef2 f d v1 v2 =
        if (v1 |> isNull) || (v2 |> isNull) then d
        else f v1 v2
        
    let nullOrDef3 f d v1 v2 v3 =
        if (v1 |> isNull) || (v2 |> isNull) || (v2 |> isNull) then d
        else f v1 v2 v3
        
