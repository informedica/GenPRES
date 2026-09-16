// Step 1.3 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #733):
// Filter.Dto and OrderScenario.Dto, the serializable shapes of a Filter and an OrderScenario,
// and the canonical serializer the signing digest and the database share (ADR-0008 §7).
// ADR-0008 invariants 1 to 4: one aggregate each, toDto total, fromDto a Result that never
// throws and never drops a failed part, a Dto only at the boundary.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Types.fs: `DtoError`, after `OrderPlanVersion`.
//   2. A new OrderPlan.fs after Totals.fs in the fsproj: the modules `TextBlock`, `Filter`,
//      `OrderScenario` and `Canonical` below.
//   3. tests/Informedica.GenORDER.Tests/Tests.fs: the tests below, a `DtoTests` module.
//
// The canonical form: one JSON serialization with no whitespace, fields in declared order,
// arrays exactly as the Dto holds them, a BigRational as "numerator/denominator" in lowest
// terms, an option as its value or null. Two Dtos equal as values serialize equal, so law
// L2 compares serializations, and the digest of a signed order plan is over this form.
//
// Run from this directory: dotnet fsi ScenarioDto.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"

open System
open Newtonsoft.Json
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


// ---------------------------------------------------------------------------
// 1. Types.fs
// ---------------------------------------------------------------------------

/// Why a Dto does not parse to its domain value.
[<RequireQualifiedAccess>]
type DtoError =
    // A dose type string the Dto carries that names no dose type
    | UnknownDoseType of string
    // A text block kind the Dto carries that names no kind
    | UnknownTextKind of string
    // The order of a scenario could not be created from its Dto
    | OrderNotCreated of string


// ---------------------------------------------------------------------------
// 2. OrderPlan.fs
// ---------------------------------------------------------------------------

module TextBlock =

    /// A text block as one kind and its text; the markup the client shows is added on
    /// the way out and is no part of the domain.
    module Dto =

        type Dto = { Kind: string; Text: string }


        let toDto =
            function
            | Valid s -> { Kind = "valid"; Text = s }
            | Caution s -> { Kind = "caution"; Text = s }
            | Warning s -> { Kind = "warning"; Text = s }
            | Alert s -> { Kind = "alert"; Text = s }


        let fromDto (dto: Dto) =
            match dto.Kind with
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


/// Every error of a list of results, or every value.
module private Results =

    let sequence (rs: Result<'a, 'e> list) : Result<'a list, 'e list> =
        let errors = rs |> List.choose (function Error e -> Some e | Ok _ -> None)

        if errors.IsEmpty then
            rs |> List.choose Result.toOption |> Ok
        else
            Error errors


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
                dto.DoseTypes |> Array.toList |> List.map DoseTypeDto.fromString |> Results.sequence

            let doseType =
                match dto.DoseType with
                | None -> Ok None
                | Some s -> s |> DoseTypeDto.fromString |> Result.map Some |> Result.mapError List.singleton

            match doseTypes, doseType with
            | Ok doseTypes, Ok doseType ->
                Ok
                    {
                        Indications = dto.Indications
                        Generics = dto.Generics
                        Routes = dto.Routes
                        Forms = dto.Forms
                        DoseTypes = doseTypes |> List.toArray
                        Diluents = dto.Diluents
                        Components = dto.Components
                        Indication = dto.Indication
                        Generic = dto.Generic
                        Route = dto.Route
                        Form = dto.Form
                        DoseType = doseType
                        Diluent = dto.Diluent
                        SelectedComponents = dto.SelectedComponents
                    }
            | Error e1, Error e2 -> Error(e1 @ e2)
            | Error e, _
            | _, Error e -> Error e


module OrderScenario =

    /// The serializable shape of an OrderScenario: text blocks as kind and text, the
    /// order as its own Dto, the dose type as a string.
    module Dto =

        type Dto =
            {
                No: int
                Name: string
                Indication: string
                Form: string
                Route: string
                DoseType: string
                Diluent: string option
                Component: string option
                Item: string option
                Diluents: string[]
                Components: string[]
                Items: string[]
                Prescription: TextBlock.Dto.Dto[][]
                Preparation: TextBlock.Dto.Dto[][]
                Administration: TextBlock.Dto.Dto[][]
                Order: Order.Dto.Dto
                UseAdjust: bool
                UseRenalRule: bool
                RenalRule: string option
                ProductsIds: string[]
            }


        let private blocksToDto (bs: TextBlock[][]) = bs |> Array.map (Array.map TextBlock.Dto.toDto)


        let private blocksFromDto (bs: TextBlock.Dto.Dto[][]) =
            bs
            |> Array.toList
            |> List.map (fun line ->
                line |> Array.toList |> List.map TextBlock.Dto.fromDto |> Results.sequence |> Result.map List.toArray
            )
            |> Results.sequence
            |> Result.map List.toArray
            |> Result.mapError List.concat


        let toDto (sc: OrderScenario) : Dto =
            {
                No = sc.No
                Name = sc.Name
                Indication = sc.Indication
                Form = sc.Form
                Route = sc.Route
                DoseType = sc.DoseType |> DoseTypeDto.toString
                Diluent = sc.Diluent
                Component = sc.Component
                Item = sc.Item
                Diluents = sc.Diluents
                Components = sc.Components
                Items = sc.Items
                Prescription = sc.Prescription |> blocksToDto
                Preparation = sc.Preparation |> blocksToDto
                Administration = sc.Administration |> blocksToDto
                Order = sc.Order |> Order.Dto.toDto
                UseAdjust = sc.UseAdjust
                UseRenalRule = sc.UseRenalRule
                RenalRule = sc.RenalRule
                ProductsIds = sc.ProductsIds
            }


        /// The scenario a Dto is, or every reason it is none. An order that cannot be
        /// created is an error, never a dropped scenario.
        let fromDto (dto: Dto) : Result<OrderScenario, DtoError list> =
            let doseType = dto.DoseType |> DoseTypeDto.fromString |> Result.mapError List.singleton
            let prescription = dto.Prescription |> blocksFromDto
            let preparation = dto.Preparation |> blocksFromDto
            let administration = dto.Administration |> blocksFromDto

            let order =
                try
                    dto.Order
                    |> Order.Dto.fromDto
                    |> Result.mapError (fun m -> [ DtoError.OrderNotCreated $"{m}" ])
                with exn ->
                    Error [ DtoError.OrderNotCreated exn.Message ]

            let errorsOf (r: Result<_, DtoError list>) =
                match r with
                | Error es -> es
                | Ok _ -> []

            let allErrors =
                errorsOf doseType
                @ errorsOf prescription
                @ errorsOf preparation
                @ errorsOf administration
                @ errorsOf order

            match allErrors, doseType, prescription, preparation, administration, order with
            | [], Ok doseType, Ok prescription, Ok preparation, Ok administration, Ok order ->
                Ok
                    {
                        No = dto.No
                        Name = dto.Name
                        Indication = dto.Indication
                        Form = dto.Form
                        Route = dto.Route
                        DoseType = doseType
                        Diluent = dto.Diluent
                        Component = dto.Component
                        Item = dto.Item
                        Diluents = dto.Diluents
                        Components = dto.Components
                        Items = dto.Items
                        Prescription = prescription
                        Preparation = preparation
                        Administration = administration
                        Order = order
                        UseAdjust = dto.UseAdjust
                        UseRenalRule = dto.UseRenalRule
                        RenalRule = dto.RenalRule
                        ProductsIds = dto.ProductsIds
                    }
            | errors, _, _, _, _, _ -> Error errors


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


    let serialize (x: 'a) = JsonConvert.SerializeObject(x, settings)


    let deserialize<'a> (s: string) = JsonConvert.DeserializeObject<'a>(s, settings)


// ---------------------------------------------------------------------------
// 3. Tests, for a DtoTests module in the GenORDER test project.
// ---------------------------------------------------------------------------

open Expecto
open Expecto.Flip


module Fixtures =

    let order med =
        match med |> Medication.toOrderDto |> Order.Dto.fromDto with
        | Ok o -> o
        | Error e -> failwith $"fixture order could not be created: {e}"

    let orders =
        [ "paracetamol supp", Scenarios.pcmSupp; "amphotericin", Scenarios.amfo; "morphine", Scenarios.morfCont ]
        |> List.map (fun (n, m) -> n, order m)

    let filter: Filter =
        {
            Indications = [| "koorts"; "pijn" |]
            Generics = [| "paracetamol" |]
            Routes = [| "rect"; "or" |]
            Forms = [| "zetpil" |]
            DoseTypes = [| Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"; Informedica.GenForm.Lib.Types.Once "" |]
            Diluents = [||]
            Components = [| "paracetamol" |]
            Indication = Some "koorts"
            Generic = Some "paracetamol"
            Route = Some "rect"
            Form = None
            DoseType = Some(Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag")
            Diluent = None
            SelectedComponents = [| "paracetamol" |]
        }

    let scenario (ord: Order) : OrderScenario =
        {
            No = 1
            Name = "paracetamol"
            Indication = "koorts"
            Form = "zetpil"
            Route = "rect"
            DoseType = Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"
            Diluent = None
            Component = Some "paracetamol"
            Item = Some "paracetamol"
            Diluents = [||]
            Components = [| "paracetamol" |]
            Items = [| "paracetamol" |]
            Prescription = [| [| Valid "paracetamol"; Caution "max 4 x/dag" |] |]
            Preparation = [| [| Valid "zetpil 240 mg" |] |]
            Administration = [| [| Warning "rectaal" |]; [| Alert "niet bij lever" |] |]
            Order = ord
            UseAdjust = true
            UseRenalRule = false
            RenalRule = None
            ProductsIds = [| "gpk-1" |]
        }


let tests =
    testList
        "Dtos"
        [
            testList
                "Order.Dto, the existing Dto the scenario nests"
                [
                    for name, ord in Fixtures.orders do
                        test $"L1 for {name}'s order" {
                            ord
                            |> Order.Dto.toDto
                            |> Order.Dto.fromDto
                            |> Expect.equal "the same order" (Ok ord)
                        }
                ]

            testList
                "Filter.Dto"
                [
                    test "L1, fromDto (toDto x) = Ok x" {
                        Fixtures.filter
                        |> Filter.Dto.toDto
                        |> Filter.Dto.fromDto
                        |> Expect.equal "the same filter" (Ok Fixtures.filter)
                    }

                    test "L2, fromDto d |> Result.map toDto = Ok d, in canonical form" {
                        let dto = Fixtures.filter |> Filter.Dto.toDto

                        dto
                        |> Filter.Dto.fromDto
                        |> Result.map (Filter.Dto.toDto >> Canonical.serialize)
                        |> Expect.equal "the same form" (Ok(Canonical.serialize dto))
                    }

                    test "an unknown dose type is an error, and every one is reported" {
                        { (Fixtures.filter |> Filter.Dto.toDto) with
                            DoseTypes = [| "weekly"; "once" |]
                            DoseType = Some "hourly"
                        }
                        |> Filter.Dto.fromDto
                        |> Expect.equal
                            "both"
                            (Error [ DtoError.UnknownDoseType "weekly"; DtoError.UnknownDoseType "hourly" ])
                    }
                ]

            testList
                "OrderScenario.Dto"
                [
                    for name, ord in Fixtures.orders do
                        test $"L1 with {name}'s order" {
                            let sc = Fixtures.scenario ord

                            sc
                            |> OrderScenario.Dto.toDto
                            |> OrderScenario.Dto.fromDto
                            |> Expect.equal "the same scenario" (Ok sc)
                        }

                    for name, ord in Fixtures.orders do
                        test $"L2 with {name}'s order, in canonical form" {
                            let dto = Fixtures.scenario ord |> OrderScenario.Dto.toDto

                            dto
                            |> OrderScenario.Dto.fromDto
                            |> Result.map (OrderScenario.Dto.toDto >> Canonical.serialize)
                            |> Expect.equal "the same form" (Ok(Canonical.serialize dto))
                        }

                    test "an unknown text kind is an error" {
                        let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto

                        { dto with
                            Prescription = [| [| { Kind = "note"; Text = "x" } |] |]
                        }
                        |> OrderScenario.Dto.fromDto
                        |> Expect.equal "named" (Error [ DtoError.UnknownTextKind "note" ])
                    }

                    test "an order that cannot be created is an error, not a dropped scenario" {
                        let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto
                        let broken = Order.Dto.Dto(dto.Order.Id, "paracetamol")
                        broken.Orderable <- Unchecked.defaultof<_>

                        match { dto with Order = broken } |> OrderScenario.Dto.fromDto with
                        | Error [ DtoError.OrderNotCreated _ ] -> ()
                        | other -> failtest $"expected one OrderNotCreated, got {other}"
                    }
                ]

            testList
                "the canonical form"
                [
                    test "no whitespace, fields in declared order, a BigRational as n/d" {
                        let s = Fixtures.filter |> Filter.Dto.toDto |> Canonical.serialize

                        // no whitespace outside string values: none after a colon or a comma
                        (s.Contains "\": " || s.Contains ", \"" || s.Contains ", [")
                        |> Expect.isFalse "no whitespace outside strings"
                        s.StartsWith "{\"Indications\":[\"koorts\",\"pijn\"],\"Generics\"" |> Expect.isTrue "declared order"

                        [ 1N; 3N / 4N; 10N / 4N ]
                        |> Canonical.serialize
                        |> Expect.equal "lowest terms" "[\"1/1\",\"3/4\",\"5/2\"]"
                    }

                    test "an option is its value or null" {
                        (Some "x", (None: string option))
                        |> Canonical.serialize
                        |> Expect.equal "value or null" "{\"Item1\":\"x\",\"Item2\":null}"
                    }

                    test "two Dtos equal as values serialize equal, a re-ordered array does not" {
                        let a = Fixtures.filter |> Filter.Dto.toDto
                        let b = { Fixtures.filter with Indications = [| "koorts"; "pijn" |] } |> Filter.Dto.toDto
                        let c = { Fixtures.filter with Indications = [| "pijn"; "koorts" |] } |> Filter.Dto.toDto

                        Canonical.serialize a |> Expect.equal "equal" (Canonical.serialize b)
                        Canonical.serialize a |> Expect.notEqual "a different order plan" (Canonical.serialize c)
                    }

                    test "a Filter.Dto reads back from its canonical form" {
                        let dto = Fixtures.filter |> Filter.Dto.toDto

                        dto
                        |> Canonical.serialize
                        |> Canonical.deserialize<Filter.Dto.Dto>
                        |> Expect.equal "the same Dto" dto
                    }

                    test "an OrderScenario.Dto reads back from its canonical form, in canonical form" {
                        let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto
                        let s = dto |> Canonical.serialize

                        s
                        |> Canonical.deserialize<OrderScenario.Dto.Dto>
                        |> Canonical.serialize
                        |> Expect.equal "the same form" s
                    }
                ]
        ]


runTestsWithCLIArgs [] [| "--summary" |] tests
