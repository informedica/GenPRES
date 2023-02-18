namespace Informedica.Utils.Lib.BCL

/// Utility methods to handle a `Char` in a 
/// functional style
[<RequireQualifiedAccess>]
module Char =

    open System

    let apply f (c: char) = f c

    let get = apply id

    let letters = [|'a'..'z'|]

    let capitals = [|'A'..'Z'|]

    let isCapital c = capitals |> Seq.exists ((=) c)

    let toLower = Char.ToLower

    let toUpper = Char.ToUpper

    let isLetter c = 
        letters
        |> Seq.exists ((=) (c |> toLower))

    let isLower c =
        if c |> isLetter |> not then false
        else
            c
            |> isCapital
            |> not
