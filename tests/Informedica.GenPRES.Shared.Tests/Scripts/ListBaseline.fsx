// The baseline of the continuous medication list calculation (#1209): fetches the continuousmeds
// sheet of the emergency-list workbook once, stores it as a fixture, writes the table the tests
// compare with, and prints which entries take the morphine quantity from the weight. The server
// calculates the emergency list, so it has no baseline here.
//
// Run it by hand, only to renew the baseline; the tests never fetch. The workbook id is a copy
// of the one in the client's Utils.fs, which is Fable code a script cannot load.
//
// Run: dotnet fsi ListBaseline.fsx, from this directory, after a build.

#r "../../../src/Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"
#r "../../../src/Informedica.GenPRES.Client.Core/bin/Debug/net10.0/Informedica.GenPRES.Client.Core.dll"

#load "../../Informedica.GenPRES.Client.Core.Tests/Models/ContinuousBaseline.fs"

open System.IO
open System.Net.Http
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Client.Core.Tests.Models


/// The emergency-list workbook, a copy of the id the client reads.
let workbook = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


/// The sheet as the client fetches it: comma separated text.
let fetch sheet =
    use client = new HttpClient()

    client.GetStringAsync($"https://docs.google.com/spreadsheets/d/{workbook}/gviz/tq?tqx=out:csv&sheet={sheet}")
    |> Async.AwaitTask
    |> Async.RunSynchronously


let coreFixtures = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "Informedica.GenPRES.Client.Core.Tests", "fixtures")

Directory.CreateDirectory coreFixtures |> ignore

let continuous = fetch "continuousmeds"

File.WriteAllText(Path.Combine(coreFixtures, "continuousmeds.csv"), continuous)
File.WriteAllText(Path.Combine(coreFixtures, "continuousmeds.tsv"), continuous |> Csv.parseCSV |> ContinuousBaseline.tsv)

printfn "written: %s" coreFixtures

let meds = continuous |> Csv.parseCSV |> Models.ContinuousMedication.parse

printfn "\ncontinuous list: the morphine quantity taken from the weight"

for m in meds do
    if m.Quantity = 0. && m.Hospital = "Radboud UMC" && m.Medication = "morfine" then
        for w in ContinuousBaseline.weights do
            if m.MinWeight <= w && (w < m.MaxWeight || m.MaxWeight = 0.) then
                printfn "%s\t%s\tweight %s kg\tquantity %s %s" m.Hospital m.Medication (ContinuousBaseline.num w) (w / 2. |> int |> float |> ContinuousBaseline.num) m.Unit
