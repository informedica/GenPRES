namespace Informedica.GenSolver.Lib


[<AutoOpen>]
module rec Types =

    open Informedica.GenUnits.Lib

    /// <summary>
    /// Represents a non-empty/null string identifying a <c>Variable</c>.
    /// <c>Name</c> can be no longer than 1000 characters and cannot be
    /// a null string
    /// </summary>
    type Name = | Name of string


    /// <summary>
    /// The minimal value in
    /// a <c>ValueRange</c>. Can be inclusive
    /// or exclusive.
    /// </summary>
    type Minimum =
        | MinIncl of ValueUnit
        | MinExcl of ValueUnit


    /// <summary>
    /// The maximum value in
    /// a <c>ValueRange</c>. Can be inclusive
    /// or exclusive.
    /// </summary>
    type Maximum =
        | MaxIncl of ValueUnit
        | MaxExcl of ValueUnit


    /// <summary>
    /// A set of discrete values
    /// </summary>
    type ValueSet = | ValueSet of ValueUnit


    /// <summary>
    /// A set of increments
    /// </summary>
    type Increment = | Increment of ValueUnit


    /// <summary>
    /// Represents a domain of rational numbers.
    /// </summary>
    /// <remarks>
    /// A <c>ValueRange</c> can be one of the following:
    /// <list type="bullet">
    /// <item><description><c>Unrestricted</c>: any rational number</description></item>
    /// <item><description><c>NonZeroNoneNegative</c>: any positive rational number greater than zero</description></item>
    /// <item><description><c>Min</c>: has a minimum</description></item>
    /// <item><description><c>Max</c>: has a maximum</description></item>
    /// <item><description><c>MinMax</c>: has both a minimum and maximum</description></item>
    /// <item><description><c>Incr</c>: any number that is a multiple of an increment</description></item>
    /// <item><description><c>MinIncr</c>: a minimum with the domain consisting of multiples of one increment</description></item>
    /// <item><description><c>IncrMax</c>: a domain of multiples of an increment with a maximum</description></item>
    /// <item><description><c>MinIncrMax</c>: a minimum with a domain of multiples of an increment with a maximum</description></item>
    /// <item><description><c>ValSet</c>: a set of discrete values</description></item>
    /// </list>
    /// </remarks>
    type ValueRange =
        | Unrestricted // <..>
        | NonZeroPositive // <0..>
        | Min of Minimum // <min .. >
        | Max of Maximum // <..max >
        | MinMax of min: Minimum * max: Maximum // <min .. max>
        | Incr of Increment // <..incr ..>
        | MinIncr of min: Minimum * incr: Increment // <min .. incr ..>
        | IncrMax of incr: Increment * max: Maximum // <.. incr .. max >
        | MinIncrMax of min: Minimum * incr: Increment * max: Maximum // <min .. incr .. mac>
        | ValSet of ValueSet // [x1;x2;x3]


    /// <summary>
    /// Represents a variable in an
    /// <c>Equation</c>. The variable is
    /// identified by <c>Name</c> and has
    /// a <c>Values</c> described by the
    /// <c>ValueRange</c>.
    /// </summary>
    type Variable =
        {
            Name: Name
            Values: ValueRange
        }


    /// <summary>
    /// Represents a property of a <c>Variable</c>.
    /// </summary>
    type Property =
        | MinProp of Minimum
        | MaxProp of Maximum
        | IncrProp of Increment
        | ValsProp of ValueSet


    /// <summary>
    /// An equation is either a <c>ProductEquation</c>
    /// or a <c>SumEquation</c>, the first variable is the
    /// dependent variable, i.e., the result of the
    /// equation, the second part are the independent
    /// variables in the equation
    /// </summary>
    type Equation =
        | ProductEquation of Variable * Variable list
        | SumEquation of Variable * Variable list


    /// <summary>
    /// The <c>Result</c> of solving an <c>Equation</c>
    /// is that either the <c>Equation</c> is the
    /// same or has <c>Changed</c>.
    /// </summary>
    type SolveResult =
        | Unchanged
        | Changed of List<Variable * Property Set>
        | Errored of Exceptions.Message list


    /// <summary>
    /// Represents a constraint on a <c>Variable</c>.
    /// I.e., either a set of values or an increment,
    /// minimum or maximum.
    /// </summary>
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
            | ValueRangeEmptyValueSet of string
            | ValueSetOverflow of valueCount: int
            | ValueRangeEmptyIncrement
            | ValueRangeMinShouldHaveOneValue of ValueUnit
            | ValueRangeMinOverFlow of Minimum
            | ValueRangeMaxShouldHaveOneValue of ValueUnit
            | ValueRangeMaxOverFlow of Maximum
            | ValueRangeMinMaxException of string
            | VariableCannotSetValueRange of Variable * ValueRange
            | VariableCannotCalcVariables of v1: Variable * op: (ValueRange -> ValueRange -> ValueRange) * v2: Variable
            | EquationDuplicateVariables of duplicateVars: Variable list
            | EquationEmptyVariableList
            | ConstraintVariableNotFound of Constraint * Equation list
            | SolverInvalidEquations of Equation list
            | SolverTooManyLoops of loopCount: int * Equation list
            | SolverErrored of loopCount: int * Message list * Equation list
            | UnexpectedException of ex: exn
            | ValueRangeBoundaryShouldHaveOneValue of ValueUnit
            | ValueRangeBoundaryOverFlow of obj

    module Events =

        type Event =
            | EquationStartedSolving of minmax: bool * Equation
            | EquationStartCalculation of
                op1: (Variable -> Variable -> Variable) *
                op2: (Variable -> Variable -> Variable) *
                y: Variable *
                xs: Variable List
            | EquationFinishedCalculation of Variable list * changed: bool
            | EquationCouldNotBeSolved of Equation
            | EquationFinishedSolving of Equation * SolveResult
            | SolverStartSolving of minmax: bool * Equation list
            | SolverLoopedQue of loopCount: int * (int * Equation) list
            | SolverFinishedSolving of Equation list
            | ConstraintSortOrder of (int * Constraint) list
            | ConstraintApplied of Constraint
            | ConstrainedSolved of Constraint


    module Logging =

        open Informedica.Logging.Lib


        type SolverMessage =
            | ExceptionMessage of Exceptions.Message
            | SolverEventMessage of Events.Event

            interface IMessage
