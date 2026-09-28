// The server fills in the estimates a request patient lacks, after the Session's age and before
// the gate, so a patient with an age alone is calculable while the normal values are loaded.
// Prototype for step 3 of docs/implementation-plans/1126-patient-data-in-the-two-modes.md.
//
// Run from this directory, after a build: dotnet fsi EstimateOnRequest.fsx
//
// Migration, beside the functions below:
// - AppEnv gets normalValues: unit -> NormalValues option, wired in ServerApi.Adapters.fs from
//   the expression the estimating port uses, extracted into one function for both. The script
//   cannot add the field (a new record type the composition root and the test fixtures do not
//   take), so it passes the estimate as a parameter of Compute.bound and
//   SigningCommand.processCmd; in the source both read Patient.estimate env.normalValues.
// - Each command's aged becomes patients, and the CompositionRoot passes XCommand.patients.
// - The test fixtures that build an AppEnv (makeEnv, makeEnvNotLoaded) get normalValues.

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"
// the stub adapter tests read the wire format
#r "nuget: Fable.Remoting.Json, 3.0.0"
#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"
#load "../../../tests/Informedica.GenPRES.Server.Tests/StubAdapterTests.fs"
#load "../../../tests/Informedica.GenPRES.Server.Tests/AgeOnRequestTests.fs"


open Expecto
open Expecto.Flip
open ServerApi
open Shared.Types
open Shared.Api


module Patient =

    /// The patient with the estimates it lacks filled in from the normal values, per measure: a
    /// weight or a height that is neither measured nor estimated gets the estimate; a measured
    /// value and an estimate already there are kept, so that the calculation uses the estimate
    /// the panel shows. The normal values are asked only when a measure is missing; without
    /// them the patient stays as it is, and the gate refuses it.
    let estimate (normalValues: unit -> NormalValues option) (pat: Patient) : Patient =
        let lacks (measured: 'a option) (estimated: 'a option) = measured.IsNone && estimated.IsNone
        let lacksWeight = lacks pat.Weight.Measured pat.Weight.Estimated
        let lacksHeight = lacks pat.Height.Measured pat.Height.Estimated

        if not (lacksWeight || lacksHeight) then
            pat
        else
            match normalValues () with
            | None -> pat
            | Some nv ->
                // apply writes both estimates; only the missing measure is taken from it
                let estimated = pat |> Shared.Models.NormalValues.apply nv

                { pat with
                    Weight =
                        if lacksWeight then
                            { pat.Weight with
                                EstimatedP3 = estimated.Weight.EstimatedP3
                                Estimated = estimated.Weight.Estimated
                                EstimatedP97 = estimated.Weight.EstimatedP97
                            }
                        else
                            pat.Weight
                    Height =
                        if lacksHeight then
                            { pat.Height with
                                EstimatedP3 = estimated.Height.EstimatedP3
                                Estimated = estimated.Height.Estimated
                                EstimatedP97 = estimated.Height.EstimatedP97
                            }
                        else
                            pat.Height
                }


module OrderContextMapper =

    /// The context with its patient mapped.
    let patients (f: Patient -> Patient) (ctx: OrderContext) : OrderContext = { ctx with Patient = f ctx.Patient }


module OrderContextCommand =

    /// The context's patient mapped.
    let patients (f: Patient -> Patient) (cmd: OrderContextCommand, ctx: OrderContext) =
        cmd, OrderContextMapper.patients f ctx


module FormularyCommand =

    /// The filter's patient mapped, where it has one; a filter without one stays the formulary
    /// unfiltered.
    let patients (f: Patient -> Patient) (form: Formulary) : Formulary =
        { form with Patient = form.Patient |> Option.map f }


module ParenteraliaCommand =

    /// No patient to map.
    let patients (_: Patient -> Patient) (par: Parenteralia) = par


module InteractionCommand =

    /// No patient to map.
    let patients (_: Patient -> Patient) (cmd: InteractionCommand) = cmd


module OrderPlanCommand =

    /// The plan's patient and that of every context in it mapped.
    let patientsPlan (f: Patient -> Patient) (plan: OrderPlan) : OrderPlan =
        { plan with
            Patient = f plan.Patient
            OrderContexts = plan.OrderContexts |> Array.map (OrderContextMapper.patients f)
        }


    /// Every patient the command carries mapped: the plan's and its contexts', and the
    /// context's where the command carries one.
    let patients (f: Patient -> Patient) (cmd: OrderPlanCommand) : OrderPlanCommand =
        match cmd with
        | OrderPlanCommand.Recalculate plan -> OrderPlanCommand.Recalculate(patientsPlan f plan)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            OrderPlanCommand.Navigate(patientsPlan f plan, contextId, ctxCmd, OrderContextMapper.patients f ctx)
        | OrderPlanCommand.AddOrderContext(plan, ctx) ->
            OrderPlanCommand.AddOrderContext(patientsPlan f plan, OrderContextMapper.patients f ctx)
        | OrderPlanCommand.NewOrderContext(plan, category) ->
            OrderPlanCommand.NewOrderContext(patientsPlan f plan, category)
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            OrderPlanCommand.RemoveOrderContexts(patientsPlan f plan, ids)
        | OrderPlanCommand.Open(pat, contexts) ->
            OrderPlanCommand.Open(f pat, contexts |> Array.map (OrderContextMapper.patients f))


module SigningCommand =

    /// The plan of a challenge or of a submission with every patient mapped.
    let patients (f: Patient -> Patient) (cmd: SigningCommand) : SigningCommand =
        match cmd with
        | SigningCommand.RequestSignChallenge(plan, opened, notice) ->
            SigningCommand.RequestSignChallenge(OrderPlanCommand.patientsPlan f plan, opened, notice)
        | SigningCommand.Submit submission ->
            SigningCommand.Submit
                { submission with
                    Plan = OrderPlanCommand.patientsPlan f submission.Plan
                }


    /// As the source, but every patient of the plan put at the Session's age and then given the
    /// estimates it lacks. In the source the estimate is Patient.estimate env.normalValues.
    let processCmd (estimate: Patient -> Patient) (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                let! answer = env.session.age id

                let cmd =
                    cmd
                    |> patients (Patient.aged (answer |> Result.defaultValue None) >> estimate)

                let plan =
                    match cmd with
                    | SigningCommand.RequestSignChallenge(plan, _, _) -> plan
                    | SigningCommand.Submit submission -> submission.Plan

                match answer with
                | Error refusal -> return SigningResponse.Refused refusal
                | Ok _ when Patient.ofPlan plan |> List.exists (Patient.patient >> _.IsError) ->
                    return SigningResponse.Refused SigningRefusal.NoPatient
                | Ok _ ->
                    match plan |> OrderPlanCommand.parsePlan with
                    | Error _ -> return SigningResponse.Refused SigningRefusal.PlanUnreadable
                    | Ok parsed ->
                        match cmd with
                        | SigningCommand.RequestSignChallenge(_, opened, notice) ->
                            let! outcome = env.session.challenge id (parsed, opened, notice)
                            return SigningCommand.toResponse env.demo outcome
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

                            return SigningCommand.toResponse env.demo outcome
        }


module Compute =

    /// As the source, with patients in place of aged: every patient the command carries put at
    /// the Session's age and then given the estimates it lacks, before anything reads it. The
    /// estimate asks the normal values per patient, and only for a patient that lacks a
    /// measure, so a command without a patient never asks the provider. In the source the
    /// estimate is Patient.estimate env.normalValues.
    let bound
        (estimate: Patient -> Patient)
        (env: AppEnv)
        (cookie: SessionCookie)
        (name: 'cmd -> string)
        (gate: 'cmd -> Gate)
        (patients: (Patient -> Patient) -> 'cmd -> 'cmd)
        (patientOf: 'cmd -> Patient option)
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
                    | Some id -> env.session.seen id request.Opened (patientOf cmd)

                // an identified Session's age, never the client's, then the estimates a patient
                // lacks, on every patient the command carries, before anything reads it
                let cmd = cmd |> patients (Patient.aged age >> estimate)

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


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------


module Fixtures =

    open Informedica.GenPRES.Server.Tests.AgeOnRequestTests


    let row sex age p3 mean p97 = Shared.Models.NormalValues.create sex age p3 mean p97


    /// Normal values of both sexes at five and at ten years: 18 kg and 110 cm, 32 kg and 140 cm.
    let tables: NormalValues =
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


    /// The normal values, counting how often they are asked.
    let counting (nv: NormalValues option) =
        let count = ref 0

        let ask () =
            count.Value <- count.Value + 1
            nv

        ask, count


    let loaded () = Some tables


    let years n = Shared.Models.Patient.Age.fromDays (n * 365)


    /// A patient with an age and nothing else.
    let ageOnly n : Patient = { Shared.Models.Patient.empty with Age = Some(years n) }


    let weightAndHeight (pat: Patient) =
        pat |> Shared.Models.Patient.getWeight, pat |> Shared.Models.Patient.getHeight


    /// The command a computing request reaches its handler as, run through the bound above
    /// over the Session named.
    let boundOver estimate (sid: string option) patients (cmd: 'cmd) : 'cmd =
        let seen: 'cmd option ref = ref None
        let env = envOver (portOver (fun () -> today) (ResizeArray()))

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

        Compute.bound
            estimate
            env
            (cookieOf sid)
            (fun _ -> "test")
            (fun _ -> Gate.Open)
            patients
            (fun _ -> None)
            handler
            request
        |> Async.RunSynchronously
        |> ignore

        seen.Value
        |> Option.defaultWith (fun () -> failtest "the handler was not reached")


    /// The answer of the order context member over a context, through the bound above, without a
    /// Session.
    let evaluate estimate (ctx: OrderContext) =
        let env = envOver (portOver (fun () -> today) (ResizeArray()))

        let request =
            {
                Opened = None
                Command = OrderContextCommand.UpdateOrderContext, ctx
            }

        Compute.bound
            estimate
            env
            (cookieOf None)
            (fun _ -> "test")
            (fun _ -> Gate.Open)
            OrderContextCommand.patients
            OrderContextCommand.patientOf
            (OrderContextCommand.processCmd env)
            request
        |> Async.RunSynchronously


    /// The patient a Session resumes on, as the client gets it, after the user cleared the
    /// measured weight and height.
    let resumedAfterClearing () : Patient =
        let cleared: Measured<_> =
            {
                Value = None
                At = today
            }

        let opened =
            { (state.Sessions |> Map.find identified).Opened with
                Measured =
                    { Measurements.none with
                        Weight = Some cleared
                        Height = Some cleared
                    }
            }

        SessionMapper.toOpened true opened
        |> _.PatientContext
        |> Option.bind _.Patient
        |> Option.defaultWith (fun () -> failtest "the Session resumes without a patient")


    /// The plan passed to the session port for a signing command, through the processCmd above.
    let signedOver estimate (sid: string option) (cmd: SigningCommand) =
        let captured = ResizeArray()
        let env = envOver (portOver (fun () -> today) captured)

        let response =
            SigningCommand.processCmd estimate env (cookieOf sid) cmd
            |> Async.RunSynchronously

        response, captured |> Seq.tryHead


open Informedica.GenPRES.Server.Tests.AgeOnRequestTests
open Fixtures


let tests =
    testList
        "the server fills in the estimates a request patient lacks"
        [
            testList
                "Patient.estimate"
                [
                    test "an age alone gets a weight and a height" {
                        ageOnly 10
                        |> Patient.estimate loaded
                        |> weightAndHeight
                        |> Expect.equal "32 kg and 140 cm" (Some 32000<gram>, Some 140<cm>)
                    }

                    test "a measured weight is kept, the height estimated" {
                        { ageOnly 10 with
                            Weight = { (ageOnly 10).Weight with Measured = Some 28000<gram> }
                        }
                        |> Patient.estimate loaded
                        |> fun pat -> pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Estimated
                        |> Expect.equal "the measured weight, no weight estimate, the height estimated"
                            (Some 28000<gram>, None, Some 140<cm>)
                    }

                    test "a client estimate of the weight is kept when only the height is filled in" {
                        { ageOnly 10 with
                            Weight = { (ageOnly 10).Weight with Estimated = Some 41000<gram> }
                        }
                        |> Patient.estimate loaded
                        |> weightAndHeight
                        |> Expect.equal "the client's 41 kg, the server's 140 cm" (Some 41000<gram>, Some 140<cm>)
                    }

                    test "a patient with a weight and a height is not changed, nor are the values asked" {
                        let ask, count = counting (Some tables)
                        let pat = StubPatientData.patient

                        (pat |> Patient.estimate ask = pat, count.Value)
                        |> Expect.equal "the same patient, asked none" (true, 0)
                    }

                    test "without the normal values the patient stays as it is and the gate refuses it" {
                        let pat = ageOnly 10 |> Patient.estimate (fun () -> None)

                        (pat = ageOnly 10, pat |> ServerApi.Patient.patient)
                        |> Expect.equal "unchanged, refused" (true, Error [| ServerApi.Patient.noWeightAndHeight |])
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
                        |> ServerApi.Patient.patient
                        |> Expect.equal "refused" (Error [| ServerApi.Patient.noWeightAndHeight |])
                    }

                    test "an unknown gender over tables with rows for one sex only is refused" {
                        let oneSex =
                            { tables with
                                Weights = tables.Weights |> List.filter (fun r -> r.Sex = "M")
                                Heights = tables.Heights |> List.filter (fun r -> r.Sex = "M")
                            }

                        ageOnly 10
                        |> Patient.estimate (fun () -> Some oneSex)
                        |> ServerApi.Patient.patient
                        |> Expect.equal "refused" (Error [| ServerApi.Patient.noWeightAndHeight |])
                    }
                ]

            testList
                "patients maps what aged mapped"
                [
                    let age = Some sessionAge

                    test "the order context" {
                        (OrderContextCommand.UpdateOrderContext, context)
                        |> OrderContextCommand.patients (Patient.aged age)
                        |> Expect.equal
                            "as aged"
                            (ServerApi.OrderContextCommand.aged age (OrderContextCommand.UpdateOrderContext, context))
                    }

                    test "the formulary, with and without a patient" {
                        [ form; Shared.Models.Formulary.empty ]
                        |> List.map (fun f ->
                            FormularyCommand.patients (Patient.aged age) f = ServerApi.FormularyCommand.aged age f
                        )
                        |> Expect.allEqual "as aged" true
                    }

                    test "every plan command" {
                        [
                            OrderPlanCommand.Recalculate plan
                            OrderPlanCommand.Navigate(plan, "1", OrderContextCommand.UpdateOrderContext, context)
                            OrderPlanCommand.AddOrderContext(plan, context)
                            OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN)
                            OrderPlanCommand.RemoveOrderContexts(plan, [| "1" |])
                            OrderPlanCommand.Open(sent, [| context |])
                        ]
                        |> List.map (fun cmd ->
                            OrderPlanCommand.patients (Patient.aged age) cmd = ServerApi.OrderPlanCommand.aged age cmd
                        )
                        |> Expect.allEqual "as aged" true
                    }

                    test "a signing challenge" {
                        let cmd = SigningCommand.RequestSignChallenge(plan, OpenedToken "t", None)

                        SigningCommand.patients (Patient.aged age) cmd
                        |> Expect.equal "as aged" (ServerApi.SigningCommand.aged age cmd)
                    }
                ]

            testList
                "a request"
                [
                    let estimate = Patient.estimate loaded

                    test "an age-only order context patient is computed on the estimate" {
                        let _, ctx =
                            boundOver
                                estimate
                                None
                                OrderContextCommand.patients
                                (OrderContextCommand.UpdateOrderContext,
                                 { Shared.Models.OrderContext.empty with Patient = ageOnly 10 })

                        ctx.Patient
                        |> weightAndHeight
                        |> Expect.equal "32 kg and 140 cm" (Some 32000<gram>, Some 140<cm>)
                    }

                    test "every patient of an age-only order plan is computed on the estimate" {
                        let ctx = { Shared.Models.OrderContext.empty with Patient = ageOnly 10 }
                        let plan = Shared.Models.OrderPlan.create (ageOnly 10) [| ctx; ctx |]

                        match boundOver estimate None OrderPlanCommand.patients (OrderPlanCommand.Recalculate plan) with
                        | OrderPlanCommand.Recalculate plan -> Patient.ofPlan plan |> List.map weightAndHeight
                        | _ -> failtest "another command"
                        |> Expect.allEqual "32 kg and 140 cm" (Some 32000<gram>, Some 140<cm>)
                    }

                    test "the Session's age is put on first, and the estimate follows it" {
                        // the client sends five years; the Session holds ten and a half
                        let _, ctx =
                            boundOver
                                estimate
                                (Some identified)
                                OrderContextCommand.patients
                                (OrderContextCommand.UpdateOrderContext,
                                 { Shared.Models.OrderContext.empty with Patient = ageOnly 5 })

                        (ctx.Patient.Age, ctx.Patient |> weightAndHeight)
                        |> Expect.equal "the Session's age, the ten-year estimate"
                            (Some sessionAge, (Some 32000<gram>, Some 140<cm>))
                    }

                    test "a command without a patient never asks the normal values" {
                        let ask, count = counting (Some tables)

                        boundOver
                            (Patient.estimate ask)
                            None
                            InteractionCommand.patients
                            InteractionCommand.GetDrugNames
                        |> ignore

                        count.Value |> Expect.equal "asked none" 0
                    }

                    test "a Session resumed after its weight and height were cleared is refused without the estimate" {
                        let ctx = { Shared.Models.OrderContext.empty with Patient = resumedAfterClearing () }

                        ctx
                        |> evaluate id
                        |> Result.mapError List.ofArray
                        |> Expect.equal "the refusal of #1126" (Error [ ServerApi.Patient.noWeightAndHeight ])
                    }

                    test "a Session resumed after its weight and height were cleared computes with it" {
                        let ctx = { Shared.Models.OrderContext.empty with Patient = resumedAfterClearing () }

                        ctx |> evaluate estimate |> Result.isOk |> Expect.isTrue "computed"
                    }

                    test "signing a plan whose patient carries only an age is not refused" {
                        let ctx = { Shared.Models.OrderContext.empty with Patient = ageOnly 10 }
                        let plan = Shared.Models.OrderPlan.create (ageOnly 10) [| ctx |]
                        let cmd = SigningCommand.RequestSignChallenge(plan, OpenedToken $"opened-{identified}", None)

                        let refusedWithout, _ = signedOver id (Some identified) cmd
                        let answered, asked = signedOver estimate (Some identified) cmd

                        (refusedWithout, answered, asked.IsSome)
                        |> Expect.equal
                            "refused without the estimate; with it, the port asked"
                            (SigningResponse.Refused SigningRefusal.NoPatient,
                             SigningResponse.ChallengeIssued "c-1",
                             true)
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
