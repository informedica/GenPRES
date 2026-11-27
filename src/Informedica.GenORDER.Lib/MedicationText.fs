namespace Informedica.GenOrder.Lib


/// Writes a medication as a YAML 1.2 text and reads it back, through the medication Dto and
/// YamlDotNet.
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


    /// Writes the medication as YAML text.
    let toString med = med |> Medication.Dto.toDto |> serializer.Serialize


    /// Reads a medication from a YAML text. Returns every error with the path of its field. A text
    /// that cannot be read returns one error with the line and column from the YAML reader.
    let fromString (s: string) =
        try
            let dto = s |> deserializer.Deserialize<Medication.Dto.Dto>
            Nested.required "Medication" dto Medication.Dto.fromDto
        with :? YamlException as e ->
            Error [ DtoError.Unreadable e.Message ]
