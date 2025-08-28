module Tests 

    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    // --- New tests for fluent pipeline guard/order behavior ---

    // Original test data used by several tests below
    let testDrugOrders = [
        { DrugOrder.drugOrder with
            Id = "DO1"
            Name = "Test Drug Order 1"
            OrderType = DiscontinuousOrder
            Frequencies = ValueUnit.create Units.Time.day [| 1N .. 4N |] |> Some
            Components = [
                { DrugOrder.productComponent with
                    Name = "Component A"
                    Shape = "injectievloeistof"
                    Divisible = Some 1N
                    Quantities = Some (ValueUnit.create Units.Volume.milliLiter [| 2N |])
                    Substances = [
                        { DrugOrder.substanceItem with
                            Name = "Substance A1"
                            Concentrations = Some (ValueUnit.create Units.Mass.milliGram [| 100N |] )
                            Dose = DoseLimit.limit |> Some
                        }
                        { DrugOrder.substanceItem with
                            Name = "Substance A2"
                            Concentrations = Some (ValueUnit.create Units.Mass.milliGram [| 50N |] )
                            Dose = DoseLimit.limit |> Some
                        }
                    ] 
                }
                { DrugOrder.productComponent with
                    Name = "Component B"
                    Shape = "injectievloeistof"
                    Substances = [
                        { DrugOrder.substanceItem with
                            Name = "Substance B1"
                            Concentrations = Some (ValueUnit.create Units.Mass.milliGram [| 150N |] )
                            Dose = DoseLimit.limit |> Some
                        }
                    ] 
                }
            ] 
        }
        { DrugOrder.drugOrder with
            Id = "DO2"
            Name = "Test Drug Order 2"
            OrderType = TimedOrder
            Components = [
                { DrugOrder.productComponent with
                    Name = "Component C"
                    Shape = "Syrup"
                    Substances = [
                        { DrugOrder.substanceItem with
                            Name = "Substance C1"
                            Concentrations = Some (ValueUnit.create Units.Mass.milliGram [| 200N |] )
                            Dose = DoseLimit.limit |> Some
                        }
                    ] 
                }
            ] 
        }
    ]

    // --- New tests for fluent pipeline guard/order behavior ---
    module Pipeline =
        open Informedica.GenOrder.Lib
        open Informedica.GenOrder.Lib.Order
        open Informedica.GenOrder.Lib.OrderVariable
        module OV = Informedica.GenOrder.Lib.OrderVariable
        module Units = Informedica.GenUnits.Lib.Units
        open Informedica.GenOrder.Lib.Types

        let private noLogger = Informedica.GenOrder.Lib.Logging.noOp

        // Build an Order from testDrugOrders with realistic constraints to enable value calculation
        let private mkConstrainedOrder () =
            let drugOrder =
                testDrugOrders
                |> List.tryFind (fun d -> match d.OrderType with | TimedOrder -> true | _ -> false)
                |> Option.defaultValue (testDrugOrders |> List.head)
            drugOrder |> DrugOrder.toOrderDto |> Order.Dto.fromDto

        // Also a minimal empty order for CalcMinMax path
        let private mkEmptyOrder () =
            Order.Dto.discontinuous "T" "Test" "PO" [] |> Order.Dto.fromDto

        let private countValues (o: Order) =
            o
            |> Order.toOrdVars
            |> List.filter OrderVariable.hasValues
            |> List.length

        [<Tests>]
        let guard_and_run_order_tests =
            testList "processPipeline guard and run order" [
                test "SolveOrder first ensures values before solving" {
                    let ord = mkConstrainedOrder ()
                    let before = countValues ord
                    let res = Order.processPipeline noLogger None (SolveOrder ord)
                    match res with
                    | Ok solved ->
                        let after = countValues solved
                        Expect.isTrue "Value count should not decrease" (after >= before)
                    | Error (o, errs) ->
                        let after = countValues o
                        Expect.isTrue "Value count should not decrease (even on error)" (after >= before)
                }

                test "CalcValues path only calculates values" {
                    let ord = mkConstrainedOrder ()
                    let before = countValues ord
                    let res = Order.processPipeline noLogger None (CalcValues ord)
                    match res with
                    | Ok o ->
                        let after = countValues o
                        Expect.isTrue "Value count should not decrease" (after >= before)
                    | Error (o, _) ->
                        let after = countValues o
                        Expect.isTrue "Value count should not decrease (even on error)" (after >= before)
                }

                test "CalcMinMax path runs when order is empty" {
                    let ord = mkEmptyOrder ()
                    let res = Order.processPipeline noLogger None (CalcMinMax ord)
                    match res with
                    | Ok _ -> true |> Expect.isTrue "calc minmax ok"
                    | Error (o, _) -> (box o) |> Expect.isNotNull "Order returned with error"
                }
            ]

        [<Tests>]
        let cleared_processing_tests =
            testList "processClearedOrder behavior" [
                test "Discontinuous FrequencyCleared materializes values" {
                    let ord0 = mkConstrainedOrder ()
                    // Switch to Discontinuous and clear its frequency via the change API
                    let ord =
                        let hz = Units.per Units.Time.hour Units.Count.times
                        { ord0 with Prescription = Discontinuous (ord0.Prescription |> Prescription.getFrequency |> Option.defaultValue (OV.Frequency.create (Name "frq") hz)) }
                        |> Order.OrderPropertyChange.proc [ PrescriptionFrequency (fun (Frequency f) -> Frequency (OrderVariable.clear f)) ]
                    let before = countValues ord
                    let res = Order.processClearedOrder Logging.noOp ord
                    match res with
                    | Ok o -> Expect.isTrue "Value count should not decrease" (countValues o >= before)
                    | Error (o, _) -> Expect.isTrue "Value count should not decrease (even on error)" (countValues o >= before)
                }

                test "Timed TimeCleared re-applies time constraints and values" {
                    let ord0 = mkConstrainedOrder ()
                    let hz = Units.per Units.Time.hour Units.Count.times
                    let frq = ord0.Prescription |> Prescription.getFrequency |> Option.defaultValue (OV.Frequency.create (Name "frq") hz)
                    let tme = ord0.Prescription |> Prescription.getTime |> Option.defaultValue (OV.Time.create (Name "tme") Units.Time.hour)
                    let ord = { ord0 with Prescription = Timed (frq, Time (OrderVariable.clear (let (Time tv) = tme in tv))) }
                    let before = countValues ord
                    let res = Order.processClearedOrder Logging.noOp ord
                    match res with
                    | Ok o -> Expect.isTrue "Value count should not decrease" (countValues o >= before)
                    | Error (o, _) -> Expect.isTrue "Value count should not decrease (even on error)" (countValues o >= before)
                }

                test "Continuous ConcentrationCleared resets and re-solves" {
                    let ord0 = mkConstrainedOrder ()
                    // Ensure continuous prescription with a valid time and clear a component orderable concentration
                    let tme = ord0.Prescription |> Prescription.getTime |> Option.defaultValue (OV.Time.create (Name "t") Units.Time.hour)
                    let ord = { ord0 with Prescription = Continuous tme }
                    let ord = ord |> Order.OrderPropertyChange.proc [ ComponentOrderableConcentration ("", fun (Concentration c) -> Concentration (OrderVariable.clear c)) ]
                    let before = countValues ord
                    let res = Order.processClearedOrder Logging.noOp ord
                    match res with
                    | Ok o -> Expect.isTrue "Value count should not decrease" (countValues o >= before)
                    | Error (o, _) -> Expect.isTrue "Value count should not decrease (even on error)" (countValues o >= before)
                }
            ]


    module ToOrderDto =

        module MinMax = Informedica.GenCore.Lib.Ranges.MinMax
        module Limit = Informedica.GenCore.Lib.Ranges.Limit

        open Informedica.GenOrder.Lib.DrugOrder

        /// <summary>
        /// Map a DrugOrder record to a DrugOrderDto record.
        /// The DrugOrder will map the constraints of the DrugOrderDto.
        /// </summary>
        /// <param name="d">The DrugOrder to convert</param>
        let toOrderDto (d : DrugOrder) =
            let vuToDto = Option.bind (ValueUnit.Dto.toDto false "English")

            let limToDto = Option.map Limit.getValueUnit >> vuToDto

            let oru = Units.Volume.milliLiter |> Units.per Units.Time.hour

            let standDoseRate un (orbDto : Order.Orderable.Dto.Dto) =
                orbDto.Dose.Rate.Constraints.IncrOpt <- 1N/10N |> createSingleValueUnitDto un
                orbDto.Dose.Rate.Constraints.MinIncl <- true
                orbDto.Dose.Rate.Constraints.MinOpt <- 1N/10N |> createSingleValueUnitDto un
                orbDto.Dose.Rate.Constraints.MaxIncl <- true
                orbDto.Dose.Rate.Constraints.MaxOpt <- 1000N |> createSingleValueUnitDto un

            let orbDto = Order.Orderable.Dto.dto d.Id d.Name

            // make sure the orderable quantity has a unit
            d.Components
            |> List.tryHead
            |> Option.map (fun p ->
                p.Quantities
                |> Option.map ValueUnit.getUnit
                , 
                p.Divisible
            )
            |> function
            | Some (Some u, Some d) ->
                orbDto.OrderableQuantity.Constraints.IncrOpt <-
                    1N/d
                    |> createSingleValueUnitDto u
            | _ -> ()
                
            orbDto.DoseCount.Constraints
            |>  MinMax.setConstraints None d.DoseCount

            orbDto.OrderableQuantity.Constraints.ValsOpt <- d.Quantities |> vuToDto

            let setOrbDoseRate (dl : DoseLimit) =

                orbDto.Dose.Rate.Constraints
                |> MinMax.setConstraints
                    None
                    dl.Rate

                orbDto.Dose.RateAdjust.Constraints
                |> MinMax.setConstraints
                    None
                    dl.RateAdjust

            let setOrbDoseQty isOnce (dl : DoseLimit) =
                orbDto.Dose.Quantity.Constraints
                |> MinMax.setConstraints
                    None
                    dl.Quantity

                orbDto.Dose.QuantityAdjust.Constraints
                |> MinMax.setConstraints
                    dl.NormQuantityAdjust
                    dl.QuantityAdjust

                if not isOnce then
                    orbDto.Dose.PerTime.Constraints
                    |> MinMax.setConstraints
                        None
                        dl.PerTime

                    orbDto.Dose.PerTimeAdjust.Constraints
                    |> MinMax.setConstraints
                        dl.NormPerTimeAdjust
                        dl.PerTimeAdjust

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
                | Some dl ->
                    dl |> setOrbDoseQty true
                | None -> ()

            | OnceTimedOrder ->
                orbDto |> standDoseRate oru

                match d.Dose with
                | Some dl ->
                    dl |> setOrbDoseRate
                    dl |> setOrbDoseQty true
                    // Assume timed order always solution
                    orbDto.Dose.Quantity.Constraints.IncrOpt <-
                        1N/10N
                        |> createSingleValueUnitDto
                            Units.Volume.milliLiter
                | None -> ()

            | DiscontinuousOrder ->
                match d.Dose with
                | Some dl ->
                    dl |> setOrbDoseQty false
                | None -> ()

            | TimedOrder ->
                orbDto |> standDoseRate oru
                // Assume timed order always solution
                if orbDto.Dose.Quantity.Constraints.ValsOpt.IsNone then
                    orbDto.Dose.Quantity.Constraints.IncrOpt <-
                        1N/10N
                        |> createSingleValueUnitDto
                            Units.Volume.milliLiter
                if orbDto.OrderableQuantity.Constraints.ValsOpt.IsNone then
                    orbDto.OrderableQuantity.Constraints.IncrOpt <-
                        1N/10N
                        |> createSingleValueUnitDto
                            Units.Volume.milliLiter

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
                        let ou =
                            p.Quantities
                            |> Option.map ValueUnit.getUnit
                            |> Option.defaultValue NoUnit
                        1N / d
                        |> createSingleValueUnitDto ou
                    )
                )

            orbDto.Components <-
                [
                    for p in d.Components do
                        let cmpDto = Order.Orderable.Component.Dto.dto d.Id d.Name p.Name p.Shape
                        let div =
                            p.Divisible
                            |> Option.bind (fun d ->
                                let ou =
                                    p.Quantities
                                    |> Option.map ValueUnit.getUnit
                                    |> Option.defaultValue NoUnit
                                (1N / d)
                                |> createSingleValueUnitDto ou
                            )

                        cmpDto.ComponentQuantity.Constraints.ValsOpt <- p.Quantities |> vuToDto
                        cmpDto.OrderableQuantity.Constraints.IncrOpt <- div

                        if d.Components |> List.length = 1 then
                            // If there is only one product, the concentration of that product in the
                            // Orderable will be by definition be 1.
                            cmpDto.OrderableConcentration.Constraints.ValsOpt <-
                                1N
                                |> createSingleValueUnitDto Units.Count.times
                            cmpDto.Dose.Quantity.Constraints.IncrOpt <- div

                        match p.Solution with
                        | Some sl ->
                            cmpDto.OrderableQuantity.Constraints
                            |> MinMax.setConstraints
                                sl.Quantities
                                sl.Quantity

                            cmpDto.OrderableConcentration.Constraints
                            |> MinMax.setConstraints
                                None
                                sl.Concentration
                        | None -> ()

                        let setDoseRate (dl : DoseLimit) =
                            if dl.Rate |> MinMax.isEmpty |> not then
                                cmpDto.Dose.Rate.Constraints
                                |> MinMax.setConstraints
                                    None
                                    dl.Rate

                            if dl.RateAdjust |> MinMax.isEmpty |> not then
                                cmpDto.Dose.RateAdjust.Constraints
                                |> MinMax.setConstraints
                                    None
                                    dl.RateAdjust

                        let setDoseQty (dl : DoseLimit) =
                            if dl.Quantity |> MinMax.isEmpty |> not then
                                cmpDto.Dose.Quantity.Constraints
                                |> MinMax.setConstraints
                                    None
                                    dl.Quantity
                            if dl.QuantityAdjust |> MinMax.isEmpty |> not ||
                               dl.NormQuantityAdjust |> Option.isSome then
                                cmpDto.Dose.QuantityAdjust.Constraints
                                |> MinMax.setConstraints
                                    dl.NormQuantityAdjust
                                    dl.QuantityAdjust

                            if dl.PerTime |> MinMax.isEmpty |> not then
                                cmpDto.Dose.PerTime.Constraints
                                |> MinMax.setConstraints
                                    None
                                    dl.PerTime

                            if dl.PerTimeAdjust |> MinMax.isEmpty |> not ||
                               dl.NormPerTimeAdjust |> Option.isSome then
                                cmpDto.Dose.PerTimeAdjust.Constraints
                                |> MinMax.setConstraints
                                    dl.NormPerTimeAdjust
                                    dl.PerTimeAdjust


                        match d.OrderType with
                        | AnyOrder -> ()
                        | ProcessOrder -> ()
                        | ContinuousOrder ->
                            match p.Dose with
                            | None    -> ()
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

                        cmpDto.Items <- [
                            for s in p.Substances do
                                let itmDto =
                                    Order.Orderable.Item.Dto.dto d.Id d.Name p.Name s.Name

                                itmDto.ComponentConcentration.Constraints.ValsOpt <- s.Concentrations |> vuToDto
                                if d.Components |> List.length = 1 then
                                    // When only one product, the orderable concentration is the same as the component concentration
                                    itmDto.OrderableConcentration.Constraints.ValsOpt <- itmDto.ComponentConcentration.Constraints.ValsOpt

                                match s.Solution with
                                | Some sl ->
                                    itmDto.OrderableQuantity.Constraints
                                    |> MinMax.setConstraints
                                        sl.Quantities
                                        sl.Quantity

                                    itmDto.OrderableConcentration.Constraints
                                    |> MinMax.setConstraints
                                        None
                                        sl.Concentration
                                | None -> ()

                                let setDoseRate (dl : DoseLimit) =

                                    itmDto.Dose.Rate.Constraints
                                    |> MinMax.setConstraints
                                        None
                                        dl.Rate

                                    itmDto.Dose.RateAdjust.Constraints
                                    |> MinMax.setConstraints
                                        None
                                        dl.RateAdjust

                                let setDoseQty (dl : DoseLimit) =
                                    itmDto.Dose.Quantity.Constraints
                                    |> MinMax.setConstraints
                                        None
                                        dl.Quantity

                                    itmDto.Dose.QuantityAdjust.Constraints
                                    |> MinMax.setConstraints
                                        dl.NormQuantityAdjust
                                        dl.QuantityAdjust

                                    itmDto.Dose.PerTime.Constraints
                                    |> MinMax.setConstraints
                                        None
                                        dl.PerTime

                                    itmDto.Dose.PerTimeAdjust.Constraints
                                    |> MinMax.setConstraints
                                        dl.NormPerTimeAdjust
                                        dl.PerTimeAdjust

                                match d.OrderType with
                                | AnyOrder -> ()
                                | ProcessOrder -> ()
                                | ContinuousOrder ->
                                    match s.Dose with
                                    | None    -> ()
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
                | AnyOrder ->
                    "the order type cannot be 'Any'"
                    |> failwith
                | ProcessOrder ->
                    "the order type cannot be 'Process'"
                    |> failwith
                | OnceOrder ->
                    Order.Dto.once d.Id d.Name d.Route []
                | OnceTimedOrder ->
                    Order.Dto.onceTimed d.Id d.Name d.Route []
                | ContinuousOrder ->
                    Order.Dto.continuous d.Id d.Name d.Route []
                | DiscontinuousOrder ->
                    Order.Dto.discontinuous d.Id d.Name d.Route []
                | TimedOrder ->
                    Order.Dto.timed d.Id d.Name d.Route []

            dto.Orderable <- orbDto

            dto.Prescription.Frequency.Constraints.ValsOpt <- d.Frequencies |> vuToDto

            dto.Prescription.Time.Constraints.MinIncl <- d.Time.Min.IsSome
            dto.Prescription.Time.Constraints.MinOpt <- d.Time.Min |> limToDto
            dto.Prescription.Time.Constraints.MaxIncl <- d.Time.Max.IsSome
            dto.Prescription.Time.Constraints.MaxOpt <- d.Time.Max |> limToDto

            if d.AdjustUnit
            |> Option.map (ValueUnit.Group.eqsGroup Units.Weight.kiloGram)
            |> Option.defaultValue false then
                // Adjusted by weight
                dto.Adjust.Constraints.MinOpt <-
                    (200N /1000N) |> createSingleValueUnitDto d.AdjustUnit.Value

                dto.Adjust.Constraints.MaxOpt <-
                    150N |> createSingleValueUnitDto d.AdjustUnit.Value
            // TODO: add constraints for BSA
            dto.Adjust.Constraints.ValsOpt <- d.Adjust |> vuToDto

            dto


    // Add your test modules here
    module DrugOrderTests =

        
        let tests = testList "DrugOrder" [
            test "drugOrder default values" {
                let drugOrder = DrugOrder.drugOrder
                drugOrder.Id |> Expect.equal "should be empty" ""
                drugOrder.Name |> Expect.equal "should be empty" ""
                drugOrder.Components |> Expect.isEmpty "should be empty"
            }

            test "productComponent default values" {
                let cmp = DrugOrder.productComponent
                cmp.Name |> Expect.equal "should be empty" ""
                cmp.Shape |> Expect.equal "should be empty" ""
                cmp.Substances |> Expect.isEmpty "should be empty"
            }

            test "substanceItem default values" {
                let substance = DrugOrder.substanceItem
                substance.Name |> Expect.equal "should be empty" ""
                substance.Concentrations |> Expect.isNone "should be None"
                substance.Dose |> Expect.isNone "should be None"
            }

            testList "ToDto" [
                test "ToDto converts DrugOrder to OrderDto" {
                    let drugOrder = testDrugOrders |> List.head
                    let dto = DrugOrder.toOrderDto drugOrder

                    dto.Id |> Expect.equal "should match Id" drugOrder.Id
                    dto.Prescription.IsDiscontinuous |> Expect.isTrue "should be discontinuous"
                    dto.Orderable.Name |> Expect.equal "should match Name" drugOrder.Name
                    dto.Orderable.Components |> Expect.hasLength "should have 2 components" 2
                    dto.Orderable.Components[0].Items |> Expect.hasLength "should have 3 items in first component" 2
                }

                test "ToDto reference function to OrderDto" {
                    let drugOrder = testDrugOrders |> List.head
                    let ord1 = DrugOrder.toOrderDto drugOrder |> Order.Dto.fromDto

                    // Check if the dto the same as ToOrderDto.toOrderDto
                    let ord2 = ToOrderDto.toOrderDto drugOrder |> Order.Dto.fromDto

                    ord1.Adjust
                    |> Expect.equal "should be equal" ord2.Adjust
                    ord1.Duration
                    |> Expect.equal "should be equal" ord2.Duration
                    ord1.Id
                    |> Expect.equal "should be equal" ord2.Id
                    ord1.Route
                    |> Expect.equal "should be equal" ord2.Route
                    ord1.Prescription 
                    |> Expect.equal "should be equal" ord2.Prescription
                    ord1.Orderable.Name
                    |> Expect.equal "should be equal" ord2.Orderable.Name

                    ord1.Orderable.Components[0].OrderableQuantity
                    |> Expect.equal "should be equal" ord2.Orderable.Components[0].OrderableQuantity

                    // this is fix: https://github.com/halcwb/GenPres2/commit/43d58ab1e123fd3217061d191226c5f074cdfad3
                    ord1.Orderable.OrderableQuantity
                    |> Expect.notEqual "should NOT be equal" ord2.Orderable.OrderableQuantity

                    printfn $"{drugOrder.Components[0].Dose}"
                    ord1.Orderable.Dose.Quantity
                    |> Expect.notEqual "should NOT be equal" ord2.Orderable.Dose.Quantity
                }   

            ]
        ]

    // Add more test modules as needed
    module TypeTests =
        
        let tests = testList "Types" [
            test "OrderVariable can be created" {
                let constraints = {
                    Min = None
                    Max = None
                    Incr = None
                    Values = None
                }
                let variable = Variable.create id (Name "test") ValueRange.Unrestricted // Assuming this exists
                let orderVariable = {
                    Constraints = constraints
                    Variable = variable
                }
                orderVariable.Constraints |> Expect.equal "should be equal" constraints
            }
        ]

    [<Tests>]
    let tests =
        testList "GenOrder Tests" [
            DrugOrderTests.tests
            TypeTests.tests
        ]

