namespace Informedica.KinderFormularium.Lib


module Export =

    open Informedica.ZIndex.Lib
    open Informedica.Utils.Lib.BCL
    open Informedica.KinderFormularium.Lib


    let map (formulary : Drug.Drug array) =
        let minMaxToString (mm : Drug.MinMax.MinMax option) =
            mm
            |> Option.map (fun mm ->
                mm.Min |> Option.map string |> Option.defaultValue "",
                mm.Max |> Option.map string |> Option.defaultValue ""
            )
            |> Option.defaultValue ("", "")

        formulary
        |> Array.toList
        |> List.collect (fun drug ->
            drug.Doses
            |> List.collect (fun dose ->
                dose.Routes
                |> List.filter (fun route ->
                    route.Name.Trim()
                    |> String.equalsCapInsens "toedieningsweg niet van toepassing"
                    |> not
                )
                |> List.collect (fun route ->
                    route.Schedules
                    |> List.collect (fun schedule ->
                        let pedFormName =
                            drug.Generic
                            |> String.toLower
                            |> String.replace "+" "/"
                            |> String.replace " / " "/"
                            |> String.replace "/ " "/"
                            |> String.replace " /" "/"
                            |> String.replace "ë" "e"
                            |> String.replace "ï" "i"
                            |> String.replace " (combinatiepreparaat)" ""
                            |> String.replace " (combinatie preparaat)" ""
                            |> String.replace " (combinatie)" ""
                            |> String.trim

                        let doseUnit =
                                Units.units
                                |> List.tryFind (fun (s, _, _, _) -> s = schedule.Unit)
                                |> Option.bind (fun (_, du, _, _) -> du)
                                |> Option.map Informedica.GenUnits.Lib.Units.toStringDutchShort
                                |> Option.defaultValue ""
                        let adjustUnit =
                                Units.units
                                |> List.tryFind (fun (s, _, _, _) -> s = schedule.Unit)
                                |> Option.bind (fun (_, _, au, _) -> au)
                                |> Option.map Informedica.GenUnits.Lib.Units.toStringDutchShort
                                |> Option.defaultValue ""
                        let freqUnit =
                                Units.units
                                |> List.tryFind (fun (s, _, _, _) -> s = schedule.Unit)
                                |> Option.bind (fun (_, _, _, fu) -> fu)
                                |> Option.map Informedica.GenUnits.Lib.Units.toStringDutchShort
                                |> Option.defaultValue ""

                        let minQty, maxQty =
                            match doseUnit |> String.notEmpty,
                                  adjustUnit |> String.notEmpty,
                                  freqUnit |> String.notEmpty with
                            | true, false, false ->
                                schedule.Value |> minMaxToString
                            | _ -> "", ""

                        let normQtyAdj, minQtyAdj, maxQtyAdj =
                            match doseUnit |> String.notEmpty,
                                  adjustUnit |> String.notEmpty,
                                  freqUnit |> String.notEmpty with
                            | true, true, false ->
                                let minQtyAdj, maxQtyAdj = schedule.Value |> minMaxToString
                                if minQtyAdj = maxQtyAdj then minQtyAdj, "", ""
                                else "", minQtyAdj, maxQtyAdj
                            | _ -> "", "", ""

                        let minPerTime, maxPerTime =
                            match doseUnit |> String.notEmpty,
                                  adjustUnit |> String.notEmpty,
                                  freqUnit |> String.notEmpty with
                            | true, false, true ->
                                schedule.Value |> minMaxToString
                            | _ -> "", ""

                        let normPerTimeAdj, minPerTimeAdj, maxPerTimeAdj =
                            match doseUnit |> String.notEmpty,
                                  adjustUnit |> String.notEmpty,
                                  freqUnit |> String.notEmpty with
                            | true, true, true ->
                                let minPerTimeAdj, maxPerTimeAdj = schedule.Value |> minMaxToString
                                if minPerTimeAdj = maxPerTimeAdj then minPerTimeAdj, "", ""
                                else "", minPerTimeAdj, maxPerTimeAdj
                            | _ -> "", "", ""

                        let route =
                            route.Name
                            |> Mapping.mapRoute
                            |> Option.defaultValue route.Name
                            |> (fun r ->
                                match r with
                                | s when s = "intralesionaal" -> "intralaesionaal"
                                | s when s = "intra_articulair" -> "intraarticulair"
                                | _ -> r
                            )
                            |> String.toLower

                        GenPresProduct.get []
                        |> Array.filter (fun gpp ->
                            let gppName = gpp.Name |> String.toLower
                            // should be a valid shape
                            Mapping.validShapes
                            |> List.exists ((=) (gpp.Shape.ToLower().Trim())) &&
                            // check if names match
                            (pedFormName = gppName ||
                            (pedFormName |> String.contains gppName && pedFormName |> String.contains "/" |> not) ||
                            ((pedFormName |> String.split "/" |> List.sort) = (gppName |> String.split "/" |> List.sort)))
                            // should have equal route
                            &&
                            gpp.Routes
                            |> Array.exists (String.equalsCapInsens route)
                        )
                        |> Array.toList
                        |> List.collect (fun gpp ->
                            gpp.GenericProducts
                            |> Array.collect _.Substances
                            |> Array.map _.SubstanceName
                            |> Array.distinct
                            |> Array.toList
                            |> List.map (fun sn ->
                                {|
                                    generic = gpp.Name.ToLower()
                                    shape = gpp.Shape.ToLower()
                                    route = route
                                    indication = dose.Indication
                                    scheduleText = schedule.ScheduleText
                                    gender = schedule.Target |> Drug.Target.genderToString
                                    minAge =
                                        schedule.Target
                                        |> Drug.Target.getAgeInDays
                                        |> function
                                            | Some days, _ -> $"{int days}"
                                            | _ -> ""
                                    maxAge =
                                        schedule.Target
                                        |> Drug.Target.getAgeInDays
                                        |> function
                                            | _, Some days -> $"{int days}"
                                            | _ -> ""
                                    minWeight =
                                        schedule.Target
                                        |> Drug.Target.getWeightInGram
                                        |> function
                                            | Some weight, _ -> $"{int weight}"
                                            | _ -> ""
                                    maxWeight =
                                        schedule.Target
                                        |> Drug.Target.getWeightInGram
                                        |> function
                                            | _, Some weight -> $"{int weight}"
                                            | _ -> ""
                                    minBSA = ""
                                    maxBSA = ""
                                    minGestAge =
                                        schedule.Target
                                        |> Drug.Target.getGestAgeInDays
                                        |> function
                                            | Some days, _ -> $"{int days}"
                                            | _ -> ""
                                    maxGestAge =
                                        schedule.Target
                                        |> Drug.Target.getGestAgeInDays
                                        |> function
                                            | _, Some days -> $"{int days}"
                                            | _ -> ""
                                    minPMAge =
                                        schedule.Target
                                        |> Drug.Target.getPMAgeInDays
                                        |> function
                                            | Some days, _ -> $"{int days}"
                                            | _ -> ""
                                    maxPMAge =
                                        schedule.Target
                                        |> Drug.Target.getPMAgeInDays
                                        |> function
                                            | _, Some days -> $"{int days}"
                                            | _ -> ""
                                    doseType =
                                        if schedule.Frequency |> Option.isNone then ""
                                        else
                                            schedule.Frequency
                                            |> Option.map Drug.Frequency.toDoseType
                                            |> Option.defaultValue ""
                                    substance = sn.ToLower().Trim()
                                    freqs =
                                        schedule.Frequency
                                        |> Option.map Drug.Frequency.getFrequency
                                        |> Option.defaultValue ""
                                    doseUnit = doseUnit
                                    adjustUnit = adjustUnit
                                    freqUnit = freqUnit
                                    minQty = minQty
                                    maxQty = maxQty
                                    normQtyAdj = normQtyAdj
                                    minQtyAdj = minQtyAdj
                                    maxQtyAdj = maxQtyAdj
                                    minPerTime = minPerTime
                                    maxPerTime = maxPerTime
                                    normPerTimeAdj = normPerTimeAdj
                                    minPerTimeAdj = minPerTimeAdj
                                    maxPerTimeAdj = maxPerTimeAdj
                                |}
                            )
                        )
                        |> function
                            | [] ->
                                [
                                    {|
                                        generic = pedFormName
                                        shape = ""
                                        route = route
                                        indication = dose.Indication
                                        scheduleText = schedule.TargetText
                                        gender = schedule.Target |> Drug.Target.genderToString
                                        minAge =
                                            schedule.Target
                                            |> Drug.Target.getAgeInDays
                                            |> function
                                                | Some days, _ -> $"{int days}"
                                                | _ -> ""
                                        maxAge =
                                            schedule.Target
                                            |> Drug.Target.getAgeInDays
                                            |> function
                                                | _, Some days -> $"{int days}"
                                                | _ -> ""
                                        minWeight =
                                            schedule.Target
                                            |> Drug.Target.getWeightInGram
                                            |> function
                                                | Some weight, _ -> $"{int weight}"
                                                | _ -> ""
                                        maxWeight =
                                            schedule.Target
                                            |> Drug.Target.getWeightInGram
                                            |> function
                                                | _, Some weight -> $"{int weight}"
                                                | _ -> ""
                                        minBSA = ""
                                        maxBSA = ""
                                        minGestAge =
                                            schedule.Target
                                            |> Drug.Target.getGestAgeInDays
                                            |> function
                                                | Some days, _ -> $"{int days}"
                                                | _ -> ""
                                        maxGestAge =
                                            schedule.Target
                                            |> Drug.Target.getGestAgeInDays
                                            |> function
                                                | _, Some days -> $"{int days}"
                                                | _ -> ""
                                        minPMAge =
                                            schedule.Target
                                            |> Drug.Target.getPMAgeInDays
                                            |> function
                                                | Some days, _ -> $"{int days}"
                                                | _ -> ""
                                        maxPMAge =
                                            schedule.Target
                                            |> Drug.Target.getPMAgeInDays
                                            |> function
                                                | _, Some days -> $"{int days}"
                                                | _ -> ""
                                        doseType =
                                            if schedule.Frequency |> Option.isNone then ""
                                            else
                                                schedule.Frequency
                                                |> Option.map Drug.Frequency.toDoseType
                                                |> Option.defaultValue ""
                                        substance = ""
                                        freqs =
                                            schedule.Frequency
                                            |> Option.map Drug.Frequency.getFrequency
                                            |> Option.defaultValue ""
                                        doseUnit = doseUnit
                                        adjustUnit = adjustUnit
                                        freqUnit = freqUnit
                                        minQty = minQty
                                        maxQty = maxQty
                                        normQtyAdj = normQtyAdj
                                        minQtyAdj = minQtyAdj
                                        maxQtyAdj = maxQtyAdj
                                        minPerTime = minPerTime
                                        maxPerTime = maxPerTime
                                        normPerTimeAdj = normPerTimeAdj
                                        minPerTimeAdj = minPerTimeAdj
                                        maxPerTimeAdj = maxPerTimeAdj
                                    |}
                                ]

                            | xs -> xs
                    )
                )
            )
        )


    let addMaxDoses (mapped : {| adjustUnit: string; doseType: string; doseUnit: string; freqUnit: string; freqs: string; gender: string; generic: string; indication: string; maxAge: string; maxBSA: string; maxGestAge: string; maxPMAge: string; maxPerTime: string; maxPerTimeAdj: string; maxQty: string; maxQtyAdj: string; maxWeight: string; minAge: string; minBSA: string; minGestAge: string; minPMAge: string; minPerTime: string; minPerTimeAdj: string; minQty: string; minQtyAdj: string; minWeight: string; normPerTimeAdj: string; normQtyAdj: string; route: string; scheduleText: string; shape: string; substance: string |} list) =
        let batches =
            mapped
            |> List.toArray
            |> Array.chunkBySize 10
        let count = batches |> Array.length
        let n = ref 0

        batches
        |> Array.collect (fun chunked ->
            n.Value <- n.Value + 1
            printfn $"processed {n.Value} of total {count}"

            chunked
            |> Array.map  OpenAI.mapMaxDoses
            |> Async.Parallel
            |> fun p ->
                async {
                    do! Async.Sleep(1000)
                    return! p
                }
            |> Async.RunSynchronously
        )


    let fields =
        [
            "Generic"
            "Shape"
            "Route"
            "Indication"
            "Dep"
            "Diagn"
            "ScheduleText"
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


    let toDataString (mapped : {| adjustUnit: string; doseType: string; doseUnit: string; freqUnit: string; freqs: string; gender: string; generic: string; indication: string; maxAge: string; maxBSA: string; maxGestAge: string; maxPMAge: string; maxPerTime: string; maxPerTimeAdj: string; maxQty: string; maxQtyAdj: string; maxWeight: string; minAge: string; minBSA: string; minGestAge: string; minPMAge: string; minPerTime: string; minPerTimeAdj: string; minQty: string; minQtyAdj: string; minWeight: string; normPerTimeAdj: string; normQtyAdj: string; route: string; scheduleText: string; shape: string; substance: string |} list) =
        mapped
        |> List.map (fun r ->
            [
                r.generic
                r.shape
                r.route
                r.indication
                "" // department
                "" // diagn
                r.scheduleText
                    |> String.replace "\t" " "
                    |> String.replace "\r\n" " "
                    |> String.replace "\n" " "
                    |> String.replace "\r" " "
                    |> String.removeBrackets
                    |> String.trim
                r.gender
                r.minAge
                r.maxAge
                r.minWeight
                r.maxWeight
                r.minBSA
                r.maxBSA
                r.minGestAge
                r.maxGestAge
                r.minPMAge
                r.maxPMAge
                r.doseType
                r.substance
                r.freqs
                r.doseUnit |> String.removeBrackets
                r.adjustUnit |> String.removeBrackets
                r.freqUnit |> String.removeBrackets
                "" // rateUnit
                "" // MinTime
                "" // MaxTime
                "" // TimeUnit
                "" // MinInt
                "" // MaxInt
                "" // IntUnit
                "" // MinDur
                "" // MaxDur
                "" // DurUnit
                r.minQty
                r.maxQty
                r.normQtyAdj // NormQtyAdj
                r.minQtyAdj // MinQtyAdj
                r.maxQtyAdj // MaxQtyAdj
                r.minPerTime // MinPerTime
                r.maxPerTime // MaxPerTime
                r.normPerTimeAdj // NormPerTimeAdj
                r.minPerTimeAdj // MinPerTimeAdj
                r.maxPerTimeAdj // MaxPerTimeAdj
                "" // MinRate
                "" // MaxRate
                "" // MinRateAdj
                "" // MaxRateAdj
            ]
            |> String.concat "\t"
        )
        |> List.append fields
        |> List.distinct


    let writeToFile path mapped =
        mapped
        |> toDataString
        |> String.concat "\n"
        |> File.writeTextToFile path



