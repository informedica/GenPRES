
#load "load.fsx"


#time

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