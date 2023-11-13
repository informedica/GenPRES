namespace Informedica.GenForm.Lib


[<AutoOpen>]
module Types =

    open MathNet.Numerics


    /// Associate a Route and a Shape
    /// setting default values for the other fields
    type RouteShape =
        {
            /// The Route
            Route : string
            /// The pharmacological Shape
            Shape : string
            /// The Unit of the Shape
            Unit : string
            /// The Dose Unit to use for Dose Limits
            DoseUnit : string
            /// The minimum Dose quantity
            MinDoseQty : float option
            /// The maximum Dose quantity
            MaxDoseQty : float option
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


    /// A MinMax type with an optional Minimum and Maximum.
    type MinMax = { Minimum : BigRational option; Maximum : BigRational option }


    /// A frequency type with a Count and a TimeUnit.
    type Frequency = { Count : BigRational; TimeUnit : string }


    /// A Shape Route with associated attributes.
    type ShapeRoute =
        {
            /// The pharmacological Shape
            Shape : string
            /// The Route of administration
            Route : string
            /// The Unit of the Shape
            Unit  : string
            /// The Dose Unit to use for Dose Limits
            DoseUnit : string
            /// Whether a Dose runs over a Time
            Timed : bool
            /// Whether the Shape needs to be reconstituted
            Reconstitute : bool
            /// Whether the Shape is a solution
            IsSolution : bool
        }


    /// A Substance type.
    type Substance =
        {
            /// The name of the Substance
            Name : string
            /// The Unit of the Substance
            Unit : string
            /// The Quantity of the Substance
            Quantity : BigRational option
            /// The indivisible Quantity of the Substance
            MultipleQuantity : BigRational option
            /// The Unit of the indivisible Quantity of the Substance
            MultipleUnit : string
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
            ShapeQuantities : BigRational []
            /// The uid of the Shape of the Product
            ShapeUnit : string
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
            DiluentVolume : BigRational
            /// An optional expansion volume of the reconstitution
            ExpansionVolume : BigRational option
            /// The Diluents for the reconstitution
            Diluents : string []
        }


    /// A DoseLimit for a Shape or Substance.
    type DoseLimit =
        {
            /// The Target for the Doselimit
            DoseLimitTarget : DoseLimitTarget
            /// The DoseUnit to use for the DoseLimit
            DoseUnit : string
            /// The RateUnit to use for the DoseLimit
            RateUnit : string
            /// An optional Dose Quantity for the DoseLimit
            NormQuantity : BigRational option
            /// A MinMax Dose Quantity for the DoseLimit
            Quantity : MinMax
            /// An optional Dose Quantity Adjust for the DoseLimit
            NormQuantityAdjust : BigRational option
            /// A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust : MinMax
            /// An optional Dose Per Time for the DoseLimit
            NormPerTime : BigRational option
            /// A MinMax Per Time for the DoseLimit
            PerTime : MinMax
            /// An optional Per Time Adjust for the DoseLimit
            NormPerTimeAdjust : BigRational option
            /// A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust : MinMax
            /// An optional Rate for the DoseLimit
            NormRate : BigRational option
            /// A MinMax Rate for the DoseLimit
            Rate : MinMax
            /// An optional Rate Adjust for the DoseLimit
            NormRateAdjust : BigRational option
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
            Department : string
            /// A list of Diagnoses of the Patient
            Diagnoses : string []
            /// The Gender of the Patient
            Gender : Gender
            /// The Age in days of the Patient
            AgeInDays : BigRational option
            /// The Weight in grams of the Patient
            WeightInGram : BigRational option
            /// The Height in cm of the Patient
            HeightInCm : BigRational option
            /// The Gestational Age in days of the Patient
            GestAgeInDays : BigRational option
            /// The Post Menstrual Age in days of the Patient
            PMAgeInDays : BigRational option
            /// The Venous Access of the Patient
            /// TODO: should be a list
            VenousAccess : VenousAccess
        }
        static member Gender_ =
            (fun (p : Patient) -> p.Gender), (fun g (p : Patient) -> { p with Gender = g})

        static member Age_ =
            (fun (p : Patient) -> p.AgeInDays), (fun a (p : Patient) -> { p with AgeInDays = a})

        static member Weight_ =
            (fun (p : Patient) -> p.WeightInGram), (fun w (p : Patient) -> { p with WeightInGram = w})

        static member Height_ =
            (fun (p : Patient) -> p.HeightInCm), (fun b (p : Patient) -> { p with HeightInCm = b})

        static member GestAge_ =
            (fun (p : Patient) -> p.GestAgeInDays), (fun a (p : Patient) -> { p with GestAgeInDays = a})

        static member PMAge_ =
            (fun (p : Patient) -> p.PMAgeInDays), (fun a (p : Patient) -> { p with PMAgeInDays = a})

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
            /// The Route of administration of the DoseRule
            Route : string
            /// The PatientCategory of the DoseRule
            Patient : PatientCategory
            /// The Adjustment Unit of the DoseRule
            AdjustUnit : string
            /// The DoseType of the DoseRule
            DoseType : DoseType
            /// The possible Frequencies of the DoseRule
            Frequencies : BigRational array
            /// The frequency time unit of the DoseRule
            FreqTimeUnit : string
            /// The MinMax Administration Time of the DoseRule
            AdministrationTime : MinMax
            /// The Administration Time Unit of the DoseRule
            AdministrationTimeUnit : string
            /// The MinMax Interval Time of the DoseRule
            IntervalTime : MinMax
            /// The Interval Time Unit of the DoseRule
            IntervalTimeUnit : string
            /// The MinMax Duration of the DoseRule
            Duration : MinMax
            /// The Duration Unit of the DoseRule
            DurationUnit : string
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
            /// The unit of the Substance for the SolutionLimit
            Unit : string
            /// The MinMax Quantity of the Substance for the SolutionLimit
            Quantity : MinMax
            /// A list of possible Quantities of the Substance for the SolutionLimit
            Quantities : BigRational []
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
            Volumes : BigRational []
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
            /// The Department to filter on
            Department : string option
            /// The list of Diagnoses to filter on
            Diagnoses : string []
            /// The Gender to filter on
            Gender : Gender
            /// The Age in days to filter on
            AgeInDays : BigRational option
            /// The Weight in grams to filter on
            WeightInGram : BigRational option
            /// The Height in cm to filter on
            HeightInCm : BigRational option
            /// The Gestational Age in days to filter on
            GestAgeInDays : BigRational option
            /// The Post Menstrual Age in days to filter on
            PMAge : BigRational option
            /// The DoseType to filter on
            DoseType : DoseType
            /// The Venous Access Location to filter on
            Location : VenousAccess
        }


    /// A PrescriptionRule for a specific Patient
    /// with a DoseRule and a list of SolutionRules.
    type PrescriptionRule =
        {
            Patient : Patient
            DoseRule : DoseRule
            SolutionRules : SolutionRule []
        }