module ScenarioResult


open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


open Shared.Types
open Shared.Api

module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


let mapToValueUnit (dto : Informedica.GenUnits.Lib.ValueUnit.Dto.Dto) : Shared.Types.ValueUnit =
    Shared.Order.ValueUnit.create
        dto.Value
        dto.Unit "" true ""


let mapFromValueUnit (vu : Shared.Types.ValueUnit) : Informedica.GenUnits.Lib.ValueUnit.Dto.Dto =
    let dto = ValueUnit.Dto.dto ()
    dto.Value <- vu.Value
    dto.Group <- vu.Group
    dto.Language <- vu.Language
    dto.Unit <- vu.Unit

    dto

let mapToVariable (dto: Informedica.GenSolver.Lib.Variable.Dto.Dto) : Variable =
    Shared.Order.Variable.create dto.Name dto.IsNonZeroNegative
        (dto.Min |> Option.map mapToValueUnit)
        dto.MinIncl
        (dto.Incr |> Option.map mapToValueUnit)
        (dto.Max |> Option.map mapToValueUnit)
        dto.MaxIncl
        (dto.Vals |> Option.map mapToValueUnit)


let mapFromVariable (var : Variable) : Informedica.GenSolver.Lib.Variable.Dto.Dto =
    let dto = Informedica.GenSolver.Lib.Variable.Dto.dto ()
    dto.Name <- var.Name
    dto.IsNonZeroNegative <- var.IsNonZeroNegative
    dto.Min <- var.Min |> Option.map mapFromValueUnit
    dto.MinIncl <- var.MinIncl
    dto.Incr <- var.Incr |> Option.map mapFromValueUnit
    dto.Max <- var.Max |> Option.map mapFromValueUnit
    dto.MaxIncl <- var.MaxIncl
    dto.Vals <- var.Vals |> Option.map mapFromValueUnit

    dto


let mapToOrderVariable (dto : OrderVariable.Dto.Dto) : Shared.Types.OrderVariable =
    Shared.Order.OrderVariable.create
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
    Shared.Order.Dose.create
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

let mapToItem (dto : Order.Orderable.Item.Dto.Dto) : Item =
    Shared.Order.Item.create
        dto.Name
        (dto.ComponentQuantity |> mapToOrderVariable)
        (dto.OrderableQuantity |> mapToOrderVariable)
        (dto.ComponentConcentration |> mapToOrderVariable)
        (dto.OrderableConcentration |> mapToOrderVariable)
        (dto.Dose |> mapToDose)


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


let mapToComponent (dto : Order.Orderable.Component.Dto.Dto) : Component =
    Shared.Order.Component.create
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
        (dto.Items |> List.toArray |> Array.map mapToItem)


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


let mapToOrderable (dto : Order.Orderable.Dto.Dto) : Orderable =
    Shared.Order.Orderable.create
        dto.Name
        (dto.OrderableQuantity |> mapToOrderVariable)
        (dto.OrderQuantity |> mapToOrderVariable)
        (dto.OrderCount |> mapToOrderVariable)
        (dto.DoseCount |> mapToOrderVariable)
        (dto.Dose |> mapToDose)
        (dto.Components |> List.toArray |> Array.map mapToComponent)


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
    Shared.Order.Prescription.create
        dto.IsContinuous
        dto.IsDiscontinuous
        dto.IsTimed
        (dto.Frequency |> mapToOrderVariable)
        (dto.Time |> mapToOrderVariable)


let mapFromPrescription (prescription : Prescription) : Order.Prescription.Dto.Dto =
    let dto = Order.Prescription.Dto.Dto ()
    dto.IsDiscontinuous <- prescription.IsDiscontinuous
    dto.IsContinuous <- prescription.IsContinuous
    dto.IsTimed <- prescription.IsTimed
    dto.Frequency <- prescription.Frequency |> mapFromOrderVariable
    dto.Time <- prescription.Time |> mapFromOrderVariable

    dto


let mapToOrder (dto : Order.Dto.Dto) : Shared.Types.Order =
    Shared.Order.create
        dto.Id
        (dto.Adjust |> mapToOrderVariable)
        (dto.Orderable |> mapToOrderable)
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

let get (sc: ScenarioResult) =
    let msg stage (sc: ScenarioResult)=
        $"""
{stage}:
Patient: {sc.Age} days, {sc.Weight} kg, {sc.Height} cm, CVL {sc.CVL}, {sc.GestAge} days
Indications: {sc.Indications |> Array.length}
Medications: {sc.Medications |> Array.length}
Routes: {sc.Routes |> Array.length}
Indication: {sc.Indication |> Option.defaultValue ""}
Medication: {sc.Medication |> Option.defaultValue ""}
Route: {sc.Route |> Option.defaultValue ""}
Scenarios: {sc.Scenarios |> Array.length}
"""

    ConsoleWriter.writeInfoMessage $"""{msg "processing" sc}""" true true

    let pat =
        { Patient.patient with
            Department = "ICK"
            Age =
                sc.Age
                |> Option.bind BigRational.fromFloat
            GestAge =
                sc.GestAge
                |> Option.map BigRational.fromInt
            Weight =
                sc.Weight
                |> Option.map (fun w -> w * 1000.)
                |> Option.bind BigRational.fromFloat
            Height =
                sc.Height
                |> Option.bind BigRational.fromFloat
            VenousAccess = if sc.CVL then VenousAccess.CVL else VenousAccess.AnyAccess
        }

    try
        let newSc =
            let r = Demo.scenarioResult pat
            { Demo.scenarioResult pat with
                Indications =
                    if sc.Indication |> Option.isSome then
                        [| sc.Indication |> Option.defaultValue "" |]
                    else
                        r.Indications
                Generics =
                    if sc.Medication |> Option.isSome then
                        [| sc.Medication |> Option.defaultValue "" |]
                    else
                        r.Generics
                Shapes =
                    if sc.Shape |> Option.isSome then
                        [| sc.Shape |> Option.defaultValue "" |]
                    else
                        r.Shapes
                Routes =
                    if sc.Route |> Option.isSome then
                        [| sc.Route |> Option.defaultValue "" |]
                    else
                        r.Routes
                Indication = sc.Indication
                Generic = sc.Medication
                Shape = sc.Shape
                Route = sc.Route
            }
            |> Demo.filter

        let sc =
            { sc with
                Indications = newSc.Indications
                Medications = newSc.Generics
                Routes = newSc.Routes
                Indication = newSc.Indication
                Medication = newSc.Generic
                Shape = newSc.Shape
                Route = newSc.Route
                Scenarios =
                    newSc.Scenarios
                    |> Array.map (fun sc ->
                        Shared.ScenarioResult.createScenario
                            sc.Shape
                            sc.DoseType
                            sc.Prescription
                            sc.Preparation
                            sc.Administration
                            (sc.Order |> Option.map (Order.Dto.toDto >> mapToOrder))
                    )
            }
        ConsoleWriter.writeInfoMessage $"""{msg "finished" sc}""" true true
        sc |> Ok
    with
    | e ->
        ConsoleWriter.writeErrorMessage $"errored:\n{e}" true true
        sc |> Ok


let calcMinIncrMaxToValues (ord : Order) =
    try
        ord
        |> mapFromOrder
        |> Order.Dto.fromDto
        |> Order.minIncrMaxToValues
        |> Order.Dto.toDto
        |> mapToOrder
        |> Ok
    with
    | e ->
        printfn $"error calculating values from min incr max {e}"
        "error calculating values from min incr max"
        |> Error


let print (sc: ScenarioResult) =
    let msg stage (sc: ScenarioResult)=
        let s =
            sc.Scenarios
            |> Array.collect (fun sc -> sc.Prescription)
        $"""
{stage}: {s}
"""

    ConsoleWriter.writeInfoMessage $"""{msg "printing" sc}""" true true

    let sc =
        { sc with
            Scenarios =
                sc.Scenarios
                |> Array.map (fun sc ->
                    let prs, prp, adm =
                        sc.Order
                        |> Option.map (fun ord ->
                            let sn =
                                ord.Orderable.Components
                                |> Array.collect (fun c -> c.Items |> Array.map (fun i -> i.Name))

                            ord
                            |> mapFromOrder
                            |> Order.Dto.fromDto
                            |> Order.Markdown.printPrescription sn
                            |> fun (prs, prp, adm) ->
                                prs |> Demo.replace |> Shared.ScenarioResult.parseTextItem,
                                prp |> Demo.replace |> Shared.ScenarioResult.parseTextItem,
                                adm |> Demo.replace |> Shared.ScenarioResult.parseTextItem

                        )
                        |> Option.defaultValue (sc.Prescription, sc.Preparation, sc.Administration)
                    { sc with
                        Prescription = prs
                        Preparation = prp
                        Administration = adm
                    }
                )
        }
    ConsoleWriter.writeInfoMessage $"""{msg "finished printing" sc}""" true true
    sc |> Ok



let solveOrder (ord : Order) =
    let path = $"{__SOURCE_DIRECTORY__}/log.txt"
    OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

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
        |> Order.Dto.fromDto
        |> Order.solveOrder false OrderLogger.logger.Logger
        |> Result.map (fun o ->
            o
            |> Order.toString
            |> String.concat "\n"
            |> sprintf "solved order:\n%s"
            |> fun s -> ConsoleWriter.writeInfoMessage s true false

            o
        )
        |> Result.map (Order.Dto.toDto >> mapToOrder)
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
