module MedicationTextTests

open Informedica.Utils.Lib.BCL
open Expecto
open Expecto.Flip

open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib


/// Medication texts in the MedicationText format, one per order type and component layout,
/// used to check that a parsed text builds an order that can be solved.
module MedicationTexts =


    let onceSingleComponentMultipleItemsNoDose =
        """
Id: 3beb2d76-625c-4e02-8c49-bcd5fa6f5166
Name: chloorhexidine
Route: CUTAAN
OrderType: OnceOrder
Adjust:
  Value:
    - "11"
  Unit: kg
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: chloorhexidine
    Form: oplossing voor cutaan gebruik
    Quantities:
      Value:
        - "1"
        - "100"
        - "50"
      Unit: ml
    Divisible: "1"
    Substances:
      - Name: ethanol, gedenatureerd
        Concentrations:
          Value:
            - "539"
          Unit: mg/ml
      - Name: chloorhexidine
        Concentrations:
          Value:
            - "5"
            - "10"
            - "40"
          Unit: mg/ml
"""


    // Once single component single item scenario
    let onceSingleComponentSingleItem =
        """
Id: 93e8c175-99a1-48d8-b2f4-90005fdb8ada
Name: paracetamol
Route: RECTAAL
OrderType: OnceOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Dose:
  Quantity:
    Min:
      Value:
        - "1"
      Unit: stuk
      Inclusive: true
    Max:
      Value:
        - "1"
      Unit: stuk
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: paracetamol
    Form: zetpil
    Quantities:
      Value:
        - "1"
      Unit: stuk
    Divisible: "1"
    Substances:
      - Name: paracetamol
        Concentrations:
          Value:
            - "120"
            - "240"
            - "500"
            - "1000"
            - "125"
            - "250"
            - "60"
            - "30"
            - "360"
            - "90"
            - "750"
            - "180"
          Unit: mg/stuk
        Dose:
          DoseUnit: mg
          Quantity:
            Max:
              Value:
                - "1000"
              Unit: mg
              Inclusive: true
          QuantityAdjust:
            Min:
              Value:
                - "40"
              Unit: mg/kg
              Inclusive: true
            Max:
              Value:
                - "40"
              Unit: mg/kg
              Inclusive: true
"""

    // OnceTimed single component single item scenario
    let onceTimedSingleComponentSingleItem =
        """
Id: 95c44266-84c5-4969-a815-9fbf2c9ed693
Name: paracetamol
Route: INTRAVENEUS
OrderType: OnceTimedOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Time:
  Min:
    Value:
      - "15"
    Unit: min
    Inclusive: true
  Max:
    Value:
      - "20"
    Unit: min
    Inclusive: true
Dose:
  Quantity:
    Max:
      Value:
        - "1000"
      Unit: ml
      Inclusive: true
  QuantityAdjust:
    Max:
      Value:
        - "20"
      Unit: ml/kg
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: paracetamol
    Form: infusievloeistof
    Quantities:
      Value:
        - "100"
        - "50"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: paracetamol
        Concentrations:
          Value:
            - "10"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          Quantity:
            Max:
              Value:
                - "1000"
              Unit: mg
              Inclusive: true
          QuantityAdjust:
            Min:
              Value:
                - "20"
              Unit: mg/kg
              Inclusive: true
            Max:
              Value:
                - "20"
              Unit: mg/kg
              Inclusive: true
"""

    // Discontinuous single component single item scenario
    let discontinuousSingleComponentSingleItem =
        """
Id: d595fdbd-51ae-489d-a316-a458b7d5d032
Name: paracetamol
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust:
  Value:
    - "40"
  Unit: kg
Frequencies:
  Value:
    - "1"
    - "2"
    - "3"
    - "4"
  Unit: x/day
Dose:
  Quantity:
    Max:
      Value:
        - "10"
      Unit: stuk
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: paracetamol
    Form: tablet
    Quantities:
      Value:
        - "1"
      Unit: stuk
    Divisible: "4"
    Substances:
      - Name: natriumwaterstofcarbonaat
        Concentrations:
          Value:
            - "632"
          Unit: mg/stuk
      - Name: paracetamol
        Concentrations:
          Value:
            - "500"
            - "1000"
          Unit: mg/stuk
        Dose:
          DoseUnit: mg
          QuantityAdjust:
            Min:
              Value:
                - "10"
              Unit: mg/kg
              Inclusive: true
            Max:
              Value:
                - "15"
              Unit: mg/kg
              Inclusive: true
          PerTime:
            Max:
              Value:
                - "4000"
              Unit: mg/day
              Inclusive: true
"""

    let discontinousMultipleComponentMultipleItems =
        """
Id: d1326abe-ca06-4c59-a52c-7af1152b75c4
Name: amoxicilline/clavulaanzuur
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Frequencies:
  Value:
    - "3"
  Unit: x/day
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: amoxicilline/clavulaanzuur
    Form: poeder voor oplossing voor infusie
    Quantities:
      Value:
        - "20"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: amoxicilline
        Concentrations:
          Value:
            - "100"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          PerTime:
            Max:
              Value:
                - "6000"
              Unit: mg/day
              Inclusive: true
          PerTimeAdjust:
            Min:
              Value:
                - "100"
              Unit: mg/kg/day
              Inclusive: true
            Max:
              Value:
                - "100"
              Unit: mg/kg/day
              Inclusive: true
      - Name: clavulaanzuur
        Concentrations:
          Value:
            - "10"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          PerTime:
            Max:
              Value:
                - "600"
              Unit: mg/day
              Inclusive: true
          PerTimeAdjust:
            Min:
              Value:
                - "10"
              Unit: mg/kg/day
              Inclusive: true
            Max:
              Value:
                - "10"
              Unit: mg/kg/day
              Inclusive: true
"""

    // Timed single component single item scenario
    let timedSingleComponentSingleItem =
        """
Id: a9e18942-f879-4df1-bc21-6375c3291ed7
Name: paracetamol
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Frequencies:
  Value:
    - "4"
  Unit: x/day
Time:
  Min:
    Value:
      - "15"
    Unit: min
    Inclusive: true
  Max:
    Value:
      - "20"
    Unit: min
    Inclusive: true
Dose:
  Quantity:
    Max:
      Value:
        - "1000"
      Unit: ml
      Inclusive: true
  QuantityAdjust:
    Max:
      Value:
        - "20"
      Unit: ml/kg
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: paracetamol
    Form: infusievloeistof
    Quantities:
      Value:
        - "100"
        - "50"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: paracetamol
        Concentrations:
          Value:
            - "10"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          Quantity:
            Max:
              Value:
                - "1000"
              Unit: mg
              Inclusive: true
          PerTime:
            Max:
              Value:
                - "4000"
              Unit: mg/day
              Inclusive: true
          PerTimeAdjust:
            Min:
              Value:
                - "60"
              Unit: mg/kg/day
              Inclusive: true
            Max:
              Value:
                - "60"
              Unit: mg/kg/day
              Inclusive: true
"""

    let continuousSingleComponentSingleItem =
        """
Id: 6854e269-df1c-480f-ac58-a08fe108a59d
Name: propofol
Route: INTRAVENEUS
OrderType: ContinuousOrder
Quantities:
  Value:
    - "50"
  Unit: ml
Adjust:
  Value:
    - "40"
  Unit: kg
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: propofol
    Form: emulsie voor injectie
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: propofol
        Concentrations:
          Value:
            - "20"
            - "10"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          RateAdjust:
            Min:
              Value:
                - "1"
              Unit: mg/kg/hr
              Inclusive: true
            Max:
              Value:
                - "4"
              Unit: mg/kg/hr
              Inclusive: true
"""

    let continuousMultipleComponent =
        """
Id: b5189d1a-c1c5-4223-9b2d-e8e35e1b22fd
Name: noradrenaline
Route: INTRAVENEUS
OrderType: ContinuousOrder
Quantities:
  Value:
    - "50"
  Unit: ml
Adjust:
  Value:
    - "40"
  Unit: kg
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: noradrenaline
    Form: concentraat voor oplossing voor infusie
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: noradrenaline
        Concentrations:
          Value:
            - "1"
          Unit: mg/ml
        Dose:
          DoseUnit: microg
          RateAdjust:
            Min:
              Value:
                - "0.05"
              Unit: microg/kg/min
              Inclusive: true
            Max:
              Value:
                - "2"
              Unit: microg/kg/min
              Inclusive: true
        Solution:
          Quantity:
            Min:
              Value:
                - "5"
              Unit: mg
              Inclusive: true
            Max:
              Value:
                - "5"
              Unit: mg
              Inclusive: true
          Concentration:
            Max:
              Value:
                - "1"
              Unit: mg/ml
              Inclusive: true
  - Name: gluc 10%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.4"
          Unit: kCal/ml
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.1"
          Unit: g/ml
"""


    let timedMultipleComponentsDoseComponent =
        """
Id: a16b1489-d1c3-4f1e-a0ae-e83b18e1ebd5
Name: samenstelling c
Route: INTRAVENEUS
OrderType: TimedOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Frequencies:
  Value:
    - "1"
  Unit: x/day
Time:
  Min:
    Value:
      - "20"
    Unit: hr
    Inclusive: true
  Max:
    Value:
      - "24"
    Unit: hr
    Inclusive: true
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: Samenstelling C
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      QuantityAdjust:
        Min:
          Value:
            - "10"
          Unit: ml/kg
          Inclusive: true
        Max:
          Value:
            - "10"
          Unit: ml/kg
          Inclusive: true
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.32"
          Unit: kCal/ml
      - Name: eiwit
        Concentrations:
          Value:
            - "0.08"
          Unit: g/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.05"
              Unit: g/ml
              Inclusive: true
      - Name: natrium
        Concentrations:
          Value:
            - "0.008"
          Unit: mmol/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.5"
              Unit: mmol/ml
              Inclusive: true
      - Name: kalium
        Concentrations:
          Value:
            - "0.02"
          Unit: mmol/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.5"
              Unit: mmol/ml
              Inclusive: true
  - Name: NaCl 3%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      QuantityAdjust:
        Min:
          Value:
            - "6"
          Unit: ml/kg
          Inclusive: true
        Max:
          Value:
            - "6"
          Unit: ml/kg
          Inclusive: true
    Substances:
      - Name: natrium
        Concentrations:
          Value:
            - "0.5"
          Unit: mmol/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.5"
              Unit: mmol/ml
              Inclusive: true
  - Name: KCl 7,4%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      QuantityAdjust:
        Min:
          Value:
            - "2"
          Unit: ml/kg
          Inclusive: true
        Max:
          Value:
            - "2"
          Unit: ml/kg
          Inclusive: true
    Substances:
      - Name: kalium
        Concentrations:
          Value:
            - "1"
          Unit: mmol/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.5"
              Unit: mmol/ml
              Inclusive: true
  - Name: gluc 10%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      QuantityAdjust:
        Min:
          Value:
            - "65"
          Unit: ml/kg
          Inclusive: true
        Max:
          Value:
            - "80"
          Unit: ml/kg
          Inclusive: true
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.4"
          Unit: kCal/ml
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.1"
          Unit: g/ml
"""

    let pcmDrink =
        """
Id: 34b81d68-86e5-4ec6-a223-cba9d2837530
Name: paracetamol
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust:
  Value:
    - "17"
  Unit: kg
Frequencies:
  Value:
    - "1"
    - "2"
    - "3"
    - "4"
  Unit: x/day
Dose:
  Quantity:
    Max:
      Value:
        - "500"
      Unit: ml
      Inclusive: true
  QuantityAdjust:
    Max:
      Value:
        - "10"
      Unit: ml/kg
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: paracetamol
    Form: drank
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: benzylalcohol
        Concentrations:
          Value:
            - "0.48"
          Unit: mg/ml
      - Name: propyleenglycol
        Concentrations:
          Value:
            - "5.44"
          Unit: mg/ml
      - Name: paracetamol
        Concentrations:
          Value:
            - "24"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          QuantityAdjust:
            Min:
              Value:
                - "10"
              Unit: mg/kg
              Inclusive: true
            Max:
              Value:
                - "15"
              Unit: mg/kg
              Inclusive: true
          PerTime:
            Max:
              Value:
                - "4000"
              Unit: mg/day
              Inclusive: true
"""


    let vancoReconst =
        """
Id: 13e4f4e5-059d-47d6-8882-46cc2ed0f072
Name: vancomycine
Route: INTRAVENEUS
OrderType: TimedOrder
Quantities:
  Value:
    - "50"
    - "100"
    - "250"
  Unit: ml
Adjust:
  Value:
    - "14.5"
  Unit: kg
Frequencies:
  Value:
    - "3"
    - "4"
  Unit: x/day
Time:
  Min:
    Value:
      - "60"
    Unit: min
    Inclusive: true
  Max:
    Value:
      - "180"
    Unit: min
    Inclusive: true
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: vancomycine
    Form: poeder voor oplossing voor infusie
    Quantities:
      Value:
        - "20"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: vancomycine
        Concentrations:
          Value:
            - "50"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          PerTime:
            Max:
              Value:
                - "4000"
              Unit: mg/day
              Inclusive: true
          PerTimeAdjust:
            Min:
              Value:
                - "60"
              Unit: mg/kg/day
              Inclusive: true
            Max:
              Value:
                - "60"
              Unit: mg/kg/day
              Inclusive: true
          Rate:
            Max:
              Value:
                - "1.7"
              Unit: mg/min
              Inclusive: true
        Solution:
          Concentration:
            Min:
              Value:
                - "2.5"
              Unit: mg/ml
              Inclusive: true
            Max:
              Value:
                - "10"
              Unit: mg/ml
              Inclusive: true
  - Name: gluc 5%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.2"
          Unit: kCal/ml
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.05"
          Unit: g/ml
"""

    /// This scenario has a variable glucose content which can result in
    /// an inappropriate low volume for the max protein concentration. This
    /// happens when recalculating all possible values. The solution is to
    /// treat the glucose component as a rest volume, i.e., only calculated.
    let tpnWithMaxQuantity =
        """
Id: 81607677-b226-4854-afd9-90faba665cc3
Name: samenstelling c
Route: INTRAVENEUS
OrderType: TimedOrder
Quantity:
  Max:
    Value:
      - "830.5"
    Unit: ml
    Inclusive: true
Adjust:
  Value:
    - "11"
  Unit: kg
Frequencies:
  Value:
    - "1"
  Unit: x/day
Time:
  Min:
    Value:
      - "20"
    Unit: hr
    Inclusive: true
  Max:
    Value:
      - "24"
    Unit: hr
    Inclusive: true
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: Samenstelling C
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      QuantityAdjust:
        Min:
          Value:
            - "10"
          Unit: ml/kg
          Inclusive: true
        Max:
          Value:
            - "25"
          Unit: ml/kg
          Inclusive: true
    Substances:
      - Name: eiwit
        Concentrations:
          Value:
            - "0.08"
          Unit: g/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.05"
              Unit: g/ml
              Inclusive: true
  - Name: NaCl 3%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      QuantityAdjust:
        Min:
          Value:
            - "6"
          Unit: ml/kg
          Inclusive: true
        Max:
          Value:
            - "6"
          Unit: ml/kg
          Inclusive: true
    Substances:
      - Name: natrium
        Concentrations:
          Value:
            - "0.5"
          Unit: mmol/ml
        Solution:
          Concentration:
            Max:
              Value:
                - "0.5"
              Unit: mmol/ml
              Inclusive: true
  - Name: gluc 10%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.1"
          Unit: g/ml
"""

    /// test case for not solved component-orderable count
    let adenosinDayOne =
        """
Id: 09be3945-a983-4209-88d5-a80006f57cd5
Name: adenosine
Route: INTRAVENEUS
OrderType: OnceOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Dose:
  Quantity:
    Max:
      Value:
        - "1000"
      Unit: ml
      Inclusive: true
  QuantityAdjust:
    Max:
      Value:
        - "20"
      Unit: ml/kg
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: adenosine
    Form: infusievloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: adenosine
        Concentrations:
          Value:
            - "3"
            - "2"
            - "5"
          Unit: mg/ml
        Dose:
          DoseUnit: microg
          Quantity:
            Max:
              Value:
                - "6000"
              Unit: microg
              Inclusive: true
          QuantityAdjust:
            Min:
              Value:
                - "100"
              Unit: microg/kg
              Inclusive: true
            Max:
              Value:
                - "100"
              Unit: microg/kg
              Inclusive: true
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
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Quantities:
  Value:
    - "5"
    - "10"
    - "50"
    - "100"
  Unit: ml
Adjust:
  Value:
    - "11"
  Unit: kg
Frequencies:
  Value:
    - "1"
  Unit: x/36 hr
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: gentamicine
    Form: injectievloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: gentamicine
        Concentrations:
          Value:
            - "10"
            - "40"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          PerTimeAdjust:
            Min:
              Value:
                - "7"
              Unit: mg/kg/day
              Inclusive: true
            Max:
              Value:
                - "7"
              Unit: mg/kg/day
              Inclusive: true
        Solution:
          Concentration:
            Min:
              Value:
                - "1.2"
              Unit: mg/ml
              Inclusive: true
            Max:
              Value:
                - "2"
              Unit: mg/ml
              Inclusive: true
  - Name: gluc 10%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.1"
          Unit: g/ml
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
Route: INTRAVENEUS
OrderType: DiscontinuousOrder
Adjust:
  Value:
    - "10"
  Unit: kg
Frequencies:
  Value:
    - "4"
    - "6"
  Unit: x/day
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: benzylpenicilline
    Form: poeder voor oplossing voor injectie
    Quantities:
      Value:
        - "10"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: benzylpenicilline
        Concentrations:
          Value:
            - "100"
          Unit: 0 IU/ml
        Dose:
          DoseUnit: IU
          PerTime:
            Max:
              Value:
                - "24"
              Unit: miljIU/day
              Inclusive: true
          PerTimeAdjust:
            Min:
              Value:
                - "0.2"
              Unit: miljIU/kg/day
              Inclusive: true
            Max:
              Value:
                - "0.6"
              Unit: miljIU/kg/day
              Inclusive: true
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
Route: INTRAVENEUS
OrderType: ContinuousOrder
Quantities:
  Value:
    - "50"
  Unit: ml
Adjust:
  Value:
    - "40"
  Unit: kg
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: propofol
    Form: emulsie voor injectie
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: propofol
        Concentrations:
          Value:
            - "20"
            - "10"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          RateAdjust:
            Min:
              Value:
                - "1"
              Unit: mg/kg/hr
              Inclusive: true
            Max:
              Value:
                - "4"
              Unit: mg/kg/hr
              Inclusive: true
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
Route: INTRAVENEUS
OrderType: TimedOrder
Quantities:
  Value:
    - "50"
    - "100"
    - "250"
  Unit: ml
Adjust:
  Value:
    - "14.5"
  Unit: kg
Frequencies:
  Value:
    - "3"
    - "4"
  Unit: x/day
Time:
  Min:
    Value:
      - "60"
    Unit: min
    Inclusive: true
  Max:
    Value:
      - "180"
    Unit: min
    Inclusive: true
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: vancomycine
    Form: poeder voor oplossing voor infusie
    Quantities:
      Value:
        - "20"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: vancomycine
        Concentrations:
          Value:
            - "50"
          Unit: mg/ml
        Dose:
          DoseUnit: mg
          PerTime:
            Max:
              Value:
                - "4000"
              Unit: mg/day
              Inclusive: true
          PerTimeAdjust:
            Min:
              Value:
                - "60"
              Unit: mg/kg/day
              Inclusive: true
            Max:
              Value:
                - "60"
              Unit: mg/kg/day
              Inclusive: true
          Rate:
            Max:
              Value:
                - "1.7"
              Unit: mg/min
              Inclusive: true
        Solution:
          Concentration:
            Min:
              Value:
                - "2.5"
              Unit: mg/ml
              Inclusive: true
            Max:
              Value:
                - "10"
              Unit: mg/ml
              Inclusive: true
  - Name: gluc 5%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.2"
          Unit: kCal/ml
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.05"
          Unit: g/ml
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
Route: INTRAVENEUS
OrderType: ContinuousOrder
Quantities:
  Value:
    - "50"
  Unit: ml
Adjust:
  Value:
    - "40"
  Unit: kg
Dose:
  DoseUnit: ml
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: noradrenaline
    Form: concentraat voor oplossing voor infusie
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: noradrenaline
        Concentrations:
          Value:
            - "1"
          Unit: mg/ml
        Dose:
          DoseUnit: microg
          RateAdjust:
            Min:
              Value:
                - "0.05"
              Unit: microg/kg/min
              Inclusive: true
            Max:
              Value:
                - "2"
              Unit: microg/kg/min
              Inclusive: true
        Solution:
          Quantity:
            Min:
              Value:
                - "5"
              Unit: mg
              Inclusive: true
            Max:
              Value:
                - "5"
              Unit: mg
              Inclusive: true
          Concentration:
            Max:
              Value:
                - "1"
              Unit: mg/ml
              Inclusive: true
  - Name: gluc 10%
    Form: vloeistof
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.4"
          Unit: kCal/ml
      - Name: koolhydraat
        Concentrations:
          Value:
            - "0.1"
          Unit: g/ml
"""


    /// Breast milk with a fortifier powder mixed in, fed as a discontinuous order.
    let feedingWithPowder =
        """
Id: 4f6671d8-30f2-4bcc-a9a3-76bb931f15e5
Name: mm met bmf
Route: ORAAL
OrderType: DiscontinuousOrder
Adjust:
  Value:
    - "4"
  Unit: kg
Frequencies:
  Value:
    - "3"
    - "4"
    - "5"
    - "6"
    - "7"
    - "8"
    - "12"
  Unit: x/day
Dose:
  Quantity:
    Max:
      Value:
        - "1000"
      Unit: ml
      Inclusive: true
  QuantityAdjust:
    Max:
      Value:
        - "20"
      Unit: ml/kg
      Inclusive: true
DoseCount:
  Min:
    Value:
      - "1"
    Unit: x
    Inclusive: true
  Max:
    Value:
      - "1"
    Unit: x
    Inclusive: true
Components:
  - Name: MM
    Form: voeding
    Quantities:
      Value:
        - "1"
      Unit: ml
    Divisible: "10"
    Dose:
      DoseUnit: ml
      Quantity:
        Max:
          Value:
            - "1000"
          Unit: ml
          Inclusive: true
      QuantityAdjust:
        Max:
          Value:
            - "20"
          Unit: ml/kg
          Inclusive: true
      PerTime:
        Max:
          Value:
            - "2500"
          Unit: ml/day
          Inclusive: true
      PerTimeAdjust:
        Min:
          Value:
            - "10"
          Unit: ml/kg/day
          Inclusive: true
        Max:
          Value:
            - "150"
          Unit: ml/kg/day
          Inclusive: true
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "0.68"
          Unit: kCal/ml
      - Name: eiwit
        Concentrations:
          Value:
            - "0.01"
          Unit: g/ml
  - Name: Nutrilon Nenatal BMF pdr
    Form: voeding
    Quantities:
      Value:
        - "1"
      Unit: g
    Divisible: "10"
    Dose:
      DoseUnit: g
      PerTimeAdjust:
        Min:
          Value:
            - "0.1"
          Unit: g/kg/day
          Inclusive: true
        Max:
          Value:
            - "0.3"
          Unit: g/kg/day
          Inclusive: true
    Substances:
      - Name: energie
        Concentrations:
          Value:
            - "3.47"
          Unit: kCal/g
      - Name: eiwit
        Concentrations:
          Value:
            - "0.252"
          Unit: x
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
        |> MedicationText.fromString
        |> Result.mapError (fun errs -> $"%A{errs}")
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


/// The medication text as a YAML document: the round trip from the scenario records, the shape of
/// the text, the targets the places give, and the errors of a text that does not read.
module MedicationTextYamlTests =

    /// The medication records of the scenarios.
    let records =
        [
            "pcmSupp", Scenarios.pcmSupp
            "amfo", Scenarios.amfo
            "morfCont", Scenarios.morfCont
            "pcmDrink", Scenarios.pcmDrink
            "cotrim", Scenarios.cotrim
            "tpnComplete", Scenarios.tpnComplete
            "tpn", Scenarios.tpn
            "fullMedication", Scenarios.fullMedication
            "morfContWithNaCl", Scenarios.morfContWithNaCl
            "morfContWithNaClOneStrength", Scenarios.morfContWithNaClOneStrength
            "amfoWithNaClTwoStrengthsTimed", Scenarios.amfoWithNaClTwoStrengthsTimed
            "amfoTimed", Scenarios.amfoTimed
            "morfTimedWithNaCl", Scenarios.morfTimedWithNaCl
        ]


    /// What the old text cannot hold: an exclusive minimum and maximum, the value 1/3, a value
    /// list with a repeat in an unsorted order, an adjust unit on a dose limit.
    let losslessMedication =
        let mg = Units.Mass.milliGram
        let kg = Units.Weight.kiloGram
        let day = Units.Time.day
        let mgPerKgPerDay = mg |> ValueUnit.per kg |> ValueUnit.per day

        { Scenarios.pcmSupp with
            Id = "lossless"
            Frequencies =
                [| 3N; 2N; 3N |]
                |> ValueUnit.withUnit (Units.Count.times |> ValueUnit.per day)
                |> Some
            Dose =
                { DoseLimit.limit with
                    DoseLimitTarget = OrderableLimitTarget
                    DoseUnit = mg
                    AdjustUnit = Some kg
                    PerTimeAdjust =
                        {
                            Min = 1N / 3N |> ValueUnit.singleWithUnit mgPerKgPerDay |> Exclusive |> Some
                            Max = 90N |> ValueUnit.singleWithUnit mgPerKgPerDay |> Exclusive |> Some
                        }
                }
                |> Some
        }


    let all = records @ [ "losslessMedication", losslessMedication ]


    /// The medication with every limit targeted as its place gives it: the orderable at
    /// medication level, a component or a substance by its name below it. The text holds no
    /// target, so this is what a medication reads back as.
    let withPlaceTargets (med: Medication) =
        let dose t =
            Option.map (fun (dl: DoseLimit) -> { dl with DoseLimitTarget = t })
        let sol t =
            Option.map (fun (sl: SolutionLimit) -> { sl with SolutionLimitTarget = t })

        { med with
            Dose = med.Dose |> dose OrderableLimitTarget
            Components =
                med.Components
                |> List.map (fun pc ->
                    { pc with
                        Dose = pc.Dose |> dose (ComponentLimitTarget pc.Name)
                        Solution = pc.Solution |> sol (ComponentLimitTarget pc.Name)
                        Substances =
                            pc.Substances
                            |> List.map (fun si ->
                                { si with
                                    Dose = si.Dose |> dose (SubstanceLimitTarget si.Name)
                                    Solution = si.Solution |> sol (SubstanceLimitTarget si.Name)
                                }
                            )
                    }
                )
        }


    /// Every limit target of a medication with its place.
    let targets (med: Medication) =
        [
            match med.Dose with
            | Some dl -> "Dose", dl.DoseLimitTarget
            | None -> ()

            for pc in med.Components do
                match pc.Dose with
                | Some dl -> $"%s{pc.Name}.Dose", dl.DoseLimitTarget
                | None -> ()

                match pc.Solution with
                | Some sl -> $"%s{pc.Name}.Solution", sl.SolutionLimitTarget
                | None -> ()

                for si in pc.Substances do
                    match si.Dose with
                    | Some dl -> $"%s{pc.Name}.%s{si.Name}.Dose", dl.DoseLimitTarget
                    | None -> ()

                    match si.Solution with
                    | Some sl -> $"%s{pc.Name}.%s{si.Name}.Solution", sl.SolutionLimitTarget
                    | None -> ()
        ]


    /// A medication text that holds the fields the error tests change.
    let validText =
        """
Id: "1"
Name: paracetamol
OrderType: DiscontinuousOrder
Components:
  - Name: paracetamol
    Form: zetpil
    Products:
      - Id: "12345"
        Strengths:
          - Substance: paracetamol
            Strength: { Value: [500], Unit: mg/piece }
    Substances:
      - Name: paracetamol
        Concentrations: { Value: [500], Unit: mg/piece }
        Dose:
          QuantityAdjust:
            Max: { Value: [20], Unit: mg/kg, Inclusive: true }
"""


    /// The error test text with a replaced by b; a must be in it.
    let replace a b =
        if validText |> String.contains a then
            validText |> String.replace a b
        else
            invalidArg (nameof a) a


    let errorsOf text =
        match text |> MedicationText.fromString with
        | Ok _ -> []
        | Error errs -> errs


    let fractionRegex = String.regex "(-?\\d+/\\d+)"


    /// The fractions of a text that have a finite decimal form; the writer writes none.
    let fractionsWithDecimalForm text =
        [
            for m in fractionRegex.Matches(text) do
                m.Groups[1].Value
        ]
        |> List.filter (
            BigRational.tryParseExact
            >> Option.bind BigRational.toDecimalString
            >> Option.isSome
        )


    let tests =
        testList
            "Medication text as YAML"
            [
                test "the error test text is read" { validText |> errorsOf |> Expect.isEmpty "no errors" }

                test "OrderType.all lists every case of OrderType" {
                    Order.OrderType.all
                    |> Array.distinct
                    |> Array.length
                    |> Expect.equal
                        "one entry per case"
                        (Microsoft.FSharp.Reflection.FSharpType.GetUnionCases typeof<OrderType>).Length
                }

                testList
                    "round trip"
                    [
                        for n, med in all do
                            test $"%s{n} comes back as the same record" {
                                let exp = med |> withPlaceTargets
                                let text = med |> MedicationText.toString

                                match text |> MedicationText.fromString with
                                | Error errs -> failtest $"%A{errs}\n%s{text}"
                                | Ok act ->
                                    let expText = exp |> MedicationText.toString
                                    let actText = act |> MedicationText.toString

                                    act |> Expect.equal $"expected:\n%s{expText}\nactual:\n%s{actText}" exp
                            }
                    ]

                testList
                    "the text"
                    [
                        for n, med in all do
                            test $"%s{n} holds no fraction with a finite decimal form" {
                                med
                                |> MedicationText.toString
                                |> fractionsWithDecimalForm
                                |> Expect.isEmpty "only fractions without a decimal form are written as one"
                            }

                        test "losslessMedication holds an exclusive limit and 1/3" {
                            let text = losslessMedication |> MedicationText.toString

                            text |> String.contains "Inclusive: false" |> Expect.isTrue text
                            text |> String.contains "1/3" |> Expect.isTrue text
                        }

                        test "an Id of digits and the name yes are quoted, a plain route is not" {
                            let text =
                                { Scenarios.pcmSupp with
                                    Id = "12345"
                                    Name = "yes"
                                }
                                |> MedicationText.toString

                            text |> String.contains "Id: \"12345\"" |> Expect.isTrue text
                            text |> String.contains "Name: \"yes\"" |> Expect.isTrue text
                            text |> String.contains "Route: RECTAAL" |> Expect.isTrue text
                        }
                    ]

                testList
                    "the limit targets"
                    [
                        for n, med in all do
                            test $"every limit of %s{n} read gets the target of its place" {
                                match med |> MedicationText.toString |> MedicationText.fromString with
                                | Error errs -> failtest $"%A{errs}"
                                | Ok act ->
                                    let exp = act |> withPlaceTargets |> targets

                                    act |> targets |> Expect.equal "the targets of the places" exp
                            }
                    ]

                testList
                    "the products"
                    [
                        for n, med in all do
                            test $"every product of %s{n} is made of its substances" {
                                med.Components
                                |> List.collect (fun pc ->
                                    pc.Products
                                    |> List.filter (Medication.ProductComponent.madeOf pc.Substances >> not)
                                    |> List.map (fun p -> $"%s{pc.Name} %s{p.Id}")
                                )
                                |> Expect.isEmpty "every product is made of its substances"
                            }
                    ]

                test "a value of 500/1 in a text reads as 500" {
                    // the key is on one line, as a checkout with CRLF line endings holds \r\n
                    let text = replace "Strength: { Value: [500]" "Strength: { Value: [\"500/1\"]"

                    match text |> MedicationText.fromString with
                    | Error errs -> failtest $"%A{errs}"
                    | Ok med ->
                        med.Components[0].Products[0].Strengths[0].Strength
                        |> ValueUnit.getValue
                        |> Expect.equal "500" [| 500N |]
                }

                testList
                    "errors"
                    [
                        let max = "Components[0].Substances[0].Dose.QuantityAdjust.Max"

                        test "a wrong number names its path" {
                            replace "Value: [20]" "Value: [abc]"
                            |> errorsOf
                            |> Expect.equal "not a number" [ DtoError.NotANumber($"%s{max}.Value[0]", "abc") ]
                        }

                        test "a unit with an unknown group names its path" {
                            replace "Unit: mg/kg," "Unit: \"mg[Banana]/kg\","
                            |> errorsOf
                            |> Expect.equal "unknown unit" [ DtoError.UnknownUnit($"%s{max}.Unit", "mg[Banana]/kg") ]
                        }

                        test "an empty document names the medication" {
                            "" |> errorsOf |> Expect.equal "missing" [ DtoError.Missing "Medication" ]
                        }

                        test "an unknown key is unreadable" {
                            replace "Form: zetpil" "Form: zetpil\n    Colour: white"
                            |> errorsOf
                            |> function
                                | [ DtoError.Unreadable m ] -> m |> Expect.stringContains "names the key" "Colour"
                                | errs -> failtest $"%A{errs}"
                        }

                        test "an unknown key inside a limit is unreadable" {
                            replace "Inclusive: true }" "Inclusive: true, Colour: white }"
                            |> errorsOf
                            |> function
                                | [ DtoError.Unreadable m ] -> m |> Expect.stringContains "names the key" "Colour"
                                | errs -> failtest $"%A{errs}"
                        }

                        test "an order type that names no case names its path" {
                            replace "OrderType: DiscontinuousOrder" "OrderType: Banana"
                            |> errorsOf
                            |> Expect.equal "unknown order type" [ DtoError.UnknownOrderType("OrderType", "Banana") ]
                        }

                        test "an adjusted limit without an adjust unit names its path" {
                            replace "Unit: mg/kg," "Unit: mg,"
                            |> errorsOf
                            |> function
                                | [ DtoError.WrongUnit("Components[0].Substances[0].Dose.QuantityAdjust", _) ] -> ()
                                | errs -> failtest $"%A{errs}"
                        }

                        test "a strength without its value is missing by its path" {
                            replace "Strength: { Value: [500], Unit: mg/piece }" "Strength: ~"
                            |> errorsOf
                            |> Expect.equal
                                "missing"
                                [ DtoError.Missing "Components[0].Products[0].Strengths[0].Strength" ]
                        }

                        test "a component left null is missing by its path" {
                            "OrderType: DiscontinuousOrder\nComponents:\n  - ~\n"
                            |> errorsOf
                            |> Expect.equal "missing" [ DtoError.Missing "Components[0]" ]
                        }

                        test "a product strength without its substance names its path" {
                            replace "- Substance: paracetamol" "- Substance: ibuprofen"
                            |> errorsOf
                            |> Expect.equal "not made of" [ DtoError.NotMadeOf("Components[0].Products[0]", "12345") ]
                        }

                        test "a product with a blank id is missing its id by its path" {
                            replace "- Id: \"12345\"" "- Id: \"\""
                            |> errorsOf
                            |> Expect.equal "missing" [ DtoError.Missing "Components[0].Products[0].Id" ]
                        }

                        test "the errors of one level are all given" {
                            replace "OrderType: DiscontinuousOrder" "OrderType: Banana"
                            |> String.replace "Unit: mg/kg," "Unit: mg,"
                            |> errorsOf
                            |> List.length
                            |> Expect.equal "two errors" 2
                        }
                    ]
            ]
