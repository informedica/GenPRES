

#load "load.fsx"


#time



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib




let path = Some $"{__SOURCE_DIRECTORY__}/log.txt"
let startLogger () =
    // Start the logger at an informative level
    OrderLogger.logger.Start path Logging.Level.Informative
let stopLogger () = OrderLogger.logger.Stop ()



let test pat n =
    let pr =
        pat
        |> PrescriptionRule.get
        |> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
        |> Array.item n

    pr
    |> Api.evaluate { Log = ignore }
    |> Array.map (function
        | Ok (ord, pr) ->
            let ns =
                pr.DoseRule.DoseLimits
                |> Array.map (fun dl -> dl.Substance)
            let o =
                ord
                |> Order.Print.printPrescription ns
            let p =
                $"{pr.DoseRule.Generic}, {pr.DoseRule.Shape}, {pr.DoseRule.DoseType |> DoseType.toString} {pr.DoseRule.Indication}"
            Ok (pat, p, o)
        | Error (ord, pr, m) ->
            let o =
                ord
                |> Order.toString
                |> String.concat "\n"
            let p =
                $"{pr.DoseRule.Generic}, {pr.DoseRule.Shape}, {pr.DoseRule.Indication}"

            Error ($"%A{m}", p, o)
    )




let getN pat =
    pat
    |> PrescriptionRule.get
    |> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
    |> Array.length


let run n pat =
    for i in [0..n-1] do
        try
            i
            |> test pat
            |> Array.map (function
                | Ok (pat, ind, (prs, prep, adm)) ->
                    [
                        ""
                        $"{i}"
                        $"Patient: {pat |> Patient.toString}"
                        $"Indicatie: {ind}"
                        $"Voorschrift: {prs}"
                        if prep |> String.notEmpty then $"Bereiding: {prep}"
                        $"Toediening: {adm}"
                        ""
                    ]
                    |> String.concat "\n"
                | Error (_, p, _) -> $"\n{i}.Fail: {p}\n"
            )
            |> String.concat "\n"

        with
        | _ ->
            let pr =
                pat
                |> PrescriptionRule.get
                |> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
                |> Array.item i
                |> fun pr ->
                    $"{pr.DoseRule.Generic}, {pr.DoseRule.Shape}, {pr.DoseRule.Indication}"

            $"\n{i}. could not calculate: {pr}\n"
        |>  File.appendTextToFile path.Value


let getRule i pat =
    pat
    |> PrescriptionRule.get
    |> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
    |> Array.item i



[
    Patient.premature
    Patient.newBorn
    Patient.infant
    Patient.toddler
    Patient.child
    Patient.teenager
    Patient.adult
]
// |> List.skip 4
// |> List.take 1
|> List.iter (fun pat ->
    let n = getN pat
    printfn $"=== Running pat: {pat |> Patient.toString}: {n} ==="

    pat
    |> run n
)



test Patient.premature 111
|> Array.iter (function
    | Ok (pat, ind, (prs, prep, adm)) ->
        [
            $"Patient: {pat |> Patient.toString}"
            $"Indicatie: {ind}"
            $"Voorschrift: {prs}"
            if prep |> String.notEmpty then $"Bereiding: {prep}"
            $"Toediening: {adm}"
        ]
        |> List.iter (printfn "%s")
    | Error _ -> ()
)


startLogger ()
stopLogger ()


Patient.premature
|> getRule 111 //|> Api.evaluate (OrderLogger.logger.Logger)
|> fun pr -> pr |> Api.createDrugOrder None //(pr.SolutionRules[0] |> Some)  //|> printfn "%A"
|> DrugOrder.toOrder
|> Order.Dto.fromDto
|> Order.applyConstraints //|> Order.toString |> List.iter (printfn "%s")
|> Order.solveMinMax true OrderLogger.logger.Logger
|> function
| Error (ord, msgs) ->
    printfn "oeps error"
    // printfn $"{msgs |> List.map string}"
    // ord
    // |> Order.toString
    // |> String.concat "\n"
    // |> printfn "%s"

| Ok ord  ->
//    ord.Orderable.OrderableQuantity
//    |> printfn "%A"

    ord
    |> Order.toString
    |> String.concat "\n"
    |> printfn "%s"



open Order

try
    let ord =
        Patient.child
        |> getRule 703
        |> Api.createDrugOrder None
        |> DrugOrder.toOrder
        |> Order.Dto.fromDto
        |> Order.applyConstraints

    let mapping =
        match ord.Prescription with
        | Continuous -> Mapping.continuous
        | Discontinuous _ -> Mapping.discontinuous
        | Timed _ -> Mapping.timed
        |> Mapping.getEquations
        |> Mapping.getEqsMapping ord

    printfn $"{mapping}"

    let oEqs =
        ord
        |> mapToEquations mapping

    oEqs
    |> Solver.mapToSolverEqs



with
| :? Informedica.GenSolver.Lib.Exceptions.SolverException as e ->
    printfn $"{e.Data0}"
    raise e



let testDto =
    Patient.infant
    |> getRule 5
    |> Api.createDrugOrder None
    |> DrugOrder.toOrder




Patient.infant
|> Api.getIndications
|> Array.iter (printfn "%s")


Patient.infant
|> Api.getGenerics
|> Array.iter (printfn "%s")




module Demo =


    let toString (sc : Scenario) =
        $"""
{sc.No}. {sc.Name} {sc.Shape} {sc.Route}
Voorschrift: {sc.Prescription}
Bereiding: {sc.Preparation}
Toediening: {sc.Administration}
"""


    let scenarioResult pat =
        let rules = pat |> PrescriptionRule.get
        {
            Indications = rules |> PrescriptionRule.indications
            Generics = rules |> PrescriptionRule.indications
            Routes = rules |> PrescriptionRule.indications
            Shapes= rules |> PrescriptionRule.indications
            Indication = None
            Generic = None
            Route = None
            Shape = None
            Patient = pat
            Scenarios = [||]
        }


    let filter (sc : ScenarioResult) =
        match sc.Patient.Weight, sc.Patient.Height, sc.Patient.Department with
        | Some w, Some _, d when d |> String.notEmpty ->

            let ind = sc.Indications |> Array.someIfOne
            let gen = sc.Generics |> Array.someIfOne
            let rte = sc.Routes |> Array.someIfOne
            let shp = sc.Shapes |> Array.someIfOne

            let filter = 
                { Filter.filter with
                    Department = Some d
                    Weight = Some w
                    Indication = ind
                    Generic = gen
                    Route = rte
                    Shape = shp
                }

            let inds = filter |> Api.filterIndictions
            let gens = filter |> Api.filterGenerics
            let rtes = filter |> Api.filterRoutes
            let shps = filter |> Api.filterShapes

            let ind = inds |> Array.someIfOne
            let gen = gens |> Array.someIfOne
            let rte = rtes |> Array.someIfOne
            let shp = shps |> Array.someIfOne

            { sc with
                Indications = inds
                Generics = gens
                Routes = rtes
                Shapes = shps
                Indication = ind
                Generic = gen
                Route = rte
                Shape = shp
                Scenarios = 
                    match gen, rte, shp with
                    | Some _, Some _, Some _ ->
                        { filter with
                            Department = Some d
                            Indication = ind
                            Generic = gen
                            Route = rte
                            Shape = shp
                        }
                        |> PrescriptionRule.filter
                        |> Array.collect (fun pr ->
                            pr
                            |> Api.evaluate OrderLogger.logger.Logger
                            |> Array.map (function
                                | Ok (ord, pr) ->
                                    let ns =
                                        pr.DoseRule.DoseLimits
                                        |> Array.map (fun dl -> dl.Substance)
                                    let prs, prp, adm =
                                        ord
                                        |> Order.Print.printPrescription ns
                                    [
                                        $"{pr.DoseRule.Generic}, {pr.DoseRule.Shape}, {pr.DoseRule.DoseType |> DoseType.toString} {pr.DoseRule.Indication}"
                                        $"Voorschrift: {prs}"
                                        $"Bereiding: {prp}"
                                        $"Toediening: {adm}"
                                    ]
                                    |> String.concat "\n"

                                | Error _ -> ""
                            )
                        )

                    | _ -> [||]
            }
        | _ -> sc


Patient.child
|> Demo.scenarioResult
|> Demo.filter
|> fun scr -> 
    { scr with
        Indications = scr.Indications |> Array.skip 1 |> Array.take 1
    }
|> Demo.filter
|> fun scr -> 
    { scr with
        Generics = scr.Generics |> Array.take 1
    }
|> Demo.filter

