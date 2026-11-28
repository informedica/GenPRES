/// The client as one transition: use cases played through it, as the lanes' tests play theirs.
module Informedica.GenPRES.Client.Core.Tests.ClientTests

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Informedica.GenPRES.Client.Core.MachinePolicies
open Informedica.GenPRES.Client.Core.Tests.StateMachines
open Informedica.GenPRES.Client.Core.Tests.MachinePolicies
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Shared.Api
open Informedica.GenPRES.Client.Core.Lanes
open PatientMachine
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open LoaderMachine
open AdminMachine
open ShellMachine
open Informedica.GenPRES.Client.Core.Client
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures

module Lanes = Informedica.GenPRES.Client.Core.Lanes
module Client = Informedica.GenPRES.Client.Core.Client


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
        landed (Landing.Settings(Ok(ShellMachineTests.settings Informedica.GenPRES.Shared.Localization.Dutch)))
        landed (Landing.Localization(Ok [||]))
        landed (Landing.Formulary(None, answered Formulary.empty))
        landed (Landing.Parenteralia(None, answered Parenteralia.empty))
    ]


let hasStep check (_, _, steps) = steps |> List.exists check


let hasEffect check (_, effects, _) = effects |> List.exists check


let seededFrom source =
    hasStep (
        function
        | ClientStep.Lanes(LanesStep.Workbench(OrderContextMsg.SeedFilter(seed, _), _, _)) -> seed.Source = source
        | _ -> false
    )


/// A Session open for a patient the EHR identified, with a plan that has a new order.
let unsignedState =
    { initial Url.none with
        Lanes =
            { Lanes.initial (Some draft) with
                Session = SessionState.opened SessionPatientPolicyTests.identified None
                OrderPlan = OrderPlanState.settled patient one None |> OrderPlanState.withOpened [||]
            }
    }


[<Tests>]
let tests =
    testList
        "Client"
        [
            test "a draft with only a weight is the current patient, so the summary shows it" {
                let weightOnly =
                    { PatientData.empty with Weight = { PatientData.empty.Weight with Measured = Some 12000<gram> } }

                let state, _, _ = loaded { Url.none with Patient = Some weightOnly }

                state |> Client.currentPatient |> Expect.equal "the draft" (Some weightOnly)
            }

            test "the current patient is the draft until the server answers, then the patient it answered" {
                let state, effects, _ = loaded { Url.none with Patient = Some draft }

                let request =
                    effects
                    |> List.pick (
                        function
                        | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(_, r))) -> Some r
                        | _ -> None
                    )

                let answer =
                    { draft with
                        Weight =
                            { draft.Weight with
                                EstimatedP3 = Some 25000<gram>
                                Estimated = Some 32000<gram>
                                EstimatedP97 = Some 45000<gram>
                            }
                    }

                let answeredState =
                    state
                    |> play [ ClientMsg.Lanes(LanesMsg.Patient(PatientMsg.Answered(request, Ok answer))) ]
                    |> stateOf

                (state |> Client.currentPatient, answeredState |> Client.currentPatient)
                |> Expect.equal "the draft, then the answer with its estimate" (Some draft, Some answer)
            }

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

                    test "the start-up ends once the Session, every load and the patient options have answered" {
                        let loads =
                            loaded Url.none
                            |> stateOf
                            |> play (
                                ClientMsg.Lanes(LanesMsg.Session(SessionMsg.Resumed(Ok ResumeResult.NotFound)))
                                :: everyLoad
                            )

                        // without patient data the patient panel asks for the options
                        let request =
                            (stateOf loads).Lanes.Patient
                            |> PatientState.inFlightRequest
                            |> Option.defaultWith (fun () -> failtest "no request for the options")

                        let state =
                            loads
                            |> stateOf
                            |> play
                                [
                                    ClientMsg.Lanes(
                                        LanesMsg.Patient(
                                            PatientMsg.Answered(
                                                request,
                                                Ok Informedica.GenPRES.Shared.Models.PatientData.empty
                                            )
                                        )
                                    )
                                ]

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
                            (StartupPolicy.Startup.Started, Informedica.GenPRES.Shared.Localization.Dutch)
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
                                            OrderContextState.evaluated
                                                patient
                                                (OrderContextState.emptyMedication patient)
                                    }
                            }
                            |> play
                                [
                                    ClientMsg.Loader(LoaderMsg.PatientSet(Some patient))
                                    landed (
                                        Landing.Formulary(
                                            None,
                                            answered { Formulary.empty with PatientData = Some patient }
                                        )
                                    )
                                    landed (
                                        Landing.Parenteralia(
                                            None,
                                            answered { Parenteralia.empty with PatientData = Some patient }
                                        )
                                    )
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
                                { OrderContextState.emptyMedication patient with
                                    Filter =
                                        { OrderContext.empty.Filter with
                                            Generic = Some "paracetamol"
                                            Indication = Some "pijn"
                                        }
                                }

                            stateOf seeded
                            |> play
                                [
                                    landed (
                                        Landing.Formulary(
                                            None,
                                            answered { Formulary.empty with PatientData = Some patient }
                                        )
                                    )
                                    ClientMsg.Lanes(
                                        LanesMsg.Workbench(
                                            OrderContextMsg.Answered(
                                                request,
                                                Ok(OrderContextResponse.Evaluated(ctx, [||]))
                                            )
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
                "the url"
                [
                    test "a url patient over a launched Session is asked about, and yes starts over on it" {
                        let launched =
                            { initial Url.none with
                                Lanes =
                                    { Lanes.initial None with
                                        Session = SessionState.opened SessionPatientPolicyTests.identified None
                                    }
                            }

                        let url = Url.parse [ "patient"; "?wks=1&dys=3" ]
                        let asked =
                            launched
                            |> play [ ClientMsg.UrlChanged([ "patient"; "?wks=1&dys=3" ], url) ]
                            |> stateOf

                        UrlPolicy.UrlState.asked asked.Shell.Url
                        |> Expect.equal "asked" (Some [ "patient"; "?wks=1&dys=3" ])

                        let result = asked |> play [ ClientMsg.Shell ShellMsg.LeftForUrl ]

                        result
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.StartedOver _) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "started over"

                        (stateOf result).Shell.Url
                        |> Expect.equal "shown" (UrlPolicy.UrlState.Shown [ "patient"; "?wks=1&dys=3" ])
                    }

                    test "yes without a question changes nothing" {
                        let state = initial Url.none

                        let after, effects, _ = state |> play [ ClientMsg.Shell ShellMsg.LeftForUrl ]

                        (after, effects) |> Expect.equal "unchanged" (state, [])
                    }
                ]

            testList
                "a failure the server tells"
                [
                    let told effect =
                        initial Url.none
                        |> Client.route (LanesTests.counter ()) (ClientEffect.Lanes effect)

                    let alerted alert = [ ClientMsg.Shell(ShellMsg.AlertRaised alert) ]

                    test "a patient change tells the first error the server gave" {
                        told (LanesEffect.Patient(PatientEffect.TellError [| "a"; "b" |]))
                        |> Expect.equal "the first" (alerted (Alert.Alert.PatientChangeFailed(Some "a")))
                    }

                    test "a patient change tells no error text when the server gave none" {
                        told (LanesEffect.Patient(PatientEffect.TellError [||]))
                        |> Expect.equal "none" (alerted (Alert.Alert.PatientChangeFailed None))
                    }

                    test "the workbench tells the first error the server gave" {
                        told (LanesEffect.Workbench(OrderContextEffect.TellError [| "a"; "b" |]))
                        |> Expect.equal "the first" (alerted (Alert.Alert.WorkbenchFailed(Some "a")))
                    }

                    test "the workbench tells no error text when the server gave none" {
                        told (LanesEffect.Workbench(OrderContextEffect.TellError [||]))
                        |> Expect.equal "none" (alerted (Alert.Alert.WorkbenchFailed None))
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
                                    ClientMsg.Loader(LoaderMsg.Start Loads.Load.Formulary)
                                    landed (Landing.Formulary(None, Error [| "down" |]))
                                ]
                            |> stateOf

                        failed.Shell.ServerError |> Expect.isSome "raised"

                        let cleared =
                            failed
                            |> play
                                [
                                    ClientMsg.Loader(LoaderMsg.Start Loads.Load.Formulary)
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
                "what the page reads"
                [
                    testList
                        "the disclaimer shows for anonymous use only"
                        [
                            for name, session, expected in
                                [
                                    "anonymous", SessionState.anonymous, true
                                    "resuming", SessionState.resuming, false
                                    "launched", SessionState.opened SessionPatientPolicyTests.identified None, false
                                    "refused", SessionState.refused LaunchRefusal.NoBrowserIdentity, false
                                ] do
                                test name {
                                    let state = initial Url.none

                                    { state with Lanes = { state.Lanes with Session = session } }
                                    |> showDisclaimer
                                    |> Expect.equal "shown" expected
                                }
                        ]

                    test "the disclaimer accepted no longer shows" {
                        initial Url.none
                        |> play [ ClientMsg.Shell ShellMsg.DisclaimerAccepted ]
                        |> stateOf
                        |> showDisclaimer
                        |> Expect.isFalse "accepted"
                    }

                    testList
                        "the url question asks for a launch when its url carries one"
                        [
                            for name, url, expected in
                                [
                                    "a launch", Some [ "session"; "?launch=abc" ], true
                                    "a patient", Some [ "patient"; "?wks=1&dys=3" ], false
                                    "no question", None, false
                                ] do
                                test name {
                                    let state = initial Url.none

                                    let shown = UrlPolicy.UrlState.Shown []

                                    let asked =
                                        match url with
                                        | Some sl -> shown |> UrlPolicy.UrlState.ask sl (Url.parse sl)
                                        | None -> shown

                                    { state with Shell = { state.Shell with Url = asked } }
                                    |> asksLaunch
                                    |> Expect.equal "asks for a launch" expected
                                }
                        ]
                ]

            testList
                "the patient and the signature"
                [
                    test "a panel edit with an unsigned order reaches the patient" {
                        unsignedState
                        |> play [ ClientMsg.PanelEdited(PatientDataEdit.SetYears(Some 5), "p-1") ]
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallEdit(_, _, "p-1"))) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "sent"
                    }

                    test "a new patient clears everything first, then sends the edit for the data shown" {
                        let _, effects, _ =
                            initial Url.none
                            |> play [ ClientMsg.NewPatient(PatientDataEdit.SetYears(Some 5), "p-1") ]

                        let cleared =
                            effects
                            |> List.findIndex (
                                function
                                | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.SetPatientData None)) -> true
                                | _ -> false
                            )

                        let sent =
                            effects
                            |> List.findIndex (
                                function
                                | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallEdit(_, _, "p-1"))) -> true
                                | _ -> false
                            )

                        cleared < sent |> Expect.isTrue "the clear before the call"
                    }

                    test "a new patient clears without a call, then sends only the edit" {
                        let _, effects, _ =
                            initial Url.none
                            |> play [ ClientMsg.NewPatient(PatientDataEdit.SetYears(Some 5), "p-1") ]

                        effects
                        |> List.choose (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(_, request))) ->
                                Some request
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallEdit(_, _, request))) ->
                                Some request
                            | _ -> None
                        )
                        |> Expect.equal "the edit only" [ "p-1" ]
                    }

                    test "a new patient withdraws the warnings and asks no page for no patient" {
                        let result =
                            initial Url.none
                            |> play [ ClientMsg.NewPatient(PatientDataEdit.SetYears(Some 5), "p-1") ]

                        result
                        |> hasEffect (
                            function
                            | ClientEffect.Loader LoaderEffect.WithdrawInteractionsFound -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "the warnings of the previous patient go"

                        result
                        |> hasEffect (
                            function
                            | ClientEffect.Loader(LoaderEffect.FetchFormulary _)
                            | ClientEffect.Loader(LoaderEffect.FetchParenteralia _) -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "only the patient goes out"
                    }

                    test "the warning follows the challenge, with the identity of the Session" {
                        let signingWith moved =
                            { unsignedState with
                                Lanes =
                                    { unsignedState.Lanes with
                                        Signing =
                                            SigningState.requesting one None "r-1"
                                            |> SigningState.transition (
                                                SigningMsg.ChallengeAnswered(
                                                    "r-1",
                                                    Ok(SigningResponse.ChallengeIssued("c-1", moved))
                                                )
                                            )
                                            |> fst
                                    }
                            }

                        signingWith true |> Client.ehrMovedOn |> Expect.isTrue "told"
                        signingWith false |> Client.ehrMovedOn |> Expect.isFalse "nothing to tell"
                        unsignedState |> Client.signedFor |> Expect.isSome "the name"
                        initial Url.none |> Client.signedFor |> Expect.isNone "no identity"
                    }

                    test "a new patient is dropped once the patient is identified" {
                        let confirmed = ClientMsg.NewPatient(PatientDataEdit.SetYears(Some 5), "p-1")

                        unsignedState
                        |> play [ confirmed ]
                        |> Expect.equal "the orders stay" (unsignedState, [], [ ClientStep.Dropped confirmed ])
                    }

                    test "the panel asks over the session, the plan and the workbench of the state" {
                        let anonymous =
                            { unsignedState with
                                Lanes = { unsignedState.Lanes with Session = Lanes.initial None |> _.Session }
                            }

                        anonymous |> Client.asksNewPatient |> Expect.isTrue "orders, and no identity"
                        unsignedState |> Client.asksNewPatient |> Expect.isFalse "identified"
                        initial Url.none |> Client.asksNewPatient |> Expect.isFalse "nothing to lose"
                    }

                    test "an anonymous edit with nothing to lose takes the ordinary path" {
                        let state = initial Url.none

                        state |> Client.asksNewPatient |> Expect.isFalse "no question"

                        state
                        |> play [ ClientMsg.PanelEdited(PatientDataEdit.SetYears(Some 5), "p-1") ]
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.SetPatientData None)) -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "nothing is cleared"
                    }

                    test "a panel edit is sent as an edit with the patient data the panel shows" {
                        initial Url.none
                        |> play [ ClientMsg.PanelEdited(PatientDataEdit.SetYears(Some 5), "p-1") ]
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallEdit(PatientDataEdit.SetYears(Some 5),
                                                                                            _,
                                                                                            "p-1"))) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "sent"
                    }

                    test "a panel reset clears the patient data and asks with empty patient data for the options" {
                        let _, effects, _ = initial Url.none |> play [ ClientMsg.PanelCleared "p-1" ]

                        effects
                        |> List.exists (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.SetPatientData None)) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "cleared"

                        effects
                        |> List.exists (
                            function
                            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(p, "p-1"))) ->
                                p = Informedica.GenPRES.Shared.Models.PatientData.empty
                            | _ -> false
                        )
                        |> Expect.isTrue "asked"
                    }

                    test "a signature answered is said on the snackbar and told to the plan" {
                        let result =
                            unsignedState
                            |> play
                                [
                                    ClientMsg.Sign(one, "s-1")
                                    ClientMsg.Lanes(LanesMsg.Signing(SigningFixtures.issued "s-1"))
                                    ClientMsg.Lanes(LanesMsg.Signing(SigningMsg.ConfirmPin("1234", "k-1")))
                                    ClientMsg.Lanes(LanesMsg.Signing(SigningFixtures.submitted "k-1"))
                                ]

                        (stateOf result).Shell.Snackbar
                        |> Expect.equal "said" (Some(Alert.Alert.OrderPlanSigned SigningFixtures.signed))

                        result
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.Plan(OrderPlanMsg.Signed, _, _)) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "the plan told"
                    }

                    test "a signature takes the plan's differences" {
                        let differences = unsignedState.Lanes.OrderPlan |> OrderPlanState.differences one

                        unsignedState
                        |> play [ ClientMsg.Sign(one, "s-1") ]
                        |> hasStep (
                            function
                            | ClientStep.Lanes(LanesStep.Signing(SigningMsg.Sign(_, d, "s-1"), _, _)) -> d = differences
                            | _ -> false
                        )
                        |> Expect.isTrue "the differences"
                    }

                    test "taking the EHR's data with an unsigned order reads the EHR again and removes nothing" {
                        let notice =
                            {
                                PatientData = Some patient
                                Token = "d-1"
                            }

                        let effects =
                            { unsignedState with
                                Lanes = { unsignedState.Lanes with Signing = SigningState.noticed one notice }
                            }
                            |> play [ ClientMsg.TakeEhrData ]

                        effects
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Session(SessionEffect.CallRefreshPatient _)) -> true
                            | _ -> false
                        )
                        |> Expect.isTrue "read again"

                        effects
                        |> hasEffect (
                            function
                            | ClientEffect.Lanes(LanesEffect.Plan(OrderPlanEffect.CallPlan _)) -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "the plan is left alone until the data come back"
                    }
                ]
        ]


/// A real case: a plan with paracetamol, and a feeding slot narrowed to Nutrilon Nenatal 1 that offers the
/// supplement Nutrilon Nenatal BMF pdr, as the LinkRules sheet links it.
module TotalsQueryFixtures =

    let feeding =
        { context "" "Nutrilon Nenatal 1" with
            Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
            Scenarios = [| scenario "o-feeding" "Nutrilon Nenatal 1" |]
        }

    let bmf =
        { context "" "Nutrilon Nenatal BMF pdr" with
            Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement
            Scenarios = [| scenario "o-bmf" "Nutrilon Nenatal BMF pdr" |]
        }

    /// A feeding with two orders, not yet narrowed.
    let twoFeedings =
        { feeding with
            Scenarios =
                [|
                    scenario "o-feeding" "Nutrilon Nenatal 1"
                    scenario "o-other" "Nutrilon Nenatal 1"
                |]
        }


    let answeredWith cmd (main: OrderContext) offers =
        OrderContextState.changing patient cmd main main "r-0"
        |> OrderContextState.transition (
            OrderContextMsg.Answered("r-0", Ok(OrderContextResponse.Evaluated(main, offers)))
        )
        |> fst


    /// The feeding slot narrowed, BMF offered and off.
    let feedingSlot = answeredWith (OrderViewCommand.SelectNthOrderScenario 0) feeding [| bmf |]


    /// The client with the plan of paracetamol and the slot s-1.
    let withSlot slot =
        { initial Url.none with
            Lanes =
                { Lanes.initial (Some draft) with
                    OrderPlan = OrderPlanState.settled patient one None
                    Slots = [ "s-1", slot ]
                }
        }


    /// The totals queries the step sent: the order ids of each query, none for the empty one.
    let queries msg state =
        let _, _, steps = state |> transition (LanesTests.counter ()) msg

        steps
        |> List.choose (
            function
            | ClientStep.Loader(LoaderMsg.QueryTotals query, _, _) ->
                Some(
                    query
                    |> Option.map (fun q -> q.OrderContexts |> Array.map (fun c -> c.Scenarios[0].Order.Id))
                )
            | _ -> None
        )


open TotalsQueryFixtures


/// The client state with the page shown and the patient from the last server answer.
let withPageAndPatient page patient =
    let state = initial Url.none

    { state with
        Shell = { state.Shell with Page = page }
        Loader = { state.Loader with Patient = patient }
    }


/// The emergency list requests among the effects of a transition.
let listFetches (_, effects, _) =
    effects
    |> List.choose (
        function
        | ClientEffect.Loader(LoaderEffect.FetchEmergencyList(_, pat)) -> Some pat
        | _ -> None
    )


[<Tests>]
let emergencyListQueryTests =
    testList
        "the emergency list query the client sends"
        [
            test "is the patient while the list page is shown and the patient has an age" {
                withPageAndPatient Page.Page.LifeSupport (Some draft)
                |> emergencyListQuery
                |> Expect.equal "the patient" (Some draft)
            }

            test "is the patient while the list page is shown and the patient is marked as an adult" {
                let adult = { PatientData.empty with AdultMark = AdultMark.MarkableAdult true }

                withPageAndPatient Page.Page.LifeSupport (Some adult)
                |> emergencyListQuery
                |> Expect.equal "the patient" (Some adult)
            }

            test "is the patient while the list page is shown and the patient has a measured weight and height" {
                let measured =
                    { PatientData.empty with
                        Weight = { PatientData.empty.Weight with Measured = Some 10000<gram> }
                        Height = { PatientData.empty.Height with Measured = Some 80<cm> }
                    }

                withPageAndPatient Page.Page.LifeSupport (Some measured)
                |> emergencyListQuery
                |> Expect.equal "the patient" (Some measured)
            }

            test "is None for a patient with a weight but without an age and a height" {
                let weighed =
                    { PatientData.empty with Weight = { PatientData.empty.Weight with Measured = Some 10000<gram> } }

                withPageAndPatient Page.Page.LifeSupport (Some weighed)
                |> emergencyListQuery
                |> Expect.isNone "no query"
            }

            test "is None for a patient without data" {
                withPageAndPatient Page.Page.LifeSupport (Some PatientData.empty)
                |> emergencyListQuery
                |> Expect.isNone "no query"
            }

            test "is None without a patient" {
                withPageAndPatient Page.Page.LifeSupport None
                |> emergencyListQuery
                |> Expect.isNone "no query"
            }

            test "is None on another page" {
                withPageAndPatient Page.Page.Prescribe (Some draft)
                |> emergencyListQuery
                |> Expect.isNone "no query"
            }

            test "a patient answer on the list page requests the list" {
                withPageAndPatient Page.Page.LifeSupport None
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some draft)) ]
                |> listFetches
                |> Expect.equal "requested" [ draft ]
            }

            test "a patient answer on another page requests nothing" {
                withPageAndPatient Page.Page.Prescribe None
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some draft)) ]
                |> listFetches
                |> Expect.isEmpty "nothing"
            }

            test "the same patient answer again requests nothing" {
                withPageAndPatient Page.Page.LifeSupport None
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some draft)) ]
                |> stateOf
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some draft)) ]
                |> listFetches
                |> Expect.isEmpty "nothing"
            }

            test "moving to the list page with a patient requests the list" {
                withPageAndPatient Page.Page.Prescribe (Some draft)
                |> play [ ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.LifeSupport) ]
                |> listFetches
                |> Expect.equal "requested" [ draft ]
            }

            test "leaving the list page clears the rows" {
                withPageAndPatient Page.Page.LifeSupport None
                |> play
                    [
                        ClientMsg.Loader(LoaderMsg.PatientSet(Some draft))
                        ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.Prescribe)
                    ]
                |> stateOf
                |> _.Loader.EmergencyList
                |> Expect.equal "cleared" HasNotStartedYet
            }
        ]


/// The patient with an age of ten years and the Location UMCU.
let located = { draft with Location = Some "UMCU" }


/// The continuous medication list requests among the effects of a transition.
let continuousFetches (_, effects, _) =
    effects
    |> List.choose (
        function
        | ClientEffect.Loader(LoaderEffect.FetchContinuousList(_, pat)) -> Some pat
        | _ -> None
    )


[<Tests>]
let continuousListQueryTests =
    testList
        "the continuous list query the client sends"
        [
            test "is the patient while the list page is shown and the patient has a Location and an age" {
                withPageAndPatient Page.Page.ContinuousMeds (Some located)
                |> continuousListQuery
                |> Expect.equal "the patient" (Some located)
            }

            test "is None for a patient without a Location" {
                withPageAndPatient Page.Page.ContinuousMeds (Some draft)
                |> continuousListQuery
                |> Expect.isNone "no query"
            }

            test "is None for a patient with a Location but without data the server can calculate with" {
                withPageAndPatient Page.Page.ContinuousMeds (Some { PatientData.empty with Location = Some "UMCU" })
                |> continuousListQuery
                |> Expect.isNone "no query"
            }

            test "is None without a patient" {
                withPageAndPatient Page.Page.ContinuousMeds None
                |> continuousListQuery
                |> Expect.isNone "no query"
            }

            test "is None on another page" {
                withPageAndPatient Page.Page.LifeSupport (Some located)
                |> continuousListQuery
                |> Expect.isNone "no query"
            }

            test "a patient answer on the list page requests the list" {
                withPageAndPatient Page.Page.ContinuousMeds None
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some located)) ]
                |> continuousFetches
                |> Expect.equal "requested" [ located ]
            }

            test "the same patient answer again requests nothing" {
                withPageAndPatient Page.Page.ContinuousMeds None
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some located)) ]
                |> stateOf
                |> play [ ClientMsg.Loader(LoaderMsg.PatientSet(Some located)) ]
                |> continuousFetches
                |> Expect.isEmpty "nothing"
            }

            test "moving to the list page with a patient requests the list" {
                withPageAndPatient Page.Page.Prescribe (Some located)
                |> play [ ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.ContinuousMeds) ]
                |> continuousFetches
                |> Expect.equal "requested" [ located ]
            }

            test "leaving the list page clears the rows" {
                withPageAndPatient Page.Page.ContinuousMeds None
                |> play
                    [
                        ClientMsg.Loader(LoaderMsg.PatientSet(Some located))
                        ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.Prescribe)
                    ]
                |> stateOf
                |> _.Loader.ContinuousList
                |> Expect.equal "cleared" HasNotStartedYet
            }
        ]


[<Tests>]
let emergencyListRowTests =
    testList
        "a row chosen on the emergency list"
        [
            test "moves to the prescribe page and puts the order context of the row in the workbench" {
                let row = { OrderContext.empty with OrderContext.Filter.Generic = Some "adrenaline" }

                let chosen =
                    { initial Url.none with Lanes = Lanes.initial (Some draft) }
                    |> play [ ClientMsg.EmergencyListRowChosen row ]

                chosen
                |> hasStep (
                    function
                    | ClientStep.Shell(ShellMsg.MovedToPage Page.Page.Prescribe, _, _) -> true
                    | _ -> false
                )
                |> Expect.isTrue "moved to the prescribe page"

                chosen
                |> hasStep (
                    function
                    | ClientStep.Lanes(LanesStep.Workbench(OrderContextMsg.Chosen ctx, _, _)) -> ctx = row
                    | _ -> false
                )
                |> Expect.isTrue "the workbench got the order context of the row"
            }
        ]


[<Tests>]
let emergencyListCheckedTests =
    let adrenaline = { OrderContext.empty with OrderContext.Filter.Generic = Some "adrenaline" }
    let atropine = { OrderContext.empty with OrderContext.Filter.Generic = Some "atropine" }

    // A calculated row without an order context, then two medication rows.
    let rows =
        [|
            Intervention.emptyIntervention
            { Intervention.emptyIntervention with OrderContext = Some adrenaline }
            { Intervention.emptyIntervention with OrderContext = Some atropine }
        |]

    let withChecked rows numbers =
        { initial Url.none with
            Loader =
                { LoaderState.initial with
                    EmergencyList = rows
                    EmergencyListChecked = numbers
                }
        }

    testList
        "the checked rows of the emergency list"
        [
            test "give their order contexts; a calculated row gives none" {
                withChecked (Resolved rows) [| 1; 3 |]
                |> emergencyListCheckedContexts
                |> Expect.equal "the atropine context" [| atropine |]
            }

            test "give no contexts without rows" {
                withChecked HasNotStartedYet [| 2 |]
                |> emergencyListCheckedContexts
                |> Expect.isEmpty "none"
            }
        ]


[<Tests>]
let continuousListRowTests =
    let morfine = { OrderContext.empty with OrderContext.Filter.Generic = Some "morfine" }
    let midazolam = { OrderContext.empty with OrderContext.Filter.Generic = Some "midazolam" }

    let rows =
        [|
            { Intervention.emptyIntervention with OrderContext = Some morfine }
            { Intervention.emptyIntervention with OrderContext = Some midazolam }
        |]

    let withChecked rows numbers =
        { initial Url.none with
            Loader =
                { LoaderState.initial with
                    ContinuousList = rows
                    ContinuousListChecked = numbers
                }
        }

    testList
        "the rows of the continuous list"
        [
            test "a chosen row moves to the prescribe page and puts its order context in the workbench" {
                let chosen =
                    { initial Url.none with Lanes = Lanes.initial (Some draft) }
                    |> play [ ClientMsg.ContinuousListRowChosen morfine ]

                chosen
                |> hasStep (
                    function
                    | ClientStep.Shell(ShellMsg.MovedToPage Page.Page.Prescribe, _, _) -> true
                    | _ -> false
                )
                |> Expect.isTrue "moved to the prescribe page"

                chosen
                |> hasStep (
                    function
                    | ClientStep.Lanes(LanesStep.Workbench(OrderContextMsg.Chosen ctx, _, _)) -> ctx = morfine
                    | _ -> false
                )
                |> Expect.isTrue "the workbench got the order context of the row"
            }

            test "the row with a number, from 1, gives its order context" {
                continuousListRowContext (withChecked (Resolved rows) [||]) 2
                |> Expect.equal "the midazolam context" (Some midazolam)
            }

            test "a number without a row gives no context" {
                continuousListRowContext (withChecked (Resolved rows) [||]) 3
                |> Expect.isNone "none"
            }

            test "the checked rows give their order contexts" {
                withChecked (Resolved rows) [| 2; 1 |]
                |> continuousListCheckedContexts
                |> Expect.equal "midazolam, morfine" [| midazolam; morfine |]
            }

            test "the checked rows give no contexts without rows" {
                withChecked HasNotStartedYet [| 1 |]
                |> continuousListCheckedContexts
                |> Expect.isEmpty "none"
            }
        ]


[<Tests>]
let totalsQueryTests =
    testList
        "the totals query the client sends"
        [
            test "a feeding narrowed in a slot asks for the totals of the plan and the feeding" {
                let narrowing = answeredWith (OrderViewCommand.ClearAllFilterProperty) twoFeedings [||]

                withSlot narrowing
                |> queries (
                    ClientMsg.Lanes(
                        LanesMsg.Slot("s-1", OrderContextMsg.Command(OrderViewCommand.SelectNthOrderScenario 0, "r-1"))
                    )
                )
                |> Expect.equal "nothing while the request is out" []

                let sent =
                    withSlot narrowing
                    |> transition
                        (LanesTests.counter ())
                        (ClientMsg.Lanes(
                            LanesMsg.Slot(
                                "s-1",
                                OrderContextMsg.Command(OrderViewCommand.SelectNthOrderScenario 0, "r-1")
                            )
                        ))
                    |> fun (state, _, _) -> state

                sent
                |> queries (
                    ClientMsg.Lanes(
                        LanesMsg.Slot(
                            "s-1",
                            OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated(feeding, [||])))
                        )
                    )
                )
                |> Expect.equal "paracetamol and the feeding" [ Some [| "o-c-1"; "o-feeding" |] ]
            }

            test "BMF turned on asks again with BMF, and turned off again without it" {
                let on =
                    withSlot feedingSlot
                    |> transition
                        (LanesTests.counter ())
                        (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOn 0)))
                    |> fun (state, _, _) -> state

                withSlot feedingSlot
                |> queries (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOn 0)))
                |> Expect.equal "with BMF" [ Some [| "o-c-1"; "o-feeding"; "o-bmf" |] ]

                on
                |> queries (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOff 0)))
                |> Expect.equal "without BMF" [ Some [| "o-c-1"; "o-feeding" |] ]
            }

            test "a step that changes no counted order asks nothing" {
                withSlot feedingSlot
                |> queries (ClientMsg.Lanes(LanesMsg.Workbench(OrderContextMsg.SelectScenario None)))
                |> Expect.equal "no query" []
            }

            test "without a narrowed slot nothing is asked" {
                withSlot (answeredWith OrderViewCommand.ClearAllFilterProperty twoFeedings [||])
                |> queries (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOff 0)))
                |> Expect.equal "no query" []
            }

            test "another patient removes the slot and sends the empty query" {
                withSlot feedingSlot
                |> queries (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.PatientDataChanged(None, "r-1"))))
                |> Expect.equal "the empty query" [ None ]
            }
        ]


/// The feeding slot of TotalsQueryFixtures, BMF with two orders, and a TPN slot.
module SlotViewFixtures =

    /// BMF with two orders, the powder in two strengths, not yet narrowed.
    let twoBmf =
        { bmf with
            Scenarios =
                [|
                    scenario "o-bmf-1" "Nutrilon Nenatal BMF pdr"
                    scenario "o-bmf-2" "Nutrilon Nenatal BMF pdr"
                |]
        }

    let tpn =
        { context "" "TPN" with
            Category = OrderCategory.Nutrition NutritionCategory.TPN
            Scenarios = [| scenario "o-tpn" "TPN" |]
        }

    let narrowed main offers =
        answeredWith (OrderViewCommand.SelectNthOrderScenario 0) main offers

    /// The client with the plan of paracetamol and the slots given.
    let withSlots slots =
        { withSlot feedingSlot with Lanes = { (withSlot feedingSlot).Lanes with Slots = slots } }

    let after msg state =
        let state, _, _ = state |> transition (LanesTests.counter ()) msg
        state


open SlotViewFixtures


[<Tests>]
let slotViewTests =
    testList
        "the slots as the nutrition page shows them"
        [
            test "a feeding narrowed to one order prescribes that order, with BMF turned off" {
                withSlots [ "s-1", narrowed feeding [| bmf |] ]
                |> Client.slots
                |> List.map (fun s -> s.Id, s.PrescribeOrderId, s.Offers.Length, s.Prescribing)
                |> Expect.equal "the feeding, one offer" [ "s-1", Some "o-feeding", 1, false ]
            }

            test "a feeding with two orders prescribes nothing" {
                withSlots [ "s-1", answeredWith OrderViewCommand.ClearAllFilterProperty twoFeedings [||] ]
                |> Client.slots
                |> List.map _.PrescribeOrderId
                |> Expect.equal "nothing" [ None ]
            }

            test "BMF turned on with two orders keeps the feeding from being prescribed" {
                withSlots [ "s-1", narrowed feeding [| twoBmf |] ]
                |> after (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOn 0)))
                |> Client.slots
                |> List.map _.PrescribeOrderId
                |> Expect.equal "nothing" [ None ]
            }

            test "the slot prescribed waits for the add, the other slot does not" {
                let state =
                    withSlots [ "s-1", narrowed feeding [||]; "s-2", narrowed tpn [||] ]
                    |> after (ClientMsg.Lanes(LanesMsg.Prescribe(Some "s-1", "o-feeding", "r-1")))

                state
                |> Client.slots
                |> List.map (fun s -> s.Id, s.Prescribing)
                |> Expect.equal "the feeding waits" [ "s-1", true; "s-2", false ]

                state
                |> Client.busyPage Page.Page.Nutrition
                |> Expect.isFalse "the nutrition page stays open"

                state
                |> Client.busyPage Page.Page.OrderPlan
                |> Expect.isTrue "the order plan page waits"
            }

            test "a request out for a slot still holds the nutrition page" {
                withSlots [ "s-1", narrowed feeding [||] ]
                |> after (
                    ClientMsg.Lanes(
                        LanesMsg.Slot("s-1", OrderContextMsg.Command(OrderViewCommand.ClearAllFilterProperty, "r-1"))
                    )
                )
                |> Client.busyPage Page.Page.Nutrition
                |> Expect.isTrue "the page waits"
            }

            test "the totals query sends the plan, the feeding and BMF turned on" {
                withSlots [ "s-1", narrowed feeding [| bmf |] ]
                |> after (ClientMsg.Lanes(LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOn 0)))
                |> Client.totalsQuery
                |> Option.map (fun q -> q.OrderContexts |> Array.map _.Scenarios[0].Order.Id)
                |> Expect.equal "paracetamol, the feeding, BMF" (Some [| "o-c-1"; "o-feeding"; "o-bmf" |])
            }

            test "without a narrowed slot the nutrition page shows the plan's own totals" {
                let state = withSlots [ "s-1", answeredWith OrderViewCommand.ClearAllFilterProperty twoFeedings [||] ]

                state
                |> Client.nutritionTotals
                |> Expect.equal "the plan's" (state.Lanes.OrderPlan |> OrderPlanState.plan |> Option.map _.Totals)
            }

            test "the combined totals once the server answered them, still shown while they are asked again" {
                let combined =
                    { Totals.empty with
                        Energy =
                            Some
                                {
                                    Text = Valid [| TextItem.Normal "120 kcal" |]
                                    State = TotalState.WithinNorm
                                }
                    }
                let reply: Reply<Totals> =
                    {
                        Response = combined
                        Notice = None
                    }
                let slots = withSlots [ "s-1", narrowed feeding [||] ]

                let asked =
                    slots
                    |> after (ClientMsg.Loader(LoaderMsg.QueryTotals(Client.totalsQuery slots)))
                let number = asked.Loader.TotalsQueryNumber

                let landed =
                    asked
                    |> after (ClientMsg.Loader(LoaderMsg.Landed(Landing.Totals(number, None, Ok reply))))

                landed
                |> Client.nutritionTotals
                |> Expect.equal "the combined totals" (Some combined)

                landed
                |> after (ClientMsg.Loader(LoaderMsg.QueryTotals(Client.totalsQuery landed)))
                |> Client.nutritionTotals
                |> Expect.equal "still shown while asked again" (Some combined)
            }
        ]


/// The real link rule of ataluren on the workbench: the morning and midday dose offers the evening dose.
module WorkbenchOfferFixtures =

    open OrderContextMachineTests.LinkedOfferFixtures
    open OrderContextMachineTests.OfferCommandFixtures


    /// The client with the workbench given.
    let withWorkbench workbench =
        let state = initial Url.none
        { state with Lanes = { state.Lanes with OrderContext = workbench } }


    let offered = withWorkbench atalurenOffered
    let eveningTurnedOn = withWorkbench eveningOn
    let eveningOfTwoTurnedOn = withWorkbench eveningOfTwoOn


open WorkbenchOfferFixtures


[<Tests>]
let workbenchOfferTests =
    testList
        "the offers on the workbench"
        [
            test "ataluren narrowed to its morning and midday dose offers the evening dose, off" {
                offered
                |> Client.workbenchOffers
                |> Array.map (fun o -> o.Context.Scenarios |> Array.map _.Order.Id, o.Switch)
                |> Expect.equal "the evening dose" [| [| "o-avond" |], OfferSwitch.Off |]
            }

            test "ataluren with the evening dose off or on, narrowed, can be prescribed" {
                offered |> Client.workbenchPrescribable |> Expect.isTrue "off"
                eveningTurnedOn |> Client.workbenchPrescribable |> Expect.isTrue "on"
            }

            test "the evening dose turned on with two sachets keeps ataluren from being prescribed" {
                eveningOfTwoTurnedOn
                |> Client.workbenchPrescribable
                |> Expect.isFalse "not narrowed"
            }
        ]


[<Tests>]
let hospitalTests =
    let newId () = "r-1"

    let withHospital hospital =
        let state = initial Url.none

        { state with
            Lanes = Lanes.initial (Some draft)
            Shell = { state.Shell with Hospital = hospital }
        }

    let call pat =
        ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(pat, "r-1")))

    let edit pat =
        ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallEdit(PatientDataEdit.SetGender Male, pat, "r-1")))

    let sentLocations (_, effects, _) =
        effects
        |> List.choose (
            function
            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallPatient(pat, _)))
            | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.CallEdit(_, pat, _))) -> Some pat.Location
            | _ -> None
        )

    testList
        "the hospital as the Location of the patient"
        [
            test "the hospitals are not there before the settings land" {
                initial Url.none
                |> Client.hospitals
                |> Expect.equal "not started" HasNotStartedYet
            }

            test "the hospitals are the locations of the server settings" {
                let state = initial Url.none

                let settings =
                    { ShellMachineTests.settings Informedica.GenPRES.Shared.Localization.Dutch with
                        Locations = [| "AMC"; "UMCU" |]
                    }

                { state with Loader = { state.Loader with Settings = Resolved settings } }
                |> Client.hospitals
                |> Expect.equal "AMC, UMCU" (Resolved [| "AMC"; "UMCU" |])
            }

            test "a patient call sends the chosen hospital as the Location" {
                call draft
                |> Client.withHospital (Some "AMC")
                |> Expect.equal "AMC" (call { draft with Location = Some "AMC" })
            }

            test "an edit sends the chosen hospital as the Location of the patient data" {
                edit { draft with Location = Some "UMCU" }
                |> Client.withHospital (Some "AMC")
                |> Expect.equal "AMC" (edit { draft with Location = Some "AMC" })
            }

            test "without a hospital chosen, the call goes as it is, so the server default applies" {
                call draft |> Client.withHospital None |> Expect.equal "as it is" (call draft)
            }

            test "an effect that is no patient call goes as it is" {
                let other = ClientEffect.Shell(ShellEffect.HospitalShown "AMC")
                other |> Client.withHospital (Some "AMC") |> Expect.equal "as it is" other
            }

            test "a panel edit and whole patient data are sent with the chosen hospital" {
                withHospital (Some "AMC")
                |> play [ ClientMsg.PanelEdited(PatientDataEdit.SetGender Male, "r-1") ]
                |> sentLocations
                |> Expect.equal "the edit" [ Some "AMC" ]

                withHospital (Some "AMC")
                |> play [ ClientMsg.Lanes(LanesMsg.Patient(PatientMsg.Changed(Some draft, "r-1"))) ]
                |> sentLocations
                |> Expect.equal "the patient data" [ Some "AMC" ]
            }

            test "without a hospital chosen, the patient data go without a Location" {
                withHospital None
                |> play [ ClientMsg.PanelEdited(PatientDataEdit.SetGender Male, "r-1") ]
                |> sentLocations
                |> Expect.equal "none" [ None ]
            }

            test "a patient answer sends no edit after it and sets its hospital" {
                withHospital (Some "AMC")
                |> route
                    newId
                    (ClientEffect.Lanes(
                        LanesEffect.Patient(PatientEffect.SetPatientData(Some { patient with Location = Some "UMCU" }))
                    ))
                |> Expect.equal
                    "the filters, the pages and the hospital"
                    [
                        ClientMsg.Shell ShellMsg.ListFiltersCleared
                        ClientMsg.Loader(LoaderMsg.PatientSet(Some { patient with Location = Some "UMCU" }))
                        ClientMsg.Shell(ShellMsg.PatientHospitalLanded "UMCU")
                    ]
            }

            test "no patient data leaves the hospital" {
                withHospital (Some "AMC")
                |> route newId (ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.SetPatientData None)))
                |> Expect.equal
                    "the filters and the pages only"
                    [
                        ClientMsg.Shell ShellMsg.ListFiltersCleared
                        ClientMsg.Loader(LoaderMsg.PatientSet None)
                    ]
            }

            test "a hospital chosen sends one edit with that hospital" {
                withHospital (Some "UMCU")
                |> play [ ClientMsg.Shell(ShellMsg.HospitalChosen "AMC") ]
                |> sentLocations
                |> Expect.equal "AMC" [ Some "AMC" ]
            }

            test "a hospital chosen without a patient sends nothing" {
                initial Url.none
                |> route newId (ClientEffect.Shell(ShellEffect.HospitalShown "AMC"))
                |> Expect.isEmpty "nothing"
            }
        ]
