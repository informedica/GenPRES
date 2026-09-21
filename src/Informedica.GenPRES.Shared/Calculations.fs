namespace Shared


/// Shared clinical calculations available to both server and client (Fable).
/// All functions use F# units of measure for type safety; UoM annotations are
/// erased at compile time so there is no runtime overhead in JavaScript.
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


    /// Body Surface Area formulas.
    ///
    /// Each public function takes weight in integer grams and height in integer
    /// centimetres (matching the Shared Patient type) and returns BSA in m².
    ///
    /// References:
    ///   - Mosteller RD. N Engl J Med 1987;317:1098
    ///   - Du Bois D, Du Bois EF. Arch Intern Med 1916;17:863-71
    ///   - Haycock GB et al. J Pediatr 1978;93:62-6
    ///   - Gehan EA, George SL. Cancer Chemother Rep 1970;54:225-35
    ///   - Fujimoto S et al. Nihon Eiseigaku Zasshi 1968;23:443-50
    module BSA =

        // -- Internal raw formulas (dimensionless float → dimensionless float) --

        let private mosteller w h = sqrt (w * h / 3600.0)

        let private duBois w h = 0.007184 * (w ** 0.425) * (h ** 0.725)

        let private haycock w h = 0.024265 * (w ** 0.5378) * (h ** 0.3964)

        let private gehanAndGeorge w h = 0.0235 * (w ** 0.51456) * (h ** 0.42246)

        let private fujimoto w h = 0.008883 * (w ** 0.444) * (h ** 0.663)


        // -- Public typed wrappers ----------------------------------------------

        /// Calculate BSA (m²) using the Mosteller formula.
        let calcMosteller (weight: int<gram>) (height: int<cm>) : float<bsa> =
            let w = weight |> Conversions.gramToKg |> float
            let h = height |> Conversions.intCmToFloat |> float
            mosteller w h * 1.0<bsa>


        /// Calculate BSA (m²) using the Du Bois formula.
        let calcDuBois (weight: int<gram>) (height: int<cm>) : float<bsa> =
            let w = weight |> Conversions.gramToKg |> float
            let h = height |> Conversions.intCmToFloat |> float
            duBois w h * 1.0<bsa>


        /// Calculate BSA (m²) using the Haycock formula.
        let calcHaycock (weight: int<gram>) (height: int<cm>) : float<bsa> =
            let w = weight |> Conversions.gramToKg |> float
            let h = height |> Conversions.intCmToFloat |> float
            haycock w h * 1.0<bsa>


        /// Calculate BSA (m²) using the Gehan & George formula.
        let calcGehanAndGeorge (weight: int<gram>) (height: int<cm>) : float<bsa> =
            let w = weight |> Conversions.gramToKg |> float
            let h = height |> Conversions.intCmToFloat |> float
            gehanAndGeorge w h * 1.0<bsa>


        /// Calculate BSA (m²) using the Fujimoto formula.
        let calcFujimoto (weight: int<gram>) (height: int<cm>) : float<bsa> =
            let w = weight |> Conversions.gramToKg |> float
            let h = height |> Conversions.intCmToFloat |> float
            fujimoto w h * 1.0<bsa>


    /// Age calculations for preterm and term infants.
    ///
    /// Provides post-menstrual age (PMA), adjusted (corrected) age,
    /// and chronological age in days — all Fable-compatible.
    ///
    /// References:
    ///   - AAP Pediatrics 2004 (post-menstrual age definition)
    ///   - ISMP medication safety guidance (preterm dosing by PMA)
    module Age =

        /// Convert weeks to days.
        let inline weeksToDays (w: int<week>) : int<day> = w * 7<day / week>


        /// Convert days to weeks (integer division).
        let inline daysToWeeks (d: int<day>) : int<week> = d / 7<day / week>


        /// Full-term gestational age.
        let fullTerm = 40<week>


        /// Post-menstrual age: gestational age at birth + chronological age.
        ///
        /// PMA expresses a preterm infant's developmental age relative to
        /// conception. Returns whole weeks (integer division).
        ///
        /// Parameter order: gestational pair (weeks, days), then chronological age.
        let postMenstrualAge (gestWeeks: int<week>) (gestDays: int<day>) (actAge: int<day>) : int<week> =
            (gestWeeks |> weeksToDays) + gestDays + actAge |> daysToWeeks


        /// Adjusted (corrected) age for preterm infants.
        ///
        /// Subtracts the degree of prematurity from the chronological age.
        /// May return negative values for very preterm infants early in life.
        ///
        /// Parameter order: gestational pair (weeks, days), then chronological age.
        let adjustedAge (gestWeeks: int<week>) (gestDays: int<day>) (chronologicalDays: int<day>) : int<day> =
            let fullTermDays = fullTerm |> weeksToDays
            let prematurityDays = fullTermDays - (gestDays + (gestWeeks |> weeksToDays))
            chronologicalDays - prematurityDays


        /// Chronological age in days between two DateTime values.
        ///
        /// Uses TimeSpan.Days which Fable polyfills via JavaScript Date arithmetic.
        let chronologicalAgeDays (dtBirth: System.DateTime) (dtNow: System.DateTime) : int<day> =
            (dtNow - dtBirth).Days * 1<day>


    // -- Units of measure for renal calculations ----------------------------

    [<Measure>]
    type mg

    [<Measure>]
    type dL

    [<Measure>]
    type mL

    [<Measure>]
    type minute

    [<Measure>]
    type L

    [<Measure>]
    type mmol

    [<Measure>]
    type microMol

    [<Measure>]
    type normalM2


    /// eGFR expressed in mL/min/1.73 m² (standard renal unit).
    type EGfr = float<mL / minute / normalM2>


    /// Biological sex — used as a formula coefficient in eGFR equations.
    [<RequireQualifiedAccess>]
    type Sex =
        | Male
        | Female


    /// Creatinine measurement in one of two units.
    [<RequireQualifiedAccess>]
    type Creatinine =
        | MgPerDl of float<mg / dL>
        | MicroMolPerL of float<microMol / L>


    /// KDIGO 2012 / 2024 GFR classification.
    ///
    /// Named `GfrClassification` to avoid conflict with the existing
    /// `Types.RenalFunction` (which represents a GFR range filter).
    [<RequireQualifiedAccess>]
    type GfrClassification =
        | Normal // >= 90  mL/min/1.73m²
        | MildlyDecreased // 60 – 89
        | MildToModeratelyDecreased // 45 – 59
        | ModerateToSeverelyDecreased // 30 – 44
        | SeverelyDecreased // 15 – 29
        | KidneyFailure // < 15
        | InvalidInput of string


    /// Creatinine and urea unit conversions.
    module RenalConversions =

        // Factor typed as the full conversion ratio so unit math is end-to-end safe.
        // 1 mg/dL = 88.42 µmol/L  →  factor has units µmol·dL / (L·mg)
        let private creatinineKFactor = 88.42<microMol * dL / (L * mg)>

        /// Convert creatinine from mg/dL to µmol/L.
        let creatMgDlToMicroMolL (v: float<mg / dL>) : float<microMol / L> = v * creatinineKFactor

        /// Convert creatinine from µmol/L to mg/dL.
        let creatMicroMolLToMgDl (v: float<microMol / L>) : float<mg / dL> = v / creatinineKFactor

        // BUN mg/dL × 0.3571 = mmol/L  →  factor has units mmol·dL / (L·mg)
        let private ureaMmolFactor = 0.3571<mmol * dL / (L * mg)>

        /// Convert BUN/Urea from mg/dL to mmol/L.
        let ureaMgDlToMmolL (v: float<mg / dL>) : float<mmol / L> = v * ureaMmolFactor

        /// Convert BUN/Urea from mmol/L to mg/dL.
        let ureaMmolLToMgDl (v: float<mmol / L>) : float<mg / dL> = v / ureaMmolFactor


    /// eGFR (estimated Glomerular Filtration Rate) formulas.
    ///
    /// All formulas express eGFR in mL/min/1.73 m².
    ///
    /// References:
    ///   - CKD-EPI 2021: Inker et al., NEJM 2021;385:1737-1749
    ///   - CKD-EPI 2009: Levey et al., Ann Intern Med 2009;150:604-612
    ///   - MDRD: Levey et al., Ann Intern Med 1999;130:461-470
    ///   - Schwartz: Schwartz et al., J Am Soc Nephrol 2009;20:629-637
    module EGfr =

        /// Normalise any Creatinine DU case to mg/dL.
        let private creatToMgDl =
            function
            | Creatinine.MgPerDl v -> v
            | Creatinine.MicroMolPerL v -> RenalConversions.creatMicroMolLToMgDl v

        let private toEgfr (x: float) : EGfr = x * 1.0<mL / minute / normalM2>


        /// CKD-EPI Creatinine 2021 eGFR (no race coefficient).
        let ckdEpi2021 (sex: Sex) (age: float<year>) (creatinine: Creatinine) : EGfr =
            let sCr = creatinine |> creatToMgDl |> float
            let age = float age

            let kappa, alpha, sexFactor =
                match sex with
                | Sex.Female -> 0.7, -0.241, 1.012
                | Sex.Male -> 0.9, -0.302, 1.0

            let ratio = sCr / kappa

            142.0
            * (min ratio 1.0 ** alpha)
            * (max ratio 1.0 ** -1.200)
            * (0.9938 ** age)
            * sexFactor
            |> toEgfr


        [<RequireQualifiedAccess>]
        type Race2009 =
            | Black
            | Other

        /// CKD-EPI Creatinine 2009 eGFR. Prefer ckdEpi2021 for new work.
        let ckdEpi2009 (sex: Sex) (race: Race2009) (age: float<year>) (creatinine: Creatinine) : EGfr =
            let sCr = creatinine |> creatToMgDl |> float
            let age = float age

            let kappa, alpha, sexFactor =
                match sex with
                | Sex.Female -> 0.7, -0.329, 1.018
                | Sex.Male -> 0.9, -0.411, 1.0

            let raceFactor =
                match race with
                | Race2009.Black -> 1.159
                | Race2009.Other -> 1.0

            let ratio = sCr / kappa

            141.0
            * (min ratio 1.0 ** alpha)
            * (max ratio 1.0 ** -1.209)
            * (0.993 ** age)
            * sexFactor
            * raceFactor
            |> toEgfr


        [<RequireQualifiedAccess>]
        type Race4v =
            | Black
            | Other

        /// MDRD 4-variable eGFR formula.
        let mdrd (sex: Sex) (race: Race4v) (age: float<year>) (creatinine: Creatinine) : EGfr =
            let sCr = creatinine |> creatToMgDl |> float
            let age = float age

            let sexFactor =
                match sex with
                | Sex.Female -> 0.742
                | Sex.Male -> 1.0

            let raceFactor =
                match race with
                | Race4v.Black -> 1.212
                | Race4v.Other -> 1.0

            175.0 * (sCr ** -1.154) * (age ** -0.203) * sexFactor * raceFactor |> toEgfr


        /// Bedside Schwartz eGFR for children / adolescents.
        let schwartz (height: float<cm>) (creatinine: Creatinine) : EGfr =
            let h = float height
            let sCr = creatinine |> creatToMgDl |> float
            0.413 * (h / sCr) |> toEgfr


    /// Classify renal function from an eGFR value (KDIGO 2012 staging).
    let classifyGfr (eGfr: EGfr) : GfrClassification =
        let v = float eGfr

        match v with
        | v when v >= 90.0 -> GfrClassification.Normal
        | v when v >= 60.0 -> GfrClassification.MildlyDecreased
        | v when v >= 45.0 -> GfrClassification.MildToModeratelyDecreased
        | v when v >= 30.0 -> GfrClassification.ModerateToSeverelyDecreased
        | v when v >= 15.0 -> GfrClassification.SeverelyDecreased
        | v when v >= 0.0 -> GfrClassification.KidneyFailure
        | _ -> GfrClassification.InvalidInput $"Negative eGFR: {v}"
