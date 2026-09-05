namespace Informedica.GenOrder.Lib


module Utils =


    open Informedica.Utils.Lib.Web


    module Constants =

        [<Literal>]
        let GENPRES_URL_ID = "GENPRES_URL_ID"


    module Web =

        open Informedica.Utils.Lib


        /// <summary>
        /// Get data from a Google sheet containing data for GenPres
        /// </summary>
        /// <returns>A array table of string arrays</returns>
        let getDataFromGenPres sheet =
            Env.loadDotEnv () |> ignore

            Env.getItem Constants.GENPRES_URL_ID
            |> Option.defaultWith (fun () -> invalidOp $"{Constants.GENPRES_URL_ID} not set")
            |> fun urlId -> GoogleSheets.getCsvDataFromSheetSync urlId sheet // urlId sheet
            |> Result.defaultValue [||]


    module MinMax =

        /// Turn a `MinMax` to a string with
        /// `mins` and `maxs` as annotations
        /// for resp. the min and max value.
        let toString = Informedica.GenForm.Lib.Utils.MinMax.toString


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


        [<Literal>]
        let concatWith = "."

        [<Literal>]
        let addWith = "_"


        /// <summary>
        /// Create a `Name` from a list of strings. The strings
        /// will be concatenated with a dot.
        ///</summary>
        let create ns =
            try
                $"[{ns |> String.concat concatWith}]" |> Name.createExc
            with e ->
                printfn $"cannot create name with {ns}"
                raise e

        /// Get the string from a `Name`
        let toString = Name.toString


        /// Create a `Name` from a string
        let fromString = Name.createExc


        /// Return a Name as a string list
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
            with e ->
                printfn $"cannot add name with {s} and {n}"
                raise e
