// Issue #1302: the dose dialog offers values the order cannot reach.
//
// The first pull request moved the fixes of the count cap into the source:
//
// - ValueSet.prune keeps the multiples of the finest rounded step that fits the cap, from a step
//   list the caller gives (Order.pruneSteps: 0.1;0.5;1;5;10;50;100;500 in mL and mL/uur), and 1
//   then multiples of 1 for a unit group without steps; off-grid ends are dropped.
// - ValueRange.increaseIncrement leaves a range that already fits the cap.
// - Orderable.increaseQuantityIncrement gives the orderable quantity and every component one
//   increment, the finest rounded step on which every component fits the cap, else the coarsest on
//   which every quantity keeps two values.
// - The dose quantity is raised and pruned like a component quantity when the dose count is a range.
// - The time of a timed order is left free, so the rate determines it.
//
// This script keeps what the second pull request still needs, measured on the compiled source of
// the first:
//
// - Fix 4, the tie equations: an item's orderable concentration is its component concentration
//   times the component's share, and the shares add up to one. Without them the item
//   concentrations of a two-component order are cross products: amfo offers 1,021 of them, 893
//   unreachable.
// - The caps as a parameter (ValueCaps), so tests can lower them.
// - The amox/clav fixture from the log of 2026-10-11 and a prednisolon tablet fixture, and the
//   helpers that pick every offered value and time the pipeline and every pick.
//
// With more than two components the item concentrations are not offered as picks (#1421). The
// earlier prototypes are in the version of this script merged with the plan (#1420).

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
module Quantity = Informedica.GenOrder.Lib.OrderVariable.Quantity
module Rate = Informedica.GenOrder.Lib.OrderVariable.Rate
module Concentration = Informedica.GenOrder.Lib.OrderVariable.Concentration
module Name = Informedica.GenSolver.Lib.Variable.Name
module Schedule = Informedica.GenOrder.Lib.Order.Schedule


let logger = Informedica.GenOrder.Lib.Logging.noOp


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
// The caps as a parameter, for Order.minIncrMaxToValues: the loop of the source with the numbers
// in its body taken from a ValueCaps record.
// ---------------------------------------------------------------------------------------------

module CapsFix =

    open Informedica.GenOrder.Lib.Order

    /// The maximum number of values a pruned variable keeps, per schedule. Max is the cap with
    /// useMaxNumberOfValues; Few is an order with at most two components, Many one with more.
    type ValueCaps =
        {
            /// Continuous order.
            Continuous: int
            /// Continuous order with useMaxNumberOfValues.
            ContinuousMax: int
            /// Once or discontinuous order.
            Discontinuous: int
            /// Once or discontinuous order with useMaxNumberOfValues and a single component.
            DiscontinuousMaxSingle: int
            /// Once or discontinuous order with useMaxNumberOfValues and more than one component.
            DiscontinuousMaxMultiple: int
            /// Timed order with at most two components.
            TimedFew: int
            /// Timed order with at most two components and useMaxNumberOfValues.
            TimedMaxFew: int
            /// Timed order with more than two components, with or without useMaxNumberOfValues.
            TimedMany: int
        }


    /// The caps Order.minIncrMaxToValues uses today.
    let defaultCaps =
        {
            Continuous = 100
            ContinuousMax = 1_000
            Discontinuous = 10
            DiscontinuousMaxSingle = 100
            DiscontinuousMaxMultiple = 20
            TimedFew = 20
            TimedMaxFew = 10
            TimedMany = 5
        }


    /// The same cap for every schedule.
    let allCaps n =
        {
            Continuous = n
            ContinuousMax = n
            Discontinuous = n
            DiscontinuousMaxSingle = n
            DiscontinuousMaxMultiple = n
            TimedFew = n
            TimedMaxFew = n
            TimedMany = n
        }


    /// The cap for an order.
    let capOf caps useMaxNumberOfValues (ord: Order) =
        let count = ord.Orderable.Components |> List.length

        match ord.Schedule with
        | Continuous _ -> if useMaxNumberOfValues then caps.ContinuousMax else caps.Continuous
        | Once
        | Discontinuous _ ->
            if not useMaxNumberOfValues then caps.Discontinuous
            elif count > 1 then caps.DiscontinuousMaxMultiple
            else caps.DiscontinuousMaxSingle
        | OnceTimed _
        | Timed _ ->
            if count > 2 then caps.TimedMany
            elif useMaxNumberOfValues then caps.TimedMaxFew
            else caps.TimedFew


    /// Order.minIncrMaxToValues with the caps and the solve given; the rest is the source.
    let minIncrMaxToValuesWith caps solveOrder useMaxNumberOfValues skipRate logger (ord: Order) =
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

                        ovar
                        |> OrderVariable.minIncrMaxToValues pruneSteps (ord |> capOf caps useMaxNumberOfValues |> Some)
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
                    | Error _ -> ord

        ord
        |> loop
        |> fun ord ->
            if not isSolved then
                ord |> solveOrder "Min Incr Max to Values final Loop" true logger |> Result.defaultValue ord
            else
                ord


// ---------------------------------------------------------------------------------------------
// The fixtures and the pipelines
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


/// Every offered concentration tried: the ones whose pick fails to solve.
let unreachablePicks ord =
    ord
    |> offeredConcentrations
    |> Array.mapi (fun nth c -> nth, c)
    |> Array.filter (fun (nth, _) -> ord |> pickConcentration nth |> Result.isError)


/// The golden pipeline of the compiled source: the four commands through OrderProcessor.
let compiledPipeline (ord: Order) =
    let run cmd ord =
        match ord |> cmd |> OrderProcessor.processPipeline logger with
        | Ok ord -> Ok ord
        | Error(ord, msgs) -> Error(ord, msgs)

    ord
    |> run CalcMinMax
    |> Result.bind (run IncreaseIncrements)
    |> Result.bind (run CalcValues)
    |> Result.bind (run SolveOrder)


/// The pipeline of OrderProcessor, CalcMinMax, the client's CalcValues for two components or less,
/// the reset and the final solve, with the min-max solve, the values step, the norm dose and the
/// final solve given, so the tie equations can be added.
let pipelineWith calcMinMax minIncrMaxToValues solveNormDose solveOrder (ord: Order) =
    let twoOrLess = ord.Orderable.Components |> List.length <= 2

    let hasTimeNotContinuous =
        ord.Schedule |> Schedule.hasTime && ord.Schedule |> Schedule.isContinuous |> not

    let toValues useMax skipRate ord =
        ord |> minIncrMaxToValues useMax skipRate logger

    ord
    |> Order.applyConstraints
    |> calcMinMax logger
    |> Result.bind (Order.increaseIncrements logger 10 10)
    |> Result.map Order.setCalculatedConstraints
    |> Result.bind (fun ord ->
        if ord |> Order.hasNormDose && twoOrLess then
            ord |> toValues true hasTimeNotContinuous |> solveNormDose logger
        else
            Ok ord
    )
    |> Result.map (fun ord ->
        if twoOrLess then
            ord
            |> toValues false hasTimeNotContinuous
            |> fun ord -> if hasTimeNotContinuous then ord |> toValues false false else ord
        else
            ord
    )
    |> Result.map Order.applyCalculatedConstraints
    |> Result.map (toValues twoOrLess hasTimeNotContinuous)
    |> Result.map (fun ord -> if hasTimeNotContinuous then ord |> toValues twoOrLess false else ord)
    |> Result.bind (solveOrder "final-solve" false logger)


let noOvars (_: Order) : OrderVariable list = []

let itemTie = [ TieFix.tieEquation ]

let bothTies = [ TieFix.tieEquation; TieFix.shareSumEquation ]

let shareOvar (ord: Order) = [ TieFix.orderableShare ord ]

let solveBoth = TieFix.solveOrder bothTies shareOvar


/// The tied pipeline with the ties, the extra variables and the caps given.
let tiedPipelineWith caps extra extraOvars =
    let solve = TieFix.solveOrder extra extraOvars

    pipelineWith
        (TieFix.calcMinMax extra extraOvars)
        (CapsFix.minIncrMaxToValuesWith caps solve)
        (TieFix.solveNormDose extra extraOvars)
        solve


let itemTiePipeline = tiedPipelineWith CapsFix.defaultCaps itemTie noOvars

let bothPipelineWith caps = tiedPipelineWith caps bothTies shareOvar

let bothPipeline = bothPipelineWith CapsFix.defaultCaps


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


/// Every value of every list in lists picked and solved: the name and value of the ones that fail.
let unreachableIn lists solveOrder (ord: Order) =
    [
        for ovar in ord |> lists do
            for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                let picked = ovar |> OrderVariable.setNthValue nth

                match ord |> Order.fromOrdVars [ picked ] |> solveOrder "pick" false logger with
                | Ok _ -> ()
                | Error _ -> yield ovar |> OrderVariable.getName |> Name.toString, (ovar |> valuesOf)[nth]
    ]


/// The short name of a variable: the last name part and the variable kind.
let shortName (name: string) =
    let parts = name.TrimStart('[').Split("]_")
    let path = parts[0].Split('.')
    $"%s{path[path.Length - 1]}_%s{parts[1]}"


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


let amoxClavMedication () =
    amoxClavText |> Medication.fromString |> Result.defaultWith (fun e -> failwith (String.concat "\n" e))


/// A tablet dividable in quarters: 1 - 2 mg/kg for 30 kg of 5 mg tablets is 6 to 12 tablets on a
/// quarter grid, 25 values.
let prednisolonText =
    """
Id: 2b1e1c1a-0000-4000-8000-000000001302
Name: prednisolon
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 30 kg
Frequencies: 1 x/dag
Time:
Dose:
Div:
DoseCount: 1 x
Components:
	Name: prednisolon
	Form: tablet
	Quantities: 1 stuk
	Divisible: 4
	Dose:
	Solution:
	Substances:

		Name: prednisolon
		Concentrations: 5 mg/stuk
		Dose: prednisolon, [qty-adj] 1 - 2 mg/kg/dosis
		Solution:
"""


let prednisolon () =
    prednisolonText |> Medication.fromString |> Result.defaultWith (fun e -> invalidOp (String.concat "\n" e))


/// Every fixture, the single-component ones included.
let allFixtures () =
    [
        "amox/clav", amoxClavMedication ()
        "amfo", Scenarios.amfo
        "morfCont", Scenarios.morfCont
        "tpn", Scenarios.tpn
        "tpnComplete", Scenarios.tpnComplete
        "pcmSupp", Scenarios.pcmSupp
        "pcmDrink", Scenarios.pcmDrink
        "cotrim", Scenarios.cotrim
        "prednisolon", prednisolon ()
    ]


/// The lists the dialog must offer as picks. With more than two components only the component
/// quantities and the rate; the item concentrations have no use as picks there.
let pickablesFor (ord: Order) =
    if ord.Orderable.Components |> List.length > 2 then
        [
            for cmp in ord.Orderable.Components do
                cmp.OrderableQuantity |> Quantity.toOrdVar
            ord.Orderable.Dose.Rate |> Rate.toOrdVar
        ]
        |> List.filter (fun ovar -> ovar |> valuesOf |> Array.length > 1)
    else
        ord |> pickables


/// The unreachable picks of the lists the dialog must offer: with more than two components the item
/// concentrations are not offered.
let unreachableOffered solveOrder (ord: Order) =
    let offered =
        ord |> pickablesFor |> List.map (OrderVariable.getName >> Name.toString)

    ord
    |> unreachableAnyPick solveOrder
    |> List.filter (fun (n, _) -> offered |> List.contains n)


/// The component quantities and the rate that hold more than one value: the picks the first pull
/// request makes reachable without the tie equations.
let quantityAndRatePicks (ord: Order) =
    [
        for cmp in ord.Orderable.Components do
            cmp.OrderableQuantity |> Quantity.toOrdVar
        ord.Orderable.Dose.Rate |> Rate.toOrdVar
    ]
    |> List.filter (fun ovar -> ovar |> valuesOf |> Array.length > 1)


/// Every pick of every list the dialog must offer, solved and timed: the number of picks, the
/// number that fail and the slowest in ms.
let timedPicksWith lists solveOrder (ord: Order) =
    let results =
        [
            for ovar in ord |> lists do
                for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                    let picked = ovar |> OrderVariable.setNthValue nth
                    let sw = System.Diagnostics.Stopwatch.StartNew()
                    let ok = ord |> Order.fromOrdVars [ picked ] |> solveOrder "pick" false logger |> Result.isOk
                    sw.Stop()
                    ok, sw.ElapsedMilliseconds
        ]

    results.Length,
    results |> List.filter (fst >> not) |> List.length,
    results |> List.map snd |> List.fold max 0L


/// The pipeline time, the picks, the failing picks and the slowest pick of a fixture.
type Measured =
    {
        PipelineMs: int64
        Picks: int
        Failing: int
        SlowestPickMs: int64
        Order: Order
    }


let measure pipeline lists solveOrder (med: Medication) =
    med
    |> solvedFixtureWith pipeline
    |> Result.map (fun (ms, ord) ->
        let picks, failing, slowest = ord |> timedPicksWith lists solveOrder

        {
            PipelineMs = ms
            Picks = picks
            Failing = failing
            SlowestPickMs = slowest
            Order = ord
        }
    )


/// Every fixture measured once: the compiled pipeline with the quantity and rate picks, and the
/// pipeline with both ties with every pick the dialog offers.
let measured =
    lazy
        (allFixtures ()
         |> List.map (fun (name, med) ->
             name,
             (med |> measure compiledPipeline quantityAndRatePicks Order.solveOrder,
              med |> measure bothPipeline pickablesFor solveBoth)
         )
         |> Map.ofList)


/// The user waits for the pipeline once and for every pick.
let maxPipelineMs = 3000L

let maxPickMs = 1000L


let fmtValues (xs: BigRational[]) =
    xs |> Array.map (fun x -> (BigRational.toDouble x).ToString("0.##")) |> String.concat ";"


/// The component quantities of a solved order.
let componentValues (ord: Order) =
    ord.Orderable.Components |> List.map (_.OrderableQuantity >> Quantity.toOrdVar >> valuesOf)


let report () =
    for name, (compiled, tied) in measured.Value |> Map.toList do
        for label, m in [ "compiled", compiled; "both ties", tied ] do
            match m with
            | Error e -> printfn $"%-12s{name} %-10s{label}: %s{e}"
            | Ok m ->
                printfn
                    $"%-12s{name} %-10s{label}: pipeline %5i{m.PipelineMs} ms, %4i{m.Picks} picks, %4i{m.Failing} failing, slowest pick %4i{m.SlowestPickMs} ms"


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

let tests =
    testList
        "issue 1302"
        [
            testList
                "the compiled pipeline of the first pull request"
                [
                    test "amox/clav: every offered amoxicilline concentration solves" {
                        match amoxClavOrder () |> compiledPipeline with
                        | Error(_, msgs) -> failtest $"%A{msgs}"
                        | Ok ord ->
                            ord |> unreachablePicks |> Array.map snd |> Expect.equal "no unreachable picks" [||]
                            (ord |> offeredConcentrations |> Array.length) > 1 |> Expect.isTrue "a choice remains"
                    }

                    test "amox/clav: the diluent keeps a whole-millilitre grid without holes" {
                        match amoxClavOrder () |> compiledPipeline with
                        | Error(_, msgs) -> failtest $"%A{msgs}"
                        | Ok ord ->
                            let xs = (ord |> findComponent nacl).OrderableQuantity |> Quantity.toOrdVar |> valuesOf

                            xs |> Expect.equal "every whole mL from min to max" [| Array.min xs .. Array.max xs |]
                    }

                    for name, _ in allFixtures () do
                        test $"%s{name}: the pipeline takes at most %i{maxPipelineMs} ms" {
                            match measured.Value[name] |> fst with
                            | Error e -> failtest e
                            | Ok m -> m.PipelineMs <= maxPipelineMs |> Expect.isTrue $"%i{m.PipelineMs} ms"
                        }

                        test $"%s{name}: every quantity and rate pick solves within %i{maxPickMs} ms" {
                            match measured.Value[name] |> fst with
                            | Error e -> failtest e
                            | Ok m ->
                                m.Failing |> Expect.equal "no failing picks" 0
                                m.SlowestPickMs <= maxPickMs |> Expect.isTrue $"slowest %i{m.SlowestPickMs} ms"
                        }
                ]

            testList
                "fix 4, the tie equations"
                [
                    test "amfo: without the ties most item concentrations are unreachable" {
                        match Scenarios.amfo |> solvedFixtureWith compiledPipeline with
                        | Error e -> failtest e
                        | Ok(_, ord) ->
                            (ord |> unreachableIn pickables Order.solveOrder |> List.length) > 0
                            |> Expect.isTrue "unreachable item concentrations"
                    }

                    test "amox/clav: the item tie makes the clavulaanzuur list a tenth of the amoxicilline list" {
                        match amoxClavMedication () |> solvedFixtureWith itemTiePipeline with
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

                    for name, _ in allFixtures () do
                        test $"%s{name}: with both ties the pipeline takes at most %i{maxPipelineMs} ms" {
                            match measured.Value[name] |> snd with
                            | Error e -> failtest e
                            | Ok m -> m.PipelineMs <= maxPipelineMs |> Expect.isTrue $"%i{m.PipelineMs} ms"
                        }

                        test $"%s{name}: with both ties every offered pick solves within %i{maxPickMs} ms" {
                            match measured.Value[name] |> snd with
                            | Error e -> failtest e
                            | Ok m ->
                                m.Failing |> Expect.equal "no failing picks" 0
                                m.SlowestPickMs <= maxPickMs |> Expect.isTrue $"slowest %i{m.SlowestPickMs} ms"
                        }
                ]

            testList
                "the caps are parameters"
                [
                    let componentsUnder caps med =
                        match med |> solvedFixtureWith (bothPipelineWith caps) with
                        | Error e -> failtest e
                        | Ok(ms, ord) -> ms, ord |> componentValues

                    test "prednisolon under today's caps keeps all 25 quarter tablets" {
                        prednisolon ()
                        |> componentsUnder CapsFix.defaultCaps
                        |> snd
                        |> Expect.equal "6 to 12 by 1/4" [ [| 6N .. (1N / 4N) .. 12N |] ]
                    }

                    test "prednisolon with a single-component cap of 10 keeps the whole tablets" {
                        prednisolon ()
                        |> componentsUnder { CapsFix.defaultCaps with DiscontinuousMaxSingle = 10 }
                        |> snd
                        |> Expect.equal "6 to 12" [ [| 6N .. 12N |] ]
                    }

                    test "prednisolon with a single-component cap of 5 keeps every second tablet" {
                        prednisolon ()
                        |> componentsUnder { CapsFix.defaultCaps with DiscontinuousMaxSingle = 5 }
                        |> snd
                        |> Expect.equal "6 to 12 by 2" [ [| 6N .. 2N .. 12N |] ]
                    }

                    for cap in [ 5; 3 ] do
                        for name, med in allFixtures () do
                            test $"%s{name}: with every cap at %i{cap} no component keeps more than %i{cap} values" {
                                let ms, cmps = med |> componentsUnder (CapsFix.allCaps cap)

                                cmps |> List.iter (fun vs -> vs.Length <= cap |> Expect.isTrue $"%s{fmtValues vs}")
                                ms <= maxPipelineMs |> Expect.isTrue $"%i{ms} ms"
                            }
                ]
        ]


report ()

runTestsWithCLIArgs [] [||] tests
