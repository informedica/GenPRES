namespace Informedica.GenOrder.Lib


/// Helper functions to
/// facilitate the use of the
/// `Informedica.GenUnits.Lib`
module ValueUnit =

    open MathNet.Numerics
    open Informedica.GenUnits.Lib
    open ValueUnit

    /// <summary>
    /// Return a Unit as a short Dutch string
    /// </summary>
    let unitToString =
        Units.toString Units.Dutch Units.Short


    /// <summary>
    /// Check if a Unit is an adjust unit, i.e.,
    /// kg or m2.
    /// </summary>
    /// <param name="u">The Unit</param>
    let isAdjust (u : Unit) =
        u |> Group.eqsGroup Units.Weight.kiloGram ||
        u |> Group.eqsGroup Units.BSA.m2


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
        | [u1; u2; u3] when u3 |> isAdjust ->
            u1
            |> Units.per u3 |> Units.per u2
            |> withValue v
        | _ -> vu



    /// <summary>
    /// Collect an array of ValueUnits into one ValueUnit
    /// Return None when the array is empty
    /// </summary>
    /// <param name="vus">The array of ValueUnits</param>
    /// <returns>An optional ValueUnit</returns>
    let collect (vus : ValueUnit[]) =
        match vus |> Array.tryHead with
        | None    -> None
        | Some vu ->
            let u = vu |> getUnit
            vus
            |> Array.collect (toBase >> getValue)
            |> withUnit u
            |> toUnit
            |> Some


    /// <summary>
    /// Get a standard frequency ValueUnit
    /// </summary>
    /// <param name="u">The full unit of the frequency</param>
    let toStandardFrequency u =
        match u |> getUnits with
        | [_; tu] ->
            match tu with
            | _ when tu |> Units.eqsUnit Units.Time.day   -> [| 1N .. 24N |] |> Some
            | _ when tu |> Units.eqsUnit Units.Time.week  -> [| 1N .. 7N |]  |> Some
            | _ when tu |> Units.eqsUnit Units.Time.month -> [| 1N .. 4N |]  |> Some
            | _ when tu |> Units.eqsUnit Units.Time.year  -> [| 1N .. 12N |] |> Some
            | _ -> None
            |> Option.map (withUnit u)

        | _ -> None


    module Units =

        let noUnit = NoUnit


module Variable =


    /// Helper functions to facilitate the use of the
    /// `Informedica.GenSolver.Lib.Variable.ValueRange` type
    module ValueRange =

        open Informedica.GenSolver.Lib.Variable.ValueRange
        open Informedica.Utils.Lib
        open ConsoleWriter.NewLineNoTime


        let inline private setOpt aOption set vr =
            try
                match aOption with
                | Some m -> vr |> set m
                | None   -> vr
            with
            | e ->
                writeErrorMessage  $"couldn't set {aOption} to {vr}"
                vr // TODO: ugly fix need to refactor


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


    open Informedica.GenSolver.Lib
    module ValueSet = Variable.ValueRange.ValueSet


    /// <summary>
    /// Replace the Values of a Variable with a ValueSet
    /// </summary>
    /// <param name="vu">The ValueUnit to set as a ValueSet</param>
    /// <param name="var">The Variable to set the ValueSet to</param>
    let replaceValuesWithValueUnit vu (var : Variable) =
        { var with
            Values =
                vu
                |> ValueSet.create
                |> ValSet
        }


/// Functions that deal with the `OrderVariable` type
module OrderVariable =


    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenSolver.Lib
    open Informedica.GenUnits.Lib


    module ValueRange = Variable.ValueRange
    module Minimum    = ValueRange.Minimum
    module Maximum    = ValueRange.Maximum
    module Increment  = ValueRange.Increment
    module ValueSet   = ValueRange.ValueSet

    module VariableDto = Variable.Dto
    module Units = ValueUnit.Units
    module Multipliers = ValueUnit.Multipliers


    module Constraints =


        /// <summary>
        /// Create a `Constraints` record
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


        /// Get the fields of a `Constraints` record
        let get (cs : Constraints) =
            cs.Min,
            cs.Incr,
            cs.Max,
            cs.Values


        /// Check whether a `Constraints` record is empty
        let isEmpty (cs: Constraints) =
            cs.Incr.IsNone &&
            cs.Max.IsNone &&
            cs.Min.IsNone &&
            cs.Values.IsNone


        /// Check whether a `Constraints` record is non-zero positive
        let isNonZeroPositive (cs: Constraints) =
            cs.Max.IsNone &&
            cs.Incr.IsNone &&
            cs.Values.IsNone &&
            cs.Min
            |> Option.map Minimum.isNonZeroPositive
            |> Option.defaultValue false


        /// <summary>
        /// Create a `ValueRange` from a `Constraints` record
        /// that only has an Increment and/or a Maximum
        /// </summary>
        let toIncrMaxRange (cs : Constraints) =
            ValueRange.unrestricted
            |> ValueRange.setOptMax cs.Max
            |> ValueRange.setOptIncr cs.Incr


        /// <summary>
        /// Create a `ValueRange` from a `Constraints` record
        /// </summary>
        let toValueRange (cs : Constraints) =
            ValueRange.unrestricted
            |> ValueRange.setOptMin cs.Min
            |> ValueRange.setOptMax cs.Max
            |> ValueRange.setOptIncr cs.Incr
            // only set a ValueSet if there is no increment
            |> fun vr ->
                if cs.Incr.IsSome then vr
                else
                    vr
                    |> ValueRange.setOptVs cs.Values


        /// Get the string representation of a `ValueRange` from a `Constraints` record
        let toValueRangeString = toValueRange >> ValueRange.toString false


        /// <summary>
        /// Get the string representation of a `Constraints` record (in Dutch)
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
        let toString (cs : Constraints) =
            let toStr = ValueUnit.toStringDutchShort

            match cs.Values with
            | None ->
                let min = cs.Min |> Option.map Minimum.toBoolValueUnit
                let max = cs.Max |> Option.map Maximum.toBoolValueUnit

                MinMax.Calculator.toStringNL toStr min max
            | Some vs ->
                vs
                |> ValueSet.toValueUnit
                |> ValueUnit.toStringDutchShort


        /// <summary>
        /// Get a min-max string representation of a `Constraints` record (in Dutch)
        /// </summary>
        /// <param name="prec">The precision for decimal values</param>
        /// <param name="cs">The Constraints record</param>
        let toMinMaxString prec (cs : Constraints) =
            let toStr = ValueUnit.toStringDecimalDutchShortWithPrec prec
            let toVal =
                ValueUnit.getValue
                >> Array.tryHead
                >> Option.map BigRational.toDecimal
                >> Option.map (Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3)
                >> Option.defaultValue ""

            let times0_90 = (90N/100N) |> ValueUnit.singleWithUnit Units.Count.times
            let times1_10 = (11N/10N) |> ValueUnit.singleWithUnit Units.Count.times

            match cs.Min |> Option.map Minimum.toValueUnit,
                  cs.Max |> Option.map Maximum.toValueUnit with
            | Some min, Some max ->
                if min |> ValueUnit.eqs max then $"{min |> toStr}"
                else
                    if min / times0_90 = max / times1_10 then
                        $"{min / times0_90 |> toStr}"
                    else
                        $"{min |> toVal} - {max |> toStr}"
            | _ -> ""


        /// <summary>
        /// Map the functions, fMin, fMax, fIncr and fVals over the `Constraints` record
        /// </summary>
        /// <param name="fMin">The function to map the Min</param>
        /// <param name="fMax">The function to map the Max</param>
        /// <param name="fIncr">The function to map the Incr</param>
        /// <param name="fVals">The function to map the Values</param>
        /// <param name="cs">The Constraints record</param>
        let map fMin fMax fIncr fVals (cs : Constraints) =
            {
                Min = cs.Min |> Option.map fMin
                Max = cs.Max |> Option.map fMax
                Incr = cs.Incr |> Option.map fIncr
                Values = cs.Values |> Option.map fVals
            }


        /// Get the Unit from a `Constraints` record
        let getUnit (cs : Constraints) =
            cs
            |> toValueRange
            |> ValueRange.getUnit


    /// <summary>
    /// Create an OrderVariable, an OrderVariable is a Variable with Constraints
    /// </summary>
    /// <param name="n">The Name of the Variable</param>
    /// <param name="min">An optional Minimum of the Variable</param>
    /// <param name="incr">An optional Increment of the Variable</param>
    /// <param name="max">An optional Maximum of the Variable</param>
    /// <param name="vs">An optional ValueSet of the Variable</param>
    /// <param name="cs">The Constraints for the OrderVariable</param>
    let create n min incr max vs cs =
        ValueRange.create min incr max vs
        |> fun vlr ->
            let var = Variable.create id n vlr
            {
                Constraints = cs
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

        Constraints.create min None None None
        |> create n min None None None


    /// <summary>
    /// Map a function f to the Values (i.e., ValueUnit) of the Variable
    /// of the OrderVariable
    /// </summary>
    /// <param name="f">The function to map</param>
    /// <param name="ovar">The OrderVariable</param>
    let map f (ovar: OrderVariable) =
        { ovar with
            Variable =
                { ovar.Variable with
                    Values =
                        ovar.Variable.Values
                        |> ValueRange.mapValueUnit f
                }

        }


    /// Get the `Variable` from an OrderVariable
    let getVar { Variable = var } = var


    /// Set the `Variable` of an OrderVariable
    let setVar var (ovar : OrderVariable) =
        { ovar with
            Variable = var
        }


    /// Get the optional `ValueUnit` from the `ValueSet` of the `Variable`
    let getValSetValueUnit ovar =
        ovar
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.getValSet
        |> Option.map ValueSet.toValueUnit


    /// Check whether two OrderVariables have the same unit group
    /// Note: then they can be added or subtracted
    let eqsUnitGroup ovar1 ovar2 =
        let var1, var2 = ovar1 |> getVar, ovar2 |> getVar
        var1 |> Variable.eqsUnitGroup var2


    /// Get the `Variable.Name` from an OrderVariable
    let getName ovar = (ovar |> getVar).Name


    /// Check whether two OrderVariables have the same name
    let eqsName ovar1 ovar2 = (ovar1 |> getName) = (ovar2 |> getName)


    /// Get the names of a list for OrderVariables as a comma-separated string
    let getNames ovars =
        ovars
        |> List.map _.Variable
        |> List.map _.Name
        |> List.map Name.toString
        |> String.concat ", "


    /// Get the `Constraints` from an OrderVariable
    let getConstraints { Constraints = cons } = cons


    /// Check whether an OrderVariable has Constraints that
    /// are not non-zero positive and not empty
    let hasConstraints =
        getConstraints
        >> fun cs ->
           cs |> Constraints.isNonZeroPositive |> not &&
           cs |> Constraints.isEmpty |> not


    /// Check whether an OrderVariable has a Maximum or a ValueSet
    let hasMaxConstraint ovar =
        ovar
        |> getConstraints
        |> Constraints.get
        |> function
            | _, _, Some _, _
            | _, _, _, Some _ -> true
            | _ -> false


    /// <summary>
    /// Set the `Constraints` of an OrderVariable
    /// </summary>
    /// <param name="cs">The Constraints to set</param>
    /// <param name="ovar">The OrderVariable</param>
    let setConstraints cs (ovar :OrderVariable) =
        { ovar with
            Constraints = cs
        }


    /// Apply only the maximum constraints of an OrderVariable
    /// If there are no constraints, then the Variable is set to
    /// non-zero positive
    let applyOnlyMaxConstraints (ovar : OrderVariable) =
        { ovar with
            Variable =
                if ovar.Constraints |> Constraints.isEmpty then
                    ovar.Variable
                    |> Variable.setNonZeroAndPositive
                else
                    { ovar.Variable with
                        Values = ovar.Constraints |> Constraints.toIncrMaxRange
                    }
        }



    /// <summary>
    /// Apply the constraints of an OrderVariable to the Variable
    /// of the OrderVariable.
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    /// <remarks>
    /// If the Constraints have an Increment and a ValueSet, then
    /// only the Increment is applied to the Variable.
    /// </remarks>
    let applyConstraints (ovar : OrderVariable) =
        { ovar with
            Variable =
                if ovar.Constraints |> Constraints.isEmpty then
                    { ovar.Variable with
                        Values =
                            ValueRange.unrestricted
                            |> ValueRange.nonZeroAndPositive
                    }
                else
                    { ovar.Variable with
                        Values = ovar.Constraints |> Constraints.toValueRange
                    }
        }


    /// Check whether the Values of the Variable of an OrderVariable
    /// are within the Constraints of the OrderVariable
    let isWithinConstraints ovar =
        if ovar |> hasConstraints |> not then true
        else
            let cs = ovar.Constraints |> Constraints.toValueRange
            ovar.Variable.Values
            |> ValueRange.isSubSetOf cs


    /// Check whether the `ValueRange` of the `Variable` of an
    /// `OrderVariable` is a `MinIncrMax`, i.e. has an increment
    let hasIncrement (ovar: OrderVariable) =
        ovar.Variable
        |> Variable.isMinIncrMax


    /// <summary>
    /// Try and increase the increment of a `ValueRange` of a Variable to an
    /// increment in incrs such that the resulting ValueRange contains
    /// at most maxCount values.
    /// </summary>
    /// <param name="maxCount">The maximum count</param>
    /// <param name="incrs">The increment list</param>
    /// <param name="ovar">The OrderVariable</param>
    /// <returns>The resulting (more restrictive) `ValueRange`</returns>
    /// <remarks>
    /// When there is no increment in the list that can be used to increase
    /// the increment of the ValueRange to the maximum count, the largest possible
    /// increment is used.
    /// </remarks>
    let increaseIncrement maxCount incrs (ovar : OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable
                |> Variable.increaseIncrement maxCount incrs
        }


    /// Helper function to map an OrderVariable in a list
    /// of OrderVariables to a 'c' with a default of 'a'.
    /// The mapped value will be returned or default 'a'.
    let private fromOrdVar toOvar c ovars a =
        ovars
        |> List.tryFind (eqsName (a |> toOvar))
        |> Option.map c
        |> Option.defaultValue a


    /// Set the 'Name' to the `Variable` of the `OrderVariable`.
    let setName n ovar =
        { ovar with
            Variable = ovar.Variable |> Variable.setName n
        }


    /// <summary>
    /// Convert the `Variable` of an `OrderVariable` to a given `Unit`.
    /// Note the first unit is replaced by the given unit; all other
    /// units are kept.
    /// Example: mg/kg/day to g/kg/day when u = g
    /// </summary>
    /// <param name="u">The Unit to convert to</param>
    /// <param name="ovar">The OrderVariable</param>
    let convertFirstUnit u ovar =
        let u =
            ovar.Variable |> Variable.getUnit
            |> Option.map (fun u1 ->
                if u1 = ZeroUnit || u1 = NoUnit then u1
                else
                    match u1 |> ValueUnit.getUnits with
                    | _ :: rest ->
                        rest
                        |> List.fold (fun acc x ->
                            acc
                            |> Units.per x
                        ) u
                    | _ -> u
            )
            |> Option.defaultValue u

        { ovar with
            Variable =
                ovar.Variable
                |> Variable.convertToUnit u
        }


    /// <summary>
    /// Convert the `Variable` of an `OrderVariable` to a given time `Unit`.
    /// Note the last unit is replaced by the given time unit; all other
    /// units are kept.
    /// Example: mg/kg/day to mg/kg/week when tu = week
    /// </summary>
    /// <param name="tu">The time Unit to convert to</param>
    /// <param name="ovar">The OrderVariable</param>
    let convertTimeUnit tu ovar =
        let u =
            ovar.Variable |> Variable.getUnit
            |> Option.map (fun u1 ->
                if u1 = ZeroUnit || u1 = NoUnit then u1
                else
                    match u1 |> ValueUnit.getUnits |> List.rev with
                    // assume the last unit is a time unit
                    | _ :: rest ->
                        match tu::rest |> List.rev with
                        | u::rest ->
                            rest
                            |> List.fold (fun acc x ->
                                acc
                                |> Units.per x
                            ) u
                        | _ -> tu
                    | _ -> tu
            )
            |> Option.defaultValue tu

        { ovar with
            Variable =
                ovar.Variable
                |> Variable.convertToUnit u
        }


    /// Set the Values of the Variable of an OrderVariable
    /// to the nearest ValueUnit in a given ValueSet
    let setNearestValue vu (ovar: OrderVariable) =
        {
            ovar with
                Variable =
                    ovar.Variable
                    |> Variable.setNearestValue vu
        }


    /// <summary>
    /// Get the string representation of an OrderVariable.
    /// </summary>
    /// <param name="exact">Whether to use the exact representation of the ValueRange</param>
    /// <param name="ovar">The OrderVariable</param>
    /// <param name="withConstraints"></param>
    /// <returns></returns>
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
                    sl
                    |> List.skip 1
                    |> String.concat "."
                    |> sprintf "[%s"

        let vs =
            ovar.Variable
            |> Variable.getValueRange
            |> ValueRange.toString exact

        let cs =
            if ovar.Constraints |> Constraints.isEmpty then ""
            else ovar.Constraints |> Constraints.toValueRangeString

        if cs |> String.isNullOrWhiteSpace ||
           withConstraints |> not then $"{ns} {vs}"
        else $"{ns} {vs} | {cs}"


    /// <summary>
    /// Get the string representation of an OrderVariable.
    /// </summary>
    let toString = toStringWithConstraints false


    /// Helper function to get an optional ValueSet from an OrderVariable
    /// and return the values as a string list.
    let toValueUnitStringList get x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.getValSet
        |> Option.map (ValueSet.toValueUnit >> ValueUnit.toStringDutchShort)


    /// Helper function to get a string representation of the ValueRange of
    /// the Variable of an OrderVariable
    let toValueUnitString get (_ : int) x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> ValueRange.toString false
        |> String.replace "x" "keer"
        // fix for example mg/kg*day to mg/kg/dag, which is mathematically the same
        |> String.replace "*" "/"


    /// Helper function to get a Markdown string representation of the ValueRange of
    /// the Variable of an OrderVariable
    let toValueUnitMarkdown get (prec : int) x =
        x
        |> get
        |> getVar
        |> Variable.getValueRange
        |> function
            | vr when vr |> ValueRange.isMinMax ||
                      vr |> ValueRange.isMinIncrMax ||
                      vr |> ValueRange.isValueSet ->
                vr
                |> ValueRange.toMarkdown prec
                // fix for example mg/kg*day to mg/kg/dag, which is mathematically the same
                |> String.replace "*" "/"
            | _ -> ""


    /// <summary>
    /// Calculate a ValueSet for a Variable of an OrderVariable if the Value
    /// of the Variable is a MinIncrMax
    /// </summary>
    /// <param name="ovar">The OrderVariable to change min incr max to a ValueSet</param>
    /// <param name="n">Prune the ValueSet to max n values</param>
    /// <returns>An OrderVariable with a Variable with a ValueSet if this can be calculated</returns>
    let minIncrMaxToValues n (ovar: OrderVariable) =
        { ovar with
            Variable =
                if ovar.Variable |> Variable.isMinIncrMax |> not then ovar.Variable
                else
                    ovar.Variable
                    |> Variable.minIncrMaxToValues n
        }


    /// Set the minimum value of the Variable of an OrderVariable
    let setMinValue (ovar: OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable |> Variable.setMinValue
        }


    /// Set the maximum value of the Variable of an OrderVariable
    let setMaxValue (ovar: OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable |> Variable.setMaxValue
        }


    /// Set the median value of the Variable of an OrderVariable
    let setMedianValue (ovar: OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable |> Variable.setMedianValue
        }


    let setNthValue nth (ovar: OrderVariable) =
        let min, incr, max, vs =
            ovar.Variable.Values
            |> ValueRange.getMinIncrMaxOrValueSet

        let vr =
            match vs with
            | Some vs ->
                vs
                |> Variable.ValueRange.ValueSet.map (fun vu ->
                    vu
                    |> ValueUnit.applyToValue (fun brs ->
                        brs
                        |> Array.tryItem nth
                        |> Option.map Array.singleton
                        |> Option.defaultValue brs
                    )
                )
                |> ValSet
            | None ->
                match min, incr with
                | Some min, Some incr ->
                    let nthVal =
                        nth - 1
                        |> BigRational.fromInt
                        |> ValueUnit.singleWithUnit Units.Count.times
                        |> ((*) (incr |> Variable.ValueRange.Increment.toValueUnit))
                        |> ((+) (min |> Variable.ValueRange.Minimum.toValueUnit))

                    match max with
                    | None ->
                        nthVal
                        |> Variable.ValueRange.ValueSet.create
                        |> ValSet
                    | Some max ->
                        let nthMax = nthVal |> Variable.ValueRange.Maximum.create true
                        if nthMax |> Variable.ValueRange.Maximum.maxGTmax max then max
                        else nthMax
                        |> Variable.ValueRange.Maximum.toValueUnit
                        |> Variable.ValueRange.ValueSet.create
                        |> ValSet

                | _ -> ovar.Variable.Values

        { ovar with
            OrderVariable.Variable.Values = vr
        }


    /// Set the Values of the Variable of an OrderVariable
    /// to a given value range
    let setValueRange vr (ovar: OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable
                |> Variable.setValueRange vr
        }


    /// Get the indices of the Values of the Variable of an OrderVariable
    /// that are within the Constraints of the OrderVariable
    let getIndices (ovar: OrderVariable) =
        ovar.Variable
        |> Variable.getValueRange
        |> ValueRange.getIndices (ovar.Constraints |> Constraints.toValueRange)


    /// <summary>
    /// Apply the indices to the Values of the Variable of an OrderVariable
    /// </summary>
    /// <param name="indices">The indices to apply</param>
    /// <param name="ovar">The OrderVariable</param>
    let applyIndices (indices: int[]) (ovar: OrderVariable) =
        { ovar with
            OrderVariable.Variable.Values =
                ovar.Constraints
                |> Constraints.toValueRange
                |> ValueRange.pickIndices indices
        }


    /// Check whether the Values of the Variable of an OrderVariable
    /// are non-zero positive or have an exclusive Minimum of zero
    let isNonZeroPositive (ovar: OrderVariable) =
        ovar.Variable
        |> Variable.isNonZeroPositive   ||
        ovar.Variable
        |> Variable.isMinExclusiveZero


    /// Set the Values of the Variable of an OrderVariable
    /// to non-zero positive values
    let setToNonZeroPositive (ovar: OrderVariable) =
        { ovar with
            OrderVariable.Variable.Values =
                match  ovar.Variable.Values |> ValueRange.getUnit with
                | None -> ValueRange.nonZeroPositive
                | Some u ->
                    u
                    |> ValueUnit.zero
                    |> Minimum.create false
                    |> Min

            (* this doesn't work when not empty?
            Variable = Variable
                ovar.Variable |> Variable.setNonZeroAndPositive
            *)
        }


    /// Clear the Values of the Variable of an OrderVariable
    let clear (ovar : OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable |> Variable.clear
        }


    /// Check whether the Values of the Variable of an OrderVariable
    /// are cleared, i.e., unrestricted
    let isCleared (ovar : OrderVariable) =
        ovar.Variable |> Variable.isUnrestricted


    /// <summary>
    /// Set the unit of the Variable of an OrderVariable according to the unit
    /// of the Constraints of the OrderVariable.
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    /// <remarks>
    /// Use the first available Unit.
    /// </remarks>
    let setUnit (ovar: OrderVariable) =
        [
            ovar.Constraints.Min |> Option.map (Minimum.toValueUnit >> ValueUnit.getUnit)
            ovar.Constraints.Max |> Option.map (Maximum.toValueUnit >> ValueUnit.getUnit)
            ovar.Constraints.Values |> Option.map (ValueSet.toValueUnit >> ValueUnit.getUnit)
            ovar.Constraints.Incr |> Option.map (Increment.toValueUnit >> ValueUnit.getUnit)
        ]
        |> List.choose id
        |> List.distinct
        |> List.tryHead
        |> function
            | None -> ovar
            | Some u ->
                { ovar with
                    Variable =
                        ovar.Variable
                        |> Variable.convertToUnit u
                }


    /// Check whether the Values of the Variable of an OrderVariable
    /// are empty, i.e., unrestricted, non-zero positive or have an exclusive Minimum of zero
    let isEmpty (ovar : OrderVariable) =
        ovar.Variable
        |> Variable.isUnrestricted ||
        ovar.Variable
        |> Variable.isNonZeroPositive ||
        ovar.Variable
        |> Variable.isMinExclusiveZero


    /// Check whether the Values of the Variable of an OrderVariable
    /// are set, i.e., have a distinct set of values
    let hasValues (ovar: OrderVariable) =
        ovar.Variable
        |> Variable.hasValues


    /// <summary>
    /// Check whether a Variable is solved
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    let isSolved (ovar : OrderVariable) =
        ovar.Variable
        |> Variable.isSolved ||
        ovar.Variable
        |> Variable.isUnrestricted ||
        ovar.Variable
        |> Variable.isNonZeroPositive


    module Dto =

        open Newtonsoft.Json


        /// The `Dto` data transfer type for an OrderVariable
        type Dto () =
            member val Name = "" with get, set
            member val Constraints = Variable.Dto.dto () with get, set
            member val Variable = Variable.Dto.dto () with get, set


        /// Create a new `Dto` for an OrderVariable
        let dto () = Dto ()


        /// Clean the Variable part of a Dto
        /// by setting all optional fields to None
        let cleanVariable (old : Dto) =
            old.Variable.MinOpt <- None
            old.Variable.IncrOpt <- None
            old.Variable.MaxOpt <- None
            old.Variable.ValsOpt <- None


        /// Create an OrderVariable from a Dto
        let fromDto (dto: Dto) =
            try
                let cs =
                    let vs =
                        dto.Constraints.ValsOpt
                        |> Option.bind ValueUnit.Dto.fromDto
                        |> Option.bind (fun vu ->
                            if vu |> ValueUnit.isEmpty then None
                            else
                                vu
                                |> ValueSet.create
                                |> Some
                        )

                    let incr =
                        dto.Constraints.IncrOpt
                        |> Option.bind ValueUnit.Dto.fromDto
                        |> Option.bind (fun vu ->
                            if vu |> ValueUnit.isEmpty then None
                            else
                                vu
                                |> Increment.create
                                |> Some
                        )

                    let min  = dto.Constraints.MinOpt  |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Minimum.create  dto.Constraints.MinIncl))
                    let max  = dto.Constraints.MaxOpt  |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Maximum.create  dto.Constraints.MaxIncl))

                    Constraints.create min incr max vs

                let n = dto.Name |> Name.fromString
                let vals =
                    dto.Variable.ValsOpt
                    |> Option.bind ValueUnit.Dto.fromDto
                    |> Option.bind (fun vu ->
                        if vu |> ValueUnit.isEmpty then None
                        else
                            vu
                            |> ValueSet.create
                            |> Some
                    )

                let incr =
                    dto.Variable.IncrOpt
                    |> Option.bind ValueUnit.Dto.fromDto
                    |> Option.bind (fun vu ->
                        if vu |> ValueUnit.isEmpty then None
                        else
                            vu
                            |> Increment.create
                            |> Some
                    )

                let min  = dto.Variable.MinOpt  |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Minimum.create  dto.Variable.MinIncl))
                let max  = dto.Variable.MaxOpt  |> Option.bind (ValueUnit.Dto.fromDto >> Option.map (Maximum.create  dto.Variable.MaxIncl))

                create n min incr max vals cs
            with
            | e ->
                writeErrorMessage $"cannot create OrderVariable fromDto: {dto |> JsonConvert.SerializeObject}"
                e |> raise


        /// Create a Dto from an OrderVariable
        let toDto (ovar : OrderVariable) =
            let vuToDto = ValueUnit.Dto.toDto true "dutch"
            let dto = dto ()
            let vr =
                ovar
                |> getVar
                |> Variable.getValueRange

            dto.Name <-
                ovar |> getName |> Name.toString

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

            dto.Variable.MinOpt <-
                vr
                |> ValueRange.getMin
                |> Option.map Minimum.toValueUnit
                |> Option.bind vuToDto

            dto.Variable.MinIncl <-
                vr
                |> ValueRange.getMin
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false

            dto.Variable.MaxOpt <-
                vr
                |> ValueRange.getMax
                |> Option.map Maximum.toValueUnit
                |> Option.bind vuToDto

            dto.Variable.MaxIncl <-
                vr
                |> ValueRange.getMax
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto.Constraints.ValsOpt <-
                ovar.Constraints.Values
                |> Option.map ValueSet.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.IncrOpt <-
                ovar.Constraints.Incr
                |> Option.map Increment.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MinOpt <-
                ovar.Constraints.Min
                |> Option.map Minimum.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MinIncl <-
                ovar.Constraints.Min
                |> Option.map Minimum.isIncl
                |> Option.defaultValue false

            dto.Constraints.MaxOpt <-
                ovar.Constraints.Max
                |> Option.map Maximum.toValueUnit
                |> Option.bind vuToDto

            dto.Constraints.MaxIncl <-
                ovar.Constraints.Max
                |> Option.map Maximum.isIncl
                |> Option.defaultValue false

            dto


    /// Type and functions that represent a count
    module Count =


        /// Create a Count from an OrderVariable
        let count = Count.Count


        let [<Literal>] name = "cnt"


        /// Get the OrderVariable in a Count
        let toOrdVar (Count.Count cnt) = cnt


        /// Create a Dto for a Count
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Count from a Dto
        let fromDto dto = dto |> Dto.fromDto |> count


        /// Set a `Count` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar = fromOrdVar toOrdVar count


        /// Create a `Count` with name n
        let create n =
            Units.Count.times
            |> createNew (n |> Name.add name)
            |> Count.Count


        /// Turn a `Count` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `Count` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Count
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Count
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Check whether a Count has Constraints that
        /// are not non-zero positive and not empty
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Count is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a Count to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> count


        /// Check whether a Count is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a Count is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a Count is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a Count
        let clear = toOrdVar >> clear >> count


        /// Set a Count to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> count


    /// Type and functions that represent a time
    module Time =


        /// Create a Time from an OrderVariable
        let time = Time.Time


        let [<Literal>] name = "tme"


        /// Get the OrderVariable in a Time
        let toOrdVar (Time.Time tme) = tme


        /// Create a Dto for a Time
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Time from a Dto
        let fromDto dto = dto |> Dto.fromDto |> time


        /// Set a `Time` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar = fromOrdVar toOrdVar time


        /// <summary>
        /// Create a Time
        /// </summary>
        /// <param name="n">The Name of the Time</param>
        /// <param name="un">The Unit of the Time</param>
        let create n un =
            un
            |> createNew (n |> Name.add name)
            |> Time.Time


        /// Turn a `Time` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `Time` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `Time` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Time
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Time
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a Time has Constraints that
        /// are not non-zero positive and not empty
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether Time is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a Time to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> time


        /// Check whether Time is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether Time is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether Time is cleared
        let isCleared = toOrdVar >> isCleared


        /// Set the minimum value for Time
        let setMinValue = toOrdVar >> setMinValue >> time


        /// Set the maximum value for Time
        let setMaxValue = toOrdVar >> setMaxValue >> time


        /// Set the median value for Time
        let setMedianValue = toOrdVar >> setMedianValue >> time


        /// Clear the values of Time
        let clear = toOrdVar >> clear >> time


        /// Set Time to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> time


    /// Type and functions that represent a frequency
    module Frequency =


        let [<Literal>] name = "frq"


        /// Get the OrderVariable in a Frequency
        let toOrdVar (Frequency frq) = frq


        /// Create a Dto for a Frequency
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Frequency from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Frequency


        /// Set a `Frequency` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar = fromOrdVar toOrdVar Frequency


        let apply f = toOrdVar >> f >> Frequency


        /// <summary>
        /// Create a Frequency
        /// </summary>
        /// <param name="n">The Name of the Frequency</param>
        /// <param name="tu">The Time Unit of the Frequency</param>
        let create n tu =
            match tu with
            | Unit.NoUnit -> Unit.NoUnit
            | _ ->
                Units.Count.times
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> Frequency


        /// Turn a `Frequency` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `Frequency` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `Frequency` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Frequency
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Frequency
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a Frequency has Constraints that
        /// are not non-zero positive and not empty
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Frequency is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a Frequency to the OrderVariable Variable
        let applyConstraints = apply applyConstraints

        /// Check whether a Frequency is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Set the `Constraints` of a Frequency
        let setConstraints cs = apply (setConstraints cs)

        /// Set the Values of the Variable of a Frequency
        /// to the minimum value
        let setMinValue = apply setMinValue

        /// Set the Values of the Variable of a Frequency
        /// to the maximum value
        let setMaxValue = apply setMaxValue

        /// Set the Values of the Variable of a Frequency
        /// to the median value
        let setMedianValue = apply setMedianValue

        /// Set the Values (or use the increment)
        /// to the nth value (if not > max)
        let setNthValue nth = apply (setNthValue nth)


    /// Set standard frequency values based on the time unit (e.g., per day/week)
        let setStandardValues frq =
            let oldCs =
                frq
                |> toOrdVar
                |> getConstraints

            let newCs =
                oldCs
                |> Constraints.getUnit
                |> Option.bind (fun u ->
                    u
                    |> ValueUnit.toStandardFrequency
                    |> Option.map Variable.ValueRange.ValueSet.create
                )
                |> function
                | Some vu ->
                    vu
                    |> Some
                    |> Constraints.create None None None
                | None -> oldCs

            frq
            |> setConstraints newCs
            |> applyConstraints
            |> setConstraints oldCs


        /// Check whether a Frequency is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a Frequency is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a Frequency
        let clear = apply clear


        /// Set a Frequency to non-zero positive values
        let setToNonZeroPositive = apply setToNonZeroPositive


    /// Type and functions that represent a concentration,
    /// and a concentration is a quantity per time
    module Concentration =


        let [<Literal>] name = "cnc"


        /// Get the OrderVariable in a Concentration
        let toOrdVar (Concentration cnc) = cnc


        /// Create a Dto for a Concentration
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Concentration from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Concentration


        /// Set a `Concentration` with an OrderVariable
        /// in a list of OrderVariables.
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
            | _ ->
                un
                |> ValueUnit.per su
            |> createNew (n |> Name.add name)
            |> Concentration


        /// Turn a `Concentration` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `Concentration` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `Concentration` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Concentration
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Concentration
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints


    /// Check whether a Concentration is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a Concentration to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Concentration


        /// Apply selected indices to the values of a Concentration
        let applyIndices indices =
            toOrdVar >> applyIndices indices >> Concentration


        /// Check whether a Concentration is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a Concentration is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a Concentration is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a Concentration
        let clear = toOrdVar >> clear >> Concentration


        /// Set the minimum value for a Concentration
        let setMinValue = toOrdVar >> setMinValue >> Concentration


        /// Set the maximum value for a Concentration
        let setMaxValue = toOrdVar >> setMaxValue >> Concentration


        /// Set the median value for a Concentration
        let setMedianValue = toOrdVar >> setMedianValue >> Concentration


        /// Set the Values (or use the increment)
        /// to the nth value (if not > max)
        let setNthValue nth = toOrdVar >> setNthValue nth >> Concentration


        /// Set a Concentration to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Concentration


    /// Type and functions that represent a quantity
    module Quantity =


        let [<Literal>] name = "qty"


        /// Get the OrderVariable in a Quantity
        let toOrdVar (Quantity qty) = qty


        /// Create a Dto for a Quantity
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Quantity from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Quantity


        /// Set a `Quantity` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar = fromOrdVar toOrdVar Quantity


        /// <summary>
        /// Create a Quantity with name n
        /// </summary>
        /// <param name="n">The Name of the Quantity</param>
        /// <param name="un">The Unit of the Quantity</param>
        let create n un =
            un
            |> createNew (n |> Name.add name)
            |> Quantity


        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> Quantity


        /// Turn a `Quantity` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `Quantity` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `Quantity` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Quantity
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Quantity
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Quantity is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> Quantity


        /// Apply the constraints of a Quantity to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Quantity


        let applyIndices indices =
            toOrdVar >> applyIndices indices >> Quantity


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
        let increaseIncrement maxCount incrs =
            toOrdVar >> increaseIncrement maxCount incrs >> Quantity


        /// Check whether a Quantity is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a Quantity is cleared
        let isCleared = toOrdVar >> isCleared


        /// Set the minimum value for a Quantity
        let setMinValue = toOrdVar >> setMinValue >> Quantity


        /// Set the maximum value for a Quantity
        let setMaxValue = toOrdVar >> setMaxValue >> Quantity


        /// Set the median value for a Quantity
        let setMedianValue = toOrdVar >> setMedianValue >> Quantity


        /// Set the Values (or use the increment)
        /// to the nth value (if not > max)
        let setNthValue nth = toOrdVar >> setNthValue nth >> Quantity


        /// Clear the values of a Quantity
        let clear = toOrdVar >> clear >> Quantity


        /// Set a Quantity to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Quantity


        /// Materialize Min/Incr/Max of a Quantity into a ValueSet
        let minIncrMaxToValues = toOrdVar >> minIncrMaxToValues None >> Quantity


    /// Type and functions that represent a quantity per time
    module PerTime =


        let [<Literal>] name = "ptm"


        /// Get the OrderVariable in a PerTime
        let toOrdVar (PerTime ptm) = ptm


        /// Create a Dto for a PerTime
        let toDto = toOrdVar >> Dto.toDto


        /// Create a PerTime from a Dto
        let fromDto dto = dto |> Dto.fromDto |> PerTime


        /// Set a `PerTime` with an OrderVariable
        /// in a list of OrderVariables.
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
            | _ ->
                un
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> PerTime


        /// Convert the first unit of a PerTime (e.g., mg/kg/day -> g/kg/day)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> PerTime


        /// Convert the time unit of a PerTime (e.g., mg/kg/day -> mg/kg/week)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> PerTime


        /// Turn a `PerTime` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `PerTime` value (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `PerTime` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a PerTime
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a PerTime
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Check whether a PerTime has constraints
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a PerTime is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a PerTime to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> PerTime


        /// Check whether a PerTime is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a PerTime is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a PerTime is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a PerTime
        let clear = toOrdVar >> clear >> PerTime


        /// Set a PerTime to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> PerTime


        /// Materialize Min/Incr/Max of a PerTime into a ValueSet
        let minIncrMaxToValues = toOrdVar >> minIncrMaxToValues None >> PerTime


    module Rate =


        let [<Literal>] name = "rte"


        /// Get the OrderVariable in a Rate
        let toOrdVar (Rate rte) = rte


        /// Create a Dto for a Rate
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Rate from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Rate


        /// Set a `Rate` with an OrderVariable
        /// in a list of OrderVariables.
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
            | _ ->
                un
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> Rate


        /// Convert the first unit of a Rate (e.g., mg/h -> g/h)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> Rate


        /// Convert the time unit of a Rate (e.g., mg/h -> mg/min)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> Rate


        /// Turn a `Rate` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `Rate` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `Rate` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Rate
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Rate
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a Rate is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a Rate to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Rate


        /// Set constraints for a Rate
        let setConstraints cons = toOrdVar >> setConstraints cons >> Rate


        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Materialize Min/Incr/Max of a Rate into a ValueSet
        let minIncrMaxToValues = toOrdVar >> minIncrMaxToValues None >> Rate


        /// <summary>
        /// Increase the increment of a Rate until the resulting ValueRange
        /// contains at most maxCount values.
        /// </summary>
        /// <param name="maxCount">The maximum number of values in the ValueRange</param>
        /// <param name="incrs">The list of increments to choose from</param>
        let increaseIncrement maxCount incrs =
            toOrdVar >> increaseIncrement maxCount incrs >> Rate


        /// Check whether a Rate is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a Rate is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a Rate
        let clear = toOrdVar >> clear >> Rate


        /// Set the minimum value for a Rate
        let setMinValue = toOrdVar >> setMinValue >> Rate


        /// Set the maximum value for a Rate
        let setMaxValue = toOrdVar >> setMaxValue >> Rate


        /// Set the median value for a Rate
        let setMedianValue = toOrdVar >> setMedianValue >> Rate


        /// Set the Values (or use the increment)
        /// to the nth value (if not > max)
        let setNthValue nth = toOrdVar >> setNthValue nth >> Rate


        /// Set a Rate to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Rate


    /// Type and functions that represent a total
    module Total =


        let [<Literal>] name = "tot"


        /// Get the OrderVariable in a Total
        let toOrdVar (Total tot) = tot


        /// Create a Dto for a Total
        let toDto = toOrdVar >> Dto.toDto


        /// Create a Total from a Dto
        let fromDto dto = dto |> Dto.fromDto |> Total


        /// Set a `Total` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar = fromOrdVar toOrdVar Total


        /// <summary>
        /// Create a Total with name n
        /// </summary>
        /// <param name="n">The Name of the Total</param>
        /// <param name="un">The Unit of the Total</param>
        let create n un =
            un
            |> createNew (n |> Name.add name)
            |> Total


        /// Convert the first unit of a Total (e.g., mg -> g)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> Total


        /// Turn a `Total` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `Total` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> toStringWithConstraints true false


        /// Get a `Total` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Total
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a Total
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a Total has constraints
        let hasConstraints = toOrdVar >> hasConstraints

        /// Check whether a Total is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a Total to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Total

        /// Check whether a Total is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Check whether a Total is solved
        let isSolved = toOrdVar >> isSolved

        /// Check whether a Total is cleared
        let isCleared = toOrdVar >> isCleared


        let clear = toOrdVar >> clear >> Total

        /// Set a Total to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> Total

        /// Materialize Min/Incr/Max of a Total into a ValueSet
        let minIncrMaxToValues = toOrdVar >> minIncrMaxToValues None >> Total


    /// Type and functions that represent an adjusted quantity,
    /// i.e., an amount per adjust unit (for example, mg/kg)
    module QuantityAdjust =


        let [<Literal>] name = "qty_adj"


        /// Get the OrderVariable in a QuantityAdjust
        let toOrdVar (QuantityAdjust qty_adj) = qty_adj


        /// Create a Dto for a QuantityAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a QuantityAdjust from a Dto
        let fromDto dto = dto |> Dto.fromDto |> QuantityAdjust


        /// Set a `QuantityAdjust` with an OrderVariable
        /// in a list of OrderVariables.
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
            | _ ->
                un
                |> ValueUnit.per adj
            |> createNew (n |> Name.add name)
            |> QuantityAdjust


        /// Convert the first unit of a QuantityAdjust (e.g., mg/kg -> g/kg)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> QuantityAdjust

        /// Set the nearest value of a QuantityAdjust to a given ValueUnit
        let setNearestValue vu = toOrdVar >> setNearestValue vu >> QuantityAdjust


        /// Turn a `QuantityAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `QuantityAdjust` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)

        /// Get a `QuantityAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar

        /// Get a ValueUnit string representation of a QuantityAdjust
        let toValueUnitString = toValueUnitString toOrdVar

        /// Get a ValueUnit Markdown representation of a QuantityAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        let hasConstraints = toOrdVar >> hasConstraints

        /// Check whether a QuantityAdjust has a maximum or a ValueSet constraint
        let hasMaxConstraint = toOrdVar >> hasMaxConstraint

        /// Check whether a QuantityAdjust is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints

        let applyOnlyMaxConstraints = toOrdVar >> applyOnlyMaxConstraints >> QuantityAdjust

        /// Apply the constraints of a QuantityAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> QuantityAdjust

        /// Check whether a QuantityAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Check whether a QuantityAdjust is solved
        let isSolved = toOrdVar >> isSolved

        /// Check whether a QuantityAdjust is cleared
        let isCleared = toOrdVar >> isCleared

        /// Clear the values of a QuantityAdjust
        let clear = toOrdVar >> clear >> QuantityAdjust

        /// Set a QuantityAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> QuantityAdjust



    /// Type and functions that represent an adjusted per-time value,
    /// i.e., an amount per adjust unit per time (for example, mg/kg/day)
    module PerTimeAdjust =


        let [<Literal>] name = "ptm_adj"


        /// Get the OrderVariable in a PerTimeAdjust
        let toOrdVar (PerTimeAdjust ptm_adj) = ptm_adj


        /// Create a Dto for a PerTimeAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a PerTimeAdjust from a Dto
        let fromDto dto =
            dto
            |> Dto.fromDto
            |> (map ValueUnit.correctAdjustOrder >> PerTimeAdjust)


        /// Set a `PerTimeAdjust` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar =
            fromOrdVar
                toOrdVar
                (map ValueUnit.correctAdjustOrder >> PerTimeAdjust)


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
            | _ ->
                un
                |> ValueUnit.per adj
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> PerTimeAdjust


        /// Convert the first unit of a PerTimeAdjust (e.g., mg/kg/day -> g/kg/day)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> PerTimeAdjust


        /// Convert the time unit of a PerTimeAdjust (e.g., mg/kg/day -> mg/kg/week)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> PerTimeAdjust


        let setNearestValue vu = toOrdVar >> setNearestValue vu >> PerTimeAdjust


        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `PerTimeAdjust` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `PerTimeAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar

        /// Get a ValueUnit string representation of a PerTimeAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a PerTimeAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a PerTimeAdjust has constraints
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a PerTimeAdjust is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a PerTimeAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> PerTimeAdjust


        /// Check whether a PerTimeAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a PerTimeAdjust is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a PerTimeAdjust is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a PerTimeAdjust
        let clear = toOrdVar >> clear >> PerTimeAdjust


        /// Set a PerTimeAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> PerTimeAdjust


    /// Type and functions that represent an adjusted rate,
    /// i.e., an amount per adjust unit per time (for example, mL/kg/h)
    module RateAdjust =


        let [<Literal>] name = "rte_adj"


        /// Get the OrderVariable in a RateAdjust
        let toOrdVar (RateAdjust rte_adj) = rte_adj


        /// Create a Dto for a RateAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a RateAdjust from a Dto
        let fromDto dto =
            dto
            |> Dto.fromDto
            |> (map ValueUnit.correctAdjustOrder >> RateAdjust)


        /// Set a `RateAdjust` with an OrderVariable
        /// in a list of OrderVariables.
        let fromOrdVar =
            fromOrdVar
                toOrdVar
                (map ValueUnit.correctAdjustOrder >> RateAdjust)


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
            | _ ->
                un
                |> ValueUnit.per adj
                |> ValueUnit.per tu
            |> createNew (n |> Name.add name)
            |> RateAdjust


        /// Convert the first unit of a RateAdjust (e.g., mL/kg/h -> L/kg/h)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> RateAdjust


        /// Convert the time unit of a RateAdjust (e.g., mL/kg/h -> mL/kg/min)
        let convertTimeUnit u = toOrdVar >> convertTimeUnit u >> RateAdjust


        /// Turn a `RateAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `RateAdjust` (including constraints)
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints true false)


        /// Get a `RateAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a RateAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit Markdown representation of a RateAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        /// Check whether a RateAdjust has constraints
        let hasConstraints = toOrdVar >> hasConstraints


        /// Check whether a RateAdjust is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints


        /// Apply the constraints of a RateAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> RateAdjust


        /// Check whether a RateAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive


        /// Check whether a RateAdjust is solved
        let isSolved = toOrdVar >> isSolved


        /// Check whether a RateAdjust is cleared
        let isCleared = toOrdVar >> isCleared


        /// Clear the values of a RateAdjust
        let clear = toOrdVar >> clear >> RateAdjust


        /// Set a RateAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> RateAdjust


    /// Type and functions that represent an adjusted total,
    /// i.e., an amount per adjust unit (for example, mL/kg)
    module TotalAdjust =


        let [<Literal>] name = "tot_adj"


        /// Get the OrderVariable in a TotalAdjust
        let toOrdVar (TotalAdjust tot_adj) = tot_adj


        /// Create a Dto for a TotalAdjust
        let toDto = toOrdVar >> Dto.toDto


        /// Create a TotalAdjust from a Dto
        let fromDto dto = dto |> Dto.fromDto |> TotalAdjust


        /// Set a `TotalAdjust` with an OrderVariable
        /// in a list of OrderVariables.
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
            | _ ->
                un
                |> ValueUnit.per adj
            |> createNew (n |> Name.add name)
            |> TotalAdjust


        /// Convert the first unit of a TotalAdjust (e.g., mL/kg -> L/kg)
        let convertFirstUnit u = toOrdVar >> convertFirstUnit u >> TotalAdjust


        /// Turn a `TotalAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a string representation of a `TotalAdjust`
        /// Note: constraints are not included by this function
        let toStringWithConstraints =
            toOrdVar >> (toStringWithConstraints false true)

        /// Get a `TotalAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar

        /// Get a ValueUnit string representation of a TotalAdjust
        let toValueUnitString = toValueUnitString toOrdVar

        /// Get a ValueUnit Markdown representation of a TotalAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar

        let hasConstraints = toOrdVar >> hasConstraints

        /// Check whether a TotalAdjust is within its constraints
        let isWithinConstraints = toOrdVar >> isWithinConstraints

        /// Apply the constraints of a TotalAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> TotalAdjust

        /// Check whether a TotalAdjust is non-zero positive
        let isNonZeroPositive = toOrdVar >> isNonZeroPositive

        /// Check whether a TotalAdjust is solved
        let isSolved = toOrdVar >> isSolved

        /// Check whether a TotalAdjust is cleared
        let isCleared = toOrdVar >> isCleared

        /// Clear the values of a TotalAdjust
        let clear = toOrdVar >> clear >> TotalAdjust

        /// Set a TotalAdjust to non-zero positive values
        let setToNonZeroPositive = toOrdVar >> setToNonZeroPositive >> TotalAdjust