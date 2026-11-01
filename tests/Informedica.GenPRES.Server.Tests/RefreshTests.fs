/// The refresh: the EHR read again for the Session and projected at the time of the refresh
/// under a fresh OpenedToken, the standing challenge spent and the notice dropped; the order plan version the Session opened with stays. No reading
/// leaves the patient as it was.
module Informedica.GenPRES.Server.Tests.RefreshTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
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

    let opened: OpenedSession =
        let ehr = StubPatientData.data "p1"

        {
            User = Some prescriber
            PatientId = Some "p1"
            EhrData = Some ehr
            OpenedToken = Some(OpenedToken "opened-s1")
            KeyThumbprint = Some "t"
            Head = None
            Act = SessionAct.Opened
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
                let _, opened, _ = stateWith (record opened) |> refreshed port

                reads.Value |> Expect.equal "read once" 1

                opened
                |> Option.bind (Session.sessionPatient later port)
                |> Expect.equal
                    "the projection at the refresh"
                    (Some(StubPatientData.port.patient later (StubPatientData.data "p1")))
            }

            test "the age is the one at the refresh" {
                let before = stateWith (record opened)
                let after, _, _ = before |> refreshed StubPatientData.port

                let months date (state: Session.State) =
                    state.Sessions["s1"].Opened
                    |> Session.sessionPatient date StubPatientData.port
                    |> Option.bind (Informedica.GenForm.Lib.Patient.Dto.toDto >> Patient.toModel >> _.Age)
                    |> Option.map _.Months

                before
                |> months openedAt
                |> Expect.equal "ten years at the open" (Some 0<month>)
                after
                |> months later
                |> Expect.equal "six months more at the refresh" (Some 6<month>)
            }

            test "a refresh with no EHR data changes nothing and writes no row" {
                let signed = { opened with Act = SessionAct.Signed }
                let nothing = { StubPatientData.port with read = fun _ -> None }
                let before = stateWith (record signed)
                let state, after, writes = before |> refreshed nothing

                after
                |> Expect.equal "the Session as it was, token and act included" (Some signed)
                state.Sessions["s1"].Opened |> Expect.equal "in the store too" signed

                writes
                |> List.exists (
                    function
                    | Session.RecordOpenedWith _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "no row: the newest still says what the Session stands on"
            }

            test "a refresh that reads names itself as the latest act" {
                let signed = { opened with Act = SessionAct.Signed }
                let _, after, _ = stateWith (record signed) |> refreshed StubPatientData.port

                after |> Option.map _.Act |> Expect.equal "the act" (Some SessionAct.Refreshed)
            }

            test "a fresh token, the version kept, the challenge spent and the notice dropped" {
                let challenge: Session.Challenge =
                    {
                        Nonce = "n-1"
                        Digest = "d"
                        Ehr = None
                        Expiry = later.AddMinutes 1.0
                    }

                let state =
                    { stateWith { record opened with OpenedWith = Some "old" } with
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

                let anonymous = { opened with User = None }
                let _, none, _ = stateWith (record anonymous) |> refreshed StubPatientData.port
                none |> Expect.isNone "anonymous"

                let noPatient = { opened with PatientId = None }
                let _, none, _ = stateWith (record noPatient) |> refreshed StubPatientData.port
                none |> Expect.isNone "no Patient"
            }
        ]
