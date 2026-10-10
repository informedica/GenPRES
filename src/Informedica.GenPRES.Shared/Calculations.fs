namespace Shared


/// The clinical calculations the patient display uses: the Du Bois body surface area and
/// the conversions it needs. All functions use F# units of measure for type safety; UoM
/// annotations are erased at compile time so there is no runtime overhead in JavaScript.
module Calculations =

    open Shared.Types


    [<Measure>]
    type bsa = m^2


    /// Unit conversion helpers (gram ↔ kg, int ↔ float).
    module Conversions =

        /// Convert integer grams to float kilograms.
        let gramToKg (w: int<gram>) : float<kg> = (float w / 1000.0) * 1.0<kg>


        /// Convert integer centimetres to float centimetres (lifts int → float).
        let intCmToFloat (h: int<cm>) : float<cm> = float h * 1.0<cm>


    /// Body Surface Area.
    ///
    /// Takes weight in integer grams and height in integer centimetres (matching the
    /// Shared Patient type) and returns BSA in m².
    ///
    /// Reference: Du Bois D, Du Bois EF. Arch Intern Med 1916;17:863-71
    module BSA =

        // -- Internal raw formula (dimensionless float → dimensionless float) --

        let private duBois w h = 0.007184 * (w ** 0.425) * (h ** 0.725)


        // -- Public typed wrapper -----------------------------------------------

        /// Calculate BSA (m²) using the Du Bois formula.
        let calcDuBois (weight: int<gram>) (height: int<cm>) : float<bsa> =
            let w = weight |> Conversions.gramToKg |> float
            let h = height |> Conversions.intCmToFloat |> float
            duBois w h * 1.0<bsa>


    /// Age conversions.
    module Age =

        /// Convert weeks to days.
        let inline weeksToDays (w: int<week>) : int<day> = w * 7<day / week>
