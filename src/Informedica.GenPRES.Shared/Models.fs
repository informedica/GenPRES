namespace Shared


module Models =

    open Types

    /// Canonical patient business logic shared between Client (Fable/JS) and Server (.NET).
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


            let fromBirthDate (now: DateTime) (bdt: DateTime) =
                if bdt > now then
                    failwith $"birthdate: {bdt} cannot be after current date: {now}"
                // calculated last birthdate and number of years ago
                let last, yrs =
                    // set day one day back if not a leap year, and the birthdate is at Feb 29 in a leap year
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
                // calculate the number of months since last birthdate
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

                create
                    (yrs * 1<year>)
                    (Some(mos * 1<month>))
                    (Some(days / 7 * 1<week>))
                    (Some((days - 7 * (days / 7)) * 1<day>))


            let getYears { Age.Years = yrs } = yrs


            let getMonths { Age.Months = mos } = mos


            let getWeeks { Age.Weeks = ws } = ws


            let getDays { Age.Days = ds } = ds


            let calcYears a =
                (a |> getYears |> float) + ((a |> getMonths |> float) / 12.)


            let calcMonths a =
                (a |> getYears |> int) * 12 + (a |> getMonths |> int)

            let gestAgeToString terms lang (age: GestationalAge) =
                let getTerm = Localization.getTerm terms

                $"""
    {age.Weeks} {getTerm lang Terms.``Patient Age weeks``} {age.Days} {getTerm lang Terms.``Patient Age days``}
                """


            let toString terms lang (age: Age) =
                let getTerm = Localization.getTerm terms lang

                let inline plur s1 s2 n =
                    if int n = 1 then $"{int n} {s1}" else $"{int n} {s2}"

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
                | _ when age.Years = 0<year> && age.Months = 0<month> && age.Weeks = 0<week> -> $"{d}"
                | _ when age.Years = 0<year> && age.Months = 0<month> ->
                    if age.Days = 0<day> then $"{w}" else $"{w} en {d}"
                | _ when age.Years = 0<year> ->
                    match age.Weeks, age.Days with
                    | ws, ds when ds > 0<day> && ws > 0<week> -> $"{m}, {w} en {d}"
                    | ws, ds when ds = 0<day> && ws > 0<week> -> $"{m}, {w}"
                    | ws, ds when ds > 0<day> && ws = 0<week> -> $"{m}, {d}"
                    | _ -> $"{m}"
                | _ ->
                    match age.Months, age.Weeks, age.Days with
                    | ms, ws, ds when ms = 0<month> && ds > 0<day> && ws > 0<week> -> $"{y}, {w}, {d}"
                    | ms, ws, ds when ms = 0<month> && ds = 0<day> && ws > 0<week> -> $"{y}, {w}"
                    | ms, ws, ds when ms = 0<month> && ds > 0<day> && ws = 0<week> -> $"{y}, {d}"
                    | ms, ws, ds when ms > 0<month> && ds > 0<day> && ws > 0<week> -> $"{y}, {m}, {w}, {d}"
                    | ms, ws, ds when ms > 0<month> && ds = 0<day> && ws > 0<week> -> $"{y}, {m}, {w}"
                    | ms, ws, ds when ms > 0<month> && ds > 0<day> && ws = 0<week> -> $"{y}, {m}, {d}"
                    | ms, ws, ds when ms > 0<month> && ds = 0<day> && ws = 0<week> -> $"{y}, {m}"
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
                | IntermittentHemodialysis -> options[4]
                | ContinuousHemodialysis -> options[5]
                | PeritonealDialysis -> options[6]


            let optionToRenal s =
                match s with
                | s when s = options[1] -> EGFR(Some 30, Some 50)
                | s when s = options[2] -> EGFR(Some 10, Some 30)
                | s when s = options[3] -> EGFR(None, Some 10)
                | s when s = options[4] -> IntermittentHemodialysis
                | s when s = options[5] -> ContinuousHemodialysis
                | s when s = options[6] -> PeritonealDialysis
                | _ -> EGFR(Some 50, None)


        let empty =
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


        let tryParse (s: string) =
            match Int32.TryParse(s) with
            | false, _ -> None
            | true, v -> v |> Some


        let toggle item (p: Patient option) : Patient option =
            p
            |> Option.map (fun p ->
                { p with
                    Access =
                        if p.Access |> List.exists ((=) item) then
                            p.Access |> List.filter ((<>) item)
                        else
                            p.Access |> List.append [ item ]
                }
            )


        let toggleCVL = toggle CVL


        let togglePVL = toggle PVL


        let toggleET = toggle EnteralTube


        let setRenal (s: string option) (p: Patient option) : Patient option =
            let set rf (p: Patient option) =
                match p with
                | None -> p
                | Some p -> { p with RenalFunction = rf } |> Some

            match s with
            | None -> p |> set None
            | Some s ->
                let rf = s |> RenalFunction.optionToRenal |> Some
                p |> set rf


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

            { (pat |> get) with Weight = { pat.Weight with Measured = gr |> Some } }


        let calcBMI (pat: Patient) =
            match pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Measured, pat.Height.Estimated with
            | Some w, _, Some h, _
            | None, Some w, None, Some h ->
                if h > 0<cm> then
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
            | None, Some w, None, Some h -> Calculations.BSA.calcDuBois w h |> Some


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
                |> List.nearestIndex age
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
                    match pat |> getPostConceptionalAgeInDays with
                    | Some days ->
                        let pcAgeInWeeks = (days |> float) / 7.

                        let weight =
                            normalNeoWeights
                            |> nearest pcAgeInWeeks
                            |> Option.map (fun (p3, m, p97) ->
                                let m =
                                    wghts |> List.nearestIndex (int m) |> (fun idx -> wghts[idx]) |> Measures.toGram

                                int p3 * 1<gram>, m, int p97 * 1<gram>
                            )

                        let height =
                            normalNeoHeights
                            |> nearest pcAgeInWeeks
                            |> Option.map (fun (p3, m, p97) ->
                                let m =
                                    hghts |> List.nearestIndex (int m) |> (fun idx -> hghts[idx]) |> Measures.toCm

                                int p3 * 1<cm>, m, int p97 * 1<cm>
                            )

                        weight, height
                    | None ->
                        let ageInYears = age |> Age.calcYears

                        let weight =
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
                            normalHeights
                            |> nearest ageInYears
                            |> Option.map (fun (p3, m, p97) ->
                                let m =
                                    hghts |> List.nearestIndex (int m) |> (fun idx -> hghts[idx]) |> Measures.toCm

                                int p3 * 1<cm>, m, int p97 * 1<cm>
                            )

                        weight, height

            { pat with
                Weight =
                    { pat.Weight with
                        EstimatedP3 = ew |> Option.map (fun (p3, _, _) -> p3)
                        Estimated = ew |> Option.map (fun (_, m, _) -> m)
                        EstimatedP97 = ew |> Option.map (fun (_, _, p97) -> p97)
                        Measured = pat.Weight.Measured |> Option.orElse (ew |> Option.map (fun (_, m, _) -> m))
                    }
                Height =
                    { pat.Height with
                        EstimatedP3 = eh |> Option.map (fun (p3, _, _) -> p3)
                        Estimated = eh |> Option.map (fun (_, m, _) -> m)
                        EstimatedP97 = eh |> Option.map (fun (_, _, p97) -> p97)
                        Measured = pat.Height.Measured |> Option.orElse (eh |> Option.map (fun (_, m, _) -> m))
                    }
            }


        let setYear s (p: Patient option) =
            match p with
            | None ->
                create
                    (s |> Option.bind tryParse |> Option.map Measures.toYear)
                    None
                    None
                    None
                    None
                    None
                    None
                    None
                    UnknownGender
                    []
                    None
                    None
            | Some p ->
                create
                    (s |> Option.bind tryParse |> Option.map Measures.toYear)
                    (p |> getAgeMonths)
                    (p |> getAgeWeeks)
                    (p |> getAgeDays)
                    None
                    None
                    None
                    None
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setMonth s (p: Patient option) =
            match p with
            | None ->
                create
                    None
                    (s |> Option.bind tryParse |> Option.map Measures.toMonth)
                    None
                    None
                    None
                    None
                    None
                    None
                    UnknownGender
                    []
                    None
                    None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (s |> Option.bind tryParse |> Option.map Measures.toMonth)
                    (p |> getAgeWeeks)
                    (p |> getAgeDays)
                    None
                    None
                    None
                    None
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setWeek s (p: Patient option) =
            match p with
            | None ->
                create
                    None
                    None
                    (s |> Option.bind tryParse |> Option.map Measures.toWeek)
                    None
                    None
                    None
                    None
                    None
                    UnknownGender
                    []
                    None
                    None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (p |> getAgeMonths)
                    (s |> Option.bind tryParse |> Option.map Measures.toWeek)
                    (p |> getAgeDays)
                    None
                    None
                    (p |> getGAWeeks)
                    (p |> getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setDay s (p: Patient option) =
            match p with
            | None ->
                create
                    None
                    None
                    None
                    (s |> Option.bind tryParse |> Option.map Measures.toDay)
                    None
                    None
                    None
                    None
                    UnknownGender
                    []
                    None
                    None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (p |> getAgeMonths)
                    (p |> getAgeWeeks)
                    (s |> Option.bind tryParse |> Option.map Measures.toDay)
                    None
                    None
                    (p |> getGAWeeks)
                    (p |> getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setWeight s (p: Patient option) =
            match p with
            | None -> create None None None None (s |> Option.bind tryParse) None None None UnknownGender [] None None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (p |> getAgeMonths)
                    (p |> getAgeWeeks)
                    (p |> getAgeDays)
                    (s |> Option.bind tryParse)
                    (p |> getHeight |> Option.map int)
                    (p |> getGAWeeks)
                    (p |> getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setHeight s (p: Patient option) =
            match p with
            | None -> create None None None None None (s |> Option.bind tryParse) None None UnknownGender [] None None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (p |> getAgeMonths)
                    (p |> getAgeWeeks)
                    (p |> getAgeDays)
                    (p |> getWeight |> Option.map int)
                    (s |> Option.bind tryParse)
                    (p |> getGAWeeks)
                    (p |> getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setGAWeek s (p: Patient option) =
            match p with
            | None ->
                create
                    None
                    None
                    None
                    None
                    None
                    None
                    (s |> Option.bind tryParse |> Option.map Measures.toWeek)
                    None
                    UnknownGender
                    []
                    None
                    None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (p |> getAgeMonths)
                    (p |> getAgeWeeks)
                    (p |> getAgeDays)
                    None
                    None
                    (s |> Option.bind tryParse |> Option.map Measures.toWeek)
                    (p |> getGADays)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let setGADay s (p: Patient option) =
            match p with
            | None ->
                create
                    None
                    None
                    None
                    None
                    None
                    None
                    None
                    (s |> Option.bind tryParse |> Option.map Measures.toDay)
                    UnknownGender
                    []
                    None
                    None
            | Some p ->
                create
                    (p |> getAgeYears)
                    (p |> getAgeMonths)
                    (p |> getAgeWeeks)
                    (p |> getAgeDays)
                    None
                    None
                    (p |> getGAWeeks)
                    (s |> Option.bind tryParse |> Option.map Measures.toDay)
                    p.Gender
                    p.Access
                    p.RenalFunction
                    p.Department


        let toString terms lang markDown (pat: Patient) =
            let getTerm = Localization.getTerm terms lang

            let toStr s n =
                n |> Option.map (Math.fixPrecision 3 >> string >> (fun s' -> $"{s}{s'}"))

            let bold s =
                s |> Option.map (fun s -> if markDown then $"**{s}**" else s)

            let italic s =
                s |> Option.map (fun s -> if markDown then $"*{s}*" else s)

            let isAdult =
                pat.Age
                |> Option.map (fun a -> a.Years >= 18<year>)
                |> Option.defaultValue false

            [
                match pat.Gender with
                | Male -> if isAdult then Some "Man" else Some "Jongen"
                | Female -> if isAdult then Some "Vrouw" else Some "Meisje"
                | UnknownGender -> Some "Onbekend geslacht"
                |> bold

                Some $"{Terms.``Patient Age`` |> getTerm}:" |> italic

                pat.Age
                |> Option.map (Age.toString terms lang)
                |> bold
                |> Option.orElse ("" |> Some)

                Some $"{Terms.``Patient Weight`` |> getTerm}:" |> italic

                pat.Weight.Measured
                |> Option.map (fun x -> float x / 1000.)
                |> toStr ""
                |> Option.map (fun s -> $"{s} kg")
                |> bold


                match pat.Weight.EstimatedP3, pat.Weight.EstimatedP97 with
                | Some p3, Some p97 ->
                    let capt = $"{Terms.``Patient Estimated`` |> getTerm}: "
                    let p3 = float p3 / 1000. |> Math.fixPrecision 3
                    let p97 = float p97 / 1000. |> Math.fixPrecision 3
                    $"{capt}({p3} - {p97} kg)" |> Some
                | _ ->
                    pat.Weight.Estimated
                    |> Option.map (fun x -> float x / 1000.)
                    |> toStr $"{Terms.``Patient Estimated`` |> getTerm}: "
                    |> Option.map (fun s -> $"({s} kg)")


                Some $"{Terms.``Patient Length`` |> getTerm}:" |> italic

                pat.Height.Measured
                |> Option.map float
                |> toStr ""
                |> Option.map (fun s -> $"{s} cm")
                |> bold


                match pat.Height.EstimatedP3, pat.Height.EstimatedP97 with
                | Some p3, Some p97 ->
                    let capt = $"{Terms.``Patient Estimated`` |> getTerm}: "
                    let p3 = float p3 |> Math.fixPrecision 3
                    let p97 = float p97 |> Math.fixPrecision 3
                    $"{capt}({p3} - {p97} cm)" |> Some
                | _ ->
                    pat.Height.Estimated
                    |> Option.map float
                    |> toStr $"{Terms.``Patient Estimated`` |> getTerm}: "
                    |> Option.map (fun s -> $"({s} cm)")


                (Some "BSA:") |> italic
                pat
                |> calcBSA
                |> Option.map (fun x ->
                    let x = x |> float |> Math.fixPrecision 2
                    $"{x} m2"
                )
                |> bold

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
            let textfn m =
                $"%s{m - 0.5 |> toStr} - %s{m |> toStr} - %s{m + 0.5 |> toStr}"

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
            let formula wght =
                joules |> List.findNearestMax (wght * 4.)

            let textfn m = $"%s{m |> toStr} joule"

            calcIntervention "" "reanimatie" "defibrillatie" "4 joule/kg" formula textfn


        let calcCardioVersion =
            let formula wght =
                joules |> List.findNearestMax (wght * 2.)

            let textfn m = $"%s{m |> toStr} joule"
            calcIntervention "" "reanimatie" "cardioversie" "2 joule/kg" formula textfn


        let calcBolusMedication wght (bolus: BolusMedication) =
            let toStr = decimal >> Decimal.toStringNumberNLWithoutTrailingZeros

            let d, v, c =
                let d, v =
                    calcDoseVol wght bolus.NormDose bolus.Concentration bolus.MinDose bolus.MaxDose

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
                    let getString n =
                        Csv.getStringColumn cms sl n |> String.trim

                    let getStringOpt n =
                        if cms |> Array.exists ((=) n) then getString n else ""

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

        open Shared


        let toStr = decimal >> Decimal.toStringNumberNLWithoutTrailingZeros


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
                    let getString n =
                        Csv.getStringColumn cms sl n |> String.trim

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

                let d = f * qty / vol / wght |> Math.fixPrecision 2

                d, doseU


            let printAdv min max unit =
                $"%s{min |> toStr} - %s{max |> toStr} %s{unit}"

            contMeds
            |> List.filter (fun m -> m.MinWeight <= wght && (wght < m.MaxWeight || m.MaxWeight = 0.))
            |> List.sortBy (fun med -> med.Category, med.Medication)
            |> List.collect (fun med ->
                let vol = med.Total
                // TODO: really ugly hack to meet specific dose calc
                // need to create a config structure for this
                let qty =
                    if med.Quantity = 0. && med.Hospital = "Radboud UMC" && med.Medication = "morfine" then
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
                            Category = med.Category
                            Name = med.Medication
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
                            SubstanceDoseText = $"1 mL/uur = %s{d |> toStr} {u}"
                            Text = printAdv med.MinDose med.MaxDose med.DoseUnit
                        }
                    ]
            )


    /// <summary>
    /// Available medication products and their concentrations: <c>parse</c> reads
    /// the "products" sheet of the emergency-list spreadsheet. Columns: indication,
    /// medication, conc, unit.
    /// </summary>
    module Products =

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


    /// <summary>
    /// Reference ranges for weight and height estimation: <c>parse</c> reads the
    /// "weight", "height", "weight neo" and "height neo" sheets of the
    /// emergency-list spreadsheet, all four sharing the columns sex ("M"/"F"), age
    /// (years), p3, mean, p97.
    /// </summary>
    module NormalValues =

        open Shared


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
                    let getString n =
                        Csv.getStringColumn cms sl n |> String.trim

                    let getFloat = Csv.getFloatColumn cms sl

                    create (getString "sex") (getFloat "age") (getFloat "p3") (getFloat "mean") (getFloat "p97")
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


        (*
            /// <summary>
            /// Get the user readable string version in Dutch with verbosity short and
            /// value as decimal with a fixed precision
            /// </summary>
            /// <param name="prec">The precision</param>
            /// <param name="vu">The ValueUnit</param>
            /// <example>
            /// <code>
            /// toStringDecimalDutchShortWithPrec 2 (ValueUnit ([|1N/3N; 2N/3N; 3N/5N|], Mass (KiloGram 1N)))
            /// = "0,33;0,67;0,6 kg"
            /// </code>
            /// </example>
            let toStringDecimalDutchShortWithPrec prec (vu: ValueUnit) =
                let v, u = vu.Value, vu.Unit

                let vs =
                    v
                    |> Array.map (snd >> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision prec)
                    |> Array.distinct
                    |> Array.toReadableString

                let us = u |> unitToReadableDutchString

                vs + " " + us
            *)


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


            let renderValue prec (var: Variable) =
                match var.Min, var.Max, var.Vals with
                | _, _, Some vals when vals.Value.Length = 1 ->
                    let v = vals.Value |> Array.head |> snd |> Decimal.fixPrecision prec
                    $"{v} {vals.Unit}"
                | _, _, Some vals when vals.Value.Length > 1 ->
                    let minVal = vals.Value |> Array.minBy snd |> snd |> Decimal.fixPrecision prec
                    let maxVal = vals.Value |> Array.maxBy snd |> snd |> Decimal.fixPrecision prec
                    $"{minVal} - {maxVal} {vals.Unit}"
                | Some min, Some max, _ ->
                    let minVal = min.Value |> Array.minBy snd |> snd |> Decimal.fixPrecision prec
                    let maxVal = max.Value |> Array.maxBy snd |> snd |> Decimal.fixPrecision prec
                    $"{minVal} - {maxVal} {min.Unit}"
                | _ -> ""


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


            let isSolved (ovar: OrderVariable) =
                ovar.Variable.Vals
                |> Option.map (_.Value >> Array.length >> ((=) 1))
                |> Option.defaultValue false


            let isNavigable (ovar: OrderVariable) =
                if ovar |> isSolved then
                    false
                else
                    // note that an ordervariable with an increment always has a
                    // min value by definition as all order variables are initialized to
                    // be non-zero positive and a min value is always a multiple of an
                    // increment
                    (ovar.Variable.Max.IsSome && ovar.DefinedConstraints.Incr.IsSome)
                    || ovar.Variable.Vals
                       |> Option.map (_.Value >> Array.length >> (fun c -> c > 1))
                       |> Option.defaultValue false


            let displayString (ovar: OrderVariable) =
                ovar.Variable.Vals
                |> Option.bind (fun v ->
                    v.Value
                    |> Array.tryHead
                    |> Option.map (fun (_, d) -> (d |> Decimal.toStringNumberNLWithoutTrailingZeros) + " " + v.Unit)
                )
                |> Option.defaultValue ""


            let displayStringFormatted (format: decimal -> string) (ovar: OrderVariable) =
                ovar.Variable.Vals
                |> Option.bind (fun v ->
                    v.Value
                    |> Array.tryHead
                    |> Option.map (fun (_, d) -> (d |> format) + " " + v.Unit)
                )
                |> Option.defaultValue ""


            let (|NonNavigable|Navigable|Selectable|Stepable|) (ovar: OrderVariable) =
                let var = ovar.Variable
                let def = ovar.DefinedConstraints

                let valsCount =
                    var.Vals
                    |> Option.map (fun vu -> vu.Value |> Array.length)
                    |> Option.defaultValue 0

                if valsCount > 1 then
                    Selectable
                elif valsCount = 1 && def.Incr.IsSome then
                    Stepable
                elif def.Incr.IsSome && var.Min.IsSome && var.Max.IsSome then
                    Navigable
                else
                    NonNavigable


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


            let setOvar s (ovar: OrderVariable) =
                { ovar with Variable = ovar.Variable |> setVar s }


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

            let create id nm sh cmp_qty orb_qty orb_cnt ord_qty ord_cnt orb_cnc dos ii =
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


        let isSolved (ord: Order) =
            [
                yield! ord.Orderable.Components |> Array.map _.OrderableQuantity
                ord.Orderable.OrderableQuantity
                ord.Orderable.Dose.Quantity

                if ord.Schedule.IsContinuous || ord.Schedule.IsOnceTimed || ord.Schedule.IsTimed then
                    ord.Orderable.Dose.Rate

                if ord.Schedule.IsDiscontinuous || ord.Schedule.IsTimed then
                    ord.Schedule.Frequency
            ]
            |> List.forall OrderVariable.isSolved


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


        // Intake substance row definitions
        let intakeRows =
            [|
                [| "volume"; ""; "ml/kg/dag" |]
                [| "energie"; ""; "kCal/kg/dag" |]
                [| "koolhydraat"; ""; "mg/kg/min" |]
                [| "eiwit"; ""; "g/kg/dag" |]
                [| "vet"; ""; "g/kg/dag" |]
                [| "natrium"; ""; "mmol/kg/dag" |]
                [| "kalium"; ""; "mmol/kg/dag" |]
                [| "chloride"; ""; "mmol/kg/dag" |]
                [| "calcium"; ""; "mmol/kg/dag" |]
                [| "magnesium"; ""; "mmol/kg/dag" |]
                [| "fosfaat"; ""; "mmol/kg/dag" |]
                [| "ijzer"; ""; "mmol/kg/dag" |]
                [| "vit D"; ""; "mmol/kg/dag" |]
                [| "ethanol"; ""; "mg/kg/dag" |]
                [| "propyleenglycol"; ""; "mg/kg/dag" |]
                [| "boorzuur"; ""; "mmol/kg/dag" |]
                [| "benzylalcohol"; ""; "mmol/kg/dag" |]
            |]


        // Map a substance name to the corresponding Totals field
        let substanceToField (intake: Totals) =
            function
            | "volume" -> intake.Volume
            | "energie" -> intake.Energy
            | "koolhydraat" -> intake.Carbohydrate
            | "eiwit" -> intake.Protein
            | "vet" -> intake.Fat
            | "natrium" -> intake.Sodium
            | "kalium" -> intake.Potassium
            | "chloride" -> intake.Chloride
            | "calcium" -> intake.Calcium
            | "magnesium" -> intake.Magnesium
            | "phosphaat"
            | "fosfaat" -> intake.Phosphate
            | "ijzer" -> intake.Iron
            | "vitamine D"
            | "vit D" -> intake.VitaminD
            | "ethanol" -> intake.Ethanol
            | "propyleenglycol" -> intake.Propyleenglycol
            | "boorzuur" -> intake.BoricAcid
            | "benzylalcohol" -> intake.BenzylAlcohol
            | _ -> [||]


    module OrderScenario =


        let create ind nme frm rte dst dil cmp itm dils cmps itms prs prep adm o adj rr rn ids =
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
            }


        let eqs (sc1: OrderScenario) (sc2: OrderScenario) = sc1.Order.Id = sc2.Order.Id


    module DoseType =


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
                DemoVersion = true
                Filter = filter
                Patient = Patient.empty
                Scenarios = [||]
                Intake = Totals.empty
            }

        let setPatient pat ctx : OrderContext = { ctx with Patient = pat }


        let setMedication ind med rte frm dtp ctx : OrderContext =
            { ctx with
                Filter =
                    { ctx.Filter with
                        Indication = ind
                        Generic = med
                        Route = rte
                        Form = frm
                        DoseType = dtp
                    }
            }


        let setScenarios srs ctx : OrderContext = { ctx with Scenarios = srs }


        let fromOrderScenario pat (sc: OrderScenario) : OrderContext =
            let ord = sc.Order

            {
                DemoVersion = false
                Filter =
                    { filter with
                        Indication = Some sc.Indication
                        Generic = ord.Orderable.Name |> Some
                        Form = sc.Form |> Some
                        Route = ord.Route |> Some
                        DoseType = sc.DoseType |> Some
                    }
                Patient = pat
                Scenarios = [| sc |]
                Intake = Totals.empty
            }


        let indicationChange s (ctx: OrderContext) : OrderContext =
            if s |> Option.isNone then
                { ctx with
                    Filter =
                        { ctx.Filter with
                            Indications = [||]
                            Indication = None
                            DoseTypes = [||]
                            DoseType = None
                        }
                    Scenarios = [||]
                }
            else
                { ctx with OrderContext.Filter.Indication = s }


        let medicationChange s (ctx: OrderContext) : OrderContext =
            if s |> Option.isNone then
                { ctx with
                    Filter =
                        { ctx.Filter with
                            Generics = [||]
                            Generic = None
                            DoseTypes = [||]
                            DoseType = None
                        }
                    Scenarios = [||]
                }
            else
                { ctx with OrderContext.Filter.Generic = s }


        let routeChange s (ctx: OrderContext) : OrderContext =
            if s |> Option.isNone then
                { ctx with
                    Filter =
                        { ctx.Filter with
                            Routes = [||]
                            Route = None
                            DoseTypes = [||]
                            DoseType = None
                        }
                    Scenarios = [||]
                }
            else
                { ctx with OrderContext.Filter.Route = s }


        let formChange s (ctx: OrderContext) : OrderContext =
            if s |> Option.isNone then
                { ctx with
                    Filter =
                        { ctx.Filter with
                            Forms = [||]
                            Form = None
                            DoseTypes = [||]
                            DoseType = None
                        }
                    Scenarios = [||]
                }
            else
                { ctx with OrderContext.Filter.Form = s }


        let diluentChange s (ctx: OrderContext) : OrderContext =
            { ctx with OrderContext.Filter.Diluent = s }


        let componentsChange cs (ctx: OrderContext) : OrderContext =
            { ctx with OrderContext.Filter.SelectedComponents = cs }


        let doseTypeChange (dt: DoseType option) (ctx: OrderContext) : OrderContext =
            if dt |> Option.isNone then
                { ctx with
                    Filter =
                        { ctx.Filter with
                            DoseTypes = [||]
                            DoseType = None
                        }
                    Scenarios = [||]
                }
            else
                { ctx with OrderContext.Filter.DoseType = dt }


        let syncFilterToFormulary (filter: Filter) (form: Formulary) : Formulary =
            { form with
                Indication = filter.Indication
                Generic = filter.Generic
                Route = filter.Route
                Form = filter.Form
                DoseType = filter.DoseType
            }


        let syncFilterToParenteralia (filter: Filter) (par: Parenteralia) : Parenteralia =
            { par with
                Generic = filter.Generic
                Form = filter.Form
                Route = filter.Route
            }


        let syncFormularyToFilter (form: Formulary) (ctx: OrderContext) : OrderContext =
            let unchanged =
                form.Indication = ctx.Filter.Indication
                && form.Generic = ctx.Filter.Generic
                && form.Route = ctx.Filter.Route
                && form.Form = ctx.Filter.Form

            { ctx with
                Filter =
                    { ctx.Filter with
                        Indication = form.Indication
                        Generic = form.Generic
                        Form = form.Form
                        Route = form.Route
                        DoseType = form.DoseType
                        Diluent = if unchanged then ctx.Filter.Diluent else None
                        SelectedComponents = if unchanged then ctx.Filter.SelectedComponents else [||]
                    }
                Scenarios = [||]
            }


        let syncParenteraliaToFilter (par: Parenteralia) (ctx: OrderContext) : OrderContext =
            let unchanged =
                par.Generic = ctx.Filter.Generic
                && par.Route = ctx.Filter.Route
                && par.Form = ctx.Filter.Form

            { ctx with
                Filter =
                    { ctx.Filter with
                        Indication = None
                        Generic = par.Generic
                        Form = par.Form
                        Route = par.Route
                        DoseType = None
                        Diluent = if unchanged then ctx.Filter.Diluent else None
                        SelectedComponents = if unchanged then ctx.Filter.SelectedComponents else [||]
                    }
                Scenarios = [||]
            }


    module TextBlock =

        let maxTb (xs: TextBlock[][]) =
            if xs |> Array.isEmpty then
                Valid
            else
                xs
                |> Array.collect (fun tbs ->
                    if tbs |> Array.isEmpty then
                        [| 0 |]
                    else
                        tbs
                        |> Array.map (fun tb ->
                            match tb with
                            | Valid _ -> 0
                            | Caution _ -> 1
                            | Warning _ -> 2
                            | Alert _ -> 3
                        )
                )
                |> Array.max
                |> function
                    | 0 -> Valid
                    | 1 -> Caution
                    | 2 -> Warning
                    | 3 -> Alert
                    | i -> failwith $"not a valid textblock: {i}"


        /// Flatten TextBlock[][] to a single-row TextBlock[][] for compact display.
        /// Joins rows with " + " separators and uses the max severity level.
        let flatten (blocks: TextBlock[][]) : TextBlock[][] =
            if blocks |> Array.isEmpty then
                blocks
            else
                let getItems tb =
                    match tb with
                    | Valid itms
                    | Caution itms
                    | Warning itms
                    | Alert itms -> itms |> Array.append [| " " |> Normal |]

                let add xs =
                    let plus = [| [| " + " |> Normal |] |]

                    xs
                    |> Array.fold
                        (fun acc x ->
                            if acc |> Array.isEmpty then
                                x
                            else
                                x |> Array.append plus |> Array.append acc
                        )
                        [||]
                    |> Array.collect id

                blocks
                |> Array.map (Array.map getItems)
                |> add
                |> (blocks |> maxTb)
                |> Array.singleton
                |> Array.singleton


    module OrderPlan =

        let create pat srs =
            {
                Patient = pat
                Selected = None
                Filtered = [||]
                Scenarios = srs
                Totals = Totals.empty
            }


    module NutritionContext =

        let create id label category removable ctx : NutritionContext =
            {
                Id = id
                Label = label
                Category = category
                Removable = removable
                OrderContext = ctx
            }


    module NutritionPlan =

        let empty: NutritionPlan =
            {
                Patient = Patient.empty
                NutritionContexts = [||]
                Totals = Totals.empty
            }

        let create patient contexts : NutritionPlan =
            {
                Patient = patient
                NutritionContexts = contexts
                Totals = Totals.empty
            }


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
