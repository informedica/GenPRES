namespace Informedica.Utils.Lib.BCL

/// Utility methods to handle a `Char` in a
/// functional style
[<RequireQualifiedAccess>]
module Char =

    open System


    /// Applies a function to a `Char`
    let apply f (c: char) = f c


    /// Util to enable type inference
    let get = apply id


    /// Array of all small caps letters
    let letters = [|'a'..'z'|]


    /// Array of all capital letters
    let capitals = [|'A'..'Z'|]


    /// Checks if a `Char` is a capital letter
    let isCapital c = capitals |> Seq.exists ((=) c)


    /// Turn char to lowercase
    let toLower = Char.ToLower


    /// Turn char to uppercase
    let toUpper = Char.ToUpper


    /// Checks if a `Char` is a letter
    let isLetter c =
        letters
        |> Seq.exists ((=) (c |> toLower))


    /// Checks if a `Char` is a small caps letter
    let isLower c =
        if c |> isLetter |> not then false
        else
            c
            |> isCapital
            |> not


    module Tests =

        open Swensen.Unquote


        // Test if a char is a capital letter
        let testIsCapital () =
            test <@ 'A' |> isCapital = true @>
            test <@ 'a' |> isCapital = false @>


        // Test if a char is a letter
        let testIsLetter () =
            test <@ 'A' |> isLetter = true @>
            test <@ '1' |> isLetter = false @>


        // Test toLower
        let testToLower () =
            test <@ 'A' |> toLower = 'a' @>


        // Test toUpper
        let testToUpper () =
            test <@ 'a' |> toUpper = 'A' @>


        // Test isLower
        let testIsLower () =
            test <@ 'a' |> isLower = true @>
            test <@ 'A' |> isLower = false @>
            test <@ '1' |> isLower = false @>


        // Test all
        let testAll () =
            testIsCapital()
            testIsLetter()
            testToLower()
            testToUpper()
            testIsLower()

