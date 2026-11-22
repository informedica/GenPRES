namespace Informedica.GenOrder.Lib


module Utils =


    open Informedica.Utils.Lib.Web


    module Constants =

        [<Literal>]
        let GENPRES_URL_ID = "GENPRES_URL_ID"


    module Web =

        open Informedica.Utils.Lib


        /// <summary>Reads a sheet of the Google spreadsheet that GENPRES_URL_ID names.</summary>
        /// <returns>The rows as string arrays, or an empty array when the download fails.</returns>
        /// <exception cref="System.InvalidOperationException">When GENPRES_URL_ID is not set.</exception>
        let getDataFromGenPres sheet =
            Env.loadDotEnv () |> ignore

            Env.getItem Constants.GENPRES_URL_ID
            |> Option.defaultWith (fun () -> invalidOp $"{Constants.GENPRES_URL_ID} not set")
            |> fun urlId -> GoogleSheets.getRowsFromSheetSync urlId sheet
            |> Result.defaultValue [||]


    module MinMax =

        /// Formats a MinMax as text, with the given labels for an inclusive or exclusive minimum and maximum.
        let toString = Informedica.GenForm.Lib.Utils.MinMax.toString


/// Functions for the Id and the variable Name.
[<AutoOpen>]
module WrappedString =

    open Informedica.Utils.Lib.BCL


    /// Functions for an Id.
    module Id =

        /// Creates an Id from a string.
        let create s = s |> Id


        /// Applies a function to the string of an Id and returns the result as an Id.
        let lift f = fun (Id s) -> s |> f |> create


        /// Returns the string of an Id.
        let toString (Id s) = s


    /// Functions for the variable Name of GenSolver.
    module Name =

        open Informedica.GenSolver.Lib

        module Name = Variable.Name


        [<Literal>]
        let concatWith = "."

        [<Literal>]
        let addWith = "_"


        /// Creates a Name from a list of strings, joined with a dot between square brackets.
        /// Raises a NameException when the name is not valid.
        let create ns =
            try
                $"[{ns |> String.concat concatWith}]" |> Name.createExc
            with e ->
                raise e

        /// Returns the string of a Name.
        let toString = Name.toString


        /// Creates a Name from a string. Raises a NameException when the name is not valid.
        let fromString = Name.createExc


        /// Splits a Name into its parts: the square brackets removed, split at every dot and underscore.
        let toStringList =
            Name.toString
            >> (String.replace "[" "")
            >> (String.replace "]" "")
            >> (String.replace addWith concatWith)
            >> (String.split concatWith)


        /// Adds a string to a Name after an underscore. Raises a NameException when the name is not valid.
        let add s n =
            try
                $"{n |> toString}{addWith}%s{s}" |> Name.createExc
            with e ->
                raise e
