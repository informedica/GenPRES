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


    let buildPatient (input: CreateOrderContextInput) : Patient.Patient =
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

        pat |> Patient.setDepartment (input.Department |> Option.orElse (Some "ICK"))


    // ── Tool handler functions ──────────────────────────────────────────────

    let getFilterOptions (provider: IResourceProvider) (input: FilterOptionsInput) : FilterOptionsOutput =
        let patient =
            buildPatient
                {
                    AgeMonths = input.AgeMonths
                    WeightKg = input.WeightKg
                    HeightCm = None
                    Sex = None
                    Department = None
                    Generic = input.Generic
                    Indication = input.Indication
                    Route = input.Route
                    Form = input.Form
                }

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


    let createOrderContext
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderContextSummaryOutput, string>
        =
        let patient = buildPatient input
        let ctx = OrderContext.create OrderLogging.noOp provider patient

        let ctx =
            ctx
            |> (fun c ->
                match input.Generic with
                | Some g -> c |> OrderContext.setFilterGeneric g
                | None -> c
            )
            |> (fun c ->
                match input.Indication with
                | Some i -> c |> OrderContext.setFilterIndication i
                | None -> c
            )
            |> (fun c ->
                match input.Route with
                | Some r -> c |> OrderContext.setFilterRoute r
                | None -> c
            )
            |> (fun c ->
                match input.Form with
                | Some f -> c |> OrderContext.setFilterForm f
                | None -> c
            )

        match
            OrderContext.UpdateOrderContext ctx
            |> OrderContext.evaluate OrderLogging.noOp provider
        with
        | Error e -> Error $"Failed to evaluate order context: {e}"
        | Ok cmd ->
            let result = cmd |> OrderContext.Command.get

            let filterOpts =
                getFilterOptions
                    provider
                    {
                        Generic = result.Filter.Generic
                        Indication = result.Filter.Indication
                        Route = result.Filter.Route
                        Form = result.Filter.Form
                        AgeMonths = input.AgeMonths
                        WeightKg = input.WeightKg
                    }

            Ok
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


    let getOrderScenarios
        (provider: IResourceProvider)
        (input: CreateOrderContextInput)
        : Result<OrderScenarioOutput[], string>
        =
        let patient = buildPatient input
        let ctx = OrderContext.create OrderLogging.noOp provider patient

        let ctx =
            ctx
            |> (fun c ->
                match input.Generic with
                | Some g -> c |> OrderContext.setFilterGeneric g
                | None -> c
            )
            |> (fun c ->
                match input.Route with
                | Some r -> c |> OrderContext.setFilterRoute r
                | None -> c
            )
            |> (fun c ->
                match input.Indication with
                | Some i -> c |> OrderContext.setFilterIndication i
                | None -> c
            )
            |> (fun c ->
                match input.Form with
                | Some f -> c |> OrderContext.setFilterForm f
                | None -> c
            )

        match
            OrderContext.UpdateOrderContext ctx
            |> OrderContext.evaluate OrderLogging.noOp provider
        with
        | Error e -> Error $"Failed to evaluate order context: {e}"
        | Ok cmd ->
            let result = cmd |> OrderContext.Command.get

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
            |> Ok
