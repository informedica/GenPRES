

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
                |> Order.Print.printOrderToString ns
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


let createScenarios () =
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



startLogger ()
stopLogger ()


Patient.teenager
|> fun p -> { p with VenousAccess = CVL }
|> PrescriptionRule.get
//|> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
|> Array.filter (fun pr ->
    pr.DoseRule.Generic = "benzylpenicilline" &&
    pr.DoseRule.Route = "iv" //&&
//    pr.DoseRule.Indication |> String.startsWith "vassopressie"
)
|> Array.item 0 //|> Api.evaluate (OrderLogger.logger.Logger)
|> fun pr -> pr |> Api.createDrugOrder (pr.SolutionRules[0] |> Some)  //|> printfn "%A"
|> DrugOrder.toOrder
|> Order.Dto.fromDto //|> Order.toString |> List.iter (printfn "%s")
|> Order.applyConstraints //|> Order.toString |> List.iter (printfn "%s")

|> Order.solveMinMax true OrderLogger.noLogger
|> Result.bind (Api.increaseIncrement OrderLogger.logger.Logger)
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
    //|> Order.Markdown.printPrescription [|"insuline aspart"|]
    //|> fun (prs, prep, adm) -> printfn $"{prs}"
    |> Order.toString
    |> String.concat "\n"
    |> printfn "%s"

    ord
    |> Order.solveOrder true OrderLogger.noLogger
    |> function
        | Error (_, msgs) ->
            printfn "oeps error"
            printfn $"{msgs |> List.map string}"
            // ord
            // |> Order.toString
            // |> String.concat "\n"
            // |> printfn "%s"
        | Ok ord  ->
            ord
            |> Order.Print.printOrderToString [|"gentamincine"|]
            |> fun (prs, prep, adm) -> printfn $"{prs}"
            // ord
            // |> Order.toString
            // |> String.concat "\n"
            // |> printfn "%s"





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
        |> mapToOrderEquations mapping

    oEqs
    |> Solver.mapToSolverEqs



with
| :? Informedica.GenSolver.Lib.Exceptions.SolverException as e ->
    printfn $"{e.Data0}"
    raise e



let testDto =
    Patient.adult
    |> PrescriptionRule.get
    |> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
    |> Array.filter (fun pr ->
        pr.DoseRule.Generic = "benzylpenicilline" &&
        pr.DoseRule.Route = "iv" //&&
//        pr.DoseRule.Indication |> String.startsWith "juveniele"
    )
    |> Array.item 0 //|> Api.evaluate (OrderLogger.logger.Logger)
    |> fun pr -> pr |> Api.createDrugOrder None //(pr.SolutionRules[0] |> Some)  //|> printfn "%A"
    |> DrugOrder.toOrder


module ValueUnit = Informedica.GenUnits.Lib.ValueUnit
testDto.Orderable.Components[0].Items[0].ComponentConcentration.Constraints.ValsOpt.Value.Value
|> fun s ->
    $"1 {s}"
    |> ValueUnit.fromString



"miljIE[IUnit]"
|> Informedica.GenUnits.Lib.Units.fromString

Patient.infant
|> Api.getGenerics
|> Array.iter (printfn "%s")


DoseRule.get ()
|> Array.filter (fun dr -> dr.Generic = "amiodaron")


let filter1 =
    { Filter.filter with
        Generic = Some "argipressine"
        Route = Some "iv"
    }
    |> Filter.setPatient Patient.child

DoseRule.get ()
|> DoseRule.filter filter1
|> Array.map (DoseRule.reconstitute "" AnyAccess)

PrescriptionRule.get Patient.patient
|> Array.map (fun pr -> pr.DoseRule.Generic)
|> Array.distinct
|> Array.sort

// quick check of products in assortment
Informedica.ZIndex.Lib.GenPresProduct.search "benzylpenicilline"
|> Array.map (fun gpp -> $"{gpp.Name}, {gpp.Shape} {(gpp.GenericProducts |> Array.head).Id}")
|> Array.distinct
|> Array.sort
|> Array.iter (printfn "%s")
175552

Informedica.ZIndex.Lib.GenPresProduct.findByGPK 47929
"INFUSIEVLOEISTOF"

"MACROGOL/NATRIUMCHLORIDE/NATRIUMWATERSTOFCARBONAAT/KALIUMCHLORIDE"
|> String.toLower

//[1.gentamicine.vlstf]_orb_qty [1 mL..0,1 mL..12,8 mL] + [1.gentamicine.oplosvlstf]_orb_qty [18,7 mL..0,1 mL..63,1 mL]
[1.0 .. 0.1 .. 12.8]
|> List.allPairs [18.7 .. 0.1 .. 63.1]
|> List.map (fun (x1, x2) -> x1 * x2)
|> List.distinct
|> List.length
