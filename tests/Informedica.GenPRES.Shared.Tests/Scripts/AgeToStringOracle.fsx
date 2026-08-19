#r "nuget: FsCheck, 2.16.6"

#load "load.fsx"

open System
open Shared
open Shared.Models
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
        let! age = Gen.choose (0, 100 * 365) |> Gen.map Patient.Age.fromDays

        let expected = Patient.Age.toString terms lang age

        return terms, lang, age, expected
    }

let testCases = Gen.sample 10 100 oracleGen
