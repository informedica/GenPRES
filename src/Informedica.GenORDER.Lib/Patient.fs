namespace Informedica.GenOrder.Lib


/// Functions that read the values of patient data.
module Patient =


    open Informedica.GenForm.Lib
    open Validus

    module Conversions = Informedica.GenCore.Lib.Conversions
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreSource = Informedica.GenCore.Lib.Patients.Source
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData


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


    /// The age as a list of age parts. The list is empty when there is no age.
    let getAge (p: PatientData) = p.Age |> Option.map partsOfAge |> Option.defaultValue []


    /// The weight used for calculation, measured or estimated.
    let getWeight (p: PatientData) =
        p.Weight.Calculation |> Option.map (CoreSource.value >> _.Weight)


    /// The height used for calculation, measured or estimated.
    let getHeight (p: PatientData) =
        p.Height.Calculation |> Option.map (CoreSource.value >> _.Height)


    /// The name of the department.
    let getDepartment (p: PatientData) = p |> PatientData.department


    /// Reads patient data from a Dto on a date. Returns the errors when a value is invalid or when
    /// the data is not enough to select rules.
    let fromDto today dto =
        dto
        |> CorePatientData.Dto.fromDto today None
        |> Result.mapError (ValidationErrors.toList >> List.map DtoError.PatientData)
        |> Result.bind (
            CorePatientData.checkMinimumData
            >> Result.mapError (DtoError.PatientDataNotEnough >> List.singleton)
        )
