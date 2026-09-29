namespace ServerApi


module FormularyService =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Shared


    let mapFormularyToFilter departments (form: Formulary) =
        { Informedica.GenForm.Lib.Filter.doseFilter with
            Generic = form.Generic
            Indication = form.Indication
            Route = form.Route
            Form = form.Form
            DoseType = form.DoseType |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
            Patient =
                form.Patient
                |> Option.map (Mappers.mapFromSharedPatient departments)
                |> Option.defaultValue Patient.patient
        }
        |> Informedica.GenForm.Lib.Filter.calcPMAge


    let selectIfOne sel xs =
        match sel, xs with
        | None, [| x |] -> Some x
        | _ -> sel


    /// Pure helpers for classifying and aggregating raw G-Standaard dose-check
    /// result strings into colored TextBlocks. Exposed for unit testing.
    module DoseCheck =

        // `Shared.Utils.String.split` (in scope via `open Shared`) returns string[].

        /// True if the line is a frequency-mismatch entry emitted by Check.fs.
        /// Expected raw shape: "{target}\t{route}\t{patientCategory}\t{message}".
        let isFrequency (s: string) =
            match s |> String.split "\t" with
            | [| _; _; _; msg |] -> msg.Contains "frequenties"
            | _ -> false

        /// Format a raw check line for display. singleRule drops the
        /// patient-category field when only one dose rule is in scope.
        let formatLine (singleRule: bool) (s: string) =
            match s |> String.split "\t" with
            | [| s1; _; p; s2 |] ->
                if singleRule then
                    $"%s{s1} %s{s2}"
                else
                    $"%s{s1} %s{p} %s{s2}"
            | _ -> s

        /// Map a GenFORM dose-check Severity to the Formulary's colored TextBlock
        /// case. This is what gives the UI its advisory-vs-absolute grading:
        ///   - OverAbsolute                         → red (Alert)
        ///   - AdvisoryOverNorm/UnderNorm/Frequency → orange (Warning)
        ///   - UnitMismatch/Incomparable/NotComparable/NoMonitor → blue (Caution)
        ///   - Within                               → green (Valid)
        let severityWrap (sev: Check.Severity) : TextItem[] -> TextBlock =
            match sev with
            | Check.Within -> Valid
            | Check.OverAbsolute -> Alert
            | Check.AdvisoryOverNorm
            | Check.UnderNorm
            | Check.FrequencyMismatch -> Warning
            | Check.UnitMismatch
            | Check.IncomparableUnits
            | Check.NotComparable
            | Check.NoMonitoring -> Caution

        /// Build the colored TextBlock[] for the Formulary's DoseCheck field from
        /// the graded dose-check signals.
        ///   - empty input            → single green "Ok!" block
        ///   - only "no monitoring"   → blue (Caution) info blocks; signals
        ///                              no G-Standaard rule exists for the
        ///                              selection
        ///   - otherwise              → one block per signal, colored by its
        ///                              Severity (advisory = orange, absolute =
        ///                              red, etc.)
        /// NoMonitoring sentinels are dropped once real violations exist so a
        /// non-violation isn't painted as a violation.
        let build
            (parseTextItem: string -> TextItem[])
            (singleRule: bool)
            (signals: (Check.Severity * string)[])
            : TextBlock[]
            =
            // Sentinel identity is carried by the Severity, not the message text.
            let violations = signals |> Array.filter (fst >> (<>) Check.NoMonitoring)

            // "no monitoring" only: signals is non-empty but every signal is a
            // NoMonitoring sentinel. Distinct from the pure pass-through
            // (signals = [||]) which keeps the green "Ok!" badge.
            let allNoMonitoring = violations |> Array.isEmpty && signals |> Array.isEmpty |> not

            if signals |> Array.isEmpty then
                [| "Ok!" |> parseTextItem |> Valid |]
            elif allNoMonitoring then
                signals
                |> Array.map (fun (_, s) -> s |> formatLine singleRule |> parseTextItem |> Caution)
            else
                violations
                |> Array.map (fun (sev, s) -> s |> formatLine singleRule |> parseTextItem |> severityWrap sev)


    let checkDoseRules provider pat (dsrs: DoseRule[]) =
        // GStand dose rules now come from the Resources provider as a
        // function-valued resource, not built ad hoc from the route mapping.
        let gstand = Api.getGStandProvider provider

        let empt, rs =
            dsrs
            |> Array.distinctBy (fun dr ->
                dr.Generic |> Generic.toString, dr.Generic.Form |> PharmaceuticalForm.toString, dr.Route, dr.DoseType
            )
            |> Array.map (Check.checkDoseRuleWithProvider gstand pat)
            |> Array.partition (fun c -> c.didPass |> Array.isEmpty && c.didNotPass |> Array.isEmpty)

        rs
        |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
        |> Array.collect _.signals
        |> Array.filter (fun (sev, s) -> sev <> Check.Within && s |> String.notEmpty)
        |> Array.distinct
        |> function
            | [||] ->
                [|
                    for e in empt do
                        Check.NoMonitoring,
                        $"geen doseer bewaking gevonden voor {e.doseRule.Generic |> Generic.toString}"
                |]
                |> Array.distinct

            | xs -> xs


    /// The lists of the chosen fields as the options each could take given the other choices,
    /// filtered without the field's own choice; a field without a choice keeps the list the whole
    /// filter gives it. What the page shows stays filtered by every choice.
    let withAlternatives
        provider
        (sent: Formulary)
        (filter: Informedica.GenForm.Lib.Types.DoseFilter)
        (form: Formulary)
        =
        let rulesWithout f = Formulary.getDoseRules provider (f filter)

        let listed (chosen: 'a option) without pick held = if chosen.IsSome then rulesWithout without |> pick else held

        { form with
            Generics = listed sent.Generic (fun f -> { f with Generic = None }) DoseRule.generics form.Generics
            Indications =
                listed sent.Indication (fun f -> { f with Indication = None }) DoseRule.indications form.Indications
            Routes = listed sent.Route (fun f -> { f with Route = None }) DoseRule.routes form.Routes
            Forms = listed sent.Form (fun f -> { f with Form = None }) DoseRule.forms form.Forms
            DoseTypes =
                listed
                    sent.DoseType
                    (fun f -> { f with DoseType = None })
                    (DoseRule.doseTypes >> Array.map Mappers.mapFromOrderDoseTypeToSharedDoseType)
                    form.DoseTypes
        }


    let get (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) (form: Formulary) =
        let sent = form

        let filter =
            form
            |> mapFormularyToFilter (provider.Get Informedica.GenForm.Lib.Resources.Keys.departments)

        $"""

Formulary filter:
Patient: {filter.Patient |> Patient.toString}
Indication: {filter.Indication |> Option.defaultValue ""}
Generic: {filter.Generic |> Option.defaultValue ""}
Route: {filter.Route |> Option.defaultValue ""}
Shape: {filter.Form |> Option.defaultValue ""}
DoseType : {filter.DoseType |> Option.map DoseType.toDescription |> Option.defaultValue ""}

"""
        |> writeDebugMessage

        let dsrs = Formulary.getDoseRules provider filter

        writeDebugMessage $"Found: {dsrs |> Array.length} formulary dose rules"

        let form =
            { form with
                Generics = dsrs |> DoseRule.generics
                Indications = dsrs |> DoseRule.indications
                Routes = dsrs |> DoseRule.routes
                Forms = dsrs |> DoseRule.forms
                DoseTypes =
                    dsrs
                    |> DoseRule.doseTypes
                    |> Array.map Mappers.mapFromOrderDoseTypeToSharedDoseType
                PatientCategories = dsrs |> DoseRule.patientCategories
            }
            |> withAlternatives provider sent filter
            |> fun form ->
                { form with
                    Generic = form.Generics |> selectIfOne form.Generic
                    Indication = form.Indications |> selectIfOne form.Indication
                    Route = form.Routes |> selectIfOne form.Route
                    Form = form.Forms |> selectIfOne form.Form
                    DoseType = form.DoseTypes |> selectIfOne form.DoseType
                    PatientCategory = form.PatientCategories |> selectIfOne form.PatientCategory
                }
            |> fun form ->
                match form.Generic, form.Indication, form.Route with
                | Some _, Some _, Some _ ->
                    writeDebugMessage $"start checking {dsrs |> Array.length} rules"

                    let singleRule = dsrs |> Array.length = 1

                    let doseCheck =
                        dsrs
                        |> checkDoseRules provider filter.Patient
                        |> DoseCheck.build Mappers.parseTextItem singleRule

                    writeDebugMessage $"finished checking {dsrs |> Array.length} rules"

                    { form with
                        Markdown =
                            dsrs
                            |> DoseRule.Print.toMarkdown (Informedica.GenForm.Lib.Api.getNKFLinkProvider provider)
                        DoseCheck = doseCheck
                    }

                | _ ->
                    { form with
                        Markdown = ""
                        DoseCheck = [||]
                    }

        $"""

Formulary:
Patients: {form.PatientCategories |> Array.length}
Indication: {form.Indications |> Array.length}
Generic: {form.Generics |> Array.length}
Route: {form.Routes |> Array.length}
Shapes: {form.Forms |> Array.length}
DoseTypes: {form.DoseTypes |> Array.length}

"""
        |> writeDebugMessage

        Ok form


module ParenteraliaService =

    open Informedica.GenOrder.Lib
    open Informedica.GenForm.Lib

    type Parenteralia = Shared.Types.Parenteralia


    let get logger provider (par: Parenteralia) : Result<Parenteralia, string> =
        Logging.ServerLogging.Info $"getting parenteralia for {par.Generic}"
        |> Informedica.Logging.Lib.Logging.logInfo logger

        let srs = Formulary.getSolutionRules provider par.Generic par.Form par.Route

        // a chosen field lists the options it could take given the other choices: filtered
        // without its own choice
        let listed (chosen: string option) gen shp rte pick =
            if chosen.IsSome then
                Formulary.getSolutionRules provider gen shp rte |> pick
            else
                srs |> pick

        let gens = listed par.Generic None par.Form par.Route SolutionRule.generics
        let shps = listed par.Form par.Generic None par.Route SolutionRule.forms
        let rtes = listed par.Route par.Generic par.Form None SolutionRule.routes

        { par with
            Generics = gens
            Forms = shps
            Routes = rtes
            Generic =
                if gens |> Array.length = 1 then
                    Some gens[0]
                else
                    par.Generic
            Form = if shps |> Array.length = 1 then Some shps[0] else par.Form
            Route = if rtes |> Array.length = 1 then Some rtes[0] else par.Route

            Markdown =
                if par.Generic |> Option.isNone then
                    ""
                else
                    srs |> SolutionRule.Print.toMarkdown ""
        }
        |> Ok


module OrderContextService =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types

    module GenOrderContext = OrderContext


    /// The messages of a failed evaluation as one refusal.
    let refusal messages =
        messages
        |> List.map (fun m -> OrderLogging.formatOrderMessage (m :> Informedica.Logging.Lib.IMessage))
        |> String.concat "\n"
        |> Array.singleton


    /// The plan context evaluated against the rules: the domain's pipeline, the intake over
    /// the provider's totals data. The start every order built here is given is the caller's,
    /// read from the clock at the edge. An exception on the way is the refusal.
    let evaluate
        (start: System.DateTime)
        logger
        (provider: Resources.IResourceProvider)
        (cmd: Informedica.GenOrder.Lib.Types.OrderContext -> GenOrderContext.Command)
        (pc: PlanContext)
        : Result<PlanContext, string[]>
        =
        try
            pc
            |> PlanContext.evaluate start logger provider (provider.GetTotals()) cmd
            |> Result.mapError refusal
        with e ->
            Logging.ServerLogging.Error $"errored:\n{e}"
            |> Informedica.Logging.Lib.Logging.logError logger

            Error [| e.Message |]


    /// The plan context evaluated against the rules, as an outcome: the domain's pipeline, the
    /// intake over the provider's totals data. An exception on the way is the refusal of the
    /// error channel.
    let evaluateOutcome
        (start: System.DateTime)
        logger
        (provider: Resources.IResourceProvider)
        (cmd: Informedica.GenOrder.Lib.Types.OrderContext -> GenOrderContext.Command)
        (pc: PlanContext)
        : Result<Outcome<PlanContext>, string[]>
        =
        try
            pc
            |> PlanContext.evaluateOutcome start logger provider provider.GetTotals cmd
            |> Result.mapError refusal
        with e ->
            Logging.ServerLogging.Error $"errored:\n{e}"
            |> Informedica.Logging.Lib.Logging.logError logger

            Error [| e.Message |]


    /// GenORDER's refusal as the contract's.
    let refusalToModel =
        function
        | Refusal.NoDoseRules -> OrderContextRefusal.NoDoseRules
        | Refusal.NoDoseRulesForPatient -> OrderContextRefusal.NoDoseRulesForPatient
        | Refusal.NoProducts -> OrderContextRefusal.NoProducts


    /// The outcome mapped out as the contract's response, with the environment's demo flag.
    let toResponse (demo: bool) (outcome: Outcome<PlanContext>) : OrderContextResponse =
        let model (pc: PlanContext) = pc |> PlanContext.Dto.toDto |> OrderContextMapper.toModel demo

        match outcome with
        | Evaluated pc -> OrderContextResponse.Evaluated(model pc)
        | Refused(pc, refusal) -> OrderContextResponse.Refused(model pc, refusalToModel refusal)


    /// The argumentation as the server takes it in: at most maxLength characters. The client
    /// normalises the text; the server only refuses what is too long to store.
    module Argumentation =

        let maxLength = 1000


        /// The text as sent, or the reason it is refused in the server's words: longer than
        /// the cap. None passes.
        let check (text: string option) : Result<string option, string[]> =
            match text with
            | Some s when s.Length > maxLength ->
                Error
                    [|
                        $"De argumentatie is te lang: %i{s.Length} tekens, ten hoogste %i{maxLength}"
                    |]
            | _ -> Ok text


        /// Every context's text checked, the first refusal the answer.
        let checkAll (contexts: OrderContext[]) : Result<unit, string[]> =
            contexts
            |> Array.tryPick (fun ctx ->
                match ctx.Argumentation |> check with
                | Error e -> Some e
                | Ok _ -> None
            )
            |> function
                | Some e -> Error e
                | None -> Ok()


    /// The contract model's context parsed: the plan context the mapper makes of it, or the
    /// reasons it is none in the server's words; a text over the cap is refused before the
    /// mapping.
    let parse (ctx: OrderContext) : Result<PlanContext, string[]> =
        ctx.Argumentation
        |> Argumentation.check
        |> Result.bind (fun _ ->
            ctx
            |> OrderContextMapper.ofModel
            |> PlanContext.Dto.fromDto
            |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)
        )
