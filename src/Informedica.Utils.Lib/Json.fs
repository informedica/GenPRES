namespace Informedica.Utils.Lib


module Json =

    open System
    open Informedica.Utils.Lib.BCL
    open Newtonsoft.Json
    open Newtonsoft.Json.Linq


    type BigRationalConverter() =
        inherit JsonConverter()

        override this.CanConvert(objectType: Type) = objectType = typeof<BigRational>

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
            let numerator = jObject["Numerator"].ToString() |> fromStr
            let denominator = jObject["Denominator"].ToString() |> fromStr
            numerator / denominator :> obj


    let settings =
        let converters = System.Collections.Generic.List<JsonConverter>()
        converters.Add(BigRationalConverter())

        // SECURITY: TypeNameHandling MUST remain `None`. Newtonsoft.Json's `Auto`
        // (and `All`/`Objects`) honour `$type` properties in incoming JSON and
        // can instantiate arbitrary types via known gadget chains, leading to
        // remote code execution if any caller ever passes attacker-controlled
        // JSON to `deSerialize`. See:
        //   - https://github.com/JamesNK/Newtonsoft.Json/issues/2457
        //   - "Friday the 13th: JSON Attacks" (Black Hat 2017)
        // If a future use case genuinely needs polymorphic deserialization,
        // opt in *locally* with a `SerializationBinder` allow-list — never by
        // changing this default.
        JsonSerializerSettings(
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = converters
        )


    /// <summary>
    /// Serializes an object to a JSON string
    /// </summary>
    /// <param name="x">The object to serialize</param>
    let serialize x = JsonConvert.SerializeObject(x, settings)


    /// <summary>
    /// Deserializes a JSON string to an object
    /// </summary>
    /// <param name="s">The JSON string to deserialize</param>
    let deSerialize<'T> (s: string) =
        try
            JsonConvert.DeserializeObject<'T>(s, settings)
        with e ->
            printfn $"cannot deserialize {s}:\n{e.ToString()}"
            raise e
