namespace Informedica.GenCore.Tests


/// The calculation weights and heights that the tests round estimates to. The GenCORE, server and MCP tests share
/// this file.
module CalculationWeightsAndHeightsFixture =

    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients


    /// Weights in steps of 50 gram from 400 gram, 100 gram from 2 kg, 500 gram from 10.5 kg and 1 kg from 21 kg up
    /// to 100 kg, and heights from 40 to 220 cm.
    let calc: CalculationWeightsAndHeights =
        {
            Weights =
                [|
                    [| 400..50..1950 |]
                    [| 2000..100..10000 |]
                    [| 10500..500..20000 |]
                    [| 21000..1000..100000 |]
                |]
                |> Array.concat
                |> Array.map Conversions.gramFromInt
            Heights = [| 40..220 |] |> Array.map Conversions.cmFromInt
        }
