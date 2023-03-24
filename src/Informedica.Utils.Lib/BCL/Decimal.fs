namespace Informedica.Utils.Lib.BCL


[<RequireQualifiedAccess>]
module Decimal =

    open System
    open System.Globalization


    //----------------------------------------------------------------------------
    // Constants
    //----------------------------------------------------------------------------


    let Ten = 10m



    //----------------------------------------------------------------------------
    // Parsing
    //----------------------------------------------------------------------------

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



    //----------------------------------------------------------------------------
    // Precision
    //----------------------------------------------------------------------------


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
    let getPrecision n (d: Decimal) =
        let n = if n < 0 then 0 else n
        if d = 0m || n = 0 then n
        else
            let absF = abs d
            let s = absF.ToString("G", CultureInfo.InvariantCulture)
            if s.Contains "E" then
                let eIndex = s.IndexOf("E") + 2
                let h = int s[eIndex..]
                h + n - 1
            else
                let parts = s.Split('.')
                let leftPart = parts[0]
                let p = n - (if leftPart = "0" then 0 else leftPart.Length)
                let p = if p < 0 then 0 else p
                if int leftPart > 0 then p
                else
                    let rightPart = parts[1]
                    let zeroCount = rightPart |> Seq.takeWhile (fun c -> c = '0') |> Seq.length
                    zeroCount + p


    /// Fix the precision of a float f to
    /// match a minimum of non zero digits n
    /// * 66.666 |> fixPrecision 1 = 67
    /// * 6.6666 |> fixPrecision 1 = 7
    /// * 0.6666 |> fixPrecision 1 = 0.7
    /// * 0.0666 |> fixPrecision 1 = 0.07
    /// * 0.0666 |> fixPrecision 1 = 0.07
    /// * 0.0666 |> fixPrecision 2 = 0.067
    /// * 0.0666 |> fixPrecision 3 = 0.0666
    /// * 6.6666 |> fixPrecision 1 = 7
    /// * 6.6666 |> fixPrecision 2 = 6.7
    /// * 6.6666 |> fixPrecision 3 = 6.67
    /// etc
    /// If n < 0 then the value is not changed.
    let fixPrecision n (d: decimal) =
        if n < 0 then d
        else
            Math.Round(d, d |> getPrecision n)


    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------


    let toStringNumberNL p (d: decimal) = d.ToString("N" + p, CultureInfo.GetCultureInfo("nl"))


    let toStringNumberNLWithoutTrailingZeros =
        toStringNumberNL "" >> String.removeTrailingZerosFromDutchNumber


    let toStringNumberNLWithoutTrailingZerosFixPrecision n =
        fixPrecision n >> toStringNumberNLWithoutTrailingZeros