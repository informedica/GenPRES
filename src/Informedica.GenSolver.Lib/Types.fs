namespace Informedica.GenSolver.Lib


[<AutoOpen>]
module rec Types =

    open System
    open MathNet.Numerics

    open Informedica.GenUnits.Lib

    /// Represents a non empty/null string identifying a `Variable`.
    /// `Name` can be no longer than 1000 characters and cannot be
    /// a null string
    type Name = Name of string


    /// The minimal value in
    /// a `ValueRange`. Can be inclusive
    /// or exclusive.
    type Minimum =
        | MinIncl of ValueUnit
        | MinExcl of ValueUnit


    /// The maximum value in
    /// a `ValueRange`. Can be inclusive
    /// or exclusive.
    type Maximum =
        | MaxIncl of ValueUnit
        | MaxExcl of ValueUnit


    type ValueSet = ValueSet of ValueUnit


    type Increment = Increment of ValueUnit


    /// `ValueRange` represents a domain of
    /// rational numbers. A `ValueRange` can be either
    /// - `Unrestricted`: any rational number
    /// - `Increment`: any number that is a multiple of an increment
    /// - `Min`: have a minimum
    /// - `MinIncrement`: a minimum with the domain consisting of multiples of one increment
    /// - `Max`: have a maximum
    /// - `IncrementMax`: a domain of multiples of an increment with a maximum
    /// - `MinMax`: have both a minimum and maximum
    type ValueRange =
        | Unrestricted
        | NonZeroNoneNegative
        | Min of Minimum
        | Max of Maximum
        | MinMax of Minimum * Maximum
        | Incr of Increment
        | MinIncr of min: Minimum * incr: Increment
        | IncrMax of incr: Increment * max: Maximum
        | MinIncrMax of min: Minimum * incr: Increment * max: Maximum
        | ValSet of ValueSet // Set<BigRational>

    /// Represents a variable in an
    /// `Equation`. The variable is
    /// identified by `Name` and has
    /// a `Values` described by the
    /// `ValueRange`.
    type Variable = { Name: Name; Values: ValueRange }

    /// Represents a property of a `Variable`.
    type Property =
        | MinProp of Minimum
        | MaxProp of Maximum
        | IncrProp of Increment
        | ValsProp of ValueSet

    /// An equation is either a `ProductEquation`
    /// or a `Sumequation`, the first variable is the
    /// dependent variable, i.e. the result of the
    /// equation, the second part are the independent
    /// variables in the equation
    type Equation =
        | ProductEquation of Variable * Variable list
        | SumEquation of Variable * Variable list

    /// The `Result` of solving an `Equation`
    /// is that either the `Equation` is the
    /// same or has `Changed`.
    type SolveResult =
        | Unchanged
        | Changed of List<Variable * Property Set>
        | Errored of Exceptions.Message list


    /// Represents a constraint on a `Variable`.
    /// I.e. either a set of values, or an increment
    /// or a minimum of maximum.
    type Constraint =
        {
            Name: Name
            Property: Property
        }


    module Exceptions =

        type Message =
            | NameNullOrWhiteSpaceException
            | NameLongerThan1000 of name: string
            | ValueRangeMinLargerThanMax of Minimum * Maximum
            | ValueRangeNotAValidOperator
            | ValueRangeEmptyValueSet
            | ValueRangeTooManyValues of valueCount: int
            | ValueRangeEmptyIncrement
            | ValueRangeMinOverFlow of Minimum
            | ValueRangeMaxOverFlow of Maximum
            | ValueRangeMinMaxException of string
            | VariableCannotSetValueRange of Variable * ValueRange
            | VariableCannotCalcVariables of
                v1: Variable *
                op: (ValueRange -> ValueRange -> ValueRange) *
                v2: Variable
            | EquationDuplicateVariables of duplicateVars: Variable list
            | EquationEmptyVariableList
            | ConstraintVariableNotFound of Constraint * Equation list
            | SolverInvalidEquations of Equation list
            | SolverTooManyLoops of loopCount : int * Equation list
            | SolverErrored of loopCount: int * Message list * Equation list


    module Events =

        type Event =
            | EquationStartedSolving of Equation
            | EquationStartCalculation of
                op1: (Variable -> Variable -> Variable) *
                op2: (Variable -> Variable -> Variable) *
                x: Variable *
                y: Variable *
                xs: Variable List
            | EquationFinishedCalculation of Variable list * changed : bool
            | EquationCouldNotBeSolved of Equation
            | EquationFinishedSolving of Equation * SolveResult
            | SolverStartSolving of Equation list
            | SolverLoopedQue of loopCount: int * Equation list
            | SolverFinishedSolving of Equation list
            | ConstraintSortOrder of (int * Constraint) list
            | ConstraintApplied of Constraint
            | ConstrainedSolved of Constraint


    module Logging =

        type IMessage =
            interface
            end


        type TimeStamp = DateTime


        type Level =
            | Informative
            | Warning
            | Debug
            | Error


        type SolverMessage =
            | ExceptionMessage of Exceptions.Message
            | SolverMessage of Events.Event
            interface IMessage


        type Message =
            {
                TimeStamp: TimeStamp
                Level: Level
                Message: IMessage
            }


        type Logger = { Log: Message -> unit }
