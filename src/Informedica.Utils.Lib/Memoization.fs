namespace Informedica.Utils.Lib


/// Utility functions to apply memoization
module Memoization =


    /// Memoize a function `f` according
    /// to its parameter `x`
    ///
    /// # Parameters
    ///
    ///  - ``f`` : the function to memoize
    ///  - ``x`` : the parameter to memoize the function with
    ///
    /// # Returns
    ///
    ///  - ``f x`` : the result of ``f`` applied to ``x``
    ///
    /// # Remarks
    ///
    ///  - the memoization is based on a map
    ///  - the cache is not cleared
    ///
    let inline memoize f x =
        let cache = ref Map.empty
        match cache.Value.TryFind(x) with
        | Some r -> r
        | None ->
            let r = f x
            cache.Value <- cache.Value.Add(x, r)
            r



    module Tests =

        open Swensen.Unquote

        /// Test the memoization of a function
        let testMemoization() =
            let f x = x + 1
            let f' = memoize f
            let r1 = f' 1
            let r2 = f' 1
            let r3 = f' 2
            test <@ r1 = r2 && r1 <> r3 @>
