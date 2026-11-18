namespace Informedica.GenOrder.Lib


/// Helper functions for the ValueUnit type of Informedica.GenUnits.Lib.
module ValueUnit =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open ValueUnit

    /// <summary>
    /// Return a Unit as a short Dutch string
    /// </summary>
    let unitToString = Units.toString None None false Units.Dutch Units.Short


    /// <summary>
    /// Check if a Unit is an adjust unit, i.e.,
    /// kg or m2.
    /// </summary>
    /// <param name="u">The Unit</param>
    let isAdjust (u: Unit) =
        u |> Group.eqsGroup Units.Weight.kiloGram || u |> Group.eqsGroup Units.BSA.m2


    /// <summary>
    /// Put an adjust unit in the middle of a combined unit.
    /// </summary>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// let u1 = Units.Mass.milliGram
    /// let u2 = Units.Weight.kiloGram
    /// let u3 = Units.Time.day
    /// // mg/day/kg
    /// let vu1 = withValue [|1N|] (u1 |> per u3 |> per u2)
    /// // = mg/kg/day
    /// let vu2 = vu1 |> correctAdjustOrder
    /// // check
    /// vu1 |> eqsGroup vu2
    /// </code>
    /// </example>
    let correctAdjustOrder vu =
        let v, u = vu |> get

        match u |> getUnits with
        | [ u1; u2; u3 ] when u3 |> isAdjust -> u1 |> per u3 |> per u2 |> withValue v
        | _ -> vu


    /// <summary>
    /// Collects the values of an array of ValueUnits into one ValueUnit in the unit of the first.
    /// </summary>
    /// <param name="vus">The array of ValueUnits</param>
    /// <returns>The ValueUnit, or None when the array or the collected values are empty</returns>
    /// <exception cref="System.ArgumentException">When the units are not all of the same group.</exception>
    let collect (vus: ValueUnit[]) =
        match vus |> Array.tryHead with
        | None -> None
        | Some vu ->
            let u = vu |> getUnit

            if vus |> Array.forall (getUnit >> Group.eqsGroup u) |> not then
                let details =
                    vus
                    |> Array.map (fun vu ->
                        let v = vu |> getValue |> Array.map string |> String.concat ","
                        let u = vu |> getUnit |> unitToString
                        $"{v} {u}"
                    )
                    |> Array.distinct
                    |> String.concat "; "

                invalidArg (nameof vus) $"Cannot collect the ValueUnit array to a single ValueUnit, values: [{details}]"

            let result = vus |> Array.collect (toBase >> getValue) |> withUnit u |> toUnit

            if result |> isEmpty then None else result |> Some


    /// Collects the values of a list of value units into one value unit in the unit of the first,
    /// sorted and without duplicates. Raises an ArgumentException when there is no value.
    let collectValues vus =
        match vus |> List.toArray |> collect with
        | Some vu -> vu |> applyToValue (Array.distinct >> Array.sort)
        | None -> invalidArg (nameof vus) "no value to collect"


    /// The value 1, for a count.
    let oneCount = Units.Count.times |> one


    module Units =

        let noUnit = NoUnit


module Variable =


    /// Helper functions to facilitate the use of the
    /// Informedica.GenSolver.Lib.Variable.ValueRange type
    module ValueRange =

        open Informedica.GenUnits.Lib
        open Informedica.GenSolver.Lib.Variable.ValueRange
        open Informedica.Utils.Lib


        let inline private setOpt aOption set vr =
            try
                match aOption with
                | Some m -> vr |> set m
                | None -> vr
            with _ ->
                vr // TODO: an exception from set returns the range unchanged; replace with a Result


        /// <summary>
        /// Sets an optional minimum value for the value range
        /// </summary>
        /// <param name="min">The optional Minimum</param>
        /// <param name="vr">The ValueRange</param>
        let setOptMin min vr = vr |> setOpt min setMin


        /// <summary>
        /// Sets an optional maximum value for the value range
        /// </summary>
        /// <param name="max">The optional Maximum</param>
        /// <param name="vr">The ValueRange</param>
        let setOptMax max vr = vr |> setOpt max setMax


        /// <summary>
        /// Sets an optional increment value for the value range
        /// </summary>
        /// <param name="incr">The optional Increment</param>
        /// <param name="vr">The ValueRange</param>
        let setOptIncr incr vr = vr |> setOpt incr setIncr


        /// <summary>
        /// Sets an optional value set for the value range
        /// </summary>
        /// <param name="vs">The optional ValueSet</param>
        /// <param name="vr">The ValueRange</param>
        let setOptVs vs vr = vr |> setOpt vs setValueSet


        /// Returns the union of value ranges. When every range is a value set, or a min, increment and max
        /// with fewer than maxCount values, the union is one value set. Otherwise the union runs from the
        /// lowest minimum to the highest maximum with the increments of all ranges; it has no minimum,
        /// maximum or increment when one of the ranges has none.
        let unionRanges maxCount (vrs: Informedica.GenSolver.Lib.Types.ValueRange list) =
            match vrs |> List.distinct with
            | [ vr ] -> vr
            | vrs ->
                match vrs |> List.map (tryToValueSet maxCount) |> List.sequenceOption with
                | Some vss ->
                    vss
                    |> List.map ValueSet.toValueUnit
                    |> ValueUnit.collectValues
                    |> ValueSet.create
                    |> Informedica.GenSolver.Lib.Types.ValSet
                | None ->
                    let all f = vrs |> List.map f |> List.sequenceOption

                    let min =
                        all getMin
                        |> Option.map (List.reduce (fun m1 m2 -> if m2 |> Minimum.minSTmin m1 then m2 else m1))

                    let max =
                        all getMax
                        |> Option.map (List.reduce (fun m1 m2 -> if m2 |> Maximum.maxGTmax m1 then m2 else m1))

                    let incr =
                        all getIncr
                        |> Option.map (
                            List.map Increment.toValueUnit
                            >> ValueUnit.collectValues
                            >> ValueUnit.removeBigRationalMultiples
                            >> Increment.create
                        )

                    create min incr max None


    open Informedica.GenSolver.Lib
    module ValueSet = Variable.ValueRange.ValueSet


    /// <summary>
    /// Replace the Values of a Variable with a ValueSet
    /// </summary>
    /// <param name="vu">The ValueUnit to set as a ValueSet</param>
    /// <param name="var">The Variable to set the ValueSet to</param>
    let replaceValuesWithValueUnit vu (var: Variable) = { var with Values = vu |> ValueSet.create |> ValSet }


/// Functions that deal with the OrderVariable type
module OrderVariable =


    open Informedica.Utils.Lib.BCL

    open Informedica.Utils.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenSolver.Lib
    open Informedica.GenUnits.Lib


    module ValueRange = Variable.ValueRange
    module Minimum = ValueRange.Minimum
    module Maximum = ValueRange.Maximum
    module Increment = ValueRange.Increment
    module ValueSet = ValueRange.ValueSet

    module VariableDto = Variable.Dto
    module Units = ValueUnit.Units
    module Multipliers = ValueUnit.Multipliers


    module Constraints =


        /// <summary>
        /// Create a <c>Constraints</c> record
        /// </summary>
        /// <param name="min">An optional Minimum</param>
        /// <param name="incr">An optional Increment</param>
        /// <param name="max">An optional Maximum</param>
        /// <param name="vs">An optional ValueSet</param>
        let create min incr max vs =
            {
                Min = min
                Max = max
                Incr = incr
                Values = vs
            }


        /// Get the fields of a Constraints record
        let get (cs: Constraints) = cs.Min, cs.Incr, cs.Max, cs.Values


        /// Sets the values the order variable may take. None or an empty ValueUnit removes the values.
        let setValues vu (cs: Constraints) =
            { cs with Values = vu |> Option.filter (ValueUnit.isEmpty >> not) |> Option.map ValueSet.create }


        /// Sets the increment between the values the order variable may take. None or an empty ValueUnit
        /// removes the increment.
        let setIncr vu (cs: Constraints) =
            { cs with Incr = vu |> Option.filter (ValueUnit.isEmpty >> not) |> Option.map Increment.create }


        /// Sets the minimum, inclusive when incl is true. None removes the minimum.
        let setMin incl vu (cs: Constraints) = { cs with Min = vu |> Option.map (Minimum.create incl) }


        /// Sets the maximum, inclusive when incl is true. None removes the maximum.
        let setMax incl vu (cs: Constraints) = { cs with Max = vu |> Option.map (Maximum.create incl) }


        /// <summary>
        /// Sets the minimum and maximum of a MinMax as inclusive bounds. A bound the MinMax does not have
        /// is left unchanged.
        /// </summary>
        /// <param name="calcNormDose">
        /// When true, a MinMax with an equal minimum and maximum is a norm dose: the minimum becomes 0.9
        /// and the maximum 1.1 times that value, so the solver has a range to work with
        /// </param>
        /// <param name="minMax">The MinMax to take the bounds from</param>
        /// <param name="cs">The Constraints to set the bounds on</param>
        let setMinMax calcNormDose (minMax: MinMax) (cs: Constraints) =
            let isNormDose =
                calcNormDose
                && (
                    match minMax.Min, minMax.Max with
                    | Some minLimit, Some maxLimit -> minLimit |> Limit.eq maxLimit
                    | _ -> false
                )

            let bound f =
                Option.map Limit.getValueUnit
                >> Option.map (fun vu ->
                    if isNormDose then
                        vu * (f |> ValueUnit.singleWithUnit Units.Count.times)
                    else
                        vu
                )

            let min = minMax.Min |> bound (90N / 100N)
            let max = minMax.Max |> bound (11N / 10N)

            cs
            |> fun cs -> if min |> Option.isSome then cs |> setMin true min else cs
            |> fun cs -> if max |> Option.isSome then cs |> setMax true max else cs


        /// Checks whether a Constraints record has only an exclusive minimum of zero.
        let isNonZeroPositive (cs: Constraints) =
            cs.Max.IsNone
            && cs.Incr.IsNone
            && cs.Values.IsNone
            && cs.Min |> Option.map Minimum.isNonZeroPositive |> Option.defaultValue false


        /// Check whether a Constraints record is empty
        let isEmpty (cs: Constraints) =
            cs.Min.IsNone && cs.Incr.IsNone && cs.Max.IsNone && cs.Values.IsNone


        /// Returns the minimum times 1.1 when that equals the maximum times 0.9, as for a norm dose set by
        /// setMinMax; otherwise None.
        let getNorm (cs: Constraints) =
            let times0_90 = 90N / 100N |> ValueUnit.singleWithUnit Units.Count.times
            let times1_10 = 11N / 10N |> ValueUnit.singleWithUnit Units.Count.times

            match cs.Min |> Option.map Minimum.toValueUnit, cs.Max |> Option.map Maximum.toValueUnit with
            | Some minVu, Some maxVu ->
                if minVu * times1_10 = maxVu * times0_90 then
                    minVu * times1_10 |> Some
                else
                    None
            | _ -> None


        /// Creates a ValueRange from only the Increment and the Maximum of a Constraints record.
        let toIncrMaxRange (cs: Constraints) =
            ValueRange.unrestricted
            |> ValueRange.setOptMax cs.Max
            |> ValueRange.setOptIncr cs.Incr


        /// Creates a ValueRange from only the Minimum and the Increment of a Constraints record.
        let toMinIncrRange (cs: Constraints) =
            ValueRange.unrestricted
            |> ValueRange.setOptMin cs.Min
            |> ValueRange.setOptIncr cs.Incr


        /// Creates a ValueRange from a Constraints record. A ValueSet, when present, replaces the minimum,
        /// maximum and increment.
        let toValueRange (cs: Constraints) =
            match cs.Values with
            | Some _ -> ValueRange.unrestricted |> ValueRange.setOptVs cs.Values
            | None ->
                ValueRange.unrestricted
                |> ValueRange.setOptVs cs.Values
                |> ValueRange.setOptMin cs.Min
                |> ValueRange.setOptMax cs.Max
                |> ValueRange.setOptIncr cs.Incr


        let fromValueRange =
            function
            | Unrestricted -> create None None None None
            | NonZeroPositive ->
                let zero = ZeroUnit |> ValueUnit.zero |> ValueRange.Minimum.create false |> Some
                create zero None None None
            | vr ->
                let min, incr, max, vs = vr |> ValueRange.getMinIncrMaxOrValueSet

                {
                    Min = min
                    Incr = incr
                    Max = max
                    Values = vs
                }


        /// Get the string representation of a ValueRange from a Constraints record
        let toValueRangeString = toValueRange >> ValueRange.toString false


        /// <summary>
        /// Get the string representation of a <c>Constraints</c> record (in Dutch)
        /// </summary>
        /// <param name="cs">The Constraints record</param>
        /// <remarks>
        /// <code>
        /// let min = 1N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Minimum.create true
        /// let max = 10N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Maximum.create false
        /// let incr = 1N |> ValueUnit.singleWithUnit Units.Mass.milliGram |> Increment.create
        /// let cs = Constraints.create (Some min) (Some incr) (Some max) None
        /// cs |> toString
        /// // vanaf 1 mg[Mass] per 1 mg[Mass] tot 10 mg[Mass]
        /// </code>
        /// </remarks>
        let toString (cs: Constraints) =
            let toStr = ValueUnit.toStringDutchShort

            match cs.Values with
            | None ->
                let min = cs.Min |> Option.map Minimum.toBoolValueUnit
                let max = cs.Max |> Option.map Maximum.toBoolValueUnit

                MinMax.Calculator.toStringNL toStr min max
            | Some vs -> vs |> ValueSet.toValueUnit |> ValueUnit.toStringDutchShort


        /// <summary>
        /// Formats the minimum and maximum of a Constraints record as a Dutch string: one value when they
        /// are equal or a norm dose, else "min - max". Returns an empty string when a bound is missing.
        /// </summary>
        /// <param name="prec">The precision for decimal values</param>
        /// <param name="cs">The Constraints record</param>
        let toMinMaxString prec (cs: Constraints) =
            let toStr = ValueUnit.toStringDecimalDutchShortWithPrec prec

            let toVal =
                ValueUnit.getValue
                >> Array.tryHead
                >> Option.map BigRational.toDecimal
                >> Option.map (Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3)
                >> Option.defaultValue ""

            let times0_90 = (90N / 100N) |> ValueUnit.singleWithUnit Units.Count.times
            let times1_10 = (11N / 10N) |> ValueUnit.singleWithUnit Units.Count.times

            match cs.Min |> Option.map Minimum.toValueUnit, cs.Max |> Option.map Maximum.toValueUnit with
            | Some min, Some max ->
                if min |> ValueUnit.eqs max then
                    $"{min |> toStr}"
                else if min / times0_90 = max / times1_10 then
                    $"{min / times0_90 |> toStr}"
                else
                    $"{min |> toVal} - {max |> toStr}"
            | _ -> ""


        /// Formats the maximum of a Constraints record as a short Dutch decimal string with precision prec;
        /// None when there is no maximum.
        let toMaxString prec (cs: Constraints) =
            cs.Max
            |> Option.map (Maximum.toValueUnit >> ValueUnit.toStringDecimalDutchShortWithPrec prec)


        /// <summary>
        /// Map the functions, fMin, fMax, fIncr and fVals over the <c>Constraints</c> record
        /// </summary>
        /// <param name="fMin">The function to map the Min</param>
        /// <param name="fMax">The function to map the Max</param>
        /// <param name="fIncr">The function to map the Incr</param>
        /// <param name="fVals">The function to map the Values</param>
        /// <param name="cs">The Constraints record</param>
        let map fMin fMax fIncr fVals (cs: Constraints) =
            { cs with
                Min = cs.Min |> Option.map fMin
                Max = cs.Max |> Option.map fMax
                Incr = cs.Incr |> Option.map fIncr
                Values = cs.Values |> Option.map fVals
            }


        /// Get the Unit from a Constraints record
        let getUnit (cs: Constraints) = cs |> toValueRange |> ValueRange.getUnit


    /// <summary>
    /// Creates an OrderVariable: a Variable with defined and calculated Constraints.
    /// </summary>
    /// <param name="n">The Name of the Variable</param>
    /// <param name="min">An optional Minimum of the Variable</param>
    /// <param name="incr">An optional Increment of the Variable</param>
    /// <param name="max">An optional Maximum of the Variable</param>
    /// <param name="vs">An optional ValueSet of the Variable</param>
    /// <param name="cst">The Constraints for the OrderVariable</param>
    /// <param name="cal">The calculated constraints</param>
    let create n min incr max vs cst cal =
        ValueRange.create min incr max vs
        |> fun vlr ->
            let var = Variable.create id n vlr

            {
                DefinedConstraints = cst
                CalculatedConstraints = cal
                Variable = var
            }


    /// <summary>
    /// Create a new OrderVariable. The OrderVariable will have an exclusive
    /// Minimum of zero and a Constraints with an exclusive Minimum of zero
    /// </summary>
    /// <param name="n">The Name of the Variable</param>
    /// <param name="un">The Unit of the Values in the Variable and Constraints</param>
    let createNew n un =
        let vu = 0N |> ValueUnit.createSingle un
        let min = Minimum.create false vu |> Some

        let cs = Constraints.create min None None None
        create n min None None None cs cs


    /// <summary>
    /// Map a function f to the Values (i.e., ValueUnit) of the Variable
    /// of the OrderVariable
    /// </summary>
    /// <param name="f">The function to map</param>
    /// <param name="ovar">The OrderVariable</param>
    let map f (ovar: OrderVariable) =
        { ovar with
            OrderVariable.Variable.Values = ovar.Variable.Values |> ValueRange.mapValueUnit f

        }


    /// Get the Variable from an OrderVariable
    let getVar { Variable = var } = var


    /// Set the Variable of an OrderVariable
    let setVar var (ovar: OrderVariable) = { ovar with Variable = var }


    /// Get the optional ValueUnit from the ValueSet of the Variable
    let getValSetValueUnit ovar =
        ovar
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.getValSet
        |> Option.map ValueSet.toValueUnit


    /// Checks whether two OrderVariables have units of the same group, so that their values can be added
    /// or subtracted.
    let eqsUnitGroup ovar1 ovar2 =
        let var1, var2 = ovar1 |> getVar, ovar2 |> getVar
        var1 |> Variable.eqsUnitGroup var2


    /// Get the Variable.Name from an OrderVariable
    let getName ovar = (ovar |> getVar).Name


    /// Check whether two OrderVariables have the same name
    let eqsName ovar1 ovar2 = (ovar1 |> getName) = (ovar2 |> getName)


    /// Get the names of a list of OrderVariables as a comma-separated string
    let getNames ovars =
        ovars
        |> List.map _.Variable
        |> List.map _.Name
        |> List.map Name.toString
        |> String.concat ", "


    /// Get the defined Constraints from an OrderVariable
    let getConstraints { DefinedConstraints = cons } = cons


    /// Checks whether the defined Constraints of an OrderVariable are not empty and not only an exclusive
    /// minimum of zero.
    let hasConstraints =
        getConstraints
        >> fun cs -> cs |> Constraints.isNonZeroPositive |> not && cs |> Constraints.isEmpty |> not


    /// Check whether the defined Constraints of an OrderVariable have a Maximum or a ValueSet
    let hasMaxConstraint ovar =
        ovar
        |> getConstraints
        |> Constraints.get
        |> function
            | _, _, Some _, _
            | _, _, _, Some _ -> true
            | _ -> false


    let getNormValue ovar = ovar |> getConstraints |> Constraints.getNorm


    /// <summary>
    /// Set the defined <c>Constraints</c> of an OrderVariable
    /// </summary>
    /// <param name="cs">The Constraints to set</param>
    /// <param name="ovar">The OrderVariable</param>
    let setConstraints cs (ovar: OrderVariable) = { ovar with DefinedConstraints = cs }


    /// Applies f to the defined Constraints of an OrderVariable.
    let mapConstraints f (ovar: OrderVariable) = ovar |> setConstraints (ovar |> getConstraints |> f)


    /// Removes the calculated constraints of an order variable.
    let clearCalculatedConstraints (ovar: OrderVariable) =
        { ovar with CalculatedConstraints = Constraints.create None None None None }


    let setCalculatedConstraints (ovar: OrderVariable) =
        { ovar with
            CalculatedConstraints =
                let cs = ovar.Variable.Values |> Constraints.fromValueRange
                // When the values have no increment, the defined increment is used.
                match ovar.DefinedConstraints.Incr, cs.Incr with
                | Some incr, None -> { cs with Incr = Some incr }
                | _ -> cs
        }


    let applyCalculatedConstraints (ovar: OrderVariable) =
        { ovar with OrderVariable.Variable.Values = ovar.CalculatedConstraints |> Constraints.toValueRange }


    /// Sets the Variable to the increment and maximum of the defined constraints. Without defined
    /// constraints, sets the Variable to non-zero positive.
    let applyOnlyMaxConstraints (ovar: OrderVariable) =
        { ovar with
            Variable =
                if ovar |> hasConstraints |> not then
                    { ovar.Variable with
                        Values =
                            match ovar.Variable.Values |> ValueRange.getUnit with
                            | None -> ValueRange.nonZeroPositive
                            | Some u -> u |> ValueUnit.zero |> Minimum.create false |> Min
                    }
                else
                    { ovar.Variable with Values = ovar.DefinedConstraints |> Constraints.toIncrMaxRange }
        }


    /// Sets the Variable to the minimum and increment of the defined constraints. Without defined
    /// constraints, sets the Variable to non-zero positive.
    let applyOnlyMinIncrConstraints (ovar: OrderVariable) =
        { ovar with
            Variable =
                if ovar |> hasConstraints |> not then
                    { ovar.Variable with
                        Values =
                            match ovar.Variable.Values |> ValueRange.getUnit with
                            | None -> ValueRange.nonZeroPositive
                            | Some u -> u |> ValueUnit.zero |> Minimum.create false |> Min
                    }
                else
                    { ovar.Variable with Values = ovar.DefinedConstraints |> Constraints.toMinIncrRange }
        }


    /// <summary>
    /// Sets the Variable to the calculated constraints when these are not empty and not only an exclusive
    /// minimum of zero. Otherwise sets the Variable to the defined constraints, or to non-zero positive
    /// when there are no defined constraints.
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    /// <remarks>
    /// A ValueSet in the constraints replaces their minimum, maximum and increment.
    /// </remarks>
    let applyConstraints (ovar: OrderVariable) =
        if
            (ovar.CalculatedConstraints |> Constraints.isEmpty
             || ovar.CalculatedConstraints |> Constraints.isNonZeroPositive)
            |> not
        then
            ovar |> applyCalculatedConstraints
        else
            { ovar with
                Variable =
                    if ovar |> hasConstraints |> not then
                        { ovar.Variable with
                            Values =
                                match ovar.Variable.Values |> ValueRange.getUnit with
                                | None -> ValueRange.nonZeroPositive
                                | Some u -> u |> ValueUnit.zero |> Minimum.create false |> Min
                        }
                    else
                        { ovar.Variable with Values = ovar.DefinedConstraints |> Constraints.toValueRange }
            }


    /// Set the Variable of an OrderVariable to its defined constraints; the constraints stay as
    /// they are.
    let applyDefinedConstraints (ovar: OrderVariable) =
        { ovar with Variable = { ovar.Variable with Values = ovar.DefinedConstraints |> Constraints.toValueRange } }


    /// Sets an inclusive maximum on the Variable at the value of upTo; an existing maximum is only
    /// lowered. Leaves the OrderVariable unchanged when upTo has no value set.
    let setMaxToValueOf upTo (ovar: OrderVariable) =
        match upTo |> getValSetValueUnit with
        | None -> ovar
        | Some vu ->
            { ovar with
                Variable =
                    { ovar.Variable with
                        Values = ovar.Variable.Values |> ValueRange.setMax (vu |> Maximum.create true)
                    }
            }


    /// Checks whether the values of the Variable lie within the calculated constraints when useCalc is
    /// true, else within the defined constraints. Returns true when there are no defined constraints.
    let isWithinConstraints useCalc ovar =
        if ovar |> hasConstraints |> not then
            true
        else
            let cs =
                if useCalc then
                    ovar.CalculatedConstraints
                else
                    ovar.DefinedConstraints
                |> Constraints.toValueRange

            ovar.Variable.Values |> ValueRange.isSubSetOf cs


    /// Check whether the ValueRange of the Variable of an
    /// OrderVariable is a MinIncrMax, i.e. has an increment
    let hasIncrement (ovar: OrderVariable) = ovar.Variable |> Variable.isMinIncrMax


    /// Checks whether the defined constraints have an increment. The values are not checked, because the
    /// solver can calculate values without an increment.
    let hasIncrConstraint ovar = (ovar |> getConstraints).Incr |> Option.isSome


    /// Checks whether the values of the OrderVariable have a maximum; a single value also has one.
    let hasMaxValue (ovar: OrderVariable) = ovar.Variable.Values |> ValueRange.getMax |> Option.isSome


    /// <summary>
    /// Try and increase the increment of a <c>ValueRange</c> of a Variable to an
    /// increment in incrs such that the resulting ValueRange contains
    /// at most maxCount values.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The increment list</param>
    /// <param name="ovar">The OrderVariable</param>
    /// <returns>The resulting (more restrictive) <c>ValueRange</c></returns>
    /// <remarks>
    /// When there is no increment in the list that can be used to increase
    /// the increment of the ValueRange to the maximum count, the largest possible
    /// increment is used.
    /// </remarks>
    let increaseIncrement maxCount incrs (ovar: OrderVariable) =
        { ovar with Variable = ovar.Variable |> Variable.increaseIncrement maxCount incrs }


    /// Finds the OrderVariable in ovars with the same name as a and returns it mapped with c; returns a
    /// when ovars has none.
    let private fromOrdVar toOvar c ovars a =
        ovars
        |> List.tryFind (eqsName (a |> toOvar))
        |> Option.map c
        |> Option.defaultValue a


    /// Sets the name of the Variable of an OrderVariable.
    let setName n ovar = { ovar with Variable = ovar.Variable |> Variable.setName n }


    /// <summary>
    /// Convert the <c>Variable</c> of an <c>OrderVariable</c> to a given <c>Unit</c>.
    /// Note the first unit is replaced by the given unit; all other
    /// units are kept.
    /// Example: mg/kg/day to g/kg/day when u = g
    /// </summary>
    /// <param name="u">The Unit to convert to</param>
    /// <param name="ovar">The OrderVariable</param>
    let convertFirstUnit u ovar =
        let u =
            ovar.Variable
            |> Variable.getUnit
            |> Option.map (fun u1 ->
                if u1 = ZeroUnit || u1 = NoUnit then
                    u1
                else
                    match u1 |> ValueUnit.getUnits with
                    | _ :: rest -> rest |> List.fold (fun acc x -> acc |> ValueUnit.per x) u
                    | _ -> u
            )
            |> Option.defaultValue u

        { ovar with Variable = ovar.Variable |> Variable.convertToUnit u }


    /// <summary>
    /// Convert the <c>Variable</c> of an <c>OrderVariable</c> to a given time <c>Unit</c>.
    /// Note the last unit is replaced by the given time unit; all other
    /// units are kept.
    /// Example: mg/kg/day to mg/kg/week when tu = week
    /// </summary>
    /// <param name="tu">The time Unit to convert to</param>
    /// <param name="ovar">The OrderVariable</param>
    let convertTimeUnit tu ovar =
        let u =
            ovar.Variable
            |> Variable.getUnit
            |> Option.map (fun u1 ->
                if u1 = ZeroUnit || u1 = NoUnit then
                    u1
                else
                    match u1 |> ValueUnit.getUnits |> List.rev with
                    // assume the last unit is a time unit
                    | _ :: rest ->
                        match tu :: rest |> List.rev with
                        | u :: rest -> rest |> List.fold (fun acc x -> acc |> ValueUnit.per x) u
                        | _ -> tu
                    | _ -> tu
            )
            |> Option.defaultValue tu

        { ovar with Variable = ovar.Variable |> Variable.convertToUnit u }


    /// Sets the Variable to the value in its value set nearest to vu. When the Variable has no value set,
    /// sets it to its median value.
    let setNearestValue vu (ovar: OrderVariable) =
        { ovar with
            Variable =
                if ovar.Variable.Values.IsValSet then
                    ovar.Variable |> Variable.setNearestValue vu
                else
                    ovar.Variable |> Variable.setMedianValue
        }


    let setNormValue ovar =
        match ovar |> getNormValue with
        | Some vu -> ovar |> setNearestValue vu
        | None -> ovar


    /// <summary>
    /// Get the string representation of an OrderVariable.
    /// </summary>
    /// <param name="withConstraints">
    /// Whether to add the defined and calculated constraints; they are added only when the defined
    /// constraints are not empty
    /// </param>
    /// <param name="exact">Whether to use the exact representation of the ValueRange</param>
    /// <param name="ovar">The OrderVariable</param>
    let toStringWithConstraints withConstraints exact ovar =
        let ns =
            ovar
            |> getName
            |> Variable.Name.toString
            |> String.split "."
            |> fun sl ->
                if sl.Length <= 1 then
                    sl
                    |> String.concat ""
                    |> String.split "_"
                    |> List.skip 1
                    |> String.concat "_"
                    |> sprintf "[]_%s"
                else
                    sl |> List.skip 1 |> String.concat "." |> sprintf "[%s"

        let vs = ovar.Variable |> Variable.getValueRange |> ValueRange.toString exact

        let cs =
            if ovar.DefinedConstraints |> Constraints.isEmpty then
                ""
            else
                ovar.DefinedConstraints |> Constraints.toValueRangeString

        let cal =
            if ovar.CalculatedConstraints |> Constraints.isEmpty then
                ""
            else
                ovar.CalculatedConstraints |> Constraints.toValueRangeString

        if cs |> String.isNullOrWhiteSpace || withConstraints |> not then
            $"{ns} {vs}"

        else
            $"{ns} {vs} | {cs} | {cal}"


    /// <summary>
    /// Get the string representation of an OrderVariable.
    /// </summary>
    let toString = toStringWithConstraints false


    /// Gets the value set of the OrderVariable that get returns from x as a short Dutch string; None
    /// without a value set.
    let toValueUnitStringList get x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.getValSet
        |> Option.map (ValueSet.toValueUnit >> ValueUnit.toStringDutchShort)


    /// Gets the ValueRange of the OrderVariable that get returns from x as a Dutch string.
    let toValueUnitString get (_: int) x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.toString false
        |> String.replace "x" "keer"
        // Writes a unit such as mg/kg*day as mg/kg/day, which means the same.
        |> String.replace "*" "/"


    /// Gets the ValueRange of the OrderVariable that get returns from x as Markdown with precision prec.
    /// Returns an empty string unless the values are a min max, a min incr max or a value set.
    let toValueUnitMarkdown get (prec: int) x =
        x
        |> get
        |> getVar
        |> function
            | vr when
                vr |> Variable.isMinMax
                || vr |> Variable.isMinIncrMax
                || vr |> Variable.isValSet
                ->
                vr.Values
                |> ValueRange.toMarkdown prec
                // Writes a unit such as mg/kg*day as mg/kg/day, which means the same.
                |> String.replace "*" "/"
            | _ -> ""


    /// <summary>
    /// Calculate a ValueSet for a Variable of an OrderVariable if the Value
    /// of the Variable is a MinIncrMax
    /// </summary>
    /// <param name="steps">The rounded steps whose multiples are kept when the ValueSet is pruned</param>
    /// <param name="n">Prune the ValueSet to max n values</param>
    /// <param name="ovar">The OrderVariable to change min incr max to a ValueSet</param>
    /// <returns>An OrderVariable with a Variable with a ValueSet if this can be calculated</returns>
    let minIncrMaxToValues steps n (ovar: OrderVariable) =
        { ovar with
            Variable =
                if ovar.Variable |> Variable.isMinIncrMax |> not then
                    ovar.Variable
                else
                    ovar.Variable |> Variable.minIncrMaxToValues steps n
        }


    /// Set the minimum value of the Variable of an OrderVariable
    let setMinValue (ovar: OrderVariable) = { ovar with Variable = ovar.Variable |> Variable.setMinValue }


    /// Set the maximum value of the Variable of an OrderVariable
    let setMaxValue (ovar: OrderVariable) = { ovar with Variable = ovar.Variable |> Variable.setMaxValue }


    /// Set the median value of the Variable of an OrderVariable
    let setMedianValue (ovar: OrderVariable) =
        { ovar with Variable = ovar.Variable |> Variable.setMedianValue }


    let setNthValue nth (ovar: OrderVariable) =
        { ovar with OrderVariable.Variable = ovar.Variable |> Variable.setNthValue nth }


    let setPercValue nth (ovar: OrderVariable) =
        { ovar with OrderVariable.Variable = ovar.Variable |> Variable.setPercValue nth }


    /// Set the Values of the Variable of an OrderVariable
    /// to a given value range
    let setValueRange vr (ovar: OrderVariable) =
        { ovar with Variable = ovar.Variable |> Variable.setValueRange vr }


    /// Check whether the Values of the Variable of an OrderVariable
    /// are non-zero positive or have an exclusive Minimum of zero
    let isNonZeroPositive (ovar: OrderVariable) =
        ovar.Variable |> Variable.isNonZeroPositive
        || ovar.Variable |> Variable.isMinExclusiveZero


    /// Set the Values of the Variable of an OrderVariable
    /// to non-zero positive values
    let setToNonZeroPositive (ovar: OrderVariable) =
        { ovar with
            OrderVariable.Variable.Values =
                match ovar.Variable.Values |> ValueRange.getUnit with
                | None -> ValueRange.nonZeroPositive
                | Some u -> u |> ValueUnit.zero |> Minimum.create false |> Min
        }


    /// Clear the Values of the Variable of an OrderVariable
    let clear (ovar: OrderVariable) = { ovar with Variable = ovar.Variable |> Variable.clear }


    /// Check whether the Values of the Variable of an OrderVariable
    /// are cleared, i.e., unrestricted
    let isCleared (ovar: OrderVariable) = ovar.Variable |> Variable.isUnrestricted


    /// <summary>
    /// Set the unit of the Variable of an OrderVariable according to the unit
    /// of the Constraints of the OrderVariable.
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    /// <remarks>
    /// Uses the first unit other than the zero unit of the Min, Max, ValueSet and Incr, in that order.
    /// </remarks>
    let setUnit (ovar: OrderVariable) =
        [
            ovar.DefinedConstraints.Min
            |> Option.map (Minimum.toValueUnit >> ValueUnit.getUnit)
            ovar.DefinedConstraints.Max
            |> Option.map (Maximum.toValueUnit >> ValueUnit.getUnit)
            ovar.DefinedConstraints.Values
            |> Option.map (ValueSet.toValueUnit >> ValueUnit.getUnit)
            ovar.DefinedConstraints.Incr
            |> Option.map (Increment.toValueUnit >> ValueUnit.getUnit)
        ]
        |> List.choose id
        |> List.filter ((<>) ZeroUnit)
        |> List.distinct
        |> List.tryHead
        |> function
            | None -> ovar
            | Some u -> { ovar with Variable = ovar.Variable |> Variable.convertToUnit u }


    /// Checks whether the values of the Variable are unrestricted, non-zero positive or have an exclusive
    /// minimum of zero, so that no constraints have been applied.
    let isConstraintsNotApplied (ovar: OrderVariable) =
        ovar.Variable |> Variable.isUnrestricted
        || ovar.Variable |> Variable.isNonZeroPositive
        || ovar.Variable |> Variable.isMinExclusiveZero


    /// Check whether the Values of the Variable of an OrderVariable
    /// are set, i.e., have a distinct set of values
    let hasValues (ovar: OrderVariable) = ovar.Variable |> Variable.hasValues


    /// <summary>
    /// Checks whether the Variable is solved: one value, unrestricted or non-zero positive.
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    let isSolved (ovar: OrderVariable) =
        ovar.Variable |> Variable.isSolved
        || ovar.Variable |> Variable.isUnrestricted
        || ovar.Variable |> Variable.isNonZeroPositive


    /// Checks whether the values of an order variable are a list of one value.
    let hasOneValue ovar = ovar |> getVar |> Variable.isSolved


    /// Returns the value of an order variable with one value; None otherwise.
    let solvedValue ovar =
        if ovar |> hasOneValue then
            ovar |> getValSetValueUnit
        else
            None


    /// Returns a map of the order variables by name.
    let byName ovars =
        ovars |> List.map (fun ovar -> ovar |> getName, ovar) |> Map.ofList


    /// Checks whether the calculated constraints of an order variable have a value set of one value.
    let hasOneCalculatedValue (ovar: OrderVariable) =
        ovar.CalculatedConstraints.Values
        |> Option.exists (fun vs -> vs |> ValueSet.count = 1)


    /// Adds the value of original to the value list of ovar when original has one value, ovar has a value
    /// list and the value lies within the calculated constraints of ovar. Otherwise returns ovar unchanged.
    let includeOriginalValue (original: OrderVariable) (ovar: OrderVariable) =
        match original.Variable.Values, ovar.Variable.Values with
        | ValSet o, ValSet vs when
            original |> hasOneValue
            && { ovar with OrderVariable.Variable.Values = original.Variable.Values }
               |> isWithinConstraints true
            ->
            // The list of ovar goes first, so the joined list has the unit of that list.
            let values =
                [ vs; o ]
                |> List.map ValueSet.toValueUnit
                |> ValueUnit.collectValues
                |> ValueSet.create

            { ovar with OrderVariable.Variable.Values = values |> ValSet }
        | _ -> ovar


    /// Returns how a value can be set on an order variable. A list of more than one value, or a min,
    /// increment and max, is selectable. One value with a defined increment is steppable. Anything else
    /// is not settable.
    let shape (ovar: OrderVariable) =
        match ovar.Variable.Values with
        | ValSet vs when vs |> ValueSet.count > 1 -> Shape.SelectableList
        | ValSet _ when ovar.DefinedConstraints.Incr |> Option.isSome -> Shape.Steppable
        | MinIncrMax _ -> Shape.SelectableRange
        | _ -> Shape.NotSettable


    /// Frees the values of an order variable. With a defined increment, the values become any multiple of
    /// that increment from the smallest increment up, with no maximum. Without a defined increment, the
    /// values become non-zero positive.
    let free (ovar: OrderVariable) =
        match ovar.DefinedConstraints.Incr with
        | None -> ovar |> setToNonZeroPositive
        | Some incr ->
            let min = incr |> Increment.minElement |> Option.map (Minimum.create true)

            { ovar with
                OrderVariable.Variable.Values =
                    ValueRange.unrestricted
                    |> ValueRange.setOptMin min
                    |> ValueRange.setOptIncr (Some incr)
            }


    /// Sets the order variable to a count of 1.
    let setToOne (ovar: OrderVariable) =
        { ovar with OrderVariable.Variable.Values = ValueUnit.oneCount |> ValueSet.create |> ValSet }


    /// Sets the order variable to any count of 1 or more.
    let setToAtLeastOne (ovar: OrderVariable) =
        { ovar with OrderVariable.Variable.Values = ValueUnit.oneCount |> Minimum.create true |> Min }


    /// Narrows the values of the order variable to the values of vu. Returns None when none of those
    /// values lie in the current range.
    let tryConstrain vu (ovar: OrderVariable) =
        // the solver raises on an empty intersection
        try
            let vr = vu |> ValueSet.create |> ValSet
            Some { ovar with Variable = ovar.Variable |> Variable.setValueRange vr }
        with Exceptions.SolverException _ ->
            None


    /// Fixes the order variable to the values of vu, as for one concentration. Sets the defined value
    /// list, and the calculated value list when there is one, to those values, so that a reset of the
    /// variable returns them. When setsBack ovar is true, the values are not narrowed; otherwise they are
    /// narrowed to vu, and the result is None when none of those values lie in the current range.
    let fixToValues setsBack vu (ovar: OrderVariable) =
        let set = vu |> ValueSet.create

        let fixedOvar =
            { ovar with
                OrderVariable.DefinedConstraints.Values = Some set
                OrderVariable.CalculatedConstraints.Values =
                    ovar.CalculatedConstraints.Values |> Option.map (fun _ -> set)
            }

        if setsBack ovar then
            Some fixedOvar
        else
            fixedOvar |> tryConstrain vu


    /// Returns the value list of an order variable: its values when these are a list, else the value list
    /// of the calculated constraints, else that of the defined constraints.
    let tryValueList (ovar: OrderVariable) =
        match ovar.Variable.Values with
        | ValSet vs -> Some vs
        | _ ->
            ovar.CalculatedConstraints.Values
            |> Option.orElse ovar.DefinedConstraints.Values


    /// Combines the same order variable of the orders per concentration into one. The values are the
    /// union of the values of ovars and the defined constraints are those of orig. The calculated
    /// constraints are those of orig, or the union of those of ovars when records is true.
    let unionPerConcentration maxCount records (orig: OrderVariable) (ovars: OrderVariable list) =
        let union = ValueRange.unionRanges maxCount

        let calculated =
            match ovars |> List.map _.CalculatedConstraints |> List.distinct with
            | _ when not records -> orig.CalculatedConstraints
            | [ cs ] -> cs
            | css -> css |> List.map Constraints.toValueRange |> union |> Constraints.fromValueRange

        { orig with
            OrderVariable.Variable.Values = ovars |> List.map _.Variable.Values |> union
            OrderVariable.CalculatedConstraints = calculated
        }


    /// Returns the increment a large step moves by: the calculated increment, or ten times the calculated
    /// increment when that is 0.1 mL or 0.1 mL/hour; None when the order variable has no calculated
    /// increment.
    let largeIncrement (ovar: OrderVariable) =
        let tenth u = u |> ValueUnit.singleWithValue (1N / 10N)

        let tenfoldIncrements =
            [
                Units.Volume.milliLiter |> tenth
                Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour |> tenth
            ]

        ovar.CalculatedConstraints.Incr
        |> Option.map (fun incr ->
            let vu = incr |> Increment.toValueUnit

            let isTenfold =
                tenfoldIncrements
                |> List.exists (fun t -> ValueUnit.eqsGroup vu t && ValueUnit.eqs vu t)

            if isTenfold then
                (10N |> ValueUnit.singleWithUnit Units.Count.times) * vu |> Increment.create
            else
                incr
        )


    /// <summary>
    /// Steps the value of an order variable up or down by n increments and returns one value.
    /// A single value (minimum equal to maximum) moves by n increments. For a range, the first step goes
    /// to the minimum when stepping up and to the maximum when stepping down, and each further step moves
    /// one increment; with only one bound, the first step goes to that bound. A decrease, or an increase
    /// from only a minimum, that falls below the floor returns the floor. The floor is the calculated
    /// increment for a large step, also when the large increment is ten times that, and the defined
    /// increment for a small step.
    /// </summary>
    /// <param name="isStepUp">Whether the operation is an increase, otherwise decrease</param>
    /// <param name="useLarge">Whether the large increment should be used, see largeIncrement</param>
    /// <param name="n">The number of increases or decreases</param>
    /// <param name="ovar">The OrderVariable to step</param>
    /// <returns>The OrderVariable with the stepped value, unchanged when there is no floor or increment</returns>
    let step isStepUp useLarge n (ovar: OrderVariable) =
        let floor, incr =
            if useLarge then
                ovar.CalculatedConstraints.Incr, ovar |> largeIncrement
            else
                ovar.DefinedConstraints.Incr, ovar.DefinedConstraints.Incr

        match floor, incr with
        | None, _
        | _, None -> ovar
        | Some floor, Some incr ->
            let floor = floor |> Increment.toValueUnit
            let incr = incr |> Increment.toValueUnit

            let minVal, maxVal =
                ovar.Variable.Values |> ValueRange.getMin |> Option.map Minimum.toValueUnit,
                ovar.Variable.Values |> ValueRange.getMax |> Option.map Maximum.toValueUnit

            let calcIncr n incr =
                if n <= 0 then
                    (0 |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Count.times) * incr
                elif n = 1 then
                    incr
                else
                    (n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Count.times) * incr

            let vr =
                match isStepUp, minVal, maxVal with
                // Increase: there is both a min and max
                | true, Some minVal, Some maxVal ->
                    // A specific value has been set, increase that value
                    if minVal = maxVal then
                        minVal + (incr |> calcIncr n)
                    else
                        // No specific value has been set, start with the min value
                        minVal + (incr |> calcIncr (n - 1))
                // Increase: prefer stepping from minVal, otherwise from maxVal
                | true, Some minVal, None ->
                    let vu = minVal + (incr |> calcIncr (n - 1))
                    // An exclusive minimum of zero gives a value below the floor.
                    if vu <? floor then floor else vu
                | true, None, Some maxVal -> maxVal + (incr |> calcIncr (n - 1))
                // Fallback when no min or max: use incr as the base value
                | true, _, _ -> incr + incr |> calcIncr (n - 1)
                // Decrease: there is both a min and a max
                | false, Some minVal, Some maxVal ->
                    // A specific value has been set, decrease that value
                    if minVal = maxVal then
                        let vu = minVal - (incr |> calcIncr n)
                        if vu <? floor then floor else vu
                    else
                        // No specific value has been set, start with the max value
                        let vu = maxVal - (incr |> calcIncr (n - 1))
                        if vu <? floor then floor else vu
                // Decrease: prefer stepping from maxVal, otherwise from minVal
                | false, _, Some maxVal ->
                    let vu = maxVal - (incr |> calcIncr (n - 1))
                    // make sure that the value doesn't get below the floor
                    if vu <? floor then floor else vu
                | false, Some minVal, None ->
                    let vu = minVal - (incr |> calcIncr (n - 1))
                    // make sure that the value doesn't get below the floor
                    if vu <? floor then floor else vu
                // Fallback when no min or max: the floor
                | false, _, _ -> floor
                |> ValueSet.create
                |> ValSet

            { ovar with OrderVariable.Variable.Values = vr }

    let decrease useLarge n = step false useLarge n


    let increase useLarge n = step true useLarge n


    let pickNearestHigherElseLower target candidates =
        match target |> getValSetValueUnit, candidates |> getValSetValueUnit with
        | Some vu1, Some vu2 ->
            vu2
            |> ValueUnit.pickNearestHigherElseLower vu1
            |> ValueSet.create
            |> ValSet
            |> fun vr -> candidates |> setValueRange vr
        | _ -> candidates


    module Dto =

        open Newtonsoft.Json


        /// The Dto data transfer type for an OrderVariable
        type Dto() =
            member val Name = "" with get, set
            member val Constraints = Variable.Dto.dto () with get, set
            member val Calculated = Variable.Dto.dto () with get, set
            member val Variable = Variable.Dto.dto () with get, set
            /// The increment a large step moves by, see largeIncrement.
            member val LargeIncrOpt: ValueUnit.Dto.Dto option = None with get, set
            member val Level = IsNormal with get, set

        and Level =
            | IsNormal
            | IsCaution
            | IsWarning
            | IsAlert

        /// Create a new Dto for an OrderVariable
        let dto () = Dto()


        /// Sets the minimum, increment, maximum and values of the Variable part of a Dto to None, and the
        /// Level to IsNormal.
        let cleanVariable (old: Dto) =
            old.Variable.MinOpt <- None
            old.Variable.IncrOpt <- None
            old.Variable.MaxOpt <- None
            old.Variable.ValsOpt <- None
            old.Level <- IsNormal

        /// Create an OrderVariable from a Dto
        let fromDto (dto: Dto) =
            try
                let cs =
                    let vs =
                        dto.Constraints.ValsOpt
                        |> Option.bind ValueUnit.Dto.fromDto
                        |> Option.bind (fun vu ->
                            if vu |> ValueUnit.isEmpty then
                                None
                            else
                                vu |> ValueSet.create |> Some
                        )

                    let incr =
                        dto.Constraints.IncrOpt
                        |> Option.bind ValueUnit.Dto.fromDto
                        |> Option.bind (fun vu ->
                            if vu |> ValueUnit.isEmpty then
                                None
                            else
                                vu |> Increment.create |> Some
                        )

                    let min =
                        dto.Constraints.MinOpt
                        |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Minimum.create dto.Constraints.MinIncl))

                    let max =
                        dto.Constraints.MaxOpt
                        |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Maximum.create dto.Constraints.MaxIncl))

                    Constraints.create min incr max vs

                let cal =
                    let vs =
                        dto.Calculated.ValsOpt
                        |> Option.bind ValueUnit.Dto.fromDto
                        |> Option.bind (fun vu ->
                            if vu |> ValueUnit.isEmpty then
                                None
                            else
                                vu |> ValueSet.create |> Some
                        )

                    let incr =
                        dto.Calculated.IncrOpt
                        |> Option.bind ValueUnit.Dto.fromDto
                        |> Option.bind (fun vu ->
                            if vu |> ValueUnit.isEmpty then
                                None
                            else
                                vu |> Increment.create |> Some
                        )

                    let min =
                        dto.Calculated.MinOpt
                        |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Minimum.create dto.Calculated.MinIncl))

                    let max =
                        dto.Calculated.MaxOpt
                        |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Maximum.create dto.Calculated.MaxIncl))

                    Constraints.create min incr max vs

                let n = dto.Name |> Name.fromString

                let vals =
                    dto.Variable.ValsOpt
                    |> Option.bind ValueUnit.Dto.fromDto
                    |> Option.bind (fun vu ->
                        if vu |> ValueUnit.isEmpty then
                            None
                        else
                            vu |> ValueSet.create |> Some
                    )

                let incr =
                    dto.Variable.IncrOpt
                    |> Option.bind ValueUnit.Dto.fromDto
                    |> Option.bind (fun vu ->
                        if vu |> ValueUnit.isEmpty then
                            None
                        else
                            vu |> Increment.create |> Some
                    )

                let min =
                    dto.Variable.MinOpt
                    |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Minimum.create dto.Variable.MinIncl))

                let max =
                    dto.Variable.MaxOpt
                    |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Maximum.create dto.Variable.MaxIncl))

                create n min incr max vals cs cal
            with e ->
                e |> raise


        /// Create a Dto from an OrderVariable
        let toDto (ovar: OrderVariable) =
            let vuToDto = ValueUnit.Dto.toDto true "dutch"
            let dto = dto ()
            let vr = ovar |> getVar |> Variable.getValueRange

            dto.Name <- ovar |> getName |> Name.toString

            dto.Variable.ValsOpt <-
                vr
                |> ValueRange.getValSet
                |> Option.map ValueSet.toValueUnit
                |> Option.bind vuToDto

            dto.Variable.IncrOpt <-
                vr
                |> ValueRange.getIncr
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto

            dto.Variable.MinOpt <- vr |> ValueRange.getMin |> Option.map Minimum.toValueUnit |> Option.bind vuToDto

            dto.Variable.MinIncl <-
                vr
                |> ValueRange.getMin
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false

            dto.Variable.MaxOpt <- vr |> ValueRange.getMax |> Option.map Maximum.toValueUnit |> Option.bind vuToDto

            dto.Variable.MaxIncl <-
                vr
                |> ValueRange.getMax
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto.Constraints.ValsOpt <-
                ovar.DefinedConstraints.Values
                |> Option.map ValueSet.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.IncrOpt <-
                ovar.DefinedConstraints.Incr
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MinOpt <-
                ovar.DefinedConstraints.Min
                |> Option.map Minimum.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MinIncl <-
                ovar.DefinedConstraints.Min
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false

            dto.Constraints.MaxOpt <-
                ovar.DefinedConstraints.Max
                |> Option.map Maximum.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MaxIncl <-
                ovar.DefinedConstraints.Max
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto.Calculated.ValsOpt <-
                ovar.CalculatedConstraints.Values
                |> Option.map ValueSet.toValueUnit
                |> Option.bind vuToDto

            dto.Calculated.IncrOpt <-
                ovar.CalculatedConstraints.Incr
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto

            dto.Calculated.MinOpt <-
                ovar.CalculatedConstraints.Min
                |> Option.map Minimum.toValueUnit
                |> Option.bind vuToDto

            dto.Calculated.MinIncl <-
                ovar.CalculatedConstraints.Min
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false

            dto.Calculated.MaxOpt <-
                ovar.CalculatedConstraints.Max
                |> Option.map Maximum.toValueUnit
                |> Option.bind vuToDto

            dto.Calculated.MaxIncl <-
                ovar.CalculatedConstraints.Max
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto.LargeIncrOpt <-
                ovar
                |> largeIncrement
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto

            dto


    /// Type and functions that represent a count
    module Count =


        /// Create a Count from an OrderVariable
        let count = Count.Count


        [<Literal>]
        let name = "cnt"


        /// Get the OrderVariable in a Count
        let toOrdVar (Count.Count cnt) = cnt


        /// Create a Dto for a Count
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Count from a Dto
        let fromDto dto = dto |> Dto.fromDto |> count


        /// Replaces a Count with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar count


        /// Create a Count with name n
        let create n =
            Units.Count.times |> createNew (n |> Name.add name) |> Count.Count


        /// Turn a Count to a string
        let toString = toOrdVar >> (toString false)


        /// Gets the value set of a Count as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Count
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Count
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Check whether a Count has Constraints that
        /// are not non-zero positive and not empty
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Count is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a Count
        let apply f = toOrdVar >> f >> count


        /// Apply the constraints of a Count to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> count


        /// Check whether a Count is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a Count is solved
        let isSolved = toOrdVar >> isSolved


        /// Set a Count to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> count


    /// Type and functions that represent a time
    module Time =


        /// Create a Time from an OrderVariable
        let time = Time.Time


        [<Literal>]
        let name = "tme"


        /// Get the OrderVariable in a Time
        let toOrdVar (Time.Time tme) = tme


        /// Create a Dto for a Time
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Time from a Dto
        let fromDto dto = dto |> Dto.fromDto |> time


        /// Replaces a Time with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar time


        /// <summary>
        /// Create a Time
        /// </summary>
        /// <param name="n">The Name of the Time</param>
        /// <param name="un">The Unit of the Time</param>
        let create n un = un |> createNew (n |> Name.add name) |> Time.Time


        /// Turn a Time to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a Time (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a Time as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Time
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Time
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a Time has Constraints that
        /// are not non-zero positive and not empty
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether Time is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a Time
        let apply f = toOrdVar >> f >> time


        /// Apply the constraints of a Time to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> time


        /// Check whether Time is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether Time is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the minimum value for Time
        let setMinValue = toOrdVar >> setMinValue >> time


        /// Set the maximum value for Time
        let setMaxValue = toOrdVar >> setMaxValue >> time


        /// Set the median value for Time
        let setMedianValue = toOrdVar >> setMedianValue >> time


        /// Set the variable to its nth value, counted from 0.
        let setNthValue nth = toOrdVar >> setNthValue nth >> time


        /// Set Time to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> time


    /// Type and functions that represent a frequency
    module Frequency =


        [<Literal>]
        let name = "frq"


        /// Get the OrderVariable in a Frequency
        let toOrdVar (Frequency frq) = frq


        /// Create a Dto for a Frequency
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Frequency from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Frequency


        /// Replaces a Frequency with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar Frequency


        /// Apply a function to the OrderVariable in a Frequency
        let apply f = toOrdVar >> f >> Frequency


        /// <summary>
        /// Create a Frequency
        /// </summary>
        /// <param name="n">The Name of the Frequency</param>
        /// <param name="tu">The Time Unit of the Frequency</param>
        let create n tu =
            match tu with
            | NoUnit -> NoUnit |> createNew (n |> Name.add name)

            | _ ->
                let frqU = Units.Count.times |> ValueUnit.per tu
                let ovar = frqU |> createNew (n |> Name.add name)
                // frequency increment defaults to 1
                { ovar with
                    OrderVariable.DefinedConstraints.Incr =
                        1N |> ValueUnit.singleWithUnit frqU |> Increment.create |> Some
                }
            |> Frequency


        /// Turn a Frequency to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a Frequency (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a Frequency as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Frequency
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Frequency
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a Frequency has Constraints that
        /// are not non-zero positive and not empty
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Frequency is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply the constraints of a Frequency to the OrderVariable Variable
        let applyConstraints = apply applyConstraints

        /// Check whether a Frequency is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Set the Values of the Variable of a Frequency
        /// to the minimum value
        let setMinValue = apply setMinValue

        /// Set the Values of the Variable of a Frequency
        /// to the maximum value
        let setMaxValue = apply setMaxValue

        /// Set the Values of the Variable of a Frequency
        /// to the median value
        let setMedianValue = apply setMedianValue

        /// Sets the values to the nth value, counted from 0, but not above the maximum.
        let setNthValue nth = apply (setNthValue nth)


        /// Sets the values to the value at nth percent of the way through all values.
        let setPercValue nth = apply (setPercValue nth)


        /// Check whether a Frequency is solved
        let isSolved = toOrdVar >> isSolved


        /// Set a Frequency to non-zero positive values
        let setToNonZeroPositive = apply setToNonZeroPositive


    /// Type and functions that represent a concentration, a quantity per quantity (for example, mg/mL)
    module Concentration =


        [<Literal>]
        let name = "cnc"


        /// Get the OrderVariable in a Concentration
        let toOrdVar (Concentration cnc) = cnc


        /// Create a Dto for a Concentration
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Concentration from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Concentration


        /// Replaces a Concentration with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar Concentration


        /// <summary>
        /// Create a Concentration with name n
        /// </summary>
        /// <param name="n">The Name of the Concentration</param>
        /// <param name="un">The first Unit of the Concentration</param>
        /// <param name="su">The second Unit of the Concentration</param>
        let create n un su =
            match un, su with
            | Unit.NoUnit, _
            | _, Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per su
            |> createNew (n |> Name.add name)
            |> Concentration


        /// Turn a Concentration to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a Concentration (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a Concentration as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Concentration
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Concentration
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Concentration is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a Concentration
        let apply f = toOrdVar >> f >> Concentration


        /// Apply the constraints of a Concentration to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Concentration


        /// Check whether a Concentration is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a Concentration is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the minimum value for a Concentration
        let setMinValue = toOrdVar >> setMinValue >> Concentration


        /// Set the maximum value for a Concentration
        let setMaxValue = toOrdVar >> setMaxValue >> Concentration


        /// Set the median value for a Concentration
        let setMedianValue = toOrdVar >> setMedianValue >> Concentration


        /// Sets the values to the nth value, counted from 0, but not above the maximum.
        let setNthValue nth = toOrdVar >> setNthValue nth >> Concentration


        /// Sets the values to the value at perc percent of the way through all values.
        let setPercValue perc = toOrdVar >> setPercValue perc >> Concentration


        /// Set a Concentration to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Concentration


    /// Type and functions that represent a quantity
    module Quantity =


        [<Literal>]
        let name = "qty"


        /// Get the OrderVariable in a Quantity
        let toOrdVar (Quantity qty) = qty


        /// Create a Dto for a Quantity
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Quantity from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Quantity


        /// Replaces a Quantity with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar Quantity


        /// <summary>
        /// Create a Quantity with name n
        /// </summary>
        /// <param name="n">The Name of the Quantity</param>
        /// <param name="un">The Unit of the Quantity</param>
        let create n un = un |> createNew (n |> Name.add name) |> Quantity


        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> Quantity


        /// Turn a Quantity to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a Quantity (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a Quantity as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Quantity
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Quantity
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Quantity is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> Quantity


        let applyOnlyMinIncrConstraints = toOrdVar >> applyOnlyMinIncrConstraints >> Quantity


        /// Set the Variable of a Quantity to its defined constraints
        let applyDefinedConstraints = toOrdVar >> applyDefinedConstraints >> Quantity


        /// Sets an inclusive maximum on a Quantity at the value of upTo, for example to limit a dose
        /// quantity to the quantity of the whole orderable. Leaves the Quantity unchanged when upTo has no
        /// value set.
        let setMaxToValueOf upTo = toOrdVar >> setMaxToValueOf (upTo |> toOrdVar) >> Quantity


        /// Apply a function to the OrderVariable in a Quantity
        let apply f = toOrdVar >> f >> Quantity


        /// Apply the constraints of a Quantity to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Quantity


        /// Check whether a Quantity is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a Quantity has an increment (Min/Incr/Max)
        let hasIncrement = toOrdVar >> hasIncrement


        /// Check whether a Quantity has a maximum or a ValueSet constraint
        let hasMaxConstraint = toOrdVar >> hasMaxConstraint


        /// <summary>
        /// Increase the increment of a Quantity until the resulting ValueRange
        /// contains at most maxCount values.
        /// </summary>
        /// <param name="maxCount">The maximum number of values in the ValueRange</param>
        /// <param name="incrs">The list of increments to choose from</param>
        let increaseIncrement maxCount incrs = toOrdVar >> increaseIncrement maxCount incrs >> Quantity


        /// Check whether a Quantity is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the minimum value for a Quantity
        let setMinValue = toOrdVar >> setMinValue >> Quantity


        /// Set the maximum value for a Quantity
        let setMaxValue = toOrdVar >> setMaxValue >> Quantity


        /// Set the median value for a Quantity
        let setMedianValue = toOrdVar >> setMedianValue >> Quantity


        /// Sets the values to the nth value, counted from 0, but not above the maximum.
        let setNthValue nth = toOrdVar >> setNthValue nth >> Quantity


        /// Sets the values to the value at perc percent of the way through all values.
        let setPercValue perc = toOrdVar >> setPercValue perc >> Quantity


        /// Set a Quantity to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Quantity


        let pickNearestHigherElseLower qty1 qty2 =
            let ovar1 = qty1 |> toOrdVar
            let ovar2 = qty2 |> toOrdVar

            ovar2 |> pickNearestHigherElseLower ovar1 |> Quantity


    /// Type and functions that represent a quantity per time
    module PerTime =


        [<Literal>]
        let name = "ptm"


        /// Get the OrderVariable in a PerTime
        let toOrdVar (PerTime ptm) = ptm


        /// Create a Dto for a PerTime
        let toDto = toOrdVar >> Dto.toDto


        /// Create a PerTime from a Dto
        let fromDto dto = dto |> Dto.fromDto |> PerTime


        /// Replaces a PerTime with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar PerTime


        /// <summary>
        /// Create a PerTime with name n
        /// </summary>
        /// <param name="n">The Name of the PerTime</param>
        /// <param name="un">The Unit of the PerTime</param>
        /// <param name="tu">The Time Unit of the PerTime</param>
        let create n un tu =
            match un with
            | Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> PerTime


        /// Convert the first unit of a PerTime (e.g., mg/kg/day -> g/kg/day)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> PerTime


        /// Convert the time unit of a PerTime (e.g., mg/kg/day -> mg/kg/week)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> PerTime


        /// Turn a PerTime to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a PerTime value (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a PerTime as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a PerTime
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a PerTime
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Check whether a PerTime has constraints
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a PerTime is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a PerTime
        let apply f = toOrdVar >> f >> PerTime


        /// Apply the constraints of a PerTime to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> PerTime


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> PerTime


        /// Check whether a PerTime is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a PerTime is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the variable to its nth value, counted from 0.
        let setNthValue nth = toOrdVar >> setNthValue nth >> PerTime


        /// Set a PerTime to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> PerTime


    module Rate =


        [<Literal>]
        let name = "rte"


        /// Get the OrderVariable in a Rate
        let toOrdVar (Rate rte) = rte


        /// Create a Dto for a Rate
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Rate from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Rate


        /// Replaces a Rate with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar Rate


        /// <summary>
        /// Create a Rate with name n
        /// </summary>
        /// <param name="n">The Name of the Rate</param>
        /// <param name="un">The Unit of the Rate</param>
        /// <param name="tu">The Time Unit of the Rate</param>
        let create n un tu =
            match un with
            | Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> Rate


        /// Convert the first unit of a Rate (e.g., mg/h -> g/h)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> Rate


        /// Convert the time unit of a Rate (e.g., mg/h -> mg/min)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> Rate


        /// Turn a Rate to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a Rate (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a Rate as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Rate
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Rate
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Rate is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a Rate
        let apply f = toOrdVar >> f >> Rate


        /// Apply the constraints of a Rate to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Rate


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> Rate


        /// Set the Variable of a Rate to its defined constraints
        let applyDefinedConstraints = toOrdVar >> applyDefinedConstraints >> Rate


        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a Rate is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the minimum value for a Rate
        let setMinValue = toOrdVar >> setMinValue >> Rate


        /// Set the maximum value for a Rate
        let setMaxValue = toOrdVar >> setMaxValue >> Rate


        /// Set the median value for a Rate
        let setMedianValue = toOrdVar >> setMedianValue >> Rate


        /// Sets the values to the nth value, counted from 0, but not above the maximum.
        let setNthValue nth = toOrdVar >> setNthValue nth >> Rate


        /// Sets the values to the value at perc percent of the way through all values.
        let setPercValue perc = toOrdVar >> setPercValue perc >> Rate


        /// Set a Rate to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Rate


    /// Type and functions that represent a total
    module Total =


        [<Literal>]
        let name = "tot"


        /// Get the OrderVariable in a Total
        let toOrdVar (Total tot) = tot


        /// Create a Dto for a Total
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Total from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Total


        /// Replaces a Total with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar Total


        /// <summary>
        /// Create a Total with name n
        /// </summary>
        /// <param name="n">The Name of the Total</param>
        /// <param name="un">The Unit of the Total</param>
        let create n un = un |> createNew (n |> Name.add name) |> Total


        /// Convert the first unit of a Total (e.g., mg -> g)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> Total


        /// Turn a Total to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a Total (including constraints)
        let toStringWithConstraints = toOrdVar >> toStringWithConstraints true false


        /// Gets the value set of a Total as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Total
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Total
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a Total has constraints
        let hasConstraints = toOrdVar >> hasConstraints

        /// Check whether a Total is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a Total
        let apply f = toOrdVar >> f >> Total


        /// Apply the constraints of a Total to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Total


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> Total

        /// Check whether a Total is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Check whether a Total is solved
        let isSolved = toOrdVar >> isSolved

        /// Set a Total to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Total

    /// Type and functions that represent an adjusted quantity,
    /// i.e., an amount per adjust unit (for example, mg/kg)
    module QuantityAdjust =


        [<Literal>]
        let name = "qty_adj"


        /// Get the OrderVariable in a QuantityAdjust
        let toOrdVar (QuantityAdjust qty_adj) = qty_adj


        /// Create a Dto for a QuantityAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a QuantityAdjust from a Dto
        let fromDto dto = dto |> Dto.fromDto |> QuantityAdjust


        /// Replaces a QuantityAdjust with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar QuantityAdjust


        /// <summary>
        /// Create a QuantityAdjust with name n
        /// </summary>
        /// <param name="n">The Name of the QuantityAdjust</param>
        /// <param name="un">The Unit of the QuantityAdjust</param>
        /// <param name="adj">The Adjust Unit of the QuantityAdjust</param>
        let create n un adj =
            match un, adj with
            | Unit.NoUnit, _
            | _, Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per adj
            |> createNew (n |> Name.add name)
            |> QuantityAdjust


        /// Convert the first unit of a QuantityAdjust (e.g., mg/kg -> g/kg)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> QuantityAdjust

        /// Set the nearest value of a QuantityAdjust to a given ValueUnit
        let setNearestValue vu = toOrdVar >> setNearestValue vu >> QuantityAdjust


        /// Turn a QuantityAdjust to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a QuantityAdjust (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)

        /// Gets the value set of a QuantityAdjust as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar

        /// Get a ValueUnit string representation of a QuantityAdjust
        let toValueUnitString = toValueUnitString toOrdVar

        /// Get a ValueUnit Markdown representation of a QuantityAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        let hasConstraints = toOrdVar >> hasConstraints

        /// Check whether a QuantityAdjust has a maximum or a ValueSet constraint
        let hasMaxConstraint = toOrdVar >> hasMaxConstraint

        /// Check whether a QuantityAdjust is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc

        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> QuantityAdjust

        /// Apply a function to the OrderVariable in a QuantityAdjust
        let apply f = toOrdVar >> f >> QuantityAdjust


        /// Apply the constraints of a QuantityAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> QuantityAdjust


        /// Set the Variable of a QuantityAdjust to its defined constraints
        let applyDefinedConstraints = toOrdVar >> applyDefinedConstraints >> QuantityAdjust

        /// Check whether a QuantityAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Check whether a QuantityAdjust is solved
        let isSolved = toOrdVar >> isSolved

        /// Set the variable to its nth value, counted from 0.
        let setNthValue nth = toOrdVar >> setNthValue nth >> QuantityAdjust

        /// Set a QuantityAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> QuantityAdjust


        let hasNormValue = toOrdVar >> getNormValue >> Option.isSome


        let setNormValue = toOrdVar >> setNormValue >> QuantityAdjust


    /// Type and functions that represent an adjusted per-time value,
    /// i.e., an amount per adjust unit per time (for example, mg/kg/day)
    module PerTimeAdjust =


        [<Literal>]
        let name = "ptm_adj"


        /// Get the OrderVariable in a PerTimeAdjust
        let toOrdVar (PerTimeAdjust ptm_adj) = ptm_adj


        /// Create a Dto for a PerTimeAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a PerTimeAdjust from a Dto
        let fromDto dto =
            dto |> Dto.fromDto |> (map ValueUnit.correctAdjustOrder >> PerTimeAdjust)


        /// Replaces a PerTimeAdjust with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar (map ValueUnit.correctAdjustOrder >> PerTimeAdjust)


        /// <summary>
        /// Create a PerTimeAdjust with name n
        /// </summary>
        /// <param name="n">The Name of the PerTimeAdjust</param>
        /// <param name="un">The Unit of the PerTimeAdjust</param>
        /// <param name="adj">The Adjust Unit of the PerTimeAdjust</param>
        /// <param name="tu">The Time Unit of the PerTimeAdjust</param>
        let create n un adj tu =
            match un, adj, tu with
            | Unit.NoUnit, _, _
            | _, Unit.NoUnit, _
            | _, _, Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per adj |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> PerTimeAdjust


        /// Convert the first unit of a PerTimeAdjust (e.g., mg/kg/day -> g/kg/day)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> PerTimeAdjust


        /// Convert the time unit of a PerTimeAdjust (e.g., mg/kg/day -> mg/kg/week)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> PerTimeAdjust


        let setNearestValue vu = toOrdVar >> setNearestValue vu >> PerTimeAdjust


        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a PerTimeAdjust (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a PerTimeAdjust as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar

        /// Get a ValueUnit string representation of a PerTimeAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a PerTimeAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a PerTimeAdjust has constraints
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a PerTimeAdjust is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a PerTimeAdjust
        let apply f = toOrdVar >> f >> PerTimeAdjust


        /// Apply the constraints of a PerTimeAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> PerTimeAdjust


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> PerTimeAdjust


        /// Check whether a PerTimeAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a PerTimeAdjust is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the variable to its nth value, counted from 0.
        let setNthValue nth = toOrdVar >> setNthValue nth >> PerTimeAdjust


        /// Set a PerTimeAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> PerTimeAdjust


        let hasNormValue = toOrdVar >> getNormValue >> Option.isSome


        let setNormValue = toOrdVar >> setNormValue >> PerTimeAdjust


    /// Type and functions that represent an adjusted rate,
    /// i.e., an amount per adjust unit per time (for example, mL/kg/h)
    module RateAdjust =


        [<Literal>]
        let name = "rte_adj"


        /// Get the OrderVariable in a RateAdjust
        let toOrdVar (RateAdjust rte_adj) = rte_adj


        /// Create a Dto for a RateAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a RateAdjust from a Dto
        let fromDto dto =
            dto |> Dto.fromDto |> (map ValueUnit.correctAdjustOrder >> RateAdjust)


        /// Replaces a RateAdjust with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar (map ValueUnit.correctAdjustOrder >> RateAdjust)


        /// <summary>
        /// Create a RateAdjust with name n
        /// </summary>
        /// <param name="n">The Name of the RateAdjust</param>
        /// <param name="un">The Unit of the RateAdjust</param>
        /// <param name="adj">The Adjust Unit of the RateAdjust</param>
        /// <param name="tu">The Time Unit of the RateAdjust</param>
        let create n un adj tu =
            match un, adj, tu with
            | Unit.NoUnit, _, _
            | _, Unit.NoUnit, _
            | _, _, Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per adj |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> RateAdjust


        /// Convert the first unit of a RateAdjust (e.g., mL/kg/h -> L/kg/h)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> RateAdjust


        /// Convert the time unit of a RateAdjust (e.g., mL/kg/h -> mL/kg/min)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> RateAdjust


        /// Turn a RateAdjust to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a RateAdjust (including constraints)
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints true false)


        /// Gets the value set of a RateAdjust as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a RateAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a RateAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a RateAdjust has constraints
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a RateAdjust is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc


        /// Apply a function to the OrderVariable in a RateAdjust
        let apply f = toOrdVar >> f >> RateAdjust


        /// Apply the constraints of a RateAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> RateAdjust


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> RateAdjust


        /// Check whether a RateAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a RateAdjust is solved
        let isSolved = toOrdVar >> isSolved


        /// Set the variable to its nth value, counted from 0.
        let setNthValue nth = toOrdVar >> setNthValue nth >> RateAdjust


        /// Set a RateAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> RateAdjust


        let hasNormValue = toOrdVar >> getNormValue >> Option.isSome


        let setNormValue = toOrdVar >> setNormValue >> RateAdjust


    /// Type and functions that represent an adjusted total,
    /// i.e., an amount per adjust unit (for example, mL/kg)
    module TotalAdjust =


        [<Literal>]
        let name = "tot_adj"


        /// Get the OrderVariable in a TotalAdjust
        let toOrdVar (TotalAdjust tot_adj) = tot_adj


        /// Create a Dto for a TotalAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a TotalAdjust from a Dto
        let fromDto dto = dto |> Dto.fromDto |> TotalAdjust


        /// Replaces a TotalAdjust with the OrderVariable of the same name from a list, when the list has one.
        let fromOrdVar = fromOrdVar toOrdVar TotalAdjust


        /// <summary>
        /// Create a TotalAdjust with name n
        /// </summary>
        /// <param name="n">The Name of the TotalAdjust</param>
        /// <param name="un">The Unit of the TotalAdjust</param>
        /// <param name="adj">The Adjust Unit of the TotalAdjust</param>
        let create n un adj =
            match un, adj with
            | Unit.NoUnit, _
            | _, Unit.NoUnit -> Unit.NoUnit
            | _ -> un |> ValueUnit.per adj
            |> createNew (n |> Name.add name)
            |> TotalAdjust


        /// Convert the first unit of a TotalAdjust (e.g., mL/kg -> L/kg)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> TotalAdjust


        /// Turn a TotalAdjust to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a TotalAdjust with exact values and without the constraints
        let toStringWithConstraints = toOrdVar >> (toStringWithConstraints false true)

        /// Gets the value set of a TotalAdjust as a Dutch string; None without a value set.
        let toValueUnitStringList = toValueUnitStringList toOrdVar

        /// Get a ValueUnit string representation of a TotalAdjust
        let toValueUnitString = toValueUnitString toOrdVar

        /// Get a ValueUnit Markdown representation of a TotalAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints

        /// Check whether a TotalAdjust is within its constraints
        let isWithinConstraints useCalc = toOrdVar >> isWithinConstraints useCalc

        /// Apply a function to the OrderVariable in a TotalAdjust
        let apply f = toOrdVar >> f >> TotalAdjust


        /// Apply the constraints of a TotalAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> TotalAdjust


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> TotalAdjust


        /// Check whether a TotalAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Check whether a TotalAdjust is solved
        let isSolved = toOrdVar >> isSolved

        /// Set a TotalAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> TotalAdjust
