namespace Informedica.GenForm.Lib


[<AutoOpen>]
module Types =

    open Informedica.Utils.Lib.BCL

    open Informedica.Logging.Lib
    open Informedica.GenUnits.Lib

    type MinMax = Informedica.GenCore.Lib.Ranges.MinMax


    /// <summary>
    /// One row of the "FormRoute" sheet, parsed by <c>Mapping.getFormRoutes</c>: the default values for a
    /// pharmaceutical form on a route. Field names equal sheet column names except where noted. A boolean
    /// column is true only for the text "true", in any case. Every column is required.
    /// </summary>
    type FormRoute =
        {
            /// The Route
            Route: string
            /// The pharmaceutical form
            Form: string
            /// The Unit of the form
            Unit: Unit
            /// The DoseUnit column; NoUnit when it is empty. The dose limits then use the Unit of the form,
            /// and they are empty only when both units are missing.
            DoseUnit: Unit
            /// The minimum Dose quantity
            MinDoseQty: ValueUnit option
            /// The maximum Dose quantity
            MaxDoseQty: ValueUnit option
            /// The minimum dose quantity per kg; sheet column "MinDoseQtyKg"
            MinDoseQtyPerKg: ValueUnit option
            /// The maximum dose quantity per kg; sheet column "MaxDoseQtyKg"
            MaxDoseQtyPerKg: ValueUnit option
            /// The Divisible of the pharmaceutical form, used for a product whose Formulary row has no
            /// Divisible; sheet column "Divisible". It means the same as the Divisible on the product.
            Divisibility: BigRational option
            /// Whether a Dose runs over a Time
            Timed: bool
            /// Whether the pharmaceutical form needs to be reconstituted
            Reconstitute: bool
            /// Whether the pharmaceutical form is a solution
            IsSolution: bool
        }


    /// The types for Access.
    type AccessDevice =
        /// Peripheral Venous Access
        | PVL
        /// Central Venous Access
        | CVL
        /// An enteral feeding tube; no venous access
        | EnteralTube
        /// Any Venous Access
        | AnyAccess


    /// Possible Genders.
    type Gender =
        | Male
        | Female
        | AnyGender


    type RenalFunction =
        | EGFR of int option * int option
        | IntermittentHemodialysis
        | ContinuousHemodialysis
        | PeritonealDialysis


    /// Possible Dose Types.
    type DoseType =
        /// A Once only Dose
        | Once of string
        /// A Maintenance Dose
        | Discontinuous of string
        /// A Continuous Dose
        | Continuous of string
        /// A discontinuous dose given over an administration time
        | Timed of string
        /// A once only dose given over an administration time
        | OnceTimed of string
        | NoDoseType


    /// <summary>
    /// One row of the "Reconstitution" sheet, parsed by <c>Product.Reconstitution.parseReconstitution</c>:
    /// the solvent that dissolves a product for administration. Field names equal sheet column names except
    /// where noted. Every column is required.
    /// </summary>
    type Reconstitution =
        {
            /// The GPK of the reconstitution
            GPK: string
            /// The route for the reconstitution
            Route: string
            /// The location for the reconstitution rule; sheet column "Loc"
            Location: string option
            /// The department for the reconstitution; sheet column "Dep"
            Department: string option
            /// The volume of the solvent, in mL; sheet column "SolventVol".
            /// Defaults to 1 mL when the cell is empty.
            SolventVolume: ValueUnit
            /// The increase in total volume, in mL, when the product dissolves; sheet column "ExpansionVol"
            ExpansionVolume: ValueUnit option
            /// The solvents for the reconstitution, ';'-separated in the sheet column
            /// "Solvents"
            Solvents: string[]
        }


    /// A substance of a product.
    type SubstanceItem =
        {
            /// The name of the substance
            Name: string
            /// The concentration of the substance
            Concentration: ValueUnit option
            /// The molar concentration of the substance
            MolarConcentration: ValueUnit option
        }

    type Brand = string
    type HPK = string

    type TradeProduct =
        {
            HPK: HPK
            Brand: Brand
            Substances: SubstanceItem list
        }

    type GPK = string
    type ATC = string
    type MainGroup = string
    type SubGroup = string

    type GenericName = string

    /// A Product type.
    type ProductComponent =
        {
            /// The GPK id of the Generic Product
            GPK: GPK
            /// The ATC code of the Product
            ATC: ATC
            /// The ATC main group of the Product
            MainGroup: MainGroup
            /// The ATC subgroup of the Product
            SubGroup: SubGroup
            /// The Generic name of the Product
            Generic: GenericName
            /// A tall-man representation of the Generic name of the Product
            TallMan: string
            /// Synonyms for the Product
            Synonyms: string array
            /// The full product names of the Product
            ProductLabels: string list
            /// The label of the Product
            Label: string
            /// The pharmaceutical form of the Product
            Form: string
            /// The possible Routes of administration of the Product
            Routes: string[]
            /// The possible quantities of the pharmaceutical form of the Product
            FormQuantities: ValueUnit
            /// The unit of the pharmaceutical form of the Product
            FormUnit: Unit
            /// Whether the pharmaceutical form of the Product requires reconstitution
            RequiresReconstitution: bool
            /// The possible reconstitution rules for the Product
            Reconstitution: Reconstitution[]
            /// The number of equal parts one unit of the Product can be divided into, from the Formulary row or
            /// else from the pharmaceutical form. A tablet in 4 parts has the value 4 and a step of 1/4 tablet.
            /// For a droplet form the value is the number of drops per mL: it sets the size of the droplet unit,
            /// and the step is one drop. None means the Product has no step.
            Divisible: BigRational option
            /// The Substances in the Product
            Substances: SubstanceItem array
            /// Brand products
            TradeProducts: TradeProduct list
        }


    /// <summary>
    /// One row of the "Formulary" sheet, parsed by <c>Product.parseFormularyProducts</c>: a product of the
    /// hospital formulary.
    /// </summary>
    /// <remarks>
    /// The sheet is also the only source of the composition of enteral and parenteral products.
    /// <c>Product.Enteral.get</c> and <c>Product.Parenteral.get</c> select these rows by <c>ProductType</c>
    /// and make a <c>ProductComponent</c> from each, with the substances from the nutrition fields below
    /// (named by <c>Product.getAdditionalSubstances</c>).
    ///
    /// Field names equal sheet column names except where noted. Every column is required: a missing column
    /// fails the parse.
    /// </remarks>
    type FormularyProduct =
        {
            /// The GPK code. A row with GPKODE "0" is a dummy and is dropped.
            GPKODE: string
            /// The type of product; sheet column "Type", one of "medication", "enteral" or "parenteral".
            /// Any other value gives NoProduct.
            ProductType: ProductType
            /// The departments whose column (UMCU, ICC, NEO, ICK, HCK) holds an "x"
            Departments: string list
            /// The generic name
            Generic: string
            /// The TallMan alternative name
            TallMan: string
            /// The number of equal parts one unit of the product can be divided into, or the
            /// number of drops per mL for a droplet form; sheet column "Divisible". See the
            /// Divisible on the Product.
            Divisible: int option
            /// Whether the substances take the G-Standaard generic name instead of the substance name;
            /// sheet column "UseGenName", "x" for true
            UseGenName: bool
            /// The mmol per unit of the form, for the substance with the same name as the product
            Mmol: BigRational option
            /// The pharmaceutical form. Required on a parenteral and an enteral row,
            /// where it is the form of the virtual product.
            Form: string option
            /// The routes, separated by ";" in the sheet column "Routes". Required on a parenteral and an
            /// enteral row, where each route must be in the Routes sheet. Empty on a medication row, which
            /// takes its routes from the G-Standaard.
            Routes: string[]
            /// Brand
            Brand: string option
            /// Generic name
            GenName: string option
            /// GStand generic name; sheet column "GStand"
            GStandName: string option
            /// The name that replaces the G-Standaard name of the product, such as
            /// "amfotericine b lipidencomplex", taken as written without lowercasing. None when the cell is
            /// empty. The name of a virtual product, such as "NaCl 0,9%", is not used yet (#1444).
            AltName: string option
            /// The unit of the form. An enteral row whose unit does not parse gives no product. A parenteral
            /// product does not read this unit; its form unit is always mL.
            Unit: string option
            // The composition columns below carry a space in the sheet header:
            // "Energy kCal", "Carb g", "Prot g", "Lip g", "Sod mmol", "Pot mmol",
            // "Calc mmol", "Posph mmol", "Magn mmol", "Chlor mmol", "Iron mmol",
            // "VitD IE".

            /// Energy in kCal
            EnergyKCal: BigRational option
            /// Carbohydrates in g
            CarbG: BigRational option
            /// Protein in g
            ProtG: BigRational option
            /// Lipids in g
            LipG: BigRational option
            /// Sodium in mmol
            SodMmol: BigRational option
            /// Potassium in mmol
            PotMmol: BigRational option
            /// Calcium in mmol
            CalcMmol: BigRational option
            /// Phosphorus in mmol
            PosphMmol: BigRational option
            /// Magnesium in mmol
            MagnMmol: BigRational option
            /// Chloride in mmol
            ChlorMmol: BigRational option
            /// Iron in mmol
            IronMmol: BigRational option
            /// Vitamin D in IE
            VitDIE: BigRational option
            // The three flag columns below hold x or TRUE for true, and nothing or FALSE
            // for false, in any case; any other value fails the parse.

            /// The product can be used as a solvent to reconstitute another product;
            /// sheet column "IsSolvent"
            CanBeUsedAsSolvent: bool
            /// The product can be used as a diluent; sheet column "IsDiluent"
            CanBeUsedAsDiluent: bool
            /// The product can be used as an additive; sheet column "IsAdditive"
            CanBeUsedAsAdditive: bool
        }

    and ProductType =
        | MedicationProduct
        | ParenteralProduct
        | EnteralProduct
        | NoProduct


    module ProductType =

        /// Returns true for a parenteral or an enteral product, which is built from its Formulary row and not
        /// from the G-Standaard.
        let isVirtual =
            function
            | ParenteralProduct
            | EnteralProduct -> true
            | MedicationProduct
            | NoProduct -> false


    type LimitTarget =
        | NoLimitTarget
        | OrderableLimitTarget
        | ComponentLimitTarget of string
        | SubstanceLimitTarget of string


    /// A DoseLimit for a pharmaceutical form or Substance.
    type DoseLimit =
        {
            DoseLimitTarget: LimitTarget
            /// The unit to adjust dosing with
            AdjustUnit: Unit option
            /// The unit to dose with
            DoseUnit: Unit
            /// A MinMax Dose Quantity for the DoseLimit
            Quantity: MinMax
            /// A MinMax Quantity Adjust for the DoseLimit
            QuantityAdjust: MinMax
            /// An optional Dose Per Time for the DoseLimit
            PerTime: MinMax
            /// A MinMax Per Time Adjust for the DoseLimit
            PerTimeAdjust: MinMax
            /// A MinMax Rate for the DoseLimit
            Rate: MinMax
            /// A MinMax Rate Adjust for the DoseLimit
            RateAdjust: MinMax
        }


    /// The age a rule applies to: an age range, or adults. A rule for adults applies from 18 years, and to a
    /// patient marked as an adult; it does not apply to an unmarked patient without an age.
    type Age =
        /// The age range the sheet names, in days.
        | AbsoluteAge of MinMax
        /// The rule applies to adults.
        | IsAdult


    /// A PatientCategory to which a Rule applies.
    type PatientCategory =
        {
            Location: string option
            Department: string option
            Gender: Gender
            Age: Age
            Weight: MinMax
            BSA: MinMax
            GestAge: MinMax
            PMAge: MinMax
            Access: AccessDevice
        }


    /// The patient data from GenCORE that selects the rules.
    type PatientData = Informedica.GenCore.Lib.Patients.PatientData


    type ProductId =
        | Gpk of string
        | Hpk of string


    type ComponentLimit =
        {
            Name: string
            /// Specific GPKs
            ProductIds: ProductId array
            Limit: DoseLimit option
            Products: ProductComponent[]
            SubstanceLimits: DoseLimit[]
        }


    type Source =
        | Identified of string
        | Other of string

    type DoseRuleId = string
    type DataId = string
    type DoseRuleGroupId = string
    type SortNo = int


    /// Generic can be constructed by
    /// different pathways
    type GenericLabel =
        /// Constructed by the list of active substances
        /// concatenated by "/"
        | Canonical of string list
        /// A short hand name
        | Shorthand of string
        /// The canonical name appended with the form
        | GenericForm of gen: string * form: string
        /// The canonical name appended with the brand
        | GenericBrand of gen: string * brand: string


    /// Distinguish between a solution
    /// and a solid pharmaceutical form
    type PharmaceuticalForm =
        | Solution of string
        | Solid of string


    type Generic =
        {
            Label: GenericLabel
            Form: PharmaceuticalForm
            Products: ProductId list
        }


    type Indication = string


    type Route = string


    /// The DoseRule type. A DoseRule is uniquely identified by the combination of
    /// Source, Generic, pharmaceutical form, Brand, Route, Indication, PatientCategory and DoseType
    /// (where DoseType also carries the original dose text as its case payload).
    type DoseRule =
        {
            /// Unique identifier of this DoseRule. Treated as opaque by consumers.
            Id: DoseRuleId
            /// The unique identifier of the original dose rule data
            DataId: DataId
            /// The identifier shared by every DoseRule with the same Source, Generic, Form, Brand, Route,
            /// Indication and PatientCategory. Rules in one group differ only in DoseType and dose schedule.
            /// Treated as opaque.
            GroupId: DoseRuleGroupId
            /// Ordinal rank within a rule group, used for stable presentation order.
            SortNo: SortNo
            /// The original source of the dose rule
            Source: Source
            /// The original source text of the dose rule, before structured decomposition.
            SourceText: string
            /// The Indication of the DoseRule
            Indication: Indication
            /// The Generic of the DoseRule
            Generic: Generic
            /// The Route of administration of the DoseRule
            Route: Route
            /// The original text describing the patient category this rule applies to.
            PatientText: string
            /// The PatientCategory the rule applies to. The Age case IsAdult marks a rule for adults.
            PatientCategory: PatientCategory
            /// The original text describing the dose schedule.
            ScheduleText: string
            /// The DoseType of the DoseRule, with the original dose text in each case.
            DoseType: DoseType
            /// The unit to adjust dosing with
            AdjustUnit: Unit option
            /// The possible Frequencies of the DoseRule
            Frequencies: ValueUnit option
            /// MinMax administration time. The time unit is part of the MinMax value.
            AdministrationTime: MinMax
            /// MinMax interval between administrations. The time unit is part of the MinMax value.
            IntervalTime: MinMax
            /// MinMax total duration of the order. The time unit is part of the MinMax value.
            Duration: MinMax
            /// The limits for the pharmaceutical form and route
            FormLimit: DoseLimit option
            /// The limits for each component and the substances in the component
            ComponentLimits: ComponentLimit[]
            /// Reference to a renal-adjustment rule that applies on top of this DoseRule.
            /// TODO: replace with a structured RenalRule type once the renal rule layer is finalised.
            RenalRuleSource: string option
            /// Validation stamp
            Validated: string option
            /// G-Standaard check
            Check: RuleCheck
        }

    and RuleCheck =
        {
            FreqCheck: string option
            DoseCheck: string option
        }


    /// What a DilutionLimit applies to: one component or one substance in the solution.
    [<RequireQualifiedAccess>]
    type DilutionTarget =
        /// The component with this name
        | Component of string
        /// The substance with this name
        | Substance of string


    /// A limit on one component or one substance in the solution: how much of the component
    /// or substance the solution may hold, and in what concentration.
    type DilutionLimit =
        {
            /// The component or the substance the DilutionLimit applies to
            DilutionLimitTarget: DilutionTarget
            /// The MinMax Quantity of the component or substance in the solution
            Quantity: MinMax
            /// The MinMax Quantity of the component or substance in the solution per adjust unit of the solution rule
            QuantityAdjust: MinMax
            /// The possible Quantities of the component or substance in the solution
            Quantities: ValueUnit option
            /// The MinMax Concentration of the component or substance in the solution
            Concentration: MinMax
            /// The Products of the component the DilutionLimit applies to
            Products: ProductComponent[]
        }


    /// The limits on the solution as a whole, which an order applies to the orderable.
    type SolutionLimit =
        {
            /// The number of equal parts one unit of the prepared solution can be divided into, so
            /// the step of the orderable is 1 divided by this value. It takes the place of the
            /// Divisible of the products of the medication.
            Div: BigRational option
            /// The possible Volumes of the solution
            Volumes: ValueUnit option
            /// The MinMax Volume of the solution
            Volume: MinMax
            /// The MinMax Volume of the solution per adjust unit of the solution rule
            VolumeAdjust: MinMax
            /// The MinMax Drip Rate of the solution
            DripRate: MinMax
            /// The fraction of the preparation that makes up one dose, for example 0.5 for half the
            /// preparation. The order uses 1 divided by the fraction as the number of doses in the
            /// preparation.
            DoseFraction: MinMax
        }


    /// A SolutionRule for a Generic, pharmaceutical form, Route, Indication, DoseType, PatientCategory and
    /// Dose range: the diluents and the limits for the solution.
    type SolutionRule =
        {
            /// The Generic of the SolutionRule
            Generic: string
            /// The pharmaceutical form of the SolutionRule
            Form: string option
            /// The Route of the SolutionRule
            Route: string
            /// The Indication of the SolutionRule
            Indication: string option
            /// The dose type of the SolutionRule
            DoseType: DoseType
            /// The PatientCategory of the SolutionRule
            PatientCategory: PatientCategory
            /// The MinMax Dose range of the SolutionRule
            Dose: MinMax
            /// The unit to adjust the limits of the SolutionRule with; None when the sheet gives no adjust unit
            AdjustUnit: Unit option
            /// The diluents to use, from the column "Diluents" of the SolutionRules sheet. A rule
            /// without diluents gives the medication undiluted.
            Diluents: ProductComponent[]
            /// The limits on the solution as a whole
            SolutionLimit: SolutionLimit
            /// The limits on the components and the substances in the solution
            DilutionLimits: DilutionLimit[]
        }


    /// A renal adjustment of the DoseLimit for a pharmaceutical form or Substance.
    type RenalLimit =
        {
            DoseLimitTarget: LimitTarget
            DoseReduction: DoseReduction
            Quantity: MinMax
            /// A MinMax Quantity Adjust for the DoseLimit. A minimum equal to the maximum is a norm dose,
            /// which the order widens for a substance to 90% and 110% of the value.
            QuantityAdjust: MinMax
            /// An optional Dose Per Time for the DoseLimit
            PerTime: MinMax
            /// A MinMax Per Time Adjust for the DoseLimit. A minimum equal to the maximum is a norm dose,
            /// which the order widens for a substance to 90% and 110% of the value.
            PerTimeAdjust: MinMax
            /// A MinMax Rate for the DoseLimit
            Rate: MinMax
            /// A MinMax Rate Adjust for the DoseLimit. A minimum equal to the maximum is a norm dose,
            /// which the order widens for a substance to 90% and 110% of the value.
            RateAdjust: MinMax
        }

    and DoseReduction =
        /// The renal values replace the dose limits of the dose rule.
        | Replace
        /// The renal values are factors that scale the dose limits of the dose rule.
        | Relative
        | NoReduction


    type RenalRule =
        {
            /// The Generic of the RenalRule
            Generic: string
            /// The Route of administration of the RenalRule
            Route: string
            Indication: string
            /// The source of the RenalRule
            Source: string
            Age: MinMax
            RenalFunction: RenalFunction
            /// The DoseType of the RenalRule
            DoseType: DoseType
            /// The possible Frequencies of the RenalRule
            Frequencies: ValueUnit option
            /// The MinMax Interval Time of the RenalRule
            IntervalTime: MinMax
            /// The list of associated RenalLimits of the RenalRule.
            RenalLimits: RenalLimit array
        }


    type ProductFilter =
        {
            Generic: string
            Form: string option
            Route: string
            FormUnit: Unit option
        }


    /// The departments a patient can be prescribed for, and the default department.
    type Departments =
        {
            /// Every department a dose rule, a solution rule or a reconstitution names, the
            /// default included, distinct and sorted.
            Names: string[]
            /// The department for a patient without a department.
            Default: string
        }


    /// The components a prescriber selected.
    [<RequireQualifiedAccess>]
    type ComponentSelection =
        /// No selection is made: the order uses every component.
        | AllComponents
        /// The selected optional components. An empty list selects no optional component.
        | Selected of string list


    /// A Filter to get the DoseRules for a specific Patient.
    type DoseFilter =
        {
            /// the Indication to filter on
            Indication: string option
            /// the Generic to filter on
            Generic: string option
            /// the pharmaceutical form to filter on
            Form: string option
            /// the Route to filter on
            Route: string option
            /// the DoseType to filter on
            DoseType: DoseType option
            /// the diluent to use
            Diluent: string option
            /// the optional components the prescriber selected
            Components: ComponentSelection
            /// the patient to filter on
            PatientData: PatientData
        }


    /// The values a dose filter can choose from
    type FilterOptions =
        {
            /// The indications of the rules
            Indications: string[]
            /// The generics of the rules
            Generics: string[]
            /// The routes of the rules
            Routes: string[]
            /// The pharmaceutical forms of the rules
            Forms: string[]
            /// The dose types of the rules
            DoseTypes: DoseType[]
        }


    type SolutionFilter =
        {
            /// The Generic of the SolutionRule
            Generic: string
            /// The pharmaceutical form of the SolutionRule
            Form: string option
            /// The Route of the SolutionRule
            Route: string option
            /// The Indication of the SolutionRule
            Indication: string option
            DoseType: DoseType option
            /// the patient
            PatientData: PatientData
            /// the diluent to dilute the component
            Diluent: string option
            /// A dose to select the SolutionRule with. SolutionRule.filter does not use it.
            Dose: ValueUnit option
        }

    /// A PrescriptionRule for a specific Patient
    /// with a DoseRule and a list of SolutionRules.
    type PrescriptionRule =
        {
            PatientData: PatientData
            DoseRule: DoseRule
            SolutionRules: SolutionRule[]
            RenalRules: RenalRule[]
        }


    /// Whether the solution rules count when two prescription rules are compared.
    [<RequireQualifiedAccess>]
    type SolutionRuleComparison =
        | Counted
        | Ignored


    // qualified, so that the field names do not take over the type inference of the dose rule records
    /// One side of a link rule: what an order must match, by the dose rule it comes from. An empty
    /// generic, route or indication matches any value, and a dose type with an empty text matches any
    /// text. The route compares by the route mapping, so a long and a short name of a route match.
    [<RequireQualifiedAccess>]
    type LinkSide =
        {
            /// The generic label of the dose rule
            Generic: string
            /// The route
            Route: string
            /// The indication
            Indication: string
            /// The dose type with its text; discontinuous, timed or continuous
            DoseType: DoseType
        }


    /// Whether a linked order can be prescribed without its main order.
    [<RequireQualifiedAccess>]
    type LinkRequirement =
        /// The linked order is only prescribed with its main order
        | Required
        /// The linked order can also be prescribed alone
        | Optional


    /// A parsed row of the LinkRules sheet: the order that may be linked, its main order, and
    /// whether the link is required.
    [<RequireQualifiedAccess>]
    type LinkRule =
        {
            /// The side of the linked order
            Linked: LinkSide
            /// The side of the main order
            Main: LinkSide
            /// Whether the linked order needs its main order
            Requirement: LinkRequirement
        }


    type NormDose =
        | NormQuantityAdjust of LimitTarget * ValueUnit
        | NormPerTimeAdjust of LimitTarget * ValueUnit
        | NormRateAdjust of LimitTarget * ValueUnit


    type Message =
        | Info of string
        | Warning of string
        | ErrorMsg of (string * exn option)

        interface IMessage


    /// The rows of the sheets, mapped into the domain types above. Defined last so that the domain types
    /// cannot refer to these types.
    [<AutoOpen>]
    module Data =

        /// <summary>
        /// One row of the "Units" sheet, parsed by <c>Mapping.getUnitMapping</c>: a long Z-Index unit name
        /// and its short name. <c>Mapping.mapUnit</c> matches Long, Short or MV and returns
        /// <c>"{Short}[{Group}]"</c> for <c>UnitsParse.fromString</c>.
        /// </summary>
        type UnitMapping =
            {
                /// sheet column "ZIndexUnitLong"
                Long: string
                /// sheet column "Unit"
                Short: string
                /// sheet column "MetaVisionUnit"
                MV: string
                /// sheet column "Group": the unit group (Mass, Volume, ...) that
                /// completes the unit expression
                Group: string
            }


        /// <summary>
        /// One row of the "Routes" sheet, parsed by <c>Mapping.getRouteMapping</c>: a long Z-Index route name
        /// and its short Dutch name. <c>Mapping.mapRoute</c> matches either name and returns <c>Long</c>.
        /// </summary>
        type RouteMapping =
            {
                /// sheet column "ZIndex"
                Long: string
                /// sheet column "ShortDutch"
                Short: string
            }


        /// <summary>
        /// One row of the "Totals" sheet, parsed by <c>Mapping.getTotals</c> and used by
        /// <c>Informedica.GenOrder.Lib.Totals.getTotals</c>: for a range of age, weight and gestational age,
        /// the unit, time unit and per-time limits of a total intake, such as volume or energy.
        /// </summary>
        /// <remarks>
        /// The resource is optional: a load failure gives an empty array and a Warning, and does not fail
        /// the whole load (see <c>Resources.defaultRegistry</c>).
        /// </remarks>
        type TotalsData =
            {
                Name: string
                /// age bounds in DAYS
                MinAge: BigRational option
                MaxAge: BigRational option
                /// weight bounds in GRAMS
                MinWeight: BigRational option
                MaxWeight: BigRational option
                /// sheet columns "MinGestAge" and "MaxGestAge": gestational age bounds in DAYS
                MinGestAge: BigRational option
                MaxGestAge: BigRational option
                Unit: Unit option
                /// sheet column "Adj": the patient adjustment unit (kg, m2)
                Adj: Unit option
                TimeUnit: Unit option
                MinPerTime: BigRational option
                MaxPerTime: BigRational option
                MinPerTimeAdj: BigRational option
                MaxPerTimeAdj: BigRational option
            }


        // qualified, so that the field names do not take over the type inference of the records before it
        /// One row of the "RuleSets" sheet: a nutrition category, its label, and an indication and a
        /// generic the category offers. Several rows make up the rule set of one category.
        [<RequireQualifiedAccess>]
        type RuleSetData =
            {
                /// sheet column "Category": EnteralFeeding, EnteralSupplement, TPN, Lipid or ElectrolyteGlucose
                Category: string
                /// sheet column "Label": the label of the category; the label of the first row of the category is used
                Label: string
                /// sheet column "Indication": an indication the dose rules of the category are filtered on
                Indication: string
                /// sheet column "Generic": a generic label the dose rules of the category are filtered on, with
                /// the brand or the form in brackets when the dose rule is restricted to one. Empty for every
                /// generic under the indication.
                Generic: string
            }


        // qualified, so that the field names do not take over the type inference of the records before it
        /// One row of the "LinkRules" sheet: the dose rule of a linked order and the dose rule of its
        /// main order. An empty field matches any value, except the two dose types, and Required, where
        /// empty means no. A row with both dose types empty is blank and skipped.
        [<RequireQualifiedAccess>]
        type LinkRuleData =
            {
                /// sheet column "Generic": the generic label of the linked order
                Generic: string
                /// sheet column "Route": the route of the linked order
                Route: string
                /// sheet column "Indication": the indication of the linked order
                Indication: string
                /// sheet column "DoseType": discontinuous, timed or continuous; never empty
                DoseType: string
                /// sheet column "DoseText": the text of the dose type; empty for any text
                DoseText: string
                /// sheet column "MainGeneric": the generic of the main order, by its label
                MainGeneric: string
                /// sheet column "MainRoute": the route of the main order
                MainRoute: string
                /// sheet column "MainIndication": the indication of the main order
                MainIndication: string
                /// sheet column "MainDoseType": discontinuous, timed or continuous; never empty
                MainDoseType: string
                /// sheet column "MainDoseText": the text of the main dose type; empty for any text
                MainDoseText: string
                /// sheet column "Required": yes when the linked order cannot be prescribed without its
                /// main order; no or empty when it can
                Required: string
            }


        type HashId = string


        /// <summary>
        /// One row of the "DoseRules" sheet, parsed by
        /// <c>DoseRuleData.parseDoseRuleData</c> and built into <c>DoseRule</c>s by
        /// <c>DoseRuleLoader.fromData</c>.
        /// </summary>
        /// <remarks>
        /// Field names equal sheet column names except where a comment says otherwise.
        /// <c>DoseRuleData.headers</c> gives the column order, which <c>dataToCsv</c> also writes. A dose rule
        /// has one row for each component or substance dose limit.
        ///
        /// A column read with <c>getIfNull</c> or <c>getOpt</c> may be absent from the sheet. Every other
        /// column is required: a missing column fails the parse.
        /// </remarks>
        type DoseRuleData =
            {
                /// A hash of the identifying fields of the row, Component and Substance included.
                /// Of the rows with the same RowId, dedupRowsByRowId keeps the first, and warns when their dose
                /// limits differ.
                RowId: HashId
                /// A hash of the dose rule, shared by every component and substance row of the rule.
                /// Becomes DoseRule.DataId.
                RuleId: HashId
                /// sheet column "GrpId": shared by every rule with the same clinical context; such rules
                /// differ only in dose type and schedule
                GrpId: HashId
                /// The rank within a rule group, for a stable order. Defaults to 1 when absent or not a number.
                SortNo: int
                /// The source, parsed into Identified or Other
                Source: string
                /// The original free text this structured row was derived from
                SourceText: string
                Generic: GenericData
                Indication: string
                Route: string
                /// The original free-text description of the patient category
                PatientText: string
                Patient: PatientCategoryData
                ScheduleText: string
                ScheduleData: ScheduleData
                /// The validation stamp; becomes DoseRule.Validated
                Validated: string option
                /// The outcome of the G-Standaard check; becomes DoseRule.Check. Empty when the rule agrees with
                /// the G-Standaard. Export.fs writes it back.
                FreqCheck: string option
                DoseCheck: string option
            }

        /// <summary>
        /// The columns of a "DoseRules" row that identify the generic, combined into the <c>Generic</c>
        /// record. Parsing keeps one narrowing, in the order HPKs, GPKs, Brand, Form (see
        /// <c>DoseRuleData.withSingleNarrowing</c>).
        /// </summary>
        and GenericData =
            {
                /// sheet column "Generic": the base substance name. The G-Standaard dose check and the matching
                /// of solution and renal rules use this name, not the generic label.
                Name: string
                Form: string
                Brand: string
                /// sheet column "GPKs", ';'-separated: narrows the products of the Generic to
                /// those with one of these GPKs. Never replaces the Generic name. Dropped at
                /// parse time when the row has HPKs.
                GPKs: string array
                /// sheet column "HPKs", ';'-separated: narrows the products of the Generic to
                /// those with a trade product that has one of these HPKs. Never replaces the
                /// Generic name.
                HPKs: string array
            }

        /// The patient-category columns of a "DoseRules" row: the demographic ranges
        /// that decide which patients a rule applies to.
        and PatientCategoryData =
            {
                /// sheet column "Loc": the location, hospital or institute. The column may be absent and is not
                /// in the headers.
                Location: string
                /// sheet column "Dep": the department or ward
                Dep: string
                /// sheet column "IsAdult", "x" for true: the rule applies to adults, and MinAge and MaxAge are
                /// empty. An empty cell does not mean the rule excludes adults.
                IsAdult: bool
                Gender: Gender
                /// age bounds in DAYS
                MinAge: BigRational option
                MaxAge: BigRational option
                /// weight bounds in GRAMS
                MinWeight: BigRational option
                MaxWeight: BigRational option
                /// body surface area bounds in m2
                MinBSA: BigRational option
                MaxBSA: BigRational option
                /// gestational age bounds in DAYS
                MinGestAge: BigRational option
                MaxGestAge: BigRational option
                /// post-menstrual age bounds in DAYS
                MinPMAge: BigRational option
                MaxPMAge: BigRational option
            }

        /// The dose-type and schedule columns of a "DoseRules" row.
        and ScheduleData =
            {
                /// one of: once, onceTimed, discontinuous, timed, continuous
                DoseType: string
                /// the free text of the dose type, may be empty
                DoseText: string
                /// sheet column "Freqs", ';'-separated
                Freqs: BigRational array
                /// the patient adjustment unit (kg, m2)
                AdjustUnit: string
                FreqUnit: string
                RateUnit: string
                /// infusion time of a single dose, in TimeUnit
                MinTime: BigRational option
                MaxTime: BigRational option
                TimeUnit: string
                /// sheet columns "MinInt"/"MaxInt": interval between two doses,
                /// in IntUnit
                MinInt: BigRational option
                MaxInt: BigRational option
                IntUnit: string
                /// sheet columns "MinDur"/"MaxDur": the total duration of the order, in DurUnit
                MinDur: BigRational option
                MaxDur: BigRational option
                DurUnit: string
                DoseLimitData: DoseLimitData
            }

        /// The dose-limit columns of a "DoseRules" row. A quantity is per administration, a per-time value
        /// is per FreqUnit, and a rate is the speed of continuous delivery. A column ending in Adj is per
        /// AdjustUnit.
        ///
        /// A norm dose has no column of its own: an adjusted minimum equal to the adjusted maximum
        /// is the norm, as in the renal rules.
        and DoseLimitData =
            {
                /// sheet column "CmpBased", "x" for true: the limit targets the
                /// component rather than a single substance
                CmpBased: bool
                Component: string
                Substance: string
                DoseUnit: string
                MinQty: BigRational option
                MaxQty: BigRational option
                MinQtyAdj: BigRational option
                MaxQtyAdj: BigRational option
                MinPerTime: BigRational option
                MaxPerTime: BigRational option
                MinPerTimeAdj: BigRational option
                MaxPerTimeAdj: BigRational option
                MinRate: BigRational option
                MaxRate: BigRational option
                MinRateAdj: BigRational option
                MaxRateAdj: BigRational option
            }

        /// The key that groups the DoseRules rows: rows with the same key belong to one dose rule group and
        /// differ only in dose type, dose text, component and substance.
        type DataGroupKey =
            {

                Source: string
                Indication: string
                Generic: GenericData
                Patient: PatientCategoryData
                Route: string
            }


        /// The DoseRuleData rows with the same DataGroupKey, with their pharmaceutical forms, products and the
        /// warnings from grouping.
        type GroupedRuleData =
            {
                DataGroupKey: DataGroupKey
                DoseRuleData: DoseRuleData[]
                Forms: string[]
                Products: ProductComponent[]
                /// The warnings from grouping, such as rows with the same RowId and different dose limits.
                /// DoseRuleLoader.fromData collects them.
                Warnings: string list
            }

        /// <summary>
        /// One row of the "SolutionRules" sheet, parsed by <c>SolutionRule.parseSolutionRuleData</c> and
        /// built into <c>SolutionRule</c>s by <c>SolutionRule.map</c>: how a medication is diluted and
        /// administered.
        /// </summary>
        /// <remarks>
        /// Field names equal sheet column names except where noted. Generic, Route and Indication must match
        /// the corresponding DoseRule. Every column is required: a missing column fails the parse.
        /// </remarks>
        type SolutionRuleData =
            {
                // solution rule section
                /// The generic of the rule
                Generic: string
                Form: string
                Route: string
                Indication: string
                /// sheet column "Loc": the location, hospital or institute
                Location: string option
                /// sheet column "Dep": the department or ward
                Department: string option
                /// the venous access, "x" when the rule applies to it
                CVL: string
                PVL: string
                /// age bounds in DAYS
                MinAge: BigRational option
                MaxAge: BigRational option
                /// weight bounds in GRAMS
                MinWeight: BigRational option
                MaxWeight: BigRational option
                /// gestational age bounds in DAYS
                MinGestAge: BigRational option
                MaxGestAge: BigRational option
                /// the dose range that selects this solution rule; not a dose limit
                MinDose: BigRational option
                MaxDose: BigRational option
                /// one of: once, onceTimed, discontinuous, timed, continuous
                DoseType: string
                DoseText: string
                /// sheet column "Diluents": the diluents, '|'-separated. An order takes the first one when none
                /// is chosen. Rows belong to one rule only when this cell is the same, so every row of a rule,
                /// the diluent row too, repeats the cell.
                Diluents: string list
                /// sheet column "Div": the number of equal parts one unit of the prepared
                /// solution can be divided into, see the Div on the SolutionRule
                Div: BigRational option
                /// standard volumes in mL, ';'-separated
                Volumes: BigRational array
                /// total volume bounds in mL
                MinVol: BigRational option
                MaxVol: BigRational option
                /// volume bounds in mL per AdjustUnit
                MinVolAdj: BigRational option
                MaxVolAdj: BigRational option
                /// the patient adjustment unit (kg, m2). A row with a volume or a quantity per adjust needs it. Rows
                /// belong to one rule only when this cell is the same, so every row of a rule repeats the cell.
                AdjustUnit: string
                /// administration fraction: the fraction of the preparation that makes up one dose,
                /// for example 0.5 for half the preparation and 1 for the whole preparation
                MinPerc: BigRational option
                MaxPerc: BigRational option
                // dilution limit section
                /// sheet column "Component": the component the row limits. The value "diluent" without a
                /// substance limits the chosen diluent of the rule.
                Component: string
                Substance: string
                /// sheet column "Unit": the substance unit the quantity and
                /// concentration limits below are expressed in
                Unit: string
                /// standard substance quantities, ';'-separated
                Quantities: BigRational array
                MinQty: BigRational option
                MaxQty: BigRational option
                /// the minimum quantity per AdjustUnit; sheet column "MinQtyAdj"
                MinQtyAdj: BigRational option
                /// the maximum quantity per AdjustUnit; sheet column "MaxQtyAdj"
                MaxQtyAdj: BigRational option
                /// infusion (drip) rate bounds in mL/hour
                MinDrip: BigRational option
                MaxDrip: BigRational option
                /// Concentration bounds in Unit/mL. On a row with a Substance the bounds are the concentration
                /// of the substance in the prepared solution. On a row with a Component, no Substance and the
                /// Unit mL, the bounds are the fraction of the whole prepared solution, other components and
                /// diluent included, that the component may be: 1 part in 8 is the value 0.125.
                MinConc: BigRational option
                MaxConc: BigRational option
            }


        /// <summary>
        /// One row of the "RenalRules" sheet, parsed by <c>RenalRule.parseRenalRuleData</c>: the adjustment of
        /// the dose limits of a matching DoseRule for a renal function. A renal rule does not apply to a
        /// patient younger than 28 days.
        /// </summary>
        /// <remarks>
        /// Field names equal sheet column names except where noted. Generic, Route, Indication and Substance
        /// must match the corresponding DoseRule. Every column is required: a missing column fails the parse.
        /// </remarks>
        type RenalRuleData =
            {
                Generic: string
                Route: string
                Indication: string
                Source: string
                /// age bounds in DAYS
                MinAge: BigRational option
                MaxAge: BigRational option
                /// the dialysis the rule applies to, "x" when it applies
                ContDial: string
                IntDial: string
                PerDial: string
                /// standardized GFR bounds in mL/min/1.73m2
                MinGFR: BigRational option
                MaxGFR: BigRational option
                /// one of: once, onceTimed, discontinuous, timed, continuous
                DoseType: string
                DoseText: string
                /// sheet column "Freqs", ';'-separated
                Frequencies: BigRational array
                /// sheet columns "MinInt"/"MaxInt", in IntervalUnit
                MinInterval: BigRational option
                MaxInterval: BigRational option
                /// sheet column "IntUnit"
                IntervalUnit: string
                Substance: string
                /// sheet column "DoseRed": "rel" when the dose values below are factors on the dose limits,
                /// "repl" when they replace the dose limits. The value "abs" is read as "repl".
                DoseRed: string
                DoseUnit: string
                AdjustUnit: string
                FreqUnit: string
                RateUnit: string
                MinQty: BigRational option
                MaxQty: BigRational option
                MinQtyAdj: BigRational option
                MaxQtyAdj: BigRational option
                MinPerTime: BigRational option
                MaxPerTime: BigRational option
                MinPerTimeAdj: BigRational option
                MaxPerTimeAdj: BigRational option
                MinRate: BigRational option
                MaxRate: BigRational option
                MinRateAdj: BigRational option
                MaxRateAdj: BigRational option
            }
