namespace Informedica.GenOrder.Lib



[<AutoOpen>]
module Types =
        open System
        open MathNet.Numerics

        open Informedica.GenUnits.Lib
        open Informedica.GenSolver.Lib.Types

        type Gender = Informedica.GenForm.Lib.Types.Gender
        type Patient = Informedica.GenForm.Lib.Types.Patient


        /// A `VariableUnit` is the combination of
        /// an `Informedica.GenSolver.Lib.Variable` with
        /// an `Informedica.GenUnits.Lib.Unit`
        /// The `Variable` stores the base values according
        /// to the `Unit`
        type OrderVariable =
            {
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


        /// An order equation is either a product equation or a
        /// sum equation
        type OrderEquation =
            | OrderProductEquation of OrderVariable * OrderVariable list
            | OrderSumEquation of OrderVariable * OrderVariable list


        /// Time "tme"
        /// Type that represents a time
        type Time = Time of OrderVariable


        /// Count "cnt"
        /// Type that represents a count
        type Count = Count of OrderVariable


        /// Count / Time "frq"
        /// Type that represents a frequency
        type Frequency = Frequency of OrderVariable


        /// Quantity "qty"
        /// Type that represents a quantity
        type Quantity = Quantity of OrderVariable


        //// Quantity / Time "ptm"
        /// Type that represents a quantity per time
        type PerTime = PerTime of OrderVariable

        /// Quantity / Time "rte"
        /// Type that represents a rate
        type Rate = Rate of OrderVariable


        /// Quantity "tot"
        /// Type that represents a total
        type Total = Total of OrderVariable


        /// Quantity / Quantity "cnc"
        /// Type that represents a concentration
        type Concentration = Concentration of OrderVariable


        /// Quantity / Adjust "qty_adj"
        /// Type that represents a adjusted quantity
        type QuantityAdjust = QuantityAdjust of OrderVariable


        /// Quantity / Adjust / Time "ptm_adj"
        /// Type that represents a adjusted quantity per time
        type PerTimeAdjust = PerTimeAdjust of OrderVariable


        /// Quantity / Adjust / Time "rte_adj"
        /// Type that represents a adjusted quantity per time
        type RateAdjust = RateAdjust of OrderVariable


        /// Quantity / Adjust "tot_adj"
        /// Type that represents a adjusted total
        type TotalAdjust = TotalAdjust of OrderVariable



        /// An Id is represented by a string
        type Id = Id of string


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
                /// The `Item` `Dose`, i.e. quantity, total and rate of `Item` administered
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
                // The `Component` `Dose`,
                /// i.e. quantity, total and rate of `Component` administered
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
                /// How the orderable is prescribed
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
            | Continuous
            /// A discontinuous prescription with a frequency
            | Discontinuous of Frequency
            /// A discontinuous prescription with both frequency and time
            | Timed of Frequency * Time


        type EquationMapping =
            | ProductMapping of string list
            | SumMapping of string list


        /// The different possible order types
        type OrderType =
            | AnyOrder
            | ProcessOrder
            | ContinuousOrder
            | DiscontinuousOrder
            | TimedOrder


        type MinMax = Informedica.GenForm.Lib.Types.MinMax
        type DoseLimit = Informedica.GenForm.Lib.Types.DoseLimit
        type SolutionLimit = Informedica.GenForm.Lib.Types.SolutionLimit


        /// The representation of a drug order that
        /// can be derived by a drug product inventory
        /// and the related dose rule
        type DrugOrder =
            {
                /// Identifies the specific drug order
                Id:  string
                /// The name of the order
                Name : string
                /// The list of drug products that can be used for the order
                Products : ProductComponent list
                /// The quantities of the drug order
                Quantities :  BigRational list
                /// The unit the `DrugOrder` is measured in,
                /// i.e. of the `Quantities`
                Unit : string
                /// The route by which the order is applied
                Route : string
                // The type of order
                OrderType : OrderType
                /// The list of possible frequency values
                Frequencies : BigRational list
                /// The time unit to be used when using a frequency
                FreqUnit : string
                /// The list of possible rate values
                Rates : BigRational list
                /// The time unit to be used when using a rate
                RateUnit : string
                /// The min and/or max time for the infusion time
                Time : MinMax
                /// The time unit for infusion time (duration)
                TimeUnit : string
                /// The dose limits for an drugorder
                Dose : DoseLimit option
                // The amount of orderable that will be given each time
                DoseCount : BigRational option
                // The adjust quantity for the adjusted dose calculations
                Adjust : BigRational option
                // The adjust unit
                AdjustUnit : string
            }
        /// The product components that are used by the drug order
        and ProductComponent =
            {
                /// The name of the product
                Name : string
                /// The shape of the product
                Shape : string
                /// The quantities of the product
                /// Note: measured in the same unit as
                /// the `DrugOrder` unit
                Quantities : BigRational list
                /// The "divisibility" of the products
                Divisible : BigRational option
                /// The time unit used for frequency
                TimeUnit : string
                /// The time unit used for rate
                RateUnit : string
                /// The list of substances contained in the product
                Substances: SubstanceItem list
            }
        and SubstanceItem =
            {
                /// The name of the substance
                Name : string
                /// The possible concentrations of the substance
                /// in the products
                Concentrations : BigRational list
                /// The unit by which the substance is
                /// measured.
                Unit : string
                /// The time unit used for the frequency
                TimeUnit : string
                /// The dose limits for a substance
                Dose : DoseLimit option
                /// The solution limits for a solution
                Solution : SolutionLimit option
            }


        type Scenario =
            {
                No : int
                Indication : string
                Name : string
                Shape : string
                Route : string
                Prescription : string
                Preparation : string
                Administration : string
            }


        type ScenarioResult =
            {
                Indications: string []
                Generics: string []
                Routes: string []
                Shapes: string []
                Indication: string option
                Generic: string option
                Route: string option
                Shape: string option
                Patient: Patient
                Scenarios: string []
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


