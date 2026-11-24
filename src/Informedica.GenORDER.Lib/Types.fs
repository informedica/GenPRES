namespace Informedica.GenOrder.Lib


[<AutoOpen>]
module Types =


    open System
    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib.Types


    /// Type alias for Gender from Informedica.GenForm.Lib.Types
    type Gender = Informedica.GenForm.Lib.Types.Gender
    /// Type alias for PatientData from Informedica.GenForm.Lib.Types
    type PatientData = Informedica.GenForm.Lib.Types.PatientData
    /// Type alias for PatientDataError from Informedica.GenCore.Lib.Patients
    type PatientDataError = Informedica.GenCore.Lib.Patients.PatientDataError
    /// Type alias for PrescriptionRule from Informedica.GenForm.Lib.Types
    type PrescriptionRule = Informedica.GenForm.Lib.Types.PrescriptionRule
    /// Type alias for ComponentSelection from Informedica.GenForm.Lib.Types
    type ComponentSelection = Informedica.GenForm.Lib.Types.ComponentSelection


    /// <summary>
    /// An OrderVariable represents the combination
    /// of a Variable and Constraints
    /// </summary>
    type OrderVariable =
        {
            /// The constraints set from the rules and the products. Only a patient data update changes
            /// them, to the constraints and the adjust value of the order built again from the current rule.
            DefinedConstraints: Constraints
            /// The constraints calculated from the defined constraints by solving the order.
            CalculatedConstraints: Constraints
            /// The current values or range of the variable
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
        /// A single value with an increment in its defined constraints, so the value can be stepped.
        | Steppable
        /// The variable has no value to set.
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
            /// The quantity of an Item in an Orderable per adjust unit of the order
            OrderableQuantityAdjust: QuantityAdjust
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


    /// The reconstitution a product was made with: the solvent and its volumes.
    type ComponentReconstitution =
        {
            /// The name of the solvent the product is dissolved in.
            Solvent: string
            /// The volume of the solvent.
            SolventVolume: ValueUnit
            /// The volume the dissolved product adds to the solvent volume, when known.
            ExpansionVolume: ValueUnit option
        }


    /// The predilution a product was made with: a product volume diluted with a volume of a diluent.
    type ComponentPredilution =
        {
            /// The volume of the product that is diluted.
            MedVolume: ValueUnit
            /// The volume of the diluent that the product volume is diluted with.
            DiluentVolume: ValueUnit
            /// The name of the diluent.
            Diluent: string
        }


    /// A product a component can be made of, with the strength of each of its substances.
    type ComponentProduct =
        {
            /// The id of the product: a GPK, PRK or HPK
            Id: string
            /// The quantities of the product: 1 mL for a liquid that needs no reconstitution, otherwise
            /// the quantities of the product itself
            FormQuantities: ValueUnit option
            /// The strength of each substance of the product; a substance without a concentration
            /// has no strength here
            Strengths: ProductStrength list
            /// The reconstitution the product was made with; none for a product that is not reconstituted.
            Reconstitution: ComponentReconstitution option
            /// The predilution the product was made with; none for a product that is not pre-diluted.
            Predilution: ComponentPredilution option
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
            /// The quantity of a Component in an Orderable per adjust unit of the order
            OrderableQuantityAdjust: QuantityAdjust
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
            /// The quantity of an orderable per adjust unit of the order
            OrderableQuantityAdjust: QuantityAdjust
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
    /// Type alias for DilutionLimit from Informedica.GenForm.Lib.Types
    type DilutionLimit = Informedica.GenForm.Lib.Types.DilutionLimit


    /// Commands that can be executed on an order for calculations
    type OrderCommand =
        | CalcMinMax of Order
        | IncreaseIncrements of Order
        | CalcValues of Order
        | ReCalcValues of Order
        /// Recalculates the order for a new adjust value, such as a new weight.
        | Readjust of Order * ValueUnit
        | SolveOrder of Order
        /// Sets each named component to the given quantity in the orderable, where the order allows it.
        | SetComponentQuantities of Order * (string * ValueUnit) list
        | ChangeProperty of Order * ChangePropertyCommand

    /// Changes an order property. A decrease or increase steps by the increment constraint and can go
    /// below the min or above the max. Min and max set the calculated min or max. Median sets the median
    /// of the calculated values, or of the increment between the min and the max.
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
        // Set the nth value of one variable, counted from 0.
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
        // Recalculate one variable.
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


    /// The variables of a dose. Total and TotalAdjust are only set back by a recipe, never selected.
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
        | OrderableQuantityAdjust
        | OrderableDoseCount
        | OrderableOrderCount
        | OrderDuration
        | OrderableOrderQuantity
        | ComponentOrderQuantity of cmp: string
        | ComponentOrderCount of cmp: string
        | OrderableDose of DoseProperty
        | ComponentQuantity of cmp: string
        | ComponentOrderableQuantity of cmp: string
        | ComponentOrderableQuantityAdjust of cmp: string
        | ComponentOrderableCount of cmp: string
        | ComponentOrderableConcentration of cmp: string
        | ComponentDose of cmp: string * DoseProperty
        | ItemComponentQuantity of cmp: string * itm: string
        | ItemComponentConcentration of cmp: string * itm: string
        | ItemOrderableQuantity of cmp: string * itm: string
        | ItemOrderableQuantityAdjust of cmp: string * itm: string
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
        /// The value at the given percentage through the values, from 0 to 100.
        | Percentile of int
        /// The value the given number of increments up, or down when the number is negative.
        | Step of int * StepIncrement


    /// An action on an order. The action names the variable; the recipe that runs it is chosen per action.
    [<RequireQualifiedAccess>]
    type OrderAction =
        /// Calculates the constraints of an order made from its medication: applies the defined constraints,
        /// solves for min and max, coarsens the increments, stores the result as the calculated constraints
        /// and sets the norm dose when there is one. A timed or once timed order with three or more components
        /// is then preset to the median of each component quantity and of the rate.
        | CalcConstraints
        /// Select a value for one variable.
        | Set of OrderVariableReference * Choice
        /// Recalculates one variable: sets it and the variables that depend on it back, and leaves the rest.
        | ReCalc of OrderVariableReference
        /// Set every variable back to its calculated constraints.
        | Reset
        /// Calculates the values to select from, as for the order of a selected scenario.
        | CalcValues
        /// Recalculates the order for its current adjust value. The dose the patient gets stays the same;
        /// the calculated constraints and the per kg or per m2 values are calculated again.
        | Readjust
        /// Set each named component to its quantity in the orderable, where the order allows it.
        | SetComponentQuantities of (string * ValueUnit) list


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
        /// Any positive value without an increment, for a value that follows from the others and may
        /// fall outside its constraints.
        | PositiveOffIncrement
        /// The value 1, for a count.
        | One
        /// Any value of 1 or more, for a count.
        | AtLeastOne


    /// Whether an order is solved for its min and max, or for its values.
    [<RequireQualifiedAccess>]
    type SolveMode =
        | MinMax
        | Values


    /// The maximum number of values in a list made from a min, increment and max: the default for the
    /// dose type, or the larger one.
    [<RequireQualifiedAccess>]
    type ValueCap =
        | Default
        | Larger


    /// Whether a list is made for the orderable dose rate too. A timed order gets a rate list only when
    /// its dose quantity has a single value.
    [<RequireQualifiedAccess>]
    type RateList =
        | WithRate
        | WithoutRate


    /// Whether the median of a rate list is selected, or the whole list is offered.
    [<RequireQualifiedAccess>]
    type RateSelection =
        | SelectMedianRate
        | OfferRates


    /// A state of an order an effect can depend on.
    [<RequireQualifiedAccess>]
    type OrderCondition =
        /// Every dose variable has a single value.
        | DoseSolved
        /// Every variable has a single value.
        | OrderSolved
        /// Each variable that values are made for has a value list or a single value.
        | HasValues
        /// The dose quantity, or the rate of a continuous order, has a value list or a single value, so
        /// the norm dose can be set.
        | CanSetNormDose
        /// The dose quantity is a range or a value list, not a single value.
        | DoseQuantityNotSolved


    /// One effect on an order. An action is run as a list of effects.
    [<RequireQualifiedAccess>]
    type OrderEffect =
        /// Sets the current range of the variable back to the target.
        | SetOrderVariableRangeBack of OrderVariableReference * SetBackTo
        /// Sets back to the target every variable that the variables in kept do not determine, except
        /// the variables in stays. A step puts the variable it moves in stays: its value changes and the
        /// step sets that value.
        | SetBackUndetermined of kept: OrderVariableReference list * stays: OrderVariableReference list * SetBackTo
        /// Sets every variable of the order back to the target.
        | SetAllOrderVariableRangesBack of SetBackTo
        /// Narrows the current range of the variable to a single value.
        | ConstrainOrderVariableRange of OrderVariableReference * ValueUnit
        /// Narrows the current range of the variable to the value the choice selects from the range at
        /// the time the effect runs.
        | SelectOrderVariableValue of OrderVariableReference * Choice
        /// Solves the order.
        | SolveOrder of SolveMode
        /// Turns the first component quantity, dose quantity or rate that is a min, increment and max into
        /// a list of values and solves the order, until none is left. With an original order, the list
        /// also has the value of the variable in that order, when the pruning dropped that value and it
        /// lies within the calculated constraints.
        | EnumerateOrderVariableValues of ValueCap * RateList * original: Order option
        /// Turns the rate of a timed order into a list once its dose quantity has a single value. With an
        /// original order, the list also has the rate of that order, as for EnumerateOrderVariableValues.
        | EnumerateRateValues of ValueCap * RateSelection * original: Order option
        /// Makes the increment of the volume quantities larger, so the orderable quantity has at most
        /// maxCount values. Does not apply to a continuous order.
        | CoarsenIncrements of maxCount: int
        /// Stores the current range of every variable as its calculated constraints.
        | RecordCalculatedConstraints
        /// Sets the dose to the median of its values and solves the order for min and max.
        | SetNormDose
        /// Sets each component quantity to the nearest value above it, else below it, and solves the order
        /// with values.
        | SelectNearestComponentQuantity
        /// Drops the values a search proves impossible from the list of the variable and solves the order
        /// with values, so the other lists follow. A value the search cannot decide stays. Runs only when
        /// the dose count has more than one value, the only case found to offer values that no complete
        /// order reaches.
        | DropImpossibleValues of OrderVariableReference
        /// Runs the effect only when none of the conditions holds on the order at that time.
        | Unless of OrderCondition list * OrderEffect
        /// Runs the effects in turn. When one fails, the order is returned as it was before the first effect.
        | Try of OrderEffect list
        /// Calculates the calculated constraints from the defined constraints by running the effects on a
        /// copy of the order, so the values of the order stay. When the effects fail, the calculated
        /// constraints stay as they are.
        | RecalculateConstraints of OrderEffect list


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


    /// The recipes. A step or percentile recipe is named after its target variable. ReCalcDose recalculates
    /// the dose quantity, ReCalcPerTime the dose quantity and the frequency, and ReCalcTime the rate.
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


    /// What setting a variable with a choice runs.
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


    /// A medication order made from the products and the matching dose rule. It maps to the Orderable
    /// and the Schedule of an Order.
    type Medication =
        {
            /// Identifies the specific medication order
            Id: string
            /// The name of the order
            Name: string
            /// The product components that can be used for the order
            Components: ProductComponent list
            /// The min max quantity
            Quantity: MinMax
            /// The minimum and maximum quantity of the orderable per adjust unit of the order
            QuantityAdjust: MinMax
            /// The quantities of the medication order
            Quantities: ValueUnit option
            /// The route by which the order is applied
            Route: string
            /// The type of order
            OrderType: OrderType
            /// The list of possible frequency values
            Frequencies: ValueUnit option
            /// The min and max of the infusion time
            Time: MinMax
            /// The dose limits for a medication order
            Dose: DoseLimit option
            /// The amount of orderable that will be given each time
            DoseCount: MinMax
            /// The number of equal parts one unit of the orderable can be divided into, set by a
            /// solution rule. The step of the orderable is 1 divided by this value, in the unit of
            /// the orderable, and it replaces the Divisible of the components.
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
            /// The number of equal parts one unit of the product can be divided into, or the
            /// number of drops per mL for a droplet form, so the step is 1 divided by this value
            /// in the unit of the component. For a droplet form that step is one drop. A rule is
            /// split so that the products of a component share one value. No value means the
            /// component gives no step.
            Divisible: BigRational option
            /// The dose limits for the product
            Dose: DoseLimit option
            /// The dilution limit of the component, when a solution rule limits it
            Dilution: DilutionLimit option
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
            /// The dilution limit of the substance, when a solution rule limits it
            Dilution: DilutionLimit option
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


    /// Type alias for ProductComponent from Informedica.GenForm.Lib.Types
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
            /// The label of the generic of the order, as in the dose rules
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
            /// The additives of the order, the components after the first. An additive the selection left
            /// out is not in this list; Filter.Components lists every option.
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
            /// Whether the order only fits its rule without the Divisible of its products
            FitsOnlyWithoutDivisible: bool
            /// Whether the current patient data select no rules, or other rules, for the filter choices of
            /// the order than the patient data of its context
            RulesNoLongerApply: bool
            /// Whether the order could not be calculated for the current patient data and shows the
            /// calculation for the previous patient data; the prescriber accepts or removes the order
            NotRecalculated: bool
            /// Associated product identifiers
            ProductsIds: string[]
            /// The access device the solution rule of the scenario prepares for; AnyAccess when the rule
            /// names none or there is no solution rule. This is never the access device of the patient.
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
            /// The optional components the prescriber can select or leave out
            Components: string[]
            /// Every component of the dose rule in rule order, the main component first; set from the first
            /// scenario, before a selection can leave out an additive
            DoseRuleComponents: string[]
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
            /// Which optional components the prescriber selected, among the options in Components
            SelectedComponents: ComponentSelection
            /// The products of the selected scenario. A rule is split per Divisible, and only these products
            /// tell the split rules apart.
            PickedProducts: string[]
        }


    /// A field of the filter. A choice names the field with the index of an option; a clear names only
    /// the field.
    [<RequireQualifiedAccess>]
    type FilterField =
        | Indication
        | Generic
        | Route
        | Form
        | DoseType
        | Diluent
        | Components


    /// The source of filter choices that are not set through the fields of the filter.
    [<RequireQualifiedAccess>]
    type SeedSource =
        /// The medication in the URL, written over the current filter.
        | Url
        /// An item of the emergency or continuous medication list, written over an empty filter.
        | MedicationList
        /// The choices made in the formulary.
        | Formulary
        /// The medication, route and form chosen in the parenteralia.
        | Parenteralia
        /// A reload of the rules; the current filter is evaluated again.
        | Reload


    /// <summary>
    /// The filter, the patient data and the scenarios calculated for them. The filter selects the scenario.
    /// </summary>
    type OrderContext =
        {
            /// The filter for selecting scenarios
            Filter: Filter
            /// The patient data
            PatientData: PatientData
            /// The list of available scenarios
            Scenarios: OrderScenario[]
            /// The reason the prescriber gives for a dose outside what the rules allow; None when no reason
            /// is given. Stored as written: the client normalises the text and the server limits its length.
            Argumentation: string option
        }


    /// Why an evaluation is refused.
    [<RequireQualifiedAccess>]
    type Refusal =
        /// No dose rule exists for the filter choices.
        | NoDoseRules
        /// Dose rules exist for the filter choices, and none of them covers this patient.
        | NoDoseRulesForPatient
        /// Dose rules cover the filter choices and the patient, and none of them can be prescribed: no
        /// product, or no dose type.
        | NoProducts
        /// Dose rules cover the filter choices and the patient, and each needs a weight, or a weight and
        /// a height, the patient does not have.
        | MeasurementMissing
        /// Dose rules cover the filter choices and the patient, and each medication is given undiluted
        /// while its concentration limit allows none of its product concentrations.
        | NoDiluent
        /// The order cannot be solved with the change; the order stays as it was.
        | NotSolved
        /// The change sets or recalculates a dose per kg or per m2, and the order has none: no
        /// adjusted dose of the order has a limit. The order stays as it was.
        | NoAdjustedDose


    /// What an evaluation answers when it does not fail: the value evaluated, or the value as
    /// it was sent together with the reason it could not be evaluated.
    type Outcome<'a> =
        /// The value evaluated.
        | Evaluated of 'a
        /// The value as it was sent, and why it could not be evaluated.
        | Refused of 'a * Refusal


    /// Type alias for NutritionCategory from Informedica.GenForm.Lib.Types
    type NutritionCategory = Informedica.GenForm.Lib.Types.NutritionCategory


    /// The category of the order in an order context: medication, or a nutrition category. It is stored
    /// on the context, not derived from the generic, because electrolytes and glucose can also be
    /// prescribed as medication. An order plan has one context per nutrition category, except for any
    /// number of supplements, each under an enteral feeding, and any number of electrolyte and glucose orders.
    [<RequireQualifiedAccess>]
    type OrderCategory =
        | Medication
        | Nutrition of NutritionCategory


    /// An order context narrowed to one scenario, with the texts a list shows for it.
    type StandardOrder =
        {
            /// The order context, narrowed to the scenario
            Context: OrderContext
            /// The indication without the prefix, such as "Noodlijst "
            Category: string
            /// The generic of the context, else the name of the scenario
            Generic: string
            /// The dose of the substance, with the dose per kg in brackets for an order per kg
            SubstanceDose: string
            /// The dose of the orderable with the concentration of the substance in it
            Preparation: string
        }


    /// Why a rule of a rule set gives no standard orders.
    [<RequireQualifiedAccess>]
    type StandardOrderWarning =
        /// The evaluation gives no scenarios and leaves no choice open
        | NoScenarios of indication: string * generic: string
        /// The evaluation gives no scenarios after four choices
        | TooManyChoices of indication: string * generic: string
        /// The rules refuse the order context
        | Refused of indication: string * generic: string * Refusal
        /// The evaluation failed with the messages
        | Failed of indication: string * generic: string * messages: string


    /// <summary>
    /// An order context in an order plan, with its id, its category, its totals and the link to its
    /// main order. The plan data wrap the context, so evaluation and the equation system never see them.
    /// </summary>
    /// <remarks>
    /// The totals are calculated when the context is evaluated and copied unchanged otherwise, so a
    /// stored plan has the totals the prescriber saw.
    /// </remarks>
    type PlanContext =
        {
            /// The id of the context in the order plan
            Id: string
            /// Medication, or the nutrition category of the order
            Category: OrderCategory
            /// The order context, evaluated with its own patient data
            Context: OrderContext
            /// The totals of the single scenario the context is narrowed to
            Totals: Totals
            /// The id of the plan context with the main order of this order; None when the order is not
            /// linked
            LinkedTo: string option
        }


    /// <summary>
    /// The order plan of a patient. The orders of the plan are the scenarios of its narrowed contexts
    /// and are not stored apart from the contexts.
    /// </summary>
    type OrderPlan =
        {
            /// The patient data the plan is calculated with
            PatientData: PatientData
            /// The ids of the contexts the filter selects; empty when it selects all contexts
            Filtered: string[]
            /// Every order context of the plan, medication and nutrition
            Contexts: PlanContext[]
            /// The totals over the orders of the filtered contexts
            Totals: Totals
        }


    /// Why an order plan refuses the link of a context to its main order.
    [<RequireQualifiedAccess>]
    type LinkError =
        /// The plan holds no context with the id of the main order
        | NoSuchMainOrder of id: string
        /// The main order with the id is itself linked to another order
        | MainOrderLinked of id: string
        /// No link rule links the order with the given generic to the main order
        | NotAllowed of generic: string
        /// A link rule requires a main order for the order, and the order is not linked
        | MainOrderRequired


    /// Why an order plan refuses a change. The server writes the message for each case, so the label of
    /// a category stays out of the domain.
    [<RequireQualifiedAccess>]
    type OrderPlanError =
        /// The plan holds no context with the id
        | NoSuchContext of id: string
        /// The order context has the given number of candidate scenarios, not a single one
        | NotNarrowed of candidates: int
        /// The plan already has the order
        | OrderHeld of orderId: string
        /// No rule set exists for the category
        | NoRuleSet of NutritionCategory
        /// The plan refuses the link of the context to its main order, for the given reason
        | BrokenLink of LinkError
        /// The plan refuses one order of a group add, named by its generic, for the reason given
        | OrderRefused of generic: string * OrderPlanError


    /// Why a Dto does not parse to its domain value.
    [<RequireQualifiedAccess>]
    type DtoError =
        /// A dose type string that names no dose type
        | UnknownDoseType of string
        /// A text block kind string that names no text block kind
        | UnknownTextKind of string
        /// The order of a scenario could not be created from its Dto
        | OrderNotCreated of string
        /// An order category string that names no order category
        | UnknownCategory of string
        /// A Dto that is null after JSON deserialization, with the name of the field or the path of
        /// the array item
        | Missing of string
        /// The patient data has an invalid value. The string is the error message of GenCORE.
        | PatientData of string
        /// The patient data is not enough to select rules.
        | PatientDataNotEnough of PatientDataError
        /// A text that is not a valid document of the expected shape: not YAML, a wrong type or an
        /// unknown key. The message is the reader error with its line and column.
        | Unreadable of message: string
        /// A number that is neither a decimal nor a fraction, with the path of its field
        | NotANumber of path: string * number: string
        /// A unit that does not parse, with the path of its field
        | UnknownUnit of path: string * unit: string
        /// A unit that does not fit its field, such as an adjusted limit without an adjust unit, with
        /// the path of its field
        | WrongUnit of path: string * reason: string
        /// An order type that names no case of OrderType, with the path of its field
        | UnknownOrderType of path: string * orderType: string
        /// A product with a strength that names no substance of its component, or a substance without
        /// concentrations in the unit group of the strength, with the path of the product
        | NotMadeOf of path: string * productId: string
        /// The plan context with the id has a link the plan refuses, for the given reason
        | BrokenLink of contextId: string * LinkError


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
