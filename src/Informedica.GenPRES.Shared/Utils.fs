namespace Shared

open System
open System.Globalization


module Measures =

    open Shared.Types

    let toGram (x: int) = x * 1<gram>

    let toCm (x: int) = x * 1<cm>

    let toYear (x: int) = x * 1<year>

    let toMonth (x: int) = x * 1<month>

    let toWeek (x: int) = x * 1<week>

    let toDay (x: int) = x * 1<day>


module String =


    /// True when `s` is null, empty, or only white space.
    let isNullOrWhiteSpace (s: String) = String.IsNullOrWhiteSpace(s)


    /// True when `s` has content other than white space. The negation of
    /// `isNullOrWhiteSpace`; a null string is not "not empty".
    let notEmpty = isNullOrWhiteSpace >> not


    /// True when `s` is null or zero-length. White space counts as content
    /// here, unlike `isNullOrWhiteSpace`.
    let isNullOrEmpty (s: String) = String.IsNullOrEmpty(s)


    let replace (s1: string) s2 (s: string) = s.Replace(s1, s2)


    let split (del: string) (s: string) = s.Split(del)


    /// Apply `f` to string `s`
    let apply f (s: string) = f s

    /// Utility to enable type inference
    let get = apply id

    /// Count the number of times that a
    /// string t starts with character c
    let countFirstChar c t =
        let _, count =
            if t |> isNullOrEmpty then
                (false, 0)
            else
                t
                |> Seq.fold (fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec)) (true, 0)

        count

    /// Check if string `s2` contains string `s1`
    let contains = fun (s1: string) (s2: string) -> (s2 |> get).Contains(s1)


    let trim (s: string) = s.Trim()


    /// Get the length of s
    let length s = (s |> get).Length


    /// Remove trailing characters from a string
    let removeTrailing chars (s: String) =
        s
        |> Seq.rev
        |> Seq.map string
        |> Seq.skipWhile (fun c -> chars |> Seq.exists ((=) c))
        |> Seq.rev
        |> String.concat ""


    /// Remove trailing zeros from a Dutch number
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

    /// Calculates the number of decimal digits that
    /// should be shown according to a precision
    /// number n that specifies the number of
    /// non-zero digits in the decimals.
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
    /// etc.
    /// If n < 0 then n = 0 is used.
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
                // calculate the first occurrence of a non-zero decimal digit
                let c = (s[1] |> String.countFirstChar '0')
                c + p

    /// Fix the precision of a float f to
    /// match a minimum of non-zero digits n
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
    /// etc.
    /// If n < 0 then n = 0 is used.
    let fixPrecision n (f: float) = Math.Round(f, f |> getPrecision n)


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


    /// Get the nearest index in a list to a target value.
    /// Returns the index of the element that has the smallest absolute difference from the target.
    /// Throws an exception if the list is empty.
    let inline nearestIndex x xs =
        match xs with
        | [] -> invalidArg "xs" "Array cannot be empty to calculate nearest value."
        | _ ->
            let deltas = xs |> List.map ((-) x) |> List.map abs
            let minDelta = deltas |> List.min
            deltas |> List.findIndex ((=) minDelta)


[<RequireQualifiedAccess>]
module Decimal =

    open System.Globalization


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

                if int leftPart > 0 then
                    p
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
    let fixPrecision n (d: decimal) = if n < 0 then d else Math.Round(d, d |> getPrecision n)


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


    /// Returns a string representation of a decimal in Dutch format without trailing zeros
    let toStringNumberNLWithoutTrailingZeros =
        toStringNumberNL "" >> String.removeTrailingZerosFromDutchNumber


    /// Returns a string representation of a float in Dutch format without trailing zeros
    /// and with a fixed precision.
    /// Example: 0.0666m |> toStringNumberNLWithoutTrailingZerosFixPrecision 2 = "0.067"
    let toStringNumberNLWithoutTrailingZerosFixPrecision n = fixPrecision n >> toStringNumberNLWithoutTrailingZeros


module Csv =

    open System.Text.RegularExpressions
    open Types


    /// The text NumberStyles.Float accepts under the invariant culture, bar Infinity and NaN:
    /// optional white space, an optional sign, digits with at most one decimal point, an optional
    /// exponent. The digits and the six white space characters .NET allows are spelled out, since
    /// \d and \s differ between .NET and JavaScript.
    let plainDecimal =
        Regex(@"^[ \t\n\v\f\r]*[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)([eE][+-]?[0-9]+)?[ \t\n\v\f\r]*$")


    /// Whether the text is a plain decimal, the only form the sheet writes a number in.
    let isPlainDecimal (x: string) = not (isNull x) && plainDecimal.IsMatch x


    /// The number as the sheet writes it, with a decimal point, whatever the culture of the host:
    /// a parse with the current culture drops the point as a group separator under a culture that
    /// writes the decimal comma, and reads 0.0192 as 192.
    let tryParseFloat (x: string) =
#if FABLE_COMPILER
        // Fable drops the style and the culture and parses as JavaScript does, which also takes
        // 0x, 0b and 0o prefixes and an underscore; the shape check keeps the browser to what the
        // server accepts. JavaScript's parse does not depend on the browser's locale
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


module TextBlock =

    open Shared.Types
    open System.Text.RegularExpressions

    /// Convert a string to a Valid TextBlock with numbers shown as Bold TextItems
    let fromString (text: string) : TextBlock =
        if text |> String.isNullOrWhiteSpace then
            Valid [| Normal "" |]
        else
            // Split text into parts where numbers (including decimals, commas, and hyphens) are separated
            let pattern = @"(\d+[\d,.\-]*\d*|\d+)"
            let matches = Regex.Matches(text, pattern)

            let rec buildItems (pos: int) (matchIdx: int) (acc: TextItem list) =
                if matchIdx >= matches.Count then
                    // Add remaining text after last match
                    if pos < text.Length then
                        let remaining = text.Substring(pos)

                        if remaining |> String.notEmpty then
                            Normal remaining :: acc
                        else
                            acc
                    else
                        acc
                else
                    let m = matches[matchIdx]

                    let items =
                        // Add text before match
                        if m.Index > pos then
                            let before = text.Substring(pos, m.Index - pos)

                            if before |> String.notEmpty then
                                Bold m.Value :: Normal before :: acc
                            else
                                Bold m.Value :: acc
                        else
                            Bold m.Value :: acc

                    buildItems (m.Index + m.Length) (matchIdx + 1) items

            let items = buildItems 0 0 []

            let finalItems =
                if items |> List.isEmpty then
                    [| Normal text |]
                else
                    items |> List.rev |> List.toArray

            Valid finalItems
