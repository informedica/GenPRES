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



    module Dto =

        open System


        module ValueUnit =

            type Dto() =
                member val Value : string [] = [||] with get, set
                member val Unit = "" with get, set
                member val Group = "" with get, set
                member val Short = true with get, set
                member val Language = "" with get, set

            let dto () = Dto()


        module OrderVariable =


            type VarDto () =
                member val Min : ValueUnit.Dto option = None with get, set
                member val MinIncl = false with get, set
                member val Incr : ValueUnit.Dto option = None with get, set
                member val Max : ValueUnit.Dto option = None with get, set
                member val MaxIncl = false with get, set
                member val Vals : ValueUnit.Dto option = None with get, set


            type Dto () =
                member val Name = "" with get, set
                member val Constraints = VarDto () with get, set
                member val Variable = VarDto () with get, set


            let dto () = Dto ()


        module Prescription =


            type Dto () =
                member val IsContinuous = false with get, set
                member val IsDiscontinuous = false with get, set
                member val IsTimed = false with get, set
                member val Frequency = OrderVariable.dto () with get, set
                member val Time = OrderVariable.dto () with get, set


            let dto () = Dto ()


        module Dose =


            type Dto () =
                member val Quantity = OrderVariable.dto () with get, set
                member val PerTime = OrderVariable.dto () with get, set
                member val Rate = OrderVariable.dto () with get, set
                member val Total = OrderVariable.dto () with get, set
                member val QuantityAdjust = OrderVariable.dto () with get, set
                member val PerTimeAdjust = OrderVariable.dto () with get, set
                member val RateAdjust = OrderVariable.dto () with get, set
                member val TotalAdjust = OrderVariable.dto () with get, set


            let dto () = Dto ()


        module Item =

            type Dto () =
                member val Name = "" with get, set
                member val ComponentQuantity = OrderVariable.dto () with get, set
                member val OrderableQuantity = OrderVariable.dto () with get, set
                member val ComponentConcentration = OrderVariable.dto () with get, set
                member val OrderableConcentration = OrderVariable.dto () with get, set
                member val Dose = Dose.dto () with get, set



        module Component =


            type Dto () =
                member val Id = "" with get, set
                member val Name = "" with get, set
                member val Shape = "" with get, set
                member val ComponentQuantity = OrderVariable.dto () with get, set
                member val OrderableQuantity = OrderVariable.dto () with get, set
                member val OrderableCount = OrderVariable.dto () with get, set
                member val OrderQuantity = OrderVariable.dto () with get, set
                member val OrderCount = OrderVariable.dto () with get, set
                member val OrderableConcentration = OrderVariable.dto () with get, set
                member val Dose = Dose.dto () with get, set
                member val Items : Item.Dto [] = [||] with get, set



        module Orderable =


            type Dto () =
                member val Name = "" with get, set
                member val OrderableQuantity = OrderVariable.dto () with get, set
                member val OrderQuantity = OrderVariable.dto () with get, set
                member val OrderCount = OrderVariable.dto () with get, set
                member val DoseCount = OrderVariable.dto () with get, set
                member val Dose = Dose.dto () with get, set
                member val Components : Component.Dto [] = [||] with get, set


            let dto () = Dto ()


        type Dto() =
            member val Id = "" with get, set
            member val Adjust = OrderVariable.dto () with get, set
            member val Orderable = Orderable.dto () with get, set
            member val Prescription = Prescription.dto () with get, set
            member val Route = "" with get, set
            member val Duration = OrderVariable.dto () with get, set
            member val Start = DateTime.Now with get, set
            member val Stop : DateTime option = None with get, set




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


    type Scenario =
        {
            Shape : string
            DoseType : string
            Prescription : TextItem[]
            Preparation : TextItem[]
            Administration : TextItem[]
// not working            Dto: Dto.Dto
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


    type Formulary =
        {
            Generics : string []
            Indications : string []
            Routes : string []
            Patients : string []
            Products : string []
            Generic: string option
            Indication: string option
            Route: string option
            Patient : string option
            Age : float option
            Weight: float option
            Height: float option
            Markdown : string
        }
