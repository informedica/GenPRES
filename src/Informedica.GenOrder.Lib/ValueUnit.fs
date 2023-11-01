namespace Informedica.GenOrder.Lib



/// Helper functions to
/// facilitate the use of the
/// `Informedica.GenUnits.Lib`
module ValueUnit =

    open MathNet.Numerics

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open ValueUnit



    let unitToString =
        Units.toString Units.Dutch Units.Short


    let isAdjust (u : Unit) =
        u |> Group.eqsGroup (Units.Weight.kiloGram) ||
        u |> Group.eqsGroup (Units.BSA.m2)


    let correctAdjustOrder vu =
        let v, u = vu |> get
        match u |> getUnits with
        | [u1; u2; u3] when u3 |> isAdjust ->
            u1
            |> Units.per u3 |> Units.per u2
            |> withValue v
        | _ -> vu



    module Units =

        let noUnit = NoUnit


