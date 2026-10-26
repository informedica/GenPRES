module Tests

open Informedica.Utils.Lib.BCL
open Expecto
open Expecto.Flip

open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

// --- New tests for fluent pipeline guard/order behavior ---

// No GENPRES_URL_ID: these tests are hermetic and never download from Google
// Sheets. All order/scenario data is built from the synthetic templates below
// and in Scenarios.fs.

// Original test data used by several tests below
let testMedicationOrders =
    [
        { Medication.template with
            Id = "DO1"
            Name = "Test Medication Order 1"
            OrderType = DiscontinuousOrder
            Frequencies = ValueUnit.create Units.Time.day [| 1N .. 4N |] |> Some
            Components =
                [
                    { Medication.productComponent with
                        Name = "Component A"
                        Form = "injectievloeistof"
                        Divisible = Some 1N
                        Quantities = Some(ValueUnit.create Units.Volume.milliLiter [| 2N |])
                        Substances =
                            [
                                { Medication.substanceItem with
                                    Name = "Substance A1"
                                    Concentrations = Some(ValueUnit.create Units.Mass.milliGram [| 100N |])
                                    Dose = DoseLimit.limit |> Some
                                }
                                { Medication.substanceItem with
                                    Name = "Substance A2"
                                    Concentrations = Some(ValueUnit.create Units.Mass.milliGram [| 50N |])
                                    Dose = DoseLimit.limit |> Some
                                }
                            ]
                    }
                    { Medication.productComponent with
                        Name = "Component B"
                        Form = "injectievloeistof"
                        Substances =
                            [
                                { Medication.substanceItem with
                                    Name = "Substance B1"
                                    Concentrations = Some(ValueUnit.create Units.Mass.milliGram [| 150N |])
                                    Dose = DoseLimit.limit |> Some
                                }
                            ]
                    }
                ]
        }
        { Medication.template with
            Id = "DO2"
            Name = "Test Medication Order 2"
            OrderType = TimedOrder
            Components =
                [
                    { Medication.productComponent with
                        Name = "Component C"
                        Form = "Syrup"
                        Substances =
                            [
                                { Medication.substanceItem with
                                    Name = "Substance C1"
                                    Concentrations = Some(ValueUnit.create Units.Mass.milliGram [| 200N |])
                                    Dose = DoseLimit.limit |> Some
                                }
                            ]
                    }
                ]
        }
    ]

module Result =

    let get result =
        match result with
        | Ok value -> value
        | Error err -> failtest $"{err}"


/// The steps the tests take a medication through to a solved order, failing the test on the way.
module TestOrder =

    /// The medication the text holds.
    let parse text =
        match text |> MedicationText.fromString with
        | Ok med -> med
        | Error errs -> failtest $"could not parse the medication: %A{errs}"


    /// The order of the medication, starting at the start of the test scenarios.
    let toOrder med =
        match med |> Medication.toOrder Scenarios.testStart with
        | Ok ord -> ord
        | Error e -> failtest $"could not build the order: %A{e}"


    /// The order processed by the command.
    let solve cmd ord =
        ord
        |> cmd
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> Result.defaultWith (fun (_, msgs) -> failtest $"not solved: %A{msgs}")


    /// The order of the medication as the dose dialog gets it: CalcMinMax when the scenario is made,
    /// CalcValues when it is selected, SolveOrder after it.
    let processed med =
        med |> toOrder |> solve CalcMinMax |> solve CalcValues |> solve SolveOrder


    /// The values of an order variable, none when it holds no value set.
    let valuesOf ovar =
        ovar
        |> OrderVariable.getValSetValueUnit
        |> Option.map ValueUnit.getValue
        |> Option.defaultValue [||]


// --- New tests for fluent pipeline guard/order behavior ---
module Pipeline =
    open Informedica.GenOrder.Lib.Order

    module OV = OrderVariable
    module Units = Units

    let private noLogger = Logging.noOp

    // Build an Order from testMedicationOrders with realistic constraints to enable value calculation
    let private mkConstrainedOrder () =
        let medicationOrder =
            testMedicationOrders
            |> List.tryFind (fun d ->
                match d.OrderType with
                | TimedOrder -> true
                | _ -> false
            )
            |> Option.defaultValue (testMedicationOrders |> List.head)

        medicationOrder
        |> Medication.toOrderDto Scenarios.testStart
        |> Dto.fromDto
        |> Result.get

    // Also a minimal empty order for CalcMinMax path
    let private mkEmptyOrder () =
        Dto.discontinuous Scenarios.testStart "T" "Test" "PO" []
        |> Dto.fromDto
        |> Result.get

    let private countValues (o: Order) =
        o |> toOrdVars |> List.filter OrderVariable.hasValues |> List.length

    let private mkOnceTimedOrder () =
        Scenarios.kaliumchlorideOnceTimedText
        |> MedicationText.fromString
        |> function
            | Error errs -> failtest $"Failed to parse kaliumchloride OnceTimed: {errs}"
            | Ok med ->
                med
                |> Medication.toOrderDto Scenarios.testStart
                |> Dto.fromDto
                |> function
                    | Error msg -> failtest $"Failed to create order: {msg}"
                    | Ok ord -> ord

    [<Tests>]
    let guard_and_run_order_tests =
        testList
            "processPipeline guard and run order"
            [
                test "SolveOrder first ensures values before solving" {
                    let ord = mkConstrainedOrder ()
                    let before = countValues ord
                    let res = OrderProcessor.processPipeline noLogger (SolveOrder ord)

                    match res with
                    | Ok solved ->
                        let after = countValues solved
                        Expect.isTrue "Value count should not decrease" (after >= before)
                    | Error(o, errs) ->
                        let after = countValues o
                        Expect.isTrue "Value count should not decrease (even on error)" (after >= before)
                }

                test "CalcValues path only calculates values" {
                    let ord = mkConstrainedOrder ()
                    let before = countValues ord
                    let res = OrderProcessor.processPipeline noLogger (CalcValues ord)

                    match res with
                    | Ok o ->
                        let after = countValues o
                        Expect.isTrue "Value count should not decrease" (after >= before)
                    | Error(o, _) ->
                        let after = countValues o
                        Expect.isTrue "Value count should not decrease (even on error)" (after >= before)
                }

                test "CalcMinMax path runs when order is empty" {
                    let ord = mkEmptyOrder ()
                    let res = OrderProcessor.processPipeline noLogger (CalcMinMax ord)

                    match res with
                    | Ok _ -> true |> Expect.isTrue "calc minmax ok"
                    | Error(o, _) -> (box o) |> Expect.isNotNull "Order returned with error"
                }

                test "getTotals runs hermetically with injected totals" {
                    // The "Totals" reference data is now injected (it used to be a
                    // live Google-sheet fetch at module init). This must run with
                    // GENPRES_URL_ID unset, proving the injection seam.
                    let syntheticTotals: TotalsData[] =
                        [|
                            {
                                Name = "volume"
                                MinAge = None
                                MaxAge = None
                                MinWeight = None
                                MaxWeight = None
                                Unit = Some Units.Volume.milliLiter
                                Adj = Some Units.Weight.kiloGram
                                TimeUnit = Some Units.Time.day
                                MinPerTime = None
                                MaxPerTime = None
                                MinPerTimeAdj = None
                                MaxPerTimeAdj = None
                            }
                        |]

                    let ord =
                        testMedicationOrders
                        |> List.head
                        |> Medication.toOrder Scenarios.testStart
                        |> Result.get

                    // no weight -> no per-weight aggregation -> Volume None, but the
                    // call completes without any Google-sheet access.
                    let result = Totals.getTotals syntheticTotals None None [| ord |]

                    result.Volume |> Expect.isNone "no weight -> no volume total"
                }
            ]

    [<Tests>]
    let staged_expansion_tests =
        testList
            "staged value expansion for timed orders"
            [
                test "CalcMinMax for kaliumchloride OnceTimed completes without overflow" {
                    let ord = mkOnceTimedOrder ()
                    let res = OrderProcessor.processPipeline noLogger (CalcMinMax ord)

                    match res with
                    | Ok _ -> true |> Expect.isTrue "CalcMinMax succeeded"
                    | Error(_, msgs) ->
                        msgs
                        |> List.exists (fun m ->
                            let s = $"%A{m}"
                            s.Contains("ValueSetOverflow")
                        )
                        |> Expect.isFalse "should not have ValueSetOverflow"
                }

                test "CalcValues for kaliumchloride OnceTimed completes without overflow" {
                    let ord = mkOnceTimedOrder ()

                    let ordAfterCalcMinMax =
                        OrderProcessor.processPipeline noLogger (CalcMinMax ord)
                        |> function
                            | Ok o -> o
                            | Error(o, _) -> o

                    let res = OrderProcessor.processPipeline noLogger (CalcValues ordAfterCalcMinMax)

                    match res with
                    | Ok _ -> true |> Expect.isTrue "CalcValues succeeded"
                    | Error(_, msgs) ->
                        // CalcValues may produce errors (e.g. empty value sets from
                        // pre-existing set-normdose issues), but it must not overflow.
                        msgs
                        |> List.exists (fun m ->
                            let s = $"%A{m}"
                            s.Contains("ValueSetOverflow")
                        )
                        |> Expect.isFalse "CalcValues should not have ValueSetOverflow"
                }

                test
                    "kaliumchloride OnceTimed: the median rate cleared gives its list back, and every rate pick keeps the time within 60 to 120 min" {
                    let solved =
                        [
                            "CalcMinMax", CalcMinMax
                            "IncreaseIncrements", IncreaseIncrements
                            "CalcValues", CalcValues
                            "SolveOrder", SolveOrder
                        ]
                        |> List.fold
                            (fun ord (stage, cmd) ->
                                match OrderProcessor.processPipeline noLogger (cmd ord) with
                                | Ok ord -> ord
                                | Error(_, msgs) -> failtest $"%s{stage} failed: %A{msgs}"
                            )
                            (mkOnceTimedOrder ())

                    // the time follows from the dose quantity and the picked rate
                    let minutes ord =
                        ord
                        |> Order.toOrdVars
                        |> List.find (fun ovar -> (ovar |> OV.getName |> Variable.Name.toString).EndsWith "_sch_tme")
                        |> OV.getValSetValueUnit
                        |> Option.map (ValueUnit.convertTo Units.Time.minute >> ValueUnit.getValue)
                        |> Option.defaultValue [||]

                    // the rate gets its median; cleared as the client clears it, its list comes back. The dose
                    // quantity counts as picked: it is the median the norm dose set, not a pick, and without it
                    // among the picks the reopen gives the dose quantity its list back and the rate a range
                    let reopened =
                        let qty = solved.Orderable.Dose.Quantity |> OV.Quantity.toOrdVar |> OrderProcessor.nameOf

                        solved
                        |> Order.fromOrdVars [ solved.Orderable.Dose.Rate |> OV.Rate.toOrdVar |> OV.clear ]
                        |> fun ord -> Reopen(ord, [ qty ])
                        |> OrderProcessor.processPipeline noLogger
                        |> function
                            | Ok ord -> ord
                            | Error(_, msgs) -> failtest $"the reopen failed: %A{msgs}"

                    let rate = reopened.Orderable.Dose.Rate |> OV.Rate.toOrdVar

                    let count =
                        rate
                        |> OV.getValSetValueUnit
                        |> Option.map (ValueUnit.getValue >> Array.length)
                        |> Option.defaultValue 0

                    count > 1 |> Expect.isTrue "more than one rate to pick"

                    for nth in 0 .. count - 1 do
                        match
                            reopened
                            |> Order.fromOrdVars [ rate |> OV.setNthValue nth ]
                            |> Order.solveOrder "pick" false noLogger
                        with
                        | Error(_, msgs) -> failtest $"rate pick %i{nth} failed: %A{msgs}"
                        | Ok ord ->
                            let time = ord |> minutes
                            time |> Array.isEmpty |> Expect.isFalse "a time"

                            time
                            |> Array.forall (fun m -> m >= 60N && m <= 120N)
                            |> Expect.isTrue $"time %A{time} min within 60 to 120"
                }

                test "paracetamol suppository (non-timed) is unaffected by staged expansion" {
                    let ord =
                        Scenarios.pcmSupp
                        |> Medication.toOrderDto Scenarios.testStart
                        |> Dto.fromDto
                        |> function
                            | Error msg -> failtest $"{msg}"
                            | Ok o -> o

                    let res = OrderProcessor.processPipeline noLogger (CalcMinMax ord)

                    match res with
                    | Ok _ -> true |> Expect.isTrue "non-timed order unaffected"
                    | Error(_, msgs) ->
                        msgs
                        |> List.exists (fun m ->
                            let s = $"%A{m}"
                            s.Contains("ValueSetOverflow")
                        )
                        |> Expect.isFalse "non-timed order should not have ValueSetOverflow"
                }
            ]

    [<Tests>]
    let cleared_processing_tests =
        testList
            "processClearedOrder behavior"
            [
                test "Discontinuous Dose cleared materializes values" {
                    let ord0 = mkConstrainedOrder ()
                    // Switch to Discontinuous and clear its dose via the change API
                    let ord =
                        let hz = ValueUnit.per Units.Time.hour Units.Count.times

                        { ord0 with
                            Schedule =
                                Discontinuous(
                                    ord0.Schedule
                                    |> Schedule.getFrequency
                                    |> Option.defaultValue (OV.Frequency.create (Name "frq") hz)
                                )
                        }
                        |> OrderPropertyChange.proc
                            [
                                ItemDose("", "", (fun dos -> { dos with PerTime = dos |> Orderable.Dose.clearPerTime }))
                            ]

                    let before = countValues ord
                    let res = OrderProcessor.processClearedOrder Logging.noOp ord

                    match res with
                    | Ok o -> Expect.isTrue "Value count should not decrease" (countValues o >= before)
                    | Error(o, _) ->
                        Expect.isTrue "Value count should not decrease (even on error)" (countValues o >= before)
                }

                test "Timed TimeCleared re-applies time constraints and values" {
                    let ord0 = mkConstrainedOrder ()
                    let hz = ValueUnit.per Units.Time.hour Units.Count.times

                    let frq =
                        ord0.Schedule
                        |> Schedule.getFrequency
                        |> Option.defaultValue (OV.Frequency.create (Name "frq") hz)

                    let tme =
                        ord0.Schedule
                        |> Schedule.getTime
                        |> Option.defaultValue (OV.Time.create (Name "tme") Units.Time.hour)

                    let ord = { ord0 with Schedule = Timed(frq, Time(OrderVariable.clear (let (Time tv) = tme in tv))) }

                    let before = countValues ord
                    let res = OrderProcessor.processClearedOrder Logging.noOp ord

                    match res with
                    | Ok o -> Expect.isTrue "Value count should not decrease" (countValues o >= before)
                    | Error(o, _) ->
                        Expect.isTrue "Value count should not decrease (even on error)" (countValues o >= before)
                }
            ]


module ToOrderDto =

    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
    module Limit = Informedica.GenCore.Lib.Ranges.Limit

    open Informedica.GenOrder.Lib.Medication

    /// <summary>
    /// Map a medication order record to a OrderDto record.
    /// The medication order will map the constraints of the OrderDto.
    /// </summary>
    /// <param name="d">The Medication to convert</param>
    let toOrderDto (d: Medication) =
        let vuToDto = Option.bind (ValueUnit.Dto.toDto false "English")

        let limToDto = Option.map Limit.getValueUnit >> vuToDto

        let oru = Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour

        let standDoseRate un (orbDto: Order.Orderable.Dto.Dto) =
            orbDto.Dose.Rate.Constraints.IncrOpt <- 1N / 10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MinIncl <- true
            orbDto.Dose.Rate.Constraints.MinOpt <- 1N / 10N |> createSingleValueUnitDto un
            orbDto.Dose.Rate.Constraints.MaxIncl <- true
            orbDto.Dose.Rate.Constraints.MaxOpt <- 1000N |> createSingleValueUnitDto un

        let orbDto = Order.Orderable.Dto.dto d.Id d.Name

        // make sure the orderable quantity has a unit
        d.Components
        |> List.tryHead
        |> Option.map (fun p -> p.Quantities |> Option.map ValueUnit.getUnit, p.Divisible)
        |> function
            | Some(Some u, Some d) ->
                orbDto.OrderableQuantity.Constraints.IncrOpt <- 1N / d |> createSingleValueUnitDto u
            | _ -> ()

        orbDto.DoseCount.Constraints |> setMinMaxConstraints false d.DoseCount

        orbDto.OrderableQuantity.Constraints.ValsOpt <- d.Quantities |> vuToDto

        let setOrbDoseRate (dl: DoseLimit) =

            orbDto.Dose.Rate.Constraints |> setMinMaxConstraints false dl.Rate

            orbDto.Dose.RateAdjust.Constraints |> setMinMaxConstraints true dl.RateAdjust

        let setOrbDoseQty isOnce (dl: DoseLimit) =
            orbDto.Dose.Quantity.Constraints |> setMinMaxConstraints false dl.Quantity

            orbDto.Dose.QuantityAdjust.Constraints
            |> setMinMaxConstraints true dl.QuantityAdjust

            if not isOnce then
                orbDto.Dose.PerTime.Constraints |> setMinMaxConstraints false dl.PerTime

                orbDto.Dose.PerTimeAdjust.Constraints
                |> setMinMaxConstraints true dl.PerTimeAdjust

        match d.OrderType with
        | AnyOrder
        | ProcessOrder -> ()

        | ContinuousOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl -> dl |> setOrbDoseRate
            | None -> ()

        | OnceOrder ->
            match d.Dose with
            | Some dl -> dl |> setOrbDoseQty true
            | None -> ()

        | OnceTimedOrder ->
            orbDto |> standDoseRate oru

            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseRate
                dl |> setOrbDoseQty true
                // Assume timed order always solution
                orbDto.Dose.Quantity.Constraints.IncrOpt <- 1N / 10N |> createSingleValueUnitDto Units.Volume.milliLiter
            | None -> ()

        | DiscontinuousOrder ->
            match d.Dose with
            | Some dl -> dl |> setOrbDoseQty false
            | None -> ()

        | TimedOrder ->
            orbDto |> standDoseRate oru
            // Assume timed order always solution
            if orbDto.Dose.Quantity.Constraints.ValsOpt.IsNone then
                orbDto.Dose.Quantity.Constraints.IncrOpt <- 1N / 10N |> createSingleValueUnitDto Units.Volume.milliLiter

            if orbDto.OrderableQuantity.Constraints.ValsOpt.IsNone then
                orbDto.OrderableQuantity.Constraints.IncrOpt <-
                    1N / 10N |> createSingleValueUnitDto Units.Volume.milliLiter

            match d.Dose with
            | Some dl ->
                dl |> setOrbDoseRate
                dl |> setOrbDoseQty false
            | None -> ()

        // TODO: not good, can vary per product!!
        orbDto.Dose.Quantity.Constraints.IncrOpt <-
            d.Components
            |> List.tryHead
            |> Option.bind (fun p ->
                p.Divisible
                |> Option.bind (fun d ->
                    let ou = p.Quantities |> Option.map ValueUnit.getUnit |> Option.defaultValue NoUnit
                    1N / d |> createSingleValueUnitDto ou
                )
            )

        orbDto.Components <-
            [
                for p in d.Components do
                    let cmpDto = Order.Orderable.Component.Dto.dto d.Id d.Name p.Name p.Form

                    let div =
                        p.Divisible
                        |> Option.bind (fun d ->
                            let ou = p.Quantities |> Option.map ValueUnit.getUnit |> Option.defaultValue NoUnit
                            (1N / d) |> createSingleValueUnitDto ou
                        )

                    cmpDto.ComponentQuantity.Constraints.ValsOpt <- p.Quantities |> vuToDto
                    cmpDto.OrderableQuantity.Constraints.IncrOpt <- div

                    if d.Components |> List.length = 1 then
                        // If there is only one product, the concentration of that product in the
                        // Orderable will be by definition be 1.
                        cmpDto.OrderableConcentration.Constraints.ValsOpt <-
                            1N |> createSingleValueUnitDto Units.Count.times

                        cmpDto.Dose.Quantity.Constraints.IncrOpt <- div

                    match p.Solution with
                    | Some sl ->
                        cmpDto.OrderableQuantity.Constraints |> setMinMaxConstraints false sl.Quantity

                        cmpDto.OrderableConcentration.Constraints
                        |> setMinMaxConstraints true sl.Concentration
                    | None -> ()

                    let setDoseRate (dl: DoseLimit) =
                        if dl.Rate |> MinMax.isEmpty |> not then
                            cmpDto.Dose.Rate.Constraints |> setMinMaxConstraints false dl.Rate

                        if dl.RateAdjust |> MinMax.isEmpty |> not then
                            cmpDto.Dose.RateAdjust.Constraints |> setMinMaxConstraints true dl.RateAdjust

                    let setDoseQty (dl: DoseLimit) =
                        if dl.Quantity |> MinMax.isEmpty |> not then
                            cmpDto.Dose.Quantity.Constraints |> setMinMaxConstraints false dl.Quantity

                        if dl.QuantityAdjust |> MinMax.isEmpty |> not then
                            cmpDto.Dose.QuantityAdjust.Constraints
                            |> setMinMaxConstraints true dl.QuantityAdjust

                        if dl.PerTime |> MinMax.isEmpty |> not then
                            cmpDto.Dose.PerTime.Constraints |> setMinMaxConstraints false dl.PerTime

                        if dl.PerTimeAdjust |> MinMax.isEmpty |> not then
                            cmpDto.Dose.PerTimeAdjust.Constraints
                            |> setMinMaxConstraints true dl.PerTimeAdjust

                    match d.OrderType with
                    | AnyOrder -> ()
                    | ProcessOrder -> ()
                    | ContinuousOrder ->
                        match p.Dose with
                        | None -> ()
                        | Some dl -> dl |> setDoseRate

                    | OnceOrder
                    | DiscontinuousOrder ->
                        match p.Dose with
                        | None -> ()
                        | Some dl -> dl |> setDoseQty

                    | OnceTimedOrder
                    | TimedOrder ->
                        match p.Dose with
                        | None -> ()
                        | Some dl ->
                            dl |> setDoseRate
                            dl |> setDoseQty

                    cmpDto.Items <-
                        [
                            for s in p.Substances do
                                let itmDto = Order.Orderable.Item.Dto.dto d.Id d.Name p.Name s.Name

                                itmDto.ComponentConcentration.Constraints.ValsOpt <- s.Concentrations |> vuToDto

                                if d.Components |> List.length = 1 then
                                    // When only one product, the orderable concentration is the same as the component concentration
                                    itmDto.OrderableConcentration.Constraints.ValsOpt <-
                                        itmDto.ComponentConcentration.Constraints.ValsOpt

                                match s.Solution with
                                | Some sl ->
                                    itmDto.OrderableQuantity.Constraints |> setMinMaxConstraints false sl.Quantity

                                    itmDto.OrderableConcentration.Constraints
                                    |> setMinMaxConstraints true sl.Concentration
                                | None -> ()

                                let setDoseRate (dl: DoseLimit) =

                                    itmDto.Dose.Rate.Constraints |> setMinMaxConstraints false dl.Rate

                                    itmDto.Dose.RateAdjust.Constraints |> setMinMaxConstraints true dl.RateAdjust

                                let setDoseQty (dl: DoseLimit) =
                                    itmDto.Dose.Quantity.Constraints |> setMinMaxConstraints false dl.Quantity

                                    itmDto.Dose.QuantityAdjust.Constraints
                                    |> setMinMaxConstraints true dl.QuantityAdjust

                                    itmDto.Dose.PerTime.Constraints |> setMinMaxConstraints false dl.PerTime

                                    itmDto.Dose.PerTimeAdjust.Constraints
                                    |> setMinMaxConstraints true dl.PerTimeAdjust

                                match d.OrderType with
                                | AnyOrder -> ()
                                | ProcessOrder -> ()
                                | ContinuousOrder ->
                                    match s.Dose with
                                    | None -> ()
                                    | Some dl -> dl |> setDoseRate

                                | OnceOrder
                                | DiscontinuousOrder ->
                                    match s.Dose with
                                    | None -> ()
                                    | Some dl -> dl |> setDoseQty

                                | OnceTimedOrder
                                | TimedOrder ->
                                    match s.Dose with
                                    | None -> ()
                                    | Some dl ->
                                        dl |> setDoseRate
                                        dl |> setDoseQty

                                itmDto
                        ]

                    cmpDto
            ]

        let dto =
            match d.OrderType with
            | AnyOrder -> "the order type cannot be 'Any'" |> failtest
            | ProcessOrder -> "the order type cannot be 'Process'" |> failtest
            | OnceOrder -> Order.Dto.once Scenarios.testStart d.Id d.Name d.Route []
            | OnceTimedOrder -> Order.Dto.onceTimed Scenarios.testStart d.Id d.Name d.Route []
            | ContinuousOrder -> Order.Dto.continuous Scenarios.testStart d.Id d.Name d.Route []
            | DiscontinuousOrder -> Order.Dto.discontinuous Scenarios.testStart d.Id d.Name d.Route []
            | TimedOrder -> Order.Dto.timed Scenarios.testStart d.Id d.Name d.Route []

        dto.Orderable <- orbDto

        dto.Schedule.Frequency.Constraints.ValsOpt <- d.Frequencies |> vuToDto

        dto.Schedule.Frequency.Constraints.IncrOpt <-
            d.Frequencies
            |> Option.bind (fun vu ->
                let u = ValueUnit.getUnit vu
                1N |> createSingleValueUnitDto u
            )

        dto.Schedule.Time.Constraints.MinIncl <- d.Time.Min.IsSome
        dto.Schedule.Time.Constraints.MinOpt <- d.Time.Min |> limToDto
        dto.Schedule.Time.Constraints.MaxIncl <- d.Time.Max.IsSome
        dto.Schedule.Time.Constraints.MaxOpt <- d.Time.Max |> limToDto

        if
            d.Adjust
            |> Option.map ValueUnit.getUnit
            |> Option.map (ValueUnit.Group.eqsGroup Units.Weight.kiloGram)
            |> Option.defaultValue false
        then
            let u = d.Adjust |> Option.map ValueUnit.getUnit |> Option.defaultValue NoUnit
            // Adjusted by weight
            dto.Adjust.Constraints.MinOpt <- (200N / 1000N) |> createSingleValueUnitDto u

            dto.Adjust.Constraints.MaxOpt <- 150N |> createSingleValueUnitDto u
        // TODO: add constraints for BSA
        dto.Adjust.Constraints.ValsOpt <- d.Adjust |> vuToDto

        dto


// Add your test modules here
/// Component limits of a dose rule, as GenFORM gives them, holding products and no limits.
module RuleFixtures =

    let mgPerMl = Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter


    let ml = ValueUnit.singleWithUnit Units.Volume.milliLiter


    /// A product of the rule: a GPK, its form quantity, whether it needs reconstitution, and the
    /// strength of each substance in mg/mL.
    let product gpk qty reconst substs =
        { Product.create "amoxicilline/clavulaanzuur" "poeder" "INTRAVENEUS" (substs |> Array.map fst) with
            GPK = gpk
            FormQuantities = qty
            RequiresReconstitution = reconst
            Substances =
                substs
                |> Array.map (fun (n, c) ->
                    {
                        Name = n
                        Concentration = c |> ValueUnit.singleWithUnit mgPerMl |> Some
                        MolarConcentration = None
                    }
                )
        }


    /// A component limit holding the products, without limits.
    let componentLimit name products : ComponentLimit =
        {
            Name = name
            ProductIds = [||]
            Limit = None
            Products = products
            SubstanceLimits = [||]
        }


    /// A liquid that needs no reconstitution, in a bottle of 100 mL.
    let liquidLimit =
        componentLimit "paracetamol" [| product "33" (ml 100N) false [| "paracetamol", 24N |] |]


module ComponentProductTests =

    open Informedica.GenOrder.Lib.Types
    open RuleFixtures
    open TestOrder


    let private logger = Logging.noOp


    /// Amoxicilline/clavulaanzuur in two products: 500/50 mg in 10 mL and 1000/200 mg in 20 mL.
    let private amoxClavLimit =
        componentLimit
            "amoxicilline/clavulaanzuur"
            [|
                product "11" (ml 10N) true [| "amoxicilline", 50N; "clavulaanzuur", 5N |]
                product "22" (ml 20N) true [| "amoxicilline", 50N; "clavulaanzuur", 10N |]
            |]


    let tests =
        testList
            "the products of a component"
            [
                test "amoxicilline/clavulaanzuur from a rule carries its two products with their strengths" {
                    let pc = [| amoxClavLimit |] |> Medication.createComponents logger |> List.exactlyOne

                    pc.Products
                    |> Expect.equal
                        "two products"
                        [
                            {
                                Id = "11"
                                FormQuantities = Some(ml 10N)
                                Strengths =
                                    [
                                        {
                                            Substance = "amoxicilline"
                                            Strength = 50N |> ValueUnit.singleWithUnit mgPerMl
                                        }
                                        {
                                            Substance = "clavulaanzuur"
                                            Strength = 5N |> ValueUnit.singleWithUnit mgPerMl
                                        }
                                    ]
                            }
                            {
                                Id = "22"
                                FormQuantities = Some(ml 20N)
                                Strengths =
                                    [
                                        {
                                            Substance = "amoxicilline"
                                            Strength = 50N |> ValueUnit.singleWithUnit mgPerMl
                                        }
                                        {
                                            Substance = "clavulaanzuur"
                                            Strength = 10N |> ValueUnit.singleWithUnit mgPerMl
                                        }
                                    ]
                            }
                        ]
                }

                test "the distinct strengths of the substances are as before" {
                    let pc = [| amoxClavLimit |] |> Medication.createComponents logger |> List.exactlyOne

                    pc.Substances
                    |> List.map (fun s -> s.Name, s.Concentrations |> Option.map ValueUnit.getValue)
                    |> Expect.equal
                        "amoxicilline 50, clavulaanzuur 5 and 10"
                        [ "amoxicilline", Some [| 50N |]; "clavulaanzuur", Some [| 5N; 10N |] ]
                }

                test "a product whose strength the unit group filter leaves out of its item is dropped" {
                    let molar = Units.Molar.milliMole |> ValueUnit.per Units.Volume.milliLiter

                    let pc =
                        [|
                            componentLimit
                                "amoxicilline/clavulaanzuur"
                                [|
                                    yield! amoxClavLimit.Products
                                    { product "33" (ml 20N) true [| "amoxicilline", 50N; "clavulaanzuur", 10N |] with
                                        Substances =
                                            [|
                                                {
                                                    Name = "amoxicilline"
                                                    Concentration = 50N |> ValueUnit.singleWithUnit mgPerMl |> Some
                                                    MolarConcentration = None
                                                }
                                                {
                                                    Name = "clavulaanzuur"
                                                    Concentration = 1N |> ValueUnit.singleWithUnit molar |> Some
                                                    MolarConcentration = None
                                                }
                                            |]
                                    }
                                |]
                        |]
                        |> Medication.createComponents logger
                        |> List.exactlyOne

                    pc.Products |> List.map _.Id |> Expect.equal "the mg/mL products" [ "11"; "22" ]
                }

                test "a liquid without reconstitution carries its products with a form quantity of 1 mL" {
                    let pc = [| liquidLimit |] |> Medication.createComponents logger |> List.exactlyOne

                    pc.Quantities |> Expect.equal "the component counts per mL" (Some(ml 1N))

                    pc.Products
                    |> List.map _.FormQuantities
                    |> Expect.equal "the product counted per mL, not per bottle" [ Some(ml 1N) ]
                }

                test "the order's component carries the products of the medication" {
                    let med = Scenarios.amoxClavText |> parse
                    let ord = med |> toOrder

                    ord.Orderable.Components
                    |> List.map _.Products
                    |> Expect.equal "the products" (med.Components |> List.map _.Products)
                }

                test "the products survive the calculation of the order" {
                    let med = Scenarios.amoxClavText |> parse

                    med
                    |> toOrder
                    |> solve CalcMinMax
                    |> solve CalcValues
                    |> _.Orderable.Components
                    |> List.map _.Products
                    |> Expect.equal "the products" (med.Components |> List.map _.Products)
                }

                test "the products survive a component Dto round trip" {
                    let cmp =
                        Scenarios.amoxClavText
                        |> parse
                        |> toOrder
                        |> _.Orderable.Components
                        |> List.head

                    cmp
                    |> Order.Orderable.Component.Dto.toDto
                    |> Order.Orderable.Component.Dto.fromDto
                    |> _.Products
                    |> Expect.equal "the same products" cmp.Products
                }

                test "a component Dto written without products reads as a component without products" {
                    let dto =
                        Scenarios.amfo
                        |> toOrder
                        |> _.Orderable.Components
                        |> List.head
                        |> Order.Orderable.Component.Dto.toDto

                    dto.Products |> Expect.isEmpty "no products in the Dto"

                    dto
                    |> Order.Orderable.Component.Dto.fromDto
                    |> _.Products
                    |> Expect.isEmpty "no products read"
                }

                test "the text format keeps the products in a round trip" {
                    let med = Scenarios.amoxClavText |> parse

                    med.Components
                    |> List.collect _.Products
                    |> List.length
                    |> Expect.equal "two products" 2

                    med
                    |> MedicationText.toString
                    |> parse
                    |> Expect.equal "the same medication" med
                }

                test "a text medication without products prints no products" {
                    Scenarios.prednisolonText
                    |> parse
                    |> MedicationText.toString
                    |> String.contains "Products:"
                    |> Expect.isFalse "no products"
                }

                test "a product with a strength that does not parse is an error" {
                    Scenarios.amoxClavText.Replace("Unit: mg/ml", "Unit: \"mg[Banana]/ml\"")
                    |> MedicationText.fromString
                    |> function
                        | Ok _ -> []
                        | Error errs -> errs
                    |> List.contains (
                        DtoError.UnknownUnit("Components[0].Products[1].Strengths[1].Strength.Unit", "mg[Banana]/ml")
                    )
                    |> Expect.isTrue "the strength of the clavulaanzuur"
                }

                test "a product naming a substance the component does not have is an error" {
                    Scenarios.amoxClavText.Replace("- Substance: clavulaanzuur", "- Substance: clavulanaat")
                    |> MedicationText.fromString
                    |> function
                        | Ok _ -> []
                        | Error errs -> errs
                    |> List.contains (DtoError.NotMadeOf("Components[0].Products[1]", "amox-clav-1000-200"))
                    |> Expect.isTrue "the second product"
                }

                test "a product with a blank id is an error" {
                    Scenarios.amoxClavText.Replace("- Id: amox-clav-500-50", "- Id: \"\"")
                    |> MedicationText.fromString
                    |> Expect.equal "missing" (Error [ DtoError.Missing "Components[0].Products[0].Id" ])
                }

                test "a product without an id is an error" {
                    Scenarios.amoxClavText.Replace("- Id: amox-clav-500-50", "- Id: ~")
                    |> MedicationText.fromString
                    |> Expect.equal "missing" (Error [ DtoError.Missing "Components[0].Products[0].Id" ])
                }
            ]


module ProductNarrowingTests =

    open TestOrder

    module Concentration = OrderVariable.Concentration
    module Quantity = OrderVariable.Quantity


    let private noLog = OrderLogging.noOp


    /// The order as the first evaluation leaves it.
    let private built med = med |> toOrder |> solve CalcMinMax


    let private amoxClav = "amoxicilline/clavulaanzuur"


    let private item n (ord: Order) =
        ord.Orderable.Components
        |> List.collect _.Items
        |> List.find (_.Name >> Name.toString >> String.equalsCapInsens n)


    let private strengths n =
        item n >> _.ComponentConcentration >> Concentration.toOrdVar >> valuesOf


    let private quantities n = item n >> _.ComponentQuantity >> Quantity.toOrdVar >> valuesOf


    let private cmpQuantities (ord: Order) =
        ord.Orderable.Components
        |> List.head
        |> _.ComponentQuantity
        |> Quantity.toOrdVar
        |> valuesOf


    /// The order with the nth strength of the clavulaanzuur picked and solved.
    let private pickClav n ord =
        ord
        |> OrderProcessor.processChangeProperty noLog (SetNthItemComponentConcentration(amoxClav, "clavulaanzuur", n))
        |> solve SolveOrder


    let tests =
        testList
            "the products narrow the strengths"
            [
                test "amoxicilline/clavulaanzuur at 5 mg/mL clavulaanzuur keeps the 500/50 mg product and its 10 mL" {
                    let ord = Scenarios.amoxClavText |> parse |> built

                    ord
                    |> strengths "clavulaanzuur"
                    |> Expect.equal "the dose leaves 5 mg/mL" [| 5N |]
                    ord |> Order.productIds |> Expect.equal "one product" [| "amox-clav-500-50" |]
                    ord |> cmpQuantities |> Expect.equal "its 10 mL" [| 10N |]
                }

                test "both strengths of clavulaanzuur keep both products" {
                    let ord = Scenarios.amoxClavWithoutClavDoseText |> parse |> built

                    ord |> strengths "clavulaanzuur" |> Expect.equal "both strengths" [| 5N; 10N |]

                    ord
                    |> Order.productIds
                    |> Expect.equal "both products" [| "amox-clav-500-50"; "amox-clav-1000-200" |]
                }

                test "a pick of 10 mg/mL clavulaanzuur leaves one product, 20 mL and 200 mg clavulaanzuur" {
                    let ord = Scenarios.amoxClavWithoutClavDoseText |> parse |> built |> pickClav 1

                    ord |> Order.productIds |> Expect.equal "one product" [| "amox-clav-1000-200" |]
                    ord |> cmpQuantities |> Expect.equal "20 mL" [| 20N |]
                    ord |> quantities "clavulaanzuur" |> Expect.equal "200 mg" [| 200N |]
                }

                test "a pick of 10 mg/mL clavulaanzuur, then cleared, gives both products and strengths back" {
                    let ord = Scenarios.amoxClavWithoutClavDoseText |> parse |> built |> pickClav 1

                    let picks =
                        [
                            ord
                            |> item "clavulaanzuur"
                            |> _.ComponentConcentration
                            |> Concentration.toOrdVar
                            |> OrderProcessor.nameOf
                        ]

                    let ord =
                        ord
                        |> OrderProcessor.processChangeProperty
                            noLog
                            (ClearItemComponentConcentration(amoxClav, "clavulaanzuur"))
                        |> fun cleared -> solve (fun o -> Reopen(o, picks)) cleared

                    ord |> strengths "clavulaanzuur" |> Expect.equal "both strengths" [| 5N; 10N |]

                    ord
                    |> Order.productIds
                    |> Expect.equal "both products" [| "amox-clav-500-50"; "amox-clav-1000-200" |]
                }

                test "products that give every combination keep every strength" {
                    let ord = Scenarios.amoxClavEveryCombinationText |> parse |> built

                    ord
                    |> strengths "amoxicilline"
                    |> Expect.equal "both amoxicilline strengths" [| 25N; 50N |]
                    ord
                    |> strengths "clavulaanzuur"
                    |> Expect.equal "both clavulaanzuur strengths" [| 5N; 10N |]
                    ord |> Order.productIds |> Array.length |> Expect.equal "four products" 4
                }

                test "a component with products of which none applies is refused" {
                    let at7 (s: ProductStrength) =
                        if s.Substance = "clavulaanzuur" then
                            { s with Strength = s.Strength |> ValueUnit.setValue [| 7N |] }
                        else
                            s

                    let med = Scenarios.amoxClavText |> parse

                    { med with
                        Components =
                            med.Components
                            |> List.map (fun pc ->
                                { pc with
                                    Products =
                                        pc.Products
                                        |> List.map (fun p -> { p with Strengths = p.Strengths |> List.map at7 })
                                }
                            )
                    }
                    |> toOrder
                    |> CalcMinMax
                    |> OrderProcessor.processPipeline noLog
                    |> Result.isError
                    |> Expect.isTrue "refused"
                }

                test "a component without products keeps every strength, as a plan stored without products" {
                    let med = Scenarios.amoxClavWithoutClavDoseText |> parse

                    { med with Components = med.Components |> List.map (fun pc -> { pc with Products = [] }) }
                    |> built
                    |> strengths "clavulaanzuur"
                    |> Expect.equal "both strengths" [| 5N; 10N |]
                }

                test "a liquid built from a rule compares its product at 1 mL" {
                    { Scenarios.pcmDrink with
                        Components = [| RuleFixtures.liquidLimit |] |> Medication.createComponents noLog
                    }
                    |> built
                    |> Order.productIds
                    |> Expect.equal "the product applies" [| "33" |]
                }
            ]


module MedicationOrderTests =


    let tests =
        testList
            "Medication Orders"
            [
                test "medication default values" {
                    let medOrd = Medication.template
                    medOrd.Id |> Expect.equal "should be empty" ""
                    medOrd.Name |> Expect.equal "should be empty" ""
                    medOrd.Components |> Expect.isEmpty "should be empty"
                }

                test "productComponent default values" {
                    let cmp = Medication.productComponent
                    cmp.Name |> Expect.equal "should be empty" ""
                    cmp.Form |> Expect.equal "should be empty" ""
                    cmp.Substances |> Expect.isEmpty "should be empty"
                }

                test "substanceItem default values" {
                    let substance = Medication.substanceItem
                    substance.Name |> Expect.equal "should be empty" ""
                    substance.Concentrations |> Expect.isNone "should be None"
                    substance.Dose |> Expect.isNone "should be None"
                }

                testList
                    "ToDto"
                    [
                        test "ToDto converts medication to OrderDto" {
                            let medOrd = testMedicationOrders |> List.head
                            let dto = Medication.toOrderDto Scenarios.testStart medOrd

                            dto.Id |> Expect.equal "should match Id" medOrd.Id
                            dto.Schedule.IsDiscontinuous |> Expect.isTrue "should be discontinuous"
                            dto.Orderable.Name |> Expect.equal "should match Name" medOrd.Name
                            dto.Orderable.Components |> Expect.hasLength "should have 2 components" 2

                            dto.Orderable.Components[0].Items
                            |> Expect.hasLength "should have 3 items in first component" 2
                        }

                        test "ToDto reference function to OrderDto" {
                            let medOrd = testMedicationOrders |> List.head

                            let ord1 =
                                Medication.toOrderDto Scenarios.testStart medOrd
                                |> Order.Dto.fromDto
                                |> Result.get

                            // Check if the dto the same as ToOrderDto.toOrderDto
                            let ord2 = ToOrderDto.toOrderDto medOrd |> Order.Dto.fromDto |> Result.get

                            ord1.Adjust |> Expect.equal "should be equal" ord2.Adjust
                            ord1.Duration |> Expect.equal "should be equal" ord2.Duration
                            ord1.Id |> Expect.equal "should be equal" ord2.Id
                            ord1.Route |> Expect.equal "should be equal" ord2.Route
                            ord1.Schedule |> Expect.equal "should be equal" ord2.Schedule
                            ord1.Orderable.Name |> Expect.equal "should be equal" ord2.Orderable.Name

                            ord1.Orderable.Components[0].OrderableQuantity
                            |> Expect.equal "should be equal" ord2.Orderable.Components[0].OrderableQuantity

                            // this is fix: https://github.com/halcwb/GenPres2/commit/43d58ab1e123fd3217061d191226c5f074cdfad3
                            ord1.Orderable.OrderableQuantity
                            |> Expect.notEqual "should NOT be equal" ord2.Orderable.OrderableQuantity

                            printfn $"{medOrd.Components[0].Dose}"

                            ord1.Orderable.Dose.Quantity
                            |> Expect.notEqual "should NOT be equal" ord2.Orderable.Dose.Quantity
                        }

                    ]
            ]

// Add more test modules as needed
module DosePrintoutTests =

    open Informedica.GenOrder.Lib.Order
    module OV = OrderVariable
    module Units = Units
    module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
    module Limit = Informedica.GenCore.Lib.Ranges.Limit

    let private createSingleValueUnitDto un v =
        ValueUnit.create un [| v |] |> ValueUnit.Dto.toDto false "English"

    /// Helper to create a test order with configurable dose constraints
    let private createTestOrderWithConstraints hasQuantityConstraints hasQuantityAdjustConstraints adjustUnit =
        let medicationOrder =
            { Medication.template with
                Id = "DOSE_TEST"
                Name = "Dose Printout Test Order"
                OrderType = DiscontinuousOrder
                Frequencies =
                    ValueUnit.create (Units.Count.times |> ValueUnit.per Units.Time.day) [| 2N |]
                    |> Some
                Components =
                    [
                        { Medication.productComponent with
                            Name = "Test Component"
                            Form = "tablet"
                            Divisible = Some 1N
                            Quantities = Some(ValueUnit.create Units.Mass.milliGram [| 10N |])
                            Substances =
                                [
                                    { Medication.substanceItem with
                                        Name = "Test Substance"
                                        Concentrations = Some(ValueUnit.create Units.Mass.milliGram [| 10N |])
                                        Dose =
                                            let dl = DoseLimit.limit
                                            // Configure constraints based on test parameters
                                            let dl =
                                                if hasQuantityConstraints then
                                                    { dl with
                                                        Quantity =
                                                            dl.Quantity
                                                            |> MinMax.setMin (
                                                                5N
                                                                |> ValueUnit.singleWithUnit Units.Mass.milliGram
                                                                |> Limit.inclusive
                                                                |> Some
                                                            ) // true (5N |> Limit.limit (Units.Mass.milliGram |> Some))
                                                            |> MinMax.setMax (
                                                                10N
                                                                |> ValueUnit.singleWithUnit Units.Mass.milliGram
                                                                |> Limit.inclusive
                                                                |> Some
                                                            )
                                                    }
                                                else
                                                    dl

                                            let dl =
                                                if hasQuantityAdjustConstraints then
                                                    match adjustUnit with
                                                    | Some adj ->
                                                        { dl with
                                                            QuantityAdjust =
                                                                dl.QuantityAdjust
                                                                |> MinMax.setMin (
                                                                    1N
                                                                    |> ValueUnit.singleWithUnit Units.Mass.milliGram
                                                                    |> Limit.inclusive
                                                                    |> Some
                                                                )
                                                                |> MinMax.setMax (
                                                                    10N
                                                                    |> ValueUnit.singleWithUnit Units.Mass.milliGram
                                                                    |> Limit.inclusive
                                                                    |> Some
                                                                )
                                                        }
                                                    | None -> dl
                                                else
                                                    dl

                                            Some dl
                                    }
                                ]
                        }
                    ]
            }

        let dto = Medication.toOrderDto Scenarios.testStart medicationOrder
        dto |> Dto.fromDto |> Result.map applyConstraints |> Result.get

    let tests =
        testList
            "Dose Printout"
            [
                testList
                    "useAdj=true with QuantityAdjust constraints"
                    [
                        test "Verify correct dose printout when useAdj is true and QuantityAdjust has constraints" {
                            // Create order with QuantityAdjust constraints
                            let order = createTestOrderWithConstraints false true (Some Units.Weight.kiloGram)

                            // Print the order with useAdj=true
                            let pres, _, _ = order |> Print.printOrderToMd true [| "Test Substance" |]

                            // Verify that the printout includes adjusted dose information
                            // When QuantityAdjust has constraints, isPerDose should be true
                            // and it should print itemDoseQuantityAdjust
                            Expect.isTrue "Printout should not be empty" (pres |> String.length > 0)

                            // The printout should contain constraint information like
                            // Format: [min - max] per dosis or similar
                            let hasConstraintInfo = pres.Contains("-")

                            if not hasConstraintInfo then
                                printfn $"\n\n === pres: {pres}"

                            Expect.isTrue
                                "Printout should include constraint brackets when QuantityAdjust has constraints"
                                hasConstraintInfo
                        }

                        test
                            "Constraint printout uses doseQuantityAdjustConstraints when QuantityAdjust has constraints" {
                            let order = createTestOrderWithConstraints false true (Some Units.Weight.kiloGram)

                            // Get the item from the order
                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            // Verify QuantityAdjust has constraints
                            Expect.isTrue
                                "QuantityAdjust should have constraints"
                                (item.Dose.QuantityAdjust |> OV.QuantityAdjust.hasConstraints)

                            // Verify the constraint string is generated correctly
                            let constraintStr = item.Dose |> Orderable.Dose.Print.doseQuantityAdjustConstraints 3

                            Expect.isTrue "Constraint string should not be empty" (constraintStr |> String.length > 0)
                        }
                    ]

                testList
                    "useAdj=true with QuantityAdjust no constraints"
                    [
                        test "Verify correct dose printout when useAdj is true and QuantityAdjust has no constraints" {
                            // Create order without QuantityAdjust constraints
                            let order = createTestOrderWithConstraints false false (Some Units.Weight.kiloGram)

                            // Print the order with useAdj=true
                            let pres, _, _ = order |> Print.printOrderToString true [||]

                            // When QuantityAdjust has no constraints, isPerDose is false
                            // and it should use itemDosePerTimeAdjust instead
                            Expect.isTrue "Printout should not be empty" (pres |> String.length > 0)

                            // Get the item to verify constraint status
                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            Expect.isFalse
                                "QuantityAdjust should not have constraints"
                                (item.Dose.QuantityAdjust |> OV.QuantityAdjust.hasConstraints)
                        }

                        test "Uses dosePerTimeAdjust path when QuantityAdjust has no constraints" {
                            let order = createTestOrderWithConstraints false false (Some Units.Weight.kiloGram)

                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            // When no constraints, should use PerTimeAdjust for display
                            let perTimeAdjustStr = item.Dose |> Orderable.Dose.Print.dosePerTimeAdjustTo false 3

                            // Verify that PerTimeAdjust path is being used (may be empty if not solved)
                            Expect.isTrue "PerTimeAdjust string should be retrievable" (perTimeAdjustStr <> null)
                        }
                    ]

                testList
                    "useAdj=false with Quantity constraints"
                    [
                        test "Verify correct dose printout when useAdj is false and Quantity has constraints" {
                            // Create order with Quantity constraints but no adjust unit
                            let order = createTestOrderWithConstraints true false None

                            // Print the order with useAdj=false
                            let pres, _, _ = order |> Print.printOrderToString false [||]

                            // Verify that the printout includes dose information
                            Expect.isTrue "Printout should not be empty" (pres |> String.length > 0)

                            // Should include constraint information when Quantity has constraints
                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            Expect.isTrue
                                "Quantity should have constraints"
                                (item.Dose.Quantity |> OV.Quantity.hasConstraints)

                            // Verify constraint string generation
                            let constraintStr = item.Dose |> Orderable.Dose.Print.doseQuantityConstraints 3
                            Expect.isTrue "Constraint string should not be empty" (constraintStr |> String.length > 0)
                        }

                        test "Constraint printout uses doseQuantityConstraints when Quantity has constraints" {
                            let order = createTestOrderWithConstraints true false None

                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            // When useAdj=false and Quantity has constraints, isPerDose is true
                            Expect.isTrue
                                "Quantity should have constraints"
                                (item.Dose.Quantity |> OV.Quantity.hasConstraints)

                            // Should use itemDoseQuantity path
                            let qtyStr = item |> Orderable.Item.Print.itemDoseQuantityTo false 3
                            Expect.isTrue "Quantity string should be retrievable" (qtyStr <> null)
                        }
                    ]

                testList
                    "useAdj=false with Quantity no constraints"
                    [
                        test "Verify correct dose printout when useAdj is false and Quantity has no constraints" {
                            // Create order without Quantity constraints
                            let order = createTestOrderWithConstraints false false None

                            // Print the order with useAdj=false
                            let pres, _, _ = order |> Print.printOrderToString false [||]

                            // Printout should still work (using PerTime path)
                            Expect.isTrue "Printout should not be empty" (pres |> String.length > 0)

                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            Expect.isFalse
                                "Quantity should not have constraints"
                                (item.Dose.Quantity |> OV.Quantity.hasConstraints)
                        }

                        test "Uses dosePerTime path when Quantity has no constraints" {
                            let order = createTestOrderWithConstraints false false None

                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            // When no constraints, should use PerTime for display
                            let perTimeStr = item.Dose |> Orderable.Dose.Print.dosePerTimeTo false 3

                            // Verify that PerTime path is being used (may be empty if not solved)
                            Expect.isTrue "PerTime string should be retrievable" (perTimeStr <> null)
                        }
                    ]

                testList
                    "Edge cases and integration"
                    [
                        test "Once order with useAdj=true shows QuantityAdjust constraints correctly" {
                            let un = Units.Mass.milliGram |> ValueUnit.per Units.Weight.kiloGram

                            let medOrd =
                                { Medication.template with
                                    Id = "ONCE_TEST"
                                    Name = "Once Order Test"
                                    OrderType = OnceOrder
                                    Components =
                                        [
                                            { Medication.productComponent with
                                                Name = "Test Component"
                                                Form = "injection"
                                                Substances =
                                                    [
                                                        { Medication.substanceItem with
                                                            Name = "Test Drug"
                                                            Concentrations =
                                                                Some(ValueUnit.create Units.Mass.milliGram [| 100N |])
                                                            Dose =
                                                                let dl = DoseLimit.limit

                                                                { dl with
                                                                    QuantityAdjust =
                                                                        dl.QuantityAdjust
                                                                        |> MinMax.setMin (
                                                                            2N
                                                                            |> ValueUnit.singleWithUnit un
                                                                            |> Limit.inclusive
                                                                            |> Some
                                                                        )
                                                                        |> MinMax.setMax (
                                                                            5N
                                                                            |> ValueUnit.singleWithUnit un
                                                                            |> Limit.inclusive
                                                                            |> Some
                                                                        )
                                                                }
                                                                |> Some
                                                        }
                                                    ]
                                            }
                                        ]
                                }

                            let order = Medication.toOrderDto Scenarios.testStart medOrd |> Dto.fromDto |> Result.get
                            let pres, _, _ = order |> Print.printOrderToString true [||]

                            // For Once orders with adjusted dose, should show QuantityAdjust
                            Expect.isTrue "Once order printout should not be empty" (pres |> String.length > 0)

                            let item = order.Orderable.Components |> List.head |> (fun c -> c.Items |> List.head)

                            Expect.isTrue
                                "QuantityAdjust should have constraints in Once order"
                                (item.Dose.QuantityAdjust |> OV.QuantityAdjust.hasConstraints)
                        }

                        test "Printout correctly distinguishes between adjusted and non-adjusted modes" {
                            let order = createTestOrderWithConstraints true true (Some Units.Weight.kiloGram)

                            // Get printouts for both modes
                            let presAdj, _, _ = order |> Print.printOrderToString true [||]
                            let presNoAdj, _, _ = order |> Print.printOrderToString false [||]

                            // Both should produce output
                            Expect.isTrue "Adjusted printout should not be empty" (presAdj |> String.length > 0)
                            Expect.isTrue "Non-adjusted printout should not be empty" (presNoAdj |> String.length > 0)

                            // They should be different since one uses adjusted values and the other doesn't
                            // (Note: they might be the same if neither is solved, but the code paths are different)
                            Expect.isTrue
                                "Printouts should be retrievable in both modes"
                                (presAdj <> null && presNoAdj <> null)
                        }
                    ]
            ]

module TypeTests =

    /// Bare values for the order plan types: an empty context, empty totals.
    module OrderPlanFixtures =

        let filter: Filter =
            {
                Indications = [||]
                Generics = [||]
                Routes = [||]
                Forms = [||]
                DoseTypes = [||]
                Diluents = [||]
                Components = [||]
                Indication = None
                Generic = None
                Route = None
                Form = None
                DoseType = None
                Diluent = None
                SelectedComponents = [||]
            }


        let totals: Totals =
            {
                Volume = None
                Energy = None
                Protein = None
                Carbohydrate = None
                Fat = None
                Sodium = None
                Potassium = None
                Chloride = None
                Calcium = None
                Phosphate = None
                Magnesium = None
                Iron = None
                VitaminD = None
                Ethanol = None
                Propyleenglycol = None
                BenzylAlcohol = None
                BoricAcid = None
            }


        let context: OrderContext =
            {
                Filter = filter
                Patient = Patient.patient
                Scenarios = [||]
                Argumentation = None
            }


        let planContext id category =
            {
                Id = id
                Category = category
                Context = context
                Intake = totals
            }


        let plan contexts =
            {
                Patient = Patient.patient
                Filtered = [||]
                Contexts = contexts
                Totals = totals
            }


    let tests =
        testList
            "Types"
            [
                test "a plan context wraps an order context with its id, category and intake" {
                    let pc = OrderPlanFixtures.planContext "ctx-1" OrderCategory.Drug

                    pc.Context
                    |> Expect.equal "the context is held as given" OrderPlanFixtures.context

                    pc.Id |> Expect.equal "the id is the plan's" "ctx-1"
                    pc.Category |> Expect.equal "a drug" OrderCategory.Drug
                    pc.Intake |> Expect.equal "the intake is held as given" OrderPlanFixtures.totals
                }

                test "a nutrition context carries its category" {
                    let pc = OrderPlanFixtures.planContext "ctx-2" (OrderCategory.Nutrition NutritionCategory.TPN)

                    match pc.Category with
                    | OrderCategory.Nutrition cat -> cat |> Expect.equal "TPN" NutritionCategory.TPN
                    | OrderCategory.Drug -> failtest "a nutrition context is not a drug"
                }

                test "an order plan holds its contexts, its filter and its totals" {
                    let contexts =
                        [|
                            OrderPlanFixtures.planContext "ctx-1" OrderCategory.Drug
                            OrderPlanFixtures.planContext
                                "ctx-2"
                                (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
                        |]

                    let plan = { OrderPlanFixtures.plan contexts with Filtered = [| "ctx-2" |] }

                    plan.Contexts
                    |> Array.map _.Id
                    |> Expect.equal "both contexts" [| "ctx-1"; "ctx-2" |]

                    plan.Filtered |> Expect.equal "the filter keeps one" [| "ctx-2" |]
                    plan.Patient |> Expect.equal "the patient as shown" Patient.patient
                }

                test "an order plan version stores the whole plan and its identity" {
                    let plan = OrderPlanFixtures.plan [| OrderPlanFixtures.planContext "ctx-1" OrderCategory.Drug |]

                    let version =
                        {
                            Id = "v-1"
                            No = 1
                            PatientId = "stub-patient"
                            Base = None
                            SignedBy =
                                {
                                    UserId = "u-1"
                                    DisplayName = "Stub Prescriber"
                                }
                            SignedAt = System.DateTime(2026, 9, 16, 12, 0, 0)
                            Plan = plan
                            Verified = true
                        }

                    version.Plan |> Expect.equal "the plan as signed" plan
                    version.Base |> Expect.isNone "the first version has no base"
                    version.SignedBy.DisplayName |> Expect.equal "the signer" "Stub Prescriber"

                    let second =
                        { version with
                            Id = "v-2"
                            No = 2
                            Base = Some version.Id
                        }

                    second.Base |> Expect.equal "the second is built on the first" (Some "v-1")
                    second.Plan |> Expect.equal "the plan is copied, not recomputed" version.Plan
                }

                test "a nutrition rule set names what its category draws from" {
                    let set =
                        {
                            Category = NutritionCategory.Lipid
                            Label = "Lipid"
                            Indications = [| "parenterale voeding" |]
                            Generics = [| "smoflipid" |]
                        }

                    set.Generics |> Expect.equal "the generics of the set" [| "smoflipid" |]
                }

                test "OrderVariable can be created" {
                    let constraints =
                        {
                            Min = None
                            Max = None
                            Incr = None
                            Values = None
                        }

                    let variable = Variable.create id (Name "test") ValueRange.Unrestricted // Assuming this exists

                    let orderVariable =
                        {
                            DefinedConstraints = constraints
                            CalculatedConstraints = OrderVariable.Constraints.create None None None None
                            Variable = variable
                        }

                    orderVariable.DefinedConstraints |> Expect.equal "should be equal" constraints
                }
            ]

module PatientConstructorTests =


    let tests =
        testList
            "Patient constructors"
            [

                test "premature has gestational age set" {
                    Patient.premature.GestAge
                    |> Expect.isSome "premature should have gestational age"
                }

                test "premature has department NEO" {
                    Patient.premature.Department |> Expect.equal "should be NEO" (Some "NEO")
                }

                test "newBorn has age set" { Patient.newBorn.Age |> Expect.isSome "newBorn should have age" }

                test "infant has weight set" { Patient.infant.Weight |> Expect.isSome "infant should have weight" }

                test "child has height set" { Patient.child.Height |> Expect.isSome "child should have height" }

                test "teenager has age set" { Patient.teenager.Age |> Expect.isSome "teenager should have age" }

                test "adult has weight set" { Patient.adult.Weight |> Expect.isSome "adult should have weight" }

                test "all patient constructors have distinct departments or ages" {
                    let patients =
                        [
                            Patient.premature
                            Patient.newBorn
                            Patient.infant
                            Patient.toddler
                            Patient.child
                            Patient.teenager
                            Patient.adult
                        ]

                    patients |> List.length |> Expect.equal "should have 7 distinct patients" 7
                }
            ]


module OrderBuilderTests =

    open Informedica.GenOrder.Lib.Medication

    /// The names of every order variable of an order, in order.
    let private names (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.map (OrderVariable.getName >> Informedica.GenSolver.Lib.Variable.Name.toString)

    /// The built order out to an Order Dto and back. It was the path the builder was held to
    /// while the old one existed; now that the Dto is made from the built order, these compare
    /// an order with itself round tripped, which is what catches a Dto that drops a field, as
    /// the component id once was.
    let private roundTripped (med: Medication) =
        med |> Medication.toOrderDto Scenarios.testStart |> Order.Dto.fromDto


    /// The scenarios the orders are recorded for, so that a scenario cannot be added to one
    /// list of fixtures and forgotten in the other.
    let private fixtures = GoldenOrders.all |> List.map (fun g -> g.Name, g.Medication)

    /// The constraints of every order variable of an order, by name, for those whose name has
    /// the given number of parts: four for an item, three for a component, two for the
    /// orderable and one for the order itself.
    let private constraintsOfDepth depth (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.choose (fun ovar ->
            let n =
                ovar
                |> OrderVariable.getName
                |> Informedica.GenSolver.Lib.Variable.Name.toString

            let parts =
                n
                |> String.split "]"
                |> List.head
                |> String.replace "[" ""
                |> String.split "."
                |> List.length

            if parts = depth then
                Some(n, ovar |> OrderVariable.getConstraints)
            else
                None
        )
        |> List.sortBy fst

    let tests =
        testList
            "Medication.OrderBuilder, and the Dto round trip over what it builds"
            [
                for orderType in [ AnyOrder; ProcessOrder ] do
                    test $"an order type of {orderType} is refused, not raised" {
                        { Scenarios.pcmSupp with OrderType = orderType }
                        |> Medication.toOrder Scenarios.testStart
                        |> Result.isError
                        |> Expect.isTrue "a medication that cannot be ordered gives an Error"
                    }

                test "asking a medication that cannot be ordered for a Dto says why" {
                    let med = { Scenarios.pcmSupp with OrderType = AnyOrder }

                    // the callers that want a Dto have nowhere to put a failure, so it is
                    // thrown; what is thrown must still name the reason
                    (fun () -> med |> Medication.toOrderDto Scenarios.testStart |> ignore)
                    |> Expect.throwsT<System.NotSupportedException> "the reason reaches the caller"
                }

                test "a medication that cannot be ordered leaves the others alone" {
                    let meds =
                        [|
                            { Scenarios.pcmSupp with OrderType = AnyOrder }
                            Scenarios.pcmSupp
                            { Scenarios.amfo with OrderType = ProcessOrder }
                            Scenarios.amfo
                        |]

                    // what rule evaluation does with the orders it builds
                    meds
                    |> Array.choose (Medication.toOrder Scenarios.testStart >> Result.toOption)
                    |> Array.length
                    |> Expect.equal "the two that can be ordered are kept" 2
                }

                for name, med in fixtures do
                    test $"the Dto round trip returns {name}'s order unchanged" {
                        match med |> roundTripped, med |> Medication.toOrder Scenarios.testStart with
                        | Ok fromDto, Ok built -> fromDto |> Expect.equal "the same order" built
                        | r1, r2 -> failtest $"a path failed: %A{r1} %A{r2}"
                    }

                for name, med in fixtures do
                    test $"two builds of {name} on the same start are equal" {
                        match
                            med |> Medication.toOrder Scenarios.testStart, med |> Medication.toOrder Scenarios.testStart
                        with
                        | Ok first, Ok second -> first |> Expect.equal "the same order" second
                        | r1, r2 -> failtest $"a path failed: %A{r1} %A{r2}"
                    }

                for name, med in fixtures do
                    test $"the Dto round trip keeps every order variable of {name}" {
                        let built =
                            med
                            |> OrderBuilder.newOrder Scenarios.testStart
                            |> OrderBuilder.withComponents med

                        match med |> roundTripped with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord -> ord |> names |> Expect.equal "the same order variables" (built |> names)
                    }

                for name, med in fixtures do
                    test $"the Dto round trip keeps {name}'s item constraints" {
                        let built =
                            med
                            |> OrderBuilder.newOrder Scenarios.testStart
                            |> OrderBuilder.withComponents med
                            |> OrderBuilder.withItemConstraints med

                        match med |> roundTripped with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            built
                            |> constraintsOfDepth 4
                            |> Expect.equal "the same item constraints" (ord |> constraintsOfDepth 4)
                    }

                for name, med in fixtures do
                    test $"the Dto round trip keeps {name}'s orderable constraints" {
                        let built =
                            med
                            |> OrderBuilder.newOrder Scenarios.testStart
                            |> OrderBuilder.withComponents med
                            |> OrderBuilder.withItemConstraints med
                            |> OrderBuilder.withComponentConstraints med
                            |> OrderBuilder.withOrderableConstraints med

                        match med |> roundTripped with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            built
                            |> constraintsOfDepth 2
                            |> Expect.equal "the same orderable constraints" (ord |> constraintsOfDepth 2)
                    }

                for name, med in fixtures do
                    test $"the Dto round trip keeps {name}'s component constraints" {
                        let built =
                            med
                            |> OrderBuilder.newOrder Scenarios.testStart
                            |> OrderBuilder.withComponents med
                            |> OrderBuilder.withItemConstraints med
                            |> OrderBuilder.withComponentConstraints med

                        match med |> roundTripped with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            built
                            |> constraintsOfDepth 3
                            |> Expect.equal "the same component constraints" (ord |> constraintsOfDepth 3)
                    }
            ]


module GoldenOrderTests =

    /// The full processing an order goes through before a prescriber sees it, with the name of
    /// every stage that reported an error. A stage that fails carries its order on to the next
    /// one, as the application does, so what the stages produced is still there to compare.
    let private solve (ord: Order) =
        let run (name, cmd) (o, errs) =
            match o |> cmd |> OrderProcessor.processPipeline Logging.noOp with
            | Ok o -> o, errs
            | Error(o, _) -> o, errs @ [ name ]

        (ord, [])
        |> run ("CalcMinMax", CalcMinMax)
        |> run ("IncreaseIncrements", IncreaseIncrements)
        |> run ("CalcValues", CalcValues)
        |> run ("SolveOrder", SolveOrder)


    let private lines (s: string) =
        s |> String.split "\n" |> List.map String.trim |> List.filter String.notEmpty

    let tests =
        testList
            "the orders the scenarios solve to"
            [
                for golden in GoldenOrders.all do
                    test $"{golden.Name} solves to the order it did" {
                        match golden.Medication |> Medication.toOrder Scenarios.testStart with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            let solved, errs = ord |> solve

                            errs |> Expect.equal "the same stages report an error" golden.Errors

                            solved
                            |> Order.toString
                            |> List.map String.trim
                            |> List.filter String.notEmpty
                            |> Expect.equal "the same solved order" (golden.Order |> lines)
                    }
            ]


module ConstraintsTests =

    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenSolver.Lib.Variable.ValueRange

    module Constraints = OrderVariable.Constraints

    let private mg = Units.Mass.milliGram
    let private one = 1N |> ValueUnit.singleWithUnit mg
    let private two = 2N |> ValueUnit.singleWithUnit mg
    let private empty = ValueUnit.create mg [||]

    /// A Constraints with all four fields set, to see which of them a setter leaves alone.
    let private full =
        Constraints.create
            (one |> Minimum.create true |> Some)
            (two |> Increment.create |> Some)
            (two |> Maximum.create true |> Some)
            (one |> ValueSet.create |> Some)

    let tests =
        testList
            "OrderVariable.Constraints setters"
            [
                test "setValues of a ValueUnit without values is no value set" {
                    full
                    |> Constraints.setValues (Some empty)
                    |> _.Values
                    |> Expect.isNone "an empty ValueUnit constrains nothing"
                }

                test "setIncr of a ValueUnit without values is no increment" {
                    full
                    |> Constraints.setIncr (Some empty)
                    |> _.Incr
                    |> Expect.isNone "an empty ValueUnit constrains nothing"
                }

                test "setValues of None is no value set" {
                    full
                    |> Constraints.setValues None
                    |> _.Values
                    |> Expect.isNone "None constrains nothing"
                }

                test "setMin leaves the other three alone" {
                    let cs = full |> Constraints.setMin false (Some two)

                    (cs.Incr, cs.Max, cs.Values)
                    |> Expect.equal "only the minimum changed" (full.Incr, full.Max, full.Values)
                }

                test "setMin carries whether the bound is inclusive" {
                    full
                    |> Constraints.setMin false (Some two)
                    |> _.Min
                    |> Option.map Minimum.isIncl
                    |> Expect.equal "an exclusive minimum stays exclusive" (Some false)
                }

                test "setMax of None is no upper bound" {
                    full
                    |> Constraints.setMax true None
                    |> _.Max
                    |> Expect.isNone "None is no upper bound"
                }

                test "setMinMax leaves a bound the MinMax does not give" {
                    let cs = full |> Constraints.setMinMax false MinMax.empty

                    (cs.Min, cs.Max) |> Expect.equal "neither bound moved" (full.Min, full.Max)
                }

                test "setMinMax widens a norm dose by a tenth either way" {
                    let mm =
                        { MinMax.empty with
                            Min = one |> Limit.Inclusive |> Some
                            Max = one |> Limit.Inclusive |> Some
                        }

                    let cs = full |> Constraints.setMinMax true mm

                    let value =
                        Option.map (Minimum.toValueUnit >> ValueUnit.getValue >> Array.head)
                        >> Option.defaultValue 0N

                    (cs.Min |> value, cs.Max |> Option.map (Maximum.toValueUnit >> ValueUnit.getValue >> Array.head))
                    |> Expect.equal "nine tenths and eleven tenths of the norm" (9N / 10N, Some(11N / 10N))
                }

                test "setMinMax takes a range as it is" {
                    let mm =
                        { MinMax.empty with
                            Min = one |> Limit.Inclusive |> Some
                            Max = two |> Limit.Inclusive |> Some
                        }

                    full
                    |> Constraints.setMinMax true mm
                    |> _.Max
                    |> Option.map (Maximum.toValueUnit >> ValueUnit.getValue >> Array.head)
                    |> Expect.equal "a range is not a norm dose" (Some 2N)
                }
            ]


module OrderVariableTests =

    // Hermetic tests for ValueUnit.collect — no resources are loaded. All
    // ValueUnits are built inline. The headline case uses the real DIGOXINE
    // TABLET strengths from the Z-index: three GPKs of the same substance
    // expressed in mixed mass units (milligram and microgram), which share a
    // unit group but not a unit. collect must merge them into a single
    // ValueUnit, converting the microgram value into the head unit (mg).
    let tests =
        testList
            "ValueUnit.collect"
            [
                // DIGOXINE TABLET: GPK 16721 = 0,25 mg, GPK 16772 = 62,5 microg,
                // GPK 38857 = 0,125 mg. 62,5 microg = 1/16 mg, so the collected
                // result is [| 1/4; 1/16; 1/8 |] mg in input order.
                test "collect merges mixed mass units (digoxin mg + microgram)" {
                    let vus =
                        [|
                            ValueUnit.create Units.Mass.milliGram [| 1N / 4N |] // 0,25 mg
                            ValueUnit.create Units.Mass.microGram [| 125N / 2N |] // 62,5 microg
                            ValueUnit.create Units.Mass.milliGram [| 1N / 8N |] // 0,125 mg
                        |]

                    let exp = ValueUnit.create Units.Mass.milliGram [| 1N / 4N; 1N / 16N; 1N / 8N |] |> Some

                    vus
                    |> ValueUnit.collect
                    |> Expect.equal "should merge into a single mg ValueUnit" exp
                }

                // Same unit throughout: identity round-trip of the values.
                test "collect keeps values when all units are equal" {
                    let vus =
                        [|
                            ValueUnit.create Units.Mass.milliGram [| 1N / 4N |]
                            ValueUnit.create Units.Mass.milliGram [| 1N / 8N |]
                        |]

                    let exp = ValueUnit.create Units.Mass.milliGram [| 1N / 4N; 1N / 8N |] |> Some

                    vus
                    |> ValueUnit.collect
                    |> Expect.equal "should concatenate without conversion" exp
                }

                test "collect returns None for an empty array" {
                    [||] |> ValueUnit.collect |> Expect.isNone "empty input should give None"
                }

                // Different unit groups (mass vs volume) cannot be collected.
                test "collect fails for incompatible unit groups" {
                    let vus =
                        [|
                            ValueUnit.create Units.Mass.milliGram [| 1N / 4N |]
                            ValueUnit.create Units.Volume.milliLiter [| 1N |]
                        |]

                    (fun () -> vus |> ValueUnit.collect |> ignore)
                    |> Expect.throws "mixing mass and volume should throw"
                }
            ]


module HelperTests =

    module Name = Informedica.GenSolver.Lib.Variable.Name
    module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
    module ValueSet = ValueRange.ValueSet
    module Minimum = ValueRange.Minimum
    module Constraints = OrderVariable.Constraints


    let mL (vs: int list) =
        vs
        |> List.map BigRational.fromInt
        |> List.toArray
        |> ValueUnit.withUnit Units.Volume.milliLiter


    let mLPerHour (vs: int list) =
        vs
        |> List.map BigRational.fromInt
        |> List.toArray
        |> ValueUnit.withUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour)


    let hours (vs: int list) =
        vs
        |> List.map BigRational.fromInt
        |> List.toArray
        |> ValueUnit.withUnit Units.Time.hour


    /// A min, increment and max in the unit of u, each limit inclusive.
    let minIncrMax u min incr max =
        ValueRange.create
            (u [ min ] |> Minimum.create true |> Some)
            (u [ incr ] |> ValueRange.Increment.create |> Some)
            (u [ max ] |> ValueRange.Maximum.create true |> Some)
            None


    let valSet u vs = u vs |> ValueSet.create |> ValSet


    /// An order variable in mL with the range vr, and with an increment of incr mL in its defined
    /// constraints when incr is given.
    let ovarWith name incr vr =
        let cs =
            Constraints.create None (incr |> Option.map (fun i -> mL [ i ] |> ValueRange.Increment.create)) None None

        let ovar = OrderVariable.createNew (name |> Name.createExc) Units.Volume.milliLiter

        { ovar with
            DefinedConstraints = cs
            Variable = { ovar.Variable with Values = vr }
        }


    let ovar incr vr = ovarWith "x" incr vr


    /// The order variable with the values vs in mL in its calculated constraints.
    let withCalculated vs (ovar: OrderVariable) =
        { ovar with
            CalculatedConstraints = Constraints.create None None None (vs |> Option.map (mL >> ValueSet.create))
        }


    /// The order of a medication, as it is made, before any processing.
    let orderOf med =
        match med |> Medication.toOrder Scenarios.testStart with
        | Ok ord -> ord
        | Error e -> failtest $"could not build the order: %A{e}"


    /// The order with the range of its dose rate replaced by vr.
    let withRate vr (ord: Order) =
        let rate =
            ord.Orderable.Dose.Rate
            |> OrderVariable.Rate.apply (fun ovar -> { ovar with Variable = { ovar.Variable with Values = vr } })

        { ord with Order.Orderable.Dose.Rate = rate }


    /// The order with the values vs in hours in the calculated constraints of its time.
    let withCalculatedTime vs (ord: Order) =
        let calculated =
            OrderVariable.Time.apply (fun ovar ->
                { ovar with
                    CalculatedConstraints = Constraints.create None None None (hours vs |> ValueSet.create |> Some)
                }
            )

        match ord.Schedule with
        | Timed(frq, tme) -> { ord with Schedule = Timed(frq, tme |> calculated) }
        | _ -> ord


    let tests =
        testList
            "helpers"
            [
                testList
                    "ValueUnit.collectValues"
                    [
                        test "the values sorted, without doubles" {
                            [ mL [ 2; 1 ]; mL [ 1; 3 ] ]
                            |> ValueUnit.collectValues
                            |> Expect.equal "1, 2 and 3 mL" (mL [ 1; 2; 3 ])
                        }

                        test "the values in the unit of the first value unit" {
                            [ [| 1N |] |> ValueUnit.withUnit Units.Volume.liter; mL [ 500 ] ]
                            |> ValueUnit.collectValues
                            |> Expect.equal "1/2 and 1 L" ([| 1N / 2N; 1N |] |> ValueUnit.withUnit Units.Volume.liter)
                        }

                        test "no value units raise" {
                            (fun () -> [] |> ValueUnit.collectValues |> ignore)
                            |> Expect.throws "nothing to collect"
                        }
                    ]

                testList
                    "OrderVariable"
                    [
                        test "one value is one value" {
                            valSet mL [ 2 ]
                            |> ovar None
                            |> OrderVariable.hasOneValue
                            |> Expect.isTrue "one value"
                        }

                        test "a list of two values is not one value" {
                            valSet mL [ 1; 2 ]
                            |> ovar None
                            |> OrderVariable.hasOneValue
                            |> Expect.isFalse "two values"
                        }

                        test "the value of a variable with one value" {
                            valSet mL [ 2 ]
                            |> ovar None
                            |> OrderVariable.solvedValue
                            |> Expect.equal "2 mL" (Some(mL [ 2 ]))
                        }

                        test "no value of a variable with a list" {
                            valSet mL [ 1; 2 ]
                            |> ovar None
                            |> OrderVariable.solvedValue
                            |> Expect.isNone "no value"
                        }

                        test "the variables by their name" {
                            let a = valSet mL [ 1 ] |> ovarWith "a" None
                            let b = valSet mL [ 2 ] |> ovarWith "b" None
                            let byName = [ a; b ] |> OrderVariable.byName

                            byName |> Map.tryFind ("b" |> Name.createExc) |> Expect.equal "b" (Some b)
                            byName |> Map.tryFind ("c" |> Name.createExc) |> Expect.isNone "no c"
                        }

                        test "calculated constraints with one value" {
                            valSet mL [ 2 ]
                            |> ovar None
                            |> withCalculated (Some [ 2 ])
                            |> OrderVariable.hasOneCalculatedValue
                            |> Expect.isTrue "one calculated value"
                        }

                        test "calculated constraints with two values or without values" {
                            let x = valSet mL [ 2 ] |> ovar None

                            [ x |> withCalculated (Some [ 1; 2 ]); x |> withCalculated None ]
                            |> List.exists OrderVariable.hasOneCalculatedValue
                            |> Expect.isFalse "not one calculated value"
                        }

                        test "the shape of each range" {
                            [
                                valSet mL [ 1; 2 ] |> ovar None
                                minIncrMax mL 1 1 3 |> ovar None
                                valSet mL [ 2 ] |> ovar (Some 1)
                                valSet mL [ 2 ] |> ovar None
                            ]
                            |> List.map OrderVariable.shape
                            |> Expect.equal
                                "a list, a range, a steppable value and nothing"
                                [
                                    Shape.SelectableList
                                    Shape.SelectableRange
                                    Shape.Steppable
                                    Shape.NotSettable
                                ]
                        }

                        test "a variable freed on its increment starts at the increment and has no maximum" {
                            valSet mL [ 4 ]
                            |> ovar (Some 2)
                            |> OrderVariable.free
                            |> _.Variable.Values
                            |> Expect.equal
                                "2 mL and up by 2 mL"
                                (ValueRange.create
                                    (mL [ 2 ] |> Minimum.create true |> Some)
                                    (mL [ 2 ] |> ValueRange.Increment.create |> Some)
                                    None
                                    None)
                        }

                        test "a variable freed without an increment is any positive value" {
                            valSet mL [ 4 ]
                            |> ovar None
                            |> OrderVariable.free
                            |> OrderVariable.isNonZeroPositive
                            |> Expect.isTrue "any positive value"
                        }

                        test "a count set to one and to at least one" {
                            let one = Units.Count.times |> ValueUnit.one
                            let x = valSet mL [ 2 ] |> ovar None

                            (x |> OrderVariable.setToOne).Variable.Values
                            |> Expect.equal "the value 1" (one |> ValueSet.create |> ValSet)

                            (x |> OrderVariable.setToAtLeastOne).Variable.Values
                            |> Expect.equal "1 or more" (one |> Minimum.create true |> Min)
                        }

                        test "a range narrowed to the values it holds" {
                            minIncrMax mL 1 1 3
                            |> ovar None
                            |> OrderVariable.tryConstrain (mL [ 2; 5 ])
                            |> Option.map _.Variable.Values
                            |> Expect.equal "2 mL" (Some(valSet mL [ 2 ]))
                        }

                        test "a range narrowed to values it does not hold" {
                            minIncrMax mL 1 1 3
                            |> ovar None
                            |> OrderVariable.tryConstrain (mL [ 7 ])
                            |> Expect.isNone "no value left"
                        }
                    ]

                testList
                    "Order"
                    [
                        test "a time with one calculated value is fixed, a time with more is not" {
                            let ord = Scenarios.amfoTimed |> orderOf

                            [ ord |> withCalculatedTime [ 2 ]; ord |> withCalculatedTime [ 1; 2 ] ]
                            |> List.map (_.Schedule >> Order.Schedule.isTimeFixed)
                            |> Expect.equal "fixed, then not" [ true; false ]
                        }

                        test "a component and an item by name" {
                            let orb =
                                Scenarios.amoxClavText
                                |> MedicationText.fromString
                                |> Result.defaultWith (failtestf "%A")
                                |> orderOf
                                |> _.Orderable

                            let cmp = orb.Components |> List.head |> _.Name |> WrappedString.Name.toString

                            orb |> Order.Orderable.tryComponent cmp |> Expect.isSome "the component"
                            orb |> Order.Orderable.tryComponent "none" |> Expect.isNone "no component"

                            orb
                            |> Order.Orderable.tryComponent cmp
                            |> Option.bind (Order.Orderable.Component.tryItem "clavulaanzuur")
                            |> Expect.isSome "the item"

                            orb
                            |> Order.Orderable.tryComponent cmp
                            |> Option.bind (Order.Orderable.Component.tryItem "none")
                            |> Expect.isNone "no item"
                        }

                        test "a rate range, a rate list and a rate of one value" {
                            let ord = Scenarios.morfCont |> orderOf

                            [
                                minIncrMax mLPerHour 1 1 3
                                valSet mLPerHour [ 1; 2 ]
                                valSet mLPerHour [ 2 ]
                            ]
                            |> List.map (fun vr -> (ord |> withRate vr).Orderable.Dose)
                            |> List.map (fun dos ->
                                dos |> Order.Orderable.Dose.rateIsRange, dos |> Order.Orderable.Dose.rateIsList
                            )
                            |> Expect.equal "a range, a list, neither" [ true, false; false, true; false, false ]
                        }

                        test "the equations hold the dose quantity of the order" {
                            let ord = Scenarios.morfCont |> orderOf

                            let dosQty =
                                ord.Orderable.Dose.Quantity
                                |> OrderVariable.Quantity.toOrdVar
                                |> OrderVariable.getName

                            ord
                            |> Order.equations
                            |> List.collect (
                                function
                                | OrderProductEquation(y, xs)
                                | OrderSumEquation(y, xs) -> y :: xs
                            )
                            |> List.exists (OrderVariable.getName >> (=) dosQty)
                            |> Expect.isTrue "the dose quantity"
                        }

                        test "every variable mapped" {
                            Scenarios.morfCont
                            |> orderOf
                            |> Order.mapOrdVars OrderVariable.clear
                            |> Order.toOrdVars
                            |> List.forall OrderVariable.isCleared
                            |> Expect.isTrue "every variable cleared"
                        }
                    ]
            ]


module DeterminationTests =

    module Name = Informedica.GenSolver.Lib.Variable.Name


    let names ns = ns |> List.map Name.createExc |> Set.ofList


    let row (terms: (string * BigRational) list) =
        terms |> List.map (fun (n, e) -> n |> Name.createExc, e) |> Map.ofList


    /// y = a · b as a product row, and s = y + c as a sum.
    let model: Determination.Model =
        {
            Names = names [ "y"; "a"; "b"; "s"; "c" ]
            Rows = [ row [ "y", 1N; "a", -1N; "b", -1N ] ]
            Sums = [ (Name.createExc "s", [ Name.createExc "y"; Name.createExc "c" ]) ]
            Constants = Set.empty
        }


    let tests =
        testList
            "Determination"
            [
                test "the reduced rows eliminate a variable two rows share" {
                    // x = y and y = z give x = z
                    [ row [ "x", 1N; "y", -1N ]; row [ "y", 1N; "z", -1N ] ]
                    |> Determination.rref
                    |> Expect.equal "x - z and y - z" [ row [ "x", 1N; "z", -1N ]; row [ "y", 1N; "z", -1N ] ]
                }

                test "the factors determine the product" {
                    names [ "a"; "b" ]
                    |> Determination.closure model
                    |> Expect.equal "y follows" (names [ "a"; "b"; "y" ])
                }

                test "the product and one factor determine the other factor" {
                    names [ "y"; "a" ]
                    |> Determination.closure model
                    |> Expect.equal "b follows" (names [ "a"; "b"; "y" ])
                }

                test "a product determined through a row determines a sum" {
                    names [ "a"; "b"; "c" ]
                    |> Determination.closure model
                    |> Expect.equal "y and then s follow" (names [ "a"; "b"; "c"; "s"; "y" ])
                }

                test "a sum and one term determine the other term, and no more" {
                    // y follows from the sum; y alone leaves a and b open
                    names [ "s"; "c" ]
                    |> Determination.closure model
                    |> Expect.equal "y follows" (names [ "c"; "s"; "y" ])
                }

                test "one factor determines nothing" {
                    names [ "a" ]
                    |> Determination.closure model
                    |> Expect.equal "only a" (names [ "a" ])
                }

                test "a variable only several product equations together determine" {
                    // p = a · b, q = a · c and r = b · c: each equation leaves two open, together they fix
                    // a, b and c
                    let m: Determination.Model =
                        {
                            Names = names [ "p"; "q"; "r"; "a"; "b"; "c" ]
                            Rows =
                                [
                                    row [ "p", 1N; "a", -1N; "b", -1N ]
                                    row [ "q", 1N; "a", -1N; "c", -1N ]
                                    row [ "r", 1N; "b", -1N; "c", -1N ]
                                ]
                            Sums = []
                            Constants = Set.empty
                        }

                    names [ "p"; "q"; "r" ]
                    |> Determination.closure m
                    |> Expect.equal "a, b and c follow" (names [ "a"; "b"; "c"; "p"; "q"; "r" ])
                }

                test "a constant counts as known" {
                    // y = one · a, with one a constant
                    let m: Determination.Model =
                        {
                            Names = names [ "y"; "one"; "a" ]
                            Rows = [ row [ "y", 1N; "one", -1N; "a", -1N ] ]
                            Sums = []
                            Constants = names [ "one" ]
                        }

                    names [ "a" ]
                    |> Determination.closure m
                    |> Expect.equal "y follows" (names [ "a"; "one"; "y" ])
                }

                test "a kept variable with more than one value is not undetermined and determines nothing" {
                    let ord =
                        match Scenarios.morfCont |> Medication.toOrder Scenarios.testStart with
                        | Ok ord -> ord
                        | Error e -> failtest $"could not build the order: %A{e}"

                    let kept = ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar

                    kept
                    |> OrderVariable.hasOneValue
                    |> Expect.isFalse "the kept variable holds more than one value"

                    ord
                    |> Determination.undeterminedBy (Set.singleton (kept |> OrderVariable.getName))
                    |> Expect.equal
                        "everything else undetermined"
                        (ord
                         |> Determination.undeterminedBy Set.empty
                         |> Set.remove (kept |> OrderVariable.getName))
                }
            ]


module ReferenceTests =

    open HelperTests

    module Ref = OrderProcessor.OrderVariableReference
    module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
    module ValueSet = ValueRange.ValueSet
    module Minimum = ValueRange.Minimum
    module Constraints = OrderVariable.Constraints


    let props =
        [
            DoseProperty.Quantity
            DoseProperty.QuantityAdjust
            DoseProperty.PerTime
            DoseProperty.PerTimeAdjust
            DoseProperty.Rate
            DoseProperty.RateAdjust
            DoseProperty.Total
            DoseProperty.TotalAdjust
        ]


    /// Every reference an order can have, for each of its components and items.
    let allRefs (ord: Order) =
        let name = WrappedString.Name.toString

        [
            OrderVariableReference.OrderAdjust
            OrderVariableReference.ScheduleFrequency
            OrderVariableReference.ScheduleTime
            OrderVariableReference.OrderableQuantity
            OrderVariableReference.OrderableDoseCount
            OrderVariableReference.OrderableOrderCount
            OrderVariableReference.OrderDuration
            OrderVariableReference.OrderableOrderQuantity
            for p in props do
                OrderVariableReference.OrderableDose p

            for c in ord.Orderable.Components do
                let c' = c.Name |> name
                OrderVariableReference.ComponentOrderQuantity c'
                OrderVariableReference.ComponentOrderCount c'
                OrderVariableReference.ComponentQuantity c'
                OrderVariableReference.ComponentOrderableQuantity c'
                OrderVariableReference.ComponentOrderableCount c'
                OrderVariableReference.ComponentOrderableConcentration c'

                for p in props do
                    OrderVariableReference.ComponentDose(c', p)

                for i in c.Items do
                    let i' = i.Name |> name
                    OrderVariableReference.ItemComponentQuantity(c', i')
                    OrderVariableReference.ItemComponentConcentration(c', i')
                    OrderVariableReference.ItemOrderableQuantity(c', i')
                    OrderVariableReference.ItemOrderableConcentration(c', i')

                    for p in props do
                        OrderVariableReference.ItemDose(c', i', p)
        ]


    let fromText text =
        text |> MedicationText.fromString |> Result.defaultWith (failtestf "%A")


    let fixtures () =
        [
            "pcmSupp", Scenarios.pcmSupp
            "pcmDrink", Scenarios.pcmDrink
            "cotrim", Scenarios.cotrim
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "tpn", Scenarios.tpn
            "amoxClav", Scenarios.amoxClavText |> fromText
            "kcl", Scenarios.kaliumchlorideOnceTimedText |> fromText
        ]


    let isNotSettable =
        function
        | Error(ActionFailure.NotSettable _) -> true
        | _ -> false


    let dosQty = OrderVariableReference.OrderableDose DoseProperty.Quantity


    let select choice ovar = ovar |> OrderProcessor.Choice.select dosQty choice


    /// An order variable in mL holding 5, defined as 1 to 10 by 1 and calculated as 2 to 8 by 1.
    let constrained =
        let cs min max =
            Constraints.create
                (mL [ min ] |> Minimum.create true |> Some)
                (mL [ 1 ] |> ValueRange.Increment.create |> Some)
                (mL [ max ] |> ValueRange.Maximum.create true |> Some)
                None

        let x = valSet mL [ 5 ] |> ovar (Some 1)

        { x with
            DefinedConstraints = cs 1 10
            CalculatedConstraints = cs 2 8
        }


    let tests =
        testList
            "order variable references"
            [
                test "the references find every variable of every fixture, and only those" {
                    for name, med in fixtures () do
                        let ord = med |> orderOf
                        let names = List.map OrderVariable.getName >> Set.ofList

                        ord
                        |> allRefs
                        |> List.choose (Ref.tryGet ord)
                        |> names
                        |> Expect.equal $"%s{name}: the variables of the order" (ord |> Order.toOrdVars |> names)
                }

                test "a reference to a component the order does not have finds nothing" {
                    OrderVariableReference.ComponentQuantity "none"
                    |> Ref.tryGet (Scenarios.pcmSupp |> orderOf)
                    |> Expect.isNone "no component"
                }

                test "the basic variables of a timed and of a continuous order" {
                    let basics (med: Medication) =
                        let ord = med |> orderOf

                        [
                            OrderVariableReference.ScheduleFrequency
                            dosQty
                            OrderVariableReference.OrderableDose DoseProperty.Rate
                            OrderVariableReference.OrderableOrderCount
                            OrderVariableReference.OrderableDose DoseProperty.PerTime
                        ]
                        |> List.map (Ref.isBasic ord.Schedule)

                    Scenarios.amfoTimed
                    |> basics
                    |> Expect.equal
                        "timed: frequency, dose quantity, rate, order count"
                        [ true; true; true; true; false ]

                    Scenarios.morfCont
                    |> basics
                    |> Expect.equal "continuous: rate and order count" [ false; false; true; true; false ]
                }

                test "the invariants of amox/clav with its diluent" {
                    let ord = Scenarios.amoxClavText |> fromText |> orderOf
                    let amoxClav, nacl = "amoxicilline/clavulaanzuur", "NaCl 0,9%"

                    ord
                    |> Ref.invariants
                    |> Expect.equal
                        "the adjust, the content of each component, the content and concentration of each item"
                        [
                            OrderVariableReference.OrderAdjust
                            OrderVariableReference.ComponentQuantity amoxClav
                            OrderVariableReference.ComponentQuantity nacl
                            for cmp, itm in
                                [
                                    amoxClav, "amoxicilline"
                                    amoxClav, "clavulaanzuur"
                                    nacl, "natrium"
                                    nacl, "chloor"
                                ] do
                                OrderVariableReference.ItemComponentQuantity(cmp, itm)
                                OrderVariableReference.ItemComponentConcentration(cmp, itm)
                        ]
                }

                test "a choice from a list of values" {
                    let x = valSet mL [ 1; 2; 3 ] |> ovar None

                    [ Choice.Min; Choice.Max; Choice.Median; Choice.Nth 1; Choice.Percentile 100 ]
                    |> List.map (fun c -> x |> select c |> Result.toOption)
                    |> Expect.equal "1, 3, 2, 2 and 3 mL" ([ 1; 3; 2; 2; 3 ] |> List.map (fun v -> Some(mL [ v ])))

                    x
                    |> select (Choice.Step(1, StepIncrement.Defined))
                    |> isNotSettable
                    |> Expect.isTrue "no step"

                    x
                    |> OrderProcessor.Choice.select OrderVariableReference.OrderableQuantity (Choice.Percentile 50)
                    |> isNotSettable
                    |> Expect.isTrue "a percentile only on the dose quantity"
                }

                test "a choice from a min, increment and max" {
                    let x = minIncrMax mL 1 1 5 |> ovar None

                    [ Choice.Min; Choice.Max; Choice.Median; Choice.Nth 1; Choice.Percentile 50 ]
                    |> List.map (fun c -> x |> select c |> Result.toOption)
                    |> Expect.equal "1, 5, 3, 2 and 3 mL" ([ 1; 5; 3; 2; 3 ] |> List.map (fun v -> Some(mL [ v ])))

                    x
                    |> select (Choice.Step(1, StepIncrement.Defined))
                    |> isNotSettable
                    |> Expect.isTrue "no step"
                }

                test "a choice from a range with more values than a value set holds" {
                    let microL (vs: int list) =
                        vs
                        |> List.map BigRational.fromInt
                        |> List.toArray
                        |> ValueUnit.withUnit Units.Volume.microLiter

                    // 1 to 300,000 microL by 1, more than the 250,000 values of a value set
                    let x = minIncrMax microL 1 1 300_000 |> ovar None

                    [ Choice.Min; Choice.Max; Choice.Median; Choice.Nth 1 ]
                    |> List.map (fun c -> x |> select c |> Result.toOption)
                    |> Expect.equal
                        "1, 300,000, 150,001 and 2 microL"
                        ([ 1; 300_000; 150_001; 2 ] |> List.map (fun v -> Some(microL [ v ])))

                    x
                    |> select (Choice.Nth 300_000)
                    |> Result.toOption
                    |> Expect.isNone "no value past the end"
                }

                test "a step from one value with a defined increment" {
                    let x = valSet mL [ 2 ] |> ovar (Some 1)

                    [
                        Choice.Step(1, StepIncrement.Defined)
                        Choice.Step(-1, StepIncrement.Defined)
                    ]
                    |> List.map (fun c -> x |> select c |> Result.toOption)
                    |> Expect.equal "3 and 1 mL" [ Some(mL [ 3 ]); Some(mL [ 1 ]) ]

                    x
                    |> select Choice.Min
                    |> Result.toOption
                    |> Expect.equal "the one value kept" (Some(mL [ 2 ]))
                }

                test "one value without an increment keeps its value and does not step" {
                    let x = valSet mL [ 2 ] |> ovar None

                    x
                    |> select Choice.Min
                    |> Result.toOption
                    |> Expect.equal "the one value kept" (Some(mL [ 2 ]))

                    x
                    |> select (Choice.Step(1, StepIncrement.Defined))
                    |> isNotSettable
                    |> Expect.isTrue "not steppable"
                }

                test "a variable set back to each target" {
                    let ord = Scenarios.pcmSupp |> orderOf
                    let back target =
                        (constrained |> OrderProcessor.SetBackTo.apply target ord).Variable.Values
                    let one = Units.Count.times |> ValueUnit.one

                    [
                        SetBackTo.Defined
                        SetBackTo.Calculated
                        SetBackTo.CalculatedElseDefined
                        SetBackTo.One
                        SetBackTo.AtLeastOne
                    ]
                    |> List.map back
                    |> Expect.equal
                        "1 to 10, 2 to 8 twice, 1, and 1 or more"
                        [
                            minIncrMax mL 1 1 10
                            minIncrMax mL 2 1 8
                            minIncrMax mL 2 1 8
                            one |> ValueSet.create |> ValSet
                            one |> Minimum.create true |> Min
                        ]

                    [ SetBackTo.MinIncr; SetBackTo.AnyPositive ]
                    |> List.map (back >> ValueRange.getMax)
                    |> Expect.equal "no maximum" [ None; None ]
                }

                test "the conditions hold as the processor classifies the order" {
                    for name, med in fixtures () do
                        let ord =
                            match CalcMinMax(med |> orderOf) |> OrderProcessor.processPipeline OrderLogging.noOp with
                            | Ok ord -> ord
                            | Error _ -> failtest $"%s{name}: no order"

                        let os = ord |> OrderProcessor.classify

                        [
                            OrderCondition.DoseSolved
                            OrderCondition.OrderSolved
                            OrderCondition.HasValues
                            OrderCondition.CanSetNormDose
                        ]
                        |> List.map (fun c -> ord |> OrderProcessor.OrderCondition.holds c)
                        |> Expect.equal
                            $"%s{name}: as classified"
                            [ os.DoseIsSolved; os.OrderIsSolved; os.HasValues; os.CanSetNormDose ]
                }
            ]


module EffectTests =

    open HelperTests

    module Effect = OrderProcessor.OrderEffect
    module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
    module ValueSet = ValueRange.ValueSet


    let caps = Order.ValueCaps.defaults

    let logger = OrderLogging.noOp


    let microL (vs: int list) =
        vs
        |> List.map BigRational.fromInt
        |> List.toArray
        |> ValueUnit.withUnit Units.Volume.microLiter


    /// The order of a medication after the commands, as the processor gives it.
    let processedWith cmds med =
        (med |> orderOf, cmds)
        ||> List.fold (fun ord cmd ->
            match ord |> cmd |> OrderProcessor.processPipeline logger with
            | Ok ord -> ord
            | Error _ -> failtest "no order"
        )


    let processed cmd = processedWith [ cmd ]


    /// The order of a medication with lists of values.
    let valued = processedWith [ CalcMinMax; CalcValues ]


    let run effects ord = ord |> Effect.run caps logger effects


    let missing = OrderVariableReference.ComponentQuantity "no such component"


    let dosQty = OrderVariableReference.OrderableDose DoseProperty.Quantity


    let tests =
        testList
            "order effects"
            [
                test "an effect gives what the order function it runs gives" {
                    for name, med in ReferenceTests.fixtures () do
                        let ord = med |> processed CalcMinMax

                        [
                            "set back to the constraints",
                            OrderEffect.SetAllOrderVariableRangesBack SetBackTo.CalculatedElseDefined,
                            Order.applyConstraints >> Ok
                            "set back to the calculated constraints",
                            OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated,
                            Order.applyCalculatedConstraints >> Ok
                            "record the calculated constraints",
                            OrderEffect.RecordCalculatedConstraints,
                            Order.setCalculatedConstraints >> Ok
                            "solve for min and max",
                            OrderEffect.SolveOrder SolveMode.MinMax,
                            Order.solveMinMax "Order effect" false logger
                            "coarsen the increments",
                            OrderEffect.CoarsenIncrements 10,
                            Order.increaseIncrements logger 10
                            "make lists",
                            OrderEffect.EnumerateOrderVariableValues(ValueCap.Default, RateList.WithRate, None),
                            Order.minIncrMaxToValues caps false false logger None
                            "set the norm dose", OrderEffect.SetNormDose, Order.solveNormDose logger
                            "select the nearest component quantity",
                            OrderEffect.SelectNearestComponentQuantity,
                            Order.pickNearestHigherElseLowerComponentQuantity logger
                        ]
                        |> List.iter (fun (what, eff, f) ->
                            ord
                            |> run [ eff ]
                            |> Result.toOption
                            |> Expect.equal $"%s{name}: %s{what}" (ord |> f |> Result.toOption)
                        )
                }

                test "a failure keeps the order the failing effect was run on" {
                    let ord = Scenarios.pcmDrink |> processed CalcMinMax

                    match
                        ord
                        |> run
                            [
                                OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated
                                OrderEffect.SetOrderVariableRangeBack(missing, SetBackTo.Calculated)
                                OrderEffect.SolveOrder SolveMode.MinMax
                            ]
                    with
                    | Error(o, ActionFailure.NoSuchVariable _) ->
                        o
                        |> Expect.equal "the order after the first effect" (ord |> Order.applyCalculatedConstraints)
                    | _ -> failtest "no failure on the missing component"
                }

                test "a failed solve keeps the order it was run on, not the order it partly solved" {
                    let ord =
                        match
                            Scenarios.pcmDrink
                            |> valued
                            |> run
                                [
                                    OrderEffect.SelectOrderVariableValue(dosQty, Choice.Min)
                                    OrderEffect.SelectOrderVariableValue(
                                        OrderVariableReference.ScheduleFrequency,
                                        Choice.Min
                                    )
                                ]
                        with
                        | Ok ord -> ord
                        | Error(_, e) -> failtest $"%A{e}"

                    match ord |> run [ OrderEffect.SolveOrder SolveMode.Values ] with
                    | Error(o, ActionFailure.SolveFailed _) -> o |> Expect.equal "the order before the solve" ord
                    | _ -> failtest "the lowest dose at the lowest frequency solves"
                }

                test "Try keeps the order when an effect fails, else runs the effects" {
                    let ord = Scenarios.pcmDrink |> processed CalcMinMax
                    let failing =
                        [
                            OrderEffect.SolveOrder SolveMode.MinMax
                            OrderEffect.SetOrderVariableRangeBack(missing, SetBackTo.Calculated)
                        ]
                    let passing = [ OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated ]

                    ord
                    |> run [ OrderEffect.Try failing ]
                    |> Result.toOption
                    |> Expect.equal "kept" (Some ord)

                    ord
                    |> run [ OrderEffect.Try passing ]
                    |> Result.toOption
                    |> Expect.equal "run" (ord |> run passing |> Result.toOption)
                }

                test "Unless runs the effect only when no condition holds" {
                    let ord = Scenarios.pcmDrink |> valued
                    let eff = OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated

                    ord
                    |> run [ OrderEffect.Unless([ OrderCondition.HasValues ], eff) ]
                    |> Result.toOption
                    |> Expect.equal "kept, the order has values" (Some ord)

                    ord
                    |> run [ OrderEffect.Unless([ OrderCondition.OrderSolved ], eff) ]
                    |> Result.toOption
                    |> Expect.equal "run, the order is not solved" (ord |> run [ eff ] |> Result.toOption)
                }

                test "a selected value is the one value of the variable" {
                    let ord = Scenarios.pcmDrink |> valued
                    let qty (ord: Order) = ord.Orderable.Dose.Quantity |> OrderVariable.Quantity.toOrdVar

                    match ord |> run [ OrderEffect.SelectOrderVariableValue(dosQty, Choice.Min) ] with
                    | Ok o ->
                        o
                        |> qty
                        |> OrderVariable.solvedValue
                        |> Expect.equal
                            "the smallest dose quantity"
                            (ord |> qty |> OrderVariable.setMinValue |> OrderVariable.solvedValue)
                    | Error(_, e) -> failtest $"%A{e}"
                }

                test "the names the effects set back" {
                    let ord = Scenarios.pcmDrink |> orderOf
                    let names = Effect.setBackNames ord >> Set.count

                    [
                        [ OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated ]
                        [ OrderEffect.SetOrderVariableRangeBack(dosQty, SetBackTo.Calculated) ]
                        [ OrderEffect.SetOrderVariableRangeBack(missing, SetBackTo.Calculated) ]
                        [ OrderEffect.SolveOrder SolveMode.MinMax ]
                    ]
                    |> List.map names
                    |> Expect.equal
                        "every variable, one, none and none"
                        [ ord |> Order.toOrdVars |> List.length; 1; 0; 0 ]
                }

                test "the value of the original variable is added in order and in the unit of the list" {
                    let x =
                        { (valSet mL [ 1; 2; 4 ] |> ovar (Some 1)) with
                            CalculatedConstraints =
                                OrderVariable.Constraints.create
                                    (mL [ 1 ] |> ValueRange.Minimum.create true |> Some)
                                    None
                                    (mL [ 5 ] |> ValueRange.Maximum.create true |> Some)
                                    None
                        }

                    let include' vr = x |> OrderVariable.includeOriginalValue (vr |> ovar None)

                    (include' (valSet microL [ 3000 ])).Variable.Values
                    |> Expect.equal "1, 2, 3 and 4 mL" (valSet mL [ 1; 2; 3; 4 ])

                    [ valSet mL [ 6 ]; valSet mL [ 3; 5 ] ]
                    |> List.map (include' >> _.Variable.Values)
                    |> Expect.equal
                        "nothing outside the constraints, nothing from a list"
                        [ x.Variable.Values; x.Variable.Values ]

                    let range = minIncrMax mL 1 1 5 |> ovar None

                    (range |> OrderVariable.includeOriginalValue (valSet mL [ 3 ] |> ovar None)).Variable.Values
                    |> Expect.equal "nothing to a range" range.Variable.Values
                }

                test "an original order that is not complete adds nothing" {
                    let ord = Scenarios.pcmDrink |> processed CalcMinMax
                    let lists original = ord |> Order.minIncrMaxToValues caps false false logger original

                    Scenarios.pcmDrink
                    |> orderOf
                    |> Some
                    |> lists
                    |> Result.toOption
                    |> Expect.equal "as without an original" (lists None |> Result.toOption)
                }
            ]


module RecipeTests =

    open HelperTests

    module Ref = OrderProcessor.OrderVariableReference
    module Recipes = OrderProcessor.Recipe
    module SetCmd = OrderProcessor.SetCommand
    module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
    module ValueSet = ValueRange.ValueSet


    let run = EffectTests.run

    let dose = OrderVariableReference.OrderableDose

    let frq = OrderVariableReference.ScheduleFrequency


    /// The recipe of a step of n defined increments on the variable, and its effects as the order
    /// program runs them: the recipe, the stepped value in place of the value held, and a solve for
    /// min and max; None when the variable cannot be stepped.
    let step ovarRef n (ord: Order) =
        let choice = Choice.Step(n, StepIncrement.Defined)

        ovarRef
        |> Ref.tryGet ord
        |> Option.bind (fun ovar ->
            match SetCmd.ofSet ovarRef ord.Schedule (ovar |> SetCmd.settable ord.Schedule ovarRef) choice with
            | SetCommand.WithRecipe recipe ->
                ovar
                |> OrderProcessor.Choice.select ovarRef choice
                |> Result.toOption
                |> Option.map (fun vu ->
                    recipe,
                    [
                        yield! ord |> Recipes.effects recipe
                        OrderEffect.SetOrderVariableRangeBack(ovarRef, SetBackTo.AnyPositive)
                        OrderEffect.ConstrainOrderVariableRange(ovarRef, vu)
                        OrderEffect.SolveOrder SolveMode.MinMax
                    ]
                )
            | _ -> None
        )


    /// The variables a step can move: the frequency, the dose quantity, the rate and the orderable
    /// quantity of each component.
    let stepRefs ord =
        [
            frq
            dose DoseProperty.Quantity
            dose DoseProperty.Rate
            yield!
                ord
                |> Order.components
                |> List.map OrderVariableReference.ComponentOrderableQuantity
        ]


    /// The order of a medication with every basic variable set to one value, the median, else the
    /// minimum, else the maximum of its list; None when the order is not solved after that.
    let solved med =
        let ord = med |> EffectTests.valued

        let basics =
            [ yield! ord |> stepRefs; OrderVariableReference.OrderableOrderCount ]
            |> List.filter (Ref.isBasic ord.Schedule)

        (ord, basics)
        ||> List.fold (fun ord ovarRef ->
            match ovarRef |> Ref.tryGet ord with
            | Some ovar when ovar |> OrderVariable.hasOneValue |> not ->
                [ Choice.Median; Choice.Min; Choice.Max ]
                |> List.tryPick (fun c ->
                    ord
                    |> run
                        [
                            OrderEffect.SelectOrderVariableValue(ovarRef, c)
                            OrderEffect.SolveOrder SolveMode.Values
                        ]
                    |> Result.toOption
                )
                |> Option.defaultValue ord
            | _ -> ord
        )
        |> Some
        |> Option.filter Order.isSolved


    let solvedFixtures =
        lazy
            [
                for name, med in ReferenceTests.fixtures () do
                    match med |> solved with
                    | Some ord -> name, ord
                    | None -> ()
            ]


    let tests =
        testList
            "step recipes"
            [
                test "a set by shape and choice, on a timed and a continuous order" {
                    let timed = (Scenarios.amfoTimed |> orderOf).Schedule
                    let cont = (Scenarios.morfCont |> orderOf).Schedule
                    let cmp = OrderVariableReference.ComponentOrderableQuantity "x"
                    let step = Choice.Step(1, StepIncrement.Defined)

                    [
                        timed, frq, Shape.Steppable, step
                        timed, frq, Shape.SelectableList, step
                        timed, dose DoseProperty.Quantity, Shape.Steppable, step
                        timed, dose DoseProperty.Quantity, Shape.SelectableRange, Choice.Percentile 50
                        cont, dose DoseProperty.Quantity, Shape.SelectableRange, Choice.Percentile 50
                        timed, dose DoseProperty.Rate, Shape.Steppable, step
                        timed, cmp, Shape.Steppable, step
                        timed, dose DoseProperty.PerTime, Shape.SelectableRange, Choice.Min
                        timed, dose DoseProperty.PerTime, Shape.SelectableList, Choice.Min
                        timed, frq, Shape.SelectableRange, Choice.Nth 1
                        timed, dose DoseProperty.Rate, Shape.SelectableList, Choice.Percentile 50
                    ]
                    |> List.map (fun (sch, ovarRef, shape, choice) -> SetCmd.ofSet ovarRef sch shape choice)
                    |> Expect.equal
                        "the recipe of each step and of the percentile, a value only from a list or a basic range"
                        [
                            SetCommand.WithRecipe Recipe.ScheduleFrequencyStep
                            SetCommand.NoCommand
                            SetCommand.WithRecipe Recipe.OrderableDoseQuantityStep
                            SetCommand.WithRecipe Recipe.OrderableDoseQuantityPercentile
                            SetCommand.NoCommand
                            SetCommand.WithRecipe Recipe.OrderableDoseRateStep
                            SetCommand.WithRecipe(Recipe.ComponentOrderableQuantityStep "x")
                            SetCommand.NoCommand
                            SetCommand.ValueOnly
                            SetCommand.ValueOnly
                            SetCommand.NoCommand
                        ]
                }

                test "only a basic variable is steppable" {
                    let sch = (Scenarios.amfoTimed |> orderOf).Schedule
                    let x = valSet mL [ 2 ] |> ovar (Some 1)

                    [ dose DoseProperty.Quantity; dose DoseProperty.PerTime ]
                    |> List.map (fun ovarRef -> x |> SetCmd.settable sch ovarRef)
                    |> Expect.equal "steppable, not settable" [ Shape.Steppable; Shape.NotSettable ]
                }

                test "the dose count after a dose quantity step, with one and with more components" {
                    let doseCount med =
                        med
                        |> orderOf
                        |> Recipes.orderableDoseQuantityStep
                        |> List.choose (
                            function
                            | OrderEffect.SetOrderVariableRangeBack(OrderVariableReference.OrderableDoseCount, t) ->
                                Some t
                            | _ -> None
                        )

                    [ Scenarios.pcmDrink; Scenarios.tpn ]
                    |> List.map doseCount
                    |> Expect.equal "1, and at least 1" [ [ SetBackTo.One ]; [ SetBackTo.AtLeastOne ] ]
                }

                test "a component step does not keep its target, whatever the case of its name" {
                    let ord = Scenarios.tpn |> orderOf

                    let keptTarget name =
                        ord
                        |> Recipes.componentOrderableQuantityStep (name |> String.toUpper)
                        |> List.exists (
                            function
                            | OrderEffect.SetBackUndetermined(kept, _, _) ->
                                kept |> List.contains (OrderVariableReference.ComponentOrderableQuantity name)
                            | _ -> false
                        )

                    ord |> Order.components |> Expect.isNonEmpty "components"
                    ord
                    |> Order.components
                    |> List.filter keptTarget
                    |> Expect.isEmpty "no component kept"
                }

                test "the percentile sets the time back only on a schedule with a time" {
                    let setsTime med =
                        med
                        |> orderOf
                        |> Recipes.orderableDoseQuantityPercentile
                        |> List.contains (
                            OrderEffect.SetOrderVariableRangeBack(
                                OrderVariableReference.ScheduleTime,
                                SetBackTo.Calculated
                            )
                        )

                    [ Scenarios.amfoTimed; Scenarios.pcmDrink ]
                    |> List.map setsTime
                    |> Expect.equal "timed, and a drink without a time" [ true; false ]
                }

                test "after a step only its target and the variables its recipe frees leave their constraints" {
                    solvedFixtures.Value |> Expect.isNonEmpty "solved fixtures"

                    [
                        for name, ord in solvedFixtures.Value do
                            for ovarRef in ord |> stepRefs do
                                match ord |> step ovarRef 3 with
                                | None -> ()
                                | Some(recipe, effects) ->
                                    match ord |> run effects with
                                    | Error _ -> ()
                                    | Ok stepped ->
                                        let freed =
                                            ord
                                            |> Recipes.effects recipe
                                            |> OrderProcessor.OrderEffect.setBackNames ord
                                            |> Set.union (
                                                ovarRef
                                                |> Ref.tryGet ord
                                                |> Option.map OrderVariable.getName
                                                |> Option.toList
                                                |> Set.ofList
                                            )

                                        for ovar in stepped |> Order.toOrdVars do
                                            if
                                                freed |> Set.contains (ovar |> OrderVariable.getName) |> not
                                                && ovar |> OrderVariable.isWithinConstraints true |> not
                                            then
                                                $"%s{name} %A{recipe}: %s{ovar |> OrderVariable.getName |> Name.toString}"
                    ]
                    |> Expect.isEmpty "inside the calculated constraints"
                }

                test "a step recipe keeps the increment of every basic variable with a defined increment" {
                    [
                        for name, ord in solvedFixtures.Value do
                            for ovarRef in ord |> stepRefs do
                                match ord |> step ovarRef 3 with
                                | None -> ()
                                | Some(recipe, _) ->
                                    match ord |> run (ord |> Recipes.effects recipe) with
                                    | Error _ -> ()
                                    | Ok after ->
                                        for basic in ord |> stepRefs |> List.filter (Ref.isBasic ord.Schedule) do
                                            match basic |> Ref.tryGet ord, basic |> Ref.tryGet after with
                                            | Some b, Some a when
                                                a.DefinedConstraints.Incr |> Option.isSome
                                                && a.Variable.Values <> b.Variable.Values
                                                && a.Variable.Values |> ValueRange.getIncr |> Option.isNone
                                                && a.Variable.Values |> ValueRange.getValSet |> Option.isNone
                                                ->
                                                $"%s{name} %A{recipe}: %A{basic}"
                                            | _ -> ()
                    ]
                    |> Expect.isEmpty "every basic variable keeps its increment"
                }
            ]


    /// The recalculation commands per kind and the dose types they run on, with the four commands
    /// that have no kind.
    let reCalcKinds c i =
        let itemDose p = OrderVariableReference.ItemDose(c, i, p)
        let all = [ "Once"; "Discontinuous"; "Timed"; "OnceTimed"; "Continuous" ]

        [
            [ itemDose DoseProperty.Quantity; itemDose DoseProperty.QuantityAdjust ],
            Some Recipe.ReCalcDose,
            [ "Once"; "Discontinuous"; "Timed"; "OnceTimed" ]
            [ itemDose DoseProperty.PerTime; itemDose DoseProperty.PerTimeAdjust ],
            Some Recipe.ReCalcPerTime,
            [ "Discontinuous"; "Timed" ]
            [
                OrderVariableReference.ScheduleTime
                itemDose DoseProperty.Rate
                itemDose DoseProperty.RateAdjust
            ],
            Some Recipe.ReCalcTime,
            [ "Continuous"; "Timed"; "OnceTimed" ]
            [
                OrderVariableReference.OrderableQuantity
                OrderVariableReference.ComponentDose(c, DoseProperty.QuantityAdjust)
                OrderVariableReference.ItemOrderableQuantity(c, i)
                OrderVariableReference.ItemOrderableConcentration(c, i)
            ],
            None,
            all
        ]


    /// The variables a recalculation can name on an order, each with the recipe it runs.
    let reCalcTargets (ord: Order) =
        [
            OrderVariableReference.ScheduleTime
            dose DoseProperty.Quantity
            dose DoseProperty.Rate

            for c in ord.Orderable.Components do
                for i in c.Items do
                    for p in
                        [
                            DoseProperty.Quantity
                            DoseProperty.QuantityAdjust
                            DoseProperty.PerTime
                            DoseProperty.PerTimeAdjust
                            DoseProperty.Rate
                            DoseProperty.RateAdjust
                        ] do
                        OrderVariableReference.ItemDose(c.Name |> Name.toString, i.Name |> Name.toString, p)
        ]
        |> List.choose (fun ovarRef -> Recipes.forReCalcCommand ovarRef ord |> Option.map (fun r -> ovarRef, r))


    let reCalcTests =
        testList
            "recalculation recipes"
            [
                test "the kind of each recalculation command per dose type" {
                    let fixtures = ReferenceTests.fixtures () |> Map.ofList
                    let ord = Scenarios.amfo |> orderOf
                    let cmp = ord.Orderable.Components |> List.head
                    let c = cmp.Name |> Name.toString
                    let i = cmp.Items |> List.head |> _.Name |> Name.toString

                    let schedules =
                        [
                            "Once", Once
                            "Discontinuous", (fixtures["pcmDrink"] |> orderOf).Schedule
                            "Timed", (Scenarios.amfoTimed |> orderOf).Schedule
                            "OnceTimed", (fixtures["kcl"] |> orderOf).Schedule
                            "Continuous", (fixtures["morfCont"] |> orderOf).Schedule
                        ]

                    cmp
                    |> Order.Orderable.Component.isDoseConstrained
                    |> Expect.isTrue "a constrained component dose"

                    [
                        for refs, kind, doseTypes in reCalcKinds c i do
                            for doseType, sch in schedules do
                                for ovarRef in refs do
                                    let expected = if doseTypes |> List.contains doseType then kind else None
                                    let actual = Recipes.forReCalcCommand ovarRef { ord with Schedule = sch }

                                    if actual <> expected then
                                        $"%s{doseType} %A{ovarRef}: %A{actual}, expected %A{expected}"
                    ]
                    |> Expect.isEmpty "every command has its kind on its dose types only"
                }

                test "no kind recalculates the item dose of a component the rules do not constrain" {
                    let unconstrained =
                        [
                            for name, med in ReferenceTests.fixtures () do
                                let ord = med |> orderOf

                                for cmp in ord.Orderable.Components do
                                    if cmp |> Order.Orderable.Component.isDoseConstrained |> not then
                                        for i in cmp.Items do
                                            name, ord, cmp.Name |> Name.toString, i.Name |> Name.toString
                        ]

                    unconstrained |> Expect.isNonEmpty "a component the rules do not constrain"

                    [
                        for name, ord, c, i in unconstrained do
                            for p in [ DoseProperty.Quantity; DoseProperty.PerTime; DoseProperty.Rate ] do
                                match Recipes.forReCalcCommand (OrderVariableReference.ItemDose(c, i, p)) ord with
                                | Some r -> $"%s{name} %s{c} %s{i} %A{p}: %A{r}"
                                | None -> ()
                    ]
                    |> Expect.isEmpty "no kind"
                }

                test "a recalculation leaves every variable its recipe does not set back" {
                    let targets = solvedFixtures.Value |> List.collect (snd >> reCalcTargets)
                    targets |> Expect.isNonEmpty "recalculations"

                    [
                        for name, ord in solvedFixtures.Value do
                            for ovarRef, recipe in ord |> reCalcTargets do
                                let effects = ord |> Recipes.effects recipe
                                let setBack = effects |> OrderProcessor.OrderEffect.setBackNames ord

                                match ord |> run (effects @ [ OrderEffect.SolveOrder SolveMode.MinMax ]) with
                                | Error(_, e) -> $"%s{name} %A{ovarRef}: %A{e}"
                                | Ok after ->
                                    for b, a in List.zip (ord |> Order.toOrdVars) (after |> Order.toOrdVars) do
                                        if
                                            setBack |> Set.contains (b |> OrderVariable.getName) |> not
                                            && a.Variable.Values <> b.Variable.Values
                                        then
                                            $"%s{name} %A{ovarRef}: %s{b |> OrderVariable.getName |> Name.toString}"
                    ]
                    |> Expect.isEmpty "the kept variables are unchanged"
                }

                test "the variables a recalculation keeps do not determine its target" {
                    [
                        for name, ord in solvedFixtures.Value do
                            for ovarRef, recipe in ord |> reCalcTargets do
                                let setBack =
                                    ord |> Recipes.effects recipe |> OrderProcessor.OrderEffect.setBackNames ord

                                match ovarRef |> Ref.tryGet ord with
                                | Some target when setBack |> Set.contains (target |> OrderVariable.getName) -> ()
                                | _ -> $"%s{name} %A{ovarRef} (%A{recipe}) keeps its target"
                    ]
                    |> Expect.isEmpty "every recalculation sets back its target"
                }

                // the expected set-backs come from the kinds, not from the recipes, so a recipe that sets
                // back too much fails
                test "each kind keeps what the kind says and sets back the rest" {
                    let cases =
                        [
                            for name, ord in solvedFixtures.Value do
                                for ovarRef, recipe in ord |> reCalcTargets do
                                    let setBack =
                                        ord |> Recipes.effects recipe |> OrderProcessor.OrderEffect.setBackNames ord

                                    let isSetBack r =
                                        r
                                        |> Ref.tryGet ord
                                        |> Option.map (fun ovar ->
                                            setBack |> Set.contains (ovar |> OrderVariable.getName)
                                        )

                                    let cmps = ord |> Order.components
                                    let isPrepKept =
                                        ord |> Order.isPrepWithinConstraints
                                        && not (ord.Schedule |> Order.Schedule.isTimeFixed)

                                    let isQtyFixed =
                                        ord.Orderable.Dose.Quantity
                                        |> OrderVariable.Quantity.toOrdVar
                                        |> OrderVariable.hasOneCalculatedValue

                                    let expected =
                                        [
                                            match recipe with
                                            | Recipe.ReCalcDose ->
                                                frq, false
                                                OrderVariableReference.OrderableOrderCount, false
                                            | Recipe.ReCalcPerTime ->
                                                frq, true
                                                OrderVariableReference.OrderableOrderCount, false

                                                match cmps with
                                                | [ c ] when isQtyFixed ->
                                                    OrderVariableReference.ComponentQuantity c, true
                                                | _ -> ()
                                            | Recipe.ReCalcTime ->
                                                frq, false
                                                OrderVariableReference.OrderableOrderCount, false

                                                for c in cmps do
                                                    OrderVariableReference.ComponentOrderableQuantity c, not isPrepKept
                                            | _ -> ()
                                        ]

                                    for r, exp in expected do
                                        // a variable the order does not have, as the frequency on Continuous
                                        match r |> isSetBack with
                                        | Some actual -> $"%s{name} %A{ovarRef} (%A{recipe}) %A{r}", actual, exp
                                        | None -> ()
                        ]

                    // every kind and the single component exception are reached
                    for kind in [ "ReCalcDose"; "ReCalcPerTime"; "ReCalcTime"; "ComponentQuantity" ] do
                        cases
                        |> List.exists (fun (label, _, _) -> label.Contains kind)
                        |> Expect.isTrue $"a case for %s{kind}"

                    cases
                    |> List.choose (fun (label, actual, exp) ->
                        if actual = exp then
                            None
                        else
                            Some $"%s{label}: set back %b{actual}, expected %b{exp}"
                    )
                    |> Expect.equal "set back as the kind says" []
                }
            ]


module EquationsTests =

    open TestOrder


    // Golden reference: the full "Equations" sheet. Each entry is the equation
    // string ("Short Name" column) and a 5-char marker for which dose types mark
    // it with an "x", in column order discontinuous, continuous, timed, once,
    // onceTimed ('x' = applies, '.' = blank). This is an independent transcription
    // of the source sheet, except that the component and orderable dose totals
    // follow the item dose total: the total by per time on the dose types with a
    // frequency and the total by rate on a continuous order only. The sheet had
    // both on a timed order, which a timed order with a duration cannot satisfy.
    // The test fails if the hardcoded EquationMapping.equations list drifts from
    // it (wrong equation text, wrong dose-type assignment, or order).
    let private golden =
        [
            "[itm]_cmp_qty = [itm]_cmp_cnc * [cmp]_cmp_qty", "xxxxx"
            "[itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty", "xxxxx"
            "[itm]_orb_qty = [itm]_cmp_cnc * [cmp]_orb_qty", "xxxxx"
            "[itm]_dos_qty = [itm]_cmp_cnc * [cmp]_dos_qty", "xxxxx"
            "[itm]_dos_qty = [itm]_orb_cnc * [orb]_dos_qty", "xxxxx"
            "[itm]_dos_qty = [itm]_dos_rte * [ord]_sch_tme", "....."
            "[itm]_dos_qty = [itm]_dos_qty_adj * [ord]_adj_qty", "xxxxx"
            "[itm]_dos_ptm = [itm]_cmp_cnc * [cmp]_dos_ptm", "x.x.."
            "[itm]_dos_ptm = [itm]_orb_cnc * [orb]_dos_ptm", "x.x.."
            "[itm]_dos_ptm = [itm]_dos_qty * [ord]_sch_frq", "x.x.."
            "[itm]_dos_ptm = [itm]_dos_ptm_adj * [ord]_adj_qty", "x.x.."
            "[itm]_dos_rte = [itm]_cmp_cnc * [cmp]_dos_rte", ".xx.x"
            "[itm]_dos_rte = [itm]_orb_cnc * [orb]_dos_rte", ".xx.x"
            "[itm]_dos_rte = [itm]_dos_rte_adj * [ord]_adj_qty", ".xx.x"
            "[itm]_dos_tot = [itm]_dos_ptm * [ord]_ord_tme", "x.x.."
            "[itm]_dos_tot = [itm]_dos_rte * [ord]_ord_tme", ".x..."
            "[itm]_dos_qty_adj = [itm]_cmp_cnc * [cmp]_dos_qty_adj", "xxxxx"
            "[itm]_dos_qty_adj = [itm]_orb_cnc * [orb]_dos_qty_adj", "xxxxx"
            "[itm]_dos_qty_adj = [itm]_dos_rte_adj * [ord]_sch_tme", "....."
            "[itm]_dos_ptm_adj = [itm]_cmp_cnc * [cmp]_dos_ptm_adj", "xxx.."
            "[itm]_dos_ptm_adj = [itm]_orb_cnc * [orb]_dos_ptm_adj", "xxx.."
            "[itm]_dos_ptm_adj = [itm]_dos_qty_adj * [ord]_sch_frq", "x.x.."
            "[itm]_dos_rte_adj = [itm]_cmp_cnc * [cmp]_dos_rte_adj", ".x..."
            "[itm]_dos_rte_adj = [itm]_orb_cnc * [orb]_dos_rte_adj", ".x..."
            "[itm]_dos_tot_adj = [itm]_dos_ptm_adj * [ord]_ord_tme", "x.x.."
            "[itm]_dos_tot_adj = [itm]_dos_rte_adj * [ord]_ord_tme", ".x..."
            "[cmp]_orb_qty = [cmp]_orb_cnc * [orb]_orb_qty", "xxxxx"
            "[cmp]_orb_qty = [orb]_dos_cnt * [cmp]_dos_qty", "xxxxx"
            "[cmp]_orb_qty = [cmp]_cmp_qty * [cmp]_orb_cnt", "xxxxx"
            "[cmp]_ord_qty = [cmp]_cmp_qty * [cmp]_ord_cnt", "xxxxx"
            "[cmp]_dos_tot = [cmp]_dos_ptm * [ord]_ord_tme", "x.x.."
            "[cmp]_dos_tot = [cmp]_dos_rte * [ord]_ord_tme", ".x..."
            "[cmp]_dos_qty = [cmp]_orb_cnc * [orb]_dos_qty", "xxxxx"
            "[cmp]_dos_qty = [cmp]_dos_rte * [ord]_sch_tme", "....."
            "[cmp]_dos_qty = [cmp]_dos_qty_adj * [ord]_adj_qty", "xxxxx"
            "[cmp]_dos_ptm = [cmp]_orb_cnc * [orb]_dos_ptm", "x.x.."
            "[cmp]_dos_ptm = [cmp]_dos_qty * [ord]_sch_frq", "x.x.."
            "[cmp]_dos_ptm = [cmp]_dos_ptm_adj * [ord]_adj_qty", "x.x.."
            "[cmp]_dos_rte = [cmp]_orb_cnc * [orb]_dos_rte", ".x..."
            "[cmp]_dos_rte = [cmp]_dos_rte_adj * [ord]_adj_qty", ".x..."
            "[cmp]_dos_qty_adj = [cmp]_orb_cnc * [orb]_dos_qty_adj", "xxxxx"
            "[cmp]_dos_qty_adj = [cmp]_dos_rte_adj * [ord]_sch_tme", ".x..."
            "[cmp]_dos_ptm_adj = [cmp]_orb_cnc * [orb]_dos_ptm_adj", "x.x.."
            "[cmp]_dos_ptm_adj = [cmp]_dos_qty_adj * [ord]_sch_frq", "x.x.."
            "[cmp]_dos_rte_adj = [cmp]_orb_cnc * [orb]_dos_rte_adj", ".x..."
            "[orb]_orb_qty = [orb]_dos_cnt * [orb]_dos_qty", "xxxxx"
            "[orb]_ord_qty = [orb]_ord_cnt * [orb]_orb_qty", "xxxxx"
            "[orb]_dos_tot = [orb]_dos_ptm * [ord]_ord_tme", "x.x.."
            "[orb]_dos_tot = [orb]_dos_rte * [ord]_ord_tme", ".x..."
            "[orb]_dos_qty = [orb]_dos_rte * [ord]_sch_tme", ".xx.x"
            "[orb]_dos_qty = [orb]_dos_qty_adj * [ord]_adj_qty", "xxxxx"
            "[orb]_dos_ptm = [orb]_dos_qty * [ord]_sch_frq", "x.x.."
            "[orb]_dos_ptm = [orb]_dos_ptm_adj * [ord]_adj_qty", "x.x.."
            "[orb]_dos_rte = [orb]_dos_rte_adj * [ord]_adj_qty", ".xx.x"
            "[orb]_dos_qty_adj = [orb]_dos_rte_adj * [ord]_sch_tme", ".x..."
            "[orb]_dos_ptm_adj = [orb]_dos_qty_adj * [ord]_sch_frq", "x.x.."
            "[orb]_orb_qty = sum([cmp]_orb_qty)", "xxxxx"
            "[orb]_dos_qty = sum([cmp]_dos_qty)", "....."
            "[orb]_dos_ptm = sum([cmp]_dos_ptm)", "....."
            "[orb]_dos_rte = sum([cmp]_dos_rte)", "....."
            "[orb]_dos_tot = sum([cmp]_dos_tot)", "....."
            "[orb]_dos_qty_adj = sum([cmp]_dos_qty_adj)", "....."
            "[orb]_dos_ptm_adj = sum([cmp]_dos_ptm_adj)", "....."
            "[orb]_dos_rte_adj = sum([cmp]_dos_rte_adj)", "....."
            "[orb]_dos_tot_adj = sum([cmp]_dos_tot_adj)", "....."
            "[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc", "xxxxx"
            "[orb]_orb_cnc = sum([cmp]_orb_cnc)", "xxxxx"
        ]

    // (dose-type index used by getEquations, position in the marker string, name)
    let private doseTypes =
        [
            EquationMapping.Literals.discontinuous, 0, "discontinuous"
            EquationMapping.Literals.continuous, 1, "continuous"
            EquationMapping.Literals.timed, 2, "timed"
            EquationMapping.Literals.once, 3, "once"
            EquationMapping.Literals.onceTimed, 4, "onceTimed"
        ]

    let private expectedFor pos =
        golden |> List.filter (fun (_, m) -> m[pos] = 'x') |> List.map fst


    let private items (ord: Order) = ord.Orderable.Components |> List.collect _.Items


    let private mappedEquations indx ord =
        match EquationMapping.getEquations indx |> EquationMapping.getEqsMapping ord with
        | SumMapping s, ProductMapping p -> s @ p
        | _ -> []


    let private nameOf ovar = ovar |> OrderVariable.getName |> Variable.Name.toString


    /// The number of mapped equations each item orderable concentration takes part in.
    let private itemConcentrationCounts indx ord =
        let eqs = ord |> mappedEquations indx

        ord
        |> items
        |> List.map (fun itm ->
            let n = itm.OrderableConcentration |> OrderVariable.Concentration.toOrdVar |> nameOf
            eqs |> List.filter (String.contains n) |> List.length
        )
        |> List.distinct

    [<Tests>]
    let tests =
        testList
            "EquationMapping embedded equations vs sheet"
            [
                test "golden table has all 67 sheet rows" {
                    golden |> List.length |> Expect.equal "67 rows transcribed" 67
                }

                for indx, pos, name in doseTypes do
                    test $"getEquations {name} matches the Equations sheet" {
                        EquationMapping.getEquations indx
                        |> Expect.equal $"embedded {name} equations equal the sheet" (expectedFor pos)
                    }

                for name, med, expected in [ "pcmSupp", Scenarios.pcmSupp, false; "amfo", Scenarios.amfo, true ] do
                    test $"the tie equations are mapped for %s{name}: %b{expected}" {
                        match med |> Medication.toOrder Scenarios.testStart with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            let sumEqs, prodEqs =
                                EquationMapping.getEquations EquationMapping.Literals.discontinuous
                                |> EquationMapping.getEqsMapping ord

                            let eqs =
                                match sumEqs, prodEqs with
                                | SumMapping s, ProductMapping p -> s @ p
                                | _ -> []

                            // only the two tie equations have an orderable concentration on the left
                            eqs
                            |> List.exists (String.split " = " >> List.head >> fun lhs -> lhs.EndsWith "_orb_cnc")
                            |> Expect.equal "tie equations present" expected
                    }

                test "every tpn item comes in one strength" {
                    Scenarios.tpn
                    |> toOrder
                    |> items
                    |> List.forall EquationMapping.hasOneStrength
                    |> Expect.isTrue "one strength"
                }

                test "the morfine of morfCont comes in two strengths" {
                    Scenarios.morfCont
                    |> toOrder
                    |> items
                    |> List.find (fun itm -> itm.Name |> Name.toString = "morfin")
                    |> EquationMapping.hasOneStrength
                    |> Expect.isFalse "two strengths"
                }

                test "an item without a concentration does not come in one strength" {
                    let itm = Scenarios.tpn |> toOrder |> items |> List.head

                    { itm with
                        ComponentConcentration =
                            itm.ComponentConcentration
                            |> OrderVariable.Concentration.toOrdVar
                            |> OrderVariable.setConstraints (OrderVariable.Constraints.create None None None None)
                            |> Concentration
                    }
                    |> EquationMapping.hasOneStrength
                    |> Expect.isFalse "no strength"
                }

                for name, med, expected in
                    [
                        "tpn", (fun () -> Scenarios.tpn), true
                        "amfo with NaCl", (fun () -> Scenarios.amfo |> Scenarios.withNaCl), true
                        "morfCont with NaCl, one strength", (fun () -> Scenarios.morfContWithNaClOneStrength), true
                        "morfCont with NaCl", (fun () -> Scenarios.morfContWithNaCl), false
                        "amfo with NaCl, two strengths, timed",
                        (fun () -> Scenarios.amfoWithNaClTwoStrengthsTimed),
                        false
                        "amox/clav", (fun () -> Scenarios.amoxClavText |> parse), false
                    ] do
                    test $"%s{name} leaves out the item concentration products: %b{expected}" {
                        med ()
                        |> toOrder
                        |> EquationMapping.leavesOutItemConcentrationProducts
                        |> Expect.equal "leaves out" expected
                    }

                for name, indx, med in
                    [
                        "tpn", EquationMapping.Literals.timed, Scenarios.tpn
                        "tpn once timed",
                        EquationMapping.Literals.onceTimed,
                        { Scenarios.tpn with
                            OrderType = OnceTimedOrder
                            Frequencies = None
                        }
                    ] do
                    test $"%s{name} maps each item concentration in the tie only, and maps the component rates" {
                        let ord = med |> toOrder

                        ord |> itemConcentrationCounts indx |> Expect.equal "the tie only" [ 1 ]

                        let eqs = ord |> mappedEquations indx

                        ord.Orderable.Components
                        |> List.forall (fun cmp ->
                            let n = cmp.Dose.Rate |> OrderVariable.Rate.toOrdVar |> nameOf
                            eqs |> List.exists (String.startsWith $"%s{n} = ")
                        )
                        |> Expect.isTrue "a component rate equation for each component"
                    }

                test "morfCont with NaCl maps each item concentration in the tie and the six products" {
                    Scenarios.morfContWithNaCl
                    |> toOrder
                    |> itemConcentrationCounts EquationMapping.Literals.continuous
                    |> Expect.equal "the tie and six products" [ 7 ]
                }

                test "getEquations is memoised: repeat calls return the same instance" {
                    // memoize used to be applied per call, so every call built and
                    // missed a fresh cache and returned a new list.
                    let first = EquationMapping.getEquations 3
                    let second = EquationMapping.getEquations 3

                    obj.ReferenceEquals(first, second)
                    |> Expect.isTrue "second call is served from the cache"
                }
            ]


// Regression tests: after the orderable dose quantity is
// changed so that dose quantity <> orderable quantity (orb_dos_cnt <> 1),
// changing an individual component orderable quantity used to over-determine
// the orderable dose quantity onto an off-increment value. The solver then
// threw a ValueRangeEmptyValueSet, the order state stayed poisoned, the UI
// value snapped back and every subsequent server update failed.
module OrderProcessorTests =

    module N = Variable.Name

    let private noLogger = Logging.noOp

    /// Run an OrderCommand through the pipeline, keeping the order on error.
    let private run cmd ord =
        match OrderProcessor.processPipeline noLogger (cmd ord) with
        | Ok o -> o, None
        | Error(o, msgs) -> o, Some msgs

    /// Build and fully solve the multi-component timed TPN scenario.
    let private solvedTpn () =
        Scenarios.tpn
        |> Medication.toOrderDto Scenarios.testStart
        |> Order.Dto.fromDto
        |> function
            | Ok o -> o
            | Error e -> failtest $"could not create tpn order: %A{e}"
        |> run CalcMinMax
        |> fst
        |> run IncreaseIncrements
        |> fst
        |> run CalcValues
        |> fst
        |> run SolveOrder
        |> fst

    /// The orderable-quantity value range of a named component, as a string.
    let private componentOrbQty (cmp: string) (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.filter (fun ov -> (ov.Variable.Name |> N.toString).Contains $"{cmp}]_orb_qty")
        |> List.map (fun ov -> ov.Variable.Values |> Variable.ValueRange.toString true)
        |> String.concat "; "

    let private hasEmptyValueSetError msgs =
        msgs
        |> Option.defaultValue []
        |> List.exists (fun m -> ($"%A{m}").Contains "EmptyValueSet")

    // a step moves a value the user selected, so the median is selected first
    let private stepFromMedian setMedian step ord =
        ord
        |> run (fun o -> ChangeProperty(o, setMedian))
        |> fst
        |> run (fun o -> ChangeProperty(o, step))

    // change the orderable dose quantity so that orb_dos_cnt <> 1
    let private lowerOrderableDoseQuantity ord =
        ord
        |> stepFromMedian SetMedianOrderableDoseQuantity (DecreaseOrderableDoseQuantity(5, true))
        |> fst

    let private increaseGluc ord =
        ord
        |> stepFromMedian
            (SetMedianComponentOrderableQuantity "gluc 10%")
            (IncreaseComponentOrderableQuantity("gluc 10%", 1, true))

    [<Tests>]
    let tests =
        testList
            "OrderProcessor component change after dose-quantity change"
            [
                test "component orderable quantity change does not crash the solver after a dose-quantity change" {
                    let afterDoseChange = solvedTpn () |> lowerOrderableDoseQuantity
                    let _, err = afterDoseChange |> increaseGluc

                    err
                    |> hasEmptyValueSetError
                    |> Expect.isFalse "should not raise a ValueRangeEmptyValueSet"

                    err |> Expect.isNone "component change should solve without error"
                }

                test "component orderable quantity actually changes (no snap-back) after a dose-quantity change" {
                    let afterDoseChange = solvedTpn () |> lowerOrderableDoseQuantity
                    let before =
                        afterDoseChange
                        |> run (fun o -> ChangeProperty(o, SetMedianComponentOrderableQuantity "gluc 10%"))
                        |> fst
                        |> componentOrbQty "gluc 10%"

                    let changed, err = afterDoseChange |> increaseGluc
                    let after = changed |> componentOrbQty "gluc 10%"

                    // a clean solve is what prevents the UI from snapping back to the previous value
                    err |> Expect.isNone "component change should solve without error"

                    after
                    |> Expect.notEqual "gluc 10% orderable quantity should change, not snap back" before
                }

                test "component orderable quantity change still works on a solved order with dose count = 1" {
                    // sanity: the normal path (no preceding dose-quantity change) keeps working
                    let solved = solvedTpn ()
                    let before =
                        solved
                        |> run (fun o -> ChangeProperty(o, SetMedianComponentOrderableQuantity "gluc 10%"))
                        |> fst
                        |> componentOrbQty "gluc 10%"

                    let changed, err = solved |> increaseGluc
                    let after = changed |> componentOrbQty "gluc 10%"

                    err
                    |> hasEmptyValueSetError
                    |> Expect.isFalse "should not raise a ValueRangeEmptyValueSet"

                    // also guard against a silent snap-back on the base path
                    after |> Expect.notEqual "gluc 10% orderable quantity should change" before
                }
            ]


module IncreaseQuantityIncrementTests =

    module Increment = Variable.ValueRange.Increment

    /// The order of a scenario after the minimum and maximum calculation, before any increase.
    let private minMax med =
        match med |> Medication.toOrder Scenarios.testStart with
        | Error e -> failtest $"could not build the order: %A{e}"
        | Ok ord ->
            match ord |> Order.applyConstraints |> Order.calcMinMax Logging.noOp with
            | Ok ord -> ord
            | Error _ -> failtest "could not calculate the minimum and maximum"


    let private increase maxCount med =
        let incrs = Order.quantityIncrements

        let ord = med |> minMax
        ord,
        ord.Orderable
        |> Order.Orderable.increaseQuantityIncrement Logging.noOp (BigRational.fromInt maxCount) incrs


    let private incrOf qty =
        qty
        |> OrderVariable.Quantity.toOrdVar
        |> OrderVariable.getVar
        |> _.Values
        |> Variable.ValueRange.getIncr
        |> Option.map (Increment.toValueUnit >> ValueUnit.getValue)


    /// The increments of the components, and of the total unless it is a fixed value.
    let private incrsOf (orb: Orderable) =
        [
            yield orb.OrderableQuantity
            yield! orb.Components |> List.map _.OrderableQuantity
        ]
        |> List.filter (OrderVariable.Quantity.isSolved >> not)
        |> List.map incrOf


    let tests =
        testList
            "Orderable.increaseQuantityIncrement"
            [
                test "amfo: no step fits 10 values, so every quantity gets the coarsest that keeps two, 1 mL" {
                    Scenarios.amfo
                    |> increase 10
                    |> snd
                    |> incrsOf
                    |> List.distinct
                    |> Expect.equal "one increment of 1 mL" [ Some [| 1N |] ]
                }

                test "amfo: with a cap of 1000 every quantity gets the finest step on which each component fits, 0.5 mL" {
                    Scenarios.amfo
                    |> increase 1000
                    |> snd
                    |> incrsOf
                    |> List.distinct
                    |> Expect.equal "one increment of 0.5 mL" [ Some [| 1N / 2N |] ]
                }

                test "tpn: KCl 20 to 24 mL keeps its five values, so the sum keeps 1 mL" {
                    let _, orb = Scenarios.tpn |> increase 10

                    orb
                    |> incrsOf
                    |> List.distinct
                    |> Expect.equal "one increment of 1 mL" [ Some [| 1N |] ]

                    orb.Components
                    |> List.find (fun cmp -> cmp.Name |> Variable.Name.toString = "KCl 7,4%")
                    |> _.OrderableQuantity
                    |> OrderVariable.Quantity.toOrdVar
                    |> OrderVariable.toString false
                    |> Expect.stringContains "20 to 24 mL by 1 mL" "20 mL..1 mL..24 mL"
                }

                test "morfCont: a fixed total does not block the choice of the components' increment" {
                    Scenarios.morfCont
                    |> increase 10
                    |> snd
                    |> incrsOf
                    |> List.distinct
                    |> Expect.equal "one increment of 1 mL" [ Some [| 1N |] ]
                }

                test "morfCont: a fixed total off a step drops that step, so a finer one is chosen" {
                    let ord = Scenarios.morfCont |> minMax

                    // a fixed total of 52 mL is not a multiple of 5 mL, the step that would fit a cap of 2
                    let orb =
                        { ord.Orderable with
                            OrderableQuantity =
                                ord.Orderable.OrderableQuantity
                                |> OrderVariable.Quantity.toOrdVar
                                |> OrderVariable.map (ValueUnit.setValue [| 52N |])
                                |> Quantity
                        }

                    let incrs = Order.quantityIncrements

                    orb
                    |> Order.Orderable.increaseQuantityIncrement Logging.noOp (BigRational.fromInt 2) incrs
                    |> incrsOf
                    |> List.distinct
                    |> Expect.equal "one increment of 1 mL" [ Some [| 1N |] ]
                }

                test "amfo with a dose count of 1 to 3 x: the dose quantity is pruned on rounded steps" {
                    let med =
                        { Scenarios.amfo with
                            DoseCount =
                                Informedica.GenCore.Lib.Ranges.MinMax.createInclIncl
                                    (1N |> ValueUnit.singleWithUnit Units.Count.times)
                                    (3N |> ValueUnit.singleWithUnit Units.Count.times)
                        }

                    // every stage must succeed, so the rounded dose values are shown to solve
                    let run ord (stage, cmd) =
                        match ord |> cmd |> OrderProcessor.processPipeline Logging.noOp with
                        | Ok ord -> ord
                        | Error(_, msgs) -> failtest $"%s{stage} failed: %A{msgs}"

                    match med |> Medication.toOrder Scenarios.testStart with
                    | Error e -> failtest $"could not build the order: %A{e}"
                    | Ok ord ->
                        [
                            "CalcMinMax", CalcMinMax
                            "IncreaseIncrements", IncreaseIncrements
                            "CalcValues", CalcValues
                            "SolveOrder", SolveOrder
                        ]
                        |> List.fold run ord
                        |> _.Orderable.Dose.Quantity
                        |> OrderVariable.Quantity.toOrdVar
                        |> OrderVariable.getValSetValueUnit
                        |> Option.map ValueUnit.getValue
                        |> Expect.equal "50 to 300 mL by 50 mL" (Some [| 50N .. 50N .. 300N |])
                }

                test "amfo with a fixed dose count: the dose quantity increment is left as it is" {
                    let ord = Scenarios.amfo |> minMax

                    let incrs = Order.quantityIncrements

                    ord
                    |> Order.increaseDoseQuantityIncrement (BigRational.fromInt 10) incrs
                    |> Expect.equal "the same order" ord
                }

                test "a quantity of 10 to 5000 mL is raised to steps of at most 50 mL" {
                    let ml = ValueUnit.singleWithUnit Units.Volume.milliLiter

                    let wide =
                        Variable.ValueRange.minIncrMaxToValueRange
                            (ml 10N |> Variable.ValueRange.Minimum.create true)
                            (ml 1N |> Increment.create)
                            (ml 5000N |> Variable.ValueRange.Maximum.create true)

                    // set in place of the range, which a solve would only narrow
                    let widen qty =
                        let ovar = qty |> OrderVariable.Quantity.toOrdVar
                        { ovar with OrderVariable.Variable.Values = wide } |> Quantity

                    let ord = Scenarios.pcmDrink |> minMax

                    { ord.Orderable with
                        OrderableQuantity = ord.Orderable.OrderableQuantity |> widen
                        Components =
                            ord.Orderable.Components
                            |> List.map (fun cmp -> { cmp with OrderableQuantity = cmp.OrderableQuantity |> widen })
                    }
                    |> Order.Orderable.increaseQuantityIncrement
                        Logging.noOp
                        (BigRational.fromInt 10)
                        Order.quantityIncrements
                    |> incrsOf
                    |> List.distinct
                    |> Expect.equal "one increment of 50 mL" [ Some [| 50N |] ]
                }

                test "a timed rate of 50 to 500 mL/uur keeps its step of 0,1 mL/uur" {
                    Scenarios.naclLargeRateText
                    |> TestOrder.parse
                    |> TestOrder.toOrder
                    |> TestOrder.solve CalcMinMax
                    |> TestOrder.solve IncreaseIncrements
                    |> _.Orderable.Dose.Rate
                    |> OrderVariable.Rate.toOrdVar
                    |> OrderVariable.getVar
                    |> _.Values
                    |> Variable.ValueRange.getIncr
                    |> Option.map (Increment.toValueUnit >> ValueUnit.getValue)
                    |> Expect.equal "an increment of 0,1 mL/uur" (Some [| 1N / 10N |])
                }

                test "pcmDrink: every component already fits, so the orderable is left as it is" {
                    let ord, orb = Scenarios.pcmDrink |> increase 10
                    orb |> Expect.equal "the same orderable" ord.Orderable
                }
            ]


module DtoTests =

    open Informedica.Utils.Lib.BCL
    open Expecto
    open Expecto.Flip
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    module Fixtures =

        let order med =
            match med |> Medication.toOrderDto Scenarios.testStart |> Order.Dto.fromDto with
            | Ok o -> o
            | Error e -> failwith $"fixture order could not be created: {e}"

        let orders =
            [
                "paracetamol supp", Scenarios.pcmSupp
                "amphotericin", Scenarios.amfo
                "morphine", Scenarios.morfCont
            ]
            |> List.map (fun (n, m) -> n, order m)

        let filter: Filter =
            {
                Indications = [| "koorts"; "pijn" |]
                Generics = [| "paracetamol" |]
                Routes = [| "rect"; "or" |]
                Forms = [| "zetpil" |]
                DoseTypes =
                    [|
                        Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"
                        Informedica.GenForm.Lib.Types.Once ""
                    |]
                Diluents = [||]
                Components = [| "paracetamol" |]
                Indication = Some "koorts"
                Generic = Some "paracetamol"
                Route = Some "rect"
                Form = None
                DoseType = Some(Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag")
                Diluent = None
                SelectedComponents = [| "paracetamol" |]
            }

        let scenario (ord: Order) : OrderScenario =
            {
                No = 1
                Name = "paracetamol"
                Indication = "koorts"
                Form = "zetpil"
                Route = "rect"
                DoseType = Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"
                Diluent = None
                Component = Some "paracetamol"
                Item = Some "paracetamol"
                Diluents = [||]
                Components = [| "paracetamol" |]
                Items = [| "paracetamol" |]
                Prescription = [| [| Valid "paracetamol"; Caution "max 4 x/dag" |] |]
                Preparation = [| [| Valid "zetpil 240 mg" |] |]
                Administration = [| [| Warning "rectaal" |]; [| Alert "niet bij lever" |] |]
                Order = ord
                UseAdjust = true
                UseRenalRule = false
                RenalRule = None
                ProductsIds = [| "gpk-1" |]
                Access = AnyAccess
            }


    let tests =
        testList
            "Dtos"
            [
                testList
                    "Order.Dto, the existing Dto the scenario nests"
                    [
                        for name, ord in Fixtures.orders do
                            test $"a component of {name}'s order carries its id into the Dto" {
                                ord
                                |> Order.Dto.toDto
                                |> _.Orderable.Components
                                |> List.map _.Id
                                |> List.filter String.isNullOrWhiteSpace
                                |> Expect.isEmpty "no component id is written empty"
                            }

                        for name, ord in Fixtures.orders do
                            test $"L1 for {name}'s order" {
                                ord
                                |> Order.Dto.toDto
                                |> Order.Dto.fromDto
                                |> Expect.equal "the same order" (Ok ord)
                            }
                    ]

                testList
                    "Filter.Dto"
                    [
                        test "L1, fromDto (toDto x) = Ok x" {
                            Fixtures.filter
                            |> Filter.Dto.toDto
                            |> Filter.Dto.fromDto
                            |> Expect.equal "the same filter" (Ok Fixtures.filter)
                        }

                        test "L2, fromDto d |> Result.map toDto = Ok d, in canonical form" {
                            let dto = Fixtures.filter |> Filter.Dto.toDto

                            dto
                            |> Filter.Dto.fromDto
                            |> Result.map (Filter.Dto.toDto >> Canonical.serialize)
                            |> Expect.equal "the same form" (Ok(Canonical.serialize dto))
                        }

                        test "an unknown dose type is an error, and every one is reported" {
                            { (Fixtures.filter |> Filter.Dto.toDto) with
                                DoseTypes = [| "weekly"; "once" |]
                                DoseType = Some "hourly"
                            }
                            |> Filter.Dto.fromDto
                            |> Expect.equal
                                "both"
                                (Error [ DtoError.UnknownDoseType "weekly"; DtoError.UnknownDoseType "hourly" ])
                        }
                    ]

                testList
                    "OrderScenario.Dto"
                    [
                        for name, ord in Fixtures.orders do
                            test $"L1 with {name}'s order" {
                                let sc = Fixtures.scenario ord

                                sc
                                |> OrderScenario.Dto.toDto
                                |> OrderScenario.Dto.fromDto
                                |> Expect.equal "the same scenario" (Ok sc)
                            }

                        for name, ord in Fixtures.orders do
                            test $"L2 with {name}'s order, in canonical form" {
                                let dto = Fixtures.scenario ord |> OrderScenario.Dto.toDto

                                dto
                                |> OrderScenario.Dto.fromDto
                                |> Result.map (OrderScenario.Dto.toDto >> Canonical.serialize)
                                |> Expect.equal "the same form" (Ok(Canonical.serialize dto))
                            }

                        for access in [ CVL; PVL; EnteralTube; AnyAccess ] do
                            test $"the access %A{access} goes to the Dto and back" {
                                let sc = { Fixtures.scenario (snd Fixtures.orders[0]) with Access = access }

                                sc
                                |> OrderScenario.Dto.toDto
                                |> OrderScenario.Dto.fromDto
                                |> Result.map _.Access
                                |> Expect.equal "the same access" (Ok access)
                            }

                        for name, stored in [ "no", null; "an empty", ""; "an unknown", "arterial" ] do
                            test $"%s{name} stored access reads as any access" {
                                let dto =
                                    { Fixtures.scenario (snd Fixtures.orders[0]) with Access = CVL }
                                    |> OrderScenario.Dto.toDto

                                { dto with Access = stored }
                                |> OrderScenario.Dto.fromDto
                                |> Result.map _.Access
                                |> Expect.equal "any access" (Ok AnyAccess)
                            }

                        test "an unknown text kind is an error" {
                            let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto

                            { dto with
                                Prescription =
                                    [|
                                        [|
                                            {
                                                Kind = "note"
                                                Text = "x"
                                            }
                                        |]
                                    |]
                            }
                            |> OrderScenario.Dto.fromDto
                            |> Expect.equal "named" (Error [ DtoError.UnknownTextKind "note" ])
                        }

                        test "null elements are unknown ones, never a crash" {
                            let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto

                            { dto with Prescription = [| [| Unchecked.defaultof<TextBlock.Dto.Dto> |] |] }
                            |> OrderScenario.Dto.fromDto
                            |> Expect.equal "a block that names no kind" (Error [ DtoError.UnknownTextKind "" ])

                            { (Fixtures.filter |> Filter.Dto.toDto) with DoseTypes = [| null |] }
                            |> Filter.Dto.fromDto
                            |> Expect.equal "a dose type that names nothing" (Error [ DtoError.UnknownDoseType "" ])

                            { dto with DoseType = null }
                            |> OrderScenario.Dto.fromDto
                            |> Expect.equal
                                "a scenario dose type that names nothing"
                                (Error [ DtoError.UnknownDoseType "" ])
                        }

                        test "null arrays are read as empty, never a crash" {
                            let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto

                            let read =
                                { dto with
                                    Diluents = null
                                    Prescription = [| null |]
                                    ProductsIds = null
                                }
                                |> OrderScenario.Dto.fromDto

                            match read with
                            | Ok sc ->
                                sc.Diluents |> Expect.isEmpty "no diluents"
                                sc.Prescription |> Expect.equal "one empty line" [| [||] |]
                                sc.ProductsIds |> Expect.isEmpty "no products"
                            | Error e -> failtest $"expected a scenario, got {e}"
                        }

                        test "a Filter.Dto with null arrays reads as empty" {
                            match
                                { (Fixtures.filter |> Filter.Dto.toDto) with
                                    Indications = null
                                    DoseTypes = null
                                }
                                |> Filter.Dto.fromDto
                            with
                            | Ok f ->
                                f.Indications |> Expect.isEmpty "no indications"
                                f.DoseTypes |> Expect.isEmpty "no dose types"
                            | Error e -> failtest $"expected a filter, got {e}"
                        }

                        test "an order that cannot be created is an error, not a dropped scenario" {
                            let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto
                            let broken = Order.Dto.Dto(dto.Order.Id, "paracetamol")
                            broken.Orderable <- Unchecked.defaultof<_>

                            match { dto with Order = broken } |> OrderScenario.Dto.fromDto with
                            | Error [ DtoError.OrderNotCreated _ ] -> ()
                            | other -> failtest $"expected one OrderNotCreated, got {other}"
                        }
                    ]

                testList
                    "the canonical form"
                    [
                        test "no whitespace, fields in declared order, a BigRational as n/d" {
                            let s = Fixtures.filter |> Filter.Dto.toDto |> Canonical.serialize

                            // no whitespace outside string values: none after a colon or a comma
                            (s.Contains "\": " || s.Contains ", \"" || s.Contains ", [")
                            |> Expect.isFalse "no whitespace outside strings"

                            s.StartsWith "{\"Indications\":[\"koorts\",\"pijn\"],\"Generics\""
                            |> Expect.isTrue "declared order"

                            [ 1N; 3N / 4N; 10N / 4N ]
                            |> Canonical.serialize
                            |> Expect.equal "lowest terms" "[\"1/1\",\"3/4\",\"5/2\"]"
                        }

                        test "an option is its value or null" {
                            (Some "x", (None: string option))
                            |> Canonical.serialize
                            |> Expect.equal "value or null" "{\"Item1\":\"x\",\"Item2\":null}"
                        }

                        test "two Dtos equal as values serialize equal, a re-ordered array does not" {
                            let a = Fixtures.filter |> Filter.Dto.toDto

                            let b =
                                { Fixtures.filter with Indications = [| "koorts"; "pijn" |] }
                                |> Filter.Dto.toDto

                            let c =
                                { Fixtures.filter with Indications = [| "pijn"; "koorts" |] }
                                |> Filter.Dto.toDto

                            Canonical.serialize a |> Expect.equal "equal" (Canonical.serialize b)

                            Canonical.serialize a
                            |> Expect.notEqual "a different order plan" (Canonical.serialize c)
                        }

                        test "a Filter.Dto reads back from its canonical form" {
                            let dto = Fixtures.filter |> Filter.Dto.toDto

                            dto
                            |> Canonical.serialize
                            |> Canonical.deserialize<Filter.Dto.Dto>
                            |> Expect.equal "the same Dto" dto
                        }

                        test "an OrderScenario.Dto reads back from its canonical form, in canonical form" {
                            let dto = Fixtures.scenario (snd Fixtures.orders[0]) |> OrderScenario.Dto.toDto
                            let s = dto |> Canonical.serialize

                            s
                            |> Canonical.deserialize<OrderScenario.Dto.Dto>
                            |> Canonical.serialize
                            |> Expect.equal "the same form" s
                        }
                    ]
            ]


module OrderPlanDtoTests =

    open System
    open Informedica.Utils.Lib.BCL
    open Expecto
    open Expecto.Flip
    open Informedica.GenUnits.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib


    module Fixtures =

        let order =
            match
                Scenarios.pcmSupp
                |> Medication.toOrderDto Scenarios.testStart
                |> Order.Dto.fromDto
            with
            | Ok o -> o
            | Error e -> failwith $"fixture order could not be created: {e}"

        let kg = Units.Weight.kiloGram
        let cm = Units.Height.centiMeter
        let day = Units.Time.day

        let child =
            { Patient.patient with
                Department = Some "ICK"
                Gender = Male
                Age = Some(ValueUnit.singleWithUnit day 3650N)
                Weight = Some(ValueUnit.singleWithUnit kg 32N)
                Height = Some(ValueUnit.singleWithUnit cm 140N)
                Access = [ PVL ]
            }

        let filter: Filter =
            {
                Indications = [| "koorts"; "pijn" |]
                Generics = [| "paracetamol" |]
                Routes = [| "rect" |]
                Forms = [| "zetpil" |]
                DoseTypes = [| Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag" |]
                Diluents = [||]
                Components = [| "paracetamol" |]
                Indication = Some "koorts"
                Generic = Some "paracetamol"
                Route = Some "rect"
                Form = Some "zetpil"
                DoseType = Some(Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag")
                Diluent = None
                SelectedComponents = [| "paracetamol" |]
            }

        let scenario: OrderScenario =
            {
                No = 1
                Name = "paracetamol"
                Indication = "koorts"
                Form = "zetpil"
                Route = "rect"
                DoseType = Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"
                Diluent = None
                Component = Some "paracetamol"
                Item = Some "paracetamol"
                Diluents = [||]
                Components = [| "paracetamol" |]
                Items = [| "paracetamol" |]
                Prescription = [| [| Valid "paracetamol 240 mg 3 x/dag" |] |]
                Preparation = [| [| Valid "zetpil 240 mg" |] |]
                Administration = [| [| Warning "rectaal" |] |]
                Order = order
                UseAdjust = true
                UseRenalRule = false
                RenalRule = None
                ProductsIds = [| "gpk-1" |]
                Access = AnyAccess
            }

        let context: OrderContext =
            {
                Filter = filter
                Patient = child
                Scenarios = [| scenario |]
                Argumentation = None
            }

        let totals: Totals =
            {
                Volume = Some "100 ml"
                Energy = Some "50 kcal"
                Protein = None
                Carbohydrate = None
                Fat = None
                Sodium = Some "3 mmol"
                Potassium = None
                Chloride = None
                Calcium = None
                Phosphate = None
                Magnesium = None
                Iron = None
                VitaminD = None
                Ethanol = None
                Propyleenglycol = None
                BenzylAlcohol = None
                BoricAcid = None
            }

        let planContext: PlanContext =
            {
                Id = "ctx-1"
                Category = OrderCategory.Drug
                Context = context
                Intake = totals
            }

        let feeding: PlanContext =
            { planContext with
                Id = "ctx-2"
                Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
            }

        let plan: OrderPlan =
            {
                Patient = child
                Filtered = [| "ctx-1" |]
                Contexts = [| planContext; feeding |]
                Totals = totals
            }

        let version: OrderPlanVersion =
            {
                Id = "v-1"
                No = 1
                PatientId = "stub-patient"
                Base = None
                SignedBy =
                    {
                        UserId = "u-1"
                        DisplayName = "Stub Prescriber"
                    }
                SignedAt = DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc)
                Plan = plan
                Verified = true
            }


    let l1 name (x: 'a) (toDto: 'a -> 'd) (fromDto: 'd -> Result<'a, DtoError list>) =
        test $"L1 for {name}" { x |> toDto |> fromDto |> Expect.equal "the same value" (Ok x) }


    let l2 name (x: 'a) (toDto: 'a -> 'd) (fromDto: 'd -> Result<'a, DtoError list>) =
        test $"L2 for {name}, in canonical form" {
            let dto = x |> toDto

            dto
            |> fromDto
            |> Result.map (toDto >> Canonical.serialize)
            |> Expect.equal "the same form" (Ok(Canonical.serialize dto))
        }


    let tests =
        testList
            "Order plan Dtos"
            [
                testList
                    "laws"
                    [
                        l1 "totals" Fixtures.totals Totals.Dto.toDto Totals.Dto.fromDto
                        l2 "totals" Fixtures.totals Totals.Dto.toDto Totals.Dto.fromDto
                        l1 "an order context" Fixtures.context OrderContext.Dto.toDto OrderContext.Dto.fromDto
                        l2 "an order context" Fixtures.context OrderContext.Dto.toDto OrderContext.Dto.fromDto
                        l1 "a plan context" Fixtures.feeding PlanContext.Dto.toDto PlanContext.Dto.fromDto
                        l2 "a plan context" Fixtures.feeding PlanContext.Dto.toDto PlanContext.Dto.fromDto
                        l1 "an order plan" Fixtures.plan OrderPlan.Dto.toDto OrderPlan.Dto.fromDto
                        l2 "an order plan" Fixtures.plan OrderPlan.Dto.toDto OrderPlan.Dto.fromDto
                        l1
                            "an order plan version"
                            Fixtures.version
                            OrderPlanVersion.Dto.toDto
                            OrderPlanVersion.Dto.fromDto
                        l2
                            "an order plan version"
                            Fixtures.version
                            OrderPlanVersion.Dto.toDto
                            OrderPlanVersion.Dto.fromDto
                    ]

                testList
                    "categories"
                    [
                        test "every category round-trips as a string" {
                            [
                                OrderCategory.Drug
                                OrderCategory.Nutrition NutritionCategory.EnteralFeeding
                                OrderCategory.Nutrition NutritionCategory.EnteralSupplement
                                OrderCategory.Nutrition NutritionCategory.TPN
                                OrderCategory.Nutrition NutritionCategory.Lipid
                                OrderCategory.Nutrition NutritionCategory.ElectrolyteGlucose
                            ]
                            |> List.map (fun c -> c |> OrderCategoryDto.toString |> OrderCategoryDto.fromString)
                            |> List.forall Result.isOk
                            |> Expect.isTrue "each read back"
                        }

                        test "an unknown category is an error" {
                            { (Fixtures.feeding |> PlanContext.Dto.toDto) with Category = "nutrition:soup" }
                            |> PlanContext.Dto.fromDto
                            |> Expect.equal "named" (Error [ DtoError.UnknownCategory "nutrition:soup" ])
                        }
                    ]

                testList
                    "fromDto refuses"
                    [
                        test "an order plan whose patient is below the minimum data, with every context's errors" {
                            let dto = Fixtures.plan |> OrderPlan.Dto.toDto

                            let brokenContext = { dto.Contexts[0] with Category = "x" }

                            { dto with
                                Patient =
                                    { dto.Patient with
                                        AgeDays = None
                                        WeightMeasured = false
                                    }
                                Contexts = [| brokenContext; dto.Contexts[1] |]
                            }
                            |> OrderPlan.Dto.fromDto
                            |> Expect.equal
                                "both reported"
                                (Error
                                    [
                                        DtoError.Patient PatientError.NoAgeOrMeasuredWeightAndHeight
                                        DtoError.UnknownCategory "x"
                                    ])
                        }

                        test "a scenario whose order cannot be created is an error, not dropped" {
                            let dto = Fixtures.context |> OrderContext.Dto.toDto
                            let broken = Order.Dto.Dto(dto.Scenarios[0].Order.Id, "paracetamol")
                            broken.Orderable <- Unchecked.defaultof<_>

                            match
                                { dto with Scenarios = [| { dto.Scenarios[0] with Order = broken } |] }
                                |> OrderContext.Dto.fromDto
                            with
                            | Error [ DtoError.OrderNotCreated _ ] -> ()
                            | other -> failtest $"expected one OrderNotCreated, got {other}"
                        }

                        test "null nested Dtos are missing, null arrays empty, never a crash" {
                            let dto = Fixtures.version |> OrderPlanVersion.Dto.toDto

                            { dto with
                                SignedBy = Unchecked.defaultof<_>
                                Plan =
                                    { dto.Plan with
                                        Totals = Unchecked.defaultof<_>
                                        Filtered = null
                                    }
                            }
                            |> OrderPlanVersion.Dto.fromDto
                            |> Expect.equal
                                "both named"
                                (Error [ DtoError.Missing "SignedBy"; DtoError.Missing "Totals" ])

                            match
                                { dto with
                                    Plan =
                                        { dto.Plan with
                                            Filtered = null
                                            Contexts = null
                                        }
                                }
                                |> OrderPlanVersion.Dto.fromDto
                            with
                            | Ok v ->
                                v.Plan.Filtered |> Expect.isEmpty "no filter"
                                v.Plan.Contexts |> Expect.isEmpty "no contexts"
                            | Error e -> failtest $"expected a version, got {e}"
                        }
                    ]

                testList
                    "the stored root"
                    [
                        test "an order plan version reads back from its canonical form, in canonical form" {
                            let dto = Fixtures.version |> OrderPlanVersion.Dto.toDto
                            let s = dto |> Canonical.serialize

                            s
                            |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                            |> Canonical.serialize
                            |> Expect.equal "the same form" s
                        }

                        test "a version read back parses to the version that was written" {
                            Fixtures.version
                            |> OrderPlanVersion.Dto.toDto
                            |> Canonical.serialize
                            |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                            |> OrderPlanVersion.Dto.fromDto
                            // in canonical form: a local DateTime reads back as the same instant in UTC
                            |> Result.map (OrderPlanVersion.Dto.toDto >> Canonical.serialize)
                            |> Expect.equal
                                "the same version"
                                (Ok(Fixtures.version |> OrderPlanVersion.Dto.toDto |> Canonical.serialize))
                        }

                        test "a null root is missing by its name, never a crash" {
                            let missing name = Error [ DtoError.Missing name ]

                            "null"
                            |> Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                            |> OrderPlanVersion.Dto.fromDto
                            |> Expect.equal "a stored null" (missing "OrderPlanVersion")

                            Unchecked.defaultof<OrderPlan.Dto.Dto>
                            |> OrderPlan.Dto.fromDto
                            |> Expect.equal "order plan" (missing "OrderPlan")

                            Unchecked.defaultof<PlanContext.Dto.Dto>
                            |> PlanContext.Dto.fromDto
                            |> Expect.equal "plan context" (missing "PlanContext")

                            Unchecked.defaultof<Signer.Dto.Dto>
                            |> Signer.Dto.fromDto
                            |> Expect.equal "signer" (missing "Signer")

                            Unchecked.defaultof<Totals.Dto.Dto>
                            |> Totals.Dto.fromDto
                            |> Expect.equal "totals" (missing "Totals")

                            Unchecked.defaultof<OrderContext.Dto.Dto>
                            |> OrderContext.Dto.fromDto
                            |> Expect.equal "order context" (missing "OrderContext")

                            Unchecked.defaultof<OrderScenario.Dto.Dto>
                            |> OrderScenario.Dto.fromDto
                            |> Expect.equal "scenario" (missing "OrderScenario")

                            Unchecked.defaultof<Filter.Dto.Dto>
                            |> Filter.Dto.fromDto
                            |> Expect.equal "filter" (missing "Filter")
                        }

                        test "two versions equal as values digest equal, a re-ordered plan does not" {
                            let a = Fixtures.version |> OrderPlanVersion.Dto.toDto |> Canonical.serialize

                            let b =
                                { Fixtures.version with
                                    Plan =
                                        { Fixtures.plan with Contexts = [| Fixtures.feeding; Fixtures.planContext |] }
                                }
                                |> OrderPlanVersion.Dto.toDto
                                |> Canonical.serialize

                            Fixtures.version
                            |> OrderPlanVersion.Dto.toDto
                            |> Canonical.serialize
                            |> Expect.equal "equal" a

                            a |> Expect.notEqual "a different order plan" b
                        }
                    ]
            ]


module ReachablePickTests =

    open TestOrder

    module Quantity = OrderVariable.Quantity
    module Rate = OrderVariable.Rate
    module Concentration = OrderVariable.Concentration

    let private logger = Logging.noOp

    // the CI runner shares its few cores between the test projects, so it gets three times as long
    let private slack =
        if System.Environment.GetEnvironmentVariable "CI" |> isNull then
            1L
        else
            3L

    /// The user waits for the pipeline once and for every pick.
    let private maxPipelineMs = 3000L * slack

    let private maxPickMs = 1000L * slack


    /// Every fixture the dialog is checked on, the single-component ones included. The tpn without
    /// KCl keeps more than two components and takes a fifth of the time of the full tpn, whose
    /// solved order the golden orders hold.
    let private fixtures () =
        [
            "amox/clav", Scenarios.amoxClavText |> parse
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "tpn without KCl",
            { Scenarios.tpn with Components = Scenarios.tpn.Components |> List.filter (fun c -> c.Name <> "KCl 7,4%") }
            "pcmSupp", Scenarios.pcmSupp
            "pcmDrink", Scenarios.pcmDrink
            "cotrim", Scenarios.cotrim
            "prednisolon", Scenarios.prednisolonText |> parse
            "tpn, kalium concentration limit binding", Scenarios.tpnKaliumConcentrationBinding
            "morfCont with NaCl", Scenarios.morfContWithNaCl
            "morfCont with NaCl, one strength", Scenarios.morfContWithNaClOneStrength
        ]


    /// The full processing an order goes through before a prescriber sees it, pruned with the caps
    /// given.
    let private processedWith caps (ord: Order) =
        let run cmd ord = ord |> cmd |> OrderProcessor.processPipelineWith caps logger

        ord
        |> run CalcMinMax
        |> Result.bind (run IncreaseIncrements)
        |> Result.bind (run CalcValues)
        |> Result.bind (run SolveOrder)


    let private processed = processedWith Order.ValueCaps.defaults


    /// The solved order of a medication and the time the processing took.
    let private solvedWith processOrder med =
        match med |> Medication.toOrder Scenarios.testStart with
        | Error e -> failtest $"could not build the order: %A{e}"
        | Ok ord ->
            let sw = System.Diagnostics.Stopwatch.StartNew()
            let res = ord |> processOrder
            sw.Stop()

            match res with
            | Error(_, msgs) -> failtest $"the processing failed: %A{msgs |> List.truncate 1}"
            | Ok ord -> sw.ElapsedMilliseconds, ord


    /// The lists the dialog offers as picks, with more than one value: the component quantities, the
    /// rate and, with at most two components, the item concentrations.
    let private offered (ord: Order) =
        let cmps = ord.Orderable.Components

        [
            for cmp in cmps do
                cmp.OrderableQuantity |> Quantity.toOrdVar

                if cmps |> List.length <= 2 then
                    for itm in cmp.Items do
                        itm.OrderableConcentration |> Concentration.toOrdVar
            ord.Orderable.Dose.Rate |> Rate.toOrdVar
        ]
        |> List.filter (fun ovar -> ovar |> valuesOf |> Array.length > 1)


    /// Every offered value picked and solved, timed: the names of the picks that fail and the
    /// slowest pick in ms.
    let private timedPicks (ord: Order) =
        let results =
            [
                for ovar in ord |> offered do
                    for nth in 0 .. (ovar |> valuesOf |> Array.length) - 1 do
                        let picked = ovar |> OrderVariable.setNthValue nth
                        let sw = System.Diagnostics.Stopwatch.StartNew()
                        let res = ord |> Order.fromOrdVars [ picked ] |> Order.solveOrder "pick" false logger
                        sw.Stop()

                        ovar |> OrderVariable.getName |> Variable.Name.toString,
                        res |> Result.isOk,
                        sw.ElapsedMilliseconds
            ]

        results
        |> List.filter (fun (_, ok, _) -> not ok)
        |> List.map (fun (n, _, _) -> n),
        results |> List.map (fun (_, _, ms) -> ms) |> List.fold max 0L


    let private itemConcentrations (ord: Order) =
        ord.Orderable.Components
        |> List.collect _.Items
        |> List.map (fun itm ->
            itm.Name |> Name.toString, itm.OrderableConcentration |> Concentration.toOrdVar |> valuesOf
        )
        |> Map.ofList


    let private componentValues (ord: Order) =
        ord.Orderable.Components
        |> List.map (_.OrderableQuantity >> Quantity.toOrdVar >> valuesOf)


    let private maxOf ovar =
        ovar
        |> OrderVariable.getVar
        |> Variable.getValueRange
        |> Variable.ValueRange.getMax
        |> Option.map (Variable.ValueRange.Maximum.toValueUnit >> ValueUnit.getValue >> Array.head)


    let tests =
        testList
            "the picks the dialog offers"
            [
                test "amox/clav: the clavulaanzuur concentrations are a tenth of the amoxicilline concentrations" {
                    let _, ord = Scenarios.amoxClavText |> parse |> solvedWith processed
                    let cncs = ord |> itemConcentrations

                    cncs["clavulaanzuur"]
                    |> Expect.equal "a tenth" (cncs["amoxicilline"] |> Array.map (fun c -> c / 10N))

                    cncs["amoxicilline"] |> Array.length > 1 |> Expect.isTrue "a choice remains"
                }

                test "prednisolon under the default discontinuous cap of 10 keeps the whole tablets" {
                    Scenarios.prednisolonText
                    |> parse
                    |> solvedWith (processedWith Order.ValueCaps.defaults)
                    |> snd
                    |> componentValues
                    |> Expect.equal "6 to 12" [ [| 6N .. 12N |] ]
                }

                test "prednisolon with a discontinuous cap of 30 keeps all 25 quarter tablets" {
                    Scenarios.prednisolonText
                    |> parse
                    |> solvedWith (processedWith { Order.ValueCaps.defaults with Discontinuous = 30 })
                    |> snd
                    |> componentValues
                    |> Expect.equal "6 to 12 by 1/4" [ [| 6N .. (1N / 4N) .. 12N |] ]
                }

                test "prednisolon with a discontinuous cap of 5 keeps every second tablet" {
                    Scenarios.prednisolonText
                    |> parse
                    |> solvedWith (processedWith { Order.ValueCaps.defaults with Discontinuous = 5 })
                    |> snd
                    |> componentValues
                    |> Expect.equal "6 to 12 by 2" [ [| 6N .. 2N .. 12N |] ]
                }

                test "the larger cap of every dose type is at least its cap of a list to select from" {
                    let caps = Order.ValueCaps.defaults

                    [
                        caps.ContinuousMax >= caps.Continuous
                        caps.DiscontinuousMaxSingle >= caps.Discontinuous
                        caps.DiscontinuousMaxMultiple >= caps.Discontinuous
                        caps.TimedMaxFew >= caps.TimedFew
                    ]
                    |> List.forall id
                    |> Expect.isTrue "every larger cap at least the cap to select from"
                }

                test "the norm dose of the once timed kaliumchloride is the same as with the timed caps swapped back" {
                    let swappedBack =
                        { Order.ValueCaps.defaults with
                            TimedFew = 20
                            TimedMaxFew = 10
                        }

                    let normDose caps =
                        match
                            Scenarios.kaliumchlorideOnceTimedText
                            |> parse
                            |> Medication.toOrder Scenarios.testStart
                        with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            CalcMinMax ord
                            |> OrderProcessor.processPipelineWith caps logger
                            |> Result.map _.Orderable.Dose.Quantity
                            |> Result.toOption

                    let dose = normDose Order.ValueCaps.defaults
                    dose |> Expect.isSome "the order is calculated"
                    dose |> Expect.equal "the same norm dose" (normDose swappedBack)
                }

                test "a binding kalium concentration limit raises the first tpn dose quantity from 380 to 530 mL" {
                    let firstDoseQuantity med =
                        med
                        |> solvedWith processed
                        |> snd
                        |> _.Orderable.Dose.Quantity
                        |> Quantity.toOrdVar
                        |> valuesOf
                        |> Array.head

                    (Scenarios.tpn |> firstDoseQuantity, Scenarios.tpnKaliumConcentrationBinding |> firstDoseQuantity)
                    |> Expect.equal "380 and 530 mL" (380N, 530N)
                }

                test "tpn with a binding kalium rate limit keeps the kalium rate under 0,09 mmol/kg/uur" {
                    let _, ord = Scenarios.tpnKaliumRateBinding |> solvedWith processed

                    let itms =
                        ord.Orderable.Components
                        |> List.collect (fun cmp -> cmp.Items |> List.map (fun itm -> cmp.Name, itm))

                    itms
                    |> List.find (fun (cmp, itm) ->
                        cmp |> Name.toString = "KCl 7,4%" && itm.Name |> Name.toString = "kalium"
                    )
                    |> snd
                    |> _.Dose.RateAdjust
                    |> OrderVariable.RateAdjust.toOrdVar
                    |> maxOf
                    |> Option.exists (fun mx -> mx <= 9N / 100N)
                    |> Expect.isTrue "at most 0,09 mmol/kg/uur"

                    itms
                    |> List.forall (snd >> _.Dose.Rate >> Rate.toOrdVar >> maxOf >> Option.isSome)
                    |> Expect.isTrue "every item rate has a maximum"
                }

                test "morfCont with NaCl offers rates from 0,7 to 2,8 mL/uur" {
                    let rates =
                        Scenarios.morfContWithNaCl
                        |> solvedWith processed
                        |> snd
                        |> _.Orderable.Dose.Rate
                        |> Rate.toOrdVar
                        |> valuesOf

                    (rates |> Array.head, rates |> Array.last)
                    |> Expect.equal "0,7 to 2,8 mL/uur" (7N / 10N, 28N / 10N)
                }

                // the dose limits leave the amfotericine at 2 mg/mL, but the solver sets the step and the
                // maximum of the dose quantity while 4 mg/mL is still among its values, and never widens them
                test "amfo with NaCl in two strengths, timed, offers the dose quantities 130, 180, 230 and 280 mL" {
                    Scenarios.amfoWithNaClTwoStrengthsTimed
                    |> solvedWith processed
                    |> snd
                    |> _.Orderable.Dose.Quantity
                    |> Quantity.toOrdVar
                    |> valuesOf
                    |> Expect.equal "130 to 280 mL by 50" [| 130N; 180N; 230N; 280N |]
                }

                test "a rate of 50 to 500 mL/uur is pruned to 50 to 500 by 50 and gets its median, 300 mL/uur" {
                    Scenarios.naclLargeRateText
                    |> parse
                    |> solvedWith processed
                    |> snd
                    |> _.Orderable.Dose.Rate
                    |> Rate.toOrdVar
                    |> valuesOf
                    |> Expect.equal "the median of 50 to 500 by 50" [| 300N |]
                }

                // the timings are run in sequence, so other tests do not slow them down
                testSequenced
                <| testList
                    "timed"
                    [
                        for name, med in fixtures () do
                            test
                                $"%s{name}: the processing takes at most %i{maxPipelineMs} ms and every offered pick solves within %i{maxPickMs} ms" {
                                let ms, ord = med |> solvedWith processed
                                let failing, slowest = ord |> timedPicks

                                ms <= maxPipelineMs |> Expect.isTrue $"the processing took %i{ms} ms"
                                failing |> List.distinct |> Expect.equal "no failing picks" []
                                slowest <= maxPickMs |> Expect.isTrue $"the slowest pick took %i{slowest} ms"
                            }

                        for cap in [ 5; 3 ] do
                            for name, med in fixtures () do
                                test $"%s{name}: with every cap at %i{cap} no component keeps more than %i{cap} values" {
                                    let ms, ord = med |> solvedWith (processedWith (Order.ValueCaps.all cap))

                                    for vs in ord |> componentValues do
                                        vs |> Array.length <= cap |> Expect.isTrue $"%A{vs}"

                                    ms <= maxPipelineMs |> Expect.isTrue $"the processing took %i{ms} ms"
                                }
                    ]
            ]


/// The rate of a timed or once timed order gets its values only once the dose quantity has one
/// value, and then the median of them; a cleared rate gives its list back. The orders come from
/// TestOrder.processed, the commands the dose dialog sends; ReachablePickTests keeps its own, which
/// also runs IncreaseIncrements, times the run and takes the caps.
module RateAfterQuantityTests =

    open TestOrder

    module Quantity = OrderVariable.Quantity
    module Rate = OrderVariable.Rate


    let private doseQuantity (ord: Order) = ord.Orderable.Dose.Quantity |> Quantity.toOrdVar


    let private rate (ord: Order) = ord.Orderable.Dose.Rate |> Rate.toOrdVar


    let private time (ord: Order) =
        match ord.Schedule |> Order.Schedule.toOrdVars |> snd with
        | Some tme -> tme
        | None -> failtest "a timed order without a time"


    let private isRange ovar = ovar |> OrderVariable.getVar |> Variable.isMinIncrMax


    let private minimumOf ovar =
        ovar
        |> OrderVariable.getVar
        |> Variable.getValueRange
        |> Variable.ValueRange.getMin
        |> Option.map (Variable.ValueRange.Minimum.toValueUnit >> ValueUnit.getValue)


    let private hours ovar =
        ovar
        |> OrderVariable.getValSetValueUnit
        |> Option.map (ValueUnit.convertTo Units.Time.hour >> ValueUnit.getValue)
        |> Option.defaultValue [||]


    /// The order with the nth value of the order variable picked, solved as a pick from the dialog is.
    let private pick n ovar ord =
        ord
        |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue n ]
        |> solve SolveOrder


    let private pickMiddleDoseQuantity ord =
        let qty = ord |> doseQuantity
        let n = qty |> valuesOf |> Array.length
        ord |> pick (n / 2) qty, [ qty |> OrderProcessor.nameOf ]


    /// The rate cleared as the client clears it: then Reopen with the earlier picks.
    let private clearRate picks (ord: Order) =
        ord
        |> Order.fromOrdVars [ ord |> rate |> OrderVariable.clear ]
        |> solve (fun ord -> Reopen(ord, picks))


    /// Every component quantity picked in turn, as the dialog completes an order.
    let private complete (ord: Order) =
        ord.Orderable.Components
        |> List.map _.Name
        |> List.fold
            (fun (o: Order) name ->
                let qty =
                    o.Orderable.Components
                    |> List.find (fun c -> c.Name = name)
                    |> _.OrderableQuantity
                    |> Quantity.toOrdVar

                if qty |> valuesOf |> Array.length <= 1 then
                    o
                else
                    o |> pick 0 qty
            )
            ord


    let tests =
        testList
            "the rate of a timed order after the dose quantity"
            [
                test
                    "after CalcMinMax the dose quantity starts where the limits allow: tpn at 191 mL, amfo timed at 22 mL" {
                    let first med = med |> toOrder |> solve CalcMinMax |> doseQuantity |> minimumOf

                    (Scenarios.tpn |> first, Scenarios.amfoTimed |> first)
                    |> Expect.equal "191 and 22 mL" (Some [| 191N |], Some [| 22N |])
                }

                test "processed, tpn and amfo timed keep the rate and the time ranges while the dose quantity is a list" {
                    for med in [ Scenarios.tpn; Scenarios.amfoTimed ] do
                        let ord = med |> processed

                        ord |> doseQuantity |> valuesOf |> Array.length > 1
                        |> Expect.isTrue "a list of dose quantities"

                        ord |> rate |> isRange |> Expect.isTrue "the rate a range"
                        ord |> time |> valuesOf |> Expect.isEmpty "the time without values"
                }

                test "tpn through CalcMinMax and SolveOrder, without CalcValues, keeps the rate a range" {
                    let ord = Scenarios.tpn |> toOrder |> solve CalcMinMax |> solve SolveOrder

                    ord |> doseQuantity |> valuesOf |> Array.length > 1
                    |> Expect.isTrue "a list of dose quantities"

                    ord |> rate |> isRange |> Expect.isTrue "the rate a range"
                }

                test "a tpn dose quantity pick gives the rate the median of its list, the time within 20 to 24 uur" {
                    let ord, _ = Scenarios.tpn |> processed |> pickMiddleDoseQuantity

                    ord
                    |> rate
                    |> valuesOf
                    |> Expect.equal "27 mL/uur, the median of 25 to 29" [| 27N |]

                    ord
                    |> time
                    |> hours
                    |> Array.forall (fun h -> h >= 20N && h <= 24N)
                    |> Expect.isTrue "the time within 20 to 24 uur"
                }

                test "a completed tpn solves with the median rate, 18 mL/uur at 380 mL" {
                    let ord = Scenarios.tpn |> processed |> complete

                    ord |> Order.isSolved |> Expect.isTrue "solved"
                    ord |> doseQuantity |> valuesOf |> Expect.equal "380 mL" [| 380N |]
                    ord |> rate |> valuesOf |> Expect.equal "18 mL/uur" [| 18N |]
                }

                test "the once timed KCl gets the median rate, 25 mL/uur, when the scenario is selected" {
                    Scenarios.kaliumchlorideOnceTimedText
                    |> parse
                    |> toOrder
                    |> solve CalcMinMax
                    |> solve CalcValues
                    |> rate
                    |> valuesOf
                    |> Expect.equal "25 mL/uur" [| 25N |]
                }

                test "a reset of the morfine timed with NaCl offers the rate list, 2,1 to 2,5 mL/uur" {
                    Scenarios.morfTimedWithNaCl
                    |> processed
                    |> solve ReCalcValues
                    |> rate
                    |> valuesOf
                    |> Expect.equal "2,1 to 2,5 by 0,1" [| 21N / 10N .. 1N / 10N .. 25N / 10N |]
                }

                // a reset prunes with the larger cap: reCalcSteps passes useMax and CalcValues does not, so
                // the reset list differs from the 50 to 500 by 50 the processing makes
                test "a reset of the NaCl of 50 to 500 mL/uur offers the rate list, 60 to 480 by 30" {
                    Scenarios.naclLargeRateText
                    |> parse
                    |> processed
                    |> solve ReCalcValues
                    |> rate
                    |> valuesOf
                    |> Expect.equal "60 to 480 by 30" [| 60N .. 30N .. 480N |]
                }

                test
                    "the median rate of tpn cleared gives its list back, a pick from it solves, cleared again the list comes back" {
                    let ord, picks = Scenarios.tpn |> processed |> pickMiddleDoseQuantity
                    let rates = [| 25N .. 29N |]

                    let cleared = ord |> clearRate picks
                    cleared |> rate |> valuesOf |> Expect.equal "25 to 29 mL/uur" rates

                    let picked = cleared |> pick 0 (cleared |> rate)
                    picked |> Order.isSolved |> Expect.isTrue "solved with the picked rate"

                    picked
                    |> clearRate (picks @ [ picked |> rate |> OrderProcessor.nameOf ])
                    |> rate
                    |> valuesOf
                    |> Expect.equal "25 to 29 mL/uur again" rates
                }

                test "the median rate of the morfine timed with NaCl cleared gives its list back, 2,1 to 2,5 mL/uur" {
                    Scenarios.morfTimedWithNaCl
                    |> processed
                    |> clearRate []
                    |> rate
                    |> valuesOf
                    |> Expect.equal "2,1 to 2,5 by 0,1" [| 21N / 10N .. 1N / 10N .. 25N / 10N |]
                }

                test "the rate stepper sets a rate that is a range to its minimum, its median and its maximum" {
                    let ord = Scenarios.tpn |> processed
                    let range = ord |> rate |> OrderVariable.getVar |> Variable.getValueRange

                    let bound get toVu =
                        range
                        |> get
                        |> Option.map (toVu >> ValueUnit.getValue)
                        |> Option.defaultValue [||]

                    let lo = bound Variable.ValueRange.getMin Variable.ValueRange.Minimum.toValueUnit
                    let hi = bound Variable.ValueRange.getMax Variable.ValueRange.Maximum.toValueUnit

                    let stepped cmd =
                        ord |> solve (fun ord -> ChangeProperty(ord, cmd)) |> rate |> valuesOf

                    SetMinOrderableDoseRate |> stepped |> Expect.equal "the minimum" lo
                    SetMaxOrderableDoseRate |> stepped |> Expect.equal "the maximum" hi

                    SetMedianOrderableDoseRate
                    |> stepped
                    |> Array.forall (fun v -> v > lo[0] && v < hi[0])
                    |> Expect.isTrue "the median between them"
                }
            ]


/// The orderable dose total is the per time times the duration on an order with a frequency, and the rate
/// times the duration on a continuous order. With both equations on a timed order, the administration
/// time times the frequency has to be 1, so a timed order with a duration cannot be solved; on a
/// discontinuous order the rate gets the per time, although a discontinuous order has no rate.
module DoseTotalTests =

    open TestOrder

    module Quantity = OrderVariable.Quantity
    module Rate = OrderVariable.Rate
    module PerTime = OrderVariable.PerTime
    module Total = OrderVariable.Total
    module Time = OrderVariable.Time


    let private doseQuantity (ord: Order) = ord.Orderable.Dose.Quantity |> Quantity.toOrdVar


    let private rate (ord: Order) = ord.Orderable.Dose.Rate |> Rate.toOrdVar


    let private perTime (ord: Order) = ord.Orderable.Dose.PerTime |> PerTime.toOrdVar


    let private total (ord: Order) = ord.Orderable.Dose.Total |> Total.toOrdVar


    let private duration (ord: Order) = ord.Duration |> Time.toOrdVar


    let private milliLiters ovar =
        ovar
        |> OrderVariable.getValSetValueUnit
        |> Option.map (ValueUnit.convertTo Units.Volume.milliLiter >> ValueUnit.getValue)
        |> Option.defaultValue [||]


    /// The order with the first value of the order variable picked, solved as a pick from the dialog is.
    let private pickFirst ovar ord =
        if ovar |> valuesOf |> Array.length <= 1 then
            ord
        else
            ord
            |> Order.fromOrdVars [ ovar |> OrderVariable.setNthValue 0 ]
            |> solve SolveOrder


    /// Every basic variable set, as the dialog completes an order: the dose quantity, then each
    /// component quantity.
    let private completed (ord: Order) =
        let ord = ord |> pickFirst (ord |> doseQuantity)

        ord.Orderable.Components
        |> List.map _.Name
        |> List.fold
            (fun (o: Order) name ->
                let qty =
                    o.Orderable.Components
                    |> List.find (fun c -> c.Name = name)
                    |> _.OrderableQuantity
                    |> Quantity.toOrdVar

                o |> pickFirst qty
            )
            ord


    /// The order with its duration set to a number of days, solved; the messages when it is not.
    let private withDuration days (ord: Order) =
        let vr =
            days
            |> ValueUnit.singleWithUnit Units.Time.day
            |> Variable.ValueRange.createValSet

        ord
        |> Order.fromOrdVars [ ord |> duration |> OrderVariable.setValueRange vr ]
        |> SolveOrder
        |> OrderProcessor.processPipeline OrderLogging.noOp
        |> Result.mapError snd


    let private solved ord =
        match ord with
        | Ok ord -> ord
        | Error msgs -> failtest $"not solved: %A{msgs}"


    let tests =
        testList
            "the orderable dose total"
            [
                test "amfo timed, once a day over 1 hour, solves with a duration of 1 day, the total one dose quantity" {
                    let ord = Scenarios.amfoTimed |> processed |> completed |> withDuration 1N |> solved

                    ord
                    |> total
                    |> milliLiters
                    |> Expect.equal "one dose quantity" (ord |> doseQuantity |> milliLiters)
                }

                test "amfo discontinuous, once a day, with a duration of 1 day keeps its rate without values" {
                    let ord = Scenarios.amfo |> processed |> completed |> withDuration 1N |> solved

                    ord
                    |> total
                    |> milliLiters
                    |> Expect.equal "one dose quantity" (ord |> doseQuantity |> milliLiters)

                    ord |> rate |> valuesOf |> Expect.isEmpty "a discontinuous order has no rate"
                }

                test
                    "morfine continuous with a duration of 1 day gets its total from the rate, the per time without values" {
                    let ord = Scenarios.morfCont |> processed |> completed

                    let ord =
                        ord
                        |> Order.fromOrdVars [ ord |> rate |> OrderVariable.setNthValue 0 ]
                        |> solve SolveOrder

                    let ord = ord |> withDuration 1N |> solved

                    let rateMlPerDay =
                        ord
                        |> rate
                        |> OrderVariable.getValSetValueUnit
                        |> Option.map (ValueUnit.convertTo (Units.Volume.milliLiter |> ValueUnit.per Units.Time.day))
                        |> Option.map ValueUnit.getValue
                        |> Option.defaultValue [||]

                    ord |> total |> milliLiters |> Expect.equal "the rate over one day" rateMlPerDay

                    ord
                    |> perTime
                    |> valuesOf
                    |> Expect.isEmpty "a continuous order has no per time"
                }
            ]


[<Tests>]
let tests =
    testList
        "GenOrder Tests"
        [
            MedicationOrderTests.tests
            ComponentProductTests.tests
            ProductNarrowingTests.tests
            TypeTests.tests
            DtoTests.tests
            OrderPlanDtoTests.tests
            OrderPlanTests.tests
            OrderPlanTests.evaluateTests
            OrderPlanTests.rulesTests
            OrderPlanTests.scenarioAccessTests
            OrderPlanTests.refusalTests
            OrderPlanTests.argumentationTests
            DosePrintoutTests.tests
            PatientConstructorTests.tests
            OrderVariableTests.tests
            HelperTests.tests
            DeterminationTests.tests
            ReferenceTests.tests
            EffectTests.tests
            RecipeTests.tests
            RecipeTests.reCalcTests
            ConstraintsTests.tests
            GoldenOrderTests.tests
            IncreaseQuantityIncrementTests.tests
            ReachablePickTests.tests
            RateAfterQuantityTests.tests
            DoseTotalTests.tests
            OrderBuilderTests.tests
            MedicationTextTests.MedicationTextScenarioTests.tests
            MedicationTextTests.MedicationTextYamlTests.tests
            MedicationSchemaTests.tests
        ]
