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
            Age = None
            Weight = None
            Height = None
            GestAge = None
            PMAge = None
            DoseType = AnyDoseType
            Dose = None
            Location = AnyAccess
        }


    let setPatient (pat : Patient) (filter : Filter) =
        { filter with
            Department = pat.Department |> Some
            Diagnoses = pat.Diagnoses
            Gender = pat.Gender
            Age = pat.Age
            Weight = pat.Weight
            Height = pat.Height
            GestAge = pat.GestAge
            PMAge = pat.PMAge
            Location = pat.VenousAccess
        }


    let getPatient (filter : Filter) =
        { Patient.patient with
            Department = filter.Department |> Option.defaultValue ""
            Diagnoses = filter.Diagnoses
            Gender = filter.Gender
            Age = filter.Age
            Weight = filter.Weight
            Height = filter.Height
            GestAge = filter.GestAge
            PMAge = filter.PMAge
            VenousAccess = filter.Location
        }

