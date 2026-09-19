namespace Informedica.GenForm.Lib


open System
open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges


/// <summary>
/// Round-trips data through the reverse map `DoseRule.toData` to check it.
/// </summary>
/// <remarks>
/// <para>
/// The reverse map lives in `DoseRule.fs`; this module just runs it and reports
/// which rows make the trip.
/// </para>
/// <para>
/// The forward map gives every rule identity (source, generic, route, ..., dose type
/// + dose text) a single dose rule, so it merges all source rows that share an
/// identity into one. Reversing that rule cannot recover the separate inputs, so
/// instead of checking input = output, `runPass` checks CONTAINMENT: every input
/// row matches some output row with the same identity (the output may hold more
/// detail). Quantities are compared in canonical units, so 1000 mg and 1 g count as
/// equal.
/// </para>
/// <para>
/// PASS 1 runs on the source data; a row going missing means the source put distinct
/// content under one identity — it does not fit the model. PASS 2 re-runs on PASS 1's
/// output, which already has one rule per identity, so everything is contained: a
/// fixpoint.
/// </para>
/// </remarks>
module Analyze =

    /// <summary>
    /// Result of a single round-trip pass: the generated (reverse-of-forward,
    /// merged) dataset plus the FORWARD containment statistics.
    /// </summary>
    type PassResult =
        {
            Label: string
            /// number of input dose rule data rows
            InputDataCount: int
            /// input rows that pass validation AND carry a substance or a dose limit
            SurvivingDataCount: int
            /// non-empty dose rules generated from the validated rows (empty-limit rules dropped)
            ForwardDoseRuleCount: int
            /// distinct dose rule data rows reverse-generated from the forward dose rules
            GeneratedDataCount: int
            /// input rows not contained in any same-identity generated row
            Missing: DoseRuleData[]
            /// missing because no generated row shares the categorical identity
            NoIdMatch: DoseRuleData[]
            /// missing because identity matched but quantitative values were not contained
            QuantMiss: DoseRuleData[]
            /// reverse-of-forward, merged (feeds the next pass)
            Generated: DoseRuleData[]
            /// generated rows indexed by categorical identity (for reasonLines)
            GenById: Map<string, DoseRuleData[]>
        }

        /// <summary>
        /// Forward round-trip percentage: share of surviving input rows contained
        /// in a same-identity generated row. Derived from SurvivingDataCount and Missing.
        /// </summary>
        member this.Pct =
            100.0 * float (this.SurvivingDataCount - this.Missing.Length)
            / float (max 1 this.SurvivingDataCount)


    /// <summary>
    /// Compares PASS 1 output (gen1) with PASS 2 output (gen2 = reverse-of-forward
    /// of gen1). An exact fixpoint means gen2 equals gen1 (compared by rowKey). It
    /// is NOT exact: the second forward merges rows sharing a categorical identity
    /// but differing in dose values (the merge keys on identity, ignoring dose
    /// values), so those gen1 rows vanish in gen2. A forward-side fold, not a
    /// reverse-map defect.
    /// </summary>
    type FixpointDelta =
        {
            /// number of rows in PASS 1 output (gen1)
            Gen1Count: int
            /// number of rows in PASS 2 output (gen2 = reverse-of-forward of gen1)
            Gen2Count: int
            /// gen1 rowKeys absent from gen2 (rows folded away by the second forward)
            InP1NotP2: int
            /// gen2 rowKeys absent from gen1 (rows that appear only after re-forwarding)
            InP2NotP1: int
            /// the actual gen1 rows that vanish in gen2 (same-identity dose-value folds)
            Collapsed: DoseRuleData[]
        }


    let brStr = Option.map _.ToString() >> Option.defaultValue ""


    let genKey (g: GenericData) =
        [
            g.Name
            g.Form
            g.Brand
            g.GPKs |> Array.sort |> String.concat ","
            g.HPKs |> Array.sort |> String.concat ","
        ]
        |> String.concat "~"


    let patKey (p: PatientCategoryData) =
        // Location recovered. When IsAdult, ages are masked (forward sets IsAdult).
        let minAge, maxAge = if p.IsAdult then "", "" else brStr p.MinAge, brStr p.MaxAge

        [
            p.Location
            p.Dep
            string p.IsAdult
            $"%A{p.Gender}"
            minAge
            maxAge
            brStr p.MinWeight
            brStr p.MaxWeight
            brStr p.MinBSA
            brStr p.MaxBSA
            brStr p.MinGestAge
            brStr p.MaxGestAge
            brStr p.MinPMAge
            brStr p.MaxPMAge
        ]
        |> String.concat "~"


    /// <summary>
    /// Populated dose-limit leaves of a row, built by the library's OWN parser
    /// (getDoseLimits builds the mg/kg/day composites and drops values whose unit is
    /// absent, exactly as the forward path does). One row -> one DoseLimit.
    /// </summary>
    let doseLeaves (d: DoseRuleData) : Map<string, string> =
        // Limit.toToken keeps the inclusive/exclusive distinction (incl|/excl|)
        // on top of the canonical ValueUnit.toToken, so >5 and >=5 don't collide.
        let limTok = Option.map Limit.toToken

        DoseRule.getDoseLimits [| d |]
        |> Array.collect (fun dl ->
            [|
                "qty.min", limTok dl.Quantity.Min
                "qty.max", limTok dl.Quantity.Max
                "qtyAdj.min", limTok dl.QuantityAdjust.Min
                "qtyAdj.max", limTok dl.QuantityAdjust.Max
                "perTime.min", limTok dl.PerTime.Min
                "perTime.max", limTok dl.PerTime.Max
                "perTimeAdj.min", limTok dl.PerTimeAdjust.Min
                "perTimeAdj.max", limTok dl.PerTimeAdjust.Max
                "rate.min", limTok dl.Rate.Min
                "rate.max", limTok dl.Rate.Max
                "rateAdj.min", limTok dl.RateAdjust.Min
                "rateAdj.max", limTok dl.RateAdjust.Max
            |]
        )
        |> Array.choose (fun (k, v) -> v |> Option.map (fun t -> k, t))
        |> Map.ofArray


    /// <summary>
    /// Populated schedule time leaves (administration / interval / duration) as base tokens.
    /// </summary>
    let schedLeaves (d: DoseRuleData) : Map<string, string> =
        let timeTok (b: BigRational option) (us: string) =
            match b, us |> Utils.Units.timeUnit with
            | Some v, Some u -> ValueUnit.singleWithUnit u v |> ValueUnit.toToken |> Some
            | Some v, None -> $"%s{string v} %s{us |> String.trim}" |> Some
            | None, _ -> None

        let s = d.ScheduleData

        [
            "admin.min", timeTok s.MinTime s.TimeUnit
            "admin.max", timeTok s.MaxTime s.TimeUnit
            "int.min", timeTok s.MinInt s.IntUnit
            "int.max", timeTok s.MaxInt s.IntUnit
            "dur.min", timeTok s.MinDur s.DurUnit
            "dur.max", timeTok s.MaxDur s.DurUnit
        ]
        |> List.choose (fun (k, v) -> v |> Option.map (fun t -> k, t))
        |> Map.ofList


    /// <summary>
    /// Frequency set, one canonical token per value. Mirrors the forward path,
    /// which builds DoseRule.Frequencies with `Utils.Units.freqUnit` (times/time)
    /// and `ValueUnit.withUnit`, then normalizes via `ValueUnit.toToken` so equivalent
    /// time-units collapse. Per-value tokens keep the subset semantics used by
    /// `containedIn`.
    /// </summary>
    let freqSet (d: DoseRuleData) : Set<string> =
        let s = d.ScheduleData

        match s.FreqUnit |> Utils.Units.freqUnit with
        | Some u ->
            s.Freqs
            |> Array.map (fun f -> [| f |] |> ValueUnit.withUnit u |> ValueUnit.toToken)
            |> Set.ofArray
        | None ->
            s.Freqs
            |> Array.map (fun f -> $"%s{string f}/%s{s.FreqUnit |> String.trim}")
            |> Set.ofArray


    /// <summary>
    /// Categorical identity of a row (exact). Excludes generated Id/GrpId/SortNo and
    /// the quantitative values (compared separately, semantically).
    /// </summary>
    let idKey (d: DoseRuleData) =
        [
            d.Source
            d.SourceText
            d.Indication
            d.Route
            d.PatientText
            d.ScheduleText
            genKey d.Generic
            patKey d.Patient
            d.ScheduleData.DoseType |> String.toLower
            d.ScheduleData.DoseText
            d.ScheduleData.DoseLimitData.Component
            d.ScheduleData.DoseLimitData.Substance
        ]
        |> String.concat "||"


    /// <summary>
    /// Full row key for dedup (identity + all quantitative leaves, base-normalized).
    /// </summary>
    let rowKey (d: DoseRuleData) =
        let m2s = Map.toList >> List.map (fun (k, v) -> k + "=" + v) >> String.concat ";"

        [
            idKey d
            doseLeaves d |> m2s
            schedLeaves d |> m2s
            freqSet d |> Set.toList |> String.concat ","
        ]
        |> String.concat "##"


    /// <summary>
    /// original row is CONTAINED in a generated row: every quantitative value present
    /// in the original equals the generated's; the generated may carry MORE values.
    /// (identity is matched separately, before calling this.)
    /// </summary>
    /// <param name="gen">The generated row that may contain the original.</param>
    /// <param name="orig">The original input row being checked for containment.</param>
    /// <returns>True when every quantitative leaf of `orig` is present and equal in `gen`.</returns>
    let containedIn (gen: DoseRuleData) (orig: DoseRuleData) =
        // every entry in the smaller map equals the corresponding entry in the larger one
        let subMap (om: Map<string, string>) (gm: Map<string, string>) =
            om
            |> Map.forall (fun k v ->
                match gm.TryFind k with
                | Some gv -> gv = v
                | None -> false
            )

        subMap (doseLeaves orig) (doseLeaves gen)
        && subMap (schedLeaves orig) (schedLeaves gen)
        && Set.isSubset (freqSet orig) (freqSet gen)


    let allLimitsEmpty (d: DoseRuleData) =
        let l = d.ScheduleData.DoseLimitData

        [
            l.MinQty
            l.MaxQty
            l.MinQtyAdj
            l.MaxQtyAdj
            l.MinPerTime
            l.MaxPerTime
            l.MinPerTimeAdj
            l.MaxPerTimeAdj
            l.MinRate
            l.MaxRate
            l.MinRateAdj
            l.MaxRateAdj
        ]
        |> List.forall Option.isNone

    /// <summary>
    /// surviving rows of any dataset = valid rows (forward keeps them), minus
    /// empty-limit no-substance rows (forward keeps them but they reverse to nothing).
    /// </summary>
    let surviving (data: DoseRuleData[]) =
        data
        |> Array.filter (fun d -> DoseRuleData.validateData d |> List.isEmpty)
        |> Array.filter (fun d ->
            not (
                d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace
                && allLimitsEmpty d
            )
        )

    let trunc n (s: string) = if s.Length > n then s[.. n - 1] + "…" else s

    let rowLine (d: DoseRuleData) =
        let l = d.ScheduleData.DoseLimitData
        let s = List.init d.Source.Length (fun _ -> " ") |> String.concat ""

        sprintf
            "%s | %s | dt=%s cmp=%s subst=%s freqs=[%s]\n  %s | %s"
            d.Source
            d.Route
            d.ScheduleData.DoseType
            l.Component
            l.Substance
            (d.ScheduleData.Freqs |> Array.map string |> String.concat ",")
            s
            (trunc 80 d.ScheduleText)

    /// <summary>
    /// The reason line(s) why a single input row is missing, against a pass's
    /// `GenById`: picks the generated sibling that matches the most leaves and
    /// shows what it lacks.
    /// </summary>
    /// <param name="genById">Generated rows indexed by categorical identity.</param>
    /// <param name="orig">The missing input row to explain.</param>
    /// <returns>Human-readable lines describing which leaves the best sibling lacks.</returns>
    let reasonLines (genById: Map<string, DoseRuleData[]>) (orig: DoseRuleData) : string list =
        match genById.TryFind(idKey orig) with
        | None -> [ "no identity match" ]
        | Some gens ->
            // pick the generated sibling that matches the most leaves, show what it lacks
            let od, os, ofq = doseLeaves orig, schedLeaves orig, freqSet orig

            let best =
                gens
                |> Array.maxBy (fun g ->
                    let gd, gs, gf = doseLeaves g, schedLeaves g, freqSet g

                    (od |> Map.filter (fun k v -> gd.TryFind k = Some v) |> Map.count)
                    + (os |> Map.filter (fun k v -> gs.TryFind k = Some v) |> Map.count)
                    + (Set.intersect ofq gf |> Set.count)
                )

            let gd, gs, gf = doseLeaves best, schedLeaves best, freqSet best

            [
                for KeyValue(k, v) in od do
                    if gd.TryFind k <> Some v then
                        sprintf "%-14s orig=%-18s gen=%s" k v (gd.TryFind k |> Option.defaultValue "·")
                for KeyValue(k, v) in os do
                    if gs.TryFind k <> Some v then
                        sprintf "%-14s orig=%-18s gen=%s" k v (gs.TryFind k |> Option.defaultValue "·")
                let fmiss = Set.difference ofq gf

                if not fmiss.IsEmpty then
                    sprintf "freqs missing  orig=%s gen=%s" (String.concat "," ofq) (String.concat "," gf)
            ]


    /// <summary>
    /// One round-trip + FORWARD containment check over <paramref name="input"/>.
    /// Pure: returns the `PassResult` (stats + generated set); does not print.
    /// </summary>
    /// <param name="forward">Rebuilds DoseRuleData[] -> DoseRule[] (passed in so this
    /// module stays free of any live provider).</param>
    /// <param name="label">Human-readable name identifying the pass (for reporting).</param>
    /// <param name="input">The dose rule data rows to round-trip and check.</param>
    /// <returns>The `PassResult` with statistics and the generated dataset.</returns>
    let runPass (forward: DoseRuleData[] -> DoseRule[]) (label: string) (input: DoseRuleData[]) : PassResult =
        let survivingInput = surviving input
        let fwd = input |> forward
        let generated = fwd |> Array.collect DoseRule.toData |> Array.distinctBy rowKey
        let genById = generated |> Array.groupBy idKey |> Map.ofArray

        // an input row is MISSING when no generated row with the same identity contains it
        let missing =
            survivingInput
            |> Array.filter (fun orig ->
                match genById.TryFind(idKey orig) with
                | Some gens -> gens |> Array.exists (fun g -> containedIn g orig) |> not
                | None -> true
            )

        let noIdMatch, quantMiss = missing |> Array.partition (fun o -> genById.ContainsKey(idKey o) |> not)

        {
            Label = label
            InputDataCount = input.Length
            SurvivingDataCount = survivingInput.Length
            ForwardDoseRuleCount = fwd.Length
            GeneratedDataCount = generated.Length
            Missing = missing
            NoIdMatch = noIdMatch
            QuantMiss = quantMiss
            Generated = generated
            GenById = genById
        }

    /// <summary>
    /// Direct fixpoint delta: rows in <paramref name="gen1"/> not in
    /// <paramref name="gen2"/> (and vice versa), keyed by `rowKey`.
    /// </summary>
    /// <param name="gen1">PASS 1 generated dataset.</param>
    /// <param name="gen2">PASS 2 generated dataset (reverse-of-forward of gen1).</param>
    /// <returns>The `FixpointDelta` with the per-direction counts and collapsed rows.</returns>
    let fixpointDelta (gen1: DoseRuleData[]) (gen2: DoseRuleData[]) : FixpointDelta =
        let g1Keys = gen1 |> Array.map rowKey |> Set.ofArray
        let g2Keys = gen2 |> Array.map rowKey |> Set.ofArray

        {
            Gen1Count = gen1.Length
            Gen2Count = gen2.Length
            InP1NotP2 = Set.difference g1Keys g2Keys |> Set.count
            InP2NotP1 = Set.difference g2Keys g1Keys |> Set.count
            Collapsed = gen1 |> Array.filter (fun d -> g2Keys.Contains(rowKey d) |> not)
        }


/// <summary>
/// Builds the canonicalized, G-Standaard-checked dose-rule export and writes it to
/// a TSV file. Wraps the live forward rebuild + reverse map around a resource
/// provider; the round-trip CHECK itself lives in <see cref="T:Informedica.GenForm.Lib.Analyze"/>.
/// </summary>
module Export =

    /// <summary>
    /// PURE rebuild: DoseRuleData[] -> DoseRule[] via the loader (warnings dropped).
    /// Kept free of the (expensive) G-Standaard check so the round-trip passes stay
    /// fast — the check is irrelevant to containment and is run once, on export.
    /// </summary>
    /// <param name="provider">Resource provider supplying route mappings, form routes and products.</param>
    /// <param name="data">The dose rule data rows to rebuild into dose rules.</param>
    /// <returns>The forward-built dose rules (loader warnings discarded).</returns>
    let forward (provider: Resources.IResourceProvider) data =
        DoseRuleLoader.fromData (provider.GetRouteMappings()) (provider.GetFormRoutes()) (provider.GetProducts()) data
        |> fst

    /// <summary>
    /// Attach the no-patient G-Standaard check to each rule, in parallel.
    /// `Check` is a RuleCheck { FreqCheck; DoseCheck }: the graded signals (severity
    /// other than Within) split by kind — FrequencyMismatch -> FreqCheck, the dose-limit
    /// severities (over norm/absolute, under norm, unit mismatch, no monitoring) ->
    /// DoseCheck. None when there is nothing to report (limits agree with G-Standaard).
    /// </summary>
    /// <remarks>
    /// G-Standaard dose-rule check WITHOUT a specific patient: the patient only scopes
    /// the G-Standaard query; the actual narrowing is done by the rule's OWN category,
    /// so an empty base patient (`Patient.patient`) returns the full G-Standaard dose
    /// set and the check is scoped purely by the rule itself.
    /// </remarks>
    /// <param name="provider">Resource provider supplying the G-Standaard provider.</param>
    /// <param name="drs">The dose rules to annotate with check signals.</param>
    /// <returns>The dose rules with their `Check` field populated.</returns>
    let withChecks (provider: Resources.IResourceProvider) drs =
        let gStand = provider.GetGStandProvider()

        // annotate: `Check` is a field on both DoseRule and DoseRuleData, so the
        // record-update target must be pinned to DoseRule.
        let check (dr: DoseRule) : DoseRule =
            let signals = dr |> Check.checkDoseRuleWithProvider gStand Patient.patient |> _.signals

            // Messages for the severities matching `keep`, deduped and joined; None when
            // empty. dataToCsv collapses tabs/newlines, so " | " keeps multiple messages
            // legible in a single TSV cell.
            let collect keep =
                signals
                |> Array.choose (fun (sev, s) -> if keep sev then Some s else None)
                |> Array.filter String.notEmpty
                |> Array.distinct
                |> function
                    | [||] -> None
                    | xs -> xs |> String.concat " | " |> Some

            { dr with
                Check =
                    {
                        FreqCheck = collect (fun sev -> sev = Check.FrequencyMismatch)
                        DoseCheck = collect (fun sev -> sev <> Check.Within && sev <> Check.FrequencyMismatch)
                    }
            }

        // No warm-up needed: Utils.Lib.Memoization.memoize is thread-safe
        // (ConcurrentDictionary + Lazy), so the shared caches the check path touches
        // (ZIndex rule cache, ZForm "Units"/"Frequencies" sheets) are each loaded at
        // most once even under this parallel map.
        drs |> Array.Parallel.map check


    /// <summary>
    /// Build the final export dataset from a (fixpoint) generated set: forward-rebuild,
    /// attach the G-Standaard check, reverse to data, dedup, then restamp RowId/RuleId/
    /// GrpId and number SortNo within each group exactly as the loader does. Single
    /// check pass — PASS 2 is a fixpoint, so re-forwarding the input is cheap.
    /// </summary>
    /// <param name="provider">Resource provider for the forward rebuild and G-Standaard check.</param>
    /// <param name="generated">The (fixpoint) generated dataset to canonicalize and check.</param>
    /// <returns>The checked, deduped, restamped dose rule data ready for export.</returns>
    let exportData (provider: Resources.IResourceProvider) generated =
        generated
        |> forward provider
        |> withChecks provider
        |> Array.collect DoseRule.toData
        |> Array.distinctBy Analyze.rowKey
        |> Array.map DoseRuleData.setDataHashIds
        |> Array.groupBy _.GrpId
        |> Array.collect (snd >> Array.mapi (fun i dd -> { dd with SortNo = i }))


    /// <summary>
    /// Write the export dataset as TSV to <paramref name="fileName"/> and return the
    /// resolved path (parent directory found upward from the current directory).
    /// </summary>
    /// <param name="fileName">The TSV file name to write.</param>
    /// <param name="data">The dose rule data rows to serialize as TSV.</param>
    /// <returns>The resolved path the file was written to.</returns>
    let writeExport fileName data =
        data
        |> DoseRuleData.dataToCsv
        |> String.concat "\n"
        |> File.writeTextToFile fileName

        let dir =
            Environment.CurrentDirectory
            |> Directory.tryFindParent fileName
            |> Option.defaultValue "."

        $"{dir}/{fileName}"
