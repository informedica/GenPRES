namespace Utils

module Math =

    open System
    open Utils.String

    let roundBy s n =
        (n / s)
            |> round
            |> double
            |> (fun f -> f * s)


    let roundBy0_5 =
        roundBy 0.5    


    /// Calculates the number of decimal digits that 
    /// should be shown according to a precision 
    /// number n that specifies the number of non
    /// zero digits in the decimals.
    /// * 66.666 |> getPrecision 1 = 0
    /// * 6.6666 |> getPrecision 1 = 0
    /// * 0.6666 |> getPrecision 1 = 1
    /// * 0.0666 |> getPrecision 1 = 2
    /// * 0.0666 |> getPrecision 0 = 0
    /// * 0.0666 |> getPrecision 1 = 2
    /// * 0.0666 |> getPrecision 2 = 3
    /// * 0.0666 |> getPrecision 3 = 4
    /// * 6.6666 |> getPrecision 0 = 0
    /// * 6.6666 |> getPrecision 1 = 0
    /// * 6.6666 |> getPrecision 2 = 1
    /// * 6.6666 |> getPrecision 3 = 2
    /// etc
    /// If n < 0 then n = 0 is used.
    let getPrecision n f =
        let n = if n < 0 then 0 else n
        if f = 0. || n = 0 then n
        else
            let s = (f |> abs |> string).Split([|'.'|])
            // calculate number of remaining decimal digits (after '.')
            let p = n - (if s.[0] = "0" then 0 else s.[0].Length)
            let p = if p < 0 then 0 else p
            if (int s.[0]) > 0 then
                p
            else
                // calculate the the first occurance of a non-zero decimal digit
                let c = (s.[1] |> String.countFirstChar '0')
                c + p


    /// Fix the precision of a float f to
    /// match a minimum of non zero digits n
    /// * 66.666 |> fixPrecision 1 = 67
    /// * 6.6666 |> fixPrecision 1 = 7
    /// * 0.6666 |> fixPrecision 1 = 0.7
    /// * 0.0666 |> fixPrecision 1 = 0.07
    /// * 0.0666 |> fixPrecision 0 = 0
    /// * 0.0666 |> fixPrecision 1 = 0.07
    /// * 0.0666 |> fixPrecision 2 = 0.067
    /// * 0.0666 |> fixPrecision 3 = 0.0666
    /// * 6.6666 |> fixPrecision 0 = 7
    /// * 6.6666 |> fixPrecision 1 = 7
    /// * 6.6666 |> fixPrecision 2 = 6.7
    /// * 6.6666 |> fixPrecision 3 = 6.67
    /// etc
    /// If n < 0 then n = 0 is used.
    let fixPrecision n (f: float) =
        Math.Round(f, f |> getPrecision n)
