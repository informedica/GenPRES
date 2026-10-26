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
            /// The Constraints to apply to the Variable
            DefinedConstraints: Constraints
            /// The Calculated constraints
            CalculatedConstraints: Constraints
            /// Stores the values/range
            Variable: Variable

        }

    /// <summary>
    /// Represents constraints that can be applied to an OrderVariable
    /// </summary>
    and Constraints =
        {
            /// The minimum value constraint
            Min: Minimum option
            /// The maximum value constraint
            Max: Maximum option
            /// The increment constraint for stepping values
            Incr: Increment option
            /// A set of allowed values
            Values: ValueSet option
        }


    /// <summary>
    /// An order equation is either a product equation or a
    /// sum equation
    /// </summary>
    type OrderEquation =
        | OrderProductEquation of OrderVariable * OrderVariable list
        | OrderSumEquation of OrderVariable * OrderVariable list


    /// How a value can be set on an order variable, decided by the shape of its current range.
    [<RequireQualifiedAccess>]
    type Shape =
        /// A list of more than one value, to select from.
        | SelectableList
        /// A minimum, an increment and a maximum; a value is selected without writing out the list.
        | SelectableRange
        /// One value with an increment in its defined constraints, to step.
        | Steppable
        /// Nothing to choose.
        | NotSettable


    /// Type that represents a time duration
    type Time = | Time of OrderVariable


    /// Type that represents a count
    type Count = | Count of OrderVariable


    /// Type that represents a frequency
    type Frequency = | Frequency of OrderVariable


    /// Type that represents a quantity
    type Quantity = | Quantity of OrderVariable


    /// Type that represents a quantity per time unit
    type PerTime = | PerTime of OrderVariable

    /// Type that represents a rate
    type Rate = | Rate of OrderVariable


    /// Type that represents a total quantity
    type Total = | Total of OrderVariable


    /// Type that represents a concentration
    type Concentration = | Concentration of OrderVariable


    /// Type that represents an adjusted quantity for dose calculations
    type QuantityAdjust = | QuantityAdjust of OrderVariable


    /// Type that represents an adjusted quantity per time for dose calculations
    type PerTimeAdjust = | PerTimeAdjust of OrderVariable


    /// Type that represents an adjusted rate for dose calculations
    type RateAdjust = | RateAdjust of OrderVariable


    /// Type that represents an adjusted total for dose calculations
    type TotalAdjust = | TotalAdjust of OrderVariable


    /// An Id is represented by a string
    type Id = | Id of string


    /// <summary>
    /// Represents a Dose with various quantity measurements
    /// </summary>
    type Dose =
        {
            /// The quantity of the dose
            Quantity: Quantity
            /// The quantity per time unit
            PerTime: PerTime
            /// The rate of administration
            Rate: Rate
            /// The total quantity
            Total: Total
            /// The adjusted quantity for dose calculations
            QuantityAdjust: QuantityAdjust
            /// The adjusted quantity per time for dose calculations
            PerTimeAdjust: PerTimeAdjust
            /// The adjusted rate for dose calculations
            RateAdjust: RateAdjust
            /// The adjusted total for dose calculations
            TotalAdjust: TotalAdjust
        }


    /// Models an Item (substance) in a Component
    type Item =
        {
            /// The name of the item
            Name: Name
            /// The quantity of an Item in a Component
            ComponentQuantity: Quantity
            /// The quantity of an Item in an Orderable
            OrderableQuantity: Quantity
            /// The Item concentration in a Component
            ComponentConcentration: Concentration
            /// The Item concentration in an Orderable
            OrderableConcentration: Concentration
            /// The Item Dose administered
            Dose: Dose
        }


    /// The strength of one substance of a product.
    type ProductStrength =
        {
            /// The name of the substance
            Substance: string
            /// The concentration of the substance in the product
            Strength: ValueUnit
        }


    /// A product a component can be made of: its strengths, paired, as one product holds them.
    type ComponentProduct =
        {
            /// The id of the product: a GPK, PRK or HPK
            Id: string
            /// The quantities of the product as the component counts them: 1 mL for a liquid
            /// that needs no reconstitution, the product's own quantities otherwise
            FormQuantities: ValueUnit option
            /// The strength of each substance of the product; a substance without a concentration
            /// has no strength here
            Strengths: ProductStrength list
        }


    /// Models a Component in an Orderable
    type Component =
        {
            /// The unique identifier of the component
            Id: Id
            /// The name of a Component
            Name: Name
            /// The pharmaceutical form of a component
            Form: string
            /// The quantity of a Component
            ComponentQuantity: Quantity
            /// The quantity of a Component in an Orderable
            OrderableQuantity: Quantity
            /// The count of a Component in an Orderable
            OrderableCount: Count
            /// The quantity of a Component in an Order
            OrderQuantity: Quantity
            /// The count of a Component in an Order
            OrderCount: Count
            /// The concentration of a Component in an Orderable
            OrderableConcentration: Concentration
            /// The Component Dose administered
            Dose: Dose
            /// The Items in a Component
            Items: Item list
            /// The products the component can be made of; empty when none are known
            Products: ComponentProduct list
        }


    /// Models an Orderable item that can be prescribed
    type Orderable =
        {
            /// The name of the orderable
            Name: Name
            /// The quantity of an orderable
            OrderableQuantity: Quantity
            /// The quantity of an orderable in an order
            OrderQuantity: Quantity
            /// The orderable count in an order
            OrderCount: Count
            /// The count of doses in an orderable quantity
            DoseCount: Count
            /// The dose of an orderable
            Dose: Dose
            /// The list of components in an orderable
            Components: Component list
        }


    /// Represents start time or start and stop time for an order
    type StartStop =
        | Start of DateTime
        | StartStop of DateTime * DateTime


    /// Models a complete order with all its properties
    type Order =
        {
            /// The id of an order
            Id: Id
            /// Used to adjust doses based on patient parameters
            Adjust: Quantity
            /// The orderable item being prescribed
            Orderable: Orderable
            /// How the Orderable is prescribed
            Schedule: Schedule
            /// The route of administration for the order
            Route: string
            /// The duration of an order
            Duration: Time
            /// The start and optional stop time of the order
            StartStop: StartStop
        }


    /// Type that represents different prescription patterns
    and Schedule =
        | Once
        | OnceTimed of Time
        | Continuous of Time
        /// A discontinuous prescription with a frequency
        | Discontinuous of Frequency
        /// A discontinuous prescription with both frequency and time
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
        // the nth value of one variable, counted from 0
        | SetNthScheduleFrequency of nth: int
        | SetNthScheduleTime of nth: int
        | SetNthOrderableQuantity of nth: int
        | SetNthOrderableDoseQuantity of nth: int
        | SetNthOrderableDoseRate of nth: int
        | SetNthComponentOrderableQuantity of cmp: string * nth: int
        | SetNthComponentDoseQuantityAdjust of cmp: string * nth: int
        | SetNthItemDoseQuantity of cmp: string * itm: string * nth: int
        | SetNthItemDoseQuantityAdjust of cmp: string * itm: string * nth: int
        | SetNthItemDosePerTime of cmp: string * itm: string * nth: int
        | SetNthItemDosePerTimeAdjust of cmp: string * itm: string * nth: int
        | SetNthItemDoseRate of cmp: string * itm: string * nth: int
        | SetNthItemDoseRateAdjust of cmp: string * itm: string * nth: int
        | SetNthItemComponentConcentration of cmp: string * itm: string * nth: int
        | SetNthItemOrderableConcentration of cmp: string * itm: string * nth: int
        | SetNthItemOrderableQuantity of cmp: string * itm: string * nth: int
        // one variable recalculated
        | ReCalcScheduleTime
        | ReCalcOrderableQuantity
        | ReCalcComponentDoseQuantityAdjust of cmp: string
        | ReCalcItemDoseQuantity of cmp: string * itm: string
        | ReCalcItemDoseQuantityAdjust of cmp: string * itm: string
        | ReCalcItemDosePerTime of cmp: string * itm: string
        | ReCalcItemDosePerTimeAdjust of cmp: string * itm: string
        | ReCalcItemDoseRate of cmp: string * itm: string
        | ReCalcItemDoseRateAdjust of cmp: string * itm: string
        | ReCalcItemOrderableConcentration of cmp: string * itm: string
        | ReCalcItemOrderableQuantity of cmp: string * itm: string


    /// The variables of a dose. Total and TotalAdjust belong to Ordering: a recipe sets them back, a
    /// client does not select them.
    [<RequireQualifiedAccess>]
    type DoseProperty =
        | Quantity
        | QuantityAdjust
        | PerTime
        | PerTimeAdjust
        | Rate
        | RateAdjust
        | Total
        | TotalAdjust


    /// One variable of an order, named by its level and property, with the names of its component
    /// and item.
    [<RequireQualifiedAccess>]
    type OrderVariableReference =
        | OrderAdjust
        | ScheduleFrequency
        | ScheduleTime
        | OrderableQuantity
        | OrderableDoseCount
        | OrderableOrderCount
        | OrderDuration
        | OrderableOrderQuantity
        | ComponentOrderQuantity of cmp: string
        | ComponentOrderCount of cmp: string
        | OrderableDose of DoseProperty
        | ComponentQuantity of cmp: string
        | ComponentOrderableQuantity of cmp: string
        | ComponentOrderableCount of cmp: string
        | ComponentOrderableConcentration of cmp: string
        | ComponentDose of cmp: string * DoseProperty
        | ItemComponentQuantity of cmp: string * itm: string
        | ItemComponentConcentration of cmp: string * itm: string
        | ItemOrderableQuantity of cmp: string * itm: string
        | ItemOrderableConcentration of cmp: string * itm: string
        | ItemDose of cmp: string * itm: string * DoseProperty


    /// The increment a step moves by: the one the rules define, or the calculated one, which can be
    /// larger.
    [<RequireQualifiedAccess>]
    type StepIncrement =
        | Defined
        | Calculated


    /// How a value is selected from the range a variable offers.
    [<RequireQualifiedAccess>]
    type Choice =
        | Min
        | Max
        | Median
        /// The nth value, counted from 0.
        | Nth of int
        /// The value at that percentage of the way through the values, from 0 to 100.
        | Percentile of int
        /// The value that many increments up, or down when negative.
        | Step of int * StepIncrement


    /// What the client asks the server to do with an order: the client names the variable, the
    /// server chooses the recipe.
    [<RequireQualifiedAccess>]
    type OrderAction =
        /// Calculate the constraints of an order made from its medication: the defined constraints
        /// applied, solved for min and max, the increments coarsened, the result recorded as the
        /// calculated constraints, and the norm dose set where there is one.
        | CalcConstraints
        /// Select a value for one variable.
        | Set of OrderVariableReference * Choice
        /// Recalculate one variable: set it back with what depends on it, keep the rest.
        | ReCalc of OrderVariableReference
        /// Set every variable back to its calculated constraints.
        | Reset
        /// Calculate the values to select from, as selecting a scenario does with its order.
        | CalcValues


    /// The range an effect sets the current range of a variable back to.
    [<RequireQualifiedAccess>]
    type SetBackTo =
        /// Its calculated constraints.
        | Calculated
        /// Its calculated constraints, else its defined constraints, else any positive value.
        | CalculatedElseDefined
        /// Its defined constraints.
        | Defined
        /// Its defined constraints, at most the current orderable quantity.
        | DefinedUpToOrderable
        /// The min and increment of its constraints, without a max.
        | MinIncr
        /// Any positive value on the defined increment, so the increment is the minimum; any
        /// positive value when there is no defined increment.
        | AnyPositive
        /// The value 1, for a count.
        | One
        /// Any value of 1 or more, for a count.
        | AtLeastOne


    /// Whether an order is solved for its min and max, or for its values.
    [<RequireQualifiedAccess>]
    type SolveMode =
        | MinMax
        | Values


    /// How many values a list made from a min, increment and max may hold: the cap of the dose type,
    /// or its larger cap.
    [<RequireQualifiedAccess>]
    type ValueCap =
        | Default
        | Larger


    /// Whether a list is made for the orderable dose rate too. The rate of a timed order waits until
    /// its dose quantity holds one value.
    [<RequireQualifiedAccess>]
    type RateList =
        | WithRate
        | WithoutRate


    /// What happens to a rate list once it is made: its median selected, or the list kept.
    [<RequireQualifiedAccess>]
    type RateSelection =
        | SelectMedianRate
        | OfferRates


    /// A state of an order an effect can depend on.
    [<RequireQualifiedAccess>]
    type OrderCondition =
        /// Every dose variable holds one value.
        | DoseSolved
        /// Every variable holds one value.
        | OrderSolved
        /// The variables the values are made for hold a value list or one value.
        | HasValues
        /// The dose quantity, or on Continuous the rate, holds a value list or one value, so the norm
        /// dose can be set.
        | CanSetNormDose


    /// One effect on an order. An action is run as a list of effects.
    [<RequireQualifiedAccess>]
    type OrderEffect =
        /// The current range of the variable set back to the target.
        | SetOrderVariableRangeBack of OrderVariableReference * SetBackTo
        /// Every variable the kept variables do not determine set back to the target, except the
        /// ones in stays. The variable a step moves is in stays: not kept, since its value changes,
        /// and not set back, since the step gives it its value.
        | SetBackUndetermined of kept: OrderVariableReference list * stays: OrderVariableReference list * SetBackTo
        /// Every variable of the order set back to the target.
        | SetAllOrderVariableRangesBack of SetBackTo
        /// The current range of the variable narrowed to one value.
        | ConstrainOrderVariableRange of OrderVariableReference * ValueUnit
        /// The current range of the variable narrowed to the value the choice selects from the range
        /// as it is when the effect runs.
        | SelectOrderVariableValue of OrderVariableReference * Choice
        /// The order solved.
        | SolveOrder of SolveMode
        /// The first component quantity, dose quantity or rate that is a min, increment and max made
        /// a list, the order solved, and again until none is left. With an original order, a variable
        /// made a list includes the value it held there when the pruning dropped it and the value
        /// lies within its calculated constraints.
        | EnumerateOrderVariableValues of ValueCap * RateList * original: Order option
        /// The rate of a timed order made a list once its dose quantity holds one value; with an
        /// original order, the rate includes the value it held there, as above.
        | EnumerateRateValues of ValueCap * RateSelection * original: Order option
        /// The increment of the volume quantities made larger, so the orderable quantity holds at
        /// most this many values; not on Continuous.
        | CoarsenIncrements of maxCount: int
        /// The current range of every variable stored as its calculated constraints.
        | RecordCalculatedConstraints
        /// The dose set to the median of its values, and the order solved for min and max.
        | SetNormDose
        /// Each component quantity set to the value nearest above it, else below it, and the order
        /// solved with values.
        | SelectNearestComponentQuantity
        /// The values a search proves impossible dropped from the list of the variable, and the order
        /// solved with values, so the other lists follow; a value a search cannot decide stays. Only
        /// when the dose count holds more than one value, the one case seen to offer values no
        /// complete order reaches.
        | DropImpossibleValues of OrderVariableReference
        /// The effect, run only when none of the conditions holds on the order as it is then.
        | Unless of OrderCondition list * OrderEffect
        /// The effects run in turn; when one fails, the order is kept as it was before the first.
        | Try of OrderEffect list


    /// Why an action or effect stops.
    [<RequireQualifiedAccess>]
    type ActionFailure =
        /// The order has no such variable.
        | NoSuchVariable of OrderVariableReference
        /// The variable cannot be set this way, for the reason given.
        | NotSettable of OrderVariableReference * reason: string
        /// The choice selects no value from the range of the variable.
        | ValueNotInRange of OrderVariableReference
        /// No recipe runs the action on this order.
        | NoRecipe of OrderAction
        /// The solver failed.
        | SolveFailed of Exceptions.Message list
        /// A failure outside the solver, such as an order that cannot be made from its medication.
        | Unexpected of string


    /// The recipes: a step or percentile named by its target, a recalculation by its kind. The kind
    /// Dose recalculates the dose quantity, Per time the dose quantity and the frequency, and Time
    /// the rate.
    [<RequireQualifiedAccess>]
    type Recipe =
        | ScheduleFrequencyStep
        | OrderableDoseQuantityStep
        | OrderableDoseQuantityPercentile
        | OrderableDoseRateStep
        | ComponentOrderableQuantityStep of cmp: string
        | ReCalcDose
        | ReCalcPerTime
        | ReCalcTime


    /// What a set of a variable with a choice does.
    [<RequireQualifiedAccess>]
    type SetCommand =
        /// No command sets the variable this way.
        | NoCommand
        /// The command sets the value without a recipe.
        | ValueOnly
        /// The command runs the recipe before it sets the value.
        | WithRecipe of Recipe


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
            /// Identifies the specific medication order
            Id: string
            /// The name of the order
            Name: string
            /// The list of drug products that can be used for the order
            Components: ProductComponent list
            /// The min max quantity
            Quantity: MinMax
            /// The quantities of the medication order
            Quantities: ValueUnit option
            /// The route by which the order is applied
            Route: string
            /// The type of order
            OrderType: OrderType
            /// The unit to adjust the dose with
            /// AdjustUnit : Unit option
            /// The list of possible frequency values
            Frequencies: ValueUnit option
            /// The min and/or max time for the infusion time
            Time: MinMax
            /// The dose limits for a medication order
            Dose: DoseLimit option
            /// The amount of orderable that will be given each time
            DoseCount: MinMax
            /// Dividability option
            Div: BigRational option
            /// The adjust quantity for the adjusted dose calculations
            Adjust: ValueUnit option
        }

    /// The product components that are used by the medication order.
    /// A product component maps to a Component in an Orderable.
    /// The first component in the list is the main component.
    /// The medication order quantities unit is the same as the unit used
    /// for the main component.
    and ProductComponent =
        {
            /// The name of the product
            Name: string
            /// The pharmaceutical form of the product
            Form: string
            /// The quantities of the product
            /// Note: the first (main) component has the same unit as
            /// the medication order unit
            Quantities: ValueUnit option
            /// The "divisibility" of the products
            Divisible: BigRational option
            /// The dose limits for the product
            Dose: DoseLimit option
            /// The solution limits for a product
            Solution: SolutionLimit option
            /// The list of substances contained in the product
            Substances: SubstanceItem list
            /// The products the component can be made of; empty when none are known
            Products: ComponentProduct list
        }

    /// A substance in a product. A substance maps to an Item in a Component.
    and SubstanceItem =
        {
            /// The name of the substance
            Name: string
            /// Quantities only needed for reconstitution scenarios
            /// to tie a specific reconstitution to a quantity
            Quantities: ValueUnit option
            /// The possible concentrations of the substance
            /// in the products
            Concentrations: ValueUnit option
            /// The dose limits for a substance
            Dose: DoseLimit option
            /// The solution limits for a substance
            Solution: SolutionLimit option
        }


    /// Represents totals for various nutrients and substances
    type Totals =
        {
            /// Total volume
            Volume: string option
            /// Total energy content
            Energy: string option
            /// Total protein content
            Protein: string option
            /// Total carbohydrate content
            Carbohydrate: string option
            /// Total fat content
            Fat: string option
            /// Total sodium content
            Sodium: string option
            /// Total potassium content
            Potassium: string option
            /// Total chloride content
            Chloride: string option
            /// Total calcium content
            Calcium: string option
            /// Total phosphate content
            Phosphate: string option
            /// Total magnesium content
            Magnesium: string option
            /// Total iron content
            Iron: string option
            /// Total vitamin D content
            VitaminD: string option
            /// Total ethanol content
            Ethanol: string option
            /// Total propylene glycol content
            Propyleenglycol: string option
            /// Total benzyl alcohol content
            BenzylAlcohol: string option
            /// Total boric acid content
            BoricAcid: string option
        }


    /// Type alias for DoseType from Informedica.GenForm.Lib.Types
    type DoseType = Informedica.GenForm.Lib.Types.DoseType


    /// Type alias for NormDose from Informedica.GenForm.Lib.Types
    type NormDose = Informedica.GenForm.Lib.Types.NormDose


    /// Type alias for the GenForm product component (formerly Product).
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
            /// The scenario number
            No: int
            /// The name of the order
            Name: string
            /// The indication for the order
            Indication: string
            /// The pharmaceutical form of the order
            Form: string
            /// The route of the order
            Route: string
            /// The dose type of the order
            DoseType: DoseType
            /// An optional diluent
            Diluent: string option
            /// The component name
            Component: string option
            /// The substance name
            Item: string option
            /// The list of diluents to choose from
            Diluents: string[]
            /// The list of components to print out
            Components: string[]
            /// The list of substances to print out
            Items: string[]
            /// The prescription instructions
            Prescription: TextBlock[][]
            /// The preparation instructions
            Preparation: TextBlock[][]
            /// The administration instructions
            Administration: TextBlock[][]
            /// The order itself
            Order: Order
            /// Whether to use adjust calculations
            UseAdjust: bool
            /// Whether to use a renal rule
            UseRenalRule: bool
            /// The renal rule name
            RenalRule: string option
            /// Associated product identifiers
            ProductsIds: string[]
            /// The access device the scenario's solution rule makes the preparation for; AnyAccess
            /// when the rule names none or the scenario has no solution rule. Never the patient's access.
            Access: Informedica.GenForm.Lib.Types.AccessDevice
        }


    /// Filter for selecting order scenarios based on various criteria
    type Filter =
        {
            /// The list of indications to select from
            Indications: string[]
            /// The list of generics to select from
            Generics: string[]
            /// The list of routes to select from
            Routes: string[]
            /// The list of pharmaceutical forms to select from
            Forms: string[]
            /// The possible dose types
            DoseTypes: DoseType[]
            /// The list of diluents to choose from
            Diluents: string[]
            /// The list of components that can be used
            Components: string[]
            /// The selected indication
            Indication: string option
            /// The selected generic
            Generic: string option
            /// The selected route
            Route: string option
            /// The selected pharmaceutical form
            Form: string option
            /// The selected dose type
            DoseType: DoseType option
            /// The diluent to use
            Diluent: string option
            /// The list of components that are selected
            SelectedComponents: string[]
        }


    /// A field of the filter: a choice is the field with the index of its option, a clear is the
    /// field alone.
    [<RequireQualifiedAccess>]
    type FilterField =
        | Indication
        | Generic
        | Route
        | Form
        | DoseType
        | Diluent
        | Components


    /// Where the choices of a filter come from when they are set from outside its own fields.
    [<RequireQualifiedAccess>]
    type SeedSource =
        /// The medication in the url, written over the filter as it is.
        | Url
        /// An item of the emergency or continuous medication list, written over an empty filter.
        | MedicationList
        /// The choices of the formulary page.
        | Formulary
        /// The medication, route and form of the parenteralia page.
        | Parenteralia
        /// The rules reloaded: the filter evaluated again as it is.
        | Reload


    /// <summary>
    /// The main communication object to transfer the
    /// results of the solver to the client. The Filter
    /// is used to select the correct scenario.
    /// </summary>
    type OrderContext =
        {
            /// The filter for selecting scenarios
            Filter: Filter
            /// The patient information
            Patient: Patient
            /// The list of available scenarios
            Scenarios: OrderScenario[]
            /// What the clinician wrote when the dose left what the rules allow; none until
            /// written. Kept as written: the client normalises it, the server caps its length.
            Argumentation: string option
        }


    /// Why an evaluation is refused: the picks of the filter match no dose rule, or the order
    /// cannot be solved with a change to it.
    [<RequireQualifiedAccess>]
    type Refusal =
        /// No dose rule exists for the picks at all.
        | NoDoseRules
        /// Dose rules exist for the picks, and none of them covers this patient.
        | NoDoseRulesForPatient
        /// Dose rules cover the picks and the patient, and none of them can be prescribed: no
        /// product, or no dose type.
        | NoProducts
        /// The order cannot be solved with the change; the order stays as it was.
        | NotSolved


    /// What an evaluation answers when it does not fail: the value evaluated, or the value as
    /// it was sent together with the reason it could not be evaluated.
    type Outcome<'a> =
        /// The value evaluated.
        | Evaluated of 'a
        /// The value as it was sent, and why it could not be evaluated.
        | Refused of 'a * Refusal


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
            /// The context's id in the order plan
            Id: string
            /// A drug, or the nutrition category the context holds
            Category: OrderCategory
            /// The order context, evaluated within its own patient
            Context: OrderContext
            /// The totals over the one scenario the context is narrowed to
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
            /// The patient data as shown, the data a version is signed on
            Patient: Patient
            /// The ids of the contexts the row filter keeps; empty for all of them
            Filtered: string[]
            /// Every order context of the plan, drug and nutrition alike
            Contexts: PlanContext[]
            /// The totals over the orders of the filtered contexts
            Totals: Totals
        }


    /// What a nutrition category draws from: data passed in by the composition
    /// root, never a constant in this library.
    type NutritionRuleSet =
        {
            /// The category the set serves
            Category: NutritionCategory
            /// The label shown for the category
            Label: string
            /// The indications the category's dose rules are filtered on
            Indications: string[]
            /// The generics the category's dose rules are filtered on
            Generics: string[]
        }


    /// Who signed an order plan version
    type Signer =
        {
            /// The user id at the identity provider
            UserId: string
            /// The name shown for the signer
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
            /// The version's own id
            Id: string
            /// The version number, one above the patient's previous version
            No: int
            /// The patient the plan belongs to
            PatientId: string
            /// The id of the version this one was built on; None for the first
            Base: string option
            /// Who signed
            SignedBy: Signer
            /// When
            SignedAt: DateTime
            /// The order plan as signed
            Plan: OrderPlan
            /// Whether the patient data was verified against the platform's reading
            /// at the challenge
            Verified: bool
        }


    /// Why an order plan refuses a change. The words shown for each are the server's, so
    /// that the label of a category stays out of the domain.
    [<RequireQualifiedAccess>]
    type OrderPlanError =
        /// The plan holds no context with the id
        | NoSuchContext of id: string
        /// The workbench holds the number of candidates, not one order
        | NotNarrowed of candidates: int
        /// The plan already holds the order
        | OrderHeld of orderId: string
        /// A supplement needs a feeding in the plan
        | SupplementNeedsFeeding
        /// The plan already holds a context of the category
        | CategoryHeld of NutritionCategory


    /// Why a Dto does not parse to its domain value.
    [<RequireQualifiedAccess>]
    type DtoError =
        /// A dose type string the Dto carries that names no dose type
        | UnknownDoseType of string
        /// A text block kind the Dto carries that names no kind
        | UnknownTextKind of string
        /// The order of a scenario could not be created from its Dto
        | OrderNotCreated of string
        /// An order category string the Dto carries that names no category
        | UnknownCategory of string
        /// A nested Dto a serializer left null, by field name
        | Missing of string
        /// The patient of a context or an order plan is none
        | Patient of Informedica.GenForm.Lib.Types.PatientError
        /// A text that is not a document of its shape: no YAML, a wrong type or an unknown key,
        /// with the reader's message, its line and its column
        | Unreadable of message: string
        /// A number that is neither a decimal nor a fraction, by the path of its field
        | NotANumber of path: string * number: string
        /// A unit that does not parse, by the path of its field
        | UnknownUnit of path: string * unit: string
        /// A unit that does not fit its field, as an adjusted limit without an adjust unit, by the
        /// path of its field
        | WrongUnit of path: string * reason: string
        /// An order type that names no case of OrderType, by the path of its field
        | UnknownOrderType of path: string * orderType: string
        /// A product a strength of which names no substance of its component, or a substance
        /// without concentrations in the strength's unit group, by the path of the product
        | NotMadeOf of path: string * productId: string


    module Exceptions =

        /// Messages for order-related exceptions
        type Message =
            | OrderCouldNotBeCreated of Exception
            | OrderCouldNotBeSolved of string * Order


    module Events =

        /// Events that can occur during order processing
        type Event =
            | MinIncrMaxToValues of OrderVariable
            | MedicationCreated of Medication
            | SolverReplaceUnit of (Name * Unit)
            | OrderIncreaseQuantityIncrement of Order
            | OrderSolveStarted of Order
            | OrderSolveFinished of Order
            | OrderSolveConstraintsStarted of Order * Constraint list
            | OrderSolveConstraintsFinished of Order * Constraint list
            | OrderScenario of string
            | OrderScenarioWithNameValue of Order * Name * BigRational
            | GenFormErrors of Informedica.GenForm.Lib.Types.Message list


    module Logging =


        open Informedica.Logging.Lib


        /// Messages for order-related logging
        type OrderMessage =
            | OrderException of Exceptions.Message
            | OrderEventMessage of Events.Event

            interface IMessage
