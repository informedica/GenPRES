namespace Informedica.GenOrder.Lib


[<AutoOpen>]
module Utils =


    open Informedica.Utils.Lib.Web


    module Web =



        //https://docs.google.com/spreadsheets/d/1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g/edit?usp=sharing
        let [<Literal>] constraints = "1nny8rn9zWtP8TMawB3WeNWhl5d4ofbWKbGzGqKTd49g"


        //https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
        let [<Literal>] genpres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"



        /// <summary>
        /// Get data from a google sheet containing constraints
        /// </summary>
        /// <param name="sheet">The sheet to get</param>
        /// <returns>A array table of string arrays</returns>
        let getDataFromConstraints sheet = GoogleSheets.getDataFromSheet constraints sheet


        /// <summary>
        /// Get data from a google sheet containing data for GenPres
        /// </summary>
        /// <param name="sheet">The sheet to get</param>
        /// <returns>A array table of string arrays</returns>
        let getDataFromGenPres sheet = GoogleSheets.getDataFromSheet genpres sheet



    module MinMax =

        open MathNet.Numerics
        open Informedica.Utils.Lib.BCL
        open Informedica.GenCore.Lib.Ranges
        open Informedica.GenUnits.Lib


        /// Turn a `MinMax` to a string with
        /// `mins` and `maxs` as annotations
        /// for resp. the min and max value.
        let toString minInclStr minExclStr maxInclStr maxExclStr { Min = min; Max = max } =
            let vuToStr vu =
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

            let vuToVal vu =
                vu
                |> ValueUnit.getValue
                |> function
                    | [| br |] ->
                        br
                        |> BigRational.toDecimal
                        |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 2
                    | _ -> ""


            let minToString min =
                match min with
                | Inclusive vu -> $"{minInclStr}{vu |> vuToStr}"
                | Exclusive vu -> $"{minExclStr}{vu |> vuToStr}"

            let maxToString min =
                match min with
                | Inclusive vu -> $"{maxInclStr}{vu |> vuToStr}"
                | Exclusive vu -> $"{maxExclStr}{vu |> vuToStr}"

            match min, max with
            | None, None -> ""
            | Some min_, Some max_ when Limit.eq min_ max_ ->
                min_ |> Limit.getValueUnit |> vuToStr
            | Some min_, Some max_ ->
                $"%s{min_ |> Limit.getValueUnit |> vuToVal} - %s{max_ |> Limit.getValueUnit |> vuToStr}"
            | Some min_, None -> min_ |> minToString
            | None, Some max_ -> max_ |> maxToString
