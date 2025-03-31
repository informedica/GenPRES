namespace Informedica.ZIndex.Lib


module FilePath =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime


    [<Literal>]
    let data = "./data/"

    [<Literal>]
    let GStandPath =  data + "zindex/"


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