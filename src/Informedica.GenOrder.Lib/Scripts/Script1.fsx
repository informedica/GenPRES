
#load "load.fsx"


#time



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib

module Orderable = Order.Orderable

let ord =
    [
        "paracetamol", "suppository", ["paracetamol"]
    ]
    |> Order.Dto.discontinuous
        "1"
        "PCM"
        "rect"
    |> Order.Dto.fromDto

ord.Orderable.Components[0].Items[0]
|> Orderable.Item.toString