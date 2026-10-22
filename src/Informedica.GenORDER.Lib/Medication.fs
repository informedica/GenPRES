namespace Informedica.GenOrder.Lib


module Medication =

    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    // Re-open the GenOrder types so the local domain types (notably
    // ProductComponent) take precedence over the GenForm types brought in by
    // `open Informedica.GenForm.Lib`. GenForm renamed its product `Product`
    // type to `ProductComponent`, which would otherwise shadow ours.
    open Informedica.GenOrder.Lib.Types
    // ...then open GenUnits so its `Unit`/`Time` cases win again over the
    // GenOrder `Time` order-variable type (GenUnits defines no ProductComponent).
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    /// Unit validation helpers for deterministic field parsing
    module UnitValidation =

        /// Check if a unit is an adjust unit (Weight - kg, or BSA - m2)
        let rec private isAdjustUnitType (u: Unit) =
            match u with
            | Weight _ -> true
            | BSA _ -> true
            | CombiUnit(ul, _, ur) -> isAdjustUnitType ul || isAdjustUnitType ur
            | _ -> false


        /// Check if a unit is a time unit
        let rec private isTimeUnitType (u: Unit) =
            match u with
            | Time _ -> true
            | CombiUnit(ul, _, ur) -> isTimeUnitType ul || isTimeUnitType ur
            | _ -> false


        /// Check if a unit has an adjust component in denominator (kg or m2)
        /// by checking if the right side of a Per operation contains Weight or BSA
        let rec hasAdjustUnit (u: Unit) =
            match u with
            | CombiUnit(ul, OpPer, ur) ->
                // Check if denominator (right side) contains adjust unit
                isAdjustUnitType ur || hasAdjustUnit ul
            | CombiUnit(ul, _, ur) -> hasAdjustUnit ul || hasAdjustUnit ur
            | _ -> false


        /// Check if a unit has a time component in denominator
        /// by checking if the right side of a Per operation contains Time
        let rec hasTimeUnit (u: Unit) =
            match u with
            | CombiUnit(ul, OpPer, ur) ->
                // Check if denominator (right side) contains time unit
                isTimeUnitType ur || hasTimeUnit ul
            | CombiUnit(ul, _, ur) -> hasTimeUnit ul || hasTimeUnit ur
            | _ -> false


        /// Validate that unit matches Quantity field pattern: no adjust, no time
        let validateQuantityUnit (u: Unit) =
            if hasAdjustUnit u then
                Error "Quantity cannot have adjust unit"
            elif hasTimeUnit u then
                Error "Quantity cannot have time unit"
            else
                Ok()


        /// Validate that unit matches QuantityAdjust field pattern: has adjust, no time
        let validateQuantityAdjustUnit (u: Unit) =
            if not (hasAdjustUnit u) then
                Error "QuantityAdjust must have adjust unit (kg/m2)"
            elif hasTimeUnit u then
                Error "QuantityAdjust cannot have time unit"
            else
                Ok()


        /// Validate that unit matches PerTime field pattern: no adjust, has time
        let validatePerTimeUnit (u: Unit) =
            if hasAdjustUnit u then
                Error "PerTime cannot have adjust unit"
            elif not (hasTimeUnit u) then
                Error "PerTime must have time unit"
            else
                Ok()


        /// Validate that unit matches PerTimeAdjust field pattern: has adjust and time
        let validatePerTimeAdjustUnit (u: Unit) =
            if not (hasAdjustUnit u) then
                Error "PerTimeAdjust must have adjust unit (kg/m2)"
            elif not (hasTimeUnit u) then
                Error "PerTimeAdjust must have time unit"
            else
                Ok()


        /// Validate that unit matches Rate field pattern: no adjust, has time
        let validateRateUnit (u: Unit) =
            if hasAdjustUnit u then
                Error "Rate cannot have adjust unit"
            elif not (hasTimeUnit u) then
                Error "Rate must have time unit"
            else
                Ok()


        /// Validate that unit matches RateAdjust field pattern: has adjust and time
        let validateRateAdjustUnit (u: Unit) =
            if not (hasAdjustUnit u) then
                Error "RateAdjust must have adjust unit (kg/m2)"
            elif not (hasTimeUnit u) then
                Error "RateAdjust must have time unit"
            else
                Ok()


    module Limit = Limit


    let private tryHead m = Array.map m >> Array.tryHead >> Option.defaultValue ""


    /// <summary>
    /// Set constraints on a Variable dto based on norm values and MinMax record.
    /// A min or max value is set only if the MinMax record is not None or
    /// the sequence of big rationals has a single value. In that case the
    /// min or max value is set to the big rational minus or plus 10%.
    /// </summary>
    /// <param name="calcNormDose">Whether this is a norm dose so a 20% range has to be calculated</param>
    /// <param name="minMax">The MinMax record containing constraints.</param>
    /// <param name="dto">The Variable dto to apply constraints to.</param>
    let setMinMaxConstraints calcNormDose (minMax: MinMax) (dto: Informedica.GenSolver.Lib.Variable.Dto.Dto) =

        let vuToDto = Option.bind (ValueUnit.Dto.toDto false ValueUnit.Dto.english)

        let limToVu = Option.map Limit.getValueUnit

        let times0_90 = 90N / 100N |> ValueUnit.singleWithUnit Units.Count.times
        let times1_10 = 11N / 10N |> ValueUnit.singleWithUnit Units.Count.times

        let isNormDose =
            if not calcNormDose then
                false
            else
                match minMax.Min, minMax.Max with
                | Some minLimit, Some maxLimit -> minLimit |> Limit.eq maxLimit
                | _ -> false

        let min =
            minMax.Min
            |> limToVu
            |> Option.map (fun vu -> if isNormDose then vu * times0_90 else vu)
            |> vuToDto

        let max =
            minMax.Max
            |> limToVu
            |> Option.map (fun vu -> if isNormDose then vu * times1_10 else vu)
            |> vuToDto

        match min with
        | None -> ()
        | Some _ ->
            dto.MinIncl <- min.IsSome
            dto.MinOpt <- min

        match max with
        | None -> ()
        | Some _ ->
            dto.MaxIncl <- max.IsSome
            dto.MaxOpt <- max


    /// <summary>
    /// Create a value unit dto from a string and a sequence of big rationals.
    /// </summary>
    /// <param name="u">The unit as a string.</param>
    /// <param name="brs">The big rationals as a sequence.</param>
    /// <remarks>
    /// If the unit is null or an empty string, the function returns None.
    /// </remarks>
    let createValueUnitDto u brs =
        if u = NoUnit then
            None
        else
            brs |> ValueUnit.withUnit u |> ValueUnit.Dto.toDto false ValueUnit.Dto.english

    /// <summary>
    /// Create a single value unit dto from a string and a big rational.
    /// </summary>
    /// <param name="u">The unit as a string.</param>
    /// <param name="br">The big rational.</param>
    /// <remarks>
    /// If the unit is null or an empty string, the function returns None.
    /// </remarks>
    let createSingleValueUnitDto u br = createValueUnitDto u [| br |]


    module SubstanceItem =


        /// An empty Substance Item record.
        let item =
            {
                Name = ""
                Quantities = None
                Concentrations = None
                Dose = None
                Solution = None
            }


        let create nme qts conc dos sol =
            {
                Name = nme
                Quantities = qts
                Concentrations = conc
                Dose = dos
                Solution = sol
            }


    module ProductComponent =


        /// An empty Product Component record.
        let cmp =
            {
                Name = ""
                Form = ""
                Quantities = None
                Divisible = None
                Solution = None
                Dose = None
                Substances = []
                Products = []
            }

        let create nme frm qts div dos sol sbs : ProductComponent =
            {
                Name = nme
                Form = frm
                Quantities = qts
                Divisible = div
                Dose = dos
                Solution = sol
                Substances = sbs
                Products = []
            }


        /// A product can be made from the substances when every strength names one of them and
        /// that substance has concentrations in the strength's unit group.
        let madeOf (sis: SubstanceItem list) (p: ComponentProduct) =
            p.Strengths
            |> List.forall (fun ps ->
                sis
                |> List.exists (fun si ->
                    si.Name |> String.equalsCapInsens ps.Substance
                    && si.Concentrations |> Option.exists (ValueUnit.eqsGroup ps.Strength)
                )
            )


    /// An empty medication record.
    let template =
        {
            Id = ""
            Name = ""
            Components = []
            Quantity = MinMax.empty
            Quantities = None
            Route = ""
            OrderType = AnyOrder
            //AdjustUnit = None
            Frequencies = None
            Time = MinMax.empty
            Dose = None
            Div = None
            DoseCount = MinMax.empty
            Adjust = None
        }


    /// The medication as a serializable shape: the shape of its text and its schema. Absent is
    /// null or an empty array; a number is its text; a limit gets its target from where it sits.
    /// The Dto classes allow null, unlike the order's, so that the serializer leaves an absent
    /// field out of the text.
    module Dto =

        open FsToolkit.ErrorHandling


        /// A value with its unit: the numbers as their exact text.
        [<AllowNullLiteral>]
        type ValueDto() =
            member val Value: string[] = [||] with get, set
            member val Unit: string = null with get, set


        /// A minimum or a maximum: a value with its unit, inclusive or exclusive.
        [<AllowNullLiteral>]
        type LimitDto() =
            member val Value: string[] = [||] with get, set
            member val Unit: string = null with get, set
            member val Inclusive = false with get, set


        /// A range: an optional minimum and maximum.
        [<AllowNullLiteral>]
        type MinMaxDto() =
            member val Min: LimitDto = null with get, set
            member val Max: LimitDto = null with get, set


        /// A dose limit; its target is where it sits.
        [<AllowNullLiteral>]
        type DoseLimitDto() =
            member val DoseUnit: string = null with get, set
            member val AdjustUnit: string = null with get, set
            member val Quantity: MinMaxDto = null with get, set
            member val QuantityAdjust: MinMaxDto = null with get, set
            member val PerTime: MinMaxDto = null with get, set
            member val PerTimeAdjust: MinMaxDto = null with get, set
            member val Rate: MinMaxDto = null with get, set
            member val RateAdjust: MinMaxDto = null with get, set


        /// A solution limit; its target is where it sits, its products are the solution rule's.
        [<AllowNullLiteral>]
        type SolutionLimitDto() =
            member val Quantity: MinMaxDto = null with get, set
            member val QuantityAdj: MinMaxDto = null with get, set
            member val Quantities: ValueDto = null with get, set
            member val Concentration: MinMaxDto = null with get, set


        /// The strength of one substance of a product.
        [<AllowNullLiteral>]
        type StrengthDto() =
            member val Substance: string = null with get, set
            member val Strength: ValueDto = null with get, set


        /// A product a component can be made of.
        [<AllowNullLiteral>]
        type ProductDto() =
            member val Id: string = null with get, set
            member val FormQuantities: ValueDto = null with get, set
            member val Strengths: StrengthDto[] = [||] with get, set


        /// A substance of a component.
        [<AllowNullLiteral>]
        type SubstanceDto() =
            member val Name: string = null with get, set
            member val Quantities: ValueDto = null with get, set
            member val Concentrations: ValueDto = null with get, set
            member val Dose: DoseLimitDto = null with get, set
            member val Solution: SolutionLimitDto = null with get, set


        /// A component of the medication.
        [<AllowNullLiteral>]
        type ComponentDto() =
            member val Name: string = null with get, set
            member val Form: string = null with get, set
            member val Quantities: ValueDto = null with get, set
            member val Divisible: string = null with get, set
            member val Dose: DoseLimitDto = null with get, set
            member val Solution: SolutionLimitDto = null with get, set
            member val Products: ProductDto[] = [||] with get, set
            member val Substances: SubstanceDto[] = [||] with get, set


        /// The medication.
        [<AllowNullLiteral>]
        type Dto() =
            member val Id: string = null with get, set
            member val Name: string = null with get, set
            member val Route: string = null with get, set
            member val OrderType: string = null with get, set
            member val Quantity: MinMaxDto = null with get, set
            member val Quantities: ValueDto = null with get, set
            member val Adjust: ValueDto = null with get, set
            member val Frequencies: ValueDto = null with get, set
            member val Time: MinMaxDto = null with get, set
            member val Dose: DoseLimitDto = null with get, set
            member val DoseCount: MinMaxDto = null with get, set
            member val Div: string = null with get, set
            member val Components: ComponentDto[] = [||] with get, set


        let numbersToText vs = vs |> Array.map BigRational.toStringExact


        let numberOptToText br = br |> Option.map BigRational.toStringExact |> Option.toObj


        /// A unit from its text, or None. The parser raises a FormatException for an unknown
        /// unit group, as in mg[Banana], and returns None for the rest it cannot read.
        let tryParseUnit s =
            try
                s |> UnitsParse.fromString
            with :? System.FormatException ->
                None


        /// A unit in English short without its group, or with the group when the short form
        /// does not parse back to the same unit.
        let unitToString u =
            let s = u |> Units.toStringEngShortWithoutGroup

            match s |> tryParseUnit with
            | Some back when back = u -> s
            | _ -> u |> Units.toStringEngShort


        let valueToDto vu =
            let v, u = vu |> ValueUnit.get
            ValueDto(Value = (v |> numbersToText), Unit = (u |> unitToString))


        let valueOptToDto vu = vu |> Option.map valueToDto |> Option.toObj


        let limitToDto lim =
            let incl, vu =
                match lim with
                | Inclusive vu -> true, vu
                | Exclusive vu -> false, vu

            let value = vu |> valueToDto
            LimitDto(Value = value.Value, Unit = value.Unit, Inclusive = incl)


        let minMaxToDto (mm: MinMax) =
            if mm = MinMax.empty then
                null
            else
                MinMaxDto(
                    Min = (mm.Min |> Option.map limitToDto |> Option.toObj),
                    Max = (mm.Max |> Option.map limitToDto |> Option.toObj)
                )


        let doseLimitToDto (dl: DoseLimit) =
            DoseLimitDto(
                DoseUnit =
                    (match dl.DoseUnit with
                     | NoUnit -> null
                     | u -> u |> unitToString),
                AdjustUnit = (dl.AdjustUnit |> Option.map unitToString |> Option.toObj),
                Quantity = (dl.Quantity |> minMaxToDto),
                QuantityAdjust = (dl.QuantityAdjust |> minMaxToDto),
                PerTime = (dl.PerTime |> minMaxToDto),
                PerTimeAdjust = (dl.PerTimeAdjust |> minMaxToDto),
                Rate = (dl.Rate |> minMaxToDto),
                RateAdjust = (dl.RateAdjust |> minMaxToDto)
            )


        let solutionLimitToDto (sl: SolutionLimit) =
            SolutionLimitDto(
                Quantity = (sl.Quantity |> minMaxToDto),
                QuantityAdj = (sl.QuantityAdj |> minMaxToDto),
                Quantities = (sl.Quantities |> valueOptToDto),
                Concentration = (sl.Concentration |> minMaxToDto)
            )


        let strengthToDto (ps: ProductStrength) =
            StrengthDto(Substance = ps.Substance, Strength = (ps.Strength |> valueToDto))


        let productToDto (p: ComponentProduct) =
            ProductDto(
                Id = p.Id,
                FormQuantities = (p.FormQuantities |> valueOptToDto),
                Strengths = (p.Strengths |> List.map strengthToDto |> List.toArray)
            )


        let substanceToDto (si: SubstanceItem) =
            SubstanceDto(
                Name = (si.Name |> DtoResult.blankToNull),
                Quantities = (si.Quantities |> valueOptToDto),
                Concentrations = (si.Concentrations |> valueOptToDto),
                Dose = (si.Dose |> Option.map doseLimitToDto |> Option.toObj),
                Solution = (si.Solution |> Option.map solutionLimitToDto |> Option.toObj)
            )


        let componentToDto (pc: ProductComponent) =
            ComponentDto(
                Name = (pc.Name |> DtoResult.blankToNull),
                Form = (pc.Form |> DtoResult.blankToNull),
                Quantities = (pc.Quantities |> valueOptToDto),
                Divisible = (pc.Divisible |> numberOptToText),
                Dose = (pc.Dose |> Option.map doseLimitToDto |> Option.toObj),
                Solution = (pc.Solution |> Option.map solutionLimitToDto |> Option.toObj),
                Products = (pc.Products |> List.map productToDto |> List.toArray),
                Substances = (pc.Substances |> List.map substanceToDto |> List.toArray)
            )


        /// The Dto of a medication; total.
        let toDto (med: Medication) =
            Dto(
                Id = (med.Id |> DtoResult.blankToNull),
                Name = (med.Name |> DtoResult.blankToNull),
                Route = (med.Route |> DtoResult.blankToNull),
                OrderType = (med.OrderType |> Order.OrderType.toString),
                Quantity = (med.Quantity |> minMaxToDto),
                Quantities = (med.Quantities |> valueOptToDto),
                Adjust = (med.Adjust |> valueOptToDto),
                Frequencies = (med.Frequencies |> valueOptToDto),
                Time = (med.Time |> minMaxToDto),
                Dose = (med.Dose |> Option.map doseLimitToDto |> Option.toObj),
                DoseCount = (med.DoseCount |> minMaxToDto),
                Div = (med.Div |> numberOptToText),
                Components = (med.Components |> List.map componentToDto |> List.toArray)
            )


        let parseUnit path s =
            match s |> tryParseUnit with
            | Some u -> Ok u
            | None -> Error [ DtoError.UnknownUnit(path, s) ]


        let unitOptFromText path s =
            if isNull s then
                Ok None
            else
                s |> parseUnit path |> Result.map Some


        let numberFromText path s =
            match s |> BigRational.tryParseExact with
            | Some br -> Ok br
            | None -> Error [ DtoError.NotANumber(path, s) ]


        let numberOptFromText path s =
            if isNull s then
                Ok None
            else
                s |> numberFromText path |> Result.map Some


        let valueFromDto path (dto: ValueDto) =
            validation {
                let! u = dto.Unit |> DtoResult.orBlank |> parseUnit $"%s{path}.Unit"
                and! vs = dto.Value |> Nested.items $"%s{path}.Value" numberFromText
                return vs |> List.toArray |> ValueUnit.create u
            }


        let valueOptFromDto path dto =
            if isNull dto then
                Ok None
            else
                dto |> valueFromDto path |> Result.map Some


        let limitFromDto path (dto: LimitDto) =
            if isNull dto then
                Ok None
            else
                ValueDto(Value = dto.Value, Unit = dto.Unit)
                |> valueFromDto path
                |> Result.map (fun vu -> Some(if dto.Inclusive then Inclusive vu else Exclusive vu))


        let minMaxFromDto path (dto: MinMaxDto) =
            if isNull dto then
                Ok MinMax.empty
            else
                validation {
                    let! min = dto.Min |> limitFromDto $"%s{path}.Min"
                    and! max = dto.Max |> limitFromDto $"%s{path}.Max"
                    return
                        {
                            Min = min
                            Max = max
                        }
                }


        /// A range of a dose limit whose units pass the check of its field.
        let doseRangeFromDto check path dto =
            dto
            |> minMaxFromDto path
            |> Result.bind (fun mm ->
                let reasons =
                    [ mm.Min; mm.Max ]
                    |> List.choose id
                    |> List.choose (fun lim ->
                        match lim |> Limit.getValueUnit |> ValueUnit.getUnit |> check with
                        | Error reason -> Some reason
                        | Ok() -> None
                    )
                    |> List.distinct

                if reasons |> List.isEmpty then
                    Ok mm
                else
                    reasons |> List.map (fun r -> DtoError.WrongUnit(path, r)) |> Error
            )


        let doseLimitFromDto target path (dto: DoseLimitDto) =
            if isNull dto then
                Ok None
            else
                let field name = $"%s{path}.%s{name}"

                validation {
                    let! doseUnit = dto.DoseUnit |> unitOptFromText (field "DoseUnit")
                    and! adjustUnit = dto.AdjustUnit |> unitOptFromText (field "AdjustUnit")
                    and! qty =
                        dto.Quantity
                        |> doseRangeFromDto UnitValidation.validateQuantityUnit (field "Quantity")
                    and! qtyAdj =
                        dto.QuantityAdjust
                        |> doseRangeFromDto UnitValidation.validateQuantityAdjustUnit (field "QuantityAdjust")
                    and! perTime =
                        dto.PerTime
                        |> doseRangeFromDto UnitValidation.validatePerTimeUnit (field "PerTime")
                    and! perTimeAdj =
                        dto.PerTimeAdjust
                        |> doseRangeFromDto UnitValidation.validatePerTimeAdjustUnit (field "PerTimeAdjust")
                    and! rate = dto.Rate |> doseRangeFromDto UnitValidation.validateRateUnit (field "Rate")
                    and! rateAdj =
                        dto.RateAdjust
                        |> doseRangeFromDto UnitValidation.validateRateAdjustUnit (field "RateAdjust")

                    return
                        Some
                            {
                                DoseLimitTarget = target
                                AdjustUnit = adjustUnit
                                DoseUnit = doseUnit |> Option.defaultValue NoUnit
                                Quantity = qty
                                QuantityAdjust = qtyAdj
                                PerTime = perTime
                                PerTimeAdjust = perTimeAdj
                                Rate = rate
                                RateAdjust = rateAdj
                            }
                }


        let solutionLimitFromDto target path (dto: SolutionLimitDto) =
            if isNull dto then
                Ok None
            else
                validation {
                    let! qty = dto.Quantity |> minMaxFromDto $"%s{path}.Quantity"
                    and! qtyAdj = dto.QuantityAdj |> minMaxFromDto $"%s{path}.QuantityAdj"
                    and! qts = dto.Quantities |> valueOptFromDto $"%s{path}.Quantities"
                    and! conc = dto.Concentration |> minMaxFromDto $"%s{path}.Concentration"

                    return
                        Some
                            {
                                SolutionLimitTarget = target
                                Quantity = qty
                                QuantityAdj = qtyAdj
                                Quantities = qts
                                Concentration = conc
                                Products = [||]
                            }
                }


        let strengthFromDto path (dto: StrengthDto) =
            let strengthPath = $"%s{path}.Strength"

            Nested.required strengthPath dto.Strength (valueFromDto strengthPath)
            |> Result.map (fun vu ->
                {
                    Substance = dto.Substance |> DtoResult.orBlank
                    Strength = vu
                }
            )


        let productFromDto path (dto: ProductDto) =
            validation {
                let! id =
                    if dto.Id |> String.isNullOrWhiteSpace then
                        Error [ DtoError.Missing $"%s{path}.Id" ]
                    else
                        Ok dto.Id

                and! qts = dto.FormQuantities |> valueOptFromDto $"%s{path}.FormQuantities"
                and! strengths = dto.Strengths |> Nested.items $"%s{path}.Strengths" strengthFromDto

                return
                    {
                        Id = id
                        FormQuantities = qts
                        Strengths = strengths
                    }
            }


        let substanceFromDto path (dto: SubstanceDto) =
            let name = dto.Name |> DtoResult.orBlank
            let target = SubstanceLimitTarget name

            validation {
                let! qts = dto.Quantities |> valueOptFromDto $"%s{path}.Quantities"
                and! concs = dto.Concentrations |> valueOptFromDto $"%s{path}.Concentrations"
                and! dose = dto.Dose |> doseLimitFromDto target $"%s{path}.Dose"
                and! sol = dto.Solution |> solutionLimitFromDto target $"%s{path}.Solution"

                return
                    {
                        Name = name
                        Quantities = qts
                        Concentrations = concs
                        Dose = dose
                        Solution = sol
                    }
            }


        /// The products, each refused that cannot be made of the substances.
        let madeOfSubstances path substances products =
            products
            |> List.mapi (fun i p ->
                if p |> ProductComponent.madeOf substances then
                    Ok p
                else
                    Error [ DtoError.NotMadeOf($"%s{path}.Products[%i{i}]", p.Id) ]
            )
            |> List.sequenceValidationA


        let componentFromDto path (dto: ComponentDto) =
            let name = dto.Name |> DtoResult.orBlank
            let target = ComponentLimitTarget name
            let substances = dto.Substances |> Nested.items $"%s{path}.Substances" substanceFromDto

            let products =
                dto.Products
                |> Nested.items $"%s{path}.Products" productFromDto
                // the products can only be checked against substances that were read
                |> Result.bind (fun ps ->
                    match substances with
                    | Ok sis -> ps |> madeOfSubstances path sis
                    | Error _ -> Ok ps
                )

            validation {
                let! qts = dto.Quantities |> valueOptFromDto $"%s{path}.Quantities"
                and! divisible = dto.Divisible |> numberOptFromText $"%s{path}.Divisible"
                and! dose = dto.Dose |> doseLimitFromDto target $"%s{path}.Dose"
                and! sol = dto.Solution |> solutionLimitFromDto target $"%s{path}.Solution"
                and! substances = substances
                and! products = products

                return
                    {
                        Name = name
                        Form = dto.Form |> DtoResult.orBlank
                        Quantities = qts
                        Divisible = divisible
                        Dose = dose
                        Solution = sol
                        Substances = substances
                        Products = products
                    }
            }


        let orderTypeFromDto path s =
            Order.OrderType.all
            |> Array.tryFind (Order.OrderType.toString >> (=) s)
            |> function
                | Some ot -> Ok ot
                | None -> Error [ DtoError.UnknownOrderType(path, s) ]


        /// The medication of a Dto, or every error of it, each with its path.
        let fromDto (dto: Dto) =
            validation {
                let! orderType = dto.OrderType |> DtoResult.orBlank |> orderTypeFromDto "OrderType"
                and! qty = dto.Quantity |> minMaxFromDto "Quantity"
                and! qts = dto.Quantities |> valueOptFromDto "Quantities"
                and! adjust = dto.Adjust |> valueOptFromDto "Adjust"
                and! freqs = dto.Frequencies |> valueOptFromDto "Frequencies"
                and! time = dto.Time |> minMaxFromDto "Time"
                and! dose = dto.Dose |> doseLimitFromDto OrderableLimitTarget "Dose"
                and! doseCount = dto.DoseCount |> minMaxFromDto "DoseCount"
                and! div = dto.Div |> numberOptFromText "Div"
                and! components = dto.Components |> Nested.items "Components" componentFromDto

                return
                    {
                        Id = dto.Id |> DtoResult.orBlank
                        Name = dto.Name |> DtoResult.orBlank
                        Components = components
                        Quantity = qty
                        Quantities = qts
                        Route = dto.Route |> DtoResult.orBlank
                        OrderType = orderType
                        Frequencies = freqs
                        Time = time
                        Dose = dose
                        Div = div
                        DoseCount = doseCount
                        Adjust = adjust
                    }
            }


    let productComponent = ProductComponent.cmp


    let substanceItem = SubstanceItem.item


    /// <summary>
    /// Create a ProductComponent from a list of Products.
    /// DoseLimits are used to set the Dose for the ProductComponent.
    /// If noSubst is true, the substances will not be added to the ProductComponent.
    /// The freqUnit is used to set the TimeUnit for the Frequencies.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="limits">The ComponentLimits for the ProductComponent</param>
    let createComponents logger (limits: ComponentLimit[]) =

        let filterByUnitGroup (context: string) (vus: ValueUnit[]) =
            if vus.Length <= 1 then
                vus
            else
                let grouped = vus |> Array.groupBy ValueUnit.getGroup

                if grouped.Length <= 1 then
                    vus
                else
                    let maxLen = grouped |> Array.map (snd >> Array.length) |> Array.max

                    let winners = grouped |> Array.filter (fun (_, xs) -> xs.Length = maxLen)

                    if winners.Length > 1 then
                        $"{context}: tie between {winners.Length} unit groups of equal size, picking first"
                        |> Events.OrderScenario
                        |> Informedica.GenOrder.Lib.Logging.logWarning logger

                    let _, kept = winners[0]
                    let filtered = vus.Length - kept.Length

                    $"{context}: filtered {filtered} value(s) with incompatible unit group"
                    |> Events.OrderScenario
                    |> Informedica.GenOrder.Lib.Logging.logWarning logger

                    kept

        limits
        |> Array.map (fun lim ->
            // the component quantity rule, shared with the products below
            let oneMl = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter

            let qts =
                lim.Products
                |> Array.map _.FormQuantities
                |> filterByUnitGroup $"Component '{lim.Name}' FormQuantities"
                |> ValueUnit.collect

            // a liquid that needs no reconstitution is counted per mL, not per package
            let perMl =
                let isVol =
                    qts
                    |> Option.map (fun vu -> vu |> ValueUnit.eqsGroup oneMl)
                    |> Option.defaultValue false

                let hasReconst = lim.Products |> Array.forall _.RequiresReconstitution

                isVol && hasReconst |> not

            let quantities = if perMl then Some oneMl else qts

            // the dose limit of a substance decides whether its strength is molar
            let substanceLimit n =
                lim.SubstanceLimits
                |> Array.tryFind (fun l ->
                    match l.DoseLimitTarget with
                    | SubstanceLimitTarget s -> s |> String.equalsCapInsens n
                    | _ -> false
                )

            // the strength of a substance: molar when its dose limit doses in moles
            let strengthOf n (s: Substance) =
                match substanceLimit n with
                | Some dl when dl.DoseUnit |> ValueUnit.Group.eqsGroup Units.Molar.mole -> s.MolarConcentration
                | _ -> s.Concentration

            let shape =
                lim.Products
                |> tryHead _.Form
                |> fun s ->
                    if s |> String.isNullOrWhiteSpace then
                        "oplosvloeistof"
                    else
                        s

            {
                Name =
                    if lim.Name |> String.isNullOrWhiteSpace then
                        "oplosvloeistof"
                    else
                        lim.Name
                Form = shape
                Quantities = quantities
                Divisible = lim.Products |> Array.choose _.Divisible |> Array.tryHead
                Solution = None
                Dose = lim.Limit
                Substances =
                    lim.Products
                    |> Array.collect (fun p -> p.Substances |> Array.map (fun s -> (s, p)))
                    |> Array.groupBy (fst >> _.Name)
                    |> Array.map (fun (n, xs) ->
                        let dl = substanceLimit n

                        {
                            Name = n
                            Quantities =
                                let prods = lim.Products |> Array.filter _.RequiresReconstitution

                                if prods |> Array.isEmpty then
                                    None
                                else
                                    xs
                                    |> Array.choose (fun (s, p) ->
                                        match s.Concentration, p.FormQuantities with
                                        | Some c, q -> c * q |> Some
                                        | _ -> None
                                    )
                                    |> filterByUnitGroup $"Component '{lim.Name}' Substance '{n}' Quantities"
                                    |> ValueUnit.collect
                            Concentrations =
                                xs
                                |> Array.choose (fst >> strengthOf n)
                                |> Array.distinct
                                |> filterByUnitGroup $"Component '{lim.Name}' Substance '{n}' Concentrations"
                                |> ValueUnit.collect
                            Dose = dl
                            Solution = None
                        }
                    )
                    |> Array.toList
                Products =
                    lim.Products
                    |> Array.map (fun p ->
                        {
                            Id = p.GPK
                            FormQuantities = if perMl then Some oneMl else Some p.FormQuantities
                            Strengths =
                                p.Substances
                                |> Array.choose (fun sub ->
                                    sub
                                    |> strengthOf sub.Name
                                    |> Option.map (fun vu ->
                                        {
                                            Substance = sub.Name
                                            Strength = vu
                                        }
                                    )
                                )
                                |> Array.toList
                        }
                    )
                    |> Array.toList
            }
            // a product whose strength the unit group filter left out of its item can never be made
            |> fun pc -> { pc with Products = pc.Products |> List.filter (ProductComponent.madeOf pc.Substances) }
        )
        |> Array.toList


    /// <summary>
    /// Set the SolutionLimits for a list of SubstanceItems.
    /// </summary>
    /// <param name="sls">The SolutionLimits to set</param>
    /// <param name="items">The SubstanceItems to set the SolutionLimits for</param>
    let setSolutionLimit (sls: SolutionLimit[]) (items: SubstanceItem list) =
        items
        |> List.map (fun item ->
            match
                sls
                |> Array.tryFind (fun sl ->
                    match sl.SolutionLimitTarget with
                    | SubstanceLimitTarget s -> s |> String.equalsCapInsens item.Name
                    | _ -> false
                )
            with
            | None -> item
            | Some sl -> { item with Solution = Some sl }
        )


    /// Add an optional solution rule to a medication order
    let addSolution logger (pat: Patient) sr med =
        match sr with
        | None -> med
        | Some sr ->
            { med with
                Dose =
                    { DoseLimit.limit with
                        Rate = sr.DripRate
                        DoseUnit = Units.Volume.milliLiter
                    }
                    |> Some
                Quantity = sr.Volume
                Quantities = if sr.Volumes.IsNone then med.Quantities else sr.Volumes
                Div = sr.Div
                DoseCount =
                    // Change percentage to count!
                    { MinMax.empty with
                        Min = sr.DosePerc.Max
                        Max = sr.DosePerc.Min
                    }
                Components =
                    let ps =
                        med.Components
                        |> List.map (fun pc ->
                            { pc with
                                Form = pc.Form
                                Substances = pc.Substances |> setSolutionLimit sr.SolutionLimits
                            }
                        )

                    sr.Diluents
                    |> Array.tryHead
                    |> function
                        | Some p ->
                            [|
                                {
                                    Name = p.Generic
                                    ProductIds = [| p.GPK |> Gpk |]
                                    Limit = None
                                    Products = [| p |]
                                    SubstanceLimits = [||]
                                }
                            |]
                            |> createComponents logger
                            |> List.append ps
                        | None ->
                            "No diluents available"
                            |> Events.OrderScenario
                            |> Informedica.GenOrder.Lib.Logging.logWarning logger

                            ps
                    |> List.map (fun pc ->
                        { pc with
                            Solution =
                                sr.SolutionLimits
                                |> Array.tryFind (fun sl ->
                                    match sl.SolutionLimitTarget with
                                    | ComponentLimitTarget c -> c |> String.equalsCapInsens pc.Name
                                    | _ -> false
                                )
                                |> Option.map (fun sol ->
                                    { sol with
                                        Quantity =
                                            match pat.Weight with
                                            | None -> sol.Quantity
                                            | Some w ->
                                                match
                                                    sol.Quantity |> MinMax.isEmpty, sol.QuantityAdj |> MinMax.isEmpty
                                                with
                                                | true, false -> sol.QuantityAdj |> MinMax.apply ((*) w)
                                                | true, true ->
                                                    [ sol.Quantity; sol.QuantityAdj |> MinMax.apply ((*) w) ]
                                                    |> MinMax.foldMinimize true true
                                                | _ -> sol.Quantity
                                    }
                                )
                        }
                    )
            }


    /// Create a Medication Order from patient information and dose rules
    let create logger (pat: Patient) au dose (dr: DoseRule) (sr: SolutionRule option) =
        { template with
            Id = Guid.NewGuid().ToString()
            Name = dr.Generic |> Generic.toString
            Components = dr.ComponentLimits |> createComponents logger
            Quantities = None
            Frequencies = dr.Frequencies
            Time = dr.AdministrationTime
            Route = dr.Route
            DoseCount =
                if sr.IsSome then
                    MinMax.empty // Note: dose count will be set in the addSolution
                else
                    // No solution rule, set it to 1
                    let u = Units.Count.times |> Some
                    Utils.MinMax.fromTuple Inclusive Inclusive u (Some 1N, Some 1N)

            OrderType =
                match dr.DoseType with
                | DoseType.Continuous _ -> ContinuousOrder
                | DoseType.OnceTimed _ -> OnceTimedOrder
                | DoseType.Once _ -> OnceOrder
                | DoseType.Discontinuous _ -> DiscontinuousOrder
                | DoseType.Timed _ -> TimedOrder
                | DoseType.NoDoseType -> AnyOrder
            Dose = dose
            Adjust =
                if au |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                    pat.Weight
                else
                    pat |> Patient.calcBSA
        //AdjustUnit = Some au
        }
        |> addSolution logger pat sr


    /// <summary>
    /// Create medication order templates from a PrescriptionRule
    /// </summary>
    /// <param name="logger">The logger instance for logging medication creation events</param>
    /// <param name="pr">The PrescriptionRule to use</param>
    let fromRule logger (pr: PrescriptionRule) =
        let au = pr.DoseRule.AdjustUnit |> Option.defaultValue Units.Weight.kiloGram

        let dose = pr.DoseRule.FormLimit

        let create = create logger pr.Patient au dose pr.DoseRule

        let meds =
            if pr.SolutionRules |> Array.isEmpty then
                [| create None |]
            else
                pr.SolutionRules |> Array.map Some |> Array.map create

        meds
        |> Array.iter (Events.MedicationCreated >> Informedica.GenOrder.Lib.Logging.logInfo logger)

        meds


    /// The concentrations of a substance that its solution limit allows, judged by the
    /// bounds withItemSolution sets: inclusive, and widened to 90 and 110 percent when the
    /// limit is one value. A substance with no concentrations or no concentration limit keeps
    /// what it has, and so does one whose concentrations are in a unit group the limit cannot
    /// be compared with (a molar concentration against a mass limit): the bounds are compared
    /// on base values, so those would compare as numbers and give a wrong answer.
    let concentrationsWithinSolution (si: SubstanceItem) =
        match si.Concentrations, si.Solution with
        | Some vu, Some sl when sl.Concentration <> MinMax.empty ->
            let comparable =
                [ sl.Concentration.Min; sl.Concentration.Max ]
                |> List.choose id
                |> List.forall (Limit.getValueUnit >> ValueUnit.eqsGroup vu)

            if comparable then
                let cs =
                    OrderVariable.Constraints.create None None None None
                    |> OrderVariable.Constraints.setMinMax true sl.Concentration

                vu
                |> ValueUnit.filter (Informedica.GenSolver.Lib.Variable.ValueRange.isBetweenMinMax cs.Min cs.Max)
                |> Some
            else
                si.Concentrations
        | _ -> si.Concentrations


    /// The substances of a medication given undiluted whose solution limit allows none of
    /// the concentrations its product has. Only a medication with one component is given
    /// undiluted; with more, the orderable concentration is the solver's to find.
    let undilutedOutsideSolution (med: Medication) =
        match med.Components with
        | [ pc ] ->
            pc.Substances
            |> List.filter (fun si ->
                si.Concentrations |> Option.exists (ValueUnit.isEmpty >> not)
                && si |> concentrationsWithinSolution |> Option.exists ValueUnit.isEmpty
            )
        | _ -> []


    /// An Order built from a Medication: the shape first, then one pass per constraint
    /// concern, each pass pure and each leaving the rest of the order alone.
    module OrderBuilder =


        /// The schedule an order type asks for. An order that is neither prescribed nor
        /// administered has no schedule to build, and no order either.
        let scheduleOf (med: Medication) =
            match med.OrderType with
            | OnceOrder -> Order.Schedule.once NoUnit NoUnit
            | OnceTimedOrder -> Order.Schedule.onceTimed NoUnit NoUnit
            | ContinuousOrder -> Order.Schedule.continuous NoUnit NoUnit
            | DiscontinuousOrder -> Order.Schedule.discontinuous NoUnit NoUnit
            | TimedOrder -> Order.Schedule.timed NoUnit NoUnit
            | AnyOrder
            | ProcessOrder ->
                $"a medication order cannot have the order type %A{med.OrderType}"
                |> NotSupportedException
                |> raise


        /// The empty order an order type asks for: its schedule, its id, its name and its
        /// route, and no component yet. The start is the caller's, in UTC.
        let newOrder (start: DateTime) (med: Medication) =
            med |> scheduleOf |> Order.createNew start med.Id med.Name <| med.Route


        /// The components of the medication and their items, as shape alone: every order
        /// variable is the empty one its name gives it, and no constraint is set here.
        let withComponents (med: Medication) (ord: Order) =
            let components =
                med.Components
                |> List.map (fun pc ->
                    let cmp = Order.Orderable.Component.createNew med.Id med.Name pc.Name pc.Form

                    { cmp with
                        Items =
                            pc.Substances
                            |> List.map (fun si -> Order.Orderable.Item.createNew med.Id med.Name pc.Name si.Name)
                        Products = pc.Products
                    }
                )

            { ord with Orderable = { ord.Orderable with Components = components } }


        /// Walk the medication and the order together. Both lists came from the same
        /// medication in the shape pass, so they pair up by position; a mismatch means the
        /// shape pass changed and the build should stop rather than quietly constrain the
        /// wrong item.
        let mapItems f (med: Medication) (ord: Order) =
            let components =
                List.zip med.Components ord.Orderable.Components
                |> List.map (fun (pc, cmp) ->
                    { cmp with Items = List.zip pc.Substances cmp.Items |> List.map (fun (si, itm) -> f pc si itm) }
                )

            { ord with Orderable = { ord.Orderable with Components = components } }


        /// A single value in a unit, or nothing when there is no unit to give it.
        let singleOrNone u br =
            if u = NoUnit then
                None
            else
                br |> ValueUnit.singleWithUnit u |> Some


        /// What a solution asks of an item: how much of it the orderable may hold, and in
        /// what concentration.
        let withItemSolution (sl: SolutionLimit) (itm: Types.Item) =
            { itm with
                OrderableQuantity =
                    itm.OrderableQuantity
                    |> OrderVariable.Quantity.apply (
                        OrderVariable.mapConstraints (OrderVariable.Constraints.setMinMax false sl.Quantity)
                    )
                OrderableConcentration =
                    itm.OrderableConcentration
                    |> OrderVariable.Concentration.apply (
                        OrderVariable.mapConstraints (OrderVariable.Constraints.setMinMax true sl.Concentration)
                    )
            }


        /// The quantities and concentrations the products give an item. With one component
        /// the orderable is the component, so the concentration in the one is the
        /// concentration in the other, and only the concentrations the solution limit allows
        /// are offered: nothing dilutes the product to bring another within the limit.
        let withItemQtyConc (med: Medication) (si: SubstanceItem) (itm: Types.Item) =
            let single = med.Components |> List.length = 1
            let setVals vu =
                OrderVariable.mapConstraints (OrderVariable.Constraints.setValues vu)

            { itm with
                ComponentConcentration =
                    itm.ComponentConcentration
                    |> OrderVariable.Concentration.apply (setVals si.Concentrations)
                ComponentQuantity = itm.ComponentQuantity |> OrderVariable.Quantity.apply (setVals si.Quantities)
                OrderableConcentration =
                    if single then
                        itm.OrderableConcentration
                        |> OrderVariable.Concentration.apply (setVals (si |> concentrationsWithinSolution))
                    else
                        itm.OrderableConcentration
            }
            |> fun itm ->
                match si.Solution with
                | None -> itm
                | Some sl -> itm |> withItemSolution sl


        /// What the dose rule allows of the substance, in the terms the order type doses in.
        let withItemDose (med: Medication) (si: SubstanceItem) (itm: Types.Item) =
            let con f = OrderVariable.mapConstraints f

            let rate (dl: DoseLimit) (dos: Dose) =
                { dos with
                    Rate =
                        dos.Rate
                        |> OrderVariable.Rate.apply (con (OrderVariable.Constraints.setMinMax false dl.Rate))
                    RateAdjust =
                        dos.RateAdjust
                        |> OrderVariable.RateAdjust.apply (con (OrderVariable.Constraints.setMinMax true dl.RateAdjust))
                }

            let quantity (dl: DoseLimit) (dos: Dose) =
                { dos with
                    Quantity =
                        dos.Quantity
                        |> OrderVariable.Quantity.apply (
                            con (fun cs ->
                                // a dose quantity without a limit still needs a unit, so that
                                // it can be added up with the others
                                if dl.Quantity |> MinMax.isEmpty then
                                    cs |> OrderVariable.Constraints.setMin false (0N |> singleOrNone dl.DoseUnit)
                                else
                                    cs |> OrderVariable.Constraints.setMinMax false dl.Quantity
                            )
                        )
                    QuantityAdjust =
                        dos.QuantityAdjust
                        |> OrderVariable.QuantityAdjust.apply (
                            con (OrderVariable.Constraints.setMinMax true dl.QuantityAdjust)
                        )
                    PerTime =
                        dos.PerTime
                        |> OrderVariable.PerTime.apply (con (OrderVariable.Constraints.setMinMax false dl.PerTime))
                    PerTimeAdjust =
                        dos.PerTimeAdjust
                        |> OrderVariable.PerTimeAdjust.apply (
                            con (OrderVariable.Constraints.setMinMax true dl.PerTimeAdjust)
                        )
                }

            let apply =
                match med.OrderType with
                | AnyOrder
                | ProcessOrder -> fun _ dos -> dos
                | ContinuousOrder -> rate
                | OnceOrder
                | DiscontinuousOrder -> quantity
                | OnceTimedOrder
                | TimedOrder -> fun dl dos -> dos |> rate dl |> quantity dl

            match si.Dose with
            | None -> itm
            | Some dl -> { itm with Dose = itm.Dose |> apply dl }


        /// Every constraint an item carries.
        let withItemConstraints (med: Medication) (ord: Order) =
            ord
            |> mapItems (fun _ si itm -> itm |> withItemQtyConc med si |> withItemDose med si) med


        /// The unit the orderable is measured in: the one the first component is measured in.
        let orderableUnit (med: Medication) =
            med.Components
            |> List.tryHead
            |> Option.bind (fun pc -> pc.Quantities |> Option.map ValueUnit.getUnit)


        /// The smallest step a product can be divided into. A medication that says how
        /// divisible it is says it for the orderable; otherwise the coarsest of what its
        /// components say.
        let divisibility (pc: ProductComponent option) (med: Medication) =
            let ou = med |> orderableUnit
            let pu = pc |> Option.bind (_.Quantities >> Option.map ValueUnit.getUnit)

            match ou, med.Div with
            | Some ou, Some br when pu.IsNone || Some ou = pu -> 1N / br |> singleOrNone ou
            | Some ou, None ->
                let incrs =
                    med.Components
                    |> List.choose (fun pc ->
                        if pc.Quantities |> Option.map ValueUnit.getUnit = pu || pu.IsNone then
                            pc.Divisible |> Option.map (fun d -> 1N / d)
                        else
                            None
                    )

                if incrs |> List.isEmpty then
                    None
                else
                    let u = pu |> Option.defaultValue ou
                    incrs |> List.max |> singleOrNone u
            | _ -> None


        /// Walk the medication's components and the order's together.
        let mapComponents f (med: Medication) (ord: Order) =
            { ord with
                Orderable =
                    { ord.Orderable with
                        Components =
                            List.zip med.Components ord.Orderable.Components
                            |> List.map (fun (pc, cmp) -> f pc cmp)
                    }
            }


        /// How much of a component the orderable holds, and in what concentration. A
        /// component can never be more than all of the orderable, and where it is the only
        /// one it is all of it.
        let withComponentQtyConc (med: Medication) (pc: ProductComponent) (cmp: Types.Component) =
            let single = med.Components |> List.length = 1
            let incr = med |> divisibility (Some pc)
            let all = Units.Count.times |> ValueUnit.singleWithValue 1N |> Some
            let con f = OrderVariable.mapConstraints f

            { cmp with
                OrderableConcentration =
                    cmp.OrderableConcentration
                    |> OrderVariable.Concentration.apply (
                        con (fun cs ->
                            cs
                            |> OrderVariable.Constraints.setMax single all
                            |> fun cs ->
                                if single then
                                    cs |> OrderVariable.Constraints.setValues all
                                else
                                    cs
                        )
                    )
                ComponentQuantity =
                    cmp.ComponentQuantity
                    |> OrderVariable.Quantity.apply (con (OrderVariable.Constraints.setValues pc.Quantities))
                OrderableQuantity =
                    cmp.OrderableQuantity
                    |> OrderVariable.Quantity.apply (
                        con (fun cs ->
                            cs
                            |> OrderVariable.Constraints.setIncr incr
                            |> fun cs ->
                                match pc.Solution with
                                | None -> cs
                                | Some sol ->
                                    cs
                                    |> OrderVariable.Constraints.setValues sol.Quantities
                                    |> OrderVariable.Constraints.setMinMax false sol.Quantity
                        )
                    )
                Dose =
                    if single then
                        { cmp.Dose with
                            Quantity =
                                cmp.Dose.Quantity
                                |> OrderVariable.Quantity.apply (con (OrderVariable.Constraints.setIncr incr))
                        }
                    else
                        cmp.Dose
            }


        /// What the dose rule allows of the component. Where it allows nothing, the dose
        /// quantity still gets a unit, so that it can be added up with the others.
        let withComponentDose (med: Medication) (pc: ProductComponent) (cmp: Types.Component) =
            let con f = OrderVariable.mapConstraints f

            let zero =
                pc.Quantities
                |> Option.map ValueUnit.getUnit
                |> Option.bind (fun u -> 0N |> singleOrNone u)

            let ifGiven (mm: MinMax) f cs = if mm |> MinMax.isEmpty then cs else cs |> f

            let rate (dl: DoseLimit) (dos: Dose) =
                { dos with
                    Rate =
                        dos.Rate
                        |> OrderVariable.Rate.apply (
                            con (ifGiven dl.Rate (OrderVariable.Constraints.setMinMax false dl.Rate))
                        )
                    RateAdjust =
                        dos.RateAdjust
                        |> OrderVariable.RateAdjust.apply (
                            con (ifGiven dl.RateAdjust (OrderVariable.Constraints.setMinMax true dl.RateAdjust))
                        )
                }

            let quantity (dl: DoseLimit) (dos: Dose) =
                { dos with
                    Quantity =
                        dos.Quantity
                        |> OrderVariable.Quantity.apply (
                            con (fun cs ->
                                if dl.Quantity |> MinMax.isEmpty then
                                    cs |> OrderVariable.Constraints.setMin false zero
                                else
                                    cs |> OrderVariable.Constraints.setMinMax false dl.Quantity
                            )
                        )
                    QuantityAdjust =
                        dos.QuantityAdjust
                        |> OrderVariable.QuantityAdjust.apply (
                            con (ifGiven dl.QuantityAdjust (OrderVariable.Constraints.setMinMax true dl.QuantityAdjust))
                        )
                    PerTime =
                        dos.PerTime
                        |> OrderVariable.PerTime.apply (
                            con (ifGiven dl.PerTime (OrderVariable.Constraints.setMinMax false dl.PerTime))
                        )
                    PerTimeAdjust =
                        dos.PerTimeAdjust
                        |> OrderVariable.PerTimeAdjust.apply (
                            con (ifGiven dl.PerTimeAdjust (OrderVariable.Constraints.setMinMax true dl.PerTimeAdjust))
                        )
                }

            let apply =
                match med.OrderType with
                | AnyOrder
                | ProcessOrder -> fun _ dos -> dos
                | ContinuousOrder -> rate
                | OnceOrder
                | DiscontinuousOrder -> quantity
                | OnceTimedOrder
                | TimedOrder -> fun dl dos -> dos |> rate dl |> quantity dl

            match pc.Dose with
            | None -> cmp
            | Some dl -> { cmp with Dose = cmp.Dose |> apply dl }


        /// Every constraint a component carries.
        let withComponentConstraints (med: Medication) (ord: Order) =
            ord
            |> mapComponents (fun pc cmp -> cmp |> withComponentQtyConc med pc |> withComponentDose med pc) med


        /// Several values in a unit, or nothing when there is no unit to give them.
        let valuesOrNone u brs =
            if u = NoUnit then
                None
            else
                brs |> ValueUnit.withUnit u |> Some


        /// The time unit a frequency is counted over: the denominator of its unit.
        let frequencyTimeUnit (med: Medication) =
            med.Frequencies
            |> Option.map (ValueUnit.getUnit >> ValueUnit.getUnits)
            |> function
                | Some [ _; tu ] -> Some tu
                | _ -> None


        /// How much of the orderable is given, and how often it is counted. A medication that
        /// names its quantities takes those; one that does not takes anything above nothing.
        let withOrderableQuantity (med: Medication) (ord: Order) =
            let zero = med |> orderableUnit |> Option.bind (fun u -> 0N |> singleOrNone u)
            let con f = OrderVariable.mapConstraints f

            { ord with
                Orderable =
                    { ord.Orderable with
                        DoseCount =
                            ord.Orderable.DoseCount
                            |> OrderVariable.Count.apply (con (OrderVariable.Constraints.setMinMax false med.DoseCount))
                        OrderableQuantity =
                            ord.Orderable.OrderableQuantity
                            |> OrderVariable.Quantity.apply (
                                con (fun cs ->
                                    cs
                                    |> OrderVariable.Constraints.setMinMax false med.Quantity
                                    |> fun cs ->
                                        match med.Quantities with
                                        | None -> cs |> OrderVariable.Constraints.setMin false zero
                                        | Some _ -> cs |> OrderVariable.Constraints.setValues med.Quantities
                                )
                            )
                    }
            }


        /// What the dose rule allows of the orderable as a whole. Where it allows nothing, a
        /// dose quantity and a dose per time still get a unit, so that they can take part in
        /// the sums.
        let withOrderableDose (med: Medication) (ord: Order) =
            let ou = med |> orderableUnit
            let rateUnit = ou |> Option.map (ValueUnit.per Units.Time.hour)
            let freqTimeUnit = med |> frequencyTimeUnit
            let incr = med |> divisibility None
            let con f = OrderVariable.mapConstraints f

            let rate (dl: DoseLimit option) (dos: Dose) =
                { dos with
                    Rate =
                        dos.Rate
                        |> OrderVariable.Rate.apply (
                            con (fun cs ->
                                // an infusion rate steps by a tenth unless something says
                                // otherwise
                                cs
                                |> OrderVariable.Constraints.setIncr (
                                    rateUnit |> Option.bind (fun ru -> [| 1N / 10N |] |> valuesOrNone ru)
                                )
                                |> fun cs ->
                                    match dl with
                                    | None -> cs
                                    | Some dl -> cs |> OrderVariable.Constraints.setMinMax false dl.Rate
                            )
                        )
                    RateAdjust =
                        match dl with
                        | None -> dos.RateAdjust
                        | Some dl ->
                            dos.RateAdjust
                            |> OrderVariable.RateAdjust.apply (
                                con (OrderVariable.Constraints.setMinMax false dl.RateAdjust)
                            )
                }

            let quantity isOnce (dl: DoseLimit option) (dos: Dose) =
                let zeroQty = ou |> Option.bind (fun u -> 0N |> singleOrNone u)

                let zeroPerTime =
                    match ou, freqTimeUnit with
                    | Some u, Some tu -> 0N |> singleOrNone (u |> ValueUnit.per tu)
                    | _ -> None

                { dos with
                    Quantity =
                        dos.Quantity
                        |> OrderVariable.Quantity.apply (
                            con (fun cs ->
                                cs
                                |> OrderVariable.Constraints.setIncr incr
                                |> fun cs ->
                                    match dl with
                                    | None -> cs |> OrderVariable.Constraints.setMin false zeroQty
                                    | Some dl ->
                                        cs
                                        |> OrderVariable.Constraints.setMinMax false dl.Quantity
                                        |> fun cs ->
                                            if dl.Quantity |> MinMax.isEmpty then
                                                cs |> OrderVariable.Constraints.setMin false zeroQty
                                            else
                                                cs
                            )
                        )
                    QuantityAdjust =
                        match dl with
                        | None -> dos.QuantityAdjust
                        | Some dl ->
                            dos.QuantityAdjust
                            |> OrderVariable.QuantityAdjust.apply (
                                con (OrderVariable.Constraints.setMinMax true dl.QuantityAdjust)
                            )
                    PerTime =
                        match dl, isOnce with
                        | None, _ ->
                            dos.PerTime
                            |> OrderVariable.PerTime.apply (con (OrderVariable.Constraints.setMin false zeroPerTime))
                        | Some _, true -> dos.PerTime
                        | Some dl, false ->
                            dos.PerTime
                            |> OrderVariable.PerTime.apply (
                                con (fun cs ->
                                    cs
                                    |> OrderVariable.Constraints.setMinMax false dl.PerTime
                                    |> fun cs ->
                                        if dl.PerTime |> MinMax.isEmpty then
                                            cs |> OrderVariable.Constraints.setMin false zeroPerTime
                                        else
                                            cs
                                )
                            )
                    PerTimeAdjust =
                        match dl, isOnce with
                        | Some dl, false ->
                            dos.PerTimeAdjust
                            |> OrderVariable.PerTimeAdjust.apply (
                                con (OrderVariable.Constraints.setMinMax true dl.PerTimeAdjust)
                            )
                        | _ -> dos.PerTimeAdjust
                }

            // a timed order is taken to be a solution: what is not given a value is stepped by
            // the coarsest step the products allow
            let timed (orb: Orderable) =
                let stepUnlessValued cs =
                    if cs.Values |> Option.isSome then
                        cs
                    else
                        cs |> OrderVariable.Constraints.setIncr incr

                { orb with
                    Dose =
                        { orb.Dose with
                            Quantity = orb.Dose.Quantity |> OrderVariable.Quantity.apply (con stepUnlessValued)
                        }
                    OrderableQuantity = orb.OrderableQuantity |> OrderVariable.Quantity.apply (con stepUnlessValued)
                }

            // with one component and no dose of its own, the orderable doses as that component
            let dose =
                match med.Dose, med.Components with
                | None, [ single ] -> single.Dose
                | _ -> med.Dose

            let withDose f (orb: Orderable) = { orb with Dose = orb.Dose |> f }

            let orderable =
                { ord.Orderable with
                    OrderableQuantity =
                        ord.Orderable.OrderableQuantity
                        |> OrderVariable.Quantity.apply (con (OrderVariable.Constraints.setIncr incr))
                }
                |> fun orb ->
                    match med.OrderType with
                    | AnyOrder
                    | ProcessOrder -> orb
                    | ContinuousOrder -> orb |> withDose (rate dose)
                    | OnceOrder -> orb |> withDose (quantity true dose)
                    | OnceTimedOrder -> orb |> withDose (rate dose) |> withDose (quantity true dose)
                    | DiscontinuousOrder -> orb |> withDose (quantity false dose)
                    | TimedOrder -> orb |> timed |> withDose (rate dose) |> withDose (quantity false dose)

            { ord with Orderable = orderable }


        /// Every constraint the orderable carries.
        let withOrderableConstraints (med: Medication) (ord: Order) =
            ord |> withOrderableQuantity med |> withOrderableDose med


        /// How often the order is given and how long an administration takes. A schedule only
        /// carries what its kind has: a once order has neither, a continuous one no frequency.
        let withPrescription (med: Medication) (ord: Order) =
            let con f = OrderVariable.mapConstraints f

            let onFrequency (frq: Frequency) =
                frq
                |> OrderVariable.Frequency.apply (
                    con (fun cs ->
                        cs
                        |> OrderVariable.Constraints.setValues med.Frequencies
                        |> fun cs ->
                            match med.Frequencies with
                            | None -> cs
                            // a frequency is a whole number of times
                            | Some fu ->
                                cs
                                |> OrderVariable.Constraints.setIncr (1N |> singleOrNone (fu |> ValueUnit.getUnit))
                    )
                )

            let onTime (tme: Time) =
                tme
                |> OrderVariable.Time.apply (
                    con (fun cs ->
                        cs
                        // a lower bound the medication does not give leaves the one the empty
                        // order variable was built with, which is nothing below nothing
                        |> fun cs ->
                            match med.Time.Min with
                            | None -> cs
                            | Some _ ->
                                cs
                                |> OrderVariable.Constraints.setMin
                                    true
                                    (med.Time.Min |> Option.map Limit.getValueUnit)
                        |> OrderVariable.Constraints.setMax
                            med.Time.Max.IsSome
                            (med.Time.Max |> Option.map Limit.getValueUnit)
                    )
                )

            { ord with
                Schedule =
                    match ord.Schedule with
                    | Once -> Once
                    | OnceTimed tme -> tme |> onTime |> OnceTimed
                    | Continuous tme -> tme |> onTime |> Continuous
                    | Discontinuous frq -> frq |> onFrequency |> Discontinuous
                    | Timed(frq, tme) -> (frq |> onFrequency, tme |> onTime) |> Timed
            }


        /// What the dose is adjusted to: the patient's weight or body surface. A weight is
        /// bounded by what a patient can weigh, so that a typo cannot pass for a dose.
        let withAdjustment (med: Medication) (ord: Order) =
            let bounds cs =
                match med.Adjust with
                | None -> cs
                | Some vu ->
                    let u = vu |> ValueUnit.getUnit

                    if u |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                        cs
                        |> OrderVariable.Constraints.setMin false (200N / 1000N |> singleOrNone u)
                        |> OrderVariable.Constraints.setMax false (150N |> singleOrNone u)
                    else
                        cs

            { ord with
                Adjust =
                    ord.Adjust
                    |> OrderVariable.Quantity.apply (
                        OrderVariable.mapConstraints (bounds >> OrderVariable.Constraints.setValues med.Adjust)
                    )
            }


        /// Build an Order from a Medication: the shape, and then every constraint. The start
        /// is the caller's, in UTC.
        let build (start: DateTime) (med: Medication) =
            med
            |> newOrder start
            |> withComponents med
            |> withItemConstraints med
            |> withComponentConstraints med
            |> withOrderableConstraints med
            |> withPrescription med
            |> withAdjustment med


    /// Build an Order from a Medication, or say why it could not be built. The start is the
    /// caller's, in UTC. A medication given undiluted at a concentration its solution limit
    /// forbids is not built: there is no diluent to bring it within the limit, so no order of
    /// it can be prescribed.
    let toOrder (start: DateTime) (med: Medication) : Result<Order, Exceptions.Message> =
        match med |> undilutedOutsideSolution with
        | [] ->
            try
                med |> OrderBuilder.build start |> Ok
            with exn ->
                exn |> Exceptions.OrderCouldNotBeCreated |> Error
        | sis ->
            let names = sis |> List.map _.Name |> String.concat ", "

            $"%s{med.Name} has no diluent, and its solution limit allows no concentration of %s{names} "
            + "its product has"
            |> InvalidOperationException
            :> exn
            |> Exceptions.OrderCouldNotBeCreated
            |> Error


    /// Convert a Medication to an Order Dto, for the callers that still want one, chiefly the
    /// totals; it goes when they take an Order instead. A medication that cannot be ordered
    /// raises here, with the exception that said why, since those callers have nowhere to put
    /// a failure; toOrder gives them the same failure as a value.
    let toOrderDto (start: DateTime) (med: Medication) =
        match med |> toOrder start with
        | Ok ord -> ord |> Order.Dto.toDto
        | Error(Exceptions.OrderCouldNotBeCreated exn) -> exn |> raise
        | Error msg -> msg |> Exceptions.OrderException |> raise
