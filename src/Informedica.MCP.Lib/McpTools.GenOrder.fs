namespace Informedica.MCP.Lib

open System

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenOrder.Lib
open Informedica.Utils.Lib.BCL

open Patient.Optics


/// Input/output types and tool handler functions for GenORDER MCP tools.
/// All tools are read-only and delegate to GenOrder.Lib API functions.
module GenOrderTools =


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

    let parseGender (s: string) =
        match s.ToLowerInvariant() with
        | "male" -> Some Gender.Male
        | "female" -> Some Gender.Female
        | _ -> None


    /// The measure a caller left out, estimated from the age and the sex it gave through the
    /// contract model, the one estimate the web client shows; a measure given stays measured.
    /// Without an age there is nothing to estimate from, and the patient stays as built.
    let estimated (nv: Shared.Types.NormalValues) (input: CreateOrderContextInput) (pat: Patient.Patient) =
        match input.AgeMonths, input.WeightKg, input.HeightCm with
        | _, Some _, Some _
        | None, _, _ -> pat
        | Some months, _, _ ->
            let gender =
                match input.Sex |> Option.map _.ToLowerInvariant() with
                | Some "male" -> Shared.Types.Male
                | Some "female" -> Shared.Types.Female
                | _ -> Shared.Types.UnknownGender

            let draft =
                Shared.Models.Patient.create
                    None
                    (months |> Math.Round |> int |> Shared.Measures.toMonth |> Some)
                    None
                    None
                    None
                    None
                    None
                    None
                    gender
                    []
                    None
                    None
                |> Option.map (Shared.Models.NormalValues.apply nv)

            let weight =
                draft
                |> Option.bind _.Weight.Estimated
                |> Option.map (fun g -> decimal g / 1000m |> Kilogram)

            let height = draft |> Option.bind _.Height.Estimated |> Option.map (int >> Centimeter)

            let pat =
                match input.WeightKg, weight with
                | None, Some w -> { (pat |> Patient.setWeight (Some w)) with WeightMeasured = false }
                | _ -> pat

            match input.HeightCm, height with
            | None, Some h -> { (pat |> Patient.setHeight (Some h)) with HeightMeasured = false }
            | _ -> pat


    /// The patient built from the input, the department the default when none is given, and
    /// what the caller left out of the weight and the height estimated from the age.
    let buildPatient (provider: IResourceProvider) (input: CreateOrderContextInput) : Patient.Patient =
        let pat = Patient.patient

        let pat =
            match input.AgeMonths with
            | Some a -> pat |> Patient.setAge [ Months(a |> Math.Round |> int) ]
            | None -> pat

        let pat =
            match input.WeightKg with
            | Some w -> pat |> Patient.setWeight (decimal w |> Kilogram |> Some)
            | None -> pat

        let pat =
            match input.HeightCm with
            | Some h -> pat |> Patient.setHeight (int h |> Centimeter |> Some)
            | None -> pat

        let pat =
            match input.Sex |> Option.bind parseGender with
            | Some g -> pat |> Patient.setGender g
            | None -> pat

        pat
        |> Patient.setDepartment (
            input.Department
            |> Informedica.GenForm.Lib.Resources.Departments.forPatient (
                provider.Get Informedica.GenForm.Lib.Resources.Keys.departments
            )
        )
        |> estimated
            (provider.Get Informedica.GenForm.Lib.Resources.Keys.normalValueRows
             |> Shared.Models.NormalValues.ofRows)
            input


    // ── Tool handler functions ──────────────────────────────────────────────

    /// The filter options for a patient already built and checked, so a caller that has just validated one
    /// (createOrderContext) asks for the options of the same patient, without a second check. The patient
    /// fields of the input are not read.
    let filterOptionsFor
        (provider: IResourceProvider)
        (patient: Patient.Patient)
        (input: FilterOptionsInput)
        : FilterOptionsOutput
        =
        let filter: DoseFilter =
            {
                Generic = input.Generic
                Indication = input.Indication
                Route = input.Route
                Form = input.Form
                DoseType = None
                Diluent = None
                Components = []
                Patient = patient
            }

        let inds = filter |> Filters.filterIndications OrderLogging.noOp provider
        let gens = filter |> Filters.filterGenerics OrderLogging.noOp provider
        let rtes = filter |> Filters.filterRoutes OrderLogging.noOp provider
        let frms = filter |> Filters.filterForms OrderLogging.noOp provider
        let dsts = filter |> Filters.filterDoseTypes OrderLogging.noOp provider

        {
            Indications = inds
            Generics = gens
            Routes = rtes
            Forms = frms
            DoseTypes = dsts |> Array.map DoseType.toString
        }


    let getDoseRulesForContext (provider: IResourceProvider) (input: DoseRulesForContextInput) =
        let filter: DoseFilter =
            {
                Generic = input.Generic
                Indication = input.Indication
                Route = input.Route
                Form = input.Form
                DoseType = None
                Diluent = None
                Components = []
                Patient = Patient.patient
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
        |> Array.map (fun sr ->
            {|
                Generic = sr.Generic
                Form = sr.Form
                Route = sr.Route
                DiluentCount = sr.Diluents |> Array.length
                Diluents = sr.Diluents |> Array.map _.Generic
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
    /// the rules know if one is given (see checkDepartment), and, after the estimate, both a weight and a
    /// height. The rules would silently answer nothing for less, so the caller is told which measure to give.
    /// Returns the input with the department as the rules spell it, next to the patient built from it.
    let validPatient
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<CreateOrderContextInput * Patient.Patient, string>
        =
        input
        |> requireAgeOrMeasures
        |> Result.bind (fun () -> input |> checkDepartment (provider.Get Keys.departments))
        |> Result.bind (fun input ->
            let patient = buildPatient provider input

            // the estimate can leave a measure blank, the tables not loaded or without a row for
            // the age and sex; the rules would then answer nothing, so the caller is told which
            match patient.Weight, patient.Height with
            | Some _, Some _ -> Ok(input, patient)
            | w, h ->
                [
                    if w.IsNone then
                        "WeightKg"
                    if h.IsNone then
                        "HeightCm"
                ]
                |> noEstimate
                |> Error
        )


    /// The filter options for the patient of the input, refused when the input is no patient
    /// (see validPatient). The tool wrapper of filterOptionsFor.
    let getFilterOptions
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
        |> validPatient provider
        |> Result.map (fun (_, patient) -> filterOptionsFor provider patient input)


    /// The context with each filter the input names set, and each one it leaves out untouched.
    let applyFilters (input: CreateOrderContextInput) (ctx: OrderContext) : OrderContext =
        // foldBack applies a setter when its filter is Some and passes the context through on None
        ctx
        |> Option.foldBack OrderContext.setFilterGeneric input.Generic
        |> Option.foldBack OrderContext.setFilterIndication input.Indication
        |> Option.foldBack OrderContext.setFilterRoute input.Route
        |> Option.foldBack OrderContext.setFilterForm input.Form


    /// The order context for the input's patient and filter selection, evaluated against the given provider.
    /// The patient is checked by validPatient; shared by createOrderContext and getOrderScenarios so the
    /// guards and the patient/filter/evaluate pipeline exist in exactly one place.
    let evaluateOrderContext
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderContext, string>
        =
        match validPatient provider input with
        | Error e -> Error e
        | Ok(input, patient) ->
            let ctx = OrderContext.create OrderLogging.noOp provider patient |> applyFilters input

            match
                OrderContext.UpdateOrderContext ctx
                |> OrderContext.evaluate System.DateTime.UtcNow OrderLogging.noOp provider
            with
            | Error e -> Error $"Failed to evaluate order context: {e}"
            | Ok result -> Ok(OrderContext.Command.get result)


    let createOrderContext
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderContextSummaryOutput, string>
        =
        input
        |> evaluateOrderContext provider
        |> Result.map (fun result ->
            let filterOpts =
                filterOptionsFor
                    provider
                    result.Patient
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
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderScenarioOutput[], string>
        =
        input
        |> evaluateOrderContext provider
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
