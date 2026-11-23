namespace Informedica.GenForm.Lib


module Filter =


    /// A dose filter with no criteria, all components and unknown patient data.
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


    /// A solution filter with no criteria and unknown patient data.
    let solutionFilter =
        {
            Generic = None
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
