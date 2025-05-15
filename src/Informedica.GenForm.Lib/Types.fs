namespace Informedica.GenForm.Lib


[<AutoOpen>]
module Types =

    open MathNet.Numerics
    open Informedica.GenUnits.Lib

    type MinMax = Informedica.GenCore.Lib.Ranges.MinMax

    /// Associate a Route and a Shape
    /// setting default values for the other fields
    type ShapeRoute =
        {
            // The Route
            Route : string
            // The pharmacological Shape
            Shape : string
            // The Unit of the Shape
            Unit : Unit
            // The Dose Unit to use for Dose Limits
            DoseUnit : Unit
            // The minimum Dose quantity
            MinDoseQty : ValueUnit option
            // The maximum Dose quantity
            MaxDoseQty : ValueUnit option
            // The minimum Adjusted Dose quantity
            MinDoseQtyPerKg : ValueUnit option
            // The maximum Adjusted Dose quantity
            MaxDoseQtyPerKg : ValueUnit option
            // The divisibility of a shape
            Divisibility: BigRational option
            // Whether a Dose runs over a Time
            Timed : bool
            // Whether the Shape needs to be reconstituted
            Reconstitute : bool
            // Whether the Shape is a solution
            IsSolution : bool
        }


    /// The types for VenousAccess.
    type Location =
        // Peripheral Venous Access
        | PVL
        // Central Venous Access
        | CVL
        // Any Venous Access
        | AnyAccess


    /// Possible Genders.
    type Gender = Male | Female | AnyGender


    type RenalFunction =
        | EGFR of int option * int option
        | IntermittentHemodialysis
        | ContinuousHemodialysis
        | PeritonealDialysis


    /// Possible Dose Types.
    type DoseType =
        // A Once only Dose
        | Once of string
        // A Maintenance Dose
        | Discontinuous of string
        // A Continuous Dose
        | Continuous of string
        // A discontinuous per time
        | Timed of string
        // A once per time
        | OnceTimed of string
        | NoDoseType


    /// A Substance type.
    type Substance =
        {
            // The name of the Substance
            Name : string
            // The Quantity of the Substance
            Concentration : ValueUnit option
            // The indivisible Quantity of the Substance
            MolarConcentration : ValueUnit option
        }

    /// A Product type.
    type Product =
        {
            // The GPK id of the Product
            GPK : string
            // The ATC code of the Product
            ATC : string
            // The ATC main group of the Product
            MainGroup : string
            // The ATC subgroup of the Product
            SubGroup : string
            // The Generic name of the Product
            Generic : string
            // Use Generic Substance Name
            UseGenericName : bool
            // Use Shape
            UseShape : bool
            // Use Brand
            UseBrand : bool
            // A tall-man representation of the Generic name of the Product
            TallMan : string
            // Synonyms for the Product
            Synonyms : string array
            // The full product name of the Product
            Product : string
            // The label of the Product
            Label : string
            // The pharmacological Shape of the Product
            Shape : string
            // The possible Routes of administration of the Product
            Routes : string []
            // The possible quantities of the Shape of the Product
            ShapeQuantities : ValueUnit
            // The uid of the Shape of the Product
            ShapeUnit : Unit
            // Whether the Shape of the Product requires reconstitution
            RequiresReconstitution : bool
            // The possible reconstitution rules for the Product
            Reconstitution : Reconstitution []
            // The division factor of the Product
            Divisible : BigRational option
            // The Substances in the Product
            Substances : Substance array
        }
    and Reconstitution =
        {
            // The route for the reconstitution
            Route : string
            // The department for the reconstitution
            Department : string
            // The volume of the reconstitution
            DiluentVolume : ValueUnit
            // An optional expansion volume of the reconstitution
            ExpansionVolume : ValueUnit option
            // The Diluents for the reconstitution
            Diluents : string []
        }


    type ComponentItem =
        {
            ComponentName: string
            ComponentQuantity: ValueUnit
            ItemName: string
            ItemConcentration: ValueUnit
        }


    type LimitTarget =
        | NoLimitTarget
        | SubstanceLimitTarget of string
        | ComponentLimitTarget of string
        | ShapeLimitTarget of string


    /// A DoseLimit for a Shape or Substance.
    type DoseLimit =
        {
            DoseLimitTarget : LimitTarget
            // The unit to adjust dosing with
            AdjustUnit : Unit option
            // The unit to dose with
            DoseUnit: Unit
            // A MinMax Dose Quantity for the DoseLimit
            Quantity : MinMax
            // An optional Dose Quantity Adjust for the DoseLimit.
            // Note: if this is specified a min and max QuantityAdjust
            // will be assumed to be 10% minus and plus the normal value
            NormQuantityAdjust : ValueUnit option
            // A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust : MinMax
            // An optional Dose Per Time for the DoseLimit
            PerTime : MinMax
            // An optional Per Time Adjust for the DoseLimit
            // Note: if this is specified a min and max NormPerTimeAdjust
            // will be assumed to be 10% minus and plus the normal value
            NormPerTimeAdjust : ValueUnit option
            // A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust : MinMax
            // A MinMax Rate for the DoseLimit
            Rate : MinMax
            // A MinMax Rate Adjust for the DoseLimit
            RateAdjust : MinMax
        }


    /// A PatientCategory to which a Rule applies.
    type PatientCategory =
        {
            Department : string option
            Gender : Gender
            Age : MinMax
            Weight : MinMax
            BSA : MinMax
            GestAge : MinMax
            PMAge : MinMax
            Location : Location
        }


    /// A specific Patient to filter DoseRules.
    type Patient =
        {
            // The Department of the Patient
            Department : string option
            // A list of Diagnoses of the Patient
            Diagnoses : string []
            // The Gender of the Patient
            Gender : Gender
            // The Age in days of the Patient
            Age : ValueUnit option
            // The Weight in grams of the Patient
            Weight : ValueUnit option
            // The Height in cm of the Patient
            Height : ValueUnit option
            // The Gestational Age in days of the Patient
            GestAge : ValueUnit option
            // The Post Menstrual Age in days of the Patient
            PMAge : ValueUnit option
            // The Venous Access of the Patient
            Locations : Location list
            RenalFunction : RenalFunction option
        }
        static member Gender_ =
            (fun (p : Patient) -> p.Gender), (fun g (p : Patient) -> { p with Gender = g})

        static member Age_ =
            (fun (p : Patient) -> p.Age), (fun a (p : Patient) -> { p with Age = a})

        static member Weight_ =
            (fun (p : Patient) -> p.Weight), (fun w (p : Patient) -> { p with Weight = w})

        static member Height_ =
            (fun (p : Patient) -> p.Height), (fun b (p : Patient) -> { p with Height = b})

        static member GestAge_ =
            (fun (p : Patient) -> p.GestAge), (fun a (p : Patient) -> { p with GestAge = a})

        static member PMAge_ =
            (fun (p : Patient) -> p.PMAge), (fun a (p : Patient) -> { p with PMAge = a})

        static member Department_ =
            (fun (p : Patient) -> p.Department), (fun d (p : Patient) -> { p with Department = d})


    type DoseRuleDetails = {
        AdjustUnit: string
        Brand: string
        Component: string
        Department: string
        DoseText: string
        DoseType: string
        DoseUnit: string
        DurUnit: string
        FreqUnit: string
        Frequencies: BigRational array
        GPKs: string array
        Gender: Gender
        Generic: string
        Indication: string
        IntervalUnit: string
        MaxAge: BigRational option
        MaxBSA: BigRational option
        MaxDur: BigRational option
        MaxGestAge: BigRational option
        MaxInterval: BigRational option
        MaxPMAge: BigRational option
        MaxPerTime: BigRational option
        MaxPerTimeAdj: BigRational option
        MaxQty: BigRational option
        MaxQtyAdj: BigRational option
        MaxRate: BigRational option
        MaxRateAdj: BigRational option
        MaxTime: BigRational option
        MaxWeight: BigRational option
        MinAge: BigRational option
        MinBSA: BigRational option
        MinDur: BigRational option
        MinGestAge: BigRational option
        MinInterval: BigRational option
        MinPMAge: BigRational option
        MinPerTime: BigRational option
        MinPerTimeAdj: BigRational option
        MinQty: BigRational option
        MinQtyAdj: BigRational option
        MinRate: BigRational option
        MinRateAdj: BigRational option
        MinTime: BigRational option
        MinWeight: BigRational option
        NormPerTimeAdj: BigRational option
        NormQtyAdj: BigRational option
        RateUnit: string
        Route: string
        ScheduleText: string
        Shape: string
        Source: string
        Substance: string
        TimeUnit: string
        Products : Product []
    }


    type ComponentLimit =
        {
            Name : string
            Limit : DoseLimit option
            Products : Product []
            SubstanceLimits : DoseLimit []
        }


    /// The DoseRule type. Identifies exactly one DoseRule
    /// for a specific PatientCategory, Indication, Generic, Shape, Route and DoseType.
    type DoseRule =
        {
            Source : string
            // The Indication of the DoseRule
            Indication : string
            // The Generic of the DoseRule
            Generic : string
            // The pharmacological Shape of the DoseRule
            Shape : string
            // The brand of the doserule
            Brand : string option
            // Specific GPKs
            GPKs : string array
            // The Route of administration of the DoseRule
            Route : string
            // The PatientCategory of the DoseRule
            PatientCategory : PatientCategory
            // the original dose schedule text
            ScheduleText : string
            // The DoseType of the DoseRule
            DoseType : DoseType
            // The unit to adjust dosing with
            AdjustUnit : Unit option
            // The possible Frequencies of the DoseRule
            Frequencies : ValueUnit option
            // The MinMax Administration Time of the DoseRule
            AdministrationTime : MinMax
            // The MinMax Interval Time of the DoseRule
            IntervalTime : MinMax
            // The MinMax Duration of the DoseRule
            Duration : MinMax
            // the limits based upon the shape and route
            ShapeLimit : DoseLimit option
            // the limits for the component and substances
            // in the component
            ComponentLimits : ComponentLimit []
            // an optional renal rule
            RenalRule : string option
        }


    /// A SolutionLimit for a Substance.
    type SolutionLimit =
        {
            // The Substance for the SolutionLimit
            SolutionLimitTarget : LimitTarget
            // The MinMax Quantity of the Substance for the SolutionLimit
            Quantity : MinMax
            // A list of possible Quantities of the Substance for the SolutionLimit
            Quantities : ValueUnit option
            // The Minmax Concentration of the Substance for the SolutionLimit
            Concentration : MinMax
        }


    /// A SolutionRule for a specific Generic, Shape, Route, DoseType, Department
    /// Venous Access Location, Age range, Weight range, Dose range and Generic Products.
    type SolutionRule =
        {
            // The Generic of the SolutionRule
            Generic : string
            // The Shape of the SolutionRule
            Shape : string option
            // The Route of the SolutionRule
            Route : string
            // The DoseType of the SolutionRule
            DoseType : DoseType
            // The PatientCategory of the DoseRule
            PatientCategory : PatientCategory
            // The MinMax Dose range of the SolutionRule
            Dose : MinMax
            // The Products the SolutionRule applies to
            Products : Product []
            // The possible Solutions to use
            Diluents : Product []
            // The possible Volumes to use
            Volumes : ValueUnit option
            // A MinMax Volume range to use
            Volume : MinMax
            // A MinMax adjusted Volume range to use
            VolumeAdjust : MinMax
            DripRate : MinMax
            // The percentage to be used as a DoseQuantity
            DosePerc : MinMax
            // The SolutionLimits for the SolutionRule
            SolutionLimits : SolutionLimit []
        }



    /// A DoseLimit for a Shape or Substance.
    type RenalLimit =
        {
            DoseLimitTarget : LimitTarget
            DoseReduction : DoseReduction
            Quantity : MinMax
            // An optional Dose Quantity Adjust for the DoseLimit.
            // Note: if this is specified a min and max QuantityAdjust
            // will be assumed to be 10% minus and plus the normal value
            NormQuantityAdjust : ValueUnit option
            // A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust : MinMax
            // An optional Dose Per Time for the DoseLimit
            PerTime : MinMax
            // An optional Per Time Adjust for the DoseLimit
            // Note: if this is specified a min and max NormPerTimeAdjust
            // will be assumed to be 10% minus and plus the normal value
            NormPerTimeAdjust : ValueUnit option
            // A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust : MinMax
            // A MinMax Rate for the DoseLimit
            Rate : MinMax
            // A MinMax Rate Adjust for the DoseLimit
            RateAdjust : MinMax
        }
    and DoseReduction = | Absolute | Relative | NoReduction


    type RenalRule =
        {
            // The Generic of the RenalRule
            Generic : string
            // The Route of administration of the RenalRule
            Route : string
            Indication : string
            // The source of the RenalRule
            Source : string
            Age : MinMax
            RenalFunction : RenalFunction
            // The DoseType of the RenalRule
            DoseType : DoseType
            // The possible Frequencies of the RenalRule
            Frequencies : ValueUnit option
            // The MinMax Interval Time of the RenalRule
            IntervalTime : MinMax
            // The list of associated RenalLimits of the RenalRule.
            RenalLimits : RenalLimit array
        }


    type ProductFilter =
        {
            Generic: string
            Shape: string option
            Route: string
            ShapeUnit: Unit option
        }


    /// A Filter to get the DoseRules for a specific Patient.
    type DoseFilter =
        {
            // the Indication to filter on
            Indication : string option
            // the Generic to filter on
            Generic : string option
            // the Shape to filter on
            Shape : string option
            // the Route to filter on
            Route : string option
            // the DoseType to filter on
            DoseType : DoseType option
            // the diluent to use
            Diluent : string option
            // the components to use
            Components: string list
            // the patient to filter on
            Patient : Patient
        }

    type SolutionFilter =
        {
            // The Generic of the SolutionRule
            Generic : string
            // The Shape of the SolutionRule
            Shape : string option
            // The Route of the SolutionRule
            Route : string option
            // The DoseType of the SolutionRule
            DoseType : DoseType option
            // the patient
            Patient : Patient
            // the diluent to dilute the component
            Diluent : string option
            // The MinMax Dose range of the SolutionRule
            Dose : ValueUnit option
        }

    /// A PrescriptionRule for a specific Patient
    /// with a DoseRule and a list of SolutionRules.
    type PrescriptionRule =
        {
            Patient : Patient
            DoseRule : DoseRule
            SolutionRules : SolutionRule []
            RenalRules : RenalRule []
        }


    type NormDose =
        | NormQuantityAdjust of LimitTarget * ValueUnit
        | NormPerTimeAdjust of LimitTarget * ValueUnit
        | NormRateAdjust of LimitTarget * ValueUnit