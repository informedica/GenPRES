namespace Informedica.KinderFormularium.Lib


[<AutoOpen>]
module Utils =


    module Regex =

        open System.Text.RegularExpressions

        let regex s = Regex(s)

        let regexMatch m s = (m |> regex).Match(s)

        [<Literal>]
        let alphaRegex = "(?<Alpha>[a-zA-Z]*)"

        [<Literal>]
        let numRegex = "(?<Numeric>[0-9]*)"

        [<Literal>]
        let floatRegex = "(?<Float>[-+]?(\d*[.])?\d+)"

        let matchFloat s =
            (s |> regexMatch floatRegex).Groups["Float"].Value

        let matchAlpha s =
            (s |> regexMatch alphaRegex).Groups["Alpha"].Value

        let matchFloatAlpha s =
            let grps = (floatRegex + alphaRegex |> regex).Match(s).Groups
            grps["Float"].Value, grps["Alpha"].Value


    module File =

        open System.IO

        [<Literal>]
        let cachePath = "pediatric.cache"

        (*
        *)
        let writeTextToFile path text =
            File.WriteAllText(path, text)

        let exists path =
            File.Exists(path)

        let readAllLines path = File.ReadAllLines(path)



    module Json =

        open Newtonsoft.Json

        ///
        let serialize x =
            JsonConvert.SerializeObject(x)


        let deSerialize<'T> (s: string) =
            JsonConvert.DeserializeObject<'T>(s)


