namespace Informedica.GenForm.Lib


module Filter =


    let filter =
        {
            Indication = None
            Generic = None
            Shape = None
            Route = None
            Department = None
            Diagnoses = [||]
            Gender = AnyGender
            AgeInDays = None
            WeightInGram = None
            HeightInCm = None
            GestAgeInDays = None
            PMAge = None
            DoseType = AnyDoseType
            Location = AnyAccess
        }


    let setPatient (pat : Patient) (filter : Filter) =
        { filter with
            Department = pat.Department |> Some
            Diagnoses = pat.Diagnoses
            Gender = pat.Gender
            AgeInDays = pat.AgeInDays
            WeightInGram = pat.WeightInGram
            HeightInCm = pat.HeightInCm
            GestAgeInDays = pat.GestAgeInDays
            PMAge = pat.PMAgeInDays
            Location = pat.VenousAccess
        }


    let getPatient (filter : Filter) =
        { Patient.patient with
            Department = filter.Department |> Option.defaultValue ""
            Diagnoses = filter.Diagnoses
            Gender = filter.Gender
            AgeInDays = filter.AgeInDays
            WeightInGram = filter.WeightInGram
            HeightInCm = filter.HeightInCm
            GestAgeInDays = filter.GestAgeInDays
            PMAgeInDays = filter.PMAge
            VenousAccess = filter.Location
        }

