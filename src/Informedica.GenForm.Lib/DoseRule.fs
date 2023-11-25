namespace Informedica.GenForm.Lib



module DoseRule =

    open System
    open MathNet.Numerics
    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL



    module DoseLimit =


        /// An empty DoseLimit.
        let limit =
            {
                DoseLimitTarget = NoDoseLimitTarget
                DoseUnit = ""
                RateUnit = ""
                Quantity = MinMax.none
                NormQuantityAdjust = None
                QuantityAdjust = MinMax.none
                PerTime = MinMax.none
                NormPerTimeAdjust = None
                PerTimeAdjust = MinMax.none
                Rate = MinMax.none
                RateAdjust = MinMax.none
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
                dl.QuantityAdjust = MinMax.none
                dl.NormPerTimeAdjust = None
                dl.PerTimeAdjust = MinMax.none
                dl.RateAdjust = MinMax.none
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


        let printFreqs (r : DoseRule) =
                let frs =
                    r.Frequencies
                    |> Array.map BigRational.ToInt32
                    |> Array.map string
                    |> String.concat ", "

                if frs |> String.isNullOrWhiteSpace then ""
                else
                    if r.FreqTimeUnit |> String.isNullOrWhiteSpace then $"{frs} x"
                    else
                        $"{frs} x / {r.FreqTimeUnit}"


        let printInterval (dr: DoseRule) =
            if dr.IntervalTimeUnit |> String.isNullOrWhiteSpace then ""
            else
                let s = dr.IntervalTime |> MinMax.toString
                if s |> String.isNullOrWhiteSpace then ""
                else
                    let s =
                        s
                        |> String.replace "≥" "min. interval "
                        |> String.replace "<" "max. interval"
                        |> fun s ->
                            if s |> String.contains "-" then $"elke {s}"
                            else s
                    $"{s} {dr.IntervalTimeUnit}"


        let printTime (dr: DoseRule) =
            if dr.AdministrationTimeUnit |> String.isNullOrWhiteSpace then ""
            else
                let s = dr.AdministrationTime |> MinMax.toString
                if s |> String.isNullOrWhiteSpace then ""
                else
                    $"{s} {dr.AdministrationTimeUnit}"
                    |> String.replace "<" "max"


        let printDuration (dr: DoseRule) =
            if dr.DurationUnit |> String.isNullOrWhiteSpace then ""
            else
                let s = dr.Duration |> MinMax.toString
                if s |> String.isNullOrWhiteSpace then ""
                else
                    $"{s} {dr.DurationUnit}"
                    |> String.replace "<" "max"


        let printMinMaxDose u (minMax : MinMax) =
            let s = minMax |> MinMax.toString
            if s |> String.isNullOrWhiteSpace then ""
            else
                $"{s} {u}"
                |> String.replace "<" "max"


        let printNormDose u br =
            match br with
            | None    -> ""
            | Some br -> $"{br |> BigRational.toStringNl} {u}"


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
                let doseQtyAdjUnit = $"{dl.DoseUnit}/{dr.AdjustUnit}/keer"
                let doseTotAdjUnit = $"{dl.DoseUnit}/{dr.AdjustUnit}/{dr.FreqTimeUnit}"
                let doseTotUnit = $"{dl.DoseUnit}/{dr.FreqTimeUnit}"
                let doseQtyUnit = $"{dl.DoseUnit}/keer"
                let doseRateUnit = $"{dl.DoseUnit}/{dl.RateUnit}"
                let doseRateAdjUnit = $"{dl.DoseUnit}/{dr.AdjustUnit}/{dl.RateUnit}"
                [
                    $"{dl.Rate |> printMinMaxDose doseRateUnit}"
                    $"{dl.RateAdjust |> printMinMaxDose doseRateAdjUnit}"

                    $"{dl.NormPerTimeAdjust |> printNormDose doseTotAdjUnit} " +
                    $"{dl.PerTimeAdjust |> printMinMaxDose doseTotAdjUnit}"

                    $"{dl.PerTime |> printMinMaxDose doseTotUnit}"

                    $"{dl.NormQuantityAdjust |> printNormDose doseQtyAdjUnit} " +
                    $"{dl.QuantityAdjust |> printMinMaxDose doseQtyAdjUnit}"

                    $"{dl.Quantity |> printMinMaxDose doseQtyUnit}"
                ]
                |> List.map String.trim
                |> List.filter (String.IsNullOrEmpty >> not)
                |> String.concat " "
                |> fun s ->
                    $"%s{dl.DoseLimitTarget |> DoseLimit.substanceDoseLimitTargetToString} {wrap}{s}{wrap}"
            )


        /// See for use of anonymous record in
        /// fold: https://github.com/dotnet/fsharp/issues/6699
        let toMarkdown (rules : DoseRule array) =
            let generic_md generic =
                $"\n# {generic}\n---\n"

            let route_md route products =
                $"\n### Route: {route}\n\n#### Producten\n%s{products}\n"

            let product_md product =  $"* {product}"

            let indication_md indication = $"\n## Indicatie: %s{indication}\n---\n"

            let doseCapt_md = "\n#### Doseringen\n"

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
                if diagn |> String.isNullOrWhiteSpace then
                    $"\n##### Patient: **%s{patient}**\n"
                else
                    $"\n##### Patient: **%s{patient}**\n\n%s{diagn}"

            let printDoses (rules : DoseRule array) =
                ("", rules |> Array.groupBy (fun d -> d.DoseType))
                ||> Array.fold (fun acc (dt, ds) ->
                    let dose =
                        if ds |> Array.isEmpty then ""
                        else
                            ds
                            |> Array.collect (printDose "")
                            |> Array.distinct
                            |> String.concat " "
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

                    if dt = Contraindicated then $"{acc}\n*gecontra-indiceerd*"
                    else
                        $"{acc}\n{dose_md dt dose freqs intv time dur}"
                )

            ({| md = ""; rules = [||] |},
             rules
             |> Array.groupBy (fun d -> d.Generic)
            )
            ||> Array.fold (fun acc (generic, rs) ->
                {| acc with
                    md = generic_md generic
                    rules = rs
                |}
                |> fun r ->
                    if r.rules = Array.empty then r
                    else
                        (r, r.rules |> Array.groupBy (fun d -> d.Indication))
                        ||> Array.fold (fun acc (indication, rs) ->
                            {| acc with
                                md = acc.md + (indication_md indication)
                                rules = rs
                            |}
                            |> fun r ->
                                if r.rules = Array.empty then r
                                else
                                    (r, r.rules |> Array.groupBy (fun r -> r.Route))
                                    ||> Array.fold (fun acc (route, rs) ->

                                        let prods =
                                            rs
                                            |> Array.collect (fun d -> d.Products)
                                            |> Array.sortBy (fun p ->
                                                p.Substances
                                                |> Array.sumBy (fun s -> s.Quantity |> Option.defaultValue 0N)
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
                                                    |> Array.groupBy (fun d -> d.PatientCategory))
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
                                                        md = acc.md + (patient_md pat diagn) + $"\n{doses}"
                                                    |}
                                                )
                                    )
                        )
            )
            |> fun r -> r.md


        let printGenerics generics (doseRules : DoseRule[]) =
            doseRules
            |> generics
            |> Array.sort
            |> Array.map(fun g ->
                doseRules
                |> Array.filter (fun dr -> dr.Generic = g)
                |> toMarkdown
            )


    open Utils


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
                   |> Array.exists (fun p -> p.RequiresReconstitution)
                   |> not then dr.Products
                else
                    dr.Products
                    |> Array.choose (Product.reconstitute dr.Route dr.DoseType dep loc)

        }


    let private get_ () =
        Web.getDataFromSheet Web.dataUrlIdGenPres "DoseRules"
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
                    Route = get "Route"
                    Department = get "Dep"
                    Diagn = get "Diagn"
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
                    DoseType = get "DoseType" |> DoseType.fromString
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
            |> Array.groupBy (fun r ->
                {
                    Indication = r.Indication
                    Generic = r.Generic
                    Shape = r.Shape
                    Route = r.Route
                    PatientCategory =
                        {
                            Department =
                                if r.Department |> String.isNullOrWhiteSpace then None
                                else
                                    r.Department |> Some
                            Diagnoses = [| r.Diagn |] |> Array.filter String.notEmpty
                            Gender = r.Gender
                            Age = (r.MinAge, r.MaxAge) |> MinMax.fromTuple
                            Weight = (r.MinWeight, r.MaxWeight) |> MinMax.fromTuple
                            BSA = (r.MinBSA, r.MaxBSA) |> MinMax.fromTuple
                            GestAge = (r.MinGestAge, r.MaxGestAge) |> MinMax.fromTuple
                            PMAge = (r.MinPMAge, r.MaxPMAge) |> MinMax.fromTuple
                            Location = AnyAccess
                        }
                    AdjustUnit = r.AdjustUnit
                    DoseType = r.DoseType
                    Frequencies = r.Frequencies
                    FreqTimeUnit = r.FreqUnit
                    AdministrationTime = (r.MinTime, r.MaxTime) |> MinMax.fromTuple
                    AdministrationTimeUnit = r.TimeUnit
                    IntervalTime = (r.MinInterval, r.MaxInterval) |> MinMax.fromTuple
                    IntervalTimeUnit = r.IntervalUnit
                    Duration = (r.MinDur, r.MaxDur) |> MinMax.fromTuple
                    DurationUnit = r.DurUnit
                    DoseLimits = [||]
                    Products = [||]
                }
            )
            |> Array.map (fun (dr, rs) ->
                { dr with
                    DoseLimits =
                        let shapeLimits =
                             Mapping.filterRouteShapeUnit dr.Route dr.Shape ""
                             |> Array.map (fun rsu ->
                                { DoseLimit.limit with
                                    DoseLimitTarget = dr.Shape |> ShapeDoseLimitTarget
                                    DoseUnit = rsu.DoseUnit
                                    Quantity =
                                        let min = rsu.MinDoseQty |> Option.bind BigRational.fromFloat
                                        let max = rsu.MaxDoseQty |> Option.bind BigRational.fromFloat
                                        (min, max) |> MinMax.fromTuple
                                }
                             )
                             |> Array.distinct

                        rs
                        |> Array.map (fun r ->
                            {
                                DoseLimitTarget = r.Substance |> SubstanceDoseLimitTarget
                                DoseUnit =
                                    if r.Substance |> String.isNullOrWhiteSpace |> not then r.DoseUnit
                                    else
                                        match shapeLimits |> Array.tryHead with
                                        | Some sl -> sl.DoseUnit
                                        | None -> ""
                                RateUnit = r.RateUnit
                                Quantity = (r.MinQty, r.MaxQty) |> MinMax.fromTuple
                                NormQuantityAdjust = r.NormQtyAdj
                                QuantityAdjust = (r.MinQtyAdj, r.MaxQtyAdj) |> MinMax.fromTuple
                                PerTime = (r.MinPerTime, r.MaxPerTime) |> MinMax.fromTuple
                                NormPerTimeAdjust = r.NormPerTimeAdj
                                PerTimeAdjust = (r.MinPerTimeAdj, r.MaxPerTimeAdj) |> MinMax.fromTuple
                                Rate = (r.MinRate, r.MaxRate) |> MinMax.fromTuple
                                RateAdjust = (r.MinRateAdj, r.MaxRateAdj) |> MinMax.fromTuple
                            }
                        )
                        |> Array.append shapeLimits

                    Products =
                        Product.get ()
                        |> Product.filter
                         { Filter.filter with
                             Generic = dr.Generic |> Some
                             Shape = dr.Shape |> Some
                             Route = dr.Route |> Some
                         }
                }
            )


    /// <summary>
    /// Get the DoseRules from the Google Sheet.
    /// </summary>
    /// <remarks>
    /// This function is memoized.
    /// </remarks>
    let get : unit -> DoseRule [] =
        Memoization.memoize get_


    /// <summary>
    /// Filter the DoseRules according to the Filter.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="drs">The DoseRule array</param>
    let filter (filter : Filter) (drs : DoseRule array) =
        let eqs a b =
            a
            |> Option.map (fun x -> x = b)
            |> Option.defaultValue true

        [|
            fun (dr : DoseRule) -> dr.Indication |> eqs filter.Indication
            fun (dr : DoseRule) -> dr.Generic |> eqs filter.Generic
            fun (dr : DoseRule) -> dr.Shape |> eqs filter.Shape
            fun (dr : DoseRule) -> dr.Route |> eqs filter.Route
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
    let indications = getMember (fun dr -> dr.Indication)


    /// Extract all the generics from the DoseRules.
    let generics = getMember (fun dr -> dr.Generic)


    /// Extract all the shapes from the DoseRules.
    let shapes = getMember (fun dr -> dr.Shape)


    /// Extract all the routes from the DoseRules.
    let routes = getMember (fun dr -> dr.Route)


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
        |> Array.map (fun r -> r.PatientCategory)
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