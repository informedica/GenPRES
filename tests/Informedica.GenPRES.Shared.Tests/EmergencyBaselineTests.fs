/// The emergency calculation against its stored baseline: the stored rows of the emergencylist
/// sheet, calculated again, give the stored table line for line.
module Informedica.GenPRES.Shared.Tests.EmergencyBaselineTests

open System
open System.IO
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared


/// A fixture file copied to the test output.
let fixture name =
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name))


/// The lines of a text, whatever its line ends.
let lines (s: string) = s.Replace("\r\n", "\n").Split('\n')


[<Tests>]
let tests =
    testList
        "EmergencyBaseline"
        [
            test "the stored sheet gives the stored table" {
                fixture "emergencylist.csv"
                |> Csv.parseCSV
                |> EmergencyBaseline.tsv
                |> lines
                |> Expect.sequenceEqual "the table" (fixture "emergencylist.tsv" |> lines)
            }
        ]
