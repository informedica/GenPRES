/// The patients on each request. An identified Session holds the age its patient opened on; the
/// patient change alone puts it on the patient, whatever age the client sent, and gives the
/// patient the estimates of weight and height it lacks. Every other request, signing included,
/// is computed with every patient as sent: no context's patient is ever rewritten. No clock is
/// read.
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
let boundOver (clock: unit -> DateTime) (sid: string option) (cmd: 'cmd) : 'cmd =
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

    Compute.bound env (cookieOf sid) (fun _ -> "test") (fun _ -> Gate.Open) (fun _ -> None) handler request
    |> Async.RunSynchronously
    |> ignore

    seen.Value
    |> Option.defaultWith (fun () -> failtest "the handler was not reached")


let over sid cmd = boundOver (fun () -> today) sid cmd


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


/// Normal values of both sexes at five and at ten years: 18 kg and 110 cm, 32 kg and 140 cm.
let tables: NormalValues =
    let row sex age p3 mean p97 = Shared.Models.NormalValues.create sex age p3 mean p97

    {
        Weights =
            [
                for sex in [ "M"; "F" ] do
                    row sex 5. 15. 18. 22.
                    row sex 10. 25. 32. 45.
            ]
        Heights =
            [
                for sex in [ "M"; "F" ] do
                    row sex 5. 100. 110. 120.
                    row sex 10. 130. 140. 150.
            ]
        NeoWeights = []
        NeoHeights = []
    }


let loaded () = Some tables


/// The normal values, counting how often they are asked.
let counting (nv: NormalValues option) =
    let count = ref 0

    let ask () =
        count.Value <- count.Value + 1
        nv

    ask, count


/// A patient with an age in years and nothing else.
let ageOnly years : Patient =
    { Shared.Models.Patient.empty with Age = Some(Shared.Models.Patient.Age.fromDays (years * 365)) }


let weightAndHeight (pat: Patient) =
    pat |> Shared.Models.Patient.getWeight, pat |> Shared.Models.Patient.getHeight


/// The command a computing request reaches its handler as, with the normal values given.
let estimatedOver normalValues (sid: string option) (cmd: 'cmd) : 'cmd =
    let seen: 'cmd option ref = ref None

    let env = { envOver (portOver (fun () -> today) (ResizeArray())) with normalValues = normalValues }

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

    Compute.bound env (cookieOf sid) (fun _ -> "test") (fun _ -> Gate.Open) (fun _ -> None) handler request
    |> Async.RunSynchronously
    |> ignore

    seen.Value
    |> Option.defaultWith (fun () -> failtest "the handler was not reached")


/// The answer of the order-context member over a context, without a Session, with the normal
/// values given.
let evaluatedWith normalValues (ctx: OrderContext) =
    let env = { envOver (portOver (fun () -> today) (ResizeArray())) with normalValues = normalValues }

    Compute.bound
        env
        (cookieOf None)
        (fun _ -> "test")
        (fun _ -> Gate.Open)
        (fun _ -> None)
        (OrderContextCommand.processCmd env)
        {
            Opened = None
            Command = OrderContextCommand.SeedFilter(Shared.Types.SeedSource.Reload, None, None, None, None, None), ctx
        }
    |> Async.RunSynchronously


/// The patient the identified Session resumes on, as the client gets it, after the measured
/// weight and height were cleared.
let resumedAfterClearing () : Patient =
    let cleared: Measured<_> =
        {
            Value = None
            At = today
        }

    { (state.Sessions |> Map.find identified).Opened with
        Measured =
            { Measurements.none with
                Weight = Some cleared
                Height = Some cleared
            }
    }
    |> SessionMapper.toOpened true
    |> _.PatientContext
    |> Option.bind _.Patient
    |> Option.defaultWith (fun () -> failtest "the Session resumes without a patient")


/// The patient a patient change answers in the Session named, at the clock given, with the
/// normal values given.
let changedAt (clock: unit -> DateTime) normalValues (sid: string option) (pat: Patient) =
    let env = { envOver (portOver clock (ResizeArray())) with normalValues = normalValues }

    Compute.bound
        env
        (cookieOf sid)
        PatientCommand.toString
        (fun _ -> Gate.Open)
        PatientCommand.patientOf
        (PatientCommand.processCmd env (cookieOf sid))
        {
            Opened = sid |> Option.map (fun sid -> OpenedToken $"opened-{sid}")
            Command = PatientCommand.ChangePatient pat
        }
    |> Async.RunSynchronously
    |> Result.map _.Response
    |> Result.defaultWith (fun errs -> failtest $"%A{errs}")


/// The answer to a signing command and the plan the session port was asked over, with the
/// normal values given.
let signedWith normalValues (sid: string option) (cmd: SigningCommand) =
    let captured = ResizeArray()

    let env = { envOver (portOver (fun () -> today) captured) with normalValues = normalValues }

    let response = SigningCommand.processCmd env (cookieOf sid) cmd |> Async.RunSynchronously

    response, captured |> Seq.tryHead


[<Tests>]
let tests =
    testList
        "the patients on each request"
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

                    test "seen marks the Session and tells no age: only the patient change asks it" {
                        let _, told, writes =
                            Session.seen today identified (Some(OpenedToken $"opened-{identified}")) None state

                        (told, writes |> List.length)
                        |> Expect.equal "no notice, and the one touch" (None, 1)
                    }
                ]

            testList
                "the patient change sets the age"
                [
                    test "an identified patient gets the Session's age, whatever age the client sent" {
                        (changedAt (fun () -> today) loaded (Some identified) sent).Age
                        |> Expect.equal "the Session's age" (Some sessionAge)
                    }

                    test "two changes of one Session use one age, whatever the clock does between them" {
                        let clock = ref today
                        let first = (changedAt (fun () -> clock.Value) loaded (Some identified) sent).Age
                        clock.Value <- today.AddDays 400.0
                        let second = (changedAt (fun () -> clock.Value) loaded (Some identified) sent).Age

                        (first, second)
                        |> Expect.equal "the age of the open, both times" (Some sessionAge, Some sessionAge)
                    }

                    test "without an identity the client's age stays" {
                        for sid in [ None; Some anonymous; Some entered; Some "nobody" ] do
                            (changedAt (fun () -> today) loaded sid sent).Age
                            |> Expect.equal $"the client's age, %A{sid}" sent.Age
                    }
                ]

            testList
                "every other request leaves every patient as sent"
                [
                    test "the order context's patient, in an identified Session and without one" {
                        for sid in [ Some identified; None; Some anonymous; Some entered; Some "nobody" ] do
                            over
                                sid
                                (OrderContextCommand.SeedFilter(
                                    Shared.Types.SeedSource.Reload,
                                    None,
                                    None,
                                    None,
                                    None,
                                    None
                                 ),
                                 context)
                            |> Expect.equal
                                $"the order context as sent, %A{sid}"
                                (OrderContextCommand.SeedFilter(
                                    Shared.Types.SeedSource.Reload,
                                    None,
                                    None,
                                    None,
                                    None,
                                    None
                                 ),
                                 context)
                    }

                    test "the plan's patient and that of every context, on every plan command" {
                        [
                            OrderPlanCommand.Recalculate plan
                            OrderPlanCommand.Navigate(
                                plan,
                                "1",
                                OrderContextCommand.SeedFilter(
                                    Shared.Types.SeedSource.Reload,
                                    None,
                                    None,
                                    None,
                                    None,
                                    None
                                ),
                                context
                            )
                            OrderPlanCommand.AddOrderContext(plan, context)
                            OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN)
                            OrderPlanCommand.RemoveOrderContexts(plan, [| "1" |])
                            OrderPlanCommand.Open(sent, [| context |])
                        ]
                        |> List.iter (fun cmd ->
                            over (Some identified) cmd
                            |> Expect.equal $"the plan command as sent, %A{cmd}" cmd
                        )
                    }

                    test "the formulary's patient" {
                        over (Some identified) form |> Expect.equal "the formulary as sent" form
                    }

                    test "the plan passed to the session port for a challenge and for a submission" {
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
                        |> Expect.allEqual "the five years the client sent, on every patient" [ Some 1825N ]
                    }
                ]

            testList
                "the estimates a patient lacks"
                [
                    test "an age alone gets a weight and a height" {
                        ageOnly 10
                        |> Patient.estimate loaded
                        |> weightAndHeight
                        |> Expect.equal "32 kg and 140 cm" (Some 32000<gram>, Some 140<cm>)
                    }

                    test "a measured weight is kept, the height estimated" {
                        { ageOnly 10 with Weight = { (ageOnly 10).Weight with Measured = Some 28000<gram> } }
                        |> Patient.estimate loaded
                        |> fun pat -> pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Estimated
                        |> Expect.equal
                            "the measured weight, no weight estimate, the height estimated"
                            (Some 28000<gram>, None, Some 140<cm>)
                    }

                    test "a client estimate of the weight is kept when only the height is filled in" {
                        { ageOnly 10 with Weight = { (ageOnly 10).Weight with Estimated = Some 41000<gram> } }
                        |> Patient.estimate loaded
                        |> weightAndHeight
                        |> Expect.equal "the client's 41 kg, the server's 140 cm" (Some 41000<gram>, Some 140<cm>)
                    }

                    test "a patient with a weight and a height is not changed, nor are the normal values asked" {
                        let ask, count = counting (Some tables)
                        let pat = StubPatientData.patient

                        (pat |> Patient.estimate ask = pat, count.Value)
                        |> Expect.equal "the same patient, asked none" (true, 0)
                    }

                    test "without the normal values the patient stays as it is and the gate refuses it" {
                        let pat = ageOnly 10 |> Patient.estimate (fun () -> None)

                        (pat = ageOnly 10, pat |> Patient.patient)
                        |> Expect.equal "unchanged, refused" (true, Error [| Patient.noWeightAndHeight |])
                    }

                    test "empty tables leave the estimate out and the gate refuses" {
                        let empty =
                            {
                                Weights = []
                                Heights = []
                                NeoWeights = []
                                NeoHeights = []
                            }

                        ageOnly 10
                        |> Patient.estimate (fun () -> Some empty)
                        |> Patient.patient
                        |> Expect.equal "refused" (Error [| Patient.noWeightAndHeight |])
                    }

                    test "an unknown gender over tables with rows for one sex only is refused" {
                        let oneSex =
                            { tables with
                                Weights = tables.Weights |> List.filter (fun r -> r.Sex = "M")
                                Heights = tables.Heights |> List.filter (fun r -> r.Sex = "M")
                            }

                        ageOnly 10
                        |> Patient.estimate (fun () -> Some oneSex)
                        |> Patient.patient
                        |> Expect.equal "refused" (Error [| Patient.noWeightAndHeight |])
                    }

                    test "the patient change estimates at the Session's age, not the client's" {
                        changedAt (fun () -> today) loaded (Some identified) (ageOnly 5)
                        |> fun pat -> pat.Age, pat |> weightAndHeight
                        |> Expect.equal
                            "the Session's age, the ten-year estimate"
                            (Some sessionAge, (Some 32000<gram>, Some 140<cm>))
                    }

                    test "an age-only patient on another request is not estimated: the patient change is" {
                        let ctx = { Shared.Models.OrderContext.empty with Patient = ageOnly 10 }

                        estimatedOver
                            loaded
                            None
                            (OrderContextCommand.SeedFilter(
                                Shared.Types.SeedSource.Reload,
                                None,
                                None,
                                None,
                                None,
                                None
                             ),
                             ctx)
                        |> Expect.equal
                            "the context as sent"
                            (OrderContextCommand.SeedFilter(
                                Shared.Types.SeedSource.Reload,
                                None,
                                None,
                                None,
                                None,
                                None
                             ),
                             ctx)
                    }

                    test "a request other than a patient change never asks the normal values" {
                        let ask, count = counting (Some tables)
                        let ctx = { Shared.Models.OrderContext.empty with Patient = ageOnly 10 }

                        estimatedOver
                            ask
                            None
                            (OrderContextCommand.SeedFilter(
                                Shared.Types.SeedSource.Reload,
                                None,
                                None,
                                None,
                                None,
                                None
                             ),
                             ctx)
                        |> ignore

                        count.Value |> Expect.equal "asked none: asking may load" 0
                    }

                    test "a Session resumed after its weight and height were cleared gets them from the patient change" {
                        changedAt (fun () -> today) loaded (Some identified) (resumedAfterClearing ())
                        |> Patient.patient
                        |> Result.isOk
                        |> Expect.isTrue "a patient, its weight and height estimated"
                    }

                    test "a Session resumed after its weight and height were cleared is refused as sent" {
                        { Shared.Models.OrderContext.empty with Patient = resumedAfterClearing () }
                        |> evaluatedWith loaded
                        |> Result.mapError List.ofArray
                        |> Expect.equal "refused" (Error [ Patient.noWeightAndHeight ])
                    }

                    test "signing takes no estimate: the plan is signed as sent, whatever the normal values" {
                        // the challenge is a digest of the plan as sent; an estimate from tables
                        // reloaded before the submission would change the plan under it
                        let ctx = { Shared.Models.OrderContext.empty with Patient = ageOnly 10 }

                        let plan = Shared.Models.OrderPlan.create (ageOnly 10) [| ctx |]

                        let cmd = SigningCommand.RequestSignChallenge(plan, OpenedToken $"opened-{identified}", None)

                        [ (fun () -> None); loaded ]
                        |> List.map (fun nv -> signedWith nv (Some identified) cmd |> fst)
                        |> Expect.allEqual
                            "an age-only plan is refused, with or without the normal values"
                            (SigningResponse.Refused SigningRefusal.NoPatient)
                    }
                ]
        ]
