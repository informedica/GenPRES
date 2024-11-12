// this first to FSI
#load "../Types.fs"
#load "../Localization.fs"
#load "../Data.fs"
#load "../Domain.fs"

open System
open System.IO
open System.Net
open Shared



printfn $"{Terms.``Continuous Medication Advice``}"





let createUrl sheet id =
    $"https://docs.google.com/spreadsheets/d/{id}/gviz/tq?tqx=out:csv&sheet={sheet}"

//https://docs.google.com/spreadsheets/d/1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM/edit?usp=sharing
[<Literal>]
let dataUrlId = "1IbIdRUJSovg3hf8E5V-ZydMidlF_iG552vK5NotZLuM"


let download url =
    async {
        let req = WebRequest.Create(Uri(url))
        use! resp = req.AsyncGetResponse()
        use stream = resp.GetResponseStream()
        use reader = new StreamReader(stream)
        return reader.ReadToEnd()
    }



createUrl "emergencylist" dataUrlId
|> download
|> Async.RunSynchronously
|> Csv.parseCSV
|> EmergencyTreatment.parse
|> EmergencyTreatment.calculate (Some 1.) (Some 10.)
|> List.length

createUrl "continuousmeds" dataUrlId
|> download
|> Async.RunSynchronously
|> Csv.parseCSV
|> ContinuousMedication.parse
|> ContinuousMedication.calculate 10.


let a = 12.
NormalValues.ageWeight
|> List.rev
|> List.tryFind (fun (n, _) -> n <= a)

List.scan