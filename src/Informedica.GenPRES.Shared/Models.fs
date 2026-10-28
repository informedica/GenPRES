namespace Informedica.GenPRES.Shared


module Models =

    open Types


    /// Why a draft is not a patient.
    [<RequireQualifiedAccess>]
    type PatientError =
        // neither an age, nor a measured weight and a measured height
        | NoAgeOrMeasuredWeightAndHeight


    /// Canonical patient business logic shared between Client (Fable/JS) and Server (.NET).
    ///
    /// The patient record in Types is the shape the wire carries and the panel edits: every
    /// field optional, so a value is a draft or a reading until `validate` says it meets the
    /// minimum. This is the one module for that record: the blank draft, the editors, the
    /// readers, the estimates and the minimum check.
    ///
    /// Architecture:
    /// - This module is the single source of truth for patient logic that runs on BOTH sides
    /// - The Client UI uses these functions directly for the patient input form
    /// - The Server uses helper functions (getAgeInDays, getWeight, etc.) in
    ///   ServerApi.mapFromSharedPatient to convert to GenForm.Lib.Types.Patient
    /// - Server-only logic (dose rules, eGFR formulas) lives in GenFORM/GenCORE
    /// - Any new patient logic must decide: needed on client? → put here.
    ///   Server-only? → put in GenFORM.Patient or GenCORE.Calculations
    module Patient =

        open System


        /// The blank draft: the one value with every field empty.
        let empty: Patient =
            {
                Age = None
                GestationalAge = None
                Weight =
                    {
                        EstimatedP3 = None
                        Estimated = None
                        EstimatedP97 = None
                        Measured = None
                    }
                Height =
                    {
                        EstimatedP3 = None
                        Estimated = None
                        EstimatedP97 = None
                        Measured = None
                    }
                Gender = UnknownGender
                Access = []
                RenalFunction = None
                Location = None
                Department = None
            }


        /// Whether a draft is a patient: with an age, the rest can be estimated; without one, a
        /// measured weight and a measured height are needed. Below that there is no patient.
        let validate (pat: Patient) : Result<Patient, PatientError> =
            if pat.Age.IsSome || (pat.Weight.Measured.IsSome && pat.Height.Measured.IsSome) then
                Ok pat
            else
                Error PatientError.NoAgeOrMeasuredWeightAndHeight


        module Age =

            open Patient

            let (>>=) r f = Result.bind f r


            let ageZero =
                {
                    Years = 0<year>
                    Months = 0<month>
                    Weeks = 0<week>
                    Days = 0<day>
                }


            let create years months weeks days =
                {
                    Years = years
                    Months = months |> Option.defaultValue 0<month>
                    Weeks = weeks |> Option.defaultValue 0<week>
                    Days = days |> Option.defaultValue 0<day>
                }


            let fromDays days =
                let yrs = days / 365
                let mos = (days - yrs * 365) / 30
                let wks = (days - yrs * 365 - mos * 30) / 7

                let dys =
                    if days - yrs * 365 - mos * 30 - wks * 7 > 0 then
                        days - yrs * 365 - mos * 30 - wks * 7
                    else
                        0

                create
                    (yrs * 1<year>)
                    (if mos > 0 then Some(mos * 1<month>) else None)
                    (if wks > 0 then Some(wks * 1<week>) else None)
                    (if dys > 0 then Some(dys * 1<day>) else None)


            let getYears { Age.Years = yrs } = yrs


            let getMonths { Age.Months = mos } = mos


            let getWeeks { Age.Weeks = ws } = ws


            let getDays { Age.Days = ds } = ds


            let calcYears a = (a |> getYears |> float) + ((a |> getMonths |> float) / 12.)


            let calcMonths a = (a |> getYears |> int) * 12 + (a |> getMonths |> int)

        let apply f (p: Patient) = f p


        let get = apply id


        let getAge p = (p |> get).Age


        let getAgeYears p = p |> getAge |> Option.map _.Years


        let getAgeMonths p = p |> getAge |> Option.map _.Months


        let getAgeWeeks p = p |> getAge |> Option.map _.Weeks


        let getAgeDays p = p |> getAge |> Option.map _.Days


        let tryParse (s: string) =
            match Int32.TryParse(s) with
            | false, _ -> None
            | true, v -> v |> Some


        let getAgeInDays p =
            [
                p |> getAgeYears |> Option.map (fun ys -> (ys |> float) * 365.)
                p |> getAgeMonths |> Option.map (fun ms -> (ms |> float) * 30.)
                p |> getAgeWeeks |> Option.map (fun ws -> (ws |> float) * 7.)
                p |> getAgeDays |> Option.map (fun ds -> (ds |> float) / 1.)
            ]
            |> List.choose id
            |> function
                | [] -> None
                | xs -> xs |> List.sum |> Some


        let getGestAgeInDays (p: Patient) =
            p.GestationalAge
            |> Option.map (fun ga -> (Calculations.Age.weeksToDays ga.Weeks + ga.Days) |> int)


        let getPostConceptionalAgeInDays (p: Patient) =
            match p.GestationalAge, p |> getAgeInDays with
            | Some ga, Some age ->
                let gaDays = (Calculations.Age.weeksToDays ga.Weeks + ga.Days) |> int
                int age + gaDays |> Some
            | _ -> None


        /// Get either the measured weight or the
        /// estimated weight if measured weight = 0
        let getWeight (pat: Patient) =
            if pat.Weight.Measured.IsSome then
                pat.Weight.Measured
            else
                pat.Weight.Estimated


        /// Get either the measured height or the
        /// estimated height if measured weight = 0
        let getHeight (pat: Patient) =
            if pat.Height.Measured.IsSome then
                pat.Height.Measured
            else
                pat.Height.Estimated


        let create years months weeks days weight height gw gd gend cvl gfr dep : Patient option =
            let a =
                if
                    Option.isNone years
                    && Option.isNone months
                    && Option.isNone weeks
                    && Option.isNone days
                then
                    None
                else
                    { Age.ageZero with
                        Age.Years = years |> Option.defaultValue 0<year>
                        Months = months |> Option.defaultValue 0<month>
                        Weeks = weeks |> Option.defaultValue 0<week>
                        Days = days |> Option.defaultValue 0<day>
                    }
                    |> Some

            let ga =
                if Option.isNone gw && Option.isNone gd then
                    None
                else
                    {
                        Patient.GestationalAge.Weeks = gw |> Option.defaultValue 37<week>
                        Patient.GestationalAge.Days = gd |> Option.defaultValue 0<day>
                    }
                    |> Some

            {
                Age = a
                GestationalAge = ga
                Weight =
                    {
                        EstimatedP3 = None
                        Estimated = None
                        EstimatedP97 = None
                        Measured = weight |> Option.map Measures.toGram
                    }
                Height =
                    {
                        EstimatedP3 = None
                        Estimated = None
                        EstimatedP97 = None
                        Measured = height |> Option.map Measures.toCm
                    }
                Gender = gend
                Access = cvl
                RenalFunction = gfr
                Location = None
                Department = dep
            }
            |> Some


        /// The estimates written, the measured values left as they are.
        let withEstimates
            (ew: (int<gram> * int<gram> * int<gram>) option)
            (eh: (int<cm> * int<cm> * int<cm>) option)
            (pat: Patient)
            =
            { pat with
                Weight =
                    { pat.Weight with
                        EstimatedP3 = ew |> Option.map (fun (p3, _, _) -> p3)
                        Estimated = ew |> Option.map (fun (_, m, _) -> m)
                        EstimatedP97 = ew |> Option.map (fun (_, _, p97) -> p97)
                    }
                Height =
                    { pat.Height with
                        EstimatedP3 = eh |> Option.map (fun (p3, _, _) -> p3)
                        Estimated = eh |> Option.map (fun (_, m, _) -> m)
                        EstimatedP97 = eh |> Option.map (fun (_, _, p97) -> p97)
                    }
            }


        let applyNormalValues
            (normalWeights: NormalValue list option)
            (normalHeights: NormalValue list option)
            (normalNeoWeights: NormalValue list option)
            (normalNeoHeights: NormalValue list option)
            (pat: Patient)
            =

            let wghts =
                [ 21000..1000..100000 ]
                |> List.append [ 10500..500..20000 ]
                |> List.append [ 2000..100..10000 ]
                |> List.append [ 400..50..1950 ]

            let hghts = [ 40..220 ]

            let forGender age nvs gend =
                let nvs = nvs |> List.filter (fun nv -> nv.Sex = gend)

                nvs
                |> List.map _.Age
                |> fun ages -> if ages.IsEmpty then -1 else ages |> List.nearestIndex age // no row for the sex
                |> fun idx ->
                    if idx < 0 || idx >= nvs.Length then
                        None
                    else
                        (nvs[idx].P3, nvs[idx].Mean, nvs[idx].P97) |> Some

            let nearest age (nvs: NormalValue list option) =
                match nvs with
                | None -> None
                | Some nvs ->
                    let forGender = forGender age nvs

                    match pat.Gender with
                    | UnknownGender ->
                        let mw = "M" |> forGender
                        let fw = "F" |> forGender
                        // take the average of two genders
                        mw
                        |> Option.bind (fun (mp3, mm, mp97) ->
                            fw
                            |> Option.map (fun (fp3, fm, fp97) -> (fp3 + mp3) / 2., (fm + mm) / 2., (fp97 + mp97) / 2.)
                        )
                    | _ ->
                        if pat.Gender = Female then "F" else "M"
                        |> forGender

            let ew, eh =
                match pat.Age with
                | None -> None, None
                | Some age ->
                    // a neonatal table answers while it reaches: past its last week an older
                    // infant with a gestational age takes the age table, or the nearest row would
                    // give a newborn's value. Each table is judged by its own reach
                    let neonatal (nvs: NormalValue list option) =
                        pat
                        |> getPostConceptionalAgeInDays
                        |> Option.map (fun days -> (days |> float) / 7.)
                        |> Option.filter (fun weeks -> nvs |> Option.exists (List.exists (fun nv -> nv.Age >= weeks)))
                        |> Option.bind (fun weeks -> nvs |> nearest weeks)

                    let ageInYears = age |> Age.calcYears

                    let weight =
                        match normalNeoWeights |> neonatal with
                        | Some(p3, m, p97) ->
                            (int p3 * 1<gram>,
                             wghts[wghts |> List.nearestIndex (int m)] |> Measures.toGram,
                             int p97 * 1<gram>)
                            |> Some
                        | None ->
                            normalWeights
                            |> nearest ageInYears
                            |> Option.map (fun (p3, m, p97) ->
                                let m =
                                    wghts
                                    |> List.nearestIndex (int (m * 1000.))
                                    |> fun idx -> wghts[idx]
                                    |> Measures.toGram

                                int p3 * 1000<gram>, m, int p97 * 1000<gram>
                            )

                    let height =
                        match normalNeoHeights |> neonatal with
                        | Some(p3, m, p97) ->
                            (int p3 * 1<cm>, hghts[hghts |> List.nearestIndex (int m)] |> Measures.toCm, int p97 * 1<cm>)
                            |> Some
                        | None ->
                            normalHeights
                            |> nearest ageInYears
                            |> Option.map (fun (p3, m, p97) ->
                                let m = hghts |> List.nearestIndex (int m) |> (fun idx -> hghts[idx]) |> Measures.toCm

                                int p3 * 1<cm>, m, int p97 * 1<cm>
                            )

                    weight, height

            // the estimate stays an estimate: the measured values hold what was entered or read
            pat |> withEstimates ew eh


    module Intervention =


        let emptyIntervention =
            {
                Hospital = ""
                Category = ""
                Name = ""
                MinWeightKg = None
                MaxWeightKg = None
                Quantity = None
                QuantityUnit = ""
                Solution = ""
                Total = None
                TotalUnit = ""
                SubstanceDose = None
                SubstanceMinDose = None
                SubstanceMaxDose = None
                SubstanceDoseUnit = ""
                SubstanceDoseAdjust = None
                SubstanceNormDoseAdjust = None
                SubstanceMinDoseAdjust = None
                SubstanceMaxDoseAdjust = None
                SubstanceDoseAdjustUnit = ""
                SubstanceDoseText = ""
                InterventionDose = None
                InterventionDoseUnit = ""
                InterventionDoseText = ""
                Text = ""
            }


    /// <summary>
    /// Bolus medication for resuscitation scenarios: <c>parse</c> reads the
    /// "emergencylist" sheet of the emergency-list spreadsheet (see
    /// <c>dataEMLUrlId</c> in the client's Utils - a different workbook from the
    /// GENPRES_URL_ID one, loaded by the client rather than by GenFORM.Lib).
    /// </summary>
    /// <remarks>
    /// Columns: hospital, indication, medication, minWeight, maxWeight, dose, min,
    /// max, conc, unit, remark. The four "template-generic" / "template-route" /
    /// "template-dose-type" / "template-indication" columns are OPTIONAL - they are
    /// read only when present in the header row - and preselect a prescription when
    /// the user picks the entry.
    /// </remarks>
    module EmergencyTreatment =


        let toStr = decimal >> Decimal.toStringNumberNLWithoutTrailingZeros


        let calcDoseVol kg doserPerKg conc min max =
            let d =
                if min = max && min > 0. then
                    min
                else
                    kg * doserPerKg
                    |> fun d ->
                        if max > 0. && d > max then max
                        else if min > 0. && d < min then min
                        else d

            let v =
                d / conc
                |> (fun v ->
                    if v >= 10. then
                        v |> Math.roundBy 1.
                    else
                        v |> Math.roundBy 0.1
                )
                |> Math.fixPrecision 2

            v * conc |> Math.fixPrecision 2, v


        let ageInMoToYrs ageInMo = (ageInMo |> float) / 12.


        let calcIntervention hosp indication name text formula doseTextFn a =
            let m = formula a

            { Intervention.emptyIntervention with
                Hospital = hosp
                Category = indication
                Name = name
                InterventionDose = Some m
                SubstanceDoseText = doseTextFn m
                Text = text
            }


        let calcTube n =
            let textfn m = $"%s{m - 0.5 |> toStr} - %s{m |> toStr} - %s{m + 0.5 |> toStr}"

            let formula age =
                n + age / 4. |> Math.roundBy0_5 |> (fun m -> if m > 7. then 7. else m)

            calcIntervention
                ""
                "reanimatie"
                $"""tube ({if n = 3.5 then "met" else "zonder"} cuff) maat"""
                $"%s{n |> toStr} + leeftijd / 4"
                formula
                textfn

        let calcOralLength =
            let formula age = 12. + age / 2. |> Math.roundBy0_5
            let textfn m = $"%s{m |> toStr} cm"

            calcIntervention "" "reanimatie" "tube lengte oraal" "12 + leeftijd / 2" formula textfn


        let calcNasalLength =
            let formula age = 15. + age / 2. |> Math.roundBy0_5
            let textfn m = $"%s{m |> toStr} cm"

            calcIntervention "" "reanimatie" "tube lengte nasaal" "15 + leeftijd / 2" formula textfn


        let joules = [ 1; 2; 3; 5; 7; 10; 20; 30; 50; 70; 100; 150 ] |> List.map float


        let calcDefib =
            let formula wght = joules |> List.findNearestMax (wght * 4.)

            let textfn m = $"%s{m |> toStr} joule"

            calcIntervention "" "reanimatie" "defibrillatie" "4 joule/kg" formula textfn


        let calcCardioVersion =
            let formula wght = joules |> List.findNearestMax (wght * 2.)

            let textfn m = $"%s{m |> toStr} joule"
            calcIntervention "" "reanimatie" "cardioversie" "2 joule/kg" formula textfn


        let calcBolusMedication wght (bolus: BolusMedication) =
            let toStr = decimal >> Decimal.toStringNumberNLWithoutTrailingZeros

            let d, v, c =
                let d, v = calcDoseVol wght bolus.NormDose bolus.Concentration bolus.MinDose bolus.MaxDose

                if d > 0. then
                    d, v, bolus.Concentration
                else
                    calcDoseVol wght bolus.NormDose (bolus.Concentration / 10.) bolus.MinDose bolus.MaxDose
                    |> fun (d, v) -> d, v, bolus.Concentration / 10.

            let adv s =
                if s <> "" then
                    s
                else
                    match bolus.MinDose = 0., bolus.MaxDose = 0. with
                    | true, true -> $"%s{bolus.NormDose |> toStr} %s{bolus.Unit}/kg"

                    | true, false ->
                        $"%s{bolus.NormDose |> toStr} %s{bolus.Unit}/kg (max %A{bolus.MaxDose} %s{bolus.Unit})"
                    | false, true ->
                        $"%s{bolus.NormDose |> toStr} %s{bolus.Unit}/kg (min %A{bolus.MinDose} %s{bolus.Unit})"
                    | false, false ->
                        if bolus.MinDose = bolus.MaxDose then
                            $"%s{bolus.MinDose |> toStr} %s{bolus.Unit}"
                        else
                            $"%s{bolus.NormDose |> toStr} %s{bolus.Unit}/kg (%A{bolus.MinDose} - %A{bolus.MaxDose} %s{bolus.Unit})"

            { Intervention.emptyIntervention with
                Hospital = bolus.Hospital
                Category = bolus.Category
                Name = bolus.Generic
                Quantity = Some c
                QuantityUnit = bolus.Unit
                TotalUnit = "ml"
                InterventionDose = Some v
                InterventionDoseUnit = "ml"
                InterventionDoseText = $"%s{v |> toStr} ml van %s{c |> toStr} {bolus.Unit}/ml"
                SubstanceDose = Some d
                SubstanceMinDose = if bolus.MinDose = 0. then None else Some bolus.MinDose
                SubstanceMaxDose = if bolus.MaxDose = 0. then None else Some bolus.MaxDose
                SubstanceDoseUnit = bolus.Unit
                SubstanceDoseAdjust =
                    if bolus.MinDose = bolus.MaxDose && bolus.MinDose > 0. then
                        None
                    else
                        Some(d / wght |> Math.fixPrecision 1)
                SubstanceNormDoseAdjust =
                    if bolus.MinDose = bolus.MaxDose && bolus.MinDose > 0. then
                        None
                    else
                        Some bolus.NormDose
                SubstanceDoseAdjustUnit =
                    if bolus.MinDose = bolus.MaxDose && bolus.MinDose > 0. then
                        ""
                    else
                        $"{bolus.Unit}/kg"
                SubstanceDoseText =
                    if bolus.MinDose = bolus.MaxDose && bolus.MinDose > 0. then
                        $"%s{d |> toStr} {bolus.Unit}"
                    else
                        $"%s{d |> toStr} {bolus.Unit} (%s{d / wght |> Math.fixPrecision 1 |> toStr} {bolus.Unit}/kg)"
                Text = adv bolus.Remark
            }


        let createBolus
            hosp
            indication
            medication
            minWght
            maxWght
            dose
            min
            max
            conc
            unit
            remark
            templateGeneric
            templateRoute
            templateDoseType
            templateIndication
            =
            {
                Hospital = hosp
                Category = indication
                Generic = medication
                MinWeight = minWght
                MaxWeight = maxWght
                NormDose = dose
                MinDose = min
                MaxDose = max
                Concentration = conc
                Unit = unit
                Remark = remark
                TemplateGeneric = templateGeneric
                TemplateRoute = templateRoute
                TemplateDoseType = templateDoseType
                TemplateIndication = templateIndication
            }


        let parse (data: string[][]) =
            match data with
            | data when data |> Array.length > 1 ->
                let cms = data |> Array.head

                data
                |> Array.skip 1
                |> Array.map (fun sl ->
                    let getString n = Csv.getStringColumn cms sl n |> String.trim

                    let getStringOpt n = if cms |> Array.exists ((=) n) then getString n else ""

                    let getFloat = Csv.getFloatOptionColumn cms sl >> Option.defaultValue 0.

                    createBolus
                        (getString "hospital")
                        (getString "indication")
                        (getString "medication")
                        (getFloat "minWeight")
                        (getFloat "maxWeight")
                        (getFloat "dose")
                        (getFloat "min")
                        (getFloat "max")
                        (getFloat "conc")
                        (getString "unit")
                        (getString "remark")
                        (getStringOpt "template-generic")
                        (getStringOpt "template-route")
                        (getStringOpt "template-dose-type")
                        (getStringOpt "template-indication")
                )
                |> Array.toList
            | _ -> []


        let calculate age weight (bolusMed: BolusMedication list) =
            if weight |> Option.isSome && weight.Value < 3. then
                [
                    calcIntervention
                        ""
                        "reanimatie"
                        "tube maat"
                        "< 1 kg: 2.5, 1-3 kg: 3.0"
                        (fun w -> if w < 1. then 2.5 else 3)
                        (fun f -> $"%s{f |> toStr}")
                        weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "tube lengte oraal"
                        "6.632 + 1.822 x ln(kg)"
                        (fun w -> 6.632 + 1.822 * System.Math.Log(w))
                        (fun f -> $"%s{f |> toStr} cm")
                        weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "tube lengte nasaal"
                        "(45 + 1.15 x \u221A (gram)) / 10"
                        (fun w -> (45. + 1.15 * System.Math.Sqrt(w * 1000.)) / 10.)
                        (fun f -> $"%s{f |> toStr} cm")
                        weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "navel lijn maat"
                        "< 1.5 kg: 3,5 anders 5 "
                        (fun w -> if w < 1.5 then 3.5 else 5.)
                        (fun f -> $"%s{f |> toStr} French")
                        weight.Value

                    if weight.Value < 1.5 then
                        calcIntervention
                            ""
                            "reanimatie"
                            "navel arterie lijn lengte"
                            "kg x 4 + 7"
                            (fun w -> w * 4. + 7.)
                            (fun f -> $"%s{f |> toStr} cm")
                            weight.Value
                    else
                        calcIntervention
                            ""
                            "reanimatie"
                            "navel arterie lijn lengte"
                            "kg x 2.5 + 9.7"
                            (fun w -> w * 2.5 + 9.7)
                            (fun f -> $"%s{f |> toStr} cm")
                            weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "navel vene lijn lengte"
                        "kg x 1.5 + 5.5"
                        (fun w -> w * 1.5 + 5.5)
                        (fun f -> $"%s{f |> toStr} cm")
                        weight.Value
                ]
            else
                [

                    // tube
                    if age |> Option.isSome then
                        calcTube 3.5 age.Value
                        calcTube 4.0 age.Value
                    // oral length
                    if age |> Option.isSome then
                        calcOralLength age.Value
                    // nasal length
                    if age |> Option.isSome then
                        calcNasalLength age.Value
                    // adrenalin
                    if weight |> Option.isSome then
                        yield!
                            bolusMed
                            |> List.filter (fun m -> m.Generic = "adrenaline")
                            |> List.map (calcBolusMedication weight.Value)
                    // defibrillation
                    if weight |> Option.isSome then
                        calcDefib weight.Value
                    // cardioversion
                    if weight |> Option.isSome then
                        calcCardioVersion weight.Value
                ]
            // add rest of bolus medication
            |> fun xs ->
                if weight.IsNone then
                    []
                else
                    bolusMed
                    |> List.filter (fun m -> m.Generic = "adrenaline" |> not)
                    |> List.filter (fun m ->
                        m.MinWeight <= weight.Value && (weight.Value < m.MaxWeight || m.MaxWeight = 0.)
                    )
                    |> List.map (calcBolusMedication weight.Value)
                |> List.append xs
                |> List.distinct


    /// <summary>
    /// Continuous infusion protocols: <c>parse</c> reads the "continuousmeds" sheet
    /// of the emergency-list spreadsheet (see <c>EmergencyTreatment</c> above for
    /// where that workbook lives). Columns: hospital, catagory [sic], indication,
    /// dosetype, medication, generic, unit, doseunit, minweight, maxweight,
    /// quantity, total, mindose, maxdose, absmax, minconc, maxconc, solution.
    /// </summary>
    module ContinuousMedication =

        open Informedica.GenPRES.Shared


        let create
            hospital
            catagory
            indication
            dosetype
            medication
            generic
            unit
            doseunit
            minweight
            maxweight
            quantity
            total
            mindose
            maxdose
            absmax
            minconc
            maxconc
            solution
            =
            {
                Hospital = hospital
                Category = catagory
                Indication = indication
                DoseType = dosetype
                Medication = medication
                Generic = generic
                Unit = unit
                DoseUnit = doseunit
                MinWeight = minweight
                MaxWeight = maxweight
                Quantity = quantity
                Total = total
                MinDose = mindose
                MaxDose = maxdose
                AbsMax = absmax
                MinConc = minconc
                MaxConc = maxconc
                Solution = solution
            }


        let parse (data: string[][]) =
            match data with
            | data when data |> Array.length > 1 ->
                let cms = data |> Array.head

                data
                |> Array.skip 1
                |> Array.map (fun sl ->
                    let getString n = Csv.getStringColumn cms sl n |> String.trim

                    let getFloat = Csv.getFloatColumn cms sl

                    create
                        (getString "hospital")
                        (getString "catagory")
                        (getString "indication")
                        (getString "dosetype")
                        (getString "medication")
                        (getString "generic")
                        (getString "unit")
                        (getString "doseunit")
                        (getFloat "minweight")
                        (getFloat "maxweight")
                        (getFloat "quantity")
                        (getFloat "total")
                        (getFloat "mindose")
                        (getFloat "maxdose")
                        (getFloat "absmax")
                        (getFloat "minconc")
                        (getFloat "maxconc")
                        (getString "solution")
                )
                |> Array.toList
            | _ -> []


    /// <summary>
    /// Available medication products and their concentrations: <c>parse</c> reads
    /// the "products" sheet of the emergency-list spreadsheet. Columns: indication,
    /// medication, conc, unit.
    /// </summary>
    module Products =

        open Informedica.GenPRES.Shared


        let create ind med conc unit =
            {
                Indication = ind
                Medication = med
                Concentration = conc
                Unit = unit
            }


        let parse (data: string[][]) =
            match data with
            | data when data |> Array.length > 1 ->
                let cms = data |> Array.head

                data
                |> Array.skip 1
                |> Array.map (fun sl ->
                    let getString n = Csv.getStringColumn cms sl n |> String.trim

                    let getFloat = Csv.getFloatColumn cms sl

                    create (getString "indication") (getString "medication") (getFloat "conc") (getString "unit")
                )
                |> Array.toList
            | _ -> []


    /// <summary>
    /// Reference ranges for weight and height estimation: <c>parse</c> reads the
    /// "weight", "height", "weight neo" and "height neo" sheets of the
    /// emergency-list spreadsheet, all four sharing the columns sex ("M"/"F"), age
    /// (years), p3, mean, p97.
    /// </summary>
    module NormalValues =

        open Informedica.GenPRES.Shared


        let create sex age p3 mean p97 =
            {
                Sex = sex
                Age = age
                P3 = p3
                Mean = mean
                P97 = p97
            }


        let parse (data: string[][]) =
            match data with
            | data when data |> Array.length > 1 ->
                let cms = data |> Array.head

                data
                |> Array.skip 1
                |> Array.map (fun sl ->
                    let getString n = Csv.getStringColumn cms sl n |> String.trim

                    let getFloat = Csv.getFloatColumn cms sl

                    create (getString "sex") (getFloat "age") (getFloat "p3") (getFloat "mean") (getFloat "p97")
                )
                |> Array.toList
            | _ -> []


        /// The tables from the rows of the four sheets, by sheet name, a sheet that is absent
        /// giving an empty table, as a sheet the client could not fetch does.
        let ofRows (rows: Map<string, string[][]>) : NormalValues =
            let table sheet =
                rows |> Map.tryFind sheet |> Option.map parse |> Option.defaultValue []

            {
                Weights = table "weight"
                Heights = table "height"
                NeoWeights = table "weight neo"
                NeoHeights = table "height neo"
            }


        /// The estimate applied from the tables.
        let apply (nv: NormalValues) (pat: Patient) : Patient =
            pat
            |> Patient.applyNormalValues (Some nv.Weights) (Some nv.Heights) (Some nv.NeoWeights) (Some nv.NeoHeights)


    module Order =


        module ValueUnit =

            // create Informedica.GenPRES.Shared.Types.ValueUnit
            let create v u g s l j =
                {
                    Value = v
                    Unit = u
                    Group = g
                    Short = s
                    Language = l
                    Json = j
                }


        module Variable =

            let create n nonZ min minIncl incr max maxIncl vals =
                {
                    Name = n
                    IsNonZeroPositive = nonZ
                    Min = min
                    MinIncl = minIncl
                    Incr = incr
                    Max = max
                    MaxIncl = maxIncl
                    Vals = vals
                }


        module OrderVariable =

            let create nme cst cal var outerIncr level =
                {
                    Name = nme
                    DefinedConstraints = cst
                    CalculatedConstraints = cal
                    Variable = var
                    LargeIncr = outerIncr
                    Level = level
                }


            let setVu s (vu: Types.ValueUnit option) =
                match vu with
                | Some vu ->
                    { vu with
                        Value =
                            vu.Value
                            |> Array.tryFind (fun (v, _) -> v = (s |> Option.defaultValue ""))
                            |> Option.map Array.singleton
                            |> Option.defaultValue vu.Value
                    }
                    |> Some
                | None -> None


            let setVar (s: string option) (var: Variable) =
                { var with
                    IsNonZeroPositive = s.IsNone
                    Vals = if s.IsNone then None else var.Vals |> setVu s
                }


            let setOvar s (ovar: OrderVariable) = { ovar with Variable = ovar.Variable |> setVar s }


        module Prescription =

            let create isOnce isOnceTimed isCont isDisc isTimed f t =
                {
                    IsOnce = isOnce
                    IsOnceTimed = isOnceTimed
                    IsContinuous = isCont
                    IsDiscontinuous = isDisc
                    IsTimed = isTimed
                    Frequency = f
                    Time = t
                }


        module Dose =


            let create qty ptm rte tot qty_adj ptm_adj rte_adj tot_adj =
                {
                    Quantity = qty
                    PerTime = ptm
                    Rate = rte
                    Total = tot
                    QuantityAdjust = qty_adj
                    PerTimeAdjust = ptm_adj
                    RateAdjust = rte_adj
                    TotalAdjust = tot_adj
                }


        module Item =

            let create n cmp_qty orb_qty cmp_cnc orb_cnc dos add =
                {
                    Name = n
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    ComponentConcentration = cmp_cnc
                    OrderableConcentration = orb_cnc
                    Dose = dos
                    IsAdditional = add
                }


        module Component =

            let create id nm sh cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii pp =
                {
                    Id = id
                    Name = nm
                    Form = sh
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    OrderableCount = orb_cnt
                    OrderQuantity = ord_qty
                    OrderCount = ord_cnt
                    OrderableConcentration = orb_cnc
                    Dose = dos
                    Items = ii
                    Products = pp
                }


        module Orderable =

            let create n orb_qty ord_qty ord_cnt dos_cnt dos cc =
                {
                    Name = n
                    OrderableQuantity = orb_qty
                    OrderQuantity = ord_qty
                    OrderCount = ord_cnt
                    DoseCount = dos_cnt
                    Dose = dos
                    Components = cc
                }


        let create id adj_qty orb prs rte tme sta sto =
            {
                Id = id
                Adjust = adj_qty
                Orderable = orb
                Schedule = prs
                Route = rte
                Duration = tme
                Start = sta
                Stop = sto
            }


    module Totals =

        let empty: Totals =
            {
                Volume = [||]
                Energy = [||]
                Protein = [||]
                Carbohydrate = [||]
                Fat = [||]
                Sodium = [||]
                Potassium = [||]
                Chloride = [||]
                Calcium = [||]
                Phosphate = [||]
                Magnesium = [||]
                Iron = [||]
                VitaminD = [||]
                Ethanol = [||]
                Propyleenglycol = [||]
                BoricAcid = [||]
                BenzylAlcohol = [||]
            }


    module OrderScenario =


        let create ind nme frm rte dst dil cmp itm dils cmps itms prs prep adm o adj rr rn ids acc =
            {
                Name = nme
                Indication = ind
                Form = frm
                Route = rte
                DoseType = dst
                Diluent = dil
                Component = cmp
                Item = itm
                Diluents = dils
                Components = cmps
                Items = itms
                Prescription = prs
                Preparation = prep
                Administration = adm
                Order = o
                UseAdjust = adj
                UseRenalRule = rr
                RenalRule = rn
                ProductIds = ids
                Access = acc
            }


    module NutritionCategory =

        /// The category's name, as the nutrition page shows it.
        let label category =
            match category with
            | NutritionCategory.EnteralFeeding -> "Enterale Voeding"
            | NutritionCategory.EnteralSupplement -> "Enteraal Supplement"
            | NutritionCategory.TPN -> "Totale Parenterale Voeding"
            | NutritionCategory.Lipid -> "Vetten"
            | NutritionCategory.ElectrolyteGlucose -> "Elektrolyten/Glucose"


    module OrderContext =


        let filter =
            {
                Indications = [||]
                Generics = [||]
                Routes = [||]
                Forms = [||]
                DoseTypes = [||]
                Diluents = [||]
                Components = [||]
                Indication = None
                Generic = None
                Form = None
                Route = None
                DoseType = None
                Diluent = None
                SelectedComponents = [||]
            }


        let empty: OrderContext =
            {
                Id = ""
                Category = OrderCategory.Drug
                DemoVersion = true
                Filter = filter
                Patient = Patient.empty
                Scenarios = [||]
                Intake = Totals.empty
                Argumentation = None
            }

        let setPatient pat ctx : OrderContext = { ctx with Patient = pat }


        /// The nutrition category of a context, none for a drug. A display copy of the
        /// domain's `PlanContext.nutritionCategory`; the agreement test in the server tests keeps
        /// them equal.
        let nutritionCategory (ctx: OrderContext) =
            match ctx.Category with
            | OrderCategory.Nutrition category -> Some category
            | OrderCategory.Drug -> None


        /// The order a context contributes to the plan: its scenario, once the context is
        /// narrowed to exactly one; nothing while it holds several candidates or none. A
        /// display copy of the domain's `PlanContext.contribution`; the agreement test in the
        /// server tests keeps them equal.
        let contribution (ctx: OrderContext) = ctx.Scenarios |> Array.tryExactlyOne


        /// A choice a page offers in the filter it builds.
        type FilterField =
            | Indication
            | Generic
            | Route
            | Form
            | DoseType


        /// Every choice there is, for emptying the filter outright.
        let filterFields = [ Indication; Generic; Route; Form; DoseType ]


        /// The order a page offers its choices in, as steps, and what decides how far a change
        /// to one of them reaches: a change lets go of the choices in the steps after its own,
        /// and leaves the ones beside it in its own step alone.
        ///
        /// A drug is found by its indication and by the medication that treats it, in either
        /// order and neither before the other, so the two stand in one step: each narrows what
        /// the other offers, so a pick from the list on the screen agrees with the one already
        /// made, and taking the medication away because the indication was named is a loss with
        /// nothing gained. A route, a form and a dose type are another matter: they belong to
        /// the medication, and the one chosen for the medication before may not exist for this
        /// one.
        ///
        /// A nutrition composition is picked first and the indication follows from it, so there
        /// the two are steps of their own. A choice a page does not offer stands below nothing.
        let chain (ctx: OrderContext) =
            match ctx.Category with
            | OrderCategory.Drug -> [ [ Indication; Generic ]; [ Route ]; [ Form ]; [ DoseType ] ]
            | OrderCategory.Nutrition _ -> [ [ Generic ]; [ Indication ]; [ DoseType ] ]


        /// One field's choice let go, the options it was picked from left standing.
        let clearChoice field (f: Filter) =
            match field with
            | Indication -> { f with Indication = None }
            | Generic -> { f with Generic = None }
            | Route -> { f with Route = None }
            | Form -> { f with Form = None }
            | DoseType -> { f with DoseType = None }


        /// One field emptied: the choice it holds and the options it was picked from.
        let clearField field (f: Filter) =
            match field with
            | Indication ->
                { f with
                    Indications = [||]
                    Indication = None
                }
            | Generic ->
                { f with
                    Generics = [||]
                    Generic = None
                }
            | Route ->
                { f with
                    Routes = [||]
                    Route = None
                }
            | Form ->
                { f with
                    Forms = [||]
                    Form = None
                }
            | DoseType ->
                { f with
                    DoseTypes = [||]
                    DoseType = None
                }


        /// Whether any choice at all is still held.
        let anyChosen (f: Filter) =
            f.Indication.IsSome
            || f.Generic.IsSome
            || f.Route.IsSome
            || f.Form.IsSome
            || f.DoseType.IsSome


        /// A change to one of the choices: the change is written, the scenarios go, since they
        /// stand on the whole filter, and nothing at all happens when the field already holds
        /// what it is given.
        ///
        /// A value picked is picked from the list the answer offered, and every list the answer
        /// offers is narrowed by every choice already made, so a pick agrees with all of them
        /// and none of them has to be let go. Letting them go took away work the user had done
        /// and had asked for: naming a medication after a route threw the route away.
        ///
        /// A field emptied is another matter: the filter widens, so the choices in the steps
        /// below it go with it, and the field keeps neither its choice nor what it was picked
        /// from.
        ///
        /// What the fields below were picked from is left standing until the answer replaces
        /// it. Taking it away as well leaves them empty, and a field with nothing to offer is a
        /// field that cannot be used, so the page goes dead for as long as the request runs.
        ///
        /// When nothing is chosen anywhere afterwards, no options are kept either: a list
        /// narrowed by choices that are gone would offer a smaller world than there is, without
        /// saying so.
        let applyChange field clearOwn write (ctx: OrderContext) : OrderContext =
            let below =
                if not clearOwn then
                    []
                else
                    match ctx |> chain |> List.skipWhile (List.contains field >> not) with
                    | [] -> []
                    | _ :: rest -> rest |> List.concat

            let filter =
                below
                |> List.fold (fun f x -> f |> clearChoice x) ctx.Filter
                |> fun f -> if clearOwn then f |> clearField field else f
                |> write

            let filter =
                if filter |> anyChosen then
                    filter
                else
                    filterFields |> List.fold (fun f x -> f |> clearField x) filter

            { ctx with
                Filter = filter
                Scenarios = [||]
            }


        let indicationChange s (ctx: OrderContext) : OrderContext =
            if ctx.Filter.Indication = s then
                ctx
            else
                ctx |> applyChange Indication s.IsNone (fun f -> { f with Indication = s })


        let medicationChange s (ctx: OrderContext) : OrderContext =
            if ctx.Filter.Generic = s then
                ctx
            else
                ctx |> applyChange Generic s.IsNone (fun f -> { f with Generic = s })


        let routeChange s (ctx: OrderContext) : OrderContext =
            if ctx.Filter.Route = s then
                ctx
            else
                ctx |> applyChange Route s.IsNone (fun f -> { f with Route = s })


        let formChange s (ctx: OrderContext) : OrderContext =
            if ctx.Filter.Form = s then
                ctx
            else
                ctx |> applyChange Form s.IsNone (fun f -> { f with Form = s })


        let diluentChange s (ctx: OrderContext) : OrderContext = { ctx with OrderContext.Filter.Diluent = s }


        let componentsChange cs (ctx: OrderContext) : OrderContext =
            { ctx with OrderContext.Filter.SelectedComponents = cs }


        let doseTypeChange (dt: DoseType option) (ctx: OrderContext) : OrderContext =
            if ctx.Filter.DoseType = dt then
                ctx
            else
                ctx |> applyChange DoseType dt.IsNone (fun f -> { f with DoseType = dt })


        /// The order variable a command addresses: a level, its property, and the names that
        /// locate a component or an item.
        [<RequireQualifiedAccess>]
        type Target =
            | Schedule of ScheduleProperty
            | Orderable of OrderableProperty
            | Component of cmp: string * ComponentProperty
            | Item of cmp: string * itm: string * ItemProperty


        /// The order variables a command addresses, read and changed per level.
        module Target =

            /// The variable of an item's property.
            let itemVar prop (itm: Item) =
                match prop with
                | ItemProperty.DoseQuantity -> itm.Dose.Quantity
                | ItemProperty.DoseQuantityAdjust -> itm.Dose.QuantityAdjust
                | ItemProperty.DosePerTime -> itm.Dose.PerTime
                | ItemProperty.DosePerTimeAdjust -> itm.Dose.PerTimeAdjust
                | ItemProperty.DoseRate -> itm.Dose.Rate
                | ItemProperty.DoseRateAdjust -> itm.Dose.RateAdjust
                | ItemProperty.ComponentConcentration -> itm.ComponentConcentration
                | ItemProperty.OrderableConcentration -> itm.OrderableConcentration
                | ItemProperty.OrderableQuantity -> itm.OrderableQuantity


            /// An item with the variable of its property changed.
            let mapItemVar prop f (itm: Item) =
                let dose (d: Dose) = { itm with Dose = d }

                match prop with
                | ItemProperty.DoseQuantity -> dose { itm.Dose with Quantity = f itm.Dose.Quantity }
                | ItemProperty.DoseQuantityAdjust -> dose { itm.Dose with QuantityAdjust = f itm.Dose.QuantityAdjust }
                | ItemProperty.DosePerTime -> dose { itm.Dose with PerTime = f itm.Dose.PerTime }
                | ItemProperty.DosePerTimeAdjust -> dose { itm.Dose with PerTimeAdjust = f itm.Dose.PerTimeAdjust }
                | ItemProperty.DoseRate -> dose { itm.Dose with Rate = f itm.Dose.Rate }
                | ItemProperty.DoseRateAdjust -> dose { itm.Dose with RateAdjust = f itm.Dose.RateAdjust }
                | ItemProperty.ComponentConcentration ->
                    { itm with ComponentConcentration = f itm.ComponentConcentration }
                | ItemProperty.OrderableConcentration ->
                    { itm with OrderableConcentration = f itm.OrderableConcentration }
                | ItemProperty.OrderableQuantity -> { itm with OrderableQuantity = f itm.OrderableQuantity }


            /// The variable of a component's property.
            let componentVar prop (cmp: Component) =
                match prop with
                | ComponentProperty.OrderableQuantity -> cmp.OrderableQuantity
                | ComponentProperty.DoseQuantityAdjust -> cmp.Dose.QuantityAdjust


            /// A component with the variable of its property changed.
            let mapComponentVar prop f (cmp: Component) =
                match prop with
                | ComponentProperty.OrderableQuantity -> { cmp with OrderableQuantity = f cmp.OrderableQuantity }
                | ComponentProperty.DoseQuantityAdjust ->
                    { cmp with Dose = { cmp.Dose with QuantityAdjust = f cmp.Dose.QuantityAdjust } }


            /// The variable the target addresses in the order; None when the order holds no such
            /// component or item.
            let tryGet target (ord: Order) =
                let tryComponent cmp =
                    ord.Orderable.Components |> Array.tryFind (fun c -> c.Name = cmp)

                match target with
                | Target.Schedule ScheduleProperty.Frequency -> Some ord.Schedule.Frequency
                | Target.Schedule ScheduleProperty.Time -> Some ord.Schedule.Time
                | Target.Orderable OrderableProperty.Quantity -> Some ord.Orderable.OrderableQuantity
                | Target.Orderable OrderableProperty.DoseQuantity -> Some ord.Orderable.Dose.Quantity
                | Target.Orderable OrderableProperty.DoseRate -> Some ord.Orderable.Dose.Rate
                | Target.Component(cmp, prop) -> tryComponent cmp |> Option.map (componentVar prop)
                // an item in any component of the name, since a diluent can carry the name of a
                // medication component, and map changes them all
                | Target.Item(cmp, itm, prop) ->
                    ord.Orderable.Components
                    |> Array.filter (fun c -> c.Name = cmp)
                    |> Array.tryPick (_.Items >> Array.tryFind (fun i -> i.Name = itm))
                    |> Option.map (itemVar prop)


            /// The order with the variable the target addresses changed; the order as it is when it
            /// holds no such variable. A component concentration is changed in the first component
            /// as well as the one named, as the dose dialog changes it.
            let map target f (ord: Order) =
                let orderable (o: Orderable) = { ord with Orderable = o }

                let components g =
                    orderable { ord.Orderable with Components = ord.Orderable.Components |> Array.mapi g }

                match target with
                | _ when ord |> tryGet target |> Option.isNone -> ord
                | Target.Schedule ScheduleProperty.Frequency ->
                    { ord with Schedule = { ord.Schedule with Frequency = f ord.Schedule.Frequency } }
                | Target.Schedule ScheduleProperty.Time ->
                    { ord with Schedule = { ord.Schedule with Time = f ord.Schedule.Time } }
                | Target.Orderable OrderableProperty.Quantity ->
                    orderable { ord.Orderable with OrderableQuantity = f ord.Orderable.OrderableQuantity }
                | Target.Orderable OrderableProperty.DoseQuantity ->
                    orderable
                        { ord.Orderable with
                            Dose = { ord.Orderable.Dose with Quantity = f ord.Orderable.Dose.Quantity }
                        }
                | Target.Orderable OrderableProperty.DoseRate ->
                    orderable { ord.Orderable with Dose = { ord.Orderable.Dose with Rate = f ord.Orderable.Dose.Rate } }
                | Target.Component(cmp, prop) ->
                    components (fun _ c -> if c.Name = cmp then c |> mapComponentVar prop f else c)
                | Target.Item(cmp, itm, prop) ->
                    let holds i (c: Component) =
                        c.Name = cmp || (prop = ItemProperty.ComponentConcentration && i = 0)

                    components (fun i c ->
                        if holds i c then
                            { c with
                                Items =
                                    c.Items
                                    |> Array.map (fun it -> if it.Name = itm then it |> mapItemVar prop f else it)
                            }
                        else
                            c
                    )


        /// What a user picks from: a filter field, the diluents, the components, the scenarios, or
        /// the values of an order variable.
        [<RequireQualifiedAccess>]
        type Options =
            | Filter of FilterField
            | Diluents
            | Components
            | Scenarios
            | Variable of Target


        /// The keys of the values a variable offers to pick from.
        let values (ovar: OrderVariable) =
            ovar.Variable.Vals
            |> Option.map _.Value
            |> Option.defaultValue [||]
            |> Array.map fst


        /// How many options a user picks from; 0 for a variable that offers no list.
        let count options (ctx: OrderContext) =
            match options with
            | Options.Filter Indication -> ctx.Filter.Indications.Length
            | Options.Filter Generic -> ctx.Filter.Generics.Length
            | Options.Filter Route -> ctx.Filter.Routes.Length
            | Options.Filter Form -> ctx.Filter.Forms.Length
            | Options.Filter DoseType -> ctx.Filter.DoseTypes.Length
            | Options.Diluents -> ctx.Filter.Diluents.Length
            | Options.Components -> ctx.Filter.Components.Length
            | Options.Scenarios -> ctx.Scenarios.Length
            | Options.Variable target ->
                match ctx.Scenarios with
                | [| sc |] ->
                    sc.Order
                    |> Target.tryGet target
                    |> Option.map (values >> Array.length)
                    |> Option.defaultValue 0
                | _ -> 0


        /// Variable names without the order id, so that a command plays the same in every run,
        /// and with it put back for the server.
        /// The one scenario of the context, or why there is none.
        let scenario (ctx: OrderContext) =
            match ctx.Scenarios with
            | [| sc |] -> Ok sc
            | scs -> Error $"one scenario expected, the context has %i{scs.Length}"


        /// The nth of the options, counted from 0, or why there is none.
        let nth what n (xs: 'a[]) =
            xs
            |> Array.tryItem n
            |> Option.map Ok
            |> Option.defaultValue (Error $"%s{what}: index %i{n} out of %i{xs.Length}")


        /// The context with the scenario of the order's id holding the order.
        let withOrder (ord: Order) (ctx: OrderContext) =
            { ctx with
                Scenarios =
                    ctx.Scenarios
                    |> Array.map (fun sc -> if sc.Order.Id = ord.Id then { sc with Order = ord } else sc)
            }


        /// A filter field set to its nth option, or emptied with None, as the page changes it.
        let changeFilter field (n: int option) (ctx: OrderContext) =
            let pick what xs change =
                match n with
                | None -> Ok(ctx |> change None)
                | Some n -> xs |> nth what n |> Result.map (fun x -> ctx |> change (Some x))

            match field with
            | Indication -> pick "indication" ctx.Filter.Indications indicationChange
            | Generic -> pick "generic" ctx.Filter.Generics medicationChange
            | Route -> pick "route" ctx.Filter.Routes routeChange
            | Form -> pick "form" ctx.Filter.Forms formChange
            | DoseType -> pick "dose type" ctx.Filter.DoseTypes doseTypeChange


        /// The diluent set to its nth option, or emptied with None.
        let changeDiluent (n: int option) (ctx: OrderContext) =
            match n with
            | None -> Ok(ctx |> diluentChange None)
            | Some n ->
                ctx.Filter.Diluents
                |> nth "diluent" n
                |> Result.map (fun d -> ctx |> diluentChange (Some d))


        /// The components at these positions of the options.
        let setNthComponents (ns: int[]) (ctx: OrderContext) =
            let picked = ns |> Array.map (fun n -> ctx.Filter.Components |> nth "component" n)

            match
                picked
                |> Array.choose (
                    function
                    | Error e -> Some e
                    | Ok _ -> None
                )
            with
            | [||] ->
                Ok(
                    ctx
                    |> componentsChange (
                        picked
                        |> Array.choose (
                            function
                            | Ok c -> Some c
                            | Error _ -> None
                        )
                    )
                )
            | errs -> Error errs[0]


        /// The whole filter emptied, the patient kept.
        let clearAll (ctx: OrderContext) = empty |> setPatient ctx.Patient


        /// The filter's choices set from outside its own fields, with the rule of their source. A
        /// page's choices keep the diluent and the selected components only when the choices above
        /// them are unchanged, and the scenarios go; the url's medication is written over the
        /// filter as it is, a list item's over an empty one; a reload changes nothing. The
        /// domain's seed applies the same rules; the agreement test in the server tests keeps them
        /// equal.
        let seedFilter source ind gen rte frm dt (ctx: OrderContext) =
            let write keep (ctx: OrderContext) =
                { ctx with
                    Filter =
                        { ctx.Filter with
                            Indication = ind
                            Generic = gen
                            Route = rte
                            Form = frm
                            DoseType = dt
                            Diluent = if keep then ctx.Filter.Diluent else None
                            SelectedComponents = if keep then ctx.Filter.SelectedComponents else [||]
                        }
                }

            let f = ctx.Filter
            let same = gen = f.Generic && rte = f.Route && frm = f.Form

            match source with
            | SeedSource.Formulary -> { (ctx |> write (same && ind = f.Indication)) with Scenarios = [||] }
            | SeedSource.Parenteralia -> { (ctx |> write same) with Scenarios = [||] }
            | SeedSource.Url -> ctx |> write true
            | SeedSource.MedicationList -> ctx |> clearAll |> write true
            | SeedSource.Reload -> ctx


        /// The context narrowed to its nth scenario and that scenario's form, as the page selects it.
        let selectNthScenario n (ctx: OrderContext) =
            ctx.Scenarios
            |> nth "scenario" n
            |> Result.map (fun sc ->
                { ctx with
                    Filter = { ctx.Filter with Form = Some sc.Form }
                    Scenarios = [| sc |]
                }
            )


        /// The variable set to its nth value, as the dose dialog sets it; None when it offers no
        /// list to pick from.
        let setNth target n (ctx: OrderContext) =
            scenario ctx
            |> Result.bind (fun sc ->
                match sc.Order |> Target.tryGet target with
                | None -> Error $"no %A{target} in the order"
                | Some ovar ->
                    match values ovar with
                    | [||] -> Ok None
                    | keys ->
                        keys
                        |> nth $"%A{target}" n
                        |> Result.map (fun key ->
                            let ord = sc.Order |> Target.map target (Order.OrderVariable.setOvar (Some key))
                            ctx |> withOrder ord |> Some
                        )
            )


        /// The variable the target addresses cleared: the preview of a recalculation while its
        /// request is under way.
        let reCalc target (ctx: OrderContext) =
            scenario ctx
            |> Result.bind (fun sc ->
                match sc.Order |> Target.tryGet target with
                | None -> Error $"no %A{target} in the order"
                | Some _ ->
                    Ok(
                        ctx
                        |> withOrder (sc.Order |> Target.map target (Order.OrderVariable.setOvar None))
                    )
            )


        /// The argumentation the user writes for a deviation from the rules.
        module Argumentation =

            /// The maximum length of a text, the same as the server's, so the server never refuses
            /// a text the client holds.
            let maxLength = 1000


            /// The text trimmed and cut to the maximum length; None when empty.
            let normalise (text: string) =
                match text with
                | null -> None
                | text ->
                    let text = text.Trim()

                    if text = "" then
                        None
                    elif text.Length > maxLength then
                        Some(text.Substring(0, maxLength))
                    else
                        Some text


            /// The context with the text written.
            let write (text: string) (ctx: OrderContext) = { ctx with Argumentation = normalise text }


    module OrderPlan =

        let create pat contexts : OrderPlan =
            {
                Patient = pat
                Filtered = [||]
                OrderContexts = contexts
                Totals = Totals.empty
            }


        let empty = create Patient.empty [||]


        /// The nutrition workbenches of the plan. A display copy of the domain's
        /// `OrderPlan.nutritionContexts`; the agreement test in the server tests keeps them equal.
        let nutritionContexts (plan: OrderPlan) =
            plan.OrderContexts
            |> Array.filter (OrderContext.nutritionCategory >> Option.isSome)


        /// The orders the plan's contexts contribute: the one scenario of every context narrowed
        /// to one, in context order. A display copy of the domain's `OrderPlan.orders`; the
        /// agreement test in the server tests keeps them equal.
        let orders (plan: OrderPlan) = plan.OrderContexts |> Array.choose OrderContext.contribution


        /// The contexts the filter keeps: those named by id, all of them when it is empty. A
        /// display copy of the domain's `OrderPlan.filtered`; the agreement test in the server
        /// tests keeps them equal.
        let filtered (plan: OrderPlan) =
            if plan.Filtered |> Array.isEmpty then
                plan.OrderContexts
            else
                plan.OrderContexts
                |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


        /// Whether the plan may take a context of the nutrition category, which decides the
        /// buttons the nutrition page offers: one context per category, except supplements
        /// (any number, each under a feeding) and electrolyte and glucose lines (any number).
        /// A display copy of the domain's `OrderPlan.admits`, which refuses what this hides; the
        /// agreement test in the server tests keeps them equal.
        let mayAdd (category: NutritionCategory) (plan: OrderPlan) =
            let holds c =
                plan.OrderContexts
                |> Array.exists (fun ctx -> ctx.Category = OrderCategory.Nutrition c)

            match category with
            | NutritionCategory.EnteralSupplement -> holds NutritionCategory.EnteralFeeding
            | NutritionCategory.ElectrolyteGlucose -> true
            | _ -> not (holds category)


    module Formulary =

        let empty: Formulary =
            {
                Generics = [||]
                Indications = [||]
                Routes = [||]
                Forms = [||]
                DoseTypes = [||]
                PatientCategories = [||]
                Products = [||]
                Generic = None
                Indication = None
                Route = None
                Form = None
                DoseType = None
                PatientCategory = None
                Patient = None
                Markdown = ""
                DoseCheck = [||]
            }


    module Parenteralia =

        let empty: Parenteralia =
            {
                Generics = [||]
                Forms = [||]
                Routes = [||]
                PatientCategories = [||]
                Generic = None
                Form = None
                Route = None
                PatientCategory = None
                Markdown = ""
            }
