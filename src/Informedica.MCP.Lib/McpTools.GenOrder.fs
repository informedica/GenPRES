namespace Informedica.MCP.Lib

open System

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenOrder.Lib
open Informedica.Utils.Lib.BCL
open Validus

open Patient.AgeParts


/// Input/output types and tool handler functions for GenORDER MCP tools.
/// All tools are read-only and delegate to GenOrder.Lib API functions.
module GenOrderTools =

    module CoreEstimates = Informedica.GenCore.Lib.Patients.Estimates
    module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
    module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
    module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
    module CoreHeight = Informedica.GenCore.Lib.Patients.Height
    module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
    module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreDepartment = Informedica.GenCore.Lib.Patients.Department
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
    module CoreGender = Informedica.GenCore.Lib.Patients.Gender

    type CoreGender = Informedica.GenCore.Lib.Patients.Gender
    type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>


    // ── Tool input types ────────────────────────────────────────────────────

    type CreateOrderContextInput =
        {
            AgeMonths: float option
            WeightKg: float option
            HeightCm: float option
            Sex: string option
            Department: string option
            Generic: string option
            Indication: string option
            Route: string option
            Form: string option
        }

    type FilterOptionsInput =
        {
            Generic: string option
            Indication: string option
            Route: string option
            Form: string option
            AgeMonths: float option
            WeightKg: float option
            HeightCm: float option
        }

    type DoseRulesForContextInput =
        {
            Generic: string option
            Indication: string option
            Route: string option
            Form: string option
        }

    type SolutionRulesForContextInput =
        {
            Generic: string option
            Form: string option
            Route: string option
        }


    // ── Tool output types ───────────────────────────────────────────────────

    type FilterOptionsOutput =
        {
            Indications: string[]
            Generics: string[]
            Routes: string[]
            Forms: string[]
            DoseTypes: string[]
        }

    type OrderScenarioOutput =
        {
            Number: int
            Name: string
            Indication: string
            Route: string
            Form: string
            DoseType: string
            HasRenalRule: bool
            Summary: string
        }

    type OrderContextSummaryOutput =
        {
            PatientAgeMonths: float option
            PatientWeightKg: float option
            SelectedGeneric: string option
            SelectedRoute: string option
            SelectedForm: string option
            ScenarioCount: int
            SelectedScenario: string option
            FilterOptions: FilterOptionsOutput
        }


    // ── Helpers ─────────────────────────────────────────────────────────────

    /// Reads a gender from a string. Any string other than male or female is an unknown gender.
    let parseGender (s: string) =
        match s.ToLowerInvariant() with
        | "male" -> CoreGender.Male
        | "female" -> CoreGender.Female
        | _ -> CoreGender.UnknownGender


    /// Reads patient data from a Dto on a date. Returns the GenCORE messages when a value is invalid.
    let readPatientData today dto =
        dto
        |> CorePatientData.Dto.fromDto today
        |> Result.mapError (fun errs ->
            let msgs = errs |> ValidationErrors.toList |> String.concat "; "
            $"The patient data is not valid: %s{msgs}"
        )


    /// Estimates the weight and the height from the age and the gender. Returns None for both without an age
    /// or without the normal-value tables.
    let estimated normalValues gender age =
        match normalValues with
        | None -> None, None
        | Some tables ->
            let est = CoreEstimates.ofAge tables gender age None

            est.Weight
            |> Option.map (fun e -> decimal e.Mean |> CoreWeightValue.weightInGram),
            est.Height |> Option.map (fun e -> decimal e.Mean |> CoreHeightValue.heightInCm)


    /// Builds the patient data from the tool input through the patient data Dto. A weight or height that
    /// the input does not give is estimated from the age. Returns an error when a value of the input is
    /// invalid.
    let buildPatient today (provider: IResourceProvider) (input: CreateOrderContextInput) =
        let gender =
            input.Sex
            |> Option.map parseGender
            |> Option.defaultValue CoreGender.UnknownGender

        let age =
            input.AgeMonths
            |> Option.map (fun a -> [ Months(a |> Math.Round |> int) ] |> daysOfParts |> CoreAgeValue.fromDays)

        let estWeight, estHeight = estimated (provider.Get Keys.normalValueTables) gender age

        let weight =
            match input.WeightKg with
            | Some w ->
                decimal w
                |> CoreWeightValue.weightInKg
                |> CoreWeightAtDate.undated
                |> CoreSource.Measured
                |> Some
            | None -> estWeight |> Option.map (CoreWeightAtDate.undated >> CoreSource.Estimated)

        let height =
            match input.HeightCm with
            | Some h ->
                decimal h
                |> CoreHeightValue.heightInCm
                |> CoreHeightAtDate.undated
                |> CoreSource.Measured
                |> Some
            | None -> estHeight |> Option.map (CoreHeightAtDate.undated >> CoreSource.Estimated)

        let dto = CorePatientData.Dto.dto ()
        dto.Age <- age |> Option.map CoreAgeValue.Dto.toDto
        dto.Gender <- gender |> CoreGender.toString
        dto.Weight <- weight |> CoreWeight.create [] None [] |> CoreWeight.Dto.toDto
        dto.Height <- height |> CoreHeight.create [] None |> CoreHeight.Dto.toDto

        input.Department
        |> Option.iter (fun d -> dto.Department <- d |> CoreDepartment.otherDepartment |> CoreDepartment.toString)

        dto |> readPatientData today


    // ── Tool handler functions ──────────────────────────────────────────────

    /// The filter options for a patient already built and checked, so a caller that has just validated one
    /// (createOrderContext) asks for the options of the same patient, without a second check. The patient
    /// fields of the input are not read.
    let filterOptionsFor provider patientData (input: FilterOptionsInput) : FilterOptionsOutput =
        let filter =
            { Filter.doseFilter with
                Generic = input.Generic
                Indication = input.Indication
                Route = input.Route
                Form = input.Form
                PatientData = patientData
            }

        let opts =
            filter
            |> FilterHelpers.getPrescribableRules OrderLogging.noOp provider
            |> Result.map PrescriptionRule.filterOptions
            |> FilterHelpers.orLogErrors OrderLogging.noOp PrescriptionRule.emptyFilterOptions

        {
            Indications = opts.Indications
            Generics = opts.Generics
            Routes = opts.Routes
            Forms = opts.Forms
            DoseTypes = opts.DoseTypes |> Array.map DoseType.toString
        }


    let getDoseRulesForContext (provider: IResourceProvider) (input: DoseRulesForContextInput) =
        let filter =
            { Filter.doseFilter with
                Generic = input.Generic
                Indication = input.Indication
                Route = input.Route
                Form = input.Form
            }

        Formulary.getDoseRules provider filter
        |> Array.map (fun dr ->
            {|
                Generic = dr.Generic |> Generic.toString
                Indication = dr.Indication
                Route = dr.Route
                Form = dr.Generic.Form |> PharmaceuticalForm.toString
                DoseType = dr.DoseType |> sprintf "%A"
                ComponentCount = dr.ComponentLimits |> Array.length
            |}
        )


    let getSolutionRulesForContext (provider: IResourceProvider) (input: SolutionRulesForContextInput) =
        Formulary.getSolutionRules provider input.Generic input.Form input.Route
        |> Array.map (fun solRule ->
            {|
                Generic = solRule.Generic
                Form = solRule.Form
                Route = solRule.Route
                DiluentCount = solRule.Diluents |> Array.length
                Diluents = solRule.Diluents |> Array.map _.Generic
            |}
        )


    /// Whether the input is a patient by the domain rule: an age, or a measured weight and
    /// height. With an age alone the weight and height are estimated, as the web client does;
    /// without either the rules would silently answer nothing, so the call is refused.
    let requireAgeOrMeasures (input: CreateOrderContextInput) =
        match input.AgeMonths, input.WeightKg, input.HeightCm with
        | Some _, _, _
        | _, Some _, Some _ -> Ok()
        | _ ->
            Error
                "A patient needs an age, or both WeightKg and HeightCm. With an age alone the \
                 weight and height are estimated from it."


    /// The input with its department one the rules know, spelled as they spell it; none leaves
    /// the default in force. A department the rules do not know is refused with the names they
    /// do and the default, since the department selects the rules and a misspelt one would
    /// silently select none. The spelling given wins when the rules know it; another case is
    /// taken only when it names exactly one of them, since the rules compare exactly.
    let checkDepartment
        (departments: Departments)
        (input: CreateOrderContextInput)
        : Result<CreateOrderContextInput, string>
        =
        match input.Department with
        | None -> Ok input
        | Some d ->
            let given = d.Trim()

            let known =
                if departments.Names |> Array.contains given then
                    Some given
                else
                    match
                        departments.Names
                        |> Array.filter (fun n -> String.Equals(n, given, StringComparison.OrdinalIgnoreCase))
                    with
                    | [| n |] -> Some n
                    | _ -> None

            match known with
            | Some n -> Ok { input with Department = Some n }
            | None ->
                let names = departments.Names |> String.concat ", "

                Error
                    $"Unknown department '{d}'. Known departments: {names}. \
                      Omit the department to prescribe for the default, {departments.Default}."


    /// The refusal when a measure could not be estimated from the age and sex given: the
    /// normal-value tables are not loaded, or hold no row for that age and sex. Names the
    /// measures the caller has to give, the ones still blank.
    let noEstimate (missing: string list) =
        let names = missing |> String.concat " and "

        $"The %s{names} could not be estimated from the age and sex given: the normal-value \
          tables are not loaded, or hold no row for them. Give %s{names}."


    /// The patient of the input, checked: an age or both measures (see requireAgeOrMeasures), a department
    /// the rules know if one is given (see checkDepartment), and, after the estimate, a patient the rules
    /// are looked up for: an adult, or a child with a weight. The rules would silently answer nothing for a
    /// child without a weight, so the caller is told to give it. Returns the input with the department as
    /// the rules spell it, next to the patient built from it.
    let validPatient
        today
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<CreateOrderContextInput * PatientData, string>
        =
        input
        |> requireAgeOrMeasures
        |> Result.bind (fun () -> input |> checkDepartment (provider.GetDepartments()))
        |> Result.bind (fun input -> buildPatient today provider input |> Result.map (fun patient -> input, patient))
        |> Result.bind (fun (input, patient) ->
            // the estimate can leave the weight blank, the tables not loaded or without a row for
            // the age and sex; for a child the rules would then answer nothing, so the caller is told
            if patient |> OrderContext.selectsRules then
                Ok(input, patient)
            else
                [ "WeightKg" ] |> noEstimate |> Error
        )


    /// The filter options for the patient of the input, refused when the input is no patient
    /// (see validPatient). The tool wrapper of filterOptionsFor.
    let getFilterOptions
        today
        (provider: IResourceProvider)
        (input: FilterOptionsInput)
        : Result<FilterOptionsOutput, string>
        =
        {
            AgeMonths = input.AgeMonths
            WeightKg = input.WeightKg
            HeightCm = input.HeightCm
            Sex = None
            Department = None
            Generic = input.Generic
            Indication = input.Indication
            Route = input.Route
            Form = input.Form
        }
        |> validPatient today provider
        |> Result.map (fun (_, patient) -> filterOptionsFor provider patient input)


    /// The context with each filter the input names set, and each one it leaves out untouched.
    let applyFilters (input: CreateOrderContextInput) (ctx: OrderContext) : OrderContext =
        // foldBack applies a setter when its filter is Some and passes the context through on None
        ctx
        |> Option.foldBack OrderContext.setFilterGeneric input.Generic
        |> Option.foldBack OrderContext.setFilterIndication input.Indication
        |> Option.foldBack OrderContext.setFilterRoute input.Route
        |> Option.foldBack OrderContext.setFilterForm input.Form


    /// Why the evaluation was refused, in the words a caller of the tools can act on.
    let refusalReason =
        function
        | Refusal.NoDoseRules -> "no dose rule exists for the picks"
        | Refusal.NoDoseRulesForPatient -> "dose rules exist for the picks, and none of them covers this patient"
        | Refusal.NoProducts ->
            "dose rules cover the picks and the patient, and none of them can be prescribed: "
            + "they have no product or no dose type"
        | Refusal.MeasurementMissing ->
            "dose rules cover the picks and the patient, and each needs a weight, or a weight and a height, "
            + "the patient does not have"
        | Refusal.NoDiluent ->
            "each medication is given undiluted, and its concentration limit allows none of its product "
            + "concentrations"
        | Refusal.NotSolved -> "the order cannot be solved"
        | Refusal.NoAdjustedDose -> "the order has no dose per kg or per m2"


    /// The error a tool answers when the evaluation did not give a context.
    let evaluationFailed reason = $"Failed to evaluate order context: %s{reason}"


    /// An evaluation as the tools answer it: the value evaluated, or an error that gives the
    /// reason for the refusal.
    let refusedAsError outcome =
        match outcome with
        | Evaluated a -> Ok a
        | Refused(_, refusal) -> Error(evaluationFailed (refusalReason refusal))


    /// The order context for the input's patient and filter selection, evaluated against the given provider.
    /// The patient is checked by validPatient; shared by createOrderContext and getOrderScenarios so the
    /// guards and the patient/filter/evaluate pipeline exist in exactly one place.
    let evaluateOrderContext
        today
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderContext, string>
        =
        match validPatient today provider input with
        | Error e -> Error e
        | Ok(input, patient) ->
            let ctx = OrderContext.create OrderLogging.noOp provider patient |> applyFilters input

            match
                OrderContext.UpdateOrderContext ctx
                |> OrderContext.evaluateOutcome System.DateTime.UtcNow OrderLogging.noOp provider
            with
            | Error e -> Error(evaluationFailed $"%A{e}")
            | Ok outcome -> outcome |> refusedAsError |> Result.map OrderContext.Command.get


    let createOrderContext
        today
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderContextSummaryOutput, string>
        =
        input
        |> evaluateOrderContext today provider
        |> Result.map (fun result ->
            let filterOpts =
                filterOptionsFor
                    provider
                    result.PatientData
                    {
                        Generic = result.Filter.Generic
                        Indication = result.Filter.Indication
                        Route = result.Filter.Route
                        Form = result.Filter.Form
                        AgeMonths = input.AgeMonths
                        WeightKg = input.WeightKg
                        HeightCm = input.HeightCm
                    }

            {
                PatientAgeMonths = input.AgeMonths
                PatientWeightKg = input.WeightKg
                SelectedGeneric = result.Filter.Generic
                SelectedRoute = result.Filter.Route
                SelectedForm = result.Filter.Form
                ScenarioCount = result.Scenarios |> Array.length
                SelectedScenario = result.Scenarios |> Array.tryExactlyOne |> Option.map _.Name
                FilterOptions = filterOpts
            }
        )


    let getOrderScenarios
        today
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderScenarioOutput[], string>
        =
        input
        |> evaluateOrderContext today provider
        |> Result.map (fun result ->
            result.Scenarios
            |> Array.mapi (fun i sc ->
                let sc = sc |> OrderScenario.setOrderTableFormat

                let summary =
                    sc.Prescription
                    |> Array.collect id
                    |> Array.map (fun tb ->
                        match tb with
                        | Valid s
                        | Caution s
                        | Warning s
                        | Alert s -> s
                    )
                    |> Array.filter (fun s -> s |> String.notEmpty)
                    |> String.concat " | "
                    |> fun s -> if s.Length > 300 then s[..299] else s

                {
                    Number = i + 1
                    Name = sc.Name
                    Indication = sc.Indication
                    Route = sc.Route
                    Form = sc.Form
                    DoseType = sc.DoseType |> DoseType.toString
                    HasRenalRule = sc.UseRenalRule
                    Summary = summary
                }
            )
        )
