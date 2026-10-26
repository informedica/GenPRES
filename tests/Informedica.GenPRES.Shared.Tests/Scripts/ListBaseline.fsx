// The baseline of the two stand-alone list calculations (#1209): fetches the emergencylist and
// continuousmeds sheets of the emergency-list workbook once, stores them as fixtures, writes the
// tables the tests compare with, and prints which entries take the tenfold dilution, a fixed
// dose, the minimum or maximum, or the morphine quantity from the weight.
//
// Run it by hand, only to renew the baseline; the tests never fetch. The workbook id is a copy
// of the one in the client's Utils.fs, which is Fable code a script cannot load.
//
// Run: dotnet fsi ListBaseline.fsx, from this directory, after a build.

#r "../../../src/Informedica.GenPRES.Shared/bin/Debug/net10.0/Informedica.GenPRES.Shared.dll"
#r "../../../src/Informedica.GenPRES.Client.Core/bin/Debug/net10.0/Informedica.GenPRES.Client.Core.dll"

#load "../EmergencyBaseline.fs"
#load "../../Informedica.GenPRES.Client.Core.Tests/Models/ContinuousBaseline.fs"

open System.IO
open System.Net.Http
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Tests
open Informedica.GenPRES.Client.Core.Tests.Models


/// The emergency-list workbook, a copy of the id the client reads.
let workbook = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


/// The sheet as the client fetches it: comma separated text.
let fetch sheet =
    use client = new HttpClient()

    client.GetStringAsync($"https://docs.google.com/spreadsheets/d/{workbook}/gviz/tq?tqx=out:csv&sheet={sheet}")
    |> Async.AwaitTask
    |> Async.RunSynchronously


/// The bolus entries the calculation runs at a weight: every adrenaline entry, and the other
/// entries whose weight range holds the weight.
let entriesAt weight (bolus: BolusMedication list) =
    bolus
    |> List.filter (fun m ->
        m.Generic = "adrenaline"
        || m.MinWeight <= weight && (weight < m.MaxWeight || m.MaxWeight = 0.)
    )


/// The marks of one entry at one weight, calculated for that entry alone: the tenfold dilution
/// when the concentration used is not the sheet's, and the bound that decided the dose.
let bolusMarks weight (b: BolusMedication) =
    let i = Models.EmergencyTreatment.calcBolusMedication weight b

    [
        if i.Quantity <> Some b.Concentration then
            "tenfold dilution"
        if b.MinDose = b.MaxDose && b.MinDose > 0. then "fixed dose"
        elif b.MaxDose > 0. && weight * b.NormDose > b.MaxDose then "maximum"
        elif b.MinDose > 0. && weight * b.NormDose < b.MinDose then "minimum"
    ]


let sharedFixtures = Path.Combine(__SOURCE_DIRECTORY__, "..", "fixtures")
let coreFixtures = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "Informedica.GenPRES.Client.Core.Tests", "fixtures")

Directory.CreateDirectory sharedFixtures |> ignore
Directory.CreateDirectory coreFixtures |> ignore

let emergency = fetch "emergencylist"
let continuous = fetch "continuousmeds"

File.WriteAllText(Path.Combine(sharedFixtures, "emergencylist.csv"), emergency)
File.WriteAllText(Path.Combine(sharedFixtures, "emergencylist.tsv"), emergency |> Csv.parseCSV |> EmergencyBaseline.tsv)
File.WriteAllText(Path.Combine(coreFixtures, "continuousmeds.csv"), continuous)
File.WriteAllText(Path.Combine(coreFixtures, "continuousmeds.tsv"), continuous |> Csv.parseCSV |> ContinuousBaseline.tsv)

printfn "written: %s, %s" sharedFixtures coreFixtures

let bolus = emergency |> Csv.parseCSV |> Models.EmergencyTreatment.parse

printfn "\nemergency list, by entry: the weights in kg of each mark"

for b in bolus do
    let marked =
        EmergencyBaseline.weights
        |> List.filter (fun w -> bolus |> entriesAt w |> List.contains b)
        |> List.collect (fun w -> bolusMarks w b |> List.map (fun mark -> mark, w))
        |> List.groupBy fst

    for mark, ws in marked do
        printfn "%s\t%s\t%s %s/mL\t%s\t%s" b.Hospital b.Generic (EmergencyBaseline.num b.Concentration) b.Unit mark (ws |> List.map (snd >> EmergencyBaseline.num) |> String.concat ", ")

let meds = continuous |> Csv.parseCSV |> Models.ContinuousMedication.parse

printfn "\ncontinuous list: the morphine quantity taken from the weight"

for m in meds do
    if m.Quantity = 0. && m.Hospital = "Radboud UMC" && m.Medication = "morfine" then
        for w in ContinuousBaseline.weights do
            if m.MinWeight <= w && (w < m.MaxWeight || m.MaxWeight = 0.) then
                printfn "%s\t%s\tweight %s kg\tquantity %s %s" m.Hospital m.Medication (EmergencyBaseline.num w) (w / 2. |> int |> float |> EmergencyBaseline.num) m.Unit
