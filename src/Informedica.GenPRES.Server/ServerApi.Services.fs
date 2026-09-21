namespace ServerApi


module FormularyService =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Shared


    let mapFormularyToFilter (form: Formulary) =
        { Informedica.GenForm.Lib.Filter.doseFilter with
            Generic = form.Generic
            Indication = form.Indication
            Route = form.Route
            Form = form.Form
            DoseType = form.DoseType |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
            Patient =
                form.Patient
                |> Option.map Mappers.mapFromSharedPatient
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


    let get provider (form: Formulary) =
        let filter = form |> mapFormularyToFilter

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

        let gens = srs |> SolutionRule.generics
        let shps = srs |> SolutionRule.forms
        let rtes = srs |> SolutionRule.routes

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


    /// The contract model's context parsed: the plan context the mapper makes of it, or the
    /// reasons it is none in the server's words.
    let parse (ctx: OrderContext) : Result<PlanContext, string[]> =
        ctx
        |> OrderContextMapper.ofModel
        |> PlanContext.Dto.fromDto
        |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)
