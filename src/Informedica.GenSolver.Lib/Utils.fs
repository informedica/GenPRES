namespace Informedica.GenSolver.Lib


[<AutoOpen>]
module Utils =

    module Constants =


        /// Maximum times a loop can run to solve a
        /// list of equations.
        let MAX_LOOP_COUNT = 10

        /// Maximum set of values that can be used to perform
        /// a Variable calculation.
        let MAX_CALC_COUNT = 100

        /// Reduce a set of values to a maximum of 20 for a Variable
        let PRUNE = 20

        /// Maximum quantity of a numerator or denominator to prevent
        /// infinite loops calculating a minimum or maximum.
        let MAX_BIGINT =
            999999999999999999999999999999999999999999999999I



    module ValueUnit =

        open MathNet.Numerics

        open Informedica.Utils.Lib
        open Informedica.Utils.Lib.BCL

        open Informedica.GenUnits.Lib
        open ValueUnit


        /// <summary>
        /// Print a ValueUnit to a string with a given precision of 3
        /// </summary>
        /// <param name="exact">Whether the exact values should be printed</param>
        /// <example>
        /// <code>
        /// ValueUnit.create [| 1N; 2N |] Units.Mass.milliGram |> toStr false
        ///
        /// </code>
        /// </example>
        let toStr exact =
            if exact then
                toStringDutchShort
                // getValue
                // >> Array.toReadableString
                >> String.removeBrackets
            else
                toStringDecimalDutchShortWithPrec 3


        let toDelimitedString prec vu =
            let u =
                vu
                |> getUnit
                |> Units.toStringDutchShort
                |> String.replace "*" "/"
                |> String.split "/"
                |> function
                | [u1;u2;u3] when u3 |> String.startsWith "kg" -> [u1;u3;u2]
                | xs -> xs
                |> String.concat "/"
                |> Units.fromString
                |> Option.defaultValue (vu |> getUnit)

            vu
            |> getValue
            |> withUnit u
            |> toStringDecimalDutchShortWithPrec prec
            |> String.split " "
            |> function
            | v::u ->
                let u = u |> String.concat " "
                let v =
                    v
                    |> String.split ";"
                    |> List.map (sprintf "#%s#")
                    |> String.concat ", "

                $"{v} |{u}|"
            | s -> s |> String.concat " "


        module Operators =

            /// Constant 0
            let zero =
                [| 0N |] |> create Units.Count.times

            /// Constant 1
            let one =
                [| 1N |] |> create Units.Count.times

            /// Constant 2
            let two =
                [| 2N |] |> create Units.Count.times

            /// Constant 3
            let three =
                [| 3N |] |> create Units.Count.times

            /// Check whether the operator is subtraction
            let opIsSubtr op = (three |> op <| two) = three - two // = 1

            /// Check whether the operator is addition
            let opIsAdd op = (three |> op <| two) = three + two // = 5

            /// Check whether the operator is multiplication
            let opIsMult op = (three |> op <| two) = three * two // = 6

            /// Check whether the operator is divsion
            let opIsDiv op = (three |> op <| two) = three / two // = 3/2



            /// Match an operator `op` to either
            /// multiplication, division, addition
            /// or subtraction, returns `NoOp` when
            /// the operation is neither.
            let (|Mult|Div|Add|Subtr|) op =
                match op with
                | _ when op |> opIsMult -> Mult
                | _ when op |> opIsDiv -> Div
                | _ when op |> opIsAdd -> Add
                | _ when op |> opIsSubtr -> Subtr
                | _ -> failwith "Operator is not supported"

