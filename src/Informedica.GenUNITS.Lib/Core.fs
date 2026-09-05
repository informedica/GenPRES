namespace Informedica.GenUnits.Lib

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib.Types

module Core =

    /// Transforms an operator to a string
    let opToStr op =
        match op with
        | OpPer -> "/"
        | OpTimes -> "*"
        | OpPlus -> "+"
        | OpMinus -> "-"


    /// Transforms a string to an operator
    /// (*, /, +, -), throws an error if
    /// no match
    let opFromString s =
        match s with
        | _ when s = "/" -> OpPer
        | _ when s = "*" -> OpTimes
        | _ when s = "+" -> OpPlus
        | _ when s = "-" -> OpMinus
        | _ -> raise (System.FormatException $"Cannot parse %s{s} to operand")


    /// A 'count' unit with n = 1
    let count = 1N |> Times |> Count
