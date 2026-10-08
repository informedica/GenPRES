/// The refresh: the EHR read again for the Session and projected at the time of the refresh
/// with what the user measured over it under a fresh OpenedToken, the standing challenge spent
/// and the notice dropped; the order plan version the Session opened with stays. No reading
/// leaves the patient as it was.
module Informedica.GenPRES.Server.Tests.RefreshTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open ServerApi


module Fixtures =

    let openedAt = DateTime(2026, 3, 15, 9, 0, 0)
    let later = DateTime(2026, 9, 27, 9, 0, 0)

    let prescriber: UserContext =
        {
            UserId = "prescriber"
            DisplayName = "Dr. Stub"
            Role = UserRole.Prescriber
        }

    /// The stub EHR, counting its reads.
    let counting () =
        let reads = ref 0

        reads,
        { StubPatientData.port with
            read =
                fun pid ->
                    reads.Value <- reads.Value + 1
                    StubPatientData.port.read pid
        }

    let opened (measured: Measurements) : OpenedSession =
        let ehr = StubPatientData.data "p1"

        {
            User = Some prescriber
            PatientId = Some "p1"
            EhrData = Some ehr
            Patient = Some(Session.merged openedAt StubPatientData.port measured ehr)
            Measured = measured
            OpenedToken = Some(OpenedToken "opened-s1")
            KeyThumbprint = Some "t"
            Head = None
        }

    let record (opened: OpenedSession) : Session.SessionRecord =
        {
            Opened = opened
            Login = Some prescriber.UserId
            OpenedWith = None
            OpenedAt = openedAt
            Seen = openedAt
        }

    let stateWith (r: Session.SessionRecord) = { Session.emptyState with Sessions = Map [ ("s1", r) ] }

    let weighed =
        { Measurements.none with
            Weight =
                Some
                    {
                        Value = Some 34000<gram>
                        At = openedAt
                    }
        }

    let ids () =
        let n = ref 0

        fun () ->
            n.Value <- n.Value + 1
            $"%i{n.Value}"

    let refreshed (port: PatientDataPort) (state: Session.State) = Session.refresh later (ids ()) port "s1" state


open Fixtures


[<Tests>]
let tests =
    testList
        "Refresh"
        [
            test "a refresh reads the EHR once and projects at the date of the refresh" {
                let reads, port = counting ()
                let _, opened, _ = stateWith (record (opened Measurements.none)) |> refreshed port

                reads.Value |> Expect.equal "read once" 1

                opened
                |> Option.bind _.Patient
                |> Expect.equal
                    "the merge at the refresh"
                    (Some(Session.merged later StubPatientData.port Measurements.none (StubPatientData.data "p1")))
            }

            test "the age is the one at the refresh" {
                let before = stateWith (record (opened Measurements.none))
                let after, _, _ = before |> refreshed StubPatientData.port

                Session.age "s1" before
                |> Option.map _.Months
                |> Expect.equal "ten years at the open" (Some 0<month>)

                Session.age "s1" after
                |> Option.map _.Months
                |> Expect.equal "six months more at the refresh" (Some 6<month>)
            }

            test "the measurements stand" {
                let _, opened, _ = stateWith (record (opened weighed)) |> refreshed StubPatientData.port

                opened
                |> Option.map _.Measured
                |> Expect.equal "the user's weight kept" (Some weighed)

                opened
                |> Option.bind _.Patient
                |> Option.bind _.Weight
                |> Expect.equal
                    "and on the patient"
                    ((Session.merged later StubPatientData.port weighed (StubPatientData.data "p1")).Weight)
            }

            test "a refresh with no EHR data leaves the patient as it was" {
                let before = opened Measurements.none
                let nothing = { StubPatientData.port with read = fun _ -> None }
                let _, after, _ = stateWith (record before) |> refreshed nothing

                after |> Option.bind _.Patient |> Expect.equal "the patient" before.Patient
                after |> Option.bind _.EhrData |> Expect.equal "the EHR data" before.EhrData
            }

            test "a fresh token, the version kept, the challenge spent and the notice dropped" {
                let challenge: Session.Challenge =
                    {
                        Nonce = "n-1"
                        Digest = "d"
                        Ehr = None
                        Reading = None
                        Expiry = later.AddMinutes 1.0
                    }

                let state =
                    { stateWith { record (opened Measurements.none) with OpenedWith = Some "old" } with
                        Challenges = Map [ ("s1", challenge) ]
                    }

                let state, opened, writes = state |> refreshed StubPatientData.port

                opened
                |> Option.bind _.OpenedToken
                |> Expect.equal "fresh" (Some(OpenedToken "opened-1"))

                state.Sessions["s1"].OpenedWith
                |> Expect.equal "the version the Session opened with" (Some "old")

                state.Challenges |> Expect.isEmpty "spent"
                state.Notices |> Expect.isEmpty "dropped"

                writes
                |> List.exists (
                    function
                    | Session.SpendChallenge("s1", "n-1", _) -> true
                    | _ -> false
                )
                |> Expect.isTrue "the challenge spent is written"

                writes
                |> List.exists (
                    function
                    | Session.RecordOpenedWith("s1", _, _) -> true
                    | _ -> false
                )
                |> Expect.isTrue "what the Session opened with is written"
            }

            test "nothing to refresh without a Session, anonymously or without a Patient" {
                let _, none, _ = Session.emptyState |> refreshed StubPatientData.port
                none |> Expect.isNone "no Session"

                let anonymous = { opened Measurements.none with User = None }
                let _, none, _ = stateWith (record anonymous) |> refreshed StubPatientData.port
                none |> Expect.isNone "anonymous"

                let noPatient = { opened Measurements.none with PatientId = None }
                let _, none, _ = stateWith (record noPatient) |> refreshed StubPatientData.port
                none |> Expect.isNone "no Patient"
            }
        ]
