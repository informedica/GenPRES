namespace Utils

module String =

    open System


    /// Apply `f` to string `s`
    let apply f (s: string) = f s
    
    /// Utility to enable type inference
    let get = apply id

    /// Count the number of times that a 
    /// string t starts with character c
    let countFirstChar c t =
        let _, count = 
            if String.IsNullOrEmpty(t) then (false, 0)
            else
                t |> Seq.fold(fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec)) (true, 0) 
        count

    /// Check if string `s2` contains string `s1`
    let contains= 
        fun s1 s2 -> (s2 |> get).Contains(s1)    


    let toLower s =
        (s |> get).ToLower()