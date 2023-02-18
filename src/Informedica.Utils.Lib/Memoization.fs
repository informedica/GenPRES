namespace Informedica.Utils.Lib

/// Utility functions to apply memoization
module Memoization =

    open System.Collections.Generic
    
    /// Memoize a function `f` according
    /// to its parameter
    let memoize f =
        let cache = ref Map.empty
        fun x ->
            match cache.Value.TryFind(x) with
            | Some r -> r
            | None ->
                let r = f x
                cache.Value <- cache.Value.Add(x, r)
                r


