namespace Informedica.GenSolver.Lib


[<AutoOpen>]
module Utils =


    module List =

        /// <summary>
        /// Reorder a liat according to a permutation. Such that a list
        /// of lists is returned such that [a1; a2; a3; ..; an] becomes
        /// [|
        ///     [ a1; a2; a3; ..; an ]
        ///     [ a2; a1; a3; ..; an ]
        ///     ...
        ///     [ an; a1; a2; ..; an-1 ]
        /// |]
        /// </summary>
        let reorder xs =
            let n = xs |> List.length
            if n <= 2 then [ xs ]
            else
                let y = xs[0]
                let xs = xs |> List.tail
                let n = n - 2
                [
                    y::xs
                    for i in 0..n do
                        match i with
                        | 0            -> xs[0]::y::xs[1..n]
                        | _ when i = n -> xs[n]::y::xs[0..n-1]
                        | _            ->
                            xs[i]::y::xs[0..i-1] @ xs[i+1..n]
                ]



    module Constants =


        /// Maximum times a loop can run to solve a
        /// list of equations.
        let MAX_LOOP_COUNT = 10

        /// Maximum set of values that can be used to perform
        /// a Variable calculation.
        let MAX_CALC_COUNT = 300

        /// Reduce a set of values to a maximum of 100 for a Variable
        let PRUNE = 4

        /// Maximum quantity of a numerator or denominator to prevent
        /// infinite loops calculating a minimum or maximum.
        let MAX_BIGINT =
            999999999999999999999999999999999999999999999999I



    module ValueUnit =

        open MathNet.Numerics

        open Informedica.Utils.Lib.BCL

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


        /// <summary>
        /// Print a ValueUnit to a string with a given precision delimited by "#" for Value and "|" for Unit
        /// </summary>
        /// <param name="prec">The precision with which value should be printed</param>
        /// <param name="vu">The ValueUnit to print</param>
        /// <example>
        /// <code>
        /// [| 1N/3N |] |> ValueUnit.create  Units.Mass.milliGram |> toDelimitedString 2
        /// // returns "#0,33# |mg|"
        /// [| 1N/3N; 1N/5N |] |> ValueUnit.create  Units.Mass.milliGram |> toDelimitedString 2
        /// // returns "#0,33#, #0,2# |mg|"
        /// </code>
        /// </example>
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
                |> List.choose Units.fromString
                |> function
                    | [] -> vu |> getUnit
                    | [ u ] -> u
                    | u::rest ->
                        rest
                        |> List.fold (fun acc u ->
                            CombiUnit(acc, OpPer, u)
                        ) u

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
            /// or subtraction, fails when
            /// the operation is neither.
            let (|Mult|Div|Add|Sub|) op =
                match op with
                | _ when op |> opIsMult -> Mult
                | _ when op |> opIsDiv -> Div
                | _ when op |> opIsAdd -> Add
                | _ when op |> opIsSubtr -> Sub
                | _ -> failwith "Operator is not supported"

