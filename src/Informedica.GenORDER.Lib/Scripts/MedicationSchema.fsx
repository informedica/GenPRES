/// The schema of the medication text, docs/domain/medication.schema.json, generated from the
/// library's Medication Dto by the test module MedicationSchemaTests.
///
/// Run from this directory, after a build:
///
///   dotnet fsi MedicationSchema.fsx            the tests, and every text literal of the tests
///   dotnet fsi MedicationSchema.fsx --print    the schema
///   dotnet fsi MedicationSchema.fsx --write    the schema written to its file

#I __SOURCE_DIRECTORY__
#load "MedicationYaml.fsx"
#load "../../../tests/Informedica.GenORDER.Tests/MedicationSchemaTests.fs"


open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib
open MedicationYaml
open MedicationSchemaTests


/// The file of the schema, from this directory.
let schemaPath = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "../../../docs/domain/medication.schema.json")


/// The tests of the test project but the one of the committed file, which the script does not
/// copy, and every text literal of the tests.
let tests =
    let schema = lazy (Schema.generate ())

    let withoutTheFile =
        Test.filter "/" (List.contains "the committed schema is the one the Dto generates" >> not)

    testList
        "the schema in the script"
        [
            MedicationSchemaTests.tests |> withoutTheFile

            testList
                "every text literal validates"
                [
                    for n, med in Fixtures.parsedTexts do
                        test $"%s{n}" {
                            med
                            |> MedicationText.toString
                            |> Schema.validate schema.Value
                            |> Expect.isEmpty "no schema errors"
                        }
                ]
        ]


let schemaArgs = fsi.CommandLineArgs |> Array.skip 1

if schemaArgs |> Array.contains "--print" then
    Schema.generate () |> Schema.toText |> printf "%s"
elif schemaArgs |> Array.contains "--write" then
    System.IO.File.WriteAllText(schemaPath, Schema.generate () |> Schema.toText)
    printfn $"written: %s{System.IO.Path.GetFullPath schemaPath}"
// only when started itself, so a script that loads this one does not run its tests
elif System.IO.Path.GetFileName fsi.CommandLineArgs[0] = "MedicationSchema.fsx" then
    tests |> runTestsWithCLIArgs [] [| "--summary" |] |> ignore
