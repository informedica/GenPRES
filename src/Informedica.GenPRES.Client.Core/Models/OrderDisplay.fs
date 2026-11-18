namespace Informedica.GenPRES.Client.Core.Models

/// An order with the component and the item a page step works on.
type OrderLoader =
    {
        Component: string option
        Item: string option
        Order: Informedica.GenPRES.Shared.Types.Order
    }


/// Functions over OrderLoader.
module OrderLoader =

    let create cmp itm o =
        {
            Component = cmp
            Item = itm
            Order = o
        }


/// Renders order values as text and tells whether an order variable or an order is solved.
module OrderDisplay =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    module Variable =

        /// Renders a variable as its single value, or as its range, with the unit and precision prec.
        /// Returns an empty string for a variable without values and without a minimum and maximum.
        let renderValue prec (var: Variable) =
            match var.Min, var.Max, var.Vals with
            | _, _, Some vals when vals.Value.Length = 1 ->
                let v = vals.Value |> Array.head |> snd |> Decimal.fixPrecision prec
                $"{v} {vals.Unit}"
            | _, _, Some vals when vals.Value.Length > 1 ->
                let minVal = vals.Value |> Array.minBy snd |> snd |> Decimal.fixPrecision prec
                let maxVal = vals.Value |> Array.maxBy snd |> snd |> Decimal.fixPrecision prec
                $"{minVal} - {maxVal} {vals.Unit}"
            | Some min, Some max, _ ->
                let minVal = min.Value |> Array.minBy snd |> snd |> Decimal.fixPrecision prec
                let maxVal = max.Value |> Array.maxBy snd |> snd |> Decimal.fixPrecision prec
                $"{minVal} - {maxVal} {min.Unit}"
            | _ -> ""


        /// <summary>Renders the variables of the items of one component as one string, grouped by unit.</summary>
        /// <param name="prec">The precision.</param>
        /// <param name="vars">The variables, one per item.</param>
        let renderValues prec (vars: Variable[]) =
            // Splits a rendered variable into its value and its unit. The unit is the last token,
            // because a unit never contains a space.
            let split s =
                let tokens = s |> String.split " " |> Array.filter String.notEmpty

                {|
                    Value = tokens |> Array.truncate (tokens.Length - 1) |> String.concat ""
                    Unit = tokens |> Array.tryLast |> Option.defaultValue ""
                |}

            vars
            |> Array.map (renderValue prec)
            |> Array.filter String.notEmpty
            |> Array.map split
            |> Array.groupBy _.Unit
            |> Array.map (fun (unit, items) ->
                let values = items |> Array.map _.Value
                // A value can be a range, such as 10-20; a spaced separator keeps the ranges apart.
                let sep =
                    if values |> Array.exists (String.contains "-") then
                        " / "
                    else
                        "/"

                $"%s{values |> String.concat sep} %s{unit}"
            )
            |> String.concat ", "


    module OrderVariable =

        let isSolved (ovar: OrderVariable) =
            ovar.Variable.Vals
            |> Option.map (_.Value >> Array.length >> ((=) 1))
            |> Option.defaultValue false


        let isNavigable (ovar: OrderVariable) =
            if ovar |> isSolved then
                false
            else
                // An order variable with an increment always has a minimum, because every order variable
                // is positive and a minimum is a multiple of the increment.
                (ovar.Variable.Max.IsSome && ovar.DefinedConstraints.Incr.IsSome)
                || ovar.Variable.Vals
                   |> Option.map (_.Value >> Array.length >> (fun c -> c > 1))
                   |> Option.defaultValue false


        let displayString (ovar: OrderVariable) =
            ovar.Variable.Vals
            |> Option.bind (fun v ->
                v.Value
                |> Array.tryHead
                |> Option.map (fun (_, d) -> (d |> Decimal.toStringNumberNLWithoutTrailingZeros) + " " + v.Unit)
            )
            |> Option.defaultValue ""


        let displayStringFormatted (format: decimal -> string) (ovar: OrderVariable) =
            ovar.Variable.Vals
            |> Option.bind (fun v ->
                v.Value
                |> Array.tryHead
                |> Option.map (fun (_, d) -> (d |> format) + " " + v.Unit)
            )
            |> Option.defaultValue ""


    let isSolved (ord: Order) =
        [
            yield! ord.Orderable.Components |> Array.map _.OrderableQuantity
            ord.Orderable.OrderableQuantity
            ord.Orderable.Dose.Quantity

            if ord.Schedule.IsContinuous || ord.Schedule.IsOnceTimed || ord.Schedule.IsTimed then
                ord.Orderable.Dose.Rate

            if ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed then
                ord.Schedule.Frequency
        ]
        |> List.forall OrderVariable.isSolved
