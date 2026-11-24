namespace Informedica.GenCore.Lib


module Calculations =


    open Informedica.Utils.Lib.BCL


    module Age =


        /// Converts months to days. Each 12 months count as a year of 365 days, each remaining month as 30 days.
        let monthsToDays (m: int<month>) =
            (int m / Constants.monthsInYear
             |> Conversions.yearFromInt
             |> Conversions.intYearsToDays)
            + (int m % Constants.monthsInYear
               |> (*) Constants.daysInMonth
               |> Conversions.dayFromInt)


        /// Converts an age in months, which may hold a fraction of a month, to days. Each 12 months count as
        /// a year of 365 days, each remaining month as 30 days, so 12.5 months is 380 days.
        let fractionalMonthsToDays m =
            let months = Constants.monthsInYear |> BigRational.fromInt
            let years = m / months |> BigRational.ToBigInt |> BigRational.FromBigInt

            years * (Constants.daysInYear |> BigRational.fromInt)
            + (m - years * months) * (Constants.daysInMonth |> BigRational.fromInt)


        /// Converts an age in days to months. Each 365 days count as 12 months, the remaining days as 30 days
        /// to the month. For whole months this is the inverse of fractionalMonthsToDays. The months after the
        /// whole years are capped at 11.99, so 360 to 364 remaining days do not match a rule for the next year
        /// up to 5 days early.
        let daysToFractionalMonths d =
            // The highest number of months below a whole year that a G-Standaard age bound with two decimals holds.
            let highestMonthsBelowAYear = 1199N / 100N
            let days = Constants.daysInYear |> BigRational.fromInt
            let years = d / days |> BigRational.ToBigInt |> BigRational.FromBigInt

            // Operators.min is written in full because min is also the unit of measure for minutes.
            years * (Constants.monthsInYear |> BigRational.fromInt)
            + Operators.min
                ((d - years * days) / (Constants.daysInMonth |> BigRational.fromInt))
                highestMonthsBelowAYear


        let yearsMonthsWeeksDaysToDays y (m: int<month>) w d =
            let dy, dm, dw = y |> Conversions.intYearsToDays, m |> monthsToDays, w |> Conversions.weeksToDays

            d + dy + dm + dw


        let ageToStringNL yrs mos wks dys =
            let yToStr = Conversions.yearToString "jaar" "jaar"
            let mToStr = Conversions.monthToString "maand" "maanden"
            let wToStr = Conversions.weekToString "week" "weken"
            let dToStr = Conversions.dayToString "dag" "dagen"

            [
                yrs |> Option.map yToStr
                mos |> Option.map mToStr
                wks |> Option.map wToStr
                dys |> Option.map dToStr
            ]
            |> List.map (Option.defaultValue "")


        let ageToStringNlShort yrs mos wks dys =
            ageToStringNL yrs mos wks dys
            |> function
                | [ ys; ms; _; _ ] when ys |> String.notEmpty -> [ ys; ms ]
                | [ _; ms; ws; _ ] when ms |> String.notEmpty -> [ ms; ws ]
                | [ _; _; ws; ds ] when ws |> String.notEmpty -> [ ws; ds ]
                | xs -> xs
            |> List.filter String.notEmpty
            |> String.concat ", "


    module BSA =


        let mosteller = fun w h -> sqrt (w * h / 3600.)

        let duBois = fun w h -> 0.007184 * (w ** 0.425) * (h ** 0.725)

        let haycock = fun w h -> 0.024265 * (w ** 0.5378) * (h ** 0.3964)

        let gehanAndGeorge = fun w h -> 0.0235 * (w ** 0.51456) * (h ** 0.42246)

        let fujimoto = fun w h -> 0.008883 * (w ** 0.444) * (h ** 0.663)


        let calcBSA formula fixPrec (weight: decimal<kg>) (height: decimal<cm>) =
            let w = decimal weight |> float
            let h = decimal height |> float

            formula w h
            |> decimal
            |> fun x ->
                let x =
                    match fixPrec with
                    | Some p -> x |> Decimal.fixPrecision p
                    | None -> x

                x * 1m<bsa>


        let calcDuBois = calcBSA duBois

        let calcMosteller = calcBSA mosteller

        let calcHaycock = calcBSA haycock

        let calcGehanAndGeorge = calcBSA gehanAndGeorge

        let calcFujimoto = calcBSA fujimoto


    module Renal =


        type Gender =
            | Male
            | Female


        type Race =
            | Black
            | Other


        type Creat =
            | CreatinineMicroMolePerLiter of float<microMol / L>
            | CreatinineMilligramPerDeciLiter of float<mg / dL>


        type Cystatin = | CystatinMilligramPerLiter of float<mg / L>


        type Urea =
            | UreaMilligramPerDeciLiter of float<mg / dL>
            | UreaMilliMolePerLiter of float<mmol / L>


        type RenalFunction =
            | Normal
            | MildlyDecreased
            | MildToModeratelyDecreased
            | ModerateToSeverlyDecreased
            | SeverelyDecreased
            | KidneyFailure
            | InvalidKidneyFunction of string


        let normal = 90.<mL / min / normalM2>

        let mild = 60.<mL / min / normalM2>

        let moderate = 45.<mL / min / normalM2>

        let severe = 30.<mL / min / normalM2>

        let failure = 15.<mL / min / normalM2>


        let renalFunction eGfr =
            match eGfr with
            | _ when eGfr >= normal -> Normal
            | _ when eGfr >= mild -> MildlyDecreased
            | _ when eGfr >= moderate -> MildToModeratelyDecreased
            | _ when eGfr >= severe -> ModerateToSeverlyDecreased
            | _ when eGfr >= failure -> SeverelyDecreased
            | _ when eGfr < failure && eGfr >= 0.<mL / min / normalM2> -> KidneyFailure
            | _ -> $"this {eGfr} is not valid" |> InvalidKidneyFunction


        let toMlMinNormBsa x = x * 1.<mL / min / normalM2>


        let creat09Formula (sCr: float<mg / dL>) (age: float<year>) alpha k a b =
            let sCr = sCr |> float
            let age = age |> float

            141.
            * (([ sCr / k; 1. ] |> List.min) ** alpha)
            * (([ sCr / k; 1. ] |> List.max) ** -1.209)
            * (0.993 ** age)
            * a
            * b
            |> toMlMinNormBsa


        let calcCreatinine09 gend race age creat =
            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let alpha, k, a =
                match gend with
                | Female -> -0.329, 0.7, 1.018
                | Male -> -0.411, 0.9, 1.

            let b =
                match race with
                | Black -> 1.159
                | Other -> 1.

            creat09Formula sCr age alpha k a b


        let creat21Formula (sCr: float<mg / dL>) (age: float<year>) alpha k a =
            let sCr = sCr |> float
            let age = age |> float

            142.
            * (([ sCr / k; 1. ] |> List.min) ** alpha)
            * (([ sCr / k; 1. ] |> List.max) ** -1.200)
            * (0.9938 ** age)
            * a
            |> toMlMinNormBsa


        let calcCreatinine21 gend age creat =
            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let alpha, k, a =
                match gend with
                | Female -> -0.241, 0.7, 1.018
                | Male -> -0.302, 0.9, 1.

            creat21Formula sCr age alpha k a


        let cystatinCreatinine12Formula (sCr: float<mg / dL>) (sCy: float<mg / L>) (age: float<year>) alpha k a b =
            let sCr = sCr |> float
            let sCy = sCy |> float
            let age = age |> float

            135.
            * (([ sCr / k; 1. ] |> List.min) ** alpha)
            * (([ sCr / k; 1. ] |> List.max) ** -0.601)
            * (([ sCy / 0.8; 1. ] |> List.min) ** -0.375)
            * (([ sCy / 0.8; 1. ] |> List.max) ** -0.711)
            * (0.995 ** age)
            * a
            * b
            |> toMlMinNormBsa


        let calcCystatinCreatinine12 gend race age creat cystatin =
            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let (CystatinMilligramPerLiter sCy) = cystatin

            let alpha, k, a =
                match gend with
                | Female -> -0.248, 0.7, 0.969
                | Male -> -0.207, 0.9, 1.

            let b =
                match race with
                | Black -> 1.08
                | Other -> 1.

            cystatinCreatinine12Formula sCr sCy age alpha k a b


        let cystatinCreatinine21Formula (sCr: float<mg / dL>) (sCy: float<mg / L>) (age: float<year>) alpha k a =
            let sCr = sCr |> float
            let sCy = sCy |> float
            let age = age |> float

            135.
            * (([ sCr / k; 1. ] |> List.min) ** alpha)
            * (([ sCr / k; 1. ] |> List.max) ** -0.544)
            * (([ sCy / 0.8; 1. ] |> List.min) ** -0.323)
            * (([ sCy / 0.8; 1. ] |> List.max) ** -0.778)
            * (0.9961 ** age)
            * a
            |> toMlMinNormBsa


        let calcCystatinCreatinine21 gend age creat cystatin =
            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let (CystatinMilligramPerLiter sCy) = cystatin

            let alpha, k, a =
                match gend with
                | Female -> -0.219, 0.7, 0.963
                | Male -> -0.144, 0.9, 1.

            cystatinCreatinine21Formula sCr sCy age alpha k a


        let cystatin12Formula (sCy: float<mg / L>) (age: float<year>) a =
            let sCy = sCy |> float
            let age = age |> float

            133.
            * (([ sCy / 0.8; 1. ] |> List.min) ** -0.4999)
            * (([ sCy / 0.8; 1. ] |> List.max) ** -1.328)
            * (0.996 ** age)
            * a
            |> toMlMinNormBsa


        let calcCystatin12 gend age cystatin =
            let (CystatinMilligramPerLiter sCy) = cystatin

            let a =
                match gend with
                | Female -> 0.932
                | Male -> 1.

            cystatin12Formula sCy age a


        let mdrdFormula (sCr: float<mg / dL>) (age: float<year>) a b =
            let sCr = sCr |> float
            let age = age |> float

            175. * (sCr ** -1.154) * (age ** -0.203) * a * b |> toMlMinNormBsa


        let calcMDRD gend race age creat =
            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let a =
                match gend with
                | Female -> 0.742
                | Male -> 1.

            let b =
                match race with
                | Black -> 1.212
                | Other -> 1.

            mdrdFormula sCr age a b


        let pediatricSchwartzFormula (sCr: float<mg / dL>) (height: float<cm>) =
            let sCr = sCr |> float
            let height = height |> float

            0.413 * (height / sCr) |> toMlMinNormBsa


        let calcPediatricScharz height creat =
            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            pediatricSchwartzFormula sCr height


        let pediatricCystatinCreatinineCKIDFormula
            (cyst: float<mg / L>)
            (sCr: float<mg / dL>)
            (bun: float<mg / dL>)
            (height: float<m>)
            k
            =
            let sCr = sCr |> float
            let cyst = cyst |> float
            let bun = bun |> float
            let height = height |> float

            39.8
            * // 39.8
            ((height / sCr) ** 0.456)
            * // x [ht/Scr]0.456
            ((1.8 / cyst) ** 0.418)
            * // x [1.8/cysC]0.418
            ((30. / bun) ** 0.079)
            * // x [30/BUN]0.079
            ((height / 1.4) ** 0.179)
            * // x [ht/1.4]0.179
            k // x [1.076male] [1.00female]
            |> toMlMinNormBsa


        let calcPediatricCystatinCreatinineCKID gender height creat cystatin bun =
            let bun =
                match bun with
                | UreaMilligramPerDeciLiter v -> v
                | UreaMilliMolePerLiter v -> v |> Conversions.Urea.toMilliGramPerDeciLiter

            let sCr =
                match creat with
                | CreatinineMilligramPerDeciLiter v -> v
                | CreatinineMicroMolePerLiter v -> v |> Conversions.Creatinine.toMilliGramPerDeciLiter

            let (CystatinMilligramPerLiter cyst) = cystatin

            let k =
                match gender with
                | Female -> 1.
                | Male -> 1.076

            pediatricCystatinCreatinineCKIDFormula cyst sCr bun height k


    /// Calculates the interventions of the emergency list that are not medication: the tube, the umbilical
    /// lines and the energy for defibrillation and cardioversion.
    module Emergency =


        /// An intervention with the calculated value.
        [<RequireQualifiedAccess>]
        type Intervention =
            /// The size of the tube for a patient under 3 kg.
            | TubeSize of decimal<mm>
            /// The size of a tube with a cuff.
            | CuffedTubeSize of decimal<mm>
            /// The size of a tube without a cuff.
            | UncuffedTubeSize of decimal<mm>
            /// The length of an oral tube.
            | OralTubeLength of decimal<cm>
            /// The length of a nasal tube.
            | NasalTubeLength of decimal<cm>
            /// The size of an umbilical line.
            | UmbilicalLineSize of decimal<french>
            /// The length of an umbilical artery line.
            | UmbilicalArteryLineLength of decimal<cm>
            /// The length of an umbilical vein line.
            | UmbilicalVeinLineLength of decimal<cm>
            /// The energy of a defibrillation.
            | Defibrillation of decimal<joule>
            /// The energy of a cardioversion.
            | Cardioversion of decimal<joule>


        /// The formula an intervention is calculated with.
        [<RequireQualifiedAccess>]
        type Formula =
            /// 2.5 mm under 1 kg, else 3 mm.
            | TubeSizeByWeight
            /// 6.632 + 1.822 x ln(kg) cm.
            | OralTubeLengthByWeight
            /// (45 + 1.15 x sqrt(gram)) / 10 cm.
            | NasalTubeLengthByWeight
            /// 3.5 French under 1.5 kg, else 5 French.
            | UmbilicalLineSizeByWeight
            /// kg x 4 + 7 cm, for a weight under 1.5 kg.
            | UmbilicalArteryLineLengthUnder1500Gram
            /// kg x 2.5 + 9.7 cm, for a weight from 1.5 kg.
            | UmbilicalArteryLineLengthFrom1500Gram
            /// kg x 1.5 + 5.5 cm.
            | UmbilicalVeinLineLengthByWeight
            /// 3.5 + years / 4 mm, rounded to 0.5 mm, at most 7 mm.
            | CuffedTubeSizeByAge
            /// 4 + years / 4 mm, rounded to 0.5 mm, at most 7 mm.
            | UncuffedTubeSizeByAge
            /// 12 + years / 2 cm, rounded to 0.5 cm.
            | OralTubeLengthByAge
            /// 15 + years / 2 cm, rounded to 0.5 cm.
            | NasalTubeLengthByAge
            /// 4 joule/kg, as an energy the defibrillator can be set to.
            | DefibrillationByWeight
            /// 2 joule/kg, as an energy the defibrillator can be set to.
            | CardioversionByWeight


        /// An intervention with its calculated value and the formula that gave the value.
        type Calculation =
            {
                /// The intervention with the calculated value.
                Intervention: Intervention
                /// The formula the value is calculated with.
                Formula: Formula
            }


        /// The weight under which the interventions are calculated from the weight alone.
        let lowWeight = 3m<kg>

        /// The weight under which a smaller umbilical line and another artery line formula apply.
        let lowUmbilicalWeight = 1.5m<kg>

        /// The largest tube size.
        let maxTubeSize = 7m<mm>

        /// The energies a defibrillator can be set to, from low to high.
        let energies =
            [ 1m; 2m; 3m; 5m; 7m; 10m; 20m; 30m; 50m; 70m; 100m; 150m ]
            |> List.map ((*) 1m<joule>)


        /// Returns an age in days as years, a year being 365 days.
        let years (age: int<day>) = decimal (int age) / decimal Constants.daysInYear * 1m<year>


        /// Rounds a value to the nearest multiple of 0.5. A value halfway between two multiples goes to the
        /// whole number.
        let roundToHalf (x: decimal<'u>) : decimal<'u> =
            round (decimal x * 2m) / 2m |> LanguagePrimitives.DecimalWithMeasure


        /// Rounds a length to 3 significant digits: 7.8949 cm gives 7.89 cm and 11.936 cm gives 11.9 cm.
        let roundLength (length: decimal<cm>) =
            length |> decimal |> Decimal.fixPrecision 3 |> Conversions.cmFromDecimal


        /// Returns the tube size for a weight under 3 kg.
        let tubeSizeByWeight weight = if weight < 1m<kg> then 2.5m<mm> else 3m<mm>


        /// Returns the oral tube length for a weight under 3 kg, rounded to 3 significant digits. The weight
        /// has to be above 0 kg.
        let oralTubeLengthByWeight (weight: decimal<kg>) =
            6.632 + 1.822 * log (weight |> decimal |> float)
            |> decimal
            |> Conversions.cmFromDecimal
            |> roundLength


        /// Returns the nasal tube length for a weight under 3 kg, rounded to 3 significant digits.
        let nasalTubeLengthByWeight weight =
            let gram = weight |> Conversions.decimalKgToDecimalGram |> decimal |> float

            (45. + 1.15 * sqrt gram) / 10.
            |> decimal
            |> Conversions.cmFromDecimal
            |> roundLength


        /// Returns the size of the umbilical line for a weight under 3 kg.
        let umbilicalLineSize weight =
            if weight < lowUmbilicalWeight then
                3.5m<french>
            else
                5m<french>


        /// Returns the length of the umbilical artery line for a weight under 1.5 kg, rounded to 3 significant
        /// digits.
        let umbilicalArteryLineLengthUnder1500Gram (weight: decimal<kg>) = weight * 4m<cm / kg> + 7m<cm> |> roundLength


        /// Returns the length of the umbilical artery line for a weight from 1.5 kg and under 3 kg, rounded to
        /// 3 significant digits.
        let umbilicalArteryLineLengthFrom1500Gram (weight: decimal<kg>) =
            weight * 2.5m<cm / kg> + 9.7m<cm> |> roundLength


        /// Returns the length of the umbilical vein line for a weight under 3 kg, rounded to 3 significant
        /// digits.
        let umbilicalVeinLineLength (weight: decimal<kg>) = weight * 1.5m<cm / kg> + 5.5m<cm> |> roundLength


        /// Returns the tube size for an age in days: the size at birth plus 1 mm for every 4 years, rounded
        /// to 0.5 mm and at most 7 mm.
        let tubeSizeByAge sizeAtBirth age =
            // Operators.min is written in full because min is also the unit of measure for minutes.
            sizeAtBirth + (age |> years) * 0.25m<mm / year>
            |> roundToHalf
            |> Operators.min maxTubeSize


        /// Returns the size of a tube with a cuff for an age in days.
        let cuffedTubeSize age = age |> tubeSizeByAge 3.5m<mm>


        /// Returns the size of a tube without a cuff for an age in days.
        let uncuffedTubeSize age = age |> tubeSizeByAge 4m<mm>


        /// Returns the oral tube length for an age in days, rounded to 0.5 cm.
        let oralTubeLengthByAge age = 12m<cm> + (age |> years) * 0.5m<cm / year> |> roundToHalf


        /// Returns the nasal tube length for an age in days, rounded to 0.5 cm.
        let nasalTubeLengthByAge age = 15m<cm> + (age |> years) * 0.5m<cm / year> |> roundToHalf


        /// Returns the lowest energy a defibrillator can be set to that is at or above the energy per kg for
        /// the weight. Returns the highest energy of the defibrillator when the weight needs more.
        let energy (perKg: decimal<joule / kg>) (weight: decimal<kg>) =
            energies
            |> List.tryFind (fun e -> e >= weight * perKg)
            |> Option.defaultValue (energies |> List.last)


        /// Returns the energy of a defibrillation for a weight.
        let defibrillation weight = weight |> energy 4m<joule / kg>


        /// Returns the energy of a cardioversion for a weight.
        let cardioversion weight = weight |> energy 2m<joule / kg>


        /// Returns the interventions for an age in days and a weight. A weight under 3 kg gives the tube and
        /// the umbilical lines from the weight. Any other weight, or no weight, gives the tubes from the age,
        /// when there is an age, and the energies from the weight, when there is a weight.
        let calculate age weight =
            let calc formula intervention value =
                {
                    Intervention = intervention value
                    Formula = formula
                }

            match weight with
            | Some w when w < lowWeight ->
                let arteryLength, arteryFormula =
                    if w < lowUmbilicalWeight then
                        umbilicalArteryLineLengthUnder1500Gram, Formula.UmbilicalArteryLineLengthUnder1500Gram
                    else
                        umbilicalArteryLineLengthFrom1500Gram, Formula.UmbilicalArteryLineLengthFrom1500Gram

                [
                    w |> tubeSizeByWeight |> calc Formula.TubeSizeByWeight Intervention.TubeSize
                    w
                    |> oralTubeLengthByWeight
                    |> calc Formula.OralTubeLengthByWeight Intervention.OralTubeLength
                    w
                    |> nasalTubeLengthByWeight
                    |> calc Formula.NasalTubeLengthByWeight Intervention.NasalTubeLength
                    w
                    |> umbilicalLineSize
                    |> calc Formula.UmbilicalLineSizeByWeight Intervention.UmbilicalLineSize
                    w |> arteryLength |> calc arteryFormula Intervention.UmbilicalArteryLineLength
                    w
                    |> umbilicalVeinLineLength
                    |> calc Formula.UmbilicalVeinLineLengthByWeight Intervention.UmbilicalVeinLineLength
                ]
            | _ ->
                [
                    match age with
                    | Some a ->
                        a
                        |> cuffedTubeSize
                        |> calc Formula.CuffedTubeSizeByAge Intervention.CuffedTubeSize
                        a
                        |> uncuffedTubeSize
                        |> calc Formula.UncuffedTubeSizeByAge Intervention.UncuffedTubeSize
                        a
                        |> oralTubeLengthByAge
                        |> calc Formula.OralTubeLengthByAge Intervention.OralTubeLength
                        a
                        |> nasalTubeLengthByAge
                        |> calc Formula.NasalTubeLengthByAge Intervention.NasalTubeLength
                    | None -> ()

                    match weight with
                    | Some w ->
                        w
                        |> defibrillation
                        |> calc Formula.DefibrillationByWeight Intervention.Defibrillation
                        w
                        |> cardioversion
                        |> calc Formula.CardioversionByWeight Intervention.Cardioversion
                    | None -> ()
                ]
