namespace ServerApi


/// The order mapper, and what the services still map by hand: the patient, the order context
/// and the totals, each now with a mapper of its own next to this file. Those parts go once
/// the services take the domain-typed ports; the order mapping, the dose type both ways and
/// the text markup parser stay.
module Mappers =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    open Shared.Types
    open Shared


    module Order =


        let mapToValueUnit (dto: ValueUnit.Dto.Dto) : Types.ValueUnit =
            let v = dto.Value |> Array.map (fun br -> $"{br}", br |> BigRational.toDecimal)
            Models.Order.ValueUnit.create v dto.Unit dto.Group dto.Short dto.Language dto.Json


        let mapFromValueUnit (vu: Types.ValueUnit) : ValueUnit.Dto.Dto =
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
            Models.Order.Variable.create
                dto.Name
                dto.IsNonZeroPositive
                (dto.MinOpt |> Option.map mapToValueUnit)
                dto.MinIncl
                (dto.IncrOpt |> Option.map mapToValueUnit)
                (dto.MaxOpt |> Option.map mapToValueUnit)
                dto.MaxIncl
                (dto.ValsOpt |> Option.map mapToValueUnit)


        let mapFromVariable (var: Variable) : Informedica.GenSolver.Lib.Variable.Dto.Dto =
            let dto = Informedica.GenSolver.Lib.Variable.Dto.dto ()

            dto.Name <- var.Name

            if var.IsNonZeroPositive && var.Incr.IsNone then
                dto.IsNonZeroPositive <- true

            else
                dto.IsNonZeroPositive <- var.IsNonZeroPositive
                dto.MinOpt <- var.Min |> Option.map mapFromValueUnit
                dto.MinIncl <- var.MinIncl
                dto.IncrOpt <- var.Incr |> Option.map mapFromValueUnit
                dto.MaxOpt <- var.Max |> Option.map mapFromValueUnit
                dto.MaxIncl <- var.MaxIncl
                dto.ValsOpt <- var.Vals |> Option.map mapFromValueUnit

            dto


        /// The per-click increment used by the outer (first/last) navigation buttons: the
        /// calculated increment (OrderVariable.step uses CalculatedConstraints.Incr for the
        /// useCalc path). When `coarse` is given and the calculated increment equals it, the
        /// count is multiplied by 10 — mirroring the server's role-specific special cases.
        /// Generic order variables pass None so NO multiple is applied (the ×10 must only
        /// happen for the rate / quantity that the server actually multiplies). Optional —
        /// many order variables have no calculated increment.
        let mapLargeIncr
            (coarse: Informedica.GenUnits.Lib.ValueUnit option)
            (dto: OrderVariable.Dto.Dto)
            : Types.ValueUnit option
            =
            dto.Calculated.IncrOpt
            |> Option.bind ValueUnit.Dto.fromDto
            |> Option.map (fun calcVu ->
                // Compare by dimension + base value rather than structural (=) equality:
                // calcVu is round-tripped through the DTO, so its unit representation may
                // differ from the locally-constructed constant even when it is the same
                // physical increment.
                let sameIncr (target: Informedica.GenUnits.Lib.ValueUnit) =
                    ValueUnit.eqsGroup calcVu target
                    && ValueUnit.getBaseValue calcVu = ValueUnit.getBaseValue target

                let mult =
                    match coarse with
                    | Some u when sameIncr u -> BigRational.fromInt 10
                    | _ -> BigRational.fromInt 1

                (mult |> ValueUnit.singleWithUnit Units.Count.times) * calcVu
                |> ValueUnit.Dto.toDtoDutchShort
                |> mapToValueUnit
            )


        let mapToOrderVariableWith
            (coarse: Informedica.GenUnits.Lib.ValueUnit option)
            (dto: OrderVariable.Dto.Dto)
            : Types.OrderVariable
            =
            let level =
                match dto.Level with
                | OrderVariable.Dto.IsNormal -> IsNormal
                | OrderVariable.Dto.IsCaution -> IsCaution
                | OrderVariable.Dto.IsWarning -> IsWarning
                | OrderVariable.Dto.IsAlert -> IsAlert

            Models.Order.OrderVariable.create
                dto.Name
                (dto.Constraints |> mapToVariable)
                (dto.Calculated |> mapToVariable)
                (dto.Variable |> mapToVariable)
                (dto |> mapLargeIncr coarse)
                level


        // Generic mapping: no outer ×10 multiple (used for every order variable that is
        // not navigated as a rate or quantity).
        let mapToOrderVariable = mapToOrderVariableWith None

        let tenthOf u =
            u |> ValueUnit.singleWithValue (BigRational.fromInt 1 / BigRational.fromInt 10)

        // The server's special "×10" outer-step cases key on these increments: a quantity
        // increment of 1/10 mL (OrderVariable.stepQuantity) and a rate increment of
        // 1/10 mL/hour (Dose.stepRate).
        let mlTenth = Units.Volume.milliLiter |> tenthOf

        let mlPerHourTenth = Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour |> tenthOf

        // The rate / quantity an order navigates DO get the server's ×10 outer step when
        // their calculated increment is the 1/10 mL[/hour] coarse increment.
        let mapToRateOrderVariable = mapToOrderVariableWith (Some mlPerHourTenth)
        let mapToQuantityOrderVariable = mapToOrderVariableWith (Some mlTenth)


        let mapFromOrderVariable (ov: Types.OrderVariable) : OrderVariable.Dto.Dto =
            let dto = OrderVariable.Dto.dto ()
            dto.Name <- ov.Name
            dto.Variable <- ov.Variable |> mapFromVariable
            dto.Constraints <- ov.DefinedConstraints |> mapFromVariable
            dto.Calculated <- ov.CalculatedConstraints |> mapFromVariable

            dto.Level <-
                match ov.Level with
                | IsNormal -> OrderVariable.Dto.IsNormal
                | IsCaution -> OrderVariable.Dto.IsCaution
                | IsWarning -> OrderVariable.Dto.IsWarning
                | IsAlert -> OrderVariable.Dto.IsAlert

            dto


        let mapToDose (dto: Order.Orderable.Dose.Dto.Dto) : Dose =
            Models.Order.Dose.create
                (dto.Quantity |> mapToQuantityOrderVariable)
                (dto.PerTime |> mapToOrderVariable)
                (dto.Rate |> mapToRateOrderVariable)
                (dto.Total |> mapToOrderVariable)
                (dto.QuantityAdjust |> mapToOrderVariable)
                (dto.PerTimeAdjust |> mapToOrderVariable)
                (dto.RateAdjust |> mapToOrderVariable)
                (dto.TotalAdjust |> mapToOrderVariable)


        let mapFromDose (dose: Dose) : Order.Orderable.Dose.Dto.Dto =
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


        let mapToItem sns (dto: Order.Orderable.Item.Dto.Dto) : Item =
            Models.Order.Item.create
                dto.Name
                (dto.ComponentQuantity |> mapToOrderVariable)
                (dto.OrderableQuantity |> mapToOrderVariable)
                (dto.ComponentConcentration |> mapToOrderVariable)
                (dto.OrderableConcentration |> mapToOrderVariable)
                (dto.Dose |> mapToDose)
                // filter out sns (substance names) to indicate an additional ingredient
                (sns |> Array.exists (String.equalsCapInsens dto.Name) |> not)


        let mapFromItem (item: Item) : Order.Orderable.Item.Dto.Dto =
            let dto = Order.Orderable.Item.Dto.Dto()
            dto.Name <- item.Name
            dto.ComponentQuantity <- item.ComponentQuantity |> mapFromOrderVariable
            dto.OrderableQuantity <- item.OrderableQuantity |> mapFromOrderVariable
            dto.ComponentConcentration <- item.ComponentConcentration |> mapFromOrderVariable
            dto.OrderableConcentration <- item.OrderableConcentration |> mapFromOrderVariable
            dto.Dose <- item.Dose |> mapFromDose

            dto


        let mapToComponent sns (dto: Order.Orderable.Component.Dto.Dto) : Component =
            Models.Order.Component.create
                dto.Id
                dto.Name
                dto.Form
                (dto.ComponentQuantity |> mapToOrderVariable)
                // the component's orderable quantity is navigated as a quantity (×10 outer)
                (dto.OrderableQuantity |> mapToQuantityOrderVariable)
                (dto.OrderableCount |> mapToOrderVariable)
                (dto.OrderQuantity |> mapToOrderVariable)
                (dto.OrderCount |> mapToOrderVariable)
                (dto.OrderableConcentration |> mapToOrderVariable)
                (dto.Dose |> mapToDose)
                (dto.Items |> List.toArray |> Array.map (mapToItem sns))


        let mapFromComponent (comp: Component) : Order.Orderable.Component.Dto.Dto =
            let dto = Order.Orderable.Component.Dto.Dto()
            dto.Id <- comp.Id
            dto.Name <- comp.Name
            dto.Form <- comp.Form
            dto.ComponentQuantity <- comp.ComponentQuantity |> mapFromOrderVariable
            dto.OrderableQuantity <- comp.OrderableQuantity |> mapFromOrderVariable
            dto.OrderableCount <- comp.OrderableCount |> mapFromOrderVariable
            dto.OrderQuantity <- comp.OrderQuantity |> mapFromOrderVariable
            dto.OrderCount <- comp.OrderCount |> mapFromOrderVariable
            dto.OrderableConcentration <- comp.OrderableConcentration |> mapFromOrderVariable
            dto.Dose <- comp.Dose |> mapFromDose
            dto.Items <- comp.Items |> Array.toList |> List.map mapFromItem

            dto


        let mapToOrderable sns (dto: Order.Orderable.Dto.Dto) : Orderable =
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
        let mapFromOrderable id n (orderable: Orderable) : Order.Orderable.Dto.Dto =
            let dto = Order.Orderable.Dto.dto id n
            dto.OrderableQuantity <- orderable.OrderableQuantity |> mapFromOrderVariable
            dto.OrderQuantity <- orderable.OrderQuantity |> mapFromOrderVariable
            dto.OrderCount <- orderable.OrderCount |> mapFromOrderVariable
            dto.DoseCount <- orderable.DoseCount |> mapFromOrderVariable
            dto.Dose <- orderable.Dose |> mapFromDose
            dto.Components <- orderable.Components |> Array.toList |> List.map mapFromComponent

            dto


        let mapToPrescription (dto: Order.Schedule.Dto.Dto) : Schedule =
            Models.Order.Prescription.create
                dto.IsOnce
                dto.IsOnceTimed
                dto.IsContinuous
                dto.IsDiscontinuous
                dto.IsTimed
                (dto.Frequency |> mapToOrderVariable)
                (dto.Time |> mapToOrderVariable)


        let mapFromPrescription (prescription: Schedule) : Order.Schedule.Dto.Dto =
            let dto = Order.Schedule.Dto.Dto()
            dto.IsOnce <- prescription.IsOnce
            dto.IsOnceTimed <- prescription.IsOnceTimed
            dto.IsDiscontinuous <- prescription.IsDiscontinuous
            dto.IsContinuous <- prescription.IsContinuous
            dto.IsTimed <- prescription.IsTimed
            dto.Frequency <- prescription.Frequency |> mapFromOrderVariable
            dto.Time <- prescription.Time |> mapFromOrderVariable

            dto


        let mapFromOrderToShared sns (dto: Order.Dto.Dto) : Types.Order =
            Models.Order.create
                dto.Id
                (dto.Adjust |> mapToOrderVariable)
                (dto.Orderable |> (mapToOrderable sns))
                (dto.Schedule |> mapToPrescription)
                dto.Route
                (dto.Duration |> mapToOrderVariable)
                dto.Start
                dto.Stop


        let mapFromSharedToOrder (order: Types.Order) : Order.Dto.Dto =
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


    let mapFromSharedPatient (pat: Patient) =
        { Patient.patient with
            Department = pat.Department |> Option.defaultValue "ICK" |> Some
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
            Access =
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
                    {
                        Delimiter = "#"
                        Constructor = Bold
                        IsActive =
                            function
                            | Bold _ -> true
                            | _ -> false
                    }
                    {
                        Delimiter = "|"
                        Constructor = Italic
                        IsActive =
                            function
                            | Italic _ -> true
                            | _ -> false
                    }
                ]

            /// Get the text content from a TextItem
            let getText =
                function
                | Normal s
                | Bold s
                | Italic s -> s

            /// Check if a delimiter is active for the current state
            let tryFindActiveDelimiter char currentItem =
                delimiters
                |> List.tryFind (fun d -> d.Delimiter = char && d.IsActive currentItem)

            /// Check if a character is any delimiter
            let tryFindDelimiter char = delimiters |> List.tryFind (fun d -> d.Delimiter = char)

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
                            | Normal _ -> Normal(currentText + char)
                            | Bold _ -> Bold(currentText + char)
                            | Italic _ -> Italic(currentText + char)

                        newItem, completedItems

            s
            |> Seq.map string
            |> Seq.fold processChar (Normal "", [])
            |> fun (lastItem, items) -> lastItem :: items
            |> List.rev
            |> List.filter (fun item -> item |> getText |> String.notEmpty)
            |> List.toArray
