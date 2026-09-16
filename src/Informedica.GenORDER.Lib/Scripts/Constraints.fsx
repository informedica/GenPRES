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

open Expecto
open Expecto.Flip


module HelperFunctions =

    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib


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


MedicationTexts.continuousMultipleComponent
|> Medication.fromString
|> function
    | Error _ -> "fail" |> failwith
    | Ok med -> [ CalcMinMax ] |> HelperFunctions.run None med
|> Result.map (fun ord ->
    ord
    |> Order.Dto.toDto
    |> Order.Dto.fromDto OrderLogging.noOp
    |> function
        | Error _ -> "fail" |> failwith
        | Ok ord ->
            ord |> Order.printTable ConsoleTables.Format.Minimal

            ord |> Ok
)
|> ignore
