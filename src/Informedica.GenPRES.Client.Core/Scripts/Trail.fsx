// A readable trail of the client state machines: one line per machine step, with the message,
// the effects it produced and the state it reached, to check the logic in the order it ran.
// Step "a readable trail of the machine steps" of #1224. Everything here is written out by hand,
// no reflection, so the browser and FSI print the same line. A line never holds a patient's
// identity, a user's name, a PIN, a code, a token or a free text.

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto"


open System
open Shared.Types
open Shared.Models
open Shared.Api


/// One machine step as the trail shows it.
module Trail =

    /// A machine step, described.
    type Step =
        {
            /// The step's place in the trail, from 1.
            No: int
            /// When the step ran, as the caller read the clock.
            At: DateTime
            /// The machine that stepped.
            Machine: string
            /// The message, described.
            Msg: string
            /// The effects the step produced, described.
            Effects: string list
            /// The state the step reached, described.
            State: string
        }


    /// The step as one line of the trail.
    let format (step: Step) =
        let effects =
            match step.Effects with
            | [] -> "none"
            | xs -> xs |> String.concat ", "

        let time = step.At.ToString "HH:mm:ss.fff"

        $"#%i{step.No} %s{time} %s{step.Machine} %s{step.Msg} -> %s{effects} | %s{step.State}"


    /// The trail with the line added, keeping the newest max lines, oldest first.
    let append (max: int) (line: string) (lines: string list) =
        let lines = lines @ [ line ]
        let drop = lines.Length - max
        if drop > 0 then lines |> List.skip drop else lines


    /// The parts a message, an effect or a state is described with.
    module Part =

        let orNone (describe: 'a -> string) (x: 'a option) =
            x |> Option.map describe |> Option.defaultValue "none"


        /// Ok with the answer described, or Error without its text.
        let result (describe: 'a -> string) (r: Result<'a, 'e>) =
            match r with
            | Ok x -> $"Ok %s{describe x}"
            | Error _ -> "Error"


        /// The patient by age and weight: a patient carries no identity, and the trail shows none.
        let patient (p: Patient) =
            let age =
                p
                |> Patient.getAgeInYears
                |> Option.map (fun a -> $"%.1f{a} y")
                |> Option.defaultValue "no age"

            let weight =
                p
                |> Patient.getWeightInKg
                |> Option.map (fun w -> $"%.1f{w} kg")
                |> Option.defaultValue "no weight"

            $"patient %s{age} %s{weight}"


        let patientOption (p: Patient option) =
            p |> Option.map patient |> Option.defaultValue "no patient"


        let doseType (dt: DoseType) =
            match dt with
            | Once s -> $"once %s{s}"
            | Discontinuous s -> $"discontinuous %s{s}"
            | Continuous s -> $"continuous %s{s}"
            | Timed s -> $"timed %s{s}"
            | OnceTimed s -> $"once timed %s{s}"
            | NoDoseType -> "no dose type"


        /// The picks of a filter, in the order the workbench asks for them.
        let filter (f: Filter) =
            [
                f.Indication
                f.Generic
                f.Route
                f.Form
                f.DoseType |> Option.map doseType
                f.Diluent
            ]
            |> List.choose id
            |> function
                | [] -> "no picks"
                | picks -> picks |> String.concat "/"


        /// An order context by its id alone; the workbench has none.
        let contextId (c: OrderContext) =
            if String.IsNullOrEmpty c.Id then "workbench" else c.Id


        /// An order context by its id, its picks and its scenarios; never its argumentation.
        let context (c: OrderContext) =
            $"%s{contextId c} %s{filter c.Filter} %i{c.Scenarios.Length} scenarios"


        let plan (p: OrderPlan) =
            $"plan %i{p.OrderContexts.Length} contexts"


        /// A version by its number; never the user who signed it.
        let head (h: OrderPlanHead) = $"v%i{h.No}"


        let signed (s: SignedOrderPlan) =
            $"%s{head s.Head} %i{s.OrderContexts.Length} contexts"


        /// Whether a token came along; never the token.
        let token (t: OpenedToken option) =
            match t with
            | Some _ -> "token"
            | None -> "no token"


        let nutrition (c: NutritionCategory) =
            match c with
            | NutritionCategory.EnteralFeeding -> "enteral feeding"
            | NutritionCategory.EnteralSupplement -> "enteral supplement"
            | NutritionCategory.TPN -> "TPN"
            | NutritionCategory.Lipid -> "lipid"
            | NutritionCategory.ElectrolyteGlucose -> "electrolyte glucose"


        /// A session by who opened it and on which version; never the user or the patient id.
        let opened (so: SessionOpened) =
            let who =
                match so.User with
                | Some _ -> "user"
                | None -> "anonymous"

            let patientContext =
                match so.PatientContext with
                | Some _ -> "patient context"
                | None -> "no patient context"

            $"%s{who} %s{patientContext} %s{so.Head |> orNone signed}"


        let launchRefusal (r: LaunchRefusal) =
            match r with
            | LaunchRefusal.LaunchExpired -> "LaunchExpired"
            | LaunchRefusal.LaunchSpent -> "LaunchSpent"
            | LaunchRefusal.LaunchInvalid -> "LaunchInvalid"
            | LaunchRefusal.NoBrowserIdentity -> "NoBrowserIdentity"
            | LaunchRefusal.NoRole -> "NoRole"
            | LaunchRefusal.WrongActivePatient -> "WrongActivePatient"
            | LaunchRefusal.EnrolmentRequired -> "EnrolmentRequired"


        let ending (e: SessionEnding) =
            match e with
            | SessionEnding.SupersededByLaunch -> "SupersededByLaunch"
            | SessionEnding.WrongPinLimit -> "WrongPinLimit"
            | SessionEnding.Unreadable -> "Unreadable"
            | SessionEnding.Idle -> "Idle"


        let pinRefusal (r: PinRefusal) =
            match r with
            | PinRefusal.WrongCode left -> $"WrongCode %i{left} left"
            | PinRefusal.CodeVoid -> "CodeVoid"
            | PinRefusal.AttemptExpired -> "AttemptExpired"
            | PinRefusal.PinFormat -> "PinFormat"
            | PinRefusal.WrongActivePatient -> "WrongActivePatient"


        let signingRefusal (r: SigningRefusal) =
            match r with
            | SigningRefusal.NoSession -> "NoSession"
            | SigningRefusal.NoPatient -> "NoPatient"
            | SigningRefusal.NotPrescriber -> "NotPrescriber"
            | SigningRefusal.Blocked h -> $"Blocked %s{head h}"
            | SigningRefusal.StaleToken -> "StaleToken"
            | SigningRefusal.ChallengeMismatch -> "ChallengeMismatch"
            | SigningRefusal.ChallengeExpired -> "ChallengeExpired"
            | SigningRefusal.PinWrong left -> $"PinWrong %i{left} left"
            | SigningRefusal.PinLimit -> "PinLimit"
            | SigningRefusal.Locked _ -> "Locked"
            | SigningRefusal.StoreFailed -> "StoreFailed"
            | SigningRefusal.PlanUnreadable -> "PlanUnreadable"
            | SigningRefusal.ContextDiffers -> "ContextDiffers"


        let contextRefusal (r: OrderContextRefusal) =
            match r with
            | OrderContextRefusal.NoDoseRules -> "NoDoseRules"
            | OrderContextRefusal.NoDoseRulesForPatient -> "NoDoseRulesForPatient"
            | OrderContextRefusal.NoProducts -> "NoProducts"


        let notice (n: RecordNotice) =
            match n with
            | RecordNotice.NewerVersion h -> $"NewerVersion %s{head h}"
            | RecordNotice.Ended e -> $"Ended %s{ending e}"


        /// A plan command by what it does, with a context as describeContext tells it; never the plan.
        let planCommand (describeContext: OrderContext -> string) (cmd: OrderPlanCommand) =
            match cmd with
            | OrderPlanCommand.Recalculate _ -> "Recalculate"
            | OrderPlanCommand.Navigate(_, id, ctxCmd, ctx) ->
                $"Navigate %s{id} %s{OrderContextCommand.toString (ctxCmd, ctx)}"
            | OrderPlanCommand.AddOrderContext(_, ctx) -> $"AddOrderContext %s{describeContext ctx}"
            | OrderPlanCommand.NewOrderContext(_, category) -> $"NewOrderContext %s{nutrition category}"
            | OrderPlanCommand.RemoveOrderContexts(_, ids) -> $"RemoveOrderContexts %i{ids.Length}"
            | OrderPlanCommand.Open(_, contexts) -> $"Open %i{contexts.Length} contexts"


    /// The session machine.
    module Session =

        open SessionMachine


        let launchOutcome (o: LaunchOutcome) =
            match o with
            | LaunchOutcome.Opened so -> $"Opened %s{Part.opened so}"
            | LaunchOutcome.RedirectTo _ -> "RedirectTo"
            | LaunchOutcome.Refused r -> $"Refused %s{Part.launchRefusal r}"


        let resumeResult (r: ResumeResult) =
            match r with
            | ResumeResult.Found so -> $"Found %s{Part.opened so}"
            | ResumeResult.NotFound -> "NotFound"
            | ResumeResult.Ended e -> $"Ended %s{Part.ending e}"
            | ResumeResult.Enrolling _ -> "Enrolling"


        let pinOutcome (o: PinOutcome) =
            match o with
            | PinOutcome.Opened so -> $"Opened %s{Part.opened so}"
            | PinOutcome.Refused r -> $"Refused %s{Part.pinRefusal r}"


        let reopened (r: Result<SessionOpened option, string>) =
            r |> Part.result (Part.orNone Part.opened)


        /// Never the launch, the key, the code, the PIN, the token or the identity.
        let msg (msg: SessionMsg) =
            match msg with
            | SessionMsg.Present _ -> "Present"
            | SessionMsg.Outcome(_, _, r) -> $"Outcome %s{r |> Part.result launchOutcome}"
            | SessionMsg.Retry -> "Retry"
            | SessionMsg.Resume -> "Resume"
            | SessionMsg.Resumed r -> $"Resumed %s{r |> Part.result resumeResult}"
            | SessionMsg.SupplyPin _ -> "SupplyPin"
            | SessionMsg.PinAnswered r -> $"PinAnswered %s{r |> Part.result pinOutcome}"
            | SessionMsg.RefusedAtCallback r -> $"RefusedAtCallback %s{Part.launchRefusal r}"
            | SessionMsg.OpenAnonymous -> "OpenAnonymous"
            | SessionMsg.Close -> "Close"
            | SessionMsg.Closed -> "Closed"
            | SessionMsg.CloseFailed _ -> "CloseFailed"
            | SessionMsg.EndedByServer e -> $"EndedByServer %s{Part.ending e}"
            | SessionMsg.TokenRenewed(_, p, _) -> $"TokenRenewed %s{Part.patient p}"
            | SessionMsg.OpenVersion id -> $"OpenVersion %s{id}"
            | SessionMsg.Reopened(from, r) -> $"Reopened %s{Part.token from} %s{reopened r}"
            | SessionMsg.Refresh -> "Refresh"
            | SessionMsg.Refreshed(from, r) -> $"Refreshed %s{Part.token from} %s{reopened r}"
            | SessionMsg.Told(from, n) -> $"Told %s{Part.token from} %s{Part.notice n}"
            | SessionMsg.Blocked h -> $"Blocked %s{Part.head h}"


        /// Never the url, the key, the code or the PIN.
        let effect (effect: SessionEffect) =
            match effect with
            | SessionEffect.CallPresentLaunch _ -> "CallPresentLaunch"
            | SessionEffect.CallGetSession -> "CallGetSession"
            | SessionEffect.CallCloseSession -> "CallCloseSession"
            | SessionEffect.CallSupplyPin _ -> "CallSupplyPin"
            | SessionEffect.GoTo _ -> "GoTo"
            | SessionEffect.SetPatient p -> $"SetPatient %s{Part.patientOption p}"
            | SessionEffect.KeepKey _ -> "KeepKey"
            | SessionEffect.LoadCart s -> $"LoadCart %s{Part.signed s}"
            | SessionEffect.CallOpenVersion(id, from) -> $"CallOpenVersion %s{id} %s{Part.token from}"
            | SessionEffect.CallRefresh from -> $"CallRefresh %s{Part.token from}"
            | SessionEffect.TellVersionOpened h -> $"TellVersionOpened %s{Part.head h}"
            | SessionEffect.TellMovedOn h -> $"TellMovedOn %s{Part.head h}"
            | SessionEffect.TellRefreshFailed -> "TellRefreshFailed"


        /// The state as the pages read it; never the name the enrolment shows.
        let view (view: SessionView) =
            match view with
            | SessionView.Anonymous -> "Anonymous"
            | SessionView.Launching attempt -> $"Launching attempt %i{attempt}"
            | SessionView.Resuming -> "Resuming"
            | SessionView.Open so -> $"Open %s{Part.opened so}"
            | SessionView.Closing so -> $"Closing %s{Part.opened so}"
            | SessionView.Refused r -> $"Refused %s{Part.launchRefusal r}"
            | SessionView.Retryable r -> $"Retryable %s{Part.launchRefusal r}"
            | SessionView.Unreachable -> "Unreachable"
            | SessionView.Ended e -> $"Ended %s{Part.ending e}"
            | SessionView.Enrolling(_, r) -> $"Enrolling refusal %s{r |> Part.orNone Part.pinRefusal}"
            | SessionView.SupplyingPin _ -> "SupplyingPin"
            | SessionView.EnrolmentFailed r -> $"EnrolmentFailed %s{Part.pinRefusal r}"


        let state (state: SessionState) = state |> SessionState.view |> view


    /// The signing machine.
    module Signing =

        open SigningMachine


        /// Never the challenge, the token or the identity.
        let response (r: SigningResponse) =
            match r with
            | SigningResponse.ChallengeIssued _ -> "ChallengeIssued"
            | SigningResponse.DataNotice n -> $"DataNotice %s{Part.patientOption n.Data}"
            | SigningResponse.Submitted(s, _, _) -> $"Submitted %s{Part.signed s}"
            | SigningResponse.Refused r -> $"Refused %s{Part.signingRefusal r}"


        /// Never the PIN.
        let msg (msg: SigningMsg) =
            match msg with
            | SigningMsg.Sign(p, differences, request) ->
                $"Sign %s{Part.plan p} %i{differences.Length} differences %s{request}"
            | SigningMsg.ChallengeAnswered(request, r) ->
                $"ChallengeAnswered %s{request} %s{r |> Part.result response}"
            | SigningMsg.Accept held -> if held then "Accept holding the context" else "Accept"
            | SigningMsg.Confirm _ -> "Confirm"
            | SigningMsg.Cancel -> "Cancel"
            | SigningMsg.SubmitAnswered(_, r) -> $"SubmitAnswered %s{r |> Part.result response}"


        /// Never the challenge, the PIN, the notice token or the identity.
        let effect (effect: SigningEffect) =
            match effect with
            | SigningEffect.CallChallenge(p, notice, request) ->
                let withNotice = if notice.IsSome then " with notice" else ""
                $"CallChallenge %s{Part.plan p}%s{withNotice} %s{request}"
            | SigningEffect.CallSubmit(p, _, _, _) -> $"CallSubmit %s{Part.plan p}"
            | SigningEffect.RenewToken(_, p, _) -> $"RenewToken %s{Part.patient p}"
            | SigningEffect.EndSession e -> $"EndSession %s{Part.ending e}"
            | SigningEffect.SetPatient p -> $"SetPatient %s{Part.patient p}"
            | SigningEffect.TellSigned s -> $"TellSigned %s{Part.signed s}"
            | SigningEffect.TellRefused r -> $"TellRefused %s{Part.signingRefusal r}"
            | SigningEffect.TellError _ -> "TellError"


        let view (view: SigningView) =
            match view with
            | SigningView.Idle -> "Idle"
            | SigningView.Requesting -> "Requesting"
            | SigningView.Noticed(p, n) -> $"Noticed %s{Part.plan p} %s{Part.patientOption n.Data}"
            | SigningView.Challenged(p, r) ->
                $"Challenged %s{Part.plan p} refusal %s{r |> Part.orNone Part.signingRefusal}"
            | SigningView.Submitting p -> $"Submitting %s{Part.plan p}"


        let state (state: SigningState) = state |> SigningState.view |> view


    /// The order plan machine.
    module OrderPlan =

        open OrderPlanMachine


        /// Never the argumentation.
        let msg (msg: OrderPlanMsg) =
            match msg with
            | OrderPlanMsg.PatientChanged(p, request) -> $"PatientChanged %s{Part.patientOption p} %s{request}"
            | OrderPlanMsg.Version(s, request) -> $"Version %s{Part.signed s} %s{request}"
            | OrderPlanMsg.Command(cmd, request) -> $"Command %s{Part.planCommand Part.context cmd} %s{request}"
            | OrderPlanMsg.Answered(request, r) -> $"Answered %s{request} %s{r |> Part.result Part.plan}"
            | OrderPlanMsg.Select id -> $"Select %s{id |> Part.orNone string}"
            | OrderPlanMsg.Filter(ids, request) -> $"Filter %i{ids.Length} %s{request}"
            | OrderPlanMsg.Reopen(cmd, request) -> $"Reopen %s{Part.planCommand Part.context cmd} %s{request}"
            | OrderPlanMsg.Restore -> "Restore"
            | OrderPlanMsg.Signed -> "Signed"
            | OrderPlanMsg.Argue(id, _) -> $"Argue %s{id}"


        let effect (effect: OrderPlanEffect) =
            match effect with
            | OrderPlanEffect.CallPlan(cmd, request) -> $"CallPlan %s{Part.planCommand Part.contextId cmd} %s{request}"
            | OrderPlanEffect.CheckInteractions drugs -> $"CheckInteractions %i{drugs.Length} drugs"
            | OrderPlanEffect.GoToPlanPage -> "GoToPlanPage"
            | OrderPlanEffect.ResetWorkbench -> "ResetWorkbench"
            | OrderPlanEffect.TellError _ -> "TellError"


        let view (view: OrderPlanView) =
            match view with
            | OrderPlanView.NoPatient -> "NoPatient"
            | OrderPlanView.Settled(p, selected) -> $"Settled %s{Part.plan p} selected %s{selected |> Part.orNone string}"
            | OrderPlanView.Changing(p, selected) ->
                $"Changing %s{Part.plan p} selected %s{selected |> Part.orNone string}"


        let state (state: OrderPlanState) = state |> OrderPlanState.view |> view


    /// The order context machine.
    module OrderContext =

        open OrderContextMachine


        let response (r: OrderContextResponse) =
            match r with
            | OrderContextResponse.Evaluated ctx -> $"Evaluated %s{Part.context ctx}"
            | OrderContextResponse.Refused(ctx, r) -> $"Refused %s{Part.context ctx} %s{Part.contextRefusal r}"


        /// The command with the context as describeContext tells it.
        let command (describeContext: OrderContext -> string) (cmd: OrderContextCommand) (ctx: OrderContext) =
            $"%s{OrderContextCommand.toString (cmd, ctx)} %s{describeContext ctx}"


        /// Never the argumentation.
        let msg (msg: OrderContextMsg) =
            match msg with
            | OrderContextMsg.PatientChanged(p, request) -> $"PatientChanged %s{Part.patientOption p} %s{request}"
            | OrderContextMsg.Seed(ctx, request) -> $"Seed %s{Part.context ctx} %s{request}"
            | OrderContextMsg.Command(cmd, ctx, request) -> $"Command %s{command Part.context cmd ctx} %s{request}"
            | OrderContextMsg.Answered(request, r) -> $"Answered %s{request} %s{r |> Part.result response}"
            | OrderContextMsg.Reset request -> $"Reset %s{request}"
            | OrderContextMsg.Select id -> $"Select %s{id |> Part.orNone string}"
            | OrderContextMsg.Argue _ -> "Argue"
            | OrderContextMsg.Reopen(cmd, ctx, request) -> $"Reopen %s{command Part.context cmd ctx} %s{request}"
            | OrderContextMsg.Restore -> "Restore"


        let effect (effect: OrderContextEffect) =
            match effect with
            | OrderContextEffect.CallContext(cmd, ctx, request) ->
                $"CallContext %s{command Part.contextId cmd ctx} %s{request}"
            | OrderContextEffect.SyncFormulary _ -> "SyncFormulary"
            | OrderContextEffect.SyncParenteralia _ -> "SyncParenteralia"
            | OrderContextEffect.TellError _ -> "TellError"


        let view (view: OrderContextView) =
            match view with
            | OrderContextView.NoPatient -> "NoPatient"
            | OrderContextView.Settled ctx -> $"Settled %s{Part.contextId ctx}"
            | OrderContextView.Refused(ctx, r) -> $"Refused %s{Part.contextId ctx} %s{Part.contextRefusal r}"
            | OrderContextView.Changing ctx -> $"Changing %s{Part.contextId ctx}"


        let state (state: OrderContextState) = state |> OrderContextState.view |> view


    /// One step of a machine, described with that machine's describers.
    let step machine describeMsg describeEffect describeState (no: int) (at: DateTime) msg (state, effects) =
        {
            No = no
            At = at
            Machine = machine
            Msg = describeMsg msg
            Effects = effects |> List.map describeEffect
            State = describeState state
        }


    let session = step "Session" Session.msg Session.effect Session.state

    let signing = step "Signing" Signing.msg Signing.effect Signing.state

    let orderPlan = step "OrderPlan" OrderPlan.msg OrderPlan.effect OrderPlan.state

    let orderContext = step "OrderContext" OrderContext.msg OrderContext.effect OrderContext.state


open Expecto
open Expecto.Flip
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine


let at = DateTime(2026, 9, 30, 10, 41, 7, 311)

/// A patient of 3 years and 14 kg.
let pat =
    { Patient.empty with
        Age =
            Some
                {
                    Years = 3<year>
                    Months = 0<month>
                    Weeks = 0<week>
                    Days = 0<day>
                }
        Weight =
            { Patient.empty.Weight with
                Measured = Some 14000<gram>
            }
    }

let ctx = OrderContext.empty

let ctxPicked =
    { ctx with
        Id = "ctx-1"
        Filter =
            { ctx.Filter with
                Indication = Some "pain"
                Generic = Some "paracetamol"
                Route = Some "oral"
            }
    }

let identity: NameAndBirthDate =
    {
        Name = "Jan Jansen"
        BirthYear = 2023
        BirthMonth = 5
        BirthDay = 17
    }

/// Every value that identifies a person or opens a door, which no line may show.
let secrets = [ "Jan Jansen"; "2023"; "1234"; "code-987"; "secret-token"; "Dr. Bakker"; "j***@hospital.nl" ]


let tests =
    testList
        "Trail"
        [
            test "a step is one line with its number, time, machine, message, effects and state" {
                let state, effects =
                    OrderContextState.noPatient
                    |> OrderContextState.transition (OrderContextMsg.PatientChanged(Some pat, "r-1"))

                Trail.orderContext 12 at (OrderContextMsg.PatientChanged(Some pat, "r-1")) (state, effects)
                |> Trail.format
                |> Expect.equal
                    "the line"
                    "#12 10:41:07.311 OrderContext PatientChanged patient 3.0 y 14.0 kg r-1 -> CallContext UpdateOrderContext workbench r-1 | Changing workbench"
            }

            test "a step without effects says none" {
                Trail.format
                    {
                        No = 1
                        At = at
                        Machine = "Signing"
                        Msg = "Cancel"
                        Effects = []
                        State = "Idle"
                    }
                |> Expect.equal "none" "#1 10:41:07.311 Signing Cancel -> none | Idle"
            }

            test "a context shows its id, its picks and its scenarios" {
                ctxPicked |> Trail.Part.context |> Expect.equal "the context" "ctx-1 pain/paracetamol/oral 0 scenarios"
            }

            test "the trail keeps the newest lines, oldest first" {
                [ "a"; "b"; "c" ]
                |> List.fold (fun lines line -> Trail.append 2 line lines) []
                |> Expect.equal "the newest two" [ "b"; "c" ]
            }

            testList
                "no line shows an identity, a PIN, a code, a token or a free text"
                [
                    let token = Some(OpenedToken "secret-token")

                    let user: UserContext =
                        {
                            UserId = "u-1"
                            DisplayName = "Dr. Bakker"
                            Role = UserRole.Prescriber
                        }

                    let opened: SessionOpened =
                        {
                            User = Some user
                            PatientContext =
                                Some
                                    {
                                        PatientId = "1234"
                                        Identity = Some identity
                                        Patient = Some pat
                                    }
                            OpenedToken = token
                            KeyThumbprint = Some "thumb"
                            Head = None
                        }

                    let lines =
                        [
                            Trail.Session.msg (SessionMsg.SupplyPin("code-987", "1234"))
                            Trail.Session.msg (SessionMsg.TokenRenewed(OpenedToken "secret-token", pat, Some identity))
                            Trail.Session.msg (SessionMsg.PinAnswered(Ok(PinOutcome.Opened opened)))
                            Trail.Session.effect (SessionEffect.CallSupplyPin("code-987", "1234"))
                            Trail.Session.view (
                                SessionView.Enrolling(
                                    {
                                        DisplayName = "Jan Jansen"
                                        MailHint = "j***@hospital.nl"
                                    },
                                    None
                                )
                            )
                            Trail.Signing.msg (SigningMsg.Confirm("1234", "key-1"))
                            Trail.Signing.effect (SigningEffect.CallSubmit(OrderPlan.empty, "secret-token", "1234", "key-1"))
                            Trail.Signing.effect (SigningEffect.RenewToken(OpenedToken "secret-token", pat, Some identity))
                            Trail.OrderContext.msg (OrderContextMsg.Argue "Jan Jansen weighs more")
                            Trail.OrderPlan.msg (OrderPlanMsg.Argue("ctx-1", "Jan Jansen weighs more"))
                        ]

                    for line in lines do
                        test line {
                            for secret in secrets do
                                line.Contains secret |> Expect.isFalse $"no %s{secret}"
                        }
                ]

            test "every machine names itself" {
                let plan = OrderPlanState.noPatient
                let signing = SigningState.idle
                let session = SessionState.anonymous

                [
                    Trail.orderPlan 1 at OrderPlanMsg.Restore (plan, []) |> _.Machine
                    Trail.signing 2 at SigningMsg.Cancel (signing, []) |> _.Machine
                    Trail.session 3 at SessionMsg.Resume (session, []) |> _.Machine
                ]
                |> Expect.equal "the machines" [ "OrderPlan"; "Signing"; "Session" ]
            }
        ]


runTestsWithCLIArgs [ CLIArguments.Summary ] [||] tests


// An example: a patient arrives, the server answers, the user picks a generic and the server
// refuses the context; each step through the order context machine, as the trail shows it.
[
    OrderContextMsg.PatientChanged(Some pat, "r-1")
    OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated ctx))
    OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, ctxPicked, "r-2")
    OrderContextMsg.Answered("r-2", Ok(OrderContextResponse.Refused(ctxPicked, OrderContextRefusal.NoProducts)))
]
|> List.mapFold
    (fun (no, state) msg ->
        let state', effects = OrderContextState.transition msg state
        let line = Trail.orderContext no (at.AddSeconds(float no)) msg (state', effects) |> Trail.format
        line, (no + 1, state')
    )
    (1, OrderContextState.noPatient)
|> fst
|> List.iter (printfn "%s")
