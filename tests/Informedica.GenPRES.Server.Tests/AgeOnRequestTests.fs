/// The patients on each request. The patient data update gives the patient the estimates of weight and
/// height it lacks. Every other request, signing included, is computed with every patient as sent:
/// no context's patient is ever rewritten. No clock is read.
module Informedica.GenPRES.Server.Tests.AgeOnRequestTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi
open Informedica.GenCore.Tests.CalculationWeightsAndHeightsFixture
open Informedica.GenPRES.Server.Tests.StubAdapterTests
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters

module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData


/// The date of the open: a September day, half a year past the day the stub patient is 3650 days old.
let today = DateTime(2026, 9, 26)


let ehr = StubEhrPatient.patient StubEhrPatient.measuredOn "p1"


/// The EHR data of an anonymous patient: an age value, no identity.
let unidentified =
    { ehr with
        Identity = None
        Data.Age = Some CoreAgeValue.ten
    }


let prescriber: UserContext =
    {
        UserId = "prescriber"
        DisplayName = "Dr. Stub"
        Role = UserRole.Prescriber
    }


/// A Session as the store holds it after an open, seen at the open.
let record (sid: string) (ehr: Informedica.GenCore.Lib.Patients.Patient option) : Session.SessionRecord =
    {
        Opened =
            {
                User = Some prescriber
                PatientId = Some "p1"
                EhrPatient = ehr
                OpenedToken = Some(OpenedToken $"opened-{sid}")
                KeyThumbprint = Some "t"
                Head = None
                Act = SessionAct.Opened
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
                    identified, record identified (Some ehr)
                    anonymous, record anonymous (Some unidentified)
                    entered, record entered None
                ]
    }


/// The session port of the tests over the state: seen through the machine at today,
/// the plan of a challenge or a submission captured.
let portOver (captured: ResizeArray<Informedica.GenOrder.Lib.Types.OrderPlan>) =
    let st = ref state

    { sessionNone with
        challenge =
            fun _ (plan, _, _) ->
                async {
                    captured.Add plan
                    return SigningOutcome.ChallengeIssued("c-1", false)
                }
        submit =
            fun _ signature ->
                async {
                    captured.Add signature.Plan
                    return SigningOutcome.Refused SigningRefusal.PinLimit
                }
        seen =
            fun sid opened ->
                async {
                    let next, told, _ = Session.seen today sid opened st.Value
                    st.Value <- next
                    return told
                }
    }


let envOver port =
    { makeEnv (formularyAlwaysOk Informedica.GenPRES.Shared.Models.Formulary.empty) (orderContextAlwaysOk emptyCtx) with
        session = port
    }


let cookieOf (sid: string option) : SessionCookie =
    {
        read = fun () -> sid
        write = ignore
        delete = ignore
    }


/// The stub's patient as a client would send it at another age than the Session's, with a weight it measured.
let sent: PatientData =
    { StubEhrPatient.contractPatient with
        Age = Some(Informedica.GenPRES.Shared.Models.PatientData.Age.create 5<year> None None None)
        Weight = { StubEhrPatient.contractPatient.Weight with Measured = Some 12000<gram> }
    }


let context = { Informedica.GenPRES.Shared.Models.OrderContext.empty with PatientData = sent }

let plan = Informedica.GenPRES.Shared.Models.OrderPlan.create sent [| context; context |]

let form = { Informedica.GenPRES.Shared.Models.Formulary.empty with PatientData = Some sent }


/// The command a computing request reaches its handler as, run through bound over the Session
/// named.
let over (sid: string option) (cmd: 'cmd) : 'cmd =
    let seen: 'cmd option ref = ref None
    let env = envOver (portOver (ResizeArray()))

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

    Compute.bound env (cookieOf sid) (fun _ -> "test") (fun _ -> Gate.Open) handler request
    |> Async.RunSynchronously
    |> ignore

    seen.Value
    |> Option.defaultWith (fun () -> failtest "the handler was not reached")


let ageInDays pat =
    pat
    |> CorePatientData.ageInDays
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


/// The plan the session port was asked over, for a signing command of the Session named.
let signedOver (sid: string option) (cmd: SigningCommand) =
    let captured = ResizeArray()
    let env = envOver (portOver captured)

    SigningCommand.processCmd env (cookieOf sid) cmd
    |> Async.RunSynchronously
    |> ignore

    captured |> Seq.tryHead


/// Normal values of both sexes at five and at ten years: 18 kg and 110 cm, 32 kg and 140 cm.
let tables: Informedica.GenCore.Lib.Patients.NormalValueTables =
    let row gender age p3 mean p97 : Informedica.GenCore.Lib.Patients.NormalValue<_, _> =
        {
            Gender = gender
            Age = age
            P3 = p3
            Mean = mean
            P97 = p97
        }

    let genders =
        [
            Informedica.GenCore.Lib.Patients.Male
            Informedica.GenCore.Lib.Patients.Female
        ]

    {
        Weights =
            [
                for g in genders do
                    row
                        g
                        5.<Informedica.GenCore.Lib.Measures.year>
                        15.<Informedica.GenCore.Lib.Measures.kg>
                        18.<Informedica.GenCore.Lib.Measures.kg>
                        22.<Informedica.GenCore.Lib.Measures.kg>
                    row
                        g
                        10.<Informedica.GenCore.Lib.Measures.year>
                        25.<Informedica.GenCore.Lib.Measures.kg>
                        32.<Informedica.GenCore.Lib.Measures.kg>
                        45.<Informedica.GenCore.Lib.Measures.kg>
            ]
        Heights =
            [
                for g in genders do
                    row
                        g
                        5.<Informedica.GenCore.Lib.Measures.year>
                        100.<Informedica.GenCore.Lib.Measures.cm>
                        110.<Informedica.GenCore.Lib.Measures.cm>
                        120.<Informedica.GenCore.Lib.Measures.cm>
                    row
                        g
                        10.<Informedica.GenCore.Lib.Measures.year>
                        130.<Informedica.GenCore.Lib.Measures.cm>
                        140.<Informedica.GenCore.Lib.Measures.cm>
                        150.<Informedica.GenCore.Lib.Measures.cm>
            ]
        NeoWeights = []
        NeoHeights = []
    }


let loaded () = Some(tables, calc)


/// The normal values, counting how often they are asked.
let counting nv =
    let count = ref 0

    let ask () =
        count.Value <- count.Value + 1
        nv

    ask, count


/// A patient with an age in years and nothing else.
let ageOnly years : PatientData =
    { Informedica.GenPRES.Shared.Models.PatientData.empty with
        Age = Some(Informedica.GenPRES.Shared.Models.PatientData.Age.create (years * 1<year>) None None None)
    }


let weightAndHeight (pat: PatientData) =
    pat |> Informedica.GenPRES.Shared.Models.PatientData.getWeight,
    pat |> Informedica.GenPRES.Shared.Models.PatientData.getHeight


/// The command a computing request reaches its handler as, with the normal values given.
let estimatedOver normalValues (sid: string option) (cmd: 'cmd) : 'cmd =
    let seen: 'cmd option ref = ref None

    let env = { envOver (portOver (ResizeArray())) with normalValues = normalValues }

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

    Compute.bound env (cookieOf sid) (fun _ -> "test") (fun _ -> Gate.Open) handler request
    |> Async.RunSynchronously
    |> ignore

    seen.Value
    |> Option.defaultWith (fun () -> failtest "the handler was not reached")


/// The answer of the order-context member over a context, without a Session, with the normal
/// values given.
let evaluatedWith normalValues (ctx: OrderContext) =
    let env = { envOver (portOver (ResizeArray())) with normalValues = normalValues }

    Compute.bound
        env
        (cookieOf None)
        (fun _ -> "test")
        (fun _ -> Gate.Open)
        (OrderContextCommand.processViewCmd env)
        {
            Opened = None
            Command =
                OrderViewCommand.SeedFilter(
                    Informedica.GenPRES.Shared.Types.SeedSource.Reload,
                    None,
                    None,
                    None,
                    None,
                    None
                ),
                ctx
        }
    |> Async.RunSynchronously


/// The patient a patient data update answers in the Session named, with the normal values given.
let changedIn normalValues (sid: string option) (pat: PatientData) =
    let env = { envOver (portOver (ResizeArray())) with normalValues = normalValues }

    Compute.bound
        env
        (cookieOf sid)
        PatientDataCommand.toString
        (fun _ -> Gate.Open)
        (PatientDataCommand.processCmd env)
        {
            Opened = sid |> Option.map (fun sid -> OpenedToken $"opened-{sid}")
            Command = PatientDataCommand.ChangePatientData pat
        }
    |> Async.RunSynchronously
    |> Result.map _.Response
    |> Result.defaultWith (fun errs -> failtest $"%A{errs}")


/// The answer to a signing command and the plan the session port was asked over, with the
/// normal values given.
let signedWith normalValues (sid: string option) (cmd: SigningCommand) =
    let captured = ResizeArray()

    let env = { envOver (portOver captured) with normalValues = normalValues }

    let response = SigningCommand.processCmd env (cookieOf sid) cmd |> Async.RunSynchronously

    response, captured |> Seq.tryHead


[<Tests>]
let tests =
    testList
        "the patients on each request"
        [
            testList
                "what seen tells"
                [
                    test "seen marks the Session and tells nothing else" {
                        let _, told, writes =
                            Session.seen today identified (Some(OpenedToken $"opened-{identified}")) state

                        (told, writes |> List.length)
                        |> Expect.equal "no notice, and the one touch" (None, 1)
                    }
                ]

            testList
                "every other request leaves every patient as sent"
                [
                    test "the order context's patient, in an identified Session and without one" {
                        for sid in [ Some identified; None; Some anonymous; Some entered; Some "nobody" ] do
                            over
                                sid
                                (OrderViewCommand.SeedFilter(
                                    Informedica.GenPRES.Shared.Types.SeedSource.Reload,
                                    None,
                                    None,
                                    None,
                                    None,
                                    None
                                 ),
                                 context)
                            |> Expect.equal
                                $"the order context as sent, %A{sid}"
                                (OrderViewCommand.SeedFilter(
                                    Informedica.GenPRES.Shared.Types.SeedSource.Reload,
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
                            OrderPlanCommand.FilterRows(plan.Filtered, plan)
                            OrderPlanCommand.EvaluateContext(
                                plan,
                                "1",
                                OrderViewCommand.SeedFilter(
                                    Informedica.GenPRES.Shared.Types.SeedSource.Reload,
                                    None,
                                    None,
                                    None,
                                    None,
                                    None
                                ),
                                context
                            )
                            OrderPlanCommand.AddOrderContext(plan, context, [||])
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
                                parsed.PatientData
                                :: (parsed.Contexts |> Array.map _.Context.PatientData |> Array.toList)
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

                    test "a measured weight is kept, with the estimate next to it, and the height estimated" {
                        { ageOnly 10 with Weight = { (ageOnly 10).Weight with Measured = Some 28000<gram> } }
                        |> Patient.estimate loaded
                        |> fun pat -> pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Estimated
                        |> Expect.equal
                            "the measured weight, the weight estimate, the height estimate"
                            (Some 28000<gram>, Some 32000<gram>, Some 140<cm>)
                    }

                    test "the estimate is the server's, whatever estimate the draft holds" {
                        { ageOnly 10 with Weight = { (ageOnly 10).Weight with Estimated = Some 41000<gram> } }
                        |> Patient.estimate loaded
                        |> weightAndHeight
                        |> Expect.equal "the server's 32 kg and 140 cm" (Some 32000<gram>, Some 140<cm>)
                    }

                    test
                        "a measured patient keeps the measured values, the calculation reads them, and gets the estimate range" {
                        let ask, count = counting (Some(tables, calc))
                        let pat = StubEhrPatient.contractPatient |> Patient.estimate ask

                        (pat.Weight.Measured,
                         pat.Height.Measured,
                         weightAndHeight pat,
                         (pat.Weight.EstimatedP3, pat.Weight.EstimatedP97),
                         count.Value)
                        |> Expect.equal
                            "measured 32 kg and 140 cm read, 25 to 45 kg estimated, asked once"
                            (StubEhrPatient.contractPatient.Weight.Measured,
                             StubEhrPatient.contractPatient.Height.Measured,
                             (StubEhrPatient.contractPatient.Weight.Measured,
                              StubEhrPatient.contractPatient.Height.Measured),
                             (Some 25000<gram>, Some 45000<gram>),
                             1)
                    }

                    test "without the normal values the patient stays as it is and the gate refuses it" {
                        let pat = ageOnly 10 |> Patient.estimate (fun () -> None)

                        (pat = ageOnly 10, pat |> Patient.patient)
                        |> Expect.equal "unchanged, refused" (true, Error [| Patient.noWeight |])
                    }

                    test "empty tables leave the estimate out and the gate refuses" {
                        let empty: Informedica.GenCore.Lib.Patients.NormalValueTables =
                            {
                                Weights = []
                                Heights = []
                                NeoWeights = []
                                NeoHeights = []
                            }

                        ageOnly 10
                        |> Patient.estimate (fun () -> Some(empty, calc))
                        |> Patient.patient
                        |> Expect.equal "refused" (Error [| Patient.noWeight |])
                    }

                    test "an unknown gender over tables with rows for one sex only is refused" {
                        let oneSex =
                            { tables with
                                Weights =
                                    tables.Weights
                                    |> List.filter (fun r -> r.Gender = Informedica.GenCore.Lib.Patients.Male)
                                Heights =
                                    tables.Heights
                                    |> List.filter (fun r -> r.Gender = Informedica.GenCore.Lib.Patients.Male)
                            }

                        ageOnly 10
                        |> Patient.estimate (fun () -> Some(oneSex, calc))
                        |> Patient.patient
                        |> Expect.equal "refused" (Error [| Patient.noWeight |])
                    }

                    test "an age-only patient on another request is not estimated: the patient data update is" {
                        let ctx = { Informedica.GenPRES.Shared.Models.OrderContext.empty with PatientData = ageOnly 10 }

                        estimatedOver
                            loaded
                            None
                            (OrderViewCommand.SeedFilter(
                                Informedica.GenPRES.Shared.Types.SeedSource.Reload,
                                None,
                                None,
                                None,
                                None,
                                None
                             ),
                             ctx)
                        |> Expect.equal
                            "the context as sent"
                            (OrderViewCommand.SeedFilter(
                                Informedica.GenPRES.Shared.Types.SeedSource.Reload,
                                None,
                                None,
                                None,
                                None,
                                None
                             ),
                             ctx)
                    }

                    test "a request other than a patient data update never asks the normal values" {
                        let ask, count = counting (Some(tables, calc))
                        let ctx = { Informedica.GenPRES.Shared.Models.OrderContext.empty with PatientData = ageOnly 10 }

                        estimatedOver
                            ask
                            None
                            (OrderViewCommand.SeedFilter(
                                Informedica.GenPRES.Shared.Types.SeedSource.Reload,
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

                    test "signing takes no estimate: the plan is signed as sent, whatever the normal values" {
                        // the challenge is a digest of the plan as sent; an estimate from tables
                        // reloaded before the submission would change the plan under it
                        let ctx = { Informedica.GenPRES.Shared.Models.OrderContext.empty with PatientData = ageOnly 10 }

                        let plan = Informedica.GenPRES.Shared.Models.OrderPlan.create (ageOnly 10) [| ctx |]

                        let cmd = SigningCommand.RequestSignChallenge(plan, OpenedToken $"opened-{identified}", None)

                        [ (fun () -> None); loaded ]
                        |> List.map (fun nv -> signedWith nv (Some identified) cmd |> fst)
                        |> Expect.allEqual
                            "an age-only plan is refused, with or without the normal values"
                            (SigningResponse.Refused SigningRefusal.NoPatient)
                    }
                ]
        ]
