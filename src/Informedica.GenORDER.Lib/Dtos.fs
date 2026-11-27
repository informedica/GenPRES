namespace Informedica.GenOrder.Lib

// The Dto helpers, the Dtos of the text block, dose type, filter and order category, and the canonical JSON
// serialization used for a digest and for the database. A toDto never fails; a fromDto returns a Result and
// never throws. OrderScenario.Dto and OrderContext.Dto are in OrderContext.fs, the order plan Dtos in OrderPlan.fs.

open System
open Newtonsoft.Json
open FsToolkit.ErrorHandling
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
// Opened after GenForm, so the TextBlock cases shadow the GenForm Message cases.
open Informedica.GenOrder.Lib.Types


/// Helpers that read values that can be null after JSON deserialization, and collect results.
module DtoResult =

    /// Returns the array, or an empty array when the array is null after JSON deserialization.
    let orEmpty (xs: 'a[]) = if isNull xs then [||] else xs


    /// Returns the string, or an empty string when the string is null after JSON deserialization.
    let orBlank (s: string) = if isNull s then "" else s


    /// Returns null for a null or empty string, and the string otherwise. orBlank reads null back as an
    /// empty string.
    let blankToNull s = if s |> String.isNullOrEmpty then null else s


    /// Returns every value when all results are Ok, otherwise every error.
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


/// Reads Dtos that can be null after JSON deserialization, without dereferencing a null Dto.
module Nested =

    /// Reads the Dto with read. A Dto that is null after JSON deserialization gives DtoError.Missing name.
    let required name (dto: 'a) (read: 'a -> Result<'b, DtoError list>) =
        if isNull (box dto) then
            Error [ DtoError.Missing name ]
        else
            read dto


    /// Reads every Dto of an array with read; a null array reads as an empty array. Each item gets the
    /// path and its index, such as "Components[0]", and a null item gives DtoError.Missing with that path.
    /// Returns the errors of every item that fails.
    let items path read xs =
        xs
        |> DtoResult.orEmpty
        |> Array.toList
        |> List.mapi (fun i x ->
            let itemPath = $"%s{path}[%i{i}]"
            required itemPath x (read itemPath)
        )
        |> List.sequenceValidationA


module TextBlock =

    /// The serializable shape of a text block: the kind as a string and the text, without markup.
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
            // A block that is null after JSON deserialization has no kind and gives UnknownTextKind.
            let kind =
                if isNull (box dto) then
                    ""
                else
                    dto.Kind |> DtoResult.orBlank

            match kind with
            | "valid" -> Ok(Valid dto.Text)
            | "caution" -> Ok(Caution dto.Text)
            | "warning" -> Ok(Warning dto.Text)
            | "alert" -> Ok(Alert dto.Text)
            | kind -> Error(DtoError.UnknownTextKind kind)


/// Writes a dose type as the string of DoseType.toString for the Dtos, and reads it back.
module DoseTypeDto =

    let toString (dt: DoseType) = dt |> DoseType.toString


    /// Reads a dose type from the category and the text, split at the first space. Returns UnknownDoseType
    /// for an unknown category or a null string; an empty string gives NoDoseType.
    let fromString (s: string) =
        if isNull s then
            Error(DtoError.UnknownDoseType "")
        else

            let category, text =
                match s.Trim().Split([| ' ' |], 2) with
                | [| c; t |] -> c, t
                | [| c |] -> c, ""
                | _ -> "", ""

            match DoseType.parse category text with
            | dt, None -> Ok dt
            | _, Some _ -> Error(DtoError.UnknownDoseType s)


module Filter =

    /// Reconciles the selections of the sent filter with the lists of a fresh filter. A selection that the
    /// fresh list still has stays selected, and the list narrows to that fresh entry; a selection that the
    /// fresh list does not have is cleared, and the fresh list is used whole. The dose types are the resulting
    /// list when it has one entry, otherwise the sent list, because the fresh list holds the dose types of
    /// every rule for the patient. The diluents, the components, their selections and the picked products
    /// come from the sent filter, because the evaluation sets them and not the rules.
    let reconcile (fresh: Filter) (sent: Filter) : Filter =
        let pick eqs itm (items: 'a[]) =
            match
                items
                |> Array.tryFind (fun x -> itm |> Option.map (eqs x) |> Option.defaultValue false)
            with
            | Some x -> itm, [| x |]
            | None -> None, items

        let ind, inds = fresh.Indications |> pick String.equalsCapInsens sent.Indication
        let gen, gens = fresh.Generics |> pick String.equalsCapInsens sent.Generic
        let rte, rtes = fresh.Routes |> pick String.equalsCapInsens sent.Route
        let frm, frms = fresh.Forms |> pick String.equalsCapInsens sent.Form
        let dtp, dtps = fresh.DoseTypes |> pick DoseType.eqs sent.DoseType

        { fresh with
            Indication = ind
            Indications = inds
            Generic = gen
            Generics = gens
            Route = rte
            Routes = rtes
            Form = frm
            Forms = frms
            DoseType = dtp
            DoseTypes = if dtps |> Array.length = 1 then dtps else sent.DoseTypes
            Diluents = sent.Diluents
            Components = sent.Components
            DoseRuleComponents = sent.DoseRuleComponents
            Diluent = sent.Diluent
            SelectedComponents = sent.SelectedComponents
            PickedProducts = sent.PickedProducts
        }


    /// Returns true when the sent filter selects an indication, generic, route, form or dose type that the
    /// reconciled filter no longer selects.
    let dropped (sent: Filter) (reconciled: Filter) =
        let gone (chosen: 'a option) (remaining: 'a option) = chosen.IsSome && remaining.IsNone

        gone sent.Indication reconciled.Indication
        || gone sent.Generic reconciled.Generic
        || gone sent.Route reconciled.Route
        || gone sent.Form reconciled.Form
        || gone sent.DoseType reconciled.DoseType


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
                DoseRuleComponents: string[]
                Indication: string option
                Generic: string option
                Route: string option
                Form: string option
                DoseType: string option
                Diluent: string option
                /// None selects all components; an empty array selects no optional component.
                SelectedComponents: string[] option
                PickedProducts: string[]
            }


        /// Maps a component selection to the Dto field: None for all components, else the selected names.
        let componentSelection =
            function
            | ComponentSelection.AllComponents -> None
            | ComponentSelection.Selected cs -> cs |> List.toArray |> Some


        let componentSelectionBack =
            function
            | None -> ComponentSelection.AllComponents
            | Some cs -> cs |> Array.toList |> ComponentSelection.Selected


        let toDto (f: Filter) : Dto =
            {
                Indications = f.Indications
                Generics = f.Generics
                Routes = f.Routes
                Forms = f.Forms
                DoseTypes = f.DoseTypes |> Array.map DoseTypeDto.toString
                Diluents = f.Diluents
                Components = f.Components
                DoseRuleComponents = f.DoseRuleComponents
                Indication = f.Indication
                Generic = f.Generic
                Route = f.Route
                Form = f.Form
                DoseType = f.DoseType |> Option.map DoseTypeDto.toString
                Diluent = f.Diluent
                SelectedComponents = f.SelectedComponents |> componentSelection
                PickedProducts = f.PickedProducts
            }


        let fromDto (dto: Dto) : Result<Filter, DtoError list> =
            Nested.required
                "Filter"
                dto
                (fun dto ->
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
                                DoseRuleComponents = dto.DoseRuleComponents |> DtoResult.orEmpty
                                Indication = dto.Indication
                                Generic = dto.Generic
                                Route = dto.Route
                                Form = dto.Form
                                DoseType = doseType
                                Diluent = dto.Diluent
                                SelectedComponents = dto.SelectedComponents |> componentSelectionBack
                                PickedProducts = dto.PickedProducts |> DtoResult.orEmpty
                            }
                    | Error e1, Error e2 -> Error(e1 @ e2)
                    | Error e, _
                    | _, Error e -> Error e
                )


/// The canonical JSON serialization of a Dto, used for a digest and for the database. It writes no
/// whitespace, the fields in declared order, arrays in their own order, a BigRational as
/// "numerator/denominator" in lowest terms, and an option as its value or null.
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
                let big (x: string) = x |> System.Numerics.BigInteger.Parse |> BigRational.fromBigInt

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


    /// Reads a class without a parameterless constructor, such as Order.Dto.Dto, by creating it
    /// uninitialized and setting its properties from the JSON. Records and unions use the Newtonsoft
    /// construction.
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
                // every DateTime in UTC, so the form never depends on the machine's time zone
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                ContractResolver = PopulateResolver()
            )

        s.Converters.Add(BigRationalConverter())
        s.Converters.Add(OptionConverter())
        s


    let serialize (x: 'a) = JsonConvert.SerializeObject(x, settings)


    let deserialize<'a> (s: string) = JsonConvert.DeserializeObject<'a>(s, settings)


/// Writes an order category as a string for the Dtos, "medication" or "nutrition:" with the nutrition
/// category, and reads it back.
module OrderCategoryDto =

    /// Writes the string of an order category. The match has no wildcard, so a new category gives an
    /// incomplete match warning.
    let toString =
        function
        | OrderCategory.Medication -> "medication"
        | OrderCategory.Nutrition NutritionCategory.EnteralFeeding -> "nutrition:enteral-feeding"
        | OrderCategory.Nutrition NutritionCategory.EnteralSupplement -> "nutrition:enteral-supplement"
        | OrderCategory.Nutrition NutritionCategory.TPN -> "nutrition:tpn"
        | OrderCategory.Nutrition NutritionCategory.Lipid -> "nutrition:lipid"
        | OrderCategory.Nutrition NutritionCategory.ElectrolyteGlucose -> "nutrition:electrolyte-glucose"


    let fromString (s: string) : Result<OrderCategory, DtoError> =
        match s |> DtoResult.orBlank |> String.toLower |> String.trim with
        | "medication" -> Ok OrderCategory.Medication
        | "nutrition:enteral-feeding" -> Ok(OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
        | "nutrition:enteral-supplement" -> Ok(OrderCategory.Nutrition NutritionCategory.EnteralSupplement)
        | "nutrition:tpn" -> Ok(OrderCategory.Nutrition NutritionCategory.TPN)
        | "nutrition:lipid" -> Ok(OrderCategory.Nutrition NutritionCategory.Lipid)
        | "nutrition:electrolyte-glucose" -> Ok(OrderCategory.Nutrition NutritionCategory.ElectrolyteGlucose)
        | s -> Error(DtoError.UnknownCategory s)
