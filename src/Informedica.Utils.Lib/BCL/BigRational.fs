namespace Informedica.Utils.Lib.BCL


/// Helper functions for `BigRational`
[<RequireQualifiedAccess>]
module BigRational =

    open System
    open MathNet.Numerics


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


    /// Apply a `f` to bigrational `x`
    let apply f (x: BigRational) = f x


    /// Utility to enable type inference
    let id = apply id


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


    /// Create a bigrational from an int
    let fromInt = BigRational.FromInt


    let fromBigInt = BigRational.FromBigInt


    /// Get the greatest common divisor
    /// of two bigrationals `a` and `b`
    let gcd (a : BigRational) (b: BigRational) =
        let den = a.Denominator * b.Denominator
        let num = BigInteger.gcd (a.Numerator * b.Denominator) (b.Numerator * a.Denominator)
        (num |> BigRational.FromBigInt) / (den |> BigRational.FromBigInt)


    /// Convert a bigrational to a string
    let toString v = (v |> id).ToString()


    let toDouble br = BigRational.ToDouble(br)


    let fixPrecision n = toDouble >> (Double.fixPrecision n)


    /// Convert an optional `BigRational` to a `string`.
    /// If `None` then return empty `string`.
    let optToString = function
        | Some v' -> v' |> toString
        | None    -> ""


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


    /// Checks whether `v` is a multiple of `incr`
    let isMultiple (incr : BigRational) (v : BigRational) =
        if incr = 0N then false
        else
            (v.Numerator * incr.Denominator) % (incr.Numerator * v.Denominator) = 0I

    /// Constant 0
    let zero = 0N

    /// Constant 1
    let one = 1N

    /// Constant 2
    let two = 2N

    /// Constant 3
    let three = 3N

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


    /// Try to convert a float `f` to
    /// a `BigRational`.
    let fromFloat f =
        f
        |> Double.floatToFract
        |> Option.bind (fun (n, d) -> BigRational.FromBigInt(n) / BigRational.FromBigInt(d) |> Some)


    /// Convert a BigRational to a float
    let toFloat br =
        ((br |> id).Numerator |> float) / (br.Denominator |> float)


    let fromDecimal = BigRational.FromDecimal


    let toDecimal = toFloat >> decimal


    /// Perform a calculation when
    /// both `n1` and `n2` are 'some'
    let calculate n1 o n2 =
        match n1, n2 with
        |Some x1, Some x2 -> x1 |> o <| x2 |> Some
        |_ -> None


    //let inline triangular n = (n * (n + (n/n))) / ((n + n) / n)


    /// Calculate an ordered farey sequence
    /// Calculate an ordered farey sequence
    let farey n asc =
        seq {
            let p = if asc then ref 0I else ref 1I
            let q = ref 1I
            let p' = if asc then ref 1I else ref (n - 1I)
            let q' = ref n
            yield (p.Value, q.Value)
            while (asc && not (p.Value = 1I && q.Value = 1I)) ||
                  (not asc && p.Value > 0I) do
                let c = (q.Value + n) / q'.Value
                let p'' = c * p'.Value - p.Value
                let q'' = c * q'.Value - q.Value
                p.Value <- p'.Value
                q.Value <- q'.Value
                p'.Value <- p''
                q'.Value <- q''
                yield (p.Value, q.Value) }

    /// Calculate the set of possible solutions with a concentration `conc` up
    /// to a maximum value `max` in descending order
    let calcConc max conc =
        seq { for f in (farey max false) do
                let fn, fd = f
                let r = (fn |> BigRational.FromBigInt) / (fd |> BigRational.FromBigInt)
                yield r * conc } |> Seq.cache


    // let rec BigPow (a:bigint) (p:bigint) :bigint =
    //   match p with
    //     | _ when (p = 0I) -> 1I
    //     | _ when (p >= 1I) -> a * (BigPow (a) (p - 1I))
    //     | _ -> failwith "Shouldn't Happen"


    // let ( ** ) : bigint -> bigint -> bigint = BigPow


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


    [<Obsolete("use numDenom")>]
    let numdenomRatio (v:BigRational) = (v.Numerator |> BigRational.FromBigInt, v.Denominator |> BigRational.FromBigInt)


    let valueToFactorRatio v r =
        let vn, vd = numDenom v
        let toBigR = BigRational.FromBigInt

        match r with
        | Some n, true,  Some d, true  -> (n, d)
        | Some n, false, Some d, false ->
            let r = (vn * d) / (vd * n)
            ((r.Numerator |> toBigR) * n), ((r.Denominator |> toBigR) * d)
        | None   , _ ,   Some d, true when (vd |> isDivisorOfBigR d) -> (vn * (d / vd), d)
        | None   , _ ,   Some d, false                           ->
            ((d / (gcd d vd)) * vn, (d / (gcd d vd)) * vd)
        | Some n, true,  None,   _   when (vn |> isDivisorOfBigR n) ->
            (n, (n / vn) * vd)
        | Some n, false, None,   _  -> ((n / (gcd n vn)) * vn, (n / (gcd n vn)) * vd)
        | None,   _ ,    None,   _  -> (vn, vd)
        | _  -> (0N, 0N)


    let valueToFactorRatio2 v r =
        let n, nv, d, dv = r
        let toBigR x = match x with |Some i -> i |> BigRational.FromBigInt |> Some |None -> None
        let n, d = (n |> toBigR, nv, d |> toBigR, dv) |> valueToFactorRatio v
        (n.Numerator, d.Numerator)


    let toNumListDenom (vl: BigRational list) =
        let d =
            vl |> List.map(fun v -> v.Denominator)
            |> Seq.distinct
            |> Seq.toList
            |> Seq.fold(fun p d -> d * p) 1I
            |> BigRational.FromBigInt
        (vl |> List.map(fun v -> v * d), d)


    /// ToDo: doesn't return `NoOp` but fails,
    /// have to rewrite
    ///
    /// Match an operator `op` to either
    /// multiplication, division, addition
    /// or subtraction, returns `NoOp` when
    /// the operation is neither.
    let (|Mult|Div|Add|Subtr|) op =
        match op with
        | _ when op |> opIsMult  -> Mult
        | _ when op |> opIsDiv   -> Div
        | _ when op |> opIsAdd   -> Add
        | _ when op |> opIsSubtr -> Subtr
        | _ -> failwith "Operator is not supported"


    let private toMultipleOf2 b d n  =
        if d = 0N then n
        else
            let m = (n / d) |> BigRational.ToBigInt |> BigRational.FromBigInt
            if b then
                if m * d < n then (m + 1N) * d else m * d
            else
                if m * d > n then (m - 1N) * d else m * d


    let toMinMultipleOf = toMultipleOf2 true


    let toMaxMultipleOf = toMultipleOf2 false


    let calcMinOrMaxToMultiple isMax isIncl incrs minOrMax =
        incrs
        |> Set.filter ((<) 0N) // only accept positive incrs
        |> Set.fold (fun (b, acc) i ->
            let ec = if isMax then (>=) else (<=)
            let nc = if isMax then (>) else (<)
            let ad = if isMax then (-) else (+)

            let m =
                if isMax then minOrMax |> toMaxMultipleOf i
                else minOrMax |> toMinMultipleOf i

            let m =
                if (isIncl |> not) && (m |> ec <| minOrMax) then
                    (m |> ad <| i)
                else m

            match acc with
            | Some a -> if (m |> nc <| a) then (true, Some m) else (b, Some a)
            | None   -> (true, Some m)
        ) (isIncl, None)
        |> fun (b, r) -> b, r |> Option.defaultValue minOrMax


    let maxInclMultipleOf = calcMinOrMaxToMultiple true true

    let maxExclMultipleOf = calcMinOrMaxToMultiple true false

    let minInclMultipleOf = calcMinOrMaxToMultiple false true

    let minExclMultipleOf = calcMinOrMaxToMultiple false false


    let toStringNl (br : BigRational) =
        if br.Denominator = 1I then
            br |> BigRational.ToInt32 |> Int32.toStringNumberNL
        else
            br |> toFloat |> Double.toStringNumberNLWithoutTrailingZeros


    let denominator (br: BigRational) = br.Denominator

    let numerator (br: BigRational) = br.Numerator
