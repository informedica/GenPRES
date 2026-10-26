namespace Informedica.GenOrder.Lib


/// Which variables of an order a set of kept variables determines. A product equation
/// y = x1 * x2 * .. is linear in the logarithms of its variables, so elimination over the exponents
/// finds every variable the kept variables fix, also when only several equations together fix it; a
/// sum fixes its last open term. Only a kept variable that holds one value fixes others.
module Determination =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenSolver.Lib


    /// The equations of an order: a product equation as exponents, y +1 and every x -1, and a sum
    /// equation as y and the xs, each variable by name.
    type Model =
        {
            /// The variables of the equations.
            Names: Set<Name>
            /// The product equations, and the sums of one term, as exponents by variable.
            Rows: Map<Name, BigRational> list
            /// The sums of more than one term, as the total and its terms.
            Sums: (Name * Name list) list
            /// The variables of the equations that are not variables of the order, such as the
            /// orderable share of 1 the equation mapping adds; they always count as known.
            Constants: Set<Name>
        }


    /// The model of the equations of an order.
    let ofOrder ord =
        let eqs = ord |> Order.equations
        let name = OrderVariable.getName

        let names =
            eqs
            |> List.collect (
                function
                | OrderProductEquation(y, xs)
                | OrderSumEquation(y, xs) -> y :: xs
            )
            |> List.map name
            |> Set.ofList

        {
            Names = names
            Rows =
                eqs
                |> List.choose (
                    function
                    | OrderProductEquation(y, xs) ->
                        (Map.empty |> Map.add (y |> name) 1N, xs)
                        ||> List.fold (fun acc x ->
                            let n = x |> name
                            acc |> Map.add n ((acc |> Map.tryFind n |> Option.defaultValue 0N) - 1N)
                        )
                        |> Some
                    // a sum of one term is the identity y = x, a product equation in the logarithms
                    | OrderSumEquation(y, [ x ]) -> Some(Map [ y |> name, 1N; x |> name, -1N ])
                    | OrderSumEquation _ -> None
                )
            Sums =
                eqs
                |> List.choose (
                    function
                    | OrderSumEquation(y, xs) when xs.Length > 1 -> Some(y |> name, xs |> List.map name)
                    | _ -> None
                )
            Constants = names - (ord |> Order.toOrdVars |> List.map name |> Set.ofList)
        }


    /// The rows in reduced row echelon form.
    let rref (rows: Map<Name, BigRational> list) =
        let rows = rows |> List.filter (Map.isEmpty >> not) |> List.toArray
        let cols = rows |> Array.collect (Map.keys >> Seq.toArray) |> Array.distinct |> Array.sort
        let mutable r = 0

        for c in cols do
            if r < rows.Length then
                match [ r .. rows.Length - 1 ] |> List.tryFind (fun i -> rows[i] |> Map.containsKey c) with
                | None -> ()
                | Some p ->
                    let tmp = rows[r]
                    rows[r] <- rows[p]
                    rows[p] <- tmp
                    let pv = rows[r][c]
                    rows[r] <- rows[r] |> Map.map (fun _ v -> v / pv)

                    for i in 0 .. rows.Length - 1 do
                        if i <> r && rows[i] |> Map.containsKey c then
                            let f = rows[i][c]

                            rows[i] <-
                                (rows[i], rows[r])
                                ||> Map.fold (fun acc k v ->
                                    let nv = (acc |> Map.tryFind k |> Option.defaultValue 0N) - f * v
                                    if nv = 0N then acc |> Map.remove k else acc |> Map.add k nv
                                )

                    r <- r + 1

        rows[.. r - 1] |> Array.toList


    /// The variables the known variables determine: an equation row that keeps one open variable
    /// after the elimination fixes that variable, and a sum with one open term fixes that term; to a
    /// fixpoint.
    let closure m known =
        let rec loop known =
            let byRows =
                m.Rows
                |> List.map (Map.filter (fun k _ -> known |> Set.contains k |> not))
                |> rref
                |> List.choose (fun row ->
                    if row.Count = 1 then
                        row |> Map.keys |> Seq.head |> Some
                    else
                        None
                )
                |> Set.ofList

            let bySums =
                m.Sums
                |> List.choose (fun (y, xs) ->
                    match y :: xs |> List.filter (fun n -> known |> Set.contains n |> not) with
                    | [ n ] -> Some n
                    | _ -> None
                )
                |> Set.ofList

            let next = known + byRows + bySums
            if next = known then known else loop next

        loop (known + m.Constants |> Set.intersect m.Names)


    /// The variables of the equations of an order that the kept variables holding one value do not
    /// determine. A kept variable is never undetermined, also when it holds more than one value.
    let undeterminedBy kept ord =
        let m = ord |> ofOrder

        let solved =
            ord
            |> Order.toOrdVars
            |> List.filter OrderVariable.hasOneValue
            |> List.map OrderVariable.getName
            |> Set.ofList

        m.Names - (kept |> Set.intersect solved |> closure m) - kept
