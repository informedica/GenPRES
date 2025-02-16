
//#I "C:\Development\Informedica\libs\GenUnits\src\Informedica.GenUnits.Lib\scripts"
//#I __SOURCE_DIRECTORY__


#load "load.fsx"

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.Utils.Lib.BCL

open Swensen.Unquote

open FParsec


"piece[General]"
|> run Parser.parseUnit

"day[General]"
|> Units.fromString

open Informedica.Utils.Lib

let isGeneral s =
    let xs = (String.regex "[^\[\]]+(?=\])").Matches(s)
    xs
    |> Seq.map _.Value
    |> Seq.forall (String.equalsCapInsens "general")


"piece"
|> isGeneral


open Informedica.Utils.Lib


module Json =

    open System
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq


    type BigRationalConverter() =
        inherit JsonConverter()

        override this.CanConvert(objectType: Type) =
            objectType = typeof<BigRational>

        override this.WriteJson(writer: JsonWriter, value: obj, _: JsonSerializer) =
            let bigRational = value :?> BigRational
            let numerator = bigRational.Numerator
            let denominator = bigRational.Denominator
            writer.WriteStartObject()
            writer.WritePropertyName("Numerator")
            writer.WriteValue(numerator.ToString())
            writer.WritePropertyName("Denominator")
            writer.WriteValue(denominator.ToString())
            writer.WriteEndObject()


        override this.ReadJson(reader: JsonReader, _: Type, _: obj, _: JsonSerializer) =
            let jObject = JObject.Load(reader)
            let fromStr = Int32.parse >> BigInteger.fromInt >> BigRational.fromBigInt
            let numerator = jObject.["Numerator"].ToString() |> fromStr
            let denominator = jObject.["Denominator"].ToString() |> fromStr
            numerator / denominator :> obj


    let settings =
        let converters = System.Collections.Generic.List<JsonConverter>()
        converters.Add(BigRationalConverter())

        JsonSerializerSettings(
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        Converters = converters
    )


    /// <summary>
    /// Serializes an object to a JSON string
    /// </summary>
    /// <param name="x">The object to serialize</param>
    let serialize x =
        JsonConvert.SerializeObject(x, settings)


    /// <summary>
    /// Deserializes a JSON string to an object
    /// </summary>
    /// <param name="s">The JSON string to deserialize</param>
    let deSerialize<'T> (s: string) =
        try
            JsonConvert.DeserializeObject<'T>(s, settings)
        with
        | e ->
            printfn $"cannot deserialize {s}:\n{e.ToString()}"
            raise e


open MathNet.Numerics

let value = (1N/3N)

value
|> Json.serialize
|> Json.deSerialize<BigRational>


Units.Mass.milliGram
|> Units.per Units.Weight.kiloGram
|> Units.per Units.Time.hour
|> Json.serialize
|> Json.deSerialize<Unit>

Units.General.general "test"
|> Json.serialize
|> Json.deSerialize<Unit>


let droplets =
    Units.Volume.dropletWithDropsPerMl 36N
    |> ValueUnit.singleWithValue 1N

droplets |> ValueUnit.toStringDecimalDutchShort

Units.Volume.dropletWithDropsPerMl 36N |> Units.eqsUnit Units.Volume.droplet


Units.Volume.milliLiter
|> ValueUnit.singleWithValue 1N
|> fun vu ->
    printfn $"vu eqsgroup : {vu |> ValueUnit.eqsGroup droplets}"
    printfn $"vu / droplets: {vu / droplets}"

Units.Volume.dropletWithDropsPerMl 36N
|> Units.toString Units.Dutch Units.Short

Units.Volume.dropletWithDropsPerMl 36N
|> Units.tryFind

let setLast u u1 =
    match u1 |> ValueUnit.getUnits |> List.rev with
    | _ :: rest ->
        match u::rest |> List.rev with
        | u::rest ->
            rest
            |> List.fold (fun acc x ->
                acc
                |> Units.per x
            ) u
        | _ -> u
    | _ -> u

Units.Mass.milliGram
|> Units.per Units.Weight.kiloGram
|> Units.per Units.Time.hour
|> setLast Units.Time.day