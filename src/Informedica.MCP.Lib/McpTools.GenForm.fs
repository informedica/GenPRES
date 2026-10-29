namespace Informedica.MCP.Lib

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenUnits.Lib


/// Input/output types and tool handler functions for GenFORM MCP tools.
/// All tools are read-only and delegate directly to GenForm.Api functions.
module GenFormTools =


    // ── Tool input types ────────────────────────────────────────────────────

    type FilterDoseRulesInput =
        {
            Generic: string option
            Route: string option
            Form: string option
            Indication: string option
            MinAge: float option
            MaxAge: float option
            MinWeight: float option
            MaxWeight: float option
        }

    type GetPrescriptionRulesInput =
        {
            Generic: string option
            Route: string option
            Form: string option
            Indication: string option
            MinAge: float option
            MaxAge: float option
            MinWeight: float option
            MaxWeight: float option
        }

    type FilterSolutionRulesInput =
        {
            Generic: string option
            Form: string option
            Route: string option
        }


    // ── Tool output types ───────────────────────────────────────────────────

    type DoseRuleOutput =
        {
            Generic: string
            Indication: string
            Route: string
            Form: string
            DoseType: string
            MinAge: float option
            MaxAge: float option
            MinWeight: float option
            MaxWeight: float option
            ComponentCount: int
        }

    type SolutionRuleOutput =
        {
            Generic: string
            Form: string option
            Route: string
            Solutions: string[]
        }

    type RenalRuleOutput =
        {
            Generic: string
            Route: string
            Indication: string
            Source: string
            DoseReduction: string
            Comment: string
        }

    type ResourceInfoOutput =
        {
            IsLoaded: bool
            LastUpdated: string
            MessageCount: int
        }


    // ── Mapping helpers ─────────────────────────────────────────────────────

    let limitToFloat limit =
        limit
        |> Option.bind (fun l ->
            match l with
            | Informedica.GenCore.Lib.Ranges.Limit.Inclusive vu
            | Informedica.GenCore.Lib.Ranges.Limit.Exclusive vu ->
                vu
                |> ValueUnit.getValue
                |> Array.tryHead
                |> Option.map Informedica.Utils.Lib.BCL.BigRational.toDouble
        )


    let doseRuleToOutput (dr: DoseRule) : DoseRuleOutput =
        {
            Generic = dr.Generic |> Generic.toString
            Indication = dr.Indication
            Route = dr.Route
            Form = dr.Generic.Form |> PharmaceuticalForm.toString
            DoseType = dr.DoseType |> DoseType.toString
            MinAge = (dr.PatientCategory |> PatientCategory.getAge).Min |> limitToFloat
            MaxAge = (dr.PatientCategory |> PatientCategory.getAge).Max |> limitToFloat
            MinWeight = dr.PatientCategory.Weight.Min |> limitToFloat
            MaxWeight = dr.PatientCategory.Weight.Max |> limitToFloat
            ComponentCount = dr.ComponentLimits |> Array.length
        }


    let solutionRuleToOutput (sr: SolutionRule) : SolutionRuleOutput =
        {
            Generic = sr.Generic
            Form = sr.Form
            Route = sr.Route
            Solutions = sr.Diluents |> Array.map _.Generic
        }


    let renalRuleToOutput (rr: RenalRule) : RenalRuleOutput =
        let doseReduction =
            rr.RenalLimits
            |> Array.tryHead
            |> Option.map (fun rl ->
                match rl.DoseReduction with
                | DoseReduction.Replace -> "replace"
                | DoseReduction.Relative -> "relative"
                | DoseReduction.NoReduction -> "none"
            )
            |> Option.defaultValue ""

        {
            Generic = rr.Generic
            Route = rr.Route
            Indication = rr.Indication
            Source = rr.Source
            DoseReduction = doseReduction
            Comment = rr |> sprintf "%A" |> (fun s -> s[.. min 200 (s.Length - 1)])
        }


    // ── Tool handler functions ──────────────────────────────────────────────

    let getResourceInfo (provider: IResourceProvider) : ResourceInfoOutput =
        let info = provider.GetResourceInfo()

        {
            IsLoaded = info.IsLoaded
            LastUpdated = info.LastUpdated.ToString "O"
            MessageCount = info.Messages |> Array.length
        }


    let getDoseRules (provider: IResourceProvider) : DoseRuleOutput[] =
        provider |> Api.getDoseRules |> Array.map doseRuleToOutput


    let filterDoseRules (provider: IResourceProvider) (input: FilterDoseRulesInput) : DoseRuleOutput[] =
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

        let allRules = provider |> Api.getDoseRules

        provider |> Api.filterDoseRules <| filter <| allRules
        |> Array.map doseRuleToOutput


    let getSolutionRules (provider: IResourceProvider) : SolutionRuleOutput[] =
        provider |> Api.getSolutionRules |> Array.map solutionRuleToOutput


    let getRenalRules (provider: IResourceProvider) : RenalRuleOutput[] =
        provider |> Api.getRenalRules |> Array.map renalRuleToOutput


    let getPrescriptionRules (provider: IResourceProvider) (input: GetPrescriptionRulesInput) : string =
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

        match provider |> Api.filterPrescriptionRules <| filter with
        | Ok rules -> $"Found {rules |> Array.length} prescription rules"
        | Error msgs ->
            msgs
            |> List.map (fun m -> m |> sprintf "%A")
            |> String.concat "; "
            |> fun s -> $"Error: {s}"


    let getFormulary (provider: IResourceProvider) =
        provider.GetFormularyProducts()
        |> Array.map (fun p ->
            {|
                Generic = p.Generic
                Form = p.Form
                Brand = p.Brand
                Departments = p.Departments
            |}
        )


    let getParenteralMeds (provider: IResourceProvider) =
        provider.GetParenteralMeds()
        |> Array.map (fun p ->
            {|
                Generic = p.Generic
                Form = p.Form
                Label = p.Label
                SubstanceName = p.Substances |> Array.tryHead |> Option.map _.Name
            |}
        )
