namespace Informedica.ZIndex.Lib


module FilePath =

    open Informedica.Utils.Lib


    [<Literal>]
    let data = "./data/"

    [<Literal>]
    let GStandPath =  data + "zindex/"

    let substanceCache useDemo =
        if File.exists (data + "cache/substance.cache") || not useDemo then data + "cache/substance.cache"
        else data + "cache/substance.demo"

    let productCache useDemo =
        if File.exists (data + "cache/product.cache")|| not useDemo  then data + "cache/product.cache"
        else data + "cache/product.demo"

    let ruleCache useDemo =
        if File.exists (data + "cache/rule.cache")|| not useDemo  then data + "cache/rule.cache"
        else data + "cache/rule.demo"

    let groupCache useDemo =
        if File.exists (data + "cache/group.cache")|| not useDemo  then data + "cache/group.cache"
        else data + "cache/group.demo"

    //https://docs.google.com/spreadsheets/d/1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ/edit?usp=sharing
    let [<Literal>] genpres = "1AEVYnqjAbVniu3VuczeoYvMu3RRBu930INhr3QzSDYQ"
