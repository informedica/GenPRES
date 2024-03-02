namespace Informedica.GenForm.Lib



module Filter =


    /// An empty Filter.
    let filter =
        {
            Indication = None
            Generic = None
            Shape = None
            Route = None
            DoseType = None
            Patient = Patient.patient
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
            Patient = pat
        }


    let calcPMAge (filter : Filter) =
        { filter with
            Patient =
                filter.Patient
                |> Patient.calcPMAge
        }


