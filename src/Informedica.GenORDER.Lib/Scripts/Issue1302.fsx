// Issue #1302: the dose dialog offers a substance concentration the order cannot reach.
//
// Played out on the logged scenario (amoxicilline/clavulaanzuur IV in NaCl 0,9%, 14.5 kg,
// 3 x/day) and checked on the amfo, morfCont, tpn and tpnComplete fixtures. Three defects:
//
// 1. Orderable.increaseQuantityIncrement harmonizes the component increments to the SMALLEST
//    one, so the diluent keeps a grid far finer than the count cap asks for (21 values at
//    0.5 mL where the cap is 10).
// 2. ValueSet.prune compares the increment in base units (1/2000 L) with values in mL, so the
//    later prune of that diluent set thins it to multiples of 1.5 mL instead of a clean coarser
//    grid.
// 3. ValueSet.prune always keeps the minimum and the maximum, also off the grid it chose; such
//    an end has no partner once the other variables follow the grid.
//
// The concentration offered is item quantity / orderable quantity over value sets; the sum
// equation ties the item quantity to its own component volume, which the division does not
// know. So any thinning of one summand to a coarser grid than the other leaves offered
// concentrations that no combination reaches, and the final solve fails on the pick.
//
// Fix 1: the common grid is the coarser of what the finest component needs and what the total
// (the orderable quantity) needs for the count cap.
// Fix 2: prune converts the increment to the unit of the values before testing multiples.
// Fix 3: prune drops an off-grid end, unless fewer than two values would remain.
//
// The loop order of Order.minIncrMaxToValues stays as it is: components first, the dose
// quantity derived last and never pruned. Three other orders were measured and rejected: the sum
// first, and the largest count first with or without the sum in the list. A prune does not
// shrink the other ranges, so the later passes prune just the same; a pruned sum leaves derived
// lists full of picks no combination reaches; the sorted components give the same result as the
// current order on every fixture.
//
// 4. Lists derived from two value sets are cross products. With fixes 1 to 3 amox/clav still
//    offers clavulaanzuur and natrium concentrations and NaCl volumes that no combination
//    reaches, with no prune involved: the fixed concentrations of the items in a component tie
//    them to the component volume and so to each other, and no equation states that tie for the
//    orderable concentrations.
//
// Fix 4: two equations, one per item and one per orderable. The item's orderable concentration
// is its component concentration times the component's share of the orderable, and the shares
// of the components add up to one. Neither adds items together, so unit groups do not matter,
// and both hold for any number of items and components. The second needs an orderable variable
// that holds one, which the Order type does not have yet; the script maps it in for the solve.

#I __SOURCE_DIRECTORY__
System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib

module Variable = Informedica.GenSolver.Lib.Variable
module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
module ValueSet = Informedica.GenSolver.Lib.Variable.ValueRange.ValueSet
module Increment = Informedica.GenSolver.Lib.Variable.ValueRange.Increment
module Quantity = Informedica.GenOrder.Lib.OrderVariable.Quantity
module Rate = Informedica.GenOrder.Lib.OrderVariable.Rate
module Concentration = Informedica.GenOrder.Lib.OrderVariable.Concentration
module Component = Informedica.GenOrder.Lib.Order.Orderable.Component
module Name = Informedica.GenSolver.Lib.Variable.Name
module Schedule = Informedica.GenOrder.Lib.Order.Schedule


let logger = Informedica.GenOrder.Lib.Logging.noOp


// ---------------------------------------------------------------------------------------------
// Fix 2: ValueSet.prune, GenSOLVER Variable.fs
// ---------------------------------------------------------------------------------------------

/// Prune a value set to at most n values: keep the values that are a multiple of m times the
/// increment for the smallest m that fits. The increment is compared in the unit of the values,
/// not in base units. A minimum or maximum off that grid is dropped, where the original always
/// kept them: an off-grid end has no partner once the other variables follow the grid. Only when
/// fewer than two values would remain are the ends kept.
module ValueSetFix =

    let prune incr n =
        let rec loop mn mx m incr (xs: BigRational[]) =
            let filtered = xs |> Array.filter (fun x -> (x / (incr * m)).Denominator = 1I)

            if filtered |> Array.length <= (max n 2) then
                if filtered |> Array.length >= 2 then
                    filtered
                else
                    [| yield mn; yield! filtered; yield mx |] |> Array.distinct
            else
                loop mn mx (m + 1N) incr xs

        fun (ValueSet vu) ->
            let u = vu |> ValueUnit.getUnit

            let v =
                // the increment in the unit of the values, where the original read its base value
                match incr |> Option.map (ValueUnit.convertTo u >> ValueUnit.getValue) with
                | Some [| incr |] ->
                    let xs = vu |> ValueUnit.getValue |> Array.distinct
                    let mn = xs |> Array.min
                    let mx = xs |> Array.max
                    xs |> loop mn mx 1N incr
                | _ -> vu |> ValueUnit.getValue |> Array.prune n

            vu |> ValueUnit.setValue v |> ValueSet.create


module ValueRangeFix =

    let prune incr n = ValueRange.map id id id id id id id (ValueSetFix.prune incr n)


module VariableFix =

    let minIncrMaxToValues n (var: Variable) =
        if var |> Variable.isMinIncrMax |> not then
            var
        else
            { var with
                Values =
                    match
                        var.Values |> ValueRange.getMin, var.Values |> ValueRange.getIncr, var.Values |> ValueRange.getMax
                    with
                    | Some min, Some incr, Some max -> ValueSet.minIncrMaxToValueSet min incr max |> ValSet
                    | _ -> var.Values
                    |> fun vr ->
                        match n with
                        | None -> vr
                        | Some n ->
                            let incr = var.Values |> ValueRange.getIncr |> Option.map Increment.toValueUnit
                            vr |> ValueRangeFix.prune incr n
            }


module OrderVariableFix =

    let minIncrMaxToValues n (ovar: OrderVariable) =
        { ovar with Variable = ovar.Variable |> VariableFix.minIncrMaxToValues n }


// ---------------------------------------------------------------------------------------------
// Fix 1: Orderable.increaseQuantityIncrement, GenORDER Order.fs
// ---------------------------------------------------------------------------------------------

module OrderableFix =

    let private incrBase incr =
        incr |> Increment.toValueUnit |> ValueUnit.getBaseValue

    let private incrOf (qty: Quantity) =
        (qty |> Quantity.toOrdVar |> OrderVariable.getVar).Values |> ValueRange.getIncr

    /// The quantity increments of an orderable and its components raised to one common grid: the
    /// coarser of the finest increment a component needs and the increment the orderable
    /// quantity, the sum of the components, needs to stay within maxCount values. The original
    /// took the finest component increment alone, which left a wide component with many more
    /// values than maxCount.
    let increaseQuantityIncrement logger maxCount incrs (orb: Orderable) =
        if
            orb.Components
            |> List.map _.OrderableQuantity
            |> List.forall Quantity.hasIncrement
            |> not
        then
            orb
        else
            let cmpIncrs =
                orb.Components
                |> List.map (Component.increaseQuantityIncrement maxCount incrs)
                |> List.choose (_.OrderableQuantity >> incrOf)

            if cmpIncrs |> List.length <> (orb.Components |> List.length) then
                orb
            else
                let finestCmp = cmpIncrs |> List.minBy incrBase

                let orbIncr =
                    orb.OrderableQuantity |> Quantity.increaseIncrement maxCount incrs |> incrOf

                let incr =
                    match orbIncr with
                    | Some orbIncr -> [ finestCmp; orbIncr ] |> List.maxBy incrBase
                    | None -> finestCmp

                Logging.logDebugLazy
                    logger
                    (fun () ->
                        $"Increase quantity increment to {incr |> Increment.toString false}"
                        |> Events.OrderScenario
                    )

                { orb with
                    OrderableQuantity = orb.OrderableQuantity |> Quantity.increaseIncrement maxCount [ incr ]
                    Components =
                        orb.Components
                        |> List.map (Component.increaseQuantityIncrement maxCount [ incr ])
                }


module OrderFix =

    open Informedica.GenOrder.Lib.Order

    let increaseQuantityIncrement logger maxCount incrs (ord: Order) =
        { ord with Orderable = ord.Orderable |> OrderableFix.increaseQuantityIncrement logger maxCount incrs }


    /// Order.increaseIncrements with the fixed quantity increment and the min-max solve given;
    /// the rest is the original.
    let increaseIncrementsWith solveMinMax logger maxQtyCount maxRateCount (ord: Order) =
        let maxQtyCount = maxQtyCount |> BigRational.fromInt
        let maxRateCount = maxRateCount |> BigRational.fromInt

        if ord.Schedule |> Schedule.isContinuous then
            ord
        else
            let orbQty = ord.Orderable.OrderableQuantity |> Quantity.toOrdVar

            let incrs u =
                [ 1N / 10N; 1N / 2N; 1N; 5N; 10N; 20N ]
                |> List.map (ValueUnit.singleWithUnit u)
                |> List.map Increment.create

            if
                orbQty.Variable
                |> Variable.getUnit
                |> Option.map (ValueUnit.Group.unitToGroup >> (=) Group.VolumeGroup >> not)
                |> Option.defaultValue false
            then
                ord
            else
                let incrOrd =
                    ord |> increaseQuantityIncrement logger maxQtyCount (incrs Units.Volume.milliLiter)

                if ord = incrOrd then
                    Ok ord
                else
                    incrOrd |> Events.OrderIncreaseQuantityIncrement |> Logging.logInfo logger
                    incrOrd |> solveMinMax "Increase Quantity Increment" false logger
                |> function
                    | Error _ -> ord
                    | Ok ord ->
                        let incrOrd =
                            ord
                            |> increaseRateIncrement
                                maxRateCount
                                (incrs (Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour))

                        if incrOrd = ord then
                            Ok ord
                        else
                            incrOrd |> Events.OrderIncreaseRateIncrement |> Logging.logInfo logger
                            incrOrd |> solveMinMax "Increase Rate Increment" false logger
                        |> function
                            | Error _ -> ord
                            | Ok ord -> ord
        |> Ok


    let increaseIncrements logger = increaseIncrementsWith solveMinMax logger


    /// Order.minIncrMaxToValues with the fixed prune and the solve given; the rest is the
    /// original. One variable is converted and pruned per pass, and the order is solved before
    /// the next pass.
    let minIncrMaxToValuesWith solveOrder useMaxNumberOfValues minTime skipRate logger (ord: Order) =
        let mutable isSolved = false

        let rec loop (ord: Order) =
            let mutable flag = false

            let ovars =
                [
                    yield! ord.Orderable.Components |> List.map (_.OrderableQuantity >> Quantity.toOrdVar)
                    ord.Orderable.Dose.Quantity |> Quantity.toOrdVar
                    if not skipRate then
                        ord.Orderable.Dose.Rate |> Rate.toOrdVar
                ]
                |> List.map (fun ovar ->
                    if
                        flag
                        || ovar.DefinedConstraints.Incr |> Option.isNone
                        || ovar.Variable |> Variable.isMinIncrMax |> not
                    then
                        ovar
                    else
                        flag <- true

                        let n =
                            match ord.Schedule with
                            | Continuous _ -> if useMaxNumberOfValues then 1_000 else 100
                            | Once
                            | Discontinuous _ ->
                                if useMaxNumberOfValues then
                                    if ord.Orderable.Components |> List.length > 1 then 20 else 100
                                else
                                    10
                            | OnceTimed _
                            | Timed _ ->
                                if useMaxNumberOfValues then
                                    if ord.Orderable.Components |> List.length > 2 then 5 else 10
                                else if ord.Orderable.Components |> List.length > 2 then
                                    5
                                else
                                    20
                            |> Some

                        ovar
                        |> OrderVariableFix.minIncrMaxToValues n
                        |> fun ovar ->
                            Events.MinIncrMaxToValues ovar |> Logging.logInfo logger
                            ovar
                )

            if not flag then
                ord
            else
                ord
                |> fromOrdVars ovars
                |> solveOrder "Min Incr Max to Values" true logger
                |> function
                    | Ok ord ->
                        isSolved <- true
                        loop ord
                    | Error err ->
                        (err |> snd |> List.map (sprintf "%A") |> String.concat "\n", err |> fst)
                        |> Exceptions.OrderCouldNotBeSolved
                        |> Logging.logError logger

                        ord

        if minTime then ord |> minimizeTime logger else ord
        |> loop
        |> fun ord ->
            if not isSolved then
                ord
                |> solveOrder "Min Incr Max to Values final Loop" true logger
                |> Result.defaultValue ord
            else
                ord


    let minIncrMaxToValues useMaxNumberOfValues = minIncrMaxToValuesWith solveOrder useMaxNumberOfValues


// ---------------------------------------------------------------------------------------------
// Fix 4: the equations that tie the items of a component to each other and the components to
// each other, for EquationMapping.fs.
//
// Today an item's orderable concentration is only item orderable quantity / orderable quantity,
// a cross product of two value sets. The item's quantity is its component concentration times
// the component volume, and the component's share of the orderable is its own variable
// ([cmp]_orb_cnc, dimensionless). So the item's orderable concentration is also its component
// concentration times that share, and the shares add up to one.
// ---------------------------------------------------------------------------------------------

module TieFix =

    open Informedica.GenOrder.Lib.Order

    module Mapping = EquationMapping

    let tieEquation = "[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc"

    /// The shares of the components in the orderable add up to one: ties the components to each
    /// other. Needs an orderable variable that holds one, which the Order does not have yet; the
    /// script maps it in for the solve.
    let shareSumEquation = "[orb]_orb_cnc = sum([cmp]_orb_cnc)"

    /// The orderable variable holding one, named as the mapping names [orb]_orb_cnc.
    let orderableShare (ord: Order) =
        let name =
            [ ord.Id |> Id.toString; ord.Orderable.Name |> Informedica.GenOrder.Lib.WrappedString.Name.toString ]
            |> Informedica.GenOrder.Lib.WrappedString.Name.create
            |> Informedica.GenOrder.Lib.WrappedString.Name.add "orb"
            |> Informedica.GenOrder.Lib.WrappedString.Name.add "cnc"

        let one = Units.Count.times |> ValueUnit.singleWithValue 1N |> ValueSet.create |> Some
        let cs = OrderVariable.Constraints.create None None None one
        OrderVariable.create name None None None one cs cs

    /// Order.mapToOrderEquations with the variables to map to given.
    let mapToOrderEquationsWith (ovars: OrderVariable list) eqMapping =
        let map repl eqMapping =
            let eqs, c =
                match eqMapping with
                | SumMapping eqs -> eqs, OrderSumEquation
                | ProductMapping eqs -> eqs, OrderProductEquation

            let find s =
                match ovars |> List.tryFind (fun v -> v.Variable.Name |> Name.toString = s) with
                | Some v -> v
                | None -> raise (System.Collections.Generic.KeyNotFoundException $"cannot find %s{s}")

            eqs
            |> List.map (String.replace "=" repl)
            |> List.map (String.split repl >> List.map String.trim >> List.filter String.notEmpty)
            |> List.map (fun xs ->
                match xs with
                | h :: rest ->
                    let h = find h

                    let rest =
                        rest
                        |> List.map find
                        |> fun rest -> if repl <> "+" then rest else rest |> List.filter (OrderVariable.eqsUnitGroup h)

                    (h, rest) |> c
                | _ -> invalidOp $"cannot map %A{eqs}"
            )

        let sumEqs, prodEqs = eqMapping
        sumEqs |> map "+" |> List.append (prodEqs |> map "*")

    /// Order.solve with extra equations appended to the mapped ones and extra variables to map
    /// them to; logging left out.
    let rec solve extra extraOvars msg minMax printErr logger (ord: Order) =
        let harmonize ord =
            ord.Orderable
            |> Orderable.harmonizeItemConcentrations logger
            |> function
                | false, _ -> ord |> Ok
                | true, orb ->
                    { ord with Order.Orderable = orb } |> solve extra extraOvars "Harmonize" minMax printErr logger

        let mapping =
            match ord.Schedule with
            | Once -> Mapping.Literals.once
            | Continuous _ -> Mapping.Literals.continuous
            | OnceTimed _ -> Mapping.Literals.onceTimed
            | Discontinuous _ -> Mapping.Literals.discontinuous
            | Timed _ -> Mapping.Literals.timed
            |> Mapping.getEquations
            |> fun eqs -> eqs @ extra
            |> Mapping.getEqsMapping ord

        let oEqs = mapping |> mapToOrderEquationsWith (toOrdVars ord @ extraOvars ord)

        try
            oEqs
            |> Solver.mapToSolverEqs
            |> fun eqs ->
                if minMax then
                    eqs |> Solver.solveMinMax logger
                else
                    eqs |> Solver.solve logger
            |> function
                | Ok eqs -> eqs |> Solver.mapToOrderEqs oEqs |> mapFromOrderEquations ord |> harmonize
                | Error(eqs, m) ->
                    let ord = eqs |> Solver.mapToOrderEqs oEqs |> mapFromOrderEquations ord
                    Error(ord, m)
        with exn ->
            Error(ord, [ exn |> Informedica.GenSolver.Lib.Types.Exceptions.UnexpectedException ])


    let solveMinMax extra extraOvars msg printErr logger = solve extra extraOvars msg true printErr logger

    let solveOrder extra extraOvars msg printErr logger = solve extra extraOvars msg false printErr logger

    let calcMinMax extra extraOvars logger = solveMinMax extra extraOvars "Calc Min Max" true logger

    let solveNormDose extra extraOvars logger ord =
        let normDoseOrd = ord |> setMedianDoseValue

        if normDoseOrd = ord then
            ord |> Ok
        else
            normDoseOrd |> solveMinMax extra extraOvars "Solve Normal Dose" false logger


// ---------------------------------------------------------------------------------------------
// The scenario: the medication as the server logged it on 2026-10-11
// ---------------------------------------------------------------------------------------------

let amoxClavText =
    """
Id: 303000f0-98b7-4db5-a0f2-a8a7642df9fc
Name: amoxicilline/clavulaanzuur
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 14.5 kg
Frequencies: 3 x/day
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

    Name: amoxicilline/clavulaanzuur
    Form: poeder voor injectievloeistof
    Quantities: 10;20 ml
    Divisible: 10
    Dose:
    Solution:
    Substances:

        Name: amoxicilline
        Quantities: 500;1000 mg
        Concentrations: 50 mg/ml
        Dose: amoxicilline, [dun] mg, [per-time-adj] 100 mg/kg/day, [per-time] max 6000 mg/day
        Solution:  [conc] 20 mg/ml

        Name: clavulaanzuur
        Quantities: 50;100;200 mg
        Concentrations: 5;10 mg/ml
        Dose: clavulaanzuur, [dun] mg, [per-time-adj] 10 mg/kg/day, [per-time] max 600 mg/day
        Solution:

    Name: NaCl 0,9%
    Form: vloeistof
    Quantities: 1 ml
    Divisible: 10
    Dose:
    Solution:
    Substances:

        Name: natrium
        Quantities:
        Concentrations: 0.155 mmol/ml
        Dose:
        Solution:

        Name: chloor
        Quantities:
        Concentrations: 0.155 mmol/ml
        Dose:
        Solution:
"""


let amoxClavOrder () =
    amoxClavText
    |> Medication.fromString
    |> Result.mapError (String.concat "\n")
    |> Result.bind (Medication.toOrder Scenarios.testStart >> Result.mapError (sprintf "%A"))
    |> function
        | Ok ord -> ord
        | Error e -> failwith e


let cmpName = "amoxicilline/clavulaanzuur"
let itmName = "amoxicilline"
let nacl = "NaCl 0,9%"


let findComponent name (ord: Order) =
    ord.Orderable.Components |> List.find (fun c -> c.Name |> Name.toString = name)


let valuesOf (ovar: OrderVariable) =
    ovar
    |> OrderVariable.getVar
    |> Variable.getValueRange
    |> ValueRange.getValSet
    |> Option.map (ValueSet.toValueUnit >> ValueUnit.getValue)
    |> Option.defaultValue [||]


let valueRangeOf (ovar: OrderVariable) =
    (ovar |> OrderVariable.getVar).Values


/// The concentrations the dialog offers for amoxicilline.
let offeredConcentrations ord =
    (ord |> findComponent cmpName).Items
    |> List.find (fun i -> i.Name |> Name.toString = itmName)
    |> _.OrderableConcentration
    |> Concentration.toOrdVar
    |> valuesOf


/// The pick of the nth offered concentration, then the final solve, as the server does.
let pickConcentration nth ord =
    ord
    |> Order.OrderPropertyChange.proc [ ItemOrderableConcentration(cmpName, itmName, Concentration.setNthValue nth) ]
    |> Order.solveOrder "final-solve" false logger


/// The CalcMinMax pipeline, the reset (ReCalcValues) and the final solve of OrderProcessor, with
/// the increment step and the values step passed in so the original and the fixed functions run
/// through the same steps.
let calcAndReset increaseIncrements minIncrMaxToValues ord =
    // two components, discontinuous: useMax true, no rate stage
    ord
    |> Order.applyConstraints
    |> Order.calcMinMax logger
    |> Result.bind (increaseIncrements logger 10 10)
    |> Result.map Order.setCalculatedConstraints
    |> Result.map (minIncrMaxToValues true true false logger)
    |> Result.bind (Order.solveNormDose logger)
    // the reset
    |> Result.map Order.applyCalculatedConstraints
    |> Result.map (minIncrMaxToValues true false false logger)
    |> Result.bind (Order.solveOrder "final-solve" false logger)


let original = calcAndReset Order.increaseIncrements Order.minIncrMaxToValues
let fixed' = calcAndReset OrderFix.increaseIncrements OrderFix.minIncrMaxToValues


/// Every offered concentration tried: the ones whose pick fails to solve.
let unreachablePicks ord =
    ord
    |> offeredConcentrations
    |> Array.mapi (fun nth c -> nth, c)
    |> Array.filter (fun (nth, _) -> ord |> pickConcentration nth |> Result.isError)


let report name (res: Result<Order, Order * _>) =
    match res with
    | Error(_, msgs) -> printfn $"%s{name}: pipeline failed: %A{msgs}"
    | Ok ord ->
        let offered = ord |> offeredConcentrations
        let bad = ord |> unreachablePicks
        let naclQty = (ord |> findComponent nacl).OrderableQuantity |> Quantity.toOrdVar |> valuesOf

        printfn $"%s{name}:"
        let naclText = naclQty |> Array.map string |> String.concat ";"
        printfn $"  NaCl orderable quantity: %s{naclText} mL"
        let totalText = ord.Orderable.OrderableQuantity |> Quantity.toOrdVar |> valuesOf |> Array.map string |> String.concat ";"
        printfn $"  orderable quantity: %s{totalText} mL"
        printfn $"  offered amoxicilline concentrations: %i{offered.Length}"
        printfn $"  unreachable picks: %i{bad.Length} %A{bad |> Array.map (snd >> string)}"


// ---------------------------------------------------------------------------------------------
// The fixes on the other fixtures: amfo (2 components, discontinuous), morfCont (2 components,
// continuous), tpn and tpnComplete (4 components, timed). The pipeline mirrors OrderProcessor:
// CalcMinMax, CalcValues when the client would send it, ReCalcValues as a reset, and the final
// solve, with the fixed increment step and the values step given.
// ---------------------------------------------------------------------------------------------

let pipelineWith calcMinMax increaseIncrements minIncrMaxToValues solveNormDose solveOrder (ord: Order) =
    let twoOrLess = ord.Orderable.Components |> List.length <= 2

    let hasTimeNotContinuous =
        ord.Schedule |> Schedule.hasTime && ord.Schedule |> Schedule.isContinuous |> not

    let toValues useMax minTime skipRate ord =
        ord |> minIncrMaxToValues useMax minTime skipRate logger

    ord
    |> Order.applyConstraints
    |> calcMinMax logger
    |> Result.bind (increaseIncrements logger 10 10)
    |> Result.map Order.setCalculatedConstraints
    |> Result.bind (fun ord ->
        if ord |> Order.hasNormDose && twoOrLess then
            ord |> toValues true true hasTimeNotContinuous |> solveNormDose logger
        else
            Ok ord
    )
    // calc-values, as the client sends it for two components or less
    |> Result.map (fun ord ->
        if twoOrLess then
            ord
            |> toValues false true hasTimeNotContinuous
            |> fun ord -> if hasTimeNotContinuous then ord |> toValues false true false else ord
        else
            ord
    )
    // the reset
    |> Result.map Order.applyCalculatedConstraints
    |> Result.map (toValues twoOrLess false hasTimeNotContinuous)
    |> Result.map (fun ord -> if hasTimeNotContinuous then ord |> toValues twoOrLess false false else ord)
    |> Result.bind (solveOrder "final-solve" false logger)


let pipeline =
    pipelineWith Order.calcMinMax OrderFix.increaseIncrements OrderFix.minIncrMaxToValues Order.solveNormDose Order.solveOrder


let tiedPipelineWith extra extraOvars =
    pipelineWith
        (TieFix.calcMinMax extra extraOvars)
        (OrderFix.increaseIncrementsWith (TieFix.solveMinMax extra extraOvars))
        (OrderFix.minIncrMaxToValuesWith (TieFix.solveOrder extra extraOvars))
        (TieFix.solveNormDose extra extraOvars)
        (TieFix.solveOrder extra extraOvars)


let noOvars (_: Order) : OrderVariable list = []

let itemTie = [ TieFix.tieEquation ]
let bothTies = [ TieFix.tieEquation; TieFix.shareSumEquation ]
let shareOvar (ord: Order) = [ TieFix.orderableShare ord ]

let tiedPipeline = tiedPipelineWith itemTie noOvars
let bothPipeline = tiedPipelineWith bothTies shareOvar


/// The item orderable concentrations and component orderable quantities that hold a value set of
/// two or more values: the pickable lists of the dialog.
let pickables (ord: Order) =
    [
        for cmp in ord.Orderable.Components do
            cmp.OrderableQuantity |> Quantity.toOrdVar
            for itm in cmp.Items do
                itm.OrderableConcentration |> Concentration.toOrdVar
    ]
    |> List.filter (fun ovar -> ovar |> valuesOf |> Array.length > 1)


/// Every value of every pickable set tried as a pick with a final solve: the ones that fail, with
/// the first error of the solve.
let unreachableAnyPickWithError solveOrder (ord: Order) =
    [
        for ovar in ord |> pickables do
            for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                let picked = ovar |> OrderVariable.setNthValue nth

                match ord |> Order.fromOrdVars [ picked ] |> solveOrder "pick" false logger with
                | Ok _ -> ()
                | Error(_, msgs) ->
                    yield ovar |> OrderVariable.getName |> Name.toString, (ovar |> valuesOf)[nth], msgs |> List.tryHead
    ]


let unreachableAnyPick solveOrder (ord: Order) =
    ord |> unreachableAnyPickWithError solveOrder |> List.map (fun (n, v, _) -> n, v)


/// The short name of a variable: the last name part and the variable kind.
let shortName (name: string) =
    let parts = name.TrimStart('[').Split("]_")
    let path = parts[0].Split('.')
    $"%s{path[path.Length - 1]}_%s{parts[1]}"


/// The unreachable picks per list, with the first error of the first failing pick.
let explainUnreachable solveOrder (ord: Order) =
    ord
    |> unreachableAnyPickWithError solveOrder
    |> List.groupBy (fun (n, _, _) -> n)
    |> List.iter (fun (n, xs) ->
        let total = ord |> pickables |> List.find (fun o -> o |> OrderVariable.getName |> Name.toString = n) |> valuesOf

        let _, v, err = xs |> List.head

        let errText =
            err
            |> Option.map (fun e -> $"%A{e}".Replace("\n", " "))
            |> Option.defaultValue ""
            |> fun t -> if t.Length > 200 then t.Substring(0, 200) else t

        printfn $"    %s{shortName n}: %i{xs.Length} of %i{total.Length} unreachable, first %s{string v}: %s{errText}"
    )


/// The solved order of a medication, with the time the pipeline took.
let solvedFixtureWith pipeline (med: Medication) =
    match med |> Medication.toOrder Scenarios.testStart with
    | Error e -> Error $"order failed: %A{e}"
    | Ok ord ->
        let sw = System.Diagnostics.Stopwatch.StartNew()
        let res = ord |> pipeline
        sw.Stop()

        match res with
        | Error(_, msgs) -> Error $"pipeline failed in %i{sw.ElapsedMilliseconds} ms: %A{msgs |> List.truncate 1}"
        | Ok ord -> Ok(sw.ElapsedMilliseconds, ord)


let solvedFixture = solvedFixtureWith pipeline


let reportFixtureWith pipeline solveOrder name (med: Medication) =
    match med |> solvedFixtureWith pipeline with
    | Error e -> printfn $"%s{name}: %s{e}"
    | Ok(ms, ord) ->
        let lists = ord |> pickables
        let picks = lists |> List.sumBy (valuesOf >> Array.length)
        let bad = ord |> unreachableAnyPick solveOrder
        printfn $"%s{name}: pipeline %i{ms} ms, %i{picks} picks over %i{lists.Length} lists, %i{bad.Length} unreachable"
        if bad.Length > 0 then ord |> explainUnreachable solveOrder


let reportFixture = reportFixtureWith pipeline Order.solveOrder

let reportTied = reportFixtureWith tiedPipeline (TieFix.solveOrder itemTie noOvars)

let reportBoth = reportFixtureWith bothPipeline (TieFix.solveOrder bothTies shareOvar)


let amoxClavMedication () =
    amoxClavText |> Medication.fromString |> Result.defaultWith (fun e -> failwith (String.concat "\n" e))


let fixtures =
    [
        "amfo", Scenarios.amfo
        "morfCont", Scenarios.morfCont
        "tpn", Scenarios.tpn
        "tpnComplete", Scenarios.tpnComplete
    ]


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

let mL = Units.Volume.milliLiter

let pruneValues incr n values =
    let (ValueSet vu) =
        mL |> ValueUnit.withValue values |> ValueSet.create |> ValueSetFix.prune incr n

    vu |> ValueUnit.getValue


let tests =
    testList
        "issue 1302"
        [
            testList
                "ValueSet.prune compares the increment in the unit of the values"
                [
                    test "10..0.5..20 mL pruned to 20 keeps the whole-millilitre grid" {
                        let incr = mL |> ValueUnit.singleWithValue (1N / 2N) |> Some

                        [| 10N .. (1N / 2N) .. 20N |]
                        |> pruneValues incr 20
                        |> Expect.equal "every whole mL from 10 to 20" [| 10N .. 20N |]
                    }

                    test "the original prune kept multiples of 1.5 mL and 10.5 mL" {
                        let incr = mL |> ValueUnit.singleWithValue (1N / 2N) |> Some

                        let (ValueSet vu) =
                            mL
                            |> ValueUnit.withValue [| 10N .. (1N / 2N) .. 20N |]
                            |> ValueSet.create
                            |> ValueSet.prune incr 20

                        vu
                        |> ValueUnit.getValue
                        |> Expect.equal
                            "the logged NaCl set"
                            [| 10N; 21N / 2N; 12N; 27N / 2N; 15N; 33N / 2N; 18N; 39N / 2N; 20N |]
                    }

                    test "an increment given in litres prunes as the same increment in millilitres" {
                        let inL = Units.Volume.liter |> ValueUnit.singleWithValue (5N / 1000N) |> Some
                        let inMl = mL |> ValueUnit.singleWithValue 5N |> Some
                        let values = [| 5N .. 5N .. 1000N |]

                        values
                        |> pruneValues inL 50
                        |> Expect.equal "same result" (values |> pruneValues inMl 50)
                    }

                    test "an off-grid min and max are dropped and the count stays within n" {
                        let incr = mL |> ValueUnit.singleWithValue 5N |> Some

                        [| yield 3N; yield! [| 5N .. 5N .. 100N |]; yield 107N |]
                        |> pruneValues incr 5
                        |> Expect.equal "multiples of 20 mL" [| 20N; 40N; 60N; 80N; 100N |]
                    }

                    test "the ends are kept only when fewer than two values would remain" {
                        let incr = mL |> ValueUnit.singleWithValue 1N |> Some

                        [| 3N; 4N; 5N; 6N; 7N |]
                        |> pruneValues incr 2
                        |> Expect.equal "the two multiples of 2, ends dropped" [| 4N; 6N |]

                        [| 3N; 4N; 5N |]
                        |> pruneValues incr 2
                        |> Expect.equal "one multiple of 2 only, so the ends come back" [| 3N; 4N; 5N |]
                    }
                ]

            testList
                "Orderable.increaseQuantityIncrement harmonizes to the grid the total needs"
                [
                    test "the diluent gets the 1 mL grid of the total, not the 0.5 mL of the powder" {
                        let ord =
                            amoxClavOrder ()
                            |> Order.applyConstraints
                            |> Order.calcMinMax logger
                            |> Result.bind (OrderFix.increaseIncrements logger 10 10)
                            |> Result.defaultWith (fun (o, _) -> o)

                        let incrOf name =
                            (ord |> findComponent name).OrderableQuantity
                            |> Quantity.toOrdVar
                            |> valueRangeOf
                            |> ValueRange.getIncr
                            |> Option.map (Increment.toValueUnit >> ValueUnit.getValue)

                        incrOf nacl |> Expect.equal "NaCl increment" (Some [| 1N |])
                        incrOf cmpName |> Expect.equal "powder increment" (Some [| 1N |])

                        ord.Orderable.OrderableQuantity
                        |> Quantity.toOrdVar
                        |> valueRangeOf
                        |> ValueRange.getIncr
                        |> Option.map (Increment.toValueUnit >> ValueUnit.getValue)
                        |> Expect.equal "orderable increment" (Some [| 1N |])
                    }

                    test "the original left the diluent at 0.5 mL, 21 values for a cap of 10" {
                        let ord =
                            amoxClavOrder ()
                            |> Order.applyConstraints
                            |> Order.calcMinMax logger
                            |> Result.bind (Order.increaseIncrements logger 10 10)
                            |> Result.defaultWith (fun (o, _) -> o)

                        (ord |> findComponent nacl).OrderableQuantity
                        |> Quantity.toOrdVar
                        |> valueRangeOf
                        |> ValueRange.getIncr
                        |> Option.map (Increment.toValueUnit >> ValueUnit.getValue)
                        |> Expect.equal "NaCl increment" (Some [| 1N / 2N |])
                    }
                ]

            testList
                "the scenario"
                [
                    let solved name pipeline =
                        match amoxClavOrder () |> pipeline with
                        | Ok ord -> ord
                        | Error(_, msgs) -> failtest $"%s{name} pipeline failed: %A{msgs}"

                    test "the original pipeline offers concentrations the order cannot reach" {
                        solved "original" original
                        |> unreachablePicks
                        |> Array.isEmpty
                        |> Expect.isFalse "unreachable picks exist"
                    }

                    test "with both fixes every offered amoxicilline concentration solves" {
                        solved "fixed" fixed'
                        |> unreachablePicks
                        |> Array.map snd
                        |> Expect.equal "no unreachable picks" [||]
                    }

                    test "with both fixes the dialog still offers more than one concentration" {
                        (solved "fixed" fixed' |> offeredConcentrations |> Array.length) > 1
                        |> Expect.isTrue "a choice remains"
                    }

                    test "with both fixes the amoxicilline list is reachable, the other lists are reported" {
                        solved "fixed" fixed'
                        |> unreachablePicks
                        |> Array.map snd
                        |> Expect.equal "no unreachable amoxicilline pick" [||]
                    }

                    test "with both fixes the diluent keeps a whole-millilitre grid without holes" {
                        let xs =
                            (solved "fixed" fixed' |> findComponent nacl).OrderableQuantity
                            |> Quantity.toOrdVar
                            |> valuesOf

                        xs
                        |> Expect.equal "every whole mL from min to max" [| Array.min xs .. Array.max xs |]
                    }
                ]

            testList
                "the fixtures under fixes 1 to 3"
                [
                    for name, med in fixtures do
                        test $"%s{name}: the pipeline solves within a few seconds" {
                            match med |> solvedFixture with
                            | Error e -> failtest e
                            | Ok(ms, _) -> ms < 3000L |> Expect.isTrue $"%i{ms} ms"
                        }

                        test $"%s{name}: every pick of every pickable list solves" {
                            match med |> solvedFixture with
                            | Error e -> failtest e
                            | Ok(_, ord) -> ord |> unreachableAnyPick Order.solveOrder |> Expect.equal "no unreachable picks" []
                        }
                ]

            testList
                "fix 4, the tie equations"
                [
                    let solveBoth = TieFix.solveOrder bothTies shareOvar

                    test "amox/clav: the item tie alone makes the clavulaanzuur list a tenth of the amoxicilline list" {
                        match amoxClavMedication () |> solvedFixtureWith tiedPipeline with
                        | Error e -> failtest e
                        | Ok(_, ord) ->
                            let conc name =
                                (ord |> findComponent cmpName).Items
                                |> List.find (fun i -> i.Name |> Name.toString = name)
                                |> _.OrderableConcentration
                                |> Concentration.toOrdVar
                                |> valuesOf

                            conc "clavulaanzuur"
                            |> Expect.equal "a tenth" (conc "amoxicilline" |> Array.map (fun c -> c / 10N))
                    }

                    test "amox/clav: with both ties every pick of every pickable list solves" {
                        match amoxClavMedication () |> solvedFixtureWith bothPipeline with
                        | Error e -> failtest e
                        | Ok(_, ord) ->
                            ord |> unreachableAnyPick solveBoth |> Expect.equal "no unreachable picks" []
                            (ord |> pickables |> List.length) > 1 |> Expect.isTrue "lists remain"
                    }

                    for name, med in fixtures do
                        test $"%s{name}: with both ties the pipeline solves within a few seconds" {
                            match med |> solvedFixtureWith bothPipeline with
                            | Error e -> failtest e
                            | Ok(ms, _) -> ms < 3000L |> Expect.isTrue $"%i{ms} ms"
                        }

                        test $"%s{name}: with both ties every pick of every pickable list solves" {
                            match med |> solvedFixtureWith bothPipeline with
                            | Error e -> failtest e
                            | Ok(_, ord) -> ord |> unreachableAnyPick solveBoth |> Expect.equal "no unreachable picks" []
                        }
                ]
        ]


amoxClavOrder () |> original |> report "original"
amoxClavOrder () |> fixed' |> report "fixed"

printfn "\n== every pickable list under the fixes, current loop order =="
amoxClavMedication () |> reportFixture "amox/clav"

for name, med in fixtures do
    reportFixture name med

printfn "\n== with the tie equation [itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc =="
amoxClavMedication () |> reportTied "amox/clav"

for name, med in fixtures do
    reportTied name med

printfn "\n== with both ties, the item tie and [orb]_orb_cnc = sum([cmp]_orb_cnc) with [orb]_orb_cnc = 1 =="
amoxClavMedication () |> reportBoth "amox/clav"

for name, med in fixtures do
    reportBoth name med

runTestsWithCLIArgs [] [||] tests
