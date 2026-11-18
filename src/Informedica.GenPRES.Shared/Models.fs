namespace Informedica.GenPRES.Shared


module Models =

    open Types


    /// The reason patient data are not valid.
    [<RequireQualifiedAccess>]
    type PatientError =
        /// Patient data without an age, a measured weight and height, or a mark as an adult.
        | NoAgeOrMeasuredWeightAndHeight


    /// Reads and changes a gestational age with the need for it that the server set.
    [<RequireQualifiedAccess>]
    module GestationalAge =

        /// Returns the gestational age, or None when none is given.
        let value ga =
            match ga with
            | GestationalAge.NoGestationalAge -> None
            | GestationalAge.OptionalGestationalAge v
            | GestationalAge.RequiredGestationalAge v -> v


        /// Applies f to the gestational age when it may be given; NoGestationalAge stays unchanged.
        let map f ga =
            match ga with
            | GestationalAge.NoGestationalAge -> GestationalAge.NoGestationalAge
            | GestationalAge.OptionalGestationalAge v -> v |> f |> GestationalAge.OptionalGestationalAge
            | GestationalAge.RequiredGestationalAge v -> v |> f |> GestationalAge.RequiredGestationalAge


        /// Whether the gestational age may be given.
        let mayBeGiven ga = ga <> GestationalAge.NoGestationalAge


        /// Whether the gestational age is needed and not given.
        let isMissing ga = ga = GestationalAge.RequiredGestationalAge None


    /// The patient data logic shared by the client and the server: the empty value, the constructors, the readers
    /// and validate. The patient data can be incomplete, so validate checks the minimum. The module
    /// calculates no age: the server sets the ages, the adult mark, the gestational age and the estimates with
    /// GenCORE. ServerApi.Patient.toCore maps the patient data to the GenCORE patient. Patient logic that only the
    /// server needs belongs in GenFORM or GenCORE.
    module PatientData =

        open System


        /// Patient data with every field empty and the patient not marked as an adult.
        let empty: PatientData =
            {
                Age = None
                GestationalAge = GestationalAge.NoGestationalAge
                PostMenstrualAge = None
                BodySurfaceArea = None
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
                AdultMark = AdultMark.MarkableAdult false
            }


        module Age =

            open PatientData

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


            let getYears { Age.Years = yrs } = yrs


            let getMonths { Age.Months = mos } = mos


            let getWeeks { Age.Weeks = ws } = ws


            let getDays { Age.Days = ds } = ds


        let apply f (p: PatientData) = f p


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


        /// Returns true when the patient is 18 years or older by the server mark, or the user marked the patient as
        /// an adult.
        let isAdult (pat: PatientData) =
            match pat.AdultMark with
            | AdultMark.Adult
            | AdultMark.MarkableAdult true -> true
            | AdultMark.NotAdult
            | AdultMark.MarkableAdult false -> false


        /// Returns the patient data when the patient is an adult, has an age, or has a measured weight and a
        /// measured height. Otherwise returns NoAgeOrMeasuredWeightAndHeight.
        let validate (pat: PatientData) =
            if
                pat |> isAdult
                || pat.Age.IsSome
                || (pat.Weight.Measured.IsSome && pat.Height.Measured.IsSome)
            then
                Ok pat
            else
                Error PatientError.NoAgeOrMeasuredWeightAndHeight


        /// Returns the measured weight, or the estimated weight when there is no measured weight.
        let getWeight (pat: PatientData) =
            if pat.Weight.Measured.IsSome then
                pat.Weight.Measured
            else
                pat.Weight.Estimated


        /// Returns the measured height, or the estimated height when there is no measured height.
        let getHeight (pat: PatientData) =
            if pat.Height.Measured.IsSome then
                pat.Height.Measured
            else
                pat.Height.Estimated


        /// Returns true when the rules can be selected for the patient: always for an adult, and for a child only
        /// with a measured or estimated weight. GenORDER and the server apply the same test.
        let selectsRules (pat: PatientData) = pat |> isAdult || pat |> getWeight |> Option.isSome


        let create years months weeks days weight height gw gd gend cvl gfr dep : PatientData option =
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

            // A gestational age is sent as optional; the server sets whether it is required.
            let ga =
                if Option.isNone gw && Option.isNone gd then
                    GestationalAge.NoGestationalAge
                else
                    {
                        PatientData.AgeWeeksDays.Weeks = gw |> Option.defaultValue 37<week>
                        PatientData.AgeWeeksDays.Days = gd |> Option.defaultValue 0<day>
                    }
                    |> Some
                    |> GestationalAge.OptionalGestationalAge

            {
                Age = a
                GestationalAge = ga
                PostMenstrualAge = None
                BodySurfaceArea = None
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
                AdultMark = AdultMark.MarkableAdult false
            }
            |> Some


        /// Sets the estimated weight and height as P3, median and P97; None clears them. The measured values do not
        /// change.
        let withEstimates
            (ew: (int<gram> * int<gram> * int<gram>) option)
            (eh: (int<cm> * int<cm> * int<cm>) option)
            (pat: PatientData)
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


    /// Calculates the bolus medication and the interventions for resuscitation. parse reads the emergencylist sheet
    /// of the emergency list workbook, which the client loads; it is not the GENPRES_URL_ID workbook. The columns
    /// are hospital, indication, medication, minWeight, maxWeight, dose, min, max, conc, unit and remark. The
    /// optional columns template-generic, template-route, template-dose-type and template-indication preselect a
    /// prescription for the entry.
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
            // Adds the other bolus medication for the weight; none without a weight.
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


    /// Continuous infusion protocols. parse reads the continuousmeds sheet of the emergency list workbook, with the
    /// columns hospital, catagory (spelled so in the sheet), indication, dosetype, medication, generic, unit,
    /// doseunit, minweight, maxweight, quantity, total, mindose, maxdose, absmax, minconc, maxconc and solution.
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


    /// Medication products with their concentrations. parse reads the products sheet of the emergency list
    /// workbook, with the columns indication, medication, conc and unit.
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


    module Order =


        module ValueUnit =

            /// Creates a ValueUnit record of the shared types.
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

            let create nme cst cal var largeIncr level =
                {
                    Name = nme
                    DefinedConstraints = cst
                    CalculatedConstraints = cal
                    Variable = var
                    LargeIncr = largeIncr
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


        let create
            ind
            nme
            frm
            rte
            dst
            dil
            cmp
            itm
            dils
            cmps
            itms
            prs
            prep
            adm
            o
            adj
            rr
            rn
            fits
            rules
            notRecalculated
            ids
            acc
            =
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
                FitsOnlyWithoutDivisible = fits
                RulesNoLongerApply = rules
                NotRecalculated = notRecalculated
                ProductIds = ids
                Access = acc
            }


    module NutritionCategory =

        /// Returns the Dutch display name of the category.
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
                DoseRuleComponents = [||]
                Indication = None
                Generic = None
                Form = None
                Route = None
                DoseType = None
                Diluent = None
                SelectedComponents = ComponentSelection.AllComponents
                PickedProducts = [||]
            }


        let empty: OrderContext =
            {
                Id = ""
                Category = OrderCategory.Medication
                DemoVersion = true
                Filter = filter
                PatientData = PatientData.empty
                Scenarios = [||]
                Totals = Totals.empty
                Argumentation = None
                LinkedTo = None
            }

        let setPatientData pat ctx : OrderContext = { ctx with PatientData = pat }


        /// Returns the nutrition category of a context, None for a medication. It copies
        /// PlanContext.nutritionCategory in GenORDER; an agreement test in the server tests keeps both equal.
        let nutritionCategory (ctx: OrderContext) =
            match ctx.Category with
            | OrderCategory.Nutrition category -> Some category
            | OrderCategory.Medication -> None


        /// Returns the scenario a context contributes to the plan when it has exactly one scenario; None otherwise.
        /// It copies PlanContext.contribution in GenORDER; an agreement test in the server tests keeps both equal.
        let contribution (ctx: OrderContext) = ctx.Scenarios |> Array.tryExactlyOne


        /// A field of the order context filter.
        type FilterField =
            | Indication
            | Generic
            | Route
            | Form
            | DoseType


        /// All filter fields, used to clear the whole filter.
        let filterFields = [ Indication; Generic; Route; Form; DoseType ]


        /// Returns the filter fields as steps in the order they are chosen. Emptying a field clears the choices in
        /// the later steps, not the choices in its own step. For a medication, the indication and the generic share
        /// the first step, because either can be chosen first and each narrows the options of the other; the route,
        /// form and dose type depend on the medication. For nutrition, the composition (the generic) comes first and
        /// the indication follows from it. A field that is not in the steps clears no other choices.
        let chain (ctx: OrderContext) =
            match ctx.Category with
            | OrderCategory.Medication -> [ [ Indication; Generic ]; [ Route ]; [ Form ]; [ DoseType ] ]
            | OrderCategory.Nutrition _ -> [ [ Generic ]; [ Indication ]; [ DoseType ] ]


        /// Clears the choice of a field and keeps its options.
        let clearChoice field (f: Filter) =
            match field with
            | Indication -> { f with Indication = None }
            | Generic -> { f with Generic = None }
            | Route -> { f with Route = None }
            | Form -> { f with Form = None }
            | DoseType -> { f with DoseType = None }


        /// Clears the choice and the options of a field.
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


        /// Clears the components of a dose rule: the additive options, the dose rule components and the component
        /// selection.
        let withoutComponents (f: Filter) =
            { f with
                Components = [||]
                DoseRuleComponents = [||]
                SelectedComponents = ComponentSelection.AllComponents
            }


        /// Returns true when any filter field has a choice.
        let anyChosen (f: Filter) =
            f.Indication.IsSome
            || f.Generic.IsSome
            || f.Route.IsSome
            || f.Form.IsSome
            || f.DoseType.IsSome


        /// Writes a change to one filter field and clears the scenarios, the picked products and the dose rule
        /// components, because these depend on the whole filter. A picked value keeps the other choices: the server
        /// narrows every option list by all choices, so the value agrees with them. When clearOwn is true, the field
        /// is emptied: its own options and the choices in the later steps of chain are cleared too. The options of
        /// those later fields stay until the server sends new ones, so the fields remain usable meanwhile. When no
        /// choice is left, all options are cleared, because they were narrowed by choices that are gone.
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
                // another dose rule has other components; the next scenario sets them again
                |> withoutComponents

            let filter =
                if filter |> anyChosen then
                    filter
                else
                    filterFields |> List.fold (fun f x -> f |> clearField x) filter

            { ctx with
                Filter = { filter with PickedProducts = [||] }
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


        /// Selects the named components; an empty array selects no optional component.
        let componentsChange (cs: string[]) (ctx: OrderContext) : OrderContext =
            { ctx with OrderContext.Filter.SelectedComponents = cs |> Array.toList |> ComponentSelection.Selected }


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


        /// Reads and changes the order variables a target addresses.
        module Target =

            /// Returns the variable of an item property.
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


            /// Applies f to the variable of an item property.
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


            /// Returns the variable of a component property.
            let componentVar prop (cmp: Component) =
                match prop with
                | ComponentProperty.OrderableQuantity -> cmp.OrderableQuantity
                | ComponentProperty.DoseQuantityAdjust -> cmp.Dose.QuantityAdjust


            /// Applies f to the variable of a component property.
            let mapComponentVar prop f (cmp: Component) =
                match prop with
                | ComponentProperty.OrderableQuantity -> { cmp with OrderableQuantity = f cmp.OrderableQuantity }
                | ComponentProperty.DoseQuantityAdjust ->
                    { cmp with Dose = { cmp.Dose with QuantityAdjust = f cmp.Dose.QuantityAdjust } }


            /// Returns the variable the target addresses in the order; None when the order has no such component or
            /// item.
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
                // Searches every component with the name, because a diluent can have the name of a medication
                // component; map changes the item in all of them.
                | Target.Item(cmp, itm, prop) ->
                    ord.Orderable.Components
                    |> Array.filter (fun c -> c.Name = cmp)
                    |> Array.tryPick (_.Items >> Array.tryFind (fun i -> i.Name = itm))
                    |> Option.map (itemVar prop)


            /// Applies f to the variable the target addresses; returns the order unchanged when it has no such
            /// variable. A component concentration is also changed in the first component, as the dose dialog does.
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


        /// Returns the keys of the values of a variable; empty when it has no values.
        let values (ovar: OrderVariable) =
            ovar.Variable.Vals
            |> Option.map _.Value
            |> Option.defaultValue [||]
            |> Array.map fst


        /// Returns the number of options. For a variable it is 0 when the variable has no values or the context does
        /// not have exactly one scenario.
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


        /// Returns the single scenario of the context, or an error with the number of scenarios.
        let scenario (ctx: OrderContext) =
            match ctx.Scenarios with
            | [| sc |] -> Ok sc
            | scs -> Error $"one scenario expected, the context has %i{scs.Length}"


        /// Returns the option at index n, counted from 0, or an error when n is out of range.
        let nth what n (xs: 'a[]) =
            xs
            |> Array.tryItem n
            |> Option.map Ok
            |> Option.defaultValue (Error $"%s{what}: index %i{n} out of %i{xs.Length}")


        /// Replaces the order in the scenario with the same order id.
        let withOrder (ord: Order) (ctx: OrderContext) =
            { ctx with
                Scenarios =
                    ctx.Scenarios
                    |> Array.map (fun sc -> if sc.Order.Id = ord.Id then { sc with Order = ord } else sc)
            }


        /// Sets a filter field to its nth option, or empties it with None. Returns an error when n is out of range.
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


        /// Sets the diluent to its nth option, or empties it with None. Returns an error when n is out of range.
        let changeDiluent (n: int option) (ctx: OrderContext) =
            match n with
            | None -> Ok(ctx |> diluentChange None)
            | Some n ->
                ctx.Filter.Diluents
                |> nth "diluent" n
                |> Result.map (fun d -> ctx |> diluentChange (Some d))


        /// Selects the components at these positions of the component options. Returns the first index error.
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


        /// Returns an empty context with the patient data of ctx.
        let clearAll (ctx: OrderContext) = empty |> setPatientData ctx.PatientData


        /// Sets the indication, generic, route, form and dose type from a source outside the filter fields.
        /// Formulary and Parenteralia keep the diluent only when the generic, route and form are the same (for
        /// Formulary also the indication), and clear the scenarios. Url writes over the current filter and keeps
        /// the diluent, MedicationList writes over an empty context, and Reload changes nothing. The selected
        /// components and picked products stay only when all five choices are the same. OrderContext.seedFilter in
        /// GenORDER applies the same rules; an agreement test in the server tests keeps both equal.
        let seedFilter source ind gen rte frm dt (ctx: OrderContext) =
            let f = ctx.Filter
            let same = gen = f.Generic && rte = f.Route && frm = f.Form
            // The same five choices select the same dose rule.
            let samePicks = same && ind = f.Indication && dt = f.DoseType

            let write keep (ctx: OrderContext) =
                let filter =
                    { ctx.Filter with
                        Indication = ind
                        Generic = gen
                        Route = rte
                        Form = frm
                        DoseType = dt
                        Diluent = if keep then ctx.Filter.Diluent else None
                        PickedProducts = if samePicks then f.PickedProducts else [||]
                    }

                // Other choices can select another dose rule with other components, so the components are cleared.
                { ctx with Filter = if samePicks then filter else filter |> withoutComponents }

            match source with
            | SeedSource.Formulary -> { (ctx |> write (same && ind = f.Indication)) with Scenarios = [||] }
            | SeedSource.Parenteralia -> { (ctx |> write same) with Scenarios = [||] }
            | SeedSource.Url -> ctx |> write true
            | SeedSource.MedicationList -> ctx |> clearAll |> write true
            | SeedSource.Reload -> ctx


        /// Narrows the context to its nth scenario and sets the filter form to the form of that scenario. Returns an
        /// error when n is out of range.
        let selectNthScenario n (ctx: OrderContext) =
            ctx.Scenarios
            |> nth "scenario" n
            |> Result.map (fun sc ->
                { ctx with
                    Filter = { ctx.Filter with Form = Some sc.Form }
                    Scenarios = [| sc |]
                }
            )


        /// Sets the variable to its nth value, as the dose dialog does. Returns None when the variable has no values,
        /// and an error without a single scenario, without the variable or for an index out of range.
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


        /// The argumentation the user writes for a deviation from the rules.
        module Argumentation =

            /// The maximum length of a text. It equals the server maximum, so the server accepts every text the
            /// client sends.
            let maxLength = 1000


            /// Trims the text and cuts it to the maximum length; None for null or an empty text.
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


            /// Writes the normalised text to the context.
            let write (text: string) (ctx: OrderContext) = { ctx with Argumentation = normalise text }


    module OrderPlan =

        let create pat contexts : OrderPlan =
            {
                PatientData = pat
                Filtered = [||]
                OrderContexts = contexts
                Totals = Totals.empty
            }


        let empty = create PatientData.empty [||]


        /// Returns the scenario of every context with exactly one scenario, in context order. It copies
        /// OrderPlan.orders in GenORDER; an agreement test in the server tests keeps both equal.
        let orders (plan: OrderPlan) = plan.OrderContexts |> Array.choose OrderContext.contribution


        /// Returns the contexts whose id is in the plan filter, or all contexts when the filter is empty. It copies
        /// OrderPlan.filtered in GenORDER; an agreement test in the server tests keeps both equal.
        let filtered (plan: OrderPlan) =
            if plan.Filtered |> Array.isEmpty then
                plan.OrderContexts
            else
                plan.OrderContexts
                |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


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
                PatientData = None
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
