namespace Informedica.GenSolver.Lib


[<AutoOpen>]
module Utils =

    open System
    open Informedica.Utils.Lib.BCL


    module Parallel =


        let maxDepth = int (Math.Log(float Environment.ProcessorCount))


        let totalWorders = int (2.0 ** (float maxDepth))


    module Constants =


        /// Maximum times a loop can run to solve a
        /// list of equations.
        let MAX_LOOP_COUNT = 20

        /// Maximum set of values that can be used to perform
        /// a Variable calculation.
        let MAX_CALC_COUNT = 500

        /// Reduce a set of values to a maximum of 100 for a Variable
        let PRUNE = 4

        /// Maximum quantity of a numerator or denominator to prevent
        /// infinite loops calculating a minimum or maximum.
        let MAX_BIGINT = 999999999999999999999999999999999999999999999999I


    module ValueUnit =

        open Informedica.GenUnits.Lib
        open ValueUnit


        /// <summary>
        /// Print a ValueUnit to a string with a given precision of 3
        /// </summary>
        /// <param name="exact">Whether the exact values should be printed</param>
        /// <example>
        /// <code>
        /// [| 1N/3N |] |> ValueUnit.create  Units.Mass.milliGram |> toStr false
        /// // returns "0,333 mg"
        /// [| 1N/3N |] |> ValueUnit.create  Units.Mass.milliGram |> toStr true
        /// // returns "1/3 mg"
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


        module Operators =

            /// Constant 0
            let zero = [| 0N |] |> create Units.Count.times

            /// Constant 1
            let one = [| 1N |] |> create Units.Count.times

            /// Constant 2
            let two = [| 2N |] |> create Units.Count.times

            /// Constant 3
            let three = [| 3N |] |> create Units.Count.times

            /// Match an operator op to either multiplication, division, addition or subtraction
            /// by delegating detection to the BigRational active pattern.
            let (|Mul|Div|Add|Sub|) op =
                // Bridge the ValueUnit op to a BigRational op using dimensionless units (Count.times)
                let toBrOp (o: ValueUnit -> ValueUnit -> ValueUnit) =
                    fun (a: BigRational) (b: BigRational) ->
                        let va = [| a |] |> create Units.Count.times
                        let vb = [| b |] |> create Units.Count.times
                        let vr = o va vb
                        let vs = vr |> getValue
                        if vs.Length > 0 then vs[0] else 0N

                match toBrOp op with
                | BigRational.Mul -> Mul
                | BigRational.Div -> Div
                | BigRational.Add -> Add
                | BigRational.Sub -> Sub
