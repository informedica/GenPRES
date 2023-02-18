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


    let unitFromString s =
        if s |> String.isNullOrWhiteSpace then None
        else
            try
                // ugly hack need to fix this
                // in the units lib
                let s =
                    s
                    |> String.replace "x[Count]" "#"
                    |> String.replace "x" "/"
                    |> String.replace "#" "x[Count]"

                "1 " + s
                |> fromString
                |> get
                |> snd
                |> Some
            with
            | e ->
                // printfn $"could not parse to unit: %s{s}\{e}"
                None


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


    module Units =

        let noUnit = NoUnit


