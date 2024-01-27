namespace Informedica.GenForm.Lib


module DoseRule =

    open System
    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Utils


    module DoseLimit =

        open Informedica.GenUnits.Lib

        /// An empty DoseLimit.
        let limit =
            {
                DoseLimitTarget = NoDoseLimitTarget
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


        /// Get the DoseLimitTarget as a string.
        let doseLimitTargetToString = function
            | NoDoseLimitTarget -> ""
            | ShapeDoseLimitTarget s
            | SubstanceDoseLimitTarget s -> s


        /// Get the substance from the SubstanceDoseLimitTarget.
        let substanceDoseLimitTargetToString = function
            | SubstanceDoseLimitTarget s -> s
            | _ -> ""


        /// Check whether the DoseLimitTarget is a SubstanceDoseLimitTarget.
        let isSubstanceLimit (dl : DoseLimit) =
            dl.DoseLimitTarget
            |> function
            | SubstanceDoseLimitTarget _ -> true
            | _ -> false


        /// Check whether the DoseLimitTarget is a SubstanceDoseLimitTarget.
        let isShapeLimit (dl : DoseLimit) =
            dl.DoseLimitTarget
            |> function
            | ShapeDoseLimitTarget _ -> true
            | _ -> false



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
                    dr.DoseLimits
                    |> Array.filter DoseLimit.isSubstanceLimit

            let shapeDls =
                dr.DoseLimits
                |> Array.filter DoseLimit.isShapeLimit

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
                    $"%s{dl.DoseLimitTarget |> DoseLimit.substanceDoseLimitTargetToString} {wrap}{s}{wrap}"
            )


        /// See for use of anonymous record in
        /// fold: https://github.com/dotnet/fsharp/issues/6699
        let toMarkdown (rules : DoseRule array) =
            let generic_md generic =
                $"\n\n# {generic}\n\n---\n"

            let route_md route products =
                $"\n\n### Route: {route}\n\n#### Producten\n%s{products}\n"

            let product_md product =  $"* {product}"

            let indication_md indication = $"\n\n## Indicatie: %s{indication}\n\n---\n"

            let doseCapt_md = "\n\n#### Doseringen\n\n"

            let dose_md dt dose freqs intv time dur =
                let dt = dt |> DoseType.toString
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

            let patient_md patient diagn =
                let patient =
                    if patient |> String.notEmpty then patient
                    else "alle patienten"
                if diagn |> String.isNullOrWhiteSpace then
                    $"\n\n##### Patient: **%s{patient}**\n\n"
                else
                    $"\n\n##### Patient: **%s{patient}**\n\n%s{diagn}"

            let printDoses (rules : DoseRule array) =
                (("", ""), rules |> Array.groupBy _.DoseType)
                ||> Array.fold (fun acc (dt, ds) ->
                    let dose =
                        if ds |> Array.isEmpty then ""
                        else
                            ds
                            |> Array.collect (printDose "")
                            |> Array.distinct
                            |> String.concat ", "
                            |> fun s -> $"{s}\n"

                    let freqs =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printFreqs
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    let pedForm =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map _.DoseText
                            |> Array.distinct
                            |> function
                            | [| s |] -> $"\n\n*Kinderformularium*: {s}"
                            | _ -> ""

                    let intv =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printInterval
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    let time =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printTime
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    let dur =
                        if dose = "" then ""
                        else
                            ds
                            |> Array.map printDuration
                            |> Array.distinct
                            |> function
                            | [| s |] -> s
                            | _ -> ""

                    if dt = Contraindicated then
                        $"{acc |> fst}\n*gecontra-indiceerd*", pedForm
                    else
                        $"{acc |> fst}\n{dose_md dt dose freqs intv time dur}", pedForm
                )
                |> fun (s1, s2) -> $"{s1}{s2}"


            ({| md = ""; rules = [||] |},
             rules
             |> Array.groupBy _.Generic
            )
            ||> Array.fold (fun acc (generic, rs) ->
                {| acc with
                    md =

                        generic_md generic
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
                                            |> Array.map (fun p -> product_md p.Label)
                                            |> Array.distinct
                                            |> String.concat "\n"
                                        {| acc with
                                            md = acc.md + (route_md route prods)
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
                                                    let diagn =
                                                        if pat.Diagnoses |> Array.isEmpty then ""
                                                        else
                                                            let s = pat.Diagnoses |> String.concat ", "
                                                            $"* Diagnose: **{s}**"
                                                    let pat = pat |> PatientCategory.toString

                                                    {| acc with
                                                        rules = rs
                                                        md =
                                                            acc.md +
                                                            (patient_md pat diagn) +
                                                            $"\n{doses}"
                                                    |}
                                                )
                                    )
                        )
            )
            |> (_.md)


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
            Products =
                if dr.Products
                   |> Array.exists (_.RequiresReconstitution)
                   |> not then dr.Products
                else
                    dr.Products
                    |> Array.choose (Product.reconstitute dr.Route dr.DoseType dep loc)

        }


    let fromTupleInclExcl = MinMax.fromTuple Inclusive Exclusive


    let fromTupleInclIncl = MinMax.fromTuple Inclusive Inclusive


    let mapToDoseRule (r : {| AdjustUnit: string; Brand : string; Department: string; Diagn: string; DoseText: string; DoseType: DoseType; DoseUnit: string; DurUnit: string; FreqUnit: string; Frequencies: BigRational array; Gender: Gender; Generic: string; Indication: string; IntervalUnit: string; MaxAge: BigRational option; MaxBSA: BigRational option; MaxDur: BigRational option; MaxGestAge: BigRational option; MaxInterval: BigRational option; MaxPMAge: BigRational option; MaxPerTime: BigRational option; MaxPerTimeAdj: BigRational option; MaxQty: BigRational option; MaxQtyAdj: BigRational option; MaxRate: BigRational option; MaxRateAdj: BigRational option; MaxTime: BigRational option; MaxWeight: BigRational option; MinAge: BigRational option; MinBSA: BigRational option; MinDur: BigRational option; MinGestAge: BigRational option; MinInterval: BigRational option; MinPMAge: BigRational option; MinPerTime: BigRational option; MinPerTimeAdj: BigRational option; MinQty: BigRational option; MinQtyAdj: BigRational option; MinRate: BigRational option; MinRateAdj: BigRational option; MinTime: BigRational option; MinWeight: BigRational option; NormPerTimeAdj: BigRational option; NormQtyAdj: BigRational option; Products : Product []; RateUnit: string; Route: string; Shape: string; Substance: string; TimeUnit: string; UseGenericName : string |}) =
        try
            {
                Indication = r.Indication
                Generic = r.Generic
                Shape = r.Shape
                Brand = r.Brand
                Route = r.Route
                DoseText = r.DoseText
                PatientCategory =
                    {
                        Department =
                            if r.Department |> String.isNullOrWhiteSpace then None
                            else
                                r.Department |> Some
                        Diagnoses = [| r.Diagn |] |> Array.filter String.notEmpty
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
                DoseType = r.DoseType
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
                DoseLimits = [||]
                Products = r.Products
            }
            |> Some
        with
        | e ->
            printfn $"{e}"
            printfn $"cannot map {r}"
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
            |> Array.map (fun r ->
                let get = getColumn r
                let toBrOpt = BigRational.toBrs >> Array.tryHead

                {|
                    Indication = get "Indication"
                    Generic = get "Generic"
                    Shape = get "Shape"
                    Brand = get "Brand"
                    Route = get "Route"
                    Department = get "Dep"
                    Diagn = get "Diagn"
                    DoseText =
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
                    Substance = get "Substance"
                    UseGenericName = get "UseGenericName"
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
                |}
            )


    let get_ dataUrl =
        let prods = Product.get ()

        dataUrl
        |> getData
        |> Array.filter (fun dr -> dr.DoseType |> String.notEmpty)
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
                    prods
                    |> Product.filter
                        { Filter.filter with
                            Generic = gen |> Some
                            Route = rte |> Some
                        }

                filtered
                |> fun xs ->
                    if xs |> Array.length = 0 then
                        printfn $"no products for {gen} {rte}"
                    xs
                |> Array.map (fun product ->
                    {| r with
                        Generic = gen
                        Shape = product.Shape |> String.toLower
                        Products =
                            filtered
                            |> Product.filter
                             { Filter.filter with
                                 Generic = gen |> Some
                                 Shape = product.Shape |> Some
                                 Route = rte |> Some
                             }
                    |}
                )
            )
        )
        |> Array.filter (fun dr -> dr.Shape |> String.notEmpty)
        |> Array.map (fun dr -> {| dr with DoseType = dr.DoseType |> DoseType.fromString  |})
        |> Array.groupBy mapToDoseRule
        |> Array.filter (fst >> Option.isSome)
        |> Array.map (fun (dr, rs) -> dr.Value, rs)
        |> Array.map (fun (dr, rs) ->
            { dr with
                DoseLimits =
                    let shapeLimits =
                         Mapping.filterRouteShapeUnit dr.Route dr.Shape NoUnit
                         |> Array.map (fun rsu ->
                            { DoseLimit.limit with
                                DoseLimitTarget = dr.Shape |> ShapeDoseLimitTarget
                                Quantity =
                                    {
                                        Min = rsu.MinDoseQty |> Option.map Limit.Inclusive
                                        Max = rsu.MaxDoseQty |> Option.map Limit.Inclusive
                                    }
                            }
                         )
                         |> Array.distinct

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
                                    dr.Shape |> ShapeDoseLimitTarget
                                else
                                    r.Substance |> SubstanceDoseLimitTarget
                            AdjustUnit = adj
                            DoseUnit = du |> Option.defaultValue NoUnit
                            Quantity =
                                (r.MinQty, r.MaxQty)
                                |> fromTupleInclIncl du
                            NormQuantityAdjust =
                                r.NormQtyAdj
                                |> ValueUnit.withOptionalUnit duAdj
                            QuantityAdjust =
                                (r.MinQtyAdj, r.MaxQtyAdj)
                                |> fromTupleInclIncl duAdj
                            PerTime =
                                (r.MinPerTime, r.MaxPerTime)
                                |> fromTupleInclIncl duTime
                            NormPerTimeAdjust =
                                r.NormPerTimeAdj
                                |> ValueUnit.withOptionalUnit duAdjTime
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
                    // check if there is only one dose limit and its
                    // a shape dose limit, then only use that shape
                    // dose limit
                    |> function
                        | [| dl  |] ->
                            if dl |> DoseLimit.isSubstanceLimit then
                                [| dl |]
                                |> Array.append shapeLimits
                            else [| dl |]
                        | dls -> dls |> Array.append shapeLimits
            }
        )


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
        |> Memoization.memoize


    /// <summary>
    /// Filter the DoseRules according to the Filter.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="drs">The DoseRule array</param>
    let filter (filter : Filter) (drs : DoseRule array) =
        // if the filter is 'empty' just return all
        if filter = Filter.filter then drs
        else
            let eqs a b =
                a
                |> Option.map (fun x -> x = b)
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
                    match filter.DoseType, dr.DoseType with
                    | AnyDoseType, _
                    | _, AnyDoseType -> true
                    | _ -> filter.DoseType = dr.DoseType
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


    /// Extract all the departments from the DoseRules.
    let departments = getMember (fun dr -> dr.PatientCategory.Department |> Option.defaultValue "")


    /// Extract all the diagnoses from the DoseRules.
    let diagnoses (drs : DoseRule []) =
        drs
        |> Array.collect (fun dr ->
            dr.PatientCategory.Diagnoses
        )
        |> Array.distinct
        |> Array.sortBy String.toLower


    /// Extract all genders from the DoseRules.
    let genders = getMember (fun dr -> dr.PatientCategory.Gender |> Gender.toString)


    /// Extract all patient categories from the DoseRules as strings.
    let patients (drs : DoseRule array) =
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
        dr.DoseLimits
        |> Array.filter DoseLimit.isSubstanceLimit
        |> Array.exists DoseLimit.useAdjust


