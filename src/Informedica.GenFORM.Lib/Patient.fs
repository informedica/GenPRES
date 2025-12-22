namespace Informedica.GenForm.Lib


module VenousAccess =


    let check location venousAccess =
        match location, venousAccess with
        | AnyAccess, _ -> true
        | _, xs when xs |> List.isEmpty  -> true
        | _ ->
            venousAccess
            |> List.exists ((=) location)



module Gender =

    open Informedica.Utils.Lib.BCL



    /// Map a string to a Gender.
    let fromString s =
        let s = s |> String.toLower |> String.trim
        match s with
        | "man" -> Male
        | "vrouw" -> Female
        | _ -> AnyGender


    /// Get the string representation of a Gender.
    let toString = function
        | Male -> "man"
        | Female -> "vrouw"
        | AnyGender -> ""


    /// Check if a Filter contains a Gender.
    /// Note if AnyGender is specified, this will always return true.
    let isMatch gender filterGender =
        match filterGender, gender with
        | _, AnyGender -> true
        | _ -> filterGender = gender



    /// Check if a Filter contains a Gender.
    /// Note if AnyGender is specified, this will always return true.
    let filter gender (filter : DoseFilter) =
        match filter.Patient.Gender, gender with
        | _, AnyGender -> true
        | _ -> filter.Patient.Gender = gender


module PatientCategory =


    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Limit = Informedica.GenCore.Lib.Ranges.Limit
    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
    module Conversions = Informedica.GenCore.Lib.Conversions


    open Utils


    let empty : PatientCategory =
        {
            Location = None
            Department = None
            Gender = AnyGender
            Age = MinMax.empty
            Weight = MinMax.empty
            BSA = MinMax.empty
            GestAge = MinMax.empty
            PMAge = MinMax.empty
            Access = AnyAccess
        }


    let isEmpty (p : PatientCategory) =
        { empty with
            Department = p.Department
            Access = p.Access
        } = p


    /// <summary>
    /// Use a PatientCategory to get a sort value.
    /// </summary>
    /// <remarks>
    /// The order will be based on the following:
    /// - Age
    /// - Weight
    /// - Gestational Age
    /// - Post Menstrual Age
    /// The first will receive the highest weight and the last the lowest.
    /// </remarks>
    let sortBy (pat : PatientCategory) =
        let toInt def = function
            | Some x ->
                x
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.tryHead
                |> function
                    | None -> 0
                    | Some x -> x |> BigRational.ToInt32
            | None -> def

        (pat.Age.Min |> toInt 0 |> fun i -> if i > 0 then i + 300 else i) +
        (pat.GestAge.Min |> toInt (7 * 39)) +
        (pat.PMAge.Min |> toInt (7 * 39)) +
        (pat.Weight.Min |> toInt 0 |> fun w -> w / 1000)


    /// <summary>
    /// Filters a PatientCategory using a Filter.
    /// Returns true if the PatientCategory matches the Filter criteria.
    /// </summary>
    /// <param name="filter">The Filter to filter the PatientCategory with</param>
    /// <param name="patCat">The Patient Category</param>
    let filter (filter : DoseFilter) (patCat : PatientCategory) =
        let eqs a b =
            match a, b with
            | None, _
            | _, None -> true
            | Some a, Some b -> a = b

        ([| patCat |],
        [|
            // if either filter location or patient category location is None, pass; otherwise must match
            fun (p: PatientCategory) -> filter.Patient.Location |> eqs p.Location
            // if either filter department or patient category department is None, pass; otherwise must match
            fun (p: PatientCategory) -> filter.Patient.Department |> eqs p.Department
            // patient age must be within patient category age range (if specified)
            fun (p: PatientCategory) -> filter.Patient.Age |> MinMax.inRange p.Age
            // patient weight must be within patient category weight range (if specified)
            fun (p: PatientCategory) -> filter.Patient.Weight |> MinMax.inRange p.Weight
            // if both weight and height are given, calculate BSA and check if within patient category BSA range
            fun (p: PatientCategory) ->
                match filter.Patient.Weight, filter.Patient.Height with
                | Some w, Some h ->
                    Calculations.calcDuBois w h
                    |> Some
                    |> Utils.MinMax.inRange p.BSA
                | _ -> true
            if filter.Patient.Age |> Option.isSome then
                yield! [|
                    // gestational age check: if patient category is empty (all patients), pass;
                    // if filter has gestational age < full term and patient category has no gest/pm age, filter out;
                    // otherwise check if gestational age (defaulting to full term) is in patient category range
                    fun (p: PatientCategory) ->
                        // all patients rule
                        if p |> isEmpty then true
                        else
                            // if gestational is set and < full term filter out all
                            // dose rules with no gestational age or pm age
                            if filter.Patient.GestAge.IsSome &&
                               p.GestAge = MinMax.empty &&
                               p.PMAge = MinMax.empty then
                                filter.Patient.GestAge.Value >=? ValueUnit.ageFullTerm
                            else
                                filter.Patient.GestAge
                                // if no gest age assume full term
                                |> Option.defaultValue Utils.ValueUnit.ageFullTerm
                                |> Some
                                |> Utils.MinMax.inRange p.GestAge
                    // post-menstrual age check: if patient category is empty (all patients), pass;
                    // otherwise calculate PM age (age + gestational age, defaulting gestational to full term)
                    // and check if within patient category PM age range
                    fun (p: PatientCategory) ->
                        // alle patients rule
                        if p |> isEmpty then true
                        else
                            filter.Patient.PMAge
                            // if no gest age assume full term
                            |> Option.defaultValue (filter.Patient.Age.Value + Utils.ValueUnit.ageFullTerm)
                            |> Some
                            |> Utils.MinMax.inRange p.PMAge
                |]
            // patient gender must match patient category gender (or category allows any gender)
            fun (p: PatientCategory) -> filter |> Gender.filter p.Gender
            // patient venous access must be compatible with patient category access requirements
            fun (p: PatientCategory) ->
                VenousAccess.check p.Access filter.Patient.Access
        |])
        ||> Array.fold(fun acc pred ->
            acc
            |> Array.filter pred
        )
        |> fun xs -> xs |> Array.length > 0


    let filterPatient (pat : Patient) (patCat : PatientCategory) =
        let eqs a b =
            match a, b with
            | None, _
            | _, None -> true
            | Some a, Some b -> a = b

        ([| patCat |],
        [|
            fun (p: PatientCategory) -> pat.Department |> eqs p.Department
            fun (p: PatientCategory) -> pat.Age |> Utils.MinMax.inRange p.Age
            fun (p: PatientCategory) -> pat.Weight |> Utils.MinMax.inRange p.Weight
            fun (p: PatientCategory) ->
                match pat.Weight, pat.Height with
                | Some w, Some h ->
                    Calculations.calcDuBois w h
                    |> Some
                    |> Utils.MinMax.inRange p.BSA
                | _ -> true
            if pat.Age |> Option.isSome then
                yield! [|
                    // check gestational age
                    fun (p: PatientCategory) ->
                        // all patients rule
                        if p |> isEmpty then true
                        else
                            // if gestational is set and < full term filter out all
                            // dose rules with no gestational age or pm age
                            if pat.GestAge.IsSome &&
                               p.GestAge = MinMax.empty &&
                               p.PMAge = MinMax.empty then
                                pat.GestAge.Value >=? Utils.ValueUnit.ageFullTerm
                            else
                                pat.GestAge
                                // if no gest age assume full term
                                |> Option.defaultValue Utils.ValueUnit.ageFullTerm
                                |> Some
                                |> Utils.MinMax.inRange p.GestAge
                    // check pm age
                    fun (p: PatientCategory) ->
                        // alle patients rule
                        if p |> isEmpty then true
                        else
                            pat.PMAge
                            // if no gest age assume full term
                            |> Option.defaultValue (pat.Age.Value + Utils.ValueUnit.ageFullTerm)
                            |> Some
                            |> Utils.MinMax.inRange p.PMAge
                |]
//            fun (p: PatientCategory) -> filter |> Gender.filter p.Gender
            fun (p: PatientCategory) ->
                VenousAccess.check p.Access pat.Access
        |])
        ||> Array.fold(fun acc pred ->
            acc
            |> Array.filter pred
        )
        |> fun xs -> xs |> Array.length > 0


    /// Check whether patCat2 is a match with patCat2
    let isMatch (patCat1 : PatientCategory) (patCat2 : PatientCategory) =
        let eqsOpt a b =
            match a, b with
            | None, _
            | _, None -> true
            | Some a, Some b -> a = b

        let inRange (minMax1 : MinMax) minMax2 =
            minMax1.Min |> Option.map Limit.getValueUnit |> Utils.MinMax.inRange minMax2 &&
            minMax2.Max |> Option.map Limit.getValueUnit |> Utils.MinMax.inRange minMax2

        ([| patCat2 |],
        [|
            fun (p: PatientCategory) -> patCat1.Department |> eqsOpt p.Department
            fun (p: PatientCategory) -> p.Age |> inRange patCat1.Age
            fun (p: PatientCategory) -> p.GestAge |> inRange patCat1.GestAge
            fun (p: PatientCategory) -> p.PMAge |> inRange patCat1.PMAge
            fun (p: PatientCategory) -> p.Weight |> inRange patCat1.Weight
            fun (p: PatientCategory) -> p.BSA |> inRange patCat1.BSA
            fun (p: PatientCategory) -> patCat1.Gender |> Gender.isMatch p.Gender
            fun (p: PatientCategory) -> VenousAccess.check p.Access  [ patCat1.Access ]
        |])
        ||> Array.fold(fun acc pred ->
            acc
            |> Array.filter pred
        )
        |> fun xs -> xs |> Array.length > 0



    /// Prints an age in days as a string.
    let printAge age =
        let age =
            age
            |> ValueUnit.convertTo Units.Time.day
            |> ValueUnit.getValue
            |> Array.tryHead
            |> Option.defaultValue 0N
        let a = age |> BigRational.ToInt32
        match a with
        | _ when a < 7 ->
            if a = 1 then $"%i{a} dag"
            else $"%i{a} dagen"
        | _ when a <= 30 ->
            let a = a / 7
            if a = 1 then $"%i{a} week"
            else $"%i{a} weken"
        | _ when a < 365 ->
            let a = a / 30
            if a = 1 then $"%i{a} maand"
            else $"%i{a} maanden"
        | _ ->
            let a = a / 365
            if a = 1 then $"%A{a} jaar"
            else $"%A{a} jaar"


    /// Print days as weeks.
    let printDaysToWeeks d =
        let d =
            d
            |> ValueUnit.convertTo Units.Time.day
            |> ValueUnit.getValue
            |> Array.tryHead
            |> Option.defaultValue 0N

        let d = d |> BigRational.ToInt32
        (d / 7) |> sprintf "%i weken"


    /// Print an MinMax age as a string.
    let printAgeMinMax (age : MinMax) =
        let printAge = Limit.getValueUnit >> printAge
        match age.Min, age.Max with
        | Some min, Some max ->
            let min = min |> printAge
            let max = max |> printAge
            $"leeftijd %s{min} tot %s{max}"
        | Some min, None ->
            let min = min |> printAge
            $"leeftijd vanaf %s{min}"
        | None, Some max ->
            let max = max |> printAge
            $"leeftijd tot %s{max}"
        | _ -> ""



    /// Print an PatientCategory as a string.
    let toString (pat : PatientCategory) =

        let gender = pat.Gender |> Gender.toString

        let age = pat.Age |> printAgeMinMax

        let neonate =
            let s =
                if pat.GestAge.Max.IsSome &&
                   pat.GestAge.Max.Value
                   |> Limit.getValueUnit  <? Utils.ValueUnit.ageFullTerm then "prematuren"
                else "neonaten"

            let printDaysToWeeks = Limit.getValueUnit >> printDaysToWeeks

            match pat.GestAge.Min, pat.GestAge.Max, pat.PMAge.Min, pat.PMAge.Max with
            | _, _, Some min, Some max ->
                let min = min |> printDaysToWeeks
                let max = max |> printDaysToWeeks
                $"{s} postconceptie leeftijd %s{min} tot %s{max}"
            | _, _, Some min, None ->
                let min = min |> printDaysToWeeks
                $"{s} postconceptie leeftijd vanaf %s{min}"
            | _, _, None, Some max ->
                let max = max |> printDaysToWeeks
                $"{s} postconceptie leeftijd tot %s{max}"

            | Some min, Some max, _, _ ->
                let min = min |> printDaysToWeeks
                let max = max |> printDaysToWeeks
                $"{s} zwangerschapsduur %s{min} tot %s{max}"
            | Some min, None, _, _ ->
                let min = min |> printDaysToWeeks
                if s = "neonaten" then s
                else
                    $"{s} zwangerschapsduur vanaf %s{min}"
            | None, Some max, _, _ ->
                let max = max |> printDaysToWeeks
                $"{s} zwangerschapsduur tot %s{max}"
            | _ -> ""

        let weight =
            let toStr lim =
                lim
                |> Limit.getValueUnit
                |> ValueUnit.convertTo Units.Weight.kiloGram
                |> Utils.ValueUnit.toString -1

            match pat.Weight.Min, pat.Weight.Max with
            | Some min, Some max -> $"gewicht %s{min |> toStr} tot %s{max |> toStr}"
            | Some min, None     -> $"gewicht vanaf %s{min |> toStr}"
            | None,     Some max -> $"gewicht tot %s{max |> toStr}"
            | None,     None     -> ""

        [
            pat.Department |> Option.defaultValue ""
            gender
            neonate
            age
            weight
        ]
        |> List.filter String.notEmpty
        |> List.filter (String.isNullOrWhiteSpace >> not)
        |> String.concat ", "



module Patient =

    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Conversions = Informedica.GenCore.Lib.Conversions
    module Limit = Informedica.GenCore.Lib.Ranges.Limit

    open Utils

    /// An empty Patient.
    let patient =
        {
            Location = None
            Department = None
            Diagnoses = [||]
            Gender = AnyGender
            Age = None
            Weight = None
            Height = None
            GestAge = None
            PMAge = None
            Access = [ ]
            RenalFunction = None
        }


    let correctAdjustUnit (pat : Patient) =
        { pat with
            Weight =
                pat.Weight
                |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)
        }


    let calcPMAge (pat: Patient) =
        { pat with
            PMAge =
                match pat.Age, pat.GestAge with
                | Some ad, Some ga -> ad + ga |> Some
                | _ -> None
        }


    /// Calculate the BSA of a Patient.
    let calcBSA (pat: Patient) =
        match pat.Weight, pat.Height with
        | Some w, Some h ->
            Calculations.calcDuBois w h
            |> Some
        | _ -> None


    /// Get the string representation of a Patient.
    let rec toString (pat: Patient) =
        [
            pat.Department |> Option.defaultValue ""
            pat.Gender |> Gender.toString
            pat.Age
            |> Option.map PatientCategory.printAge
            |> Option.defaultValue ""

            let printDaysToWeeks = PatientCategory.printDaysToWeeks

            let s =
                if pat.GestAge.IsSome &&
                   pat.GestAge.Value  < Utils.ValueUnit.ageFullTerm then "prematuren"
                else "neonaten"

            match pat.GestAge, pat.PMAge with
            | _, Some a ->
                let a = a |> printDaysToWeeks
                $"{s} postconceptie leeftijd %s{a}"
            | Some a, _ ->
                let a = a |> printDaysToWeeks
                $"{s} zwangerschapsduur %s{a}"
            | _ -> ""

            let toStr u vu =
                let v =
                    vu
                    |> ValueUnit.convertTo u
                    |> ValueUnit.getValue
                    |> Array.tryHead
                    |> Option.defaultValue 0N
                if v.Denominator = 1I then v |> BigRational.ToInt32 |> sprintf "%i"
                else
                    v
                    |> BigRational.ToDouble
                    |> sprintf "%A"

            pat.Weight
            |> Option.map (fun w -> $"gewicht %s{w |> toStr Units.Mass.kiloGram } kg")
            |> Option.defaultValue ""

            pat.Height
            |> Option.map (fun h -> $"lengte {h |> toStr Units.Height.centiMeter} cm")
            |> Option.defaultValue ""

            pat
            |> calcBSA
            |> Option.map (fun bsa ->
                $"BSA {bsa |> Utils.ValueUnit.toString 3}"
            )
            |> Option.defaultValue ""
        ]
        |> List.filter String.notEmpty
        |> List.filter (String.isNullOrWhiteSpace >> not)
        |> String.concat ", "