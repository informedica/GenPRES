/// Decides why the rules mark a value, from the bounds they define and the values the order
/// variable holds.
module SeverityReasonPolicy

open Shared.Types


/// A bound a value crossed.
type Bound =
    {
        /// The bound's value, in the values' unit.
        Value: decimal
        /// The unit of the bound and the values.
        Unit: string
        /// Whether a value on the bound is still allowed.
        Inclusive: bool
    }


/// Why a value is marked.
[<RequireQualifiedAccess>]
type Reason =
    /// The highest value is above the maximum.
    | AboveMax of Bound
    /// The lowest value is below the minimum.
    | BelowMin of Bound
    /// Marked by the server for a reason the bounds do not show, such as a value that is not a
    /// multiple of the increment.
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


/// Why the values fall outside the defined constraints, or None when there are no values, no
/// bound in the values' unit, or the values are within both bounds. The maximum is checked
/// first, so values beyond both bounds are reported as above the maximum.
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


/// Why the order variable is marked: from its constraints when they explain it, else Outside.
/// None when it is not marked.
let ofOrderVariable (ovar: OrderVariable) =
    match ovar.Level with
    | IsNormal -> None
    | IsCaution
    | IsWarning
    | IsAlert ->
        ofConstraints ovar.DefinedConstraints ovar.Variable
        |> Option.orElse (Some Reason.Outside)
