namespace Informedica.GenForm.Lib


module Filter =


    /// An empty Filter.
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
            PMAgeInDays = None
            DoseType = AnyDoseType
            Location = AnyAccess
        }


    /// <summary>
    /// Apply a Patient to a Filter.
    /// </summary>
    /// <param name="pat">The Patient</param>
    /// <param name="filter">The Filter</param>
    /// <returns>The Filter with the Patient applied</returns>
    let setPatient (pat : Patient) (filter : Filter) =
        let pat =
            pat
            |> Patient.calcPMAge

        { filter with
            Department = pat.Department |> Some
            Diagnoses = pat.Diagnoses
            Gender = pat.Gender
            AgeInDays = pat.AgeInDays
            WeightInGram = pat.WeightInGram
            HeightInCm = pat.HeightInCm
            GestAgeInDays = pat.GestAgeInDays
            PMAgeInDays = pat.PMAgeInDays
            Location = pat.VenousAccess
        }


    /// <summary>
    /// Extract a Patient from a Filter.
    /// </summary>
    /// <param name="filter">The Filter</param>
    /// <returns>The Patient</returns>
    let getPatient (filter : Filter) =
        { Patient.patient with
            Department = filter.Department |> Option.defaultValue ""
            Diagnoses = filter.Diagnoses
            Gender = filter.Gender
            AgeInDays = filter.AgeInDays
            WeightInGram = filter.WeightInGram
            HeightInCm = filter.HeightInCm
            GestAgeInDays = filter.GestAgeInDays
            PMAgeInDays = filter.PMAgeInDays
            VenousAccess = filter.Location
        }


    let calcPMAge (filter : Filter) =
        { filter with
            PMAgeInDays =
                filter
                |> getPatient
                |> Patient.calcPMAge
                |> fun pat -> pat.PMAgeInDays
        }