namespace Informedica.GenOrder.Lib


/// The medication as its text, a YAML 1.2 document, and back: the medication's Dto written and
/// read by YamlDotNet.
module MedicationText =

    open YamlDotNet.Core
    open YamlDotNet.Serialization
    open Informedica.GenOrder.Lib.Types


    /// Leaves a null and an empty array out, quotes a string another reader would read as a
    /// number, a boolean or a null, and indents a sequence under its key.
    let serializer =
        SerializerBuilder()
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull ||| DefaultValuesHandling.OmitEmptyCollections
            )
            .WithQuotingNecessaryStrings(true)
            .WithIndentedSequences()
            .Build()


    /// Reads a text into the Dto; an unknown key is an error.
    let deserializer = DeserializerBuilder().Build()


    /// The medication as its text.
    let toString med = med |> Medication.Dto.toDto |> serializer.Serialize


    /// The medication of a text, or every error of it, each with the path of its field. A text
    /// that is no document of this shape is one error with the reader's line and column.
    let fromString (s: string) =
        try
            let dto = s |> deserializer.Deserialize<Medication.Dto.Dto>
            Nested.required "Medication" dto Medication.Dto.fromDto
        with :? YamlException as e ->
            Error [ DtoError.Unreadable e.Message ]
