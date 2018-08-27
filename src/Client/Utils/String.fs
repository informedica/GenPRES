namespace Utils

module String =

    open System

    /// Count the number of times that a 
    /// string t starts with character c
    let countFirstChar c t =
        let _, count = 
            if String.IsNullOrEmpty(t) then (false, 0)
            else
                t |> Seq.fold(fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec)) (true, 0) 
        count