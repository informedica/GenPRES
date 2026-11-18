namespace Informedica.GenPRES.Shared

open System
open System.Globalization


module Measures =

    open Informedica.GenPRES.Shared.Types

    let toGram (x: int) = x * 1<gram>

    let toCm (x: int) = x * 1<cm>

    let toYear (x: int) = x * 1<year>

    let toMonth (x: int) = x * 1<month>

    let toWeek (x: int) = x * 1<week>

    let toDay (x: int) = x * 1<day>


module String =


    /// True when the string is null, empty or only white space.
    let isNullOrWhiteSpace (s: String) = String.IsNullOrWhiteSpace(s)


    /// True when the string has a character other than white space. False for a null string.
    let notEmpty = isNullOrWhiteSpace >> not


    /// True when the string is null or has no characters. A string of only white space is not empty.
    let isNullOrEmpty (s: String) = String.IsNullOrEmpty(s)


    let replace (s1: string) s2 (s: string) = s.Replace(s1, s2)


    let split (del: string) (s: string) = s.Split(del)


    /// Applies f to the string s.
    let apply f (s: string) = f s

    /// Returns the string unchanged. Callers use it to fix the type of a value to string for inference.
    let get = apply id

    /// Counts how many times the character c repeats at the start of the string t.
    /// Returns 0 for a null or empty string.
    let countFirstChar c t =
        let _, count =
            if t |> isNullOrEmpty then
                (false, 0)
            else
                t
                |> Seq.fold (fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec)) (true, 0)

        count

    /// True when the string s2 contains the string s1.
    let contains = fun (s1: string) (s2: string) -> (s2 |> get).Contains(s1)


    let trim (s: string) = s.Trim()


    /// Get the length of s
    let length s = (s |> get).Length


    /// Removes from the end of the string every character that is in chars.
    let removeTrailing chars (s: String) =
        s
        |> Seq.rev
        |> Seq.map string
        |> Seq.skipWhile (fun c -> chars |> Seq.exists ((=) c))
        |> Seq.rev
        |> String.concat ""


    /// Removes the trailing zeros after the decimal comma of a Dutch number.
    /// Removes the comma too when no decimals remain.
    let removeTrailingZerosFromDutchNumber (s: string) =
        s.Split([| "," |], StringSplitOptions.None)
        |> function
            | [| n; d |] ->
                let d = d |> removeTrailing [ "0" ]
                if d |> isNullOrEmpty then n else n + "," + d
            | _ -> s


module Math =


    let roundBy s n = (n / s) |> round |> double |> (fun f -> f * s)


    let roundBy0_5 = roundBy 0.5

    /// Returns the number of decimals that shows f with at least n significant digits.
    /// For example, 0.0666 |> getPrecision 2 = 3 and 6.6666 |> getPrecision 2 = 1.
    /// A negative n counts as 0. Returns n when f is zero.
    let getPrecision n f = // ToDo fix infinity case
        let n = if n < 0 then 0 else n

        if f = 0. || n = 0 then
            n
        else
            let s = (f |> abs |> string).Split([| '.' |])

            // calculate number of remaining decimal digits (after '.')
            let p = n - (if s[0] = "0" then 0 else s[0].Length)

            let p = if p < 0 then 0 else p

            if (int s[0]) > 0 then
                p
            else
                // Adds the zeros after the decimal point that come before the first non-zero digit.
                let c = (s[1] |> String.countFirstChar '0')
                c + p

    /// Rounds f to the number of decimals that shows at least n significant digits.
    /// For example, 0.0666 |> fixPrecision 2 = 0.067 and 6.6666 |> fixPrecision 2 = 6.7.
    /// A negative n counts as 0. The number of decimals is at most 15, because Math.Round takes at
    /// most 15 decimals for a float.
    let fixPrecision n (f: float) =
        // Adding zero turns a negative zero into zero, so that it does not print as "-0".
        Math.Round(f, f |> getPrecision n |> min 15) + 0.


module List =


    let inline findNearestMax n ns =
        match ns with
        | [] -> n
        | _ ->
            let n = if n > (ns |> List.max) then ns |> List.max else n

            ns
            |> List.sort
            |> List.rev
            |> List.fold (fun x a -> if (a - x) < (n - x) then x else a) n


[<RequireQualifiedAccess>]
module Decimal =

    open System.Globalization


    //----------------------------------------------------------------------------
    // Precision
    //----------------------------------------------------------------------------


    /// Returns the number of decimals that shows d with at least n significant digits.
    /// For example, 0.0666m |> getPrecision 2 = 3 and 6.6666m |> getPrecision 2 = 1.
    /// A negative n counts as 0. Returns n when d is zero.
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

                if int leftPart > 0 then
                    p
                else
                    let rightPart = parts[1]
                    let zeroCount = rightPart |> Seq.takeWhile (fun c -> c = '0') |> Seq.length
                    zeroCount + p


    /// Returns the number of decimals that d is rounded to for n significant digits.
    /// The number is at most 28, because Math.Round takes at most 28 decimals for a decimal.
    let roundingDecimals n (d: decimal) = d |> getPrecision n |> min 28


    /// Rounds d to the number of decimals that shows at least n significant digits.
    /// For example, 0.0666m |> fixPrecision 2 = 0.067m and 6.6666m |> fixPrecision 2 = 6.7m.
    /// Returns d unchanged when n is negative.
    let fixPrecision n (d: decimal) =
        if n < 0 then
            d
        else
            // A stop-gap until #1730 updates Fable: in the browser, Fable 5.0 reads a decimal from text
            // with the zeros after the decimal point kept in its digits, and then rounds 0.041666 to 4
            // decimals as 0.04167. Multiplying by one stores the digits without those zeros; in .NET it
            // changes nothing.
            let d = d * 1m
            Math.Round(d, d |> roundingDecimals n)


    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------


    /// Formats d as a Dutch number: a space between the thousands and a decimal comma.
    /// The text p gives the number of decimals; an empty p gives the default of the F format, two in .NET.
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
    /// A negative n prints the number in full: it counts as 28, the most significant digits of a decimal.
    /// Example: 0.0666m with n = 2 gives "0,067".
    let toStringNumberNLWithoutTrailingZerosFixPrecision n d =
        let n = if n < 0 then 28 else n

        d
        |> fixPrecision n
        |> toStringNumberNL $"%i{d |> roundingDecimals n}"
        |> String.removeTrailingZerosFromDutchNumber


    /// Formats d as a Dutch number with at least 3 significant digits and without trailing zeros.
    let toStringNumberNLWithoutTrailingZeros d = d |> toStringNumberNLWithoutTrailingZerosFixPrecision 3


module Csv =

    open System.Text.RegularExpressions
    open Types


    /// Matches the text that NumberStyles.Float accepts under the invariant culture, except Infinity and NaN:
    /// optional white space, an optional sign, digits with at most one decimal point and an optional exponent.
    /// The digits and the six white space characters of .NET are spelled out, because \d and \s differ
    /// between .NET and JavaScript.
    let plainDecimal =
        Regex(@"^[ \t\n\v\f\r]*[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?[ \t\n\v\f\r]*$")


    /// True when the text is a plain decimal, the only form in which the sheet writes a number. False for null.
    let isPlainDecimal (x: string) = not (isNull x) && plainDecimal.IsMatch x


    /// Parses a number written with a decimal point, whatever the culture of the host.
    /// A parse with a culture that uses a decimal comma would read 0.0192 as 192.
    /// Returns false and 0 when the text is not a number.
    let tryParseFloat (x: string) =
#if FABLE_COMPILER
        // Fable ignores the style and the culture and parses as JavaScript does, which also accepts
        // the prefixes 0x, 0b and 0o and an underscore. The shape check limits the browser to the
        // text the server accepts. The JavaScript parse does not depend on the locale of the browser.
        if isPlainDecimal x then Double.TryParse x else false, 0.
#else
        Double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture)
#endif


    let tryCast dt (x: string) =
        match dt with
        | StringData -> box (x.Trim())
        | FloatData ->
            match tryParseFloat x with
            | true, n -> n |> box
            | _ -> raise (System.FormatException $"cannot parse {x} to double")
        | FloatOptionData ->
            match tryParseFloat x with
            | true, n -> n |> Some |> box
            | _ -> None |> box


    let getColumn dt columns sl s =
        columns
        |> Array.tryFindIndex ((=) s)
        |> function
            | None ->
                raise (
                    System.Collections.Generic.KeyNotFoundException
                        $"""cannot find column {s} in {columns |> String.concat ", "}"""
                )
            | Some i -> sl |> Array.item i |> tryCast dt


    let getStringColumn columns sl s = getColumn StringData columns sl s |> unbox<string>


    let getFloatColumn columns sl s = getColumn FloatData columns sl s |> unbox<float>


    let getFloatOptionColumn columns sl s = getColumn FloatOptionData columns sl s |> unbox<float option>


    let parseCSV (s: string) =
        s.Split("\n")
        |> Array.filter String.notEmpty
        |> Array.map (String.replace "\",\"" "|")
        |> Array.map (String.replace "\"" "")
        |> Array.map (fun s -> s.Split("|") |> Array.map _.Trim())
