namespace Informedica.GenForm.Lib


module DoseLimit =


    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib


    /// An empty DoseLimit.
    let limit =
        {
            DoseLimitTarget = NoLimitTarget
            AdjustUnit = None
            DoseUnit = NoUnit
            Quantity = MinMax.empty
            NormQuantityAdjust = None
            QuantityAdjust = MinMax.empty
            PerTime = MinMax.empty
            NormPerTimeAdjust = None
            PerTimeAdjust = MinMax.empty
            Rate = MinMax.empty
            RateAdjust = MinMax.empty
        }


    /// <summary>
    /// Check whether an adjust is used in
    /// the DoseLimit.
    /// </summary>
    /// <remarks>
    /// If any of the adjust values is not None
    /// then an adjust is used.
    /// </remarks>
    let useAdjust (dl : DoseLimit) =
        [
            dl.NormQuantityAdjust = None
            dl.QuantityAdjust = MinMax.empty
            dl.NormPerTimeAdjust = None
            dl.PerTimeAdjust = MinMax.empty
            dl.RateAdjust = MinMax.empty
        ]
        |> List.forall id
        |> not


    let hasNoLimits (dl : DoseLimit) =
        { limit with
            DoseLimitTarget = dl.DoseLimitTarget
            AdjustUnit = dl.AdjustUnit
            DoseUnit = dl.DoseUnit
        } = dl


    let isSubstanceLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isSubstanceTarget


    let isComponentLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isComponentTarget


    let isShapeLimit (dl : DoseLimit) = dl.DoseLimitTarget |> LimitTarget.isShapeTarget


module DoseRule =

    open System
    open MathNet.Numerics

    open FSharp.Data
    open FSharp.Data.JsonExtensions

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineNoTime
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Utils


    module Print =


        open Informedica.GenUnits.Lib

        let printFreqs (r : DoseRule) =
            r.Frequencies
            |> Option.map (fun vu ->
                vu
                |> Utils.ValueUnit.toString 0
            )
            |> Option.defaultValue ""


        let printInterval (dr: DoseRule) =
            if dr.IntervalTime = MinMax.empty then ""
            else
                dr.IntervalTime
                |> MinMax.toString
                    "min. interval "
                    "min. interval "
                    "max. interval "
                    "max. interval "


        let printTime (dr: DoseRule) =
            if dr.AdministrationTime = MinMax.empty then ""
            else
                dr.AdministrationTime
                |> MinMax.toString
                    "min. "
                    "min. "
                    "max. "
                    "max. "


        let printDuration (dr: DoseRule) =
            if dr.Duration = MinMax.empty then ""
            else
                dr.Duration
                |> MinMax.toString
                    "min. duur "
                    "min. duur "
                    "max. duur "
                    "max. duur "


        let printMinMaxDose perDose (minMax : MinMax) =
            if minMax = MinMax.empty then ""
            else
                minMax
                |> MinMax.toString
                    "min "
                    "min "
                    "max "
                    "max "
                |> fun s ->
                    $"{s}{perDose}"

        let printNormDose perDose vu =
            match vu with
            | None    -> ""
            | Some vu ->
                $"{vu |> Utils.ValueUnit.toString 3}{perDose}"


        let printDose wrap (dr : DoseRule) =
            let substDls =
                    dr.ComponentLimits
                    |> Array.collect _.SubstanceLimits

            let shapeDls =
                dr.ComponentLimits
                |> Array.choose _.Limit

            let useSubstDl = substDls |> Array.length > 0
            // only use shape dose limits if there are no substance dose limits
            if useSubstDl then substDls
            else shapeDls
            |> Array.map (fun dl ->
                let perDose = "/dosis"
                let emptyS = ""
                [
                    $"{dl.Rate |> printMinMaxDose emptyS}"
                    $"{dl.RateAdjust |> printMinMaxDose emptyS}"

                    $"{dl.NormPerTimeAdjust |> printNormDose emptyS} " +
                    $"{dl.PerTimeAdjust |> printMinMaxDose emptyS}"

                    $"{dl.PerTime |> printMinMaxDose emptyS}"

                    $"{dl.NormQuantityAdjust |> printNormDose perDose} " +
                    $"{dl.QuantityAdjust |> printMinMaxDose perDose}"

                    $"{dl.Quantity |> printMinMaxDose perDose}"
                ]
                |> List.map String.trim
                |> List.filter (String.IsNullOrEmpty >> not)
                |> String.concat ", "
                |> fun s ->
                    $"%s{dl.DoseLimitTarget |> LimitTarget.toString} {wrap}{s}{wrap}"
            )


        // get all medications from Kinderformularium
        let kinderFormUrl = "https://www.kinderformularium.nl/geneesmiddelen.json"

        let private _medications () =
            let replace =
                [
                    "Ergocalciferol / fytomenadion / retinol / tocoferol (Vitamine A/D/E/K)",
                    "ergocalciferol-fytomenadion-retinol-tocoferol-vitamine-adek"

                    "Natriumdocusaat (al dan niet i.c.m. sorbitol)",
                    "natriumdocusaat-al-dan-niet-icm-sorbitol"
                ]
            let res = JsonValue.Load(kinderFormUrl)
            [ for v in res do
                {|
                    id = v?id.AsString()
                    generic = v?generic_name.AsString().Trim().ToLower()
                |}
            ]
            |> List.distinct


        let getKFMedications = Memoization.memoize _medications


        let getLink gen =
            getKFMedications ()
            |> List.tryFind (fun m ->
                m.generic
                |> String.split "+"
                |> List.map String.trim
                |> String.concat "/"
                |> String.equalsCapInsens gen
            )
            |> Option.map (fun m ->
                let gen = gen |> String.replace "/" "-"
                $"[Kinderformularium](https://www.kinderformularium.nl/geneesmiddel/{m.id}/{gen})"
            )


        /// See for use of anonymous record in
        /// fold: https://github.com/dotnet/fsharp/issues/6699
        let toMarkdown (rules : DoseRule array) =
            let generic_md generic =
                $"\n\n# {generic}\n\n---\n"

            let route_md route products synonyms =
                if synonyms |> String.isNullOrWhiteSpace then
                    $"\n\n### Route: {route}\n\n#### Producten\n%s{products}\n"
                else
                    $"\n\n### Route: {route}\n\n#### Producten\n%s{products}\n\n#### Synoniemen\n%s{synonyms}\n"

            let product_md product =  $"* {product}"

            let synonyms_md names =
                if names |> Seq.isEmpty then ""
                else
                    let names = names |> String.concat ", "
                    $"* {names}"

            let indication_md indication = $"\n\n## Indicatie: %s{indication}\n\n---\n"

            let doseCapt_md = "\n\n#### Doseringen\n\n"

            let dose_md dt dose freqs intv time dur =
                let dt = dt |> DoseType.toDescription
                let freqs =
                    if freqs |> String.isNullOrWhiteSpace then ""
                    else
                        $" in {freqs}"

                let s =
                    [
                        if intv |> String.isNullOrWhiteSpace |> not then
                            $" {intv}"
                        if time |> String.isNullOrWhiteSpace |> not then
                            $" inloop tijd {time}"
                        if dur |> String.isNullOrWhiteSpace |> not then
                            $" {dur}"
                    ]
                    |> String.concat ", "
                    |> fun s ->
                        if s |> String.isNullOrWhiteSpace then ""
                        else
                            $" ({s |> String.trim})"

                $"* *{dt}*: {dose}{freqs}{s}"

            let patient_md patient =
                let patient =
                    if patient |> String.notEmpty then patient
                    else "alle patienten"
                $"\n\n##### Patient: **%s{patient}**\n\n"

            let printDoses (rules : DoseRule array) =
                ("", rules |> Array.groupBy _.DoseType)
                ||> Array.fold (fun acc (dt, ds) ->
                    let pedForm =
                        let link =
                            ds
                            |> Array.tryHead
                            |> Option.bind (fun dr ->
                                dr.Generic |> getLink
                            )
                            |> Option.defaultValue "*Kinderformularium*"

                        ds
                        |> Array.map _.ScheduleText
                        |> Array.distinct
                        |> function
                        | [| s |] -> $"\n\n{link}: {s}"
                        | _ -> ""

                    ds
                    |> Array.fold (fun acc r ->
                        let dose =
                            r
                            |> printDose ""
                            |> Array.distinct
                            |> String.concat ", "

                        let freqs = r |> printFreqs
                        let intv = r |> printInterval
                        let time = r |> printTime
                        let dur = r |> printDuration

                        let md = dose_md dt dose freqs intv time dur
                        if acc |> String.containsCapsInsens md then acc // prevent duplicate doserule per shape print
                        else
                            $"{acc}\n{md}{pedForm}"

                    ) acc
                )

            ({| md = ""; rules = [||] |},
             rules
             |> Array.groupBy _.Generic
            )
            ||> Array.fold (fun acc (generic, rs) ->
                {| acc with
                    md = generic_md generic
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then r
                    else
                        (r, r.rules |> Array.groupBy _.Indication)
                        ||> Array.fold (fun acc (indication, rs) ->
                            {| acc with
                                md = acc.md + (indication_md indication)
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules = Array.empty then r
                                else
                                    (r, r.rules |> Array.groupBy _.Route)
                                    ||> Array.fold (fun acc (route, rs) ->
                                        let prods =
                                            rs
                                            |> Array.collect _.ComponentLimits
                                            |> Array.collect _.Products
                                            |> Array.sortBy (fun p ->
                                                p.Substances
                                                |> Array.sumBy (fun s ->
                                                    s.Concentration
                                                    |> Option.map ValueUnit.getValue
                                                    |> Option.bind Array.tryHead
                                                    |> Option.defaultValue 0N
                                                )
                                            )
                                            |> Array.map (fun p ->
                                                if p.GPK |> String.IsNullOrWhiteSpace then p.Label
                                                else $"{p.GPK} - {p.Label}"
                                                |> product_md
                                            )
                                            |> Array.distinct
                                            |> String.concat "\n"

                                        let synonyms =
                                            rs
                                            |> Array.collect _.ComponentLimits
                                            |> Array.collect _.Products
                                            |> Array.collect _.Synonyms
                                            |> Array.distinct
                                            |> synonyms_md

                                        {| acc with
                                            md = acc.md + (route_md route prods synonyms)
                                                        + doseCapt_md
                                            rules = rs
                                        |}
                                        |> fun r ->
                                            if r.rules = Array.empty then r
                                            else
                                                (r, r.rules
                                                    |> Array.sortBy (fun d -> d.PatientCategory |> PatientCategory.sortBy)
                                                    |> Array.groupBy _.PatientCategory)
                                                ||> Array.fold (fun acc (pat, rs) ->
                                                    let doses =
                                                        rs
                                                        |> Array.sortBy (fun r -> r.DoseType |> DoseType.sortBy)
                                                        |> printDoses
                                                    let pat = pat |> PatientCategory.toString

                                                    {| acc with
                                                        rules = rs
                                                        md =
                                                            acc.md +
                                                            (patient_md pat) +
                                                            $"\n{doses}"
                                                    |}
                                                )
                                    )
                        )
            )
            |> _.md


        let printGenerics generics (doseRules : DoseRule[]) =
            doseRules
            |> generics
            |> Array.sort
            |> Array.map(fun g ->
                doseRules
                |> Array.filter (fun dr -> dr.Generic = g)
                |> toMarkdown
            )


    open Informedica.GenUnits.Lib

    module GenPresProduct = Informedica.ZIndex.Lib.GenPresProduct
    module GenericProduct = Informedica.ZIndex.Lib.GenericProduct


    /// <summary>
    /// Reconstitute the products in a DoseRule that require reconstitution.
    /// </summary>
    /// <param name="dep">The Department to select the reconstitution</param>
    /// <param name="loc">The VenousAccess location to select the reconstitution</param>
    /// <param name="dr">The DoseRule</param>
    let reconstitute dep loc (dr : DoseRule) =
        { dr with
            ComponentLimits =
                dr.ComponentLimits
                |> Array.map (fun dl ->
                    { dl with
                        Products =
                            if dl.Products
                               |> Array.exists _.RequiresReconstitution
                               |> not then dl.Products
                            else
                                dl.Products
                                |> Array.choose (Product.reconstitute dr.Route dr.DoseType dep loc)
                    }
                )
        }


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let mapToDoseRule (r : DoseRuleDetails) =
        try
            {
                Source = r.Source
                Indication = r.Indication
                Generic = r.Generic
                Shape = r.Shape
                Brand =
                    if r.Brand |> String.isNullOrWhiteSpace then None
                    else r.Brand |> Some
                GPKs = r.GPKs
                Route = r.Route
                ScheduleText = r.ScheduleText
                PatientCategory =
                    {
                        Department =
                            if r.Department |> String.isNullOrWhiteSpace then None
                            else
                                r.Department |> Some
                        Gender = r.Gender
                        Age =
                            (r.MinAge, r.MaxAge)
                            |> fromTupleInclExcl (Some Utils.Units.day)
                        Weight =
                            (r.MinWeight, r.MaxWeight)
                            |> fromTupleInclExcl (Some Utils.Units.weightGram)
                        BSA =
                            (r.MinBSA, r.MaxBSA)
                            |> fromTupleInclExcl (Some Utils.Units.bsaM2)
                        GestAge =
                            (r.MinGestAge, r.MaxGestAge)
                            |> fromTupleInclExcl (Some Utils.Units.day)
                        PMAge =
                            (r.MinPMAge, r.MaxPMAge)
                            |> fromTupleInclExcl (Some Utils.Units.day)
                        Location = AnyAccess
                    }
                DoseType = r.DoseText |> DoseType.fromString r.DoseType
                AdjustUnit = r.AdjustUnit |> Units.adjustUnit
                Frequencies =
                    match r.FreqUnit |> Units.freqUnit with
                    | None -> None
                    | Some u ->
                        if r.Frequencies |> Array.isEmpty then None
                        else
                            r.Frequencies
                            |> ValueUnit.withUnit u
                            |> Some
                AdministrationTime =
                    (r.MinTime, r.MaxTime)
                    |> fromTupleInclIncl (r.TimeUnit |> Utils.Units.timeUnit)
                IntervalTime =
                    (r.MinInterval, r.MaxInterval)
                    |> fromTupleInclIncl (r.IntervalUnit |> Utils.Units.timeUnit)
                Duration =
                    (r.MinDur, r.MaxDur)
                    |> fromTupleInclIncl (r.DurUnit |> Utils.Units.timeUnit)
                ShapeLimit = None
                ComponentLimits = [||]
                RenalRule = None
            }
            |> Some
        with
        | e ->
            writeErrorMessage $"""
{e}
cannot map {r}
"""
            None


    let getData dataUrlId =
        Web.getDataFromSheet dataUrlId "DoseRules"
        |> fun data ->
            let getColumn =
                data
                |> Array.head
                |> Csv.getStringColumn

            data
            |> Array.tail
            |> Array.distinctBy (fun row -> row |> Array.tail)
            |> Array.map (fun r ->
                let get = getColumn r
                let toBrOpt = BigRational.toBrs >> Array.tryHead

                {
                    Source = get "Source"
                    Indication = get "Indication"
                    Generic = get "Generic"
                    Shape = get "Shape"
                    Brand = get "Brand"
                    GPKs =
                        get "GPKs"
                        |> String.splitAt ';'
                        |> Array.map String.trim
                        |> Array.filter String.notEmpty
                        |> Array.distinct
                    Route = get "Route"
                    Department = get "Dep"
                    ScheduleText =
                        try
                            get "ScheduleText"
                        with
                        | _ -> ""
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
                    DoseType = get "DoseType"
                    DoseText = get "DoseText"
                    Frequencies = get "Freqs" |> BigRational.toBrs
                    DoseUnit = get "DoseUnit"
                    AdjustUnit = get "AdjustUnit"
                    FreqUnit = get "FreqUnit"
                    RateUnit = get "RateUnit"
                    MinTime = get "MinTime" |> toBrOpt
                    MaxTime = get "MaxTime" |> toBrOpt
                    TimeUnit = get "TimeUnit"
                    MinInterval = get "MinInt" |> toBrOpt
                    MaxInterval = get "MaxInt" |> toBrOpt
                    IntervalUnit = get "IntUnit"
                    MinDur = get "MinDur" |> toBrOpt
                    MaxDur = get "MaxDur" |> toBrOpt
                    DurUnit = get "DurUnit"
                    Component = get "Component"
                    Substance = get "Substance"
                    MinQty = get "MinQty" |> toBrOpt
                    MaxQty = get "MaxQty" |> toBrOpt
                    NormQtyAdj = get "NormQtyAdj" |> toBrOpt
                    MinQtyAdj = get "MinQtyAdj" |> toBrOpt
                    MaxQtyAdj = get "MaxQtyAdj" |> toBrOpt
                    MinPerTime = get "MinPerTime" |> toBrOpt
                    MaxPerTime = get "MaxPerTime" |> toBrOpt
                    NormPerTimeAdj = get "NormPerTimeAdj" |> toBrOpt
                    MinPerTimeAdj = get "MinPerTimeAdj" |> toBrOpt
                    MaxPerTimeAdj = get "MaxPerTimeAdj" |> toBrOpt
                    MinRate = get "MinRate" |> toBrOpt
                    MaxRate = get "MaxRate" |> toBrOpt
                    MinRateAdj = get "MinRateAdj" |> toBrOpt
                    MaxRateAdj = get "MaxRateAdj" |> toBrOpt
                    Products = [||]
                }
            )
        |> Array.distinct


    let doseDetailsIsValid (dd: DoseRuleDetails) =
            dd.DoseType |> String.notEmpty &&
            (dd.Frequencies |> Array.length > 0 && dd.FreqUnit |> String.notEmpty ||
             dd.MaxQty |> Option.isSome ||
             dd.NormQtyAdj |> Option.isSome ||
             dd.MaxQtyAdj |> Option.isSome ||
             dd.MaxPerTime |> Option.isSome ||
             dd.NormPerTimeAdj |> Option.isSome ||
             dd.MaxPerTime |> Option.isSome ||
             dd.MaxRate |> Option.isSome ||
             dd.MaxRateAdj |> Option.isSome)


    let getDoseRuleDetails dataUrl =
        let prods = Product.get ()
        let warnings = System.Collections.Generic.Dictionary<_, _>()

        dataUrl
        |> getData
        |> Array.filter doseDetailsIsValid
        |> Array.groupBy (fun d ->
            match d.Shape, d.Brand with
            | s, _ when s |> String.notEmpty -> $"{d.Generic} ({d.Shape |> String.toLower})"
            | _, s when s |> String.notEmpty -> $"{d.Generic} ({d.Brand |> String.toLower |> String.capitalize})"
            | _ -> d.Generic
            ,
            d.Route
        )
        |> Array.collect (fun ((gen, rte), rs) ->
            rs
            |> Array.collect (fun r ->
                let filtered =
                    if r.GPKs |> Array.isEmpty then
                        prods
                        |> Product.filter
                            { Filter.doseFilter with
                                Generic = r.Component |> Some
                                Route = rte |> Some
                            }
                    else
                        prods
                        |> Array.filter (fun p -> r.GPKs |> Array.exists (String.equalsCapInsens p.GPK))

                if filtered |> Array.length = 0 then
                    let key = $"{gen} {rte}"
                    if warnings.ContainsKey(key) |> not then
                        warnings.Add(key, key)
                        writeWarningMessage $"no products for {key}"

                    [|
                        { r with
                            Products =
                                [|
                                    rs
                                    |> Array.map _.Substance
                                    |> Array.filter String.notEmpty
                                    |> Array.distinct
                                    |> Product.create gen rte
                                |]
                        }
                    |]
                else
                    filtered
                    |> Array.map (fun product ->
                        { r with
                            Generic = gen
                            Shape = product.Shape |> String.toLower
                            Products =
                                if r.GPKs |> Array.length > 0 then filtered
                                else
                                    filtered
                                    |> Product.filter
                                     { Filter.doseFilter with
                                         Generic = r.Component |> Some
                                         Shape = product.Shape |> Some
                                         Route = rte |> Some
                                     }
                        }
                    )
                    |> Array.distinct
            )
        )


    let getShapeLimits (prods : Product []) (dr : DoseRule) =
        let droplets =
            prods
            |> Array.filter (fun p ->
                p.Shape |> String.containsCapsInsens "druppel"
            )
            |> Array.choose _.Divisible
            |> Array.distinct
            |> Array.tryExactlyOne

        let setDroplet vu =
            let v, u = vu |> ValueUnit.get
            match droplets with
            | None -> vu
            | Some m ->
                u
                |> Units.Volume.dropletSetDropsPerMl m
                |> ValueUnit.withValue v

        if dr.Shape |> String.isNullOrWhiteSpace then [||]
        else
            Mapping.filterRouteShapeUnit dr.Route dr.Shape NoUnit
            |> Array.map (fun rsu ->
                { DoseLimit.limit with
                    DoseLimitTarget = dr.Shape |> ShapeLimitTarget
                    Quantity =
                        {
                            Min = rsu.MinDoseQty |> Option.map Limit.Inclusive
                            Max = rsu.MaxDoseQty |> Option.map Limit.Inclusive
                        }
                }
                |> fun dl ->
                    if droplets |> Option.isNone then dl
                    else
                        { dl with
                            DoseUnit =
                                droplets
                                |> Option.map Units.Volume.dropletWithDropsPerMl
                                |> Option.defaultValue rsu.DoseUnit
                            Quantity =
                                {
                                    Min =
                                        dl.Quantity.Min
                                        |> Option.map (
                                            Limit.apply
                                                setDroplet
                                                setDroplet
                                        )
                                    Max =
                                        dl.Quantity.Max
                                        |> Option.map (
                                            Limit.apply
                                                setDroplet
                                                setDroplet
                                        )
                                }

                        }
            )


    let getDoseLimits (rs : DoseRuleDetails []) =
        rs
        |> Array.map (fun r ->
            // the adjust unit
            let adj = r.AdjustUnit |> Utils.Units.adjustUnit
            // the dose unit
            let du = r.DoseUnit |> Units.fromString
            // the adjusted dose unit
            let duAdj =
                match adj, du with
                | Some adj, Some du ->
                    du
                    |> Units.per adj
                    |> Some
                | _ -> None
            // the time unit
            let tu = r.FreqUnit |> Utils.Units.timeUnit
            // the dose unit per time unit
            let duTime =
                match du, tu with
                | Some du, Some tu ->
                    du
                    |> Units.per tu
                    |> Some
                | _ -> None
            // the adjusted dose unit per time unit
            let duAdjTime =
                match duAdj, tu with
                | Some duAdj, Some tu ->
                    duAdj
                    |> Units.per tu
                    |> Some
                | _ -> None
            // the rate unit
            let ru = r.RateUnit |> Units.fromString
            // the dose unit per rate unit
            let duRate =
                match du, ru with
                | Some du, Some ru ->
                    du
                    |> Units.per ru
                    |> Some
                | _ -> None
            // the adjusted dose unit per rate unit
            let duAdjRate =
                match duAdj, ru with
                | Some duAdj, Some ru ->
                    duAdj
                    |> Units.per ru
                    |> Some
                | _ -> None

            {
                DoseLimitTarget =
                    if r.Substance |> String.isNullOrWhiteSpace then
                        r.Component |> ComponentLimitTarget
                    else
                        r.Substance |> SubstanceLimitTarget
                AdjustUnit = adj
                DoseUnit = du |> Option.defaultValue NoUnit
                Quantity =
                    (r.MinQty, r.MaxQty)
                    |> fromTupleInclIncl du
                NormQuantityAdjust =
                    r.NormQtyAdj
                    |> ValueUnit.withOptSingleAndOptUnit duAdj
                QuantityAdjust =
                    (r.MinQtyAdj, r.MaxQtyAdj)
                    |> fromTupleInclIncl duAdj
                PerTime =
                    (r.MinPerTime, r.MaxPerTime)
                    |> fromTupleInclIncl duTime
                NormPerTimeAdjust =
                    r.NormPerTimeAdj
                    |> ValueUnit.withOptSingleAndOptUnit duAdjTime
                PerTimeAdjust =
                    (r.MinPerTimeAdj, r.MaxPerTimeAdj)
                    |> fromTupleInclIncl duAdjTime
                Rate =
                    (r.MinRate, r.MaxRate)
                    |> fromTupleInclIncl duRate
                RateAdjust =
                    (r.MinRateAdj, r.MaxRateAdj)
                    |> fromTupleInclIncl duAdjRate
            }
        )


    let addDoseLimits (rs: DoseRuleDetails[]) (dr : DoseRule) =
        { dr with
            ShapeLimit =
                dr
                |> getShapeLimits (rs |> Array.collect _.Products)
                |> Array.tryExactlyOne

            ComponentLimits =
                rs
                |> Array.groupBy _.Component
                |> Array.map (fun (cmp , rs) ->
                    let lim =
                            rs
                            // if no substance the dose limit is a component limit
                            |> Array.filter (_.Substance >> String.isNullOrWhiteSpace)
                            |> getDoseLimits
                            |> Array.tryExactlyOne

                    {
                        Name = cmp
                        Limit = lim
                        Products =
                            rs
                            |> Array.collect _.Products
                            |> Array.filter (fun p ->
                                match lim with
                                | None -> true
                                | Some l ->
                                    l.DoseUnit
                                    |> ValueUnit.Group.eqsGroup p.ShapeUnit
                            )
                            |> Array.distinct
                        SubstanceLimits =
                            rs
                            // if a substance the limit is a substance limit
                            |> Array.filter (_.Substance >> String.isNullOrWhiteSpace >> not)
                            |> getDoseLimits
                    }
                )
        }


    let get_ dataUrl =
        dataUrl
        |> getDoseRuleDetails
        |> Array.groupBy mapToDoseRule
        |> Array.filter (fst >> Option.isSome)
        |> Array.map (fun (dr, rs) -> dr.Value, rs)


    /// <summary>
    /// Get the DoseRules from the Google Sheet.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : unit -> DoseRule [] =
        fun () ->
            Web.getDataUrlIdGenPres ()
            |> get_
            |> Array.map (fun (dr, rs) -> dr |> addDoseLimits rs)

        |> Memoization.memoize


    /// <summary>
    /// Filter the DoseRules according to the Filter.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="drs">The DoseRule array</param>
    let filter (filter : DoseFilter) (drs : DoseRule array) =
        // if the filter is 'empty' just return all
        if filter = Filter.doseFilter then drs
        else
            let eqs a b =
                a
                |> Option.map (String.equalsCapInsens b)
                |> Option.defaultValue true

            [|
                fun (dr : DoseRule) -> dr.Indication |> eqs filter.Indication
                fun (dr : DoseRule) -> dr.Generic |> eqs filter.Generic
                fun (dr : DoseRule) -> dr.Shape |> eqs filter.Shape
                fun (dr : DoseRule) -> filter.Route |> Option.isNone || dr.Route |> Mapping.eqsRoute filter.Route
                // don't filter on patients if patient is not set
                if filter.Patient = Patient.patient |> not then
                    fun (dr : DoseRule) -> dr.PatientCategory |> PatientCategory.filter filter
                fun (dr : DoseRule) ->
                    filter.DoseType
                    |> Option.map ((=) dr.DoseType)
                    |> Option.defaultValue true
            |]
            |> Array.fold (fun (acc : DoseRule[]) pred ->
                acc |> Array.filter pred
            ) drs


    let private getMember getter (drs : DoseRule[]) =
        drs
        |> Array.map getter
        |> Array.map String.trim
        |> Array.distinctBy String.toLower
        |> Array.sortBy String.toLower


    /// Extract all indications from the DoseRules.
    let indications = getMember _.Indication


    /// Extract all the generics from the DoseRules.
    let generics = getMember _.Generic


    /// Extract all the shapes from the DoseRules.
    let shapes = getMember _.Shape


    /// Extract all the routes from the DoseRules.
    let routes = getMember _.Route


    let doseTypes (dsrs : DoseRule []) =
        dsrs
        |> Array.map _.DoseType
        |> Array.distinct


    /// Extract all the departments from the DoseRules.
    let departments = getMember (fun dr -> dr.PatientCategory.Department |> Option.defaultValue "")


    /// Extract all genders from the DoseRules.
    let genders = getMember (fun dr -> dr.PatientCategory.Gender |> Gender.toString)


    /// Extract all patient categories from the DoseRules as strings.
    let patientCategories (drs : DoseRule array) =
        drs
        |> Array.map _.PatientCategory
        |> Array.sortBy PatientCategory.sortBy
        |> Array.map PatientCategory.toString
        |> Array.distinct


    /// Extract all frequencies from the DoseRules as strings.
    let frequencies (drs : DoseRule array) =
        drs
        |> Array.map Print.printFreqs
        |> Array.distinct


    let useAdjust (dr : DoseRule) =
        dr.ComponentLimits
        |> Array.collect _.SubstanceLimits
        |> Array.exists DoseLimit.useAdjust


    let getNormDose (dr : DoseRule) =
        dr.ComponentLimits
        |> Array.collect _.SubstanceLimits
        |> Array.collect (fun dl ->
            [|
                if dl.NormPerTimeAdjust |> Option.isSome then
                    (dl.DoseLimitTarget, dl.NormPerTimeAdjust.Value)
                    |> NormPerTimeAdjust
                    |> Some
                if dl.NormQuantityAdjust |> Option.isSome then
                    (dl.DoseLimitTarget, dl.NormQuantityAdjust.Value)
                    |> NormQuantityAdjust
                    |> Some
            |]
        )
        |> Array.choose id
        |> Array.tryHead


    module DataToCSV =


        let headers =
            [
                "SortNo"
                "Source"
                "Generic"
                "Shape"
                "Brand"
                "Route"
                "GPKs"
                "Indication"
                "ScheduleText"
                "Dep"
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
                "Component"
                "UseGenericName"
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
                "NormQtyAdj"
                "MinQtyAdj"
                "MaxQtyAdj"
                "MinPerTime"
                "MaxPerTime"
                "NormPerTimeAdj"
                "MinPerTimeAdj"
                "MaxPerTimeAdj"
                "MinRate"
                "MaxRate"
                "MinRateAdj"
                "MaxRateAdj"
            ]
            |> String.concat "\t"
            |> List.singleton


        let distinctByDoseLimit (d : DoseRuleDetails) =
                d.DoseType,
                d.Substance |> String.isNullOrWhiteSpace,
                d.AdjustUnit |> String.isNullOrWhiteSpace,
                d.MinQty.IsSome,
                d.MaxQty.IsSome,
                d.MinQtyAdj.IsSome,
                d.MaxQtyAdj.IsSome,
                d.NormQtyAdj.IsSome,
                d.MinPerTime.IsSome,
                d.MaxPerTime.IsSome,
                d.NormPerTimeAdj.IsSome,
                d.MinPerTimeAdj.IsSome,
                d.MaxPerTimeAdj.IsSome,
                d.MinRate.IsSome,
                d.MaxRate.IsSome,
                d.MinRateAdj.IsSome,
                d.MaxRateAdj.IsSome


        let dataToCsv distBy dataUrlId =
            let grouped =
                dataUrlId
                |> getDoseRuleDetails
                |> Array.groupBy mapToDoseRule
                |> Array.filter (fst >> Option.isSome)
                |> Array.map (fun (dr, details) -> dr.Value, details)

            let distinct =
                grouped
                |> Array.collect snd
                |> Array.filter (_.Products >> Array.isEmpty >> not)
                |> Array.filter (_.Shape >> String.notEmpty)
                |> Array.distinctBy distBy

            grouped
            |> Array.filter (snd >> Array.exists(fun d -> distinct |> Array.exists ((=) d)))
            |> Array.collect snd
            |> Array.toList
            |> List.mapi (fun i d ->
                let bigRatToStringList =
                    Array.map BigRational.toDouble
                    >> Array.map string
                    >> String.concat ";"

                let bigRatOptToString =
                    Option.map (BigRational.toDouble >> string)
                    >> Option.defaultValue ""
                [
                    $"{i}"
                    d.Source
                    d.Generic
                    d.Shape
                    d.Brand
                    d.Route
                    (d.GPKs |> String.concat ";")
                    d.Indication
                    d.ScheduleText
                    d.Department
                    (d.Gender |> Gender.toString)
                    (d.MinAge |> bigRatOptToString)
                    (d.MaxAge |> bigRatOptToString)
                    (d.MinWeight |> bigRatOptToString)
                    (d.MaxWeight |> bigRatOptToString)
                    (d.MinBSA |> bigRatOptToString)
                    (d.MaxBSA |> bigRatOptToString)
                    (d.MinGestAge |> bigRatOptToString)
                    (d.MaxGestAge |> bigRatOptToString)
                    (d.MinPMAge |> bigRatOptToString)
                    (d.MaxPMAge |> bigRatOptToString)
                    d.DoseType
                    d.DoseText
                    d.Component
                    ""
                    d.Substance
                    (d.Frequencies |> bigRatToStringList)
                    d.DoseUnit
                    d.AdjustUnit
                    d.FreqUnit
                    d.RateUnit
                    (d.MinTime |> bigRatOptToString)
                    (d.MaxTime |> bigRatOptToString)
                    d.TimeUnit
                    "" //d.MinInt
                    "" //d.MaxInt
                    "" //d.IntUnit
                    (d.MinDur |> bigRatOptToString)
                    (d.MaxDur |> bigRatOptToString)
                    d.DurUnit
                    (d.MinQty |> bigRatOptToString)
                    (d.MaxQty |> bigRatOptToString)
                    (d.NormQtyAdj |> bigRatOptToString)
                    (d.MinQtyAdj |> bigRatOptToString)
                    (d.MaxQtyAdj |> bigRatOptToString)
                    (d.MinPerTime |> bigRatOptToString)
                    (d.MaxPerTime |> bigRatOptToString)
                    (d.NormPerTimeAdj |> bigRatOptToString)
                    (d.MinPerTimeAdj |> bigRatOptToString)
                    (d.MaxPerTimeAdj |> bigRatOptToString)
                    (d.MinRate |> bigRatOptToString)
                    (d.MaxRate |> bigRatOptToString)
                    (d.MinRateAdj |> bigRatOptToString)
                    (d.MaxRateAdj |> bigRatOptToString)

                ]
                |> String.concat "\t"
            )
            |> List.append headers