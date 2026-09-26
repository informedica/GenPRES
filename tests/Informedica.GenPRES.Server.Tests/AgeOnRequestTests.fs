/// The age on each request. An identified Session holds the age its patient opened on, and
/// every request of that Session is evaluated at that age, whatever age the client sent: a
/// computing request through Compute.bound, a signing request through its own member. No
/// clock is read. A request without a Session, or in a Session whose EHR data names no
/// patient, is unchanged field for field.
module Informedica.GenPRES.Server.Tests.AgeOnRequestTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters

module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CorePatientAge = Informedica.GenCore.Lib.Patients.PatientAge


/// The date of the open: a September day, half a year past the stub patient's tenth birthday.
let today = DateTime(2026, 9, 26)


/// The Session's age at the open: ten years, six months, one week and four days.
let sessionAge = Shared.Models.Patient.Age.fromDays 3841


let ehr = StubPatientData.data "p1"


/// EHR data with an age value in place of a birthdate: no identity.
let unidentified =
    { ehr with Patient = { ehr.Patient with Age = CoreAgeValue.ten |> CorePatientAge.ageValue } }


let prescriber: UserContext =
    {
        UserId = "prescriber"
        DisplayName = "Dr. Stub"
        Role = UserRole.Prescriber
    }


/// A Session as the store holds it after an open, seen at the open.
let record
    (sid: string)
    (ehr: Informedica.GenForm.Lib.Types.EhrPatientData option)
    (patient: Informedica.GenForm.Lib.Types.Patient option)
    : Session.SessionRecord
    =
    {
        Opened =
            {
                User = Some prescriber
                PatientId = Some "p1"
                EhrData = ehr
                Patient = patient
                Measured = Measurements.none
                OpenedToken = Some(OpenedToken $"opened-{sid}")
                KeyThumbprint = Some "t"
                Head = None
            }
        Login = Some prescriber.UserId
        OpenedWith = None
        OpenedAt = today
        Seen = today
    }


/// Three Sessions: one identified, one on EHR data without an identity, one without EHR data
/// on a patient the user entered.
let identified = "s-identified"

let anonymous = "s-anonymous"

let entered = "s-entered"


let state =
    { Session.emptyState with
        Sessions =
            Map.ofList
                [
                    identified, record identified (Some ehr) (Some(StubPatientData.port.patient today ehr))
                    anonymous,
                    record anonymous (Some unidentified) (Some(StubPatientData.port.patient today unidentified))
                    entered, record entered None (Patient.parse StubPatientData.patient |> Result.toOption)
                ]
    }


/// The session port of the tests over the state: seen through the machine at the clock given,
/// the plan of a challenge or a submission captured.
let portOver (clock: unit -> DateTime) (captured: ResizeArray<Informedica.GenOrder.Lib.Types.OrderPlan>) =
    let st = ref state

    { sessionNone with
        challenge =
            fun _ (plan, _, _) ->
                async {
                    captured.Add plan
                    return SigningOutcome.ChallengeIssued "c-1"
                }
        submit =
            fun _ signature ->
                async {
                    captured.Add signature.Plan
                    return SigningOutcome.Refused SigningRefusal.PinLimit
                }
        seen =
            fun sid opened draft ->
                async {
                    let next, told, _ = Session.seen (clock ()) sid opened draft st.Value
                    st.Value <- next
                    return told
                }
        age = fun sid -> async { return Ok(Session.age sid st.Value) }
    }


let envOver port =
    { makeEnv (formularyAlwaysOk Shared.Models.Formulary.empty) (orderContextAlwaysOk emptyCtx) with session = port }


let cookieOf (sid: string option) : SessionCookie =
    {
        read = fun () -> sid
        write = ignore
        delete = ignore
    }


/// The stub's patient as a client would send it at the wrong age, with a weight it measured.
let sent: Patient =
    { StubPatientData.patient with
        Age = Some(Shared.Models.Patient.Age.fromDays (5 * 365))
        Weight = { StubPatientData.patient.Weight with Measured = Some 12000<gram> }
    }


let context = { Shared.Models.OrderContext.empty with Patient = sent }

let plan = Shared.Models.OrderPlan.create sent [| context; context |]

let form = { Shared.Models.Formulary.empty with Patient = Some sent }


/// The command a computing request reaches its handler as, run through bound over the Session
/// named, at the clock given.
let boundOver (clock: unit -> DateTime) (sid: string option) aged (cmd: 'cmd) : 'cmd =
    let seen: 'cmd option ref = ref None
    let env = envOver (portOver clock (ResizeArray()))

    let request =
        {
            Opened = sid |> Option.map (fun sid -> OpenedToken $"opened-{sid}")
            Command = cmd
        }

    let handler cmd =
        async {
            seen.Value <- Some cmd
            return Ok()
        }

    Compute.bound env (cookieOf sid) (fun _ -> "test") (fun _ -> Gate.Open) aged (fun _ -> None) handler request
    |> Async.RunSynchronously
    |> ignore

    seen.Value
    |> Option.defaultWith (fun () -> failtest "the handler was not reached")


let over sid aged cmd = boundOver (fun () -> today) sid aged cmd


let ageInDays (pat: Informedica.GenForm.Lib.Types.Patient) =
    pat.Age
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


/// The plan the session port was asked over, for a signing command of the Session named.
let signedOver (sid: string option) (cmd: SigningCommand) =
    let captured = ResizeArray()
    let env = envOver (portOver (fun () -> today) captured)

    SigningCommand.processCmd env (cookieOf sid) cmd
    |> Async.RunSynchronously
    |> ignore

    captured |> Seq.tryHead


[<Tests>]
let tests =
    testList
        "the age on each request"
        [
            testList
                "the age a Session holds"
                [
                    test "an identified Session holds the age its patient opened on" {
                        Session.age identified state
                        |> Expect.equal "ten years, six months, one week and four days" (Some sessionAge)
                    }

                    test "no age without an identity, without EHR data, or without a Session" {
                        [ anonymous; entered; "nobody" ]
                        |> List.map (fun sid -> Session.age sid state)
                        |> Expect.allEqual "none, every one" None
                    }

                    test "seen tells the age beside the notice" {
                        let _, told, writes =
                            Session.seen today identified (Some(OpenedToken $"opened-{identified}")) None state

                        (told, writes |> List.length)
                        |> Expect.equal "no notice, the age, and the one touch" ((None, Some sessionAge), 1)
                    }
                ]

            testList
                "an identified request is evaluated at the Session's age"
                [
                    test "the order context's patient, its measured weight kept" {
                        let _, ctx =
                            over
                                (Some identified)
                                OrderContextCommand.aged
                                (OrderContextCommand.UpdateOrderContext, context)

                        (ctx.Patient.Age, ctx.Patient.Weight.Measured)
                        |> Expect.equal "the Session's age, the weight as measured" (Some sessionAge, Some 12000<gram>)
                    }

                    test "the plan's patient and that of every context, on every plan command" {
                        [
                            OrderPlanCommand.Recalculate plan
                            OrderPlanCommand.Navigate(plan, "1", OrderContextCommand.UpdateOrderContext, context)
                            OrderPlanCommand.AddOrderContext(plan, context)
                            OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN)
                            OrderPlanCommand.RemoveOrderContexts(plan, [| "1" |])
                            OrderPlanCommand.Open(sent, [| context |])
                        ]
                        |> List.map (fun cmd ->
                            let patients =
                                match over (Some identified) OrderPlanCommand.aged cmd with
                                | OrderPlanCommand.Recalculate plan
                                | OrderPlanCommand.NewOrderContext(plan, _)
                                | OrderPlanCommand.RemoveOrderContexts(plan, _) -> Patient.ofPlan plan
                                | OrderPlanCommand.Navigate(plan, _, _, ctx)
                                | OrderPlanCommand.AddOrderContext(plan, ctx) -> Patient.ofPlan plan @ [ ctx.Patient ]
                                | OrderPlanCommand.Open(pat, contexts) ->
                                    pat :: (contexts |> Array.map _.Patient |> Array.toList)

                            patients |> List.map _.Age |> List.distinct
                        )
                        |> Expect.allEqual "the Session's age on every patient" [ Some sessionAge ]
                    }

                    test "the formulary's patient where it has one; without one it stays unfiltered" {
                        ((over (Some identified) FormularyCommand.aged form).Patient |> Option.bind _.Age,
                         (over (Some identified) FormularyCommand.aged Shared.Models.Formulary.empty).Patient)
                        |> Expect.equal "the Session's age; none" (Some sessionAge, None)
                    }

                    test "the plan passed to the session port for a challenge and for a submission carries it" {
                        let token = OpenedToken $"opened-{identified}"

                        let submission: Submission =
                            {
                                Plan = plan
                                Opened = token
                                Challenge = "c-1"
                                Pin = "1234"
                                IdemKey = "k-1"
                            }

                        [
                            SigningCommand.RequestSignChallenge(plan, token, None)
                            SigningCommand.Submit submission
                        ]
                        |> List.map (fun cmd ->
                            match signedOver (Some identified) cmd with
                            | None -> failtest "the port was not asked"
                            | Some parsed ->
                                parsed.Patient
                                :: (parsed.Contexts |> Array.map _.Context.Patient |> Array.toList)
                                |> List.map ageInDays
                                |> List.distinct
                        )
                        |> Expect.allEqual "3841 days on the plan's patient and on every context's" [ Some 3841N ]
                    }

                    test "two requests of one Session use one age, whatever the clock does between them" {
                        let clock = ref today

                        let ageOf () =
                            let _, ctx =
                                boundOver
                                    (fun () -> clock.Value)
                                    (Some identified)
                                    OrderContextCommand.aged
                                    (OrderContextCommand.UpdateOrderContext, context)

                            ctx.Patient.Age

                        let first = ageOf ()
                        clock.Value <- today.AddDays 400.0
                        let second = ageOf ()

                        (first, second)
                        |> Expect.equal "the age of the open, both times" (Some sessionAge, Some sessionAge)
                    }
                ]

            testList
                "a request without an identity is unchanged"
                [
                    test "without a Session, or in a Session without an identity: field for field" {
                        for sid in [ None; Some anonymous; Some entered; Some "nobody" ] do
                            over sid OrderContextCommand.aged (OrderContextCommand.UpdateOrderContext, context)
                            |> Expect.equal
                                $"the order context as sent, %A{sid}"
                                (OrderContextCommand.UpdateOrderContext, context)

                            over sid OrderPlanCommand.aged (OrderPlanCommand.Recalculate plan)
                            |> Expect.equal $"the plan as sent, %A{sid}" (OrderPlanCommand.Recalculate plan)

                            over sid FormularyCommand.aged form
                            |> Expect.equal $"the formulary as sent, %A{sid}" form
                    }

                    test "the plan of a signing request as sent" {
                        let token = OpenedToken $"opened-{entered}"

                        match signedOver (Some entered) (SigningCommand.RequestSignChallenge(plan, token, None)) with
                        | None -> failtest "the port was not asked"
                        | Some parsed ->
                            parsed.Patient
                            |> ageInDays
                            |> Expect.equal "the five years the client sent" (Some 1825N)
                    }

                    test "the families without a patient are the identity" {
                        (ParenteraliaCommand.aged (Some sessionAge) Shared.Models.Parenteralia.empty,
                         InteractionCommand.aged (Some sessionAge) InteractionCommand.GetDrugNames)
                        |> Expect.equal "as given" (Shared.Models.Parenteralia.empty, InteractionCommand.GetDrugNames)
                    }
                ]
        ]
