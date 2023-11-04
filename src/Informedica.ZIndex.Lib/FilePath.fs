namespace Informedica.ZIndex.Lib


module FilePath =

    open Informedica.Utils.Lib


    [<Literal>]
    let data = "./data/"

    [<Literal>]
    let GStandPath =  data + "zindex/"

    let substanceCache useDemo =
        if not useDemo then data + "cache/substance.cache"
        else data + "cache/substance.demo"

    let productCache useDemo =
        if not useDemo  then data + "cache/product.cache"
        else data + "cache/product.demo"

    let ruleCache useDemo =
        if not useDemo  then data + "cache/rule.cache"
        else data + "cache/rule.demo"

    let groupCache useDemo =
        if not useDemo  then data + "cache/group.cache"
        else data + "cache/group.demo"

    //https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
    let [<Literal>] genpres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"


    let [<Literal>] GENPRES_PROD = "GENPRES_PROD"

    let useDemo () =
        Env.getItem GENPRES_PROD
        |> Option.map ((<>) "1")
        |> Option.defaultValue true
