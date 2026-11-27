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
    /// using InvariantCulture
    let parse (s: string) = Decimal.Parse(s, CultureInfo.InvariantCulture)


    /// Get a float Option from a string
    let tryParse (s: string) =
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

        if d = 0m || n = 0 then
            n
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

                if leftPart <> "0" then
                    p
                else
                    let rightPart = parts[1]
                    let zeroCount = rightPart |> Seq.takeWhile (fun c -> c = '0') |> Seq.length
                    zeroCount + p


    /// Returns the number of decimals that d is rounded to for n significant digits.
    /// The number is at most 28, because Math.Round takes at most 28 decimals for a decimal.
    let roundingDecimals n (d: decimal) = d |> getPrecision n |> min 28


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
    let fixPrecision n (d: decimal) = if n < 0 then d else Math.Round(d, d |> roundingDecimals n)


    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------


    /// Returns a string representation of a decimal in Dutch format
    let toStringNumberNL p (d: decimal) =
        let invariantStr = d.ToString("F" + p, CultureInfo.InvariantCulture)
        let parts = invariantStr.Split('.')
        let integerPart = parts[0]
        let decimalPart = if parts.Length > 1 then parts[1] else ""

        // Add thousands separators
        let formattedInteger =
            integerPart
            |> Seq.rev
            |> Seq.chunkBySize 3
            |> Seq.map (Seq.rev >> Seq.map string >> String.concat "")
            |> Seq.rev
            |> String.concat " "

        if decimalPart |> String.isNullOrEmpty then
            formattedInteger
        else
            formattedInteger + "," + decimalPart

    /// Formats d as a Dutch number with at least n significant digits and without trailing zeros.
    /// A negative n prints the number in full, without rounding.
    /// Example: 0.0666m with n = 2 gives "0,067".
    let toStringNumberNLWithoutTrailingZerosFixPrecision n d =
        // a decimal has at most 28 decimals
        let decimals = if n < 0 then 28 else d |> roundingDecimals n

        d
        |> fixPrecision n
        |> toStringNumberNL $"%i{decimals}"
        |> String.removeTrailingZerosFromDutchNumber


    /// Formats d as a Dutch number with at least 3 significant digits and without trailing zeros.
    let toStringNumberNLWithoutTrailingZeros d = d |> toStringNumberNLWithoutTrailingZerosFixPrecision 3
