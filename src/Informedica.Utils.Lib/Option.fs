namespace Informedica.Utils.Lib

/// Additional functions for the
/// fsharp option type
[<RequireQualifiedAccess>]
module Option =


    /// Create a `None`
    let none _ = None


    /// Choose `opt1` or `opt2` based on a 
    /// predicate function `cp`
    let choose cp opt1 opt2 = 
        match opt1, opt2 with
        | None, None   -> None
        | Some _, None -> opt1
        | None, Some _ -> opt2
        | Some x1, Some x2 -> 
            if cp x1 x2 then x1 |> Some else x2 |> Some


    /// Choose the minimum of two options
    let min opt1 opt2 = choose (<=) opt1 opt2


    /// choose the maximum of two options
    let max opt1 opt2 = choose (>=) opt1 opt2
