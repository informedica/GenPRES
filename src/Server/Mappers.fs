module Mappers


open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared


module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


let mapPatient
    (sr: PrescriptionResult)
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



let mapToValueUnit (dto : ValueUnit.Dto.Dto) : Types.ValueUnit =
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


let mapFromValueUnit (vu : Types.ValueUnit) : ValueUnit.Dto.Dto =
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


let mapToOrderVariable (dto : OrderVariable.Dto.Dto) : Types.OrderVariable =
    Models.Order.OrderVariable.create
        dto.Name
        (dto.Constraints |> mapToVariable)
        (dto.Variable |> mapToVariable)


let mapFromOrderVariable (ov : Types.OrderVariable) : OrderVariable.Dto.Dto =
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


let mapToOrder sns (dto : Order.Dto.Dto) : Types.Order =
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
let mapFromOrder (order : Types.Order) : Order.Dto.Dto =
    let dto = Order.Dto.Dto(order.Id, order.Orderable.Name)
    dto.Adjust <- order.Adjust |> mapFromOrderVariable
    dto.Orderable <- order.Orderable |> mapFromOrderable order.Id order.Orderable.Name
    dto.Prescription <- order.Prescription |> mapFromPrescription
    dto.Route <- order.Route
    dto.Duration <- order.Duration |> mapFromOrderVariable
    dto.Start <- order.Start
    dto.Stop <- order.Stop

    dto


let mapFromSharedDoseType (dt: Types.DoseType) : Informedica.GenForm.Lib.Types.DoseType =
        match dt with
        | OnceTimed s -> s |> Informedica.GenForm.Lib.Types.OnceTimed
        | Once s -> s |> Informedica.GenForm.Lib.Types.Once
        | Timed s -> s |> Informedica.GenForm.Lib.Types.Timed
        | Discontinuous s -> s |> Informedica.GenForm.Lib.Types.Discontinuous
        | Continuous s -> s |> Informedica.GenForm.Lib.Types.Continuous
        | NoDoseType -> Informedica.GenForm.Lib.Types.NoDoseType



let mapToSharedDoseType (dt: Informedica.GenForm.Lib.Types.DoseType) : Types.DoseType =
        match dt with
        | Informedica.GenForm.Lib.Types.OnceTimed s -> s |> OnceTimed
        | Informedica.GenForm.Lib.Types.Once s -> s |> Once
        | Informedica.GenForm.Lib.Types.Timed s -> s |> Timed
        | Informedica.GenForm.Lib.Types.Discontinuous s -> s |> Discontinuous
        | Informedica.GenForm.Lib.Types.Continuous s -> s |> Continuous
        | Informedica.GenForm.Lib.Types.NoDoseType -> NoDoseType


let mapToIntake (intake : Informedica.GenOrder.Lib.Types.Intake) : Intake =
    let toTextItem =
        Option.map Models.OrderScenario.parseTextItem
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