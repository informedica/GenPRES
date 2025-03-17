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
                        (fun (flag, dec) c' -> if c' = c && flag then (true, dec + 1) else (false, dec))
                        (true, 0)

            count

        /// Check if string `s2` contains string `s1`
        let contains = fun (s1: string) (s2: string) -> (s2 |> get).Contains(s1)


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
                let s = (f |> abs |> string).Split([| '.' |])

                // calculate number of remaining decimal digits (after '.')
                let p = n - (if s[0] = "0" then 0 else s[0].Length)

                let p = if p < 0 then 0 else p

                if (int s[0]) > 0 then
                    p
                else
                    // calculate the the first occurance of a non-zero decimal digit
                    let c = (s[1] |> String.countFirstChar '0')
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
                let n = if n > (ns |> List.max) then ns |> List.max else n

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


        let optionToDate (yr: int option) mo dy =
            match yr, mo, dy with
            | Some y, Some m, Some d -> DateTime(y, m, d) |> Some
            | _ -> None


        let dateDiff dt1 dt2 = (dt1 |> get) - (dt2 |> get)


        let dateDiffDays dt1 dt2 = (dateDiff dt1 dt2).Days


        let dateDiffMonths dt1 dt2 =
            (dateDiffDays dt1 dt2) |> float |> (fun x -> x / 365.) |> ((*) 12.)


        let dateDiffYearsMonths dt1 dt2 =
            let mos = (dateDiffMonths dt1 dt2) |> int
            (mos / 12), (mos % 12)


module Models =

    open Types

    module Patient =

        open System
        open Utils


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
                    yrs
                    (if mos > 0 then Some mos else None)
                    (if wks > 0 then Some wks else None)
                    (if dys > 0 then Some dys else None)


            let fromBirthDate (now: DateTime) (bdt: DateTime) =
                if bdt > now then
                    failwith $"birthdate: {bdt} cannot be after current date: {now}"
                // calculated last birthdate and number of years ago
                let last, yrs =
                    // set day one day back if not a leap year and birthdate is at Feb 29 in a leap year
                    let day =
                        if (bdt.Month = 2 && bdt.Day = 29) |> not then bdt.Day
                        else if DateTime.IsLeapYear(now.Year) then bdt.Day
                        else bdt.Day - 1

                    if now.Year - bdt.Year <= 0 then
                        bdt, 0
                    else
                        let cur = DateTime(now.Year, bdt.Month, day)

                        if cur <= now then
                            cur, cur.Year - bdt.Year
                        else
                            cur.AddYears(-1), cur.Year - bdt.Year - 1
                // printfn $"last birthdate: {last|> printDate}"
                // calculate number of months since last birth date
                let mos =
                    [ 1..11 ]
                    |> List.fold
                        (fun (mos, n) _ ->
                            let n = n + 1
                            // printfn $"folding: {last.AddMonths(n) |> printDate}, {mos}"
                            if last.AddMonths(n) <= now then mos + 1, n else mos, n
                        )
                        (0, 0)
                    |> fst

                let last = last.AddMonths(mos)
                // calculate number of days
                let days =
                    if now.Day >= last.Day && now.Month = last.Month then
                        now.Day - last.Day
                    else
                        DateTime.DaysInMonth(last.Year, last.Month) - last.Day + now.Day

                create yrs (Some mos) (Some(days / 7)) (Some(days - 7 * (days / 7)))


            let validateMinMax lbl min max n =
                if n >= min && n <= max then
                    Result.Ok n
                else
                    $"%s{lbl}: %i{n} not >= %i{min} and <= %i{max}" |> Result.Error


            let set setter lbl min max n age : Result<Age, string> =
                n |> validateMinMax lbl min max >== ((setter age) >> Result.Ok)


            let setYears = set (fun age n -> { age with Years = n }) "Years" 0 100


            let setMonths mos age =
                age |> setYears (mos / 12)
                >== set (fun age n -> { age with Months = n }) "Months" 0 11 (mos % 12)


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

                let mos = ((float dys) - (float yrs) * 356.) / c |> int

                let wks =
                    (float dys) - ((float mos) * c) - (yrs * 356 |> float)
                    |> int
                    |> fun x -> x / 7

                let dys =
                    (float dys) - ((float mos) * c) - (yrs * 356 |> float)
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
                (a |> getYears |> float) + ((a |> getMonths |> float) / 12.)


            let calcMonths a = (a |> getYears) * 12 + (a |> getMonths)

            let gestAgeToString terms lang (age: GestationalAge) =
                let getTerm = Localization.getTerm terms

                $"""
    {age.Weeks} {getTerm lang Terms.``Patient Age weeks``} {age.Days} {getTerm lang Terms.``Patient Age days``}
                """


            let toString terms lang (age: Age) =
                let getTerm = Localization.getTerm terms lang

                let plur s1 s2 n =
                    if n = 1 then $"{n} {s1}" else $"{n} {s2}"

                let d =
                    age.Days
                    |> plur (getTerm Terms.``Patient Age day``) (getTerm Terms.``Patient Age days``)

                let w =
                    age.Weeks
                    |> plur (getTerm Terms.``Patient Age week``) (getTerm Terms.``Patient Age weeks``)

                let m =
                    age.Months
                    |> plur (getTerm Terms.``Patient Age month``) (getTerm Terms.``Patient Age months``)

                let y =
                    age.Years
                    |> plur (getTerm Terms.``Patient Age year``) (getTerm Terms.``Patient Age years``)

                match age with
                | _ when age.Years = 0 && age.Months = 0 && age.Weeks = 0 -> $"{d}"
                | _ when age.Years = 0 && age.Months = 0 -> if age.Days = 0 then $"{w}" else $"{w} en {d}"
                | _ when age.Years = 0 ->
                    match age.Weeks, age.Days with
                    | ws, ds when ds > 0 && ws > 0 -> $"{m}, {w} en {d}"
                    | ws, ds when ds = 0 && ws > 0 -> $"{m}, {w}"
                    | ws, ds when ds > 0 && ws = 0 -> $"{m}, {d}"
                    | _ -> $"{m}"
                | _ ->
                    match age.Months, age.Weeks, age.Days with
                    | ms, ws, ds when ms = 0 && ds > 0 && ws > 0 -> $"{y}, {w}, {d}"
                    | ms, ws, ds when ms = 0 && ds = 0 && ws > 0 -> $"{y}, {w}"
                    | ms, ws, ds when ms = 0 && ds > 0 && ws = 0 -> $"{y}, {d}"
                    | ms, ws, ds when ms > 0 && ds > 0 && ws > 0 -> $"{y}, {m}, {w}, {d}"
                    | ms, ws, ds when ms > 0 && ds = 0 && ws > 0 -> $"{y}, {m}, {w}"
                    | ms, ws, ds when ms > 0 && ds > 0 && ws = 0 -> $"{y}, {m}, {d}"
                    | ms, ws, ds when ms > 0 && ds = 0 && ws = 0 -> $"{y}, {m}"
                    | _ -> $"{y}"


        module RenalFunction =

            let options =
                [|
                    "> 50 mL/min/1,73 m2"
                    "30 - 50 mL/min/1,73 m2"
                    "10 - 30 mL/min/1,73 m2"
                    "< 10 mL/min/1,73 m2"
                    "Intermitterende Hemodialyse"
                    "Continue Hemodialyse"
                    "Peritioneaal dialyse"
                |]


            let renalToOption =
                function
                | EGFR(min, max) ->
                    match min, max with
                    | _, Some max when max <= 10 -> options[3]
                    | _, Some max when max <= 30 -> options[2]
                    | _, Some max when max <= 50 -> options[1]
                    | _ -> options[0]
                | IntermittendHemoDialysis -> options[4]
                | ContinuousHemoDialysis -> options[5]
                | PeritionealDialysis -> options[6]


            let optionToRenal s =
                match s with
                | s when s = options[1] -> EGFR(Some 30, Some 50)
                | s when s = options[2] -> EGFR(Some 10, Some 30)
                | s when s = options[3] -> EGFR(None, Some 10)
                | s when s = options[4] -> IntermittendHemoDialysis
                | s when s = options[5] -> ContinuousHemoDialysis
                | s when s = options[6] -> PeritionealDialysis
                | _ -> EGFR(Some 50, None)


        let empty =
            {
                Age = None
                GestationalAge = None
                Weight = { Estimated = None; Measured = None }
                Height = { Estimated = None; Measured = None }
                Gender = UnknownGender
                Access = []
                RenalFunction = None
                Department = None
            }


        let apply f (p: Patient) = f p


        let get = apply id


        let getAge p = (p |> get).Age


        let getAgeYears p = p |> getAge |> Option.map _.Years


        let getAgeMonths p = p |> getAge |> Option.map _.Months


        let getAgeWeeks p = p |> getAge |> Option.map _.Weeks


        let getAgeDays p = p |> getAge |> Option.map _.Days


        let getGAWeeks (p: Patient) = p.GestationalAge |> Option.map _.Weeks


        let getGADays (p: Patient) = p.GestationalAge |> Option.map _.Days


        let getRenalFunction (p: Patient) =
            p.RenalFunction |> Option.map RenalFunction.renalToOption


        let create years months weeks days weight height gw gd gend cvl gfr dep : Patient option =
            if
                [ years; months; weeks; days; (weight |> Option.map int) ]
                |> List.forall Option.isNone
            then
                None
            else
                let a =
                    if [ years; months; weeks; days ] |> List.forall Option.isNone then
                        None
                    else
                        { Age.ageZero with
                            Age.Years = years |> Option.defaultValue 0
                            Months = months |> Option.defaultValue 0
                            Weeks = weeks |> Option.defaultValue 0
                            Days = days |> Option.defaultValue 0
                        }
                        |> Some

                let ga =
                    if [ gw; gd ] |> List.forall Option.isNone then
                        None
                    else
                        {
                            Patient.GestationalAge.Weeks = gw |> Option.defaultValue 37
                            Patient.GestationalAge.Days = gd |> Option.defaultValue 0
                        }
                        |> Some

                let ew, eh =
                    if a |> Option.isNone || gw |> Option.defaultValue 37 < 37 then
                        None, None
                    else
                        let a =
                            (years |> Option.defaultValue 0 |> float) * 12.
                            + (months |> Option.defaultValue 0 |> float) / 1.
                            + (weeks |> Option.defaultValue 0 |> float) / 4.
                            + (days |> Option.defaultValue 0 |> float) / 30.

                        let w =
                            NormalValues.ageWeight
                            |> List.rev
                            |> List.tryFind (fun (x, _) -> x <= a)
                            |> Option.map snd
                            |> Option.map (Math.fixPrecision 2)
                            |> Option.map (fun x -> x * 1000.)

                        let h =
                            NormalValues.ageHeight
                            |> List.rev
                            |> List.tryFind (fun (x, _) -> x <= a)
                            |> Option.map snd

                        w, h

                let weight =
                    if weight |> Option.isSome then
                        weight
                    else
                        ew |> Option.map (Math.fixPrecision 2) |> Option.map int

                let height =
                    if height |> Option.isSome then
                        height
                    else
                        eh |> Option.map (Math.fixPrecision 0) |> Option.map int

                {
                    Age = a
                    GestationalAge = ga
                    Weight =
                        {
                            Estimated = ew |> Option.map int
                            Measured = weight
                        }
                    Height =
                        {
                            Estimated = eh |> Option.map int
                            Measured = height
                        }
                    Gender = gend
                    Access = cvl
                    RenalFunction = gfr
                    Department = dep
                }
                |> Some


        let getAgeInYears p =
            [
                p |> getAgeYears |> Option.map float
                p |> getAgeMonths |> Option.map (fun ms -> (ms |> float) / 12.)
                p |> getAgeWeeks |> Option.map (fun ws -> (ws |> float) / 52.)
                p |> getAgeDays |> Option.map (fun ds -> (ds |> float) / 365.)
            ]
            |> List.choose id
            |> function
                | [] -> None
                | xs -> xs |> List.sum |> Some


        let getAgeInMonths p =
            [
                p |> getAgeYears |> Option.map (fun ys -> (ys |> float) * 12.)
                p |> getAgeMonths |> Option.map (fun ms -> (ms |> float) / 1.)
                p |> getAgeWeeks |> Option.map (fun ws -> (ws |> float) / 4.)
                p |> getAgeDays |> Option.map (fun ds -> (ds |> float) / 30.)
            ]
            |> List.choose id
            |> function
                | [] -> None
                | xs -> xs |> List.sum |> Some


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
            p.GestationalAge |> Option.map (fun ga -> ga.Weeks * 7 + ga.Days)


        /// Get either the measured weight or the
        /// estimated weight if measured weight = 0
        let getWeight (pat: Patient) =
            if pat.Weight.Measured.IsSome then
                pat.Weight.Measured
            else
                pat.Weight.Estimated


        let getWeightInKg (pat: Patient) =
            pat |> getWeight |> Option.map (fun x -> float x / 1000.)


        /// Get either the measured height or the
        /// estimated height if measured weight = 0
        let getHeight (pat: Patient) =
            if pat.Height.Measured.IsSome then
                pat.Height.Measured
            else
                pat.Height.Estimated


        let updateWeightGram gr pat =

            { (pat |> get) with
                Weight =
                    { pat.Weight with
                        Measured = gr |> Some
                    }
            }


        let calcBMI (pat: Patient) =
            match pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Measured, pat.Height.Estimated with
            | Some w, _, Some h, _
            | None, Some w, None, Some h ->
                if h > 0 then
                    float w / 1000. / float h ** 2. |> Some
                else
                    None
            | _ -> None


        let calcBSA (pat: Patient) =
            match pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Measured, pat.Height.Estimated with
            | None, None, _, _
            | _, _, None, None -> None

            | Some w, _, Some h, _
            | Some w, _, None, Some h
            | None, Some w, Some h, _
            | None, Some w, None, Some h -> sqrt (float w * (h |> float) / 3600.) |> Math.fixPrecision 2 |> Some


        let calcNormalFluid pat =
            let a = pat |> getAge
            a


        let toString terms lang markDown (pat: Patient) =
            let getTerm = Localization.getTerm terms lang
            let toStr s n = n |> Option.map (sprintf "%s%.1f" s)

            let bold s =
                s |> Option.map (fun s -> if markDown then $"**{s}**" else s)

            let italic s =
                s |> Option.map (fun s -> if markDown then $"*{s}*" else s)

            [
                (Some $"{Terms.``Patient Age`` |> getTerm}:") |> italic

                pat.Age
                |> Option.map (Age.toString terms lang)
                |> bold
                |> Option.orElse ("" |> Some)

                (Some $"{Terms.``Patient Weight`` |> getTerm}:") |> italic

                pat.Weight.Measured
                |> Option.map (fun x -> float x / 1000.)
                |> toStr ""
                |> Option.map (fun s -> $"{s} kg")
                |> bold

                pat.Weight.Estimated
                |> Option.map (fun x -> float x / 1000.)
                |> toStr $"{Terms.``Patient Estimated`` |> getTerm}: "
                |> Option.map (fun s -> $"({s} kg)")


                (Some $"{Terms.``Patient Length`` |> getTerm}:") |> italic

                pat.Height.Measured
                |> Option.map float
                |> toStr ""
                |> Option.map (fun s -> $"{s} cm")
                |> bold

                pat.Height.Estimated
                |> Option.map float
                |> toStr $"{Terms.``Patient Estimated`` |> getTerm}: "
                |> Option.map (fun s -> $"({s} cm)")


                (Some "BSA:") |> italic
                pat |> calcBSA |> Option.map (fun x -> $"{x} m2") |> bold

                if
                    pat
                    |> getAgeInDays
                    |> Option.map (fun ds -> ds < 365.)
                    |> Option.defaultValue false
                then
                    (Some $", {Terms.``Patient GA Age`` |> getTerm}:") |> italic

                    pat.GestationalAge
                    |> Option.map (Age.gestAgeToString terms lang)
                    |> Option.orElse ("" |> Some)

                if pat.RenalFunction |> Option.isSome then
                    Some "Nierfunctie:" |> italic
                    pat.RenalFunction |> Option.map RenalFunction.renalToOption |> bold

            ]
            |> List.choose id
            |> String.concat " "
            |> String.replace "  " " "


    module Intervention =


        let emptyIntervention =
            {
                Hospital = ""
                Indication = ""
                Name = ""
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


    module EmergencyTreatment =

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


        let ageInMoToYrs ageInMo = (ageInMo |> float) / 12.


        let calcIntervention hosp indication name text formula doseTextFn a =
            let m = formula a

            { Intervention.emptyIntervention with
                Hospital = hosp
                Indication = indication
                Name = name
                InterventionDose = Some m
                SubstanceDoseText = doseTextFn m
                Text = text
            }


        let calcTube s =
            let textfn m =
                $"%.1f{m - 0.5} - %.1f{m} - %.1f{m + 0.5}"

            let formula age =
                s + age / 4. |> Math.roundBy0_5 |> (fun m -> if m > 7. then 7. else m)

            calcIntervention
                ""
                "reanimatie"
                $"""tube ({if s = 3.5 then "met" else "zonder"} cuff) maat"""
                $"%A{s} + leeftijd / 4"
                formula
                textfn

        let calcOralLength =
            let formula age = 12. + age / 2. |> Math.roundBy0_5
            let textfn m = $"%A{m} cm"

            calcIntervention "" "reanimatie" "tube lengte oraal" "12 + leeftijd / 2" formula textfn


        let calcNasalLength =
            let formula age = 15. + age / 2. |> Math.roundBy0_5
            let textfn m = $"%A{m} cm"

            calcIntervention "" "reanimatie" "tube lengte nasaal" "15 + leeftijd / 2" formula textfn


        let calcFluidBolus wght =
            let d, _ = calcDoseVol wght 10. 1. 0. 500.

            { Intervention.emptyIntervention with
                Indication = "reanimatie"
                Name = "vaatvulling"
                SubstanceDose = Some d
                SubstanceDoseUnit = "ml"
                SubstanceMaxDose = Some 500.
                SubstanceDoseText = (sprintf "%A ml NaCl 0.9%%" d)
                SubstanceDoseAdjust = d / wght |> Math.fixPrecision 1 |> Some
                SubstanceDoseAdjustUnit = "ml/kg"
                Text = "10 ml/kg (max 500 ml)"
            }


        let joules = [ 1; 2; 3; 5; 7; 10; 20; 30; 50; 70; 100; 150 ] |> List.map float


        let calcDefib =
            let formula wght =
                joules |> List.findNearestMax (wght * 4.)

            let textfn m = $"{m} joule"

            calcIntervention "" "reanimatie" "defibrillatie" "4 joule/kg" formula textfn


        let calcCardioVersion =
            let formula wght =
                joules |> List.findNearestMax (wght * 2.)

            let textfn m = $"{m} joule"
            calcIntervention "" "reanimatie" "cardioversie" "2 joule/kg" formula textfn


        let calcBolusMedication wght (bolus: BolusMedication) =
            let d, v, c =
                let d, v =
                    calcDoseVol wght bolus.NormDose bolus.Concentration bolus.MinDose bolus.MaxDose

                if d > 0. then
                    (d, v, bolus.Concentration)
                else
                    calcDoseVol wght bolus.NormDose (bolus.Concentration / 10.) bolus.MinDose bolus.MaxDose
                    |> fun (d, v) -> d, v, (bolus.Concentration / 10.)

            let adv s =
                if s <> "" then
                    s
                else
                    let minmax =
                        match (bolus.MinDose = 0., bolus.MaxDose = 0.) with
                        | true, true -> ""
                        | true, false -> $"(max %A{bolus.MaxDose} %s{bolus.Unit})"
                        | false, true -> $"(min %A{bolus.MinDose} %s{bolus.Unit})"
                        | false, false -> $"(%A{bolus.MinDose} - %A{bolus.MaxDose} %s{bolus.Unit})"

                    $"%A{bolus.NormDose} %s{bolus.Unit}/kg %s{minmax}"

            { Intervention.emptyIntervention with
                Hospital = bolus.Hospital
                Indication = bolus.Indication
                Name = bolus.Generic
                Quantity = Some(c)
                QuantityUnit = bolus.Unit
                TotalUnit = "ml"
                InterventionDose = Some v
                InterventionDoseUnit = "ml"
                InterventionDoseText = $"{v} ml van {c} {bolus.Unit}/ml"
                SubstanceDose = Some d
                SubstanceMinDose = if bolus.MinDose = 0. then None else Some bolus.MinDose
                SubstanceMaxDose = if bolus.MaxDose = 0. then None else Some bolus.MaxDose
                SubstanceDoseUnit = bolus.Unit
                SubstanceDoseAdjust = Some(d / wght |> Math.fixPrecision 1)
                SubstanceNormDoseAdjust = Some bolus.NormDose
                SubstanceDoseAdjustUnit = $"{bolus.Unit}/kg"
                SubstanceDoseText = $"{d} {bolus.Unit} ({d / wght |> Math.fixPrecision 1} {bolus.Unit}/kg)"
                Text = adv bolus.Remark
            }


        let createBolus hosp indication medication dose min max conc unit remark =
            {
                Hospital = hosp
                Indication = indication
                Generic = medication
                NormDose = dose
                MinDose = min
                MaxDose = max
                Concentration = conc
                Unit = unit
                Remark = remark
            }


        let parse (data: string[][]) =
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
                        (getString "hospital")
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


        let calculate age weight (bolusMed: BolusMedication list) =
            if weight |> Option.isSome && weight.Value < 3. then
                [
                    calcIntervention
                        ""
                        "reanimatie"
                        "tube maat"
                        "< 1 kg: 2.5, 1-3 kg: 3.0"
                        (fun w -> if w < 1. then 2.5 else 3)
                        (fun f -> $"%.1f{f}")
                        weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "tube lengte oraal"
                        "6.632 + 1.822 x ln(kg)"
                        (fun w -> 6.632 + 1.822 * System.Math.Log(w))
                        (fun f -> $"%.1f{f} cm")
                        weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "tube lengte nasaal"
                        "(45 + 1.15 x \u221A (gram)) / 10"
                        (fun w -> (45. + 1.15 * System.Math.Sqrt(w * 1000.)) / 10.)
                        (fun f -> $"%.1f{f} cm")
                        weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "navel lijn maat"
                        "< 1.5 kg: 3,5 anders 5 "
                        (fun w -> if w < 1.5 then 3.5 else 5.)
                        (fun f -> $"%A{f} French")
                        weight.Value

                    if weight.Value < 1.5 then
                        calcIntervention
                            ""
                            "reanimatie"
                            "navel arterie lijn lengte"
                            "kg x 4 + 7"
                            (fun w -> w * 4. + 7.)
                            (fun f -> $"%.1f{f} cm")
                            weight.Value
                    else
                        calcIntervention
                            ""
                            "reanimatie"
                            "navel arterie lijn lengte"
                            "kg x 2.5 + 9.7"
                            (fun w -> w * 2.5 + 9.7)
                            (fun f -> $"%.1f{f} cm")
                            weight.Value

                    calcIntervention
                        ""
                        "reanimatie"
                        "navel vene lijn lengte"
                        "kg x 1.5 + 5.5"
                        (fun w -> w * 1.5 + 5.5)
                        (fun f -> $"%.1f{f} cm")
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
                    // fluid bolus
                    if weight |> Option.isSome then
                        calcFluidBolus weight.Value
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
                        |> List.map (calcBolusMedication weight.Value)
                    |> List.append xs
                    |> List.distinct


    module ContinuousMedication =

        open Utils
        open Shared


        let create
            hospital
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
                Hospital = hospital
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


        let parse (data: string[][]) =
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
                        (getString "hospital")
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

            let calcDose qty vol wght unit doseU =
                let wght = if doseU |> String.contains "kg" then wght else 1.

                let f =
                    let t =
                        match doseU with
                        | _ when doseU |> String.contains "dag" -> 24.
                        | _ when doseU |> String.contains "min" -> 1. / 60.
                        | _ -> 1.

                    let u =
                        match unit, doseU with
                        | _ when unit = "mg" && doseU |> String.contains "microg" -> 1000.
                        | _ when unit = "mg" && doseU |> String.contains "nanog" -> 1000. * 1000.
                        | _ -> 1.

                    1. * t * u

                let d = (f * qty / vol / wght) |> Math.fixPrecision 2

                d, doseU


            let printAdv min max unit = $"%A{min} - %A{max} %s{unit}"

            contMeds
            |> List.filter (fun m -> m.MinWeight <= wght && (wght < m.MaxWeight || m.MaxWeight = 0.))
            |> List.sortBy (fun med -> med.Indication, med.Generic)
            |> List.collect (fun med ->
                let vol = med.Total
                // TODO: really ugly hack to meet specific dose calc
                // need to create a config structure for this
                let qty =
                    if med.Quantity = 0. && med.Hospital = "Radboud UMC" && med.Generic = "morfine" then
                        wght / 2. |> int |> float
                    else
                        med.Quantity

                if vol = 0. then
                    []
                else
                    let d, u = calcDose qty vol wght med.Unit med.DoseUnit

                    [
                        { Intervention.emptyIntervention with
                            Hospital = med.Hospital
                            Indication = med.Indication
                            Name = med.Generic
                            Quantity = Some qty
                            QuantityUnit = med.Unit
                            Total = Some vol
                            TotalUnit = "mL"
                            Solution = med.Solution
                            InterventionDose = Some 1.
                            InterventionDoseUnit = "mL/uur"
                            SubstanceMaxDose = Some med.AbsMax
                            SubstanceDoseAdjust = Some d
                            SubstanceDoseAdjustUnit = u
                            SubstanceMinDoseAdjust = Some med.MinDose
                            SubstanceMaxDoseAdjust = Some med.MaxDose
                            SubstanceDoseText = $"1 mL/uur = {d} {u}"
                            Text = (printAdv med.MinDose med.MaxDose med.DoseUnit)
                        }
                    ]
            )


    module Products =

        open Utils
        open Shared


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
                    let getString n =
                        Csv.getStringColumn cms sl n |> String.trim

                    let getFloat = Csv.getFloatColumn cms sl

                    create (getString "indication") (getString "medication") (getFloat "conc") (getString "unit")
                )
                |> Array.toList
            | _ -> []


    module Order =


        module ValueUnit =

            // create Shared.Types.ValueUnit
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
                    IsNonZeroNegative = nonZ
                    Min = min
                    MinIncl = minIncl
                    Incr = incr
                    Max = max
                    MaxIncl = maxIncl
                    Vals = vals
                }


        module OrderVariable =

            let create n c v =
                {
                    Name = n
                    Constraints = c
                    Variable = v
                }


        module Prescription =

            let create isOnce isOnceTimed isC isD isT f t =
                {
                    IsOnce = isOnce
                    IsOnceTimed = isOnceTimed
                    IsContinuous = isC
                    IsDiscontinuous = isD
                    IsTimed = isT
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

            let create id nm sh cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii =
                {
                    Id = id
                    Name = nm
                    Shape = sh
                    ComponentQuantity = cmp_qty
                    OrderableQuantity = orb_qty
                    OrderableCount = orb_cnt
                    OrderQuantity = ord_qty
                    OrderCount = ord_cnt
                    OrderableConcentration = orb_cnc
                    Dose = dos
                    Items = ii
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
                Prescription = prs
                Route = rte
                Duration = tme
                Start = sta
                Stop = sto
            }


        module OrderLoader =

            let create cmp itm o =
                {
                    Component = cmp
                    Item = itm
                    Order = o
                }


        module LoadedOrder =

            let create adj cmp itm o =
                {
                    UseAdjust = adj
                    Component = cmp
                    Item = itm
                    Order = o
                }


    module Intake =

        let empty: Intake =
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


        let parseTextItem (s: string) =
            s
            |> Seq.map (id >> string)
            |> Seq.fold
                (fun acc c ->
                    if s |> String.isNullOrWhiteSpace then
                        acc
                    else
                        match c |> string, acc |> fst with
                        | s, Normal _ when s = "#" -> Bold "", (acc |> fst) :: (acc |> snd)
                        | s, Italic _ when s = "#" -> Bold s, (acc |> fst) :: (acc |> snd)
                        | s, Bold _ when s = "#" -> Normal "", (acc |> fst) :: (acc |> snd)
                        | s, Bold b -> Bold $"{b}{s}", (acc |> snd)

                        | s, Normal _ when s = "|" -> Italic "", (acc |> fst) :: (acc |> snd)
                        | s, Italic _ when s = "|" -> Normal "", (acc |> fst) :: (acc |> snd)
                        | s, Italic i -> Italic $"{i}{s}", (acc |> snd)

                        | s2, Normal s1 -> Normal $"{s1}{s2}", acc |> snd

                )
                (Normal "", [])
            |> fun (md, acc) -> md :: acc
            |> Seq.rev
            |> Seq.filter (fun ti ->
                match ti with
                | Bold s
                | Italic s
                | Normal s -> s |> String.isNullOrWhiteSpace |> not
            )
            |> Seq.toArray


        let create ind nme shp rte dst dil cmp itm dils cmps itms prs prep adm o adj rr rn =
            {
                Name = nme
                Indication = ind
                Shape = shp
                Route = rte
                DoseType = dst
                Diluent = dil
                Component = cmp
                Item = itm
                Diluents = dils
                Components = cmps
                Items = itms
                Prescription = prs |> Array.map (Array.map parseTextItem)
                Preparation = prep |> Array.map (Array.map parseTextItem)
                Administration = adm |> Array.map (Array.map parseTextItem)
                Order = o
                UseAdjust = adj
                UseRenalRule = rr
                RenalRule = rn
            }


        let eqs (sc1 : OrderScenario) (sc2 : OrderScenario) =
            sc1.Order.Id = sc2.Order.Id


    module PrescriptionContext =


        let doseTypeToDescription doseType =
            match doseType with
            | OnceTimed s
            | Once s
            | Timed s
            | Discontinuous s
            | Continuous s ->
                if String.isNullOrWhiteSpace s |> not then
                    s
                else
                    match doseType with
                    | OnceTimed _
                    | Once _ -> "eenmalig"
                    | Timed _
                    | Discontinuous _ -> "onderhoud"
                    | Continuous _ -> "continu"
                    | NoDoseType -> ""

            | NoDoseType -> ""


        let doseTypeToString doseType =
            match doseType with
            | OnceTimed s -> "oncetimed", s
            | Once s -> "once", s
            | Timed s -> "timed", s
            | Discontinuous s -> "discontinuous", s
            | Continuous s -> "continuous", s
            | NoDoseType -> "", ""
            |> fun (s1, s2) -> if String.isNullOrWhiteSpace s2 then s1 else $"{s1} {s2}"


        let doseTypeFromString s =
            let matchDoseType (dt: string) dd =
                let dt = dt.ToLower().Trim()
                let withText c = dd |> c

                match dt with
                | "once" -> Once |> withText
                | "oncetimed" -> OnceTimed |> withText
                | "timed" -> Timed |> withText
                | "discontinuous" -> Discontinuous |> withText
                | "continuous" -> Continuous |> withText
                | _ -> NoDoseType

            match s |> String.split " " |> Array.toList with
            | [ dt ] -> matchDoseType dt ""
            | dt :: rest -> rest |> String.concat " " |> matchDoseType dt
            | _ -> NoDoseType


        let filter =
            {
                Indications = [||]
                Medications = [||]
                Routes = [||]
                DoseTypes = [||]
                Diluents = [||]
                Components = [||]
                Indication = None
                Medication = None
                Shape = None
                Route = None
                DoseType = None
                Diluent = None
                SelectedComponents = [||]
            }


        let empty: PrescriptionContext =
            {
                DemoVersion = true
                Filter = filter
                Patient = Patient.empty
                Scenarios = [||]
                Intake = Intake.empty
            }

        let setPatient pat sr : PrescriptionContext = { sr with Patient = pat }


        let setScenarios srs sr : PrescriptionContext = { sr with Scenarios = srs }


        let fromOrderScenario (os: OrderScenario) : PrescriptionContext =
            let ord = os.Order

            {
                DemoVersion = false
                Filter =
                    { filter with
                        Indication = Some os.Indication
                        Medication = ord.Orderable.Name |> Some
                        Shape = os.Shape |> Some
                        Route = ord.Route |> Some
                        DoseType = os.DoseType |> Some
                        Diluent = os.Diluent
                    }
                Patient = Patient.empty
                Scenarios = [| os |]
                Intake = Intake.empty
            }

    module TreatmentPlan =

        let create pat srs =
            {
                Patient = pat
                Selected = None
                Filtered = [||]
                Scenarios = srs
                Intake = Intake.empty
            }


    module Formulary =

        let empty: Formulary =
            {
                Generics = [||]
                Indications = [||]
                Routes = [||]
                PatientCategories = [||]
                Products = [||]
                Generic = None
                Indication = None
                Route = None
                PatientCategory = None
                Patient = None
                Markdown = ""
            }


    module Parenteralia =

        let empty: Parenteralia =
            {
                Generics = [||]
                Shapes = [||]
                Routes = [||]
                PatientCategories = [||]
                Generic = None
                Shape = None
                Route = None
                PatientCategory = None
                Markdown = ""
            }