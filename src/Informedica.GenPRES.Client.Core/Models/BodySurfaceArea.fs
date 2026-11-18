namespace Informedica.GenPRES.Client.Core.Models

/// Calculates the body surface area in m² with the Du Bois formula from a weight in grams and a height
/// in centimetres. Reference: Du Bois D, Du Bois EF. Arch Intern Med 1916;17:863-71
module BodySurfaceArea =

    open Informedica.GenPRES.Shared.Types


    [<Measure>]
    type bsa = m^2


    /// Unit conversion helpers (gram ↔ kg, int ↔ float).
    module Conversions =

        /// Convert integer grams to float kilograms.
        let gramToKg (w: int<gram>) : float<kg> = (float w / 1000.0) * 1.0<kg>


        /// Convert integer centimetres to float centimetres.
        let intCmToFloat (h: int<cm>) : float<cm> = float h * 1.0<cm>


    // The Du Bois formula on a weight in kg and a height in cm, both without a unit of measure.
    let private duBois w h = 0.007184 * (w ** 0.425) * (h ** 0.725)


    /// Calculate BSA (m²) using the Du Bois formula.
    let calcDuBois (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        duBois w h * 1.0<bsa>
