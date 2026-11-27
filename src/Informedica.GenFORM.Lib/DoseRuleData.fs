namespace Informedica.GenForm.Lib


module DoseRuleData =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open Utils


    /// <summary>
    /// The header line of the DoseRules sheet, with the columns in the order that <c>dataToCsv</c> writes them.
    /// </summary>
    // TODO: parseDoseRuleData reads the Loc column, but Loc is missing here, so dataToCsv drops the location.
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


    /// Keeps one product restriction of a generic, in the order HPKs, GPKs, Brand, Form, and clears the
    /// fields after it. A generic without a restriction is returned unchanged.
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
                    // get raises an exception when the sheet has no such column, so a column read with get is
                    // required. A column read with getIfNull, getOpt or getInt may be missing from the sheet.
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

                    // A required column. The sheet uses "x" for true; "true", "1" and "yes" are also accepted.
                    let getBool =
                        get
                        >> fun s ->
                            let v = s.Trim().ToLowerInvariant()
                            v = "true" || v = "x" || v = "1" || v = "yes"

                    {
                        RowId = getIfNull "RowId" |> HashId.create
                        RuleId = getIfNull "RuleId" |> HashId.create
                        GrpId = getIfNull "GrpId" |> HashId.create
                        SortNo = getInt "SortNo" |> Option.defaultValue 1
                        Source = get "Source"
                        Generic =
                            {
                                Name = get "Generic"
                                Form = get "Form"
                                Brand = get "Brand"
                                GPKs = get "GPKs" |> ProductId.parseCodes
                                HPKs = get "HPKs" |> ProductId.parseCodes
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
            // Keeps one product restriction per row, then removes duplicate rows.
            |> Array.map (fun d -> { d with Generic = d.Generic |> withSingleNarrowing })
            |> Array.distinct
            |> Ok
        with exn ->
            Result.createError "getDataResult" exn


    /// <summary>
    /// Writes dose rule rows as tab-separated lines in the column order of <c>headers</c>, after the header line.
    /// Numbers are written as doubles, as in the sheet.
    /// </summary>
    let dataToCsv (data: DoseRuleData[]) =
        let brsToString = Array.map (BigRational.toDouble >> string) >> String.concat ";"

        let brOptToString = Option.map (BigRational.toDouble >> string) >> Option.defaultValue ""

        data
        |> Array.toList
        |> List.map (fun d ->
            let g = d.Generic
            let p = d.Patient
            let s = d.ScheduleData
            let dl = s.DoseLimitData

            [
                d.RowId |> HashId.toString
                d.RuleId |> HashId.toString
                d.GrpId |> HashId.toString
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
            |> List.map Tsv.cell
            |> String.concat "\t"
        )
        |> List.append headers


    /// <summary>
    /// Returns the fields that identify a dose rule group: rows in one group differ only in dose type, dose text,
    /// component and substance. The loader groups rows by these fields and <c>setDataHashIds</c> hashes them into
    /// the GrpId, so both use the same definition.
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
        // The rules of one group differ only in dose type and dose text.
        let groupFields = groupKeyFields dd
        // Within a group, the dose type and dose text identify a rule.
        let ruleFields = groupFields @ [ sch.DoseType; sch.DoseText ]
        // Within a rule, the component and substance identify a row. A substance occurs once per component.
        let rowFields = ruleFields @ [ dos.Component; dos.Substance ]

        { dd with
            GrpId = groupFields |> HashId.ofFields
            RuleId = ruleFields |> HashId.ofFields
            RowId = rowFields |> HashId.ofFields
        }


    let dataGroupKey (d: DoseRuleData) =
        {
            Source = d.Source
            Indication = d.Indication
            Generic = d.Generic
            Patient = d.Patient
            Route = d.Route
        }


    /// Returns the warnings for a dose rule row with a missing dose type or a missing schedule unit or frequency.
    /// Checks only the schedule; the component and substance data are checked once the rows form a dose rule.
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
    /// Removes rows of one dose rule with the same RowId, that is the same component and substance, and keeps the
    /// first row. Returns a warning for each RowId whose rows have different dose limits.
    /// </summary>
    let dedupRowsByRowId (rows: DoseRuleData[]) : DoseRuleData[] * string list =
        let warns = ResizeArray<string>()

        let deduped =
            rows
            |> Array.groupBy _.RowId
            |> Array.map (fun (_, grp) ->
                // The rows share the component and substance, so more than one distinct limit means
                // the dose limits differ.
                let distinctLimits = grp |> Array.map _.ScheduleData.DoseLimitData |> Array.distinct

                if distinctLimits.Length > 1 then
                    let r = grp[0]
                    let d = r.ScheduleData.DoseLimitData

                    warns.Add
                        $"duplicate dose rule row for rule %s{r.RuleId |> HashId.toString} component '%s{d.Component}' substance '%s{d.Substance}': %i{distinctLimits.Length} differing dose-limit value sets — keeping first, dropping %i{distinctLimits.Length - 1}"

                grp[0]
            )

        deduped, warns |> List.ofSeq


    /// The adjust unit of a row: kg, m2, or none.
    let adjustUnitOf (d: DoseRuleData) = d.ScheduleData.AdjustUnit |> Units.adjustUnit


    /// Checks whether a row has a value in one of the given dose limit columns.
    let hasValueIn columns (d: DoseRuleData) =
        d.ScheduleData.DoseLimitData |> columns |> List.exists Option.isSome


    /// Returns the per kg and per m2 columns, which use the adjust unit.
    let adjustUnitColumns (l: DoseLimitData) =
        [
            l.MinQtyAdj
            l.MaxQtyAdj
            l.MinPerTimeAdj
            l.MaxPerTimeAdj
            l.MinRateAdj
            l.MaxRateAdj
        ]


    /// Returns all dose limit columns, which all use the dose unit.
    let doseUnitColumns (l: DoseLimitData) =
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


    /// Returns the per time columns, which use the frequency unit.
    let freqUnitColumns (l: DoseLimitData) = [ l.MinPerTime; l.MaxPerTime; l.MinPerTimeAdj; l.MaxPerTimeAdj ]


    /// Returns the rate columns, which use the rate unit.
    let rateUnitColumns (l: DoseLimitData) = [ l.MinRate; l.MaxRate; l.MinRateAdj; l.MaxRateAdj ]


    /// Checks whether a row has a value in a per kg or per m2 column.
    let hasAdjustedValue (d: DoseRuleData) = d |> hasValueIn adjustUnitColumns


    /// Describes the dose rule of a row by generic, route, indication, source, patient and dose type,
    /// so a sheet author can find it.
    let describe (d: DoseRuleData) =
        $"the dose rule %s{d.Generic.Name} %s{d.Route} %s{d.Indication} (%s{d.Source}, %s{d.PatientText}, %s{d.ScheduleData.DoseType} %s{d.ScheduleData.DoseText})"


    /// Returns a warning for each dose rule with a row that has IsAdult and an age, with the age in days; the rule
    /// gets the age range.
    let adultRowsWithAge (data: DoseRuleData[]) =
        let days = Option.map (fun d -> $"%s{d |> BigRational.toString} days")

        data
        |> Array.filter (fun d -> d.Patient.IsAdult && d.Patient |> DoseRule.marksAdults |> not)
        |> Array.distinctBy _.RuleId
        |> Array.map (fun d ->
            let age =
                [
                    d.Patient.MinAge |> days |> Option.map (fun a -> $"the minimum age %s{a}")
                    d.Patient.MaxAge |> days |> Option.map (fun a -> $"the maximum age %s{a}")
                ]
                |> List.choose id
                |> String.concat " and "

            $"%s{d |> describe} has IsAdult and %s{age}; the age range is used"
        )


    /// Returns the substance of a row, or the component when the row has no substance.
    let rowName (l: DoseLimitData) =
        if l.Substance |> String.isNullOrWhiteSpace then
            l.Component
        else
            l.Substance


    /// Returns a reason for each row with a limit value but no dose unit, a per time value but no frequency unit,
    /// or a rate value but no rate unit. Such a value would be lost. An unknown unit text still gives a general
    /// unit, so only an empty unit loses the value.
    let missingUnitRefusals (rows: DoseRuleData[]) =
        [
            for r in rows do
                let name = r.ScheduleData.DoseLimitData |> rowName

                if
                    r |> hasValueIn doseUnitColumns
                    && r.ScheduleData.DoseLimitData.DoseUnit |> UnitsParse.fromString |> Option.isNone
                then
                    $"the row for %s{name} has a value without a dose unit"

                if
                    r |> hasValueIn freqUnitColumns
                    && r.ScheduleData.FreqUnit |> Units.timeUnit |> Option.isNone
                then
                    $"the row for %s{name} has a per time value without a frequency unit"

                if
                    r |> hasValueIn rateUnitColumns
                    && r.ScheduleData.RateUnit |> UnitsParse.fromString |> Option.isNone
                then
                    $"the row for %s{name} has a rate value without a rate unit"
        ]


    /// Returns the reasons to refuse a dose rule, given as its rows: rows with different adjust units (an empty
    /// unit counts as one), a per kg or per m2 value without an adjust unit, or a value without its dose,
    /// frequency or rate unit. The rule takes its adjust unit from its first row, so mixed adjust units can give
    /// a wrong dose. A value without its unit is lost, so the rule could lose a maximum.
    let unitRefusals (rows: DoseRuleData[]) =
        [
            let units = rows |> Array.map adjustUnitOf |> Array.distinct

            if units |> Array.length > 1 then
                let names =
                    units
                    |> Array.map (DoseRule.adjStr >> fun s -> if s = "" then "none" else s)
                    |> String.concat ", "

                $"its rows have different adjust units (%s{names}); give every row of the rule the rule's adjust unit"

            for r in
                rows
                |> Array.filter (fun r -> r |> hasAdjustedValue && r |> adjustUnitOf |> Option.isNone) do
                let name = r.ScheduleData.DoseLimitData |> rowName
                $"the row for %s{name} has a per kg or per m2 value without an adjust unit"

            yield! rows |> missingUnitRefusals
        ]


    /// Returns a warning for a dose rule with one adjust unit and dose limit values, but no per kg or per m2
    /// value in any row. Returns None for a rule without dose limit values.
    let unitWarning (rows: DoseRuleData[]) =
        match rows |> Array.map adjustUnitOf |> Array.distinct with
        | [| Some _ as u |] when
            rows |> Array.exists (hasValueIn doseUnitColumns)
            && rows |> Array.exists hasAdjustedValue |> not
            ->
            let u = u |> DoseRule.adjStr

            $"%s{rows |> Array.head |> describe} has the adjust unit %s{u} but no per %s{u} value"
            |> Some
        | _ -> None


    /// Returns the rows of the dose rules that pass the unit checks, with a message for each refused rule and
    /// each rule with an adjust unit but no per kg or per m2 value. The rows of a rule share a RuleId.
    let checkUnits (data: DoseRuleData[]) =
        let data = data |> Array.map setDataHashIds
        let rules = data |> Array.groupBy _.RuleId

        let refused =
            rules
            |> Array.choose (fun (id, rows) ->
                match rows |> unitRefusals with
                | [] -> None
                | reasons ->
                    let reasons = reasons |> String.concat "; "
                    Some(id, $"%s{rows |> Array.head |> describe} is left out: %s{reasons}")
            )

        let ids = refused |> Array.map fst |> Set.ofArray

        data |> Array.filter (_.RuleId >> ids.Contains >> not),
        [|
            yield! refused |> Array.map snd
            yield!
                rules
                |> Array.filter (fst >> ids.Contains >> not)
                |> Array.choose (snd >> unitWarning)
        |]


    /// Formats a dose rule row as text for the log.
    let doseRuleDataToString dd =
        let showOpt = Option.map string >> Option.defaultValue "-"

        let showStr s = if s |> String.isNullOrWhiteSpace then "-" else s

        let showArray toStr xs =
            if xs |> Array.isEmpty then
                "-"
            else
                xs |> Array.map toStr |> String.concat ","

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
