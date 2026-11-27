namespace Informedica.GenCore.Tests


/// The calculation weights and heights that the tests round estimates to. The GenCORE, server and MCP tests share
/// this file.
module CalculationWeightsAndHeightsFixture =

    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients


    /// The lists of the production sheets calc_weight and calc_height, read on 2026-11-19: weights in steps of
    /// 50 gram from 400 gram, 100 gram from 2 kg, 500 gram from 10.5 kg, 1 kg from 21 kg and 5 kg from 105 kg up
    /// to 250 kg, and heights from 30 to 220 cm.
    let calc: CalculationWeightsAndHeights =
        {
            Weights =
                [|
                    [| 400..50..1950 |]
                    [| 2000..100..10000 |]
                    [| 10500..500..20000 |]
                    [| 21000..1000..100000 |]
                    [| 105000..5000..250000 |]
                |]
                |> Array.concat
                |> Array.map Conversions.gramFromInt
            Heights = [| 30..220 |] |> Array.map Conversions.cmFromInt
        }
