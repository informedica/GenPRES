namespace Informedica.GenPRES.Shared


/// The clinical calculation the patient readers use: weeks and months to days, with units of measure.
module Calculations =

    open Informedica.GenPRES.Shared.Types


    /// Age conversions.
    module Age =

        /// Convert weeks to days.
        let inline weeksToDays (w: int<week>) : int<day> = w * 7<day / week>

        /// The days of a number of months: each 12 months a year of 365 days, the rest 30 days each.
        let monthsToDays (m: int<month>) : int<day> = (int m / 12 * 365 + int m % 12 * 30) * 1<day>
