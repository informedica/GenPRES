namespace Informedica.GenForm.Lib


/// <summary>
/// Orchestration / loading layer for dose rules. Composes the pure dose-rule
/// DATA module (<c>DoseRuleData</c>) with the pure mapping module
/// (<c>DoseRule</c>) and the resource-dependent <c>Product</c>/<c>Mapping</c>
/// modules. This is the resources-side seam: <c>DoseRule</c> never references
/// <c>DoseRuleData</c>; instead this module wires the two together.
/// </summary>
module DoseRuleLoader =

    open Informedica.Utils.Lib.BCL

    open Utils


    let getData dataUrlId =
        Web.getDataFromSheet dataUrlId "DoseRules" |> DoseRuleData.parseDoseRuleData


    /// Cheap pre-filter: keep only products whose Generic matches a component
    /// used somewhere in the group, before the per-component product narrowing.
    let candidateProducts (prods: ProductComponent[]) (rs: DoseRuleData[]) =
        let cmps = rs |> Array.map _.ScheduleData.DoseLimitData.Component |> Array.distinct

        prods
        |> Array.filter (fun p -> cmps |> Array.exists (String.equalsCapInsens p.Generic))


    let group routeMapping (prods: ProductComponent[]) chunk =
        chunk
        |> Array.collect (fun (_groupKey: string list, data) ->
            // every row in the group shares the same DataGroupKey (it is part of
            // the group identity); rebuild the structured key from a representative
            // row for the Generic/Route access below and for GroupedRuleData.
            let key = DoseRuleData.dataGroupKey (Array.head data)

            let candidates = data |> candidateProducts prods

            data
            // make sure that the chunked dose rule data
            // have ids set
            |> Array.map DoseRuleData.setDataHashIds
            // make sure the sort order is preserved
            |> Array.mapi (fun i dd -> { dd with SortNo = i })
            // perform an additional grouping to differentiate
            // between dose type, note dose type is part of the
            // dose rule hashed id
            |> Array.groupBy _.RuleId
            |> Array.map (fun (_, data) ->
                // collapse duplicate (component, substance) rows within the rule;
                // warns when collapsed rows carry differing dose-limit values
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


    let fromData routeMapping formRoutes prods (data: DoseRuleData[]) =
        // get the validated dose rule data from data
        // and the warnings
        let data, warnings =
            let valid, invalid =
                data
                // validate the dose rule data
                |> Array.map (fun d -> d, DoseRuleData.validateData d)
                |> Array.partition (snd >> List.isEmpty)

            valid |> Array.map fst, invalid |> Array.map (snd >> List.toArray) |> Array.collect id

        let addFormLimits = DoseRule.addFormLimits routeMapping formRoutes

        let grouped =
            data
            // group by dose rule data group (single source of truth shared with
            // the GrpId hash in DoseRuleData.setDataHashIds)
            |> Array.groupBy DoseRuleData.groupKeyFields
            // setup chunks of grouped dose rule data
            |> Array.chunkBySize Parallel.totalWorders
            // map to grouped dose rule data to map to a dose rule
            |> Array.map (fun chunk -> async { return chunk |> group routeMapping prods })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.collect id

        // RowId dedup warnings raised inside the parallel group pass
        let dedupWarnings = grouped |> Array.collect (_.Warnings >> List.toArray)

        // Warm-up phase to enable safe parallelization below. mapToDoseRule /
        // addFormLimits are the first code to exercise the unit / dose-limit
        // modules; running a sample single-threaded forces their one-time
        // (static) initialization on one thread, avoiding a cold concurrent
        // type-initialization deadlock when the parallel tasks below trigger
        // those inits at once. We warm a whole chunk (not a single group) so the
        // initialized code paths do not depend on which group happens to be first.
        let warm, tail =
            if grouped |> Array.length <= Parallel.totalWorders then
                grouped, [||]
            else
                grouped[.. Parallel.totalWorders - 1], grouped[Parallel.totalWorders ..]

        let head = warm |> Array.collect (DoseRule.mapToDoseRule >> Array.map addFormLimits)

        tail
        |> Array.chunkBySize Parallel.totalWorders
        // map each grouped dose rule data to mapToDoseRule
        // that will expand for all available product forms
        // and form unit groups
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
        // drop dose rules left with no meaningful limits (a row that passes
        // validateData but carries no dose values yields an empty dose rule)
        |> DoseRule.removeEmptyLimits,
        Array.append warnings dedupWarnings |> Array.map Warning |> Array.toList


    /// <summary>
    /// Impure adapter: loads DoseRuleData via the `getData` thunk and delegates
    /// to the pure <c>fromData</c>, carrying the product warnings.
    /// Kept for existing callers/tests.
    /// </summary>
    let get getData routeMapping formRoutes prods = getData () |> fromData routeMapping formRoutes prods


    /// Build a GetDoseRules-shaped function from a custom data source.
    /// `getData` reads DoseRuleData rows from `path` (e.g. a Pass-4 TSV).
    /// The result matches the ResourceConfig.GetDoseRules field shape exactly:
    /// DoseRuleData[] -> RouteMapping[] -> FormRoute[] -> ProductComponent[] ->
    /// (DoseRule[] * Message list). The leading DoseRuleData[] (the
    /// resources-loaded rows) is ignored here because this adapter reads its
    /// rows from `path` instead.
    let getFromGetData getData path = fun (_: DoseRuleData[]) -> get (fun () -> getData path)
