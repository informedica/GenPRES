namespace ServerApi


module Mappers =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared.Types
    open Shared


    module Order =


        module ValueUnit = Informedica.GenUnits.Lib.ValueUnit



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
            Models.Order.Variable.create dto.Name dto.IsNonZeroPositive
                (dto.MinOpt |> Option.map mapToValueUnit)
                dto.MinIncl
                (dto.IncrOpt |> Option.map mapToValueUnit)
                (dto.MaxOpt |> Option.map mapToValueUnit)
                dto.MaxIncl
                (dto.ValsOpt |> Option.map mapToValueUnit)


        let mapFromVariable (var : Variable) : Informedica.GenSolver.Lib.Variable.Dto.Dto =
            let dto = Informedica.GenSolver.Lib.Variable.Dto.dto ()

            if var.IsNonZeroPositive && var.Incr.IsNone then
                dto.IsNonZeroPositive <- true

            else
                dto.Name <- var.Name
                dto.IsNonZeroPositive <- var.IsNonZeroPositive
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


        let mapFromOrderToShared sns (dto : Order.Dto.Dto) : Types.Order =
            Models.Order.create
                dto.Id
                (dto.Adjust |> mapToOrderVariable)
                (dto.Orderable |> (mapToOrderable sns))
                (dto.Prescription |> mapToPrescription)
                dto.Route
                (dto.Duration |> mapToOrderVariable)
                dto.Start
                dto.Stop


        let mapFromSharedToOrder (order : Types.Order) : Order.Dto.Dto =
            let dto = Order.Dto.Dto(order.Id, order.Orderable.Name)

            dto.Adjust <- order.Adjust |> mapFromOrderVariable
            dto.Orderable <- order.Orderable |> mapFromOrderable order.Id order.Orderable.Name
            dto.Prescription <- order.Prescription |> mapFromPrescription
            dto.Route <- order.Route
            dto.Duration <- order.Duration |> mapFromOrderVariable
            dto.Start <- order.Start
            dto.Stop <- order.Stop

            dto


    let mapFromSharedDoseTypeToOrderDoseType (dt: Types.DoseType) : Informedica.GenForm.Lib.Types.DoseType =
            match dt with
            | OnceTimed s -> s |> Informedica.GenForm.Lib.Types.OnceTimed
            | Once s -> s |> Informedica.GenForm.Lib.Types.Once
            | Timed s -> s |> Informedica.GenForm.Lib.Types.Timed
            | Discontinuous s -> s |> Informedica.GenForm.Lib.Types.Discontinuous
            | Continuous s -> s |> Informedica.GenForm.Lib.Types.Continuous
            | NoDoseType -> Informedica.GenForm.Lib.Types.NoDoseType


    let mapFromOrderDoseTypeToSharedDoseType (dt: Informedica.GenForm.Lib.Types.DoseType) : Types.DoseType =
            match dt with
            | Informedica.GenForm.Lib.Types.OnceTimed s -> s |> OnceTimed
            | Informedica.GenForm.Lib.Types.Once s -> s |> Once
            | Informedica.GenForm.Lib.Types.Timed s -> s |> Timed
            | Informedica.GenForm.Lib.Types.Discontinuous s -> s |> Discontinuous
            | Informedica.GenForm.Lib.Types.Continuous s -> s |> Continuous
            | Informedica.GenForm.Lib.Types.NoDoseType -> NoDoseType


    let mapFromSharedPatient
        (pat: Types.Patient)
        =
        { Patient.patient with
            Department =
                pat.Department
                |> Option.defaultValue "ICK"
                |> Some
            Age =
                pat
                |> Models.Patient.getAgeInDays
                |> Option.bind BigRational.fromFloat
                |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
            GestAge =
                pat
                |> Models.Patient.getGestAgeInDays
                |> Option.map BigRational.fromInt
                |> Option.map (ValueUnit.singleWithUnit Units.Time.day)
            Weight =
                pat
                |> Models.Patient.getWeight
                |> Option.map (int >> BigRational.fromInt)
                |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
                |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)
            Height =
                pat
                |> Models.Patient.getHeight
                |> Option.map (int >> BigRational.fromInt)
                |> Option.map (ValueUnit.singleWithUnit Units.Height.centiMeter)
            Gender =
                match pat.Gender with
                | Male -> Informedica.GenForm.Lib.Types.Male
                | Female -> Informedica.GenForm.Lib.Types.Female
                | UnknownGender -> AnyGender
            Locations =
                pat.Access
                // TODO make proper mapping
                |> List.choose (fun a ->
                    match a with
                    | CVL -> Informedica.GenForm.Lib.Types.CVL |> Some
                    | PVL -> Informedica.GenForm.Lib.Types.PVL |> Some
                    | _ -> None
                )
            RenalFunction =
                pat.RenalFunction
                |> Option.map (fun rf ->
                    match rf with
                    | EGFR(min, max) -> Informedica.GenForm.Lib.Types.RenalFunction.EGFR(min, max)
                    | IntermittendHemoDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.IntermittentHemodialysis
                    | ContinuousHemoDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.ContinuousHemodialysis
                    | PeritionealDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.PeritonealDialysis
                )
        }
        |> Patient.calcPMAge


    let mapFromShared pat (ctx : OrderContext)  : Informedica.GenOrder.Lib.Types.OrderContext =

        let mappedCtx = OrderContext.create pat

        { mappedCtx with
            Scenarios =
                ctx.Scenarios
                |> Array.mapi (fun i sc ->
                    let ord =
                        sc.Order
                        |> Order.mapFromSharedToOrder
                        |> Order.Dto.fromDto

                    OrderScenario.create
                        i
                        sc.Indication
                        sc.Name
                        sc.Shape
                        sc.Route
                        (sc.DoseType |> mapFromSharedDoseTypeToOrderDoseType)
                        sc.Diluent
                        sc.Component
                        sc.Item
                        sc.Diluents
                        sc.Components
                        sc.Items
                        ord
                        sc.UseAdjust
                        sc.UseRenalRule
                        sc.RenalRule
                        sc.ProductIds
                )

            Filter =
                { mappedCtx.Filter with
                    Indications =
                        if ctx.Filter.Indication |> Option.isSome then
                            [| ctx.Filter.Indication |> Option.defaultValue "" |]
                        else
                            mappedCtx.Filter.Indications
                    Generics =
                        if ctx.Filter.Medication |> Option.isSome then
                            [| ctx.Filter.Medication |> Option.defaultValue "" |]
                        else
                            mappedCtx.Filter.Generics
                    Routes =
                        if ctx.Filter.Route |> Option.isSome then
                            [| ctx.Filter.Route |> Option.defaultValue "" |]
                        else
                            mappedCtx.Filter.Routes
                    Shapes =
                        if ctx.Filter.Shape |> Option.isSome then
                            [| ctx.Filter.Shape |> Option.defaultValue "" |]
                        else
                            mappedCtx.Filter.Shapes
                    DoseTypes =
                        if ctx.Filter.DoseType |> Option.isSome then
                            [| ctx.Filter.DoseType |> Option.defaultValue NoDoseType |]
                            |> Array.map mapFromSharedDoseTypeToOrderDoseType
                        else
                            mappedCtx.Filter.DoseTypes
                    Diluents = ctx.Filter.Diluents
                    Components = ctx.Filter.Components
                    Indication = ctx.Filter.Indication
                    Generic = ctx.Filter.Medication
                    Shape = ctx.Filter.Shape
                    Route = ctx.Filter.Route
                    DoseType = ctx.Filter.DoseType |> Option.map mapFromSharedDoseTypeToOrderDoseType
                    Diluent = ctx.Filter.Diluent
                    SelectedComponents = ctx.Filter.SelectedComponents
                }
        }


    let mapToShared ctx (newCtx : Informedica.GenOrder.Lib.Types.OrderContext) : OrderContext =
            { ctx with
                Filter =
                    { ctx.Filter with
                        Indications = newCtx.Filter.Indications
                        Medications = newCtx.Filter.Generics
                        Routes = newCtx.Filter.Routes
                        Shapes = newCtx.Filter.Shapes
                        DoseTypes = newCtx.Filter.DoseTypes |> Array.map mapFromOrderDoseTypeToSharedDoseType
                        Diluents = newCtx.Filter.Diluents
                        Components = newCtx.Filter.Components
                        Indication = newCtx.Filter.Indication
                        Medication = newCtx.Filter.Generic
                        Shape = newCtx.Filter.Shape
                        Route = newCtx.Filter.Route
                        DoseType = newCtx.Filter.DoseType |> Option.map mapFromOrderDoseTypeToSharedDoseType
                        Diluent = newCtx.Filter.Diluent
                        SelectedComponents = newCtx.Filter.SelectedComponents
                    }

                Scenarios =
                    newCtx.Scenarios
                    |> Array.map (fun sc ->
                        Models.OrderScenario.create
                            sc.Name
                            sc.Indication
                            sc.Shape
                            sc.Route
                            (sc.DoseType |> mapFromOrderDoseTypeToSharedDoseType)
                            sc.Diluent
                            sc.Component
                            sc.Item
                            sc.Diluents
                            sc.Components
                            sc.Items
                            sc.Prescription
                            sc.Preparation
                            sc.Administration
                            (sc.Order |> (Order.Dto.toDto >> Order.mapFromOrderToShared sc.Items))
                            sc.UseAdjust
                            sc.UseRenalRule
                            sc.RenalRule
                            sc.ProductsIds
                    )
            }


    let mapToIntake (intake : Informedica.GenOrder.Lib.Types.Totals) : Totals =
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
            Shape = form.Shape
            DoseType = form.DoseType |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
            Patient =
                form.Patient
                |> Option.map Mappers.mapFromSharedPatient
                |> Option.defaultValue Patient.patient
        }
        |> Filter.calcPMAge


    let selectIfOne sel xs =
        match sel, xs with
        | None, [|x|] -> Some x
        | _ -> sel


    let checkDoseRules pat (dsrs : DoseRule []) =
        let routeMapping = Informedica.GenForm.Lib.Api.getRouteMappings ()

        let empt, rs =
            dsrs
            |> Array.distinctBy (fun dr -> dr.Generic, dr.Shape, dr.Route, dr.DoseType)
            |> Array.map (Check.checkDoseRule routeMapping pat)
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

        $"""

Formulary filter:
Patient: {filter.Patient |> Patient.toString}
Indication: {filter.Indication |> Option.defaultValue ""}
Generic: {filter.Generic |> Option.defaultValue ""}
Route: {filter.Route |> Option.defaultValue ""}
Shape: {filter.Shape |> Option.defaultValue ""}
DoseType : {filter.DoseType |> Option.map DoseType.toDescription |> Option.defaultValue ""}

"""
        |> writeInfoMessage

        let dsrs = Formulary.getDoseRules filter

        writeInfoMessage $"Found: {dsrs |> Array.length} formulary dose rules"
        let form =
            { form with
                Generics = dsrs |> DoseRule.generics
                Indications = dsrs |> DoseRule.indications
                Routes = dsrs |> DoseRule.routes
                Shapes = dsrs |> DoseRule.shapes
                DoseTypes = dsrs |> DoseRule.doseTypes |> Array.map Mappers.mapFromOrderDoseTypeToSharedDoseType
                PatientCategories = dsrs |> DoseRule.patientCategories
            }
            |> fun form ->
                { form with
                    Generic = form.Generics |> selectIfOne form.Generic
                    Indication = form.Indications |> selectIfOne form.Indication
                    Route = form.Routes |> selectIfOne form.Route
                    Shape = form.Shapes |> selectIfOne form.Shape
                    DoseType = form.DoseTypes |> selectIfOne form.DoseType
                    PatientCategory = form.PatientCategories |> selectIfOne form.PatientCategory
                }
            |> fun form ->
                { form with
                    Markdown =
                        match form.Generic, form.Indication, form.Route with
                        | Some _, Some _, Some _ ->
                            writeDebugMessage $"start checking {dsrs |> Array.length} rules"

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

                            writeDebugMessage $"finished checking {dsrs |> Array.length} rules"

                            dsrs
                            |> DoseRule.Print.toMarkdown
                            |> fun md ->
                                $"{md}\n\n### Doseer controle volgens de G-Standaard\n\n{s}"

                        | _ -> ""
                }
        $"""

Formulary:
Patients: {form.PatientCategories |> Array.length}
Indication: {form.Indications |> Array.length}
Generic: {form.Generics |> Array.length}
Route: {form.Routes |> Array.length}
Shapes: {form.Shapes |> Array.length}
DoseTypes: {form.DoseTypes |> Array.length}

"""
        |> writeInfoMessage

        Ok form


module Parenteralia =

    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib

    type Parenteralia = Shared.Types.Parenteralia


    let get (par : Parenteralia) : Result<Parenteralia, string> =
        writeInfoMessage $"getting parenteralia for {par.Generic}"

        let srs =
            Formulary.getSolutionRules
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

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Mappers


    let getIntake age wghtInGram (ords: Order []) =
        let wghtInKg =
            wghtInGram
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
            |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        let age =
            age
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)

        ords
        |> Array.map Order.mapFromSharedToOrder
        |> Totals.getTotals age wghtInKg
        |> mapToIntake


module OrderContext =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared
    open Shared.Types
    open Mappers


    let setDemoVersion ctx =
        { ctx with
            DemoVersion =
                Env.getItem "GENPRES_PROD"
                |> Option.map (fun v -> v <> "1")
                |> Option.defaultValue true
        }


    let updateIntake (ctx : OrderContext) =
        { ctx with
            Intake =
                let w = ctx.Patient |> Models.Patient.getWeight |> Option.map int
                let a =
                    ctx.Patient
                    |> Models.Patient.getAgeInDays
                    |> Option.map int

                ctx.Scenarios
                |> Array.map _.Order
                |> Order.getIntake a w
        }


    let evaluate cmd : Result<OrderContext,  string []> =
        let eval ctx =
            cmd
            |> function
                | Api.UpdateOrderContext _ -> ctx |> OrderContext.UpdateOrderContext
                | Api.SelectOrderScenario _ -> ctx |> OrderContext.SelectOrderScenario
                | Api.UpdateOrderScenario _ -> ctx |> OrderContext.UpdateOrderScenario
                | Api.ResetOrderScenario _ -> ctx |> OrderContext.ResetOrderScenario
                | Api.ReloadResources _ -> ctx |> OrderContext.ReloadResources
            |> OrderContext.printCtx "start eval"
            |> OrderContext.evaluate
            |> OrderContext.printCtx "finish eval"

        match cmd with
        | Api.UpdateOrderContext ctx
        | Api.SelectOrderScenario ctx
        | Api.UpdateOrderScenario ctx
        | Api.ResetOrderScenario ctx
        | Api.ReloadResources ctx
         ->
            let map = mapToShared ctx >> updateIntake >> setDemoVersion

            let pat =
                ctx.Patient
                |> mapFromSharedPatient
                |> Patient.calcPMAge

            try
                ctx
                |> mapFromShared pat
                |> eval
                |> function
                    | OrderContext.UpdateOrderContext newCtx -> newCtx |> map
                    | OrderContext.SelectOrderScenario newCtx -> newCtx |> map
                    | OrderContext.UpdateOrderScenario newCtx -> newCtx |> map
                    | OrderContext.ResetOrderScenario newCtx -> newCtx |> map
                    | OrderContext.ReloadResources newCtx -> newCtx |> map
            with
            | e ->
                writeErrorMessage $"errored:\n{e}"
                ctx
            |> Ok


module TreatmentPlan =

    open Shared
    open Shared.Types

    module OrderLogger = Informedica.GenOrder.Lib.OrderLogger


    let updateTreatmentPlan (tp : TreatmentPlan) =
        match tp.Selected with
        | Some os ->
            os
            |> Models.OrderContext.fromOrderScenario tp.Patient
            |> Api.UpdateOrderScenario
            |> OrderContext.evaluate
            |> Result.map (fun pr ->
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


    let calculateTotals (tp : TreatmentPlan) =
        { tp with
            Totals =
                let w = tp.Patient |> Models.Patient.getWeight |> Option.map int
                let a = tp.Patient |> Models.Patient.getAgeInDays |> Option.map int

                let scs =
                    if tp.Filtered |> Array.isEmpty then tp.Scenarios
                    else
                        tp.Scenarios
                        |> Array.filter (fun sc -> tp.Filtered |> Array.exists ((=) sc))

                scs
                |> Array.map _.Order
                |> Order.getIntake a w
        }


module Command =

    open Shared.Api
    open Informedica.Utils.Lib

    module OrderLogger = Informedica.GenOrder.Lib.OrderLogger


    let processCmd cmd =
        if Env.getItem "GENPRES_LOG" |> Option.map (fun s -> s = "1") |> Option.defaultValue false then
            let path = $"{__SOURCE_DIRECTORY__}/log.txt"
            OrderLogger.logger.Start (Some path) OrderLogger.Level.Informative

        match cmd with
        | OrderContextCmd ctxCmd ->
            ctxCmd
            |> OrderContext.evaluate
            |> Result.map (OrderContextUpdated >> OrderContextResp)

        | TreatmentPlanCmd (UpdateTreatmentPlan tp) ->
                tp
                |> TreatmentPlan.updateTreatmentPlan
                |> TreatmentPlan.calculateTotals
                |> TreatmentPlanUpdated
                |> TreatmentPlanResp
                |> Ok

        | TreatmentPlanCmd (FilterTreatmentPlan tp) ->
            tp
            |> TreatmentPlan.calculateTotals
            |> TreatmentPlanFiltered
            |> TreatmentPlanResp
            |> Ok

        | FormularyCmd form ->
            form
            |> Formulary.get
            |> Result.map FormularyResp

        | ParenteraliaCmd par ->
            par
            |> Parenteralia.get
            |> Result.mapError Array.singleton
            |> Result.map ParentaraliaResp


[<AutoOpen>]
module ApiImpl =

    open Shared.Api


    /// An implementation of the Shared IServerApi protocol.
    let serverApi: IServerApi =
        {
            processMessage =
                fun msg ->
                    async {
                        return msg |> Command.processCmd
                    }

            testApi =
                fun () ->
                    async {
                        return "Hello world!"
                    }
        }