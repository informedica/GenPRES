namespace Informedica.Utils.Lib.BCL

open System
open System.Numerics


/// <summary>
/// Internal spill-tier alias. <c>RationalX</c> keeps exact rationals in an
/// <c>int64</c> fast path and only falls back to this MathNet
/// <c>BigRational</c> when an intermediate overflows <c>int64</c>.
/// </summary>
type MBR = MathNet.Numerics.BigRational


/// <summary>
/// Heap cell holding a spilled (big) rational. Used as <c>RationalX</c>'s big
/// tier; its presence (non-null) doubles as the small/big discriminator, which
/// is what lets <c>RationalX</c> avoid a DU tag word and stay 24 bytes wide.
/// </summary>
[<Sealed; AllowNullLiteral>]
type BigCell(value: MBR) =
    member _.Value = value


[<AutoOpen>]
module private RationalXHelpers =

    let inline absL (a: int64) = if a < 0L then -a else a

    /// <summary>
    /// Fold an int64 into an int32 hash, mixing the upper 32 bits in (a plain
    /// <c>int</c> cast would discard them, collapsing values that differ only
    /// above bit 32 into the same bucket).
    /// </summary>
    let inline hash64 (x: int64) = int (x ^^^ (x >>> 32))

    /// Greatest common divisor of two int64 values (always non-negative).
    let rec gcd64 (a: int64) (b: int64) : int64 =
        if b = 0L then absL a else gcd64 b (a % b)

    /// Normalize an int64 fraction: reduce by gcd, force a positive denominator.
    let normPair (p: int64) (q: int64) : int64 * int64 =
        if q = 0L then
            raise (DivideByZeroException())

        let g = gcd64 p q
        let g = if g = 0L then 1L else g
        let p, q = p / g, q / g
        if q < 0L then (-p, -q) else (p, q)

    /// Build an MBR from an int64 pair (MBR normalizes itself).
    let sToLarge (p: int64) (q: int64) : MBR =
        MBR.FromBigIntFraction(bigint p, bigint q)

    let minB = BigInteger Int64.MinValue
    let maxB = BigInteger Int64.MaxValue
    let inline fitsI64 (b: BigInteger) = b >= minB && b <= maxB

    // branchless overflow detection: no try/with region, no Int128

    /// Multiply with overflow check. Returns the product when it fits int64.
    let mulFits (a: int64) (b: int64) : int64 voption =
        let mutable low = 0L
        let high = Math.BigMul(a, b, &low)
        if high = (low >>> 63) then ValueSome low else ValueNone

    /// Add with overflow check (sign-bit test).
    let addFits (a: int64) (b: int64) : int64 voption =
        let s = a + b

        if ((a ^^^ s) &&& (b ^^^ s)) < 0L then
            ValueNone
        else
            ValueSome s

    /// Subtract with overflow check (sign-bit test).
    let subFits (a: int64) (b: int64) : int64 voption =
        let s = a - b

        if ((a ^^^ b) &&& (a ^^^ s)) < 0L then
            ValueNone
        else
            ValueSome s


/// <summary>
/// A two-tier exact rational packed into 24 bytes: two <c>int64</c>s for the
/// reduced small-tier numerator/denominator, and a nullable <c>BigCell</c>
/// reference for the rare spill tier. <c>Big = null</c> means small (value
/// <c>P/Q</c>); a non-null <c>Big</c> means spilled (value <c>Big.Value</c>).
/// </summary>
/// <remarks>
/// Hand-rolled (rather than a struct DU) so the spill reference itself is the
/// small/big discriminator — no DU tag word, so every value is 24 bytes and the
/// common small case is still inline and allocation-free. There is duplicate logic
/// for handling spill/non-spill cases, however, for performance reasons this is
/// accepted.
///
/// Canonical-representation invariant: any value that fits <c>int64</c> is
/// ALWAYS stored small (<c>Big = null</c>), never spilled. Every constructor and
/// arithmetic result funnels through <c>OfLarge</c> (which re-narrows) or
/// <c>normPair</c>. This guarantees a single representation per value, so
/// equality and hashing are consistent across the two tiers.
/// </remarks>
[<Struct; CustomEquality; CustomComparison>]
type RationalX =

    // small tier: reduced, Q > 0 (the zero-initialized default 0/0 is guarded)
    val internal P: int64
    val internal Q: int64
    // big tier: null => small (use P/Q); non-null => spilled (use Big.Value)
    val internal Big: BigCell

    internal new(p: int64, q: int64) =
        {
            P = p
            Q = q
            Big = null
        }

    internal new(cell: BigCell) =
        {
            P = 0L
            Q = 0L
            Big = cell
        }

    member internal this.IsSmall = isNull this.Big

    /// True when the value spilled out of the int64 fast path.
    member this.IsSpilled = not (isNull this.Big)

    // --- canonical constructors ---------------------------------------------

    /// Re-narrow an MBR to the small tier whenever it fits int64. Linchpin of
    /// the canonical-representation invariant.
    static member OfLarge(x: MBR) : RationalX =
        if fitsI64 x.Numerator && fitsI64 x.Denominator then
            RationalX(int64 x.Numerator, int64 x.Denominator) // MBR is already reduced, den > 0
        else
            RationalX(BigCell x)

    static member OfFraction(n: int64, d: int64) =
        // normPair sign-normalizes a negative denominator via (-p, -q); that
        // negation overflows when n or d is Int64.MinValue (no positive int64
        // counterpart exists), so route those through the big tier, which
        // re-narrows if the reduced result happens to fit.
        if n = Int64.MinValue || d = Int64.MinValue then
            RationalX.OfLarge(sToLarge n d)
        else
            let n, d = normPair n d in RationalX(n, d)

    /// Build a small-tier value from a raw numerator/denominator whose
    /// denominator may be negative, normalizing the sign. Spills to the big
    /// tier when sign-flipping would overflow int64 (n or m = Int64.MinValue).
    static member internal OfSmallSigned(n: int64, m: int64) : RationalX =
        if m < 0L then
            if n = Int64.MinValue || m = Int64.MinValue then
                RationalX.OfLarge(sToLarge n m)
            else
                RationalX(-n, -m)
        else
            RationalX(n, m)

    static member Zero = RationalX(0L, 1L)
    static member One = RationalX(1L, 1L)

    // --- conversions (the drop-in surface) ----------------------------------

    static member FromInt(x: int) = RationalX(int64 x, 1L)

    static member FromInt64Fraction(n: int64, d: int64) = RationalX.OfFraction(n, d)

    static member FromIntFraction(n: int, d: int) = RationalX.OfFraction(int64 n, int64 d)

    static member FromBigInt(z: bigint) =
        if fitsI64 z then
            RationalX(int64 z, 1L)
        else
            RationalX(BigCell(MBR.FromBigInt z))

    static member FromDecimal(d: decimal) = RationalX.OfLarge(MBR.FromDecimal d)

    static member ToDouble(r: RationalX) =
        if r.IsSmall then
            float r.P / float r.Q
        else
            MBR.ToDouble r.Big.Value

    // delegate to MBR so rounding (floor toward -inf) matches the original exactly
    static member ToBigInt(r: RationalX) : bigint =
        if r.IsSmall then
            MBR.ToBigInt(sToLarge r.P r.Q)
        else
            MBR.ToBigInt r.Big.Value

    static member ToInt32(r: RationalX) : int =
        if r.IsSmall then
            MBR.ToInt32(sToLarge r.P r.Q)
        else
            MBR.ToInt32 r.Big.Value

    static member Abs(r: RationalX) : RationalX =
        if r.IsSmall then
            // absL Int64.MinValue stays Int64.MinValue (still negative); the
            // positive magnitude does not fit int64, so spill to the big tier.
            if r.P = Int64.MinValue then
                RationalX.OfLarge(-(sToLarge r.P r.Q)) // P < 0, so this is |value|
            else
                RationalX(absL r.P, r.Q) // Q is already > 0
        else
            let v = r.Big.Value
            if v.Numerator.Sign < 0 then RationalX.OfLarge(-v) else r

    static member Parse(s: string) : RationalX = RationalX.OfLarge(MBR.Parse s)

    // --- numerator / denominator (return bigint, MBR sign convention) --------

    member this.Numerator: bigint =
        if this.IsSmall then
            bigint this.P
        else
            this.Big.Value.Numerator

    member this.Denominator: bigint =
        if this.IsSmall then
            bigint this.Q
        else
            this.Big.Value.Denominator

    // --- arithmetic (cross-reduced hot path, spill to MBR) -------------------

    static member (*)(x: RationalX, y: RationalX) : RationalX =
        if x.IsSmall && y.IsSmall then
            let a, b, c, d = x.P, x.Q, y.P, y.Q
            let g1 = let g = gcd64 a d in if g = 0L then 1L else g
            let g2 = let g = gcd64 c b in if g = 0L then 1L else g
            let a2, d2 = a / g1, d / g1
            let c2, b2 = c / g2, b / g2

            match mulFits a2 c2, mulFits b2 d2 with
            | ValueSome n, ValueSome m -> RationalX.OfSmallSigned(n, m)
            | _ -> RationalX.OfLarge(sToLarge a2 b2 * sToLarge c2 d2)
        elif x.IsSmall then
            RationalX.OfLarge(sToLarge x.P x.Q * y.Big.Value)
        elif y.IsSmall then
            RationalX.OfLarge(x.Big.Value * sToLarge y.P y.Q)
        else
            RationalX.OfLarge(x.Big.Value * y.Big.Value)

    static member (/)(x: RationalX, y: RationalX) : RationalX =
        if x.IsSmall && y.IsSmall then
            let a, b, c, d = x.P, x.Q, y.P, y.Q

            if c = 0L then
                raise (DivideByZeroException())

            let g1 = let g = gcd64 a c in if g = 0L then 1L else g
            let g2 = let g = gcd64 d b in if g = 0L then 1L else g
            let a2, c2 = a / g1, c / g1
            let d2, b2 = d / g2, b / g2

            match mulFits a2 d2, mulFits b2 c2 with
            | ValueSome n, ValueSome m -> RationalX.OfSmallSigned(n, m)
            | _ -> RationalX.OfLarge(sToLarge a2 b2 / sToLarge c2 d2)
        elif x.IsSmall then
            RationalX.OfLarge(sToLarge x.P x.Q / y.Big.Value)
        elif y.IsSmall then
            RationalX.OfLarge(x.Big.Value / sToLarge y.P y.Q)
        else
            RationalX.OfLarge(x.Big.Value / y.Big.Value)

    static member (+)(x: RationalX, y: RationalX) : RationalX =
        if x.IsSmall && y.IsSmall then
            let a, b, c, d = x.P, x.Q, y.P, y.Q
            let g = let g = gcd64 b d in if g = 0L then 1L else g
            let bg, dg = b / g, d / g

            match mulFits a dg, mulFits c bg, mulFits b dg with
            | ValueSome t1, ValueSome t2, ValueSome den ->
                match addFits t1 t2 with
                | ValueSome num -> let n, m = normPair num den in RationalX(n, m)
                | ValueNone -> RationalX.OfLarge(sToLarge a b + sToLarge c d)
            | _ -> RationalX.OfLarge(sToLarge a b + sToLarge c d)
        elif x.IsSmall then
            RationalX.OfLarge(sToLarge x.P x.Q + y.Big.Value)
        elif y.IsSmall then
            RationalX.OfLarge(x.Big.Value + sToLarge y.P y.Q)
        else
            RationalX.OfLarge(x.Big.Value + y.Big.Value)

    static member (-)(x: RationalX, y: RationalX) : RationalX =
        if x.IsSmall && y.IsSmall then
            let a, b, c, d = x.P, x.Q, y.P, y.Q
            let g = let g = gcd64 b d in if g = 0L then 1L else g
            let bg, dg = b / g, d / g

            match mulFits a dg, mulFits c bg, mulFits b dg with
            | ValueSome t1, ValueSome t2, ValueSome den ->
                match subFits t1 t2 with
                | ValueSome num -> let n, m = normPair num den in RationalX(n, m)
                | ValueNone -> RationalX.OfLarge(sToLarge a b - sToLarge c d)
            | _ -> RationalX.OfLarge(sToLarge a b - sToLarge c d)
        elif x.IsSmall then
            RationalX.OfLarge(sToLarge x.P x.Q - y.Big.Value)
        elif y.IsSmall then
            RationalX.OfLarge(x.Big.Value - sToLarge y.P y.Q)
        else
            RationalX.OfLarge(x.Big.Value - y.Big.Value)

    static member (~-)(x: RationalX) : RationalX =
        if x.IsSmall then
            // -Int64.MinValue overflows back to Int64.MinValue; its positive
            // counterpart does not fit int64, so spill to the big tier.
            if x.P = Int64.MinValue then
                RationalX.OfLarge(-(sToLarge x.P x.Q))
            else
                RationalX(-x.P, x.Q) // Q stays > 0
        else
            // negating a spilled value can land back inside int64 (e.g. +2^63 ->
            // -2^63); funnel through OfLarge so it re-narrows and the canonical
            // small representation (hence hashing) stays consistent.
            RationalX.OfLarge(-x.Big.Value)

    // --- comparison / equality / hash ---------------------------------------

    /// Small-tier pair with the zero-initialized default (Q = 0) normalized to 0/1.
    static member inline private SmallPair(r: RationalX) : struct (int64 * int64) =
        if r.Q = 0L then struct (0L, 1L) else struct (r.P, r.Q)

    static member Compare(x: RationalX, y: RationalX) : int =
        if x.IsSmall && y.IsSmall then
            let struct (a, b) = RationalX.SmallPair x
            let struct (c, d) = RationalX.SmallPair y

            match mulFits a d, mulFits c b with // b, d > 0 so direction is preserved
            | ValueSome l, ValueSome r -> compare l r
            | _ -> compare (sToLarge a b) (sToLarge c d)
        elif x.IsSmall then
            let struct (a, b) = RationalX.SmallPair x
            compare (sToLarge a b) y.Big.Value
        elif y.IsSmall then
            let struct (c, d) = RationalX.SmallPair y
            compare x.Big.Value (sToLarge c d)
        else
            compare x.Big.Value y.Big.Value

    member this.Equals(r: RationalX) = RationalX.Compare(this, r) = 0

    override this.Equals(o: obj) =
        match o with
        | :? RationalX as r -> RationalX.Compare(this, r) = 0
        | _ -> false

    override this.GetHashCode() =
        if this.IsSmall then
            let struct (p, q) = RationalX.SmallPair this
            // fold the full int64 in; a plain (int p) cast would drop the upper
            // 32 bits and collide values that differ only above bit 32
            if q = 1L then hash64 p else (hash64 p <<< 3) + hash64 q
        else
            // spilled values never fit int64, so they cannot collide-by-value with a small one
            let v = this.Big.Value
            let n = v.Numerator
            let d = v.Denominator

            if d.IsOne then
                n.GetHashCode()
            else
                (n.GetHashCode() <<< 3) + d.GetHashCode()

    interface IEquatable<RationalX> with
        member this.Equals(r: RationalX) = RationalX.Compare(this, r) = 0

    interface IComparable with
        member this.CompareTo(o: obj) =
            match o with
            | :? RationalX as r -> RationalX.Compare(this, r)
            | _ -> invalidArg "o" "not a RationalX"

    interface System.IComparable<RationalX> with
        member this.CompareTo(r: RationalX) = RationalX.Compare(this, r)

    override this.ToString() =
        if this.IsSmall then
            let struct (p, q) = RationalX.SmallPair this
            if q = 1L then string p else $"{p}/{q}"
        else
            this.Big.Value.ToString()


/// Numeric literal support so `0N`, `1N`, `1000N`, ... produce `RationalX`.
[<AutoOpen>]
module NumericLiteralN =

    let FromZero () = RationalX.Zero
    let FromOne () = RationalX.One
    let FromInt32 (i: int) = RationalX.FromInt i
    let FromInt64 (i: int64) = RationalX.FromInt64Fraction(i, 1L)
    let FromString (s: string) = RationalX.Parse s


/// Drop-in alias: existing code that refers to the `BigRational` type now uses
/// the faster two-tier `RationalX`. Coexists with the `BigRational` module.
type BigRational = RationalX
