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
            Patient = PatientData.unknown
        }


    let solutionFilter gen =
        {
            Generic = gen
            Form = None
            Route = None
            Indication = None
            DoseType = None
            Diluent = None
            Patient = PatientData.unknown
            Dose = None
        }


    /// Sets the patient data of a dose filter.
    let setPatient pat (filter: DoseFilter) = { filter with Patient = pat }
