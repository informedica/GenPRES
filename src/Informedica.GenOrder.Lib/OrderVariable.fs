namespace Informedica.GenOrder.Lib


/// Functions that deal with the `VariableUnit` type
module OrderVariable =


    open MathNet.Numerics

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
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


        let toMinMaxString prec (cs : Constraints) =
            let toStr = ValueUnit.toStringDecimalDutchShortWithPrec prec
            let toVal =
                ValueUnit.getValue
                >> Array.tryHead
                >> Option.map BigRational.toDecimal
                >> Option.map (Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3)
                >> Option.defaultValue ""

            match cs.Min |> Option.map Minimum.toValueUnit,
                  cs.Max |> Option.map Maximum.toValueUnit with
            | Some min, Some max ->
                if min |> ValueUnit.eqs max then $"{min |> toStr}"
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
    /// Map a function f to the Values (i.e. ValueUnit) of the Variable
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


    /// Get the `Variable.Name` from an OrderVariable
    let getName ovar = (ovar |> getVar).Name


    /// Check whether two OrderVariables have the same name
    let eqsName ovar1 ovar2 = (ovar1 |> getName) = (ovar2 |> getName)


    /// <summary>
    /// Apply the constraints of an OrderVariable to the Variable
    /// of the OrderVariable.
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    /// <remarks>
    /// If the Constraints have an Increment and a ValueSet, then the
    /// only the Increment is applied to the Variable.
    /// </remarks>
    let applyConstraints (ovar : OrderVariable) =
        { ovar with
            Variable =
                ovar.Variable.Values
                |> ValueRange.setOptMin ovar.Constraints.Min
                |> ValueRange.setOptMax ovar.Constraints.Max
                |> ValueRange.setOptIncr ovar.Constraints.Incr
                // only set a ValueSet if there is no increment
                |> fun vr ->
                    if ovar.Constraints.Incr.IsSome then vr
                    else
                        vr
                        |> ValueRange.setOptVs ovar.Constraints.Values
                |> Variable.setValueRange ovar.Variable
        }


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


    let setFirstUnit u ovar =
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
    /// Get the string representation of an OrderVariable.
    /// </summary>
    /// <param name="exact">Whether to use the exact representation of the ValueRange</param>
    /// <param name="ovar">The OrderVariable</param>
    let toString exact ovar =
        let ns =
            ovar
            |> getName
            |> Variable.Name.toString
            |> String.split "."
            |> List.skip 1
            |> String.concat "."
            |> fun s -> $"[{s}"

        ns +
        (ovar.Variable
        |> Variable.getValueRange
        |> ValueRange.toString exact)


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


    /// Helper function to get a markdown string representation of the ValueRange of
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


    /// <summary>
    /// Check whether a Variable is solved
    /// </summary>
    /// <param name="ovar">The OrderVariable</param>
    let isSolved (ovar : OrderVariable) =
        ovar.Variable
        |> Variable.isSolved ||
        ovar.Variable
        |> Variable.isUnrestricted


    module Dto =

        open Newtonsoft.Json


        /// The `Dto` data transfer type for an OrderVariable
        type Dto () =
            member val Name = "" with get, set
            member val Constraints = Variable.Dto.dto () with get, set
            member val Variable = Variable.Dto.dto () with get, set


        /// Create a new `Dto` for an OrderVariable
        let dto () = Dto ()


        let clean (old : Dto) =
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

                    let min  = dto.Constraints.MinOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Minimum.create  dto.Constraints.MinIncl)))
                    let max  = dto.Constraints.MaxOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Maximum.create  dto.Constraints.MaxIncl)))

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

                let min  = dto.Variable.MinOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Minimum.create  dto.Variable.MinIncl)))
                let max  = dto.Variable.MaxOpt  |> Option.bind (ValueUnit.Dto.fromDto >> (Option.map (Maximum.create  dto.Variable.MaxIncl)))

                create n min incr max vals cs
            with
            | e ->
                ConsoleWriter.writeErrorMessage
                    $"cannot create OrderVariable fromDto: {dto.Name |> JsonConvert.DeserializeObject}"
                    true false
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


        /// Get a ValueUnit markdown representation of a Count
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Count to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> count



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


        /// Get a `Time` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Time
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a Time
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Time to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> time



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


        /// Get a `Frequency` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Frequency
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a Frequency
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Frequency to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Frequency



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


        /// Get a `Concentration` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Concentration
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a Concentration
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Concentration to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Concentration



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


        let setDoseUnit u = toOrdVar >> setFirstUnit u >> Quantity


        /// Turn a `Quantity` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `Quantity` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Quantity
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a Quantity
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Quantity to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Quantity


        let hasIncrement =
            toOrdVar >> hasIncrement


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


        let setDoseUnit u = toOrdVar >> setFirstUnit u >> PerTime


        /// Turn a `PerTime` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `PerTime` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a PerTime
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a PerTime
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a PerTime to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> PerTime


        /// Check whether a PerTime is solved
        let isSolved = toOrdVar >> isSolved


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



        let setDoseUnit u = toOrdVar >> setFirstUnit u >> Rate


        /// Turn a `Rate` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `Rate` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Rate
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a Rate
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Rate to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Rate


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



        let setDoseUnit u = toOrdVar >> setFirstUnit u >> Total


        /// Turn a `Total` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `Total` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a Total
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a Total
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a Total to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> Total



    /// Type and functions that represent a adjusted quantity,
    /// and a adjusted quantity is a quantity per time
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


        let setDoseUnit u = toOrdVar >> setFirstUnit u >> QuantityAdjust


        /// Turn a `QuantityAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `QuantityAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a QuantityAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a QuantityAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a QuantityAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> QuantityAdjust


        /// Check whether a QuantityAdjust is solved
        let isSolved = toOrdVar >> isSolved


    /// Type and functions that represent a adjusted total,
    /// and a adjusted total is a quantity per time
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



        let setDoseUnit u = toOrdVar >> setFirstUnit u >> PerTimeAdjust


        let toString = toOrdVar >> (toString false)


        let toValueUnitStringList = toValueUnitStringList toOrdVar


        let toValueUnitString = toValueUnitString toOrdVar


        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar



        let applyConstraints = toOrdVar >> applyConstraints >> PerTimeAdjust


        /// Check whether a PerTimeAdjust is solved
        let isSolved = toOrdVar >> isSolved


    /// Type and functions that represent a adjusted total,
    /// and a adjusted total is a quantity per time
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


        let setDoseUnit u = toOrdVar >> setFirstUnit u >> RateAdjust


        /// Turn a `RateAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `RateAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a RateAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a RateAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a RateAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> RateAdjust


        /// Check whether a RateAdjust is solved
        let isSolved = toOrdVar >> isSolved


    /// Type and functions that represent a adjusted quantity,
    /// and a adjusted quantity is a quantity per time
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


        let setDoseUnit u = toOrdVar >> setFirstUnit u >> TotalAdjust


        /// Turn a `TotalAdjust` to a string
        let toString = toOrdVar >> (toString false)


        /// Get a `TotalAdjust` as a value unit string list
        let toValueUnitStringList = toValueUnitStringList toOrdVar


        /// Get a ValueUnit string representation of a TotalAdjust
        let toValueUnitString = toValueUnitString toOrdVar


        /// Get a ValueUnit markdown representation of a TotalAdjust
        let toValueUnitMarkdown = toValueUnitMarkdown toOrdVar


        /// Apply the constraints of a TotalAdjust to the OrderVariable Variable
        let applyConstraints = toOrdVar >> applyConstraints >> TotalAdjust


        /// Check whether a TotalAdjust is solved
        let isSolved = toOrdVar >> isSolved

