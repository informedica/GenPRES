namespace Domain

module Patient =
    open Utils.Utils
    open GenPres.Shared

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

        let hgt =
            pat
            |> getHeight
            |> Math.fixPrecision 0
            |> string

        let ew =
            pat.Weight.Estimated
            |> Math.fixPrecision 2
            |> string

        let eh =
            pat.Height.Estimated
            |> Math.fixPrecision 0
            |> string

        let bsa =
            match pat |> calcBSA false with
            | Some bsa -> sprintf ", BSA %A m2" (bsa |> Math.fixPrecision 2)
            | None -> ""

        sprintf
            "Leeftijd: %i jaren en %i maanden, Gewicht: %s kg (geschat %s kg), Lengte: %s (geschat %s cm)%s"
            pat.Age.Years pat.Age.Months wght ew hgt eh bsa
