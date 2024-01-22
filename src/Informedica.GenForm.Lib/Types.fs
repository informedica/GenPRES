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
            /// The Route
            Route : string
            /// The pharmacological Shape
            Shape : string
            /// The Unit of the Shape
            Unit : Unit
            /// The Dose Unit to use for Dose Limits
            DoseUnit : Unit
            /// The minimum Dose quantity
            MinDoseQty : ValueUnit option
            /// The maximum Dose quantity
            MaxDoseQty : ValueUnit option
            /// The divisibility of a shape
            Divisibility: BigRational option
            /// Whether a Dose runs over a Time
            Timed : bool
            /// Whether the Shape needs to be reconstituted
            Reconstitute : bool
            /// Whether the Shape is a solution
            IsSolution : bool
        }


    /// The types for VenousAccess.
    type VenousAccess =
        /// Peripheral Venous Access
        | PVL
        /// Central Venous Access
        | CVL
        /// Any Venous Access
        | AnyAccess


    /// Possible Genders.
    type Gender = Male | Female | AnyGender


    /// Possible Dose Types.
    type DoseType =
        /// A Start Dose
        | Start
        /// A Once only Dose
        | Once
        /// A PRN Dose
        | PRN
        /// A Maintenance Dose
        | Maintenance
        /// A Continuous Dose
        | Continuous
        /// A Step Down Dose in a number of steps.
        | StepDown of int
        /// A Step Up Dose in a number of steps.
        | StepUp of int

        | Contraindicated
        /// Any Dosetype
        | AnyDoseType


    /// A Substance type.
    type Substance =
        {
            /// The name of the Substance
            Name : string
            /// The Quantity of the Substance
            Concentration : ValueUnit option
            /// The indivisible Quantity of the Substance
            MultipleQuantity : ValueUnit option
        }

    /// A Product type.
    type Product =
        {
            /// The GPK id of the Product
            GPK : string
            /// The ATC code of the Product
            ATC : string
            /// The ATC main group of the Product
            MainGroup : string
            /// The ATC sub group of the Product
            SubGroup : string
            /// The Generic name of the Product
            Generic : string
            /// A tall-man representation of the Generic name of the Product
            TallMan : string
            /// Synonyms for the Product
            Synonyms : string array
            /// The full product name of the Product
            Product : string
            /// The label of the Product
            Label : string
            /// The pharmacological Shape of the Product
            Shape : string
            /// The possible Routes of administration of the Product
            Routes : string []
            /// The possible quantities of the Shape of the Product
            ShapeQuantities : ValueUnit
            /// The uid of the Shape of the Product
            ShapeUnit : Unit
            /// Whether the Shape of the Product requires reconstitution
            RequiresReconstitution : bool
            /// The possible reconstitution rules for the Product
            Reconstitution : Reconstitution []
            /// The division factor of the Product
            Divisible : BigRational option
            /// The Substances in the Product
            Substances : Substance array
        }
    and Reconstitution =
        {
            /// The route for the reconstitution
            Route : string
            /// The DoseType for the reconstitution
            DoseType: DoseType
            /// The department for the reconstitution
            Department : string
            /// The location for the reconstitution
            Location : VenousAccess
            /// The volume of the reconstitution
            DiluentVolume : ValueUnit
            /// An optional expansion volume of the reconstitution
            ExpansionVolume : ValueUnit option
            /// The Diluents for the reconstitution
            Diluents : string []
        }


    /// A DoseLimit for a Shape or Substance.
    type DoseLimit =
        {
            DoseLimitTarget : DoseLimitTarget
            /// The unit to dose with
            DoseUnit: Unit
            /// A MinMax Dose Quantity for the DoseLimit
            Quantity : MinMax
            /// An optional Dose Quantity Adjust for the DoseLimit.
            /// Note: if this is specified a min and max QuantityAdjust
            /// will be assumed to be 10% minus and plus the normal value
            NormQuantityAdjust : ValueUnit option
            /// A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust : MinMax
            /// An optional Dose Per Time for the DoseLimit
            PerTime : MinMax
            /// An optional Per Time Adjust for the DoseLimit
            /// Note: if this is specified a min and max NormPerTimeAdjust
            /// will be assumed to be 10% minus and plus the normal value
            NormPerTimeAdjust : ValueUnit option
            /// A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust : MinMax
            /// A MinMax Rate for the DoseLimit
            Rate : MinMax
            /// A MinMax Rate Adjust for the DoseLimit
            RateAdjust : MinMax
        }
    and DoseLimitTarget =
        | NoDoseLimitTarget
        | SubstanceDoseLimitTarget of string
        | ShapeDoseLimitTarget of string


    /// A PatientCategory to which a DoseRule applies.
    type PatientCategory =
        {
            Department : string option
            Diagnoses : string []
            Gender : Gender
            Age : MinMax
            Weight : MinMax
            BSA : MinMax
            GestAge : MinMax
            PMAge : MinMax
            Location : VenousAccess
        }


    /// A specific Patient to filter DoseRules.
    type Patient =
        {
            /// The Department of the Patient
            Department : string option
            /// A list of Diagnoses of the Patient
            Diagnoses : string []
            /// The Gender of the Patient
            Gender : Gender
            /// The Age in days of the Patient
            Age : ValueUnit option
            /// The Weight in grams of the Patient
            Weight : ValueUnit option
            /// The Height in cm of the Patient
            Height : ValueUnit option
            /// The Gestational Age in days of the Patient
            GestAge : ValueUnit option
            /// The Post Menstrual Age in days of the Patient
            PMAge : ValueUnit option
            /// The Venous Access of the Patient
            VenousAccess : VenousAccess list
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


    /// The DoseRule type. Identifies exactly one DoseRule
    /// for a specific PatientCategory, Indication, Generic, Shape, Route and DoseType.
    type DoseRule =
        {
            /// The Indication of the DoseRule
            Indication : string
            /// The Generic of the DoseRule
            Generic : string
            /// The pharmacological Shape of the DoseRule
            Shape : string
            /// The brand of the doserule
            Brand : string
            /// The Route of administration of the DoseRule
            Route : string
            /// The PatientCategory of the DoseRule
            PatientCategory : PatientCategory
            DoseText : string
            /// The DoseType of the DoseRule
            DoseType : DoseType
            /// The unit to adjust dosing with
            AdjustUnit : Unit option
            /// The possible Frequencies of the DoseRule
            Frequencies : ValueUnit option
            /// The MinMax Administration Time of the DoseRule
            AdministrationTime : MinMax
            /// The MinMax Interval Time of the DoseRule
            IntervalTime : MinMax
            /// The MinMax Duration of the DoseRule
            Duration : MinMax
            /// The list of associated DoseLimits of the DoseRule.
            /// In principle for the Shape and each Substance .
            DoseLimits : DoseLimit array
            /// The list of associated Products of the DoseRule.
            Products : Product array
        }


    /// A SolutionLimit for a Substance.
    type SolutionLimit =
        {
            /// The Substance for the SolutionLimit
            Substance : string
            /// The MinMax Quantity of the Substance for the SolutionLimit
            Quantity : MinMax
            /// A list of possible Quantities of the Substance for the SolutionLimit
            Quantities : ValueUnit option
            /// The Minmax Concentration of the Substance for the SolutionLimit
            Concentration : MinMax
        }


    /// A SolutionRule for a specific Generic, Shape, Route, DoseType, Department
    /// Venous Access Location, Age range, Weight range, Dose range and Generic Products.
    type SolutionRule =
        {
            /// The Generic of the SolutionRule
            Generic : string
            /// The Shape of the SolutionRule
            Shape : string
            /// The Route of the SolutionRule
            Route : string
            /// The DoseType of the SolutionRule
            DoseType : DoseType
            /// The Department of the SolutionRule
            Department : string
            /// The Venous Access Location of the SolutionRule
            Location : VenousAccess
            /// The MinMax Age range of the SolutionRule
            Age : MinMax
            /// The MinMax Weight range of the SolutionRule
            Weight : MinMax
            /// The MinMax Dose range of the SolutionRule
            Dose : MinMax
            /// The Products the SolutionRule applies to
            Products : Product []
            /// The possible Solutions to use
            Solutions : string []
            /// The possible Volumes to use
            Volumes : ValueUnit option
            /// A MinMax Volume range to use
            Volume : MinMax
            /// The percentage to be use as a DoseQuantity
            DosePerc : MinMax
            /// The SolutionLimits for the SolutionRule
            SolutionLimits : SolutionLimit []
        }


    /// A Filter to get the DoseRules for a specific Patient.
    type Filter =
        {
            /// The Indication to filter on
            Indication : string option
            /// The Generic to filter on
            Generic : string option
            /// The Shape to filter on
            Shape : string option
            /// The Route to filter on
            Route : string option
            /// The DoseType to filter on
            DoseType : DoseType
            /// The patient to filter on
            Patient : Patient
        }


    /// A PrescriptionRule for a specific Patient
    /// with a DoseRule and a list of SolutionRules.
    type PrescriptionRule =
        {
            Patient : Patient
            DoseRule : DoseRule
            SolutionRules : SolutionRule []
        }

