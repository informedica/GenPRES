namespace Informedica.Utils.Lib


module Env =

    open System
    open System.Collections.Generic
    open System.Diagnostics
    open System.Runtime.InteropServices


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
            if variables.ContainsKey(key) then variables[key] <- value
            else variables.Add(key, value)
        variables


    /// Get an environment variable from the current process.
    /// Returns None if the variable does not exist.
    let getItem envName =
        match Environment.GetEnvironmentVariable(envName) with
        | null -> None
        | v -> Some v


    let getSystemInfo () : string =
        // helper to format bytes in a friendly way
        let formatBytes (bytes: int64) =
            let b = float bytes
            let kb, mb, gb, tb = 1024.0, 1024.0 ** 2.0, 1024.0 ** 3.0, 1024.0 ** 4.0
            if b >= tb then $"%.2f{b / tb} TB"
            elif b >= gb then $"%.2f{b / gb} GB"
            elif b >= mb then $"%.2f{b / mb} MB"
            elif b >= kb then $"%.2f{b / kb} KB"
            else $"%d{bytes} B"

        let machine = Environment.MachineName
        let user = Environment.UserName
        let osDesc = RuntimeInformation.OSDescription
        let osArch = RuntimeInformation.OSArchitecture
        let procArch = RuntimeInformation.ProcessArchitecture
        let framework = RuntimeInformation.FrameworkDescription
        let cores = Environment.ProcessorCount

        // Cross-platform memory info:
        // - TotalAvailableMemoryBytes: memory limit GC can use (approx system memory/cgroup limit)
        // - WorkingSet64: physical memory used by current process
        // - GC.GetTotalMemory: managed heap size
        let gcInfo = GC.GetGCMemoryInfo()
        let totalAvailBytes = gcInfo.TotalAvailableMemoryBytes
        let totalAvailStr = if totalAvailBytes > 0L then formatBytes totalAvailBytes else "unknown"

        let workingSet =
            try Process.GetCurrentProcess().WorkingSet64 with _ -> 0L
        let workingSetStr = formatBytes workingSet

        let managedHeapStr = GC.GetTotalMemory(false) |> int64 |> formatBytes

        String.Join(
            Environment.NewLine,
            [| $"Machine: %s{machine}"
               $"User: %s{user}"
               $"OS: %s{osDesc} (%A{osArch})"
               $".NET: %s{framework} (%A{procArch})"
               $"CPU cores: %d{cores}"
               $"Memory (GC total available): %s{totalAvailStr}"
               $"Memory (process working set): %s{workingSetStr}"
               $"Memory (managed heap): %s{managedHeapStr}" |]
        )