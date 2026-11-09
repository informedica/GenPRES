module Informedica.GenPRES.Client.Core.Tests.MachinePolicies.BusyTests

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Informedica.GenPRES.Client.Core.MachinePolicies
open Informedica.GenPRES.Client.Core.Tests.StateMachines
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Api
open Loads
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures
open OrderPlanFixtures

module Lanes = Informedica.GenPRES.Client.Core.Lanes


let allPages =
    [
        Page.Page.LifeSupport
        Page.Page.ContinuousMeds
        Page.Page.Prescribe
        Page.Page.Nutrition
        Page.Page.OrderPlan
        Page.Page.Formulary
        Page.Page.Parenteralia
        Page.Page.Interactions
        Page.Page.Settings
    ]


let allLoads =
    [
        Load.Settings
        Load.Localization
        Load.Hospitals
        Load.BolusMedication
        Load.ContinuousMedication
        Load.Products
        Load.Formulary
        Load.Parenteralia
        Load.Interactions
        Load.DrugNames
        Load.Totals
        Load.LogFiles
        Load.LogAnalysis
        Load.Reload
    ]


/// The pages a request disables, in the order of allPages.
let disabled request =
    allPages |> List.filter (fun p -> [ request ] |> BusyPolicy.page p)


/// Nothing out in any lane: the lanes each test changes one lane of.
let idleLanes = { Lanes.initial (Some draft) with OrderPlan = shown }


/// Nothing out in any lane but the loads.
let idle loads = Lanes.out false idleLanes loads


[<Tests>]
let tests =
    testList
        "Busy"
        [
            test "nothing out: nothing is busy" {
                let out = idle []

                out |> Expect.isEmpty "no request"
                out |> BusyPolicy.any |> Expect.isFalse "the menu is free"

                for p in allPages do
                    out |> BusyPolicy.page p |> Expect.isFalse $"%A{p} is free"
            }

            test "each lane with a request out is a request" {
                let workbench = OrderContextState.opening patient "w-1"
                let plan = recalculating one None "p-1" (OrderPlanCommand.FilterRows([||], one))
                let signing = SigningState.requesting one None "s-1"

                Lanes.out false { idleLanes with Patient = patientChanging } []
                |> Expect.equal "the patient" [ Request.Patient ]

                Lanes.out false { idleLanes with OrderContext = workbench } []
                |> Expect.equal "the workbench" [ Request.Workbench ]

                Lanes.out false { idleLanes with OrderPlan = plan } []
                |> Expect.equal "the plan" [ Request.Plan ]

                Lanes.out false { idleLanes with Session = SessionState.resuming } []
                |> Expect.equal "the Session" [ Request.Session ]

                Lanes.out false { idleLanes with Signing = signing } []
                |> Expect.equal "the signature" [ Request.Signature ]

                let refreshing =
                    SessionState.opened SessionMachineTests.full None
                    |> SessionState.transition SessionMsg.RefreshPatient
                    |> fst

                Lanes.out false { idleLanes with Session = refreshing } []
                |> Expect.equal "a refresh" [ Request.Session ]
            }

            test "a signature is a request in each of its phases, and disables every page" {
                let noticed =
                    SigningState.noticed
                        SigningFixtures.plan
                        {
                            Data = None
                            Token = "d-1"
                        }

                for name, signing in
                    [
                        "requesting", SigningFixtures.requesting
                        "noticed", noticed
                        "challenged", SigningFixtures.challenged
                        "submitting", SigningFixtures.submitting
                    ] do
                    let out = Lanes.out false { idleLanes with Signing = signing } []

                    out |> Expect.equal name [ Request.Signature ]
                    out |> BusyPolicy.any |> Expect.isTrue $"%s{name}: the menu waits"

                    for p in allPages do
                        out |> BusyPolicy.page p |> Expect.isTrue $"%s{name}: %A{p}"
            }

            test "a workbench or a plan request disables the page that would send the next command or change" {
                let workbench =
                    Lanes.out false { idleLanes with OrderContext = OrderContextState.opening patient "w-1" } []

                workbench
                |> BusyPolicy.page Page.Page.Prescribe
                |> Expect.isTrue "the Prescribe page"

                let plan =
                    Lanes.out
                        false
                        { idleLanes with
                            OrderPlan = recalculating one None "p-1" (OrderPlanCommand.FilterRows([||], one))
                        }
                        []

                plan
                |> BusyPolicy.page Page.Page.OrderPlan
                |> Expect.isTrue "the OrderPlan page"
                plan
                |> BusyPolicy.page Page.Page.Nutrition
                |> Expect.isFalse "the Nutrition page, whose other slots go on"
            }

            test "a patient change disables the pages of both order machines" {
                let out = Lanes.out false { idleLanes with Patient = patientChanging } []

                for p in [ Page.Page.Prescribe; Page.Page.OrderPlan; Page.Page.Nutrition ] do
                    out |> BusyPolicy.page p |> Expect.isTrue $"%A{p}"
            }

            test "a field counting step clicks disables every page but Nutrition, whose fields wait themselves" {
                let out = Lanes.out true idleLanes []

                out |> Expect.equal "the count" [ Request.Counting ]
                out |> BusyPolicy.any |> Expect.isTrue "the menu and the title bar wait"

                for p in allPages do
                    out |> BusyPolicy.page p |> Expect.equal $"%A{p}" (p <> Page.Page.Nutrition)
            }

            test "every load out is a request" {
                for load in allLoads do
                    idle [ load ] |> Expect.equal $"%A{load}" [ Request.Load load ]
            }

            test "every request holds the menu, the drug names and the totals excepted" {
                let requests =
                    [
                        Request.Patient
                        Request.Workbench
                        Request.Plan
                        Request.Session
                        Request.Signature
                        Request.Counting
                    ]
                    @ (allLoads |> List.map Request.Load)

                for request in requests do
                    [ request ]
                    |> BusyPolicy.any
                    |> Expect.equal
                        $"%A{request}"
                        (request <> Request.Load Load.DrugNames && request <> Request.Load Load.Totals)
            }

            test "the patient, the Session, the signature, the settings and the localization disable every page" {
                for request in
                    [
                        Request.Patient
                        Request.Session
                        Request.Signature
                        Request.Load Load.Settings
                        Request.Load Load.Localization
                    ] do
                    request |> disabled |> Expect.equal $"%A{request}" allPages
            }

            test "a workbench request disables the pages that show the workbench, and Settings for a reload" {
                Request.Workbench
                |> disabled
                |> Expect.equal
                    "workbench pages"
                    [
                        Page.Page.LifeSupport
                        Page.Page.ContinuousMeds
                        Page.Page.Prescribe
                        Page.Page.Formulary
                        Page.Page.Parenteralia
                        Page.Page.Settings
                    ]
            }

            test "a plan request disables the pages that show the plan, not the nutrition page" {
                Request.Plan
                |> disabled
                |> Expect.equal "plan pages" [ Page.Page.OrderPlan; Page.Page.Interactions ]
            }

            test "a page's own load disables that page; the formulary and parenteralia loads Settings as well" {
                [
                    Load.BolusMedication, [ Page.Page.LifeSupport ]
                    Load.ContinuousMedication, [ Page.Page.ContinuousMeds ]
                    Load.Products, [ Page.Page.LifeSupport; Page.Page.ContinuousMeds ]
                    Load.Formulary, [ Page.Page.Formulary; Page.Page.Settings ]
                    Load.Parenteralia, [ Page.Page.Parenteralia; Page.Page.Settings ]
                    Load.Interactions, [ Page.Page.Interactions ]
                    Load.DrugNames, [ Page.Page.Interactions ]
                    Load.Totals, []
                    Load.LogFiles, [ Page.Page.Settings ]
                    Load.LogAnalysis, [ Page.Page.Settings ]
                    Load.Reload, [ Page.Page.Settings ]
                    Load.Hospitals, []
                ]
                |> List.iter (fun (load, pages) -> Request.Load load |> disabled |> Expect.equal $"%A{load}" pages)
            }

            test "the drug names disable the Page.Page.Interactions page and hold nothing else" {
                let out = idle [ Load.DrugNames ]

                out |> BusyPolicy.any |> Expect.isFalse "the menu is free"

                for p in allPages do
                    out |> BusyPolicy.page p |> Expect.equal $"%A{p}" (p = Page.Page.Interactions)
            }
        ]
