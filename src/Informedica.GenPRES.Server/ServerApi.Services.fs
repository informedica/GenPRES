namespace ServerApi


module FormularyService =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared


    /// Maps the formulary request to a dose filter for the patient on date today. A request without patient
    /// data gets the unknown patient. Returns the error when the patient data cannot be read.
    let mapFormularyToFilter today (form: Formulary) =
        form.PatientData
        |> Option.map (ServerApi.Patient.toCore today)
        |> Option.defaultValue (Ok PatientData.unknown)
        |> Result.map (fun pat ->
            { Informedica.GenForm.Lib.Filter.doseFilter with
                Generic = form.Generic
                Indication = form.Indication
                Route = form.Route
                Form = form.Form
                DoseType = form.DoseType |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
                PatientData = pat
            }
        )


    let selectIfOne sel xs =
        match sel, xs with
        | None, [| x |] -> Some x
        | _ -> sel


    /// Pure helpers for classifying and aggregating raw G-Standaard dose-check
    /// result strings into colored TextBlocks. Exposed for unit testing.
    module DoseCheck =

        // String.split here comes from Informedica.GenPRES.Shared and returns a string array.

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

        /// Maps a dose check severity to a coloured text block: OverAbsolute to Alert (red), the advisory,
        /// under-norm and frequency severities to Warning (orange), the unit, comparison and no-monitoring
        /// severities to Caution (blue), and Within to Valid (green).
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

        /// Builds the dose check text blocks from the dose check signals. No signals give one green Ok! block.
        /// Only NoMonitoring signals, meaning no G-Standaard rule exists, give blue Caution blocks. Otherwise
        /// each violation gives a block coloured by its severity, and the NoMonitoring signals are dropped.
        let build
            (parseTextItem: string -> TextItem[])
            (singleRule: bool)
            (signals: (Check.Severity * string)[])
            : TextBlock[]
            =
            // The severity, not the message text, marks a no-monitoring signal.
            let violations = signals |> Array.filter (fst >> (<>) Check.NoMonitoring)

            // True when there are signals and all of them are NoMonitoring signals.
            // No signals at all give the green Ok! block instead.
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
        // The resource provider supplies the G-Standaard dose rules as a function.
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


    /// Fills the formulary with the dose rules the filter selects. Adds the markdown and the dose check
    /// only when a generic, an indication and a route are selected.
    let getFiltered provider (form: Formulary) (filter: DoseFilter) =
        $"""

Formulary filter:
Patient: {filter.PatientData |> PatientData.toString}
Indication: {filter.Indication |> Option.defaultValue ""}
Generic: {filter.Generic |> Option.defaultValue ""}
Route: {filter.Route |> Option.defaultValue ""}
Shape: {filter.Form |> Option.defaultValue ""}
DoseType : {filter.DoseType |> Option.map DoseType.toDescription |> Option.defaultValue ""}

"""
        |> writeDebugMessage

        let dsrs = Api.getDoseRules provider |> Api.filterDoseRules provider filter

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
                        |> checkDoseRules provider filter.PatientData
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


    /// Returns the formulary for the request on date today, or the error when the patient data cannot be read.
    let get today provider (form: Formulary) =
        form |> mapFormularyToFilter today |> Result.bind (getFiltered provider form)


module ParenteraliaService =

    open Informedica.GenForm.Lib

    type Parenteralia = Informedica.GenPRES.Shared.Types.Parenteralia


    let get logger provider (par: Parenteralia) : Result<Parenteralia, string> =
        Logging.ServerLogging.Info $"getting parenteralia for {par.Generic}"
        |> Informedica.Logging.Lib.Logging.logInfo logger

        let solRules =
            Api.getSolutionRules provider
            |> Api.filterSolutionRules
                provider
                { Filter.solutionFilter with
                    Generic = par.Generic
                    Form = par.Form
                    Route = par.Route
                }

        let gens = solRules |> SolutionRule.generics
        let shps = solRules |> SolutionRule.forms
        let rtes = solRules |> SolutionRule.routes

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
                    solRules |> SolutionRule.Print.toMarkdown ""
        }
        |> Ok


module OrderContextService =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Informedica.GenPRES.Shared.Types

    module GenOrderContext = OrderContext


    /// Joins the messages of a failed evaluation into a single error text.
    let refusal messages =
        messages
        |> List.map (fun m -> OrderLogging.formatOrderMessage (m :> Informedica.Logging.Lib.IMessage))
        |> String.concat "\n"
        |> Array.singleton


    /// Evaluates the plan context against the rules, with start as the start time of every new order.
    /// Returns the error messages, or the exception message when an exception is raised.
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


    /// Evaluates the plan context against the rules and returns the outcome, evaluated or refused.
    /// Returns the error messages, or the exception message when an exception is raised.
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


    /// Maps a GenORDER refusal to the contract refusal.
    let refusalToModel =
        function
        | Refusal.NoDoseRules -> OrderContextRefusal.NoDoseRules
        | Refusal.NoDoseRulesForPatient -> OrderContextRefusal.NoDoseRulesForPatient
        | Refusal.NoProducts -> OrderContextRefusal.NoProducts
        | Refusal.MeasurementMissing -> OrderContextRefusal.MeasurementMissing
        | Refusal.NoDiluent -> OrderContextRefusal.NoDiluent
        | Refusal.NotSolved -> OrderContextRefusal.NotSolved
        | Refusal.NoAdjustedDose -> OrderContextRefusal.NoAdjustedDose


    /// Maps the plan context to the contract order context with the demo flag.
    let toModel demo (pc: PlanContext) = pc |> PlanContext.Dto.toDto |> OrderContextMapper.toModel demo


    /// Maps the outcome to the contract response with the demo flag. An evaluated context carries the
    /// offers; a refused context carries none.
    let toResponse (demo: bool) (offers: PlanContext[]) (outcome: Outcome<PlanContext>) : OrderContextResponse =
        match outcome with
        | Evaluated pc -> OrderContextResponse.Evaluated(toModel demo pc, offers |> Array.map (toModel demo))
        | Refused(pc, refusal) -> OrderContextResponse.Refused(toModel demo pc, refusalToModel refusal)


    /// Checks the argumentation text the server receives. The client normalises the text; the server
    /// only refuses a text longer than maxLength characters.
    module Argumentation =

        let maxLength = 1000


        /// Returns the text, or an error when it is longer than maxLength. Missing text is accepted.
        let check (text: string option) : Result<string option, string[]> =
            match text with
            | Some s when s.Length > maxLength ->
                Error
                    [|
                        $"De argumentatie is te lang: %i{s.Length} tekens, ten hoogste %i{maxLength}"
                    |]
            | _ -> Ok text


        /// Checks the argumentation of every context and returns the first error.
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


    /// Reads the order context of the contract on today's date. Returns the messages when it is
    /// invalid. An argumentation that is too long is refused first.
    let parse today (ctx: OrderContext) : Result<PlanContext, string[]> =
        ctx.Argumentation
        |> Argumentation.check
        |> Result.bind (fun _ ->
            ctx
            |> OrderContextMapper.ofModel
            |> PlanContext.Dto.fromDto today
            |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)
        )
