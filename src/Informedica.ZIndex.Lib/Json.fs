namespace Informedica.ZIndex.Lib


module Json =

    open System.IO
    open Newtonsoft.Json

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime


    /// <summary>
    /// Serializes an object to a JSON string
    /// </summary>
    /// <param name="x">The object to serialize</param>
    let serialize x =
        JsonConvert.SerializeObject(x)


    /// <summary>
    /// Deserializes a JSON string to an object
    /// </summary>
    /// <param name="s">The JSON string to deserialize</param>
    let deSerialize<'T> (s: string) =
        JsonConvert.DeserializeObject<'T>(s)


    /// <summary>
    /// Serialize an object to a JSON string
    /// and write it to a file
    /// </summary>
    /// <param name="p">The path to the file</param>
    /// <param name="o">The object to serialize</param>
    let cache p o =
        o
        |> serialize
        |> File.writeTextToFile p


    /// <summary>
    /// Clear the cache files for either a product
    /// or a demo version of the cache.
    /// </summary>
    /// <param name="useDemo">Whether to clear the demo cache files</param>
    let clearCache useDemo =
        File.Delete(FilePath.groupCache useDemo)
        File.Delete(FilePath.substanceCache useDemo)
        File.Delete(FilePath.productCache useDemo)
        File.Delete(FilePath.ruleCache useDemo)


    /// <summary>
    /// Read a cache file and deserialize it to an object
    /// </summary>
    /// <param name="p">The path to the file</param>
    let getCache<'T> p =
        writeInfoMessage $"Reading cache: %s{p}"

        File.readAllLines p
        |> String.concat ""
        |> deSerialize<'T>