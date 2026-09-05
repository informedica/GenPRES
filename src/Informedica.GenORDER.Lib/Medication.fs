namespace Informedica.GenOrder.Lib


module Medication =

    open System
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime
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


    /// Parser module to parse Medication text back to Medication record
    module Parser =


        /// Parse a line into (indentLevel, key, value)
        /// Handles both tab and space indentation for compatibility with different FSI environments
        let parseLine (line: string) =
            // Try tabs first (preferred), then fall back to spaces (groups of 4)
            let tabIndent = line |> Seq.takeWhile ((=) '\t') |> Seq.length

            let indent =
                if tabIndent > 0 then
                    tabIndent
                else
                    // Count leading spaces and convert to indent level (4 spaces = 1 indent)
                    let spaceCount = line |> Seq.takeWhile ((=) ' ') |> Seq.length
                    spaceCount / 4

            let content = line.TrimStart([| '\t'; ' ' |])

            match content.IndexOf(':') with
            | -1 -> None
            | i ->
                let key = content[.. i - 1].Trim()
                let value = content[i + 1 ..].Trim()
                Some(indent, key, value)


        /// Parse OrderType from string
        let parseOrderType (s: string) =
            match s with
            | "AnyOrder" -> Ok AnyOrder
            | "ProcessOrder" -> Ok ProcessOrder
            | "OnceOrder" -> Ok OnceOrder
            | "OnceTimedOrder" -> Ok OnceTimedOrder
            | "ContinuousOrder" -> Ok ContinuousOrder
            | "DiscontinuousOrder" -> Ok DiscontinuousOrder
            | "TimedOrder" -> Ok TimedOrder
            | _ -> Error $"Unknown OrderType: {s}"


        /// Parse BigRational option from string (e.g., "2" or "1,5")
        let parseBigRationalOpt (s: string) =
            if s |> String.IsNullOrWhiteSpace then
                None
            else
                // Handle Dutch decimal format (comma)
                s.Replace(",", ".") |> Double.tryParse |> Option.bind BigRational.fromFloat


        /// Parse Dutch decimal value (comma as decimal separator, space as thousands separator)
        let parseDutchDecimal (s: string) : BigRational option =
            let cleaned =
                s
                    .Trim()
                    .Replace(" ", "") // Remove space thousands separator
                    .Replace(",", ".") // Convert Dutch decimal to standard

            cleaned |> Double.tryParse |> Option.bind BigRational.fromFloat


        /// Parse ValueUnit from Dutch format string
        /// Handles: "3;4 x/dag", "1, 10 mg/mL", "1 000 mg", "0,5 mL", "1 stuk"
        /// Note: comma-space "," is value separator, comma without space is Dutch decimal
        let parseValueUnit (s: string) : Result<ValueUnit, string> =
            if s |> String.IsNullOrWhiteSpace then
                Error "Empty ValueUnit string"
            else
                s |> ValueUnit.fromString


        /// Parse ValueUnit option (returns Ok None for empty string)
        let parseValueUnitOpt (s: string) : Result<ValueUnit option, string> =
            if s |> String.IsNullOrWhiteSpace then
                Ok None
            else
                parseValueUnit s |> Result.map Some


        /// Parse MinMax from formatted string
        /// Handles: "" (empty), "10 mg" (exact), "10 - 20 mg" (range),
        ///          "min 10 mg" (min only), "max 10 mg" (max only)
        let parseMinMax = MinMax.parseMinMax


        /// Parse MinMax that may have /dosis suffix stripped
        let parseMinMaxWithSuffix (s: string) (suffix: string) : Result<MinMax, string> =
            let s = s.Replace(suffix, "").Trim()
            parseMinMax s


        /// Get the unit from a MinMax if available
        let getMinMaxUnit (mm: MinMax) : Unit option =
            match mm.Min, mm.Max with
            | Some lim, _ -> lim |> Limit.getValueUnit |> ValueUnit.getUnit |> Some
            | _, Some lim -> lim |> Limit.getValueUnit |> ValueUnit.getUnit |> Some
            | None, None -> None


        /// Parse and validate a MinMax value for a specific field type
        let parseMinMaxForField
            (validator: Unit -> Result<unit, string>)
            (s: string)
            (perDose: bool)
            : Result<MinMax, string>
            =
            let s = if perDose then s.Replace("/dosis", "").Trim() else s

            match parseMinMax s with
            | Error e -> Error e
            | Ok mm when mm = MinMax.empty -> Ok mm
            | Ok mm ->
                match getMinMaxUnit mm with
                | None -> Ok mm
                | Some unit ->
                    match validator unit with
                    | Error e -> Error e
                    | Ok() -> Ok mm


        /// Parse a MinMax value without validation (for labeled fields where we trust the label)
        let parseMinMaxNoValidation (s: string) (perDose: bool) : Result<MinMax, string> =
            let s = if perDose then s.Replace("/dosis", "").Trim() else s
            parseMinMax s


        /// Parse DoseLimit from comma-separated constraint string with explicit field labels
        /// Format: "targetName, [field-label] constraint1, [field-label] constraint2, ..."
        /// e.g., "paracetamol, [qty-adj] 10 - 20 mg/kg/dosis"
        /// Note: Cannot split naively by comma because Dutch decimals use comma (e.g., "5,4")
        let parseDoseLimitOpt target (s: string) : Result<DoseLimit option, string> =
            if s |> String.IsNullOrWhiteSpace then
                Ok None
            else
                // All known field labels
                let allLabels =
                    [
                        DoseLimit.FieldLabels.DoseUnit
                        DoseLimit.FieldLabels.Quantity
                        DoseLimit.FieldLabels.QuantityAdjust
                        DoseLimit.FieldLabels.PerTime
                        DoseLimit.FieldLabels.PerTimeAdjust
                        DoseLimit.FieldLabels.Rate
                        DoseLimit.FieldLabels.RateAdjust
                    ]

                // Find the position of the first field label to separate target from constraints
                let firstLabelPos =
                    allLabels
                    |> List.choose (fun label ->
                        let idx = s.IndexOf(label)
                        if idx >= 0 then Some idx else None
                    )
                    |> List.sort
                    |> List.tryHead

                // Extract target name (everything before first label, trimmed of ", ")
                let target, constraintsStr =
                    match firstLabelPos with
                    | Some pos when pos > 0 ->
                        let targetStr = s.Substring(0, pos).Trim().TrimEnd(',').Trim()
                        let constraintsStr = s.Substring(pos)

                        if
                            targetStr |> String.isNullOrWhiteSpace
                            && constraintsStr |> String.isNullOrWhiteSpace
                        then
                            NoLimitTarget, constraintsStr
                        else
                            target targetStr, constraintsStr
                    | Some 0 -> NoLimitTarget, s
                    | None ->
                        // No labels found - try legacy parsing
                        // Split by ", " followed by a digit or min/max
                        let splitRegex = Text.RegularExpressions.Regex(", (?=\d|min |max |\[)")
                        let parts = splitRegex.Split(s) |> Array.map _.Trim() |> Array.toList

                        match parts with
                        | [] -> NoLimitTarget, ""
                        | first :: rest when not (first |> Seq.exists Char.IsDigit) ->
                            SubstanceLimitTarget first, rest |> String.concat ", "
                        | _ -> NoLimitTarget, s
                    | _ -> raise (System.FormatException $"{firstLabelPos} not valid")

                // Split constraints by field labels using regex
                // Pattern matches: [label] value until next [label] or end
                let constraintRegex = Text.RegularExpressions.Regex(@"\[([^\]]+)\]\s*([^[]*)")
                let matches = constraintRegex.Matches(constraintsStr)

                // Parse constraints into appropriate MinMax fields
                let mutable dl = { DoseLimit.limit with DoseLimitTarget = target }
                let mutable errors = []

                // Field label parsers - when we have explicit labels, we trust them
                // and don't apply validation (validation is only for heuristic fallback)
                let fieldParsers
                    : (string *
                      (string -> Result<Informedica.GenCore.Lib.Ranges.MinMax, string>) *
                      (DoseLimit -> Informedica.GenCore.Lib.Ranges.MinMax -> DoseLimit)) list =
                    [
                        DoseLimit.FieldLabels.Quantity,
                        (fun s -> parseMinMaxNoValidation s true),
                        (fun (dl: DoseLimit) mm -> { dl with Quantity = mm })

                        DoseLimit.FieldLabels.QuantityAdjust,
                        (fun s -> parseMinMaxNoValidation s true),
                        (fun (dl: DoseLimit) mm -> { dl with QuantityAdjust = mm })

                        DoseLimit.FieldLabels.PerTime,
                        (fun s -> parseMinMaxNoValidation s false),
                        (fun (dl: DoseLimit) mm -> { dl with PerTime = mm })

                        DoseLimit.FieldLabels.PerTimeAdjust,
                        (fun s -> parseMinMaxNoValidation s false),
                        (fun (dl: DoseLimit) mm -> { dl with PerTimeAdjust = mm })

                        DoseLimit.FieldLabels.Rate,
                        (fun s -> parseMinMaxNoValidation s false),
                        (fun (dl: DoseLimit) mm -> { dl with Rate = mm })

                        DoseLimit.FieldLabels.RateAdjust,
                        (fun s -> parseMinMaxNoValidation s false),
                        (fun (dl: DoseLimit) mm -> { dl with RateAdjust = mm })
                    ]

                // Process regex matches for labeled fields
                for m in matches do
                    let labelContent = m.Groups[1].Value // e.g., "qty-adj"
                    let valueStr = m.Groups[2].Value.Trim().TrimEnd(',').Trim()
                    let fullLabel = $"[{labelContent}]"

                    // Skip empty values - they represent default/unset fields
                    if valueStr |> String.IsNullOrWhiteSpace then
                        () // Skip this field
                    elif fullLabel = DoseLimit.FieldLabels.DoseUnit then
                        let dun = valueStr |> UnitsParse.fromString

                        match dun with
                        | Some un -> dl <- { dl with DoseUnit = un }
                        | None -> errors <- $"Unknown dose unit: {valueStr}" :: errors
                    else
                        let labelMatch =
                            fieldParsers |> List.tryFind (fun (label, _, _) -> label = fullLabel)

                        match labelMatch with
                        | Some(label, parser, setter) ->
                            match parser valueStr with
                            | Ok mm -> dl <- setter dl mm
                            | Error e -> errors <- $"{label}: {e}" :: errors
                        | None -> errors <- $"Unknown field label: {fullLabel}" :: errors

                // If no labeled matches and we have constraintsStr, return error requiring labels
                if matches.Count = 0 && not (constraintsStr |> String.IsNullOrWhiteSpace) then
                    errors <-
                        "DoseLimit fields must use labels like [qty], [qty-adj], [per-time], etc. Unlabeled input is not supported."
                        :: errors

                // Return result
                if errors.IsEmpty then
                    Ok(Some dl)
                else
                    Error(errors |> String.concat "; ")


        /// Parse SolutionLimit from formatted string using labeled fields
        /// Labels: [qty] for Quantity, [qty-adj] for QuantityAdj, [conc] for Concentration
        let parseSolutionLimitOpt (s: string) : Result<SolutionLimit option, string> =
            if s |> String.IsNullOrWhiteSpace then
                Ok None
            else
                // Match labeled fields: [label] value
                let labeledFieldRegex =
                    System.Text.RegularExpressions.Regex(@"\[([^\]]+)\]\s*([^[]*)")

                let matches = labeledFieldRegex.Matches(s)

                let mutable sl = SolutionLimit.limit
                let mutable errors = []

                for m in matches do
                    let label = m.Groups[1].Value.Trim().ToLowerInvariant()
                    let valueStr = m.Groups[2].Value.Trim()

                    if not (valueStr |> String.IsNullOrWhiteSpace) then
                        match label with
                        | "qts" ->
                            match parseValueUnitOpt valueStr with
                            | Ok vuOpt -> sl <- { sl with Quantities = vuOpt }
                            | Error e -> errors <- $"[qts]: {e}" :: errors
                        | "qty" ->
                            match parseMinMax valueStr with
                            | Ok mm -> sl <- { sl with Quantity = mm }
                            | Error e -> errors <- $"[qty]: {e}" :: errors
                        | "qty-adj" ->
                            match parseMinMax valueStr with
                            | Ok mm -> sl <- { sl with QuantityAdj = mm }
                            | Error e -> errors <- $"[qty-adj]: {e}" :: errors
                        | "conc" ->
                            match parseMinMax valueStr with
                            | Ok mm -> sl <- { sl with Concentration = mm }
                            | Error e -> errors <- $"[conc]: {e}" :: errors
                        | _ -> errors <- $"Unknown SolutionLimit label: [{label}]" :: errors

                // If no labeled matches and we have input, return error requiring labels
                if matches.Count = 0 then
                    errors <-
                        "SolutionLimit fields must use labels like [qty], [qty-adj], [conc]. Unlabeled input is not supported."
                        :: errors

                if errors.IsEmpty then
                    if sl = SolutionLimit.limit then Ok None else Ok(Some sl)
                else
                    Error(errors |> String.concat "; ")


        /// Parse SubstanceItem from field map
        let parseSubstanceItem (fields: Map<string, string>) : Result<SubstanceItem, string list> =
            let mutable errors = []

            let name = fields |> Map.tryFind "Name" |> Option.defaultValue ""

            let quantities =
                match fields |> Map.tryFind "Quantities" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseValueUnitOpt s with
                    | Ok vu -> vu
                    | Error e ->
                        errors <- e :: errors
                        None

            let concentrations =
                match fields |> Map.tryFind "Concentrations" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseValueUnitOpt s with
                    | Ok vu -> vu
                    | Error e ->
                        errors <- e :: errors
                        None

            let dose =
                match fields |> Map.tryFind "Dose" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseDoseLimitOpt SubstanceLimitTarget s with
                    | Ok dl -> dl
                    | Error e ->
                        errors <- e :: errors
                        None

            let solution =
                match fields |> Map.tryFind "Solution" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseSolutionLimitOpt s with
                    | Ok sl -> sl
                    | Error e ->
                        errors <- e :: errors
                        None

            if errors.IsEmpty then
                Ok
                    {
                        Name = name
                        Quantities = quantities
                        Concentrations = concentrations
                        Dose = dose
                        Solution = solution
                    }
            else
                Error errors


        /// Parse ProductComponent from field map and nested substances
        let parseProductComponent
            (fields: Map<string, string>)
            (substances: SubstanceItem list)
            : Result<ProductComponent, string list>
            =
            let mutable errors = []

            let name = fields |> Map.tryFind "Name" |> Option.defaultValue ""

            let form = fields |> Map.tryFind "Form" |> Option.defaultValue ""

            let quantities =
                match fields |> Map.tryFind "Quantities" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseValueUnitOpt s with
                    | Ok vu -> vu
                    | Error e ->
                        errors <- e :: errors
                        None

            let divisible = fields |> Map.tryFind "Divisible" |> Option.bind parseBigRationalOpt

            let dose =
                match fields |> Map.tryFind "Dose" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseDoseLimitOpt ComponentLimitTarget s with
                    | Ok dl -> dl
                    | Error e ->
                        errors <- e :: errors
                        None

            let solution =
                match fields |> Map.tryFind "Solution" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseSolutionLimitOpt s with
                    | Ok sl -> sl
                    | Error e ->
                        errors <- e :: errors
                        None

            if errors.IsEmpty then
                Ok
                    {
                        Name = name
                        Form = form
                        Quantities = quantities
                        Divisible = divisible
                        Dose = dose
                        Solution = solution
                        Substances = substances
                    }
            else
                Error errors


        /// Main parsing function
        let fromString (s: string) : Result<Medication, string list> =
            let lines =
                s.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
                |> Array.choose parseLine
                |> Array.toList

            // Separate top-level (indent 0), component (indent 1), substance (indent 2) fields
            let mutable topFields = Map.empty
            let mutable components = []
            let mutable currentComponentFields = Map.empty
            let mutable currentSubstances = []
            let mutable currentSubstanceFields = Map.empty

            let mutable parseErrors: string list = []

            let finishSubstance () =
                if not currentSubstanceFields.IsEmpty then
                    match parseSubstanceItem currentSubstanceFields with
                    | Ok si -> currentSubstances <- si :: currentSubstances
                    | Error errs ->
                        for e in errs do
                            parseErrors <- e :: parseErrors

                    currentSubstanceFields <- Map.empty

            let finishComponent () =
                finishSubstance ()

                if not currentComponentFields.IsEmpty then
                    match parseProductComponent currentComponentFields (currentSubstances |> List.rev) with
                    | Ok pc -> components <- pc :: components
                    | Error errs ->
                        for e in errs do
                            parseErrors <- e :: parseErrors

                    currentComponentFields <- Map.empty
                    currentSubstances <- []

            for indent, key, value in lines do
                match indent with
                | 0 ->
                    if key = "Components" then
                        () // Marker, no value
                    else
                        topFields <- topFields |> Map.add key value
                | 1 ->
                    if key = "Name" && currentComponentFields.ContainsKey "Name" then
                        // Starting a new component
                        finishComponent ()

                    if key = "Substances" then
                        () // Marker for substances section
                    else
                        currentComponentFields <- currentComponentFields |> Map.add key value
                | 2 ->
                    if key = "Name" && currentSubstanceFields.ContainsKey "Name" then
                        // Starting a new substance
                        finishSubstance ()

                    currentSubstanceFields <- currentSubstanceFields |> Map.add key value
                | _ -> ()

            // Finish the last component/substance
            finishComponent ()

            // Parse top-level fields
            let mutable errors = []

            let id = topFields |> Map.tryFind "Id" |> Option.defaultValue ""

            let name = topFields |> Map.tryFind "Name" |> Option.defaultValue ""

            let route = topFields |> Map.tryFind "Route" |> Option.defaultValue ""

            let orderType =
                match topFields |> Map.tryFind "OrderType" with
                | None -> AnyOrder
                | Some s ->
                    match parseOrderType s with
                    | Ok ot -> ot
                    | Error e ->
                        errors <- e :: errors
                        AnyOrder

            let quantity =
                match topFields |> Map.tryFind "Quantity" with
                | None
                | Some "" -> MinMax.empty
                | Some s ->
                    match parseMinMax s with
                    | Ok mm -> mm
                    | Error e ->
                        errors <- e :: errors
                        MinMax.empty

            let quantities =
                match topFields |> Map.tryFind "Quantities" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseValueUnitOpt s with
                    | Ok vu -> vu
                    | Error e ->
                        errors <- e :: errors
                        None

            let adjust =
                match topFields |> Map.tryFind "Adjust" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseValueUnitOpt s with
                    | Ok vu -> vu
                    | Error e ->
                        errors <- e :: errors
                        None

            let frequencies =
                match topFields |> Map.tryFind "Frequencies" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseValueUnitOpt s with
                    | Ok vu -> vu
                    | Error e ->
                        errors <- e :: errors
                        None

            let time =
                match topFields |> Map.tryFind "Time" with
                | None
                | Some "" -> MinMax.empty
                | Some s ->
                    match parseMinMax s with
                    | Ok mm -> mm
                    | Error e ->
                        errors <- e :: errors
                        MinMax.empty

            let dose =
                match topFields |> Map.tryFind "Dose" with
                | None
                | Some "" -> None
                | Some s ->
                    match parseDoseLimitOpt (fun _ -> OrderableLimitTarget) s with
                    | Ok dl -> dl
                    | Error e ->
                        errors <- e :: errors
                        None

            let div = topFields |> Map.tryFind "Div" |> Option.bind parseBigRationalOpt

            let doseCount =
                match topFields |> Map.tryFind "DoseCount" with
                | None
                | Some "" -> MinMax.empty
                | Some s ->
                    match parseMinMax s with
                    | Ok mm -> mm
                    | Error e ->
                        errors <- e :: errors
                        MinMax.empty

            // Combine all errors
            let allErrors = errors @ parseErrors

            if allErrors.IsEmpty then
                Ok
                    {
                        Id = id
                        Name = name
                        Components = components |> List.rev
                        Quantity = quantity
                        Quantities = quantities
                        Route = route
                        OrderType = orderType
                        Frequencies = frequencies
                        Time = time
                        Dose = dose
                        Div = div
                        DoseCount = doseCount
                        Adjust = adjust
                    }
            else
                Error allErrors


    module Limit = Limit


    let private tryHead m =
        Array.map m >> Array.tryHead >> Option.defaultValue ""


    let valueUnitOptToString =
        Option.map (ValueUnit.toStringDecimalDutchShortWithPrec 2)
        >> Option.defaultValue ""


    let minMaxToString (minMax: MinMax) =
        if minMax = MinMax.empty then
            ""
        else
            minMax |> Utils.MinMax.toString "min " "min " "max " "max "


    let limitOptToString =
        let toStr =
            DoseLimit.toString
            >> List.map String.trim
            >> List.filter (String.isNullOrWhiteSpace >> not)
            >> String.concat ", "

        Option.map toStr >> Option.defaultValue ""


    let solutionLimitOptToString =
        let toStr =
            SolutionLimit.toString
            >> List.map String.trim
            >> List.filter (String.isNullOrWhiteSpace >> not)
            >> String.concat ", "

        Option.map toStr >> Option.defaultValue ""


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


        let toString (subst: SubstanceItem) =
            [
                "Name", subst.Name
                "Concentrations", subst.Concentrations |> valueUnitOptToString
                "Dose", subst.Dose |> limitOptToString
                "Solution", subst.Solution |> solutionLimitOptToString
            ]


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
            }

        let toString (prodCmp: ProductComponent) =
            [
                "Name", prodCmp.Name
                "Form", prodCmp.Form
                "Quantities", prodCmp.Quantities |> valueUnitOptToString
                "Divisible", prodCmp.Divisible |> BigRational.optToString
                "Dose", prodCmp.Dose |> limitOptToString
                "Solution", prodCmp.Solution |> solutionLimitOptToString
                "Substances", ""
            ],
            prodCmp.Substances |> List.map SubstanceItem.toString


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


    let toString (med: Medication) =
        let emptyStr = ""

        let optToStr f opt =
            opt |> Option.map f |> Option.defaultValue emptyStr

        let mmToStr =
            MinMax.toString
                ValueUnit.toStringDecimalEngShortWithoutGroup
                ValueUnit.toStringDecimalEngShortWithoutGroup
                "min "
                "min "
                "max "
                "max "
        // Convert SolutionLimit to labeled string format
        let slToStr = SolutionLimit.toString >> String.concat " "
        let vuToStr = optToStr ValueUnit.toStringDecimalEngShortWithoutGroup

        [
            $"Id: %s{med.Id}"
            $"Name: %s{med.Name}"
            $"Quantity: %s{med.Quantity |> mmToStr}"
            $"Quantities: %s{med.Quantities |> vuToStr}"
            $"Route: %s{med.Route}"
            $"OrderType: {med.OrderType}"
            $"Adjust: %s{med.Adjust |> vuToStr}"
            $"Frequencies: %s{med.Frequencies |> vuToStr}"
            $"Time: %s{med.Time |> mmToStr}"
            $"Dose: %s{med.Dose |> limitOptToString}"
            $"Div: %s{med.Div |> optToStr BigRational.toString}"
            $"DoseCount: %s{med.DoseCount |> mmToStr}"
            "Components:"
            for cmp in med.Components do
                emptyStr
                $"\tName: %s{cmp.Name}"
                $"\tForm: %s{cmp.Form}"
                $"\tQuantities: %s{cmp.Quantities |> vuToStr}"
                $"\tDivisible: %s{cmp.Divisible |> optToStr BigRational.toString}"
                $"\tDose: %s{cmp.Dose |> limitOptToString}"
                $"\tSolution: %s{cmp.Solution |> optToStr slToStr}"
                $"\tSubstances:"

                for sub in cmp.Substances do
                    emptyStr
                    $"\t\tName: %s{sub.Name}"
                    $"\t\tQuantities: %s{sub.Quantities |> vuToStr}"
                    $"\t\tConcentrations: %s{sub.Concentrations |> vuToStr}"
                    $"\t\tDose: %s{sub.Dose |> limitOptToString}"
                    $"\t\tSolution: %s{sub.Solution |> optToStr slToStr}"
        ]


    let fromString = Parser.fromString


    let productComponent = ProductComponent.cmp


    let substanceItem = SubstanceItem.item


    /// Shorthand for UnitsParse.stringWithGroup to append the unit group to a unit.
    let unitGroup = UnitsParse.stringWithGroup


    /// <summary>
    /// Create a ProductComponent from a list of Products.
    /// DoseLimits are used to set the Dose for the ProductComponent.
    /// If noSubst is true, the substances will not be added to the ProductComponent.
    /// The freqUnit is used to set the TimeUnit for the Frequencies.
    /// </summary>
    /// <param name="limits">The ComponentLimits for the ProductComponent</param>
    let createComponents (limits: ComponentLimit[]) =

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
                        writeWarningMessage
                            $"{context}: tie between {winners.Length} unit groups of equal size, picking first"

                    let _, kept = winners[0]
                    let filtered = vus.Length - kept.Length

                    writeWarningMessage $"{context}: filtered {filtered} value(s) with incompatible unit group"

                    kept

        limits
        |> Array.map (fun lim ->
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
                Quantities =
                    let oneMl = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter

                    let qts =
                        lim.Products
                        |> Array.map _.FormQuantities
                        |> filterByUnitGroup $"Component '{lim.Name}' FormQuantities"
                        |> ValueUnit.collect

                    let isVol =
                        qts
                        |> Option.map (fun vu -> vu |> ValueUnit.eqsGroup oneMl)
                        |> Option.defaultValue false

                    let hasReconst = lim.Products |> Array.forall _.RequiresReconstitution

                    if isVol && hasReconst |> not then Some oneMl else qts
                Divisible = lim.Products |> Array.choose _.Divisible |> Array.tryHead
                Solution = None
                Dose = lim.Limit
                Substances =
                    lim.Products
                    |> Array.collect (fun p -> p.Substances |> Array.map (fun s -> (s, p)))
                    |> Array.groupBy (fst >> _.Name)
                    |> Array.map (fun (n, xs) ->
                        let dl =
                            lim.SubstanceLimits
                            |> Array.tryFind (fun l ->
                                match l.DoseLimitTarget with
                                | SubstanceLimitTarget s -> s |> String.equalsCapInsens n
                                | _ -> false
                            )

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
                                match dl with
                                | Some dl when dl.DoseUnit |> ValueUnit.Group.eqsGroup Units.Molar.mole ->
                                    xs
                                    |> Array.choose (fst >> _.MolarConcentration)
                                    |> Array.distinct
                                    |> filterByUnitGroup $"Component '{lim.Name}' Substance '{n}' MolarConcentrations"
                                    |> ValueUnit.collect
                                | _ ->
                                    xs
                                    |> Array.choose (fst >> _.Concentration)
                                    |> Array.distinct
                                    |> filterByUnitGroup $"Component '{lim.Name}' Substance '{n}' Concentrations"
                                    |> ValueUnit.collect
                            Dose = dl
                            Solution = None
                        }
                    )
                    |> Array.toList
            }
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
    let addSolution (pat: Patient) sr med =
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
                            |> createComponents
                            |> List.append ps
                        | None ->
                            writeWarningMessage "No diluents available"
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
    let create (pat: Patient) au dose (dr: DoseRule) (sr: SolutionRule option) =
        { template with
            Id = Guid.NewGuid().ToString()
            Name = dr.Generic |> Generic.toString
            Components = dr.ComponentLimits |> createComponents
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
        |> addSolution pat sr


    /// <summary>
    /// Create medication order templates from a PrescriptionRule
    /// </summary>
    /// <param name="logger">The logger instance for logging medication creation events</param>
    /// <param name="pr">The PrescriptionRule to use</param>
    let fromRule logger (pr: PrescriptionRule) =
        let au = pr.DoseRule.AdjustUnit |> Option.defaultValue Units.Weight.kiloGram

        let dose = pr.DoseRule.FormLimit

        let create = create pr.Patient au dose pr.DoseRule

        let meds =
            if pr.SolutionRules |> Array.isEmpty then
                [| create None |]
            else
                pr.SolutionRules |> Array.map Some |> Array.map create

        meds
        |> Array.iter (fun med ->
            // FLAG: expensive message build (Medication.toString formats every
            // substance/component incl. ValueUnits) — kept lazy so it is skipped
            // when the logger is off.
            Informedica.GenOrder.Lib.Logging.logInfoLazy
                logger
                (fun () ->
                    med
                    |> toString
                    |> List.map (sprintf "%s")
                    |> String.concat "\n"
                    |> Events.MedicationCreated
                )
        )

        meds


    module OrderDtoHelpers =

        let vuToDto = Option.bind (ValueUnit.Dto.toDto false ValueUnit.Dto.dutch)
        let limToDto = Option.map Limit.getValueUnit >> vuToDto

        /// Create the base Order DTO based on order type
        let createBaseOrderDto (med: Medication) =
            match med.OrderType with
            | AnyOrder ->
                raise (
                    System.NotSupportedException
                        "Not implemented for a medication order, the order type cannot be 'Any'"
                )
            | ProcessOrder ->
                raise (
                    System.NotSupportedException
                        "Not implemented for a medication order, the order type cannot be 'Process'"
                )
            | OnceOrder -> Order.Dto.once med.Id med.Name med.Route []
            | OnceTimedOrder -> Order.Dto.onceTimed med.Id med.Name med.Route []
            | ContinuousOrder -> Order.Dto.continuous med.Id med.Name med.Route []
            | DiscontinuousOrder -> Order.Dto.discontinuous med.Id med.Name med.Route []
            | TimedOrder -> Order.Dto.timed med.Id med.Name med.Route []


        let getOrderableUnit (med: Medication) =
            med.Components
            |> List.tryHead
            |> Option.bind (fun p -> p.Quantities |> Option.map ValueUnit.getUnit)


        /// Calculate divisibility increment for a component
        let calculateDivisibility (pc: ProductComponent option) (med: Medication) =
            let ou = med |> getOrderableUnit
            let pu = pc |> Option.bind (_.Quantities >> Option.map ValueUnit.getUnit)

            match ou, med.Div with
            | Some ou, Some br when Some ou = pu -> 1N / br |> createSingleValueUnitDto ou
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
                    incrs |> List.max |> createSingleValueUnitDto u
            | _ -> None

        /// Apply solution constraints to an item
        let setItemSolutionConstraints (itmDto: Order.Orderable.Item.Dto.Dto) (sl: SolutionLimit) =
            itmDto.OrderableQuantity.Constraints |> setMinMaxConstraints false sl.Quantity

            itmDto.OrderableConcentration.Constraints
            |> setMinMaxConstraints true sl.Concentration

        /// Set specific constraints for timed orders
        let setTimedOrderConstraints (med: Medication) (orbDto: Order.Orderable.Dto.Dto) =
            // Assume timed order always solution
            if orbDto.Dose.Quantity.Constraints.ValsOpt.IsNone then
                orbDto.Dose.Quantity.Constraints.IncrOpt <- med |> calculateDivisibility None

            if orbDto.OrderableQuantity.Constraints.ValsOpt.IsNone then
                orbDto.OrderableQuantity.Constraints.IncrOpt <- med |> calculateDivisibility None

        /// Set basic item-level constraints
        let setItemQtyConcConstraints (itmDto: Order.Orderable.Item.Dto.Dto) (med: Medication) (si: SubstanceItem) =
            itmDto.ComponentConcentration.Constraints.ValsOpt <- si.Concentrations |> vuToDto
            itmDto.ComponentQuantity.Constraints.ValsOpt <- si.Quantities |> vuToDto

            // Handle single component case
            if med.Components |> List.length = 1 then
                itmDto.OrderableConcentration.Constraints.ValsOpt <- itmDto.ComponentConcentration.Constraints.ValsOpt

            // Apply solution constraints if present
            si.Solution |> Option.iter (setItemSolutionConstraints itmDto)

        /// Set item dose constraints based on order type
        let setItemDoseConstraints (itmDto: Order.Orderable.Item.Dto.Dto) (med: Medication) (si: SubstanceItem) =
            let setDoseRate (dl: DoseLimit) =
                itmDto.Dose.Rate.Constraints |> setMinMaxConstraints false dl.Rate
                itmDto.Dose.RateAdjust.Constraints |> setMinMaxConstraints true dl.RateAdjust

            let setDoseQty (dl: DoseLimit) =
                let zero = 0N |> createSingleValueUnitDto dl.DoseUnit

                if dl.Quantity |> MinMax.isEmpty then
                    itmDto.Dose.Quantity.Constraints.MinOpt <- zero
                else
                    itmDto.Dose.Quantity.Constraints |> setMinMaxConstraints false dl.Quantity

                itmDto.Dose.QuantityAdjust.Constraints
                |> setMinMaxConstraints true dl.QuantityAdjust

                itmDto.Dose.PerTime.Constraints |> setMinMaxConstraints false dl.PerTime

                itmDto.Dose.PerTimeAdjust.Constraints
                |> setMinMaxConstraints true dl.PerTimeAdjust

            match med.OrderType with
            | AnyOrder
            | ProcessOrder -> ()
            | ContinuousOrder -> si.Dose |> Option.iter setDoseRate
            | OnceOrder
            | DiscontinuousOrder -> si.Dose |> Option.iter setDoseQty
            | OnceTimedOrder
            | TimedOrder ->
                si.Dose
                |> Option.iter (fun dl ->
                    setDoseRate dl
                    setDoseQty dl
                )

        /// Create a single item DTO with all its constraints
        let createSingleItemDto (med: Medication) (pc: ProductComponent) (si: SubstanceItem) =
            let itmDto = Order.Orderable.Item.Dto.dto med.Id med.Name pc.Name si.Name

            // Set basic item constraints
            setItemQtyConcConstraints itmDto med si

            // Set item dose constraints based on order type
            setItemDoseConstraints itmDto med si

            itmDto

        /// Create item DTOs for a component
        let createItemDtos (med: Medication) (p: ProductComponent) =
            [ for s in p.Substances -> createSingleItemDto med p s ]

        /// Set basic component-level constraints
        let setComponentQtyConcConstraints
            (med: Medication)
            (pc: ProductComponent)
            (cmpDto: Order.Orderable.Component.Dto.Dto)
            =
            let incr = med |> calculateDivisibility (Some pc)

            cmpDto.OrderableConcentration.Constraints.MaxOpt <-
                Units.Count.times |> ValueUnit.singleWithValue 1N |> Some |> vuToDto

            cmpDto.OrderableConcentration.Constraints.MaxIncl <- med.Components |> List.length = 1

            cmpDto.ComponentQuantity.Constraints.ValsOpt <- pc.Quantities |> vuToDto
            cmpDto.OrderableQuantity.Constraints.IncrOpt <- incr

            match pc.Solution with
            | None -> ()
            | Some sol ->
                cmpDto.OrderableQuantity.Constraints.ValsOpt <- sol.Quantities |> vuToDto
                cmpDto.OrderableQuantity.Constraints |> setMinMaxConstraints false sol.Quantity

            // Handle single component case
            if med.Components |> List.length = 1 then
                cmpDto.OrderableConcentration.Constraints.ValsOpt <- 1N |> createSingleValueUnitDto Units.Count.times
                cmpDto.Dose.Quantity.Constraints.IncrOpt <- incr

        /// Set component dose constraints based on order type
        let setComponentDoseConstraints
            (cmpDto: Order.Orderable.Component.Dto.Dto)
            (med: Medication)
            (pc: ProductComponent)
            =
            let zero =
                pc.Quantities
                |> Option.map ValueUnit.getUnit
                |> Option.bind (fun u -> 0N |> createSingleValueUnitDto u)

            let setDoseRate (dl: DoseLimit) =
                if dl.Rate |> MinMax.isEmpty |> not then
                    cmpDto.Dose.Rate.Constraints |> setMinMaxConstraints false dl.Rate

                if dl.RateAdjust |> MinMax.isEmpty |> not then
                    cmpDto.Dose.RateAdjust.Constraints |> setMinMaxConstraints true dl.RateAdjust

            let setDoseQty (dl: DoseLimit) =
                if dl.Quantity |> MinMax.isEmpty |> not then
                    cmpDto.Dose.Quantity.Constraints |> setMinMaxConstraints false dl.Quantity
                else
                    // dose quantities can only add up with the same unit
                    // so this makes sure a dose quantity has a unit and
                    // can be included in to the addition equation
                    cmpDto.Dose.Quantity.Constraints.MinOpt <- zero

                if dl.QuantityAdjust |> MinMax.isEmpty |> not then
                    cmpDto.Dose.QuantityAdjust.Constraints
                    |> setMinMaxConstraints true dl.QuantityAdjust

                if dl.PerTime |> MinMax.isEmpty |> not then
                    cmpDto.Dose.PerTime.Constraints |> setMinMaxConstraints false dl.PerTime

                if dl.PerTimeAdjust |> MinMax.isEmpty |> not then
                    cmpDto.Dose.PerTimeAdjust.Constraints
                    |> setMinMaxConstraints true dl.PerTimeAdjust

            match med.OrderType with
            | AnyOrder
            | ProcessOrder -> ()
            | ContinuousOrder -> pc.Dose |> Option.iter setDoseRate
            | OnceOrder
            | DiscontinuousOrder -> pc.Dose |> Option.iter setDoseQty
            | OnceTimedOrder
            | TimedOrder ->
                pc.Dose
                |> Option.iter (fun dl ->
                    setDoseRate dl
                    setDoseQty dl
                )

        /// Create a single component DTO with all its constraints and items
        let createSingleComponentDto (med: Medication) (pc: ProductComponent) =
            let cmpDto = Order.Orderable.Component.Dto.dto med.Id med.Name pc.Name pc.Form

            // Set basic component constraints
            cmpDto |> setComponentQtyConcConstraints med pc

            // Set component dose constraints based on order type
            setComponentDoseConstraints cmpDto med pc

            // Create and set item DTOs
            cmpDto.Items <- createItemDtos med pc

            cmpDto

        /// Create component DTOs from medication order template components
        let createComponentDtos (med: Medication) =
            [
                for pc in med.Components -> createSingleComponentDto med pc
            ]

        /// Set basic orderable-level constraints
        let setOrderableConstraints (orbDto: Order.Orderable.Dto.Dto) (med: Medication) =
            let zero =
                med.Components
                |> List.tryHead
                |> Option.bind (fun p ->
                    p.Quantities
                    |> Option.map ValueUnit.getUnit
                    |> Option.bind (fun u -> 0N |> createSingleValueUnitDto u)
                )

            orbDto.DoseCount.Constraints |> setMinMaxConstraints false med.DoseCount

            orbDto.OrderableQuantity.Constraints |> setMinMaxConstraints false med.Quantity

            match med.Quantities with
            | None ->
                orbDto.OrderableQuantity.Constraints.MinOpt <- zero
                orbDto.OrderableQuantity.Constraints.MinIncl <- false

            | Some _ -> orbDto.OrderableQuantity.Constraints.ValsOpt <- med.Quantities |> vuToDto

        /// Set dose-constraints on orderable based on order-type
        let setOrderableDoseConstraints (orbDto: Order.Orderable.Dto.Dto) (med: Medication) =
            let orderableUnit =
                med.Components
                |> List.tryHead
                |> Option.bind (fun p -> p.Quantities |> Option.map ValueUnit.getUnit)

            let rateUnit = orderableUnit |> Option.map (ValueUnit.per Units.Time.hour)

            let freqTimeUnit =
                med.Frequencies
                |> Option.map (ValueUnit.getUnit >> ValueUnit.getUnits)
                |> function
                    | Some [ _; tu ] -> Some tu
                    | _ -> None

            let incr = med |> calculateDivisibility None

            // orderable quantity increment defaults to smallest product component increment (based on component divisibility)
            orbDto.OrderableQuantity.Constraints.IncrOpt <- incr

            let setOrbDoseRate (dl: DoseLimit option) =

                match rateUnit with
                | None -> ()
                | Some ru ->
                    // increment defaults to 0.1
                    orbDto.Dose.Rate.Constraints.IncrOpt <- [| 1N / 10N |] |> createValueUnitDto ru

                match dl with
                | None -> ()
                | Some dl ->
                    orbDto.Dose.Rate.Constraints |> setMinMaxConstraints false dl.Rate
                    orbDto.Dose.RateAdjust.Constraints |> setMinMaxConstraints false dl.RateAdjust

            let setOrbDoseQty isOnce (dl: DoseLimit option) =
                // set a default increment based on the smallest product component increment
                orbDto.Dose.Quantity.Constraints.IncrOpt <- incr

                match dl with
                | None ->
                    match orderableUnit with
                    | Some u ->
                        orbDto.Dose.Quantity.Constraints.MinOpt <- 0N |> createSingleValueUnitDto u
                        orbDto.Dose.Quantity.Constraints.MinIncl <- false
                    | None -> ()

                    match orderableUnit, freqTimeUnit with
                    | Some u, Some tu ->
                        orbDto.Dose.PerTime.Constraints.MinOpt <- 0N |> createSingleValueUnitDto (u |> ValueUnit.per tu)
                        orbDto.Dose.PerTime.Constraints.MinIncl <- false
                    | _ -> ()

                | Some dl ->
                    orbDto.Dose.Quantity.Constraints |> setMinMaxConstraints false dl.Quantity

                    orbDto.Dose.QuantityAdjust.Constraints
                    |> setMinMaxConstraints true dl.QuantityAdjust

                    // make sure that orderable dose quantity has constraints with a unit
                    if dl.Quantity |> MinMax.isEmpty then
                        match orderableUnit with
                        | Some u ->
                            orbDto.Dose.Quantity.Constraints.MinOpt <- 0N |> createSingleValueUnitDto u
                            orbDto.Dose.Quantity.Constraints.MinIncl <- false
                        | None -> ()

                    if not isOnce then
                        orbDto.Dose.PerTime.Constraints |> setMinMaxConstraints false dl.PerTime
                        // make sure that orderable dose per time has constraints with a unit
                        if dl.PerTime |> MinMax.isEmpty then
                            match orderableUnit, freqTimeUnit with
                            | Some u, Some tu ->
                                orbDto.Dose.PerTime.Constraints.MinOpt <-
                                    0N |> createSingleValueUnitDto (u |> ValueUnit.per tu)

                                orbDto.Dose.PerTime.Constraints.MinIncl <- false
                            | _ -> ()

                        orbDto.Dose.PerTimeAdjust.Constraints
                        |> setMinMaxConstraints true dl.PerTimeAdjust

            // when there is exactly one component and no orderable-level dose,
            // use the single component's dose as the orderable dose
            let dose =
                match med.Dose, med.Components with
                | None, [ single ] -> single.Dose
                | _ -> med.Dose

            match med.OrderType with
            | AnyOrder
            | ProcessOrder -> ()
            | ContinuousOrder -> dose |> setOrbDoseRate
            | OnceOrder -> dose |> setOrbDoseQty true
            | OnceTimedOrder ->
                dose |> setOrbDoseRate
                dose |> setOrbDoseQty true
            | DiscontinuousOrder -> dose |> setOrbDoseQty false
            | TimedOrder ->
                orbDto |> setTimedOrderConstraints med
                dose |> setOrbDoseRate
                dose |> setOrbDoseQty false

        /// Create and configure the Orderable DTO with all constraints
        let createOrderableDto (med: Medication) =
            let orbDto = Order.Orderable.Dto.dto med.Id med.Name

            // Set basic orderable constraints
            setOrderableConstraints orbDto med

            // Set dose-constraints based on order-type
            setOrderableDoseConstraints orbDto med

            // Create and set component DTOs
            orbDto.Components <- createComponentDtos med

            orbDto

        /// Set prescription-level constraints (frequency and time)
        let setPrescriptionConstraints (dto: Order.Dto.Dto) (med: Medication) =
            dto.Schedule.Frequency.Constraints.ValsOpt <- med.Frequencies |> vuToDto

            match med.Frequencies with
            | None -> ()
            | Some fu -> // frequency increment always defaults to 1
                let freqUnit = fu |> ValueUnit.getUnit
                let incr = 1N |> createSingleValueUnitDto freqUnit
                dto.Schedule.Frequency.Constraints.IncrOpt <- incr

            dto.Schedule.Time.Constraints.MinIncl <- med.Time.Min.IsSome
            // fix: do not overwrite non zero min
            if med.Time.Min.IsSome then
                dto.Schedule.Time.Constraints.MinOpt <- med.Time.Min |> limToDto

            dto.Schedule.Time.Constraints.MaxIncl <- med.Time.Max.IsSome
            dto.Schedule.Time.Constraints.MaxOpt <- med.Time.Max |> limToDto

        /// Set patient adjustment constraints (weight/BSA based)
        let setAdjustmentConstraints (dto: Order.Dto.Dto) (med: Medication) =
            match med.Adjust with
            | None -> ()
            | Some vu ->
                let adjustUnit = vu |> ValueUnit.getUnit

                // Handle weight-based adjustment
                if adjustUnit |> ValueUnit.Group.eqsGroup Units.Weight.kiloGram then
                    dto.Adjust.Constraints.MinOpt <- 200N / 1000N |> createSingleValueUnitDto adjustUnit
                    dto.Adjust.Constraints.MaxOpt <- 150N |> createSingleValueUnitDto adjustUnit

            // TODO: add constraints for BSA
            dto.Adjust.Constraints.ValsOpt <- med.Adjust |> vuToDto


    /// <summary>
    /// Convert a Medication order to an Order DTO for the solver system
    /// </summary>
    /// <param name="med">The Medication order to convert</param>
    let toOrderDto (med: Medication) =
        // Create the base DTO structure
        let dto = OrderDtoHelpers.createBaseOrderDto med

        // Set up the orderable with all its constraints
        let orbDto = OrderDtoHelpers.createOrderableDto med
        dto.Orderable <- orbDto

        // Apply prescription constraints
        OrderDtoHelpers.setPrescriptionConstraints dto med

        // Apply patient adjustment constraints
        OrderDtoHelpers.setAdjustmentConstraints dto med

        dto
