namespace Shared


/// This module defines shared types between
/// the client and the server
[<AutoOpen>]
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


    type Medication =
        | Bolus of BolusMedication
        | Continuous of ContinuousMedication

    and BolusMedication =
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

    and ContinuousMedication =
        {
            Indication: string
            Generic: string
            Unit: string
            Quantity: float
            Total: float
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

    // CalculatedDose
    // MinDose
    // MaxDose
    // DoseUnit
    // CalculatedDoseAdjust
    // NormDoseAdjust
    // MinDoseAdjust
    // MaxDoseAdjust
    // DoseAdjustUnit
    // DoseText
    type Intervention =
        {
            // == Intervention ==
            // Indication for the intervention
            Indication: string
            // Name of the intervantion
            Name: string
            // == Product ==
            // Substance quantity
            Quantity: float option
            // Quantity unit
            QuantityUnit: string
            // Name of the solution
            Solution: string
            // Total quantity
            Total: float option
            // Total unit
            TotalUnit: string
            // == Dose ==
            // Intervention dose
            InterventionDose: float option
            // Intervention dose unit
            InterventionDoseUnit: string
            // Text representation
            InterventionDoseText: string
            // Dose of the substance
            SubstanceDose: float option
            // Min dose of the substance
            SubstanceMinDose: float option
            // Max dose of the substance
            SubstanceMaxDose: float option
            // Dose unit
            SubstanceDoseUnit: string
            // Adjusted dose
            SubstanceDoseAdjust: float option
            // Norm adjusted dose
            SubstanceNormDoseAdjust: float option
            // Min adjusted dose
            SubstanceMinDoseAdjust: float option
            // Max adjusted dose
            SubstanceMaxDoseAdjust: float option
            // Adjusted dose unit
            SubstanceDoseAdjustUnit: string
            // Dose remarks
            SubstanceDoseText: string
            Text: string
        }


    type Product =
        {
            Indication: string
            Medication: string
            Concentration: float
            Unit: string
        }

    type Scenario =
        {
            Shape : string
            DoseType : string
            Prescription : string
            Preparation : string
            Administration : string
        }

    type ScenarioResult =
        {
            Indications: string []
            Medications: string []
            Routes: string []
            Scenarios: Scenario []
            Indication: string option
            Medication: string option
            Shape : string option
            Route: string option
            Age : float option
            Weight: float option
            Height: float option
        }