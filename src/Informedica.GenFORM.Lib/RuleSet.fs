namespace Informedica.GenForm.Lib


/// The check of the rows of the "RuleSets" sheet against the other sheets.
module RuleSet =

    open Informedica.Utils.Lib.BCL


    /// A dose rule as the check reads it: its indication, its generic and its component.
    type DoseRuleKey =
        {
            Indication: string
            Generic: string
            Component: string
        }


    /// The warnings of the rows against the dose rules and the generics of the Formulary sheet: a
    /// category with more than one label, an indication that no dose rule has, a generic that no
    /// dose rule has under its indication, and a generic or a component of a dose rule under the
    /// indication of a row that has no product.
    let check (doseRules: DoseRuleKey[]) (products: Set<string>) (rows: RuleSetData[]) =
        let pairs = doseRules |> Array.map (fun d -> d.Indication, d.Generic) |> Set.ofArray
        let indications = pairs |> Set.map fst

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
                    Warning $"RuleSets: %s{r.Category}, no dose rule has %s{r.Generic} under %s{r.Indication}"

            for category, indication in rows |> Array.map (fun r -> r.Category, r.Indication) |> Array.distinct do
                yield!
                    doseRules
                    |> Array.filter (fun d -> d.Indication = indication)
                    |> Array.collect (fun d -> [| d.Generic; d.Component |])
                    |> Array.filter String.notEmpty
                    |> Array.distinct
                    |> Array.filter (fun n -> products |> Set.contains n |> not)
                    |> Array.map (fun n ->
                        Warning
                            $"RuleSets: %s{category}, %s{n} under %s{indication} has no product in the Formulary sheet"
                    )
        ]


    /// The check of the rows against the dose rule data and the formulary products.
    let checkData (doseRules: DoseRuleData[]) (products: FormularyProduct[]) rows =
        let doseRules =
            doseRules
            |> Array.map (fun d ->
                {
                    Indication = d.Indication
                    Generic = d.Generic.Name
                    Component = d.ScheduleData.DoseLimitData.Component
                }
            )

        rows |> check doseRules (products |> Array.map _.Generic |> Set.ofArray)
