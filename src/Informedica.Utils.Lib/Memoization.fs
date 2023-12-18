namespace Informedica.Utils.Lib


/// Utility functions to apply memoization
module Memoization =

    open System.Collections.Generic

    /// <summary>
    /// Memoize a function `f` according
    /// to its parameter `x`
    /// </summary>
    /// <param name="f">The function to memoize</param>
    /// <remarks>
    ///  - the memoization is based on a map
    ///  - the cache is not cleared
    /// </remarks>
    let inline memoize f =
        let cache = ref Map.empty
        fun x ->
            match cache.Value.TryFind(x) with
            | Some r -> r
            | None ->
                let r = f x
                cache.Value <- cache.Value.Add(x, r)
                r

    let inline memoizeOne f =
        let dic = Dictionary<_, _>()
        let memoized par =
            if dic.ContainsKey(par) then
                dic[par]
            else
                let result = f par
                dic.Add(par, result)
                result

        memoized

    let inline memoize2Int f =
        let dic = Dictionary<int * int, _>()
        let memoized p1 p2 =
            let hash = p1.GetHashCode(), p2.GetHashCode()
            if dic.ContainsKey(hash) then
                dic[hash]
            else
                let result = f p1 p2
                dic.Add(hash, result)
                result

        memoized


    module Tests =

        open Swensen.Unquote

        /// Test the memoization of a function
        let testMemoization () =
            let f x = x + 1
            let f' = memoize f
            let r1 = f' 1
            let r2 = f' 1
            let r3 = f' 2
            test <@ r1 = r2 && r1 <> r3 @>


        // test that second use of memoized function is much
        // faster than first use
        let testMemoizationSpeed () =
            // create a function that takes a long time to compute
            // for example a Fibonacci function
            let rec fib(n: int):int =
                match n with
                | 0 | 1 -> n
                | n -> fib (n-1) + fib (n - 2)

            // create a memoized version of the function
            let f' = memoize fib

            // create a stopwatch
            let sw = System.Diagnostics.Stopwatch()
            // call the function twice
            let r1 =
                sw.Start()
                f' 37 |> ignore
                sw.Stop()
                sw.ElapsedMilliseconds

            sw.Reset()
            let r2 =
                sw.Start()
                f' 37 |> ignore
                sw.Stop()
                sw.ElapsedMilliseconds

            // check that the second call is much faster
            test <@ r1 > r2 @>