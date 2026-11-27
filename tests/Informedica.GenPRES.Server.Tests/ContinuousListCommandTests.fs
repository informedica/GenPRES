/// The rows of the continuous medication list for a patient.
module Informedica.GenPRES.Server.Tests.ContinuousListCommandTests

open Expecto
open Expecto.Flip
// Opened after Expecto, so that its names win over the same names there.
open Informedica.GenPRES.Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


module GenOrder = Informedica.GenOrder.Lib.Types
module SharedPatient = Informedica.GenPRES.Shared.Models.PatientData


/// Fixtures for the tests: a standard infusion of morphine and an environment that collects the log.
module Fixtures =

    let standardOrder: GenOrder.StandardOrder =
        { StandardOrderCommandTests.Fixtures.standardOrder with
            Category = "Pijnstilling"
            Generic = "morfine"
            Texts =
                GenOrder.StandardOrderTexts.Infusion
                    {
                        Quantity = "10 mg"
                        Solution = "50 mL gluc 10%"
                        Dose = "0,7 - 2,8 mL/uur = 10 - 40 microg/kg/uur"
                        Advice = "10 - 40 microg/kg/uur"
                    }
        }


    /// The environment of the tests with the standard orders of the continuous medication list and a logger that
    /// collects the events.
    let envWith standardInfusions =
        let env, events = StandardOrderCommandTests.Fixtures.loggingEnv ()
        { env with standardInfusions = standardInfusions }, events


[<Tests>]
let tests =
    testList
        "ContinuousListCommand"
        [
            testList
                "processCmd"
                [
                    testAsync "returns a row for each standard order and logs the warnings" {
                        let env, events =
                            Fixtures.envWith (fun _ ->
                                [| Fixtures.standardOrder |],
                                [|
                                    GenOrder.StandardOrderWarning.Failed(
                                        "Standaard infusie Sedatie",
                                        "midazolam",
                                        "failed"
                                    )
                                |]
                            )

                        match! StubEhrPatient.contractPatient |> ContinuousListCommand.processCmd env with
                        | Ok rows ->
                            rows
                            |> Array.map (fun r -> r.Name, r.QuantityUnit, r.Solution)
                            |> Expect.equal "the morphine row" [| "morfine", "10 mg", "50 mL gluc 10%" |]
                        | Error errs -> failtest $"expected the rows, got %A{errs}"

                        events
                        |> StandardOrderCommandTests.Fixtures.texts
                        |> List.filter (fun s -> s.StartsWith "ContinuousList")
                        |> Expect.equal
                            "the warning"
                            [ "ContinuousList: Standaard infusie Sedatie, midazolam gives no rows: failed" ]
                    }

                    testAsync "returns the messages for patient data the server does not accept" {
                        let env, _ = Fixtures.envWith (fun _ -> [| Fixtures.standardOrder |], [||])

                        match! SharedPatient.empty |> ContinuousListCommand.processCmd env with
                        | Ok rows -> failtest $"expected the messages, got %A{rows}"
                        | Error errs -> errs |> Expect.isNonEmpty "the messages"
                    }
                ]
        ]
