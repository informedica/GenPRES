namespace Informedica.GenPRES.Client.Core.Models

/// What the client reads from a patient: its age from a birth date, the age in years and
/// the weight in kg the lists are calculated for, and the values the panel's fields show.
module PatientRead =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Patient


    module Age =

        open System
        open Informedica.GenPRES.Shared.Models.Patient.Age


        let fromBirthDate (now: DateTime) (bdt: DateTime) =
            if bdt > now then
                invalidArg (nameof bdt) $"birthdate: {bdt} cannot be after current date: {now}"
            // calculated last birthdate and number of years ago
            let last, yrs =
                // set day one day back if not a leap year, and the birthdate is at Feb 29 in a leap year
                let day =
                    if (bdt.Month = 2 && bdt.Day = 29) |> not then bdt.Day
                    else if DateTime.IsLeapYear(now.Year) then bdt.Day
                    else bdt.Day - 1

                if now.Year - bdt.Year <= 0 then
                    bdt, 0
                else
                    let cur = DateTime(now.Year, bdt.Month, day)

                    if cur <= now then
                        cur, cur.Year - bdt.Year
                    else
                        cur.AddYears(-1), cur.Year - bdt.Year - 1
            // printfn $"last birthdate: {last|> printDate}"
            // calculate the number of months since last birthdate
            let mos =
                [ 1..11 ]
                |> List.fold
                    (fun (mos, n) _ ->
                        let n = n + 1
                        // printfn $"folding: {last.AddMonths(n) |> printDate}, {mos}"
                        if last.AddMonths(n) <= now then mos + 1, n else mos, n
                    )
                    (0, 0)
                |> fst

            let last = last.AddMonths(mos)
            // calculate number of days
            let days =
                if now.Day >= last.Day && now.Month = last.Month then
                    now.Day - last.Day
                else
                    DateTime.DaysInMonth(last.Year, last.Month) - last.Day + now.Day

            create
                (yrs * 1<year>)
                (Some(mos * 1<month>))
                (Some(days / 7 * 1<week>))
                (Some((days - 7 * (days / 7)) * 1<day>))


    let getGAWeeks (p: Patient) = p.GestationalAge |> Option.map _.Weeks


    let getGADays (p: Patient) = p.GestationalAge |> Option.map _.Days


    let getRenalFunction (p: Patient) = p.RenalFunction |> Option.map RenalFunctionText.renalToOption


    let getAgeInYears p =
        [
            p |> getAgeYears |> Option.map float
            p |> getAgeMonths |> Option.map (fun ms -> (ms |> float) / 12.)
            p |> getAgeWeeks |> Option.map (fun ws -> (ws |> float) / 52.)
            p |> getAgeDays |> Option.map (fun ds -> (ds |> float) / 365.)
        ]
        |> List.choose id
        |> function
            | [] -> None
            | xs -> xs |> List.sum |> Some


    let getWeightInKg (pat: Patient) = pat |> getWeight |> Option.map (fun x -> float x / 1000.)
