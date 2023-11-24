namespace Informedica.GenForm.Lib


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
    let filter pat (filter : Filter) =
        match filter.Gender, pat with
        | AnyGender, _ -> true
        | _ -> filter.Gender = pat



module PatientCategory =


    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Conversions = Informedica.GenCore.Lib.Conversions


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
        let toInt = function
            | Some x -> x |> BigRational.ToInt32
            | None -> 0

        (pat.Age.Minimum |> toInt |> fun i -> if i > 0 then i + 300 else i) +
        (pat.GestAge.Minimum |> toInt) +
        (pat.PMAge.Minimum |> toInt) +
        (pat.Weight.Minimum |> Option.map (fun w -> w / 1000N) |> toInt)


    /// <summary>
    /// Check whether a Filter belongs to a PatientCategory.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <param name="pat">The Patient Category</param>
    let filter (filter : Filter) (pat : PatientCategory) =
        let eqs a b =
            match a, b with
            | None, _
            | _, None -> true
            | Some a, Some b -> a = b

        ([| pat |]
        |> Array.filter (fun p ->
            if filter.Diagnoses |> Array.isEmpty then true
            else
                p.Diagnoses
                |> Array.exists (fun d ->
                    filter.Diagnoses |> Array.exists (String.equalsCapInsens d)
                )
        ),
        [|
            fun (p: PatientCategory) -> filter.Department |> eqs p.Department
            fun (p: PatientCategory) -> filter.AgeInDays |> MinMax.isBetween p.Age
            fun (p: PatientCategory) -> filter.WeightInGram |> MinMax.isBetween p.Weight
            fun (p: PatientCategory) ->
                match filter.WeightInGram, filter.HeightInCm with
                | Some w, Some h ->
                    BSA.calcDuBois (Some 3)
                        (w |> BigRational.toDecimal |> Conversions.kgFromDecimal)
                        (h |> BigRational.toDecimal |> Conversions.cmFromDecimal)
                    |> decimal
                    |> BigRational.fromDecimal
                    |> Some
                | _ -> None
                |> MinMax.isBetween p.BSA
            if filter.AgeInDays |> Option.isSome then
                yield! [|
                    fun (p: PatientCategory) ->
                        // defaults to normal gestation
                        filter.GestAgeInDays
                        |> Option.defaultValue 259N
                        |> Some
                        |> MinMax.isBetween p.GestAge
                    fun (p: PatientCategory) ->
                        // defaults to normal postmenstrual age
                        filter.PMAge
                        |> Option.defaultValue 259N
                        |> Some
                        |> MinMax.isBetween p.PMAge
                |]
            fun (p: PatientCategory) -> filter |> Gender.filter p.Gender
            fun (p: PatientCategory) ->
                match p.Location, filter.Location with
                | AnyAccess, _
                | _, AnyAccess -> true
                | _ -> p.Location = filter.Location
        |])
        ||> Array.fold(fun acc pred ->
            acc
            |> Array.filter pred
        )
        |> fun xs -> xs |> Array.length > 0


    /// <summary>
    /// Check whether an age and weight are between the
    /// specified age MinMax and weight MinMax.
    /// </summary>
    /// <param name="age">An optional age</param>
    /// <param name="weight">An optional weight</param>
    /// <param name="aMinMax">The age MinMax</param>
    /// <param name="wMinMax">The weight MinMax</param>
    /// <remarks>
    /// When age and or weight are not specified, they are
    /// considered to be between the minimum and maximum.
    /// </remarks>
    let checkAgeWeightMinMax age weight aMinMax wMinMax =
        age |> MinMax.isBetween aMinMax &&
        weight |> MinMax.isBetween wMinMax


    /// Prints an age in days as a string.
    let printAge age =
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
        let d = d |> BigRational.ToInt32
        (d / 7) |> sprintf "%i weken"


    /// Print an MinMax age as a string.
    let printAgeMinMax (age : MinMax) =
        match age.Minimum, age.Maximum with
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
                if pat.GestAge.Maximum.IsSome && pat.GestAge.Maximum.Value < 259N then "prematuren"
                else "neonaten"

            match pat.GestAge.Minimum, pat.GestAge.Maximum, pat.PMAge.Minimum, pat.PMAge.Maximum with
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
            let toStr (v : BigRational) =
                let v = v / 1000N
                if v.Denominator = 1I then v |> BigRational.ToInt32 |> sprintf "%i"
                else
                    v
                    |> BigRational.ToDouble
                    |> sprintf "%A"

            match pat.Weight.Minimum, pat.Weight.Maximum with
            | Some min, Some max -> $"gewicht %s{min |> toStr} tot %s{max |> toStr} kg"
            | Some min, None     -> $"gewicht vanaf %s{min |> toStr} kg"
            | None,     Some max -> $"gewicht tot %s{max |> toStr} kg"
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

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Conversions = Informedica.GenCore.Lib.Conversions


    /// An empty Patient.
    let patient =
        {
            Department = ""
            Diagnoses = [||]
            Gender = AnyGender
            AgeInDays = None
            WeightInGram = None
            HeightInCm = None
            GestAgeInDays = None
            PMAgeInDays = None
            VenousAccess = AnyAccess
        }


    let calcPMAge (pat: Patient) =
        { pat with
            PMAgeInDays =
                pat.AgeInDays
                |> Option.map (fun ad ->
                    (pat.GestAgeInDays
                    |> Option.defaultValue 0N) +
                    ad
                )
        }


    /// Calculate the BSA of a Patient.
    let calcBSA (pat: Patient) =
        match pat.WeightInGram, pat.HeightInCm with
        | Some w, Some h ->
            let w =
                (w |> BigRational.toDouble) / 1000.
                |> decimal
                |> Conversions.kgFromDecimal
            let h =
                h
                |> BigRational.toDouble
                |> decimal
                |> Conversions.cmFromDecimal

            BSA.calcDuBois (Some 2) w h
            |> decimal
            |> BigRational.fromDecimal
            |> Some
        | _ -> None


    /// Get the string representation of a Patient.
    let rec toString (pat: Patient) =
        [
            pat.Department
            pat.Gender |> Gender.toString
            pat.AgeInDays
            |> Option.map PatientCategory.printAge
            |> Option.defaultValue ""

            let printDaysToWeeks = PatientCategory.printDaysToWeeks

            let s =
                if pat.GestAgeInDays.IsSome && pat.GestAgeInDays.Value < 259N then "prematuren"
                else "neonaten"

            match pat.GestAgeInDays, pat.PMAgeInDays with
            | _, Some a ->
                let a = a |> printDaysToWeeks
                $"{s} postconceptie leeftijd %s{a}"
            | Some a, _ ->
                let a = a |> printDaysToWeeks
                $"{s} zwangerschapsduur %s{a}"
            | _ -> ""

            let toStr (v : BigRational) =
                let v = v / 1000N
                if v.Denominator = 1I then v |> BigRational.ToInt32 |> sprintf "%i"
                else
                    v
                    |> BigRational.toStringNl

            pat.WeightInGram
            |> Option.map (fun w -> $"gewicht %s{w |> toStr} kg")
            |> Option.defaultValue ""

            pat.HeightInCm
            |> Option.map (fun h -> $"lengte {h |> BigRational.toStringNl} cm")
            |> Option.defaultValue ""

            pat
            |> calcBSA
            |> Option.map (fun bsa ->
                let bsa =
                    bsa
                    |> BigRational.toDouble
                    |> Double.fixPrecision 2
                $"BSA {bsa} m2"
            )
            |> Option.defaultValue ""
        ]
        |> List.filter String.notEmpty
        |> List.filter (String.isNullOrWhiteSpace >> not)
        |> String.concat ", "

