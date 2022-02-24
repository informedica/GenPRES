namespace Shared


/// This module defines shared types between
/// the client and the server
module Types =

    type DataType =
        | StringData
        | FloatData
        | FloatOptionData


    type Configuration = Setting list

    and Setting =
        {
            Department: string
            MinAge: int
            MaxAge: int
            MinWeight: float
            MaxWeight: float
        }


    type Ranges =
        {
            Years: int list
            Months: int list
            Weights: float list
            Heights: int list
        }


    module Patient =

        type Age =
            {
                Years: int
                Months: int
                Weeks: int
                Days: int
            }


    type Age = Patient.Age

    /// Patient model for calculations
    type Patient =
        {
            Age: Age option
            Weight: Weight
            Height: Height
        }
    /// Weight in kg
    and Weight =
        {
            Estimated: float option
            Measured: float option
        }
    /// Length in cm
    and Height =
        {
            Estimated: float option
            Measured: float option
        }


    type MedicationDefs =
        (string * string * float * float * float * float * string * string) list


    type Medication2 =
        | Bolus of Bolus2
        | Continuous of Continuous2

    and Bolus2 =
        {
            Indication: string
            Generic: string
            NormDose: float
            MinDose: float
            MaxDose: float
            Concentration: float
            Unit: string
            Remark: string
        }

    and Continuous2 =
        {
            Indication: string
            Generic: string
            Unit: string
            DoseUnit: string
            Quantity2To6: float
            Volume2To6: float
            Quantity6To11: float
            Volume6To11: float
            Quantity11To40: float
            Volume11To40: float
            QuantityFrom40: float
            VolumeFrom40: float
            MinDose: float
            MaxDose: float
            AbsMax: float
            MinConc: float
            MaxConc: float
            Solution: string
        }


    type Medication =
        | Bolus of Bolus
        | Continuous of Continuous

    and Bolus =
        {
            Indication: string
            Generic: string
            NormDose: float
            MinDose: float
            MaxDose: float
            Concentration: float
            Unit: string
            Remark: string
        }

    and Continuous =
        {
            Indication: string
            Generic: string
            Unit: string
            Quantity : float
            Total : float
            DoseUnit: string
            MinWeight: float
            MaxWeight: float
            MinDose: float
            MaxDose: float
            AbsMax: float
            MinConc: float
            MaxConc: float
            Solution: string
        }