namespace Informedica.GenForm.Lib



module Filter =


    /// An empty Filter.
    let doseFilter =
        {
            Indication = None
            Generic = None
            Shape = None
            Route = None
            DoseType = None
            Diluent = None
            Patient = Patient.patient
        }


    let solutionFilter gen =
        {
            Generic = gen
            Shape = None
            Route = None
            DoseType = None
            Diluent = None
            Department = None
            Locations = []
            Age = None
            Weight = None
            Dose = None
        }


    /// <summary>
    /// Apply a Patient to a Filter.
    /// </summary>
    /// <param name="pat">The Patient</param>
    /// <param name="filter">The Filter</param>
    /// <returns>The Filter with the Patient applied</returns>
    let setPatient (pat : Patient) (filter : DoseFilter) =
        let pat =
            pat
            |> Patient.correctAdjustUnit
            |> Patient.calcPMAge

        { filter with
            Patient = pat
        }


    let calcPMAge (filter : DoseFilter) =
        { filter with
            Patient =
                filter.Patient
                |> Patient.calcPMAge
        }