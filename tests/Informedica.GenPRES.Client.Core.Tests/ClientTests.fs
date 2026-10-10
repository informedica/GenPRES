/// The client as one transition: use cases played through it, as the lanes' tests play theirs.
module Informedica.GenPRES.Client.Core.Tests.ClientTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Models
open Shared.Api
open Lanes
open PatientMachine
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open LoaderMachine
open AdminMachine
open ShellMachine
open Client
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures


/// Messages played one after another over the state, with fresh request ids; the state, the effects
/// and the steps of the last.
let play msgs state =
    let newId = LanesTests.counter ()
    msgs
    |> List.fold (fun (state, _, _) msg -> state |> transition newId msg) (state, [], [])


/// The state after a page load of this url.
let loaded url = initial url |> pageLoad (LanesTests.counter ()) [ "p" ] url


let stateOf (state, _, _) = state


let answered reply =
    Ok
        {
            Response = reply
            Notice = None
        }


let landed landing = ClientMsg.Loader(LoaderMsg.Landed landing)


/// Every load the page load asks for, landed.
let everyLoad =
    [
        landed (Landing.Settings(Ok(ShellMachineTests.settings Shared.Localization.Dutch)))
        landed (Landing.Localization(Ok [||]))
        landed (
            Landing.NormalValues(
                Ok
                    {
                        Weights = []
                        Heights = []
                        NeoWeights = []
                        NeoHeights = []
                    }
            )
        )
        landed (Landing.BolusMedication(Ok [ LoaderMachineTests.bolus "UMCU" ]))
        landed (Landing.ContinuousMedication(Ok []))
        landed (Landing.Products(Ok []))
        landed (Landing.Formulary(None, answered Formulary.empty))
        landed (Landing.Parenteralia(None, answered Parenteralia.empty))
    ]


let hasStep check (_, _, steps) = steps |> List.exists check


let hasEffect check (_, effects, _) = effects |> List.exists check


/// The emergency list loaded, with an item for adrenaline.
let withList state =
    state
    |> play [ landed (Landing.BolusMedication(Ok [ LoaderMachineTests.bolus "UMCU" ])) ]
    |> stateOf


let adrenaline = "list.UMCU.cat.adrenaline"


let seededFrom source =
    hasStep (
        function
        | ClientStep.Lanes(LanesStep.Workbench(OrderContextMsg.SeedFilter(seed, _), _, _)) -> seed.Source = source
        | _ -> false
    )


/// A Session open for a patient the EHR identified, with a plan that has a new order: the patient
/// is held.
let heldState =
    { initial Url.none with
        Lanes =
            { Lanes.initial (Some draft) with
                Session = SessionState.opened HeldPanelPolicyTests.identified None
                OrderPlan = OrderPlanState.held patient one None |> OrderPlanState.withOpened [||]
            }
    }


[<Tests>]
let tests =
    testList
        "Client"
        [
            testList
                "the page load"
                [
                    test "a url patient leaves the Session, is sent, and the loads are asked for" {
                        let result = loaded { Url.none with Patient = Some draft }

                        result
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(p, _))) -> p = draft
                            | _ -> false
                        )
                        |> Expect.isTrue "the patient sent"

                        result
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Session SessionEffect.CallResume) -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "no resume"

                        result
                        |> hasEffect ((=) (ClientEffect.Loader LoaderEffect.FetchSettings))
                        |> Expect.isTrue "the settings asked for"
                    }

                    test "without a patient the Session is resumed" {
                        loaded Url.none
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Session SessionEffect.CallResume) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "resumed"
                    }

                    test "the start-up ends once the Session and every load have answered" {
                        let state =
                            loaded Url.none
                            |> stateOf
                            |> play (
                                ClientMsg.Lanes(LanesMsg.Session(SessionMsg.Resumed(Ok ResumeResult.NotFound)))
                                :: everyLoad
                            )

                        state
                        |> hasStep (
                            function
                            | ClientStep.Shell(ShellMsg.StartupEnded, _, _) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "ended at the last landing"

                        (stateOf state |> startup, (stateOf state).Shell.Language.Current)
                        |> Expect.equal
                            "started, the settings' language"
                            (StartupPolicy.Startup.Started, Shared.Localization.Dutch)
                    }
                ]

            testList
                "the pages and the workbench"
                [
                    test "a formulary change seeds the workbench, and the workbench's answer syncs the page" {
                        let state =
                            { initial Url.none with
                                Lanes =
                                    { Lanes.initial (Some draft) with
                                        OrderContext =
                                            OrderContextState.held patient (OrderContextState.emptyFor patient)
                                    }
                            }
                            |> play
                                [
                                    ClientMsg.Loader(LoaderMsg.PatientSet(Some patient))
                                    landed (Landing.Formulary(None, answered Formulary.empty))
                                    landed (Landing.Parenteralia(None, answered Parenteralia.empty))
                                ]
                            |> stateOf

                        let form = { Formulary.empty with Generic = Some "paracetamol" }

                        state
                        |> play [ ClientMsg.Loader(LoaderMsg.FormularyChanged form) ]
                        |> fun seeded ->
                            seeded |> seededFrom SeedSource.Formulary |> Expect.isTrue "seeded"

                            // the workbench answers the seed with its filter, which the formulary page follows
                            let _, effects, _ = seeded

                            let request =
                                effects
                                |> List.pick (
                                    function
                                    | ClientEffect.Lanes(LanesEffect.Workbench(OrderContextEffect.CallContext(_, r))) ->
                                        Some r
                                    | _ -> None
                                )

                            let ctx =
                                { OrderContextState.emptyFor patient with
                                    Filter =
                                        { OrderContext.empty.Filter with
                                            Generic = Some "paracetamol"
                                            Indication = Some "pijn"
                                        }
                                }

                            stateOf seeded
                            |> play
                                [
                                    landed (Landing.Formulary(None, answered Formulary.empty))
                                    ClientMsg.Lanes(
                                        LanesMsg.Workbench(
                                            OrderContextMsg.Answered(request, Ok(OrderContextResponse.Evaluated ctx))
                                        )
                                    )
                                ]
                            |> hasEffect (
                                function
                                | ClientEffect.Loader(LoaderEffect.FetchFormulary f) -> f.Indication = Some "pijn"
                                | _ -> false
                            )
                            |> Expect.isTrue "the page asked for the workbench's filter"
                    }

                    test "a reload without a patient asks for both pages" {
                        initial Url.none
                        |> play
                            [
                                ClientMsg.Admin(AdminMsg.Landed(Landing.Reload(Ok AdminResponse.ResourcesReloaded)))
                            ]
                        |> fun (_, effects, _) ->
                            effects
                            |> List.choose (
                                function
                                | ClientEffect.Loader(LoaderEffect.FetchFormulary _) -> Some "formulary"
                                | ClientEffect.Loader(LoaderEffect.FetchParenteralia _) -> Some "parenteralia"
                                | _ -> None
                            )
                        |> Expect.equal "both pages" [ "formulary"; "parenteralia" ]
                    }

                    test "a reload with a patient seeds the workbench again" {
                        initial Url.none
                        |> play
                            [
                                ClientMsg.Loader(LoaderMsg.PatientSet(Some patient))
                                ClientMsg.Admin(AdminMsg.Landed(Landing.Reload(Ok AdminResponse.ResourcesReloaded)))
                            ]
                        |> seededFrom SeedSource.Reload
                        |> Expect.isTrue "seeded again"
                    }
                ]

            testList
                "a list item"
                [
                    test "with a patient: the Prescribe page and the seed" {
                        let result =
                            initial { Url.none with Patient = Some draft }
                            |> withList
                            |> play [ ClientMsg.EmergencyListItemChosen adrenaline ]

                        (stateOf result).Shell.Page |> Expect.equal "Prescribe" Page.Page.Prescribe
                        result |> seededFrom SeedSource.MedicationList |> Expect.isTrue "seeded"
                    }

                    test "without a patient: dropped and said" {
                        let result =
                            initial Url.none
                            |> withList
                            |> play [ ClientMsg.EmergencyListItemChosen adrenaline ]

                        ((stateOf result).Shell.Snackbar, result |> seededFrom SeedSource.MedicationList)
                        |> Expect.equal "said, not seeded" (Some Alert.Alert.NoPatientForMedication, false)
                    }

                    test "an item the list does not hold changes nothing" {
                        let state = initial { Url.none with Patient = Some draft } |> withList
                        let other = ClientMsg.EmergencyListItemChosen "list.UMCU.cat.other"

                        state
                        |> play [ other ]
                        |> Expect.equal "dropped, and said so" (state, [], [ ClientStep.Dropped other ])
                    }
                ]

            testList
                "the url"
                [
                    test "a url patient over a launched Session is asked about, and yes starts over on it" {
                        let launched =
                            { initial Url.none with
                                Lanes =
                                    { Lanes.initial None with
                                        Session = SessionState.opened HeldPanelPolicyTests.identified None
                                    }
                            }

                        let url = Url.parse (System.DateTime(2026, 1, 1)) [ "patient"; "?agd=10" ]
                        let asked =
                            launched
                            |> play [ ClientMsg.UrlChanged([ "patient"; "?agd=10" ], url) ]
                            |> stateOf

                        UrlPolicy.UrlState.asked asked.Shell.Url
                        |> Expect.equal "asked" (Some [ "patient"; "?agd=10" ])

                        let result = asked |> play [ ClientMsg.Shell ShellMsg.LeftForUrl ]

                        result
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.StartedOver _) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "started over"

                        (stateOf result).Shell.Url
                        |> Expect.equal "shown" (UrlPolicy.UrlState.Shown [ "patient"; "?agd=10" ])
                    }

                    test "yes without a question changes nothing" {
                        let state = initial Url.none

                        let after, effects, _ = state |> play [ ClientMsg.Shell ShellMsg.LeftForUrl ]

                        (after, effects) |> Expect.equal "unchanged" (state, [])
                    }
                ]

            testList
                "the error banner"
                [
                    test "a failed formulary load raises the banner, and its next landing clears it" {
                        let failed =
                            initial Url.none
                            |> play
                                [
                                    ClientMsg.Loader(LoaderMsg.Start Busy.Load.Formulary)
                                    landed (Landing.Formulary(None, Error [| "down" |]))
                                ]
                            |> stateOf

                        failed.Shell.ServerError |> Expect.isSome "raised"

                        let cleared =
                            failed
                            |> play
                                [
                                    ClientMsg.Loader(LoaderMsg.Start Busy.Load.Formulary)
                                    landed (Landing.Formulary(None, answered Formulary.empty))
                                ]
                            |> stateOf

                        cleared.Shell.ServerError |> Expect.isNone "cleared"
                    }

                    test "the plan's answer clears the banner its failure raised" {
                        let sent = OrderPlanCommand.FilterRows(one.Filtered, one)

                        let state =
                            { initial Url.none with
                                Lanes =
                                    { Lanes.initial (Some draft) with
                                        OrderPlan = OrderPlanState.changing patient one None sent "r-1"
                                    }
                            }
                            |> play
                                [
                                    ClientMsg.Shell(
                                        ShellMsg.ServerErrorRaised(
                                            ServerErrorPolicy.ErrorSource.OrderPlan,
                                            [| "down" |]
                                        )
                                    )
                                    ClientMsg.Lanes(LanesMsg.Plan(OrderPlanMsg.Answered("r-1", Ok one)))
                                ]
                            |> stateOf

                        state.Shell.ServerError |> Expect.isNone "cleared"
                    }
                ]

            testList
                "the admin"
                [
                    test "a logout leaves the settings page" {
                        let state =
                            { initial Url.none with Admin = { AdminState.initial with IsAuthenticated = true } }
                            |> play [ ClientMsg.PageChosen Page.Page.Settings ]
                            |> stateOf

                        state.Shell.Page |> Expect.equal "shown" Page.Page.Settings

                        (state |> play [ ClientMsg.Admin AdminMsg.Logout ] |> stateOf).Shell.Page
                        |> Expect.equal "left" Page.Page.LifeSupport
                    }

                    test "the settings page is refused to a user not logged in" {
                        (initial Url.none |> play [ ClientMsg.PageChosen Page.Page.Settings ] |> stateOf).Shell.Page
                        |> Expect.equal "refused" Page.Page.LifeSupport
                    }
                ]

            testList
                "the patient held and the signature"
                [
                    test "a panel change while the patient is held falls to the closing arm" {
                        let change = ClientMsg.PanelChanged(Some otherDraft, PatientDraftPolicy.Estimates.Kept, "p-1")

                        heldState
                        |> play [ change ]
                        |> Expect.equal "dropped, and said so" (heldState, [], [ ClientStep.Dropped change ])
                    }

                    test "a panel change reaches the patient when the patient is not held" {
                        initial Url.none
                        |> play [ ClientMsg.PanelChanged(Some draft, PatientDraftPolicy.Estimates.Kept, "p-1") ]
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(_, "p-1"))) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "sent"
                    }

                    test "a signature answered is said on the snackbar and told to the plan" {
                        let result =
                            heldState
                            |> play
                                [
                                    ClientMsg.Sign(one, "s-1")
                                    ClientMsg.Lanes(LanesMsg.Signing(SigningMachineTests.Fixtures.issued "s-1"))
                                    ClientMsg.Lanes(LanesMsg.Signing(SigningMsg.ConfirmPin("1234", "k-1")))
                                    ClientMsg.Lanes(LanesMsg.Signing(SigningMachineTests.Fixtures.submitted "k-1"))
                                ]

                        (stateOf result).Shell.Snackbar
                        |> Expect.equal "said" (Some(Alert.Alert.OrderPlanSigned SigningMachineTests.Fixtures.signed))

                        result
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.Plan(OrderPlanMsg.Signed, _, _)) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "the plan told"
                    }

                    test "a signature takes the plan's differences, and an accept whether the patient is held" {
                        let differences = heldState.Lanes.OrderPlan |> OrderPlanState.differences one

                        heldState
                        |> play [ ClientMsg.Sign(one, "s-1") ]
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.Signing(SigningMsg.Sign(_, d, "s-1"), _, _)) -> d = differences
                            | _ -> false
                        )
                        |> Expect.isTrue "the differences"

                        heldState
                        |> play [ ClientMsg.AcceptDataChange ]
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.Signing(SigningMsg.AcceptDataChange true, _, _)) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "held"
                    }
                ]
        ]
