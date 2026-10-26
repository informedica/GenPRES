namespace Informedica.GenPRES.Client.Core.Models

/// The body surface area by the Du Bois formula, and the conversions it needs. Takes weight in
/// integer grams and height in integer centimetres, as the Shared patient holds them, and returns
/// BSA in m². Reference: Du Bois D, Du Bois EF. Arch Intern Med 1916;17:863-71
module BodySurfaceArea =

    open Informedica.GenPRES.Shared.Types


    [<Measure>]
    type bsa = m^2


    /// Unit conversion helpers (gram ↔ kg, int ↔ float).
    module Conversions =

        /// Convert integer grams to float kilograms.
        let gramToKg (w: int<gram>) : float<kg> = (float w / 1000.0) * 1.0<kg>


        /// Convert integer centimetres to float centimetres (lifts int → float).
        let intCmToFloat (h: int<cm>) : float<cm> = float h * 1.0<cm>


    // -- Internal raw formula (dimensionless float → dimensionless float) --

    let private duBois w h = 0.007184 * (w ** 0.425) * (h ** 0.725)


    // -- Public typed wrapper -----------------------------------------------

    /// Calculate BSA (m²) using the Du Bois formula.
    let calcDuBois (weight: int<gram>) (height: int<cm>) : float<bsa> =
        let w = weight |> Conversions.gramToKg |> float
        let h = height |> Conversions.intCmToFloat |> float
        duBois w h * 1.0<bsa>
