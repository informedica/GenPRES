/// The code the standard order lists share: the warning for a rule that gives no rows, and a standard order as a row.
module Informedica.GenPRES.Server.Tests.StandardOrderCommandTests

open Expecto
open Expecto.Flip
// Opened after Expecto, so that its names win over the same names there.
open Informedica.GenPRES.Shared.Types
open ServerApi
open ServerApi.StandardOrderCommand
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


module GenOrder = Informedica.GenOrder.Lib.Types
module GenOrderContext = Informedica.GenOrder.Lib.OrderContext
module SharedFormulary = Informedica.GenPRES.Shared.Models.Formulary

type LogEvent = Informedica.Logging.Lib.Event
type Logger = Informedica.Logging.Lib.Logger


/// Fixtures for the tests: a standard order of a paracetamol suppository, and an environment that collects the log.
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
            Texts =
                GenOrder.StandardOrderTexts.Bolus
                    {
                        SubstanceDose = "0 - 100 mg"
                        Preparation = "1 zetpil van 100 mg/stuk"
                        Advice = "max 100 mg"
                    }
        }


    /// The environment of the tests with a logger that collects the events, and the events.
    let loggingEnv () =
        let events = ResizeArray<LogEvent>()

        let logger: Logger =
            {
                Log = events.Add
                Enabled = fun _ -> true
            }

        let env = makeEnv (formularyAlwaysOk SharedFormulary.empty) (orderContextAlwaysFails [| "not used" |])

        { env with logger = logger }, events


    /// The texts of the logged events.
    let texts (events: ResizeArray<LogEvent>) =
        events
        |> Seq.map (_.Message >> Logging.ServerLogging.formatMessage)
        |> Seq.toList


[<Tests>]
let tests =
    testList
        "StandardOrderCommand"
        [
            testList
                "warning"
                [
                    let textOf = warning "EmergencyList" >> Logging.ServerLogging.formatMessage

                    test "names the list" {
                        GenOrder.StandardOrderWarning.Failed("Standaard infusie Sedatie", "midazolam", "failed")
                        |> warning "ContinuousList"
                        |> Logging.ServerLogging.formatMessage
                        |> Expect.equal
                            "the text"
                            "ContinuousList: Standaard infusie Sedatie, midazolam gives no rows: failed"
                    }

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

                    test "puts the texts of an infusion in the quantity, the solution, the dose and the text" {
                        let infusion =
                            { Fixtures.standardOrder with
                                Texts =
                                    GenOrder.StandardOrderTexts.Infusion
                                        {
                                            Quantity = "10 mg"
                                            Solution = "50 mL gluc 10%"
                                            Dose = "0,7 - 2,8 mL/uur = 10 - 40 microg/kg/uur"
                                            Advice = "10 - 40 microg/kg/uur"
                                        }
                            }
                            |> toIntervention false

                        (infusion.QuantityUnit, infusion.Solution, infusion.SubstanceDoseText, infusion.Text)
                        |> Expect.equal
                            "the texts"
                            ("10 mg",
                             "50 mL gluc 10%",
                             "0,7 - 2,8 mL/uur = 10 - 40 microg/kg/uur",
                             "10 - 40 microg/kg/uur")
                    }

                    test "carries the order context of the standard order as a medication" {
                        row.OrderContext
                        |> Option.map (fun ctx -> ctx.Category, ctx.Filter.Generic)
                        |> Expect.equal "the context" (Some(OrderCategory.Medication, Some "paracetamol"))
                    }
                ]
        ]
