namespace Informedica.GenOrder.Lib


[<AutoOpen>]
module Types =


    open System
    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib.Types


    /// Type alias for Gender from Informedica.GenForm.Lib.Types
    type Gender = Informedica.GenForm.Lib.Types.Gender
    /// Type alias for Patient from Informedica.GenForm.Lib.Types
    type Patient = Informedica.GenForm.Lib.Types.Patient
    /// Type alias for PrescriptionRule from Informedica.GenForm.Lib.Types
    type PrescriptionRule = Informedica.GenForm.Lib.Types.PrescriptionRule


    /// <summary>
    /// An OrderVariable represents the combination
    /// of a Variable and Constraints
    /// </summary>
    type OrderVariable =
        {
            // The Constraints to apply to the Variable
            DefinedConstraints: Constraints
            // The Calculated constraints
            CalculatedConstraints: Constraints
            // Stores the values/range
            Variable: Variable

        }

    /// <summary>
    /// Represents constraints that can be applied to an OrderVariable
    /// </summary>
    and Constraints =
        {
            // The minimum value constraint
            Min: Minimum option
            // The maximum value constraint
            Max: Maximum option
            // The increment constraint for stepping values
            Incr: Increment option
            // A set of allowed values
            Values: ValueSet option
        }


    /// <summary>
    /// An order equation is either a product equation or a
    /// sum equation
    /// </summary>
    type OrderEquation =
        | OrderProductEquation of OrderVariable * OrderVariable list
        | OrderSumEquation of OrderVariable * OrderVariable list


    /// Type that represents a time duration
    type Time = Time of OrderVariable


    /// Type that represents a count
    type Count = Count of OrderVariable


    /// Type that represents a frequency
    type Frequency = Frequency of OrderVariable


    /// Type that represents a quantity
    type Quantity = Quantity of OrderVariable


    /// Type that represents a quantity per time unit
    type PerTime = PerTime of OrderVariable

    /// Type that represents a rate
    type Rate = Rate of OrderVariable


    /// Type that represents a total quantity
    type Total = Total of OrderVariable


    /// Type that represents a concentration
    type Concentration = Concentration of OrderVariable


    /// Type that represents an adjusted quantity for dose calculations
    type QuantityAdjust = QuantityAdjust of OrderVariable


    /// Type that represents an adjusted quantity per time for dose calculations
    type PerTimeAdjust = PerTimeAdjust of OrderVariable


    /// Type that represents an adjusted rate for dose calculations
    type RateAdjust = RateAdjust of OrderVariable


    /// Type that represents an adjusted total for dose calculations
    type TotalAdjust = TotalAdjust of OrderVariable


    /// An Id is represented by a string
    type Id = Id of string


    /// <summary>
    /// Represents a Dose with various quantity measurements
    /// </summary>
    type Dose =
        {
            // The quantity of the dose
            Quantity: Quantity
            // The quantity per time unit
            PerTime: PerTime
            // The rate of administration
            Rate: Rate
            // The total quantity
            Total: Total
            // The adjusted quantity for dose calculations
            QuantityAdjust: QuantityAdjust
            // The adjusted quantity per time for dose calculations
            PerTimeAdjust: PerTimeAdjust
            // The adjusted rate for dose calculations
            RateAdjust: RateAdjust
            // The adjusted total for dose calculations
            TotalAdjust: TotalAdjust
        }


    /// Models an Item (substance) in a Component
    type Item =
        {
            // The name of the item
            Name: Name
            // The quantity of an Item in a Component
            ComponentQuantity: Quantity
            // The quantity of an Item in an Orderable
            OrderableQuantity: Quantity
            // The Item concentration in a Component
            ComponentConcentration: Concentration
            // The Item concentration in an Orderable
            OrderableConcentration: Concentration
            // The Item Dose administered
            Dose: Dose
        }


    /// Models a Component in an Orderable
    type Component =
        {
            // The unique identifier of the component
            Id: Id
            // The name of a Component
            Name: Name
            // The pharmaceutical form of a component
            Form: string
            // The quantity of a Component
            ComponentQuantity: Quantity
            // The quantity of a Component in an Orderable
            OrderableQuantity: Quantity
            // The count of a Component in an Orderable
            OrderableCount: Count
            // The quantity of a Component in an Order
            OrderQuantity: Quantity
            // The count of a Component in an Order
            OrderCount: Count
            // The concentration of a Component in an Orderable
            OrderableConcentration: Concentration
            // The Component Dose administered
            Dose: Dose
            // The Items in a Component
            Items: Item list
        }


    /// Models an Orderable item that can be prescribed
    type Orderable =
        {
            // The name of the orderable
            Name: Name
            // The quantity of an orderable
            OrderableQuantity: Quantity
            // The quantity of an orderable in an order
            OrderQuantity: Quantity
            // The orderable count in an order
            OrderCount: Count
            // The count of doses in an orderable quantity
            DoseCount: Count
            // The dose of an orderable
            Dose: Dose
            // The list of components in an orderable
            Components: Component list
        }


    /// Represents start time or start and stop time for an order
    type StartStop =
        | Start of DateTime
        | StartStop of DateTime * DateTime


    /// Models a complete order with all its properties
    type Order =
        {
            // The id of an order
            Id: Id
            // Used to adjust doses based on patient parameters
            Adjust: Quantity
            // The orderable item being prescribed
            Orderable: Orderable
            // How the Orderable is prescribed
            Schedule: Schedule
            // The route of administration for the order
            Route: string
            // The duration of an order
            Duration: Time
            // The start and optional stop time of the order
            StartStop: StartStop
        }


    /// Type that represents different prescription patterns
    and Schedule =
        | Once
        | OnceTimed of Time
        | Continuous of Time
        // A discontinuous prescription with a frequency
        | Discontinuous of Frequency
        // A discontinuous prescription with both frequency and time
        | Timed of Frequency * Time


    /// A string list that represents either a product or sum equation mapping
    type EquationMapping =
        | ProductMapping of string list
        | SumMapping of string list


    /// Represents different types of property changes that can be applied to an order
    type OrderPropertyChange =
        | OrderAdjust of (Quantity -> Quantity)
        | ScheduleFrequency of (Frequency -> Frequency)
        | ScheduleTime of (Time -> Time)

        | OrderableQuantity of (Quantity -> Quantity)
        | OrderableDoseCount of (Count -> Count)
        | OrderableDose of (Dose -> Dose)

        | ComponentQuantity of cmp: string * (Quantity -> Quantity)
        | ComponentOrderableQuantity of cmp: string * (Quantity -> Quantity)
        | ComponentOrderableCount of cmp: string * (Count -> Count)
        | ComponentOrderableConcentration of cmp: string * (Concentration -> Concentration)
        | ComponentDose of cmp: string * (Dose -> Dose)

        | ItemComponentQuantity of cmp: string * itm: string * (Quantity -> Quantity)
        | ItemComponentConcentration of cmp: string * itm: string * (Concentration -> Concentration)
        | ItemOrderableQuantity of cmp: string * itm: string * (Quantity -> Quantity)
        | ItemOrderableConcentration of cmp: string * itm: string * (Concentration -> Concentration)
        | ItemDose of cmp: string * itm: string * (Dose -> Dose)


    /// Type alias for MinMax from Informedica.GenForm.Lib.Types
    type MinMax = Informedica.GenForm.Lib.Types.MinMax
    /// Type alias for DoseLimit from Informedica.GenForm.Lib.Types
    type DoseLimit = Informedica.GenForm.Lib.Types.DoseLimit
    /// Type alias for SolutionLimit from Informedica.GenForm.Lib.Types
    type SolutionLimit = Informedica.GenForm.Lib.Types.SolutionLimit


    /// Commands that can be executed on an order for calculations
    type OrderCommand =
        | CalcMinMax of Order
        | IncreaseIncrements of Order
        | CalcValues of Order
        | ReCalcValues of Order
        | SolveOrder of Order
        | ChangeProperty of Order * ChangePropertyCommand

    /// Change an order property, either by
    /// - Decrementing or Incrementing by using the set increment constraint.
    ///   This can result in an in- or decrease below of above the min max constraint.
    /// - Set to Min or Max. Uses the calculated min max constraints
    /// - Set to Median. Uses the calculated values or the increment with a min and max constraint.
    and ChangePropertyCommand =
        | DecreaseScheduleFrequency
        | IncreaseScheduleFrequency
        | SetMinScheduleFrequency
        | SetMaxScheduleFrequency
        | SetMedianScheduleFrequency
        | DecreaseOrderableDoseQuantity of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseQuantity of ntimes: int * useCalc: bool
        | SetOrderableDoseQuantityPerc of perc: int
        | SetMinOrderableDoseQuantity
        | SetMaxOrderableDoseQuantity
        | SetMedianOrderableDoseQuantity
        | DecreaseOrderableDoseRate of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseRate of ntimes: int * useCalc: bool
        | SetMinOrderableDoseRate
        | SetMaxOrderableDoseRate
        | SetMedianOrderableDoseRate
        | DecreaseComponentOrderableQuantity of cmp: string * ntimes: int * useCalc: bool
        | IncreaseComponentOrderableQuantity of cmp: string * ntimes: int * useCalc: bool
        | SetMinComponentOrderableQuantity of cmp: string
        | SetMaxComponentOrderableQuantity of cmp: string
        | SetMedianComponentOrderableQuantity of cmp: string
        | ComponentInStock of cmp: string * onlyInStock: bool


    /// The different possible order types
    type OrderType =
        | AnyOrder
        | ProcessOrder
        | OnceOrder
        | OnceTimedOrder
        | ContinuousOrder
        | DiscontinuousOrder
        | TimedOrder


    /// The representation of a medication order that
    /// can be derived by a drug product inventory
    /// and the related dose rule. A medication order maps
    /// to an Orderable and a Prescription.
    type Medication =
        {
            // Identifies the specific medication order
            Id: string
            // The name of the order
            Name: string
            // The list of drug products that can be used for the order
            Components: ProductComponent list
            // The min max quantity
            Quantity: MinMax
            // The quantities of the medication order
            Quantities: ValueUnit option
            // The route by which the order is applied
            Route: string
            // The type of order
            OrderType: OrderType
            // The unit to adjust the dose with
            // AdjustUnit : Unit option
            // The list of possible frequency values
            Frequencies: ValueUnit option
            // The min and/or max time for the infusion time
            Time: MinMax
            // The dose limits for a medication order
            Dose: DoseLimit option
            // The amount of orderable that will be given each time
            DoseCount: MinMax
            // Dividability option
            Div: BigRational option
            // The adjust quantity for the adjusted dose calculations
            Adjust: ValueUnit option
        }

    /// The product components that are used by the medication order.
    /// A product component maps to a Component in an Orderable.
    /// The first component in the list is the main component.
    /// The medication order quantities unit is the same as the unit used
    /// for the main component.
    and ProductComponent =
        {
            // The name of the product
            Name: string
            // The pharmaceutical form of the product
            Form: string
            // The quantities of the product
            // Note: the first (main) component has the same unit as
            // the medication order unit
            Quantities: ValueUnit option
            // The "divisibility" of the products
            Divisible: BigRational option
            // The dose limits for the product
            Dose: DoseLimit option
            // The solution limits for a product
            Solution: SolutionLimit option
            // The list of substances contained in the product
            Substances: SubstanceItem list
        }

    /// A substance in a product. A substance maps to an Item in a Component.
    and SubstanceItem =
        {
            // The name of the substance
            Name: string
            // Quantities only needed for reconstitution scenarios
            // to tie a specific reconstitution to a quantity
            Quantities: ValueUnit option
            // The possible concentrations of the substance
            // in the products
            Concentrations: ValueUnit option
            // The dose limits for a substance
            Dose: DoseLimit option
            // The solution limits for a substance
            Solution: SolutionLimit option
        }


    /// Represents totals for various nutrients and substances
    type Totals =
        {
            // Total volume
            Volume: string option
            // Total energy content
            Energy: string option
            // Total protein content
            Protein: string option
            // Total carbohydrate content
            Carbohydrate: string option
            // Total fat content
            Fat: string option
            // Total sodium content
            Sodium: string option
            // Total potassium content
            Potassium: string option
            // Total chloride content
            Chloride: string option
            // Total calcium content
            Calcium: string option
            // Total phosphate content
            Phosphate: string option
            // Total magnesium content
            Magnesium: string option
            // Total iron content
            Iron: string option
            // Total vitamin D content
            VitaminD: string option
            // Total ethanol content
            Ethanol: string option
            // Total propylene glycol content
            Propyleenglycol: string option
            // Total benzyl alcohol content
            BenzylAlcohol: string option
            // Total boric acid content
            BoricAcid: string option
        }


    /// Type alias for DoseType from Informedica.GenForm.Lib.Types
    type DoseType = Informedica.GenForm.Lib.Types.DoseType


    /// Type alias for NormDose from Informedica.GenForm.Lib.Types
    type NormDose = Informedica.GenForm.Lib.Types.NormDose


    /// Type alias for the GenForm product component (formerly `Product`).
    type Product = Informedica.GenForm.Lib.Types.ProductComponent


    type TextBlock =
        | Valid of string
        | Caution of string
        | Warning of string
        | Alert of string


    /// <summary>
    /// The representation of an order scenario with prescription,
    /// preparation, and administration information
    /// </summary>
    type OrderScenario =
        {
            // The scenario number
            No: int
            // The name of the order
            Name: string
            // The indication for the order
            Indication: string
            // The pharmaceutical form of the order
            Form: string
            // The route of the order
            Route: string
            // The dose type of the order
            DoseType: DoseType
            // An optional diluent
            Diluent: string option
            // The component name
            Component: string option
            // The substance name
            Item: string option
            // The list of diluents to choose from
            Diluents: string[]
            // The list of components to print out
            Components: string[]
            // The list of substances to print out
            Items: string[]
            // The prescription instructions
            Prescription: TextBlock[][]
            // The preparation instructions
            Preparation: TextBlock[][]
            // The administration instructions
            Administration: TextBlock[][]
            // The order itself
            Order: Order
            // Whether to use adjust calculations
            UseAdjust: bool
            // Whether to use a renal rule
            UseRenalRule: bool
            // The renal rule name
            RenalRule: string option
            // Associated product identifiers
            ProductsIds: string[]
        }


    /// Filter for selecting order scenarios based on various criteria
    type Filter =
        {
            // The list of indications to select from
            Indications: string[]
            // The list of generics to select from
            Generics: string[]
            // The list of routes to select from
            Routes: string[]
            // The list of pharmaceutical forms to select from
            Forms: string[]
            // The possible dose types
            DoseTypes: DoseType[]
            // The list of diluents to choose from
            Diluents: string[]
            // The list of components that can be used
            Components: string[]
            // The selected indication
            Indication: string option
            // The selected generic
            Generic: string option
            // The selected route
            Route: string option
            // The selected pharmaceutical form
            Form: string option
            // The selected dose type
            DoseType: DoseType option
            // The diluent to use
            Diluent: string option
            // The list of components that are selected
            SelectedComponents: string[]
        }


    module FilterItem =

        /// Represents different filter item types with their index positions
        type FilterItem =
            | Indication of int
            | Generic of int
            | Route of int
            | Form of int
            | DoseType of int
            | Diluent of int
            | Component of int list


    /// <summary>
    /// The main communication object to transfer the
    /// results of the solver to the client. The Filter
    /// is used to select the correct scenario.
    /// </summary>
    type OrderContext =
        {
            // The filter for selecting scenarios
            Filter: Filter
            // The patient information
            Patient: Patient
            // The list of available scenarios
            Scenarios: OrderScenario[]
        }


    /// The kinds of nutrition order an order plan holds, one context per category
    /// except supplements (any number, each under a feeding) and the electrolyte
    /// and glucose lines (any number).
    [<RequireQualifiedAccess>]
    type NutritionCategory =
        | EnteralFeeding
        | EnteralSupplement
        | TPN
        | Lipid
        | ElectrolyteGlucose


    /// What kind of order an order context holds: a drug, or a nutrition order of
    /// one category. Recorded on the context, never derived from the generic, since
    /// electrolytes and glucose are prescribable as drugs too.
    [<RequireQualifiedAccess>]
    type OrderCategory =
        | Drug
        | Nutrition of NutritionCategory


    /// <summary>
    /// An order context as held in an order plan: its id in the plan, its category,
    /// the context itself, and its intake. The context is wrapped, not extended, so
    /// evaluation and the equation system never see the plan's bookkeeping.
    /// </summary>
    /// <remarks>
    /// The intake is computed when the context is evaluated and copied unchanged
    /// everywhere else, so a signed version stores the intake the signer saw.
    /// </remarks>
    type PlanContext =
        {
            // The context's id in the order plan
            Id: string
            // A drug, or the nutrition category the context holds
            Category: OrderCategory
            // The order context, evaluated within its own patient
            Context: OrderContext
            // The totals over the one scenario the context is narrowed to
            Intake: Totals
        }


    /// <summary>
    /// The one order plan for a patient: the patient data as shown, the plan's
    /// contexts, the ids the row filter keeps, and the totals over the orders of the
    /// filtered contexts. Its orders are derived, the scenario of every narrowed
    /// context; nothing beside the contexts is stored.
    /// </summary>
    type OrderPlan =
        {
            // The patient data as shown, the data a version is signed on
            Patient: Patient
            // The ids of the contexts the row filter keeps; empty for all of them
            Filtered: string[]
            // Every order context of the plan, drug and nutrition alike
            Contexts: PlanContext[]
            // The totals over the orders of the filtered contexts
            Totals: Totals
        }


    /// What a nutrition category draws from: data passed in by the composition
    /// root, never a constant in this library.
    type NutritionRuleSet =
        {
            // The category the set serves
            Category: NutritionCategory
            // The label shown for the category
            Label: string
            // The indications the category's dose rules are filtered on
            Indications: string[]
            // The generics the category's dose rules are filtered on
            Generics: string[]
        }


    /// Who signed an order plan version
    type Signer =
        {
            // The user id at the identity provider
            UserId: string
            // The name shown for the signer
            DisplayName: string
        }


    /// <summary>
    /// An order plan version: the order plan as the prescriber signed it, as the
    /// record holds it. A prescriber creates one by signing; it stores the whole
    /// plan, filter, totals and each context's intake included, so a reopen restores
    /// what the signer saw, nothing recomputed.
    /// </summary>
    type OrderPlanVersion =
        {
            // The version's own id
            Id: string
            // The version number, one above the patient's previous version
            No: int
            // The patient the plan belongs to
            PatientId: string
            // The id of the version this one was built on; None for the first
            Base: string option
            // Who signed
            SignedBy: Signer
            // When
            SignedAt: DateTime
            // The order plan as signed
            Plan: OrderPlan
            // Whether the patient data was verified against the platform's reading
            // at the challenge
            Verified: bool
        }


    /// Why a Dto does not parse to its domain value.
    [<RequireQualifiedAccess>]
    type DtoError =
        // A dose type string the Dto carries that names no dose type
        | UnknownDoseType of string
        // A text block kind the Dto carries that names no kind
        | UnknownTextKind of string
        // The order of a scenario could not be created from its Dto
        | OrderNotCreated of string
        // An order category string the Dto carries that names no category
        | UnknownCategory of string
        // A nested Dto a serializer left null, by field name
        | Missing of string
        // The patient of a context or an order plan is none
        | Patient of Informedica.GenForm.Lib.Types.PatientError


    module Exceptions =

        /// Messages for order-related exceptions
        type Message =
            | OrderCouldNotBeCreated of Exception
            | OrderCouldNotBeSolved of string * Order


    module Events =

        /// Events that can occur during order processing
        type Event =
            | MinIncrMaxToValues of OrderVariable
            | MedicationCreated of string
            | ComponentItemsHarmonized of string
            | SolverReplaceUnit of (Name * Unit)
            | OrderIncreaseQuantityIncrement of Order
            | OrderIncreaseRateIncrement of Order
            | OrderSolveStarted of Order
            | OrderSolveFinished of Order
            | OrderSolveConstraintsStarted of Order * Constraint list
            | OrderSolveConstraintsFinished of Order * Constraint list
            | OrderScenario of string
            | OrderScenarioWithNameValue of Order * Name * BigRational


    module Logging =


        open Informedica.Logging.Lib


        /// Messages for order-related logging
        type OrderMessage =
            | OrderException of Exceptions.Message
            | OrderEventMessage of Events.Event

            interface IMessage
