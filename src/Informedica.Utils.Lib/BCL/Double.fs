namespace Informedica.Utils.Lib.BCL

open System.Globalization


[<RequireQualifiedAccess>]
module Double =

    open System
    open System.Numerics

    open MathNet.Numerics


    //----------------------------------------------------------------------------
    // Logic
    //----------------------------------------------------------------------------

    /// Check whether a `Double` is a `NaN` value
    let isNaN = Double.IsNaN


    /// Check whether a `Double` is positive or
    /// negative infinity
    let isInfinity n = (n = infinity) || (n = -infinity)


    /// Check whether a `Double` is valid
    let isValid n =
        (isNaN n ||
         isInfinity n ||
         n >= Double.MaxValue ||
         n <= Double.MinValue) |> not


    /// Check whether a float has any
    /// decimal digits
    let floatHasDecimals (v: float) =
        v <> 0. &&
        (if v  > 0. then v > float(BigInteger v)
         else v < float(BigInteger v))


    //----------------------------------------------------------------------------
    // Parsing
    //----------------------------------------------------------------------------

    /// Get the double value of a string
    /// using `InvariantCulture`
    let parse s = Double.Parse(s, CultureInfo.InvariantCulture)


    /// Get a `float Option` from a string
    let tryParse (s : string) =
        let style = NumberStyles.Any
        let cult = CultureInfo.InvariantCulture
        match Double.TryParse(s, style, cult) with
        | true, v -> Some v
        | false, _ -> None


    /// Get a `float32` from a string
    /// returns a 0 value when the string
    /// cannot be parsed
    let stringToFloat32 s =
        match s |> tryParse with
        | Some v -> float32 v
        | None -> 0.f


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
    let getPrecision n (f: float) =
        let n = if n < 0 then 0 else n
        if f = 0. || n = 0 then n
        else
            let absF = abs f
            let s = absF.ToString("G")
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
    let fixPrecision n (f: float) =
        if n < 0 then f
        else
            let precision = getPrecision n f
            Math.Round(f, precision)


    //----------------------------------------------------------------------------
    // Math functions
    //----------------------------------------------------------------------------


    /// Return a float as a fraction of
    /// two `BigInteger`s
    let floatToFract v =
        if v = infinity || v = -infinity || v |> isNaN then None
        else
            let rec fract (v:float) m =
                match v |> floatHasDecimals with
                | false -> (BigInteger(v) , m)
                | true  -> fract (v * 10.) (m * 10N)
            fract v 1N
            |> (fun (n, d) -> (n, d.Numerator))
            |> Some


    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------

    let toStringNumberNL p (n: float) = n.ToString("R" + p, CultureInfo.GetCultureInfo("nl"))


    let toStringNumberNLWithoutTrailingZeros =
        toStringNumberNL "" >> String.removeTrailingZerosFromDutchNumber


    let toStringNumberNLWithoutTrailingZerosFixPrecision n =
        fixPrecision n >> toStringNumberNLWithoutTrailingZeros