namespace Informedica.GenSolver.Lib


[<AutoOpen>]
module Utils =

    module Constants =


        let MAX_LOOP_COUNT = 10


        let MAX_CALC_COUNT = 5000


        let MAX_BIGINT =
            999999999999999999999999999999999999999999999999I



    module BigRational =

        open MathNet.Numerics


        let denominator (br: BigRational) = br.Denominator

        let numerator (br: BigRational) = br.Numerator



    module Array =

        open Informedica.Utils.Lib.BCL

        let removeBigRationalMultiples xs =
            if xs |> Array.isEmpty then
                xs
            else
                xs
                |> Array.fold
                    (fun acc x1 ->
                        acc
                        |> Array.filter (fun x2 -> x1 = x2 || x2 |> BigRational.isMultiple x1 |> not)
                    )
                    xs



    module ValueUnit =

        open MathNet.Numerics

        open Informedica.Utils.Lib
        open Informedica.Utils.Lib.BCL

        open Informedica.GenUnits.Lib
        open ValueUnit




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