namespace Informedica.Utils.Lib

/// Helper functions for FSharp.Reflection
module Reflection =

    open Microsoft.FSharp.Reflection

    /// Turn a union case to a string value
    let toString (x:'a) = 
        match FSharpValue.GetUnionFields(x, typeof<'a>) with
        | case, _ -> case.Name

    /// Create a union case option from a string value
    let fromString<'T> (*(t:System.Type)*) (s:string) =
        let t = typeof<'T>
        match FSharpType.GetUnionCases t |> Array.filter (fun case -> case.Name = s) with
        |[|case|] -> Some(FSharpValue.MakeUnion(case,[||]) :?> 'T)
        |_ -> None

