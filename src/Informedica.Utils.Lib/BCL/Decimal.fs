namespace Informedica.Utils.Lib.BCL


[<RequireQualifiedAccess>]
module Decimal =

    open System
    open System.Globalization


    let Ten = 10m


    /// Get the double value of a string
    /// using `InvariantCulture`
    let parse s = Decimal.Parse(s, CultureInfo.InvariantCulture)


    /// Get a `float Option` from a string
    let tryParse (s : string) =
        let style = NumberStyles.Any
        let cult = CultureInfo.InvariantCulture
        match Decimal.TryParse(s, style, cult) with
        | true, v -> Some v
        | false, _ -> None



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
    let getPrecision n d =
        try
            let n = if n < 0 then 0 else n
            if d = 0m || n = 0 then n
            else
                let s = (d |> abs |> string)
                // chech whether the string is of type "1E-05"
                if(s.Contains "E") then
                    let k = s.IndexOf("E")+2 // get the index of the number after the "-"
                    let h = s[k..] |> int // get int 5
                    let p = h + n - 1  // precision is 5 + n -1
                    p
                else
                let s = s.Split([|'.'|])
                // calculate number of remaining decimal digits (after '.')
                let p = n - (if s[0] = "0" then 0 else s[0].Length)
                let p = if p < 0 then 0 else p
                //printfn $"parse int: {s.[0]}"
                if (int s[0]) > 0 then // s.[0] |> int64 > 0L if (*)
                    p
                else
                    // calculate the the first occurance of a non-zero decimal digit
                    let c = (s[1] |> String.countFirstChar '0')
                    c + p
        with
        | e ->
            printfn $"cannot get precision %i{n} for %A{d}"
            printfn $"catching error %A{e}"
            printfn "returning 1 as default value"
            1




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
    let fixPrecision n (d: decimal) =
        Math.Round(d, d |> getPrecision n)


    let toStringNumberNL p (d: decimal) = d.ToString("R" + p, CultureInfo.GetCultureInfo("nl"))


    let toStringNumberNLWithoutTrailingZeros =
        toStringNumberNL "" >> String.removeTrailingZerosFromDutchNumber


    let toStringNumberNLWithoutTrailingZerosFixPrecision n =
        fixPrecision n >> toStringNumberNLWithoutTrailingZeros