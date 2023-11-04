namespace Informedica.ZIndex.Lib


module Json =

    open System.IO
    open Newtonsoft.Json

    open Informedica.Utils.Lib

    ///
    let serialize x =
        JsonConvert.SerializeObject(x)


    let deSerialize<'T> (s: string) =
        JsonConvert.DeserializeObject<'T>(s)

    let cache p o =
        o
        |> serialize
        |> File.writeTextToFile p

    let clearCache useDemo =
        File.Delete(FilePath.groupCache useDemo)
        File.Delete(FilePath.substanceCache useDemo)
        File.Delete(FilePath.productCache useDemo)
        File.Delete(FilePath.ruleCache useDemo)

    let getCache<'T> p =
        ConsoleWriter.writeInfoMessage $"Reading cache: %s{p}" true false

        File.readAllLines p
        |> String.concat ""
        |> deSerialize<'T>