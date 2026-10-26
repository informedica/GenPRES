namespace Informedica.GenPRES.Shared


/// The clinical calculation the patient readers use: weeks to days, with units of measure.
module Calculations =

    open Informedica.GenPRES.Shared.Types


    /// Age conversions.
    module Age =

        /// Convert weeks to days.
        let inline weeksToDays (w: int<week>) : int<day> = w * 7<day / week>
