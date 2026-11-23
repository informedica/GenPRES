namespace Informedica.GenForm.Lib


/// The dose rule selection of a kind: the rows of the "RuleSets" sheet of one category.
type RuleSet =
    {
        /// What the rule set selects the dose rules for
        Kind: RuleSetKind
        /// The label of the rule set
        Label: string
        /// The rows of the rule set
        Rows: RuleSetData[]
    }


/// The rule sets: the check of the rows of the "RuleSets" sheet against the other sheets, their expansion,
/// and the rows of a rule set that apply to a patient.
module RuleSet =

    open Informedica.Utils.Lib.BCL


    /// The names of a dose rule that the checks and the expansion of the rule set rows match on.
    type DoseRuleNames =
        {
            /// The indication of the dose rule
            Indication: string
            /// The name of the generic as in the Formulary sheet, used to look up the products
            Generic: string
            /// The generic name, followed in brackets by the brand or the form when the dose rule is
            /// restricted to one
            Label: string
            /// The component of the dose rule
            Component: string
            /// The route of the dose rule
            Route: string
            /// The dose type of the dose rule with its text
            DoseType: DoseType
        }


    module DoseRuleNames =

        /// Returns the names of every dose rule row.
        let ofData (doseRules: DoseRuleData[]) =
            doseRules
            |> Array.map (fun d ->
                {
                    Indication = d.Indication
                    Generic = d.Generic.Name
                    Label = d.Generic |> GenericLabel.ofGenericData |> GenericLabel.toString
                    Component = d.ScheduleData.DoseLimitData.Component
                    Route = d.Route
                    DoseType = DoseType.fromString d.ScheduleData.DoseType d.ScheduleData.DoseText
                }
            )


    /// Checks the rows against the dose rules and the generics of the Formulary sheet, and returns a
    /// warning for: a category with more than one label; an indication that no dose rule has; a generic
    /// that no dose rule under the indication has as a label, with the labels to use when dose rules have
    /// it as a generic name; and a generic or component under the indication of a row without a product.
    /// A product name is matched without regard to case, as the dose rules match their products.
    let check (doseRules: DoseRuleNames[]) (products: Set<string>) (rows: RuleSetData[]) =
        let products = products |> Set.map String.toLower
        let pairs = doseRules |> Array.map (fun d -> d.Indication, d.Label) |> Set.ofArray
        let indications = pairs |> Set.map fst

        // The labels of the dose rules under the indication whose generic name is the generic.
        let labelsOf indication generic =
            doseRules
            |> Array.filter (fun d -> d.Indication = indication && d.Generic = generic)
            |> Array.map _.Label
            |> Array.distinct

        [
            for category, rs in rows |> Array.groupBy _.Category do
                match rs |> Array.map _.Label |> Array.distinct with
                | [| _ |] -> ()
                | labels ->
                    Warning $"""RuleSets: %s{category} has more than one label: %s{labels |> String.concat ", "}"""

            for r in rows do
                if indications |> Set.contains r.Indication |> not then
                    Warning $"RuleSets: %s{r.Category}, no dose rule has the indication %s{r.Indication}"
                elif
                    r.Generic |> String.notEmpty
                    && pairs |> Set.contains (r.Indication, r.Generic) |> not
                then
                    match labelsOf r.Indication r.Generic with
                    | [||] -> Warning $"RuleSets: %s{r.Category}, no dose rule has %s{r.Generic} under %s{r.Indication}"
                    | labels ->
                        let labels = labels |> String.concat ", "
                        Warning
                            $"RuleSets: %s{r.Category}, %s{r.Generic} under %s{r.Indication} is offered as %s{labels}"

            for category, indication in rows |> Array.map (fun r -> r.Category, r.Indication) |> Array.distinct do
                yield!
                    doseRules
                    |> Array.filter (fun d -> d.Indication = indication)
                    |> Array.collect (fun d -> [| d.Generic; d.Component |])
                    |> Array.filter String.notEmpty
                    |> Array.distinct
                    |> Array.filter (fun n -> products |> Set.contains (String.toLower n) |> not)
                    |> Array.map (fun n ->
                        Warning
                            $"RuleSets: %s{category}, %s{n} under %s{indication} has no product in the Formulary sheet"
                    )
        ]


    /// Checks the rows against the dose rule data and the formulary products, and returns the warnings.
    let checkData doseRules (products: FormularyProduct[]) rows =
        rows
        |> check (DoseRuleNames.ofData doseRules) (products |> Array.map _.Generic |> Set.ofArray)


    /// Replaces each row without a generic by one row per distinct label of the dose rules under its
    /// indication, in the order of the dose rules. A row whose indication no dose rule has stays as it is.
    /// The evaluation does not filter on the indications of a rule set, so only the generics keep a
    /// medication out of a nutrition category.
    let expand (doseRules: DoseRuleNames[]) (rows: RuleSetData[]) =
        rows
        |> Array.collect (fun r ->
            if r.Generic |> String.notEmpty then
                [| r |]
            else
                match
                    doseRules
                    |> Array.filter (fun d -> d.Indication = r.Indication)
                    |> Array.map _.Label
                    |> Array.distinct
                with
                | [||] -> [| r |]
                | labels -> labels |> Array.map (fun l -> { r with Generic = l })
        )


    /// Every nutrition category.
    let categories =
        [|
            NutritionCategory.EnteralFeeding
            NutritionCategory.EnteralSupplement
            NutritionCategory.TPN
            NutritionCategory.Lipid
            NutritionCategory.ElectrolyteGlucose
        |]


    /// Every kind of rule set: one for each nutrition category, the emergency list and the standard infusions.
    let kinds =
        [|
            yield! categories |> Array.map RuleSetKind.Nutrition
            RuleSetKind.EmergencyList
            RuleSetKind.StandardInfusions
        |]


    /// Returns the rule set of a nutrition category, or None when there is none.
    let tryFind category (sets: RuleSet[]) =
        sets |> Array.tryFind (fun s -> s.Kind = RuleSetKind.Nutrition category)


    /// Returns the rule set with only the rows that apply to the patient. A row without a location applies to
    /// every patient, and a patient without a location to every row. A row without a department applies to
    /// every patient, and a row with a department only to a patient of that department; the caller gives a
    /// patient without a department the default department first, as for the dose rules.
    let forPatient (pat: PatientData) (set: RuleSet) =
        { set with
            Rows =
                set.Rows
                |> Array.filter (fun r ->
                    PatientCategory.canMeet r.Location pat.Location
                    && pat |> PatientData.department |> PatientCategory.departmentMatches r.Department
                )
        }


    /// Returns the distinct non-empty values of a field of the rows, in row order.
    let distinctOf f (rows: RuleSetData[]) =
        rows |> Array.map f |> Array.filter String.notEmpty |> Array.distinct


    /// Whether a list of a rule set allows the name: an empty list allows any name, and names compare
    /// without regard to case.
    let allows (allowed: string[]) name =
        allowed |> Array.isEmpty
        || allowed |> Array.exists (String.equalsCapInsens name)
