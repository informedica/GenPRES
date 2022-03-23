namespace Shared


module Utils =

    open System


    module String =

        /// Apply `f` to string `s`
        let apply f (s: string) = f s

        /// Utility to enable type inference
        let get = apply id

        /// Count the number of times that a
        /// string t starts with character c
        let countFirstChar c t =
            let _, count =
                if String.IsNullOrEmpty(t) then
                    (false, 0)
                else
                    t
                    |> Seq.fold
                        (fun (flag, dec) c' ->
                            if c' = c && flag then
                                (true, dec + 1)
                            else
                                (false, dec)
                        )
                        (true, 0)

            count

        /// Check if string `s2` contains string `s1`
        let contains =
            fun (s1: string) (s2: string) -> (s2 |> get).Contains(s1)


        let toLower s = (s |> get).ToLower()


        let replace (s1: string) (s2: string) s = (s |> get).Replace(s1, s2)


        let trim (s: string) = s.Trim()


    module Math =


        let roundBy s n =
            (n / s) |> round |> double |> (fun f -> f * s)


        let roundBy0_5 = roundBy 0.5

        /// Calculates the number of decimal digits that
        /// should be shown according to a precision
        /// number n that specifies the number of non
        /// zero digits in the decimals.
        /// * 66.666 |> getPrecision 1 = 0
        /// * 6.6666 |> getPrecision 1 = 0
        /// * 0.6666 |> getPrecision 1 = 1
        /// * 0.0666 |> getPrecision 1 = 2
        /// * 0.0666 |> getPrecision 0 = 0
        /// * 0.0666 |> getPrecision 1 = 2
        /// * 0.0666 |> getPrecision 2 = 3
        /// * 0.0666 |> getPrecision 3 = 4
        /// * 6.6666 |> getPrecision 0 = 0
        /// * 6.6666 |> getPrecision 1 = 0
        /// * 6.6666 |> getPrecision 2 = 1
        /// * 6.6666 |> getPrecision 3 = 2
        /// etc
        /// If n < 0 then n = 0 is used.
        let getPrecision n f = // ToDo fix infinity case
            let n = if n < 0 then 0 else n

            if f = 0. || n = 0 then
                n
            else
                let s =
                    (f |> abs |> string).Split([| '.' |])

                // calculate number of remaining decimal digits (after '.')
                let p =
                    n - (if s.[0] = "0" then 0 else s.[0].Length)

                let p = if p < 0 then 0 else p

                if (int s.[0]) > 0 then
                    p
                else
                    // calculate the the first occurance of a non-zero decimal digit
                    let c = (s.[1] |> String.countFirstChar '0')
                    c + p

        /// Fix the precision of a float f to
        /// match a minimum of non zero digits n
        /// * 66.666 |> fixPrecision 1 = 67
        /// * 6.6666 |> fixPrecision 1 = 7
        /// * 0.6666 |> fixPrecision 1 = 0.7
        /// * 0.0666 |> fixPrecision 1 = 0.07
        /// * 0.0666 |> fixPrecision 0 = 0
        /// * 0.0666 |> fixPrecision 1 = 0.07
        /// * 0.0666 |> fixPrecision 2 = 0.067
        /// * 0.0666 |> fixPrecision 3 = 0.0666
        /// * 6.6666 |> fixPrecision 0 = 7
        /// * 6.6666 |> fixPrecision 1 = 7
        /// * 6.6666 |> fixPrecision 2 = 6.7
        /// * 6.6666 |> fixPrecision 3 = 6.67
        /// etc
        /// If n < 0 then n = 0 is used.
        let fixPrecision n (f: float) = Math.Round(f, f |> getPrecision n)


    module List =


        let create x = x :: []


        let inline findNearestMax n ns =
            match ns with
            | [] -> n
            | _ ->
                ns
                |> List.sort
                |> List.rev
                |> List.fold (fun x a -> if (a - x) < (n - x) then x else a) n


        let removeDuplicates xs =
            xs
            |> List.fold
                (fun xs x ->
                    if xs |> List.exists ((=) x) then
                        xs
                    else
                        [ x ] |> List.append xs
                )
                []


    module DateTime =


        let apply f (dt: DateTime) = f dt


        let get = apply id


        let optionToDate yr mo dy =
            match yr, mo, dy with
            | Some y, Some m, Some d -> new DateTime(y, m, d) |> Some
            | _ -> None


        let dateDiff dt1 dt2 = (dt1 |> get) - (dt2 |> get)


        let dateDiffDays dt1 dt2 = (dateDiff dt1 dt2).Days


        let dateDiffMonths dt1 dt2 =
            (dateDiffDays dt1 dt2)
            |> float
            |> (fun x -> x / 365.)
            |> ((*) 12.)


        let dateDiffYearsMonths dt1 dt2 =
            let mos = (dateDiffMonths dt1 dt2) |> int
            (mos / 12), (mos % 12)


module Route =
    let hello = "/api/hello"


module Configuration =

    open Types


    let calculateRanges dep config =
        let ranges =
            {
                Years = []
                Months = []
                Weights = []
                Heights = []
            }

        match config
              |> List.tryFind (fun s -> s.Department = dep)
            with
        | Some set ->
            { ranges with
                Years = [ set.MinAge .. set.MaxAge / 12 ]
                Months = [ 0..11 ]
                Weights =
                    [ set.MinWeight .. 0.1 .. 9.9 ]
                    @ [ 10.0..1.0..19.0 ]
                      @ [ 20.0 .. 5.0 .. set.MaxWeight ]
                Heights = [ 50..200 ]
            }
        | None -> ranges


module Patient =

    open System
    open Utils
    open Types


    module Age =

        open Patient

        let (>==) r f = Result.bind f r


        let ageZero =
            {
                Years = 0
                Months = 0
                Weeks = 0
                Days = 0
            }


        let create years months weeks days =
            {
                Years = years
                Months = months |> Option.defaultValue 0
                Weeks = weeks |> Option.defaultValue 0
                Days = days |> Option.defaultValue 0
            }


        let validateMinMax lbl min max n =
            if n >= min && n <= max then
                Result.Ok n
            else
                sprintf "%s: %i not >= %i and <= %i" lbl n min max
                |> Result.Error


        let set setter lbl min max n age : Result<Age, string> =
            n |> validateMinMax lbl min max
            >== ((setter age) >> Result.Ok)


        let setYears =
            set (fun age n -> { age with Years = n }) "Years" 0 100


        let setMonths mos age =
            age |> setYears (mos / 12)
            >== set
                (fun age n -> { age with Months = n })
                "Months"
                0
                11
                (mos % 12)


        let setWeeks wks age =
            let yrs = wks / 52
            let mos = (wks - yrs * 52) / 4
            let wks = wks - (mos * 4) - (yrs * 52)

            age |> setYears yrs
            >== set (fun age n -> { age with Months = n }) "Months" 0 12 mos
            >== set (fun age n -> { age with Weeks = n }) "Weeks" 0 4 wks


        let setDays dys age =
            let c = 356. / 12.
            let yrs = dys / 356

            let mos =
                ((float dys) - (float yrs) * 356.) / c |> int

            let wks =
                (float dys)
                - ((float mos) * c)
                - (yrs * 356 |> float)
                |> int
                |> fun x -> x / 7

            let dys =
                (float dys)
                - ((float mos) * c)
                - (yrs * 356 |> float)
                |> int
                |> fun x -> x % 7

            age |> setYears yrs
            >== set (fun age n -> { age with Months = n }) "Months" 0 12 mos
            >== set (fun age n -> { age with Weeks = n }) "Weeks" 0 4 wks
            >== set (fun age n -> { age with Days = n }) "Days" 0 6 dys


        let getYears { Age.Years = yrs } = yrs


        let getMonths { Age.Months = mos } = mos


        let getWeeks { Age.Weeks = ws } = ws


        let getDays { Age.Days = ds } = ds


        let calcYears a =
            (a |> getYears |> float)
            + ((a |> getMonths |> float) / 12.)


        let calcMonths a = (a |> getYears) * 12 + (a |> getMonths)


        let toString age =
            let plur s1 s2 n =
                if n = 1 then
                    $"{n} {s1}"
                else
                    $"{n} {s2}"

            let d = age.Days |> plur "dag" "dagen"
            let w = age.Weeks |> plur "week" "weken"
            let m = age.Months |> plur "maand" "maanden"
            let y = age.Years |> plur "jaar" "jaren"

            match age with
            | _ when age.Years = 0 && age.Months = 0 && age.Weeks = 0 -> $"{d}"
            | _ when age.Years = 0 && age.Months = 0 ->
                if age.Days = 0 then
                    $"{w}"
                else
                    $"{w} en {d}"
            | _ when age.Years = 0 ->
                match age.Weeks, age.Days with
                | ws, ds when ds > 0 && ws > 0 -> $"{m}, {w} en {d}"
                | ws, ds when ds = 0 && ws > 0 -> $"{m} en {w}"
                | ws, ds when ds > 0 && ws = 0 -> $"{m} en {d}"
                | _ -> $"{m}"
            | _ ->
                match age.Months, age.Weeks, age.Days with
                | ms, ws, ds when ms = 0 && ds > 0 && ws > 0 ->
                    $"{y}, {w} en {d}"
                | ms, ws, ds when ms = 0 && ds = 0 && ws > 0 -> $"{y} en {w}"
                | ms, ws, ds when ms = 0 && ds > 0 && ws = 0 -> $"{y} en {d}"
                | ms, ws, ds when ms > 0 && ds > 0 && ws > 0 ->
                    $"{y}, {m}, {w} en {d}"
                | ms, ws, ds when ms > 0 && ds = 0 && ws > 0 ->
                    $"{y}, {m} en {w}"
                | ms, ws, ds when ms > 0 && ds > 0 && ws = 0 ->
                    $"{y}, {m} en {d}"
                | ms, ws, ds when ms > 0 && ds = 0 && ws = 0 -> $"{y} en {m}"
                | _ -> $"{y}"



    let apply f (p: Patient) = f p


    let get = apply id


    let getAge p = (p |> get).Age


    let getAgeYears p =
        p |> getAge |> Option.map (fun a -> a.Years)


    let getAgeMonths p =
        p |> getAge |> Option.map (fun a -> a.Months)


    let getAgeWeeks p =
        p |> getAge |> Option.map (fun a -> a.Months)


    let getAgeDays p =
        p |> getAge |> Option.map (fun a -> a.Days)


    let create years months weeks days weight height =
        if [
            years
            months
            weeks
            days
            (weight |> Option.map int)
           ]
           |> List.forall Option.isNone then
            None
        else
            let a =
                if [ years; months; weeks; days ]
                   |> List.forall Option.isNone then
                    None
                else
                    { Age.ageZero with
                        Years = years |> Option.defaultValue 0
                        Months = months |> Option.defaultValue 0
                        Weeks = weeks |> Option.defaultValue 0
                        Days = days |> Option.defaultValue 0
                    }
                    |> Some

            let ew, eh =
                if a |> Option.isNone then
                    None, None
                else
                    let a =
                        ((years |> Option.defaultValue 0 |> float) * 12.)
                        + ((months |> Option.defaultValue 0 |> float) / 12.)
                        + ((weeks |> Option.defaultValue 0 |> float) / 56.)
                        + ((days |> Option.defaultValue 0 |> float) / 365.)

                    let w =
                        NormalValues.ageWeight
                        |> List.find (fun (x, _) -> x >= a)
                        |> snd
                        |> Some

                    let h =
                        NormalValues.ageHeight
                        |> List.find (fun (x, _) -> x >= a)
                        |> snd
                        |> Some

                    w, h

            {
                Age = a
                Weight = { Estimated = ew; Measured = weight }
                Height = { Estimated = eh; Measured = height }
            }
            |> Some


    let getAgeInYears p =
        p
        |> getAgeYears
        |> Option.bind (fun ys ->
            p
            |> getAgeMonths
            |> Option.bind (fun ms ->
                (ys |> float) + ((ms |> float) / 12.) |> Some
            )
        )


    let getAgeInMonths p =
        p
        |> getAgeInYears
        |> Option.bind (fun ys -> ys * 12. |> Some)

    /// Get either the measured weight or the
    /// estimated weight if measured weight = 0
    let getWeight pat =
        if pat.Weight.Measured.IsSome then
            pat.Weight.Measured
        else
            pat.Weight.Estimated

    /// Get either the measured height or the
    /// estimated height if measured weight = 0
    let getHeight pat =
        if pat.Height.Measured.IsSome then
            pat.Height.Measured
        else
            pat.Height.Estimated


    let updateWeightGram gr pat =
        let kg = gr / 1000.

        { (pat |> get) with
            Weight =
                { pat.Weight with
                    Measured = kg |> Some
                }
        }


    let calcBMI pat =
        match pat.Weight.Measured,
              pat.Weight.Estimated,
              pat.Height.Measured,
              pat.Height.Estimated
            with
        | Some w, _, Some h, _
        | None, Some w, None, Some h ->
            if h > 0. then
                (w / (h ** 2.)) |> Some
            else
                None
        | _ -> None


    let calcBSA pat =
        match pat.Weight.Measured,
              pat.Weight.Estimated,
              pat.Height.Measured,
              pat.Height.Estimated
            with
        | None, None, _, _
        | _, _, None, None -> None

        | Some w, _, Some h, _
        | Some w, _, None, Some h
        | None, Some w, Some h, _
        | None, Some w, None, Some h ->
            sqrt (w * ((h |> float)) / 3600.)
            |> Math.fixPrecision 1
            |> Some


    let calcNormalFluid pat =
        let a = pat |> getAge
        a


    let toString markDown pat =
        let toStr s n = n |> Option.map (sprintf "%s%.1f" s)

        let bold s =
            s
            |> Option.map (fun s -> if markDown then $"**{s}**" else s)

        [
            (Some "Leeftijd:") |> bold
            pat.Age
            |> Option.map Age.toString
            |> Option.orElse (Some "onbekend")

            (Some "Gewicht:") |> bold
            pat.Weight.Measured
            |> toStr ""
            |> Option.map (fun s -> $"{s} kg")
            pat.Weight.Estimated
            |> toStr "geschat: "
            |> Option.map (fun s -> $"({s} kg)")


            (Some "Lengte:") |> bold
            pat.Height.Measured
            |> toStr ""
            |> Option.map (fun s -> $"{s} cm")
            pat.Height.Estimated
            |> toStr "geschat: "
            |> Option.map (fun s -> $"({s} cm)")


            (Some "BSA") |> bold
            pat |> calcBSA |> Option.map (fun x -> $" {x} m2")
        ]
        |> List.map (Option.defaultValue "")
        |> String.concat " "
        |> String.replace "  " " "


module Intervention =

    open Types


    let emptyIntervention =
        {
            Indication= ""
            Name= ""
            Quantity= None
            QuantityUnit= ""
            Solution= ""
            Total= None
            TotalUnit= ""
            SubstanceDose= None
            SubstanceMinDose= None
            SubstanceMaxDose= None
            SubstanceDoseUnit= ""
            SubstanceDoseAdjust= None
            SubstanceNormDoseAdjust= None
            SubstanceMinDoseAdjust= None
            SubstanceMaxDoseAdjust= None
            SubstanceDoseAdjustUnit= ""
            SubstanceDoseText= ""
            InterventionDose= None
            InterventionDoseUnit= ""
            InterventionDoseText = ""
            Text = ""
        }


module EmergencyTreatment =

    open Types
    open Utils


    let calcDoseVol kg doserPerKg conc min max =
        let d =
            kg * doserPerKg
            |> (fun d ->
                if max > 0. && d > max then max
                else if min > 0. && d < min then min
                else d
            )

        let v =
            d / conc
            |> (fun v ->
                if v >= 10. then
                    v |> Math.roundBy 1.
                else
                    v |> Math.roundBy 0.1
            )
            |> Math.fixPrecision 2

        (v * conc |> Math.fixPrecision 2, v)


    let age ageInMo = (ageInMo |> float) / 12.


    let calculateIntervention formula textfn a =
        let m = formula a

        { Intervention.emptyIntervention with
            InterventionDose = Some m
            Text = textfn m
        }


    let tube =
        let textfn m =sprintf "%A-%A-%A" (m - 0.5) m (m + 0.5)
        let formula age =
            4. + age / 4.
            |> Math.roundBy0_5
            |> (fun m -> if m > 7. then 7. else m)
        calculateIntervention formula textfn


    let oral =
        let formula age = 12. + age / 2. |> Math.roundBy0_5
        let textfn m = sprintf "%A cm" m
        calculateIntervention formula textfn


    let nasal =
        let formula age = 15. + age / 2. |> Math.roundBy0_5
        let textfn m = sprintf "%A cm" m
        calculateIntervention formula textfn


    let epiIv wght =
        let d, v =
            calcDoseVol wght 0.01 0.1 0.01 0.5
        {
            Intervention.emptyIntervention with
                SubstanceDose = Some d
                SubstanceDoseUnit = "mg"
                SubstanceDoseText = sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 1)
                Text =
                    sprintf
                        "%A ml van 0,1 mg/ml (1:10.000)"
                        v

        }


    let epiIv2 wght =
        let d, v =
            calcDoseVol wght 0.01 0.1 0.01 0.5
        {
            Intervention.emptyIntervention with
                SubstanceDose = Some d
                SubstanceDoseUnit = "mg"
                SubstanceDoseText = sprintf "%A mg (%A mg/kg)" d (d / wght |> Math.fixPrecision 1)
                Text =
                    sprintf
                        "%A ml van 0,1 mg/ml (1:10.000)"
                        (v / 10. |> Math.fixPrecision 2)

        }


    let fluid wght =
        let d, _ = calcDoseVol wght 10. 1. 0. 500.
        { Intervention.emptyIntervention with
            SubstanceDose = Some d
            SubstanceDoseUnit = "ml"
            SubstanceDoseText = (sprintf "%A ml NaCl 0.9%%" d)
        }


    let defib joules wght =
        let j =
            joules |> List.findNearestMax (wght * 4.)
        { Intervention.emptyIntervention with
            SubstanceDose = Some j
            SubstanceDoseText = sprintf "%A Joule" j
        }


    let cardio joules wght =
        let j =
            joules |> List.findNearestMax (wght * 2.)
        { Intervention.emptyIntervention with
            SubstanceDose = Some j
            SubstanceDoseText = sprintf "%A Joule" j
        }



    let calcIntervention wght (bolus: BolusMedication) =
        let d, v =
            calcDoseVol
                wght
                bolus.NormDose
                bolus.Concentration
                bolus.MinDose
                bolus.MaxDose

        let adv s =
            if s <> "" then
                s
            else
                let minmax =
                    match (bolus.MinDose = 0., bolus.MaxDose = 0.) with
                    | true, true -> ""
                    | true, false ->
                        sprintf "(max %A %s)" bolus.MaxDose bolus.Unit
                    | false, true ->
                        sprintf "(min %A %s)" bolus.MinDose bolus.Unit
                    | false, false ->
                        sprintf
                            "(%A - %A %s)"
                            bolus.MinDose
                            bolus.MaxDose
                            bolus.Unit

                sprintf "%A %s/kg %s" bolus.NormDose bolus.Unit minmax

        { Intervention.emptyIntervention with
            Indication = bolus.Indication
            Name = bolus.Generic
            SubstanceDoseText =
                sprintf
                    "%A %s (%A %s/kg)"
                    d
                    bolus.Unit
                    (d / wght |> Math.fixPrecision 1)
                    bolus.Unit
            InterventionDoseText = sprintf "%A ml van %A %s/ml" v bolus.Concentration bolus.Unit
            Text = adv bolus.Remark
        }

    let joules =
        [
            1
            2
            3
            5
            7
            10
            20
            30
            50
            70
            100
            150
        ]
        |> List.map float


    let createBolus indication medication dose min max conc unit remark =
        {
            Indication = indication
            Generic = medication
            NormDose = dose
            MinDose = min
            MaxDose = max
            Concentration = conc
            Unit = unit
            Remark = remark
        }


    let getBolusMed (data : string [][]) =
        match data with
        | data when data |> Array.length > 1 ->
            let cms = data |> Array.head

            data
            |> Array.skip 1
            |> Array.map (fun sl ->
                let getString n =
                    Csv.getStringColumn cms sl n |> String.trim

                let getFloat = Csv.getFloatColumn cms sl

                createBolus
                    (getString "indication")
                    (getString "medication")
                    (getFloat "dose")
                    (getFloat "min")
                    (getFloat "max")
                    (getFloat "conc")
                    (getString "unit")
                    (getString "remark")
            )
            |> Array.toList
        | _ -> []


    let getTableData2 age (weight : float option) (bolusMed: BolusMedication list) =
        [
            if age |> Option.isSome then
                {
                    (tube age.Value) with
                        Indication = "reanimatie"
                        Name = "tube maat"
                        SubstanceDoseText = "4 + leeftijd / 4"
                }
            if age |> Option.isSome then
                { (oral age.Value) with
                    Indication = "reanimatie"
                    Name = "tube lengte oraal"
                    Text = "12 + leeftijd / 2"
                }
            if age |> Option.isSome then
                { (nasal age.Value) with
                    Indication = "reanimatie"
                    Name = "tube lengte nasaal"
                    SubstanceDoseText = "15 + leeftijd / 2"

                }
            if weight |> Option.isSome then
                { (epiIv weight.Value) with
                    Indication = "reanimatie"
                    Name = "adrenaline iv/io"
                    SubstanceDoseText = "0,01 mg/kg"
                }
            if weight |> Option.isSome then
                { (epiIv2 weight.Value) with
                    Indication = "reanimatie"
                    Name = "adrenaline iv/io"
                    SubstanceDoseText = "0,01 mg/kg"
                }
            if weight |> Option.isSome then
                { (fluid weight.Value) with
                    Indication = "reanimatie"
                    Name = "vaatvulling"
                    SubstanceDoseText = "10 ml/kg"
                }
            if weight |> Option.isSome then
                { (defib joules weight.Value) with
                    Indication = "reanimatie"
                    Name = "defibrillatie"
                }
            if weight |> Option.isSome then
                { (cardio joules weight.Value)  with
                    Indication = "reanimatie"
                    Name = "cardioversie"
                }
        ]
        |> fun xs ->
            if weight.IsNone then
                []
            else
                bolusMed |> List.map (calcIntervention weight.Value)
            |> List.append xs


module ContinuousMedication =

    open Utils
    open Types
    open Shared


    let create
        indication
        medication
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
            Indication = indication
            Generic = medication
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


    let parse (data : string[][]) =
        match data with
        | data when data |> Array.length > 1 ->
            let cms = data |> Array.head

            data
            |> Array.skip 1
            |> Array.map (fun sl ->
                let getString n =
                    Csv.getStringColumn cms sl n |> String.trim

                let getFloat = Csv.getFloatColumn cms sl

                create
                    (getString "indication")
                    (getString "medication")
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


    let calculate wght (contMeds: ContinuousMedication list) =

        let calcDose qty vol unit doseU =
            let f =
                let t =
                    match doseU with
                    | _ when doseU |> String.contains "dag" -> 24.
                    | _ when doseU |> String.contains "min" -> 1. / 60.
                    | _ -> 1.

                let u =
                    match unit, doseU with
                    | _ when unit = "mg" && doseU |> String.contains "microg" ->
                        1000.
                    | _ when unit = "mg" && doseU |> String.contains "nanog" ->
                        1000. * 1000.
                    | _ -> 1.

                1. * t * u

            let d =
                (f * qty / vol / wght) |> Math.fixPrecision 2

            d, doseU


        let printAdv min max unit = sprintf "%A - %A %s" min max unit

        contMeds
        |> List.filter (fun m ->
            m.MinWeight <= wght
            && (wght < m.MaxWeight || m.MaxWeight = 0.)
        )
        |> List.sortBy (fun med -> med.Indication, med.Generic)
        |> List.collect (fun med ->
            let vol = med.Total
            let qty = med.Quantity

            if vol = 0. then
                []
            else
                let d, u = calcDose qty vol med.Unit med.DoseUnit
                [
                    { Intervention.emptyIntervention with
                        Indication = med.Indication
                        Name = med.Generic
                        Quantity = Some qty
                        QuantityUnit = med.Unit
                        Total = Some vol
                        TotalUnit = "ml"
                        Solution = med.Solution
                        InterventionDoseText =
                                $"1 ml/uur = {d} {u}"
                        SubstanceDoseText = (printAdv med.MinDose med.MaxDose med.DoseUnit)
                    }
                ]
        )
