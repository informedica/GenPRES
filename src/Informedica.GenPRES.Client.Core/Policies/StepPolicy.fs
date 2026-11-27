namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what the step arrows of a field send. Clicks add up to one net count per arrow pair, and the
/// field sends one command for that count, so no click is lost while the field waits for its own request.
/// While one pair has clicks, the other pair is inactive, so only one pair has a count.
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
        // Both pairs cannot have clicks, because one pair is inactive while the other has clicks.
        | _ -> None


    /// Whether the pair is inactive: while the other pair has clicks.
    let rests (pair: Pair) (inner: int) (outer: int) =
        match pair with
        | Pair.Inner -> outer <> 0
        | Pair.Outer -> inner <> 0
