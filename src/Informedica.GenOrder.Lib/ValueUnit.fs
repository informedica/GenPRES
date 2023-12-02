namespace Informedica.GenOrder.Lib



/// Helper functions to
/// facilitate the use of the
/// `Informedica.GenUnits.Lib`
module ValueUnit =


    open Informedica.GenUnits.Lib
    open ValueUnit

    /// <summary>
    /// Return a Unit as a short Dutch string
    /// </summary>
    let unitToString =
        Units.toString Units.Dutch Units.Short


    /// <summary>
    /// Check if a Unit is an adjust unit, i.e.
    /// kg or m2.
    /// </summary>
    /// <param name="u">The Unit</param>
    let isAdjust (u : Unit) =
        u |> Group.eqsGroup (Units.Weight.kiloGram) ||
        u |> Group.eqsGroup (Units.BSA.m2)


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



    module Units =

        let noUnit = NoUnit


