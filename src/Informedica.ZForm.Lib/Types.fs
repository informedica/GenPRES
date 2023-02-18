namespace Informedica.ZForm.Lib


[<AutoOpen>]
module Types =

    open MathNet.Numerics

    open Informedica.GenUnits.Lib

    type Route = Informedica.ZIndex.Lib.Types.Route.Route
    type MinMax = Informedica.GenCore.Lib.Ranges.MinIncrMax


    /// A product is a medical substance or
    /// substances with a specific shape
    type Product =
        {
            // The name of the product which is the generic
            // substance of the product or a concatenation of
            // generic substance names or a 'name'.
            Name : string
            /// The pharmacological shape of a product.
            DisplayName : string
            Synonyms : string list
            Shape : string
            /// The route of a product
            Unit : string
            Routes : Route list
            Pharmacologic : string list
            /// The display name of the generic
            DivisibleBy : Divisibility
            GenericProducts : GenericProduct List
        }

    and Divisibility = NoDiv | Div of bigint

    and GenericProduct =
        {
            Id : int
            Label : string
            /// The substances on which the concentration and dosing is based.
            Substances : Substance list
            TradeProducts : TradeProduct list
        }

    and Substance =
        {
            Name : string
            Concentration : ValueUnit
        }

    and TradeProduct =
        {
            Id : int
            Names : string
            Label : string
            Quantity : ValueUnit
        }


    type PatientCategory =
        {
            GestAge : MinMax
            Age : MinMax
            Weight : MinMax
            BSA : MinMax
            Gender : Gender
        }

        static member GestAge_ :
            (PatientCategory -> MinMax) * (MinMax -> PatientCategory -> PatientCategory) =
            (fun p -> p.GestAge), (fun a p -> { p with GestAge = a })

        static member Age_ :
            (PatientCategory -> MinMax) * (MinMax -> PatientCategory -> PatientCategory) =
            (fun p -> p.Age), (fun a p -> { p with Age = a })

        static member Weight_ :
            (PatientCategory -> MinMax) * (MinMax -> PatientCategory -> PatientCategory) =
            (fun p -> p.Weight), (fun w p -> { p with Weight = w })

        static member BSA_ :
            (PatientCategory -> MinMax) * (MinMax -> PatientCategory -> PatientCategory) =
            (fun p -> p.BSA), (fun b p -> { p with BSA = b })

        static member Gender_ :
            (PatientCategory -> Gender) * (Gender -> PatientCategory -> PatientCategory) =
            (fun p -> p.Gender), (fun g p -> { p with Gender = g })

    and Gender = Male | Female | Undetermined




    /// Dose limits
    type DoseRange =
        {
            // Normal limits
            Norm : MinMax
            // Normal limits adjusted by weight (Unit = weight unit)
            NormWeight : MinMax * Unit
            // Normal limits adjusted by BSA (Unit = BSA unit)
            NormBSA : MinMax * Unit
            // Absolute limits
            Abs : MinMax
            // Absolute limits adjusted by weight (Unit = weight unit)
            AbsWeight : MinMax * Unit
            // Absolute limits adjusted by BSA (Unit = bsa unit)
            AbsBSA : MinMax * Unit
        }
        static member Norm_ :
            (DoseRange -> MinMax) * (MinMax -> DoseRange -> DoseRange) =
            (fun dr -> dr.Norm),
            (fun mm dr -> { dr with Norm = mm })

        static member NormWeight_ :
            (DoseRange -> MinMax * Unit) * (MinMax * Unit -> DoseRange -> DoseRange) =
            (fun dr -> dr.NormWeight),
            (fun mm dr -> { dr with NormWeight = mm })

        static member NormBSA_ :
            (DoseRange -> MinMax * Unit) * (MinMax * Unit -> DoseRange -> DoseRange) =
            (fun dr -> dr.NormBSA),
            (fun mm dr -> { dr with NormBSA = mm })

        static member Abs_ :
            (DoseRange -> MinMax) * (MinMax -> DoseRange -> DoseRange) =
            (fun dr -> dr.Abs),
            (fun mm dr -> { dr with Abs = mm })

        static member AbsWeight_ :
            (DoseRange -> MinMax * Unit) * (MinMax * Unit -> DoseRange -> DoseRange) =
            (fun dr -> dr.AbsWeight),
            (fun mm dr -> { dr with AbsWeight = mm })

        static member AbsBSA_ :
            (DoseRange -> MinMax * Unit) * (MinMax * Unit -> DoseRange -> DoseRange) =
            (fun dr -> dr.AbsBSA),
            (fun mm dr -> { dr with AbsBSA = mm })


    /// Dosage
    type Dosage =
        {
            /// Indentifies the indication
            Name : string
            /// Dosage at the start
            StartDosage : DoseRange
            /// Dosage per administration
            SingleDosage : DoseRange
            /// Dosage rate
            RateDosage : DoseRange * RateUnit
            /// Total dosage per time period
            TotalDosage : DoseRange * Frequency
            /// List of original doserules
            Rules : Rule list
        }
        static member Name_ :
            (Dosage -> string) * (string -> Dosage -> Dosage) =
            (fun d -> d.Name),
            (fun s d -> { d with Name = s })

        static member StartDosage_ :
            (Dosage -> DoseRange) * (DoseRange -> Dosage -> Dosage) =
            (fun d -> d.StartDosage),
            (fun dr d -> { d with StartDosage = dr })

        static member SingleDosage_ :
            (Dosage -> DoseRange) * (DoseRange -> Dosage -> Dosage) =
            (fun d -> d.SingleDosage),
            (fun dr d -> { d with SingleDosage = dr })

        static member RateDosage_ :
            (Dosage -> DoseRange * RateUnit) * (DoseRange * RateUnit -> Dosage -> Dosage) =
            (fun d -> d.RateDosage),
            (fun dr d -> { d with RateDosage = dr })

        static member TotalDosage_ :
            (Dosage -> DoseRange * Frequency) * (DoseRange * Frequency -> Dosage -> Dosage) =
            (fun d -> d.TotalDosage),
            (fun dt d -> { d with TotalDosage = dt })

        static member Rules_ :
            (Dosage -> Rule list) * (Rule list -> Dosage -> Dosage) =
            (fun d -> d.Rules) ,
            (fun rs d -> { d with Rules = rs })

    and Frequency =
        {
            Frequencies : Frequencies
            TimeUnit : TimeUnit
            MinimalInterval : ValueUnit Option
        }
        static member Frequencies_ :
            (Frequency -> Frequencies) * (Frequencies -> Frequency -> Frequency) =
            (fun fr -> fr.Frequencies) ,
            (fun frs fr -> { fr with Frequencies = frs })

        static member TimeUnit_ :
            (Frequency -> Unit) * (Unit -> Frequency -> Frequency) =
            (fun fr -> fr.TimeUnit) ,
            (fun tu fr -> { fr with TimeUnit = tu })

        static member MinimalInterval_ :
            (Frequency -> ValueUnit Option) * (ValueUnit Option -> Frequency -> Frequency) =
            (fun fr -> fr.MinimalInterval) ,
            (fun mi fr -> { fr with MinimalInterval = mi })

    and Frequencies = BigRational list

    and TimeUnit = Unit

    and RateUnit = Unit

    and Rule = GStandRule of string | PedFormRule of string


    type PatientDosage =
        {
            // The patient group the doserules applies
            Patient : PatientCategory
            // List of shapes that have a dosage
            ShapeDosage : Dosage
            // List of substances that have a dosage
            SubstanceDosages : Dosage list
        }
        static member Patient_ :
            (PatientDosage -> PatientCategory) * (PatientCategory -> PatientDosage -> PatientDosage) =
            (fun pd -> pd.Patient) ,
            (fun pat pd -> { pd with Patient = pat })

        static member ShapeDosage_ :
            (PatientDosage -> Dosage) * (Dosage -> PatientDosage -> PatientDosage) =
            (fun pd -> pd.ShapeDosage) ,
            (fun sd pd -> { pd with ShapeDosage = sd })

        static member SubstanceDosages_ :
            (PatientDosage -> Dosage list) * (Dosage list -> PatientDosage -> PatientDosage) =
            (fun sd -> sd.SubstanceDosages) ,
            (fun d sd -> { sd with SubstanceDosages = d })


    type TradeProductLabel =
        { HPK : int; Label : string }
        static member HPK_ :
            (TradeProductLabel -> int) * (int -> TradeProductLabel -> TradeProductLabel) =
            (fun tp -> tp.HPK) ,
            (fun hpk tp -> { tp with HPK = hpk })


        static member Label_ :
            (TradeProductLabel -> string) * (string -> TradeProductLabel -> TradeProductLabel) =
            (fun tp -> tp.Label) ,
            (fun lbl tp -> { tp with Label = lbl })


    type GenericProductLabel =
        { GPK : int; Label : string }
        static member GPK_ :
            (GenericProductLabel -> int) * (int -> GenericProductLabel -> GenericProductLabel) =
            (fun tp -> tp.GPK) ,
            (fun hpk tp -> { tp with GPK = hpk })


        static member Label_ :
            (GenericProductLabel -> string) * (string -> GenericProductLabel -> GenericProductLabel) =
            (fun tp -> tp.Label) ,
            (fun lbl tp -> { tp with Label = lbl })


    type ShapeDosage =
        {
            // Name of the shape the doserule applies to
            Shape : string list
            // TradeProducts the doserule applies to
            TradeProducts : TradeProductLabel list
            // GenericProducts the doserule applies to
            GenericProducts : GenericProductLabel list
            // Patients to wich the doserule applies to
            PatientDosages : PatientDosage list
        }

        static member Shape_ :
            (ShapeDosage -> string list) * (string list -> ShapeDosage -> ShapeDosage) =
            (fun rd -> rd.Shape) ,
            (fun s rd -> { rd with Shape = s })

        static member TradeProducts_ :
            (ShapeDosage -> TradeProductLabel list) * (TradeProductLabel list -> ShapeDosage -> ShapeDosage) =
            (fun sd -> sd.TradeProducts) ,
            (fun tps sd -> { sd with TradeProducts = tps |> List.distinct })

        static member GenericProducts_ :
            (ShapeDosage -> GenericProductLabel list) * (GenericProductLabel list -> ShapeDosage -> ShapeDosage) =
            (fun sd -> sd.GenericProducts) ,
            (fun tps sd -> { sd with GenericProducts = tps |> List.distinct })

        static member PatientDosages_ :
            (ShapeDosage -> PatientDosage list) * (PatientDosage list -> ShapeDosage -> ShapeDosage) =
            (fun rd -> rd.PatientDosages) ,
            (fun pdl rd -> { rd with PatientDosages = pdl })


    type RouteDosage =
        {
            // Administration route
            Route : string
            // The dosage rules per shape
            ShapeDosages : ShapeDosage list
        }
        static member Route_ :
            (RouteDosage -> string) * (string -> RouteDosage -> RouteDosage) =
            (fun rd -> rd.Route) ,
            (fun s rd -> { rd with Route = s })

        static member ShapeDosages_ :
            (RouteDosage -> ShapeDosage list) * (ShapeDosage list -> RouteDosage -> RouteDosage) =
            (fun rd -> rd.ShapeDosages) ,
            (fun pdl rd -> { rd with ShapeDosages = pdl })


    type IndicationDosage =
        {
            // The indication(-s) the dose rule applies to
            Indications : string list
            // The dosage rules per administration route
            RouteDosages : RouteDosage list
        }
        static member Indications_ :
            (IndicationDosage -> string list) * (string list -> IndicationDosage -> IndicationDosage) =
            (fun inds -> inds.Indications) ,
            (fun sl inds -> { inds with Indications = sl })

        static member RouteDosages_ :
            (IndicationDosage -> RouteDosage list) * (RouteDosage list -> IndicationDosage -> IndicationDosage) =
            (fun inds -> inds.RouteDosages) ,
            (fun rdl inds -> { inds with RouteDosages = rdl })


    /// Doserule
    type DoseRule =
        {
            // Generic the doserule applies to
            Generic : string
            // List of synonyms for the generic
            Synonyms : string list
            // The ATC code
            ATC : string
            // ATCTherapyGroup the doserule applies to
            ATCTherapyGroup : string
            // ATCTherapySubGroup the doserule applies to
            ATCTherapySubGroup : string
            // The generic group the doserule applies to
            GenericGroup : string
            // The generic subgroup the doserule applies to
            GenericSubGroup : string
            // The doserules per indication(-s)
            IndicationsDosages : IndicationDosage list
        }
        static member Generic_ :
            (DoseRule -> string) * (string -> DoseRule -> DoseRule) =
            (fun dr -> dr.Generic),
            (fun s dr -> { dr with Generic = s })

        static member Synonyms_ :
            (DoseRule -> string list) * (string list -> DoseRule -> DoseRule) =
            (fun dr -> dr.Synonyms) ,
            (fun sns dr -> { dr with Synonyms = sns |> List.distinct })


        static member IndicationDosages_ :
            (DoseRule -> IndicationDosage list) * (IndicationDosage list -> DoseRule -> DoseRule) =
            (fun dr -> dr.IndicationsDosages) ,
            (fun inds dr -> { dr with IndicationsDosages = inds })


    type DoseMapping =
        | Norm
        | Abs
        | NormKg
        | AbsKg
        | NormM2
        | AbsM2


    type UnitMapping =
        { 
            ZIndexLong : string
            ZIndexShort : string
            MetaVision : string
            Unit : Unit
        }


    type FrequencyMapping =
        {
            ZIndex : string
            ZIndexFreq : BigRational
            ZIndexUnit : string
            MetaVision1 : string
            MetaVision2 : string
            Frequency : BigRational
            Unit : Unit
        }            



    type CreateConfig =
        {
            UseAll : bool
            IsRate : bool
            SubstanceUnit : Unit Option
            TimeUnit : Unit Option
        }


    type TextConfig =
        {
            MainText: string
            IndicationText : string
            RouteText : string
            ShapeText : string
            PatientText : string
            DosageText : string
        }


