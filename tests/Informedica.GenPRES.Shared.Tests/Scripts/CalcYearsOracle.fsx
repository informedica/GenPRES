#r "nuget: FsCheck, 2.16.6"

#load "load.fsx"

open System
open System.IO
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open FsCheck

let oracleGen =
    gen {
        let! years = Gen.choose (0, 100)
        let! months = Gen.choose (0, 11)
        let expected = Patient.Age.calcYears (Patient.Age.create (years * 1<year>) (Some(months * 1<month>)) None None)
        return (years, months), expected
    }

let testCases = Gen.sample 0 100 oracleGen
