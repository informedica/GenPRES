namespace Shared

open System


module Measures =

    open Shared.Types

    let toGram (x: int) = x * 1<gram>

    let toCm (x: int) = x * 1<cm>


module String =



    let isNullOrWhiteSpace (s: String) = String.IsNullOrWhiteSpace(s)


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
            if String.IsNullOrEmpty(t) then
                (false, 0)
            else
                t
                |> Seq.fold
                    (fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec))
                    (true, 0)

        count

    /// Check if string `s2` contains string `s1`
    let contains = fun (s1: string) (s2: string) -> (s2 |> get).Contains(s1)


    let toLower s = (s |> get).ToLower()


    let toUpper s = (s |> get).ToUpper()


    let trim (s: string) = s.Trim()


    /// Get a substring starting at `start` with length `length`
    let subString start length s =
        if start < 0 || s |> String.length < start + length || start + length < 0  then ""
        else
            let s' = if length < 0 then start + length else start
            let l' = if length < 0 then -1 * length else length
            s.Substring(s', l')


    /// Get the first character of a string
    /// as a string
    let firstStringChar = subString 0 1


    /// Get the length of s
    let length s =
        (s |> get).Length


    /// Return the rest of a string as a string
    let restString s =
        if s = "" then ""
        else
            subString 1 ((s |> length) - 1) s


    /// Removes the last 'n' characters from the input string 's'.
    /// If the resulting string length is less than 0, an empty string is returned.
    ///
    /// Parameters:
    ///   - n: Number of characters to remove from the end of the string.
    ///   - s: Input string.
    ///
    /// Returns:
    ///   - Modified string with the last 'n' characters removed.
    let remove n s =
        let l = String.length s - n
        if l < 0 then "" else s |> subString 0 l


    /// Make the first char of a string upper case
    let firstToUpper = firstStringChar >> toUpper


    /// Make the first character upper and the rest lower of a string
    let capitalize s =
        if s = "" then ""
        else
            (s |> firstToUpper) + (s |> restString |> toLower)


    /// Remove trailing characters from a string
    let removeTrailing chars (s : String) =
        s
        |> Seq.rev
        |> Seq.map string
        |> Seq.skipWhile (fun c ->
            chars |> Seq.exists ((=) c)
        )
        |> Seq.rev
        |> String.concat ""


    /// Remove trailing zeros from a Dutch number
    let removeTrailingZerosFromDutchNumber (s : string) =
        s.Split([|","|], StringSplitOptions.None)
        |> function
        | [|n; d|] ->
            let d = d |> removeTrailing ["0"]
            if d |> String.IsNullOrEmpty then n
            else
                n + "," + d
        | _ -> s


module Math =


    let roundBy s n =
        (n / s) |> round |> double |> (fun f -> f * s)


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


    let create x = x :: []


    let inline findNearestMax n ns =
        match ns with
        | [] -> n
        | _ ->
            let n = if n > (ns |> List.max) then ns |> List.max else n

            ns
            |> List.sort
            |> List.rev
            |> List.fold (fun x a -> if (a - x) < (n - x) then x else a) n


    let removeDuplicates xs =
        xs
        |> List.fold
            (fun xs x ->
                if xs |> List.exists ((=) x) then
                    xs
                else
                    [ x ] |> List.append xs
            )
            []


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



module DateTime =


    let apply f (dt: DateTime) = f dt


    let get = apply id


    let optionToDate (yr: int option) mo dy =
        match yr, mo, dy with
        | Some y, Some m, Some d -> DateTime(y, m, d) |> Some
        | _ -> None


    let dateDiff dt1 dt2 = (dt1 |> get) - (dt2 |> get)


    let dateDiffDays dt1 dt2 = (dateDiff dt1 dt2).Days


    let dateDiffMonths dt1 dt2 =
        (dateDiffDays dt1 dt2) |> float |> (fun x -> x / 365.) |> ((*) 12.)


    let dateDiffYearsMonths dt1 dt2 =
        let mos = (dateDiffMonths dt1 dt2) |> int
        (mos / 12), (mos % 12)




[<RequireQualifiedAccess>]
module Decimal =

    open System
    open System.Globalization


    //----------------------------------------------------------------------------
    // Constants
    //----------------------------------------------------------------------------


    let Ten = 10m



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
        
        if String.IsNullOrEmpty(decimalPart) then formattedInteger
        else formattedInteger + "," + decimalPart


    /// Returns a string representation of a decimal in Dutch format without trailing zeros
    let toStringNumberNLWithoutTrailingZeros =
        toStringNumberNL "" >> String.removeTrailingZerosFromDutchNumber


    /// Returns a string representation of a float in Dutch format without trailing zeros
    /// and with a fixed precision.
    /// Example: 0.0666m |> toStringNumberNLWithoutTrailingZerosFixPrecision 2 = "0.067"
    let toStringNumberNLWithoutTrailingZerosFixPrecision n =
        fixPrecision n >> toStringNumberNLWithoutTrailingZeros



module Csv =

    open System
    open Types


    let tryCast dt (x: string) =
        match dt with
        | StringData -> box (x.Trim())
        | FloatData ->
            match Double.TryParse(x) with
            | true, n -> n |> box
            | _ ->
                $"cannot parse {x} to double"
                |> failwith
        | FloatOptionData ->
            match Double.TryParse(x) with
            | true, n -> n |> Some |> box
            | _ -> None |> box


    let getColumn dt columns sl s =
        columns
        |> Array.tryFindIndex ((=) s)
        |> function
            | None ->
                $"""cannot find column {s} in {columns |> String.concat ", "}"""
                |> failwith
            | Some i ->
                sl
                |> Array.item i
                |> tryCast dt


    let getStringColumn columns sl s =
        getColumn StringData columns sl s |> unbox<string>


    let getFloatColumn columns sl s =
        getColumn FloatData columns sl s |> unbox<float>


    let getFloatOptionColumn columns sl s =
        getColumn FloatOptionData columns sl s
        |> unbox<float option>


    let parseCSV (s: string) =
        s.Split("\n")
        |> Array.filter (String.isNullOrWhiteSpace >> not)
        |> Array.map (String.replace "\",\"" "|")
        |> Array.map (String.replace "\"" "")
        |> Array.map (fun s ->
            s.Split("|")
            |> Array.map (fun s -> s.Trim())
        )

