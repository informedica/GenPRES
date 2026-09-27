module MedicationTextTests

open Informedica.Utils.Lib.BCL
open Expecto
open Expecto.Flip

open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


/// Medication texts in the Medication.toString format, one per order type and component layout,
/// used to check that a parsed text builds an order that can be solved.
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


    /// Gentamicin dosed every 36 hours — special time unit.
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


    /// Benzylpenicilline with international units — different dosing units
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


    /// Continuous infusion — propofol.
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


    /// Medication reconstitution — vancomycin powder for infusion.
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


    /// Medication in solution — noradrenaline continuous infusion.
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


    /// Breast milk with a fortifier powder mixed in, fed as a discontinuous order.
    let feedingWithPowder =
        """
Id: 4f6671d8-30f2-4bcc-a9a3-76bb931f15e5
Name: mm met bmf
Quantity:
Quantities:
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust: 4 kg
Frequencies: 3;4;5;6;7;8;12 x/day
Time:
Dose: [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
Div:
DoseCount: 1 x
Components:

	Name: MM
	Form: voeding
	Quantities: 1 ml
	Divisible: 10
	Dose: MM, [dun] ml, [per-time-adj] 10 ml/kg/day - 150 ml/kg/day, [per-time] max 2500 ml/day, [qty-adj] max 20 ml/kg/dosis, [qty] max 1000 ml/dosis
	Solution:
	Substances:

		Name: energie
		Concentrations: 0.68 kCal/ml
		Dose:
		Solution:

		Name: eiwit
		Concentrations: 0.01 g/ml
		Dose:
		Solution:

	Name: Nutrilon Nenatal BMF pdr
	Form: voeding
	Quantities: 1 g
	Divisible: 10
	Dose: Nutrilon Nenatal BMF pdr, [dun] g, [per-time-adj] 0.1 g/kg/day - 0.3 g/kg/day
	Solution:
	Substances:

		Name: energie
		Concentrations: 3.47 kCal/g
		Dose:
		Solution:

		Name: eiwit
		Concentrations: 0.252 x
		Dose:
		Solution:
"""


/// Tests of the field labels, the unit checks and the text round trip of a Medication.
module MedicationTextFormatTests =

    let private mgPerKg = Units.Mass.milliGram |> ValueUnit.per Units.Weight.kiloGram


    let private mgPerKgPerDay = mgPerKg |> ValueUnit.per Units.Time.day


    let private microgPerKgPerHour =
        Units.Mass.microGram
        |> ValueUnit.per Units.Weight.kiloGram
        |> ValueUnit.per Units.Time.hour


    let private range u min max =
        MinMax.createInclIncl (min |> ValueUnit.singleWithUnit u) (max |> ValueUnit.singleWithUnit u)


    let private doseLimitText dl = dl |> DoseLimit.toString |> String.concat ""


    let private toText med = med |> Medication.toString |> String.concat "\n"


    let private parse text =
        match text |> Medication.fromString with
        | Ok med -> med
        | Error errs ->
            let errMsg = errs |> String.concat "; "
            failtest $"Parse failed: %s{errMsg}"


    let private substance name (med: Medication) =
        med.Components
        |> List.head
        |> _.Substances
        |> List.find (fun s -> s.Name = name)


    let tests =
        testList
            "Medication text format"
            [
                testList
                    "DoseLimit with field labels"
                    [
                        test "Quantity field gets [qty] label" {
                            { DoseLimit.limit with
                                Quantity = 10N |> ValueUnit.singleWithUnit Units.Volume.milliLiter |> MinMax.createExact
                            }
                            |> doseLimitText
                            |> Expect.stringContains "should contain [qty]" "[qty]"
                        }

                        test "QuantityAdjust field gets [qty-adj] label" {
                            { DoseLimit.limit with QuantityAdjust = range mgPerKg 10N 20N }
                            |> doseLimitText
                            |> Expect.stringContains "should contain [qty-adj]" "[qty-adj]"
                        }

                        test "PerTimeAdjust field gets [per-time-adj] label" {
                            { DoseLimit.limit with PerTimeAdjust = range mgPerKgPerDay 10N 20N }
                            |> doseLimitText
                            |> Expect.stringContains "should contain [per-time-adj]" "[per-time-adj]"
                        }

                        test "RateAdjust field gets [rate-adj] label" {
                            { DoseLimit.limit with RateAdjust = range microgPerKgPerHour 10N 40N }
                            |> doseLimitText
                            |> Expect.stringContains "should contain [rate-adj]" "[rate-adj]"
                        }

                        test "a QuantityAdjust is labelled [qty-adj] and not [per-time-adj]" {
                            let text =
                                { DoseLimit.limit with
                                    DoseLimitTarget = "test" |> SubstanceLimitTarget
                                    QuantityAdjust = range mgPerKg 10N 20N
                                }
                                |> doseLimitText

                            text |> Expect.stringContains "should have [qty-adj] label" "[qty-adj]"

                            text.Contains("[per-time-adj]")
                            |> Expect.isFalse "should not have [per-time-adj] label"
                        }
                    ]

                testList
                    "Unit validation"
                    [
                        test "hasAdjustUnit detects kg" {
                            mgPerKg
                            |> Medication.UnitValidation.hasAdjustUnit
                            |> Expect.isTrue "should detect kg as adjust unit"
                        }

                        test "hasAdjustUnit detects m2" {
                            Units.Mass.milliGram
                            |> ValueUnit.per Units.BSA.m2
                            |> Medication.UnitValidation.hasAdjustUnit
                            |> Expect.isTrue "should detect m2 as adjust unit"
                        }

                        test "hasTimeUnit detects day" {
                            Units.Mass.milliGram
                            |> ValueUnit.per Units.Time.day
                            |> Medication.UnitValidation.hasTimeUnit
                            |> Expect.isTrue "should detect day as time unit"
                        }

                        test "hasTimeUnit detects hour" {
                            Units.Volume.milliLiter
                            |> ValueUnit.per Units.Time.hour
                            |> Medication.UnitValidation.hasTimeUnit
                            |> Expect.isTrue "should detect hour as time unit"
                        }

                        test "complex unit mg/kg/dag has both adjust and time" {
                            mgPerKgPerDay
                            |> Medication.UnitValidation.hasAdjustUnit
                            |> Expect.isTrue "should have adjust unit"

                            mgPerKgPerDay
                            |> Medication.UnitValidation.hasTimeUnit
                            |> Expect.isTrue "should have time unit"
                        }
                    ]

                testList
                    "toString and fromString round trip"
                    [
                        test "pcmSupp round trip keeps the basic fields" {
                            let original = Scenarios.pcmSupp
                            let med = original |> toText |> parse

                            med.Id |> Expect.equal "Id" original.Id
                            med.Name |> Expect.equal "Name" original.Name
                            med.Route |> Expect.equal "Route" original.Route
                            med.OrderType |> Expect.equal "OrderType" original.OrderType

                            med.Components.Length
                            |> Expect.equal "Components count" original.Components.Length
                        }

                        test "pcmSupp round trip keeps the component details" {
                            let original = Scenarios.pcmSupp
                            let origCmp = original.Components |> List.head
                            let parsedCmp = original |> toText |> parse |> _.Components |> List.head

                            parsedCmp.Name |> Expect.equal "Component Name" origCmp.Name
                            parsedCmp.Form |> Expect.equal "Component Form" origCmp.Form

                            parsedCmp.Substances.Length
                            |> Expect.equal "Substances count" origCmp.Substances.Length
                        }

                        test "pcmSupp round trip gives back the same text" {
                            let text = Scenarios.pcmSupp |> toText

                            text |> parse |> toText |> Expect.equal "should resemble the original text" text
                        }

                        test "amfo round trip keeps the PerTimeAdjust field" {
                            let original = Scenarios.amfo
                            let med = original |> toText |> parse

                            med.Id |> Expect.equal "Id" original.Id
                            med.Name |> Expect.equal "Name" original.Name
                            med.OrderType |> Expect.equal "OrderType" original.OrderType

                            let parsed = med |> substance "amfotericine b liposomaal"
                            parsed.Dose.IsSome |> Expect.isTrue "Dose should be Some"

                            parsed.Dose.Value.PerTimeAdjust
                            |> Expect.notEqual "PerTimeAdjust should not be empty" MinMax.empty
                        }

                        test "morfCont round trip keeps the RateAdjust field" {
                            let original = Scenarios.morfCont
                            let med = original |> toText |> parse

                            med.Id |> Expect.equal "Id" original.Id
                            med.OrderType |> Expect.equal "OrderType" original.OrderType

                            let parsed = med |> substance "morfin"
                            parsed.Dose.IsSome |> Expect.isTrue "Dose should be Some"

                            parsed.Dose.Value.RateAdjust
                            |> Expect.notEqual "RateAdjust should not be empty" MinMax.empty
                        }

                        test "cotrim round trip keeps the QuantityAdjust field" {
                            let original = Scenarios.cotrim
                            let med = original |> toText |> parse

                            med.Id |> Expect.equal "Id" original.Id
                            med.OrderType |> Expect.equal "OrderType" original.OrderType

                            for sub in med.Components |> List.head |> _.Substances do
                                sub.Dose.IsSome |> Expect.isTrue $"Dose for %s{sub.Name} should be Some"

                                sub.Dose.Value.QuantityAdjust
                                |> Expect.notEqual $"QuantityAdjust for %s{sub.Name} should not be empty" MinMax.empty
                        }

                        test "tpn round trip keeps every component" {
                            let original = Scenarios.tpn
                            let med = original |> toText |> parse

                            med.Id |> Expect.equal "Id" original.Id

                            med.Components.Length
                            |> Expect.equal "Components count" original.Components.Length

                            for i, origCmp in original.Components |> List.indexed do
                                let parsedCmp = med.Components[i]
                                parsedCmp.Name |> Expect.equal $"Component %i{i} name" origCmp.Name

                                if origCmp.Dose.IsSome then
                                    parsedCmp.Dose.IsSome |> Expect.isTrue $"Component %i{i} Dose should be Some"
                        }

                        test "fullMedication round trip keeps all fields" {
                            let original = Scenarios.fullMedication
                            let med = original |> toText |> parse

                            med.Id |> Expect.equal "Id" original.Id
                            med.Name |> Expect.equal "Name" original.Name
                            med.Route |> Expect.equal "Route" original.Route
                            med.OrderType |> Expect.equal "OrderType" original.OrderType

                            med.Components.Length
                            |> Expect.equal "Components count" original.Components.Length

                            med.Div.IsSome |> Expect.equal "Div is Some" original.Div.IsSome
                        }

                        test "fromString returns an error for an invalid OrderType" {
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
                            | Ok _ -> failtest "Expected error for invalid OrderType"
                        }
                    ]

                testList
                    "parseLine handles different indentation"
                    [
                        test "parseLine handles tab indentation" {
                            match Medication.Parser.parseLine "\t\tName: test" with
                            | Some(indent, key, value) ->
                                indent |> Expect.equal "should be indent 2" 2
                                key |> Expect.equal "key" "Name"
                                value |> Expect.equal "value" "test"
                            | None -> failtest "Expected successful parse"
                        }

                        test "parseLine handles space indentation (4 spaces = 1 indent)" {
                            match Medication.Parser.parseLine "        Name: test" with
                            | Some(indent, key, value) ->
                                indent |> Expect.equal "should be indent 2" 2
                                key |> Expect.equal "key" "Name"
                                value |> Expect.equal "value" "test"
                            | None -> failtest "Expected successful parse"
                        }

                        test "fromString works with space-indented input" {
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

                            let med = spaceIndented |> parse
                            med.Components.Length |> Expect.equal "should have 1 component" 1
                            let comp = med.Components |> List.head
                            comp.Name |> Expect.equal "component name" "comp1"
                            comp.Substances.Length |> Expect.equal "should have 1 substance" 1
                        }
                    ]
            ]


/// Each medication text parses, builds an order and solves it for its minimum and maximum values.
module MedicationTextScenarioTests =

    let private calcMinMax text =
        text
        |> Medication.fromString
        |> Result.mapError (String.concat "; ")
        |> Result.bind (fun med ->
            med
            |> Medication.toOrderDto Scenarios.testStart
            |> Order.Dto.fromDto
            |> Result.mapError (fun e -> $"%A{e}")
        )
        |> Result.bind (fun ord ->
            ord
            |> OrderCommand.CalcMinMax
            |> OrderProcessor.processPipeline OrderLogging.noOp
            |> Result.mapError (fun (_, msgs) -> $"%A{msgs}")
        )


    let tests =
        testList
            "Medication text scenarios"
            [
                for name, text in
                    [
                        "once, one component, several items, no dose",
                        MedicationTexts.onceSingleComponentMultipleItemsNoDose
                        "once, one component, one item", MedicationTexts.onceSingleComponentSingleItem
                        "once timed, one component, one item", MedicationTexts.onceTimedSingleComponentSingleItem
                        "discontinuous, one component, one item", MedicationTexts.discontinuousSingleComponentSingleItem
                        "timed, one component, one item", MedicationTexts.timedSingleComponentSingleItem
                        "continuous, one component, one item", MedicationTexts.continuousSingleComponentSingleItem
                        "continuous, several components", MedicationTexts.continuousMultipleComponent
                        "timed, several components with a component dose",
                        MedicationTexts.timedMultipleComponentsDoseComponent
                        "paracetamol drink", MedicationTexts.pcmDrink
                        "vancomycin reconstitution", MedicationTexts.vancoReconst
                        "discontinuous, several components and items",
                        MedicationTexts.discontinousMultipleComponentMultipleItems
                        "tpn with a maximum quantity", MedicationTexts.tpnWithMaxQuantity
                        "gentamicin every 36 hours in solution", MedicationTexts.gentamicin36hText
                        "propofol continuous infusion", MedicationTexts.continuousInfusionText
                        "vancomycin powder reconstituted for infusion", MedicationTexts.vancomycinReconstitutionText
                        "noradrenaline in solution", MedicationTexts.noradrenalineInSolutionText
                    ] do
                    test $"%s{name} can be solved for min and max" {
                        match text |> calcMinMax with
                        | Ok _ -> ()
                        | Error e -> failtest $"should be able to run %s{name}: %s{e}"
                    }

                // Pending: both stop with a DivideByZeroException in a unit conversion while solving.
                for name, text in
                    [
                        "benzylpenicillin in international units", MedicationTexts.benzylpenicillineText
                        "breast milk with fortifier powder", MedicationTexts.feedingWithPowder
                    ] do
                    ptest $"%s{name} can be solved for min and max" {
                        match text |> calcMinMax with
                        | Ok _ -> ()
                        | Error e -> failtest $"should be able to run %s{name}: %s{e}"
                    }
            ]
