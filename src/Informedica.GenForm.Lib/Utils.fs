namespace Informedica.GenForm.Lib



[<AutoOpen>]
module Utils =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL



    module Web =


        /// The url to the data sheet for Constraints
        let [<Literal>] dataUrlIdConstraints = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        /// The url to the data sheet for GenPRES
        /// https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
        let [<Literal>] dataUrlIdGenPres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"


        /// <summary>
        /// Get data from a web sheet
        /// </summary>
        /// <param name="urlId">The Url Id of the web sheet</param>
        /// <param name="sheet">The specific sheet</param>
        /// <returns>The data as a table of string array array</returns>
        let getDataFromSheet urlId sheet =
            fun () -> Web.GoogleSheets.getDataFromSheet urlId sheet
            |> StopWatch.clockFunc $"loaded {sheet} from web sheet"



    module BigRational =


        /// <summary>
        /// Parse an array of strings in float format to an array of BigRational
        /// </summary>
        /// <remarks>
        /// Uses ; as separator. Filters out non parsable strings.
        /// </remarks>
        /// <example>
        /// <code>
        /// let brs = toBrs "1.0;2.0;3.0"
        /// // returns [|1N; 2N; 3N|]
        /// let brs = toBrs "1.0;2.0;3.0;abc"
        /// // returns [|1N; 2N; 3N|]
        /// </code>
        /// </example>
        let toBrs s =
            s
            |> String.splitAt ';'
            |> Array.choose Double.tryParse
            |> Array.choose BigRational.fromFloat


        /// <summary>
        /// Return 2 BigRational arrays as a tuple of optional first BigRational
        /// of the first and second array. A None is returned for an empty array.
        /// </summary>
        /// <example>
        /// <code>
        /// let brs1 = [|1N|]
        /// let brs2 = [|4N|]
        /// tupleBrOpt brs1 brs2
        /// // returns (Some 1N, Some 4N)
        /// let brs1 = [|1N|]
        /// let brs2 = [||]
        /// tupleBrOpt brs1 brs2
        /// // returns (Some 1N, None)
        /// </code>
        /// </example>
        let tupleBrOpt brs1 brs2 =
            brs1 |> Array.tryHead,
            brs2 |> Array.tryHead


    module Calculations =

        open MathNet.Numerics
        open Informedica.GenUnits.Lib

        module Conversions = Informedica.GenCore.Lib.Conversions
        module BSA = Informedica.GenCore.Lib.Calculations.BSA

        let calcDuBois weight height =
            let w =
                weight
                |> ValueUnit.convertTo Units.Mass.kiloGram
                |> ValueUnit.getValue
                |> Array.tryHead
                |> Option.defaultValue 0N
                |> BigRational.toDecimal
                |> Conversions.kgFromDecimal
            let h =
                height
                |> ValueUnit.convertTo Units.Height.centiMeter
                |> ValueUnit.getValue
                |> Array.tryHead
                |> Option.defaultValue 0N
                |> BigRational.toDecimal
                |> Conversions.cmFromDecimal

            BSA.calcDuBois (Some 2) w h
            |> decimal
            |> BigRational.fromDecimal
            |> ValueUnit.singleWithUnit Units.BSA.m2


    module Units =

        open Informedica.GenUnits.Lib

        let week = Units.Time.week

        let day = Units.Time.day

        let weightGram = Units.Weight.gram

        let heightCm = Units.Height.centiMeter

        let bsaM2 = Units.BSA.m2

        let timeUnit s =
            if s |> String.isNullOrWhiteSpace then None
            else
                $"{s}[Time]" |> Units.fromString

        let freqUnit s =
            if s |> String.isNullOrWhiteSpace then None
            else
                $"times[Count]/{s}[Time]" |> Units.fromString

        let adjustUnit s =
            match s with
            | _ when s |> String.equalsCapInsens "kg" -> Units.Weight.kiloGram |> Some
            | _ when s |> String.equalsCapInsens "m2" -> bsaM2 |> Some
            | _ -> None


        let mL = Units.Volume.milliLiter



    module ValueUnit =

        open MathNet.Numerics
        open Informedica.GenUnits.Lib


        /// The full term age for a neonate
        /// which is 37 weeks
        let ageFullTerm = 37N |> ValueUnit.singleWithUnit Units.Time.week


        let withOptionalUnit u v =
            match v, u with
            | Some v, Some u ->
                v
                |> ValueUnit.singleWithUnit u
                |> Some
            | _ -> None


        let toString prec vu =
            ValueUnit.toStringDecimalDutchShortWithPrec prec vu
            |> String.replace ";" ", "


    module MinMax =

        open Informedica.GenUnits.Lib
        open Informedica.GenCore.Lib.Ranges

        let fromTuple minIncl maxIncl u (min, max) =
            match u with
            | None -> MinMax.empty
            | Some u ->
                {
                    Min =
                        min
                        |> Option.map (ValueUnit.singleWithUnit u)
                        |> Option.map minIncl
                    Max =
                        max
                        |> Option.map (ValueUnit.singleWithUnit u)
                        |> Option.map maxIncl
                }


        let inRange minMax vu =
            if minMax = MinMax.empty &&
               vu |> Option.isNone then true
            else
                vu
                |> Option.map (fun v ->
                    minMax |> MinMax.inRange v
                )
                |> Option.defaultValue false

