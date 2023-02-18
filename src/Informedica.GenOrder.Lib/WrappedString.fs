namespace Informedica.GenOrder.Lib



/// Types and functions to deal with
/// value primitives
[<AutoOpen>]
module WrappedString =

    open Informedica.Utils.Lib.BCL


    /// Type and functions that
    /// deal with an identifier
    module Id =

        let create s = s |> Id

        let lift f = fun (Id s) -> s |> f |> create

        let toString (Id s) = s



    /// Helper functions for `Informedica.GenSolver.Variable.Name` type
    module Name =

        open Informedica.GenSolver.Lib

        module Name = Variable.Name


        let [<Literal>] concatWith = "."
        let [<Literal>] addWith = "_"


        /// Create a `Name` from a list of strings that
        let create ns =
            try
                $"[{ns |> String.concat concatWith}]" |> Name.createExc
            with
            | e ->
                printfn $"cannot create name with {ns}"
                raise e

        let toString  = Name.toString


        let fromString = Name.createExc


        let toStringList =
            Name.toString
            >> (String.replace "[" "")
            >> (String.replace "]" "")
            >> (String.replace addWith concatWith)
            >> (String.split concatWith)


        let add s n =
            try
                $"{n |> toString}{addWith}{s}" |> Name.createExc
            with
            | e ->
                printfn $"cannot add name with {s} and {n}"
                raise e



