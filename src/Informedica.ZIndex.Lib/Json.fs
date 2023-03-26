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

    let clearCache () =
        File.Delete(FilePath.groupCache false)
        File.Delete(FilePath.substanceCache false)
        File.Delete(FilePath.productCache false)
        File.Delete(FilePath.ruleCache false)

    let getCache<'T> p =
        ConsoleWriter.writeInfoMessage $"Reading cache: %s{p}" true false

        File.readAllLines p
        |> String.concat ""
        |> deSerialize<'T>