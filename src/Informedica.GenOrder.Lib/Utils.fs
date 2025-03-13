namespace Informedica.GenOrder.Lib


[<AutoOpen>]
module Utils =


    open Informedica.Utils.Lib.Web


    module Web =

        open System
        open Informedica.Utils.Lib
        open Informedica.ZIndex.Lib


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
        /// <returns>A array table of string arrays</returns>
        let getDataFromGenPres =
            Env.getItem "GENPRES_URL_ID"
            |> Option.defaultValue genpres
            |> GoogleSheets.getDataFromSheet



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


    module Constants =

        let [<Literal>] GENPRES_URL_ID = "GENPRES_URL_ID"




/// Types and functions to deal with
/// value primitives
[<AutoOpen>]
module WrappedString =

    open Informedica.Utils.Lib.BCL


    /// Type and functions that
    /// deal with an identifier
    module Id =

        /// <summary>
        /// Create an Id from a string
        /// </summary>
        /// <param name="s">The id string</param>
        let create s = s |> Id


        /// <summary>
        /// Lift a function to hande the identifier string
        /// </summary>
        /// <param name="f">The function</param>
        /// <returns>A function that handles the identifier</returns>
        let lift f = fun (Id s) -> s |> f |> create


        /// <summary>
        /// Get the string from an Id
        /// </summary>
        let toString (Id s) = s



    /// Helper functions for `Informedica.GenSolver.Variable.Name` type
    module Name =

        open Informedica.GenSolver.Lib

        module Name = Variable.Name


        let [<Literal>] concatWith = "."
        let [<Literal>] addWith = "_"


        /// <summary>
        /// Create a `Name` from a list of strings. The strings
        /// will be concatenated with a dot.
        ///</summary>
        let create ns =
            try
                $"[{ns |> String.concat concatWith}]" |> Name.createExc
            with
            | e ->
                printfn $"cannot create name with {ns}"
                raise e

        /// Get the string from a `Name`
        let toString  = Name.toString


        /// Create a `Name` from a string
        let fromString = Name.createExc


        /// Return a Name as string list
        let toStringList =
            Name.toString
            >> (String.replace "[" "")
            >> (String.replace "]" "")
            >> (String.replace addWith concatWith)
            >> (String.split concatWith)


        /// <summary>
        /// Add a string to a `Name`. The string will be
        /// added with an underscore.
        /// </summary>
        /// <param name="s">The string to add</param>
        /// <param name="n">The Name</param>
        /// <returns>The new Name</returns>
        let add s n =
            try
                $"{n |> toString}{addWith}%s{s}" |> Name.createExc
            with
            | e ->
                printfn $"cannot add name with {s} and {n}"
                raise e