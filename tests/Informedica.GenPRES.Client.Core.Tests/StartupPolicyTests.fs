module Informedica.GenPRES.Client.Core.Tests.StartupPolicyTests

open Expecto
open Expecto.Flip
open Shared
open Busy
open SessionMachine
open StartupPolicy


let tr = english


[<Tests>]
let tests =
    testList
        "StartupPolicy"
        [
            test "nothing out and every required load loaded: started" {
                status [] required [] |> Expect.equal "started" Startup.Started
            }

            test "a request out holds the start-up, whatever it is" {
                for request in
                    [
                        Request.Patient
                        Request.Workbench
                        Request.Plan
                        Request.Session
                        Request.Load Load.Settings
                        Request.Load Load.Formulary
                    ] do
                    status [ request ] required [] |> Expect.equal $"%A{request}" Startup.Starting
            }

            test "a required load not loaded yet holds the start-up" {
                for load in required do
                    status [] (required |> List.filter ((<>) load)) []
                    |> Expect.equal $"%A{load}" Startup.Starting
            }

            test "the drug names never hold the start-up" {
                status [ Request.Load Load.DrugNames ] required []
                |> Expect.equal "started" Startup.Started
            }

            test "a failed required load is named, also while other requests are out" {
                status [ Request.Session ] [ Load.Localization ] [ Load.Products; Load.NormalValues ]
                |> Expect.equal
                    "the two, in the order of the required loads"
                    (Startup.Failed [ Load.NormalValues; Load.Products ])
            }

            test "the gate while starting, after a failure, and none once started" {
                startupGate tr Startup.Started |> Expect.isNone "started"

                startupGate tr Startup.Starting
                |> Option.map (fun gate -> gate.Title, gate.Busy)
                |> Expect.equal "starting" (Some("Starting GenPRES", true))

                startupGate tr (Startup.Failed [ Load.BolusMedication; Load.Products ])
                |> Option.map (fun gate -> gate.Body, gate.Busy, gate.Actions)
                |> Expect.equal
                    "failed"
                    (Some("Not loaded: the emergency list, the products. Reload the page to try again.", false, []))
            }

            test "the session's gate goes before the start-up's" {
                gate tr Startup.Starting SessionView.Resuming
                |> Option.map _.Title
                |> Expect.equal "resuming" (Some(SessionGatePolicy.english Terms.``Session Gate Resuming``))

                gate tr Startup.Starting SessionView.Anonymous
                |> Option.map _.Title
                |> Expect.equal "starting" (Some "Starting GenPRES")

                gate tr Startup.Started SessionView.Anonymous |> Expect.isNone "nothing"
            }

            test "the gate covers until started, and while the session's gate does" {
                let idle = SigningMachine.SigningView.Idle

                isGated Startup.Starting SessionView.Anonymous idle |> Expect.isTrue "starting"
                isGated (Startup.Failed [ Load.Products ]) SessionView.Anonymous idle
                |> Expect.isTrue "failed"
                isGated Startup.Started SessionView.Anonymous idle |> Expect.isFalse "started"
                isGated Startup.Started SessionView.Resuming idle |> Expect.isTrue "resuming"
            }

            test "the session's gate waits for a submission out when the session ended" {
                let ended = SessionView.Ended Shared.Types.SessionEnding.WrongPinLimit
                let submitting = SigningMachine.SigningView.Submitting SigningMachineTests.Fixtures.plan

                isGated Startup.Started ended submitting |> Expect.isFalse "the outcome first"

                isGated Startup.Started ended SigningMachine.SigningView.Idle
                |> Expect.isTrue "then the gate"

                isGated Startup.Starting SessionView.Anonymous submitting
                |> Expect.isTrue "the start-up gate does not wait"
            }

            test "every start-up term has its own English" {
                for term in
                    [
                        Terms.``Startup Gate Starting``
                        Terms.``Startup Gate Starting Text``
                        Terms.``Startup Gate Failed``
                        Terms.``Startup Gate Failed Text``
                        Terms.``Startup Load Localization``
                        Terms.``Startup Load Normal Values``
                        Terms.``Startup Load Bolus Medication``
                        Terms.``Startup Load Continuous Medication``
                        Terms.``Startup Load Products``
                    ] do
                    english term |> Expect.notEqual $"default for {term}" $"{term}"
            }
        ]
