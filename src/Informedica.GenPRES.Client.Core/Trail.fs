namespace Informedica.GenPRES.Client.Core

/// A readable trail of the client state machines: one line per machine step, with the message, the
/// effects it produced and the state it reached, to check the logic in the order it ran. Every
/// message, effect and state is described by hand, without reflection, so the browser and .NET
/// print the same line. The caller passes the time and the step's number; the trail reads no clock.
///
/// A line never holds a patient's identity, a user's name, a PIN, a code, a token, a url, an error
/// text or the argumentation: a patient shows as age and weight, a version as its number.
module Trail =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open System
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api


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


    /// Replaces every line break and other control character with a space, so that a step stays on one line.
    let oneLine (text: string) =
        text |> String.map (fun c -> if Char.IsControl c then ' ' else c)


    /// The step as one line of the trail.
    let format (step: Step) =
        let effects =
            match step.Effects with
            | [] -> "none"
            | xs -> xs |> String.concat ", "

        let time = step.At.ToString "HH:mm:ss.fff"

        $"#%i{step.No} %s{time} %s{step.Machine} %s{step.Msg} -> %s{effects} | %s{step.State}"
        |> oneLine


    /// Adds the line to the trail and keeps the newest max lines, oldest first.
    let append (max: int) (line: string) (lines: string list) =
        let lines = lines @ [ line ]
        let drop = lines.Length - max
        if drop > 0 then lines |> List.skip drop else lines


    /// The parts a message, an effect or a state is described with.
    [<RequireQualifiedAccess>]
    module Part =

        /// An id or a request id by its first 8 characters: enough to follow it through the trail and to find
        /// it in the server log.
        let shortId (id: string) = if id.Length > 8 then id.Substring(0, 8) else id


        /// The word when the condition is true, None otherwise.
        let flag word condition = if condition then Some word else None


        /// Why the server refused the order context.
        let contextRefusal (r: OrderContextRefusal) =
            match r with
            | OrderContextRefusal.NoDoseRules -> "NoDoseRules"
            | OrderContextRefusal.NoDoseRulesForPatient -> "NoDoseRulesForPatient"
            | OrderContextRefusal.NoProducts -> "NoProducts"
            | OrderContextRefusal.MeasurementMissing -> "MeasurementMissing"
            | OrderContextRefusal.NoDiluent -> "NoDiluent"
            | OrderContextRefusal.NotSolved -> "NotSolved"
            | OrderContextRefusal.NoAdjustedDose -> "NoAdjustedDose"


        /// The case name of a snackbar alert.
        let alert (a: Alert.Alert) =
            match a with
            | Alert.Alert.DrugNamesNotLoaded -> "DrugNamesNotLoaded"
            | Alert.Alert.InteractionsFound n -> $"InteractionsFound %i{n}"
            | Alert.Alert.InvalidPassword -> "InvalidPassword"
            | Alert.Alert.CloseFailed -> "CloseFailed"
            | Alert.Alert.PinNotSent -> "PinNotSent"
            | Alert.Alert.RequestFailed -> "RequestFailed"
            | Alert.Alert.NoPatientForMedication -> "NoPatientForMedication"
            | Alert.Alert.LaunchNotOpened -> "LaunchNotOpened"
            | Alert.Alert.PatientChangeFailed _ -> "PatientChangeFailed"
            | Alert.Alert.WorkbenchFailed _ -> "WorkbenchFailed"
            | Alert.Alert.SigningSendFailed -> "SigningSendFailed"
            | Alert.Alert.OrderPlanSigned _ -> "OrderPlanSigned"
            | Alert.Alert.SigningRefused _ -> "SigningRefused"
            | Alert.Alert.SignedPlanOpened _ -> "SignedPlanOpened"
            | Alert.Alert.NewerSignedPlan _ -> "NewerSignedPlan"
            | Alert.Alert.PatientRefreshFailed -> "PatientRefreshFailed"
            | Alert.Alert.ChangeNotApplied r -> $"ChangeNotApplied %s{contextRefusal r}"
            | Alert.Alert.PatientDataChanged summary ->
                $"PatientDataChanged %i{summary.Orders} orders %i{summary.Marked} marked"


        /// Describes the value, or returns none when there is no value.
        let orNone (describe: 'a -> string) (x: 'a option) = x |> Option.map describe |> Option.defaultValue "none"


        /// Describes an Ok result with its value, or an Error result without the error text.
        let result (describe: 'a -> string) (r: Result<'a, 'e>) =
            match r with
            | Ok x -> $"Ok %s{describe x}"
            | Error _ -> "Error"


        /// Describes the patient by age, gestational age, weight, height and gender, with est before an estimated
        /// weight or height. The trail shows no identity. The department and the location are left out, because
        /// both can arrive as free text in the url and the location can name a bed.
        let patient (p: PatientData) =
            let age =
                p
                |> PatientRead.getAgeInYears
                |> Option.map (fun a -> $"%.1f{a} y")
                |> Option.defaultValue "no age"

            let gestationalAge =
                p.GestationalAge
                |> GestationalAge.value
                |> Option.map (fun ga -> $" GA %i{int ga.Weeks}+%i{int ga.Days}")
                |> Option.defaultValue ""

            let weight =
                match p.Weight.Measured, p.Weight.Estimated with
                | Some g, _ -> $"%.1f{float (int g) / 1000.} kg"
                | None, Some g -> $"est %.1f{float (int g) / 1000.} kg"
                | None, None -> "no weight"

            let height =
                match p.Height.Measured, p.Height.Estimated with
                | Some cm, _ -> $"%i{int cm} cm"
                | None, Some cm -> $"est %i{int cm} cm"
                | None, None -> "no height"

            let gender =
                match p.Gender with
                | Male -> "male"
                | Female -> "female"
                | UnknownGender -> "gender unknown"

            $"patient %s{age}%s{gestationalAge} %s{weight} %s{height} %s{gender}"


        /// Describes the patient as patient does, or returns no patient.
        let patientOption (p: PatientData option) = p |> Option.map patient |> Option.defaultValue "no patient"


        /// The dose type with its name.
        let doseType (dt: DoseType) =
            let named kind (name: string) =
                if String.IsNullOrWhiteSpace name then
                    kind
                else
                    $"%s{kind} %s{name.Trim()}"

            match dt with
            | Once s -> named "once" s
            | Discontinuous s -> named "discontinuous" s
            | Continuous s -> named "continuous" s
            | Timed s -> named "timed" s
            | OnceTimed s -> named "once timed" s
            | NoDoseType -> "no dose type"


        /// The chosen values of a filter, in the order the workbench asks for them.
        let picks (f: Filter) =
            [
                f.Indication
                f.Generic
                f.Route
                f.Form
                f.DoseType |> Option.map doseType
                f.Diluent
            ]
            |> List.choose id


        /// The chosen values of a filter as one text, or no picks when there are none.
        let filter (f: Filter) =
            match picks f with
            | [] -> "no picks"
            | xs -> xs |> String.concat "/"


        /// An order context by its short id, or workbench for the workbench, which has no id.
        let contextId (c: OrderContext) =
            if String.IsNullOrEmpty c.Id then
                "workbench"
            else
                shortId c.Id


        /// A scenario by its short order id and its component; never its texts.
        let scenario (sc: OrderScenario) =
            let cmp = sc.Component |> Option.defaultValue "none"

            $"%s{shortId sc.Order.Id} cmp %s{cmp}"


        /// Describes an order context by its id, its chosen values and the number of scenarios, with the
        /// scenario when there is only one; never the argumentation.
        let context (c: OrderContext) =
            match c.Scenarios with
            | [| sc |] -> $"%s{contextId c} %s{filter c.Filter} 1 scenario %s{scenario sc}"
            | scs -> $"%s{contextId c} %s{filter c.Filter} %i{scs.Length} scenarios"


        /// A plan by the number of its contexts; never the orders.
        let plan (p: OrderPlan) = $"plan %i{p.OrderContexts.Length} contexts"


        /// A version by its number; never the user who signed it.
        let head (h: OrderPlanHead) = $"v%i{h.No}"


        /// A signed version by its number and the number of its contexts; never the identity.
        let signed (s: SignedOrderPlan) = $"%s{head s.Head} %i{s.OrderContexts.Length} contexts"


        /// Whether a token came along; never the token.
        let token (t: OpenedToken option) =
            match t with
            | Some _ -> "token"
            | None -> "no token"


        /// Describes a session by whether a user opened it, whether it has a patient context, and its signed
        /// version; never the user or the patient id.
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


        /// Why the server refused the launch.
        let launchRefusal (r: LaunchRefusal) =
            match r with
            | LaunchRefusal.LaunchExpired -> "LaunchExpired"
            | LaunchRefusal.LaunchSpent -> "LaunchSpent"
            | LaunchRefusal.LaunchInvalid -> "LaunchInvalid"
            | LaunchRefusal.NoBrowserIdentity -> "NoBrowserIdentity"
            | LaunchRefusal.NoRole -> "NoRole"
            | LaunchRefusal.WrongActivePatient -> "WrongActivePatient"
            | LaunchRefusal.EnrolmentRequired -> "EnrolmentRequired"


        /// Why the session ended.
        let ending (e: SessionEnding) =
            match e with
            | SessionEnding.SupersededByLaunch -> "SupersededByLaunch"
            | SessionEnding.WrongPinLimit -> "WrongPinLimit"
            | SessionEnding.Unreadable -> "Unreadable"
            | SessionEnding.Idle -> "Idle"
            | SessionEnding.InactiveTab -> "InactiveTab"


        /// Why the server refused the PIN or the code.
        let pinRefusal (r: PinRefusal) =
            match r with
            | PinRefusal.WrongCode left -> $"WrongCode %i{left} left"
            | PinRefusal.CodeVoid -> "CodeVoid"
            | PinRefusal.AttemptExpired -> "AttemptExpired"
            | PinRefusal.PinFormat -> "PinFormat"
            | PinRefusal.WrongActivePatient -> "WrongActivePatient"


        /// Why the server refused the signature.
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


        /// Describes the record notice that a reply carries besides its answer.
        let notice (n: RecordNotice) =
            match n with
            | RecordNotice.NewerVersion h -> $"NewerVersion %s{head h}"
            | RecordNotice.Ended e -> $"Ended %s{ending e}"


        /// Describes a change to the plan by its kind; never the plan.
        let change (c: OrderPlanMachine.OrderPlanChange) =
            match c with
            | OrderPlanMachine.OrderPlanChange.Add(ctx, linked) -> $"Add %s{context ctx} %i{linked.Length} linked"
            | OrderPlanMachine.OrderPlanChange.AddMany contexts -> $"AddMany %i{contexts.Length}"
            | OrderPlanMachine.OrderPlanChange.Remove ids -> $"Remove %i{ids.Length}"
            | OrderPlanMachine.OrderPlanChange.FilterRows ids -> $"FilterRows %i{ids.Length}"
            | OrderPlanMachine.OrderPlanChange.OrderDialogCommand(id, cmd) ->
                $"OrderDialogCommand %s{shortId id} %s{OrderViewCommand.toString (cmd, OrderContext.empty)}"


        /// Describes a plan command, with a context as describeContext describes it; never the plan.
        let planCommand (describeContext: OrderContext -> string) (cmd: OrderPlanCommand) =
            match cmd with
            | OrderPlanCommand.UpdatePatientData _ -> "UpdatePatientData"
            | OrderPlanCommand.FilterRows(ids, _) -> $"FilterRows %i{ids.Length}"
            | OrderPlanCommand.EvaluateContext(_, id, ctxCmd, ctx) ->
                // an effect describes the context by its id, which the line already shows
                let described = describeContext ctx
                let context = if described = shortId id then "" else $" %s{described}"
                $"EvaluateContext %s{shortId id} %s{OrderViewCommand.toString (ctxCmd, ctx)}%s{context}"
            | OrderPlanCommand.AddOrderContext(_, ctx, _) -> $"AddOrderContext %s{describeContext ctx}"
            | OrderPlanCommand.AddOrderContexts(_, contexts) -> $"AddOrderContexts %i{contexts.Length}"
            | OrderPlanCommand.RemoveOrderContexts(_, ids) -> $"RemoveOrderContexts %i{ids.Length}"
            | OrderPlanCommand.Open(_, contexts) -> $"Open %i{contexts.Length} contexts"


    /// The session machine.
    [<RequireQualifiedAccess>]
    module Session =

        open SessionMachine


        /// The server's answer to a launch; never the url.
        let launchOutcome (o: LaunchOutcome) =
            match o with
            | LaunchOutcome.Opened so -> $"Opened %s{Part.opened so}"
            | LaunchOutcome.RedirectTo _ -> "RedirectTo"
            | LaunchOutcome.Refused r -> $"Refused %s{Part.launchRefusal r}"


        /// The server's answer when the session was read again; never the enrolment's name.
        let resumeResult (r: ResumeResult) =
            match r with
            | ResumeResult.Found so -> $"Found %s{Part.opened so}"
            | ResumeResult.NotFound -> "NotFound"
            | ResumeResult.Ended e -> $"Ended %s{Part.ending e}"
            | ResumeResult.Enrolling _ -> "Enrolling"


        /// The server's answer to a PIN.
        let pinOutcome (o: PinOutcome) =
            match o with
            | PinOutcome.Opened so -> $"Opened %s{Part.opened so}"
            | PinOutcome.Refused r -> $"Refused %s{Part.pinRefusal r}"


        /// The server's answer to an open or a refresh of a version.
        let reopened (r: Result<SessionOpened option, string>) = r |> Part.result (Part.orNone Part.opened)


        /// Describes a session message by its name and its answer. Never shows the launch, the key, the mailed
        /// code, the PIN, the token or the person the session is for.
        let msg (msg: SessionMsg) =
            match msg with
            | SessionMsg.PresentLaunch _ -> "PresentLaunch"
            | SessionMsg.LaunchOutcome(_, _, r) -> $"LaunchOutcome %s{r |> Part.result launchOutcome}"
            | SessionMsg.RetryLaunch -> "RetryLaunch"
            | SessionMsg.Resume -> "Resume"
            | SessionMsg.Resumed r -> $"Resumed %s{r |> Part.result resumeResult}"
            | SessionMsg.SupplyPin _ -> "SupplyPin"
            | SessionMsg.PinAnswered r -> $"PinAnswered %s{r |> Part.result pinOutcome}"
            | SessionMsg.LaunchRefused r -> $"LaunchRefused %s{Part.launchRefusal r}"
            | SessionMsg.ContinueAnonymous -> "ContinueAnonymous"
            | SessionMsg.UrlMovedOn -> "UrlMovedOn"
            | SessionMsg.CloseSession -> "CloseSession"
            | SessionMsg.SessionClosed -> "SessionClosed"
            | SessionMsg.CloseFailed _ -> "CloseFailed"
            | SessionMsg.SignatureEndedSession e -> $"SignatureEndedSession %s{Part.ending e}"
            | SessionMsg.SignatureRenewedToken(_, p, _) -> $"SignatureRenewedToken %s{Part.patient p}"
            | SessionMsg.OpenSignedPlan id -> $"OpenSignedPlan %s{Part.shortId id}"
            | SessionMsg.SignedPlanOpened(from, r) -> $"SignedPlanOpened %s{Part.token from} %s{reopened r}"
            | SessionMsg.RefreshPatient -> "RefreshPatient"
            | SessionMsg.InactiveTab from -> $"InactiveTab %s{Part.token from}"
            | SessionMsg.PatientRefreshed(from, r) -> $"PatientRefreshed %s{Part.token from} %s{reopened r}"
            | SessionMsg.NoticeReceived(from, n) -> $"NoticeReceived %s{Part.token from} %s{Part.notice n}"
            | SessionMsg.SignatureBlocked h -> $"SignatureBlocked %s{Part.head h}"


        /// Describes a session effect; never the url, the key, the code or the PIN.
        let effect (effect: SessionEffect) =
            match effect with
            | SessionEffect.CallPresentLaunch _ -> "CallPresentLaunch"
            | SessionEffect.CallResume -> "CallResume"
            | SessionEffect.CallCloseSession _ -> "CallCloseSession"
            | SessionEffect.CallSupplyPin _ -> "CallSupplyPin"
            | SessionEffect.GoToIdentityProvider _ -> "GoToIdentityProvider"
            | SessionEffect.SetPatientData p -> $"SetPatientData %s{Part.patientOption p}"
            | SessionEffect.SetPatientDataRead p -> $"SetPatientDataRead %s{Part.patientOption p}"
            | SessionEffect.TakeAge p -> $"TakeAge %s{Part.patientOption p}"
            | SessionEffect.KeepBrowserKey _ -> "KeepBrowserKey"
            | SessionEffect.LoadSignedPlan s -> $"LoadSignedPlan %s{Part.signed s}"
            | SessionEffect.CallOpenSignedPlan(id, from) ->
                $"CallOpenSignedPlan %s{Part.shortId id} %s{Part.token from}"
            | SessionEffect.CallRefreshPatient from -> $"CallRefreshPatient %s{Part.token from}"
            | SessionEffect.TellSignedPlanOpened h -> $"TellSignedPlanOpened %s{Part.head h}"
            | SessionEffect.TellNewerSignedPlan h -> $"TellNewerSignedPlan %s{Part.head h}"
            | SessionEffect.TellPatientRefreshFailed -> "TellPatientRefreshFailed"
            | SessionEffect.Alert a -> $"Alert %s{Part.alert a}"


        /// Describes the session view; never the name the enrolment shows.
        let view (view: SessionView) =
            match view with
            | SessionView.Anonymous -> "Anonymous"
            | SessionView.Launching attempt -> $"Launching attempt %i{attempt}"
            | SessionView.Resuming -> "Resuming"
            | SessionView.Open so -> $"Open %s{Part.opened so}"
            | SessionView.Closing so -> $"Closing %s{Part.opened so}"
            | SessionView.LaunchRefused r -> $"LaunchRefused %s{Part.launchRefusal r}"
            | SessionView.LaunchRetryable r -> $"LaunchRetryable %s{Part.launchRefusal r}"
            | SessionView.ServerUnreachable -> "ServerUnreachable"
            | SessionView.Ended e -> $"Ended %s{Part.ending e}"
            | SessionView.Enrolling(_, r) -> $"Enrolling refusal %s{r |> Part.orNone Part.pinRefusal}"
            | SessionView.SupplyingPin _ -> "SupplyingPin"
            | SessionView.EnrolmentFailed r -> $"EnrolmentFailed %s{Part.pinRefusal r}"


        /// The session state, through the view the pages read.
        let state (state: SessionState) = state |> SessionState.view |> view


    /// The signing machine.
    [<RequireQualifiedAccess>]
    module Signing =

        open SigningMachine


        /// Describes a signing response; never the challenge, the token or the identity.
        let response (r: SigningResponse) =
            match r with
            | SigningResponse.ChallengeIssued _ -> "ChallengeIssued"
            | SigningResponse.DataNotice n -> $"DataNotice %s{Part.patientOption n.PatientData}"
            | SigningResponse.Submitted(s, _, _) -> $"Submitted %s{Part.signed s}"
            | SigningResponse.Refused r -> $"Refused %s{Part.signingRefusal r}"


        /// Describes a signing message; never the PIN.
        let msg (msg: SigningMsg) =
            match msg with
            | SigningMsg.Sign(p, differences, request) ->
                $"Sign %s{Part.plan p} %i{differences.Length} differences %s{Part.shortId request}"
            | SigningMsg.ChallengeAnswered(request, r) ->
                $"ChallengeAnswered %s{Part.shortId request} %s{r |> Part.result response}"
            | SigningMsg.TakeEhrData -> "TakeEhrData"
            | SigningMsg.KeepPlanData -> "KeepPlanData"
            | SigningMsg.ConfirmPin _ -> "ConfirmPin"
            | SigningMsg.Cancel -> "Cancel"
            | SigningMsg.SubmitAnswered(_, r) -> $"SubmitAnswered %s{r |> Part.result response}"
            | SigningMsg.SessionEnded -> "SessionEnded"


        /// Describes a signing effect; never the challenge, the PIN, the notice token or the identity.
        let effect (effect: SigningEffect) =
            match effect with
            | SigningEffect.CallChallenge(p, notice, request) ->
                let withNotice = if notice.IsSome then " with notice" else ""
                $"CallChallenge %s{Part.plan p}%s{withNotice} %s{Part.shortId request}"
            | SigningEffect.CallSubmit(p, _, _, _) -> $"CallSubmit %s{Part.plan p}"
            | SigningEffect.RenewSessionToken(_, p, _) -> $"RenewSessionToken %s{Part.patient p}"
            | SigningEffect.EndSession e -> $"EndSession %s{Part.ending e}"
            | SigningEffect.TellSigned s -> $"TellSigned %s{Part.signed s}"
            | SigningEffect.TellRefused r -> $"TellRefused %s{Part.signingRefusal r}"
            | SigningEffect.TellError _ -> "TellError"
            | SigningEffect.ReadEhrAgain -> "ReadEhrAgain"


        /// Describes the signing view; never the challenge.
        let view (view: SigningView) =
            match view with
            | SigningView.Idle -> "Idle"
            | SigningView.RequestingChallenge -> "RequestingChallenge"
            | SigningView.DataChanged(p, n) -> $"DataChanged %s{Part.plan p} %s{Part.patientOption n.PatientData}"
            | SigningView.AskingPin(p, r) ->
                $"AskingPin %s{Part.plan p} refusal %s{r |> Part.orNone Part.signingRefusal}"
            | SigningView.Submitting p -> $"Submitting %s{Part.plan p}"


        /// The signing state, through the view the dialog reads.
        let state (state: SigningState) = state |> SigningState.view |> view


    /// The order plan machine.
    [<RequireQualifiedAccess>]
    module OrderPlan =

        open OrderPlanMachine


        /// Describes an order plan message; never the argumentation.
        let msg (msg: OrderPlanMsg) =
            match msg with
            | OrderPlanMsg.PatientDataChanged(p, request) ->
                $"PatientDataChanged %s{Part.patientOption p} %s{Part.shortId request}"
            | OrderPlanMsg.OpenSignedPlan(s, request) -> $"OpenSignedPlan %s{Part.signed s} %s{Part.shortId request}"
            | OrderPlanMsg.Change(change, request) -> $"Change %s{Part.change change} %s{Part.shortId request}"
            | OrderPlanMsg.Answered(request, r) -> $"Answered %s{Part.shortId request} %s{r |> Part.result Part.plan}"
            | OrderPlanMsg.SelectContext id -> $"SelectContext %s{id |> Part.orNone Part.shortId}"
            | OrderPlanMsg.RestoreField -> "RestoreField"
            | OrderPlanMsg.Signed -> "Signed"


        /// Describes an order plan effect, with a context by its id; never the error texts.
        let effect (effect: OrderPlanEffect) =
            match effect with
            | OrderPlanEffect.CallPlan(cmd, request) ->
                $"CallPlan %s{Part.planCommand Part.contextId cmd} %s{Part.shortId request}"
            | OrderPlanEffect.CheckInteractions drugs -> $"CheckInteractions %i{drugs.Length} drugs"
            | OrderPlanEffect.TellError _ -> "TellError"
            | OrderPlanEffect.TellAnswered -> "TellAnswered"
            | OrderPlanEffect.TellPatientDataChanged summary ->
                $"TellPatientDataChanged %i{summary.Orders} orders %i{summary.Marked} marked"
            | OrderPlanEffect.PatientDataNotFollowed -> "PatientDataNotFollowed"
            | OrderPlanEffect.OrderAdded None -> "OrderAdded"
            | OrderPlanEffect.OrderAdded(Some slotId) -> $"OrderAdded %s{slotId}"


        /// The order plan state as the pages read it.
        let view (view: OrderPlanView) =
            match view with
            | OrderPlanView.NoPatientData -> "NoPatientData"
            | OrderPlanView.Settled(p, selected) ->
                $"Settled %s{Part.plan p} selected %s{selected |> Part.orNone Part.shortId}"
            | OrderPlanView.Changing(p, selected) ->
                $"Changing %s{Part.plan p} selected %s{selected |> Part.orNone Part.shortId}"


        /// Describes the order plan state by its view, the request it awaits and whether it is recalculating.
        let state (state: OrderPlanState) =
            [
                state |> OrderPlanState.view |> view |> Some
                state
                |> OrderPlanState.inFlightRequest
                |> Option.map (fun r -> $"awaits %s{Part.shortId r}")
                state |> OrderPlanState.isRecalculating |> Part.flag "recalculating"
            ]
            |> List.choose id
            |> String.concat " "


    /// The order context machine.
    [<RequireQualifiedAccess>]
    module OrderContext =

        open FilterSeed
        open OrderContextMachine


        /// The server's answer to an order context command.
        let response (r: OrderContextResponse) =
            match r with
            | OrderContextResponse.Evaluated(ctx, linked) -> $"Evaluated %s{Part.context ctx} %i{linked.Length} offers"
            | OrderContextResponse.Refused(ctx, r) -> $"Refused %s{Part.context ctx} %s{Part.contextRefusal r}"


        /// Where a seed comes from.
        let seedSource (source: SeedSource) =
            match source with
            | SeedSource.Url -> "url"
            | SeedSource.Formulary -> "formulary"
            | SeedSource.Parenteralia -> "parenteralia"
            | SeedSource.Reload -> "reload"


        /// Counts the choices a seed sets; never their text.
        let seedChoices (seed: FilterSeed) =
            [ seed.Indication; seed.Generic; seed.Route; seed.Form ]
            |> List.filter Option.isSome
            |> List.length
            |> (+) (if seed.DoseType.IsSome then 1 else 0)


        /// Describes the command, with the context as describeContext describes it. A seed shows only the number
        /// of its choices, because the choices come from the url or a list before the server has checked them.
        let command (describeContext: OrderContext -> string) (cmd: OrderViewCommand) (ctx: OrderContext) =
            match cmd with
            | OrderViewCommand.SeedFilter(source, ind, gen, rte, frm, dt) ->
                let seed =
                    {
                        Source = source
                        Indication = ind
                        Generic = gen
                        Route = rte
                        Form = frm
                        DoseType = dt
                    }

                $"SeedFilter %s{seedSource source} %i{seedChoices seed} choices %s{describeContext ctx}"
            | _ -> $"%s{OrderViewCommand.toString (cmd, ctx)} %s{describeContext ctx}"


        /// Describes the command without a context, as a message from the page carries it.
        let commandAlone (cmd: OrderViewCommand) = (command (fun _ -> "") cmd OrderContext.empty).TrimEnd()


        /// Describes an order context message; never the argumentation.
        let msg (msg: OrderContextMsg) =
            match msg with
            | OrderContextMsg.PatientDataChanged(p, request) ->
                $"PatientDataChanged %s{Part.patientOption p} %s{Part.shortId request}"
            | OrderContextMsg.SeedFilter(seed, request) ->
                $"SeedFilter %s{seedSource seed.Source} %i{seedChoices seed} choices %s{Part.shortId request}"
            | OrderContextMsg.Command(cmd, request) -> $"Command %s{commandAlone cmd} %s{Part.shortId request}"
            | OrderContextMsg.Answered(request, r) -> $"Answered %s{Part.shortId request} %s{r |> Part.result response}"
            | OrderContextMsg.Reset request -> $"Reset %s{Part.shortId request}"
            | OrderContextMsg.SelectScenario id -> $"SelectScenario %s{id |> Part.orNone Part.shortId}"
            | OrderContextMsg.RestoreField -> "RestoreField"
            | OrderContextMsg.TurnOfferOn index -> $"TurnOfferOn %i{index}"
            | OrderContextMsg.TurnOfferOff index -> $"TurnOfferOff %i{index}"
            | OrderContextMsg.OfferCommand(index, cmd, request) ->
                $"OfferCommand %i{index} %s{commandAlone cmd} %s{Part.shortId request}"
            | OrderContextMsg.Chosen ctx -> $"Chosen %s{Part.context ctx}"


        /// Describes an order context effect, with a context by its id; never the filter or the error texts.
        let effect (effect: OrderContextEffect) =
            match effect with
            | OrderContextEffect.CallContext(OrderContextCommand.Command(cmd, ctx), request) ->
                $"CallContext %s{command Part.contextId cmd ctx} %s{Part.shortId request}"
            | OrderContextEffect.SyncPages _ -> "SyncPages"
            | OrderContextEffect.TellError _ -> "TellError"
            | OrderContextEffect.TellNotApplied r -> $"TellNotApplied %s{Part.contextRefusal r}"


        /// Describes the order context view, with a context by its id.
        let view (view: OrderContextView) =
            match view with
            | OrderContextView.NoPatientData -> "NoPatientData"
            | OrderContextView.Settled ctx -> $"Settled %s{Part.contextId ctx}"
            | OrderContextView.Refused(ctx, r) -> $"Refused %s{Part.contextId ctx} %s{Part.contextRefusal r}"
            | OrderContextView.Changing ctx -> $"Changing %s{Part.contextId ctx}"


        /// Describes the order context state by its view, the request it awaits and whether it is
        /// recalculating.
        let state (state: OrderContextState) =
            [
                state |> OrderContextState.view |> view |> Some
                state
                |> OrderContextState.inFlightRequest
                |> Option.map (fun r -> $"awaits %s{Part.shortId r}")
                state |> OrderContextState.isRecalculating |> Part.flag "recalculating"
            ]
            |> List.choose id
            |> String.concat " "


    /// The patient machine.
    [<RequireQualifiedAccess>]
    module Patient =

        open PatientMachine


        /// Describes a patient message, with the patient data as Part.patient describes it; never the identity.
        let msg (msg: PatientMsg) =
            match msg with
            | PatientMsg.Changed(p, request) -> $"Changed %s{Part.patientOption p} %s{Part.shortId request}"
            | PatientMsg.Cleared -> "Cleared"
            | PatientMsg.Edited(edit, p, request) ->
                $"Edited %A{edit} %s{Part.patientOption p} %s{Part.shortId request}"
            | PatientMsg.Answered(request, r) -> $"Answered %s{Part.shortId request} %s{r |> Part.result Part.patient}"
            | PatientMsg.Refreshed(p, request) -> $"Refreshed %s{Part.patientOption p} %s{Part.shortId request}"
            | PatientMsg.AgeTaken(p, request) -> $"AgeTaken %s{Part.patientOption p} %s{Part.shortId request}"
            | PatientMsg.PlanNotFollowed -> "PlanNotFollowed"
            | PatientMsg.GiveAgain request -> $"GiveAgain %s{Part.shortId request}"


        /// Describes a patient effect; never the error texts.
        let effect (effect: PatientEffect) =
            match effect with
            | PatientEffect.CallPatient(p, request) -> $"CallPatient %s{Part.patient p} %s{Part.shortId request}"
            | PatientEffect.CallEdit(edit, p, request) ->
                $"CallEdit %A{edit} %s{Part.patient p} %s{Part.shortId request}"
            | PatientEffect.SetPatientData p -> $"SetPatientData %s{Part.patientOption p}"
            | PatientEffect.TellError _ -> "TellError"
            | PatientEffect.GivePlan p -> $"GivePlan %s{Part.patient p}"


        /// Describes the patient data draft and the request it awaits.
        let state (state: PatientState) =
            [
                state |> PatientState.draft |> Part.patientOption |> Some
                state
                |> PatientState.inFlightRequest
                |> Option.map (fun r -> $"awaits %s{Part.shortId r}")
            ]
            |> List.choose id
            |> String.concat " "


    /// The loader machine.
    [<RequireQualifiedAccess>]
    module Loader =

        open Loads
        open LoaderMachine

        /// A load by name.
        let load (l: Load) =
            match l with
            | Load.Settings -> "Settings"
            | Load.Localization -> "Localization"
            | Load.EmergencyList -> "EmergencyList"
            | Load.ContinuousList -> "ContinuousList"
            | Load.Formulary -> "Formulary"
            | Load.Parenteralia -> "Parenteralia"
            | Load.Interactions -> "Interactions"
            | Load.DrugNames -> "DrugNames"
            | Load.Totals -> "Totals"
            | Load.LogFiles -> "LogFiles"
            | Load.LogAnalysis -> "LogAnalysis"
            | Load.Reload -> "Reload"


        /// The kind of request an error answered.
        let source (s: ServerErrorPolicy.ErrorSource) =
            match s with
            | ServerErrorPolicy.ErrorSource.OrderPlan -> "OrderPlan"
            | ServerErrorPolicy.ErrorSource.Formulary -> "Formulary"
            | ServerErrorPolicy.ErrorSource.Parenteralia -> "Parenteralia"
            | ServerErrorPolicy.ErrorSource.Interactions -> "Interactions"
            | ServerErrorPolicy.ErrorSource.Totals -> "Totals"
            | ServerErrorPolicy.ErrorSource.EmergencyList -> "EmergencyList"
            | ServerErrorPolicy.ErrorSource.ContinuousList -> "ContinuousList"
            | ServerErrorPolicy.ErrorSource.Login -> "Login"
            | ServerErrorPolicy.ErrorSource.LogFiles -> "LogFiles"
            | ServerErrorPolicy.ErrorSource.LogAnalysis -> "LogAnalysis"
            | ServerErrorPolicy.ErrorSource.Reload -> "Reload"
            | ServerErrorPolicy.ErrorSource.Server -> "Server"


        /// A page by its name.
        let page (p: Page.Page) =
            match p with
            | Page.Page.LifeSupport -> "LifeSupport"
            | Page.Page.ContinuousMeds -> "ContinuousMeds"
            | Page.Page.Prescribe -> "Prescribe"
            | Page.Page.Nutrition -> "Nutrition"
            | Page.Page.OrderPlan -> "OrderPlan"
            | Page.Page.Formulary -> "Formulary"
            | Page.Page.Parenteralia -> "Parenteralia"
            | Page.Page.Interactions -> "Interactions"
            | Page.Page.Settings -> "Settings"


        /// Counts the given choices; never their text.
        let choices (xs: string option list) = xs |> List.filter Option.isSome |> List.length


        /// Describes the formulary by the number of choices and its patient data.
        let formulary (f: Formulary) =
            let n = choices [ f.Indication; f.Generic; f.Route; f.Form ]
            let n = if f.DoseType.IsSome then n + 1 else n
            $"%i{n} choices %s{Part.patientOption f.PatientData}"


        /// Describes the parenteralia page by the number of choices.
        let parenteralia (p: Parenteralia) = $"%i{choices [ p.Generic; p.Route; p.Form ]} choices"


        /// An answer by its load and its size; never the error text.
        let landing (landing: Landing) =
            let rows (xs: string[][]) = $"%i{xs.Length} rows"
            let named name _ = name

            let interactionReply (reply: Reply<InteractionResponse>) =
                match reply.Response with
                | InteractionResponse.DrugNamesLoaded names -> $"%i{names.Length} names"
                | InteractionResponse.InteractionsChecked rows -> $"%i{rows.Length} interactions"

            let name, answer =
                match landing with
                | Landing.Settings r -> "Settings", r |> Part.result (named "settings")
                | Landing.Localization r -> "Localization", r |> Part.result rows
                | Landing.DrugNames(from, r) -> $"DrugNames %s{Part.token from}", r |> Part.result interactionReply
                | Landing.Interactions(check, from, r) ->
                    $"Interactions check %i{check} %s{Part.token from}", r |> Part.result interactionReply
                | Landing.Totals(number, from, r) ->
                    $"Totals %i{number} %s{Part.token from}", r |> Part.result (named "totals")
                | Landing.EmergencyList(number, from, r) ->
                    $"EmergencyList %i{number} %s{Part.token from}",
                    r |> Part.result (fun reply -> $"%i{reply.Response.Length} rows")
                | Landing.ContinuousList(number, from, r) ->
                    $"ContinuousList %i{number} %s{Part.token from}",
                    r |> Part.result (fun reply -> $"%i{reply.Response.Length} rows")
                | Landing.Formulary(from, r) -> $"Formulary %s{Part.token from}", r |> Part.result (named "formulary")
                | Landing.Parenteralia(from, r) ->
                    $"Parenteralia %s{Part.token from}", r |> Part.result (named "parenteralia")

            $"%s{name} %s{answer}"


        /// A loader message.
        let msg (msg: LoaderMsg) =
            let answered () = "answered"

            match msg with
            | LoaderMsg.Start l -> $"Start %s{load l}"
            | LoaderMsg.Landed l -> $"Landed %s{landing l}"
            | LoaderMsg.CheckServer -> "CheckServer"
            | LoaderMsg.ServerChecked r -> $"ServerChecked %s{r |> Part.result answered}"
            | LoaderMsg.PatientExpected -> "PatientExpected"
            | LoaderMsg.PatientSet p -> $"PatientSet %s{Part.patientOption p}"
            | LoaderMsg.FilterAnswered f -> $"FilterAnswered %s{Part.filter f}"
            | LoaderMsg.PageShown p -> $"PageShown %s{page p}"
            | LoaderMsg.FormularyChanged f -> $"FormularyChanged %s{formulary f}"
            | LoaderMsg.ParenteraliaChanged p -> $"ParenteraliaChanged %s{parenteralia p}"
            | LoaderMsg.ResourcesReloaded -> "ResourcesReloaded"
            | LoaderMsg.CheckInteractions drugs -> $"CheckInteractions %i{drugs.Length} drugs"
            | LoaderMsg.QueryTotals query -> $"QueryTotals %s{query |> Part.orNone TotalsQuery.toString}"
            | LoaderMsg.QueryEmergencyList p -> $"QueryEmergencyList %s{Part.patientOption p}"
            | LoaderMsg.CheckEmergencyListRows numbers -> $"CheckEmergencyListRows %i{numbers.Length}"
            | LoaderMsg.QueryContinuousList p -> $"QueryContinuousList %s{Part.patientOption p}"
            | LoaderMsg.CheckContinuousListRows numbers -> $"CheckContinuousListRows %i{numbers.Length}"


        /// Describes a loader effect.
        let effect (effect: LoaderEffect) =
            match effect with
            | LoaderEffect.FetchSettings -> "FetchSettings"
            | LoaderEffect.FetchLocalization -> "FetchLocalization"
            | LoaderEffect.FetchDrugNames -> "FetchDrugNames"
            | LoaderEffect.FetchFormulary f -> $"FetchFormulary %s{formulary f}"
            | LoaderEffect.FetchParenteralia p -> $"FetchParenteralia %s{parenteralia p}"
            | LoaderEffect.FetchInteractions(check, drugs) ->
                $"FetchInteractions check %i{check} %i{drugs.Length} drugs"
            | LoaderEffect.FetchTotals(number, query) -> $"FetchTotals %i{number} %s{TotalsQuery.toString query}"
            | LoaderEffect.FetchEmergencyList(number, p) -> $"FetchEmergencyList %i{number} %s{Part.patient p}"
            | LoaderEffect.FetchContinuousList(number, p) -> $"FetchContinuousList %i{number} %s{Part.patient p}"
            | LoaderEffect.SeedWorkbench seed ->
                $"SeedWorkbench %s{OrderContext.seedSource seed.Source} %i{OrderContext.seedChoices seed} choices"
            | LoaderEffect.SettingsLanded s ->
                let data = if s.IsDemo then "demo" else "production"
                $"SettingsLanded %s{data}"
            | LoaderEffect.CheckServer -> "CheckServer"
            | LoaderEffect.CheckServerLater seconds -> $"CheckServerLater %i{seconds}s"
            | LoaderEffect.AskAgainLater(l, seconds) -> $"AskAgainLater %s{load l} %i{seconds}s"
            | LoaderEffect.Alert a -> $"Alert %s{Part.alert a}"
            | LoaderEffect.WithdrawInteractionsFound -> "WithdrawInteractionsFound"
            | LoaderEffect.RequestFailed(s, _) -> $"RequestFailed %s{source s}"
            | LoaderEffect.RequestSucceeded s -> $"RequestSucceeded %s{source s}"
            | LoaderEffect.NoticeReceived(from, n) -> $"NoticeReceived %s{Part.token from} %s{Part.notice n}"


        /// Describes the loader state: the pending, loaded and failed loads, the pages asked again, the
        /// medication name failures, the interaction check number and the server check. Returns nothing asked
        /// when there is nothing to show.
        let state (state: LoaderState) =
            let names loads = loads |> List.map load |> String.concat ", "

            [
                match out state with
                | [] -> ()
                | loads -> $"out %s{names loads}"
                match loaded state with
                | [] -> ()
                | loads -> $"loaded %s{names loads}"
                match state.Failed with
                | [] -> ()
                | loads -> $"failed %s{names loads}"
                if state.FormularyAskAgain.IsSome then
                    "formulary asked again"
                if state.ParenteraliaAskAgain.IsSome then
                    "parenteralia asked again"
                if state.DrugNameFailures > 0 then
                    $"drug names failed %i{state.DrugNameFailures}"
                if state.InteractionCheck > 0 then
                    $"interaction check %i{state.InteractionCheck}"
                match state.Server with
                | Resolved true -> "server up"
                | Resolved false -> "server down"
                | InProgress
                | Refreshing _ -> "server checking"
                | HasNotStartedYet -> ()
            ]
            |> function
                | [] -> "nothing asked"
                | parts -> parts |> String.concat "; "


    /// Describes the admin machine, never the password, the token or the text of a log file.
    [<RequireQualifiedAccess>]
    module Admin =

        open AdminMachine


        /// Describes an admin answer by its result.
        let response (r: AdminResponse) =
            match r with
            | AdminResponse.PasswordValidated(true, _) -> "valid"
            | AdminResponse.PasswordValidated(false, _) -> "invalid"
            | AdminResponse.LogFilesListed files -> $"%i{files.Length} files"
            | AdminResponse.LogFileAnalyzed _ -> "analyzed"
            | AdminResponse.ResourcesReloaded -> "reloaded"


        /// Describes an admin answer by its call and its result; never the error text.
        let landing (landing: Landing) =
            match landing with
            | Landing.Login(attempt, r) -> $"Login attempt %i{attempt} %s{r |> Part.result response}"
            | Landing.LogFiles r -> $"LogFiles %s{r |> Part.result response}"
            | Landing.LogAnalysis r -> $"LogAnalysis %s{r |> Part.result response}"
            | Landing.Reload r -> $"Reload %s{r |> Part.result response}"


        /// An admin message.
        let msg (msg: AdminMsg) =
            match msg with
            | AdminMsg.Login _ -> "Login"
            | AdminMsg.Logout -> "Logout"
            | AdminMsg.ListLogFiles -> "ListLogFiles"
            | AdminMsg.AnalyzeLogFile fileName -> $"AnalyzeLogFile %s{fileName}"
            | AdminMsg.ReloadResources -> "ReloadResources"
            | AdminMsg.Landed l -> $"Landed %s{landing l}"


        /// Describes an admin effect.
        let effect (effect: AdminEffect) =
            match effect with
            | AdminEffect.ValidatePassword(attempt, _) -> $"ValidatePassword attempt %i{attempt}"
            | AdminEffect.FetchLogFiles _ -> "FetchLogFiles"
            | AdminEffect.FetchLogAnalysis(_, fileName) -> $"FetchLogAnalysis %s{fileName}"
            | AdminEffect.Reload _ -> "Reload"
            | AdminEffect.ReloadDone -> "ReloadDone"
            | AdminEffect.LoggedOut -> "LoggedOut"
            | AdminEffect.Alert a -> $"Alert %s{Part.alert a}"
            | AdminEffect.RequestFailed(s, _) -> $"RequestFailed %s{Loader.source s}"
            | AdminEffect.RequestSucceeded s -> $"RequestSucceeded %s{Loader.source s}"


        /// Describes the admin state: logged in or out, the login attempt and the pending loads.
        let state (state: AdminState) =
            let names loads = loads |> List.map Loader.load |> String.concat ", "

            [
                if state.IsAuthenticated then "logged in" else "logged out"
                $"attempt %i{state.LoginAttempt}"
                match out state with
                | [] -> ()
                | loads -> $"out %s{names loads}"
            ]
            |> String.concat "; "


    /// Describes the shell machine, never the patient of a url or an error text.
    [<RequireQualifiedAccess>]
    module Shell =

        open ShellMachine


        /// A language by its name.
        let language (l: Informedica.GenPRES.Shared.Localization.Locales) =
            Informedica.GenPRES.Shared.Localization.toString l


        /// Describes a parsed url by its page and language, and the patient, the medication and the launch only
        /// by kind, because the values are patient data and a launch contains a token.
        let url (url: Url.UrlParts) =
            [
                $"page %s{url.Page |> Part.orNone Loader.page}"
                $"language %s{url.Language |> Part.orNone language}"
                if url.Patient.IsSome then
                    "patient"
                if url.Medication.IsSome then
                    "medication"
                match url.Launch with
                | Some(Url.LaunchUrl.Launch _) -> "launch"
                | Some(Url.LaunchUrl.Refused r) -> $"refused %s{Part.launchRefusal r}"
                | None -> ()
            ]
            |> String.concat " "


        /// Describes the conditions that a url change is checked against, or idle when none applies.
        let check (check: UrlCheck) =
            [
                if check.SigningUnderWay then
                    "signing"
                if BusyPolicy.any check.Out then
                    "busy"
                if check.UnsignedWork then
                    "unsigned work"
                if check.Launched then
                    "launched"
            ]
            |> function
                | [] -> "idle"
                | parts -> parts |> String.concat ", "


        /// A shell message.
        let msg (msg: ShellMsg) =
            match msg with
            | ShellMsg.PageChosen(p, loggedIn) ->
                let login = if loggedIn then "logged in" else "logged out"
                $"PageChosen %s{Loader.page p} %s{login}"
            | ShellMsg.MovedToPage p -> $"MovedToPage %s{Loader.page p}"
            | ShellMsg.LoggedOut -> "LoggedOut"
            | ShellMsg.LanguageChosen l -> $"LanguageChosen %s{language l}"
            | ShellMsg.HospitalChosen h -> $"HospitalChosen %s{h}"
            | ShellMsg.PatientHospitalLanded h -> $"PatientHospitalLanded %s{h}"
            | ShellMsg.DisclaimerAccepted -> "DisclaimerAccepted"
            | ShellMsg.SettingsLanded s ->
                let data = if s.IsDemo then "demo" else "production"
                $"SettingsLanded %s{data} %s{language s.Language}"
            | ShellMsg.PageLoaded(_, u) -> $"PageLoaded %s{url u}"
            | ShellMsg.UrlChanged(_, u, c) -> $"UrlChanged %s{url u} (%s{check c})"
            | ShellMsg.LeftForUrl -> "LeftForUrl"
            | ShellMsg.UrlKept -> "UrlKept"
            | ShellMsg.AlertRaised a -> $"AlertRaised %s{Part.alert a}"
            | ShellMsg.WithdrawInteractionsFound -> "WithdrawInteractionsFound"
            | ShellMsg.SnackbarClosed -> "SnackbarClosed"
            | ShellMsg.ServerErrorRaised(s, _) -> $"ServerErrorRaised %s{Loader.source s}"
            | ShellMsg.ServerErrorCleared s -> $"ServerErrorCleared %s{Loader.source s}"
            | ShellMsg.ServerErrorDismissed -> "ServerErrorDismissed"
            | ShellMsg.CountingChanged c -> $"CountingChanged %b{c}"
            | ShellMsg.StartupEnded -> "StartupEnded"
            | ShellMsg.EmergencyListFiltered f -> $"EmergencyListFiltered %i{f.Length}"
            | ShellMsg.ContinuousMedsFiltered f -> $"ContinuousMedsFiltered %i{f.Length}"
            | ShellMsg.ListFiltersCleared -> "ListFiltersCleared"


        /// A shell effect.
        let effect (effect: ShellEffect) =
            match effect with
            | ShellEffect.PageShown p -> $"PageShown %s{Loader.page p}"
            | ShellEffect.PutBackUrl _ -> "PutBackUrl"
            | ShellEffect.EraseLaunch -> "EraseLaunch"
            | ShellEffect.HospitalShown h -> $"HospitalShown %s{h}"
            | ShellEffect.PresentLaunch _ -> "PresentLaunch"
            | ShellEffect.LaunchRefused r -> $"LaunchRefused %s{Part.launchRefusal r}"
            | ShellEffect.StartOver p -> $"StartOver %s{Part.patientOption p}"
            | ShellEffect.PatientFromUrl p -> $"PatientFromUrl %s{Part.patientOption p}"
            | ShellEffect.SeedWorkbench seed ->
                $"SeedWorkbench %s{OrderContext.seedSource seed.Source} %i{OrderContext.seedChoices seed} choices"
            | ShellEffect.ResumeSession -> "ResumeSession"
            | ShellEffect.LeaveSession -> "LeaveSession"


        /// Describes the shell state: the page, the language, what shows over the page, and the flags set.
        let state (state: ShellState) =
            [
                Loader.page state.Page
                language state.Language.Current
                if (UrlPolicy.UrlState.asked state.Url).IsSome then
                    "url asked"
                if state.ShowDisclaimer then
                    "disclaimer"
                match state.Snackbar with
                | Some a -> $"snackbar %s{Part.alert a}"
                | None -> ()
                match state.ServerError with
                | Some e -> $"banner %s{Loader.source e.Source}"
                | None -> ()
                if state.Counting then
                    "counting"
                if state.Started then
                    "started"
            ]
            |> String.concat "; "


    /// Describes the messages of the client itself, never the patient data or the plan.
    module Client =

        open Client


        /// A client message.
        let msg (msg: ClientMsg) =
            match msg with
            | ClientMsg.Lanes _ -> "Lanes"
            | ClientMsg.Loader m -> $"Loader %s{Loader.msg m}"
            | ClientMsg.Admin m -> $"Admin %s{Admin.msg m}"
            | ClientMsg.Shell m -> $"Shell %s{Shell.msg m}"
            | ClientMsg.PageChosen p -> $"PageChosen %s{Loader.page p}"
            | ClientMsg.PanelCleared request -> $"PanelCleared %s{Part.shortId request}"
            | ClientMsg.PanelEdited(_, request) -> $"PanelEdited %s{Part.shortId request}"
            | ClientMsg.NewPatient(_, request) -> $"NewPatient %s{Part.shortId request}"
            | ClientMsg.UrlChanged(_, u) -> $"UrlChanged %s{Shell.url u}"
            | ClientMsg.EmergencyListRowChosen ctx -> $"EmergencyListRowChosen %s{Part.context ctx}"
            | ClientMsg.ContinuousListRowChosen ctx -> $"ContinuousListRowChosen %s{Part.context ctx}"
            | ClientMsg.Sign(_, request) -> $"Sign %s{Part.shortId request}"
            | ClientMsg.TakeEhrData -> "TakeEhrData"


    /// Describes one step of a machine with the describe functions of that machine.
    let step machine describeMsg describeEffect describeState (no: int) (at: DateTime) msg (state, effects) =
        {
            No = no
            At = at
            Machine = machine
            Msg = describeMsg msg
            Effects = effects |> List.map describeEffect
            State = describeState state
        }


    /// Describes a state that the App set outside the machine, with the reason; the step has no message and
    /// no effects.
    let reset machine describeState (no: int) (at: DateTime) (why: string) state =
        {
            No = no
            At = at
            Machine = machine
            Msg = $"reset: %s{why}"
            Effects = []
            State = describeState state
        }


    /// Describes the lanes set back after a url brought a patient, a medication or a launch, by the patient
    /// data draft.
    let startedOver = reset "Lanes" (fun (lanes: Lanes.LanesState) -> Patient.state lanes.Patient)


    /// A step of the session machine.
    let session = step "Session" Session.msg Session.effect Session.state

    /// A step of the signing machine.
    let signing = step "Signing" Signing.msg Signing.effect Signing.state

    /// A step of the order plan machine.
    let orderPlan = step "OrderPlan" OrderPlan.msg OrderPlan.effect OrderPlan.state

    /// A step of the order context machine.
    let orderContext = step "OrderContext" OrderContext.msg OrderContext.effect OrderContext.state

    /// A step of the order context machine of the nutrition slot with the id.
    let slot id =
        step $"Slot %s{id}" OrderContext.msg OrderContext.effect OrderContext.state

    /// A step of the patient machine.
    let patient = step "Patient" Patient.msg Patient.effect Patient.state

    /// A step of the loader machine.
    let loader = step "Loader" Loader.msg Loader.effect Loader.state

    /// A step of the admin machine.
    let admin = step "Admin" Admin.msg Admin.effect Admin.state

    /// A step of the shell machine.
    let shell = step "Shell" Shell.msg Shell.effect Shell.state


    /// Describes a step of the lanes: a machine step, a removed nutrition slot, or a start-over.
    let lanes no at step =
        match step with
        | Lanes.LanesStep.Signing(msg, state, effects) -> signing no at msg (state, effects)
        | Lanes.LanesStep.Session(msg, state, effects) -> session no at msg (state, effects)
        | Lanes.LanesStep.Patient(msg, state, effects) -> patient no at msg (state, effects)
        | Lanes.LanesStep.Plan(msg, state, effects) -> orderPlan no at msg (state, effects)
        | Lanes.LanesStep.Workbench(msg, state, effects) -> orderContext no at msg (state, effects)
        | Lanes.LanesStep.Slot(id, msg, state, effects) -> slot id no at msg (state, effects)
        | Lanes.LanesStep.SlotRemoved id ->
            {
                No = no
                At = at
                Machine = $"Slot %s{id}"
                Msg = "Removed"
                Effects = []
                State = "gone"
            }
        | Lanes.LanesStep.StartedOver state -> startedOver no at "the url moved on" state


    /// Describes a step of the client: a step of the lanes, the loader, the admin or the shell, or a dropped
    /// message.
    let client no at step =
        match step with
        | Client.ClientStep.Lanes step -> lanes no at step
        | Client.ClientStep.Loader(msg, state, effects) -> loader no at msg (state, effects)
        | Client.ClientStep.Admin(msg, state, effects) -> admin no at msg (state, effects)
        | Client.ClientStep.Shell(msg, state, effects) -> shell no at msg (state, effects)
        | Client.ClientStep.Dropped msg ->
            {
                No = no
                At = at
                Machine = "Client"
                Msg = $"%s{Client.msg msg} dropped"
                Effects = []
                State = "unchanged"
            }
