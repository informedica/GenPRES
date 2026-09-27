namespace Informedica.Utils.Lib


module Env =

    open System
    open System.IO
    open System.Collections.Generic
    open System.Diagnostics
    open System.Runtime.InteropServices

    open Informedica.Utils.Lib.BCL


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
            if variables.ContainsKey(key) then
                variables[key] <- value
            else
                variables.Add(key, value)

        variables


    /// Get an environment variable from the current process.
    /// Returns None if the variable does not exist.
    let getItem envName =
        match Environment.GetEnvironmentVariable(envName) with
        | null -> None
        | v -> Some v


    /// Removes one pair of matching surrounding quotes, double or single, from a value.
    /// A value without them, or with a quote at one end only, is returned as it is.
    let unquote (value: string) =
        let quotedWith (q: string) = value.Length >= 2 && value.StartsWith q && value.EndsWith q

        if quotedWith "\"" || quotedWith "'" then
            value.Substring(1, value.Length - 2)
        else
            value


    /// Parses one line of a .env file into its key and value, both trimmed and the value
    /// unquoted. The value runs from the first equals sign to the end of the line, so it can
    /// hold an equals sign itself. A blank line, a comment, a line without an equals sign and a
    /// line with an empty key give None.
    let parseLine (line: string) =
        let trimmed = line.Trim()

        if trimmed |> String.isNullOrWhiteSpace || trimmed.StartsWith "#" then
            None
        else
            match trimmed.IndexOf '=' with
            | -1 -> None
            | idx ->
                let key = trimmed.Substring(0, idx).Trim()
                let value = trimmed.Substring(idx + 1).Trim() |> unquote

                if key |> String.isNullOrWhiteSpace then
                    None
                else
                    Some(key, value)


    /// Load environment variables from a .env file.
    /// Searches upward from the current directory for a .env file.
    /// Only sets variables that are not already set in the environment,
    /// preserving the override chain: shell/CI/Docker > .env > code defaults.
    /// Returns true if a .env file was found and processed.
    let loadDotEnv () =
        match Environment.CurrentDirectory |> Directory.tryFindFileUpward ".env" with
        | None -> false
        | Some path ->
            File.ReadAllLines(path)
            |> Array.choose parseLine
            |> Array.iter (fun (key, value) ->
                // Only set if not already present in the environment
                if Environment.GetEnvironmentVariable(key) |> isNull then
                    Environment.SetEnvironmentVariable(key, value)
            )

            true


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

        let totalAvailStr =
            if totalAvailBytes > 0L then
                formatBytes totalAvailBytes
            else
                "unknown"

        let workingSet =
            try
                Process.GetCurrentProcess().WorkingSet64
            with _ ->
                0L

        let workingSetStr = formatBytes workingSet

        let managedHeapStr = GC.GetTotalMemory(false) |> int64 |> formatBytes

        String.Join(
            Environment.NewLine,
            [|
                $"Machine: %s{machine}"
                $"User: %s{user}"
                $"OS: %s{osDesc} (%A{osArch})"
                $".NET: %s{framework} (%A{procArch})"
                $"CPU cores: %d{cores}"
                $"Memory (GC total available): %s{totalAvailStr}"
                $"Memory (process working set): %s{workingSetStr}"
                $"Memory (managed heap): %s{managedHeapStr}"
            |]
        )
