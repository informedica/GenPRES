namespace Informedica.ZForm.Lib


module DoseRule =

    open MathNet.Numerics

    open Aether
    open Aether.Operators

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib

    module ValueUnit = Informedica.GenUnits.Lib.ValueUnit
    module PatientCategory = PatientCategory


    /// <summary>
    /// Functions that handles a DoseRange. A DoseRange models
    /// a medication dose range with lower and upper limits
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///     <item>
    ///         <term>Norm</term>
    ///         <description>the 'normal' non-adjusted upper and lower limits</description>
    ///     </item>
    ///     <item>
    ///         <term>NormWeight</term>
    ///         <description>the 'normal' by weight adjusted upper and lower limits</description>
    ///     </item>
    ///     <item>
    ///         <term>NormBSA</term>
    ///         <description>the 'normal' by BSA adjusted upper and lower limits</description>
    ///     </item>
    ///     <item>
    ///         <term>Abs</term>
    ///         <description>the 'absolute' non-adjusted upper and lower limits</description>
    ///     </item>
    ///     <item>
    ///         <term>AbsWeight</term>
    ///         <description>the 'absolute' by weight adjusted upper and lower limits</description>
    ///     </item>
    ///     <item>
    ///         <term>AbsBSA</term>
    ///         <description>the 'absolute' by BSA adjusted upper and lower limits</description>
    ///     </item>
    /// </list>
    /// </remarks>
    module DoseRange =

        /// <summary>
        /// Create a DoseRange with a Norm, NormWeight, NormBSA,
        /// Abs, AbsWeight and AbsBSA min max values.
        /// </summary>
        /// <remarks>
        /// Note: units in normWght and absWght are weight units and
        /// units in normBSA and absBSA are bsa units.
        /// </remarks>
        let create norm normWght normBSA abs absWght absBSA =
            {
                Norm = norm
                NormWeight = normWght
                NormBSA = normBSA
                Abs = abs
                AbsWeight = absWght
                AbsBSA = absBSA
            }


        /// An empty Weight MinMax.
        let emptyWeight =
            MinMax.empty, NoUnit


        /// An empty BSA MinMax.
        let emptyBSA = MinMax.empty, NoUnit


        /// An empty DoseRange.
        let empty =
            create MinMax.empty emptyWeight emptyBSA MinMax.empty emptyWeight emptyBSA


        /// <summary>
        /// Creates a DoseRange with the same min and max values and
        /// a Count unit.
        /// </summary>
        /// <param name="vu">The ValueUnit to create the DoseRange</param>
        let count vu =
            let setMinIncl =
                Optic.set MinMax.Optics.inclMinLens

            let setMaxIncl =
                Optic.set MinMax.Optics.inclMaxLens

            let mm =
                MinMax.empty
                |> setMinIncl (Some vu)
                |> setMaxIncl (Some vu)

            let wmm = (mm, Units.Weight.kiloGram)

            let bmm = (mm, Units.BSA.m2)

            create mm wmm bmm mm wmm bmm


        /// <summary>
        /// Apply a calculation to a DoseRange.
        /// </summary>
        /// <param name="op">The calculation operator</param>
        /// <param name="dr1">The first DoseRange</param>
        /// <param name="dr2">The second DoseRange</param>
        /// <returns>The resulting DoseRange of the calculation</returns>
        let calc op (dr1: DoseRange) (dr2: DoseRange) =
            {
                Norm = dr1.Norm |> op <| dr2.Norm
                NormWeight =
                    (dr1.NormWeight |> fst) |> op
                    <| (dr2.NormWeight |> fst),
                    (dr1.NormWeight |> snd)
                NormBSA = (dr1.NormBSA |> fst) |> op <| (dr2.NormBSA |> fst), (dr1.NormBSA |> snd)
                Abs = dr1.Abs |> op <| dr2.Abs
                AbsWeight =
                    (dr1.AbsWeight |> fst) |> op
                    <| (dr2.AbsWeight |> fst),
                    (dr1.AbsWeight |> snd)
                AbsBSA = (dr1.AbsBSA |> fst) |> op <| (dr2.AbsBSA |> fst), (dr1.AbsBSA |> snd)
            }


        /// <summary>
        /// Convert the Unit of a DoseRange.
        /// </summary>
        /// <param name="u">The Unit to convert to</param>
        /// <param name="dr">The DoseRange to convert</param>
        /// <remarks>
        /// Only converts the substance unit to unit u
        /// weight and bsa units remain the same!
        /// </remarks>
        let convertTo u (dr: DoseRange) =

            {
                Norm = dr.Norm |> MinMax.convertTo u
                NormWeight = dr.NormWeight |> fst |> MinMax.convertTo u, dr.NormWeight |> snd
                NormBSA = dr.NormBSA |> fst |> MinMax.convertTo u, dr.NormBSA |> snd
                Abs = dr.Abs |> MinMax.convertTo u
                AbsWeight = dr.AbsWeight |> fst |> MinMax.convertTo u, dr.AbsWeight |> snd
                AbsBSA = dr.AbsBSA |> fst |> MinMax.convertTo u, dr.AbsBSA |> snd
            }



        module Optics =

            module MinMax = MinMax.Optics

            let inclMinNormLens =
                DoseRange.Norm_ >-> MinMax.inclMinLens


            let exclMinNormLens =
                DoseRange.Norm_ >-> MinMax.exclMinLens


            let inclMaxNormLens =
                DoseRange.Norm_ >-> MinMax.inclMaxLens


            let exclMaxNormLens =
                DoseRange.Norm_ >-> MinMax.exclMaxLens


            let normWeightUnitLens =
                DoseRange.NormWeight_ >-> snd_


            let inclMinNormWeightLens =
                DoseRange.NormWeight_
                >-> fst_
                >-> MinMax.inclMinLens


            let exclMinNormWeightLens =
                DoseRange.NormWeight_
                >-> fst_
                >-> MinMax.exclMinLens


            let inclMaxNormWeightLens =
                DoseRange.NormWeight_
                >-> fst_
                >-> MinMax.inclMaxLens


            let exclMaxNormWeightLens =
                DoseRange.NormWeight_
                >-> fst_
                >-> MinMax.exclMaxLens


            let normBSAUnitLens =
                DoseRange.NormBSA_ >-> snd_


            let inclMinNormBSALens =
                DoseRange.NormBSA_ >-> fst_ >-> MinMax.inclMinLens


            let exclMinNormBSALens =
                DoseRange.NormBSA_ >-> fst_ >-> MinMax.exclMinLens


            let inclMaxNormBSALens =
                DoseRange.NormBSA_ >-> fst_ >-> MinMax.inclMaxLens


            let exclMaxNormBSALens =
                DoseRange.NormBSA_ >-> fst_ >-> MinMax.exclMaxLens


            let minAbsLens =
                DoseRange.Abs_ >-> MinMax.min_


            let inclMinAbsLens =
                DoseRange.Abs_ >-> MinMax.inclMinLens


            let exclMinAbsLens =
                DoseRange.Abs_ >-> MinMax.exclMinLens


            let inclMaxAbsLens =
                DoseRange.Abs_ >-> MinMax.inclMaxLens


            let exclMaxAbsLens =
                DoseRange.Abs_ >-> MinMax.exclMaxLens


            let absWeightUnitLens =
                DoseRange.AbsWeight_ >-> snd_


            let inclMinAbsWeightLens =
                DoseRange.AbsWeight_
                >-> fst_
                >-> MinMax.inclMinLens


            let exclMinAbsWeightLens =
                DoseRange.AbsWeight_
                >-> fst_
                >-> MinMax.exclMinLens


            let inclMaxAbsWeightLens =
                DoseRange.AbsWeight_
                >-> fst_
                >-> MinMax.inclMaxLens


            let exclMaxAbsWeightLens =
                DoseRange.AbsWeight_
                >-> fst_
                >-> MinMax.exclMaxLens


            let absBSAUnitLens =
                DoseRange.AbsBSA_ >-> snd_


            let inclMinAbsBSALens =
                DoseRange.AbsBSA_ >-> fst_ >-> MinMax.inclMinLens


            let exclMinAbsBSALens =
                DoseRange.AbsBSA_ >-> fst_ >-> MinMax.exclMinLens


            let inclMaxAbsBSALens =
                DoseRange.AbsBSA_ >-> fst_ >-> MinMax.inclMaxLens


            let exclMaxAbsBSALens =
                DoseRange.AbsBSA_ >-> fst_ >-> MinMax.exclMaxLens

        let private vuToStr vu =
            let milliGram = Units.Mass.milliGram

            let gram = Units.Mass.gram
            let day = Units.Time.day

            let per = ValueUnit.per
            let convertTo = ValueUnit.convertTo

            let milliGramPerDay = milliGram |> per day
            let gramPerDay = gram |> per day

            vu
            |> (fun vu ->
                match vu |> ValueUnit.get with
                | v, u when v >= [| 1000N |] && u = milliGram -> vu |> convertTo gram
                | v, u when v >= [| 1000N |] && u = milliGramPerDay -> vu |> convertTo gramPerDay
                | _ -> vu
            )
            |> ValueUnit.toStringDecimalDutchShortWithPrec 2


        /// <summary>
        /// Get the string representation of a DoseRange.
        /// </summary>
        /// <param name="ru">The unit to use for the string representation</param>
        /// <param name="dr">The DoseRange to get the string representation of</param>
        /// <remarks>
        /// If the DoseRange is a rate unit, then ru is used.
        /// </remarks>
        let toString ru dr =
            let
                {
                    Norm = norm
                    NormWeight = normwght
                    NormBSA = normbsa
                    Abs = abs
                    AbsWeight = abswght
                    AbsBSA = absbsa
                } = dr

            let (>+) sl sr =
                let sl = sl |> String.trim
                let sr = sr |> String.trim

                if sl |> String.isNullOrWhiteSpace then
                    sr
                else
                    let sr =
                        if sr |> String.isNullOrWhiteSpace then
                            sr
                        else
                            " of " + sr

                    sl + sr


            let optRate mm =
                match ru with
                | Some u -> mm / (u |> MinMax.one)
                | None -> mm

            let norm, abs =
                if norm = abs then
                    MinMax.empty, abs |> optRate
                else
                    norm |> optRate, abs |> optRate

            let nw, nuw = normwght
            let nb, nub = normbsa
            let aw, auw = abswght
            let ab, aub = absbsa

            let nw, aw =
                if nw = aw then
                    MinMax.empty, aw / (auw |> MinMax.one) |> optRate
                else
                    nw / (nuw |> MinMax.one) |> optRate, aw / (auw |> MinMax.one) |> optRate

            let nb, ab =
                if nb = ab then
                    MinMax.empty, ab / (aub |> MinMax.one) |> optRate
                else
                    nb / (nub |> MinMax.one) |> optRate, ab / (aub |> MinMax.one) |> optRate

            let mmToStr =
                MinMax.toString vuToStr vuToStr "van (incl) " "van (excl) " "tot (incl) " "tot (excl) "

            norm |> mmToStr
            >+ (nw |> mmToStr)
            >+ (nb |> mmToStr)
            |> (fun s ->
                let s = s |> String.trim

                if s |> String.isNullOrWhiteSpace then
                    s
                else
                    " " + s
            )
            |> (fun sn ->
                let sn = sn |> String.trim

                let sa =
                    abs |> mmToStr
                    >+ (aw |> mmToStr)
                    >+ (ab |> mmToStr)

                if sa |> String.isNullOrWhiteSpace then
                    sn
                else
                    let sn =
                        if sn |> String.isNullOrWhiteSpace then
                            sn
                        else
                            sn + " "

                    sn + "maximaal " + sa
            )


        module Dto =

            type Dto() =
                member val Norm = MinMax.Dto.dto () with get, set
                member val NormWeight = MinMax.Dto.dto () with get, set
                member val NormWeightUnit = "" with get, set
                member val NormBSA = MinMax.Dto.dto () with get, set
                member val NormBSAUnit = ""
                member val Abs = MinMax.Dto.dto () with get, set
                member val AbsWeight = MinMax.Dto.dto () with get, set
                member val AbsWeightUnit = "" with get, set
                member val AbsBSA = MinMax.Dto.dto () with get, set
                member val AbsBSAUnit = "" with get, set

            let dto () = Dto()

            let toDto (dr: DoseRange) =
                let dto = dto ()

                let unstr =
                    snd >> ValueUnit.unitToReadableDutchString

                dto.Norm <- dr.Norm |> MinMax.Dto.toDto
                dto.NormWeight <- dr.NormWeight |> fst |> MinMax.Dto.toDto
                dto.NormWeightUnit <- dr.NormWeight |> unstr
                dto.NormBSA <- dr.NormBSA |> fst |> MinMax.Dto.toDto
                dto.Abs <- dr.Abs |> MinMax.Dto.toDto
                dto.AbsWeight <- dr.AbsWeight |> fst |> MinMax.Dto.toDto
                dto.AbsWeightUnit <- dr.AbsWeight |> unstr
                dto.AbsBSA <- dr.AbsBSA |> fst |> MinMax.Dto.toDto
                dto.AbsBSAUnit <- dr.AbsBSA |> unstr

                dto

            let fromDto (dto: Dto) =
                let set f o x =
                    match x |> f with
                    | Some x -> x
                    | None -> o

                let setug u g o mm =
                    if u |> String.isNullOrWhiteSpace
                       || g |> String.isNullOrWhiteSpace then
                        o
                    else
                        match $"%s{u}[%s{g}]" |> Units.fromString with
                        | None -> o
                        | Some u ->
                            match mm |> MinMax.Dto.fromDto with
                            | Some mm -> (mm, u)
                            | None -> o

                {
                    Norm = dto.Norm |> set MinMax.Dto.fromDto empty.Norm
                    NormWeight =
                        dto.NormWeight
                        |> setug dto.NormWeightUnit "weight" empty.NormWeight
                    NormBSA =
                        dto.NormBSA
                        |> setug dto.NormBSAUnit "bsa" empty.NormBSA
                    Abs = dto.Abs |> set MinMax.Dto.fromDto empty.Abs
                    AbsWeight =
                        dto.AbsWeight
                        |> setug dto.AbsWeightUnit "weight" empty.AbsWeight
                    AbsBSA =
                        dto.AbsBSA
                        |> setug dto.AbsBSAUnit "bsa" empty.AbsBSA
                }



    /// <summary>
    /// Models a drug dosage. For each combination
    /// of a drug, indication there is one dosage.
    /// The indication is identified by the name of
    /// the dosage.
    /// </summary>
    /// Per dosage the following `DoseRange`
    /// <remarks>
    /// items can be defined:
    /// <list type="bullet">
    ///     <item>
    ///         <term>StartDosage</term>
    ///         <description>dosage at the start</description>
    ///     </item>
    ///     <item>
    ///         <term>SingleDosage</term>
    ///         <description>dosage per administration</description>
    ///     </item>
    ///     <item>
    ///         <term>RateDosage</term>
    ///         <description>dosage rate, has a rate unit</description>
    ///     </item>
    ///     <item>
    ///         <term>TotalDosage</term>
    ///         <description>dosage per time period, has a `Frequency`</description>
    ///     </item>
    /// </list>
    /// The frequency is defined by a list of possible frequencies
    /// per time period and/or a minimal interval
    /// </remarks>
    module Dosage =


        /// Creates the Frequency for a TotalDosage.
        let createFrequency frs tu mi =
            {
                Frequencies = frs
                TimeUnit = tu
                MinimalInterval = mi
            }


        /// Creates a Dosage.
        let create nm start single rate total rls =
            {
                Name = nm
                StartDosage = start
                SingleDosage = single
                RateDosage = rate
                TotalDosage = total
                Rules = rls
            }


        /// An empty Frequency.
        let emptyFrequency =
            {
                Frequencies = []
                TimeUnit = Unit.NoUnit
                MinimalInterval = None
            }


        /// An empty Dosage.
        let empty =
            create
                ""
                DoseRange.empty
                DoseRange.empty
                (DoseRange.empty, Unit.NoUnit)
                (DoseRange.empty, emptyFrequency)
                []


        /// <summary>
        /// Change the substance unit of a Dosage.
        /// </summary>
        /// <param name="u">The unit to change to</param>
        /// <param name="ds">The Dosage to change</param>
        let convertSubstanceUnitTo u (ds: Dosage) =
            let convert = DoseRange.convertTo u

            { ds with
                StartDosage = ds.StartDosage |> convert
                SingleDosage = ds.SingleDosage |> convert
                RateDosage = ds.RateDosage |> fst |> convert, ds.RateDosage |> snd
                TotalDosage = ds.TotalDosage |> fst |> convert, ds.TotalDosage |> snd
            }


        /// <summary>
        /// Change the rate unit of a Dosage.
        /// </summary>
        /// <param name="u">The unit to change to</param>
        /// <param name="ds">The Dosage to change</param>
        let convertRateUnitTo u (ds: Dosage) =
            let getCount u1 u2 =
                1N
                |> ValueUnit.createSingle u2
                |> ValueUnit.convertTo u1
                |> ValueUnit.get
                |> fst
                |> ValueUnit.create Units.Count.times
                |> DoseRange.count

            { ds with
                RateDosage =
                    let factor =
                        ds.RateDosage |> snd |> getCount u

                    DoseRange.calc (/) (ds.RateDosage |> fst) factor, u
            }


        module Optics =


            module DoseRange = DoseRange.Optics


            let getName = Optic.get Dosage.Name_


            let setName = Optic.set Dosage.Name_


            let setRules = Optic.set Dosage.Rules_


            let freqsFrequencyLens =
                Dosage.TotalDosage_
                >-> snd_
                >-> Frequency.Frequencies_


            let getFrequencyValues =
                Optic.get freqsFrequencyLens


            let setFrequencyValues =
                Optic.set freqsFrequencyLens


            let timeUnitFrequencyLens =
                Dosage.TotalDosage_
                >-> snd_
                >-> Frequency.TimeUnit_


            let getFrequencyTimeUnit =
                Optic.get timeUnitFrequencyLens


            let setFrequencyTimeUnit =
                Optic.set timeUnitFrequencyLens


            let minIntervalValueFrequencyLens =
                Dosage.TotalDosage_
                >-> snd_
                >-> Frequency.MinimalInterval_


            let inclMinNormStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.inclMinNormLens


            let exclMinNormStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.exclMinNormLens


            let inclMaxNormStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.inclMaxNormLens


            let exclMaxNormStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.exclMaxNormLens


            let normWeightUnitStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.normWeightUnitLens


            let inclMinNormWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMinNormWeightLens


            let exclMinNormWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMinNormWeightLens


            let inclMaxNormWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMaxNormWeightLens


            let exclMaxNormWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMaxNormWeightLens


            let normBSAUnitStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.normBSAUnitLens


            let inclMinNormBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMinNormBSALens


            let exclMinNormBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMinNormBSALens


            let inclMaxNormBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMaxNormBSALens


            let exclMaxNormBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMaxNormBSALens


            let inclMinNormSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.inclMinNormLens


            let exclMinNormSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.exclMinNormLens


            let inclMaxNormSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.inclMaxNormLens


            let exclMaxNormSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.exclMaxNormLens


            let normWeightUnitSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.normWeightUnitLens


            let inclMinNormWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMinNormWeightLens


            let exclMinNormWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMinNormWeightLens


            let inclMaxNormWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMaxNormWeightLens


            let exclMaxNormWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMaxNormWeightLens


            let normBSAUnitSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.normBSAUnitLens


            let inclMinNormBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMinNormBSALens


            let exclMinNormBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMinNormBSALens


            let inclMaxNormBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMaxNormBSALens


            let exclMaxNormBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMaxNormBSALens


            let rateUnitRateDosagePrism =
                Dosage.RateDosage_ >-> snd_


            let normWeightUnitRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.normWeightUnitLens


            let inclMinNormRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMinNormLens


            let exclMinNormRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMinNormLens


            let inclMaxNormRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMaxNormLens


            let exclMaxNormRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMaxNormLens


            let inclMinNormWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMinNormWeightLens


            let exclMinNormWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMinNormWeightLens


            let inclMaxNormWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMaxNormWeightLens


            let exclMaxNormWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMaxNormWeightLens


            let normBSAUnitRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.normBSAUnitLens


            let inclMinNormBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMinNormBSALens


            let exclMinNormBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMinNormBSALens


            let inclMaxNormBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMaxNormBSALens


            let exclMaxNormBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMaxNormBSALens


            let timeUnitTotalDosagePrism =
                Dosage.TotalDosage_ >-> snd_


            let normWeightUnitTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.normWeightUnitLens


            let inclMinNormTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMinNormLens


            let exclMinNormTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMinNormLens


            let inclMaxNormTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMaxNormLens


            let exclMaxNormTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMaxNormLens


            let inclMinNormWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMinNormWeightLens


            let exclMinNormWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMinNormWeightLens


            let inclMaxNormWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMaxNormWeightLens


            let exclMaxNormWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMaxNormWeightLens


            let normBSAUnitTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.normBSAUnitLens


            let inclMinNormBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMinNormBSALens


            let exclMinNormBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMinNormBSALens


            let inclMaxNormBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMaxNormBSALens


            let exclMaxNormBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMaxNormBSALens


            let inclMinAbsStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.inclMinAbsLens


            let exclMinAbsStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.exclMinAbsLens


            let inclMaxAbsStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.inclMaxAbsLens


            let exclMaxAbsStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.exclMaxAbsLens


            let absWeightUnitStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.absWeightUnitLens


            let inclMinAbsWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMinAbsWeightLens


            let exclMinAbsWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMinAbsWeightLens


            let inclMaxAbsWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMaxAbsWeightLens


            let exclMaxAbsWeightStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMaxAbsWeightLens


            let absBSAUnitStartDosagePrism =
                Dosage.StartDosage_ >-> DoseRange.absBSAUnitLens


            let inclMinAbsBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMinAbsBSALens


            let exclMinAbsBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMinAbsBSALens


            let inclMaxAbsBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.inclMaxAbsBSALens


            let exclMaxAbsBSAStartDosagePrism =
                Dosage.StartDosage_
                >-> DoseRange.exclMaxAbsBSALens


            let inclMinAbsSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.inclMinAbsLens


            let exclMinAbsSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.exclMinAbsLens


            let inclMaxAbsSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.inclMaxAbsLens


            let exclMaxAbsSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.exclMaxAbsLens


            let absWeightUnitSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.absWeightUnitLens


            let inclMinAbsWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMinAbsWeightLens


            let exclMinAbsWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMinAbsWeightLens


            let inclMaxAbsWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMaxAbsWeightLens


            let exclMaxAbsWeightSingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMaxAbsWeightLens


            let absBSAUnitSingleDosagePrism =
                Dosage.SingleDosage_ >-> DoseRange.absBSAUnitLens


            let inclMinAbsBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMinAbsBSALens


            let exclMinAbsBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMinAbsBSALens


            let inclMaxAbsBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.inclMaxAbsBSALens


            let exclMaxAbsBSASingleDosagePrism =
                Dosage.SingleDosage_
                >-> DoseRange.exclMaxAbsBSALens


            let inclMinAbsRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMinAbsLens


            let exclMinAbsRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMinAbsLens


            let inclMaxAbsRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMaxAbsLens


            let exclMaxAbsRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMaxAbsLens


            let absWeightUnitRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.absWeightUnitLens


            let inclMinAbsWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMinAbsWeightLens


            let exclMinAbsWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMinAbsWeightLens


            let inclMaxAbsWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMaxAbsWeightLens


            let exclMaxAbsWeightRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMaxAbsWeightLens


            let absBSAUnitRateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.absBSAUnitLens


            let inclMinAbsBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMinAbsBSALens


            let exclMinAbsBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMinAbsBSALens


            let inclMaxAbsBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.inclMaxAbsBSALens


            let exclMaxAbsBSARateDosagePrism =
                Dosage.RateDosage_
                >-> fst_
                >-> DoseRange.exclMaxAbsBSALens


            let inclMinAbsTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMinAbsLens


            let exclMinAbsTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMinAbsLens


            let inclMaxAbsTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMaxAbsLens


            let exclMaxAbsTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMaxAbsLens


            let absWeightUnitTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.absWeightUnitLens


            let inclMinAbsWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMinAbsWeightLens


            let exclMinAbsWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMinAbsWeightLens


            let inclMaxAbsWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMaxAbsWeightLens


            let exclMaxAbsWeightTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMaxAbsWeightLens


            let absBSAUnitTotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.absBSAUnitLens


            let inclMinAbsBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMinAbsBSALens


            let exclMinAbsBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMinAbsBSALens


            let inclMaxAbsBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.inclMaxAbsBSALens


            let exclMaxAbsBSATotalDosagePrism =
                Dosage.TotalDosage_
                >-> fst_
                >-> DoseRange.exclMaxAbsBSALens


        /// Get the string representation of a Frequency.
        let freqsToStr (freqs: Frequency) =
            let fu =
                freqs.TimeUnit
                |> ValueUnit.unitToReadableDutchString
                |> String.replace "x/" ""


            if freqs.Frequencies
               |> List.isConsecutive 0N 1N
               |> not then
                freqs.Frequencies |> List.toString2
            else
                match freqs.Frequencies |> List.headTail with
                | Some h, Some t -> $"%s{h.ToString()} - %s{t.ToString()}"
                | _ -> freqs.Frequencies |> List.toString2
            |> (fun s ->
                if s |> String.isNullOrWhiteSpace
                   || s |> String.isNullOrWhiteSpace then
                    ""
                else
                    $"%s{s} x/%s{fu}"
            )


        /// Get the string representation of a Dosage.
        let toString
            rules
            {
                Name = n
                StartDosage = start
                SingleDosage = single
                RateDosage = rate
                TotalDosage = total
                Rules = rs
            }
            =
            let vuToStr = ValueUnit.toStringDecimalDutchShortWithPrec 2

            let (>+) sl sr =
                let l, s = sr

                if s |> String.isNullOrWhiteSpace then
                    sl
                else
                    let sl = sl |> String.trim

                    (if sl |> String.isNullOrWhiteSpace then
                         sl
                     else
                         sl + ", ")
                    + (if l |> String.isNullOrWhiteSpace then
                           ""
                       else
                           l + " ")
                    + s

            let rt, ru = rate
            let tt, _ = total

            let frqs = total |> snd

            let fu =
                frqs.TimeUnit
                |> ValueUnit.unitToReadableDutchString
                |> String.replace "x/" ""

            let drToStr = DoseRange.toString None

            ""
            >+ ("oplaad:", start |> drToStr)
            >+ ("per keer:", single |> drToStr)
            >+ ("snelheid:", rt |> DoseRange.toString (Some ru))
            >+ ("dosering:", tt |> drToStr)
            |> (fun s ->
                let s = s |> String.trim

                if frqs.Frequencies |> List.isEmpty
                   || fu |> String.isNullOrWhiteSpace then
                    s
                else
                    $"%s{s} in %s{frqs |> freqsToStr}"
                    |> (fun s ->
                        match frqs.MinimalInterval with
                        | Some mi -> s + " " + $"minimaal interval: %s{mi |> vuToStr}"
                        | None -> s

                    )
            )
            |> String.removeTrailing [ "\n" ]
            |> (fun s -> (n |> String.toLower) + " " + (s |> String.trim))
            |> (fun s ->
                if not rules then
                    s
                else
                    s
                    + "\n"
                    + (rs
                       |> List.map (fun r ->
                           match r with
                           | GStandRule r
                           | PedFormRule r -> r
                       )
                       |> String.concat "\n")
            )


        module Dto =


            type Dto() =
                member val Name = "" with get, set
                member val StartDosage = DoseRange.Dto.dto () with get, set
                member val SingleDosage = DoseRange.Dto.dto () with get, set
                member val DoseRate = DoseRange.Dto.dto () with get, set
                member val DoseRateUnit = "" with get, set
                member val TotalDosage = DoseRange.Dto.dto () with get, set
                member val TotalDosageFrequencyValues: float list = [] with get, set
                member val TotalDosageFrequencyUnit = "" with get, set
                member val TotalDosageHasMinimalPeriod = false with get, set
                member val TotalDosageMinimalPeriod = ValueUnit.Dto.dto () with get, set
                member val GStandRules: string list = [] with get, set
                member val PedFormRules: string list = [] with get, set


            let dto () = Dto()

            let toDto (ds: Dosage) =
                let dto = dto ()

                dto.Name <- ds.Name
                dto.StartDosage <- ds.StartDosage |> DoseRange.Dto.toDto
                dto.SingleDosage <- ds.SingleDosage |> DoseRange.Dto.toDto
                dto.DoseRate <- ds.RateDosage |> fst |> DoseRange.Dto.toDto

                dto.DoseRateUnit <-
                    ds.RateDosage
                    |> snd
                    |> ValueUnit.unitToReadableDutchString

                dto.TotalDosage <- ds.TotalDosage |> fst |> DoseRange.Dto.toDto

                match ds.TotalDosage |> snd with
                | {
                      Frequencies = freqs
                      TimeUnit = u
                      MinimalInterval = vu
                  } ->
                    dto.TotalDosageFrequencyValues <- freqs |> List.map BigRational.toFloat
                    dto.TotalDosageFrequencyUnit <- u |> ValueUnit.unitToReadableDutchString

                    match vu with
                    | None -> ()
                    | Some vu ->
                        dto.TotalDosageHasMinimalPeriod <- true
                        dto.TotalDosageMinimalPeriod <- vu |> ValueUnit.Dto.toDtoDutchShort

                ds.Rules
                |> List.iter (fun r ->
                    match r with
                    | GStandRule s -> dto.GStandRules <- [ s ] |> List.append dto.GStandRules
                    | PedFormRule s -> dto.PedFormRules <- [ s ] |> List.append dto.PedFormRules
                )

                dto


            let fromDto (dto: Dto) =
                {
                    Name = dto.Name
                    StartDosage = dto.StartDosage |> DoseRange.Dto.fromDto
                    SingleDosage = dto.SingleDosage |> DoseRange.Dto.fromDto
                    RateDosage =
                        match dto.DoseRateUnit
                              |> ValueUnit.readableStringToTimeUnit
                            with
                        | None -> empty.RateDosage
                        | Some u -> dto.DoseRate |> DoseRange.Dto.fromDto, u
                    TotalDosage =
                        match dto.TotalDosageFrequencyUnit
                              |> ValueUnit.readableStringToTimeUnit
                            with
                        | None -> empty.TotalDosage
                        | Some u ->
                            dto.TotalDosage |> DoseRange.Dto.fromDto,
                            {
                                Frequencies =
                                    dto.TotalDosageFrequencyValues
                                    |> List.map BigRational.fromFloat
                                    |> List.filter Option.isSome
                                    |> List.map Option.get
                                TimeUnit = u
                                MinimalInterval =
                                    if dto.TotalDosageHasMinimalPeriod |> not then
                                        None
                                    else
                                        dto.TotalDosageMinimalPeriod
                                        |> ValueUnit.Dto.fromDto
                            }
                    Rules =
                        dto.GStandRules
                        |> List.map GStandRule
                        |> List.append (dto.PedFormRules |> List.map PedFormRule)
                }



    /// A PatientDosage is a ShapeDosage or list of SubstanceDosages
    /// for a specific patient group.
    module PatientDosage =


        /// Create a PatientDosage using a PatientCategory.
        let create pat =
            {
                Patient = pat
                ShapeDosage = Dosage.empty
                SubstanceDosages = []
            }



        module Optics =



            let setPatient =
                Optic.set PatientDosage.Patient_

            let getPatient =
                Optic.get PatientDosage.Patient_

            let setShapeDosage =
                Optic.set PatientDosage.ShapeDosage_

            let getShapeDosage =
                Optic.get PatientDosage.ShapeDosage_

            let setSubstanceDosages =
                Optic.set PatientDosage.SubstanceDosages_

            let getSubstanceDosages =
                Optic.get PatientDosage.SubstanceDosages_



        module Dto =


            type Dto() =
                // The patient group the doserules applies
                member val Patient = PatientCategory.Dto.dto () with get, set
                // List of shapes that have a dosage
                member val ShapeDosage = Dosage.Dto.dto () with get, set
                // List of substances that have a dosage
                member val SubstanceDosages: Dosage.Dto.Dto list = [] with get, set

            let dto () = Dto()

            let fromDto (dto: Dto) =
                match dto.Patient |> PatientCategory.Dto.fromDto with
                | None -> None
                | Some p ->
                    p
                    |> create
                    |> Optics.setShapeDosage (dto.ShapeDosage |> Dosage.Dto.fromDto)
                    |> Optics.setSubstanceDosages (
                        dto.SubstanceDosages
                        |> List.map Dosage.Dto.fromDto
                    )
                    |> Some


            let toDto (pd: PatientDosage) =
                let dto = dto ()

                dto.Patient <- pd.Patient |> PatientCategory.Dto.toDto
                dto.ShapeDosage <- pd.ShapeDosage |> Dosage.Dto.toDto
                dto.SubstanceDosages <- pd.SubstanceDosages |> List.map Dosage.Dto.toDto

                dto



    /// A ShapeDosage is a Dosage for a specific Shape,
    /// and related GenericProducts and TradeProducts.
    module ShapeDosage =


        module TradeProduct =

            let create hpk label = { HPK = hpk; Label = label }

            let apply f (x: TradeProductLabel) = x |> f

            let get = apply id

            let label tp = (tp |> get).Label

            let hpk tp = (tp |> get).HPK



            module Optics =

                let setHPK =
                    Optic.set TradeProductLabel.HPK_

                let getHPK =
                    Optic.get TradeProductLabel.HPK_

                let setLabel =
                    Optic.set TradeProductLabel.Label_

                let getLabel =
                    Optic.get TradeProductLabel.Label_



            module Dto =


                type Dto() =
                    member val HPK = 0 with get, set
                    member val Label = "" with get, set


                let dto () = Dto()


                let toDto (tp: TradeProductLabel) =
                    let dto = dto ()

                    dto.HPK <- tp.HPK
                    dto.Label <- tp.Label

                    dto


                let fromDto (dto: Dto) = { HPK = dto.HPK; Label = dto.Label }



        module GenericProduct =

            let create gpk label = { GPK = gpk; Label = label }

            let apply f (x: GenericProductLabel) = x |> f

            let get = apply id

            let lablel gp = (gp |> get).Label

            let gpk gp = (gp |> get).GPK



            module Optics =

                let setGPK =
                    Optic.set GenericProductLabel.GPK_

                let getGPK =
                    Optic.get GenericProductLabel.GPK_

                let setLabel =
                    Optic.set GenericProductLabel.Label_

                let getLabel =
                    Optic.get GenericProductLabel.Label_


            module Dto =


                type Dto() =
                    member val GPK = 0 with get, set
                    member val Label = "" with get, set


                let dto () = Dto()


                let toDto (gp: GenericProductLabel) =
                    let dto = dto ()

                    dto.GPK <- gp.GPK
                    dto.Label <- gp.Label

                    dto


                let fromDto (dto: Dto) = { GPK = dto.GPK; Label = dto.Label }


        /// <summary>
        /// Create a ShapeDosage.
        /// </summary>
        /// <param name="shps">The list of Shapes</param>
        /// <param name="gps">The list of GenericProducts</param>
        /// <param name="tps">The list of TradeProducts</param>
        /// <remarks>
        /// If the list of Shapes is empty, None is returned.
        /// </remarks>
        let create shps gps tps =
            if shps |> List.exists String.isNullOrWhiteSpace then
                None
            else
                {
                    Shape = shps
                    GenericProducts = gps
                    TradeProducts = tps
                    PatientDosages = []
                }
                |> Some



        module Optics =

            let genericProducts =
                ShapeDosage.GenericProducts_

            let tradeProducts =
                ShapeDosage.TradeProducts_

            let patientDosages =
                ShapeDosage.PatientDosages_


            let setShape = Optic.set ShapeDosage.Shape_

            let getShape = Optic.get ShapeDosage.Shape_

            let setTradeProducts =
                Optic.set ShapeDosage.TradeProducts_

            let getTradeProducts =
                Optic.get ShapeDosage.TradeProducts_

            let setGenericProducts =
                Optic.set ShapeDosage.GenericProducts_

            let getGenericProducts =
                Optic.get ShapeDosage.GenericProducts_

            let setPatientDosages =
                Optic.set ShapeDosage.PatientDosages_

            let getPatientDosages =
                Optic.get ShapeDosage.PatientDosages_


        module Dto =

            module GenericProduct = GenericProduct

            type Dto() =
                member val Shape: string list = [] with get, set
                member val TradeProducts: TradeProduct.Dto.Dto list = [] with get, set
                member val GenericProducts: GenericProduct.Dto.Dto list = [] with get, set
                member val PatientDosages: PatientDosage.Dto.Dto list = [] with get, set


            let dto () = Dto()

            let toDto (sd: ShapeDosage) =
                let dto = dto ()

                dto.Shape <- sd.Shape

                dto.GenericProducts <-
                    sd.GenericProducts
                    |> List.map GenericProduct.Dto.toDto

                dto.TradeProducts <-
                    sd.TradeProducts
                    |> List.map TradeProduct.Dto.toDto

                dto


            let fromDto (dto: Dto) =
                create [] [] []
                |> Option.bind ((Optics.setShape dto.Shape) >> Some)
                |> Option.bind (
                    (Optics.setGenericProducts (
                        dto.GenericProducts
                        |> List.map GenericProduct.Dto.fromDto
                    ))
                    >> Some
                )
                |> Option.bind (
                    (Optics.setTradeProducts (
                        dto.TradeProducts
                        |> List.map TradeProduct.Dto.fromDto
                    ))
                    >> Some
                )
                |> Option.bind (
                    (Optics.setPatientDosages (
                        dto.PatientDosages
                        |> List.map PatientDosage.Dto.fromDto
                        |> List.filter Option.isSome
                        |> List.map Option.get
                    ))
                    >> Some
                )



    /// A RouteDosage are a ShapeDosages for a specific Route.
    module RouteDosage =


        /// <summary>
        /// Create a RouteDosage.
        /// </summary>
        /// <param name="rt">The Route</param>
        /// <remarks>
        /// If the Route is empty or only space(s), None is returned.
        /// </remarks>
        let create rt =
            if rt |> String.isNullOrWhiteSpace then
                None
            else
                { Route = rt; ShapeDosages = [] } |> Some



        module Optics =

            let getShapeDosage n =
                List.pos_ n >?> RouteDosage.ShapeDosages_

            let shapeDosages = RouteDosage.ShapeDosages_

            let setRoute = Optic.set RouteDosage.Route_

            let getRoute = Optic.get RouteDosage.Route_

            let setShapeDosages =
                Optic.set RouteDosage.ShapeDosages_

            let getShapeDosages =
                Optic.get RouteDosage.ShapeDosages_



        module Dto =

            type Dto() =
                member val Route = "" with get, set
                member val ShapeDosages: ShapeDosage.Dto.Dto list = [] with get, set


            let dto () = Dto()


            let toDto (rd: RouteDosage) =
                let dto = dto ()

                dto.Route <- rd.Route
                dto.ShapeDosages <- rd.ShapeDosages |> List.map ShapeDosage.Dto.toDto

                dto


            let fromDto (dto: Dto) =
                create dto.Route
                |> Option.bind (
                    Optics.setShapeDosages (
                        dto.ShapeDosages
                        |> List.map ShapeDosage.Dto.fromDto
                        |> List.filter Option.isSome
                        |> List.map Option.get
                    )
                    >> Some
                )



    /// An IndicationDosage is a list of RouteDosages for a specific Indication.
    module IndicationDosage =


        /// <summary>
        /// Create an IndicationDosage.
        /// </summary>
        /// <param name="inds">The Indications</param>
        let create inds =
            {
                Indications = inds
                RouteDosages = []
            }



        module Optics =

            let getRouteDosage n =
                List.pos_ n >?> IndicationDosage.RouteDosages_

            let setIndications =
                Optic.set IndicationDosage.Indications_

            let getIndications =
                Optic.get IndicationDosage.Indications_

            let setRouteDosages =
                Optic.set IndicationDosage.RouteDosages_

            let getRouteDosages =
                Optic.get IndicationDosage.RouteDosages_


        module Dto =

            type Dto() =
                member val Indications: string list = [] with get, set
                member val RouteDosages: RouteDosage.Dto.Dto list = [] with get, set


            let dto () = Dto()


            let toDto (id: IndicationDosage) =
                let dto = dto ()

                dto.Indications <- id.Indications
                dto.RouteDosages <- id.RouteDosages |> List.map RouteDosage.Dto.toDto

                dto


            let fromDto (dto: Dto) =
                create []
                |> Optics.setRouteDosages (
                    dto.RouteDosages
                    |> List.map RouteDosage.Dto.fromDto
                    |> List.filter Option.isSome
                    |> List.map Option.get
                )




    /// Utility function to apply a function f to a DoseRule dr.
    let apply f (dr: DoseRule) = f dr


    /// Utility function to enable type inference.
    let get = apply id


    /// <summary>
    /// Create a DoseRule.
    /// </summary>
    /// <param name="gen">The Generic</param>
    /// <param name="syn">Synonyms for the Generic</param>
    /// <param name="atc">The ATC5 code for the Generic</param>
    /// <param name="thg">The TherapeuticGroup for the Generic</param>
    /// <param name="sub">The TherapeuticSubGroup for the Generic</param>
    /// <param name="ggp">The GenericGroup for the Generic</param>
    /// <param name="gsg">The GenericSubGroup for the Generic</param>
    /// <param name="idl">The list of IndicationDosages</param>
    let create gen syn atc thg sub ggp gsg idl =
        {
            Generic = gen
            Synonyms = syn
            ATC = atc
            ATCTherapyGroup = thg
            ATCTherapySubGroup = sub
            GenericGroup = ggp
            GenericSubGroup = gsg
            IndicationsDosages = idl
        }


    /// Create an IndicationDosage.
    let createIndicationDosage =
        IndicationDosage.create


    /// Create a RouteDosage.
    let createRouteDosage = RouteDosage.create


    /// Create a ShapeDosage.
    let createShapeDosage = ShapeDosage.create


    /// Create a PatientDosage.
    let createPatientDosage =
        PatientDosage.create


    /// Create a Dosage.
    let createDosage n = Dosage.empty |> Dosage.Optics.setName n


    /// Create a Dosage for a Substance.
    let createSubstanceDosage sn =
        if sn |> String.isNullOrWhiteSpace then
            None
        else
            sn |> createDosage |> Some


    /// <summary>
    /// Try find the index of an IndicationDosage in a DoseRule.
    /// </summary>
    /// <param name="inds">The list of Indications</param>
    /// <param name="dr">The DoseRule</param>
    let indxIndications inds (dr: DoseRule) =
        dr.IndicationsDosages
        |> List.tryFindIndex (fun id -> id.Indications = inds)


    /// <summary>
    /// Try find the indexs of a RouteDosage in a DoseRule.
    /// </summary>
    /// <param name="inds">The list of Indications</param>
    /// <param name="rt">The Route</param>
    /// <param name="dr">The DoseRule</param>
    let indxRoute inds rt dr =
        dr
        |> indxIndications inds
        |> Option.bind (fun ni ->
            match dr.IndicationsDosages[ni].RouteDosages
                  |> List.tryFindIndex (fun rd -> rd.Route = rt)
                with
            | None -> None
            | Some nr -> (ni, nr) |> Some
        )


    /// <summary>
    /// Try find the indexs of a ShapeDosage in a DoseRule.
    /// </summary>
    /// <param name="inds">The list of Indications</param>
    /// <param name="rt">The Route</param>
    /// <param name="shp">The Shape</param>
    /// <param name="dr">The DoseRule</param>
    let indxShape inds rt shp dr =
        match dr |> indxRoute inds rt with
        | Some (ni, nr) ->
            match dr.IndicationsDosages[ni].RouteDosages[nr]
                      .ShapeDosages
                  |> List.tryFindIndex (fun sd -> sd.Shape = shp)
                with
            | Some ns -> (ni, nr, ns) |> Some
            | None -> None
        | None -> None


    /// <summary>
    /// Try find the indexes of a PatientDosage in a DoseRule.
    /// </summary>
    /// <param name="inds">The list of Indications</param>
    /// <param name="rt">The Route</param>
    /// <param name="shp">The Shape</param>
    /// <param name="pat">The PatientCategory</param>
    /// <param name="dr">The DoseRule</param>
    let indxPatient inds rt shp pat dr =
        match dr |> indxShape inds rt shp with
        | Some (ni, nr, ns) ->
            match dr.IndicationsDosages[ni].RouteDosages[nr]
                      .ShapeDosages[ns]
                      .PatientDosages
                  |> List.tryFindIndex (fun rd -> rd.Patient = pat)
                with
            | Some np -> (ni, nr, ns, np) |> Some
            | None -> None
        | None -> None


    /// <summary>
    /// Add an IndicationDosage to a DoseRule.
    /// </summary>
    /// <param name="inds">The list of Indications</param>
    /// <param name="dr">The DoseRule</param>
    let addIndications inds (dr: DoseRule) =
        let indd = createIndicationDosage inds

        match dr |> indxIndications inds with
        | Some _ -> dr
        | None ->
            { dr with
                IndicationsDosages = dr.IndicationsDosages |> List.prepend [ indd ]
            }



    module Optics =

        module Patient = PatientCategory.Optics
        module Dosage = Dosage.Optics


        let setGeneric = Optic.set DoseRule.Generic_


        let setSynonyms =
            Optic.set DoseRule.Synonyms_


        let indDosDosagesLens n =
            DoseRule.IndicationDosages_
            >-> IndicationDosage.Optics.getRouteDosage n


        let getRouteDosages indd dr =
            match dr |> indxIndications indd with
            | Some n ->
                match dr |> Optic.get (indDosDosagesLens n) with
                | Some rtds -> rtds
                | None -> []
            | None -> []


        let addRoute inds rt dr =
            match rt |> createRouteDosage with
            | None -> dr
            | Some rtd ->
                match dr |> indxIndications inds with
                | Some n ->
                    match dr |> indxRoute inds rt with
                    | Some _ -> dr
                    | None ->
                        dr
                        |> Optic.set (indDosDosagesLens n) (dr |> getRouteDosages inds |> List.prepend [ rtd ])
                | None -> dr


        let shapeDosagesPrism n1 n2 =
            indDosDosagesLens n1
            >?> RouteDosage.Optics.getShapeDosage n2
        //List.pos_ n2 >?> RouteDosage.ShapeDosages_


        let getShapeDosages inds rt dr =

            match dr |> indxRoute inds rt with
            | Some (ni, nr) ->
                match dr |> Optic.get (shapeDosagesPrism ni nr) with
                | Some pds -> pds
                | None -> []
            | None -> []


        let setShapeDosages inds rt pds dr =

            match dr |> indxRoute inds rt with
            | Some (ni, nr) -> dr |> Optic.set (shapeDosagesPrism ni nr) pds
            | None -> dr


        let addShape inds rt shp dr =
            match createShapeDosage shp [] [] with
            | None -> dr
            | Some shpd ->

                match dr |> indxShape inds rt shp with
                | Some _ -> dr
                | None ->
                    let pds =
                        dr
                        |> getShapeDosages inds rt
                        |> List.prepend [ shpd ]

                    dr |> setShapeDosages inds rt pds


        let shapeDosagePrism n1 n2 n3 =
            shapeDosagesPrism n1 n2 >?> List.pos_ n3


        let inline private shapeDosageProductsGetter prism inds rt shp dr =
            match dr |> indxShape inds rt shp with
            | Some (ni, nr, ns) ->
                dr
                |> Optic.get ((shapeDosagePrism ni nr ns) >?> prism)
            | None -> None


        let inline private shapeDosageProductsSetter prism inds rt shp ps dr =
            match dr |> indxShape inds rt shp with
            | Some (ni, nr, ns) ->
                dr
                |> Optic.set ((shapeDosagePrism ni nr ns) >?> prism) ps
            | None -> dr


        let setGenericProducts =
            shapeDosageProductsSetter ShapeDosage.Optics.genericProducts


        let setTradeProducts =
            shapeDosageProductsSetter ShapeDosage.Optics.tradeProducts


        let patientDosagesPrism n1 n2 n3 =
            shapeDosagePrism n1 n2 n3
            >?> ShapeDosage.Optics.patientDosages


        let getPatientDosages inds rt shp dr =
            match dr |> indxShape inds rt shp with
            | Some (ni, nr, ns) ->
                match dr |> Optic.get (patientDosagesPrism ni nr ns) with
                | Some sds -> sds
                | None -> []
            | None -> []


        let setPatientDosages inds rt shp pds dr =
            match dr |> indxShape inds rt shp with
            | Some (ni, nr, ns) -> dr |> Optic.set (patientDosagesPrism ni nr ns) pds
            | None -> dr

        let addPatient inds rt shp pat dr =
            match dr |> indxPatient inds rt shp pat with
            | Some _ -> dr
            | None ->
                let pds =
                    dr
                    |> getPatientDosages inds rt shp
                    |> List.prepend [ createPatientDosage pat ]

                dr |> setPatientDosages inds rt shp pds


        let patientDosagePrism n1 n2 n3 n4 =
            patientDosagesPrism n1 n2 n3 >?> List.pos_ n4


        let substanceDosagesPrism n1 n2 n3 n4 =
            patientDosagePrism n1 n2 n3 n4
            >?> PatientDosage.SubstanceDosages_


        let getSubstanceDosages inds rt shp pat dr =
            match dr |> indxPatient inds rt shp pat with
            | Some (ni, nr, np, ns) ->
                match
                    dr
                    |> Optic.get (substanceDosagesPrism ni nr np ns)
                    with
                | Some sds -> sds
                | None -> []
            | None -> []


        let setSubstanceDosages inds rt shp pat sds dr =
            match dr |> indxPatient inds rt shp pat with
            | Some (ni, nr, np, ns) ->
                dr
                |> Optic.set (substanceDosagesPrism ni nr np ns) sds
            | None -> dr



    /// <summary>
    /// Convert a Unit of a DoseRule to a new Unit.
    /// </summary>
    /// <param name="conv">The conversion function</param>
    /// <param name="gen">The Generic</param>
    /// <param name="u">The new SubstanceUnit</param>
    /// <param name="dr">The DoseRule</param>
    let private convertTo conv gen u (dr: DoseRule) =
        { dr with
            IndicationsDosages =
                dr.IndicationsDosages
                |> List.map (fun id ->
                    { id with
                        RouteDosages =
                            id.RouteDosages
                            |> List.map (fun rd ->
                                { rd with
                                    ShapeDosages =
                                        rd.ShapeDosages
                                        |> List.map (fun sd ->
                                            { sd with
                                                PatientDosages =
                                                    sd.PatientDosages
                                                    |> List.map (fun pd ->
                                                        { pd with
                                                            SubstanceDosages =
                                                                pd.SubstanceDosages
                                                                |> List.map (fun sd ->
                                                                    if sd.Name = gen then
                                                                        sd |> conv u
                                                                    else
                                                                        sd
                                                                )
                                                        }
                                                    )
                                            }
                                        )
                                }
                            )
                    }
                )

        }


    /// <summary>
    /// Convert the SubstanceUnit of a DoseRule to a new SubstanceUnit.
    /// </summary>
    /// <param name="gen">The Generic</param>
    /// <param name="u">The new SubstanceUnit</param>
    /// <param name="dr">The DoseRule</param>
    let convertSubstanceUnitTo gen u dr =
        convertTo
            Dosage.convertSubstanceUnitTo
            gen
            u
            dr


    /// <summary>
    /// Convert the RateUnit of a DoseRule to a new RateUnit.
    /// </summary>
    /// <param name="gen">The Generic</param>
    /// <param name="u">The new RateUnit</param>
    /// <param name="dr">The DoseRule</param>
    let convertRateUnitTo gen u dr =
        convertTo
            Dosage.convertRateUnitTo
            gen
            u
            dr


    module Operators =

        let (|>>) (x1, x2) f = f x2 x1


        let (|>>>) (x1, x2) f = f x2 x1, x2



    let mdText =
        """
## _Stofnaam_: {generic}
Synoniemen: {synonym}

---

### _ATC code_: {atc}

### _Therapeutische groep_: {thergroup}

### _Therapeutische subgroep_: {thersub}

### _Generiek groep_: {gengroup}

### _Generiek subgroep_: {gensub}

"""

    let mdIndicationText =
        """

---

### _Indicatie_: {indication}
"""


    let mdRouteText =
        """
* _Route_: {route}
"""

    let mdShapeText =
        """
  * _Vorm_: {shape}
  * _Producten_:
  * {products}
"""

    let mdPatientText =
        """
    * _Patient_: __{patient}__
"""

    let mdDosageText =
        """
      * {dosage}

"""


    /// A type to configure the text output of a DoseRule.
    type TextConfig =
        {
            MainText: string
            IndicationText: string
            RouteText: string
            ShapeText: string
            PatientText: string
            DosageText: string
        }


    /// A Markdown text configuration for a DoseRule.
    let mdConfig =
        {
            MainText = mdText
            IndicationText = mdIndicationText
            RouteText = mdRouteText
            ShapeText = mdShapeText
            PatientText = mdPatientText
            DosageText = mdDosageText
        }


    /// <summary>
    /// Convert a DoseRule to a string using a TextConfig.
    /// </summary>
    /// <param name="config">The textConfig</param>
    /// <param name="printRules">Whether to print the original DoseRules</param>
    /// <param name="dr">The DoseRule</param>
    let toStringWithConfig (config: TextConfig) printRules (dr: DoseRule) =
        let gpsToString (gps: GenericProductLabel list) =
            gps
            |> List.map _.Label
            |> String.concat ", "

        config.MainText
        |> String.replace "{generic}" dr.Generic
        |> String.replace "{synonym}" (dr.Synonyms |> String.concat ",")
        |> String.replace "{atc}" dr.ATC
        |> String.replace "{thergroup}" dr.ATCTherapyGroup
        |> String.replace "{thersub}" dr.ATCTherapySubGroup
        |> String.replace "{gengroup}" dr.GenericGroup
        |> String.replace "{gensub}" dr.GenericSubGroup
        |> (fun s ->
            dr.IndicationsDosages
            |> List.fold
                (fun acc id ->
                    let ind =
                        id.Indications |> String.concat ", "

                    id.RouteDosages
                    |> List.fold
                        (fun acc rd ->

                            rd.ShapeDosages
                            |> List.fold
                                (fun acc sd ->
                                    let shapeStr =
                                        config.ShapeText
                                        |> String.replace "{shape}" (sd.Shape |> String.concat ",")
                                        |> String.replace "{products}" (sd.GenericProducts |> gpsToString)

                                    sd.PatientDosages
                                    |> List.fold
                                        (fun acc pd ->
                                            let pat =
                                                if
                                                    pd.Patient
                                                    |> PatientCategory.toString
                                                    |> String.isNullOrWhiteSpace then "alle patienten"
                                                else
                                                    pd.Patient
                                                    |> PatientCategory.toString

                                            let s =
                                                (config.PatientText
                                                 |> String.replace "{patient}" pat)
                                                + ("{dosage}"
                                                   |> String.replace
                                                       "{dosage}"
                                                       (pd.ShapeDosage |> Dosage.toString printRules))

                                            pd.SubstanceDosages
                                            |> List.fold
                                                (fun acc sd ->

                                                    acc
                                                    + (config.DosageText
                                                       |> String.replace "{dosage}" (sd |> Dosage.toString printRules))

                                                )
                                                (acc + s)

                                        )
                                        (acc + shapeStr)

                                )
                                (acc
                                 + (config.RouteText
                                    |> String.replace "{route}" rd.Route))

                        )
                        (acc
                         + (config.IndicationText
                            |> String.replace "{indication}" ind))
                )
                s
        )


    /// Get the Markdown text of a DoseRule.
    let toString = toStringWithConfig mdConfig


    module Dto =


        type Dto() =
            member val Generic = "" with get, set
            // List of synonyms for the generic
            member val Synomyms: string list = [] with get, set
            // The ATC code
            member val ATC = "" with get, set
            // ATCTherapyGroup the doserule applies to
            member val ATCTherapyGroup = "" with get, set
            // ATCTherapySubGroup the doserule applies to
            member val ATCTherapySubGroup = "" with get, set
            // The generic group the doserule applies to
            member val GenericGroup = "" with get, set
            // The generic subgroup the doserule applies to
            member val GenericSubGroup = "" with get, set
            // The doserules per indication(-s)
            member val Indications: IndicationDosage.Dto.Dto list = [] with get, set


        let dto () = Dto()


        let toDto (dr: DoseRule) =
            let dto = dto ()

            dto.Generic <- dr.Generic
            dto.Synomyms <- dr.Synonyms
            dto.ATC <- dr.ATC
            dto.ATCTherapyGroup <- dr.ATCTherapyGroup
            dto.ATCTherapySubGroup <- dr.ATCTherapySubGroup
            dto.GenericGroup <- dr.GenericGroup
            dto.GenericSubGroup <- dr.GenericSubGroup

            dto.Indications <-
                dr.IndicationsDosages
                |> List.map IndicationDosage.Dto.toDto

            dto

        let fromDto (dto: Dto) =
            let gen = dto.Generic
            let syn = dto.Synomyms
            let atc = dto.ATC
            let thg = dto.ATCTherapyGroup
            let sub = dto.ATCTherapySubGroup
            let ggp = dto.GenericGroup
            let gsg = dto.GenericSubGroup

            dto.Indications
            |> List.map IndicationDosage.Dto.fromDto
            |> create gen syn atc thg sub ggp gsg