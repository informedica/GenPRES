/// A readable trail of the client state machines: one line per machine step, with the message, the
/// effects it produced and the state it reached, to check the logic in the order it ran. Every
/// message, effect and state is described by hand, without reflection, so the browser and .NET
/// print the same line. The caller passes the time and the step's number; the trail reads no clock.
///
/// A line never holds a patient's identity, a user's name, a PIN, a code, a token, a url, an error
/// text or the argumentation: a patient shows as age and weight, a version as its number.
module Trail

open System
open Shared.Types
open Shared.Models
open Shared.Api


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


/// The text with every line break and other control character replaced by a space, so that no text a step
/// carries can split its line.
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


/// The trail with the line added, keeping the newest max lines, oldest first.
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


    /// The value described, or none.
    let orNone (describe: 'a -> string) (x: 'a option) = x |> Option.map describe |> Option.defaultValue "none"


    /// Ok with the answer described, or Error without its text.
    let result (describe: 'a -> string) (r: Result<'a, 'e>) =
        match r with
        | Ok x -> $"Ok %s{describe x}"
        | Error _ -> "Error"


    /// The patient by age, gestational age, weight, height and gender, a weight or a height marked est when it is
    /// estimated rather than measured, so that two patients that differ show as different. A patient carries no
    /// identity, and the trail shows none. The department and the location are left out: both can arrive as free
    /// text in the url, and the location can name a bed.
    let patient (p: Patient) =
        let age =
            p
            |> Patient.getAgeInYears
            |> Option.map (fun a -> $"%.1f{a} y")
            |> Option.defaultValue "no age"

        let gestationalAge =
            p.GestationalAge
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


    /// The patient by age and weight, or no patient.
    let patientOption (p: Patient option) = p |> Option.map patient |> Option.defaultValue "no patient"


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


    /// The picks of a filter, in the order the workbench asks for them.
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


    /// The picks of a filter as one text.
    let filter (f: Filter) =
        match picks f with
        | [] -> "no picks"
        | xs -> xs |> String.concat "/"


    /// An order context by its id alone; the workbench has none.
    let contextId (c: OrderContext) =
        if String.IsNullOrEmpty c.Id then
            "workbench"
        else
            shortId c.Id


    /// A scenario by its short order id and its component; never its texts.
    let scenario (sc: OrderScenario) =
        let cmp = sc.Component |> Option.defaultValue "none"

        $"%s{shortId sc.Order.Id} cmp %s{cmp}"


    /// An order context by its id, its picks and its scenarios, the one scenario shown; never its
    /// argumentation.
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


    /// The nutrition category.
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
        | SigningRefusal.ContextDiffers -> "ContextDiffers"


    /// Why the server refused the order context.
    let contextRefusal (r: OrderContextRefusal) =
        match r with
        | OrderContextRefusal.NoDoseRules -> "NoDoseRules"
        | OrderContextRefusal.NoDoseRulesForPatient -> "NoDoseRulesForPatient"
        | OrderContextRefusal.NoProducts -> "NoProducts"


    /// What a reply told besides its answer.
    let notice (n: RecordNotice) =
        match n with
        | RecordNotice.NewerVersion h -> $"NewerVersion %s{head h}"
        | RecordNotice.Ended e -> $"Ended %s{ending e}"


    /// A page's change to the plan by what it wants; never the plan.
    let change (c: OrderPlanMachine.OrderPlanChange) =
        match c with
        | OrderPlanMachine.OrderPlanChange.Add ctx -> $"Add %s{context ctx}"
        | OrderPlanMachine.OrderPlanChange.New category -> $"New %s{nutrition category}"
        | OrderPlanMachine.OrderPlanChange.Remove ids -> $"Remove %i{ids.Length}"
        | OrderPlanMachine.OrderPlanChange.Filter ids -> $"Filter %i{ids.Length}"
        | OrderPlanMachine.OrderPlanChange.Navigate(id, cmd) ->
            $"Navigate %s{shortId id} %s{OrderViewCommand.toString (cmd, OrderContext.empty)}"


    /// A plan command by what it does, with a context as describeContext tells it; never the plan.
    let planCommand (describeContext: OrderContext -> string) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.UpdatePatient _ -> "UpdatePatient"
        | OrderPlanCommand.FilterRows(ids, _) -> $"FilterRows %i{ids.Length}"
        | OrderPlanCommand.Navigate(_, id, ctxCmd, ctx) ->
            // an effect describes the context by its id, which the line already shows
            let described = describeContext ctx
            let context = if described = shortId id then "" else $" %s{described}"
            $"Navigate %s{shortId id} %s{OrderViewCommand.toString (ctxCmd, ctx)}%s{context}"
        | OrderPlanCommand.AddOrderContext(_, ctx) -> $"AddOrderContext %s{describeContext ctx}"
        | OrderPlanCommand.NewOrderContext(_, category) -> $"NewOrderContext %s{nutrition category}"
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


    /// A Session message as one trail line: its name and the answer it carries. The line never
    /// shows the launch, the key, the mailed code, the PIN, the token or whom the Session is for.
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
        | SessionMsg.PatientRefreshed(from, r) -> $"PatientRefreshed %s{Part.token from} %s{reopened r}"
        | SessionMsg.NoticeReceived(from, n) -> $"NoticeReceived %s{Part.token from} %s{Part.notice n}"
        | SessionMsg.SignatureBlocked h -> $"SignatureBlocked %s{Part.head h}"


    /// Never the url, the key, the code or the PIN.
    let effect (effect: SessionEffect) =
        match effect with
        | SessionEffect.CallPresentLaunch _ -> "CallPresentLaunch"
        | SessionEffect.CallResume -> "CallResume"
        | SessionEffect.CallCloseSession -> "CallCloseSession"
        | SessionEffect.CallSupplyPin _ -> "CallSupplyPin"
        | SessionEffect.GoToIdentityProvider _ -> "GoToIdentityProvider"
        | SessionEffect.SetPatient p -> $"SetPatient %s{Part.patientOption p}"
        | SessionEffect.KeepBrowserKey _ -> "KeepBrowserKey"
        | SessionEffect.LoadSignedPlan s -> $"LoadSignedPlan %s{Part.signed s}"
        | SessionEffect.CallOpenSignedPlan(id, from) -> $"CallOpenSignedPlan %s{Part.shortId id} %s{Part.token from}"
        | SessionEffect.CallRefreshPatient from -> $"CallRefreshPatient %s{Part.token from}"
        | SessionEffect.TellSignedPlanOpened h -> $"TellSignedPlanOpened %s{Part.head h}"
        | SessionEffect.TellNewerSignedPlan h -> $"TellNewerSignedPlan %s{Part.head h}"
        | SessionEffect.TellPatientRefreshFailed -> "TellPatientRefreshFailed"


    /// The state as the pages read it; never the name the enrolment shows.
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
            $"Sign %s{Part.plan p} %i{differences.Length} differences %s{Part.shortId request}"
        | SigningMsg.ChallengeAnswered(request, r) ->
            $"ChallengeAnswered %s{Part.shortId request} %s{r |> Part.result response}"
        | SigningMsg.AcceptDataChange held ->
            if held then
                "AcceptDataChange holding the context"
            else
                "AcceptDataChange"
        | SigningMsg.ConfirmPin _ -> "ConfirmPin"
        | SigningMsg.Cancel -> "Cancel"
        | SigningMsg.SubmitAnswered(_, r) -> $"SubmitAnswered %s{r |> Part.result response}"


    /// Never the challenge, the PIN, the notice token or the identity.
    let effect (effect: SigningEffect) =
        match effect with
        | SigningEffect.CallChallenge(p, notice, request) ->
            let withNotice = if notice.IsSome then " with notice" else ""
            $"CallChallenge %s{Part.plan p}%s{withNotice} %s{Part.shortId request}"
        | SigningEffect.CallSubmit(p, _, _, _) -> $"CallSubmit %s{Part.plan p}"
        | SigningEffect.RenewSessionToken(_, p, _) -> $"RenewSessionToken %s{Part.patient p}"
        | SigningEffect.EndSession e -> $"EndSession %s{Part.ending e}"
        | SigningEffect.SetNoticedPatient p -> $"SetNoticedPatient %s{Part.patient p}"
        | SigningEffect.TellSigned s -> $"TellSigned %s{Part.signed s}"
        | SigningEffect.TellRefused r -> $"TellRefused %s{Part.signingRefusal r}"
        | SigningEffect.TellError _ -> "TellError"


    /// The signing state as the dialog reads it; never the challenge.
    let view (view: SigningView) =
        match view with
        | SigningView.Idle -> "Idle"
        | SigningView.RequestingChallenge -> "RequestingChallenge"
        | SigningView.DataChanged(p, n) -> $"DataChanged %s{Part.plan p} %s{Part.patientOption n.Data}"
        | SigningView.AskingPin(p, r) -> $"AskingPin %s{Part.plan p} refusal %s{r |> Part.orNone Part.signingRefusal}"
        | SigningView.Submitting p -> $"Submitting %s{Part.plan p}"


    /// The signing state, through the view the dialog reads.
    let state (state: SigningState) = state |> SigningState.view |> view


/// The order plan machine.
[<RequireQualifiedAccess>]
module OrderPlan =

    open OrderPlanMachine


    /// Never the argumentation.
    let msg (msg: OrderPlanMsg) =
        match msg with
        | OrderPlanMsg.PatientChanged(p, request) -> $"PatientChanged %s{Part.patientOption p} %s{Part.shortId request}"
        | OrderPlanMsg.Version(s, request) -> $"Version %s{Part.signed s} %s{Part.shortId request}"
        | OrderPlanMsg.Change(change, request) -> $"Change %s{Part.change change} %s{Part.shortId request}"
        | OrderPlanMsg.Answered(request, r) -> $"Answered %s{Part.shortId request} %s{r |> Part.result Part.plan}"
        | OrderPlanMsg.Select id -> $"Select %s{id |> Part.orNone Part.shortId}"
        | OrderPlanMsg.Reopen(id, cmd, request) ->
            $"Reopen %s{id} %s{OrderViewCommand.toString (cmd, OrderContext.empty)} %s{Part.shortId request}"
        | OrderPlanMsg.Restore -> "Restore"
        | OrderPlanMsg.Signed -> "Signed"


    /// An order plan effect; a context by its id, never the error texts.
    let effect (effect: OrderPlanEffect) =
        match effect with
        | OrderPlanEffect.CallPlan(cmd, request) ->
            $"CallPlan %s{Part.planCommand Part.contextId cmd} %s{Part.shortId request}"
        | OrderPlanEffect.CheckInteractions drugs -> $"CheckInteractions %i{drugs.Length} drugs"
        | OrderPlanEffect.TellError _ -> "TellError"


    /// The order plan state as the pages read it.
    let view (view: OrderPlanView) =
        match view with
        | OrderPlanView.NoPatient -> "NoPatient"
        | OrderPlanView.Settled(p, selected) ->
            $"Settled %s{Part.plan p} selected %s{selected |> Part.orNone Part.shortId}"
        | OrderPlanView.Changing(p, selected) ->
            $"Changing %s{Part.plan p} selected %s{selected |> Part.orNone Part.shortId}"


    /// The order plan state, through the view the pages read, with the request it awaits and whether a
    /// plan is kept for a reopen.
    let state (state: OrderPlanState) =
        [
            state |> OrderPlanState.view |> view |> Some
            state
            |> OrderPlanState.inFlightRequest
            |> Option.map (fun r -> $"awaits %s{Part.shortId r}")
            if OrderPlanState.isKept state then Some "kept" else None
        ]
        |> List.choose id
        |> String.concat " "


/// The order context machine.
[<RequireQualifiedAccess>]
module OrderContext =

    open OrderContextMachine


    /// The server's answer to an order context command.
    let response (r: OrderContextResponse) =
        match r with
        | OrderContextResponse.Evaluated ctx -> $"Evaluated %s{Part.context ctx}"
        | OrderContextResponse.Refused(ctx, r) -> $"Refused %s{Part.context ctx} %s{Part.contextRefusal r}"


    /// Where a seed comes from.
    let seedSource (source: SeedSource) =
        match source with
        | SeedSource.Url -> "url"
        | SeedSource.MedicationList -> "list"
        | SeedSource.Formulary -> "formulary"
        | SeedSource.Parenteralia -> "parenteralia"
        | SeedSource.Reload -> "reload"


    /// How many choices a seed sets, never their text.
    let seedChoices (seed: FilterSeed) =
        [ seed.Indication; seed.Generic; seed.Route; seed.Form ]
        |> List.filter Option.isSome
        |> List.length
        |> (+) (if seed.DoseType.IsSome then 1 else 0)


    /// The command with the context as describeContext tells it. A seed's choices come from the url
    /// or a list, before the server has checked them, so a seed shows how many, never their text.
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


    /// The command alone, as a message from the page carries it.
    let commandAlone (cmd: OrderViewCommand) = (command (fun _ -> "") cmd OrderContext.empty).TrimEnd()


    /// Never the argumentation.
    let msg (msg: OrderContextMsg) =
        match msg with
        | OrderContextMsg.PatientChanged(p, request) ->
            $"PatientChanged %s{Part.patientOption p} %s{Part.shortId request}"
        | OrderContextMsg.SeedFilter(seed, request) ->
            $"SeedFilter %s{seedSource seed.Source} %i{seedChoices seed} choices %s{Part.shortId request}"
        | OrderContextMsg.Command(cmd, request) -> $"Command %s{commandAlone cmd} %s{Part.shortId request}"
        | OrderContextMsg.Answered(request, r) -> $"Answered %s{Part.shortId request} %s{r |> Part.result response}"
        | OrderContextMsg.Reset request -> $"Reset %s{Part.shortId request}"
        | OrderContextMsg.Select id -> $"Select %s{id |> Part.orNone Part.shortId}"
        | OrderContextMsg.Reopen(cmd, request) -> $"Reopen %s{commandAlone cmd} %s{Part.shortId request}"
        | OrderContextMsg.Restore -> "Restore"


    /// An order context effect; a context by its id, never the filter or the error texts.
    let effect (effect: OrderContextEffect) =
        match effect with
        | OrderContextEffect.CallContext(cmd, ctx, request) ->
            $"CallContext %s{command Part.contextId cmd ctx} %s{Part.shortId request}"
        | OrderContextEffect.CallPatientChanged(_, ctx, request) ->
            $"CallPatientChanged %s{Part.contextId ctx} %s{Part.shortId request}"
        | OrderContextEffect.SyncPages _ -> "SyncPages"
        | OrderContextEffect.TellError _ -> "TellError"


    /// The order context state as the page reads it; a context by its id.
    let view (view: OrderContextView) =
        match view with
        | OrderContextView.NoPatient -> "NoPatient"
        | OrderContextView.Settled ctx -> $"Settled %s{Part.contextId ctx}"
        | OrderContextView.Refused(ctx, r) -> $"Refused %s{Part.contextId ctx} %s{Part.contextRefusal r}"
        | OrderContextView.Changing ctx -> $"Changing %s{Part.contextId ctx}"


    /// The order context state, through the view the page reads, with the request it awaits and whether
    /// a state is kept for a reopen.
    let state (state: OrderContextState) =
        [
            state |> OrderContextState.view |> view |> Some
            state
            |> OrderContextState.inFlightRequest
            |> Option.map (fun r -> $"awaits %s{Part.shortId r}")
            if OrderContextState.isKept state then Some "kept" else None
        ]
        |> List.choose id
        |> String.concat " "


/// The patient machine.
[<RequireQualifiedAccess>]
module Patient =

    open PatientMachine


    /// What becomes of the estimates once the change is answered.
    let estimates (estimates: PatientDraftPolicy.Estimates) =
        match estimates with
        | PatientDraftPolicy.Estimates.Renewed -> "estimates renewed"
        | PatientDraftPolicy.Estimates.Kept -> "estimates kept"


    /// A patient by age and weight, never its identity.
    let msg (msg: PatientMsg) =
        match msg with
        | PatientMsg.Changed(p, e, request) ->
            $"Changed %s{Part.patientOption p} %s{estimates e} %s{Part.shortId request}"
        | PatientMsg.Answered(request, r) -> $"Answered %s{Part.shortId request} %s{r |> Part.result Part.patient}"


    /// A patient effect; never the error texts.
    let effect (effect: PatientEffect) =
        match effect with
        | PatientEffect.CallPatient(p, request) -> $"CallPatient %s{Part.patient p} %s{Part.shortId request}"
        | PatientEffect.SetPatient p -> $"SetPatient %s{Part.patientOption p}"
        | PatientEffect.TellError _ -> "TellError"


    /// The draft, with the request it awaits.
    let state (state: PatientState) =
        [
            state |> PatientState.draft |> Part.patientOption |> Some
            state
            |> PatientState.inFlightRequest
            |> Option.map (fun r -> $"awaits %s{Part.shortId r}")
        ]
        |> List.choose id
        |> String.concat " "


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


/// A state the App set outside the machine, with why: no message and no effects.
let reset machine describeState (no: int) (at: DateTime) (why: string) state =
    {
        No = no
        At = at
        Machine = machine
        Msg = $"reset: %s{why}"
        Effects = []
        State = describeState state
    }


/// The signing state the App set back when the session left Open.
let signingReset = reset "Signing" Signing.state


/// The lanes set back when the url moved on, by the patient draft they start over on.
let startedOver = reset "Lanes" (fun (lanes: Lanes.LanesState) -> Patient.state lanes.Patient)


/// A step of the session machine.
let session = step "Session" Session.msg Session.effect Session.state

/// A step of the signing machine.
let signing = step "Signing" Signing.msg Signing.effect Signing.state

/// A step of the order plan machine.
let orderPlan = step "OrderPlan" OrderPlan.msg OrderPlan.effect OrderPlan.state

/// A step of the order context machine.
let orderContext = step "OrderContext" OrderContext.msg OrderContext.effect OrderContext.state

/// A step of the patient machine.
let patient = step "Patient" Patient.msg Patient.effect Patient.state


/// A step the lanes took: a machine's step, or the signing lane set idle.
let lanes no at step =
    match step with
    | Lanes.LanesStep.Signing(msg, state, effects) -> signing no at msg (state, effects)
    | Lanes.LanesStep.Session(msg, state, effects) -> session no at msg (state, effects)
    | Lanes.LanesStep.Patient(msg, state, effects) -> patient no at msg (state, effects)
    | Lanes.LanesStep.Plan(msg, state, effects) -> orderPlan no at msg (state, effects)
    | Lanes.LanesStep.Workbench(msg, state, effects) -> orderContext no at msg (state, effects)
    | Lanes.LanesStep.SigningReset signing -> signingReset no at "the session is no longer open" signing
    | Lanes.LanesStep.StartedOver state -> startedOver no at "the url moved on" state
