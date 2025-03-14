namespace ServerApi


module Mappers =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared.Types
    open Shared


    module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


    let mapPatient
        (sr: PrescriptionContext)
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


module Formulary =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineTime
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared.Types
    open Shared


    module ValueUnit = Informedica.GenUnits.Lib.ValueUnit


    let mapFormularyToFilter (form: Formulary)=
        { Filter.doseFilter with
            Generic = form.Generic
            Indication = form.Indication
            Route = form.Route
            Patient =
                match form.Patient with
                | Some pat ->
                    { Patient.patient with
                        Age =
                            pat
                            |> Models.Patient.getAgeInDays
                            |> Option.bind BigRational.fromFloat
                            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
                        Weight =
                            pat
                            |> Models.Patient.getWeightInKg
                            |> Option.bind BigRational.fromFloat
                            |> Option.map (ValueUnit.singleWithUnit Units.Weight.kiloGram)
                        GestAge =
                            pat
                            |> Models.Patient.getHeight
                            |> Option.map BigRational.fromInt
                            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
                    }
                | None -> Patient.patient
        }
        |> Filter.calcPMAge


    let selectIfOne sel xs =
        match sel, xs with
        | None, [|x|] -> Some x
        | _ -> sel


    let checkDoseRules pat (dsrs : DoseRule []) =
        let empt, rs =
            dsrs
            |> Array.distinctBy (fun dr -> dr.Generic, dr.Shape, dr.Route, dr.DoseType)
            |> Array.map (Check.checkDoseRule pat)
            |> Array.partition (fun c ->
                c.didPass |> Array.isEmpty &&
                c.didNotPass |> Array.isEmpty
            )

        rs
        |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
        |> Array.collect _.didNotPass
        |> Array.filter String.notEmpty
        |> Array.distinct
        |> function
            | [||] ->
                [|
                    for e in empt do
                        $"geen doseer bewaking gevonden voor {e.doseRule.Generic}"
                |]
                |> Array.distinct

            | xs -> xs


    let get (form : Formulary) =
        let filter = form |> mapFormularyToFilter

        let dsrs = Api.getDoseRules filter

        printfn $"found: {dsrs |> Array.length} dose rules"
        let form =
            { form with
                Generics = dsrs |> DoseRule.generics
                Indications = dsrs |> DoseRule.indications
                Routes = dsrs |> DoseRule.routes
                PatientCategories = dsrs |> DoseRule.patientCategories
            }
            |> fun form ->
                { form with
                    Generic = form.Generics |> selectIfOne form.Generic
                    Indication = form.Indications |> selectIfOne form.Indication
                    Route = form.Routes |> selectIfOne form.Route
                    PatientCategory = form.PatientCategories |> selectIfOne form.PatientCategory
                }
            |> fun form ->
                { form with
                    Markdown =
                        match form.Generic, form.Indication, form.Route with
                        | Some _, Some _, Some _ ->
                            writeInfoMessage $"start checking {dsrs |> Array.length} rules"

                            let s =
                                dsrs
                                |> checkDoseRules filter.Patient
                                |> Array.map (fun s ->
                                    match s |> String.split "\t" with
                                    | [| s1; _; p; s2 |] ->
                                        if dsrs |> Array.length = 1 then $"{s1} {s2}"
                                        else
                                            $"{s1} {p} {s2}"
                                    | _ -> s
                                )
                                |> Array.map (fun s -> $"* {s}")
                                |> String.concat "\n"
                                |> fun s -> if s |> String.isNullOrWhiteSpace then "Ok!" else s

                            writeInfoMessage $"finished checking {dsrs |> Array.length} rules"

                            dsrs
                            |> DoseRule.Print.toMarkdown
                            |> fun md ->
                                $"{md}\n\n### Doseer controle volgens de G-Standaard\n\n{s}"

                        | _ -> ""
                }

        Ok form


module Parenteralia =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib

    type Parenteralia = Shared.Types.Parenteralia

    module Api = Informedica.GenOrder.Lib.Api


    let get (par : Parenteralia) : Result<Parenteralia, string> =
        writeInfoMessage $"getting parenteralia for {par.Generic}"

        let srs =
            Api.getSolutionRules
                par.Generic
                par.Shape
                par.Route

        let gens = srs |> SolutionRule.generics
        let shps = srs |> SolutionRule.shapes
        let rtes = srs |> SolutionRule.routes

        { par with
            Generics = gens
            Shapes = shps
            Routes = rtes
            Generic =
                if gens |> Array.length = 1 then Some gens[0]
                else
                    par.Generic
            Shape =
                if shps |> Array.length = 1 then Some shps[0]
                else
                    par.Shape
            Route =
                if rtes |> Array.length = 1 then Some rtes[0]
                else
                    par.Route

            Markdown =
                if par.Generic |> Option.isNone then ""
                else
                    srs
                    |> SolutionRule.Print.toMarkdown ""
        }
        |> Ok


module Order =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open Informedica.GenUnits.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Mappers


    let calcValues (ord : Order) =
        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        let sns =
            ord.Orderable.Components
            |> Array.collect _.Items
            |> Array.filter (_.IsAdditional >> not)
            |> Array.map _.Name

        let dto = ord |> mapFromOrder

        dto
        |> Order.Dto.toString
        |> sprintf "calc values:\n%s"
        |> writeInfoMessage

        try
            dto
            |> Api.calcOrderValues
            |> Result.map (Order.Dto.toDto >> mapToOrder sns)
            |> Result.map Calculated
        with
        | e ->
            writeErrorMessage $"error calculating values from min incr max {e}"
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


    let solveOrder (ord : Order) =
        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        let sns =
            ord.Orderable.Components
            |> Array.collect _.Items
            |> Array.filter (_.IsAdditional >> not)
            |> Array.map _.Name

        let dto = ord |> mapFromOrder

        dto
        |> Order.Dto.toString
        |> sprintf "solving order:\n%s"
        |> writeInfoMessage

        try
            let ord =
                ord
                |> mapFromOrder
                |> Api.solveOrder

            let state =
                match ord with
                | Ok ord ->
                    if ord |> Order.isSolved then Solved else Calculated
                | Error _ -> Constrained

            ord
            |> Result.map (Order.Dto.toDto >> mapToOrder sns >> state)
            |> Result.mapError (fun (_, errs) ->
                let s =
                    errs
                    |> List.map string
                    |> String.concat "\n"
                writeErrorMessage $"error solving order\n{s}"
                s
            )
        with
        | e ->
            failwith $"error solving order\n{e}"


module PrescriptionResult =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared.Types
    open Shared
    open Mappers

    let toString stage (pr: PrescriptionContext)=
            $"""
    {stage}:
    Patient: {pr |> mapPatient |> Patient.toString}
    Indications: {pr.Filter.Indications |> Array.length}
    Medications: {pr.Filter.Medications |> Array.length}
    Routes: {pr.Filter.Routes |> Array.length}
    Indication: {pr.Filter.Indication |> Option.defaultValue ""}
    Medication: {pr.Filter.Medication |> Option.defaultValue ""}
    Route: {pr.Filter.Route |> Option.defaultValue ""}
    DoseType: {pr.Filter.DoseType}
    Diluent: {pr.Filter.Diluent}
    Scenarios: {pr.Scenarios |> Array.length}
    """


    let get (pr: PrescriptionContext) =
        let pat =
            pr
            |> mapPatient
            |> Patient.calcPMAge

        try
            let newResult =
                let patResult = PrescriptionResult.create pat

                let dil =
                    if pr.Filter.Diluent.IsSome then pr.Filter.Diluent
                    else
                        pr.Scenarios
                        |> Array.tryExactlyOne
                        |> Option.bind _.Diluent

                { patResult with
                    Filter =
                        { patResult.Filter with
                            Indications =
                                if pr.Filter.Indication |> Option.isSome then
                                    [| pr.Filter.Indication |> Option.defaultValue "" |]
                                else
                                    patResult.Filter.Indications
                            Generics =
                                if pr.Filter.Medication |> Option.isSome then
                                    [| pr.Filter.Medication |> Option.defaultValue "" |]
                                else
                                    patResult.Filter.Generics
                            Shapes =
                                if pr.Filter.Shape |> Option.isSome then
                                    [| pr.Filter.Shape |> Option.defaultValue "" |]
                                else
                                    patResult.Filter.Shapes
                            Routes =
                                if pr.Filter.Route |> Option.isSome then
                                    [| pr.Filter.Route |> Option.defaultValue "" |]
                                else
                                    patResult.Filter.Routes
                            DoseTypes =
                                if pr.Filter.DoseType |> Option.isSome then
                                    [| pr.Filter.DoseType |> Option.defaultValue NoDoseType |]
                                    |> Array.map mapFromSharedDoseType
                                else
                                    patResult.Filter.DoseTypes
                            Indication = pr.Filter.Indication
                            Generic = pr.Filter.Medication
                            Shape = pr.Filter.Shape
                            Route = pr.Filter.Route
                            DoseType = pr.Filter.DoseType |> Option.map mapFromSharedDoseType
                            Diluent = dil
                        }
                }
                |> Api.evaluate

            { pr with
                Filter =
                    { pr.Filter with
                        Indications = newResult.Filter.Indications
                        Medications = newResult.Filter.Generics
                        Routes = newResult.Filter.Routes
                        DoseTypes = newResult.Filter.DoseTypes |> Array.map mapToSharedDoseType
                        Diluents = newResult.Filter.Diluents
                        Components = newResult.Filter.Components
                        Indication = newResult.Filter.Indication
                        Medication = newResult.Filter.Generic
                        Shape = newResult.Filter.Shape
                        Route = newResult.Filter.Route
                        DoseType = newResult.Filter.DoseType |> Option.map mapToSharedDoseType
                        Diluent = newResult.Filter.Diluent
                        SelectedComponents = newResult.Filter.SelectedComponents
                    }

                Scenarios =
                    newResult.Scenarios
                    |> Array.map (fun sc ->
                        Models.OrderScenario.create
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
        with
        | e ->
            writeErrorMessage $"errored:\n{e}"
            pr //|> Ok
        |> fun sc ->
            { sc with
                DemoVersion =
                    Env.getItem "GENPRES_PROD"
                    |> Option.map (fun v -> v <> "1")
                    |> Option.defaultValue true
            }
            |> Ok


    let print (pr: PrescriptionContext) =
        { pr with
            Scenarios =
                pr.Scenarios
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
                            prs |> Array.map (Array.map (OrderScenario.replace >> Models.OrderScenario.parseTextItem)),
                            prp |> Array.map (Array.map (OrderScenario.replace >> Models.OrderScenario.parseTextItem)),
                            adm |> Array.map (Array.map (OrderScenario.replace >> Models.OrderScenario.parseTextItem))
                    { sc with
                        Prescription = prs
                        Preparation = prp
                        Administration = adm
                    }
                )
        }


module Message =

    open Shared
    open Shared.Types
    open Shared.Api
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime

    let printMsg msg pr =
        pr
        |> PrescriptionResult.toString msg
        |> writeInfoMessage
        pr


    let processOrders (pr : PrescriptionContext) =
        let mutable errors = [||]

        let scenarios =
            pr.Scenarios
            |> Array.map (fun sc ->
                { sc with
                    Order =
                        let ord, f =
                            match sc.Order with
                            | Constrained o -> o, Order.calcValues
                            | Calculated o  -> o, Order.solveOrder
                            | Solved o      -> o, Order.calcValues

                        match ord |> f with
                        | Ok ord -> ord
                        | Error errs ->
                            errors <- Array.append errors [| errs |]
                            sc.Order
                }
            )

        let calculated =
            { pr with
                Scenarios = scenarios

                Intake =
                    let w = pr.Patient |> Models.Patient.getWeight

                    scenarios
                    |> Array.map (_.Order >> Models.OrderState.getOrder)
                    |> Order.getIntake w
            }

        if errors |> Array.isEmpty then calculated |> Ok
        else errors |> Error


    let checkDiluent (pr: PrescriptionContext) =
        pr.Scenarios
        |> Array.tryExactlyOne
        |> Option.map (fun sc ->
            let ord = sc.Order |> Models.OrderState.getOrder

            match sc.Diluent with
            | None -> true
            | Some dil ->
                // check if diluent is used in order
                ord.Orderable.Components
                |> Array.map _.Name
                |> Array.exists ((=) dil)
        )
        |> Option.defaultValue true


    let checkComponents (pr: PrescriptionContext) =
        pr.Scenarios
        |> Array.tryExactlyOne
        |> Option.map (fun sc ->
            let ord = sc.Order |> Models.OrderState.getOrder

            if pr.Filter.SelectedComponents |> Array.isEmpty then true
            else
                // check if there is a component that is used
                // not in selected components
                ord.Orderable.Components
                |> Array.map _.Name
                |> Array.sort
                |> ((=) (pr.Filter.SelectedComponents |> Array.sort))
        )
        |> Option.defaultValue true


    let processMsg msg =
        match msg with
        | PrescriptionContextMsg pr ->
            pr |> printMsg "prescription context msg start" |> ignore

            if pr.Scenarios |> Array.isEmpty then
                pr
                |> PrescriptionResult.get

            else
                if pr |> checkDiluent ||
                   pr |> checkComponents then
                    pr
                    |> processOrders
                    |> Result.map PrescriptionResult.print
                else
                    pr
                    |> PrescriptionResult.get

            |> Result.map (fun pr ->
                { pr with
                    Intake =
                        let w = pr.Patient |> Models.Patient.getWeight
                        pr.Scenarios
                        |> Array.map (_.Order >> Models.OrderState.getOrder)
                        |> Order.getIntake w
                }
            )
            |> Result.map (printMsg "prescription context msg finish")
            |> Result.map PrescriptionContextMsg

        | TreatmentPlanMsg tp ->
            match tp.Selected with
            | Some os ->
                os
                |> Models.PrescriptionContext.fromOrderScenario
                |> printMsg "TreatmentPlan started"
                |> processOrders
                |> Result.map PrescriptionResult.print
                |> Result.map (fun pr ->
                    pr |> printMsg "TreatmentPlan finished" |> ignore
                    let newOsc = pr.Scenarios |> Array.tryExactlyOne

                    { tp with
                        Selected = newOsc
                        Scenarios =
                            match newOsc with
                            | None -> tp.Scenarios
                            | Some newOsc ->
                                tp.Scenarios
                                |> Array.map (fun sc ->
                                    if sc |> Models.OrderScenario.eqs newOsc then newOsc
                                    else sc
                                )
                    }
                )
                |> Result.defaultValue tp
            | None -> tp
            |> fun tp ->
                { tp with
                    Intake =
                        let w = tp.Patient |> Models.Patient.getWeight

                        tp.Scenarios
                        |> Array.map (_.Order >> Models.OrderState.getOrder)
                        |> Order.getIntake w
                }
                |> TreatmentPlanMsg
                |> Ok

        | FormularyMsg form ->
            form
            |> Formulary.get
            |> Result.map FormularyMsg

        | ParenteraliaMsg par ->
            par
            |> Parenteralia.get
            |> Result.mapError Array.singleton
            |> Result.map ParenteraliaMsg


[<AutoOpen>]
module ApiImpl =

    open Shared.Api


    /// An implementation of the Shared IServerApi protocol.
    let serverApi: IServerApi =
        {
            processMessage =
                fun msg ->
                    async {
                        return msg |> Message.processMsg
                    }

            testApi =
                fun () ->
                    async {
                        return "Hello world!"
                    }
        }