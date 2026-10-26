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


/// The order as the pages show it: values rendered, and whether a variable or the order is solved.
module OrderDisplay =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    module Variable =

        /// A variable as one text: its value, or its range, with the unit.
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


        /// <summary>
        /// Render an array of variables as a single string, as used for the
        /// items (substances) of one component in the order plan overview.
        /// </summary>
        /// <param name="prec">The precision</param>
        /// <param name="vars">The variables, one per item</param>
        let renderValues prec (vars: Variable[]) =
            // split a rendered variable in its value part and its unit,
            // the unit is the last token as units never contain a space
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
                // a value can be a range itself, i.e. "10-20", so in that case
                // the items are spaced out to keep the ranges apart visually
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
                // note that an ordervariable with an increment always has a
                // min value by definition as all order variables are initialized to
                // be non-zero positive and a min value is always a multiple of an
                // increment
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
