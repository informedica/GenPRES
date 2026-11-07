namespace Informedica.GenOrder.Lib


module Patient =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenForm.Lib
    open Informedica.GenUnits.Lib

    module Conversions = Informedica.GenCore.Lib.Conversions
    module Measures = Informedica.GenCore.Lib.ValueUnits.ValueUnit.Measures
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CorePatient = Informedica.GenCore.Lib.Patients.Patient
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    type Patient = Types.Patient
    type Access = AccessDevice


    /// <summary>
    /// And empty Patient
    /// </summary>
    let patient: Patient =
        {
            Location = None
            Department = None
            Diagnoses = [||]
            Gender = AnyGender
            Age = None
            Weight = None
            Height = None
            WeightMeasured = true
            HeightMeasured = true
            MarkedAdult = false
            GestAge = None
            PMAge = None
            Access = []
            RenalFunction = None
        }


    [<AutoOpen>]
    module Optics =


        type Age =
            | Years of int
            | Months of int
            | Weeks of int
            | Days of int


        /// The age value of one age part.
        let ageValueOf =
            function
            | Years x -> CoreAgeValue.zero |> CoreAgeValue.SetGet.setIntYears x
            | Months x -> CoreAgeValue.zero |> CoreAgeValue.SetGet.setIntMonths x
            | Weeks x -> CoreAgeValue.zero |> CoreAgeValue.SetGet.setIntWeeks x
            | Days x -> CoreAgeValue.zero |> CoreAgeValue.SetGet.setIntDays x


        /// The age in days of a list of age parts, as GenCORE counts it.
        let ageToValueUnit ags =
            ags
            |> List.sumBy (ageValueOf >> CoreAgeValue.getAgeInDays >> int)
            |> Conversions.dayFromInt
            |> Measures.fromIntDay


        /// The age parts of an age, as GenCORE splits the whole days; the parts that are zero are
        /// left out.
        let ageFromValueUnit vu =
            let ys, ms, ws, ds =
                vu
                |> Measures.toSingleIntDay
                |> CoreAgeValue.fromDays
                |> CoreAgeValue.getDef0
                |> fun (y, m, w, d) -> int y, int m, int w, int d

            [ Years ys; Months ms; Weeks ws; Days ds ]
            |> List.filter (
                function
                | Years n
                | Months n
                | Weeks n
                | Days n -> n > 0
            )

        // Helper pair (to list of Age / from list of Age) for the get/set below
        let ageAgeList = Option.map ageFromValueUnit >> (Option.defaultValue []), (ageToValueUnit >> Some)


        type Weight =
            | Kilogram of decimal
            | Gram of int


        let vuWeight =
            let get w =
                w
                |> ValueUnit.convertTo Units.Weight.gram
                |> ValueUnit.getValue
                |> Array.head
                |> BigRational.toDecimal
                |> int
                |> Gram

            let set w =
                match w with
                | Kilogram w -> w * 1000m
                | Gram w -> w |> decimal
                |> BigRational.fromDecimal
                |> ValueUnit.singleWithUnit Units.Weight.gram

            Option.map get, Option.map set


        type Height =
            | Meter of decimal
            | Centimeter of int


        let vuHeight =
            let get h =
                h
                |> ValueUnit.convertTo Units.Height.centiMeter
                |> ValueUnit.getValue
                |> Array.head
                |> BigRational.toDecimal
                |> int
                |> Centimeter

            let set h =
                match h with
                | Meter h -> h * 100m
                | Centimeter h -> h |> decimal
                |> BigRational.fromDecimal
                |> ValueUnit.singleWithUnit Units.Height.centiMeter

            Option.map get, Option.map set


    let getGender (p: Patient) = p.Gender


    let setGender g (p: Patient) = { p with Gender = g }


    let getAge (p: Patient) = p.Age |> fst ageAgeList


    /// The patient with the post-menstrual age GenCORE calculates from its age and its gestational
    /// age; none without either. The setters of the age and of the gestational age both call it, so
    /// the order they are called in does not matter.
    let withPMAge (p: Patient) =
        { CorePatientData.unknown with
            Age = p.Age |> Option.map (Measures.toSingleIntDay >> CoreAgeValue.fromDays)
            GestationalAge = p.GestAge |> Option.map CoreAgeWeeksDays.fromValueUnit
        }
        |> CorePatient.create None
        |> CorePatient.calculatePostMenstrualAge
        |> _.Data.PostMenstrualAge
        |> fun pma -> { p with PMAge = pma |> Option.map CoreAgeWeeksDays.toValueUnit }


    let setAge ags (p: Patient) = { p with Age = ags |> snd ageAgeList } |> withPMAge


    let getWeight (p: Patient) = p.Weight |> fst vuWeight


    let setWeight w (p: Patient) = { p with Weight = w |> snd vuWeight }


    let getHeight (p: Patient) = p.Height |> fst vuHeight


    let setHeight h (p: Patient) = { p with Height = h |> snd vuHeight }


    let setGestAge ags (p: Patient) = { p with GestAge = ags |> ageToValueUnit |> Some } |> withPMAge


    let getDepartment (p: Patient) = p.Department


    let setDepartment d (p: Patient) = { p with Department = d }


    let premature =
        patient
        |> setAge [ 1 |> Weeks ]
        |> setGestAge [ 32 |> Weeks ]
        |> setWeight (1200 |> Gram |> Some)
        |> setHeight (45 |> Centimeter |> Some)
        |> setDepartment (Some "NEO")


    let newBorn =
        patient
        |> setAge [ 1 |> Weeks ]
        |> setWeight (3.5m |> Kilogram |> Some)
        |> setHeight (60 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let infant =
        patient
        |> setAge [ 1 |> Years ]
        |> setWeight (11.5m |> Kilogram |> Some)
        |> setHeight (70 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let toddler =
        patient
        |> setAge [ 3 |> Years ]
        |> setWeight (15m |> Kilogram |> Some)
        |> setHeight (90 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let child =
        patient
        |> setAge [ 4 |> Years ]
        |> setWeight (17m |> Kilogram |> Some)
        |> setHeight (100 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")
        |> fun p -> { p with Access = [ CVL ] }


    let teenager =
        patient
        |> setAge [ 12 |> Years ]
        |> setWeight (40m |> Kilogram |> Some)
        |> setHeight (150 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")


    let adult =
        patient
        |> setAge [ 18 |> Years ]
        |> setWeight (70m |> Kilogram |> Some)
        |> setHeight (180 |> Centimeter |> Some)
        |> setDepartment (Some "ICK")
