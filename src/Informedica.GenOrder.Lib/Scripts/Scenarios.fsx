
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


/// Get all possible prescriptions for a child
let prescrs =
    Patient.child
    |> PrescriptionRule.get

Informedica.ZIndex.Lib.GenPresProduct.findByGPK 170976

prescrs |> Array.length


let getPrescr gen shp rte prescrs =
    prescrs
    |> Array.filter (fun prescr ->
        prescr.DoseRule.Generic = gen &&
        (shp |> String.isNullOrWhiteSpace || prescr.DoseRule.Shape = shp) &&
        (rte |> String.isNullOrWhiteSpace || prescr.DoseRule.Route = rte)
    )
    |> Array.item 0
    |> Api.evaluate Logging.ignore
    |> Array.head
    |> function
        | Ok (o, _) -> o
        | Error (o, _, _) -> o


let isVolume (var : Variable) =
    var
    |> Variable.getUnit
    |> Option.map (fun u ->
        u |> Units.hasGroup Units.Volume.liter
    )
    |> Option.defaultValue false


let getDose tu prescr (dose: Dose) =
    match prescr with
    | Timed _
    | Discontinuous _ ->
        dose.PerTimeAdjust
        |> OrderVariable.PerTimeAdjust.toOrdVar
        |> OrderVariable.getVar
    | Continuous ->
        dose.RateAdjust
        |> OrderVariable.RateAdjust.toOrdVar
        |> OrderVariable.getVar
    | Once
    | OnceTimed _ ->
        let var =
            dose.QuantityAdjust
            |> OrderVariable.QuantityAdjust.toOrdVar
            |> OrderVariable.getVar
        let unt =
            var
            |> Variable.getUnit
            |> Option.map (fun u -> u |> Units.per tu)

        unt
        |> Option.map (fun u ->
            var
            |> Variable.setUnit u
        )
        |> Option.defaultValue var


let getVolume tu prescr (dose: Dose) =
    let ovar = getDose tu prescr dose

    if ovar |> isVolume then Some ovar
    else None


let calcIntake tu (ords : Order[]) =
    [|
        for o in ords do
            let vol = getVolume tu o.Prescription o.Orderable.Dose
            if vol.IsSome then "volume", vol.Value

            for cmp in o.Orderable.Components do
                for itm in cmp.Items do
                    itm.Name |> Name.toString, getDose tu o.Prescription itm.Dose
    |]
    |> Array.groupBy fst
    |> Array.map (fun (item, xs) ->
        item,
        xs
        |> Array.map snd
        |> Array.reduce (^+)
    )


// oral solution for PCM
let pcmOralSolution =
    prescrs
    |> getPrescr "paracetamol" "drank" ""


let amoxyIv =
    prescrs
    |> getPrescr "amoxicilline" "" ""

let adrenInf =
    prescrs
    |> getPrescr "adrenaline" "" "INTRAVENEUS"


adrenInf |> Order.toString |> List.iter (printfn "%s")


[|
    pcmOralSolution
    amoxyIv
    adrenInf
|]
|> calcIntake Units.Time.day


let shp = ""
let rte = "INTRAVENEUS"
prescrs
|> Array.filter (fun prescr ->
    prescr.DoseRule.Generic = "adrenaline" &&
    (shp |> String.isNullOrWhiteSpace || prescr.DoseRule.Shape = shp) &&
    (rte |> String.isNullOrWhiteSpace || prescr.DoseRule.Route = rte)
)
|> Array.item 0
|> fun r -> DrugOrder.createDrugOrder (Some r.SolutionRules[0]) r

Informedica.GenForm.Lib.Product.get ()
|> Array.filter (fun p -> p.GPK = "170976")

