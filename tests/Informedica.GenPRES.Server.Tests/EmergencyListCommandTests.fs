/// The medication rows of the emergency list: the warning for a rule that gives no rows, a standard order as a row,
/// and the rows for a patient.
module Informedica.GenPRES.Server.Tests.EmergencyListCommandTests

open Expecto
open Expecto.Flip
// Opened after Expecto, so that its names win over the same names there.
open Informedica.GenPRES.Shared.Types
open ServerApi
open ServerApi.EmergencyListCommand
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


module GenOrder = Informedica.GenOrder.Lib.Types
module GenOrderContext = Informedica.GenOrder.Lib.OrderContext
module SharedFormulary = Informedica.GenPRES.Shared.Models.Formulary
module SharedPatient = Informedica.GenPRES.Shared.Models.PatientData

type LogEvent = Informedica.Logging.Lib.Event
type Logger = Informedica.Logging.Lib.Logger


/// Fixtures for the tests: a standard order of a paracetamol suppository and an environment that collects the log.
module Fixtures =

    let standardOrder: GenOrder.StandardOrder =
        {
            Context =
                {
                    Filter = { GenOrderContext.emptyFilter with Generic = Some "paracetamol" }
                    PatientData = OrderContextRuleTests.FeedingFixtures.patient
                    Scenarios = [||]
                    Argumentation = None
                }
            Category = "Reanimatie"
            Generic = "paracetamol"
            SubstanceDose = "0 - 100 mg"
            Preparation = "1 zetpil van 100 mg/stuk"
            Advice = "max 100 mg"
        }


    /// The environment of the tests with the standard orders of the emergency list and a logger that collects the
    /// events.
    let envWith emergencyList =
        let events = ResizeArray<LogEvent>()

        let logger: Logger =
            {
                Log = events.Add
                Enabled = fun _ -> true
            }

        let env = makeEnv (formularyAlwaysOk SharedFormulary.empty) (orderContextAlwaysFails [| "not used" |])

        { env with
            logger = logger
            emergencyList = emergencyList
        },
        events


    /// The texts of the logged events.
    let texts (events: ResizeArray<LogEvent>) =
        events
        |> Seq.map (_.Message >> Logging.ServerLogging.formatMessage)
        |> Seq.toList


[<Tests>]
let tests =
    testList
        "EmergencyListCommand"
        [
            testList
                "warning"
                [
                    let textOf = warning >> Logging.ServerLogging.formatMessage

                    test "says that the evaluation gives no scenarios and leaves no choice open" {
                        GenOrder.StandardOrderWarning.NoScenarios("Noodlijst Reanimatie", "paracetamol")
                        |> textOf
                        |> Expect.equal
                            "the text"
                            "EmergencyList: Noodlijst Reanimatie, paracetamol gives no rows: no scenarios and no open choice"
                    }

                    test "says that the evaluation gives no scenarios after four choices" {
                        GenOrder.StandardOrderWarning.TooManyChoices("Noodlijst Reanimatie", "")
                        |> textOf
                        |> Expect.equal
                            "the text"
                            "EmergencyList: Noodlijst Reanimatie,  gives no rows: no scenarios after four choices"
                    }

                    test "says that the rules refuse the order context, with the refusal" {
                        GenOrder.StandardOrderWarning.Refused(
                            "Noodlijst Reanimatie",
                            "paracetamol",
                            GenOrder.Refusal.NoDoseRules
                        )
                        |> textOf
                        |> Expect.equal
                            "the text"
                            "EmergencyList: Noodlijst Reanimatie, paracetamol gives no rows: refused, NoDoseRules"
                    }

                    test "says that the evaluation failed, with the messages" {
                        GenOrder.StandardOrderWarning.Failed("Noodlijst Reanimatie", "paracetamol", "failed")
                        |> textOf
                        |> Expect.equal
                            "the text"
                            "EmergencyList: Noodlijst Reanimatie, paracetamol gives no rows: failed"
                    }
                ]

            testList
                "toIntervention"
                [
                    let row = Fixtures.standardOrder |> toIntervention false

                    test "takes the category and the generic of the standard order" {
                        (row.Category, row.Name)
                        |> Expect.equal "category and name" ("Reanimatie", "paracetamol")
                    }

                    test "puts the substance dose and the preparation in the text fields" {
                        (row.SubstanceDoseText, row.InterventionDoseText)
                        |> Expect.equal "the texts" ("0 - 100 mg", "1 zetpil van 100 mg/stuk")
                    }

                    test "puts the advice in the text" { row.Text |> Expect.equal "the advice" "max 100 mg" }

                    test "carries the order context of the standard order as a medication" {
                        row.OrderContext
                        |> Option.map (fun ctx -> ctx.Category, ctx.Filter.Generic)
                        |> Expect.equal "the context" (Some(OrderCategory.Medication, Some "paracetamol"))
                    }
                ]

            testList
                "processCmd"
                [
                    testAsync "returns a row for each standard order and logs a warning for each rule without rows" {
                        let env, events =
                            Fixtures.envWith (fun _ ->
                                [| Fixtures.standardOrder |],
                                [|
                                    GenOrder.StandardOrderWarning.Failed(
                                        "Noodlijst Reanimatie",
                                        "adrenaline",
                                        "failed"
                                    )
                                |]
                            )

                        match! StubEhrPatient.contractPatient |> processCmd env with
                        | Ok rows -> rows |> Array.map _.Name |> Expect.equal "one row" [| "paracetamol" |]
                        | Error errs -> failtest $"expected the rows, got %A{errs}"

                        events
                        |> Fixtures.texts
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
