namespace Informedica.Utils.Lib.BCL


[<RequireQualifiedAccess>]
module Int32 =

    open System
    open System.Globalization


    /// Parses a string to an Int32. If the string cannot be parsed, an exception is thrown.
    let parse (s: string) =
        try
        Int32.Parse(s, CultureInfo.InvariantCulture)
        with
        | e ->
            printfn $"cannot parse {s} to Int32"
            raise e


    /// Parses a string to an Int32. If the string cannot be parsed, None is returned.
    let tryParse (s : string) =
        let (b, n) = Int32.TryParse(s)
        if b then n |> Some else None


    /// Returns the string representation of the Int32 as a Dutch number.
    /// Example: 1234567 becomes 1.234.567
    let toStringNumberNL (n: int) = n.ToString("N0", CultureInfo.GetCultureInfo("nl"))

