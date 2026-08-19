#r "nuget: FsCheck, 2.16.6"

#load "load.fsx"

open System
open System.IO
open Shared.Models
open FsCheck

let oracleGen =
    gen {
        let! days = Gen.choose (0, 100 * 365)
        let expected = Patient.Age.calcMonths (Patient.Age.fromDays days)
        return days, expected
    }

let testCases = Gen.sample 0 100 oracleGen
