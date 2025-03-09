module ScenarioResult


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared


module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


let mapPatient
    (sr: ScenarioResult)
    =
    { Patient.patient with
        Department =
            sr.Patient.Department
            |> Option.defaultValue "ICK"
            |> Some
        Age =
            sr.Patient
            |> Models.Patient.getAgeInDays
            |> Option.bind BigRational.fromFloat
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
        GestAge =
            sr.Patient |> Models.Patient.getGestAgeInDays
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
        Weight =
            sr.Patient |> Models.Patient.getWeight
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
            |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)
        Height =
            sr.Patient |> Models.Patient.getHeight
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Height.centiMeter)
        Gender =
            match sr.Patient.Gender with
            | Male -> Informedica.GenForm.Lib.Types.Male
            | Female -> Informedica.GenForm.Lib.Types.Female
            | UnknownGender -> AnyGender
        Locations =
            sr.Patient.Access
            // TODO make proper mapping
            |> List.choose (fun a ->
                match a with
                | CVL -> Informedica.GenForm.Lib.Types.CVL |> Some
                | PVL -> Informedica.GenForm.Lib.Types.PVL |> Some
                | _ -> None
            )
        RenalFunction =
            sr.Patient.RenalFunction
            |> Option.map (fun rf ->
                match rf with
                | EGFR(min, max) -> Informedica.GenForm.Lib.Types.RenalFunction.EGFR(min, max)
                | IntermittendHemoDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.IntermittentHemodialysis
                | ContinuousHemoDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.ContinuousHemodialysis
                | PeritionealDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.PeritonealDialysis
            )
    }
    |> Patient.calcPMAge



let mapToValueUnit (dto : ValueUnit.Dto.Dto) : Shared.Types.ValueUnit =
    let v =
        dto.Value
        |> Array.map (fun br ->
            $"{br}", br |> BigRational.toDecimal
        )
    Models.Order.ValueUnit.create
        v
        dto.Unit
        dto.Group
        dto.Short
        dto.Language
        dto.Json


let mapFromValueUnit (vu : Shared.Types.ValueUnit) : ValueUnit.Dto.Dto =
    let v = vu.Value |> Array.map (fst >> BigRational.parse)
    let dto = ValueUnit.Dto.dto ()
    dto.Value <- v
    dto.Unit <- vu.Unit
    dto.Group <- vu.Group
    dto.Language <- vu.Language
    dto.Short <- vu.Short
    dto.Json <- vu.Json

    dto


let mapToVariable (dto: Informedica.GenSolver.Lib.Variable.Dto.Dto) : Variable =
    Models.Order.Variable.create dto.Name dto.IsNonZeroNegative
        (dto.MinOpt |> Option.map mapToValueUnit)
        dto.MinIncl
        (dto.IncrOpt |> Option.map mapToValueUnit)
        (dto.MaxOpt |> Option.map mapToValueUnit)
        dto.MaxIncl
        (dto.ValsOpt |> Option.map mapToValueUnit)


let mapFromVariable (var : Variable) : Informedica.GenSolver.Lib.Variable.Dto.Dto =
    let dto = Informedica.GenSolver.Lib.Variable.Dto.dto ()
    dto.Name <- var.Name
    dto.IsNonZeroNegative <- var.IsNonZeroNegative
    dto.MinOpt <- var.Min |> Option.map mapFromValueUnit
    dto.MinIncl <- var.MinIncl
    dto.IncrOpt <- var.Incr |> Option.map mapFromValueUnit
    dto.MaxOpt <- var.Max |> Option.map mapFromValueUnit
    dto.MaxIncl <- var.MaxIncl
    dto.ValsOpt <- var.Vals |> Option.map mapFromValueUnit

    dto


let mapToOrderVariable (dto : OrderVariable.Dto.Dto) : Shared.Types.OrderVariable =
    Models.Order.OrderVariable.create
        dto.Name
        (dto.Constraints |> mapToVariable)
        (dto.Variable |> mapToVariable)


let mapFromOrderVariable (ov : Shared.Types.OrderVariable) : OrderVariable.Dto.Dto =
    let dto = OrderVariable.Dto.dto ()
    dto.Name <- ov.Name
    dto.Variable <- ov.Variable |> mapFromVariable
    dto.Constraints <- ov.Constraints |> mapFromVariable

    dto


let mapToDose (dto : Order.Orderable.Dose.Dto.Dto) : Dose =
    Models.Order.Dose.create
        (dto.Quantity |> mapToOrderVariable)
        (dto.PerTime |> mapToOrderVariable)
        (dto.Rate |> mapToOrderVariable)
        (dto.Total |> mapToOrderVariable)
        (dto.QuantityAdjust |> mapToOrderVariable)
        (dto.PerTimeAdjust |> mapToOrderVariable)
        (dto.RateAdjust |> mapToOrderVariable)
        (dto.TotalAdjust |> mapToOrderVariable)


let mapFromDose (dose : Dose) : Order.Orderable.Dose.Dto.Dto =
    let dto = Order.Orderable.Dose.Dto.dto ()
    dto.Quantity <- dose.Quantity |> mapFromOrderVariable
    dto.PerTime <- dose.PerTime |> mapFromOrderVariable
    dto.Rate <- dose.Rate |> mapFromOrderVariable
    dto.Total <- dose.Total |> mapFromOrderVariable
    dto.QuantityAdjust <- dose.QuantityAdjust |> mapFromOrderVariable
    dto.PerTimeAdjust <- dose.PerTimeAdjust |> mapFromOrderVariable
    dto.RateAdjust <- dose.RateAdjust |> mapFromOrderVariable
    dto.TotalAdjust <- dose.TotalAdjust |> mapFromOrderVariable

    dto

let mapToItem sns (dto : Order.Orderable.Item.Dto.Dto) : Item =
    Models.Order.Item.create
        dto.Name
        (dto.ComponentQuantity |> mapToOrderVariable)
        (dto.OrderableQuantity |> mapToOrderVariable)
        (dto.ComponentConcentration |> mapToOrderVariable)
        (dto.OrderableConcentration |> mapToOrderVariable)
        (dto.Dose |> mapToDose)
        // filter out sns (substance names) to indicate an additional ingredient
        (sns |> Array.exists (String.equalsCapInsens dto.Name) |> not)


// member val Name = "" with get, set
// member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
// member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
// member val ComponentConcentration = OrderVariable.Dto.dto () with get, set
// member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
// member val Dose = Dose.Dto.dto () with get, set
let mapFromItem (item : Item) : Order.Orderable.Item.Dto.Dto =
    let dto = Order.Orderable.Item.Dto.Dto ()
    dto.Name <- item.Name
    dto.ComponentQuantity <- item.ComponentQuantity |> mapFromOrderVariable
    dto.OrderableQuantity <- item.OrderableQuantity |> mapFromOrderVariable
    dto.ComponentConcentration <- item.ComponentConcentration |> mapFromOrderVariable
    dto.OrderableConcentration <- item.OrderableConcentration |> mapFromOrderVariable
    dto.Dose <- item.Dose |> mapFromDose

    dto


let mapToComponent sns (dto : Order.Orderable.Component.Dto.Dto) : Component =
    Models.Order.Component.create
        dto.Id
        dto.Name
        dto.Shape
        (dto.ComponentQuantity |> mapToOrderVariable)
        (dto.OrderableQuantity |> mapToOrderVariable)
        (dto.OrderableCount |> mapToOrderVariable)
        (dto.OrderQuantity |> mapToOrderVariable)
        (dto.OrderCount |> mapToOrderVariable)
        (dto.OrderableConcentration |> mapToOrderVariable)
        (dto.Dose |> mapToDose)
        (dto.Items |> List.toArray |> Array.map (mapToItem sns))


// member val Id = "" with get, set
// member val Name = "" with get, set
// member val Shape = "" with get, set
// member val ComponentQuantity = OrderVariable.Dto.dto () with get, set
// member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
// member val OrderableCount = OrderVariable.Dto.dto () with get, set
// member val OrderQuantity = OrderVariable.Dto.dto () with get, set
// member val OrderCount = OrderVariable.Dto.dto () with get, set
// member val OrderableConcentration = OrderVariable.Dto.dto () with get, set
// member val Dose = Dose.Dto.dto () with get, set
// member val Items : Item.Dto.Dto list = [] with get, set
let mapFromComponent (comp : Component) : Order.Orderable.Component.Dto.Dto =
    let dto = Order.Orderable.Component.Dto.Dto()
    dto.Id <- comp.Id
    dto.Name <- comp.Name
    dto.Shape <- comp.Shape
    dto.ComponentQuantity <- comp.ComponentQuantity |> mapFromOrderVariable
    dto.OrderableQuantity <- comp.OrderableQuantity |> mapFromOrderVariable
    dto.OrderableCount <- comp.OrderableCount |> mapFromOrderVariable
    dto.OrderQuantity <- comp.OrderQuantity |> mapFromOrderVariable
    dto.OrderCount <- comp.OrderCount |> mapFromOrderVariable
    dto.OrderableConcentration <- comp.OrderableConcentration |> mapFromOrderVariable
    dto.Dose <- comp.Dose |> mapFromDose
    dto.Items <- comp.Items |> Array.toList |> List.map mapFromItem

    dto


let mapToOrderable sns (dto : Order.Orderable.Dto.Dto) : Orderable =
    Models.Order.Orderable.create
        dto.Name
        (dto.OrderableQuantity |> mapToOrderVariable)
        (dto.OrderQuantity |> mapToOrderVariable)
        (dto.OrderCount |> mapToOrderVariable)
        (dto.DoseCount |> mapToOrderVariable)
        (dto.Dose |> mapToDose)
        (dto.Components |> List.toArray |> Array.map (mapToComponent sns))


// member val Name = "" with get, set
// member val OrderableQuantity = OrderVariable.Dto.dto () with get, set
// member val OrderQuantity = OrderVariable.Dto.dto () with get, set
// member val OrderCount = OrderVariable.Dto.dto () with get, set
// member val DoseCount = OrderVariable.Dto.dto () with get, set
// member val Dose = Dose.Dto.dto () with get, set
// member val Components : Component.Dto.Dto list = [] with get, set
let mapFromOrderable id n (orderable : Orderable) : Order.Orderable.Dto.Dto =
    let dto = Order.Orderable.Dto.dto id n
    dto.OrderableQuantity <- orderable.OrderableQuantity |> mapFromOrderVariable
    dto.OrderQuantity <- orderable.OrderQuantity |> mapFromOrderVariable
    dto.OrderCount <- orderable.OrderCount |> mapFromOrderVariable
    dto.DoseCount <- orderable.DoseCount |> mapFromOrderVariable
    dto.Dose <- orderable.Dose |> mapFromDose
    dto.Components <- orderable.Components |> Array.toList |> List.map mapFromComponent

    dto


let mapToPrescription (dto : Order.Prescription.Dto.Dto) : Prescription =
    Models.Order.Prescription.create
        dto.IsOnce
        dto.IsOnceTimed
        dto.IsContinuous
        dto.IsDiscontinuous
        dto.IsTimed
        (dto.Frequency |> mapToOrderVariable)
        (dto.Time |> mapToOrderVariable)


let mapFromPrescription (prescription : Prescription) : Order.Prescription.Dto.Dto =
    let dto = Order.Prescription.Dto.Dto ()
    dto.IsOnce <- prescription.IsOnce
    dto.IsOnceTimed <- prescription.IsOnceTimed
    dto.IsDiscontinuous <- prescription.IsDiscontinuous
    dto.IsContinuous <- prescription.IsContinuous
    dto.IsTimed <- prescription.IsTimed
    dto.Frequency <- prescription.Frequency |> mapFromOrderVariable
    dto.Time <- prescription.Time |> mapFromOrderVariable

    dto


let mapToOrder sns (dto : Order.Dto.Dto) : Shared.Types.Order =
    Models.Order.create
        dto.Id
        (dto.Adjust |> mapToOrderVariable)
        (dto.Orderable |> (mapToOrderable sns))
        (dto.Prescription |> mapToPrescription)
        dto.Route
        (dto.Duration |> mapToOrderVariable)
        dto.Start
        dto.Stop


// member val Id = id with get, set
// member val Adjust = OrderVariable.Dto.dto () with get, set
// member val Orderable = Orderable.Dto.dto id n with get, set
// member val Prescription = Prescription.Dto.dto n with get, set
// member val Route = "" with get, set
// member val Duration = OrderVariable.Dto.dto () with get, set
// member val Start = DateTime.now () with get, set
// member val Stop : DateTime option = None with get, set
let mapFromOrder (order : Shared.Types.Order) : Order.Dto.Dto =
    let dto = Order.Dto.Dto(order.Id, order.Orderable.Name)
    dto.Adjust <- order.Adjust |> mapFromOrderVariable
    dto.Orderable <- order.Orderable |> mapFromOrderable order.Id order.Orderable.Name
    dto.Prescription <- order.Prescription |> mapFromPrescription
    dto.Route <- order.Route
    dto.Duration <- order.Duration |> mapFromOrderVariable
    dto.Start <- order.Start
    dto.Stop <- order.Stop

    dto


let mapFromSharedDoseType (dt: Shared.Types.DoseType) : Informedica.GenForm.Lib.Types.DoseType =
        match dt with
        | OnceTimed s -> s |> Informedica.GenForm.Lib.Types.OnceTimed
        | Once s -> s |> Informedica.GenForm.Lib.Types.Once
        | Timed s -> s |> Informedica.GenForm.Lib.Types.Timed
        | Discontinuous s -> s |> Informedica.GenForm.Lib.Types.Discontinuous
        | Continuous s -> s |> Informedica.GenForm.Lib.Types.Continuous
        | NoDoseType -> Informedica.GenForm.Lib.Types.NoDoseType



let mapToSharedDoseType (dt: Informedica.GenForm.Lib.Types.DoseType) : Shared.Types.DoseType =
        match dt with
        | Informedica.GenForm.Lib.Types.OnceTimed s -> s |> OnceTimed
        | Informedica.GenForm.Lib.Types.Once s -> s |> Once
        | Informedica.GenForm.Lib.Types.Timed s -> s |> Timed
        | Informedica.GenForm.Lib.Types.Discontinuous s -> s |> Discontinuous
        | Informedica.GenForm.Lib.Types.Continuous s -> s |> Continuous
        | Informedica.GenForm.Lib.Types.NoDoseType -> NoDoseType


let mapToIntake (intake : Informedica.GenOrder.Lib.Types.Intake) : Intake =
    let toTextItem =
        Option.map Models.ScenarioResult.parseTextItem
        >> (Option.defaultValue [||])
    {
        Volume = intake.Volume |> toTextItem
        Energy = intake.Energy |> toTextItem
        Protein = intake.Protein |> toTextItem
        Carbohydrate = intake.Carbohydrate |> toTextItem
        Fat = intake.Fat |> toTextItem
        Sodium = intake.Sodium |> toTextItem
        Potassium = intake.Potassium |> toTextItem
        Chloride = intake.Chloride |> toTextItem
        Calcium = intake.Calcium |> toTextItem
        Magnesium = intake.Magnesium |> toTextItem
        Phosphate = intake.Phosphate |> toTextItem
        Iron = intake.Iron |> toTextItem
        VitaminD = intake.VitaminD |> toTextItem
        Ethanol = intake.Ethanol |> toTextItem
        Propyleenglycol = intake.Propyleenglycol |> toTextItem
        BenzylAlcohol = intake.BenzylAlcohol |> toTextItem
        BoricAcid = intake.BoricAcid |> toTextItem
    }


let get (sr: ScenarioResult) =
    let msg stage (sr: ScenarioResult)=
        $"""
{stage}:
Patient: {sr |> mapPatient |> Patient.toString}
Indications: {sr.Filter.Indications |> Array.length}
Medications: {sr.Filter.Medications |> Array.length}
Routes: {sr.Filter.Routes |> Array.length}
Indication: {sr.Filter.Indication |> Option.defaultValue ""}
Medication: {sr.Filter.Medication |> Option.defaultValue ""}
Route: {sr.Filter.Route |> Option.defaultValue ""}
DoseType: {sr.Filter.DoseType}
Diluent: {sr.Filter.Diluent}
Scenarios: {sr.Scenarios |> Array.length}
"""

    ConsoleWriter.writeInfoMessage $"""{msg "processing" sr}""" true true

    let pat =
        sr
        |> mapPatient
        |> Patient.calcPMAge

    try
        let newResult =
            let patResult = Api.scenarioResult pat

            let dil =
                if sr.Filter.Diluent.IsSome then sr.Filter.Diluent
                else
                    sr.Scenarios
                    |> Array.tryExactlyOne
                    |> Option.bind _.Diluent

            { patResult with
                Filter =
                    { patResult.Filter with
                        Indications =
                            if sr.Filter.Indication |> Option.isSome then
                                [| sr.Filter.Indication |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Indications
                        Generics =
                            if sr.Filter.Medication |> Option.isSome then
                                [| sr.Filter.Medication |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Generics
                        Shapes =
                            if sr.Filter.Shape |> Option.isSome then
                                [| sr.Filter.Shape |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Shapes
                        Routes =
                            if sr.Filter.Route |> Option.isSome then
                                [| sr.Filter.Route |> Option.defaultValue "" |]
                            else
                                patResult.Filter.Routes
                        DoseTypes =
                            if sr.Filter.DoseType |> Option.isSome then
                                [| sr.Filter.DoseType |> Option.defaultValue NoDoseType |]
                                |> Array.map mapFromSharedDoseType
                            else
                                patResult.Filter.DoseTypes
                        Indication = sr.Filter.Indication
                        Generic = sr.Filter.Medication
                        Shape = sr.Filter.Shape
                        Route = sr.Filter.Route
                        DoseType = sr.Filter.DoseType |> Option.map mapFromSharedDoseType
                        Diluent = dil
                    }
            }
            |> Api.filter

        { sr with
            Filter =
                { sr.Filter with
                    Indications = newResult.Filter.Indications
                    Medications = newResult.Filter.Generics
                    Routes = newResult.Filter.Routes
                    DoseTypes = newResult.Filter.DoseTypes |> Array.map mapToSharedDoseType
                    Diluents = newResult.Filter.Diluents
                    Indication = newResult.Filter.Indication
                    Medication = newResult.Filter.Generic
                    Shape = newResult.Filter.Shape
                    Route = newResult.Filter.Route
                    DoseType = newResult.Filter.DoseType |> Option.map mapToSharedDoseType
                    Diluent = newResult.Filter.Diluent
                }

            Scenarios =
                newResult.Scenarios
                |> Array.map (fun sc ->
                    Models.ScenarioResult.createScenario
                        sc.Id
                        sc.Indication
                        sc.Diluents
                        sc.Components
                        sc.Items
                        sc.Shape
                        sc.Diluent
                        (sc.DoseType |> mapToSharedDoseType)
                        sc.Component
                        sc.Item
                        sc.Prescription
                        sc.Preparation
                        sc.Administration
                        (sc.Order |> (Order.Dto.toDto >> mapToOrder sc.Items >> Constrained))
                        sc.UseAdjust
                        sc.UseRenalRule
                        sc.RenalRule
                )
        }
        |> fun sr ->
            ConsoleWriter.writeInfoMessage $"""{msg "finished" sr}""" true true
            sr //|> Ok
    with
    | e ->
        ConsoleWriter.writeErrorMessage $"errored:\n{e}" true true
        sr //|> Ok
    |> fun sc ->
        { sc with
            DemoVersion =
                Env.getItem "GENPRES_PROD"
                |> Option.map (fun v -> v <> "1")
                |> Option.defaultValue true
        }
        |> Ok


let calcValues (ord : Order) =
    if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
        let path = $"{__SOURCE_DIRECTORY__}/log.txt"
        OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

    let sns =
        ord.Orderable.Components
        |> Array.collect _.Items
        |> Array.filter (_.IsAdditional >> not)
        |> Array.map _.Name

    try
        ord
        |> mapFromOrder
        |> Api.calc
        |> Result.map (mapToOrder sns)
    with
    | e ->
        ConsoleWriter.writeErrorMessage $"error calculating values from min incr max {e}" true true
        "error calculating values from min incr max"
        |> Error


let getIntake wghtInGram (ords: Order []) =
    let wghtInKg =
        wghtInGram
        |> Option.map BigRational.fromInt
        |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
        |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

    ords
    |> Array.map mapFromOrder
    |> Api.getIntake wghtInKg
    |> mapToIntake


let print (sc: ScenarioResult) =
    let msg stage (sc: ScenarioResult)=
        let s =
            sc.Scenarios
            |> Array.collect _.Prescription
        $"""
{stage}: {s}
"""

    ConsoleWriter.writeInfoMessage $"""{msg "printing" sc}""" true true

    let sc =
        { sc with
            Scenarios =
                sc.Scenarios
                |> Array.map (fun sc ->
                    let ord = sc.Order |> Models.OrderState.getOrder

                    let prs, prp, adm =
                        // only print the item quantities of the principal component
                        let sns =
                            match ord.Orderable.Components |> Array.tryHead with
                            | Some c ->
                                c.Items
                                |> Array.map _.Name
                                |> Array.filter (fun n -> Array.exists ((=) n) sc.Items)
                            | None -> [||]

                        ord
                        |> mapFromOrder
                        |> Order.Dto.fromDto
                        |> Order.Print.printOrderToTableFormat sc.UseAdjust true sns
                        |> fun (prs, prp, adm) ->
                            prs |> Array.map (Array.map (Api.replace >> Models.ScenarioResult.parseTextItem)),
                            prp |> Array.map (Array.map (Api.replace >> Models.ScenarioResult.parseTextItem)),
                            adm |> Array.map (Array.map (Api.replace >> Models.ScenarioResult.parseTextItem))
                    { sc with
                        Prescription = prs
                        Preparation = prp
                        Administration = adm
                    }
                )
        }
    ConsoleWriter.writeInfoMessage $"""{msg "finished printing" sc}""" true true
    sc


let solveOrder (ord : Order) =
    if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
        let path = $"{__SOURCE_DIRECTORY__}/log.txt"
        OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

    let sns =
        ord.Orderable.Components
        |> Array.collect _.Items
        |> Array.filter (_.IsAdditional >> not)
        |> Array.map _.Name

    ord
    |> mapFromOrder
    |> Order.Dto.fromDto
    |> Order.toString
    |> String.concat "\n"
    |> sprintf "solving order:\n%s"
    |> fun s -> ConsoleWriter.writeInfoMessage s true false

    try
        ord
        |> mapFromOrder
        |> Api.solve
        |> Result.map (mapToOrder sns)
        |> Result.mapError (fun (_, errs) ->
            let s =
                errs
                |> List.map string
                |> String.concat "\n"
            ConsoleWriter.writeErrorMessage $"error solving order\n{s}" true false
            s
        )
    with
    | e ->
        failwith $"error solving order\n{e}"