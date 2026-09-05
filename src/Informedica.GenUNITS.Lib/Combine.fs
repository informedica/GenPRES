namespace Informedica.GenUnits.Lib


module Combine =

    open Core

    //----------------------------------------------------------------------------
    // Create CombiUnit
    //----------------------------------------------------------------------------


    /// <summary>
    /// Create a CombiUnit with u1, Operator op and unit u2.
    /// Recalculates the unit n values. Takes care of dividing
    /// by the same unitgroups and multipying with count groups.
    /// </summary>
    /// <param name="u1">Unit 1</param>
    /// <param name="op">Operator</param>
    /// <param name="u2">Unit 2</param>
    /// <example>
    /// <code>
    /// createCombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N)) =
    /// CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 1N))
    /// // Example with same unit division
    /// createCombiUnit (Mass (KiloGram 4N), OpPer, Mass (KiloGram 2N)) =
    /// Count (Times 2N)
    /// // Example with count unit multiplication
    /// createCombiUnit (Mass (KiloGram 4N), OpTimes, Count (Times 2N)) =
    /// Mass (KiloGram 8N)
    /// </code>
    /// </example>
    /// <remarks>
    /// Will fail when adding or subtracting units with different groups
    /// </remarks>
    // Known limitation: the OpPer same-group simplification below does not
    // cancel when u2 is a CombiUnit that *contains* u1 (see the inline note
    // at the OpPer branch). Such cases are left to simplifyUnit upstream.
    let createCombiUnit (u1, op, u2) =
        match u1, u2 with
        | NoUnit, NoUnit -> NoUnit
        | ZeroUnit, ZeroUnit -> ZeroUnit
        | _ ->
            match op with
            | OpPer ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup ZeroUnit || u2 |> Group.eqsGroup ZeroUnit -> ZeroUnit
                // this is not enough when u2 is combiunit but
                // contains u1!
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = (u1 |> Units.getUnitValue)
                    let n2 = (u2 |> Units.getUnitValue)

                    match n1, n2 with
                    | Some x1, Some x2 -> count |> Units.setUnitValue (x1 / x2)
                    | _ -> count
                | _ when u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 / x2)
                    | _ -> u1
                | _ -> (u1, OpPer, u2) |> CombiUnit
            | OpTimes ->
                match u1, u2 with
                | _ when u1 |> Group.eqsGroup ZeroUnit -> ZeroUnit
                | _ when u2 |> Group.eqsGroup ZeroUnit -> ZeroUnit
                | _ when u1 |> Group.eqsGroup count && u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 * x2)
                    | _ -> u1
                | _ when u1 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u2 |> Units.setUnitValue (x1 * x2)
                    | _ -> u2
                | _ when u2 |> Group.eqsGroup count ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 * x2)
                    | _ -> u1
                | _ ->
                    // In physics, multiplying quantities with different units, like mass and volume,
                    // doesn't yield a meaningful result and is generally not done.

                    // The multiplication of physical quantities should be dimensionally consistent,
                    // meaning the units on both sides of the equation should be the same.
                    // This is a principle of dimensional analysis.

                    // Mass is typically measured in kilograms (kg) and volume is measured in cubic meters (m^3).
                    // The product of mass and volume would have units of kg*m^3,
                    // which is not a standard unit and doesn't correspond to any commonly recognized physical quantity.

                    // However, there are instances in physics where you multiply quantities with different units
                    // to get a meaningful result. For example, multiplying mass (kg) and acceleration (m/s^2)
                    // gives you force (N), which is meaningful and consistent with Newton's second law (F=ma).
                    (u1, OpTimes, u2) |> CombiUnit
            | OpPlus
            | OpMinus ->
                match u1, u2 with
                | ZeroUnit, u
                | u, ZeroUnit -> u
                | _ when u1 |> Group.eqsGroup u2 ->
                    let n1 = u1 |> Units.getUnitValue
                    let n2 = u2 |> Units.getUnitValue

                    match n1, n2 with
                    | Some x1, Some x2 -> u1 |> Units.setUnitValue (x1 + x2)
                    | _ -> u1
                | _ -> raise (System.NotSupportedException $"Cannot combine units {u1} and {u2} with operator {op}")


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpPer and unit u2. If u1 is u2
    /// then return Count (Times 1N)
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// <code>
    /// Mass (KiloGram 1N) |> per (Volume (Liter 2N)) = CombiUnit (Mass (KiloGram 1N), OpPer, Volume (Liter 2N))
    /// // units are the same, so return Count (Times 1N)
    /// Mass (KiloGram 1N) |> per (Mass (KiloGram 1N)) = Count (Times 1N)
    /// </code>
    /// </example>
    let per u2 u1 = (u1, OpPer, u2) |> createCombiUnit


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpTimes and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Distance (Meter 2N) |> times (Distance (Meter 3N)) = CombiUnit (Distance (Meter 2N), OpTimes, Distance (Meter 3N))
    /// </example>
    let times u2 u1 = (u1, OpTimes, u2) |> createCombiUnit


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpPlus and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Mass (KiloGram 1N) |> plus (Volume (Liter 1N)) = CombiUnit (Mass (KiloGram 1N), OpPlus, Volume (Liter 1N))
    /// </example>
    let plus u2 u1 = (u1, OpPlus, u2) |> createCombiUnit


    /// <summary>
    /// Create a CombiUnit with u1, Operator OpMinus and unit u2.
    /// </summary>
    /// <param name="u2"></param>
    /// <param name="u1"></param>
    /// <example>
    /// Mass (KiloGram 1N) |> minus (Volume (Liter 1N)) = CombiUnit (Mass (KiloGram 1N), OpMinus, Volume (Liter 1N))
    /// </example>
    let minus u2 u1 =
        match u2, u1 with
        | ZeroUnit, u
        | u, ZeroUnit -> u
        | _ -> (u1, OpMinus, u2) |> createCombiUnit
