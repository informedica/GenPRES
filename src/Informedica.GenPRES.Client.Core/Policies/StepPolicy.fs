namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what a field's arrows send. The clicks of all four arrows add up to one net count per
/// pair, and the field sends one command for them, so a click is never lost to a request its own
/// field started. While one pair holds clicks, the other pair rests, so the net is always one
/// pair's.
module StepPolicy =


    /// The arrows of a field: the inner pair steps by the increment, the outer pair by the larger
    /// step.
    [<RequireQualifiedAccess>]
    type Pair =
        | Inner
        | Outer


    /// The pair and the net count the field sends for its clicks: positive up, negative down; None
    /// when the clicks cancel out.
    let net (inner: int) (outer: int) =
        match inner, outer with
        | 0, 0 -> None
        | n, 0 -> Some(Pair.Inner, n)
        | 0, n -> Some(Pair.Outer, n)
        // the other pair rests while one holds clicks, so both cannot count
        | _ -> None


    /// Whether the pair rests: while the other pair holds clicks.
    let rests (pair: Pair) (inner: int) (outer: int) =
        match pair with
        | Pair.Inner -> outer <> 0
        | Pair.Outer -> inner <> 0
