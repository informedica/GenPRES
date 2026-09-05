namespace Informedica.ZIndex.Lib


module FilePath =

    open System
    open System.IO

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime


    /// Find the data directory by searching up from the given starting directory
    /// The data directory must contain a 'zindex' subfolder to be valid
    let findDataDir startDir =
        let rec search dir =
            if String.IsNullOrEmpty(dir) then
                None
            else
                let dataPath = Path.Combine(dir, "data")
                let zindexPath = Path.Combine(dataPath, "zindex")
                // Only accept data directories that contain the zindex subfolder
                if Directory.Exists(dataPath) && Directory.Exists(zindexPath) then
                    Some dataPath
                else
                    let parent = Directory.GetParent(dir)
                    if parent <> null then search parent.FullName else None

        search startDir


    /// Find the data directory by searching up from the given starting directory (internal for testing)
    let findDataDirInternal startDir = findDataDir startDir


    /// Get the base data path (internal for testing)
    /// Parameters allow injecting different directory sources for testing.
    /// NOTE: this helper is retained only for the existing FilePath unit tests
    /// (it is no longer used at runtime — runtime paths go through AppPath). Its
    /// relative "./data" fallback is intentionally left unchanged to preserve the
    /// existing test contract and must not be "unified" with AppPath's
    /// CurrentDirectory fallback without updating those tests.
    let getDataPathInternal currentDir assemblyPath =
        match findDataDir currentDir with
        | Some p -> p
        | None ->
            match findDataDir assemblyPath with
            | Some p -> p
            | None -> "./data"


    /// The base data directory, resolved by the unified AppPath resolver.
    ///
    /// A function, not a value: as a value this ran in this file's static constructor
    /// and forced AppPath's `lazy` root at whatever moment anything in FilePath was
    /// first touched — before an Env.loadDotEnv () could set GENPRES_ROOT, defeating
    /// the deferral documented at AppPath.fs. No cache is needed; AppPath.root is
    /// already lazy, so this is a Path.Combine and a concat. See issue #523.
    let data () = AppPath.dataDir () + "/"


    let GStandPath () = data () + "zindex/"


    /// Get the path to the Substance cache file
    let substanceCache useDemo =
        if not useDemo then
            data () + "cache/substance.cache"
        else
            data () + "cache/substance.demo"
        |> fun s ->
            let s = s |> Path.GetFullPath
            writeInfoMessage $"substance cache path: {s}"
            s


    /// Get the path to the Product cache file
    let productCache useDemo =
        if not useDemo then
            data () + "cache/product.cache"
        else
            data () + "cache/product.demo"
        |> fun s ->
            let s = s |> Path.GetFullPath
            writeInfoMessage $"product cache path: {s}"
            s


    /// Get the path to the Rule cache file
    let ruleCache useDemo =
        if not useDemo then
            data () + "cache/rule.cache"
        else
            data () + "cache/rule.demo"
        |> fun s ->
            let s = s |> Path.GetFullPath
            writeInfoMessage $"rule cache path: {s}"
            s


    /// Get the path to the Group cache file
    let groupCache useDemo =
        if not useDemo then
            data () + "cache/group.cache"
        else
            data () + "cache/group.demo"
        |> fun s ->
            let s = s |> Path.GetFullPath
            writeInfoMessage $"group cache path: {s}"
            s


    [<Literal>]
    let GENPRES_PROD = "GENPRES_PROD"


    /// Check whether the demo version of
    /// the cache files should be used.
    let useDemo () =
        Env.getItem GENPRES_PROD |> Option.map ((<>) "1") |> Option.defaultValue true
