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
            PatientData = PatientData.unknown
        }


    let solutionFilter gen =
        {
            Generic = gen
            Form = None
            Route = None
            Indication = None
            DoseType = None
            Diluent = None
            PatientData = PatientData.unknown
            Dose = None
        }


    /// Sets the patient data of a dose filter.
    let setPatientData patientData (filter: DoseFilter) = { filter with PatientData = patientData }
