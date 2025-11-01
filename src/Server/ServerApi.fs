namespace ServerApi


module Mappers =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared.Types
    open Shared


    module Order =


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


        let mapToPrescription (dto : Order.Schedule.Dto.Dto) : Schedule =
            Models.Order.Prescription.create
                dto.IsOnce
                dto.IsOnceTimed
                dto.IsContinuous
                dto.IsDiscontinuous
                dto.IsTimed
                (dto.Frequency |> mapToOrderVariable)
                (dto.Time |> mapToOrderVariable)


        let mapFromPrescription (prescription : Schedule) : Order.Schedule.Dto.Dto =
            let dto = Order.Schedule.Dto.Dto ()
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
                (dto.Schedule |> mapToPrescription)
                dto.Route
                (dto.Duration |> mapToOrderVariable)
                dto.Start
                dto.Stop


        let mapFromSharedToOrder (order : Types.Order) : Order.Dto.Dto =
            let dto = Order.Dto.Dto(order.Id, order.Orderable.Name)

            dto.Adjust <- order.Adjust |> mapFromOrderVariable
            dto.Orderable <- order.Orderable |> mapFromOrderable order.Id order.Orderable.Name
            dto.Schedule <- order.Schedule |> mapFromPrescription
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
                    | IntermittentHemodialysis -> Informedica.GenForm.Lib.Types.RenalFunction.IntermittentHemodialysis
                    | ContinuousHemodialysis -> Informedica.GenForm.Lib.Types.RenalFunction.ContinuousHemodialysis
                    | PeritonealDialysis -> Informedica.GenForm.Lib.Types.RenalFunction.PeritonealDialysis
                )
        }
        |> Patient.calcPMAge


    let mapFromShared logger provider pat (ctx : OrderContext)  : Informedica.GenOrder.Lib.Types.OrderContext =

        let mappedCtx = OrderContext.create logger provider pat

        let setFilter eqs itm items =
            match items |> Array.tryFind (fun x -> itm |> Option.map (eqs x) |> Option.defaultValue false) with
            | Some x -> itm, [| x |]
            | None   -> None, items

        { mappedCtx with
            Scenarios =
                ctx.Scenarios
                |> Array.collect (fun sc ->
                        match sc.Order |> Order.mapFromSharedToOrder |> Order.Dto.fromDto with
                        | Ok ord -> [| (sc, ord) |]
                        | Error _ -> [||]

                )
                |> Array.mapi (fun i (sc, ord) ->
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
                let ind, inds = mappedCtx.Filter.Indications |> setFilter String.equalsCapInsens ctx.Filter.Indication
                let gen, gens = mappedCtx.Filter.Generics |> setFilter String.equalsCapInsens ctx.Filter.Medication
                let rte, rtes = mappedCtx.Filter.Routes |> setFilter String.equalsCapInsens ctx.Filter.Route
                let shp, shps = mappedCtx.Filter.Shapes |> setFilter String.equalsCapInsens ctx.Filter.Shape
                let dtp, dtps = mappedCtx.Filter.DoseTypes |> setFilter DoseType.eqs (ctx.Filter.DoseType |> Option.map mapFromSharedDoseTypeToOrderDoseType)

                { mappedCtx.Filter with
                    Indication = ind
                    Indications = inds
                    Generic = gen
                    Generics = gens
                    Route = rte
                    Routes = rtes
                    Shape = shp
                    Shapes = shps
                    DoseType = dtp
                    DoseTypes =
                        if dtps |> Array.length = 1 then dtps
                        else ctx.Filter.DoseTypes |> Array.map mapFromSharedDoseTypeToOrderDoseType
                    Diluents = ctx.Filter.Diluents
                    Components = ctx.Filter.Components
                    Diluent = ctx.Filter.Diluent
                    SelectedComponents = ctx.Filter.SelectedComponents
                }
        }


    /// Configuration for text item delimiters
    /// Each delimiter maps to a constructor function and its delimiter character
    type private DelimiterConfig =
        {
            Delimiter: string
            Constructor: string -> TextItem
            IsActive: TextItem -> bool
        }


    let parseTextItem (s: string) =
        if s |> String.isNullOrWhiteSpace then
            [||]
        else
            // Define delimiter configurations - easy to extend with new cases
            let delimiters =
                [
                    { Delimiter = "#"; Constructor = Bold; IsActive = function Bold _ -> true | _ -> false }
                    { Delimiter = "|"; Constructor = Italic; IsActive = function Italic _ -> true | _ -> false }
                ]

            /// Get the text content from a TextItem
            let getText = function
                | Normal s | Bold s | Italic s -> s

            /// Check if a delimiter is active for the current state
            let tryFindActiveDelimiter char currentItem =
                delimiters
                |> List.tryFind (fun d -> d.Delimiter = char && d.IsActive currentItem)

            /// Check if a character is any delimiter
            let tryFindDelimiter char =
                delimiters
                |> List.tryFind (fun d -> d.Delimiter = char)

            /// Process each character through the state machine
            let processChar (currentItem, completedItems) char =
                match tryFindActiveDelimiter char currentItem with
                | Some _ ->
                    // Toggle off: return to Normal state
                    Normal "", currentItem :: completedItems
                | None ->
                    match tryFindDelimiter char with
                    | Some config ->
                        // Toggle on: switch to new state
                        config.Constructor "", currentItem :: completedItems
                    | None ->
                        // Regular character: append to current item
                        let currentText = getText currentItem
                        let newItem =
                            match currentItem with
                            | Normal _ -> Normal (currentText + char)
                            | Bold _ -> Bold (currentText + char)
                            | Italic _ -> Italic (currentText + char)
                        newItem, completedItems

            s
            |> Seq.map string
            |> Seq.fold processChar (Normal "", [])
            |> fun (lastItem, items) -> lastItem :: items
            |> List.rev
            |> List.filter (fun item -> item |> getText |> String.isNullOrWhiteSpace |> not)
            |> List.toArray


    let mapTextBlock (tb: Informedica.GenOrder.Lib.Types.TextBlock) =
        match tb with
        | Informedica.GenOrder.Lib.Types.Valid s
        | Informedica.GenOrder.Lib.Types.Caution s
        | Informedica.GenOrder.Lib.Types.Warning s
        | Informedica.GenOrder.Lib.Types.Alert s ->
            if s |> String.isNullOrWhiteSpace then [||] |> Valid
            else
                let ti = s |> parseTextItem
                match tb with
                | Informedica.GenOrder.Lib.Types.Valid _ -> ti |> Valid
                | Informedica.GenOrder.Lib.Types.Caution _ -> ti |> Caution
                | Informedica.GenOrder.Lib.Types.Warning _ -> ti |> Warning
                | Informedica.GenOrder.Lib.Types.Alert _ -> ti |> Alert



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
                            (sc.Prescription |> Array.map (Array.map mapTextBlock))
                            (sc.Preparation |> Array.map (Array.map mapTextBlock))
                            (sc.Administration |> Array.map (Array.map mapTextBlock))
                            (sc.Order |> (Order.Dto.toDto >> Order.mapFromOrderToShared sc.Items))
                            sc.UseAdjust
                            sc.UseRenalRule
                            sc.RenalRule
                            sc.ProductsIds
                    )
            }


    let mapToIntake (intake : Informedica.GenOrder.Lib.Types.Totals) : Totals =
        let toTextItem =
            Option.map parseTextItem
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
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Shared


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


    let checkDoseRules provider pat (dsrs : DoseRule []) =
        let routeMapping = Api.getRouteMapping provider

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


    let get provider (form : Formulary) =
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
        |> writeDebugMessage

        let dsrs = Formulary.getDoseRules provider filter

        writeDebugMessage $"Found: {dsrs |> Array.length} formulary dose rules"
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
                                |> checkDoseRules provider filter.Patient
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
        |> writeDebugMessage

        Ok form


module Parenteralia =

    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib

    type Parenteralia = Shared.Types.Parenteralia


    let get provider (par : Parenteralia) : Result<Parenteralia, string> =
        writeInfoMessage $"getting parenteralia for {par.Generic}"

        let srs =
            Formulary.getSolutionRules provider
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


    let evaluate logger provider cmd : Result<OrderContext,  string []> =
        let eval ctx =
            cmd
            |> function
                | Api.UpdateOrderContext _ -> ctx |> OrderContext.UpdateOrderContext
                | Api.SelectOrderScenario _ -> ctx |> OrderContext.SelectOrderScenario
                | Api.UpdateOrderScenario _ -> ctx |> OrderContext.UpdateOrderScenario
                | Api.ResetOrderScenario _ -> ctx |> OrderContext.ResetOrderScenario
                | Api.ReloadResources _ -> ctx |> OrderContext.ReloadResources
            |> OrderContext.logOrderContext logger "start eval"
            |> OrderContext.evaluate logger provider
            |> ValidatedResult.get
            |> OrderContext.logOrderContext logger "finish eval"

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
                |> mapFromShared logger provider pat
                |> eval
                |> function
                    | OrderContext.UpdateOrderContext newCtx -> newCtx |> map
                    | OrderContext.SelectOrderScenario newCtx -> newCtx |> map
                    | OrderContext.UpdateOrderScenario newCtx -> newCtx |> map
                    | OrderContext.ResetOrderScenario newCtx -> newCtx |> map
                    | OrderContext.ReloadResources newCtx -> newCtx |> map
                |> Ok
            with
            | e ->
                writeErrorMessage $"errored:\n{e}"
                raise e


module TreatmentPlan =

    open Shared
    open Shared.Types

    module OrderLogger = Informedica.GenOrder.Lib.OrderLogging


    let updateTreatmentPlan logger provider (tp : TreatmentPlan) =
        match tp.Selected with
        | Some os ->
            os
            |> Models.OrderContext.fromOrderScenario tp.Patient
            |> Api.UpdateOrderScenario
            |> OrderContext.evaluate logger provider
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

    let processCmd provider cmd =
        match cmd with
        | OrderContextCmd ctxCmd ->
            async {
                let logger = Logging.getLogger Logging.OrderLogger
                do! logger |> Logging.setComponentName (Some "OrderContext")
                return
                    ctxCmd
                    |> OrderContext.evaluate logger.Logger provider
                    |> Result.map (OrderContextUpdated >> OrderContextResp)
            }

        | TreatmentPlanCmd (UpdateTreatmentPlan tp) ->
            async {
                let logger = Logging.getLogger Logging.TherapyTreatmentPlanLogger
                do! logger |> Logging.setComponentName (Some "TreatmentPlan")
                return
                    tp
                    |> TreatmentPlan.updateTreatmentPlan logger.Logger provider
                    |> TreatmentPlan.calculateTotals
                    |> TreatmentPlanUpdated
                    |> TreatmentPlanResp
                    |> Ok
            }

        | TreatmentPlanCmd (FilterTreatmentPlan tp) ->
            async {
                let logger = Logging.getLogger Logging.TherapyTreatmentPlanLogger
                do! logger |> Logging.setComponentName (Some "TreatmentPlan")
                return
                    tp
                    |> TreatmentPlan.calculateTotals
                    |> TreatmentPlanFiltered
                    |> TreatmentPlanResp
                    |> Ok
            }

        | FormularyCmd form ->
            async {
                let logger = Logging.getLogger Logging.FormularyLogger
                do! logger |> Logging.setComponentName (Some "Formulary")
                return
                    form
                    |> Formulary.get provider
                    |> Result.map FormularyResp
            }

        | ParenteraliaCmd par ->
            async {
                let logger = Logging.getLogger Logging.ParenteraliaLogger
                do! logger |> Logging.setComponentName (Some "Parenteralia")
                return
                    par
                    |> Parenteralia.get provider
                    |> Result.mapError Array.singleton
                    |> Result.map ParentaraliaResp
            }


[<AutoOpen>]
module ApiImpl =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Api


    /// An implementation of the Shared IServerApi protocol.
    let createServerApi provider : IServerApi =
        {
            processCommand =
                fun cmd ->
                    async {
                        try
                            writeInfoMessage $"Processing command: {cmd |> Command.toString}"
                            let! result = Command.processCmd provider cmd
                            writeInfoMessage $"Finished processing command: {cmd |> Command.toString}"
                            return result
                        with
                        | ex ->
                            writeErrorMessage $"Error processing command: {cmd |> Command.toString}\n{ex.Message}"
                            return Error [| ex.Message |]
                    }

            testApi =
                fun () ->
                    async {
                        return "Hello world!"
                    }
        }