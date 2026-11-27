/// The rows of the emergency list: a calculated intervention as a row, and the rows for a patient.
module Informedica.GenPRES.Server.Tests.EmergencyListCommandTests

open Expecto
open Expecto.Flip
// Opened after Expecto, so that its names win over the same names there.
open Informedica.GenPRES.Shared.Types
open ServerApi
open ServerApi.EmergencyListCommand
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


module CoreConversions = Informedica.GenCore.Lib.Conversions
module Emergency = Informedica.GenCore.Lib.Calculations.Emergency
module GenOrder = Informedica.GenOrder.Lib.Types
module SharedPatient = Informedica.GenPRES.Shared.Models.PatientData


/// Fixtures for the tests: an environment with the standard orders of the emergency list that collects the log.
module Fixtures =

    /// The environment of the tests with the standard orders of the emergency list and a logger that collects the
    /// events.
    let envWith emergencyList =
        let env, events = StandardOrderCommandTests.Fixtures.loggingEnv ()
        { env with emergencyList = emergencyList }, events


[<Tests>]
let tests =
    testList
        "EmergencyListCommand"
        [
            testList
                "ofCalculation"
                [
                    /// The name, the value as text and the advice of each row for an age in days and a weight in kg.
                    let texts age weight =
                        Emergency.calculate
                            (age |> Option.map CoreConversions.dayFromInt)
                            (weight |> Option.map CoreConversions.kgFromDecimal)
                        |> List.map (ofCalculation >> fun r -> r.Name, r.SubstanceDoseText, r.Text)

                    test "maps the six interventions from a weight of 1 kg" {
                        texts (Some 10) (Some 1m)
                        |> Expect.equal
                            "the rows"
                            [
                                "tube maat", "3", "< 1 kg: 2.5, 1-3 kg: 3.0"
                                "tube lengte oraal", "6,63 cm", "6.632 + 1.822 x ln(kg)"
                                "tube lengte nasaal", "8,14 cm", "(45 + 1.15 x \u221A (gram)) / 10"
                                "navel lijn maat", "3,5 French", "< 1.5 kg: 3,5 anders 5 "
                                "navel arterie lijn lengte", "11 cm", "kg x 4 + 7"
                                "navel vene lijn lengte", "7 cm", "kg x 1.5 + 5.5"
                            ]
                    }

                    test "maps the artery line formula for a weight from 1.5 kg" {
                        texts None (Some 2m)
                        |> Expect.contains "the row" ("navel arterie lijn lengte", "14,7 cm", "kg x 2.5 + 9.7")
                    }

                    test "maps the tubes from an age of 10 years and the energies from a weight of 32 kg" {
                        texts (Some 3650) (Some 32m)
                        |> Expect.equal
                            "the rows"
                            [
                                "tube (met cuff) maat", "5,5 - 6 - 6,5", "3,5 + leeftijd / 4"
                                "tube (zonder cuff) maat", "6 - 6,5 - 7", "4 + leeftijd / 4"
                                "tube lengte oraal", "17 cm", "12 + leeftijd / 2"
                                "tube lengte nasaal", "20 cm", "15 + leeftijd / 2"
                                "defibrillatie", "150 joule", "4 joule/kg"
                                "cardioversie", "70 joule", "2 joule/kg"
                            ]
                    }

                    test "gives every row the category reanimatie, the value as a number and no order context" {
                        Emergency.calculate None (32m |> CoreConversions.kgFromDecimal |> Some)
                        |> List.map (ofCalculation >> fun r -> r.Category, r.InterventionDose, r.OrderContext.IsNone)
                        |> Expect.equal "the rows" [ "reanimatie", Some 150., true; "reanimatie", Some 70., true ]
                    }
                ]

            testList
                "processCmd"
                [
                    testAsync "returns the calculated rows, then a row for each standard order, and logs the warnings" {
                        let env, events =
                            Fixtures.envWith (fun _ ->
                                [| StandardOrderCommandTests.Fixtures.standardOrder |],
                                [|
                                    GenOrder.StandardOrderWarning.Failed(
                                        "Noodlijst Reanimatie",
                                        "adrenaline",
                                        "failed"
                                    )
                                |]
                            )

                        match! StubEhrPatient.contractPatient |> processCmd env with
                        | Ok rows ->
                            rows
                            |> Array.map _.Name
                            |> Expect.equal
                                "the rows of the stub patient of 10 years and 32 kg"
                                [|
                                    "tube (met cuff) maat"
                                    "tube (zonder cuff) maat"
                                    "tube lengte oraal"
                                    "tube lengte nasaal"
                                    "defibrillatie"
                                    "cardioversie"
                                    "paracetamol"
                                |]
                        | Error errs -> failtest $"expected the rows, got %A{errs}"

                        events
                        |> StandardOrderCommandTests.Fixtures.texts
                        |> List.filter (fun s -> s.StartsWith "EmergencyList")
                        |> Expect.equal
                            "the warning"
                            [ "EmergencyList: Noodlijst Reanimatie, adrenaline gives no rows: failed" ]
                    }

                    testAsync "returns the messages for invalid patient data" {
                        let env, _ = Fixtures.envWith (fun _ -> [||], [||])

                        match! SharedPatient.empty |> processCmd env with
                        | Ok _ -> failtest "expected the messages, got rows"
                        | Error errs -> errs |> Expect.isNonEmpty "the messages"
                    }
                ]
        ]
