/// The schema of the medication text, docs/domain/medication.schema.json: generated from the
/// medication's Dto, and every text of the tests valid against it.
module MedicationSchemaTests

open Newtonsoft.Json.Linq
open NJsonSchema
open NJsonSchema.Generation
open YamlDotNet.Core
open YamlDotNet.RepresentationModel
open Expecto
open Expecto.Flip

open Informedica.Utils.Lib.BCL
open Informedica.GenOrder.Lib


/// The schema generated from the medication's Dto, and a text validated against it.
module Schema =


    /// A number as the text writes it: a decimal, or a fraction without a decimal form.
    let numberPattern = @"^-?\d+(\.\d+)?$|^-?\d+/\d+$"


    /// The fields of the Dto that hold one number.
    let numberFields = [ "Div"; "Divisible" ]


    /// The fields of the Dto that hold an array of numbers.
    let numberArrayFields = [ "Value" ]


    /// A number in a hand-written text may be plain, 500, or quoted, "500" or "1/3", as the reader
    /// takes both.
    let asNumber (s: JsonSchema) =
        s.Type <- JsonObjectType.Number ||| JsonObjectType.String
        s.Pattern <- numberPattern


    /// The schema of the medication text: absent members allowed, null refused, no other member.
    /// The cases of Order.OrderType.all are the enum of OrderType.
    let generate () =
        let settings =
            SystemTextJsonSchemaGeneratorSettings(DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull)

        let schema = JsonSchema.FromType<Medication.Dto.Dto>(settings)

        let objects = [ schema; yield! schema.Definitions.Values ]

        for o in objects do
            for KeyValue(name, p) in o.Properties do
                if numberFields |> List.contains name then
                    p |> asNumber

                if numberArrayFields |> List.contains name then
                    p.Item |> asNumber

        let orderType = schema.Properties["OrderType"]

        for ot in Order.OrderType.all do
            orderType.Enumeration.Add(ot |> Order.OrderType.toString)

        schema


    /// The schema as the text of its file, with line feeds only, so the text is the same on every
    /// platform.
    let toText (schema: JsonSchema) = schema.ToJson().Replace("\r\n", "\n") + "\n"


    /// A number as a plain YAML 1.2 scalar writes it, which JSON reads as a number.
    let plainNumberRegex = String.regex @"^-?(0|[1-9]\d*)(\.\d+)?([eE][-+]?\d+)?$"


    /// A YAML scalar as JSON: a plain true, false or null as such, a plain number as a number,
    /// anything else, and every quoted scalar, as a string.
    let scalarToJson (s: YamlScalarNode) =
        match s.Style, s.Value with
        | ScalarStyle.Plain, "true" -> JValue true :> JToken
        | ScalarStyle.Plain, "false" -> JValue false
        | ScalarStyle.Plain, ("null" | "~" | "") -> JValue.CreateNull()
        | ScalarStyle.Plain, v when plainNumberRegex.IsMatch v -> JToken.Parse v
        | _, v -> JValue v


    /// A YAML node as the JSON an outside YAML tool reads it as.
    let rec nodeToJson (node: YamlNode) : JToken =
        match node with
        | :? YamlScalarNode as s -> s |> scalarToJson
        | :? YamlSequenceNode as xs -> JArray(xs.Children |> Seq.map nodeToJson)
        | :? YamlMappingNode as m ->
            JObject(
                m.Children
                |> Seq.map (fun kv -> JProperty((kv.Key :?> YamlScalarNode).Value, kv.Value |> nodeToJson))
            )
        | _ -> invalidArg (nameof node) $"%s{node.GetType().Name} is no scalar, sequence or mapping"


    /// The JSON of a YAML text, its one document.
    let yamlToJson text =
        let stream = YamlStream()
        stream.Load(new System.IO.StringReader(text))
        stream.Documents[0].RootNode |> nodeToJson


    /// The errors of a text against the schema, by their path and kind.
    let validate (schema: JsonSchema) text =
        text
        |> yamlToJson
        |> schema.Validate
        |> Seq.map (fun e -> $"%s{e.Path}: %A{e.Kind}")
        |> Seq.toList


open MedicationTextTests.MedicationTextYamlTests


/// The committed schema file, copied next to the test assembly.
let committedText =
    lazy
        (System.IO.Path.Combine(System.AppContext.BaseDirectory, "medication.schema.json")
         |> System.IO.File.ReadAllText
         |> String.replace "\r\n" "\n")


let tests =
    let schema = lazy (Schema.generate ())

    testList
        "the schema of the medication text"
        [
            test "the committed schema is the one the Dto generates" {
                committedText.Value
                |> Expect.equal
                    "the same schema; run MedicationSchema.fsx --write after a change of the Dto"
                    (schema.Value |> Schema.toText)
            }

            testList
                "every text validates"
                [
                    for n, med in all do
                        test $"%s{n}" {
                            med
                            |> MedicationText.toString
                            |> Schema.validate schema.Value
                            |> Expect.isEmpty "no schema errors"
                        }

                    test "the valid text of the error tests" {
                        validText |> Schema.validate schema.Value |> Expect.isEmpty "no schema errors"
                    }
                ]

            testList
                "a broken text does not validate"
                [
                    test "a wrong type" {
                        replace "Form: zetpil" "Form: [zetpil]"
                        |> Schema.validate schema.Value
                        |> Expect.isNonEmpty "a schema error"
                    }

                    test "an unknown key inside a nested Dto" {
                        replace "Inclusive: true }" "Inclusive: true, Colour: white }"
                        |> Schema.validate schema.Value
                        |> Expect.isNonEmpty "a schema error"
                    }

                    test "an order type that names no case" {
                        replace "OrderType: DiscontinuousOrder" "OrderType: Banana"
                        |> Schema.validate schema.Value
                        |> Expect.isNonEmpty "a schema error"
                    }

                    test "a number that is no number" {
                        replace "Value: [20]" "Value: [abc]"
                        |> Schema.validate schema.Value
                        |> Expect.isNonEmpty "a schema error"
                    }
                ]

            testList
                "the JSON conversion"
                [
                    test "an Id of digits and the name yes come back as strings" {
                        let json =
                            { Scenarios.pcmSupp with
                                Id = "12345"
                                Name = "yes"
                            }
                            |> MedicationText.toString
                            |> Schema.yamlToJson

                        json["Id"].Type |> Expect.equal "Id is a string" JTokenType.String
                        json["Name"].Type |> Expect.equal "Name is a string" JTokenType.String
                    }

                    test "a value comes back as a string" {
                        let json = Scenarios.pcmSupp |> MedicationText.toString |> Schema.yamlToJson

                        json.SelectToken("Adjust.Value[0]").Type
                        |> Expect.equal "a string" JTokenType.String
                    }
                ]
        ]
