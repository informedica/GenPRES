namespace Informedica.Utils.Lib.BCL

/// Helper functions for `System.String`
//open System.Security.Cryptography
[<RequireQualifiedAccess>]
module String =

    open System
    open System.Text.RegularExpressions

    open Informedica.Utils.Lib

    /// Apply `f` to string `s`
    let apply f (s: string) = f s

    /// Utility to enable type inference
    let get = apply id

    /// Split string `s` at character `c`
    let splitAt c s =
        s |> NullCheck.nullOrDef (fun s' -> (s' |> get).Split([|c|])) [||]

    let arrayConcat (cs : char[]) = String.Concat(cs)

    /// Check if string `s2` contains string `s1`
    let contains=
        NullCheck.nullOrDef2 (fun s1 s2 -> (s2 |> get).Contains(s1)) false

    /// Trim string `s`
    let trim=
        NullCheck.nullOrDef (fun s -> (s |> get).Trim()) ""

    /// Make string all lower chars
    let toLower =
        NullCheck.nullOrDef (fun s -> (s |> get).ToLower()) ""

    /// Make string all upper chars
    let toUpper =
        NullCheck.nullOrDef (fun s -> (s |> get).ToUpper()) ""

    /// Get the length of s
    let length =
        NullCheck.nullOrDef (fun s -> (s |> get).Length) 0

    /// Check if string is null or only white space
    let isNullOrWhiteSpace = String.IsNullOrWhiteSpace

    /// Check if string is null or only white space
    let empty s = String.IsNullOrWhiteSpace(s)

    /// Check if string is not null or only white space
    let notEmpty = empty >> not

    /// Replace `os` with `ns` in string `s`.
    let replace =
        NullCheck.nullOrDef3 (fun os ns s -> (s |> get).Replace(os, ns)) ""

    /// Convert object to string
    let toString o =
        o |> NullCheck.nullOrDef (fun o' ->  o'.ToString()) ""

    /// Get a substring starting at `start` with length `length`
    let subString start length =
        let sub s =
            if start < 0 || s |> String.length < start + length || start + length < 0  then ""
            else
                let s' = if length < 0 then start + length else start
                let l' = if length < 0 then -1 * length else length
                s.Substring(s', l')
        NullCheck.nullOrDef sub ""

    /// Get the first character of a string
    /// as a string
    let firstStringChar = subString 0 1

    /// Return the rest of a string as a string
    let restString s =
        if s = "" then ""
        else
            subString 1 ((s |> length) - 1) s

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

    /// Get all letters as a string list
    let letters =
        ['a'..'z'] @ ['A'..'Z']
        |> List.map string

    /// Check if a string is a letter
    let isLetter s = List.exists (fun s' -> s' = s) letters

    let equals s1 s2 = s1 = s2

    /// Check if string `s1` equals `s2` caps insensitive
    let equalsCapInsens s1 s2 = s1 |> toLower |> trim = (s2 |> toLower |> trim)

    /// Split a string `s` at string `dels`
    let split (dels: string) (s: string) =
        NullCheck.nullOrDef2 (fun (s': string) (dels': string) -> s'.Split(dels') |> Array.toList) [] s dels

    /// Check whether **s1** starts with
    /// **s2** using string comparison **eqs**
    let startsWithEqs eqs s2 s1 =
        let sw s1 s2 =
            if s2 |> String.length > (s1 |> String.length) then false
            else
                s1 |> subString 0 (s2 |> String.length) |> eqs s2
        NullCheck.nullOrDef2 sw false s1 s2

    /// Check whether **s1** starts with
    /// **s2** caps sensitive
    let startsWith = startsWithEqs equals

    /// Check whether **s1** starts with
    /// **s2** caps insensitive
    let startsWithCapsInsens = startsWithEqs equalsCapInsens

    let regex s = new Regex(s)

    /// Count the number of times character
    /// c appears in string t
    let countChar c t =
        if String.IsNullOrEmpty(c) then "Cannot count empty string in text: '" + t + "'" |> failwith
        (c |> regex).Matches(t).Count

    /// Count the number of times that a
    /// string t starts with character c
    let countFirstChar c t =
        let _, count =
            if String.IsNullOrEmpty(t) then (false, 0)
            else
                t |> Seq.fold(fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec)) (true, 0)
        count


    let removeTextBetween start stop text =
        let regs = @"\" + start + @"[^\" + stop + "]*]"

        (regex regs).Replace(text, "")
        |> trim


    let removeTextBetweenBrackets = removeTextBetween "[" "]"


    let removeBrackets (s: String) =
        (regex "\[[^\]]*]").Replace(s, "")


    let removeTrailing chars (s : String) =
        s
        |> Seq.rev
        |> Seq.map string
        |> Seq.skipWhile (fun c ->
            chars |> Seq.exists ((=) c)
        )
        |> Seq.rev
        |> String.concat ""


    let removeTrailingZerosFromDutchNumber (s : string) =
        s.Split([|","|], StringSplitOptions.None)
        |> function
        | [|n; d|] ->
            let d = d |> removeTrailing ["0"]
            n + "," + d
        | _ -> s


    let containsCapsInsens s2 s1 = 
        let s1 = s1 |> toLower
        let s2 = s2 |> toLower
        s1 |> contains s2
