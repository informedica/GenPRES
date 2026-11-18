namespace Informedica.GenForm.Lib


/// Builds the dose rules from the rows of the DoseRules sheet and the products. DoseRule does not
/// reference DoseRuleData; this module joins the two.
module DoseRuleLoader =

    open Informedica.Utils.Lib.BCL

    open Utils


    let getData dataUrlId =
        Web.getDataFromSheet dataUrlId "DoseRules" |> DoseRuleData.parseDoseRuleData


    /// Returns the products whose generic equals, ignoring case, a component of one of the rows.
    let candidateProducts (prods: ProductComponent[]) (rs: DoseRuleData[]) =
        let cmps = rs |> Array.map _.ScheduleData.DoseLimitData.Component |> Array.distinct

        prods
        |> Array.filter (fun p -> cmps |> Array.exists (String.equalsCapInsens p.Generic))


    let group routeMapping (prods: ProductComponent[]) chunk =
        chunk
        |> Array.collect (fun (_groupKey: string list, data) ->
            // Every row in the group has the same group key, so the first row gives it.
            let key = DoseRuleData.dataGroupKey (Array.head data)

            let candidates = data |> candidateProducts prods

            data
            |> Array.map DoseRuleData.setDataHashIds
            // Keeps the order of the rows.
            |> Array.mapi (fun i dd -> { dd with SortNo = i })
            // Groups by rule id, which includes the dose type.
            |> Array.groupBy _.RuleId
            |> Array.map (fun (_, data) ->
                // Keeps the first of the rows of a rule with the same component and substance, with a
                // warning when their dose limits differ.
                let data, rowWarns = data |> DoseRuleData.dedupRowsByRowId

                let cmps = data |> Array.map _.ScheduleData.DoseLimitData.Component |> Array.distinct

                let prods =
                    cmps
                    |> Array.collect (fun cmp ->
                        candidates
                        |> Product.filter
                            routeMapping
                            key.Route
                            cmp
                            key.Generic.Form
                            key.Generic.Brand
                            key.Generic.GPKs
                            key.Generic.HPKs

                    )

                let frms = prods |> Array.map _.Form |> Array.distinct

                {
                    DataGroupKey = key
                    DoseRuleData = data
                    Forms = frms
                    Products = prods
                    Warnings = rowWarns
                }
            )
        )


    /// The dose rules of the rows, with the warnings. A dose rule without any limit is kept when a
    /// solution rule that applies to it limits every component; when a solution rule applies but does
    /// not, it is left out with a warning.
    let fromData routeMapping formRoutes prods (solutionRules: SolutionRule[]) (data: DoseRuleData[]) =
        // Keeps the valid rows, with a warning for each error of an invalid row.
        let data, warnings =
            let valid, invalid =
                data
                |> Array.map (fun d -> d, DoseRuleData.validateData d)
                |> Array.partition (snd >> List.isEmpty)

            valid |> Array.map fst, invalid |> Array.map (snd >> List.toArray) |> Array.collect id

        // Leaves out a rule whose rows do not share one adjust unit, or that has a per kg or per m2
        // value without an adjust unit.
        let data, unitWarnings = data |> DoseRuleData.checkUnits

        let addFormLimits = DoseRule.addFormLimits routeMapping formRoutes

        let grouped =
            data
            // Groups by the same fields that DoseRuleData.setDataHashIds hashes into the GrpId.
            |> Array.groupBy DoseRuleData.groupKeyFields
            |> Array.chunkBySize Parallel.totalWorders
            |> Array.map (fun chunk -> async { return chunk |> group routeMapping prods })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.collect id

        // The warnings for rows of a rule with the same component and substance.
        let dedupWarnings = grouped |> Array.collect (_.Warnings >> List.toArray)

        // Maps the first chunk on one thread before the rest in parallel. This runs the static
        // initialization of the unit and dose-limit modules on one thread, because concurrent type
        // initialization can deadlock. A whole chunk is used so that the initialization does not
        // depend on which group comes first.
        let warm, tail =
            if grouped |> Array.length <= Parallel.totalWorders then
                grouped, [||]
            else
                grouped[.. Parallel.totalWorders - 1], grouped[Parallel.totalWorders ..]

        let head = warm |> Array.collect (DoseRule.mapToDoseRule >> Array.map addFormLimits)

        let rules =
            tail
            |> Array.chunkBySize Parallel.totalWorders
            // Maps each group to a dose rule per product form and form unit group.
            |> Array.map (fun grps ->
                async {
                    let rules = grps |> Array.collect DoseRule.mapToDoseRule |> Array.map addFormLimits
                    return rules
                }
            )
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.collect id
            |> Array.append head

        rules
        // Leaves out the dose rules without limits, such as a valid row without dose values, except
        // a dose rule that a solution rule limits.
        |> DoseRule.removeEmptyLimits (SolutionRule.anyLimitsDoseRule routeMapping solutionRules),
        ([ warnings; unitWarnings; dedupWarnings ]
         |> Array.concat
         |> Array.map Warning
         |> Array.toList)
        @ (solutionRules |> SolutionRule.unlimitedDoseRules routeMapping rules)
