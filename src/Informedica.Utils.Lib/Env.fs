namespace Informedica.Utils.Lib


module Env =

    open System
    open System.IO
    open System.Linq
    open System.Collections.Generic


    /// Returns current process environment variables as a dictionary (portable)
    /// - Uses the current process environment only (works on all platforms)
    /// - Case-insensitive keys to avoid casing pitfalls across OSes
    /// - Does not filter out PATH or any other variable
    let environmentVars () =
        let variables = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let all = Environment.GetEnvironmentVariables()
        for pair in all do
            let entry = unbox<Collections.DictionaryEntry> pair
            let key = string entry.Key
            let value =
                match entry.Value with
                | null -> ""
                | :? string as s -> s
                | v -> v.ToString()
            // last one wins if duplicates appear (shouldn't for process scope)
            if variables.ContainsKey(key) then variables.[key] <- value
            else variables.Add(key, value)
        variables


    /// Get an environment variable from the current process.
    /// Returns None if the variable does not exist.
    let getItem envName =
        match Environment.GetEnvironmentVariable(envName) with
        | null -> None
        | v -> Some v