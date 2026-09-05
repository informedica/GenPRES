namespace Informedica.GenForm.Tests


open System
open Informedica.Utils.Lib.BCL
open Newtonsoft.Json


/// <summary>
/// Shared BigRational JSON helper for the offline round-trip fixtures.
///
/// Defined ONCE and used by both the round-trip tests (<c>Tests.fs</c>) and the
/// fixture generator (<c>Scripts/DownloadFixtures.fsx</c>, which <c>#load</c>s
/// this file), so the writer and the reader can never drift out of sync — a
/// mismatch would make every committed fixture silently fail to deserialize.
/// </summary>
/// <remarks>
/// This deliberately does NOT reuse <c>Informedica.Utils.Lib.Json</c>: that
/// converter (a) checks <c>t = typeof&lt;BigRational&gt;</c>, which never matches
/// because MathNet instances are the nested type <c>BigRational+Q</c>, and
/// (b) reads with <c>Int32.parse</c>, overflowing ValueUnit base values &gt; 2^31.
/// Fixing the central converter would change app-wide serialization output and
/// risk persisted caches, so the fixture format is kept local. This converter
/// matches with <c>IsAssignableFrom</c> and round-trips a BigRational as a
/// compact, BigInteger-safe "num/den" string. Newtonsoft's built-in F# union
/// converter handles ValueUnit / Unit / option.
/// </remarks>
module FixtureJson =

    type BigRationalConverter() =
        inherit JsonConverter()

        override _.CanConvert t = typeof<BigRational>.IsAssignableFrom t

        override _.WriteJson(w: JsonWriter, v: obj, _: JsonSerializer) =
            let br = v :?> BigRational
            w.WriteValue($"%s{br.Numerator.ToString()}/%s{br.Denominator.ToString()}")

        override _.ReadJson(r: JsonReader, _: Type, _: obj, _: JsonSerializer) =
            let s = string r.Value
            let parts = s.Split('/')

            if parts.Length <> 2 then
                raise (FormatException $"BigRational fixture token expected 'num/den', got: '%s{s}'")

            let bi (t: string) = System.Numerics.BigInteger.Parse t
            (BigRational.FromBigInt(bi parts[0]) / BigRational.FromBigInt(bi parts[1])) :> obj

    let private settings =
        let cs = System.Collections.Generic.List<JsonConverter>()
        cs.Add(BigRationalConverter())

        JsonSerializerSettings(
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = cs
        )

    let serialize (x: 'a) =
        JsonConvert.SerializeObject(x, settings)

    let deSerialize<'T> (s: string) : 'T =
        JsonConvert.DeserializeObject<'T>(s, settings)
