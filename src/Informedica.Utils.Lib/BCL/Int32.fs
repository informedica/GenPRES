namespace Informedica.Utils.Lib.BCL


[<RequireQualifiedAccess>]
module Int32 =

    open System
    open System.Globalization


    /// Parses a string to an Int32. If the string cannot be parsed, an exception is thrown.
    let parse (s: string) = Int32.Parse(s, CultureInfo.InvariantCulture)


    /// Parses a string to an Int32. If the string cannot be parsed, None is returned.
    let tryParse (s: string) =
        let b, n = Int32.TryParse(s)
        if b then n |> Some else None


    /// Returns the Int32 as a Dutch number, with a dot between each group of three digits. The number
    /// format is its own, so it also works where the invariant culture is the only culture.
    /// Example: 1234567 becomes 1.234.567
    let toStringNumberNL (n: int) = n.ToString("N0", NumberFormatInfo(NumberGroupSeparator = "."))
