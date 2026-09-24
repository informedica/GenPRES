/// Why a value is marked, read from the bounds the rules define and the values the variable
/// has, both of which the client receives with every order variable. Pure F#, no React, so it
/// runs under Expecto; the mark shows what it answers.
module SeverityReason

open Shared.Types


/// A bound a value crossed: what it is, in what unit, and whether the bound itself is still
/// allowed.
type Bound =
    {
        /// The bound's value, in the values' unit.
        Value: decimal
        /// The unit, the values' own.
        Unit: string
        /// Whether a value on the bound is still allowed.
        Inclusive: bool
    }


/// What a value crossed: the maximum the rules define, their minimum, or something the bounds
/// do not show, such as an increment the value is not a multiple of.
[<RequireQualifiedAccess>]
type Reason =
    /// The values' top is over the maximum.
    | AboveMax of Bound
    /// The values' bottom is under the minimum.
    | BelowMin of Bound
    /// Marked by the server, for a reason the bounds do not show.
    | Outside


let private bound inclusive (vu: ValueUnit) (pick: (string * decimal)[] -> decimal) =
    match vu.Value with
    | [||] -> None
    | values ->
        Some
            {
                Value = values |> pick
                Unit = vu.Unit
                Inclusive = inclusive
            }


let private highest (values: (string * decimal)[]) = values |> Array.maxBy snd |> snd
let private lowest (values: (string * decimal)[]) = values |> Array.minBy snd |> snd


/// Why the variable's values stand outside the constraints defined for it, or nothing when the
/// client cannot tell: no values, no bound in the values' unit, or values within both bounds.
/// The highest value against the maximum first, then the lowest against the minimum, so a
/// range that spills over both is reported by its top.
let ofConstraints (defined: Variable) (var: Variable) =
    match var.Vals with
    | None -> None
    | Some vals when vals.Value |> Array.isEmpty -> None
    | Some vals ->
        let sameUnit (b: ValueUnit) = b.Unit = vals.Unit

        let max =
            defined.Max
            |> Option.filter sameUnit
            |> Option.bind (fun m -> bound defined.MaxIncl m lowest)

        let min =
            defined.Min
            |> Option.filter sameUnit
            |> Option.bind (fun m -> bound defined.MinIncl m highest)

        let top = vals.Value |> highest
        let bottom = vals.Value |> lowest

        let above (b: Bound) = if b.Inclusive then top > b.Value else top >= b.Value
        let below (b: Bound) = if b.Inclusive then bottom < b.Value else bottom <= b.Value

        match max, min with
        | Some b, _ when above b -> Some(Reason.AboveMax b)
        | _, Some b when below b -> Some(Reason.BelowMin b)
        | _ -> None


/// The reason for a marked order variable: read from its defined constraints when they explain
/// the mark, and Outside when the server marked it and the bounds do not say why.
let ofOrderVariable (ovar: OrderVariable) =
    match ovar.Level with
    | IsNormal -> None
    | IsCaution
    | IsWarning
    | IsAlert ->
        ofConstraints ovar.DefinedConstraints ovar.Variable
        |> Option.orElse (Some Reason.Outside)
