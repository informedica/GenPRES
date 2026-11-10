namespace Informedica.GenForm.Lib


module Filter =


    /// An empty Filter.
    let doseFilter =
        {
            Indication = None
            Generic = None
            Form = None
            Route = None
            DoseType = None
            Diluent = None
            Components = ComponentSelection.AllComponents
            Patient = Patient.patient
        }


    let dilutionFilter gen =
        {
            Generic = gen
            Form = None
            Route = None
            Indication = None
            DoseType = None
            Diluent = None
            Patient = Patient.patient
            Dose = None
        }


    /// <summary>
    /// Apply a Patient to a Filter.
    /// </summary>
    /// <param name="pat">The Patient</param>
    /// <param name="filter">The Filter</param>
    /// <returns>The Filter with the Patient applied</returns>
    let setPatient (pat: Patient) (filter: DoseFilter) =
        let pat = pat |> Patient.correctAdjustUnit

        { filter with Patient = pat }
