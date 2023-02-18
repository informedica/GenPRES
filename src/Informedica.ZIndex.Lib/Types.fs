namespace Informedica.ZIndex.Lib


[<AutoOpen>]
module Types =


    module Names =

        type Name = Full | Short | Memo | Label


        type Length = TwentyFive | Fifty


        type Item =
            | Shape
            | Route
            | GenericUnit
            | ShapeUnit
            | PrescriptionContainer
            | ConsumerContainer


    module Route =


        type Route =
            | AURICULAR
            | CUTANEOUS
            | DENTAL
            | ENDOCERVICAL
            | ENDOTRACHEOPULMONAR
            | EPIDURAL
            | EPILESIONAL
            | EXTRACORPORAL
            | GASTRO_ENTERAL
            | HEMODIALYSIS
            | IMPLANT
            | INHALATION
            | INTRA_ARTERIAL
            | INTRA_ARTICULAR
            | INTRA_OCULAR
            | INTRA_UTERINE
            | INTRABURSAL
            | INTRACAMERAL
            | INTRACARDIAL
            | INTRACAVERNEUS
            | INTRACEREBRAL
            | INTRACERVICAL
            | INTRACORONARY
            | INTRADERMAL
            | INTRALAESIONAL
            | INTRALYMPHATIC
            | INTRAMUSCULAR
            | INTRAOSSAL
            | INTRAPERICARDIAL
            | INTRAPERITONEAL
            | INTRAPLEURAL
            | INTRATHECAL
            | INTRATUMORAL
            | INTRATYMPANAL
            | INTRAVENEUS
            | INTRAVESICAL
            | INTRAVITREAL
            | LOCAL
            | NASAL
            | NON_SPECIFIED
            | NOT_APPLICABLE
            | OCULARY
            | ORAL
            | OROMUCOSAL
            | PARABULBAR
            | PARENTERAL
            | PERI_ARTICULAR
            | PERIBULBAR
            | PERINEURAL
            | RECTAL
            | RETROBULBAR
            | SUBCONJUNCTIVAL
            | SUBCUTAN
            | SUBLINGUAL
            | SUBMUCOSAL
            | SUBRETINAL
            | TRANSDERMAL
            | URETHRAL
            | VAGINAL
            | NoRoute


        type Mapping = 
            {
                Route : Route
                Name : string
                ZIndex : string
                Product : string
                Rule : string
                Short : string
            }


    type Substance =
        {
            Id : int
            Pk : int
            Name : string
            Mole : decimal
            MoleReal : decimal
            Formula : string
            Unit : string
            Density : decimal
        }


    type ConsumerProduct =
        {
            Id : int
            Name : string
            Label : string
            Quantity : decimal
            Container : string
            BarCodes : string []
        }


    type TradeProduct =
        {
            Id: int
            Name : string
            Label : string
            Brand : string
            Company : string
            Denominator : int
            UnitWeight : decimal
            Route : string []
            ConsumerProducts : ConsumerProduct []
        }


    type PrescriptionProduct =
        {
            Id : int
            Name : string
            Label  : string
            Quantity : decimal
            Unit : string
            Container : string
            TradeProducts : TradeProduct []
        }


    type GenericProduct =
        {
            Id : int
            Name : string
            Label : string
            ATC : string
            ATCName : string
            Shape : string
            Route : string []
            Substances : ProductSubstance []
            PrescriptionProducts : PrescriptionProduct []
        }

    and ProductSubstance =
        {
            SubstanceId : int
            SortOrder : int
            SubstanceName : string
            SubstanceQuantity : decimal
            SubstanceUnit : string
            GenericId : int
            GenericName : string
            GenericQuantity : decimal
            GenericUnit : string
            ShapeUnit : string
        }


    type GenPresProduct =
        {
            Name : string
            Shape : string
            Routes : string []
            PharmacologicalGroups : string []
            GenericProducts : GenericProduct []
            DisplayName: string
            Unit : string
            Synonyms: string []
        }


    type RuleFrequency = { Frequency: decimal; Time: string }


    type RuleMinMax = { Min: decimal Option; Max: decimal Option }


    type DoseRule =
        {
            /// The id of the doserule
            Id : int
            /// The caregroup the doserule applies to
            /// this is either 'intensieve' or 'niet-intensieve' or 'all'
            CareGroup : string
            /// This is the usage of the dose rule, can be therapeutic or
            /// profylactic
            Usage : string
            /// The dose type, 'standaard' means that the dose rule applies without
            /// a specific indication, 'verbyzondering' means the dose rule needs
            /// an indication other than 'Algemeen'.
            DoseType : string
            /// The list of generic products for which the dose rule applies
            GenericProduct : RuleGenericProduct[]
            /// The list of prescription products for which the dose rule applies
            PrescriptionProduct : RuleProduct[]
            /// The list of trade products for which the dose rule applies
            TradeProduct : RuleProduct[]
            /// The route for which the dose rule applies
            Routes : string []
            /// The indication id for which the dose rule applies.
            /// The indications are coded by ICPC/ICD-10
            IndicationId : int
            /// The indication text for which the dose rule applies.
            /// The indications are coded by ICPC/ICD-10
            Indication : string
            /// If high risk, than the dose margins are smaller
            HighRisk : bool
            /// Gender is either 'man', 'vrouw' or an empty string.
            /// When gender is empty the dose rule can apply to either
            /// gender.
            Gender : string
            /// The optional minimum or maximum age limits for the dose rule
            Age : RuleMinMax
            /// The optional minimum or maximum weight limits for which the dose
            /// rule applies
            Weight : RuleMinMax
            /// The optional BSA min/max for which the dose rule applies
            BSA : RuleMinMax
            /// The frequency of the dose rule. The total dose can be calculated
            /// by multiplying the dose by the frequency.
            Freq : RuleFrequency
            /// The normal optional min/max of the unadjusted dose
            Norm : RuleMinMax
            /// The absolute optional min/max of the unadjusted dose
            Abs : RuleMinMax
            /// The normal optional min/max of the dose adjusted by weight
            NormKg : RuleMinMax
            /// The absolute optional min/max of the dose adjusted by weight
            AbsKg : RuleMinMax
            /// The absolute optional min/max of the dose adjusted by BSA
            NormM2 : RuleMinMax
            /// The absolute optional min/max of the dose adjusted by BSA
            AbsM2 : RuleMinMax
            /// The unit in which the dose is measured
            Unit : string
        }

        static member Weight_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.Weight) ,
            (fun mm dr -> { dr with Weight = mm })

        static member BSA_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.BSA) ,
            (fun mm dr -> { dr with BSA = mm })

        static member Norm_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.Norm) ,
            (fun mm dr -> { dr with Norm = mm })

        static member Abs_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.Abs) ,
            (fun mm dr -> { dr with Abs = mm })

        static member NormKg_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.NormKg) ,
            (fun mm dr -> { dr with NormKg = mm })

        static member AbsKg_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.AbsKg) ,
            (fun mm dr -> { dr with AbsKg = mm })

        static member NormM2_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.NormM2) ,
            (fun mm dr -> { dr with NormM2 = mm })

        static member AbsM2_ :
            (DoseRule -> RuleMinMax) * (RuleMinMax -> DoseRule -> DoseRule) =
            (fun dr -> dr.AbsM2) ,
            (fun mm dr -> { dr with AbsM2 = mm })

    and RuleProduct = { Id: int; Name: string }

    and RuleGenericProduct =
        {
            Id: int
            Name: string
            Route: string []
            Unit: string
            Substances : RuleSubstance []
        }

    and RuleSubstance = { Name: string; Quantity: decimal; Unit: string }


    type ATCGroup =
        {
            ATC1 : string
            AnatomicalGroup : string
            AnatomicalGroupEng : string
            ATC2 : string
            TherapeuticMainGroup : string
            TherapeuticMainGroupEng : string
            ATC3 : string
            TherapeuticSubGroup : string
            TherapeuticSubGroupEng : string
            ATC4 : string
            PharmacologicalGroup : string
            PharmacologicalGroupEng : string
            ATC5 : string
            Substance : string
            SubstanceEng : string
            Generic : string
            Shape : string
            Routes : string
        }


    type AgeInMo = decimal Option

    type WeightInKg = decimal Option

    type BSAInM2 = decimal Option


    type PatientFilter =
        {
            Age: AgeInMo
            Weight: WeightInKg
            BSA: BSAInM2
        }


    type ProductFilter =
        | GPKRoute of (int * string)
        | GenericShapeRoute of GenericShapeRoute

    and GenericShapeRoute =
        {
            Generic: string
            Shape: string
            Route: string
        }


    type Filter =
        {
            Patient: PatientFilter
            Product: ProductFilter
        }


    type RuleResult =
        {
            Product: GenPresProduct
            DoseRules: string []
            Doses: FreqDose []
        }

    and FreqDose =
        {
            /// The frequency of the dose rule
            Freq: RuleFrequency
            /// The optional min/max values of a 'normal dose range'
            NormDose: RuleMinMax
            /// The optional min/max values of the 'absolute dose range'
            AbsDose: RuleMinMax
            /// The optional min/max values of a 'normal dose range' per kg
            NormKg: RuleMinMax
            /// The optional min/max values of the 'absolute dose range' per kg
            AbsKg: RuleMinMax
            /// The optional min/max values of a 'normal dose range' per m2
            NormM2: RuleMinMax
            /// The optional min/max values of the 'absolute dose range' per m2
            AbsM2: RuleMinMax
            /// The unit in which the doses are measured
            Unit: string
        }


    type Assortment =
        {
            GPK: int
            Generic: string
            TallMan : string
            Divisible : int
        }
