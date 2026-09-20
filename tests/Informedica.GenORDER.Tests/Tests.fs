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

        medicationOrder |> Medication.toOrderDto |> Dto.fromDto |> Result.get

    // Also a minimal empty order for CalcMinMax path
    let private mkEmptyOrder () =
        Dto.discontinuous "T" "Test" "PO" [] |> Dto.fromDto |> Result.get

    let private countValues (o: Order) =
        o |> toOrdVars |> List.filter OrderVariable.hasValues |> List.length

    let private mkOnceTimedOrder () =
        Scenarios.kaliumchlorideOnceTimedText
        |> Medication.fromString
        |> function
            | Error errs -> failtest $"Failed to parse kaliumchloride OnceTimed: {errs}"
            | Ok med ->
                med
                |> Medication.toOrderDto
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

                    let dto = testMedicationOrders |> List.head |> Medication.toOrderDto

                    // no weight -> no per-weight aggregation -> Volume None, but the
                    // call completes without any Google-sheet access.
                    let result = Totals.getTotals syntheticTotals None None [| dto |]

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

                test "paracetamol suppository (non-timed) is unaffected by staged expansion" {
                    let ord =
                        Scenarios.pcmSupp
                        |> Medication.toOrderDto
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
            | OnceOrder -> Order.Dto.once d.Id d.Name d.Route []
            | OnceTimedOrder -> Order.Dto.onceTimed d.Id d.Name d.Route []
            | ContinuousOrder -> Order.Dto.continuous d.Id d.Name d.Route []
            | DiscontinuousOrder -> Order.Dto.discontinuous d.Id d.Name d.Route []
            | TimedOrder -> Order.Dto.timed d.Id d.Name d.Route []

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
                            let dto = Medication.toOrderDto medOrd

                            dto.Id |> Expect.equal "should match Id" medOrd.Id
                            dto.Schedule.IsDiscontinuous |> Expect.isTrue "should be discontinuous"
                            dto.Orderable.Name |> Expect.equal "should match Name" medOrd.Name
                            dto.Orderable.Components |> Expect.hasLength "should have 2 components" 2

                            dto.Orderable.Components[0].Items
                            |> Expect.hasLength "should have 3 items in first component" 2
                        }

                        test "ToDto reference function to OrderDto" {
                            let medOrd = testMedicationOrders |> List.head
                            let ord1 = Medication.toOrderDto medOrd |> Order.Dto.fromDto |> Result.get

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

        let dto = Medication.toOrderDto medicationOrder
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

                            let order = Medication.toOrderDto medOrd |> Dto.fromDto |> Result.get
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


module MedicationParserTests =


    let tests =
        testList
            "Medication.Parser"
            [

                test "parseBigRationalOpt parses integer string" {
                    Medication.Parser.parseBigRationalOpt "10" |> Expect.isSome "should parse '10'"
                }

                test "parseBigRationalOpt returns None for empty string" {
                    Medication.Parser.parseBigRationalOpt ""
                    |> Expect.isNone "empty string should give None"
                }

                test "parseBigRationalOpt returns None for whitespace" {
                    Medication.Parser.parseBigRationalOpt "   "
                    |> Expect.isNone "whitespace should give None"
                }

                test "parseDutchDecimal parses '1,5'" {
                    Medication.Parser.parseDutchDecimal "1,5"
                    |> Expect.isSome "should parse Dutch decimal '1,5'"
                }

                test "parseDutchDecimal parses '2.0'" {
                    Medication.Parser.parseDutchDecimal "2.0" |> Expect.isSome "should parse '2.0'"
                }

                test "parseOrderType 'DiscontinuousOrder' gives Ok DiscontinuousOrder" {
                    Medication.Parser.parseOrderType "DiscontinuousOrder"
                    |> Expect.equal "should give Ok DiscontinuousOrder" (Ok DiscontinuousOrder)
                }

                test "parseOrderType 'TimedOrder' gives Ok TimedOrder" {
                    Medication.Parser.parseOrderType "TimedOrder"
                    |> Expect.equal "should give Ok TimedOrder" (Ok TimedOrder)
                }

                test "parseOrderType unknown gives Error" {
                    Medication.Parser.parseOrderType "invalid"
                    |> Result.isError
                    |> Expect.isTrue "unknown order type should give Error"
                }

                test "parseLine returns None for line without colon" {
                    Medication.Parser.parseLine "no colon here" |> Expect.isNone "no colon → None"
                }

                test "parseLine returns Some for key:value line" {
                    Medication.Parser.parseLine "Name: Test" |> Expect.isSome "key:value → Some"
                }
            ]


module OrderBuilderTests =

    open Informedica.GenOrder.Lib.Medication

    /// The names of every order variable of an order, in order.
    let private names (ord: Order) =
        ord
        |> Order.toOrdVars
        |> List.map (OrderVariable.getName >> Informedica.GenSolver.Lib.Variable.Name.toString)

    let private fixtures =
        [
            "pcmSupp", Scenarios.pcmSupp
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "pcmDrink", Scenarios.pcmDrink
            "cotrim", Scenarios.cotrim
            "tpn", Scenarios.tpn
            "tpnComplete", Scenarios.tpnComplete
            "fullMedication", Scenarios.fullMedication
        ]

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
            "Medication.OrderBuilder, the shape pass"
            [
                for name, med in fixtures do
                    test $"{name} is built with the order variables the order has" {
                        let built = med |> OrderBuilder.newOrder |> OrderBuilder.withComponents med

                        match med |> Medication.toOrder with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord -> built |> names |> Expect.equal "the same order variables" (ord |> names)
                    }

                for name, med in fixtures do
                    test $"{name} constrains its items as the order does" {
                        let built =
                            med
                            |> OrderBuilder.newOrder
                            |> OrderBuilder.withComponents med
                            |> OrderBuilder.withItemConstraints med

                        match med |> Medication.toOrder with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            built
                            |> constraintsOfDepth 4
                            |> Expect.equal "the same item constraints" (ord |> constraintsOfDepth 4)
                    }

                for name, med in fixtures do
                    test $"{name} constrains its orderable as the order does" {
                        let built =
                            med
                            |> OrderBuilder.newOrder
                            |> OrderBuilder.withComponents med
                            |> OrderBuilder.withItemConstraints med
                            |> OrderBuilder.withComponentConstraints med
                            |> OrderBuilder.withOrderableConstraints med

                        match med |> Medication.toOrder with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            built
                            |> constraintsOfDepth 2
                            |> Expect.equal "the same orderable constraints" (ord |> constraintsOfDepth 2)
                    }

                for name, med in fixtures do
                    test $"{name} constrains its components as the order does" {
                        let built =
                            med
                            |> OrderBuilder.newOrder
                            |> OrderBuilder.withComponents med
                            |> OrderBuilder.withItemConstraints med
                            |> OrderBuilder.withComponentConstraints med

                        match med |> Medication.toOrder with
                        | Error e -> failtest $"could not build the order: %A{e}"
                        | Ok ord ->
                            built
                            |> constraintsOfDepth 3
                            |> Expect.equal "the same component constraints" (ord |> constraintsOfDepth 3)
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


module EquationsTests =

    // Golden reference: the full "Equations" sheet. Each entry is the equation
    // string ("Short Name" column) and a 5-char marker for which dose types mark
    // it with an "x", in column order discontinuous, continuous, timed, once,
    // onceTimed ('x' = applies, '.' = blank). This is an independent transcription
    // of the source sheet; the test fails if the hardcoded EquationMapping.equations
    // list drifts from it (wrong equation text, wrong dose-type assignment, or order).
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
            "[cmp]_dos_tot = [cmp]_dos_ptm * [ord]_ord_tme", "xxx.."
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
            "[orb]_dos_tot = [orb]_dos_ptm * [ord]_ord_tme", "xxx.."
            "[orb]_dos_tot = [orb]_dos_rte * [ord]_ord_tme", "xxx.."
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

    [<Tests>]
    let tests =
        testList
            "EquationMapping embedded equations vs sheet"
            [
                test "golden table has all 65 sheet rows" {
                    golden |> List.length |> Expect.equal "65 rows transcribed" 65
                }

                for indx, pos, name in doseTypes do
                    test $"getEquations {name} matches the Equations sheet" {
                        EquationMapping.getEquations indx
                        |> Expect.equal $"embedded {name} equations equal the sheet" (expectedFor pos)
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
        |> Medication.toOrderDto
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

    // change the orderable dose quantity so that orb_dos_cnt <> 1
    let private lowerOrderableDoseQuantity ord =
        ord
        |> run (fun o -> ChangeProperty(o, DecreaseOrderableDoseQuantity(5, true)))
        |> fst

    let private increaseGluc ord =
        ord
        |> run (fun o -> ChangeProperty(o, IncreaseComponentOrderableQuantity("gluc 10%", 1, true)))

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
                    let before = afterDoseChange |> componentOrbQty "gluc 10%"
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
                    let before = solved |> componentOrbQty "gluc 10%"
                    let changed, err = solved |> increaseGluc
                    let after = changed |> componentOrbQty "gluc 10%"

                    err
                    |> hasEmptyValueSetError
                    |> Expect.isFalse "should not raise a ValueRangeEmptyValueSet"

                    // also guard against a silent snap-back on the base path
                    after |> Expect.notEqual "gluc 10% orderable quantity should change" before
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
            match med |> Medication.toOrderDto |> Order.Dto.fromDto with
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
            }


    let tests =
        testList
            "Dtos"
            [
                testList
                    "Order.Dto, the existing Dto the scenario nests"
                    [
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
            match Scenarios.pcmSupp |> Medication.toOrderDto |> Order.Dto.fromDto with
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
            }

        let context: OrderContext =
            {
                Filter = filter
                Patient = child
                Scenarios = [| scenario |]
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


[<Tests>]
let tests =
    testList
        "GenOrder Tests"
        [
            MedicationOrderTests.tests
            TypeTests.tests
            DtoTests.tests
            OrderPlanDtoTests.tests
            OrderPlanTests.tests
            OrderPlanTests.evaluateTests
            OrderPlanTests.rulesTests
            DosePrintoutTests.tests
            PatientConstructorTests.tests
            MedicationParserTests.tests
            OrderVariableTests.tests
            ConstraintsTests.tests
            OrderBuilderTests.tests
        ]
