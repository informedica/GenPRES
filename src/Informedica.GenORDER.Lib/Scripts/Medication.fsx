#time

#load "load.fsx"

// load demo or product cache

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types.Logging
open Informedica.GenSolver.Lib.Types.Logging

open Informedica.Logging.Lib
open Informedica.Agents.Lib

open Expecto
open Expecto.Flip


let devScriptFormatter =
    MessageFormatter.create
        [
            typeof<OrderMessage>, OrderLogging.formatOrderMessage
            typeof<SolverMessage>, SolverLogging.formatSolverMessage
            typeof<Informedica.GenForm.Lib.Types.Message>, Informedica.GenForm.Lib.FormLogging.formatMessage
        ]

let consoleLogger = ConsoleFileLogger.createConsole devScriptFormatter


let fileLogger = ConsoleFileLogger.createFile "log.txt" devScriptFormatter


module HelperFunctions =

    open Informedica.GenOrder.Lib


    let print sl = sl |> List.iter (printfn "%s")


    let inline printOrderTable order =
        order |> Result.iter (Order.printTable ConsoleTables.Format.Minimal)

        order


    let solveOrder order =
        match order with
        | Error e -> $"Error solving order: {e}" |> failwith
        | Ok o -> o |> Order.solveMinMax "Solve Order" true OrderLogging.noOp


    let run logger med cmds =
        let logger, usePrintTable =
            logger |> Option.defaultValue OrderLogging.noOp, logger.IsNone

        let rec loop cmds ord =
            ord |> Result.iter (OrderProcessor.classify >> printfn "%A")

            match cmds with
            | [] -> ord |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
            | cmd :: rest ->
                match ord with
                | Error(_, msgs) -> failwith $"Errors occured: {msgs}"
                | Ok ord ->
                    if usePrintTable then
                        ord |> Ok |> printOrderTable |> ignore

                    ord |> cmd |> OrderProcessor.processPipeline logger |> loop rest

        med
        |> Medication.toOrderDto System.DateTime.UtcNow
        |> Order.Dto.fromDto OrderLogging.noOp
        |> function
            | Error msg -> failwith $"{msg}"
            | Ok ord ->
                ord
                |> Ok
                |> fun ord -> if usePrintTable then ord |> printOrderTable else ord
                |> loop cmds


module GenFormResult = Utils.GenFormResult


module UnitValidation = Medication.UnitValidation


module MedicationTests =

    let tests =

        testList
            "medication"
            [

                // Test labeled DoseLimit.toString
                testList
                    "DoseLimit with field labels"
                    [
                        test "Quantity field gets [qty] label" {
                            let dl =
                                { DoseLimit.limit with
                                    Quantity =
                                        10N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> MinMax.createExact
                                }

                            let str = dl |> DoseLimit.toString |> String.concat ""
                            str |> Expect.stringContains "should contain [qty]" "[qty]"
                        }

                        test "QuantityAdjust field gets [qty-adj] label" {
                            let dl =
                                { DoseLimit.limit with
                                    QuantityAdjust =
                                        MinMax.createInclIncl
                                            (10N
                                             |> ValueUnit.singleWithUnit (
                                                 Units.Mass.milliGram |> Units.per Units.Weight.kiloGram
                                             ))
                                            (20N
                                             |> ValueUnit.singleWithUnit (
                                                 Units.Mass.milliGram |> Units.per Units.Weight.kiloGram
                                             ))
                                }

                            let str = dl |> DoseLimit.toString |> String.concat ""
                            str |> Expect.stringContains "should contain [qty-adj]" "[qty-adj]"
                        }

                        test "PerTimeAdjust field gets [per-time-adj] label" {
                            let dl =
                                { DoseLimit.limit with
                                    PerTimeAdjust =
                                        MinMax.createInclIncl
                                            (10N
                                             |> ValueUnit.singleWithUnit (
                                                 Units.Mass.milliGram
                                                 |> Units.per Units.Weight.kiloGram
                                                 |> Units.per Units.Time.day
                                             ))
                                            (20N
                                             |> ValueUnit.singleWithUnit (
                                                 Units.Mass.milliGram
                                                 |> Units.per Units.Weight.kiloGram
                                                 |> Units.per Units.Time.day
                                             ))
                                }

                            let str = dl |> DoseLimit.toString |> String.concat ""
                            str |> Expect.stringContains "should contain [per-time-adj]" "[per-time-adj]"
                        }

                        test "RateAdjust field gets [rate-adj] label" {
                            let dl =
                                { DoseLimit.limit with
                                    RateAdjust =
                                        MinMax.createInclIncl
                                            (10N
                                             |> ValueUnit.singleWithUnit (
                                                 Units.Mass.microGram
                                                 |> Units.per Units.Weight.kiloGram
                                                 |> Units.per Units.Time.hour
                                             ))
                                            (40N
                                             |> ValueUnit.singleWithUnit (
                                                 Units.Mass.microGram
                                                 |> Units.per Units.Weight.kiloGram
                                                 |> Units.per Units.Time.hour
                                             ))
                                }

                            let str = dl |> DoseLimit.toString |> String.concat ""
                            str |> Expect.stringContains "should contain [rate-adj]" "[rate-adj]"
                        }
                    ]

                // Unit validation tests
                testList
                    "Unit validation"
                    [
                        test "hasAdjustUnit detects kg" {
                            let unit = Units.Mass.milliGram |> Units.per Units.Weight.kiloGram

                            UnitValidation.hasAdjustUnit unit
                            |> Expect.isTrue "should detect kg as adjust unit"
                        }

                        test "hasAdjustUnit detects m2" {
                            let unit = Units.Mass.milliGram |> Units.per Units.BSA.m2

                            UnitValidation.hasAdjustUnit unit
                            |> Expect.isTrue "should detect m2 as adjust unit"
                        }

                        test "hasTimeUnit detects day" {
                            let unit = Units.Mass.milliGram |> Units.per Units.Time.day

                            UnitValidation.hasTimeUnit unit
                            |> Expect.isTrue "should detect day as time unit"
                        }

                        test "hasTimeUnit detects hour" {
                            let unit = Units.Volume.milliLiter |> Units.per Units.Time.hour

                            UnitValidation.hasTimeUnit unit
                            |> Expect.isTrue "should detect hour as time unit"
                        }

                        test "complex unit mg/kg/dag has both adjust and time" {
                            let unit =
                                Units.Mass.milliGram
                                |> Units.per Units.Weight.kiloGram
                                |> Units.per Units.Time.day

                            UnitValidation.hasAdjustUnit unit |> Expect.isTrue "should have adjust unit"
                            UnitValidation.hasTimeUnit unit |> Expect.isTrue "should have time unit"
                        }
                    ]

                // Roundtrip tests
                test "pcmSupp roundtrip - basic fields" {
                    let original = Scenarios.pcmSupp
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med.Id |> Expect.equal "Id" original.Id
                        med.Name |> Expect.equal "Name" original.Name
                        med.Route |> Expect.equal "Route" original.Route
                        med.OrderType |> Expect.equal "OrderType" original.OrderType

                        med.Components.Length
                        |> Expect.equal "Components count" original.Components.Length
                }

                test "pcmSupp roundtrip - component details" {
                    let original = Scenarios.pcmSupp
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        let origCmp = original.Components |> List.head
                        let parsedCmp = med.Components |> List.head
                        parsedCmp.Name |> Expect.equal "Component Name" origCmp.Name
                        parsedCmp.Form |> Expect.equal "Component Form" origCmp.Form

                        parsedCmp.Substances.Length
                        |> Expect.equal "Substances count" origCmp.Substances.Length
                }

                test "pcmSupp roundtrip - full roundtrip" {
                    let original = Scenarios.pcmSupp
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med
                        |> Medication.toString
                        |> String.concat "\n"
                        |> Expect.equal "should resemble the original text" text
                }

                test "amfo roundtrip - PerTimeAdjust field" {
                    let original = Scenarios.amfo
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med.Id |> Expect.equal "Id" original.Id
                        med.Name |> Expect.equal "Name" original.Name
                        med.OrderType |> Expect.equal "OrderType" original.OrderType

                        // Check the substance has PerTimeAdjust
                        let origSubstance =
                            original.Components
                            |> List.head
                            |> _.Substances
                            |> List.find (fun s -> s.Name = "amfotericine b liposomaal")

                        let parsedSubstance =
                            med.Components
                            |> List.head
                            |> _.Substances
                            |> List.find (fun s -> s.Name = "amfotericine b liposomaal")

                        parsedSubstance.Dose.IsSome |> Expect.isTrue "Dose should be Some"

                        parsedSubstance.Dose.Value.PerTimeAdjust
                        |> Expect.notEqual "PerTimeAdjust should not be empty" MinMax.empty
                }

                test "morfCont roundtrip - RateAdjust field" {
                    let original = Scenarios.morfCont
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med.Id |> Expect.equal "Id" original.Id
                        med.OrderType |> Expect.equal "OrderType" original.OrderType

                        // Check the substance has RateAdjust
                        let parsedSubstance =
                            med.Components
                            |> List.head
                            |> _.Substances
                            |> List.find (fun s -> s.Name = "morfin")

                        parsedSubstance.Dose.IsSome |> Expect.isTrue "Dose should be Some"

                        parsedSubstance.Dose.Value.RateAdjust
                        |> Expect.notEqual "RateAdjust should not be empty" MinMax.empty
                }

                test "cotrim roundtrip - QuantityAdjust field" {
                    let original = Scenarios.cotrim
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med.Id |> Expect.equal "Id" original.Id
                        med.OrderType |> Expect.equal "OrderType" original.OrderType

                        // Check the substances have QuantityAdjust
                        let parsedSubstances = med.Components |> List.head |> _.Substances

                        for sub in parsedSubstances do
                            sub.Dose.IsSome |> Expect.isTrue $"Dose for {sub.Name} should be Some"

                            sub.Dose.Value.QuantityAdjust
                            |> Expect.notEqual $"QuantityAdjust for {sub.Name} should not be empty" MinMax.empty
                }

                test "tpn roundtrip - complex multi-component" {
                    let original = Scenarios.tpn
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med.Id |> Expect.equal "Id" original.Id

                        med.Components.Length
                        |> Expect.equal "Components count" original.Components.Length

                        // Check component doses with QuantityAdjust
                        for i, origCmp in original.Components |> List.indexed do
                            let parsedCmp = med.Components[i]
                            parsedCmp.Name |> Expect.equal $"Component {i} name" origCmp.Name

                            if origCmp.Dose.IsSome then
                                parsedCmp.Dose.IsSome |> Expect.isTrue $"Component {i} Dose should be Some"
                }

                test "fullMedication roundtrip - all fields" {
                    let original = Scenarios.fullMedication
                    let text = original |> Medication.toString |> String.concat "\n"

                    match text |> Medication.fromString with
                    | Error errs ->
                        let errMsg = errs |> String.concat "; "
                        failwith $"Parse failed: {errMsg}"
                    | Ok med ->
                        med.Id |> Expect.equal "Id" original.Id
                        med.Name |> Expect.equal "Name" original.Name
                        med.Route |> Expect.equal "Route" original.Route
                        med.OrderType |> Expect.equal "OrderType" original.OrderType

                        med.Components.Length
                        |> Expect.equal "Components count" original.Components.Length
                        // Check Div field
                        med.Div.IsSome |> Expect.equal "Div is Some" original.Div.IsSome
                }

                test "fromString returns error for invalid OrderType" {
                    let invalidText =
                        """
Id: test-id
Name: test
Route: test
OrderType: InvalidType
Components:
"""

                    match invalidText |> Medication.fromString with
                    | Error errs ->
                        errs
                        |> List.exists _.Contains("Unknown OrderType")
                        |> Expect.isTrue "should contain OrderType error"
                    | Ok _ -> failwith "Expected error for invalid OrderType"
                }

                // Test that labels enable deterministic parsing
                test "labeled parsing is deterministic for QuantityAdjust vs PerTimeAdjust" {
                    // This tests that with labels, we can distinguish fields that have similar units
                    let dlWithQtyAdj =
                        { DoseLimit.limit with
                            DoseLimitTarget = "test" |> SubstanceLimitTarget
                            QuantityAdjust =
                                MinMax.createInclIncl
                                    (10N
                                     |> ValueUnit.singleWithUnit (
                                         Units.Mass.milliGram |> Units.per Units.Weight.kiloGram
                                     ))
                                    (20N
                                     |> ValueUnit.singleWithUnit (
                                         Units.Mass.milliGram |> Units.per Units.Weight.kiloGram
                                     ))
                        }

                    let str = dlWithQtyAdj |> DoseLimit.toString |> String.concat ""
                    str |> Expect.stringContains "should have [qty-adj] label" "[qty-adj]"
                    str |> Expect.stringContains "should NOT have [per-time-adj] label" |> ignore

                    (str.Contains("[per-time-adj]") |> not)
                    |> Expect.isTrue "should NOT have [per-time-adj]"
                }

                // Indentation handling tests - verify both tabs and spaces work
                testList
                    "parseLine handles different indentation"
                    [
                        test "parseLine handles tab indentation" {
                            let line = "\t\tName: test"

                            match Medication.Parser.parseLine line with
                            | Some(indent, key, value) ->
                                indent |> Expect.equal "should be indent 2" 2
                                key |> Expect.equal "key" "Name"
                                value |> Expect.equal "value" "test"
                            | None -> failwith "Expected successful parse"
                        }

                        test "parseLine handles space indentation (4 spaces = 1 indent)" {
                            let line = "        Name: test" // 8 spaces = indent 2

                            match Medication.Parser.parseLine line with
                            | Some(indent, key, value) ->
                                indent |> Expect.equal "should be indent 2" 2
                                key |> Expect.equal "key" "Name"
                                value |> Expect.equal "value" "test"
                            | None -> failwith "Expected successful parse"
                        }

                        test "fromString works with space-indented input" {
                            // This simulates what VS Code FSI might send
                            let spaceIndented =
                                """
Id: test-id
Name: test-med
Route: ORAAL
OrderType: OnceOrder
Components:

    Name: comp1
    Form: tablet
    Substances:

        Name: subst1
        Concentrations: 10 mg/stuk
"""

                            match spaceIndented |> Medication.fromString with
                            | Error errs ->
                                let errMsg = errs |> String.concat "; "
                                failwith $"Parse with spaces failed: {errMsg}"
                            | Ok med ->
                                med.Components.Length |> Expect.equal "should have 1 component" 1
                                let comp = med.Components |> List.head
                                comp.Name |> Expect.equal "component name" "comp1"
                                comp.Substances.Length |> Expect.equal "should have 1 substance" 1
                        }
                    ]
            ]


module MedicationTexts =


    let onceSingleComponentMultipleItemsNoDose =
        """
Id: 3beb2d76-625c-4e02-8c49-bcd5fa6f5166
Name: chloorhexidine
Quantity:
Quantities:
Route: CUTAAN
OrderType: OnceOrder
Adjust: 11 kg
Frequencies:
Time:
Dose:
Div:
DoseCount: 1 x
Components:

	Name: chloorhexidine
	Form: oplossing voor cutaan gebruik
	Quantities: 1;100;50 ml
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: ethanol, gedenatureerd
		Concentrations: 539 mg/ml
		Dose:
		Solution:

		Name: chloorhexidine
		Concentrations: 5;10;40 mg/ml
		Dose:
		Solution:
"""


    // Once single component single item scenario
    let onceSingleComponentSingleItem =
        """
Id: 93e8c175-99a1-48d8-b2f4-90005fdb8ada
Name: paracetamol
Quantity:
Quantities:
Route: RECTAAL
OrderType: OnceOrder
Adjust: 10 kg
Frequencies:
Time:
Dose: [dun], [qty] 1 stuk/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: zetpil
	Quantities: 1 stuk
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 120;240;500;1000;125;250;60;30;360;90;750;180 mg/stuk
		Dose: paracetamol, [dun] mg, [qty-adj] 40 mg/kg/dosis, [qty] max 1000 mg/dosis
		Solution:
"""

    // OnceTimed single component single item scenario
    let onceTimedSingleComponentSingleItem =
        """
Id: 95c44266-84c5-4969-a815-9fbf2c9ed693
Name: paracetamol
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: OnceTimedOrder
Adjust: 10 kg
Frequencies:
Time: 15 min - 20 min
Dose: [dun], [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: infusievloeistof
	Quantities: 100;50 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 10 mg/ml
		Dose: paracetamol, [dun] mg, [qty-adj] 20 mg/kg/dosis, [qty] max 1000 mg/dosis
		Solution:
"""

    // Discontinuous single component single item scenario
    let discontinuousSingleComponentSingleItem =
        """
Id: d595fdbd-51ae-489d-a316-a458b7d5d032
Name: paracetamol
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 40 kg
Frequencies: 1;2;3;4 x/day
Time:
Dose: [qty] max 10 stuk/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: tablet
	Quantities: 1 stuk
	Divisible: 4
	Dose:
	Solution:
	Substances:

		Name: natriumwaterstofcarbonaat
		Concentrations: 632 mg/stuk
		Dose:
		Solution:

		Name: paracetamol
		Concentrations: 500;1000 mg/stuk
		Dose: paracetamol, [dun] mg, [per-time] max 4000 mg/day, [qty-adj] 10 mg/kg - 15 mg/kg/dosis
		Solution:
"""

    let discontinousMultipleComponentMultipleItems =
        """
Id: d1326abe-ca06-4c59-a52c-7af1152b75c4
Name: amoxicilline/clavulaanzuur
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 10 kg
Frequencies: 3 x/day
Time:
Dose:
Div:
DoseCount: 1 x
Components:

	Name: amoxicilline/clavulaanzuur
	Form: poeder voor oplossing voor infusie
	Quantities: 20 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: amoxicilline
		Concentrations: 100 mg/ml
		Dose: amoxicilline, [dun] mg, [per-time-adj] 100 mg/kg/day, [per-time] max 6000 mg/day
		Solution:

		Name: clavulaanzuur
		Concentrations: 10 mg/ml
		Dose: clavulaanzuur, [dun] mg, [per-time-adj] 10 mg/kg/day, [per-time] max 600 mg/day
		Solution:
"""

    // Timed single component single item scenario
    let timedSingleComponentSingleItem =
        """
Id: a9e18942-f879-4df1-bc21-6375c3291ed7
Name: paracetamol
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 10 kg
Frequencies: 4 x/day
Time: 15 min - 20 min
Dose: [dun], [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: infusievloeistof
	Quantities: 100;50 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: paracetamol
		Concentrations: 10 mg/ml
		Dose: paracetamol, [dun] mg, [per-time-adj] 60 mg/kg/day, [per-time] max 4000 mg/day, [qty] max 1000 mg/dosis
		Solution:
"""

    let continuousSingleComponentSingleItem =
        """
Id: 6854e269-df1c-480f-ac58-a08fe108a59d
Name: propofol
Quantity:
Quantities: 50 ml
Route: INTRAVENEUS
OrderType: ContinuousOrder
Adjust: 40 kg
Frequencies:
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: propofol
	Form: emulsie voor injectie
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: propofol
		Concentrations: 20;10 mg/ml
		Dose: propofol, [dun] mg, [rate-adj] 1 mg/kg/hr - 4 mg/kg/hr
		Solution:
"""

    let continuousMultipleComponent =
        """"
Id: b5189d1a-c1c5-4223-9b2d-e8e35e1b22fd
Name: noradrenaline
Quantity:
Quantities: 50 ml
Route: INTRAVENEUS
OrderType: ContinuousOrder
Adjust: 40 kg
Frequencies:
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: noradrenaline
	Form: concentraat voor oplossing voor infusie
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: noradrenaline
		Concentrations: 1 mg/ml
		Dose: noradrenaline, [dun] microg, [rate-adj] 0.05 microg/kg/min - 2 microg/kg/min
		Solution: [qty] 5 mg  [conc] max 1 mg/ml

	Name: gluc 10%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.4 kCal/ml
		Dose:
		Solution:

		Name: koolhydraat
		Concentrations: 0.1 g/ml
		Dose:
		Solution:
"""


    let timedMultipleComponentsDoseComponent =
        """
Id: a16b1489-d1c3-4f1e-a0ae-e83b18e1ebd5
Name: samenstelling c
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 10 kg
Frequencies: 1 x/day
Time: 20 hr - 24 hr
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: Samenstelling C
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose: Samenstelling C, [dun] ml, [qty-adj] 10 ml/kg/dosis
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.32 kCal/ml
		Dose:
		Solution:

		Name: eiwit
		Concentrations: 0.08 g/ml
		Dose:
		Solution:  [conc] max 0.05 g/ml

		Name: natrium
		Concentrations: 0.008 mmol/ml
		Dose:
		Solution:  [conc] max 0.5 mmol/ml

		Name: kalium
		Concentrations: 0.02 mmol/ml
		Dose:
		Solution:  [conc] max 0.5 mmol/ml

	Name: NaCl 3%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose: NaCl 3%, [dun] ml, [qty-adj] 6 ml/kg/dosis
	Solution:
	Substances:

		Name: natrium
		Concentrations: 0.5 mmol/ml
		Dose:
		Solution:  [conc] max 0.5 mmol/ml

	Name: KCl 7,4%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose: KCl 7,4%, [dun] ml, [qty-adj] 2 ml/kg/dosis
	Solution:
	Substances:

		Name: kalium
		Concentrations: 1 mmol/ml
		Dose:
		Solution:  [conc] max 0.5 mmol/ml

	Name: gluc 10%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose: gluc 10%, [dun] ml, [qty-adj] 65 ml/kg - 80 ml/kg/dosis
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.4 kCal/ml
		Dose:
		Solution:

		Name: koolhydraat
		Concentrations: 0.1 g/ml
		Dose:
		Solution:
"""

    let pcmDrink =
        """
Id: 34b81d68-86e5-4ec6-a223-cba9d2837530
Name: paracetamol
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 17 kg
Frequencies: 1;2;3;4 x/day
Time:
Dose: [qty-adj] max 10 ml/kg/dosis, [qty] max 500 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: paracetamol
	Form: drank
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: benzylalcohol
		Quantities:
		Concentrations: 0.48 mg/ml
		Dose:
		Solution:

		Name: propyleenglycol
		Quantities:
		Concentrations: 5.44 mg/ml
		Dose:
		Solution:

		Name: paracetamol
		Quantities:
		Concentrations: 24 mg/ml
		Dose: paracetamol, [dun] mg, [per-time] max 4000 mg/day, [qty-adj] 10 mg/kg - 15 mg/kg/dosis
		Solution:
"""


    let vancoReconst =
        """
Id: 13e4f4e5-059d-47d6-8882-46cc2ed0f072
Name: vancomycine
Quantity:
Quantities: 50;100;250 ml
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 14.5 kg
Frequencies: 3;4 x/day
Time: 60 min - 180 min
Dose: [dun] ml
Div:
DoseCount: min 1 x
Components:

	Name: vancomycine
	Form: poeder voor oplossing voor infusie
	Quantities: 20 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: vancomycine
		Concentrations: 50 mg/ml
		Dose: vancomycine, [dun] mg, [rate] max 1.7 mg/min, [per-time-adj] 60 mg/kg/day, [per-time] max 4000 mg/day
		Solution:  [conc] 2.5 mg/ml - 10 mg/ml

	Name: gluc 5%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.2 kCal/ml
		Dose:
		Solution:

		Name: koolhydraat
		Concentrations: 0.05 g/ml
		Dose:
		Solution:
"""

    /// This scenario has a variable glucose content which can result in
    /// an inappropriate low volume for the max protein concentration. This
    /// happens when recalculating all possible values. The solution is to
    /// treat the glucose component as a rest volume, i.e., only calculated.
    let tpnWithMaxQuantity =
        """
Id: 81607677-b226-4854-afd9-90faba665cc3
Name: samenstelling c
Quantity: max 830.5 ml
Quantities:
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 11 kg
Frequencies: 1 x/day
Time: 20 hr - 24 hr
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: Samenstelling C
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose: Samenstelling C, [dun] ml, [qty-adj] 10 ml/kg - 25 ml/kg/dosis
	Solution:
	Substances:

		Name: eiwit
		Quantities:
		Concentrations: 0.08 g/ml
		Dose:
		Solution:  [conc] max 0.05 g/ml

	Name: NaCl 3%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose: NaCl 3%, [dun] ml, [qty-adj] 6 ml/kg/dosis
	Solution:
	Substances:

		Name: natrium
		Quantities:
		Concentrations: 0.5 mmol/ml
		Dose:
		Solution:  [conc] max 0.5 mmol/ml

	Name: gluc 10%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: koolhydraat
		Quantities:
		Concentrations: 0.1 g/ml
		Dose:
		Solution:
"""

    /// test case for not solved component-orderable count
    let adenosinDayOne =
        """
54. 16,599: Informative
Medication created:

Id: 09be3945-a983-4209-88d5-a80006f57cd5
Name: adenosine
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: OnceOrder
Adjust: 10 kg
Frequencies:
Time:
Dose: [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: adenosine
	Form: infusievloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: adenosine
		Quantities:
		Concentrations: 3;2;5 mg/ml
		Dose: adenosine, [dun] microg, [qty-adj] 100 microg/kg/dosis, [qty] max 6000 microg/dosis
		Solution:
"""


module MedicationScenarios =

    open Informedica.Utils.Lib


    let createTest logger txt =
        test txt {
            txt
            |> Medication.fromString
            |> Result.bind (fun med -> [ OrderCommand.CalcMinMax ] |> run logger med |> Result.mapError (fun _ -> []))
            |> function
                | Ok _ -> true
                | Error _ -> false
            |> Expect.isTrue $"should be able to run: {txt}"
        }

    let scenarioTests logger =
        testList
            "Scenario Tests"
            [
                yield!
                    [
                        MedicationTexts.onceSingleComponentMultipleItemsNoDose
                        MedicationTexts.onceSingleComponentSingleItem
                        MedicationTexts.onceTimedSingleComponentSingleItem
                        MedicationTexts.discontinuousSingleComponentSingleItem
                        MedicationTexts.timedSingleComponentSingleItem
                        MedicationTexts.continuousSingleComponentSingleItem
                        MedicationTexts.continuousMultipleComponent
                        MedicationTexts.timedMultipleComponentsDoseComponent
                        MedicationTexts.pcmDrink
                        MedicationTexts.vancoReconst
                        MedicationTexts.discontinousMultipleComponentMultipleItems
                        MedicationTexts.tpnWithMaxQuantity
                    ]
                    // |> List.last |> List.singleton
                    |> List.map (createTest logger)
            ]


testList
    "Medication"
    [
        //MedicationTests.tests
        MedicationScenarios.scenarioTests None
    ]
|> runTestsWithCLIArgs [ CLIArguments.Sequenced ] [||]


"""
Id: d1326abe-ca06-4c59-a52c-7af1152b75c4
Name: amoxicilline/clavulaanzuur
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 10 kg
Frequencies: 3 x/day
Time:
Dose:
Div:
DoseCount: 1 x
Components:

	Name: amoxicilline/clavulaanzuur
	Form: poeder voor oplossing voor infusie
	Quantities: 20 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: amoxicilline
		Concentrations: 2000 mg/20 ml
		Dose: amoxicilline, [dun] mg, [per-time-adj] 100 mg/kg/day, [per-time] max 6000 mg/day
		Solution:

		Name: clavulaanzuur
		Concentrations: 200 mg/20 ml
		Dose: clavulaanzuur, [dun] mg, [per-time-adj] 10 mg/kg/day, [per-time] max 600 mg/day
		Solution:
"""
|> Medication.fromString
|> function
    | Error _ -> "fail" |> failwith
    | Ok med ->
        med
        |> Medication.toOrderDto System.DateTime.UtcNow
        |> Order.Dto.fromDto OrderLogging.noOp
        |> Result.map Order.toConsoleTableString


"""
Id: 13e4f4e5-059d-47d6-8882-46cc2ed0f072
Name: vancomycine
Quantity:
Quantities: 50;100;250 ml
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 14.5 kg
Frequencies: 3;4 x/day
Time: 60 min - 180 min
Dose: [dun] ml
Div:
DoseCount: min 1 x
Components:

	Name: vancomycine
	Form: poeder voor oplossing voor infusie
	Quantities: 20 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: vancomycine
		Concentrations: 50 mg/ml
		Dose: vancomycine, [dun] mg, [rate] max 1.7 mg/min, [per-time-adj] 60 mg/kg/day, [per-time] max 4000 mg/day
		Solution:  [conc] 2.5 mg/ml - 10 mg/ml

	Name: gluc 5%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.2 kCal/ml
		Dose:
		Solution:

		Name: koolhydraat
		Concentrations: 0.05 g/ml
		Dose:
		Solution:
"""
|> Medication.fromString
|> function
    | Error _ -> "fail" |> failwith
    | Ok med ->
        [
            CalcMinMax
            IncreaseIncrements
            OrderCommand.CalcValues
        //(fun ord -> (ord, DecreaseFrequency) |> ChangeProperty)
        //(fun ord -> (ord, SetMaxFrequency) |> ChangeProperty)
        //(fun ord -> (ord, "vancomycine" |> SetMaxComponentQuantity) |> ChangeProperty)
        //(fun ord -> (ord, SetMinDoseQuantity) |> ChangeProperty)
        ]
        |> HelperFunctions.run None med
|> ignore


MedicationTexts.timedMultipleComponentsDoseComponent
|> Medication.fromString
|> function
    | Error _ -> "fail" |> failwith
    | Ok med ->
        [
            CalcMinMax
            IncreaseIncrements
            (fun ord ->
                (ord, "Samenstelling C" |> SetMedianComponentOrderableQuantity)
                |> ChangeProperty
            )
            (fun ord -> (ord, "NaCl 3%" |> SetMedianComponentOrderableQuantity) |> ChangeProperty)
            (fun ord -> (ord, "KCl 7,4%" |> SetMedianComponentOrderableQuantity) |> ChangeProperty)
            (fun ord -> (ord, "gluc 10%" |> SetMedianComponentOrderableQuantity) |> ChangeProperty)
            (fun ord -> (ord, DecreaseOrderableDoseQuantity 5000) |> ChangeProperty)
            (fun ord -> (ord, "Samenstelling C" |> SetMinComponentOrderableQuantity) |> ChangeProperty)
        ]
        |> HelperFunctions.run (Some fileLogger) med
|> ignore


MedicationTexts.adenosinDayOne
|> Medication.fromString
|> function
    | Error _ -> "fail" |> failwith
    | Ok med -> [ CalcMinMax ] |> HelperFunctions.run None med
|> ignore


"""
Id: 0b404ef4-22c2-46fd-b553-77818e64ed0b
Name: natriumbenzoaat/natriumfenylacetaat
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: OnceTimedOrder
Adjust: 17 kg
Frequencies:
Time: 90 min - 120 min
Dose:
Div:
DoseCount: 1 x
Components:

	Name: natriumbenzoaat/natriumfenylacetaat
	Form: natriumbenzoaat/natriumfenylacetaat
	Quantities:
	Divisible:
	Dose:
	Solution:
	Substances:

		Name: natriumbenzoaat
		Quantities:
		Concentrations:
		Dose: natriumbenzoaat, [dun] mg, [qty-adj] 250 mg/kg/dosis
		Solution:

		Name: natriumfenylacetaat
		Quantities:
		Concentrations:
		Dose: natriumfenylacetaat, [dun] mg, [qty-adj] 250 mg/kg/dosis
		Solution:
"""
|> Medication.fromString
