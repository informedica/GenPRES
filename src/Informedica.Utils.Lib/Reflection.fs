namespace Informedica.Utils.Lib

/// Helper functions for FSharp.Reflection
[<RequireQualifiedAccess>]
module Reflection =

    open Microsoft.FSharp.Reflection

    /// Turn a union case to a string value
    let toString (x:'a) =
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
        | case, _ -> case.Name

    /// Create a union case option from a string value
    /// Returns None if the string value does not match a union case
    let fromString<'T> (s:string) =
        let t = typeof<'T>
        match FSharpType.GetUnionCases t |> Array.filter (fun case -> case.Name = s) with
        |[|case|] -> Some(FSharpValue.MakeUnion(case,[||]) :?> 'T)
        |_ -> None



    module Tests =

        open Swensen.Unquote

        type TestUnion = | A | B | C

        /// Test Reflection.toString
        let testToString() =
            let a = A
            let b = B
            let c = C

            let aString = toString a
            let bString = toString b
            let cString = toString c

            test <@ (aString = "A") @>
            test <@ (bString = "B") @>
            test <@ (cString = "C") @>

        /// Test Reflection.fromString
        let testFromString() =
            let a = fromString<TestUnion> "A"
            let b = fromString<TestUnion> "B"
            let c = fromString<TestUnion> "C"
            let d = fromString<TestUnion> "D"

            test <@ a = Some A @>
            test <@ b = Some B @>
            test <@ c = Some C @>
            test <@ d = None @>


        /// Run all tests
        let testAll() =
            testToString()
            testFromString()