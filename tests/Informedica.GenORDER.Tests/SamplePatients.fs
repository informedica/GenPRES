/// Seven sample patients for the tests. Every weight and height has the same date.
module SamplePatients

open System

open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Patient.AgeParts

module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue

type CoreAccess = Informedica.GenCore.Lib.Patients.Access


/// The date of every weight and height in the tests.
let measuredOn = DateTime(2026, 11, 17)


/// Sets a measured weight in kg.
let withWeightInKg kg pat =
    pat |> Patient.setWeight measuredOn (kg |> CoreWeightValue.weightInKg |> Some)


/// Sets a measured weight in gram.
let withWeightInGram (gram: decimal) pat =
    pat
    |> Patient.setWeight measuredOn (gram |> CoreWeightValue.weightInGram |> Some)


/// Sets a measured height in cm.
let withHeightInCm cm pat =
    pat |> Patient.setHeight measuredOn (cm |> CoreHeightValue.heightInCm |> Some)


/// A premature of one week, born at 32 weeks, 1200 gram and 45 cm, on the NEO.
let premature =
    PatientData.unknown
    |> Patient.setAge [ Weeks 1 ]
    |> Patient.setGestAge [ Weeks 32 ]
    |> withWeightInGram 1200m
    |> withHeightInCm 45m
    |> Patient.setDepartment (Some "NEO")


/// A newborn of one week, 3.5 kg and 60 cm, on the ICK.
let newBorn =
    PatientData.unknown
    |> Patient.setAge [ Weeks 1 ]
    |> withWeightInKg 3.5m
    |> withHeightInCm 60m
    |> Patient.setDepartment (Some "ICK")


/// An infant of one year, 11.5 kg and 70 cm, on the ICK.
let infant =
    PatientData.unknown
    |> Patient.setAge [ Years 1 ]
    |> withWeightInKg 11.5m
    |> withHeightInCm 70m
    |> Patient.setDepartment (Some "ICK")


/// A toddler of 3 years, 15 kg and 90 cm, on the ICK.
let toddler =
    PatientData.unknown
    |> Patient.setAge [ Years 3 ]
    |> withWeightInKg 15m
    |> withHeightInCm 90m
    |> Patient.setDepartment (Some "ICK")


/// A child of 4 years, 17 kg and 100 cm, on the ICK. The child has a central venous line.
let child =
    PatientData.unknown
    |> Patient.setAge [ Years 4 ]
    |> withWeightInKg 17m
    |> withHeightInCm 100m
    |> Patient.setDepartment (Some "ICK")
    |> fun p -> { p with Access = [ CoreAccess.CVL ] }


/// A teenager of 12 years, 40 kg and 150 cm, on the ICK.
let teenager =
    PatientData.unknown
    |> Patient.setAge [ Years 12 ]
    |> withWeightInKg 40m
    |> withHeightInCm 150m
    |> Patient.setDepartment (Some "ICK")


/// An adult of 18 years, 70 kg and 180 cm, on the ICK.
let adult =
    PatientData.unknown
    |> Patient.setAge [ Years 18 ]
    |> withWeightInKg 70m
    |> withHeightInCm 180m
    |> Patient.setDepartment (Some "ICK")
