namespace Informedica.Utils.Lib


/// Utility functions to apply memoization
module Memoization =

    open System.Collections.Concurrent

    /// <summary>
    /// Memoize a function <c>f</c> according
    /// to its parameter <c>x</c>
    /// </summary>
    /// <param name="f">The function to memoize</param>
    /// <remarks>
    ///  - the memoization is based on a thread-safe <c>ConcurrentDictionary</c>
    ///  - the cache is not cleared
    ///  - safe under concurrent access: the <c>Lazy</c> wrapper guarantees that
    ///    <c>f x</c> is evaluated AT MOST ONCE per distinct <c>x</c>, even when many
    ///    threads request the same uncached key simultaneously. (A bare
    ///    <c>GetOrAdd(x, f)</c> can run the factory on several threads before one
    ///    wins; wrapping the value in <c>Lazy</c> means only the stored instance is
    ///    ever forced, so a cold-cache stampede cannot re-run an expensive loader.)
    ///  - the key constraint is <c>equality</c> (was <c>comparison</c> for the old
    ///    Map-based version); every comparable key is also an equality key, so all
    ///    existing call sites remain valid.
    ///  - <c>null</c> keys are supported (the old F# <c>Map</c> tolerated them but a
    ///    <c>ConcurrentDictionary</c> throws on a null key), cached in a dedicated
    ///    cell so the null-key result is also computed at most once.
    /// </remarks>
    let memoize (f: 'a -> 'b) : 'a -> 'b =
        let cache = ConcurrentDictionary<'a, Lazy<'b>>()
        // ConcurrentDictionary rejects a null key; keep the null-key result aside.
        // `lazy` (not forced unless a null key actually arrives) keeps value-type
        // 'a — which can never be null — from ever evaluating f on default('a).
        let nullCell = lazy (f Unchecked.defaultof<'a>)

        fun x ->
            if obj.ReferenceEquals(box x, null) then
                nullCell.Value
            else
                cache.GetOrAdd(x, (fun k -> lazy (f k))).Value

    /// <summary>
    /// Thread-safe single-argument memoization. Behaves exactly like <c>memoize</c>
    /// (ConcurrentDictionary + Lazy: <c>f</c> runs at most once per key, null keys
    /// supported); kept as a separate name for source compatibility.
    /// </summary>
    let memoizeOne (f: 'a -> 'b) : 'a -> 'b = memoize f

    /// <summary>
    /// Thread-safe memoization of a two-argument function, keyed on the pair of
    /// argument hash codes.
    /// </summary>
    /// <remarks>
    ///  - thread-safe (ConcurrentDictionary + Lazy: <c>f</c> runs at most once per key)
    ///  - the key is a value tuple of hash codes, so it can never be null
    ///  - hash collisions share a slot (the first pair to hash to a given key wins) —
    ///    unchanged from the original implementation
    /// </remarks>
    let memoize2Int (f: 'a -> 'b -> 'c) : 'a -> 'b -> 'c =
        let cache = ConcurrentDictionary<int * int, Lazy<'c>>()

        fun p1 p2 ->
            let key = p1.GetHashCode(), p2.GetHashCode()
            cache.GetOrAdd(key, (fun _ -> lazy (f p1 p2))).Value


    /// Memoizes f for at most maxCount distinct arguments, compared by structural equality. A new argument that
    /// finds maxCount entries first removes the entries whose value is built; the entries still being built stay,
    /// so that concurrent calls with one argument run f once, and the cache can hold more than maxCount entries
    /// while they are built. When f raises an exception, the entry is removed so that the next call runs f again.
    /// The argument must not be null.
    let memoizeBounded maxCount (f: 'a -> 'b) =
        let cache = ConcurrentDictionary<'a, Lazy<'b>>(HashIdentity.Structural)

        fun x ->
            let entry =
                match cache.TryGetValue x with
                | true, entry -> entry
                | false, _ ->
                    // Removing every built entry keeps the bound without bookkeeping of use. The size check and
                    // the insert are separate steps, so concurrent new arguments can pass the bound briefly.
                    if cache.Count >= maxCount then
                        for kv in cache do
                            if kv.Value.IsValueCreated then
                                cache.TryRemove kv |> ignore

                    cache.GetOrAdd(x, (fun k -> lazy (f k)))

            try
                entry.Value
            with _ ->
                // A Lazy keeps the exception; remove this entry, and only this entry, so that the next call
                // runs f again.
                cache.TryRemove(System.Collections.Generic.KeyValuePair(x, entry)) |> ignore
                reraise ()


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
            let rec fib (n: int) : int =
                match n with
                | 0
                | 1 -> n
                | n -> fib (n - 1) + fib (n - 2)

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
