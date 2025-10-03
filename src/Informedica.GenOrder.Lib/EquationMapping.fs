namespace Informedica.GenOrder.Lib

module EquationMapping =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime
    open WrappedString


    module Literals =

        let [<Literal>] qty = OrderVariable.Quantity.name
        let [<Literal>] cnc = OrderVariable.Concentration.name
        let [<Literal>] ptm = OrderVariable.PerTime.name
        let [<Literal>] rte = OrderVariable.Rate.name
        let [<Literal>] tot = OrderVariable.Total.name
        let [<Literal>] qtyAdj = OrderVariable.QuantityAdjust.name
        let [<Literal>] ptmAdj = OrderVariable.PerTimeAdjust.name
        let [<Literal>] rteAdj = OrderVariable.RateAdjust.name
        let [<Literal>] totAdj = OrderVariable.TotalAdjust.name
        let [<Literal>] cnt = OrderVariable.Count.name
        let [<Literal>] frq = OrderVariable.Frequency.name
        let [<Literal>] tme = OrderVariable.Time.name
        let [<Literal>] itm = "itm" //Orderable.Literals.item
        let [<Literal>] cmp = "cmp" //Orderable.Literals.comp
        let [<Literal>] orb = "orb" //Orderable.Literals.orderable
        let [<Literal>] dos = "dos" //Orderable.Literals.dose
        let [<Literal>] prs = "prs" //"Prescription"
        let [<Literal>] ord = "ord" // "Order"
        let [<Literal>] adj = "adj" // "Adjust"

        let [<Literal>] discontinuous = 3
        let [<Literal>] continuous = 4
        let [<Literal>] timed = 5
        let [<Literal>] once = 6
        let [<Literal>] onceTimed = 7


    // Get the equations from a Google spreadsheet
    let private getEquations_ indx =
        Web.getDataFromGenPres "Equations"
        |> Array.skip 1
        // only pick those equations that are marked with an 'x'
        |> Array.filter (fun xs -> xs[indx] = "x")
        |> Array.map (Array.item 1)
        |> Array.toList


    /// <summary>
    /// Get a string list of Equations and
    /// use an index to filter out the relevant equations
    /// </summary>
    /// <param name="indx">The index to filter the equations</param>
    /// <remarks>
    /// The indx can be 3 for discontinuous equations, 4 for continuous
    /// and 5 for timed equations.
    /// </remarks>
    let getEquations indx =
        indx
        |> Memoization.memoize getEquations_


    /// <summary>
    /// Create an Equations mapping for an `Order`
    /// </summary>
    /// <param name="ord">The Order to Map</param>
    /// <param name="eqs">The equations as a string list</param>
    /// <returns>
    /// A tuple of `SumMapping` and `ProductMapping`
    /// </returns>
    let getEqsMapping (ord: Order) (eqs : string list) =
        let sumEqs =
            eqs
            |> List.filter (String.contains "sum")

        let prodEqs =
            eqs
            |> List.filter (String.contains "sum" >> not)

        let itmEqs =
            prodEqs
            |> List.filter (String.contains "[itm]")

        let cmpEqs =
            prodEqs
            |> List.filter (fun e ->
                itmEqs
                |> List.exists ((=) e)
                |> not &&
                e.Contains("[cmp]")
            )

        let orbEqs =
            prodEqs
            |> List.filter (fun e ->
                itmEqs
                |> List.exists ((=) e)
                |> not &&
                cmpEqs
                |> List.exists((=) e)
                |> not
            )

        let idN = [ord.Id |> Id.toString] |> Name.create
        let orbN = [ord.Id |> Id.toString; ord.Orderable.Name |> Name.toString] |> Name.create

        ord.Orderable.Components
        |> List.fold (fun acc c ->
            let cmpN =
                [
                    yield! orbN |> Name.toStringList
                    c.Name |> Name.toString
                ]
                |> Name.create

            let itms =
                c.Items
                |> List.collect (fun i ->
                    itmEqs
                    |> List.map (fun s ->
                        let itmN =
                            [
                                yield! cmpN |> Name.toStringList
                                i.Name |> Name.toString
                            ]
                            |> Name.create
                        s
                        |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                        |> String.replace "[itm]" $"{itmN |> Name.toString}"
                    )
                )

            let cmps =
                cmpEqs
                |> List.map (String.replace "[cmp]" $"{cmpN |> Name.toString}")

            acc
            |> List.append cmps
            |> List.append itms
        ) []
        |> fun es ->
            let sumEqs =
                sumEqs
                |> List.map (fun e ->
                    match e
                          |> String.replace "sum(" ""
                          |> String.replace ")" ""
                          |> String.split " = " with
                    | [lv; rv] ->
                        ord.Orderable.Components
                        |> List.map(fun c ->
                            let cmpN =
                                [
                                    yield! orbN |> Name.toStringList
                                    c.Name |> Name.toString
                                ]
                                |> Name.create

                            rv
                            |> String.replace "[cmp]" $"{cmpN |> Name.toString}"
                        )
                        |> String.concat " + "
                        |> fun s -> $"{lv} = {s}"
                    | _ ->
                        writeErrorMessage $"could not match {e}"
                        ""
                )
                |> List.filter (String.isNullOrWhiteSpace >> not)
                |> List.map (String.replace "[orb]" $"{orbN |> Name.toString}")
                |> SumMapping

            let prodEqs =
                es
                |> List.append orbEqs
                |> List.append es
                |> List.map (String.replace "[orb]" $"{orbN |> Name.toString}")
                |> List.map (String.replace "[ord]" $"{idN |> Name.toString}")
                |> List.distinct
                |> ProductMapping

            sumEqs, prodEqs