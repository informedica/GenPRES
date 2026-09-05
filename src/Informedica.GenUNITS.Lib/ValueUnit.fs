namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL

open Informedica.Utils.Lib


/// A ValueUnit pairs an array of values with a Unit. Declared at namespace
/// level (not in Types.fs) so the `ValueUnit` module below can shadow it
/// (type-first convention) and so its intrinsic operator members live in the
/// same module group as the arithmetic (calc/eqs/cmp/convertTo) they delegate
/// to.
type ValueUnit = ValueUnit of BigRational[] * Unit


module ValueUnit =

    open Core

    /// Parse a string to a ValueUnit. Moved here from the Parser module so it
    /// can construct the ValueUnit type, which lives alongside this module.
    module private Parsing =

        open FParsec

        let parse s =
            // need to change nan to nnn to avoid getting a float 'nan'
            // NOTE: raw substring replace; a name containing "nan" would be mangled.
            let s = s |> String.replace "nan" "nnn"

            let pBigRatList = sepBy Parser.pBigRat (Parser.ws >>. pstring ";" .>> Parser.ws)

            let pValue = between (pstring "[") (pstring "]") pBigRatList <|> pBigRatList

            let p =
                pValue .>>. Parser.parseUnit
                |>> fun (brs, u) -> ValueUnit(brs |> List.toArray, u)

            s |> run p


    /// Parse a string to a ValueUnit.
    let parse s = Parsing.parse s

    //----------------------------------------------------------------------------
    // Re-exports from the Core and Combine layers
    //----------------------------------------------------------------------------


    let opToStr = Core.opToStr

    let opFromString = Core.opFromString

    let count = Core.count

    let createCombiUnit uop = Combine.createCombiUnit uop

    let per u2 u1 = Combine.per u2 u1

    let times u2 u1 = Combine.times u2 u1

    let plus u2 u1 = Combine.plus u2 u1

    let minus u2 u1 = Combine.minus u2 u1


    /// Backward-compatible re-export of the Group functions, which now live
    /// in Informedica.GenUnits.Lib.Group, under the historical `ValueUnit.Group` path. A real
    /// (non-abbreviation) module so the members are exported to consumers.
    module Group =

        /// Re-export the Group type so the historical `ValueUnit.Group.Group`
        /// type path keeps resolving for consumers.
        type Group = Informedica.GenUnits.Lib.Group

        let unitToGroup = Group.unitToGroup

        let contains = Group.contains

        let getGroups = Group.getGroups

        let numDenom = Group.numDenom

        let eqsGroup = Group.eqsGroup

        let toString = Group.toString

        let toStringDutch = Group.toStringDutch

        let getGroupUnits = Group.getGroupUnits

        let getUnitCombinations = Group.getUnitCombinations


    module Multipliers =

        let zero = 0N
        let one = 1N
        let kilo = 1000N
        let deci = 1N / 10N
        let centi = deci / 10N
        let milli = 1N / kilo
        let micro = milli / kilo
        let nano = micro / kilo

        let second = 1N
        let minute = 60N * second
        let hour = 60N * minute
        let day = 24N * hour
        let week = 7N * day
        let year = (365N + (1N / 4N)) * day
        let month = year / 12N

        /// Returns the value v as a basevalue using multiplier m
        let inline toBase m v = v * m
        /// Returns the value v as a unitvalue using multiplier m
        let inline toUnit m v = v / m


        /// <summary>
        /// Get the multiplier of a unit
        /// (also when this is a combination of units)
        /// </summary>
        /// <example>
        /// getMultiplier (Mass (KiloGram 1N)) = 1000N <br/>
        /// getMultiplier (CombiUnit(Mass (MilliGram 1N), OpPer, Volume (MilliLiter 1N))) = 1N <br/>
        /// </example>
        let getMultiplier u =
            let rec get u m =
                match u with
                | NoUnit
                | ZeroUnit -> one
                | General(_, n) -> n * one
                | Count g ->
                    match g with
                    | Times n -> n * one
                | Mass g ->
                    match g with
                    | KiloGram n -> n * kilo
                    | Gram n -> n * one
                    | MilliGram n -> n * milli
                    | MicroGram n -> n * micro
                    | NanoGram n -> n * nano
                | Distance d ->
                    match d with
                    | Meter n -> n * one
                    | CentiMeter n -> n * centi
                    | MilliMeter n -> n * milli
                | Volume g ->
                    match g with
                    | Liter n -> n * one
                    | DeciLiter n -> n * deci
                    | MilliLiter n -> n * milli
                    | MicroLiter n -> n * micro
                    | Droplet(n, m) -> n * (milli / m)
                | Time g ->
                    match g with
                    | Year n -> n * year
                    | Month n -> n * month
                    | Week n -> n * week
                    | Day n -> n * day
                    | Hour n -> n * hour
                    | Minute n -> n * minute
                    | Second n -> n * second
                | Molar g ->
                    match g with
                    | Mole n -> n * one
                    | MilliMole n -> n * milli
                    | MicroMole n -> n * micro
                | International g ->
                    match g with
                    | MIU n -> n * kilo * kilo
                    | IU n -> n * one
                    | MilliIU n -> n * milli
                | Weight g ->
                    match g with
                    | WeightKiloGram n -> n * kilo
                    | WeightGram n -> n * one
                | Height g ->
                    match g with
                    | HeightMeter n -> n * one
                    | HeightCentiMeter n -> n * centi
                | BSA g ->
                    match g with
                    | M2 n -> n * one
                | Energy e ->
                    match e with
                    | Calorie n -> n * one
                    | KiloCalorie n -> n * kilo
                | CombiUnit(u1, op, u2) ->
                    let m1 = get u1 m
                    let m2 = get u2 m

                    match op with
                    | OpTimes -> m1 * m2
                    | OpPer -> m1 / m2
                    | OpMinus
                    | OpPlus -> m

            get u 1N


    //----------------------------------------------------------------------------
    // Create functions
    //----------------------------------------------------------------------------


    /// <summary>
    /// Create a ValueUnit from a value v
    /// (a bigrational array) and a unit u
    /// </summary>
    /// <example>
    /// create (Mass (KiloGram 1N)) [| 1N; 2N; 3N |] = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let create u v =
        match u with
        | NoUnit when v = [| 0N |] -> ([| 0N |], ZeroUnit) |> ValueUnit
        | ZeroUnit -> ([| 0N |], ZeroUnit) |> ValueUnit
        | _ -> (v, u) |> ValueUnit


    /// An empty ValueUnit that has no value
    /// and no unit, i.e. an empty array with
    /// NoUnit.
    let empty = create NoUnit [||]


    /// <summary>
    /// Create a ValueUnit from a single
    /// value v and a unit u
    /// </summary>
    /// <example>
    /// createSingle (Mass (KiloGram 1N)) 1N = ValueUnit ([|1N|], Mass (KiloGram 1N))
    /// </example>
    let createSingle u v = [| v |] |> create u


    /// <summary>
    /// Utility create function to allow piping
    /// v |> WithUnit u
    /// </summary>
    /// <example>
    /// [| 1N; 2N; 3N |] |> withUnit (Mass (KiloGram 1N)) = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let withUnit u v = v |> create u


    /// <summary>
    /// Utility create function to allow piping
    /// with a single value v |> WithUnit u
    /// </summary>
    /// <example>
    /// 1N |> singleWithUnit (Mass (KiloGram 1N)) = ValueUnit ([|1N|], Mass (KiloGram 1N))
    /// </example>
    let singleWithUnit u v = [| v |] |> withUnit u


    /// <summary>
    /// create a ValueUnit with syntax like
    /// u |> withValue v
    /// </summary>
    /// <example>
    /// (Mass (KiloGram 1N)) |> withValue [| 1N; 2N; 3N |] = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let withValue v u = create u v


    /// <summary>
    /// create a ValueUnit with syntax like
    /// u |> withValue v, where v is a single value
    /// </summary>
    /// <example>
    /// (Mass (KiloGram 1N)) |> withSingleValue 1N = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </example>
    let singleWithValue v u = [| v |] |> create u


    /// create a general unit with unit value n
    /// and string s
    let generalUnit n s = (s, n) |> General


    /// Create a general ValueUnit with unit value
    /// n general text s and value v
    let generalValueUnit v n s = create (generalUnit n s) v


    /// Create a general 'single' ValueUnit with unit value
    /// n general text s and single value v
    let generalSingleValueUnit v n s = generalValueUnit [| v |] n s


    //----------------------------------------------------------------------------
    // ValueUnit Setters and Getters
    //----------------------------------------------------------------------------


    /// Get the value and the unit of a ValueUnit as a tuple.
    /// Example: get (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    let get (ValueUnit(v, u)) = v, u


    /// Get the value of a ValueUnit.
    /// Example: getValue (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// [|1N; 2N; 3N|]
    let getValue (ValueUnit(v, _)) = v


    /// Get the unit of a ValueUnit
    let getUnit (ValueUnit(_, u)) = u


    /// Just sets a value without calculation.
    /// Example: ValueUnit ([|1N|], Mass (KiloGram 1N)) |> setValue [| 1N; 2N; 3N |] =
    /// ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    let setValue v (ValueUnit(_, u)) = v |> create u


    /// Sets a single value to a ValueUnit.
    /// Example: ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)) |> setSingleValue 1N =
    /// ValueUnit ([|1N|], Mass (KiloGram 1N))
    let setSingleValue v = setValue [| v |]


    /// Set the unit of a ValueUnit, keeping its value
    let setUnit u vu = vu |> getValue |> withUnit u


    /// Get the full unit group of a ValueUnit.
    /// Example: getGroup (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// MassGroup
    let getGroup = getUnit >> Group.unitToGroup


    //----------------------------------------------------------------------------
    // Constants
    //----------------------------------------------------------------------------


    /// Create a 'zero' with unit u
    let zero u = [| 0N |] |> create u


    /// Create a 'one' with unit u
    let one u = [| 1N |] |> create u


    //----------------------------------------------------------------------------
    // Logic functions
    //----------------------------------------------------------------------------


    /// Check whether the unit is a count unit, i.e.
    /// belongs to the Count group
    let isCountUnit = Group.eqsGroup (1N |> Times |> Count)


    /// Checks whether a ValueUnit has an
    /// empty value
    let isEmpty = getValue >> Array.isEmpty


    let hasZeroUnit = getUnit >> ((=) ZeroUnit)


    let hasNoUnit = getUnit >> ((=) NoUnit)


    /// Check whether a ValueUnit is a single value
    let isSingleValue = getValue >> Array.length >> ((=) 1)


    /// Checks whether vu1 is of the
    /// same unit group as vu2
    let eqsGroup vu1 vu2 =
        let u1 = vu1 |> getUnit
        let u2 = vu2 |> getUnit
        u1 |> Group.eqsGroup u2


    /// Checks wheter u1 has a unit u2
    let hasUnit u2 u1 =
        let rec has u =
            match u with
            | CombiUnit(lu, _, ru) -> if lu = u2 || ru = u2 then true else has lu || (has ru)
            | _ -> u = u2

        has u1


    /// Checks whether unit u
    /// is not a CombiUnit
    let notCombiUnit u =
        match u with
        | CombiUnit _ -> false
        | _ -> true


    //----------------------------------------------------------------------------
    // Conversions
    //----------------------------------------------------------------------------


    /// Convert a value to v to the
    /// base value of unit u.
    /// For example u = mg v = 1 -> 1/1000
    let valueToBase u v =
        v |> Multipliers.toBase (u |> Multipliers.getMultiplier)

    /// Get the value of a ValueUnit as
    /// a base value.
    /// For example ValueUnit(1000, mg) -> 1
    let toBaseValue vu =
        let v, u = vu |> get

        if u |> Multipliers.getMultiplier = 1N then
            v
        else
            v |> Array.map (valueToBase u)


    /// Convert a value to v to the
    /// unit value of unit u.
    /// For example u = mg v = 1 -> 1000
    let valueToUnit u v =
        v |> Multipliers.toUnit (u |> Multipliers.getMultiplier)


    /// Get the value of a ValueUnit as
    /// a unit value ValueUnit(1, mg) -> 1000
    let toUnitValue vu =
        let v, u = vu |> get

        if u |> Multipliers.getMultiplier = 1N then
            v
        else
            v |> Array.map (valueToUnit u)


    /// Replace the Value in a ValueUnit to its base.
    /// For example ValueUnit(1000, mg) -> ValueUnit(1, mg)
    let toBase vu =
        let v, u = vu |> get

        if u |> Multipliers.getMultiplier = 1N then
            vu
        else
            v |> Array.map (valueToBase u) |> create u


    /// Transforms a ValueUnit to its unit.
    /// For example ValueUnit(1, mg) -> ValueUnit(1000, mg)
    let toUnit vu =
        let v, u = vu |> get

        if u |> Multipliers.getMultiplier = 1N then
            vu
        else
            v |> Array.map (valueToUnit u) |> create u


    /// <summary>
    /// Get the Value of a ValueUnit vu as the base value
    /// </summary>
    /// <example>
    /// <code>
    /// ValueUnit([|1N|], Units.Mass.kiloGram) |> getBaseValue = [|1000N|]
    /// </code>
    /// </example>
    let getBaseValue = toBase >> getValue


    //----------------------------------------------------------------------------
    // Value application functions
    //----------------------------------------------------------------------------

    /// <summary>
    /// Apply a function fValue to the Value of a ValueUnit vu
    /// </summary>
    /// <param name="fValue">The function to apply to the Value</param>
    /// <param name="vu">The ValueUnit</param>
    /// <returns>The updated ValueUnit</returns>
    /// <example>
    /// <code>
    /// let fValue = Array.map ((+) 1N) // add 1 to each value
    /// applyToValue fValue (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N))
    /// </code>
    /// </example>
    let applyToValue fValue vu =
        let u = vu |> getUnit
        vu |> getValue |> fValue |> create u


    /// <summary>
    /// Filter the value of a value unit using
    /// a predicate function pred. This function
    /// is parameterized on the base value of the value
    /// unit.
    /// </summary>
    /// <example>
    /// <code>
    /// // Get all even numbers, note that the base value of 1 KiloGram is 1000
    /// // so the predicate function is applied to 1000, 2000, 3000, etc.
    /// // ValueUnit = ValueUnit ([|-2N; 2N|], Mass (KiloGram 1N))
    /// filter (fun br -> (br / 2000N).Denominator = 1I) (ValueUnit ([|1N; 2N; 3N; -1N; -2N; -3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let filter pred =
        toBase >> applyToValue (Array.filter pred) >> toUnit


    // Apply an array function to a ValueUnit
    let internal applyArrayFunction fArr fVal vu =
        let u = vu |> getUnit
        vu |> getValue |> fArr fVal |> create u


    /// <summary>
    /// Filter the values in a ValueUnit using a predicate function pred.
    /// </summary>
    /// <param name="fPred">The predicate function to use</param>
    let filterValues fPred = applyArrayFunction Array.filter fPred


    /// <summary>
    /// Map the values in a ValueUnit using a function fMap.
    /// </summary>
    /// <param name="fMap">The function to appy to each individual value</param>
    let mapValues fMap = applyArrayFunction Array.map fMap


    //----------------------------------------------------------------------------
    // Value selection
    //----------------------------------------------------------------------------


    /// Make sure that a ValueUnit has a positive value
    /// or zero. NoUnit is transformed to ZeroUnit to enable
    /// logic for calculation of min and max values. If a ValueUnit
    /// has a Value then all negative or zero values are removed.
    let setZeroOrPositive vu =
        if vu |> getUnit = NoUnit then
            ZeroUnit |> zero
        else
            let vu = vu |> filter (fun br -> br > 0N)

            if vu |> isEmpty |> not then vu else vu |> setValue [| 0N |]


    /// Get the indices of the values in vu1 that are also in vu2
    let getIndices vu1 vu2 =
        let vals1, vals2 = vu1 |> getBaseValue, vu2 |> getBaseValue

        let pred x = vals2 |> Array.exists ((=) x)
        vals1 |> Array.indices pred


    let pickIndices indices vu =
        let vals = vu |> getValue

        indices
        |> Array.choose (fun i -> vals |> Array.tryItem i)
        |> create (vu |> getUnit)


    module private UnitItem =

        type UnitItem =
            | UnitItem of Types.Unit
            | OpPlusMinItem of Operator
            | OpMultItem of Operator
            | OpDivItem of Operator


        // Takes a list of UnitItems and create a Unit from it
        let listToUnit ul =
            let rec toUnit ul u =
                match ul with
                | [] -> u
                | ui :: rest ->
                    match u with
                    | NoUnit ->
                        match ui with
                        | UnitItem u' -> u'
                        | _ -> NoUnit
                        |> toUnit rest
                    | _ ->
                        match ul with
                        | oi :: ui :: rest ->
                            match oi, ui with
                            | OpDivItem op, UnitItem ur
                            | OpPlusMinItem op, UnitItem ur
                            | OpMultItem op, UnitItem ur -> createCombiUnit (u, op, ur) |> toUnit rest
                            | _ -> u
                        | _ -> u

            toUnit ul NoUnit


    //----------------------------------------------------------------------------
    // Operations
    //----------------------------------------------------------------------------


    /// Get a list of the units in a unit u
    let rec getUnits u =
        match u with
        | CombiUnit(ul, _, ur) -> ul |> getUnits |> List.prepend (ur |> getUnits)
        | _ -> [ u ]


    // Separate numerators from denominators of a unit.
    // The recursion is always entered at the numerator, so the
    // isNum flag (true = numerator, false = denominator) is an
    // internal concern hidden behind the one-argument numDenom.
    let internal numDenom u =
        let rec loop isNum u =
            match u with
            | CombiUnit(ul, OpTimes, ur) ->
                let lns, lds = ul |> loop isNum
                let rns, rds = ur |> loop isNum
                lns @ rns, lds @ rds
            | CombiUnit(ul, OpPer, ur) ->
                if isNum then
                    let lns, lds = ul |> loop true
                    let rns, rds = ur |> loop false
                    lns @ rns, lds @ rds
                else
                    let lns, lds = ur |> loop true
                    let rns, rds = ul |> loop false
                    lns @ rns, lds @ rds
            | _ -> if isNum then (u |> getUnits, []) else ([], u |> getUnits)

        loop true u


    // Build a unit from a list of numerators and denominators.
    // Uses an accumulator to build the unit and a boolean to indicate
    // whether there is a count unit in the numerator.
    // isCount is true when there is a count unit in the numerator
    // and false when there is no count unit in the numerator.
    // Note when ns = ds then the result is isCount = true and u = NoUnit
    let rec build ns ds (isCount, u) =
        match ns with
        | [] ->
            match ds with
            | [] ->
                if isCount && u = NoUnit then
                    (true, count)
                else
                    (isCount, u)
            | _ ->
                let d = ds |> List.rev |> List.reduce times

                if u = NoUnit then Count(Times 1N) |> per d else u |> per d
                |> fun u -> (isCount, u)
        | h :: tail ->
            if ds |> List.exists (Group.eqsGroup h) then
                build tail (ds |> List.removeFirst (Group.eqsGroup h)) (true, u)
            else
                let isCount = isCount || (u |> Group.eqsGroup count) || (h |> Group.eqsGroup count)

                if u = NoUnit then h else u |> times h
                |> fun u -> build tail ds (isCount, u)


    /// <summary>
    /// Simplify a unit u such that units are algebraically removed or
    /// transformed to count units, where applicable.
    /// </summary>
    /// <param name="u">The unit to simplify</param>
    /// <returns>
    /// The simplified unit
    /// </returns>
    let simplifyUnit u =
        if u = NoUnit then
            u
        else
            let ns, ds = u |> numDenom

            (false, NoUnit)
            |> build ns ds
            |> fun (_, newU) ->
                // nothing changed so just return original
                if u = newU then
                    u
                else
                    match newU with
                    | CombiUnit(u1, OpPer, CombiUnit(u2, OpTimes, u3)) ->
                        if u2 |> Group.eqsGroup u3 then
                            newU
                        else
                            CombiUnit(CombiUnit(u1, OpPer, u2), OpPer, u3)
                    | _ -> newU


    /// <summary>
    /// Simplify a value unit u such that units are algebraically removed or
    /// transformed to count units, where applicable.
    /// </summary>
    /// <param name="vu">The value unit to simplify</param>
    /// <returns>
    /// The simplified value unit
    /// </returns>
    /// <example>
    /// <code>
    /// simplify (ValueUnit ([|1N; 2N; 3N|], CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N)))) =
    /// ValueUnit ([|1N; 2N; 3N|], CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N)))
    /// simplify (ValueUnit ([|1N; 2N; 3N|], CombiUnit (Mass (KiloGram 1N), OpPer, Mass (KiloGram 1N)))) =
    /// ValueUnit ([|1N; 2N; 3N|], Count (Times 1N))
    /// </code>
    /// </example>
    let rec simplify vu =
        let v, u = vu |> get

        if u = NoUnit then
            vu
        else
            let u = simplifyUnit u

            v
            |> create u
            // calculate to the new combiunit
            |> toUnitValue
            // recreate again to final value unit
            |> create u


    /// <summary>
    /// Calculate a ValueUnit by applying an operator op
    /// to ValueUnit vu1 and vu2. The operator can be addition,
    /// subtraction, multiplication or division.
    /// The boolean b results in whether the result is
    /// simplified.
    /// </summary>
    /// <param name="b">Whether to simplify the result</param>
    /// <param name="op">The operator to apply</param>
    /// <param name="vu1">The first ValueUnit</param>
    /// <param name="vu2">The second ValueUnit</param>
    /// <returns>
    /// The result of applying the operator to the ValueUnits
    /// </returns>
    /// <remarks>
    /// fails when adding or subtracting different units
    /// </remarks>
    /// <example>
    /// <code>
    /// calc true (+) (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) =
    /// ValueUnit ([|2N; 3N; 4N; 5N; 6N|], Mass (KiloGram 1N))
    /// </code>
    /// </example>
    let calc b op vu1 vu2 =

        let (ValueUnit(_, u1)) = vu1
        let (ValueUnit(_, u2)) = vu2
        // calculate value in base
        let v =
            let vs1 = vu1 |> toBaseValue
            let vs2 = vu2 |> toBaseValue
            BigRational.calcCartesian op vs1 vs2 |> BigRational.distinct

        (*
            Array.allPairs vs1 vs2
            |> Array.map (fun (v1, v2) -> v1 |> op <| v2)
            |> Array.distinct
            *)

        // calculate new combi unit
        let u =
            match op with
            | BigRational.Mul -> u1 |> times u2
            | BigRational.Div -> u1 |> per u2
            | BigRational.Add
            | BigRational.Sub ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup u2 -> u2
                // Special case when one value is a dimensionless zero
                | ZeroUnit, u
                | u, ZeroUnit -> u
                // Otherwise fail
                | _ -> invalidArg (nameof vu2) $"cannot add or subtract different units %A{u1} %A{u2}"
            |> fun u -> if b then simplifyUnit u else u
        // recreate valueunit with base value and combined unit
        v
        |> create u
        // calculate to the new combiunit
        |> toUnitValue
        // recreate again to final value unit
        |> create u


    /// <summary>
    /// Compare a ValueUnit vu1 with vu2.Comparison can be:
    /// greater, greater or equal, smaller, smaller or equal.
    /// </summary>
    /// <remarks>
    /// Checks if the comparison is true for all individual values.
    /// Doesn't work for equal.
    /// </remarks>
    /// <param name="cp">The operator to use</param>
    /// <param name="vu1">The first ValueUnit</param>
    /// <param name="vu2">The second ValueUnit</param>
    /// <returns>
    /// True if the comparison is true, false otherwise
    /// </returns>
    /// <example>
    /// <code>
    /// // 1 kg > 1000 g = true
    /// cmp (>) (ValueUnit ([|1N|], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = true
    /// </code>
    /// </example>
    let cmp cp vu1 vu2 =
        // TODO need better eqsGroup like mg/kg/day = (mg/kg)/day = (mg/kg*day) <> mg/(kg/day) = mg*day/kg
        if
            (vu1 |> hasZeroUnit |> not && vu2 |> hasZeroUnit |> not)
            && (vu1 |> hasNoUnit |> not && vu2 |> hasNoUnit |> not)
            && (vu1 |> eqsGroup vu2 |> not)
        then
            invalidArg (nameof vu2) $"cannot compare {vu1} with {vu2}"
        //else
        let vs1 = vu1 |> toBaseValue
        let vs2 = vu2 |> toBaseValue

        Array.allPairs vs1 vs2 |> Array.forall (fun (v1, v2) -> v1 |> cp <| v2)


    /// <summary>
    /// Determine if vu1 equals vu2. This is true when
    /// both ValueUnits have the same unit and the same value
    /// </summary>
    /// <param name="vu1">The first ValueUnit</param>
    /// <param name="vu2">The second ValueUnit</param>
    let eqs vu1 vu2 =
        // TODO need better eqsGroup like mg/kg/day = (mg/kg)/day = (mg/kg*day) <> mg/(kg/day) = mg*day/kg
        if
            (vu1 |> hasZeroUnit |> not && vu2 |> hasZeroUnit |> not)
            && (vu1 |> hasNoUnit |> not && vu2 |> hasNoUnit |> not)
            && (vu1 |> eqsGroup vu2 |> not)
        then
            invalidArg (nameof vu2) $"cannot compare {vu1} with {vu2}"

        let vs1 = vu1 |> toBaseValue |> BigRational.distinct |> Array.sort

        let vs2 = vu2 |> toBaseValue |> BigRational.distinct |> Array.sort

        vs1 = vs2


    /// <summary>
    /// Validates the values of Value for a ValueUnit.
    /// </summary>
    /// <param name="fValid">The validator function</param>
    /// <param name="errMsg">The error message</param>
    /// <param name="vu">The ValueUnit</param>
    /// <returns>
    /// Result.Ok vu if the values are valid, Result.Error errMsg otherwise
    /// </returns>
    let validate fValid errMsg vu =
        if vu |> getValue |> fValid then
            vu |> Ok
        else
            errMsg |> Error


    /// Check if first ValueUnit is greater than second ValueUnit
    /// Example: gt (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = true
    let gt = cmp (>)


    /// Check if first ValueUnit is smaller than second ValueUnit
    /// Example: st (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = false
    // Check if left vu is greater than or equal to right vu
    let st = cmp (<)


    /// Check if first ValueUnit is greater than or equal to second ValueUnit
    /// Example: gte (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = true
    let gte = cmp (>=)


    /// Check if first ValueUnit is smaller than or equal to second ValueUnit
    /// Example: ste (ValueUnit ([|1N |], Mass (KiloGram 1N))) (ValueUnit ([|10N|], Mass (Gram 1N))) = false
    let ste = cmp (<=)


    /// <summary>
    /// Convert a ValueUnit vu to
    /// a unit u.
    /// Do not convert to no unit or zerounit
    /// </summary>
    /// <example>
    /// <code>
    /// //For example 1 gram -> 1000 mg:
    /// ValueUnit([|1N|], Units.Mass.gram) |> convertTo Units.Mass.milliGram
    /// </code>
    /// </example>
    let convertTo u vu =
        let _, oldU = vu |> get

        if u = oldU || u = NoUnit || u = ZeroUnit then
            vu
        else
            vu |> toBaseValue |> create u |> toUnitValue |> create u


    /// Check if Value is zero
    let isZero = getValue >> Array.forall ((=) 0N)

    /// Check if Value is > 0
    let gtZero = getValue >> Array.forall ((<) 0N)

    /// Check if Value >= 0
    let gteZero = getValue >> Array.forall ((<=) 0N)

    /// Check if Value < 0
    let stZero = getValue >> Array.forall ((>) 0N)

    /// Check if Value <= 0
    let steZero = getValue >> Array.forall ((>=) 0N)


    /// Get the smallest value of a ValueUnit.
    /// Returns None if the ValueUnit is empty.
    let minValue vu =
        if vu |> isEmpty then
            None
        else
            vu |> applyToValue (Array.min >> Array.singleton) |> Some


    /// Get the largest value of a ValueUnit.
    /// Returns None if the ValueUnit is empty.
    let maxValue vu =
        if vu |> isEmpty then
            None
        else
            vu |> applyToValue (Array.max >> Array.singleton) |> Some


    /// <summary>
    /// Get the median value of a ValueUnit. Returns None if empty.
    /// </summary>
    /// <remarks>
    /// For an even-length value set this returns the upper-middle element
    /// (the value at sorted index <c>length / 2</c>), NOT the average of the
    /// two middle values.
    /// </remarks>
    let medianValue vu =
        if vu |> isEmpty then
            None
        else
            vu
            |> applyToValue (fun xs ->
                let i = (xs |> Array.length) / 2

                xs
                |> Array.sort
                |> Array.tryItem i
                |> Option.map Array.singleton
                |> Option.defaultValue xs
            )
            |> Some


    // Helper function to calculate the min or max value
    // that is inclusive or exclusive and is a multiple of
    // increment 'incr'.
    let internal multipleOf f incr vu =
        vu
        |> toBase
        |> applyToValue (fun vs ->
            let incr = incr |> getBaseValue |> Set.ofArray

            vs |> Array.map (f incr) //|> Array.map snd
        )
        |> toUnit


    /// <summary>
    /// Calculate the minimum value of a ValueUnit that is a minimum inclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// minInclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|4N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let minInclMultipleOf incr vu =
        multipleOf BigRational.minInclMultipleOf incr vu
        |> minValue
        |> Option.defaultValue vu


    /// <summary>
    /// Calculate the minimum value of a ValueUnit that is a minimum exclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// minExclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|4N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let minExclMultipleOf incr vu =
        multipleOf BigRational.minExclMultipleOf incr vu
        |> minValue
        |> Option.defaultValue vu


    /// <summary>
    /// Calculate the maximum value of a ValueUnit that is a maximum inclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// maxInclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|8N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let maxInclMultipleOf incr vu =
        multipleOf BigRational.maxInclMultipleOf incr vu
        |> maxValue
        |> Option.defaultValue vu


    /// <summary>
    /// Calculate the maximum value of a ValueUnit that is a maximum exclusive
    /// and is a multiple of Increment.
    /// </summary>
    /// <param name="incr">The Increment</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// maxExclMultipleOf (ValueUnit ([|3N|], Mass (Gram 1N))) (ValueUnit ([|9N|], Mass (Gram 1N))) =
    /// ValueUnit ([|6N|], Mass (Gram 1N))
    /// </code>
    /// </example>
    let maxExclMultipleOf incr vu =
        multipleOf BigRational.maxExclMultipleOf incr vu
        |> maxValue
        |> Option.defaultValue vu


    /// <summary>
    /// Get the denominators of the value of a ValueUnit.
    /// </summary>
    /// <example>
    /// <code>
    /// // returns 2, 3, 5
    /// denominator (ValueUnit ([|1N/2N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let denominator = getValue >> (Array.map BigRational.denominator)


    /// <summary>
    /// Get the numerators of the value of a ValueUnit.
    /// </summary>
    /// <example>
    /// <code>
    /// // returns 1, 2, 3
    /// numerator (ValueUnit ([|1N/2N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let numerator = getValue >> (Array.map BigRational.numerator)


    /// <summary>
    /// Remove all big rational multiples of the value of a ValueUnit.
    /// </summary>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|2N; 3N; 5N; 7N|], Mass (KiloGram 1N))
    /// removeBigRationalMultiples (ValueUnit ([|2N..1N..10N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let removeBigRationalMultiples =
        toBase >> applyToValue Array.removeBigRationalMultiples >> toUnit


    /// <summary>
    /// Get the intersection of two ValueUnits.
    /// </summary>
    /// <param name="vu1">ValueUnit 1</param>
    /// <param name="vu2">ValueUnit 2</param>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))
    /// intersect (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let intersect vu1 vu2 =
        vu1
        |> toBase
        |> applyToValue (fun vs ->
            vu2
            |> getBaseValue
            |> Set.ofArray
            |> Set.intersect (vs |> Set.ofArray)
            |> Set.toArray
        )
        |> toUnit


    /// <summary>
    /// Check if a ValueUnit is a subset of another ValueUnit.
    /// </summary>
    /// <param name="vu1">ValueUnit 1 the possible subset</param>
    /// <param name="vu2">ValueUnit 2 the set to check against</param>
    /// <example>
    /// <code>
    /// // returns true
    /// isSubset (ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// // returns false
    /// isSubset (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let isSubset vu1 vu2 =
        let s1 = vu1 |> getBaseValue |> Set.ofArray
        let s2 = vu2 |> getBaseValue |> Set.ofArray
        Set.isSubset s1 s2


    /// <summary>
    /// Check if ValueUnit vu1 contains ValueUnit vu2.
    /// </summary>
    /// <param name="vu2">The ValueUnit to check</param>
    /// <param name="vu1">The ValueUnit that should contain vu2</param>
    /// <example>
    /// <code>
    /// // returns true
    /// containsValue (ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// // returns true
    /// containsValue (ValueUnit ([|2000N; 3000N|], Mass (Gram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// // returns false
    /// containsValue (ValueUnit ([|2N; 3N|], Mass (Gram 1N))) (ValueUnit ([|2N; 3N; 4N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let containsValue vu2 vu1 =
        vu2
        |> toBase
        |> getValue
        |> Array.forall (fun v -> vu1 |> toBase |> getValue |> Array.exists ((=) v))


    /// <summary>
    /// Take the first n elements of a Value in a ValueUnit
    /// </summary>
    /// <param name="n">The n elements to take</param>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|1N; 2N|], Mass (KiloGram 1N))
    /// takeFirst 2 (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let takeFirst n = applyToValue (Array.take n)


    /// <summary>
    /// Take the last n elements of a Value in a ValueUnit
    /// </summary>
    /// <param name="n">The n elements to take</param>
    /// <example>
    /// <code>
    /// // returns ValueUnit ([|2N; 3N|], Mass (KiloGram 1N))
    /// takeLast 2 (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let takeLast n =
        applyToValue (Array.rev >> Array.take n >> Array.rev)


    /// <summary>
    /// Get the count of elements in a Value
    /// </summary>
    /// <example>
    /// <code>
    /// // returns 3
    /// valueCount (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N)))
    /// </code>
    /// </example>
    let valueCount = getValue >> Array.length


    /// <summary>
    /// Set vu2 to the single value in vu2 nearest to the single value in vu1.
    /// </summary>
    /// <remarks>
    /// Returns vu2 UNCHANGED when vu1 does not hold exactly one value, or when
    /// vu1's value lies entirely above or below vu2's range (i.e. greater than
    /// all of vu2 or smaller than all of vu2): there is no nearest-clamp in
    /// that case.
    /// </remarks>
    let setNearestValue vu1 vu2 =
        if vu1 |> valueCount <> 1 then
            vu2
        else if cmp (>) vu1 vu2 || cmp (<) vu1 vu2 then
            vu2
        else
            let vu1 = vu1 |> getBaseValue |> Array.head
            let vs2 = vu2 |> getBaseValue
            // find the nearest value in vs2 to vu1
            vs2
            |> Array.map (fun v -> (v, v - vu1 |> BigRational.Abs))
            |> Array.minBy snd
            |> fun (v, _) -> setSingleValue v vu2 |> toUnit


    let pickNearestHigherElseLower (target: ValueUnit) (candidates: ValueUnit) =
        if candidates |> isEmpty then
            candidates
        elif candidates |> eqsGroup target |> not then
            candidates
        else
            candidates
            |> toBase
            |> applyToValue (fun brs1 ->
                target
                |> getBaseValue
                |> Array.tryExactlyOne
                |> Option.map (fun br -> [| brs1 |> Array.pickNearestHigherElseLower br |])
                |> Option.defaultValue brs1
            ) // set selected base value
            |> toUnit


    //----------------------------------------------------------------------------
    // ValueUnit string functions
    //----------------------------------------------------------------------------


    /// <summary>
    /// Returns a string representation of a ValueUnit comparing operator.
    /// When  the operator is unknown, "unknown comparison" is returned.
    /// </summary>
    /// <example>
    /// <code>
    /// cmpToStr (>) = ">"
    /// </code>
    /// </example>
    let cmpToStr cp =
        let z = 1N |> Times |> Count |> zero
        let o = 1N |> Times |> Count |> one

        match cp with
        | _ when (z |> cp <| z) && not (z |> cp <| o) && not (o |> cp <| z) -> "="
        | _ when (z |> cp <| z) && (z |> cp <| o) && not (o |> cp <| z) -> "<="
        | _ when (z |> cp <| z) && not (z |> cp <| o) && (o |> cp <| z) -> ">="
        | _ when not (z |> cp <| z) && (z |> cp <| o) && not (o |> cp <| z) -> "<"
        | _ when not (z |> cp <| z) && not (z |> cp <| o) && (o |> cp <| z) -> ">"
        | _ -> "unknown comparison"


    /// <summary>
    /// Get the user readable string version of a unit in Dutch short format
    /// without unit group annotation (i.e., without brackets)
    /// </summary>
    /// <param name="u">The unit to convert to string</param>
    /// <example>
    /// <code>
    /// unitToReadableDutchString (Mass (KiloGram 1N)) = "kg"
    /// </code>
    /// </example>
    let unitToReadableDutchString u =
        u |> Units.toString None None false Units.Dutch Units.Short

    /// <summary>
    /// Get the user readable string version of a unit in Dutch short format
    /// without unit group annotation (i.e., without brackets) and a wrapper w
    /// </summary>
    /// <param name="vw">A wrapper around a unit value > 1</param>
    /// <param name="uw">A wrapper around a unit</param>
    /// <param name="u">The unit to convert to string</param>
    /// <example>
    /// <code>
    /// unitToReadableDutchString (Mass (KiloGram 1N)) = "kg"
    /// </code>
    /// </example>
    let unitToReadableDutchStringWithWrappers vw uw u =
        u |> Units.toString (Some vw) (Some uw) false Units.Dutch Units.Short


    /// <summary>
    /// Get the user readable string version of a ValueUnit
    /// </summary>
    /// <param name="hasGroup">When true, includes the unit group in brackets (e.g., "[Mass]"); when false, omits it</param>
    /// <param name="brf">The function to turn a BigRational into a string</param>
    /// <param name="loc">The localization to use</param>
    /// <param name="verb">The verbosity to use</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// toString
    ///     true
    ///     BigRational.toString
    ///     Units.Dutch
    ///     Units.Short
    ///     (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) = "1;2;3 kg[Mass]" <br/>
    /// toString
    ///     false
    ///     BigRational.toString
    ///     Units.Dutch
    ///     Units.Short
    ///     (ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))) = "1;2;3 kg"
    /// </code>
    /// </example>
    let toString hasGroup brf loc verb vu =
        let v, u = vu |> get

        $"{v |> Array.map brf |> Array.distinct |> Array.toReadableString} {Units.toString None None hasGroup loc verb u}"


    /// <summary>
    /// Get the user readable string version in Dutch with verbosity short and group annotation
    /// </summary>
    let toStringDutchShort = toString true BigRational.toString Units.Dutch Units.Short

    /// <summary>
    /// Get the user readable string version in Dutch with verbosity long and group annotation
    /// </summary>
    let toStringDutchLong = toString true BigRational.toString Units.Dutch Units.Long

    /// <summary>
    /// Get the user readable string version in English with verbosity short and group annotation
    /// </summary>
    let toStringEngShort = toString true BigRational.toString Units.English Units.Short

    /// <summary>
    /// Get the user readable string version in English with verbosity long and group annotation
    /// </summary>
    let toStringEngLong = toString true BigRational.toString Units.English Units.Long

    /// <summary>
    /// Get the user readable string version in Dutch with verbosity short,
    /// value as decimal, and group annotation
    /// </summary>
    let toStringDecimalDutchShort =
        toString true (BigRational.toDecimal >> string) Units.Dutch Units.Short

    /// <summary>
    /// Get the user readable string version in Dutch with verbosity long,
    /// value as decimal, and group annotation
    /// </summary>
    let toStringDecimalDutchLong =
        toString true (BigRational.toDecimal >> string) Units.Dutch Units.Long

    /// <summary>
    /// Get the user readable string version in English with verbosity short,
    /// value as decimal, and group annotation
    /// </summary>
    let toStringDecimalEngShort =
        toString true (BigRational.toDecimal >> string) Units.English Units.Short

    /// <summary>
    /// Get the user readable string version in English with verbosity short,
    /// value as decimal, without group annotation
    /// </summary>
    let toStringDecimalEngShortWithoutGroup =
        toString false (BigRational.toDecimal >> string) Units.English Units.Short

    /// <summary>
    /// Get the user readable string version in English with verbosity long,
    /// value as decimal, and group annotation
    /// </summary>
    let toStringDecimalEngLong =
        toString true (BigRational.toDecimal >> string) Units.English Units.Long


    /// <summary>
    /// Get the user readable string version in Dutch with verbosity short,
    /// value as decimal with a fixed precision, and without group annotation
    /// </summary>
    /// <param name="prec">The precision</param>
    /// <param name="vu">The ValueUnit</param>
    /// <example>
    /// <code>
    /// toStringDecimalDutchShortWithPrec 2 (ValueUnit ([|1N/3N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
    /// = "0,33;0,67;0,6 kg"
    /// </code>
    /// </example>
    let toStringDecimalDutchShortWithPrec prec vu =
        let v, u = vu |> get

        let vs =
            v
            |> Array.map BigRational.toDecimal
            |> Array.map (Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision prec)
            |> Array.distinct
            |> Array.toReadableString

        let us = u |> unitToReadableDutchString

        vs + " " + us


    /// <summary>
    /// Parse a string into a ValueUnit
    /// </summary>
    /// <example>
    /// <code>
    /// // returns Success: ValueUnit ([|3N|], Volume (MilliLiter 1N))
    /// fromString "3 mL[Volume]"
    ///
    /// // returns Success: ValueUnit ([|3N|], CombiUnit (Volume (MilliLiter 1N), OpPer, Time (Minute 1N)))
    /// fromString "3 mL/min" = ValueUnit ([|1N; 2N; 3N|], Mass (KiloGram 1N))
    /// </code>
    /// </example>
    let fromString s =
        match s |> parse with
        | FParsec.CharParsers.Success(s, _, _) -> s |> Result.Ok
        | FParsec.CharParsers.Failure(s, err, _) -> $"{s} with error: {err}" |> Result.Error


    let toToken vu =
        let b = vu |> toBase

        sprintf
            "%s %s"
            (b |> getValue |> Array.sort |> Array.map string |> String.concat ",")
            (b |> getUnit |> Group.unitToGroup |> Group.toStringLong)


    module Operators =

        // Arithmetic operators as inline functions (replacing the former
        // `type ValueUnit with static member (*)` intrinsic extension).
        // Distinct symbols (*? /? +? -?) avoid shadowing the built-in
        // numeric operators; consumers `open ...ValueUnit.Operators`.
        let inline ( *? ) vu1 vu2 = calc true (*) vu1 vu2

        let inline (/?) vu1 vu2 = calc true (/) vu1 vu2

        let inline (+?) vu1 vu2 = calc true (+) vu1 vu2

        let inline (-?) vu1 vu2 = calc true (-) vu1 vu2

        let inline (=?) vu1 vu2 = eqs vu1 vu2

        let inline (>?) vu1 vu2 = cmp (>) vu1 vu2

        let inline (<?) vu1 vu2 = cmp (<) vu1 vu2

        let inline (>=?) vu1 vu2 = cmp (>=) vu1 vu2

        let inline (<=?) vu1 vu2 = cmp (<=) vu1 vu2

        /// <summary>
        /// Convert a ValueUnit vu to
        /// </summary>
        /// <param name="vu">The ValueUnit</param>
        /// <param name="u">The Unit to convert to</param>
        let inline (==>) vu u = vu |> convertTo u


    module Dto =

        module Group = Informedica.GenUnits.Lib.Group


        type Dto() =
            member val Value: BigRational[] = [||] with get, set
            member val Unit = "" with get, set
            member val Group = "" with get, set
            member val Short = true with get, set
            member val Language = "" with get, set
            member val Json = "" with get, set


        [<Literal>]
        let english = "english"

        [<Literal>]
        let dutch = "dutch"

        let dto () = Dto()

        let toString (dto: Dto) = $"%A{dto.Value} %s{dto.Unit}"

        let toDto short lang vu =
            let isLang s l =
                l |> String.trim |> String.toLower |> (fun l -> s |> String.startsWith l)

            let l =
                match lang with
                | _ when lang |> isLang english -> Units.English |> Some
                | _ when lang |> isLang dutch -> Units.Dutch |> Some
                | _ -> None

            match l with
            | None -> None
            | Some l ->
                let s = if short then Units.Short else Units.Long

                let v, u = vu |> get

                let g = u |> Group.unitToGroup |> Group.toString

                let u = u |> Units.toString None None false l s //|> String.removeBrackets

                let dto = dto ()
                dto.Value <- v
                dto.Unit <- u
                dto.Group <- g
                dto.Language <- lang
                dto.Short <- short
                dto.Json <- vu |> getUnit |> Json.serialize

                dto |> Some


        let toDtoDutchShort vu = vu |> toDto true dutch |> Option.get
        let toDtoDutchLong vu = vu |> toDto false dutch |> Option.get
        let toDtoEnglishShort vu = vu |> toDto true english |> Option.get
        let toDtoEnglishLong vu = vu |> toDto false english |> Option.get


        let fromDto (dto: Dto) =
            let v = dto.Value

            if dto.Json |> String.notEmpty then
                dto.Json |> Json.deSerialize<Types.Unit> |> Some
            else if dto.Group |> String.isNullOrWhiteSpace then
                dto.Unit |> UnitsParse.fromString
            else
                // TODO only works for "per" combiunits
                let us = dto.Unit |> String.split "/"
                let gs = dto.Group |> String.split "/"

                if us |> List.length <> (gs |> List.length) then
                    printfn $"warning: {us} not the same length as {gs}!"
                    printfn $"unit: {dto.Unit} group {dto.Group}!"

                    $"{dto.Unit}[{dto.Group}]" |> UnitsParse.fromString
                else
                    List.zip us gs
                    |> List.choose (fun (u, g) -> $"{u}[{g}]" |> UnitsParse.fromString)
                    |> function
                        | [] -> None
                        | [ u ] -> u |> Some
                        | u :: rest -> rest |> List.fold (fun acc u -> CombiUnit(acc, OpPer, u)) u |> Some
            |> function
                | None -> None
                | Some u -> v |> withUnit u |> Some


/// Intrinsic operator augmentation for ValueUnit. Lives in the same module
/// group as the type definition (above) so `a * b`, `a =? b`, etc. resolve via
/// member lookup. The `ValueUnit.Operators` module additionally exposes the
/// same operations as the distinct `*? /? +? -?` symbols.
type ValueUnit with

    static member (*)(vu1, vu2) = ValueUnit.calc true (*) vu1 vu2

    static member (/)(vu1, vu2) = ValueUnit.calc true (/) vu1 vu2

    static member (+)(vu1, vu2) = ValueUnit.calc true (+) vu1 vu2

    static member (-)(vu1, vu2) = ValueUnit.calc true (-) vu1 vu2

    static member (=?)(vu1, vu2) = ValueUnit.eqs vu1 vu2

    static member (>?)(vu1, vu2) = ValueUnit.cmp (>) vu1 vu2

    static member (<?)(vu1, vu2) = ValueUnit.cmp (<) vu1 vu2

    static member (>=?)(vu1, vu2) = ValueUnit.cmp (>=) vu1 vu2

    static member (<=?)(vu1, vu2) = ValueUnit.cmp (<=) vu1 vu2

    static member (==>)(vu, u) = vu |> ValueUnit.convertTo u
