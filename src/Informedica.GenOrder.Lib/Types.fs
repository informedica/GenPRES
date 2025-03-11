namespace Informedica.GenOrder.Lib



[<AutoOpen>]
module Types =


        open System
        open Informedica.Utils.Lib.Csv
        open MathNet.Numerics

        open Informedica.GenUnits.Lib
        open Informedica.GenSolver.Lib.Types


        type Gender = Informedica.GenForm.Lib.Types.Gender
        type Patient = Informedica.GenForm.Lib.Types.Patient


        /// <summary>
        /// An OrderVariable represents the combination
        /// of a Variable and Constraints
        /// </summary>
        type OrderVariable =
            {
                /// The Constraints to apply to the Variable
                Constraints : Constraints
                /// Stores the values/range
                Variable:  Variable

            }
        and Constraints =
            {
                    Min : Minimum option
                    Max : Maximum option
                    Incr : Increment option
                    Values : ValueSet option
            }


        /// <summary>
        /// An order equation is either a product equation or a
        /// sum equation
        /// </summary>
        type OrderEquation =
            | OrderProductEquation of OrderVariable * OrderVariable list
            | OrderSumEquation of OrderVariable * OrderVariable list


        /// Type that represents a time
        type Time = Time of OrderVariable


        /// Type that represents a count
        type Count = Count of OrderVariable


        /// Type that represents a frequency
        type Frequency = Frequency of OrderVariable


        /// Type that represents a quantity
        type Quantity = Quantity of OrderVariable


        /// Type that represents a quantity per time
        type PerTime = PerTime of OrderVariable

        /// Type that represents a rate
        type Rate = Rate of OrderVariable


        /// Type that represents a total
        type Total = Total of OrderVariable


        /// Type that represents a concentration
        type Concentration = Concentration of OrderVariable


        /// Type that represents a adjusted quantity
        type QuantityAdjust = QuantityAdjust of OrderVariable


        /// Type that represents a adjusted quantity per time
        type PerTimeAdjust = PerTimeAdjust of OrderVariable


        /// Type that represents a adjusted quantity per time
        type RateAdjust = RateAdjust of OrderVariable


        /// Type that represents a adjusted total
        type TotalAdjust = TotalAdjust of OrderVariable


        /// An Id is represented by a string
        type Id = Id of string


        /// <summary>
        /// Represents a Dose
        /// </summary>
        type Dose =
            {
                Quantity : Quantity
                PerTime : PerTime
                Rate : Rate
                Total : Total
                QuantityAdjust : QuantityAdjust
                PerTimeAdjust : PerTimeAdjust
                RateAdjust : RateAdjust
                TotalAdjust : TotalAdjust
            }


        /// Models an `Item` in a `Component`
        type Item =
            {
                /// The name of the item
                Name: Name
                /// The quantity of an `Item` in a `Component`
                ComponentQuantity: Quantity
                /// The quantity of an `Item` in an `Orderable`
                OrderableQuantity: Quantity
                /// The `Item` concentration in a `Component`
                ComponentConcentration: Concentration
                /// The  `Item` concentration in an `Orderable`
                OrderableConcentration: Concentration
                /// The `Item` `Dose` of `Item` administered
                Dose: Dose
            }


        /// Models in a `Component` in and `Orderable`
        type Component =
            {
                Id : Id
                /// The name of a `Component`
                Name: Name
                // The shape of an component
                Shape : string
                /// The quantity of a `Component`
                ComponentQuantity: Quantity
                /// The quantity of a `Component` in an `Orderable`
                OrderableQuantity: Quantity
                /// The count of a `Component` in an `Orderable`
                OrderableCount: Count
                /// The quantity of a `Component` in an `Order`
                OrderQuantity: Quantity
                /// The count of a `Component` in an `Order`
                OrderCount: Count
                /// The concentration of a `Component` in an `Orderable`
                OrderableConcentration: Concentration
                // The `Component` `Dose` of `Component` administered
                Dose: Dose
                /// The `Item`s in a `Component`
                Items: Item list
            }


        /// Models an `Orderable`
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
                // The count of doses in an orderable quantity
                DoseCount: Count
                /// The dose of an orderable
                Dose: Dose
                /// The list of components in an orderable
                Components: Component list
            }


        /// There is always a `Start` or
        /// both a `StartStop`
        type StartStop =
            | Start of DateTime
            | StartStop of DateTime * DateTime


        /// Models an order
        type Order =
            {
                /// The id of an order
                Id: Id
                /// Used to adjust doses
                Adjust: Quantity
                /// That what can be ordered
                Orderable: Orderable
                /// How the Orderable is prescribed
                Prescription: Prescription
                /// The route of administration of the order
                Route: string // Route
                /// The duration of an order
                Duration: Time
                /// The start stop date of the order
                StartStop: StartStop
            }


        /// Type that represents a prescription
        and Prescription =
            | Once
            | OnceTimed of Time
            | Continuous
            /// A discontinuous prescription with a frequency
            | Discontinuous of Frequency
            /// A discontinuous prescription with both frequency and time
            | Timed of Frequency * Time


        /// A string list that represents either a product or sum equation
        type EquationMapping =
            | ProductMapping of string list
            | SumMapping of string list


        /// The different possible order types
        type OrderType =
            | AnyOrder
            | ProcessOrder
            | OnceOrder
            | OnceTimedOrder
            | ContinuousOrder
            | DiscontinuousOrder
            | TimedOrder


        /// Shorthand for a Informedica.GenForm.Lib.Types.MinMax
        type MinMax = Informedica.GenForm.Lib.Types.MinMax
        /// Shorthand for a Informedica.GenForm.Lib.Types.DoseLimit
        type DoseLimit = Informedica.GenForm.Lib.Types.DoseLimit
        /// Shorthand for a Informedica.GenForm.Lib.Types.SolutionLimit
        type SolutionLimit = Informedica.GenForm.Lib.Types.SolutionLimit


        /// The representation of a drug order that
        /// can be derived by a drug product inventory
        /// and the related dose rule. A DrugOrder maps
        /// to an Orderable and a Prescription.
        type DrugOrder =
            {
                /// Identifies the specific drug order
                Id:  string
                /// The name of the order
                Name : string
                /// The list of drug products that can be used for the order
                Products : ProductComponent list
                /// The quantities of the drug order
                Quantities :  ValueUnit option
                /// The route by which the order is applied
                Route : string
                /// The type of order
                OrderType : OrderType
                /// The unit to adjust the dose with
                AdjustUnit : Unit option
                /// The list of possible frequency values
                Frequencies : ValueUnit option
                /// The list of possible rate values
                Rates : ValueUnit option
                /// The min and/or max time for the infusion time
                Time : MinMax
                /// The dose limits for an DrugOrder
                Dose : DoseLimit option
                /// The amount of orderable that will be given each time
                DoseCount : ValueUnit option
                /// The adjust quantity for the adjusted dose calculations
                Adjust : ValueUnit option
            }
        /// The product components that are used by the drug order.
        /// A product component maps to a Component in an Orderable.
        and ProductComponent =
            {
                /// The name of the product
                Name : string
                /// The shape of the product
                Shape : string
                /// The quantities of the product
                /// Note: measured in the same unit as
                /// the `DrugOrder` unit
                Quantities : ValueUnit option
                /// The "divisibility" of the products
                Divisible : BigRational option
                Dose : DoseLimit option
                /// The solution limits for a product
                Solution : SolutionLimit option
                /// The list of substances contained in the product
                Substances: SubstanceItem list
            }
        /// A substance in a product. A substance maps to an Item in a Component.
        and SubstanceItem =
            {
                /// The name of the substance
                Name : string
                /// The possible concentrations of the substance
                /// in the products
                Concentrations : ValueUnit option
                /// The dose limits for a substance
                Dose : DoseLimit option
                /// The solution limits for a solution
                Solution : SolutionLimit option
            }


        type Intake =
            {
                Volume : string option
                Energy : string option
                Protein : string option
                Carbohydrate : string option
                Fat : string option
                Sodium : string option
                Potassium : string option
                Chloride : string option
                Calcium : string option
                Phosphate : string option
                Magnesium : string option
                Iron : string option
                VitaminD : string option
                Ethanol : string option
                Propyleenglycol : string option
                BenzylAlcohol : string option
                BoricAcid : string option
            }


        type DoseType = Informedica.GenForm.Lib.Types.DoseType


        type NormDose = Informedica.GenForm.Lib.Types.NormDose


        /// <summary>
        /// The representation of an order with a
        /// <list type="bullet">
        /// <item>Prescription</item>
        /// <item>Preparation</item>
        /// <item>Administration</item>
        /// </list>
        ///
        /// </summary>
        type OrderScenario =
            {
                No : int
                /// the indication for the order
                Indication : string
                /// the dose type of the order
                DoseType : DoseType
                /// the name of the order
                Name : string
                Diluents : string []
                // The list of components to print out
                Components : string []
                // The list of substances to print out
                Items : string []
                /// the shape of the order
                Shape : string
                /// the route of the order
                Route : string
                /// The diluent
                Diluent : string option
                Component : string option
                Item : string option
                /// the prescription of the order
                Prescription : string[][]
                /// the preparation of the order
                Preparation : string[][]
                /// the administration of the order
                Administration : string[][]
                /// the order itself
                Order : Order
                /// Whether to us adjust
                UseAdjust : bool
                /// Whether to use a renal rule
                UseRenalRule : bool
                /// Renal rule name
                RenalRule : string option
            }


        type Filter =
            {
                /// the list of indications to select from
                Indications: string []
                /// the list of generics to select from
                Generics: string []
                /// the list of routes to select from
                Routes: string []
                /// the list of shapes to select from
                Shapes: string []
                /// the possible dose types
                DoseTypes : DoseType []
                Diluents : string []
                /// the selected indication
                Indication: string option
                /// the selected generic
                Generic: string option
                /// the selected route
                Route: string option
                /// the selected shape
                Shape: string option
                /// the DoseType
                DoseType : DoseType option
                Diluent : string option
            }


        /// <summary>
        /// The main communication object to transfer the
        /// results of the solver to the client. The fields
        /// are used to select the correct scenario.
        /// </summary>
        type PrescriptionResult =
            {
                Filter : Filter
                /// the patient
                Patient: Patient
                /// the list of scenarios
                Scenarios: OrderScenario []
            }


        module Exceptions =

            type Message =
                | OrderCouldNotBeSolved of string * Order


        module Events =

            type Event =
                | SolverReplaceUnit of (Name * Unit)
                | OrderSolveStarted of Order
                | OrderSolveFinished of Order
                | OrderSolveConstraintsStarted of Order * Constraint list
                | OrderSolveConstraintsFinished of Order * Constraint list
                | OrderScenario of string
                | OrderScenarioWithNameValue of Order * Name * BigRational


        module Logging =

            open Informedica.GenSolver.Lib.Types.Logging

            type OrderMessage =
                | OrderException of Exceptions.Message
                | OrderEvent of Events.Event
                interface IMessage