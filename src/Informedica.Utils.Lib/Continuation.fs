namespace Informedica.Utils.Lib

/// Utility functions to support a continuation
/// stile of programming, i.e. provide success and
/// failure functions
module Continuation =

    /// Apply a function `f` to `x` and
    /// return the result to the `fsucc` function.
    /// Catch errors and return the original parameter `x`
    /// along with the exception `exn` to the `ffail` function.
    let tryCatchCont f fsucc ffail x =
        try
            f x
            |> fsucc
        with
        | exn -> (x, exn) |> ffail

    /// Handle a null result `y` by applying `x` to `f`
    /// and in case of null pass the argument `x` to `fnull`
    /// otherwise return `y` to `fsucc`
    let nullCont f fsucc fnull x =
        match x |> f with
        | y when y |> isNull -> x |> fnull
        | y -> y |> fsucc


    module Tests =

        /// Test the function tryCatchCont
        let testTryCatchCont () =
            let f x = x + 1
            let fsucc x = $"success: %d{x}"
            let ffail (x, _) = $"failed: %d{x}"

            let res = 1 |> tryCatchCont f fsucc ffail
            printfn $"%s{res}"

            let f x = x / 0
            let res = 1 |> tryCatchCont f fsucc ffail
            printfn $"%s{res}"

        /// Test the function nullCont
        let testNullCont () =
            let f x = x + 1 |> box
            let fsucc x = $"success: %A{x}"
            let fnull x = $"null: %A{x}"

            let res = nullCont f fsucc fnull 1
            printfn $"%s{res}"

            let f _ = null
            let res = nullCont f fsucc fnull 1
            printfn $"%s{res}"

        /// Run all tests
        let testAll () =
            testTryCatchCont ()
            testNullCont ()