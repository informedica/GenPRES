namespace Informedica.GenForm.Lib


module VenousAccess =


    let check location venousAccess =
        match location, venousAccess with
        | AnyAccess, _ -> true
        | _, xs when xs |> List.isEmpty -> true
        | _ -> venousAccess |> List.exists ((=) location)


    /// Returns true when one patient can have both access devices: AnyAccess goes with every device, and
    /// other devices must be equal.
    let canMeet a b =
        match a, b with
        | AnyAccess, _
        | _, AnyAccess -> true
        | a, b -> a = b


module Gender =

    open Informedica.Utils.Lib.BCL

    type CoreGender = Informedica.GenCore.Lib.Patients.Gender


    /// Parses man or vrouw, in any case. Any other string gives AnyGender.
    let fromString s =
        let s = s |> String.toLower |> String.trim

        match s with
        | "man" -> Male
        | "vrouw" -> Female
        | _ -> AnyGender


    /// Get the string representation of a Gender.
    let toString =
        function
        | Male -> "man"
        | Female -> "vrouw"
        | AnyGender -> ""


    /// Parses the string form of a gender. An empty string gives AnyGender, and any other string that
    /// toString does not write gives None.
    let tryFromString s =
        match s |> String.toLower |> String.trim with
        | "man" -> Some Male
        | "vrouw" -> Some Female
        | "" -> Some AnyGender
        | _ -> None


    /// Maps the gender of a patient to the gender of the rules. An unknown gender matches only the rules
    /// for any gender.
    let ofPatient =
        function
        | CoreGender.Male -> Male
        | CoreGender.Female -> Female
        | CoreGender.UnknownGender -> AnyGender


    /// Returns true when gender is AnyGender or equals filterGender.
    let isMatch gender filterGender =
        match filterGender, gender with
        | _, AnyGender -> true
        | _ -> filterGender = gender


module PatientCategory =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenUnits.Lib

    module BSA = Informedica.GenCore.Lib.Calculations.BSA
    module Limit = Informedica.GenCore.Lib.Ranges.Limit
    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
    module Conversions = Informedica.GenCore.Lib.Conversions
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module Measures = Informedica.GenCore.Lib.ValueUnits.ValueUnit.Measures
    module Constants = Informedica.GenCore.Lib.Constants


    open Utils


    let empty: PatientCategory =
        {
            Location = None
            Department = None
            Gender = AnyGender
            Age = MinMax.empty |> AbsoluteAge
            Weight = MinMax.empty
            BSA = MinMax.empty
            GestAge = MinMax.empty
            PMAge = MinMax.empty
            Access = AnyAccess
        }


    let isEmpty (p: PatientCategory) =
        { empty with
            Department = p.Department
            Access = p.Access
        }
            =
            p


    /// Returns the age range of a category: the absolute range, or for an adult category the range from
    /// the adult age of 18 years up.
    let getAge (pat: PatientCategory) =
        match pat.Age with
        | AbsoluteAge a -> a
        | IsAdult -> { MinMax.empty with Min = Some(Inclusive Utils.ValueUnit.ageAdult) }


    /// Returns a patient category with an age range in days and a weight range in grams, and no limit on
    /// any other value. Each range has an inclusive minimum and an exclusive maximum.
    let ofAgeAndWeight minAge maxAge minWeight maxWeight =
        { empty with
            Age =
                (minAge, maxAge)
                |> MinMax.fromTuple Inclusive Exclusive (Some Units.day)
                |> AbsoluteAge
            Weight =
                (minWeight, maxWeight)
                |> MinMax.fromTuple Inclusive Exclusive (Some Units.weightGram)
        }


    /// Returns true when the two values are equal, or when either value is None.
    let canMeet a b =
        match a, b with
        | Some a, Some b -> a = b
        | _ -> true


    /// Whether one patient can be in both categories. Categories for a CVL and for a PVL have no
    /// patient in common, because the access device tells them apart. Department names are compared
    /// apart from capitals and surrounding spaces.
    let canShare (a: PatientCategory) (b: PatientCategory) =
        MinMax.canMeet (a |> getAge) (b |> getAge)
        && VenousAccess.canMeet a.Access b.Access
        && (Option.map2 String.equalsCapInsens a.Department b.Department
            |> Option.defaultValue true)
        && canMeet a.Location b.Location
        && MinMax.canMeet a.Weight b.Weight
        && MinMax.canMeet a.BSA b.BSA
        && MinMax.canMeet a.GestAge b.GestAge
        && MinMax.canMeet a.PMAge b.PMAge


    /// Returns a sort value for a patient category. The value is the sum of the minimum age in days, plus 300
    /// when above zero, the minimum gestational and post-menstrual age in days, term when absent, and the
    /// minimum weight divided by 1000.
    let sortBy (pat: PatientCategory) =
        let toInt def =
            function
            | Some x ->
                x
                |> Limit.getValueUnit
                |> ValueUnit.getValue
                |> Array.tryHead
                |> function
                    | None -> 0
                    | Some x -> x |> BigRational.ToInt32
            | None -> def

        // a rule without a minimum gestational or post-menstrual age sorts as term
        let term = CoreAgeWeeksDays.term |> CoreAgeWeeksDays.toDays |> int

        ((pat |> getAge).Min |> toInt 0 |> (fun i -> if i > 0 then i + 300 else i))
        + (pat.GestAge.Min |> toInt term)
        + (pat.PMAge.Min |> toInt term)
        + (pat.Weight.Min |> toInt 0 |> (fun w -> w / 1000))


    /// Returns true when a rule applies to the department of a patient. A rule without a department
    /// applies to every patient. A patient without a department matches only those rules; otherwise the
    /// names must be equal, apart from capitals and surrounding spaces.
    let departmentMatches rule patient =
        match rule, patient with
        | None, _ -> true
        | Some _, None -> false
        | Some rule, Some patient -> String.equalsCapInsens rule patient


    /// Returns true when a rule applies to the location of a patient. A rule without a location applies to every
    /// patient. A rule with a location applies only to a patient at that location, apart from capitals and
    /// surrounding spaces.
    let locationMatches rule patient =
        match rule, patient with
        | None, _ -> true
        | Some _, None -> false
        | Some rule, Some patient -> String.equalsCapInsens rule patient


    /// Prints an age in days as a string, in its largest unit: days below one week, weeks up to 30
    /// days, months below one year, years from 365 days, each count rounded down.
    let printAge age =
        let a = age |> Measures.toSingleIntDay |> int

        match a with
        | _ when a < Constants.daysInWeek -> if a = 1 then $"%i{a} dag" else $"%i{a} dagen"
        | _ when a <= Constants.daysInMonth ->
            let a = a / Constants.daysInWeek
            if a = 1 then $"%i{a} week" else $"%i{a} weken"
        | _ when a < Constants.daysInYear ->
            let a = a / Constants.daysInMonth
            if a = 1 then $"%i{a} maand" else $"%i{a} maanden"
        | _ -> $"%i{a / Constants.daysInYear} jaar"


    /// Print days as weeks.
    let printDaysToWeeks d =
        let weeks = (d |> Measures.toSingleIntDay |> int) / Constants.daysInWeek
        $"%i{weeks} weken"


    /// Prints an age range as Dutch text, such as leeftijd vanaf 1 jaar. An empty range gives an empty
    /// string.
    let printAgeMinMax (age: MinMax) =
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


    /// Prints the age of a category: volwassenen for an adult category, otherwise the age range.
    let ageToString (pat: PatientCategory) =
        match pat.Age with
        | IsAdult -> "volwassenen"
        | AbsoluteAge a -> a |> printAgeMinMax


    /// Prints a patient category as Dutch text: the department, gender, neonatal ages, age and weight.
    let toString (pat: PatientCategory) =

        let gender = pat.Gender |> Gender.toString

        let age = pat |> ageToString

        let neonate =
            let s =
                if
                    pat.GestAge.Max.IsSome
                    && pat.GestAge.Max.Value |> Limit.getValueUnit |> CoreAgeWeeksDays.isPretermValue
                then
                    "prematuren"
                else
                    "neonaten"

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

                if s = "neonaten" then
                    s
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
            | Some min, None -> $"gewicht vanaf %s{min |> toStr}"
            | None, Some max -> $"gewicht tot %s{max |> toStr}"
            | None, None -> ""

        [ pat.Department |> Option.defaultValue ""; gender; neonate; age; weight ]
        |> List.filter String.notEmpty
        |> List.filter String.notEmpty
        |> String.concat ", "


    /// Returns the ranges of a patient category, each with its name. An adult category has no age range
    /// in the list.
    let ranges (pc: PatientCategory) =
        [
            match pc.Age with
            | AbsoluteAge mm -> "age", mm
            | IsAdult -> ()
            "weight", pc.Weight
            "BSA", pc.BSA
            "gestational age", pc.GestAge
            "post-menstrual age", pc.PMAge
        ]


/// Maps the access of a patient to an access device, and an access device to and from its string form
/// for the Dto.
module AccessDevice =

    open Informedica.Utils.Lib.BCL

    type CoreAccess = Informedica.GenCore.Lib.Patients.Access


    /// Maps the access of a patient to the access device of the rules.
    let ofPatient =
        function
        | CoreAccess.PVL -> PVL
        | CoreAccess.CVL -> CVL
        | CoreAccess.EnteralTube -> EnteralTube


    let toString =
        function
        | PVL -> "pvl"
        | CVL -> "cvl"
        | EnteralTube -> "enteral-tube"
        | AnyAccess -> "any"


    let tryFromString s =
        match s |> String.toLower |> String.trim with
        | "pvl" -> Some PVL
        | "cvl" -> Some CVL
        | "enteral-tube" -> Some EnteralTube
        | "any" -> Some AnyAccess
        | _ -> None


/// The string form of a renal function, for the Dto: egfr:min:max with an empty
/// bound for None, or the name of a dialysis.
module RenalFunction =

    open Informedica.Utils.Lib.BCL

    type CoreRenalFunction = Informedica.GenCore.Lib.Patients.RenalFunction
    type EGFRBand = Informedica.GenCore.Lib.Patients.EGFRBand


    /// Maps the renal function of a patient to the renal function of the renal rules. An eGFR band
    /// becomes the pair of its bounds.
    let ofPatient =
        function
        | CoreRenalFunction.EGFR EGFRBand.Above50 -> EGFR(Some 50, None)
        | CoreRenalFunction.EGFR EGFRBand.From30To50 -> EGFR(Some 30, Some 50)
        | CoreRenalFunction.EGFR EGFRBand.From10To30 -> EGFR(Some 10, Some 30)
        | CoreRenalFunction.EGFR EGFRBand.UpTo10 -> EGFR(None, Some 10)
        | CoreRenalFunction.IntermittentHemodialysis -> IntermittentHemodialysis
        | CoreRenalFunction.ContinuousHemodialysis -> ContinuousHemodialysis
        | CoreRenalFunction.PeritonealDialysis -> PeritonealDialysis


    let toString =
        function
        | EGFR(min, max) ->
            let bound = Option.map string >> Option.defaultValue ""
            $"egfr:{bound min}:{bound max}"
        | IntermittentHemodialysis -> "intermittent-hemodialysis"
        | ContinuousHemodialysis -> "continuous-hemodialysis"
        | PeritonealDialysis -> "peritoneal-dialysis"


    let tryFromString (s: string) =
        let bound (b: string) =
            match b.Trim() with
            | "" -> Some None
            | b ->
                match System.Int32.TryParse b with
                | true, n -> Some(Some n)
                | _ -> None

        match s |> String.toLower |> String.trim with
        | "intermittent-hemodialysis" -> Some IntermittentHemodialysis
        | "continuous-hemodialysis" -> Some ContinuousHemodialysis
        | "peritoneal-dialysis" -> Some PeritonealDialysis
        | s when s.StartsWith "egfr:" ->
            match s.Split ':' with
            | [| _; min; max |] ->
                match bound min, bound max with
                | Some min, Some max -> Some(EGFR(min, max))
                | _ -> None
            | _ -> None
        | _ -> None


/// Reads the GenCORE patient data for rule selection: the values of a patient in the terms of the
/// rules, whether a patient is an adult, and whether a patient matches a patient category.
module PatientData =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges

    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreAdultMark = Informedica.GenCore.Lib.Patients.AdultMark
    module CoreDepartment = Informedica.GenCore.Lib.Patients.Department
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    type CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
    type CoreAdultMark = Informedica.GenCore.Lib.Patients.AdultMark

    open Utils


    /// Patient data without any value.
    let unknown = CorePatientData.unknown


    /// The name of the department. A rule names a department by this name.
    let department (dat: CorePatientData) = dat.Department |> CoreDepartment.name


    /// The gender in the terms of the rules. An unknown gender matches the rules for any gender.
    let gender (dat: CorePatientData) = dat.Gender |> Gender.ofPatient


    /// The access devices in the terms of the rules.
    let access (dat: CorePatientData) = dat.Access |> List.map AccessDevice.ofPatient


    /// The renal function in the terms of the renal rules.
    let renalFunction (dat: CorePatientData) = dat.RenalFunction |> Option.map RenalFunction.ofPatient


    /// True when the adult mark of the patient data says adult.
    let isAdult (dat: CorePatientData) = dat.AdultMark |> CoreAdultMark.isAdult


    /// Whether the rules for adults apply because of the mark: a marked patient of 16 or 17 years,
    /// or a marked patient without an age.
    let matchedAsAdult (dat: CorePatientData) = dat.AdultMark = CoreAdultMark.MarkableAdult true


    /// Gives a patient the age of 18 years and removes the gestational and post-menstrual age. A
    /// gestational age has no meaning for a patient older than one year.
    let asAdult (dat: CorePatientData) =
        { dat with
            Age = Some CoreAgeValue.eighteen
            GestationalAge = None
            PostMenstrualAge = None
        }


    /// Returns the patient to match and an extra check on the age range of a rule. For a patient
    /// matched as an adult, the patient gets the adult age and the age range must have no maximum.
    /// Any other patient is returned unchanged, with a check that always passes.
    let forAdultMatch dat =
        if dat |> matchedAsAdult then
            dat |> asAdult, (fun (age: MinMax) -> age.Max.IsNone)
        else
            dat, (fun _ -> true)


    /// Whether the age and the weight of a patient are in the ranges of a patient category. A patient
    /// without an age or a weight does not match a range for that value. A patient matched as an adult
    /// has the age of 18 years and matches only an age range without a maximum.
    let matchesAgeAndWeight dat (cat: PatientCategory) =
        let dat, ageHolds = dat |> forAdultMatch
        let age = cat |> PatientCategory.getAge

        age |> ageHolds
        && dat |> CorePatientData.ageInDays |> MinMax.inRange age
        && dat |> CorePatientData.weightInKg |> MinMax.inRange cat.Weight


    /// Whether a patient matches a patient category. The location and the gender are not checked.
    let matchesCategoryWithoutLocationAndGender dat patCat =
        let ageAndWeightMatch = patCat |> matchesAgeAndWeight dat
        let dat, _ = dat |> forAdultMatch
        let gestAge = dat |> CorePatientData.gestationalAgeInDays

        let agesMatch () =
            // A category for all patients does not check the gestational or post-menstrual age.
            if patCat |> PatientCategory.isEmpty then
                true
            else
                let gestAgeMatches =
                    match gestAge with
                    // A preterm patient does not match a rule without a gestational or post-menstrual age.
                    | Some ga when patCat.GestAge = MinMax.empty && patCat.PMAge = MinMax.empty ->
                        ga |> CoreAgeWeeksDays.isPretermValue |> not
                    | _ -> gestAge |> MinMax.inRange patCat.GestAge

                gestAgeMatches
                && dat |> CorePatientData.postMenstrualAgeInDays |> MinMax.inRange patCat.PMAge

        ageAndWeightMatch
        && dat |> department |> PatientCategory.departmentMatches patCat.Department
        && dat |> CorePatientData.bodySurfaceArea |> MinMax.inRange patCat.BSA
        && agesMatch ()
        && VenousAccess.check patCat.Access (dat |> access)


    /// Whether a patient matches a patient category, including the location and the gender. A category
    /// with a location matches only a patient at that location.
    let matchesCategory (dat: CorePatientData) (patCat: PatientCategory) =
        PatientCategory.locationMatches patCat.Location dat.Location
        && Gender.isMatch patCat.Gender (dat |> gender)
        && matchesCategoryWithoutLocationAndGender dat patCat


    /// Returns the value to adjust a limit with: the weight in kg for the adjust unit kg, the body surface
    /// area for any other adjust unit. Returns None without an adjust unit or without that value.
    let adjustValue dat adjustUnit =
        adjustUnit
        |> Option.bind (fun u ->
            if u |> Units.eqsUnit Units.Weight.kiloGram then
                dat |> CorePatientData.weightInKg
            else
                dat |> CorePatientData.bodySurfaceArea
        )


    /// Whether the patient has any value other than the department.
    let hasDataBesidesDepartment (dat: CorePatientData) =
        { dat with Department = CoreDepartment.unknown } <> CorePatientData.unknown


    /// Prints the patient data as Dutch text.
    let toString dat =
        let gestAge = dat |> CorePatientData.gestationalAgeInDays

        let toStr u vu =
            let v =
                vu
                |> ValueUnit.convertTo u
                |> ValueUnit.getValue
                |> Array.tryHead
                |> Option.defaultValue 0N

            if v.Denominator = 1I then
                v |> BigRational.ToInt32 |> sprintf "%i"
            else
                v |> BigRational.ToDouble |> sprintf "%A"

        let group =
            if gestAge |> Option.exists CoreAgeWeeksDays.isPretermValue then
                "prematuren"
            else
                "neonaten"

        [
            dat |> department |> Option.defaultValue ""
            dat |> gender |> Gender.toString
            dat
            |> CorePatientData.ageInDays
            |> Option.map PatientCategory.printAge
            |> Option.defaultValue ""

            match gestAge, dat |> CorePatientData.postMenstrualAgeInDays with
            | _, Some a -> $"%s{group} postconceptie leeftijd %s{a |> PatientCategory.printDaysToWeeks}"
            | Some a, _ -> $"%s{group} zwangerschapsduur %s{a |> PatientCategory.printDaysToWeeks}"
            | _ -> ""

            dat
            |> CorePatientData.weightInKg
            |> Option.map (fun w -> $"gewicht %s{w |> toStr Units.Mass.kiloGram} kg")
            |> Option.defaultValue ""

            dat
            |> CorePatientData.heightInCm
            |> Option.map (fun h -> $"lengte %s{h |> toStr Units.Height.centiMeter} cm")
            |> Option.defaultValue ""

            dat
            |> CorePatientData.bodySurfaceArea
            |> Option.map (fun bsa -> $"BSA %s{bsa |> Utils.ValueUnit.toString 3}")
            |> Option.defaultValue ""
        ]
        |> List.filter String.notEmpty
        |> String.concat ", "
