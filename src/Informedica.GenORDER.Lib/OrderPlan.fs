namespace Informedica.GenOrder.Lib

// The Dtos of the order plan types (ADR-0008, docs/adr/0008-contract-model-dto-mapping-boundary.md):
// a serializable shape per aggregate, toDto total, fromDto a Result that never throws, and
// the canonical serialization the signing digest and the database share. OrderScenario.Dto
// lives with the OrderScenario module in Api.fs.

open System
open Newtonsoft.Json
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
// after GenForm, so that the TextBlock cases win over its Message cases
open Informedica.GenOrder.Lib.Types


/// Every error of a list of results, or every value.
module DtoResult =

    /// A reference field a serializer left null is read as absent, never dereferenced.
    let orEmpty (xs: 'a[]) = if isNull xs then [||] else xs


    /// A string a serializer left null is read as blank.
    let orBlank (s: string) = if isNull s then "" else s


    let sequence (rs: Result<'a, 'e> list) : Result<'a list, 'e list> =
        let errors =
            rs
            |> List.choose (
                function
                | Error e -> Some e
                | Ok _ -> None
            )

        if errors.IsEmpty then
            rs |> List.choose Result.toOption |> Ok
        else
            Error errors


module TextBlock =

    /// A text block as one kind and its text; the markup the client shows is added on
    /// the way out and is no part of the domain.
    module Dto =

        type Dto =
            {
                Kind: string
                Text: string
            }


        let toDto =
            function
            | Valid s ->
                {
                    Kind = "valid"
                    Text = s
                }
            | Caution s ->
                {
                    Kind = "caution"
                    Text = s
                }
            | Warning s ->
                {
                    Kind = "warning"
                    Text = s
                }
            | Alert s ->
                {
                    Kind = "alert"
                    Text = s
                }


        let fromDto (dto: Dto) =
            match dto.Kind |> DtoResult.orBlank with
            | "valid" -> Ok(Valid dto.Text)
            | "caution" -> Ok(Caution dto.Text)
            | "warning" -> Ok(Warning dto.Text)
            | "alert" -> Ok(Alert dto.Text)
            | kind -> Error(DtoError.UnknownTextKind kind)


/// The string form of a dose type, for the Dtos: the category and its text as
/// `DoseType.toString` writes them, and the same read back strictly.
module DoseTypeDto =

    let toString (dt: DoseType) = dt |> DoseType.toString


    /// A dose type from its string form; an unknown category is an error, an empty string
    /// is NoDoseType, as the parser reads it.
    let fromString (s: string) =
        let category, text =
            match s.Trim().Split([| ' ' |], 2) with
            | [| c; t |] -> c, t
            | [| c |] -> c, ""
            | _ -> "", ""

        match DoseType.parse category text with
        | dt, None -> Ok dt
        | _, Some _ -> Error(DtoError.UnknownDoseType s)


module Filter =

    /// The serializable shape of a Filter: the same fields, dose types as strings.
    module Dto =

        type Dto =
            {
                Indications: string[]
                Generics: string[]
                Routes: string[]
                Forms: string[]
                DoseTypes: string[]
                Diluents: string[]
                Components: string[]
                Indication: string option
                Generic: string option
                Route: string option
                Form: string option
                DoseType: string option
                Diluent: string option
                SelectedComponents: string[]
            }


        let toDto (f: Filter) : Dto =
            {
                Indications = f.Indications
                Generics = f.Generics
                Routes = f.Routes
                Forms = f.Forms
                DoseTypes = f.DoseTypes |> Array.map DoseTypeDto.toString
                Diluents = f.Diluents
                Components = f.Components
                Indication = f.Indication
                Generic = f.Generic
                Route = f.Route
                Form = f.Form
                DoseType = f.DoseType |> Option.map DoseTypeDto.toString
                Diluent = f.Diluent
                SelectedComponents = f.SelectedComponents
            }


        let fromDto (dto: Dto) : Result<Filter, DtoError list> =
            let doseTypes =
                dto.DoseTypes
                |> DtoResult.orEmpty
                |> Array.toList
                |> List.map DoseTypeDto.fromString
                |> DtoResult.sequence

            let doseType =
                match dto.DoseType with
                | None -> Ok None
                | Some s ->
                    s
                    |> DtoResult.orBlank
                    |> DoseTypeDto.fromString
                    |> Result.map Some
                    |> Result.mapError List.singleton

            match doseTypes, doseType with
            | Ok doseTypes, Ok doseType ->
                Ok
                    {
                        Indications = dto.Indications |> DtoResult.orEmpty
                        Generics = dto.Generics |> DtoResult.orEmpty
                        Routes = dto.Routes |> DtoResult.orEmpty
                        Forms = dto.Forms |> DtoResult.orEmpty
                        DoseTypes = doseTypes |> List.toArray
                        Diluents = dto.Diluents |> DtoResult.orEmpty
                        Components = dto.Components |> DtoResult.orEmpty
                        Indication = dto.Indication
                        Generic = dto.Generic
                        Route = dto.Route
                        Form = dto.Form
                        DoseType = doseType
                        Diluent = dto.Diluent
                        SelectedComponents = dto.SelectedComponents |> DtoResult.orEmpty
                    }
            | Error e1, Error e2 -> Error(e1 @ e2)
            | Error e, _
            | _, Error e -> Error e


/// The one serialization of a Dto: the form the signing digest is computed over and the
/// form the database stores. No whitespace, fields in declared order, arrays as held, a
/// BigRational as "numerator/denominator" in lowest terms, an option as its value or null.
module Canonical =

    type BigRationalConverter() =
        inherit JsonConverter()

        override _.CanConvert(t: Type) = t = typeof<BigRational>

        override _.WriteJson(writer: JsonWriter, value: obj, _: JsonSerializer) =
            let br = value :?> BigRational
            writer.WriteValue($"{br.Numerator}/{br.Denominator}")

        override _.ReadJson(reader: JsonReader, _: Type, _: obj, _: JsonSerializer) =
            match (reader.Value :?> string).Split '/' with
            | [| n; d |] ->
                let big (x: string) =
                    x |> System.Numerics.BigInteger.Parse |> BigRational.fromBigInt

                big n / big d :> obj
            | _ -> raise (JsonSerializationException "a BigRational is written as numerator/denominator")


    /// An option as its value or null, instead of the union's Case and Fields.
    type OptionConverter() =
        inherit JsonConverter()

        override _.CanConvert(t: Type) =
            t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

        override _.WriteJson(writer: JsonWriter, value: obj, serializer: JsonSerializer) =
            match value with
            | null -> writer.WriteNull()
            | v ->
                let inner = v.GetType().GetProperty("Value").GetValue v
                serializer.Serialize(writer, inner)

        override _.ReadJson(reader: JsonReader, t: Type, _: obj, serializer: JsonSerializer) =
            let inner = t.GetGenericArguments()[0]

            if reader.TokenType = JsonToken.Null then
                null
            else
                let v = serializer.Deserialize(reader, inner)
                let some = t.GetMethod("Some")
                some.Invoke(null, [| v |])


    /// The Dto classes of the libraries (`Order.Dto.Dto`, `Orderable.Dto.Dto`, ...) have no
    /// parameterless constructor and every property settable, so on read they are created
    /// uninitialized and populated from the JSON, property by property. Records and unions
    /// keep Newtonsoft's own construction.
    type PopulateResolver() =
        inherit Serialization.DefaultContractResolver()

        override _.CreateObjectContract(t: Type) =
            let contract = base.CreateObjectContract t

            let isFSharpData =
                Microsoft.FSharp.Reflection.FSharpType.IsRecord t
                || Microsoft.FSharp.Reflection.FSharpType.IsUnion t

            if isNull contract.DefaultCreator && not t.IsAbstract && not isFSharpData then
                contract.OverrideCreator <-
                    Serialization.ObjectConstructor<obj>(fun _ ->
                        Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject t
                    )

                contract.CreatorParameters.Clear()

            contract


    let settings =
        let s =
            JsonSerializerSettings(
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Include,
                Formatting = Formatting.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                ContractResolver = PopulateResolver()
            )

        s.Converters.Add(BigRationalConverter())
        s.Converters.Add(OptionConverter())
        s


    let serialize (x: 'a) =
        JsonConvert.SerializeObject(x, settings)


    let deserialize<'a> (s: string) =
        JsonConvert.DeserializeObject<'a>(s, settings)
