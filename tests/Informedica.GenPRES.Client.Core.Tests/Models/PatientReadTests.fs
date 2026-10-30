/// An age taken from a birth date: an adult from the 18th birthday, and the mark as an adult from the
/// 16th, by the calendar. The age is split in years by the calendar first, so the days the rules match
/// on reach 18 years of 365 days on the birthday itself.
module Informedica.GenPRES.Client.Core.Tests.Models.PatientReadTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models


let now = DateTime(2026, 10, 30)


/// The patient of a birth date at a moment, marked as an adult or not.
let bornOn at marked (bd: DateTime) =
    { Patient.empty with
        Age = Some(PatientRead.Age.fromBirthDate at bd)
        MarkedAdult = marked
    }


[<Tests>]
let tests =
    testList
        "PatientRead age from a birth date"
        [
            test "born 18 years ago today is an adult" {
                now.AddYears(-18)
                |> bornOn now false
                |> Patient.isAdult
                |> Expect.isTrue "adult on the 18th birthday"
            }

            test "born 18 years ago tomorrow is a child" {
                now.AddYears(-18).AddDays(1.)
                |> bornOn now false
                |> Patient.isAdult
                |> Expect.isFalse "a child the day before the 18th birthday"
            }

            test "born 16 years ago today may be marked as an adult" {
                now.AddYears(-16)
                |> bornOn now true
                |> Patient.validate
                |> Result.isOk
                |> Expect.isTrue "the mark is valid on the 16th birthday"
            }

            test "born 16 years ago tomorrow may not be marked as an adult" {
                now.AddYears(-16).AddDays(1.)
                |> bornOn now true
                |> Patient.validate
                |> Expect.equal "the mark is refused the day before" (Error PatientError.MarkedAdultBelowSixteen)
            }

            test "born on 29 February is 18 years on 28 February of a year that is not a leap year" {
                [ DateTime(2026, 2, 27); DateTime(2026, 2, 28); DateTime(2026, 3, 1) ]
                |> List.map (fun at -> DateTime(2008, 2, 29) |> bornOn at false |> Patient.isAdult)
                |> Expect.equal "a child on 27 February, an adult from 28 February" [ false; true; true ]
            }

            test "every birth date over four years is an adult exactly from its 18th birthday" {
                // the adult test counts days; the years of an age from a birth date are counted by
                // the calendar and the rest holds at most 11 months and 30 days, so the days agree
                // with the date for every birth date, also at the end of a month and around 29 February
                [ 0 .. 4 * 366 ]
                |> List.map (fun d -> now.AddYears(-20).AddDays(float d))
                |> List.filter (fun bd -> (bd.AddYears 18 <= now) <> (bd |> bornOn now false |> Patient.isAdult))
                |> Expect.isEmpty "no birth date where the date and the adult test disagree"
            }
        ]
