namespace Informedica.GenOrder.Lib



/// Helper functions to
/// facilitate the use of the
/// `Informedica.GenUnits.Lib`
module ValueUnit =

    open MathNet.Numerics

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open ValueUnit


    let valueToBase u v =
        create u v
        |> toBaseValue


    let unitToString =
        Units.toString Units.Dutch Units.Short


    let calcUnit op u1 u2 =

        match u1, u2 with
        | NoUnit, _
        | _, NoUnit -> NoUnit
        | u1, u2 ->
            let vu1 = 1N |> createSingle u1
            let vu2 = 1N |> createSingle u2

            vu1 |> op <| vu2
            |> get
            |> snd


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


