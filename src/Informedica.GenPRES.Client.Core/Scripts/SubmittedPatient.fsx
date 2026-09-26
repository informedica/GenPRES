// The patient after the sign, on the client. The Submitted answer carries the Session's patient
// beside the fresh token: the age computed again at the sign, the projection and estimate
// over it, the measured values kept. The signing machine hands both on with the token, and the
// session machine takes the patient as it does at a resume: into the Session it holds, and to
// the panel through SetPatient, so that everything derived from the patient follows.
//
// The two machines keep their state records private, so this script does not copy their
// transitions. It states the two arms that change as the functions the transitions will call,
// over the public constructors and the contract, and tests them; the migration inlines them.
//
// Accepting a data notice needs no change here: the notice's data is the merge as the server
// builds it, at the date of the open, over what the user measured, and the accept already
// sets the draft to it and challenges over it.
#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"

open System
open Expecto
open Expecto.Flip
open Shared.Types

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


/// The effect the signing machine will carry: the token re-minted over the new head, with the
/// Session's patient after the sign, both from the one Submitted answer.
[<RequireQualifiedAccess>]
type SigningEffect =
    | RenewToken of OpenedToken * Patient
    | TellSigned of SignedOrderPlan * askedOver: PlanWorkPolicy.PlanWork


module SigningMachine =

    /// The arm of the transition on a Submitted answer to the Submission under way: idle, the
    /// token with the patient to the Session, the signature told over the work it was asked
    /// over.
    let submitted (signed: SignedOrderPlan) (token: OpenedToken) (patient: Patient) (askedOver: PlanWorkPolicy.PlanWork) =
        SigningMachine.SigningState.idle,
        [
            SigningEffect.RenewToken(token, patient)
            SigningEffect.TellSigned(signed, askedOver)
        ]


module SessionMachine =

    open SessionMachine

    /// The Session after a signature: the token the next signature has to present, and the
    /// patient as the Session now holds it, in the context it opened with. A Session without
    /// a patient context signs nothing, so there is no context to fill.
    let renewed (token: OpenedToken) (patient: Patient) (session: SessionOpened) : SessionOpened =
        { session with
            OpenedToken = Some token
            PatientContext = session.PatientContext |> Option.map (fun c -> { c with Patient = Some patient })
        }


    /// The arm of the transition on TokenRenewed from Open with nothing under way: the Session
    /// renewed, the notice that the record moved on kept, and the patient to the panel as at a
    /// resume.
    let tokenRenewed (token: OpenedToken) (patient: Patient) (session: SessionOpened) (movedOn: OrderPlanHead option) =
        SessionState.opened (renewed token patient session) movedOn, [ SessionEffect.SetPatient(Some patient) ]


module Fixtures =

    let patient = Shared.Models.Patient.empty
    // the age as the Session computed it again at the sign: a day older
    let aged =
        { patient with
            Age =
                Some
                    { Shared.Models.Patient.Age.ageZero with
                        Age.Years = 10<year>
                        Age.Days = 1<day>
                    }
        }

    let prescriber =
        {
            UserId = "prescriber"
            DisplayName = "Stub Prescriber"
            Role = UserRole.Prescriber
        }

    let head =
        {
            Id = "plan-1"
            No = 1
            By = prescriber
            SignedAt = DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc)
        }

    let signed: SignedOrderPlan =
        {
            Head = head
            PatientId = "stub-patient"
            Base = None
            OrderContexts = [||]
            Patient = patient
            Identity = None
            Verified = true
        }

    let identity =
        {
            Name = "Stub Testpatiënt"
            BirthYear = 2016
            BirthMonth = 3
            BirthDay = 15
        }

    let session: SessionOpened =
        {
            User = Some prescriber
            PatientContext =
                Some
                    {
                        PatientId = "stub-patient"
                        Identity = Some identity
                        Patient = Some patient
                    }
            OpenedToken = Some(OpenedToken "t1")
            KeyThumbprint = Some "thumb"
            Head = None
        }

    let work = PlanWorkPolicy.PlanWork.Changed 1


open Fixtures


let tests =
    testList
        "the patient after the sign"
        [
            test "the signing machine hands the token and the patient on together, and tells the signature" {
                SigningMachine.submitted signed (OpenedToken "t2") aged work
                |> Expect.equal
                    "idle, the token with the patient, the signature over the work"
                    (SigningMachine.SigningState.idle,
                     [
                         SigningEffect.RenewToken(OpenedToken "t2", aged)
                         SigningEffect.TellSigned(signed, work)
                     ])
            }

            test "the session machine takes the patient into the context it holds, with the token" {
                let renewed = SessionMachine.renewed (OpenedToken "t2") aged session

                renewed.OpenedToken |> Expect.equal "the token" (Some(OpenedToken "t2"))

                renewed.PatientContext
                |> Option.bind _.Patient
                |> Expect.equal "the patient after the sign" (Some aged)

                renewed.PatientContext
                |> Option.bind _.Identity
                |> Expect.equal "the identity kept" (Some identity)

                { renewed with
                    OpenedToken = session.OpenedToken
                    PatientContext = session.PatientContext
                }
                |> Expect.equal "nothing else moved" session
            }

            test "the patient goes to the panel as at a resume, and the moved-on notice is kept" {
                let state, effects =
                    SessionMachine.tokenRenewed (OpenedToken "t2") aged session (Some head)

                let _, atResume = SessionMachine.SessionState.onOpened (SessionMachine.renewed (OpenedToken "t2") aged session)

                effects |> Expect.equal "SetPatient with the patient" [ SessionMachine.SessionEffect.SetPatient(Some aged) ]

                atResume
                |> List.head
                |> Expect.equal "the same SetPatient a resume gives" (effects |> List.head)

                state
                |> SessionMachine.SessionState.movedOn
                |> Expect.equal "the notice kept" (Some head)

                state
                |> SessionMachine.SessionState.session
                |> Option.bind _.PatientContext
                |> Option.bind _.Patient
                |> Expect.equal "the Session holds the patient" (Some aged)
            }

            test "a Session without a patient context gets the token and no patient" {
                let none = { session with PatientContext = None }

                SessionMachine.renewed (OpenedToken "t2") aged none
                |> Expect.equal "the token only" { none with OpenedToken = Some(OpenedToken "t2") }
            }

            test "the existing machine on a Submitted answer still ends idle and tells the signature" {
                let submitting = SigningMachine.SigningState.submitting "c-1" (Shared.Models.OrderPlan.create patient [||]) "k-1" work

                let state, effects =
                    SigningMachine.SigningState.transition
                        (SigningMachine.SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Submitted(signed, OpenedToken "t2", aged))))
                        submitting

                state |> Expect.equal "idle" SigningMachine.SigningState.idle

                effects
                |> List.last
                |> Expect.equal "the signature told" (SigningMachine.SigningEffect.TellSigned(signed, work))
            }
        ]


runTestsWithCLIArgs [] [||] tests
