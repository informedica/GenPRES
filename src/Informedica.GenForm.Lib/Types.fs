namespace Informedica.GenForm.Lib


[<AutoOpen>]
module Types =

    open MathNet.Numerics


    type VenousAccess =
        | PVL
        | CVL
        | AnyAccess


    type Gender = Male | Female | AnyGender


    type DoseType =
        | Start
        | Once
        | PRN
        | Maintenance
        | Continuous
        | StepDown of int
        | StepUp of int
        | Contraindicated
        | AnyDoseType


    type MinMax = { Minimum : BigRational option; Maximum : BigRational option }


    type Frequency = { Count : BigRational; TimeUnit : string }


    type ShapeRoute =
        {
            Shape : string
            Route : string
            Unit  : string
            DoseUnit : string
            Timed : bool
            Reconstitute : bool
            IsSolution : bool
        }


    type Substance =
        {
            Name : string
            Unit : string
            Quantity : BigRational option
            MultipleQuantity : BigRational option
            MultipleUnit : string
        }


    type Product =
        {
            GPK : string
            ATC : string
            MainGroup : string
            SubGroup : string
            Generic : string
            TallMan : string
            Synonyms : string array
            Product : string
            Label : string
            Shape : string
            ShapeQuantities : BigRational []
            ShapeUnit : string
            RequiresReconstitution : bool
            Reconstitution : Reconstitution []
            Divisible : BigRational option
            Substances : Substance array
        }
    and Reconstitution =
        {
            Route : string
            DoseType: DoseType
            Department : string
            Location : VenousAccess
            DiluentVolume : BigRational
            ExpansionVolume : BigRational option
            Diluents : string []
        }


    type DoseLimit =
        {
            Substance : string
            DoseUnit : string
            RateUnit : string
            NormQuantity : BigRational []
            Quantity : MinMax
            NormQuantityAdjust : BigRational option
            QuantityAdjust : MinMax
            NormPerTime : BigRational []
            PerTime : MinMax
            NormPerTimeAdjust : BigRational option
            PerTimeAdjust : MinMax
            NormRate : BigRational []
            Rate : MinMax
            NormRateAdjust : BigRational option
            RateAdjust : MinMax
        }


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


    type Patient =
        {
            Department : string
            Diagnoses : string []
            Gender : Gender
            Age : BigRational option
            Weight : BigRational option
            Height : BigRational option
            GestAge : BigRational option
            PMAge : BigRational option
            Location : VenousAccess
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


    type DoseRule =
        {
            Indication : string
            Generic : string
            Shape : string
            Route : string
            Patient : PatientCategory
            AdjustUnit : string
            DoseType : DoseType
            Frequencies : BigRational array
            FreqUnit : string
            Time : MinMax
            TimeUnit : string
            Interval : MinMax
            IntervalUnit : string
            Duration : MinMax
            DurationUnit : string
            DoseLimits : DoseLimit array
            Products : Product array
        }


    type SolutionLimit =
        {
            Substance : string
            Unit : string
            Quantity : MinMax
            Quantities : BigRational []
            Concentration : MinMax
        }


    type SolutionRule =
        {
            Generic : string
            Shape : string
            Route : string
            DoseType : DoseType
            Department : string
            Location : VenousAccess
            Age : MinMax
            Weight : MinMax
            Dose : MinMax
            Solutions : string []
            Volumes : BigRational []
            Volume : MinMax
            DosePerc : MinMax
            Products : Product []
            SolutionLimits : SolutionLimit []
        }


    type Filter =
        {
            Indication : string option
            Generic : string option
            Shape : string option
            Route : string option
            Department : string option
            Diagnoses : string []
            Gender : Gender
            Age : BigRational option
            Weight : BigRational option
            BSA : BigRational option
            GestAge : BigRational option
            PMAge : BigRational option
            DoseType : DoseType
            Dose : BigRational option
            Location : VenousAccess
        }


    type PrescriptionRule =
        {
            Patient : Patient
            DoseRule : DoseRule
            SolutionRules : SolutionRule []
        }