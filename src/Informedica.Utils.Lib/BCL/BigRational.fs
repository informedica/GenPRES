namespace Informedica.Utils.Lib.BCL


/// Helper functions for `BigRational`
[<RequireQualifiedAccess>]
module BigRational =

    open System
    open MathNet.Numerics

    //----------------------------------------------------------------------------
    // Error management
    //----------------------------------------------------------------------------

    /// Message type to be used when
    /// an exception message is warranted
    type Message =
        | CannotMatchOperator
        | CannotDivideByZero
        | CannotParseString of string


    /// Exception type
    exception BigRationalException of Message



    /// Raise exception with message `m`
    let raiseExc m = m |> BigRationalException |> raise


    //----------------------------------------------------------------------------
    // Identity functions
    //----------------------------------------------------------------------------

    /// Apply a `f` to bigrational `x`
    let apply f (x: BigRational) = f x


    /// Utility to enable type inference
    let id = apply id


    //----------------------------------------------------------------------------
    // Parsing
    //----------------------------------------------------------------------------


    /// Parse a string and pass the result
    /// either to `succ` or `fail` function
    let parseCont succ fail s =
        try
            s
            |> BigRational.Parse
            |> succ
        with
        | _ -> s |> CannotParseString |> fail


    /// Parse a string to a bigrational
    /// Raises an exception `Message` when
    /// the string cannot be parsed
    let parse = parseCont id raiseExc


    /// Try to parse a string and
    /// return `None` if it fails
    /// otherwise `Some` bigrational
    let tryParse =
        parseCont Some (fun _ -> None)


    //----------------------------------------------------------------------------
    // Conversion functions
    //----------------------------------------------------------------------------

    /// Create a BigRational from an int
    let fromInt = BigRational.FromInt


    /// Create a BigRational from an bigint
    let fromBigInt = BigRational.FromBigInt


    /// Create a float from a BigRational
    let toDouble br = BigRational.ToDouble(br)


    /// Fix the precision of a BigRational and
    /// return the result as a float
    let fixPrecision n = toDouble >> (Double.fixPrecision n)


    /// Try to convert a float `f` to
    /// a `BigRational`.
    let fromFloat f =
        f
        |> Double.floatToFract
        |> Option.bind (fun (n, d) -> BigRational.FromBigInt(n) / BigRational.FromBigInt(d) |> Some)


    /// Convert a BigRational to a float
    let toFloat br =
        ((br |> id).Numerator |> float) / (br.Denominator |> float)


    /// Create a BigRational from a decimal
    let fromDecimal = BigRational.FromDecimal


    /// Create a decimal from a BigRational
    let toDecimal = toFloat >> decimal


    /// Get the denominator of a BigRational
    let denominator (br: BigRational) = br.Denominator


    /// Get the numerator of a BigRational
    let numerator (br: BigRational) = br.Numerator


    //----------------------------------------------------------------------------
    // String functions
    //----------------------------------------------------------------------------


    /// Convert a bigrational to a string
    let toString v = (v |> id).ToString()


    /// Convert an optional `BigRational` to a `string`.
    /// If `None` then return empty `string`.
    let optToString = function
        | Some v' -> v' |> toString
        | None    -> ""


    /// Convert a BigRational to a string in Dutch format
    let toStringNl (br : BigRational) =
        if br.Denominator = 1I then
            br |> BigRational.ToInt32 |> Int32.toStringNumberNL
        else
            br |> toFloat |> Double.toStringNumberNLWithoutTrailingZeros


    //----------------------------------------------------------------------------
    // Constants
    //----------------------------------------------------------------------------


    /// Constant 0
    let zero = 0N

    /// Constant 1
    let one = 1N

    /// Constant 2
    let two = 2N

    /// Constant 3
    let three = 3N


    //----------------------------------------------------------------------------
    // Math functions
    //----------------------------------------------------------------------------


    /// Get the greatest common divisor
    /// of two BigRationals `a` and `b`
    let gcd (a : BigRational) (b: BigRational) =
        let den = a.Denominator * b.Denominator
        let num = BigInteger.gcd (a.Numerator * b.Denominator) (b.Numerator * a.Denominator)
        (num |> BigRational.FromBigInt) / (den |> BigRational.FromBigInt)


    (*
    /// Convert `n` to a multiple of `d`.
    /// Passes an `CannotDivideByZero` message
    /// to `fail` when `d` is zero.
    let toMultipleOfCont succ fail d n  =
        if d = 0N then CannotDivideByZero |> fail
        else
            let m = (n / d) |> BigRational.ToBigInt |> BigRational.FromBigInt
            if m * d < n then (m + 1N) * d else m * d
            |> succ


    /// Convert `n` to a multiple of `d`.
    /// Raises an `CannotDivideByZero` message
    /// exception when `d` is zero.
    let toMultipleOf = toMultipleOfCont id raiseExc

    /// Convert `n` to a multiple of `d`.
    /// Returns `None` when `d` is zero.
    let toMultipleOfOpt = toMultipleOfCont Some (fun _ -> None)
    *)


    /// Checks whether `v` is a multiple of `incr`
    let isMultiple (incr : BigRational) (v : BigRational) =
        if incr = 0N then false
        else
            (v.Numerator * incr.Denominator) % (incr.Numerator * v.Denominator) = 0I


    /// Check whether the operator is subtraction
    let opIsSubtr op = (three |> op <| two) = three - two // = 1


    /// Check whether the operator is addition
    let opIsAdd op   = (three |> op <| two) = three + two // = 5


    /// Check whether the operator is multiplication
    let opIsMult op  = (three |> op <| two) = three * two // = 6


    /// Check whether the operator is divsion
    let opIsDiv op   = (three |> op <| two) = three / two // = 3/2


    /// Match an operator `op` to either
    /// multiplication, division, addition
    /// or subtraction. </br>
    /// Returns NoMatch otherwise
    let (|Mult|Div|Add|Subtr|NoMatch|) op =
        match op with
        | _ when op |> opIsMult  -> Mult
        | _ when op |> opIsDiv   -> Div
        | _ when op |> opIsAdd   -> Add
        | _ when op |> opIsSubtr -> Subtr
        | _ -> NoMatch



    /// Perform a calculation when
    /// both `n1` and `n2` are 'some'
    let calculate n1 o n2 =
        match n1, n2 with
        |Some x1, Some x2 -> x1 |> o <| x2 |> Some
        |_ -> None


    //let inline triangular n = (n * (n + (n/n))) / ((n + n) / n)


    /// Calculate the set of possible solutions with a concentration `conc` up
    /// to a maximum value `max` in descending order
    let calcConc max conc =
        seq { for f in (BigInteger.farey max false) do
                let fn, fd = f
                let r = (fn |> BigRational.FromBigInt) / (fd |> BigRational.FromBigInt)
                yield r * conc } |> Seq.cache


    /// Generic function to calculate all divisors
    /// of `n`, using a `modulo` function
    let inline getDivisors modulo zero one two n =
        let n = abs n
        match n with
        | _ when n = zero-> []
        | _ -> List.append ([one..(n/two)] |> List.filter(fun x -> modulo n x = zero)) [n]


    /// Get all divisors of a BigInt
    let divisorsOfBigInt = getDivisors (fun n x -> n % x) 0I 1I 2I


    /// Get all the divisors of a BigRational
    let divisorsOfBigR =
        let modulo =
            fun (n : BigRational) (x : BigRational) ->
                n.Numerator % x.Numerator
                |> BigRational.FromBigInt
        getDivisors modulo 0N 1N 2N

    /// Generic function to check whether a `divisor`
    /// is a divisor of a `dividend`, i.e. the number being
    /// divided
    let inline isDivisor zero dividend divisor =
        dividend % divisor = zero


    /// Check whether a divisor divides a dividend
    let isDivisorOfBigR  (dividend:BigRational) (divisor:BigRational) =
        isDivisor 0I dividend.Numerator divisor.Numerator


    /// Check whether a divisor divides a dividend
    let isDivisorOfBigInt (dividend:bigint) (divisor:bigint) =
        isDivisor 0I dividend divisor


    /// Reduce a ratio where `num` is the
    /// numerator and `denom` is the denominator
    let reduceRatio num denom =
        let n   = num / (gcd num denom)
        let denom = denom / (gcd n denom)
        (n, denom)


    /// Split a rational number in a
    /// numerator and denominator
    let numDenom (v:BigRational) = (v.Numerator |> BigRational.FromBigInt, v.Denominator |> BigRational.FromBigInt)


    /// Calculate a rational factor ratio for a given input value based on conditions.
    ///
    /// This function takes an input value `v` of type `BigRational` and a tuple `r` containing the following components:
    ///   - `n`: An optional numerator.
    ///   - `nIsMult`: A boolean flag indicating whether the numerator should be a multiple of n (`true`) or exact value (`false`).
    ///   - `d`: An optional denominator.
    ///   - `dIsMult`: A boolean flag indicating whether the denominator should be a multiple of d (`true`) or exact value (`false`).
    /// Parameters:
    ///   - v: The input value for which the factor ratio is calculated (of type `BigRational`).
    ///   - r: A tuple containing the conditions and components for calculating the factor ratio.
    /// Returns:
    ///   - If the calculated factor ratio accurately represents the original value, returns `Some (n, d)`.
    ///   - Otherwise, returns `None`.
    let valueToFactorRatio v r =
        let vn, vd = numDenom v
        let toBigR = BigRational.FromBigInt

        match r with
        | Some n, false,  Some d, false ->
            (n, d)
        | Some n, true, Some d, true ->
            let r = (vn * d) / (vd * n)
            ((r.Numerator |> toBigR) * n), ((r.Denominator |> toBigR) * d)
        | None   , _ ,   Some d, false when (vd |> isDivisorOfBigR d) ->
            (vn * (d / vd), d)
        | None   , _ ,   Some d, true ->
            ((d / (gcd d vd)) * vn, (d / (gcd d vd)) * vd)
        | Some n, false,  None, _  when (vn |> isDivisorOfBigR n) ->
            (n, (n / vn) * vd)
        | Some n, true, None, _ ->
            ((n / (gcd n vn)) * vn, (n / (gcd n vn)) * vd)
        | None, _ , None, _ ->
            (vn, vd)
        | _  -> (0N, 0N)
        |> fun (n, d) ->
            if d <> 0N && n / d = v then Some (n, d)
            else None


    /// Calculate a rational factor ratio for a given input value based on conditions.
    let valueToBigIntFactorRatio v r =
        let n, nv, d, dv = r
        let toBigR x = match x with |Some i -> i |> BigRational.FromBigInt |> Some |None -> None
        match (n |> toBigR, nv, d |> toBigR, dv) |> valueToFactorRatio v with
        | None -> None
        | Some (n, d) -> Some (n.Numerator, d.Numerator)


    /// ToDo: doesn't return `NoOp` but fails,
    /// have to rewrite
    ///
    /// Match an operator `op` to either
    /// multiplication, division, addition
    /// or subtraction, returns `NoOp` when
    /// the operation is neither.
    let (|Mul|Div|Add|Sub|) op =
        match op with
        | _ when op |> opIsMult  -> Mul
        | _ when op |> opIsDiv   -> Div
        | _ when op |> opIsAdd   -> Add
        | _ when op |> opIsSubtr -> Sub
        | _ -> failwith "Operator is not supported"


    let calcCartesian op (vs1: BigRational[]) (vs2: BigRational[]) : BigRational[] =
        let n = vs1.Length
        let m = vs2.Length
        
        match n, m with
        | 0, _ | _, 0 -> [||]
        | 1, m ->
            let a = vs1[0]
            Array.init m (fun x -> op a vs2[x])
        | n, 1 ->
            let b0 = vs2[0]
            Array.init n (fun x -> op vs1[x] b0)
        | _ ->
            let res = Array.zeroCreate<_> (n * m)
            if n = 0 || m = 0 then res else
            for i = 0 to n - 1 do
                let v1 = vs1[i]
                let baseIdx = i * m
                for j = 0 to m - 1 do
                    res[baseIdx + j] <- op v1 vs2[j]
            res



    /// Calculates the nearest multiple of the given `multiple` based on whether it's required
    /// to be a minimum or maximum value.
    ///
    /// - Parameters:
    ///   - isMinOrMax: Specifies whether the value should be the minimum or maximum multiple.
    ///   - multiple: The increment value for which to find the nearest multiple.
    ///   - value: The value for which the nearest multiple needs to be calculated.
    ///
    /// - Returns: The calculated nearest multiple value.
    let toMultipleOf isMinOrMax multiple value  =
        if multiple = 0N then value
        else
            let m = (value / multiple) |> BigRational.ToBigInt |> BigRational.FromBigInt
            if isMinOrMax then
                if m * multiple < value then (m + 1N) * multiple else m * multiple
            else
                if m * multiple > value then (m - 1N) * multiple else m * multiple


    /// <summary>
    /// Calculates the smallest multiple of a specified quantity (`multiple`) that is greater
    /// than or equal to the given `value`.
    /// </summary>
    /// <example>
    /// <code>
    /// 3N |> BigRational.toMinMultipleOf 2N = 4N
    /// </code>
    /// </example>
    let toMinMultipleOf = toMultipleOf true


    /// <summary>
    /// Calculates the largest multiple of a specified quantity (`multiple`) that is less than
    /// or equal to the given `value`.
    /// </summary>
    /// <example>
    /// <code>
    /// 3N |> BigRational.toMaxMultipleOf 2N = 2N
    /// </code>
    /// </example>
    let toMaxMultipleOf = toMultipleOf false


    /// <summary>
    /// Calculates the minimum or maximum value that can be obtained by adding (or subtracting)
    /// positive increments to a given starting value (`minOrMax`).
    /// </summary>
    /// <param name="isMax">Specifies whether the maximum value is being calculated.</param>
    /// <param name="isIncl">Indicates whether the result should be inclusive or exclusive of <paramref name="minOrMax"/>.</param>
    /// <param name="incrs">A set of positive increments to consider for adjusting the starting value.</param>
    /// <param name="minOrMax">The starting value for which to calculate the minimum or maximum.</param>
    /// <returns>The calculated minimum or maximum value based on the provided conditions.</returns>
    let calcMinOrMaxToMultiple isMax isIncl incrs minOrMax =
        incrs
        |> Set.filter ((<) 0N) // only accept positive incrs
        |> Set.map (fun i ->
            let ec = if isMax then (>=) else (<=)
            let ad = if isMax then (-) else (+)

            let m =
                if isMax then minOrMax |> toMaxMultipleOf i
                else minOrMax |> toMinMultipleOf i

            if (isIncl |> not) && (m |> ec <| minOrMax) then
                (m |> ad <| i)
            else m
        )
        |> Seq.minBy (fun x -> if isMax then -x else x)


    /// <summary>
    /// Calculates the maximum value (inclusive) that can be obtained by adding positive increments
    /// to a given starting value (`minOrMax`) and returning the smallest result.
    /// </summary>
    /// <example>
    /// <code>
    /// 3N |> BigRational.maxInclMultipleOf (set [2N; 3N; 4N]) = 3N
    /// </code>
    /// </example>
    let maxInclMultipleOf = calcMinOrMaxToMultiple true true


    /// <summary>
    /// Calculates the maximum value (exclusive) that can be obtained by adding positive increments
    /// to a given starting value (`minOrMax`) and returning the smallest result.
    /// </summary>
    /// <example>
    /// <code>
    /// 3N |> BigRational.maxExclMultipleOf (set [2N; 3N]) = 2N
    /// </code>
    /// </example>
    let maxExclMultipleOf = calcMinOrMaxToMultiple true false


    /// <summary>
    /// Calculates the minimum value (inclusive) that can be obtained by adding positive increments
    /// to a given starting value (`minOrMax`) and returning the smallest result.
    /// </summary>
    /// <example>
    /// <code>
    /// 3N |> BigRational.minInclMultipleOf (set [2N; 3N]) = 3N
    /// 3N |> BigRational.minInclMultipleOf (set [2N..2N..10N]) = 4N
    /// </code>
    /// </example>
    let minInclMultipleOf = calcMinOrMaxToMultiple false true


    /// <summary>
    /// Calculates the minimum value (exclusive) that can be obtained by adding positive increments
    /// to a given starting value (`minOrMax`) and returning the smallest result.
    /// </summary>
    /// <example>
    /// <code>
    /// 3N |> BigRational.minExclMultipleOf (set [2N; 3N]) = 4N
    /// </code>
    /// </example>
    let minExclMultipleOf = calcMinOrMaxToMultiple false false


    /// <summary>
    /// Takes a min, set of increments, and max and returns a sequence of all values
    /// that are a multiple of an increment, have a minimum value that is a multiple of that increment
    /// and a maximum value that is a multiple of that increment.
    /// </summary>
    /// <example>
    /// <code>
    /// minIncrMaxToSeq 1N [2N; 3N] 13N
    /// |> Seq.toList
    /// // returns [2N; 3N; 4N; 6N; 8N; 9N; 10N; 12N]
    /// </code>
    /// </example>
    let minIncrMaxToSeq min incr max : BigRational seq =
        incr
        |> Seq.fold (fun acc i ->
            let min = min |> toMinMultipleOf i
            let max = max |> toMaxMultipleOf i
            seq {min..i..max} |> Seq.append acc
        ) Seq.empty
        |> Seq.sort
        |> Seq.distinct


    module Tests =

        open Swensen.Unquote


        // Test valueToFactorRatio
        let testValueToFactorRatio () =
            test <@
                let v = 1N / 3N
                let r : BigRational option * bool * BigRational option * bool= (None, false, None, false)
                match valueToFactorRatio v r with
                | None -> false
                | Some (n, d) -> n = 1N && d = 3N
            @>

            test <@
                let v = 1N / 3N
                let r = (Some 1N, false, Some 3N, false)
                match valueToFactorRatio v r with
                | None -> false
                | Some (n, d) -> n = 1N && d = 3N
            @>

            test <@
                let v = 2N
                let r : BigRational option * bool * BigRational option * bool= (None, false, Some 2N, false)
                match valueToFactorRatio v r with
                | None -> false
                | Some (n, d) -> n = 4N && d = 2N
            @>

            test <@
                let v = 1N/2N
                let r : BigRational option * bool * BigRational option * bool= (None, false, Some 2N, false)
                match valueToFactorRatio v r with
                | None -> false
                | Some (n, d) -> n = 1N && d = 2N
            @>

            test <@
                let v = 3N/10N
                let r : BigRational option * bool * BigRational option * bool= (None, false, Some 2N, false)
                match valueToFactorRatio v r with
                | None -> true
                | Some _ -> false
            @>
