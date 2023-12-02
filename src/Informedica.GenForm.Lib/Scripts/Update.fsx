

#load "load.fsx"
#load "../Types.fs"
#load "../Utils.fs"
#load "../Mapping.fs"
#load "../VenousAccess.fs"
#load "../Patient.fs"
#load "../DoseType.fs"
#load "../Product.fs"
#load "../Filter.fs"
#load "../DoseRule.fs"
#load "../SolutionRule.fs"
#load "../PrescriptionRule.fs"


open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenCore.Lib.Ranges


{ PatientCategory.patientCategory with
    Department = None
    Age =
        { PatientCategory.patientCategory.Age with
            Min =
                90N
                |> ValueUnit.singleWithUnit Units.Time.day
                |> Limit.Inclusive
                |> Some
        }
    Weight =
        { PatientCategory.patientCategory.Weight with
            Max =
                37.5m
                |> BigRational.fromDecimal
                |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                |> ValueUnit.convertTo Units.Weight.gram
                |> Limit.Inclusive
                |> Some
        }

}
|> PatientCategory.filter
    { Filter.filter with
         Patient =
            { Patient.patient with
                Department = (Some "ICK")
                Age =
                    1460N
                    |> ValueUnit.singleWithUnit Units.Time.day
                    |> Some
                Weight =
                    37500N
                    |> ValueUnit.singleWithUnit Units.Weight.gram
                    |> Some
                Height =
                    100N
                    |> ValueUnit.singleWithUnit Units.Height.centiMeter
                    |> Some
                VenousAccess = [CVL]
            }
    }
(*
 { Department = Some "ICK"
    Diagnoses = [||]
    Gender = AnyGender
    Age = Some (ValueUnit ([|1460N|], Time (Day 1N)))
    Weight = Some (ValueUnit ([|17000N|], Weight (WeightGram 1N)))
    Height = Some (ValueUnit ([|100N|], Height (HeightCentiMeter 1N)))
    GestAge = None
    PMAge = None
    VenousAccess = [CVL] }
 *)

PrescriptionRule.get Patient.patient
//|> Seq.length
|> Seq.item 0