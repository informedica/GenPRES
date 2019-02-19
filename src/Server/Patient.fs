namespace GenPres.Server

open GenPres.Shared

module Patient =
    open Utils

    module Age =
        open Types.Patient.Age

        let (>==) r f = Result.bind f r

        let ageZero =
            { Years = 0
              Months = 0
              Weeks = 0
              Days = 0 }

        let validateMinMax lbl min max n =
            if n >= min && n <= max then Result.Ok n
            else
                sprintf "%s: %i not >= %i and <= %i" lbl n min max
                |> Result.Error

        let set setter lbl min max n age =
            n
            |> validateMinMax lbl min max
            >== ((setter age) >> Result.Ok)
        let setYears = set (fun age n -> { age with Years = n }) "Years" 0 100
        let setMonths mos age =
            age
            |> setYears (mos / 12)
            >== set (fun age n -> { age with Months = n }) "Months" 0 11
                    (mos % 12)

        let setWeeks wks age =
            let yrs = wks / 52
            let mos = (wks - yrs * 52) / 4
            let wks = wks - (mos * 4) - (yrs * 52)
            age
            |> setYears yrs
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

            age
            |> setYears yrs
            >== set (fun age n -> { age with Months = n }) "Months" 0 12 mos
            >== set (fun age n -> { age with Weeks = n }) "Weeks" 0 4 wks
            >== set (fun age n -> { age with Days = n }) "Days" 0 6 dys

        let getYears { Years = yrs } = yrs
        let getMonths { Months = mos } = mos
        let getWeeks { Weeks = ws } = ws
        let getDays { Days = ds } = ds

        let calcYears a =
            (a
             |> getYears
             |> float)
            + ((a
                |> getMonths
                |> float)
               / 12.)

        let calcMonths a = (a |> getYears) * 12 + (a |> getMonths)

    open Types.Patient

    let apply f (p : Patient) = f p
    let get = apply id

    /// Estimate the weight according to age
    /// in `yr` years and `mo` months
    let ageToWeight yr mo =
        let age = (double yr) * 12. + (double mo)
        match Data.NormalValues.ageWeight
              |> List.filter (fun (a, _) -> age <= a) with
        | (_, w) :: _ -> w
        | [] -> 0.
        |> Math.fixPrecision 2

    let ageToHeight yr mo =
        let age = (double yr) * 12. + (double mo)
        match Data.NormalValues.ageHeight |> List.filter (fun (a, _) -> age < a) with
        | (_, h) :: _ -> h
        | _ -> 0.
        |> Math.fixPrecision 0

    let patient =
        let age = Age.ageZero

        let wght : Weight =
            { Estimated = ageToWeight 0 0 
              Measured = 0. }

        let hght =
            { Estimated = ageToHeight 0 0 
              Measured = 0. }

        { Age = age
          Weight = wght
          Height = hght }

    let getAge p = (p |> get).Age
    let getAgeYears p = (p |> getAge).Years
    let getAgeMonths p = (p |> getAge).Months

    let getAgeInYears p =
        (p
         |> getAgeYears
         |> float)
        + ((p
            |> getAgeMonths
            |> float)
           / 12.)

    let getAgeInMonths p = (p |> getAgeYears) * 12 + (p |> getAgeMonths)

    /// Get either the measured weight or the
    /// estimated weight if measured weight = 0
    let getWeight pat =
        if (pat |> get).Weight.Measured = 0. then pat.Weight.Estimated
        else pat.Weight.Measured

    /// Get either the measured height or the
    /// estimated height if measured weight = 0
    let getHeight pat =
        if (pat |> get).Height.Measured = 0. then pat.Height.Estimated
        else pat.Height.Measured

    /// ToDo: make function more general by
    /// being able to set mo > 12 -> yr
    let private updateAge yr mo (pat : Patient) =
        match yr, mo with
        | Some y, None ->
            if y > 18 || y < 0 then pat
            else
                let w = ageToWeight y (pat.Age.Months) 
                let h = ageToHeight y (pat.Age.Months) 
                { pat with Age = { pat.Age with Years = y }
                           Weight = { pat.Weight with Weight.Estimated = w }
                           Height = { pat.Height with Estimated = h } }
        | None, Some m ->
            let age = pat.Age
            let w = ageToWeight (age.Years) m

            let y =
                if m = 12 && age.Years < 18 then age.Years + 1
                else if m = -1 && pat.Age.Years > 0 then age.Years - 1
                else age.Years

            let m =
                if m >= 12 then 0
                else if m = -1 && y = 0 then 0
                else if m = -1 && y > 0 then 11
                else m

            let h = ageToHeight y (pat.Age.Months)
            { pat with Age =
                           { pat.Age with Years = y
                                          Months = m }
                       Weight = { pat.Weight with Weight.Estimated = w }
                       Height = { pat.Height with Estimated = h } }
        | _ -> pat

    let updateAgeYears yr = updateAge (Some yr) None
    let updateAgeMonths mo = updateAge None (Some mo)

    let calculate pat =
        pat
        |> updateAgeYears pat.Age.Years
        |> updateAgeMonths pat.Age.Months

    let updateWeightGram gr pat =
        let kg = gr / 1000.
        { (pat |> get) with Weight = { pat.Weight with Measured = kg } }

    let calcBMI isEst pat =
        let l =
            if isEst then pat.Height.Estimated
            else pat |> getHeight
            |> fun x -> x / 100.

        let w =
            if isEst then pat.Weight.Estimated
            else pat |> getWeight

        if l > 0. then (w / (l ** 2.)) |> Some
        else None

    let calcBSA isEst pat =
        let l =
            if isEst then pat.Height.Estimated
            else pat |> getHeight

        let w =
            if isEst then pat.Weight.Estimated
            else pat |> getWeight

        if l > 0. then sqrt (w * ((l |> float)) / 3600.) |> Some
        else None

    let calcNormalFluid pat =
        let a = pat |> getAge
        a

    let show pat =
        let pat = pat |> get

        let wght =
            let w = pat |> getWeight
            if w < 2. then ""
            else
                w
                |> Math.fixPrecision 2
                |> string

        let ew =
            pat.Weight.Estimated
            |> Math.fixPrecision 2
            |> string

        let bsa =
            match pat |> calcBSA false with
            | Some bsa -> sprintf ", BSA %A m2" (bsa |> Math.fixPrecision 2)
            | None -> ""

        sprintf
            "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg (geschat %s kg)%s"
            pat.Age.Years pat.Age.Months wght ew bsa
