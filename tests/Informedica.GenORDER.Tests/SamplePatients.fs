/// Seven sample patients for the tests, and the helpers that set patient data through the Dto. The weights
/// and heights have no date.
module SamplePatients

open System

open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Patient.AgeParts

module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
module CoreDepartment = Informedica.GenCore.Lib.Patients.Department
module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreHeight = Informedica.GenCore.Lib.Patients.Height
module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData

type CoreAccess = Informedica.GenCore.Lib.Patients.Access
type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>


/// The date the Dtos of the tests are read on. The weights and heights have no date, so the date checks
/// none of them.
let readAt = DateTime(2026, 11, 17)


/// Writes the patient data to its Dto and reads it back, so that PatientData.Dto.fromDto calculates the
/// post-menstrual age and the body surface area. A gestational age is removed without an age or from 1
/// year. Fails the test when the Dto does not read.
let throughDto pat =
    pat
    |> CorePatientData.Dto.toDto
    |> CorePatientData.Dto.fromDto readAt None
    |> Expect.wantOk "the patient data Dto reads"


/// Sets the age from age parts.
let withAge ags (pat: PatientData) =
    { pat with Age = ags |> daysOfParts |> CoreAgeValue.fromDays |> Some }
    |> throughDto


/// Sets the gestational age from age parts. The patient data keeps it only with an age under 1 year, so
/// set the age first.
let withGestAge ags (pat: PatientData) =
    { pat with GestationalAge = ags |> daysOfParts |> CoreAgeWeeksDays.fromDays |> Some }
    |> throughDto


/// Sets an undated calculation weight. The source is Measured or Estimated; None removes the weight.
let withWeightFrom source wght (pat: PatientData) =
    { pat with
        Weight =
            wght
            |> Option.map (CoreWeightAtDate.undated >> source)
            |> CoreWeight.create [] None []
    }
    |> throughDto


/// Sets a measured weight; None removes the weight.
let withWeight wght pat = pat |> withWeightFrom CoreSource.Measured wght


/// Sets an estimated weight; None removes the weight.
let withEstimatedWeight wght pat = pat |> withWeightFrom CoreSource.Estimated wght


/// Sets an undated calculation height. The source is Measured or Estimated; None removes the height.
let withHeightFrom source hght (pat: PatientData) =
    { pat with
        Height =
            hght
            |> Option.map (CoreHeightAtDate.undated >> source)
            |> CoreHeight.create [] None
    }
    |> throughDto


/// Sets a measured height; None removes the height.
let withHeight hght pat = pat |> withHeightFrom CoreSource.Measured hght


/// Sets an estimated height; None removes the height.
let withEstimatedHeight hght pat = pat |> withHeightFrom CoreSource.Estimated hght


/// Sets the gender.
let withGender gend (pat: PatientData) = { pat with Gender = gend } |> throughDto


/// Sets the department by its name. None sets an unknown department.
let withDepartment name (pat: PatientData) =
    { pat with
        Department =
            name
            |> Option.map CoreDepartment.otherDepartment
            |> Option.defaultValue CoreDepartment.unknown
    }
    |> throughDto


/// Sets a measured weight in kg.
let withWeightInKg kg pat = pat |> withWeight (kg |> CoreWeightValue.weightInKg |> Some)


/// Sets a measured weight in gram.
let withWeightInGram (gram: decimal) pat = pat |> withWeight (gram |> CoreWeightValue.weightInGram |> Some)


/// Sets a measured height in cm.
let withHeightInCm cm pat = pat |> withHeight (cm |> CoreHeightValue.heightInCm |> Some)


/// A premature of one week, born at 32 weeks, 1200 gram and 45 cm, on the NEO.
let premature =
    PatientData.unknown
    |> withAge [ Weeks 1 ]
    |> withGestAge [ Weeks 32 ]
    |> withWeightInGram 1200m
    |> withHeightInCm 45m
    |> withDepartment (Some "NEO")


/// A newborn of one week, 3.5 kg and 60 cm, on the ICK.
let newBorn =
    PatientData.unknown
    |> withAge [ Weeks 1 ]
    |> withWeightInKg 3.5m
    |> withHeightInCm 60m
    |> withDepartment (Some "ICK")


/// An infant of one year, 11.5 kg and 70 cm, on the ICK.
let infant =
    PatientData.unknown
    |> withAge [ Years 1 ]
    |> withWeightInKg 11.5m
    |> withHeightInCm 70m
    |> withDepartment (Some "ICK")


/// A toddler of 3 years, 15 kg and 90 cm, on the ICK.
let toddler =
    PatientData.unknown
    |> withAge [ Years 3 ]
    |> withWeightInKg 15m
    |> withHeightInCm 90m
    |> withDepartment (Some "ICK")


/// A child of 4 years, 17 kg and 100 cm, on the ICK. The child has a central venous line.
let child =
    PatientData.unknown
    |> withAge [ Years 4 ]
    |> withWeightInKg 17m
    |> withHeightInCm 100m
    |> withDepartment (Some "ICK")
    |> fun p -> { p with Access = [ CoreAccess.CVL ] }


/// A teenager of 12 years, 40 kg and 150 cm, on the ICK.
let teenager =
    PatientData.unknown
    |> withAge [ Years 12 ]
    |> withWeightInKg 40m
    |> withHeightInCm 150m
    |> withDepartment (Some "ICK")


/// An adult of 18 years, 70 kg and 180 cm, on the ICK.
let adult =
    PatientData.unknown
    |> withAge [ Years 18 ]
    |> withWeightInKg 70m
    |> withHeightInCm 180m
    |> withDepartment (Some "ICK")
