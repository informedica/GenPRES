#r "nuget: FsCheck, 2.16.6"

#load "load.fsx"
#r "../../../src/Informedica.GenPRES.Client.Core/bin/Debug/net10.0/Informedica.GenPRES.Client.Core.dll"

open System
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Client.Core.Models
open FsCheck

let genAlphaNum = Gen.elements (['a'..'z'] @ ['A'..'Z'] @ ['0'..'9'])

let oracleGen =
    gen {
        let! terms =
            [|
                Terms.``Patient Age day``
                Terms.``Patient Age days``
                Terms.``Patient Age week``
                Terms.``Patient Age weeks``
                Terms.``Patient Age month``
                Terms.``Patient Age months``
                Terms.``Patient Age year``
                Terms.``Patient Age years``
            |]
            |> Gen.collect (fun t ->
                genAlphaNum
                |> Gen.arrayOf
                |> Gen.map String
                |> Gen.listOfLength 6
                |> Gen.map ((fun l -> (string t) :: l) >> List.toArray))
            |> Gen.map List.toArray
        let! lang = Gen.elements Localization.languages
        let! years = Gen.choose (0, 100)
        let! months = Gen.choose (0, 11)
        let! weeks = Gen.choose (0, 4)
        let! days = Gen.choose (0, 6)

        let age =
            Patient.Age.create (years * 1<year>) (Some(months * 1<month>)) (Some(weeks * 1<week>)) (Some(days * 1<day>))

        let expected = PatientText.Age.toString terms lang age

        return terms, lang, (years, months, weeks, days), expected
    }

let testCases = Gen.sample 10 100 oracleGen
