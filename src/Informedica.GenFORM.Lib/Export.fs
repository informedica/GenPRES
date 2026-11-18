namespace Informedica.GenForm.Lib


open System
open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges


/// <summary>
/// Checks the reverse map DoseRule.toData with a round trip of dose rule data through the dose rules.
/// </summary>
/// <remarks>
/// <para>
/// The forward map merges all rows with the same identity (source, generic, route, ..., dose type and dose text)
/// into one dose rule, so the reverse map cannot give back the separate rows. Instead, runPass checks that every
/// input row is contained in an output row with the same identity. Quantities are compared in canonical units,
/// so 1000 mg equals 1 g.
/// </para>
/// <para>
/// Pass 1 runs on the source data; a missing row means that the source has different content under one identity.
/// Pass 2 runs on the output of pass 1, which has one rule per identity, so every row is contained.
/// </para>
/// </remarks>
module Analyze =

    /// <summary>The result of one round-trip pass: the generated rows and the containment counts.</summary>
    type PassResult =
        {
            Label: string
            /// The number of input dose rule data rows.
            InputDataCount: int
            /// The number of input rows that pass validation and that loading keeps.
            SurvivingDataCount: int
            /// The number of dose rules the forward map builds from the input rows.
            ForwardDoseRuleCount: int
            /// The number of distinct rows the reverse map generates from the forward dose rules.
            GeneratedDataCount: int
            /// The surviving input rows that no generated row with the same identity contains.
            Missing: DoseRuleData[]
            /// The missing rows for which no generated row has the same identity.
            NoIdMatch: DoseRuleData[]
            /// The missing rows with a matching identity whose quantities are not contained.
            QuantMiss: DoseRuleData[]
            /// The generated rows, which are the input of the next pass.
            Generated: DoseRuleData[]
            /// The generated rows by identity key, used by reasonLines.
            GenById: Map<string, DoseRuleData[]>
        }

        /// <summary>
        /// The percentage of surviving input rows that a generated row with the same identity contains.
        /// </summary>
        member this.Pct =
            100.0 * float (this.SurvivingDataCount - this.Missing.Length)
            / float (max 1 this.SurvivingDataCount)


    /// <summary>
    /// Compares the output of pass 1 (gen1) with the output of pass 2 (gen2) by rowKey. gen2 can lack rows of gen1,
    /// because the second forward map merges rows with the same identity but different dose values. This comes
    /// from the forward merge, not from a defect in the reverse map.
    /// </summary>
    type FixpointDelta =
        {
            /// The number of rows in the output of pass 1.
            Gen1Count: int
            /// The number of rows in the output of pass 2.
            Gen2Count: int
            /// The number of row keys of gen1 that are absent from gen2.
            InP1NotP2: int
            /// The number of row keys of gen2 that are absent from gen1.
            InP2NotP1: int
            /// The rows of gen1 that are absent from gen2.
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
        // Leaves out the ages when IsAdult is set, because the forward map sets IsAdult.
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
    /// Returns the dose limits of a row as tokens by limit name. Uses DoseRule.getDoseLimits, which builds the
    /// combined units such as mg/kg/day and drops values without a unit, as the forward map does.
    /// </summary>
    let doseLeaves (d: DoseRuleData) : Map<string, string> =
        // Limit.toToken marks a limit as inclusive or exclusive, so >5 and >=5 give different tokens.
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
    /// Returns the administration time, interval and duration limits of a row as tokens by name.
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
    /// Returns the frequencies of a row as a set of canonical tokens, one per frequency, built with the units of the
    /// forward map. Equal frequencies in different time units give the same token. One token per frequency lets
    /// containedIn check for a subset.
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
    /// Returns the identity key of a row. Leaves out the generated ids, the sort number and the quantities.
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
    /// Returns the key of a row used to remove duplicates: the identity key and all quantities as canonical tokens.
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
    /// Tells whether a generated row contains an original row. The generated row may have more values.
    /// The caller checks the identity first.
    /// </summary>
    /// <param name="gen">The generated row.</param>
    /// <param name="orig">The original input row.</param>
    /// <returns>True when every quantity of orig is present in gen with the same value.</returns>
    let containedIn (gen: DoseRuleData) (orig: DoseRuleData) =
        // Every entry of om has the same value in gm.
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
    /// Returns the rows that loading keeps: the valid rows, without the rows that have no substance and no dose
    /// limits. A row without limits stays when its dose rule is kept without limits, because a solution rule
    /// limits it. A row belongs to the dose rule whose DataId is the RuleId of the row; a sheet row has no RuleId,
    /// so the RuleId is computed.
    /// </summary>
    let surviving (doseRules: DoseRule[]) (data: DoseRuleData[]) =
        let ruleIdsWithoutLimits =
            doseRules
            |> Array.filter DoseRule.hasNoLimits
            |> Array.map _.DataId
            |> Set.ofArray

        data
        |> Array.filter (fun d -> DoseRuleData.validateData d |> List.isEmpty)
        |> Array.filter (fun d ->
            not (
                d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace
                && allLimitsEmpty d
            )
            || ruleIdsWithoutLimits.Contains (d |> DoseRuleData.setDataHashIds).RuleId
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
    /// Explains why an input row is missing, using the generated row with the same identity that matches the most
    /// values.
    /// </summary>
    /// <param name="genById">The generated rows by identity key.</param>
    /// <param name="orig">The missing input row.</param>
    /// <returns>One line per value that the best generated row lacks or has different; "no identity match"
    /// when no generated row has the same identity.</returns>
    let reasonLines (genById: Map<string, DoseRuleData[]>) (orig: DoseRuleData) : string list =
        match genById.TryFind(idKey orig) with
        | None -> [ "no identity match" ]
        | Some gens ->
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
    /// Runs one round trip over the input and checks that a generated row contains every surviving input row.
    /// Prints nothing.
    /// </summary>
    /// <param name="forward">Builds the dose rules from rows; a parameter so this module needs no resource
    /// provider.</param>
    /// <param name="label">The name of the pass in the report.</param>
    /// <param name="input">The dose rule data rows to check.</param>
    /// <returns>The counts and the generated rows.</returns>
    let runPass (forward: DoseRuleData[] -> DoseRule[]) (label: string) (input: DoseRuleData[]) : PassResult =
        let fwd = input |> forward
        let survivingInput = surviving fwd input
        let generated = fwd |> Array.collect DoseRule.toData |> Array.distinctBy rowKey
        let genById = generated |> Array.groupBy idKey |> Map.ofArray

        // An input row is missing when no generated row with the same identity contains it.
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

    /// <summary>Compares the rows of two passes by rowKey, in both directions.</summary>
    /// <param name="gen1">The rows generated by pass 1.</param>
    /// <param name="gen2">The rows generated by pass 2 from gen1.</param>
    /// <returns>The counts per direction and the rows of gen1 that are absent from gen2.</returns>
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
/// Builds the dose rule export with the G-Standaard check and writes it to a TSV file.
/// The round-trip check is in <see cref="T:Informedica.GenForm.Lib.Analyze"/>.
/// </summary>
module Export =

    /// <summary>
    /// Builds the dose rules from dose rule data with the loader and drops the warnings. Leaves out the slow
    /// G-Standaard check, which does not matter for containment; exportData runs it once.
    /// </summary>
    /// <param name="provider">The resource provider with the route mappings, form routes, products and solution
    /// rules.</param>
    /// <param name="data">The dose rule data rows.</param>
    /// <returns>The dose rules.</returns>
    let forward (provider: Resources.IResourceProvider) data =
        DoseRuleLoader.fromData
            (provider.GetRouteMappings())
            (provider.GetFormRoutes())
            (provider.GetProducts())
            (provider.GetSolutionRules())
            data
        |> fst

    /// <summary>
    /// Adds the G-Standaard check without a patient to each dose rule, in parallel. A frequency mismatch goes to
    /// FreqCheck, every other signal except Within goes to DoseCheck. A check is None when it has no message.
    /// </summary>
    /// <remarks>
    /// The unknown patient data return all G-Standaard doses, so the category of the dose rule alone narrows the check.
    /// </remarks>
    /// <param name="provider">The resource provider with the G-Standaard provider.</param>
    /// <param name="drs">The dose rules to check.</param>
    /// <returns>The dose rules with the Check field set.</returns>
    let withChecks (provider: Resources.IResourceProvider) drs =
        let gStand = provider.GetGStandProvider()

        // Check is a field of both DoseRule and DoseRuleData, so the annotation makes the record update a DoseRule.
        let check (dr: DoseRule) : DoseRule =
            let signals = dr |> Check.checkDoseRuleWithProvider gStand PatientData.unknown |> _.signals

            // Joins the distinct messages of the severities that keep selects; None when there are none.
            // dataToCsv collapses tabs and newlines, so " | " separates the messages in one TSV cell.
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

        // Memoization.memoize is thread-safe, so each shared cache of the check is loaded at most once in this
        // parallel map.
        drs |> Array.Parallel.map check


    /// <summary>
    /// Builds the export rows from generated rows. Builds the dose rules, adds the G-Standaard check, maps them back
    /// to rows and removes duplicates. Then sets the ids and numbers SortNo within each group, as the loader does.
    /// </summary>
    /// <param name="provider">The resource provider for the forward map and the G-Standaard check.</param>
    /// <param name="generated">The generated rows of the last pass.</param>
    /// <returns>The checked export rows.</returns>
    let exportData (provider: Resources.IResourceProvider) generated =
        generated
        |> forward provider
        |> withChecks provider
        |> Array.collect DoseRule.toData
        |> Array.distinctBy Analyze.rowKey
        |> Array.map DoseRuleData.setDataHashIds
        |> Array.groupBy _.GrpId
        |> Array.collect (snd >> Array.mapi (fun i dd -> { dd with SortNo = i }))


    /// <summary>Writes the export rows as TSV to a file and returns the path of the file.</summary>
    /// <param name="fileName">The name of the TSV file.</param>
    /// <param name="data">The dose rule data rows.</param>
    /// <returns>The path in the parent directory that has the file, searched upward from the current
    /// directory.</returns>
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
