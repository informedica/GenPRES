

#load "load.fsx"


#time



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib

module DoseLimit = DoseRule.DoseLimit

open Informedica.ZIndex.Lib
// load demo or product cache
System.Environment.SetEnvironmentVariable(FilePath.GENPRES_PROD, "1")



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
                |> Array.map (fun dl -> dl.DoseLimitTarget |> DoseLimit.doseLimitTargetToString)
            let o =
                ord
                |> Order.Print.printOrderToString true ns
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


Informedica.GenForm.Lib.DoseRule.get ()
|> DoseRule.filter
//    Filter.filter
   { Filter.filter with Patient = Patient.infant }
|> Array.filter (fun dr ->
    dr.Generic = "propranolol" &&
    dr.Route = "or"
)
//|> Array.skip 2
//|> Array.take 1
|> Array.length

Mapping.mappingShapeRoute
|> Array.map _.DoseUnit
|> Array.distinct
|> Array.iter (printfn "%A")

Mapping.mapUnit ""

Units.fromString "E[InternationalUnit]"

Informedica.GenForm.Lib.DoseRule.get ()
|> Array.item 2
|> fun dr ->
    dr.PatientCategory
    |> PatientCategory.filter { Filter.filter with Patient = Patient.child }


{ Department = Some "ICK"
  Diagnoses = [||]
  Gender = AnyGender
  Age = Some (ValueUnit ([|0N|], Units.Time.day))
  Weight = Some (ValueUnit ([|757N/200N|], Weight (WeightKiloGram 1N)))
  Height = Some (ValueUnit ([|1059N/20N|], Height (HeightCentiMeter 1N)))
  GestAge = None
  PMAge = None
  VenousAccess = []
}
|> Api.scenarioResult
|> Api.filter

Patient.child
|> Patient.toString

"1000 kg[Weight]"
|> Units.fromString


Patient.infant
|> fun p -> { p with
                Weight =
                  p.Weight
                  |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)
}
//|> fun p -> { p with VenousAccess = CVL; AgeInDays = Some 0N }
|> PrescriptionRule.get
//|> Array.filter (fun pr -> pr.DoseRule.Products |> Array.isEmpty |> not)
|> Array.filter (fun pr ->
    pr.DoseRule.Route = "iv" &&
    pr.DoseRule.Generic = "vancomycine"
)
|> Array.item 3 //|> Api.evaluate (OrderLogger.logger.Logger)
|> fun pr -> pr |> DrugOrder.createDrugOrder (pr.SolutionRules[0] |> Some)  //|> printfn "%A"
|> DrugOrder.toOrderDto
|> Order.Dto.fromDto //|> Order.toString |> List.iter (printfn "%s")
|> Order.applyConstraints //|> Order.toString |> List.iter (printfn "%s")
|> fun ord ->
    printfn "constraints applied"
    ord
    |> Order.toString
    |> String.concat "\n"
    |> printfn "%s"
    ord

|> Order.solveMinMax true OrderLogger.logger.Logger
|> Result.map (fun ord ->
    printfn "solve min max"
    ord
    |> Order.toString
    |> String.concat "\n"
    |> printfn "%s"
    ord
)
|> Result.bind (Api.increaseIncrements OrderLogger.logger.Logger)
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
    printfn "increased increment"
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
            |> Order.Print.printOrderToString true [|"vancomycine"|]
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


try
    let ord =
        Patient.child
        |> getRule 703
        |> DrugOrder.createDrugOrder None
        |> DrugOrder.toOrderDto
        |> Order.Dto.fromDto
        |> Order.applyConstraints

    let mapping =
        match ord.Prescription with
        | Continuous -> Order.Mapping.continuous
        | Discontinuous _ -> Order.Mapping.discontinuous
        | Timed _ -> Order.Mapping.timed
        |> Order.Mapping.getEquations
        |> Order.Mapping.getEqsMapping ord

    printfn $"{mapping}"

    let oEqs =
        ord
        |> Order.mapToOrderEquations mapping

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
        pr.DoseRule.Generic = "trimethoprim/sulfamethoxazol" &&
        pr.DoseRule.Route = "iv" //&&
//        pr.DoseRule.Indication |> String.startsWith "juveniele"
    )
    |> Array.item 0 //|> Api.evaluate (OrderLogger.logger.Logger)
    |> fun pr -> pr |> DrugOrder.createDrugOrder None //(pr.SolutionRules[0] |> Some)  //|> printfn "%A"
    |> DrugOrder.toOrderDto


module ValueUnit = Informedica.GenUnits.Lib.ValueUnit
testDto.Orderable.Components[0].Items[0].ComponentConcentration.Constraints.ValsOpt.Value.Value
|> fun s ->
    $"1 {s}"
    |> ValueUnit.fromString


let vancoDoseRules =
    Informedica.GenForm.Lib.DoseRule.get ()
    |> DoseRule.filter
    //    Filter.filter
       { Filter.filter with Patient = Patient.child }
    |> Array.filter (fun dr ->
        dr.Generic = "vancomycine" &&
        dr.Route = "iv"
    )
    |> Array.skip 2
    |> Array.take 1


vancoDoseRules
//Informedica.GenForm.Lib.DoseRule.get ()
|> DoseRule.filter
    { Filter.filter with
        Patient =
            Patient.child
            |> Patient.calcPMAge
    }
|> Array.map (fun dr -> dr |> DoseRule.reconstitute Patient.child.Department [VenousAccess.CVL])
|> Array.filter (fun dr ->
    dr.Generic = "vancomycine" &&
    dr.Route = "iv" &&
    dr.Products |> Array.isEmpty |> not
)


Patient.child
|> PrescriptionRule.get
|> Array.filter (fun dr ->
    dr.DoseRule.Generic = "vancomycine" &&
    dr.DoseRule.Route = "iv"
)

let add a b = a + b
let addOne = add 1

addOne 4