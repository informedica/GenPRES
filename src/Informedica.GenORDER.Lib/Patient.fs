namespace Informedica.GenOrder.Lib


/// Functions that read and set the values of patient data.
module Patient =


    open Informedica.GenForm.Lib

    module Conversions = Informedica.GenCore.Lib.Conversions
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreDepartment = Informedica.GenCore.Lib.Patients.Department
    module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
    module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
    module CoreHeight = Informedica.GenCore.Lib.Patients.Height
    module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
    module CoreSource = Informedica.GenCore.Lib.Patients.Source
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

    type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>


    /// The parts of an age and their conversion to and from a GenCORE age.
    [<AutoOpen>]
    module AgeParts =


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


        /// The number of days of a list of age parts.
        let daysOfParts ags =
            ags
            |> List.sumBy (ageValueOf >> CoreAgeValue.getAgeInDays >> int)
            |> Conversions.dayFromInt


        /// The age parts of an age, as GenCORE splits the whole days; the parts that are zero are
        /// left out.
        let partsOfAge age =
            let ys, ms, ws, ds =
                age
                |> CoreAgeValue.getAgeInDays
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


    let getGender (p: PatientData) = p.Gender


    let setGender g (p: PatientData) = { p with Gender = g }


    /// The age as a list of age parts. The list is empty when there is no age.
    let getAge (p: PatientData) = p.Age |> Option.map partsOfAge |> Option.defaultValue []


    /// Sets the post-menstrual age that GenCORE calculates from the age and the gestational age, or
    /// None when either is missing. The age and gestational age setters both call this function, so
    /// the order of those calls does not matter.
    let withPMAge (p: PatientData) =
        { p with PostMenstrualAge = Option.map2 CoreAgeWeeksDays.postMenstrualAge p.Age p.GestationalAge }


    let setAge ags (p: PatientData) =
        { p with Age = ags |> daysOfParts |> CoreAgeValue.fromDays |> Some }
        |> withPMAge


    /// The weight used for calculation, measured or estimated.
    let getWeight (p: PatientData) =
        p.Weight.Calculation |> Option.map (CoreSource.value >> _.Weight)


    /// Sets the weight used for calculation, with its date. The source says whether the weight is
    /// measured or estimated. None removes the weight.
    let setWeightFrom source date wght (p: PatientData) =
        { p with
            Weight =
                wght
                |> Option.map (CoreWeightAtDate.create date >> source)
                |> CoreWeight.create [] None []
        }


    /// Sets a weight that was measured on a date.
    let setWeight date wght p = p |> setWeightFrom CoreSource.Measured date wght


    /// Sets a weight that was estimated on a date.
    let setEstimatedWeight date wght p = p |> setWeightFrom CoreSource.Estimated date wght


    /// The height used for calculation, measured or estimated.
    let getHeight (p: PatientData) =
        p.Height.Calculation |> Option.map (CoreSource.value >> _.Height)


    /// Sets the height used for calculation, with its date. The source says whether the height is
    /// measured or estimated. None removes the height.
    let setHeightFrom source date hght (p: PatientData) =
        { p with
            Height =
                hght
                |> Option.map (CoreHeightAtDate.create date >> source)
                |> CoreHeight.create [] None
        }


    /// Sets a height that was measured on a date.
    let setHeight date hght p = p |> setHeightFrom CoreSource.Measured date hght


    /// Sets a height that was estimated on a date.
    let setEstimatedHeight date hght p = p |> setHeightFrom CoreSource.Estimated date hght


    let setGestAge ags (p: PatientData) =
        { p with GestationalAge = ags |> daysOfParts |> CoreAgeWeeksDays.fromDays |> Some }
        |> withPMAge


    /// The name of the department.
    let getDepartment (p: PatientData) = p |> PatientData.department


    /// Sets the department by its name. None sets an unknown department.
    let setDepartment name (p: PatientData) =
        { p with
            Department =
                name
                |> Option.map CoreDepartment.otherDepartment
                |> Option.defaultValue CoreDepartment.unknown
        }


    /// Reads patient data from a Dto on a date. Returns the errors when a value is invalid or when
    /// the data is not enough to select rules.
    let fromDto today dto =
        dto
        |> CorePatientData.Dto.fromDto today
        |> Result.mapError (Validus.ValidationErrors.toList >> List.map DtoError.PatientData)
        |> Result.bind (
            CorePatientData.checkMinimumData
            >> Result.mapError (DtoError.PatientDataNotEnough >> List.singleton)
        )
