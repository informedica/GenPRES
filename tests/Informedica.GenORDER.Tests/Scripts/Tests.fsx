#I __SOURCE_DIRECTORY__

#time

#load "load.fsx"


open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")


open MathNet.Numerics
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


// ============================================================
// Helper functions
// ============================================================

/// Run a medication text scenario through CalcMinMax and return the result.
/// This verifies the scenario can be parsed as a Medication and executed as
/// an Order without errors.
let runScenario (txt: string) =
    txt
    |> Medication.fromString
    |> function
        | Error errs -> Error errs
        | Ok med ->
            med
            |> Medication.toOrderDto System.DateTime.UtcNow
            |> Order.Dto.fromDto OrderLogging.noOp
            |> function
                | Error msg -> Error [ msg ]
                | Ok ord ->
                    ord
                    |> OrderCommand.CalcMinMax
                    |> OrderProcessor.processPipeline OrderLogging.noOp
                    |> Result.mapError (snd >> List.map string)


/// Create an Expecto test that verifies a medication text scenario can be
/// successfully parsed and executed as an executable order.
let createTest (name: string) (txt: string) =
    test name {
        match txt |> runScenario with
        | Ok _ -> ()
        | Error msgs ->
            let msg = msgs |> String.concat "; "
            failwith $"Scenario '{name}' failed: {msg}"
    }


// ============================================================
// Special medication scenario texts
// ============================================================

/// 1. Paracetamol suppository with product selection.
///
/// Demonstrates:
/// - DiscontinuousOrder with multiple product strengths (product selection)
/// - Weight-adjusted dosing (mg/kg/dosis)
/// - The system selects the appropriate product strength based on the
///   calculated dose and the patient weight (14 kg)
let pcmSuppText =
    """
Id: 047f9e19-4cfc-43cb-b7ee-f88f23d2eab6
Name: paracetamol
Quantity:
Quantities:
Route: RECTAAL
OrderType: DiscontinuousOrder
Adjust: 14 kg
Frequencies: 3;4 x/dag
Time:
Dose: 1 stuk/dosis
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
		Concentrations: 120;240;500;1 000;125;250;60;30;360;90;750;180 mg/stuk
		Dose: paracetamol, [dun] mg, [qty-adj] 10 mg/kg - 20 mg/kg/dosis
		Solution:
"""


/// 2. Cotrimoxazol (trimethoprim/sulfamethoxazole) with multi-component calculation.
///
/// Demonstrates:
/// - DiscontinuousOrder with two co-dosed substances in a fixed ratio
/// - QuantityAdjust for both sulfamethoxazol and trimethoprim
/// - The solver calculates doses for both substances simultaneously,
///   respecting the 5:1 ratio (sulfamethoxazol:trimethoprim) inherent
///   in the product concentration
let cotrimText =
    """
Id: cotrim-001
Name: cotrimoxazol
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 10 kg
Frequencies: 2 x/dag
Time:
Dose:
Div:
DoseCount: 1 x
Components:

	Name: cotrimoxazol
	Form: drank
	Quantities: 1 ml
	Divisible: 1
	Dose:
	Solution:
	Substances:

		Name: sulfamethoxazol
		Concentrations: 40;400;800 mg/ml
		Dose: sulfamethoxazol, [dun] mg, [qty-adj] 27 mg/kg - 30 mg/kg/dosis
		Solution:

		Name: trimethoprim
		Concentrations: 8;80;160 mg/ml
		Dose: trimethoprim, [dun] mg, [qty-adj] 5.4 mg/kg - 6 mg/kg/dosis
		Solution:
"""


/// 3. Gentamicin dosed every 36 hours — special time unit.
///
/// Demonstrates:
/// - DiscontinuousOrder with a non-standard dosing interval (36 hours)
/// - Frequency expressed as "1 x/36 hour", which the unit system handles
///   by representing 36 hours as a composite time unit
/// - Medication in solution (gentamicin diluted in glucose 10%)
/// - Concentration constraint on the prepared solution (1.2-2 mg/ml)
let gentamicin36hText =
    """
Id: genta-36h-001
Name: gentamicine
Quantity:
Quantities: 5;10;50;100 ml
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 11 kg
Frequencies: 1 x/36 hour
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: gentamicine
	Form: injectievloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: gentamicine
		Concentrations: 10;40 mg/ml
		Dose: gentamicine, [dun] mg, [per-time-adj] 7 mg/kg/day
		Solution:  [conc] 1.2 mg/ml - 2 mg/ml

	Name: gluc 10%
	Form: vloeistof
	Quantities: 1 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: koolhydraat
		Concentrations: 0.1 g/ml
		Dose:
		Solution:
"""


/// 4. Benzylpenicilline with international units — different dosing units
///    and readability of large numbers.
///
/// Demonstrates:
/// - DiscontinuousOrder with international units (IE = internationale eenheid)
/// - Dosing expressed in millions of units (miljIE), illustrating how the
///   system formats large numbers (e.g., 100 000 IE/ml concentration)
/// - The concentration is expressed per ml (IE/ml), while the dose per-time
///   limit is in miljIE/kg/dag, showing unit conversion across scales
let benzylpenicillineText =
    """
Id: benzylpen-001
Name: benzylpenicilline
Quantity:
Quantities:
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust: 10 kg
Frequencies: 4;6 x/dag
Time:
Dose: [dun] ml
Div:
DoseCount: 1 x
Components:

	Name: benzylpenicilline
	Form: poeder voor oplossing voor injectie
	Quantities: 10 ml
	Divisible: 10
	Dose:
	Solution:
	Substances:

		Name: benzylpenicilline
		Concentrations: 100 000 IE/ml
		Dose: benzylpenicilline, [dun] IE, [per-time-adj] 0.2 miljIE/kg/dag - 0.6 miljIE/kg/dag, [per-time] max 24 miljIE/dag
		Solution:
"""


/// 5. Continuous infusion — propofol.
///
/// Demonstrates:
/// - ContinuousOrder without a fixed dose time or frequency
/// - Rate-adjusted dosing in mg/kg/hr
/// - A fixed total volume (50 ml) for the prepared syringe
let continuousInfusionText =
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


/// 6. Medication reconstitution — vancomycin powder for infusion.
///
/// Demonstrates:
/// - TimedOrder where the active drug must be reconstituted (dissolved)
///   from a powder vial before dilution in a carrier fluid
/// - Solution concentration constraint (2.5-10 mg/ml)
/// - Rate limit (max 1.7 mg/min) alongside per-time and per-time-adjusted
///   dose limits — multiple simultaneous dose constraints
/// - Carrier fluid (glucose 5%) as a second component
let vancomycinReconstitutionText =
    """
Id: 13e4f4e5-059d-47d6-8882-46cc2ed0f072
Name: vancomycine
Quantity:
Quantities: 50;100;250 ml
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust: 14.5 kg
Frequencies: 3;4 x/dag
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
		Dose: vancomycine, [dun] mg, [rate] max 1.7 mg/min, [per-time-adj] 60 mg/kg/dag, [per-time] max 4000 mg/dag
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


/// 7. Medication in solution — noradrenaline continuous infusion.
///
/// Demonstrates:
/// - ContinuousOrder where the active drug is diluted in a carrier fluid
/// - Solution concentration constraint for the active substance (max 1 mg/ml)
/// - Rate-adjusted dosing in microg/kg/min
/// - A carrier fluid (glucose 10%) as a second component contributing to
///   total infusion volume
let noradrenalineInSolutionText =
    """
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


// ============================================================
// Test suite
// ============================================================

let tests =
    testList
        "Special medication scenarios"
        [

            createTest "paracetamol supp — product selection from multiple strengths" pcmSuppText

            createTest "cotrimoxazol — trimethoprim/sulfamethoxazole multi-component calculation" cotrimText

            createTest "gentamicine — 1/36 hour dosing interval with special time unit" gentamicin36hText

            createTest
                "benzylpenicilline — international unit dosing (IE/miljIE) and large number readability"
                benzylpenicillineText

            createTest "propofol — continuous infusion with rate-adjusted dosing" continuousInfusionText

            createTest
                "vancomycine — medication reconstitution from powder with concentration constraint"
                vancomycinReconstitutionText

            createTest "noradrenaline — continuous infusion in solution with carrier fluid" noradrenalineInSolutionText

        ]


tests |> runTestsWithCLIArgs [ CLIArguments.Sequenced ] [||] |> ignore
