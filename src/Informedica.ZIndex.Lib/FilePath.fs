namespace Informedica.ZIndex.Lib


module FilePath =

    open System
    open System.IO
    open System.Reflection

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime


    /// Find the data directory by searching up from the given starting directory
    /// The data directory must contain a 'zindex' subfolder to be valid
    let private findDataDir startDir =
        let rec search dir =
            if String.IsNullOrEmpty(dir) then None
            else
                let dataPath = Path.Combine(dir, "data")
                let zindexPath = Path.Combine(dataPath, "zindex")
                // Only accept data directories that contain the zindex subfolder
                if Directory.Exists(dataPath) && Directory.Exists(zindexPath) then Some dataPath
                else
                    let parent = Directory.GetParent(dir)
                    if parent <> null then search parent.FullName
                    else None
        search startDir


    /// Get the base data path, searching from current directory or assembly location
    let private getDataPath () =
        // First try current directory (for production scenarios)
        match findDataDir Environment.CurrentDirectory with
        | Some p -> p
        | None ->
            // Fall back to assembly location (for dotnet test scenarios)
            let assemblyPath =
                try
                    let location = Assembly.GetExecutingAssembly().Location
                    if not (String.IsNullOrEmpty(location)) then
                        Path.GetDirectoryName(location)
                    else ""
                with _ -> ""

            match findDataDir assemblyPath with
            | Some p -> p
            | None -> "./data"  // Last resort fallback


    let data = getDataPath () + "/"

    let GStandPath = data + "zindex/"


    /// Get the path to the Substance cache file
    let substanceCache useDemo =
        if not useDemo then data + "cache/substance.cache"
        else data + "cache/substance.demo"
        |> fun s ->
            let s = s |> System.IO.Path.GetFullPath
            writeInfoMessage $"substance cache path: {s}"
            s


    /// Get the path to the Product cache file
    let productCache useDemo =
        if not useDemo  then data + "cache/product.cache"
        else data + "cache/product.demo"
        |> fun s ->
            let s = s |> System.IO.Path.GetFullPath
            writeInfoMessage $"product cache path: {s}"
            s


    /// Get the path to the Rule cache file
    let ruleCache useDemo =
        if not useDemo  then data + "cache/rule.cache"
        else data + "cache/rule.demo"
        |> fun s ->
            let s = s |> System.IO.Path.GetFullPath
            writeInfoMessage $"rule cache path: {s}"
            s


    /// Get the path to the Group cache file
    let groupCache useDemo =
        if not useDemo  then data + "cache/group.cache"
        else data + "cache/group.demo"
        |> fun s ->
            let s = s |> System.IO.Path.GetFullPath
            writeInfoMessage $"group cache path: {s}"
            s


    //https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
    let [<Literal>] genpres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"


    let [<Literal>] GENPRES_PROD = "GENPRES_PROD"


    /// Check whether the demo version of
    /// the cache files should be used.
    let useDemo () =
        Env.getItem GENPRES_PROD
        |> Option.map ((<>) "1")
        |> Option.defaultValue true