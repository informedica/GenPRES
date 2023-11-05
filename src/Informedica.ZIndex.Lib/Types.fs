namespace Informedica.ZIndex.Lib


[<AutoOpen>]
module Types =


    module Names =

        /// Different types of names
        type Name = Full | Short | Memo | Label


        /// The length of the name
        type Length = TwentyFive | Fifty


        /// Naming items
        type Item =
            | Shape
            | Route
            | GenericUnit
            | ShapeUnit
            | PrescriptionContainer
            | ConsumerContainer


    module Route =

        /// Possible Z-Index routes
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


        /// Mapping between Z-Index route
        type Mapping =
            {
                Route : Route
                Name : string
                ZIndex : string
                Product : string
                Rule : string
                Short : string
            }


    /// <summary>
    /// A Z-Index Substance
    /// </summary>
    /// <remarks>
    /// The substance contains a Mole and MoleReal this
    /// corresponds to the substance and the full
    /// generic form of the Substance.
    /// see: https://www.z-index.nl/documentatie/bestandsbeschrijvingen/veld?veldnaam=GNMOLS
    /// </remarks>
    type Substance =
        {
            /// The Id of the Substance
            Id : int
            /// The Id for the full Generic Substance Name
            Pk : int
            /// The name of the substance
            Name : string
            /// The molar mass of the substance
            /// https://www.z-index.nl/documentatie/bestandsbeschrijvingen/veld?veldnaam=GNMOLS
            Mole : float
            /// The molar mass of the real substance
            /// https://www.z-index.nl/documentatie/bestandsbeschrijvingen/veld?veldnaam=GNMOLE
            MoleReal : float
            /// The chemical formula of the substance
            Formula : string
            /// The unit of the substance
            Unit : string
            /// The density of the substance
            Density : float
        }


    /// <summary>
    /// A Z-Index Consumer Product
    /// </summary>
    /// <remarks>
    /// A consumer product is a trade product with the
    /// total amount of products. I.e. a paracetmol
    /// product in a box of 10 tablets.
    /// </remarks>
    type ConsumerProduct =
        {
            Id : int
            Name : string
            Label : string
            Quantity : float
            Container : string
            BarCodes : string []
        }


    /// <summary>
    /// A Z-Index Trade Product
    /// </summary>
    /// <remarks>
    /// A trade product is a prescription product
    /// with the trade name and associated traits.
    /// </remarks>
    type TradeProduct =
        {
            Id: int
            Name : string
            Label : string
            Brand : string
            Company : string
            Denominator : int
            UnitWeight : float
            Route : string []
            ConsumerProducts : ConsumerProduct []
        }


    /// <summary>
    /// A Z-Index Prescription Product
    /// </summary>/>
    /// <remarks>
    /// A prescription product is a generic product with
    /// the quantity and container of the product.
    /// </remarks>
    type PrescriptionProduct =
        {
            /// The id of the prescription product, i.e. PRK.
            Id : int
            /// The full name of the product.
            Name : string
            /// The label of the product.
            Label  : string
            /// The pharmocological shape quantity of the product.
            Quantity : float
            /// The pharmocological shape unit of the product.
            Unit : string
            /// The container of the product.
            Container : string
            /// The trade products of the prescription product.
            TradeProducts : TradeProduct []
        }


    /// <summary>
    /// A Z-Index Generic Product
    /// </summary>
    /// <remarks>
    /// A generic product is the most basic Z-index product.
    /// It contains the substances along with the substance
    /// concentration. Note that 2 ampoules, one of 1 mL en one
    /// of 5 mL with the same substance concentration comprise
    /// the same generic product.
    /// </remarks>
    type GenericProduct =
        {
            /// The id of the generic product,i.e. GPK.
            Id : int
            /// The full name of the product.
            Name : string
            /// The label of the product.
            Label : string
            /// The ATC-5 code of the product.
            /// The code is a string of 7 characters.
            ATC : string
            /// The ATC-5 name of the product.
            ATCName : string
            /// The pharmacological shape of the product.
            Shape : string
            /// The route of administration of the product.
            Route : string []
            /// The substances of the product.
            Substances : ProductSubstance []
            /// The prescription products of the generic product.
            PrescriptionProducts : PrescriptionProduct []
        }
    /// A substance in a product.
    and ProductSubstance =
        {
            /// The id of the substance.
            SubstanceId : int
            /// The order in which the substances are listed
            /// in a GenericProduct and GenPresProduct name.
            SortOrder : int
            /// The name of the substance.
            /// For example: 'NORADRENALINE'
            SubstanceName : string
            /// The quantity of the substance.
            SubstanceQuantity : float
            /// The unit of the substance.
            SubstanceUnit : string
            /// The id of the generic substance (salt form).
            GenericId : int
            /// The full name (salt form) of the substance.
            /// For example: 'NORADRENALINE WATERSTOFTARTRAAT-1-WATER'
            GenericName : string
            /// The quantity of the generic substance (salt form).
            GenericQuantity : float
            /// The unit of the generic substance (salt form).
            GenericUnit : string
            /// The pharmacological shape unit in which the substance is contained.
            ShapeUnit : string
        }


    /// <summary>
    /// A GenPresProduct
    /// </summary>
    /// <remarks>
    /// A GenPres product is a higher level product. It contains
    /// the generic products with the same substance composition.
    /// </remarks>
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


    /// A Z-Index rule Frequency.
    type RuleFrequency = { Frequency: float; Time: string }


    /// A Z-Index rule MinMax.
    type RuleMinMax = { Min: float Option; Max: float Option }


    /// A Z-Index dose rule.
    type DoseRule =
        {
            /// The id of the DoseRule
            Id : int
            /// The care group the DoseRule applies to
            /// this is either 'intensieve' or 'niet-intensieve' or 'all'
            CareGroup : string
            /// This is the usage of the dose rule, can be therapeutic or
            /// prophylactic.
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
    /// A Z-Index trade or consumer product that is part of a dose rule.
    and RuleProduct = { Id: int; Name: string }
    /// A Z-Index generic product that is part of a dose rule.
    and RuleGenericProduct =
        {
            Id: int
            Name: string
            Route: string []
            Unit: string
            Substances : RuleSubstance []
        }
    /// A Z-Index substance that is part of a RuleGenericProduct.
    and RuleSubstance = { Name: string; Quantity: float; Unit: string }


    /// The ATC group coding
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


    type AgeInMo = float Option

    type WeightInKg = float Option

    type BSAInM2 = float Option


    /// The patient filter to get the
    /// DoseRules for a specific patient.
    type PatientFilter =
        {
            Age: AgeInMo
            Weight: WeightInKg
            BSA: BSAInM2
        }


    /// The ProductFilter to get the
    /// DoseRules for a specific product.
    type ProductFilter =
        | GPKRoute of (int * string)
        | GenericShapeRoute of GenericShapeRoute

    and GenericShapeRoute =
        {
            Generic: string
            Shape: string
            Route: string
        }


    /// The Filter to get the DoseRules
    /// for a specific patient and product.
    type Filter =
        {
            Patient: PatientFilter
            Product: ProductFilter
        }


    /// The DoseRules that belong to a specific
    /// GenPresProduct.
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


    /// The Assortment Product that is
    /// available as a GenericProduct.
    type Assortment =
        {
            /// The GPK code
            GPK: int
            /// The generic name
            Generic: string
            /// The TallMan alternative name
            TallMan : string
            /// The Divisibility of the product
            Divisible : int
        }
