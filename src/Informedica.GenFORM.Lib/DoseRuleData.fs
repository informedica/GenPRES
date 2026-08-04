namespace Informedica.GenForm.Lib


module DoseRuleData =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL

    open Utils


    /// <summary>
    /// The canonical column order of the "DoseRules" sheet, and the emit order of
    /// <c>dataToCsv</c>. The column semantics live on <c>DoseRuleData</c> in
    /// Types.fs; which of these may be absent from a sheet is fixed by the tolerant
    /// readers in <c>parseDoseRuleData</c> and pinned by the column-contract tests.
    /// </summary>
    // TODO: "Loc" is read by parseDoseRuleData (into PatientCategoryData.Location)
    // but missing here, so a dataToCsv round-trip silently drops the location.
    // Either add it in the position the sheet uses, or stop reading it.
    let headers =
        [
            "RowId"
            "RuleId"
            "GrpId"
            "SortNo"
            "Source"
            "Generic"
            "Form"
            "Brand"
            "Route"
            "GPKs"
            "HPKs"
            "Indication"
            "SourceText"
            "PatientText"
            "ScheduleText"
            "Dep"
            "IsAdult"
            "Gender"
            "MinAge"
            "MaxAge"
            "MinWeight"
            "MaxWeight"
            "MinBSA"
            "MaxBSA"
            "MinGestAge"
            "MaxGestAge"
            "MinPMAge"
            "MaxPMAge"
            "DoseType"
            "DoseText"
            "CmpBased"
            "Component"
            "Substance"
            "Freqs"
            "DoseUnit"
            "AdjustUnit"
            "FreqUnit"
            "RateUnit"
            "MinTime"
            "MaxTime"
            "TimeUnit"
            "MinInt"
            "MaxInt"
            "IntUnit"
            "MinDur"
            "MaxDur"
            "DurUnit"
            "MinQty"
            "MaxQty"
            "MinQtyAdj"
            "MaxQtyAdj"
            "MinPerTime"
            "MaxPerTime"
            "MinPerTimeAdj"
            "MaxPerTimeAdj"
            "MinRate"
            "MaxRate"
            "MinRateAdj"
            "MaxRateAdj"
            "Validated"
            "FreqCheck"
            "DoseCheck"
        ]
        |> String.concat "\t"
        |> List.singleton


    let distinctByDoseLimit (d: DoseRuleData) =
        d.ScheduleData.DoseType,
        d.ScheduleData.DoseLimitData.Substance |> String.isNullOrWhiteSpace,
        d.ScheduleData.AdjustUnit |> String.isNullOrWhiteSpace,
        d.ScheduleData.DoseLimitData.MinQty.IsSome,
        d.ScheduleData.DoseLimitData.MaxQty.IsSome,
        d.ScheduleData.DoseLimitData.MinQtyAdj.IsSome,
        d.ScheduleData.DoseLimitData.MaxQtyAdj.IsSome,
        d.ScheduleData.DoseLimitData.MinPerTime.IsSome,
        d.ScheduleData.DoseLimitData.MaxPerTime.IsSome,
        d.ScheduleData.DoseLimitData.MinPerTimeAdj.IsSome,
        d.ScheduleData.DoseLimitData.MaxPerTimeAdj.IsSome,
        d.ScheduleData.DoseLimitData.MinRate.IsSome,
        d.ScheduleData.DoseLimitData.MaxRate.IsSome,
        d.ScheduleData.DoseLimitData.MinRateAdj.IsSome,
        d.ScheduleData.DoseLimitData.MaxRateAdj.IsSome


    /// Reduce a row's product narrowing to a single mechanism by precedence
    /// HPKs > GPKs > Brand > Form: keep the highest-precedence field that is
    /// present and clear every lower-precedence one. Rows with no narrowing are
    /// left unchanged (they remain generic-wide rules).
    let withSingleNarrowing (g: GenericData) =
        let hasHpks = g.HPKs |> Array.isEmpty |> not
        let hasGpks = g.GPKs |> Array.isEmpty |> not
        let hasBrand = g.Brand |> String.notEmpty

        if hasHpks then
            { g with
                GPKs = [||]
                Brand = ""
                Form = ""
            }
        elif hasGpks then
            { g with
                Brand = ""
                Form = ""
            }
        elif hasBrand then
            { g with Form = "" }
        else
            g


    let parseDoseRuleData data =
        try
            data
            |> fun data ->
                let getColumn = data |> Array.head |> Csv.getStringColumn

                data
                |> Array.tail
                |> Array.distinctBy (fun row -> row |> Array.tail)
                |> Array.map (fun r ->
                    // `get` raises when the column is absent from the header row, so
                    // every column read through it is REQUIRED. The three readers
                    // below are the deliberate exceptions: they swallow that failure,
                    // so the columns they read may be missing from a sheet. Which
                    // reader a column uses is the whole of its optionality contract.
                    let get = getColumn r

                    let getIfNull col =
                        try
                            get col
                        with _ ->
                            ""

                    let getOpt col =
                        let s = getIfNull col
                        if s |> String.isNullOrWhiteSpace then None else s |> Some

                    let getInt = getIfNull >> Int32.tryParse
                    let toBrOpt = BigRational.toBrs >> Array.tryHead

                    // required column; "x" is the sheet convention, the rest are
                    // tolerated spellings
                    let getBool =
                        get
                        >> fun s ->
                            let v = s.Trim().ToLowerInvariant()
                            v = "true" || v = "x" || v = "1" || v = "yes"

                    {
                        RowId = getIfNull "RowId"
                        RuleId = getIfNull "RuleId"
                        GrpId = getIfNull "GrpId"
                        SortNo = getInt "SortNo" |> Option.defaultValue 1
                        Source = get "Source"
                        Generic =
                            {
                                Name = get "Generic"
                                Form = get "Form"
                                Brand = get "Brand"
                                GPKs =
                                    get "GPKs"
                                    |> String.splitAt ';'
                                    |> Array.map String.trim
                                    |> Array.filter String.notEmpty
                                    |> Array.distinct
                                HPKs =
                                    get "HPKs"
                                    |> String.splitAt ';'
                                    |> Array.map String.trim
                                    |> Array.filter String.notEmpty
                                    |> Array.distinct
                            }
                        Route = get "Route"
                        Indication = get "Indication"
                        SourceText = getIfNull "SourceText"
                        PatientText = getIfNull "PatientText"
                        Patient =
                            {
                                Location = getIfNull "Loc"
                                Dep = get "Dep"
                                IsAdult = getBool "IsAdult"
                                Gender = get "Gender" |> Gender.fromString
                                MinAge = get "MinAge" |> toBrOpt
                                MaxAge = get "MaxAge" |> toBrOpt
                                MinWeight = get "MinWeight" |> toBrOpt
                                MaxWeight = get "MaxWeight" |> toBrOpt
                                MinBSA = get "MinBSA" |> toBrOpt
                                MaxBSA = get "MaxBSA" |> toBrOpt
                                MinGestAge = get "MinGestAge" |> toBrOpt
                                MaxGestAge = get "MaxGestAge" |> toBrOpt
                                MinPMAge = get "MinPMAge" |> toBrOpt
                                MaxPMAge = get "MaxPMAge" |> toBrOpt
                            }
                        ScheduleText = getIfNull "ScheduleText"
                        ScheduleData =
                            {
                                DoseType = get "DoseType"
                                DoseText = get "DoseText"
                                Freqs = get "Freqs" |> BigRational.toBrs
                                AdjustUnit = get "AdjustUnit"
                                FreqUnit = get "FreqUnit"
                                RateUnit = get "RateUnit"
                                MinTime = get "MinTime" |> toBrOpt
                                MaxTime = get "MaxTime" |> toBrOpt
                                TimeUnit = get "TimeUnit"
                                MinInt = get "MinInt" |> toBrOpt
                                MaxInt = get "MaxInt" |> toBrOpt
                                IntUnit = get "IntUnit"
                                MinDur = get "MinDur" |> toBrOpt
                                MaxDur = get "MaxDur" |> toBrOpt
                                DurUnit = get "DurUnit"
                                DoseLimitData =
                                    {
                                        CmpBased = getBool "CmpBased"
                                        Component = get "Component"
                                        Substance = get "Substance"
                                        DoseUnit = get "DoseUnit"
                                        MinQty = get "MinQty" |> toBrOpt
                                        MaxQty = get "MaxQty" |> toBrOpt
                                        MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                                        MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                                        MinPerTime = get "MinPerTime" |> toBrOpt
                                        MaxPerTime = get "MaxPerTime" |> toBrOpt
                                        MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                                        MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                                        MinRate = get "MinRate" |> toBrOpt
                                        MaxRate = get "MaxRate" |> toBrOpt
                                        MinRateAdj = get "MinRateAdj" |> toBrOpt
                                        MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                                    }
                            }
                        Validated = getOpt "Validated"
                        FreqCheck = getOpt "FreqCheck"
                        DoseCheck = getOpt "DoseCheck"
                    }
                )
            // Keep one product-selection narrowing per row, by precedence
            // HPKs > GPKs > Brand > Form, then dedupe.
            |> Array.map (fun d -> { d with Generic = d.Generic |> withSingleNarrowing })
            |> Array.distinct
            |> Ok
        with exn ->
            Result.createError "getDataResult" exn


    /// <summary>
    /// Serialize <c>DoseRuleData</c> rows to TSV lines. Pure inverse of
    /// <c>parseDoseRuleData</c>: emits one tab-separated line per row in the exact
    /// column order of <c>headers</c>, prepended with the header line. BigRationals
    /// are written as doubles to match the original source number format.
    /// </summary>
    let dataToCsv (data: DoseRuleData[]) =
        let brsToString = Array.map (BigRational.toDouble >> string) >> String.concat ";"

        let brOptToString =
            Option.map (BigRational.toDouble >> string) >> Option.defaultValue ""

        // A cell must never contain the TSV delimiters. Free-text fields (and the
        // generated Check message, which is itself tab-formatted) can carry tabs and
        // newlines; collapse them to spaces so every row stays one line of cells.
        let clean (s: string) =
            if isNull s then
                ""
            else
                s
                |> String.replace "\t" " "
                |> String.replace "\r" " "
                |> String.replace "\n" " "

        data
        |> Array.toList
        |> List.map (fun d ->
            let g = d.Generic
            let p = d.Patient
            let s = d.ScheduleData
            let dl = s.DoseLimitData

            [
                d.RowId
                d.RuleId
                d.GrpId
                string d.SortNo
                d.Source
                g.Name
                g.Form
                g.Brand
                d.Route
                g.GPKs |> String.concat ";"
                g.HPKs |> String.concat ";"
                d.Indication
                d.SourceText
                d.PatientText
                d.ScheduleText
                p.Dep
                (if p.IsAdult then "x" else "")
                p.Gender |> Gender.toString
                p.MinAge |> brOptToString
                p.MaxAge |> brOptToString
                p.MinWeight |> brOptToString
                p.MaxWeight |> brOptToString
                p.MinBSA |> brOptToString
                p.MaxBSA |> brOptToString
                p.MinGestAge |> brOptToString
                p.MaxGestAge |> brOptToString
                p.MinPMAge |> brOptToString
                p.MaxPMAge |> brOptToString
                s.DoseType
                s.DoseText
                (if dl.CmpBased then "x" else "")
                dl.Component
                dl.Substance
                s.Freqs |> brsToString
                dl.DoseUnit
                s.AdjustUnit
                s.FreqUnit
                s.RateUnit
                s.MinTime |> brOptToString
                s.MaxTime |> brOptToString
                s.TimeUnit
                s.MinInt |> brOptToString
                s.MaxInt |> brOptToString
                s.IntUnit
                s.MinDur |> brOptToString
                s.MaxDur |> brOptToString
                s.DurUnit
                dl.MinQty |> brOptToString
                dl.MaxQty |> brOptToString
                dl.MinQtyAdj |> brOptToString
                dl.MaxQtyAdj |> brOptToString
                dl.MinPerTime |> brOptToString
                dl.MaxPerTime |> brOptToString
                dl.MinPerTimeAdj |> brOptToString
                dl.MaxPerTimeAdj |> brOptToString
                dl.MinRate |> brOptToString
                dl.MaxRate |> brOptToString
                dl.MinRateAdj |> brOptToString
                dl.MaxRateAdj |> brOptToString
                d.Validated |> Option.defaultValue ""
                d.FreqCheck |> Option.defaultValue ""
                d.DoseCheck |> Option.defaultValue ""
            ]
            |> List.map clean
            |> String.concat "\t"
        )
        |> List.append headers


    /// <summary>
    /// Canonical group-identity fields: the single definition of "rows that
    /// belong to the same dose-rule group" (they differ only in dose type/text
    /// and component/substance). Shared by <c>DoseRuleLoader.fromData</c>, which
    /// groups raw rows by this, and by <c>setDataHashIds</c>, which hashes it
    /// into <c>GrpId</c> — so the runtime grouping and the surfaced GroupId
    /// cannot drift when <c>GenericData</c>/<c>PatientCategoryData</c> gain a field.
    /// </summary>
    let groupKeyFields (dd: DoseRuleData) =
        let optBrToStr = Option.map BigRational.toString >> Option.defaultValue ""
        let pat = dd.Patient

        [
            dd.Source
            dd.Generic.Name
            dd.Generic.Form
            dd.Generic.Brand
            dd.Generic.GPKs |> String.concat ";"
            dd.Generic.HPKs |> String.concat ";"
            dd.Indication
            dd.Route
            pat.Location
            pat.Dep
            pat.IsAdult |> sprintf "%b"
            pat.Gender |> Gender.toString
            pat.MinAge |> optBrToStr
            pat.MaxAge |> optBrToStr
            pat.MinWeight |> optBrToStr
            pat.MaxWeight |> optBrToStr
            pat.MinBSA |> optBrToStr
            pat.MaxBSA |> optBrToStr
            pat.MinGestAge |> optBrToStr
            pat.MaxGestAge |> optBrToStr
            pat.MinPMAge |> optBrToStr
            pat.MaxPMAge |> optBrToStr
        ]


    let setDataHashIds (dd: DoseRuleData) =
        let sch = dd.ScheduleData
        let dos = dd.ScheduleData.DoseLimitData
        // identifies a group of rules that belong together and
        // only differ in dose type/dose text
        let groupFields = groupKeyFields dd
        // a rule within a rule group is only identified
        // by the dose type/dose text
        let ruleFields = groupFields @ [ sch.DoseType; sch.DoseText ]
        // rows should only have unique component/substance combinations
        // multiple rows with the same substance in the same component are
        // not allowed
        let rowFields = ruleFields @ [ dos.Component; dos.Substance ]

        { dd with
            GrpId = groupFields |> String.sha1Short // group of related rules where only the dose type differs
            RuleId = ruleFields |> String.sha1Short // identity of a rule (selection + dose type + dose text); rows sharing it form one rule
            RowId = rowFields |> String.sha1Short // complete row id: rule + component + substance (a substance is unique within a component)
        }


    let dataGroupKey (d: DoseRuleData) =
        {
            Source = d.Source
            Indication = d.Indication
            Generic = d.Generic
            Patient = d.Patient
            Route = d.Route
        }


    /// Determine whether a raw DoseRuleData row is valid.
    /// Can only determine validity up to schedule data as
    /// Component and Substance related data has to be aggregated
    /// into a single dose rule
    let validateData dd =
        let doseType, _ = DoseType.parse dd.ScheduleData.DoseType dd.ScheduleData.DoseText

        let warning = $"%s{dd.Generic.Name} | %s{dd.Route} |"

        match doseType with
        | NoDoseType -> [ "Has no dose type" ]
        | Once _ -> []
        | OnceTimed _ ->
            [
                if dd.ScheduleData.TimeUnit |> String.isNullOrWhiteSpace then
                    "TimeUnit is missing"
            ]
        | Discontinuous _ ->
            [
                if dd.ScheduleData.Freqs.Length = 0 then
                    "Frequencies is empty"
                if dd.ScheduleData.FreqUnit |> String.isNullOrWhiteSpace then
                    "FreqUnit is missing"
            ]
        | Timed _ ->
            [
                if dd.ScheduleData.Freqs.Length = 0 then
                    "Frequencies is empty"
                if dd.ScheduleData.FreqUnit |> String.isNullOrWhiteSpace then
                    "FreqUnit is missing"
                if dd.ScheduleData.TimeUnit |> String.isNullOrWhiteSpace then
                    "TimeUnit is missing"
            ]
        | Continuous _ ->

            [
                if dd.ScheduleData.RateUnit |> String.isNullOrWhiteSpace then
                    "RateUnit is missing"
            ]
        |> List.map (sprintf "%s %s" warning)


    /// <summary>
    /// Deduplicate DoseRuleData rows by RowId. Intended to run on the rows of a
    /// single rule (i.e. after <c>Array.groupBy _.RuleId</c>). Equal RowId means
    /// the same (component, substance) within the rule. Keeps the first
    /// occurrence; when the collapsed rows differ in their dose-limit values,
    /// a warning is returned because the "no duplicate substance within a
    /// component" invariant is violated and one value set is silently dropped.
    /// </summary>
    let dedupRowsByRowId (rows: DoseRuleData[]) : DoseRuleData[] * string list =
        let warns = ResizeArray<string>()

        let deduped =
            rows
            |> Array.groupBy _.RowId
            |> Array.map (fun (_, grp) ->
                // Component/Substance are equal across the group by construction
                // (both are part of RowId), so a non-singleton distinct here
                // means the dose-limit VALUES differ — the invariant is violated.
                let distinctLimits = grp |> Array.map _.ScheduleData.DoseLimitData |> Array.distinct

                if distinctLimits.Length > 1 then
                    let r = grp[0]
                    let d = r.ScheduleData.DoseLimitData

                    warns.Add
                        $"duplicate dose rule row for rule %s{r.RuleId} component '%s{d.Component}' substance '%s{d.Substance}': %i{distinctLimits.Length} differing dose-limit value sets — keeping first, dropping %i{distinctLimits.Length - 1}"

                grp[0]
            )

        deduped, warns |> List.ofSeq


    /// Pretty print dose rule data for  logging
    let doseRuleDataToString dd =
        let showOpt = Option.map string >> Option.defaultValue "-"

        let showStr s =
            if s |> String.isNullOrWhiteSpace then "-" else s

        let showArray toStr xs =
            if xs |> Array.isEmpty then
                "-"
            else
                xs |> Array.map toStr |> String.concat ","

        // Bind the deeply nested schedule and dose-limit fields to short locals
        // so the interpolated strings below stay readable (and Fantomas does not
        // wrap the long member-access chains into unreadable indentation).
        let sd = dd.ScheduleData
        let dl = sd.DoseLimitData

        let gen = dd.Generic.Name |> showStr
        let comp = dl.Component |> showStr
        let subst = dl.Substance |> showStr
        let gpks = dd.Generic.GPKs |> showArray id

        let route = dd.Route |> showStr
        let form = dd.Generic.Form |> showStr
        let brand = dd.Generic.Brand |> showStr
        let dept = dd.Patient.Dep |> showStr
        let ind = dd.Indication |> showStr

        let doseType = sd.DoseType |> showStr
        let doseText = sd.DoseText |> showStr
        let doseUnit = dl.DoseUnit |> showStr
        let adjUnit = sd.AdjustUnit |> showStr

        let freq = sd.Freqs |> showArray string
        let freqUnit = sd.FreqUnit |> showStr
        let maxTime = sd.MaxTime |> showOpt
        let timeUnit = sd.TimeUnit |> showStr
        let maxRate = dl.MaxRate |> showOpt
        let maxRateAdj = dl.MaxRateAdj |> showOpt
        let rateUnit = sd.RateUnit |> showStr

        let src = dd.Source |> showStr

        [
            $"Id   : Gen=%s{gen} | Comp=%s{comp} | Subst=%s{subst} | GPKs=%s{gpks}"
            $"Ctx  : Route=%s{route} | Form=%s{form} | Brand=%s{brand} | Dept=%s{dept} | Ind=%s{ind}"
            $"Dose : Type=%s{doseType} | Text=%s{doseText} | DoseUnit=%s{doseUnit} | AdjUnit=%s{adjUnit}"
            $"Rate : Freq=%s{freq} %s{freqUnit} | MaxTime=%s{maxTime} %s{timeUnit} | MaxRate=%s{maxRate} | MaxRateAdj=%s{maxRateAdj} %s{rateUnit}"
            $"Meta : Src=%s{src}"
        ]
        |> List.map String.trim
        |> List.filter String.notEmpty
        |> String.concat "\n"
