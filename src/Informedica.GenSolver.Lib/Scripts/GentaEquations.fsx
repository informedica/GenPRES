// throw this line first to the fsi

#load "../Scripts/load.fsx"

#time

open Informedica.GenSolver.Lib

open System
open System.IO

open Informedica.Utils.Lib.BCL
open MathNet.Numerics


module Name = Variable.Name
module ValueRange = Variable.ValueRange

module Minimum = ValueRange.Minimum
module Maximum = ValueRange.Maximum
module ValueSet = ValueRange.ValueSet


open Informedica.Utils.Lib


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__



module Solve =

    let procss s =
        File.AppendAllLines("order.log", [s])
        $"{s} " |> printfn "%s"


    let printEqs = Solver.printEqs true procss
    let solve n p eqs =
        let logger =
            fun s ->
                File.AppendAllLines("order.log", [s])
            |> SolverLogging.logger
        let n = n |> Name.createExc
        try
            Api.solve true Solver.sortQue logger n p eqs
            |> Result.get
        with
        | _ ->
            procss $"cannot set {n |> Name.toString} with {p}"
            eqs


    let solveMinIncl n min = solve n (min |> Minimum.create true |> MinProp)
    let solveMinExcl n min = solve n (min |> Minimum.create false |> MinProp)
    let solveMaxIncl n max = solve n (max |> Maximum.create true |> MaxProp)
    let solveMaxExcl n max = solve n (max |> Maximum.create false |> MaxProp)
    let solveValues n vals = solve n (vals |> ValueSet.create |> ValsProp)


    let init     = Api.init
    let nonZeroNegative = Api.nonZeroNegative


    type Take = | TakeFromMin of int | TakeFromMax of int


    let pick s incr take (eqs : Equation list) =
        let var =
            eqs
            |> List.collect Equation.toVars
            |> List.tryFind (fun v ->
                v
                |> Variable.getName
                |> Name.toString
                |> fun x -> x = s
            )
            |> Option.get

        match var.Values |> ValueRange.getValSet with
        | None    ->
            match incr with
            | None -> eqs
            | Some incr ->
                let min, max =
                    var.Values |> ValueRange.getMin,
                    var.Values |> ValueRange.getMax
                match min, max with
                | Some min, Some max ->
                    let (incl_min, min), (incl_max, max) =
                        min |> ValueRange.Minimum.toBoolBigRational,
                        max |> ValueRange.Maximum.toBoolBigRational
                    let min =
                        if min = 0N then incr
                        else
                            let m = min |> BigRational.toMultipleOf incr
                            if incl_min || m > min then m
                            else
                                m + incr
                    let max =
                        let m = max |> BigRational.toMultipleOf incr
                        if incl_max || m < max then m
                        else
                            m - incr
                    let vs =
                        take
                        |> function
                        | TakeFromMin x -> [min..incr..max] |> List.take x
                        | TakeFromMax x -> [min..incr..max] |> List.rev |> List.take x
                    eqs |> solveValues s vs
                | _ -> eqs
        | Some (ValueSet vs) ->
            match take with
            | TakeFromMin x ->
                    let x = if x > vs.Count then vs.Count else x
                    let vs = vs |> Set.toList |> List.take x
                    try
                        eqs
                        |> solveValues s vs
                    with
                    | _ ->
                        printfn $"cannot set {vs}"
                        eqs
            | TakeFromMax x ->
                    let x = if x > vs.Count then vs.Count else x
                    let vs = vs |> Set.toList |> List.rev |> List.take x
                    try
                        eqs
                        |> solveValues s vs
                    with
                    | _ ->
                        printfn $"cannot set {vs}"
                        eqs


    type Orb =
        {
            Components : Component list
        }
    and Component =
        {
            Name : string
            Items : string list
        }


    let createEqs (orb : Orb) =
        [
            "itm_cmp_qty = itm_cmp_cnc * cmp_qty"
            "itm_orb_qty = itm_orb_cnc * orb_qty"
            "itm_orb_qty = itm_cmp_cnc * cmp_orb_qty"
            "itm_dos_qty = itm_cmp_cnc * cmp_dos_qty"
            "itm_dos_qty = itm_orb_cnc * orb_dos_qty"
            "itm_dos_qty = itm_dos_rte * pres_time"
            "itm_dos_qty = itm_dos_qty_adj * ord_adj"
            "itm_dos_tot = itm_cmp_cnc * cmp_dos_tot"
            "itm_dos_tot = itm_orb_cnc * orb_dos_tot"
            "itm_dos_tot = itm_dos_qty * pres_freq"
            "itm_dos_tot = itm_dos_tot_adj * ord_adj"
            "itm_dos_rte = itm_cmp_cnc * cmp_dos_rte"
            "itm_dos_rte = itm_orb_cnc * orb_dos_rte"
            "itm_dos_rte = itm_dos_rte_adj * ord_adj"
            "itm_dos_ord = itm_dos_tot * ord_time"
            "itm_dos_ord = itm_dos_rte * ord_time"
            "itm_dos_qty_adj = itm_cmp_cnc * cmp_dos_qty_adj"
            "itm_dos_qty_adj = itm_orb_cnc * orb_dos_qty_adj"
            "itm_dos_qty_adj = itm_dos_rte_adj * pres_time"
            "itm_dos_tot_adj = itm_cmp_cnc * cmp_dos_tot_adj"
            "itm_dos_tot_adj = itm_orb_cnc * orb_dos_tot_adj"
            "itm_dos_tot_adj = itm_dos_qty_adj * pres_freq"
            "itm_dos_rte_adj = itm_cmp_cnc * cmp_dos_rte_adj"
            "itm_dos_rte_adj = itm_orb_cnc * orb_dos_rte_adj"
            "cmp_orb_qty = cmp_orb_cnc * orb_qty"
            "cmp_orb_qty = cmp_qty * cmp_orb_cnt"
            "cmp_ord_qty = cmp_qty * cmp_ord_cnt"
            "cmp_ord_qty = cmp_dos_tot * ord_time"
            "cmp_ord_qty = cmp_dos_rte * ord_time"
            "cmp_dos_qty = cmp_orb_cnc * orb_dos_qty"
            "cmp_dos_qty = cmp_dos_rte * pres_time"
            "cmp_dos_qty = cmp_dos_qty_adj * ord_adj"
            "cmp_dos_tot = cmp_orb_cnc * orb_dos_tot"
            "cmp_dos_tot = cmp_dos_qty * pres_freq"
            "cmp_dos_tot = cmp_dos_tot_adj * ord_adj"
            "cmp_dos_rte = cmp_orb_cnc * orb_dos_rte"
            "cmp_dos_rte = cmp_dos_rte_adj * ord_adj"
            "cmp_dos_qty_adj = cmp_orb_cnc * orb_dos_qty_adj"
            "cmp_dos_qty_adj = cmp_dos_rte_adj * pres_time"
            "cmp_dos_tot_adj = cmp_orb_cnc * orb_dos_tot_adj"
            "cmp_dos_tot_adj = cmp_dos_qty_adj * pres_freq"
            "cmp_dos_rte_adj = cmp_orb_cnc * orb_dos_rte_adj"
            "orb_ord_qty = orb_ord_cnt * orb_qty"
            "orb_ord_qty = orb_dos_tot * ord_time"
            "orb_ord_qty = orb_dos_rte * ord_time"
            "orb_dos_qty = orb_dos_rte * pres_time"
            "orb_dos_qty = orb_dos_qty_adj * ord_adj"
            "orb_dos_tot = orb_dos_qty * pres_freq"
            "orb_dos_tot = orb_dos_tot_adj * ord_adj"
            "orb_dos_rte = orb_dos_rte_adj * ord_adj"
            "orb_dos_qty_adj = orb_dos_rte_adj * pres_time"
            "orb_dos_tot_adj = orb_dos_qty_adj * pres_freq"
            "orb_qty = orb_qty_adj * ord_adj"
            "orb_qty = sum(cmp_orb_qty)"
            // only add these when single values are used!!
            "orb_dos_qty = sum(cmp_dos_qty)"
            "orb_dos_tot = sum(cmp_dos_tot)"
            "orb_dos_rte = sum(cmp_dos_rte)"
            "orb_dos_qty_adj = sum(cmp_dos_qty_adj)"
            "orb_dos_tot_adj = sum(cmp_dos_tot_adj)"
            "orb_dos_rte_adj = sum(cmp_dos_rte_adj)"
        ]
        |> fun eqs ->
            let eqs = eqs |> List.map (fun s -> $" {s}")
            let sumEqs =
                eqs
                |> List.filter (fun e ->
                    e.Contains("sum")
                )
            let eqs = eqs |> List.filter (fun e -> e.Contains("sum") |> not)
            let itmEqs =
                eqs
                |> List.filter (fun e ->
                    e.Contains("itm")
                )
            let cmpEqs =
                eqs
                |> List.filter (fun e ->
                    itmEqs
                    |> List.exists ((=) e)
                    |> not &&
                    e.Contains("cmp")
                )
            let orbEqs =
                eqs
                |> List.filter (fun e ->
                    itmEqs
                    |> List.exists ((=) e)
                    |> not &&
                    cmpEqs
                    |> List.exists((=) e)
                    |> not
                )

            orb.Components
            |> List.fold (fun acc c ->
                let itms =
                    c.Items
                    |> List.collect (fun i ->
                        itmEqs
                        |> List.map (fun s -> s.Replace(" cmp", $" {c.Name}").Replace(" itm", $" {i}"))
                    )
                let cmps =
                    cmpEqs
                    |> List.map (fun s1 ->
                        s1.Replace(" cmp", $" {c.Name}")
                    )
                itms @ cmps @ acc
            ) []
            |> fun es ->
                let sumEqs =
                    sumEqs
                    |> List.map (fun e ->
                        match e.Replace("sum(", "").Replace(")", "").Split(" = ") with
                        | [|lv; rv|] ->
                            orb.Components
                            |> List.map(fun c -> rv.Replace("cmp", c.Name))
                            |> String.concat(" + ")
                            |> fun s -> $"{lv} = {s}"
                        | _ -> ""
                    )
                es @ orbEqs @ sumEqs
        |> Api.init



open Solve



let gentaEqs =
    {
        Components =
            [
                {
                    Name = "genta_sol"
                    Items = ["gentamicin"]
                }
                {
                    Name = "saline"
                    Items = ["sodium"; "chloride"]
                }
            ]
    }
    |> createEqs



// find min/max scenarios
gentaEqs
|> printEqs
// patient
|> solveValues "ord_adj" [755N/100N]
|> printEqs
// product
|> solveValues "genta_sol_qty" [2N; 10N]
|> solveValues "gentamicin_cmp_cnc" [10N; 40N]
|> solveValues "gentamicin_cmp_qty" [20N;80N;400N]
|> printEqs
// preparation
|> solveMaxIncl "gentamicin_orb_cnc" 2N // max conc
|> solveMinIncl "orb_qty_ad" 5N
|> solveMaxIncl "orb_qty_adj" 20N //
|> solveValues "orb_qty" [20N] //
|> solveValues "genta_sol_orb_qty" ([1N/10N..1N/10N..10N] @ [11N..50N]) // max conc
//|> solveValues "saline_orb_qty" ([1N/10N..1N/10N..10N] @ [11N..50N])
|> printEqs
// dose
|> solveMaxIncl "orb_dos_qty_adj" 10N
|> solveMinIncl "gentamicin_dos_tot_adj" 5N // max dose
|> solveMaxIncl "gentamicin_dos_tot_adj" 7N // max dose
|> printEqs
// administration
|> solveValues "pres_freq" [1N] // freq
|> solveMinIncl "pres_time" (1N/2N) // time
|> solveMaxIncl "pres_time" 1N // time
|> printEqs
// pick the optimal scenario
|> pick "orb_dos_qty" (Some 1N) (TakeFromMin 10) // pick between min and max
//|> pick "orb_dos_rte" (Some (1N/10N)) (TakeFromMax 1) // pick between min and max
|> pick "gentamicin_dos_tot_adj" None (TakeFromMax 2) // choose from list
|> printEqs
|> ignore




// find min/max scenarios
gentaEqs
// patient
|> solveValues "ord_adj" [755N/100N]
|> printEqs
// product
|> solveValues "genta_sol_qty" [2N; 10N]
|> solveValues "gentamicin_cmp_cnc" [10N; 40N]
|> solveValues "gentamicin_cmp_qty" [20N;80N;400N]
|> printEqs
// preparation
|> solveMaxIncl "gentamicin_orb_cnc" 2N // max conc
|> solveMinIncl "orb_qty_ad" 5N
|> solveMaxIncl "orb_qty_adj" 20N //
|> solveValues "orb_qty" [20N] //
|> solveValues "genta_sol_orb_qty" ([1N/10N..1N/10N..10N] @ [11N..50N]) // max conc
|> solveValues "saline_orb_qty" ([1N/10N..1N/10N..10N] @ [11N..50N])
|> printEqs
// dose
|> solveMaxIncl "orb_dos_qty_adj" 10N
|> solveMinIncl "gentamicin_dos_tot_adj" 5N // max dose
|> solveMaxIncl "gentamicin_dos_tot_adj" 7N // max dose
|> printEqs
// administration
|> solveValues "pres_freq" [1N] // freq
|> solveMinIncl "pres_time" (1N/2N) // time
|> solveMaxIncl "pres_time" 1N // time
|> printEqs
// pick the optimal scenario
|> pick "orb_dos_qty" (Some 1N) (TakeFromMin 10) // pick between min and max
|> pick "orb_dos_rte" (Some (1N/10N)) (TakeFromMax 1) // pick between min and max
|> pick "gentamicin_dos_tot_adj" None (TakeFromMax 2) // choose from list
|> printEqs
|> ignore




let examplEqs =
    {
        Components =
            [
                {
                    Name = "CMPA"
                    Items = ["ITMA"]
                }
                {
                    Name = "CMPB"
                    Items = ["ITMB"]
                }
            ]
    }
    |> createEqs


examplEqs
|> printEqs
|> List.collect Equation.toVars
|> List.map (fun v -> v.Name |> Name.toString)
|> List.distinct
|> fun vars ->
    let cmps =
        vars
        |> List.filter (fun s ->
            s.Contains("CMPB") || s.Contains("ITMB")
        )
        |> List.filter (fun s ->
            s.Contains("cmp_cnc") || s.Contains("CMPB_qty")
        )
    vars
    |> List.filter (fun s ->
        s.Contains("CMPB") |> not &&
        s.Contains("ITMB") |> not
    )
    |> List.append cmps
    |> List.length

