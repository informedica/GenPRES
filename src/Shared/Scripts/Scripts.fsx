// this first to FSI
#load "../Types.fs"
#load "../Localization.fs"
#load "../Data.fs"
#load "../Domain.fs"

open System
open System.IO
open System.Net
open Shared

Patient.create None (Some 56) None None None None
|> Option.map (Patient.toString Localization.English false)
|> Option.defaultValue ""



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


open Localization

getTerm Dutch Terms.``Patient enter patient data``


open Types.Patient
open Patient.Age


let printDate (dt: DateTime) = $"""{dt.ToString("dd-MMM-yyyy")}"""


let fromBirthDate (now: DateTime) (bdt: DateTime) =
    if bdt > now then
        failwith $"birthdate: {bdt} cannot be after current date: {now}"
    // set day one day back if not a leap year and birthdate is at Feb 29 in a leap year
    let day =
        if (bdt.Month = 2 && bdt.Day = 29) |> not then
            bdt.Day
        else if DateTime.IsLeapYear(now.Year) then
            bdt.Day
        else
            bdt.Day - 1
    // calculated last birthdate and number of years ago
    let last, yrs =
        if now.Year - bdt.Year <= 0 then
            bdt, 0
        else
            let cur = DateTime(now.Year, bdt.Month, day)

            if cur <= now then
                cur, cur.Year - bdt.Year
            else
                cur.AddYears(-1), cur.Year - bdt.Year - 1

    printfn $"last birthdate: {last |> printDate}"
    // calculate number of months since last birth date
    let mos =
        [ 1..11 ]
        |> List.fold
            (fun (mos, n) _ ->
                let n = n + 1
                printfn $"folding: {last.AddMonths(n) |> printDate}, {mos}"

                if last.AddMonths(n) <= now then
                    mos + 1, n
                else
                    mos, n)
            (0, 0)
        |> fst
    // calculate number of days
    let days =
        if now.Day >= day then
            now.Day - day
        else
            DateTime.DaysInMonth(now.Year, last.AddMonths(mos).Month)
            - day
            + now.Day

    create yrs (Some mos) (Some(days / 7)) (Some(days - 7 * (days / 7)))


DateTime(2005, 2, 19)
|> fromBirthDate (DateTime(2022, 4, 8))
|> Patient.Age.toString Localization.Dutch