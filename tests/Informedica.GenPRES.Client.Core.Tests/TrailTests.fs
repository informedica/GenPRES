module Informedica.GenPRES.Client.Core.Tests.TrailTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Models
open Shared.Api
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine


let at = DateTime(2026, 9, 30, 10, 41, 7, 311)

/// A patient of 3 years and 14 kg.
let pat =
    { Patient.empty with
        Age =
            Some
                {
                    Years = 3<year>
                    Months = 0<month>
                    Weeks = 0<week>
                    Days = 0<day>
                }
        Weight = { Patient.empty.Weight with Measured = Some 14000<gram> }
    }

let ctx = OrderContext.empty

let ctxPicked =
    { ctx with
        Id = "ctx-1"
        Filter =
            { ctx.Filter with
                Indication = Some "pain"
                Generic = Some "paracetamol"
                Route = Some "oral"
            }
    }

let identity: NameAndBirthDate =
    {
        Name = "Jan Jansen"
        BirthYear = 2023
        BirthMonth = 5
        BirthDay = 17
    }

/// Every value that identifies a person or opens a door, which no line may show.
let secrets =
    [
        "Jan Jansen"
        "2023"
        "1234"
        "code-987"
        "secret-token"
        "Dr. Bakker"
        "j***@hospital.nl"
    ]


[<Tests>]
let tests =
    testList
        "Trail"
        [
            test "a step is one line with its number, time, machine, message, effects and state" {
                let state, effects =
                    OrderContextState.noPatient
                    |> OrderContextState.transition (OrderContextMsg.PatientChanged(Some pat, "r-1"))

                Trail.orderContext 12 at (OrderContextMsg.PatientChanged(Some pat, "r-1")) (state, effects)
                |> Trail.format
                |> Expect.equal
                    "the line"
                    "#12 10:41:07.311 OrderContext PatientChanged patient 3.0 y 14.0 kg no height gender unknown r-1 -> CallContext ClearAllFilterProperty workbench r-1 | Changing workbench awaits r-1"
            }

            test "a seed shows its source and how many choices, never their text" {
                let seed: OrderContextMachine.FilterSeed =
                    {
                        Source = SeedSource.Url
                        Indication = Some "Milde pijn"
                        Generic = Some "paracetamol"
                        Route = Some "oraal"
                        Form = None
                        DoseType = None
                    }

                let held = OrderContextState.held pat (OrderContextState.emptyFor pat)
                let msg = OrderContextMsg.SeedFilter(seed, "r-1")

                Trail.orderContext 3 at msg (held |> OrderContextState.transition msg)
                |> Trail.format
                |> Expect.equal
                    "the line"
                    "#3 10:41:07.311 OrderContext SeedFilter url 3 choices r-1 -> CallContext SeedFilter url 3 choices workbench r-1 | Changing workbench awaits r-1"
            }

            test "a step without effects says none" {
                Trail.format
                    {
                        No = 1
                        At = at
                        Machine = "Signing"
                        Msg = "Cancel"
                        Effects = []
                        State = "Idle"
                    }
                |> Expect.equal "none" "#1 10:41:07.311 Signing Cancel -> none | Idle"
            }

            test "a context shows its id, its picks and its scenarios" {
                ctxPicked
                |> Trail.Part.context
                |> Expect.equal "the context" "ctx-1 pain/paracetamol/oral 0 scenarios"
            }

            test "a patient shows what tells two patients apart, an estimate marked as such" {
                { pat with
                    Weight =
                        { pat.Weight with
                            Measured = None
                            Estimated = Some 15500<gram>
                        }
                    Height = { pat.Height with Measured = Some 98<cm> }
                    Gender = Female
                    Department = Some "ICK"
                    Location = Some "bed 12"
                }
                |> Trail.Part.patient
                |> Expect.equal "the patient" "patient 3.0 y est 15.5 kg 98 cm female"
            }

            test "a seed message shows its source and how many choices, never their text" {
                let seed: OrderContextMachine.FilterSeed =
                    {
                        Source = SeedSource.MedicationList
                        Indication = None
                        Generic = Some "Jan Jansen"
                        Route = None
                        Form = None
                        DoseType = None
                    }

                OrderContextMsg.SeedFilter(seed, "r-4")
                |> Trail.OrderContext.msg
                |> Expect.equal "the count" "SeedFilter list 1 choices r-4"
            }

            test "a line never breaks, whatever text a step carries" {
                Trail.format
                    {
                        No = 1
                        At = at
                        Machine = "OrderContext"
                        Msg = "Argue\r\nsecond line"
                        Effects = [ "Tell\tError" ]
                        State = "Settled\nworkbench"
                    }
                |> Expect.equal
                    "one line"
                    "#1 10:41:07.311 OrderContext Argue  second line -> Tell Error | Settled workbench"
            }

            test "an id shows its first 8 characters" {
                [ "9bab44df-d8a3-4439-9392-42cac1f1dd92"; "r-1"; "" ]
                |> List.map Trail.Part.shortId
                |> Expect.equal "short ids" [ "9bab44df"; "r-1"; "" ]
            }

            test "a dose type without a name shows its kind alone" {
                [ Continuous " "; Continuous ""; Once "stat" ]
                |> List.map Trail.Part.doseType
                |> Expect.equal "the dose types" [ "continuous"; "continuous"; "once stat" ]
            }

            test "a reopen shows the request it awaits and the state it keeps" {
                OrderContextState.held pat ctxPicked
                |> OrderContextState.transition (
                    OrderContextMsg.ReopenField(
                        OrderViewCommand.ClearScheduleProperty(ScheduleProperty.Frequency, [||]),
                        "r-9"
                    )
                )
                |> fst
                |> Trail.OrderContext.state
                |> Expect.equal "awaits and kept" "Changing ctx-1 awaits r-9 kept"
            }

            test "a plan navigation shows the context in a message, and its id once in an effect" {
                let cmd =
                    OrderPlanCommand.Navigate(OrderPlan.empty, "ctx-1", OrderViewCommand.ResetOrderScenario, ctxPicked)

                [
                    Trail.OrderPlan.msg (
                        OrderPlanMsg.Change(
                            OrderPlanChange.OrderDialogCommand("ctx-1", OrderViewCommand.ResetOrderScenario),
                            "r-3"
                        )
                    )
                    Trail.OrderPlan.effect (OrderPlanEffect.CallPlan(cmd, "r-3"))
                ]
                |> Expect.equal
                    "the lines"
                    [
                        "Change OrderDialogCommand ctx-1 ResetOrderScenario r-3"
                        "CallPlan Navigate ctx-1 ResetOrderScenario r-3"
                    ]
            }

            test "a page's change to the plan reads by what it wants" {
                [
                    Trail.OrderPlan.msg (OrderPlanMsg.Change(OrderPlanChange.Add ctxPicked, "r-1"))
                    Trail.OrderPlan.msg (OrderPlanMsg.Change(OrderPlanChange.NewNutrition NutritionCategory.TPN, "r-2"))
                    Trail.OrderPlan.msg (OrderPlanMsg.Change(OrderPlanChange.Remove [| "c-1"; "c-2" |], "r-3"))
                ]
                |> Expect.equal
                    "the lines"
                    [
                        "Change Add ctx-1 pain/paracetamol/oral 0 scenarios r-1"
                        "Change NewNutrition TPN r-2"
                        "Change Remove 2 r-3"
                    ]
            }

            test "a reset by the App reads as a signing step without message or effects" {
                Trail.signingReset 7 at "the session is no longer open" SigningState.idle
                |> Trail.format
                |> Expect.equal "the line" "#7 10:41:07.311 Signing reset: the session is no longer open -> none | Idle"
            }

            test "a lanes step reads as the step of its machine, the reset included" {
                Trail.lanes 7 at (Lanes.LanesStep.SigningReset SigningState.idle)
                |> Trail.format
                |> Expect.equal "the line" "#7 10:41:07.311 Signing reset: the session is no longer open -> none | Idle"

                let session = SessionState.anonymous

                Trail.lanes 3 at (Lanes.LanesStep.Session(SessionMsg.Resume, session, []))
                |> Expect.equal "the session's step" (Trail.session 3 at SessionMsg.Resume (session, []))
            }

            test "the trail keeps the newest lines, oldest first" {
                [ "a"; "b"; "c" ]
                |> List.fold (fun lines line -> Trail.append 2 line lines) []
                |> Expect.equal "the newest two" [ "b"; "c" ]
            }

            testList
                "no line shows an identity, a PIN, a code, a token or a free text"
                [
                    let token = Some(OpenedToken "secret-token")

                    let signed =
                        { SessionMachineTests.signedVersion with
                            Patient = pat
                            Identity = Some identity
                        }

                    let user: UserContext =
                        {
                            UserId = "u-1"
                            DisplayName = "Dr. Bakker"
                            Role = UserRole.Prescriber
                        }

                    let opened: SessionOpened =
                        {
                            User = Some user
                            PatientContext =
                                Some
                                    {
                                        PatientId = "1234"
                                        Identity = Some identity
                                        Patient = Some pat
                                    }
                            OpenedToken = token
                            KeyThumbprint = Some "thumb"
                            Head = None
                        }

                    let lines =
                        [
                            Trail.Session.msg (SessionMsg.SupplyPin("code-987", "1234"))
                            Trail.Session.msg (
                                SessionMsg.SignatureRenewedToken(OpenedToken "secret-token", pat, signed)
                            )
                            Trail.Session.msg (SessionMsg.PinAnswered(Ok(PinOutcome.Opened opened)))
                            Trail.Session.effect (SessionEffect.CallSupplyPin("code-987", "1234"))
                            Trail.Session.view (
                                SessionView.Enrolling(
                                    {
                                        DisplayName = "Jan Jansen"
                                        MailHint = "j***@hospital.nl"
                                    },
                                    None
                                )
                            )
                            Trail.Signing.msg (SigningMsg.ConfirmPin("1234", "key-1"))
                            Trail.Signing.effect (
                                SigningEffect.CallSubmit(OrderPlan.empty, "secret-token", "1234", "key-1")
                            )
                            Trail.Signing.effect (
                                SigningEffect.RenewSessionToken(OpenedToken "secret-token", pat, signed)
                            )
                            Trail.OrderContext.msg (
                                OrderContextMsg.Command(
                                    OrderViewCommand.SetArgumentationProperty "Jan Jansen weighs more",
                                    "r-1"
                                )
                            )
                            Trail.OrderPlan.msg (
                                OrderPlanMsg.Change(
                                    OrderPlanChange.OrderDialogCommand(
                                        "ctx-1",
                                        OrderViewCommand.SetArgumentationProperty "Jan Jansen weighs more"
                                    ),
                                    "r-1"
                                )
                            )
                        ]

                    for line in lines do
                        test line {
                            for secret in secrets do
                                line.Contains secret |> Expect.isFalse $"no %s{secret}"
                        }
                ]

            test "every machine names itself" {
                let plan = OrderPlanState.noPatient
                let signing = SigningState.idle
                let session = SessionState.anonymous

                [
                    Trail.orderPlan 1 at OrderPlanMsg.RestoreField (plan, []) |> _.Machine
                    Trail.signing 2 at SigningMsg.Cancel (signing, []) |> _.Machine
                    Trail.session 3 at SessionMsg.Resume (session, []) |> _.Machine
                ]
                |> Expect.equal "the machines" [ "OrderPlan"; "Signing"; "Session" ]
            }
        ]


[<Tests>]
let exampleTests =
    test "a patient, an answer, a pick and a refusal read as four lines" {
        [
            OrderContextMsg.PatientChanged(Some pat, "r-1")
            OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated ctx))
            OrderContextMsg.Command(OrderViewCommand.SetNthFilterProperty(OrderContext.Generic, 0), "r-2")
            OrderContextMsg.Answered("r-2", Ok(OrderContextResponse.Refused(ctxPicked, OrderContextRefusal.NoProducts)))
        ]
        |> List.mapFold
            (fun (no, state) msg ->
                let state', effects = OrderContextState.transition msg state
                let line =
                    Trail.orderContext no (at.AddSeconds(float no)) msg (state', effects)
                    |> Trail.format
                line, (no + 1, state')
            )
            (1, OrderContextState.noPatient)
        |> fst
        |> Expect.equal
            "the lines"
            [
                "#1 10:41:08.311 OrderContext PatientChanged patient 3.0 y 14.0 kg no height gender unknown r-1 -> CallContext ClearAllFilterProperty workbench r-1 | Changing workbench awaits r-1"
                "#2 10:41:09.311 OrderContext Answered r-1 Ok Evaluated workbench no picks 0 scenarios -> SyncPages | Settled workbench"
                "#3 10:41:10.311 OrderContext Command SetNthFilterProperty Generic nth=0 r-2 -> CallContext SetNthFilterProperty Generic nth=0 workbench r-2 | Changing workbench awaits r-2"
                "#4 10:41:11.311 OrderContext Answered r-2 Ok Refused ctx-1 pain/paracetamol/oral 0 scenarios NoProducts -> SyncPages | Refused ctx-1 NoProducts"
            ]
    }


/// A scenario on the line: the plan fixtures' scenario with its component.
module ScenarioLine =

    open Informedica.GenPRES.Client.Core.Tests.OrderPlanMachineTests.Fixtures

    let one = { scenario "order-0001-abcd" "paracetamol" with Component = Some "paracetamol" }

    let ctxOne =
        { OrderContext.empty with
            Id = "ctx-1"
            Filter = { OrderContext.empty.Filter with Generic = Some "paracetamol" }
            Scenarios = [| one |]
        }


    [<Tests>]
    let tests =
        testList
            "Trail scenario"
            [
                test "a scenario shows its order id and its component" {
                    Trail.Part.scenario one
                    |> Expect.equal "the scenario" "order-00 cmp paracetamol"
                }

                test "a scenario without a component says none" {
                    { one with Component = None }
                    |> Trail.Part.scenario
                    |> Expect.equal "none" "order-00 cmp none"
                }

                test "a context with one scenario shows it" {
                    Trail.Part.context ctxOne
                    |> Expect.equal "the context" "ctx-1 paracetamol 1 scenario order-00 cmp paracetamol"
                }

                test "a context with several scenarios shows their count" {
                    { ctxOne with Scenarios = [| one; one |] }
                    |> Trail.Part.context
                    |> Expect.equal "the count" "ctx-1 paracetamol 2 scenarios"
                }

                test "a context without scenarios reads as before" {
                    { ctxOne with Scenarios = [||] }
                    |> Trail.Part.context
                    |> Expect.equal "as before" "ctx-1 paracetamol 0 scenarios"
                }

                test "a scenario's texts never show" {
                    { one with Prescription = [| [| TextBlock.Valid [| TextItem.Normal "Jan Jansen" |] |] |] }
                    |> Trail.Part.scenario
                    |> _.Contains("Jan Jansen")
                    |> Expect.isFalse "no text"
                }
            ]
