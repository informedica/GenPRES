
// load demo or product cache
System.Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
System.Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA")

#load "load.fsx"

#time

open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib
open Informedica.GenSolver.Lib.Variable.Operators




let getPrescr gen shp rte prescrs =
    prescrs
    |> Array.filter (fun prescr ->
        prescr.DoseRule.Generic |> String.equalsCapInsens gen &&
        (shp |> String.isNullOrWhiteSpace || prescr.DoseRule.Shape |> String.equalsCapInsens shp) &&
        (rte |> String.isNullOrWhiteSpace || prescr.DoseRule.Route |> String.equalsCapInsens rte)
    )
    |> Array.item 0
    |> Api.evaluate Logging.ignore
    |> Array.head
    |> function
        | Ok (o, _) -> o
        | Error (o, _, _) -> o


/// Get all possible prescriptions for a child
let prescrs =
    Patient.child
    |> fun p -> { p with Locations = [CVL]; Department = Some "ICK"  }
    |> PrescriptionRule.get



prescrs
|> Array.find (fun pr ->
    pr.DoseRule.Generic |> String.equalsCapInsens "paracetamol" &&
    pr.DoseRule.Route |> String.equalsCapInsens "rectaal"
)
|> fun pr ->
    pr.DoseRule.DoseLimits
    |> Array.map _.Component


let dros =
    prescrs
    |> Array.find (fun pr ->
        pr.DoseRule.Generic |> String.equalsCapInsens "gentamicine" &&
        pr.DoseRule.Route |> String.equalsCapInsens "intraveneus"
    )
    |> DrugOrder.fromRule

let presr =
    prescrs
    |> Array.find (fun pr ->
        pr.DoseRule.Generic |> String.equalsCapInsens "gentamicine" &&
        pr.DoseRule.Route |> String.equalsCapInsens "intraveneus"
    )

presr.SolutionRules[0].Diluents

dros[0].Products |> Array.length

dros[0]
|> DrugOrder.toOrderDto
|> Order.Dto.fromDto
|> Order.applyConstraints
|> Order.toString
|> List.iter (printfn "%s")


prescrs
|> Array.find (fun pr ->
    pr.DoseRule.Generic = "paracetamol" &&
    pr.DoseRule.Route = "ORAAL"
)
|> fun dr ->
    printfn $"{dr.SolutionRules |> Array.toList}"
    dr
    |> DrugOrder.fromRule
    |> Array.map (fun dro ->

        dro
        |> DrugOrder.toOrderDto
        |> Order.Dto.fromDto
        |> (Order.applyConstraints >> Order.toString >> List.iter (printfn "%s"))
    )



let naclIV =
    prescrs
    |> getPrescr "natriumchloride" "" "INTRAVENEUS"

// oral solution for PCM
let pcmOralSolution =
    prescrs
    |> getPrescr "paracetamol" "drank" ""

let pcmOralSolution =
    prescrs
    |> getPrescr "paracetamol" "tablet" ""

// rectal solution for PCM
let pcmRect =
    prescrs
    |> getPrescr "paracetamol" "" "rectaal"


let amoxyIv =
    prescrs
    |> getPrescr "amoxicilline" "" ""

let adrenInf =
    prescrs
    |> getPrescr "adrenaline" "" "INTRAVENEUS"

let cotrimIv =
    prescrs
    |> getPrescr "trimethoprim/sulfamethoxazol" "" "INTRAVENEUS"

let tpn =
    prescrs
    |> getPrescr "Samenstelling D" "" ""

let gentaIv =
    prescrs
    |> getPrescr "gentamicine" "" "intraveneus"


tpn |> Order.toString |> List.iter (printfn "%s")

pcmRect
|> Order.solve false true Logging.ignore
|> Result.get
|> Order.Print.printOrderToTableFormat true false [|"paracetamol"|]


let w =
    Patient.child.Weight
    |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

open Informedica.GenSolver.Lib.Variable.ValueRange.Minimum

[|
    naclIV
|]
|> Intake.calc w Units.Time.day
|> fun intake ->
    intake[0]
    |> snd
    |> Variable.getValueRange
    |> Variable.ValueRange.getMin
    |> Option.map (fun min ->
        let _, vu = min |> toBoolValueUnit

        let s =
            $"""{vu |> ValueUnit.toDelimitedString 3}"""

        if false then s
        else
            let u =
                vu
                |> ValueUnit.getUnit
                |> Units.toStringDutchShort
                |> String.removeBrackets

            printfn $"{u}"
            s
            |> String.replace $"|{u}|" ""
    )

adrenInf
|> Order.Dto.toDto
|> Array.singleton
|> Api.getIntake w

let shp = ""
let rte = "INTRAVENEUS"
prescrs
|> Array.filter (fun prescr ->
    prescr.DoseRule.Generic = "lorazepam" &&
    (shp |> String.isNullOrWhiteSpace || prescr.DoseRule.Shape = shp) &&
    (rte |> String.isNullOrWhiteSpace || prescr.DoseRule.Route = rte)
)
|> Array.item 0
|> fun r -> DrugOrder.createDrugOrder None r

Informedica.GenForm.Lib.Product.get ()
|> Array.filter (fun p -> p.GPK = "170976")