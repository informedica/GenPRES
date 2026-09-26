/// The age on each request. An identified Session holds the age its patient opened on, and a
/// request of that Session is evaluated at that age, whatever age the client sent: a computing
/// request is told the Session's age beside the notice, and every contract patient in the
/// command is rewritten with it before the command is parsed; a signing request asks the
/// Session's age and rewrites the plan the same way before the check and the parse, so that the
/// version signed carries it. No clock is read: the age is the one the Session holds, up to and
/// including the next sign. A request without a Session, or in a Session whose EHR data names
/// no patient, is unchanged field for field; a measured weight survives the rewrite.
///
/// - Session.fs `age`: the Session's age as the contract carries it, none without an identity;
///   `seen` tells it beside the notice.
/// - Ports.fs `SessionPort.seen` answers the notice and the age; `SessionPort.age` the age alone,
///   for the signing request, which touches the Session in the challenge or the commit itself.
/// - Mappers.Patient.fs `Patient.aged`; `aged` on each command family that carries a patient, the
///   identity on the two that do not.
/// - Compute.fs `bound` takes `aged` beside `name` and `gate`.
/// - SigningCommand.fs `processCmd` rewrites the plan before the check and the parse.
///
/// The shadowed port and environment keep only the members the functions here use; the source
/// keeps them all.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi AgeOnRequest.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
// the contract before the domain, so that the domain's units and measures win unqualified
open Shared.Types
open Shared.Api
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi

module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CorePatientAge = Informedica.GenCore.Lib.Patients.PatientAge
module GenOrder = Informedica.GenOrder.Lib.Types


// ── Session.fs, the age a Session holds ───────────────────────────────────────────────────


module Session =

    open ServerApi.Session

    /// The age a Session holds for an identified patient, as the contract carries it: the age
    /// in days of the patient it opened on, split as the contract splits a number of days.
    /// None without a Session, in a Session whose EHR data names no patient, or when its
    /// patient has no age. No clock: the age is the one the Session opened on.
    let age (sid: string) (state: State) : Age option =
        state.Sessions
        |> Map.tryFind sid
        |> Option.filter (fun r ->
            r.Opened.EhrData
            |> Option.bind Informedica.GenForm.Lib.EhrPatientData.identity
            |> Option.isSome
        )
        |> Option.bind _.Opened.Patient
        |> Option.bind _.Age
        |> Option.map (
            ValueUnit.convertTo Units.Time.day
            >> ValueUnit.getValue
            >> Array.head
            >> Patient.toInt
            >> Shared.Models.Patient.Age.fromDays
        )


    /// For every computing request that names a Session: what the Session is told, as before,
    /// and beside it the age the Session holds for an identified patient.
    let seen
        (now: DateTime)
        (sid: string)
        (opened: OpenedToken option)
        (state: State)
        : State * (RecordNotice option * Age option) * Persist list
        =
        let state, notice, writes = ServerApi.Session.seen now sid opened state
        state, (notice, age sid state), writes


// ── Ports.fs ──────────────────────────────────────────────────────────────────────────────


type SessionPort =
    {
        challenge: string -> GenOrder.OrderPlan * OpenedToken * string option -> Async<SigningOutcome>
        submit: string -> Signature -> Async<SigningOutcome>
        /// every computing request: the Session the cookie names is marked seen and told whether
        /// the record moved on or the Session ended, and the age it holds for an identified
        /// patient
        seen: string -> OpenedToken option -> Async<RecordNotice option * Age option>
        /// the age the Session the cookie names holds for an identified patient; the Session is
        /// not touched, the signing request does that itself
        age: string -> Async<Age option>
    }


type AppEnv =
    {
        requireLoaded: unit -> string[] option
        session: SessionPort
        demo: bool
        logger: Informedica.Logging.Lib.Logger
    }


// ── Mappers.Patient.fs and the command families ───────────────────────────────────────────


module Patient =

    open ServerApi.Patient

    /// The draft with the Session's age in place of the one the client sent, everything else
    /// as sent; as sent when the Session holds none.
    let aged (age: Age option) (draft: Shared.Types.Patient) : Shared.Types.Patient =
        match age with
        | Some age -> { draft with Age = Some age }
        | None -> draft


module OrderContextCommand =

    open ServerApi.OrderContextCommand

    /// The context's patient at the Session's age.
    let agedContext (age: Age option) (ctx: OrderContext) : OrderContext =
        { ctx with Patient = ctx.Patient |> Patient.aged age }


    let aged (age: Age option) (cmd: OrderContextCommand, ctx: OrderContext) = cmd, agedContext age ctx


module OrderPlanCommand =

    open ServerApi.OrderPlanCommand

    /// The plan's patient and that of every context in it at the Session's age.
    let agedPlan (age: Age option) (plan: OrderPlan) : OrderPlan =
        { plan with
            Patient = plan.Patient |> Patient.aged age
            OrderContexts = plan.OrderContexts |> Array.map (OrderContextCommand.agedContext age)
        }


    /// Every patient the command carries at the Session's age: the plan's and its contexts',
    /// and the context's where the command carries one.
    let aged (age: Age option) (cmd: OrderPlanCommand) : OrderPlanCommand =
        match cmd with
        | OrderPlanCommand.Recalculate plan -> OrderPlanCommand.Recalculate(agedPlan age plan)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            OrderPlanCommand.Navigate(agedPlan age plan, contextId, ctxCmd, OrderContextCommand.agedContext age ctx)
        | OrderPlanCommand.AddOrderContext(plan, ctx) ->
            OrderPlanCommand.AddOrderContext(agedPlan age plan, OrderContextCommand.agedContext age ctx)
        | OrderPlanCommand.NewOrderContext(plan, category) -> OrderPlanCommand.NewOrderContext(agedPlan age plan, category)
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) -> OrderPlanCommand.RemoveOrderContexts(agedPlan age plan, ids)
        | OrderPlanCommand.Open(pat, contexts) ->
            OrderPlanCommand.Open(Patient.aged age pat, contexts |> Array.map (OrderContextCommand.agedContext age))


module FormularyCommand =

    open ServerApi.FormularyCommand

    /// The filter's patient, where it has one, at the Session's age; a filter without one stays
    /// the formulary unfiltered.
    let aged (age: Age option) (form: Formulary) : Formulary =
        { form with Patient = form.Patient |> Option.map (Patient.aged age) }


module ParenteraliaCommand =

    open ServerApi.ParenteraliaCommand

    /// No patient to age.
    let aged (_: Age option) (par: Parenteralia) = par


module InteractionCommand =

    open ServerApi.InteractionCommand

    /// No patient to age.
    let aged (_: Age option) (cmd: InteractionCommand) = cmd


// ── Compute.fs ────────────────────────────────────────────────────────────────────────────


module Compute =

    /// Every computing member: the Session the cookie names is marked seen and told whether the
    /// record moved on or the Session ended, and every patient the command carries is put at
    /// the age the Session holds, before the command is computed; without a cookie the request
    /// computes as it always did. The gate refuses a command that needs the formulary while it
    /// is not loaded, with the provider's messages. An exception is an Error with its message.
    /// The token is never logged.
    let bound
        (env: AppEnv)
        (cookie: SessionCookie)
        (name: 'cmd -> string)
        (gate: 'cmd -> Gate)
        (aged: Age option -> 'cmd -> 'cmd)
        (handler: 'cmd -> Async<Result<'resp, string[]>>)
        (request: Request<'cmd>)
        : Async<Result<Reply<'resp>, string[]>>
        =
        let cmd = request.Command

        async {
            try
                Logging.ServerLogging.Info $"Processing command: {name cmd}"
                |> Informedica.Logging.Lib.Logging.logInfo env.logger

                let! notice, age =
                    match cookie.read () with
                    | None -> async { return None, None }
                    | Some id -> env.session.seen id request.Opened

                // an identified Session's age, never the client's, on every patient the command
                // carries, before anything reads it
                let cmd = cmd |> aged age

                // an open command never asks the provider: asking may load
                let! result =
                    match gate cmd with
                    | Gate.Open -> handler cmd
                    | Gate.RequiresLoaded ->
                        match env.requireLoaded () with
                        | Some msgs -> async { return Error msgs }
                        | None -> handler cmd

                let told =
                    match notice with
                    | Some(RecordNotice.NewerVersion _) -> ", the record moved on"
                    | Some(RecordNotice.Ended _) -> ", the Session ended"
                    | None -> ""

                Logging.ServerLogging.Info $"Finished processing command: {name cmd}{told}"
                |> Informedica.Logging.Lib.Logging.logInfo env.logger

                return
                    result
                    |> Result.map (fun response ->
                        {
                            Response = response
                            Notice = notice
                        }
                    )
            with ex ->
                Logging.ServerLogging.Error $"Error processing command: {name cmd}\n{ex}"
                |> Informedica.Logging.Lib.Logging.logError env.logger

                return Error [| ex.Message |]
        }


// ── SigningCommand.fs ─────────────────────────────────────────────────────────────────────


module SigningCommand =

    open ServerApi.SigningCommand

    /// The plan of a challenge or of a submission at the Session's age.
    let aged (age: Age option) (cmd: SigningCommand) : SigningCommand =
        match cmd with
        | SigningCommand.RequestSignChallenge(plan, opened, notice) ->
            SigningCommand.RequestSignChallenge(OrderPlanCommand.agedPlan age plan, opened, notice)
        | SigningCommand.Submit submission ->
            SigningCommand.Submit
                { submission with
                    Plan = OrderPlanCommand.agedPlan age submission.Plan
                }


    /// A signing command for the Session the cookie names. No cookie, no Session: refused
    /// before the port is asked. The plan's patient and that of every context in it put at the
    /// age the Session holds and made at the inbound boundary, then the plan parsed into the
    /// domain; a plan the domain does not read is refused before the port is asked. Writes no
    /// cookie.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                let! age = env.session.age id
                let cmd = cmd |> aged age

                let plan =
                    match cmd with
                    | SigningCommand.RequestSignChallenge(plan, _, _) -> plan
                    | SigningCommand.Submit submission -> submission.Plan

                if
                    ServerApi.Patient.ofPlan plan
                    |> List.exists (ServerApi.Patient.patient >> _.IsError)
                then
                    return SigningResponse.Refused SigningRefusal.NoPatient
                else
                    match plan |> ServerApi.OrderPlanCommand.parsePlan with
                    | Error _ -> return SigningResponse.Refused SigningRefusal.PlanUnreadable
                    | Ok parsed ->
                        match cmd with
                        | SigningCommand.RequestSignChallenge(_, opened, notice) ->
                            let! outcome = env.session.challenge id (parsed, opened, notice)
                            return toResponse env.demo outcome
                        | SigningCommand.Submit submission ->
                            let! outcome =
                                env.session.submit
                                    id
                                    {
                                        Plan = parsed
                                        Opened = submission.Opened
                                        Challenge = submission.Challenge
                                        Pin = submission.Pin
                                        IdemKey = submission.IdemKey
                                    }

                            return toResponse env.demo outcome
        }


// ── The proof ─────────────────────────────────────────────────────────────────────────────


/// The date of the open: a September day, half a year past the stub patient's tenth birthday.
let today = DateTime(2026, 9, 26)


/// The Session's age at the open: ten years, six months, one week and four days.
let sessionAge = Shared.Models.Patient.Age.fromDays 3841


let ehr = StubPatientData.data "p1"


/// EHR data with an age value in place of a birthdate: no identity.
let unidentified =
    { ehr with
        Patient =
            { ehr.Patient with
                Age = CoreAgeValue.ten |> CorePatientAge.ageValue
            }
    }


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
    : ServerApi.Session.SessionRecord
    =
    {
        Opened =
            {
                User = Some prescriber
                PatientId = Some "p1"
                EhrData = ehr
                Patient = patient
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
    { ServerApi.Session.emptyState with
        Sessions =
            Map.ofList
                [
                    identified, record identified (Some ehr) (Some(StubPatientData.port.patient today ehr))
                    anonymous,
                    record anonymous (Some unidentified) (Some(StubPatientData.port.patient today unidentified))
                    entered, record entered None (ServerApi.Patient.parse StubPatientData.patient |> Result.toOption)
                ]
    }


/// The session port of the tests over the state: seen through the machine at the clock given,
/// the plan of a challenge or a submission captured.
let portOver (clock: unit -> DateTime) (captured: ResizeArray<GenOrder.OrderPlan>) : SessionPort =
    let st = ref state

    {
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
            fun sid opened ->
                async {
                    let next, told, _ = Session.seen (clock ()) sid opened st.Value
                    st.Value <- next
                    return told
                }
        age = fun sid -> async { return Session.age sid st.Value }
    }


let envOver (port: SessionPort) : AppEnv =
    {
        requireLoaded = fun () -> None
        session = port
        demo = true
        logger = Informedica.Logging.Lib.Logging.noOp
    }


let cookieOf (sid: string option) : SessionCookie =
    {
        read = fun () -> sid
        write = ignore
        delete = ignore
    }


/// The stub's patient as a client would send it at the wrong age, with a weight it measured.
let sent: Shared.Types.Patient =
    { StubPatientData.patient with
        Age = Some(Shared.Models.Patient.Age.fromDays (5 * 365))
        Weight =
            { StubPatientData.patient.Weight with
                Measured = Some 12000<gram>
            }
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

    Compute.bound env (cookieOf sid) (fun _ -> "test") (fun _ -> Gate.Open) aged handler request
    |> Async.RunSynchronously
    |> ignore

    seen.Value |> Option.defaultWith (fun () -> failtest "the handler was not reached")


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
                            Session.seen today identified (Some(OpenedToken $"opened-{identified}")) state

                        (told, writes |> List.length)
                        |> Expect.equal "no notice, the age, and the one touch" ((None, Some sessionAge), 1)
                    }
                ]

            testList
                "an identified request is evaluated at the Session's age"
                [
                    test "the order context's patient, its measured weight kept" {
                        let _, ctx =
                            over (Some identified) OrderContextCommand.aged (OrderContextCommand.UpdateOrderContext, context)

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
                                | OrderPlanCommand.RemoveOrderContexts(plan, _) -> ServerApi.Patient.ofPlan plan
                                | OrderPlanCommand.Navigate(plan, _, _, ctx)
                                | OrderPlanCommand.AddOrderContext(plan, ctx) -> ServerApi.Patient.ofPlan plan @ [ ctx.Patient ]
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
                                parsed.Patient :: (parsed.Contexts |> Array.map _.Context.Patient |> Array.toList)
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
                            |> Expect.equal $"the order context as sent, %A{sid}" (OrderContextCommand.UpdateOrderContext, context)

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
                        |> Expect.equal
                            "as given"
                            (Shared.Models.Parenteralia.empty, InteractionCommand.GetDrugNames)
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
