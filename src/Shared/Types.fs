namespace Shared


/// This module defines shared types between
/// the client and the server
module Types =

    open System


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

        type GestationalAge = { Weeks: int; Days: int }


    type Age = Patient.Age
    type GestAge = Patient.GestationalAge

    /// Patient model for calculations
    type Patient =
        {
            Age: Age option
            GestationalAge: GestAge option
            Weight: Weight
            Height: Height
            Gender: Gender
            Access: Access list
            RenalFunction: RenalFunction option
            Department: string option
        }

    /// Weight in kg
    and Weight =
        {
            Estimated: int option
            Measured: int option
        }

    /// Length in cm
    and Height =
        {
            Estimated: int option
            Measured: int option
        }

    and Gender =
        | Male
        | Female
        | UnknownGender

    and Access =
        | CVL
        | PVL
        | EnteralTube

    and RenalFunction =
        | EGFR of int option * int option
        | IntermittendHemoDialysis
        | ContinuousHemoDialysis
        | PeritionealDialysis


    type ValueUnit =
        {
            Value: (string * decimal)[]
            Unit: string
            Group: string
            Short: bool
            Language: string
            Json: string
        }


    type Variable =
        {
            Name: string
            IsNonZeroNegative: bool
            Min: ValueUnit option
            MinIncl: bool
            Incr: ValueUnit option
            Max: ValueUnit option
            MaxIncl: bool
            Vals: ValueUnit option
        }


    type OrderVariable =
        {
            Name: string
            Constraints: Variable
            Variable: Variable
        }


    type Prescription =
        {
            IsOnce: bool
            IsOnceTimed: bool
            IsContinuous: bool
            IsDiscontinuous: bool
            IsTimed: bool
            Frequency: OrderVariable
            Time: OrderVariable
        }


    type Dose =
        {
            Quantity: OrderVariable
            PerTime: OrderVariable
            Rate: OrderVariable
            Total: OrderVariable
            QuantityAdjust: OrderVariable
            PerTimeAdjust: OrderVariable
            RateAdjust: OrderVariable
            TotalAdjust: OrderVariable
        }


    type Item =
        {
            Name: string
            ComponentQuantity: OrderVariable
            OrderableQuantity: OrderVariable
            ComponentConcentration: OrderVariable
            OrderableConcentration: OrderVariable
            Dose: Dose
            IsAdditional: bool
        }


    type Component =
        {
            Id: string
            Name: string
            Shape: string
            ComponentQuantity: OrderVariable
            OrderableQuantity: OrderVariable
            OrderableCount: OrderVariable
            OrderQuantity: OrderVariable
            OrderCount: OrderVariable
            OrderableConcentration: OrderVariable
            Dose: Dose
            Items: Item[]
        }


    type Orderable =
        {
            Name: string
            OrderableQuantity: OrderVariable
            OrderQuantity: OrderVariable
            OrderCount: OrderVariable
            DoseCount: OrderVariable
            Dose: Dose
            Components: Component[]
        }


    type Order =
        {
            Id: string
            Adjust: OrderVariable
            Orderable: Orderable
            Prescription: Prescription
            Route: string
            Duration: OrderVariable
            Start: DateTime
            Stop: DateTime option
        }


    type OrderLoader =
        {
            Component: string option
            Item: string option
            Order: Order
        }


    type LoadedOrder =
        {
            UseAdjust: bool
            Component: string option
            Item: string option
            Order: Order
        }


    type Medication =
        | Bolus of BolusMedication
        | Continuous of ContinuousMedication

    and BolusMedication =
        {
            Hospital: string
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
            Hospital: string
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


    type Intervention =
        {
            Hospital: string
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


    type TextItem =
        | Normal of string
        | Bold of string
        | Italic of string


    type Product =
        {
            Indication: string
            Medication: string
            Concentration: float
            Unit: string
        }


    /// Possible Dose Types.
    type DoseType =
        /// A Once only Dose
        | Once of string
        /// A Maintenance Dose
        | Discontinuous of string
        /// A Continuous Dose
        | Continuous of string
        /// A discontinuous per time
        | Timed of string
        /// A once per time
        | OnceTimed of string
        | NoDoseType


    type OrderState =
        | Constrained of Order
        | Calculated of Order
        | Solved of Order


    type Filter =
        {
            Indications: string[]
            Medications: string[]
            Routes: string[]
            DoseTypes: DoseType[]
            Diluents: string[]
            Components: string []
            Indication: string option
            Medication: string option
            Shape: string option
            Route: string option
            DoseType: DoseType option
            Diluent: string option
            SelectedComponents : string []
        }


    type Intake =
        {
            Volume: TextItem[]
            Energy: TextItem[]
            Protein: TextItem[]
            Carbohydrate: TextItem[]
            Fat: TextItem[]
            Sodium: TextItem[]
            Potassium: TextItem[]
            Chloride: TextItem[]
            Calcium: TextItem[]
            Phosphate: TextItem[]
            Magnesium: TextItem[]
            Iron: TextItem[]
            VitaminD: TextItem[]
            Ethanol: TextItem[]
            Propyleenglycol: TextItem[]
            BoricAcid: TextItem[]
            BenzylAlcohol: TextItem[]
        }


    type OrderScenario =
        {
            Indication: string
            Diluents: string[]
            Components: string[]
            Items: string[]
            Shape: string
            Diluent: string option
            Component: string option
            Item: string option
            DoseType: DoseType
            Prescription: TextItem[][][]
            Preparation: TextItem[][][]
            Administration: TextItem[][][]
            Order: OrderState
            UseAdjust: bool
            UseRenalRule: bool
            RenalRule: string option
        }


    type PrescriptionContext =
        {
            DemoVersion: bool
            Filter: Filter
            Patient: Patient
            Scenarios: OrderScenario[]
            Intake: Intake
        }


    type TreatmentPlan =
        {
            Patient: Patient
            Selected : OrderScenario option
            Filtered : OrderScenario []
            Scenarios: OrderScenario []
            Intake: Intake
        }


    type Formulary =
        {
            Generics: string[]
            Indications: string[]
            Routes: string[]
            PatientCategories: string[]
            Products: string[]
            Generic: string option
            Indication: string option
            Route: string option
            PatientCategory: string option
            Patient: Patient option
            Markdown: string
        }


    type Parenteralia =
        {
            Generics: string[]
            Shapes: string[]
            Routes: string[]
            PatientCategories: string[]
            Generic: string option
            Shape: string option
            Route: string option
            PatientCategory: string option
            Markdown: string
        }