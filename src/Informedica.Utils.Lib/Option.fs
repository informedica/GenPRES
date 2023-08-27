namespace Informedica.Utils.Lib

/// Additional functions for the
/// fsharp option type
[<RequireQualifiedAccess>]
module Option =


    /// Create a `None`
    let none _ = None


    /// Choose `opt1` or `opt2` based on a
    /// predicate function `cp`
    let choose cp opt1 opt2 =
        match opt1, opt2 with
        | None, None   -> None
        | Some _, None -> opt1
        | None, Some _ -> opt2
        | Some x1, Some x2 ->
            if cp x1 x2 then x1 |> Some else x2 |> Some


    /// Choose the minimum of two options
    let min opt1 opt2 = choose (<=) opt1 opt2


    /// choose the maximum of two options
    let max opt1 opt2 = choose (>=) opt1 opt2


    module Tests =

        open Swensen.Unquote

        /// Test none function
        let testNone () =
            let opt = none ()
            match opt with
            | None -> "None"
            | Some _ -> "Some"
            |> fun s -> test <@ (s = "None") @>


        /// Test choose function
        let testChoose () =
            let opt1 = Some 1
            let opt2 = Some 2
            let opt3 = None
            let opt4 = None
            let opt5 = Some 3
            let opt6 = Some 4
            let opt7 = choose (<=) opt1 opt2
            let opt8 = choose (<=) opt3 opt4
            let opt9 = choose (<=) opt5 opt6
            match opt7, opt8, opt9 with
            | Some x, None, Some y -> test <@ x = 1 && y = 3 @>
            | _ -> test <@ false @>


        /// Test min function
        let testMin () =
            let opt1 = Some 1
            let opt2 = Some 2
            let opt3 = None
            let opt4 = None
            let opt5 = Some 3
            let opt6 = Some 4
            let opt7 = min opt1 opt2
            let opt8 = min opt3 opt4
            let opt9 = min opt5 opt6
            match opt7, opt8, opt9 with
            | Some x, None, Some y -> test <@ (x = 1 && y = 3) @>
            | _ -> test <@ false @>


        /// Test max function
        let testMax () =
            let opt1 = Some 1
            let opt2 = Some 2
            let opt3 = None
            let opt4 = None
            let opt5 = Some 3
            let opt6 = Some 4
            let opt7 = max opt1 opt2
            let opt8 = max opt3 opt4
            let opt9 = max opt5 opt6
            match opt7, opt8, opt9 with
            | Some x, None, Some y -> test <@ (x = 2 && y = 4) @>
            | _ -> test <@ false @>


        /// Run all tests
        let testAll () =
            testNone ()
            testChoose ()
            testMin ()
            testMax ()